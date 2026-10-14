@tool
extends CanvasLayer
## Shared semantic packet presentation; chapter logic and content remain in C#.
signal finished
signal line_shown(character: String)

@export_group("Editor Preview")
@export var preview_enabled := true:
	set(value):
		preview_enabled = value
		_refresh_editor_preview()
@export var preview_speaker := "说话人":
	set(value):
		preview_speaker = value
		_refresh_editor_preview()
@export_multiline var preview_body := "我想先看看这张纸上写了什么，再听听你的说法。":
	set(value):
		preview_body = value
		_refresh_editor_preview()
@export var preview_responses := PackedStringArray(["询问来意", "出示线索", "再想一想", "结束交谈"]):
	set(value):
		preview_responses = value
		_refresh_editor_preview()
@export var preview_portrait: Texture2D:
	set(value):
		preview_portrait = value
		_refresh_editor_preview()
@export var preview_show_history := true:
	set(value):
		preview_show_history = value
		_refresh_editor_preview()
@export_multiline var preview_history := "说话人甲\n示例台词。\n\n说话人乙\n另一句示例台词。":
	set(value):
		preview_history = value
		_refresh_editor_preview()
@export_group("Dialogue")
@export var response_scene: PackedScene = preload("res://scenes/UI/Components/DialogueResponse.tscn")
@export var base_voice_volume_db: float = -6.0
@export_range(0.0, 64.0, 1.0) var portrait_text_gap := 8.0
@export_range(0.0, 64.0, 1.0) var response_row_gap := 8.0

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
const StoryStyle := preload("res://scenes/UI/Components/Paper.gd")
const VOICE_DETECTIVE: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_detective.wav")
const VOICE_ENGINEER: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_engineer.wav")
const VOICE_CHEF: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_chef.wav")
const VOICE_MUSICIAN: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_musician.wav")
const VOICE_TUBE: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_tube.wav")

var packet_engine: Node
var packet_serial := 0
var _session_serial := 0

## Presentation adapter only: conditions, choices and mutations stay in C#.
class PacketResponse extends RefCounted:
	var next_id: String
	var text: String
	var is_allowed: bool
	var lock_hint: String
	func _init(data: Dictionary) -> void:
		next_id = str(data["id"])
		text = str(data["label"])
		is_allowed = bool(data.get("is_available", true))
		lock_hint = str(data.get("lock_hint", ""))

class PacketLine extends RefCounted:
	var source: Dictionary
	var id: String
	var next_id := "@packet:next"
	var character: String
	var text: String
	var mood: String
	var voice_clip: String
	var responses: Array = []
	func _init(packet: Dictionary, serial: int) -> void:
		id = "packet:%d" % serial
		var lines: Array = packet.get("lines", [])
		var data: Dictionary = lines[0] if not lines.is_empty() else {}
		source = {"line_id": str(data.get("line_id", "")), "source_id": str(data.get("source_id", "")),
			"trace_id": str(data.get("trace_id", "")), "line_index": int(data.get("line_index", 0))}
		character = str(data.get("speaker", ""))
		text = str(data.get("text", ""))
		mood = str(data.get("mood", "calm"))
		voice_clip = str(data.get("voice_clip", ""))
		for option in packet.get("choices", []):
			responses.append(PacketResponse.new(option))
	func get_tag_value(tag: String) -> String:
		return mood if tag == "emotion" else ""
var context: Node
## 说话人 → 立绘与声线键（来自剧本“对白演员”表，经宿主快照 cast 传入）。
var dialogue_cast: Dictionary = {}
var line
var busy := false
var active := false
var speaker: RichTextLabel
var body: RichTextLabel
var choices: GridContainer
var next_button: Button
var history_button: Button
var history_close_button: Button
var heading: Label
var panel: Panel
var history_panel: Panel
var history_text: RichTextLabel
var history: Array[String] = []
var elapsed := 0.0
var compact := false
var dialogue_theme: Theme
var character_portraits: Dictionary = {}
var character_portrait: TextureRect
var portrait_frame: PanelContainer
## Photographer's imprint on the cabinet-card mount: the current speaker's display name.
var portrait_name: Label
## Full-length portraits are shown as a head-and-shoulders card (fractions of the image).
const PORTRAIT_BUST := Rect2(0.2, 0.02, 0.6, 0.4)
var portrait_character := ""
var portrait_mood := "calm"
const PORTRAIT_MOODS := ["calm", "thoughtful", "worried", "angry", "warm", "hooked"]
## Hosts may dim responses the player has already chosen once (opt-in).
var dim_visited_responses := false
var _visited_responses: Dictionary = {}
## Opening dialogue consumes keyboard input before focused GUI buttons.
var single_press_keyboard_continue := false


## ISO 226 / Fletcher-Munson equal-loudness baseline compensation (dB).
## Compensates for human ear frequency sensitivity differences across character voice pitches:
## (keyed by voice asset) Chef (~141 Hz, bass): ear sensitivity is lower, boost +2.4 dB.
## Detective (~200 Hz, reference): 0.0 dB.
## Engineer (~315 Hz, tenor): ear sensitivity rises, attenuate -1.5 dB.
## Musician (~489 Hz, treble): near high-sensitivity ear canal resonance, attenuate -3.0 dB.
## Tube (~468 Hz): attenuate -2.6 dB.
const VOICE_LOUDNESS_COMPENSATION := {
	"detective": 0.0,
	"chef": 2.4,
	"engineer": -1.5,
	"musician": -3.0,
	"tube": -2.6,
}
## 声线素材按对白演员表（宿主快照 cast）里的立绘键取；没有演员条目的说话人（铜口之声）用 tube。
const VOICES := {"detective": VOICE_DETECTIVE, "engineer": VOICE_ENGINEER, "chef": VOICE_CHEF,
	"musician": VOICE_MUSICIAN, "tube": VOICE_TUBE}

## Estimated typing speed (characters per second) across languages to normalize sentence audio duration.
## The same semantic sentence expands in character count in English (~2.6x) and Japanese (~1.5x) vs Chinese.
## Adjusting characters-per-second according to linguistic density keeps typewriter audio duration balanced.
const LOCALE_TYPING_SPEEDS := {
	"zh": 26.0,
	"zh_cn": 26.0,
	"en": 68.0,
	"ja": 39.0,
}

## Estimated voice chirp interval (characters per chirp) per language.
## Keeps chirp frequency (~13 chirps/sec) and audio rhythm natural across languages.
const LOCALE_VOICE_INTERVALS := {
	"zh": 2,
	"zh_cn": 2,
	"en": 5,
	"ja": 3,
}

## Calculates Fletcher-Munson / ISO 226 equal-loudness gain adjustment (in dB) for a given pitch scale.
## In speech / auditory range (100 Hz - 4000 Hz), perceived loudness increases by ~3.5 dB per octave.
## Higher pitch sounds louder to the human ear; we attenuate gain by -3.5 * log2(pitch) to maintain constant phon.
static func calculate_pitch_loudness_compensation(pitch: float) -> float:
	var clamped := clampf(pitch, 0.25, 4.0)
	return -3.5 * (log(clamped) / log(2.0))

static func get_locale_typing_speed(locale: String) -> float:
	var norm := locale.to_lower()
	if LOCALE_TYPING_SPEEDS.has(norm):
		return float(LOCALE_TYPING_SPEEDS[norm])
	for key in LOCALE_TYPING_SPEEDS:
		if norm.begins_with(key):
			return float(LOCALE_TYPING_SPEEDS[key])
	return 32.0

static func get_locale_voice_interval(locale: String) -> int:
	var norm := locale.to_lower()
	if LOCALE_VOICE_INTERVALS.has(norm):
		return int(LOCALE_VOICE_INTERVALS[norm])
	for key in LOCALE_VOICE_INTERVALS:
		if norm.begins_with(key):
			return int(LOCALE_VOICE_INTERVALS[key])
	return 2

## Calculates estimated dialogue audio duration (in seconds) for a given text and locale.
static func estimate_dialogue_audio_duration(text: String, locale: String = "") -> float:
	var loc := locale if not locale.is_empty() else TranslationServer.get_locale()
	var speed := get_locale_typing_speed(loc)
	return float(text.length()) / maxf(1.0, speed)

var voice_player: AudioStreamPlayer
var current_voice_stream: AudioStream = null
## 铜口开口时先响一声盖片与管道共鸣；同一段连说不重复。
var _previous_voice_stream: AudioStream = null
var last_voice_char_index := -1
var voice_char_interval := 2

func _notification(what: int) -> void:
	if Engine.is_editor_hint(): return
	if what == NOTIFICATION_TRANSLATION_CHANGED:
		if is_instance_valid(next_button): _refresh_next_caption()
		if is_instance_valid(history_button): history_button.text = _caption("回看", "History", "履歴")
		if is_instance_valid(history_close_button): history_close_button.text = _caption("关闭", "Close", "閉じる")

func _localized_speaker(canonical_name: String) -> String:
	if is_instance_valid(context) and context.has_method("_localized_speaker"):
		return context.call("_localized_speaker", canonical_name)
	return canonical_name

func _caption(zh: String, en: String, ja: String) -> String:
	var locale := TranslationServer.get_locale()
	return en if locale.begins_with("en") else ja if locale.begins_with("ja") else zh

func _refresh_next_caption() -> void:
	next_button.text = _caption("继续", "Continue", "続ける") + "  ▸" if compact else _caption("下一句", "Next", "次へ")

func _escape_rich_text(value: String) -> String:
	return value.replace("[", "[lb]").replace("]", "[rb]")

## Character labels map to existing cast keys; each sheet supplies six explicit moods.
func set_dialogue_cast(mapping: Dictionary) -> void:
	dialogue_cast = mapping.duplicate()
	var portraits := {}
	for character in mapping:
		var moods := {}
		for mood in PORTRAIT_MOODS:
			moods[mood] = "res://ShareAsset/Internal/Characters/DialogueCast/%s_%s.tres" % [str(mapping[character]), mood]
		portraits[character] = moods
	set_character_portraits(portraits)

func set_character_portraits(mapping: Dictionary) -> void:
	character_portraits.clear()
	for canonical_name in mapping:
		var character := _portrait_character(str(canonical_name))
		if character.is_empty():
			continue
		var asset: Variant = mapping[canonical_name]
		var moods: Dictionary = asset if asset is Dictionary else {"calm": asset}
		var textures := {}
		for mood in PORTRAIT_MOODS:
			var value: Variant = moods.get(mood)
			var texture: Texture2D = null
			if value is Texture2D:
				texture = value
			elif value is String and ResourceLoader.exists(value):
				texture = load(value) as Texture2D
			if texture != null:
				textures[mood] = texture if texture.get_meta("portrait_bust", false) else _bust(texture)
		character_portraits[character] = textures
	if not is_instance_valid(panel):
		return
	character_portrait.texture = null
	portrait_character = ""
	portrait_mood = "calm"
	_refresh_character_portrait()
	layout_compact_line()

func _bust(texture: Texture2D) -> Texture2D:
	var size := texture.get_size()
	var crop := AtlasTexture.new()
	crop.atlas = texture
	crop.region = Rect2(size * PORTRAIT_BUST.position, size * PORTRAIT_BUST.size)
	return crop

func _portrait_character(canonical_name: String) -> String:
	# 说话人直接按宿主提供的演员表查找立绘；不在表里的（旁白、铜口之声）没有立绘。
	return canonical_name

func _refresh_character_portrait() -> void:
	if not is_instance_valid(character_portrait):
		return
	var character := _portrait_character(str(line.character)) if active and not busy and line != null else ""
	var mood: String = str(line.get_tag_value("emotion")) if active and not busy and line != null else "calm"
	if mood not in PORTRAIT_MOODS:
		mood = "calm"
	if character != portrait_character or mood != portrait_mood:
		portrait_character = character
		portrait_mood = mood
		var textures: Dictionary = character_portraits.get(character, {})
		character_portrait.texture = textures.get(mood, textures.get("calm")) as Texture2D
	var shown := active and not busy and line != null and character_portrait.texture != null and panel.visible and not history_panel.visible
	if compact:
		# The card keeps one layout for every speaker: a portrait, or a monogram for
		# narration and voices without a face. It stays put between lines and under the log.
		var line_ready := active and not busy and line != null
		shown = active and panel.visible
		character_portrait.visible = line_ready and character_portrait.texture != null
		if line_ready: _refresh_monogram()
	else:
		character_portrait.visible = shown
	_refresh_portrait_name()
	var changed := is_instance_valid(portrait_frame) and portrait_frame.visible != shown
	if is_instance_valid(portrait_frame):
		portrait_frame.visible = shown
		# 旁白没有说话人时肖像卡隐去但保留位置，正文不随之移动。
		var narration := compact and active and not busy and line != null and character_portrait.texture == null and str(speaker.text).strip_edges().is_empty()
		portrait_frame.modulate.a = 0.0 if narration else 1.0
	if changed and compact: layout_compact_line()

var portrait_monogram: Label

func _refresh_portrait_name() -> void:
	if not is_instance_valid(portrait_name): return
	# Blank (not hidden) for narration so the mount keeps one size for every speaker.
	portrait_name.text = str(speaker.text).strip_edges() if active and not busy and line != null else ""

func _refresh_monogram() -> void:
	var name_text := str(speaker.text).strip_edges()
	portrait_monogram.text = name_text.left(1) if not name_text.is_empty() and not name_text.begins_with("？") else "·"
	portrait_monogram.visible = not character_portrait.visible

## Text already shaped with the previous font still holds its RIDs; freeing that font
## mid-frame makes the text server report `Parameter "fd" is null`. Keep it alive.
var _retired_fonts: Array[Font] = []

func apply_locale_font(font: Font, _theme: Theme) -> void:
	if dialogue_theme.default_font != null and dialogue_theme.default_font != font and not _retired_fonts.has(dialogue_theme.default_font):
		_retired_fonts.append(dialogue_theme.default_font)
	dialogue_theme.default_font = font
	dialogue_theme.default_font_size = 18
	panel.theme = dialogue_theme
	history_panel.theme = dialogue_theme
	for control in [speaker, body, next_button, history_text]:
		if is_instance_valid(control):
			control.theme = dialogue_theme
	for container in [choices, history_panel]:
		for child in container.get_children():
			if child is Control:
				child.theme = dialogue_theme

var _base_panel_rect := Rect2()
var _base_text_offsets: Dictionary = {}
var _base_speaker_right := 0.0
var _base_panel_bottom_margin := 0.0

func _ready() -> void:
	if not has_node("DialoguePanel"):
		push_error("StoryDialogue requires the ChapterDialogue.tscn node hierarchy")
		return
	_bind_existing()
	get_viewport().size_changed.connect(layout_compact_line)
	history_button.text = _caption("回看", "History", "履歴")
	history_close_button.text = _caption("关闭", "Close", "閉じる")
	if Engine.is_editor_hint():
		_refresh_editor_preview()
		return
	panel.gui_input.connect(on_panel_input)
	next_button.pressed.connect(advance)
	history_button.pressed.connect(toggle_history)
	history_close_button.pressed.connect(toggle_history)
	_setup_voice_player()
	_clear_responses()
	idle()

func _bind_existing() -> void:
	panel = $DialoguePanel
	history_panel = $HistoryPanel
	# Runtime locale changes need a private Theme; native local overrides remain untouched.
	var authored_theme := panel.theme
	dialogue_theme = authored_theme.duplicate() if authored_theme != null else Theme.new()
	if not Engine.is_editor_hint():
		panel.theme = dialogue_theme
		if history_panel.theme == authored_theme:
			history_panel.theme = dialogue_theme
	heading = $DialoguePanel/Heading
	speaker = $DialoguePanel/Speaker
	body = $DialoguePanel/Body
	# Long lines remain readable within the authored box; short lines keep its layout.
	body.scroll_active = true
	body.scroll_following = false
	# Receive wheel scrolling while ordinary clicks still bubble to the panel.
	body.mouse_filter = Control.MOUSE_FILTER_PASS
	choices = $DialoguePanel/Choices
	next_button = $DialoguePanel/Next
	history_button = $DialoguePanel/HistoryButton
	history_text = $HistoryPanel/HistoryText
	history_close_button = $HistoryPanel/Close
	portrait_frame = $DialoguePanel/PortraitFrame
	character_portrait = $DialoguePanel/PortraitFrame/Column/Stage/CharacterPortrait
	portrait_monogram = $DialoguePanel/PortraitFrame/Column/Stage/Monogram
	portrait_name = $DialoguePanel/PortraitFrame/Column/PortraitName
	_base_panel_rect = Rect2(panel.position, panel.size)
	_base_panel_bottom_margin = get_viewport().get_visible_rect().size.y - _base_panel_rect.end.y
	for node in [speaker, body, choices]:
		_base_text_offsets[node] = node.offset_left
	_base_speaker_right = speaker.offset_right

func _response_button(text: String) -> Button:
	var button := response_scene.instantiate() as Button
	button.text = text
	return button

func _clear_responses() -> void:
	if not is_instance_valid(choices): return
	for child in choices.get_children():
		choices.remove_child(child)
		child.queue_free()

func _refresh_editor_preview() -> void:
	if not Engine.is_editor_hint() or not is_instance_valid(panel):
		return
	panel.visible = preview_enabled
	history_panel.visible = preview_enabled and preview_show_history
	if not preview_enabled:
		return
	speaker.text = preview_speaker
	body.text = preview_body
	body.visible_characters = -1
	character_portrait.texture = preview_portrait
	character_portrait.visible = preview_portrait != null
	portrait_frame.show()
	portrait_monogram.text = preview_speaker.left(1)
	portrait_name.text = preview_speaker
	portrait_monogram.visible = preview_portrait == null
	history_text.text = preview_history
	# Authored sample nodes stay in the scene tree. Extra previews have no owner and are not saved.
	for child in choices.get_children():
		if child.owner == null:
			choices.remove_child(child)
			child.queue_free()
	var samples := choices.get_children()
	for index in samples.size():
		var button := samples[index] as Button
		button.visible = index < preview_responses.size()
		if button.visible: button.text = preview_responses[index]
	for index in range(samples.size(), preview_responses.size()):
		choices.add_child(_response_button(preview_responses[index]))
	choices.visible = not preview_responses.is_empty()
	next_button.visible = preview_responses.is_empty()
	compact = true
	layout_compact_line()

func _setup_voice_player() -> void:
	voice_player = $VoicePlayer
	# 对白语音走 Voice 总线（音量由 GameSettings 管理），没有这条总线时留在原总线。
	if AudioServer.get_bus_index(&"Voice") != -1: voice_player.bus = &"Voice"

func use_compact_layout() -> void:
	compact = true
	_refresh_next_caption()
	layout_compact_line()

func layout_compact_line() -> void:
	if not compact or not is_instance_valid(panel): return
	var count := preview_responses.size() if Engine.is_editor_hint() else choices.get_child_count()
	var sample: Control = choices.get_child(0) as Control if count > 0 else null
	var row_height := sample.custom_minimum_size.y if sample != null else 44.0
	var height := _base_panel_rect.size.y + ceilf(float(count) / choices.columns) * (row_height + response_row_gap)
	panel.size.y = height
	panel.position.y = get_viewport().get_visible_rect().size.y - _base_panel_bottom_margin - height
	var portrait_space := portrait_frame.size.x + portrait_text_gap if portrait_frame.visible else 0.0
	for node in _base_text_offsets:
		node.offset_left = float(_base_text_offsets[node]) + portrait_space
	speaker.offset_right = _base_speaker_right + portrait_space

func idle(title: String = "", text: String = "") -> void:
	if is_instance_valid(character_portrait):
		character_portrait.hide()
		if is_instance_valid(portrait_frame): portrait_frame.hide()
	speaker.text = tr(title) if not title.is_empty() else ""
	if is_instance_valid(portrait_name): portrait_name.text = ""
	body.text = tr(text) if not text.is_empty() else ""
	body.visible_characters = -1
	body.get_v_scroll_bar().value = 0.0
	next_button.hide()

func cancel_presentation() -> void:
	_session_serial += 1
	var was_active := active
	active = false
	busy = false
	line = null
	packet_engine = null
	context = null
	if is_instance_valid(voice_player):
		voice_player.stop()
		voice_player.stream = null
	current_voice_stream = null
	_previous_voice_stream = null
	_clear_responses()
	if is_instance_valid(history_panel): history_panel.hide()
	if is_instance_valid(panel): panel.hide()
	if is_instance_valid(next_button): next_button.hide()
	if was_active: finished.emit()

func _exit_tree() -> void:
	cancel_presentation()

func start_packet(source: Node, title: String, state: Node) -> void:
	if Engine.is_editor_hint() or active or title == "" or title == "<null>": return
	packet_engine = source
	context = state
	_session_serial += 1
	active = true
	busy = true
	panel.show()
	history_panel.hide()
	# Defer presentation so the host can subscribe without retaining a frame-await coroutine.
	call_deferred("_start_packet_ready", source, title, _session_serial)

func _start_packet_ready(source: Node, title: String, serial: int) -> void:
	if not active or serial != _session_serial or not is_instance_valid(source): return
	var packet = source.StartDialogue(title)
	if packet == null:
		active = false
		busy = false
		finished.emit()
		return
	await _show_packet(packet)

func _show_packet(packet: Dictionary) -> void:
	var serial := _session_serial
	busy = true
	next_button.hide()
	choices.hide()
	while not packet.get("commands", []).is_empty():
		speaker.text = ""
		body.text = ""
		for command in packet["commands"]:
			await context.present_dialogue_command(str(command["kind"]), command.get("args", []))
			if not active or serial != _session_serial: return
		packet = packet_engine.AdvanceDialogue()
	busy = false
	if packet.get("finished", false):
		line = null
		active = false
		_refresh_character_portrait()
		finished.emit()
		return
	packet_serial += 1
	line = PacketLine.new(packet, packet_serial)
	_present_line()

func _voice_key(char_name: String) -> String:
	if char_name.is_empty():
		return "detective"
	if not dialogue_cast.has(char_name):
		return "tube"
	var key := str(dialogue_cast[char_name])
	return key if VOICES.has(key) else "detective"

func _get_character_voice(char_name: String) -> AudioStream:
	return VOICES[_voice_key(char_name)]

func show_line(key: String) -> void:
	if Engine.is_editor_hint() or busy or not active or not is_instance_valid(packet_engine): return
	await _show_packet(packet_engine.AdvanceDialogue() if key == "@packet:next" else packet_engine.Choose(key))

func _present_line() -> void:
	_clear_responses()
	speaker.text = _localized_speaker(line.character) if not line.character.is_empty() else ""
	_refresh_character_portrait()
	body.text = StoryStyle.story_text(line.text)
	body.set_meta("player_source", line.source)
	body.visible_characters = 0
	body.get_v_scroll_bar().value = 0.0
	elapsed = 0
	_auto_wait = 0.0
	current_voice_stream = _get_character_voice(line.character)
	if not line.voice_clip.is_empty():
		current_voice_stream = VOICES.get(line.voice_clip.trim_prefix("voice_"), current_voice_stream)
	# STORY-HARDCODE[CSH-022][logic][production]: 铜口之声每段首次开口触发盖片与管道共鸣音。
	if current_voice_stream == VOICE_TUBE and _previous_voice_stream != VOICE_TUBE:
		Sound.sfx("tube", -6.0)
	_previous_voice_stream = current_voice_stream
	last_voice_char_index = -1
	history.append(_escape_rich_text(speaker.text) + "\n" + body.text)
	line_shown.emit(line.character)
	for response in line.responses:
		# Conditional story topics remain hidden until learned. Explicit locks carry a hint.
		if not response.is_allowed and response.lock_hint.is_empty():
			continue
		var button := _response_button(response.text)
		button.disabled = not response.is_allowed
		button.tooltip_text = response.lock_hint
		button.set_meta("dialogue_next_id", response.next_id)
		button.pressed.connect(_on_choice_pressed.bind(button))
		if dim_visited_responses and _visited_responses.has(str(response.next_id)):
			button.theme_type_variation = &"ChapterDialogueVisitedResponse"
		choices.add_child(button)
	choices.hide()
	layout_compact_line()
	_refresh_next_caption()

func choose(key: String) -> void:
	if busy or not active or history_panel.visible:
		return
	await show_line(key)

func _on_choice_pressed(button: Button) -> void:
	_visited_responses[str(button.get_meta("dialogue_next_id"))] = true
	choose(str(button.get_meta("dialogue_next_id")))

func advance() -> void:
	if busy or not active or history_panel.visible:
		return
	if choices.visible and choices.get_child_count() > 0:
		return
	if body.visible_characters >= 0:
		reveal()
	elif line.responses.is_empty():
		await show_line(line.next_id)

func reveal() -> void:
	if not active or line == null: return
	body.visible_characters = -1
	last_voice_char_index = -1
	choices.show()
	next_button.visible = line.responses.is_empty()
	for button in choices.get_children():
		if not button.disabled:
			button.grab_focus()
			break

func _process(delta: float) -> void:
	if Engine.is_editor_hint():
		return
	_refresh_character_portrait()
	_fast_forward(delta)
	if active and not busy and not history_panel.visible and body.visible_characters >= 0:
		var scale := float(_setting("accessibility", "text_speed", 1.0))
		if scale <= 0.0:
			# 文字速度“立即”：整句直接显示。
			reveal()
			_auto_advance(delta)
			return
		elapsed += delta
		var loc := TranslationServer.get_locale()
		var speed := get_locale_typing_speed(loc) * scale
		var target_chars: int = int(elapsed * speed)
		if target_chars != body.visible_characters:
			_play_typewriter_voice(body.visible_characters, target_chars)
			body.visible_characters = target_chars
		if body.visible_characters >= body.get_total_character_count():
			reveal()
	_auto_advance(delta)

## 辅助功能“自动前进”：整句显示完、没有回应选项时，等待设定秒数后自动翻到下一句。
var _auto_wait := 0.0

func _auto_advance(delta: float) -> void:
	if not active or busy or line == null or history_panel.visible or body.visible_characters >= 0 \
			or not bool(_setting("accessibility", "auto_advance", false)) or not line.responses.is_empty():
		_auto_wait = 0.0
		return
	if get_viewport().gui_get_focus_owner() is LineEdit:
		return
	if is_instance_valid(context) and context.has_method("dialogue_has_modal_input") and context.call("dialogue_has_modal_input"):
		return
	_auto_wait += delta
	if _auto_wait < float(_setting("accessibility", "auto_advance_delay", 2.0)): return
	_auto_wait = 0.0
	show_line(line.next_id)

## Holding Ctrl in the compact box reveals and advances plain lines quickly; choices still stop it.
const FAST_FORWARD_INTERVAL := 0.09
var fast_forward_wait := 0.0

## 玩家设置（自动加载 GameSettings）；无头检查脚本里它缺席，读取一律回退到默认值。
var _settings_hub: Node

func _setting(section: String, key: String, fallback: Variant) -> Variant:
	if not is_instance_valid(_settings_hub) and is_inside_tree():
		_settings_hub = get_tree().root.get_node_or_null("GameSettings")
		if _settings_hub != null and not _settings_hub.setting_changed.is_connected(_on_setting_changed):
			_settings_hub.setting_changed.connect(_on_setting_changed)
	if _settings_hub == null: return fallback
	var value: Variant = _settings_hub.get_value(section, key)
	return fallback if value == null else value

func _on_setting_changed(section: String, key: String, _value: Variant) -> void:
	if section == "controls" and key == "hold_fast_forward":
		_fast_forward_latched = false

## 快进键为“切换”模式（GameSettings controls.hold_fast_forward = false）时，按一下开、再按一下关。
var _fast_forward_latched := false

func _fast_forward_held() -> bool:
	if bool(_setting("controls", "hold_fast_forward", true)):
		return Input.is_action_pressed("fast_forward")
	return _fast_forward_latched

func _fast_forward(delta: float) -> void:
	if not active: _fast_forward_latched = false
	if not compact or not active or busy or line == null or history_panel.visible or not _fast_forward_held():
		fast_forward_wait = 0.0
		return
	if not line.responses.is_empty():
		# 回应选项处停下；切换模式下顺手关掉，选完后不会自己接着快进。
		_fast_forward_latched = false
		return
	if get_viewport().gui_get_focus_owner() is LineEdit:
		return
	if is_instance_valid(context) and context.has_method("dialogue_has_modal_input") and context.call("dialogue_has_modal_input"):
		return
	fast_forward_wait -= delta
	if fast_forward_wait > 0.0: return
	fast_forward_wait = FAST_FORWARD_INTERVAL
	if body.visible_characters >= 0: reveal()
	else: show_line(line.next_id)

func _play_typewriter_voice(from_count: int, to_count: int) -> void:
	if Engine.is_editor_hint(): return
	if current_voice_stream == null or not is_instance_valid(voice_player):
		return
	var text: String = body.text
	var total_len: int = text.length()
	var loc := TranslationServer.get_locale()
	var interval := voice_char_interval if voice_char_interval != 2 else get_locale_voice_interval(loc)
	for i in range(maxi(0, from_count), mini(to_count, total_len)):
		var c: String = text.substr(i, 1)
		if c in [" ", "\t", "\n", "　", "，", "。", "！", "？", "…", "、", "—", ".", "!", "?", ",", ":", "："]:
			continue
		if last_voice_char_index == -1 or (i - last_voice_char_index) >= interval:
			last_voice_char_index = i
			voice_player.stream = current_voice_stream
			var pitch := randf_range(0.95, 1.05)
			voice_player.pitch_scale = pitch
			var char_name: String = str(line.character) if line != null else ""
			var char_comp: float = VOICE_LOUDNESS_COMPENSATION.get(_voice_key(char_name), 0.0)
			var pitch_comp := calculate_pitch_loudness_compensation(pitch)
			voice_player.volume_db = base_voice_volume_db + char_comp + pitch_comp
			voice_player.play()
			break

func clear_history() -> void:
	history.clear()
	if is_instance_valid(history_text):
		history_text.text = ""

func toggle_history() -> void:
	var focused := get_viewport().gui_get_focus_owner()
	if is_instance_valid(focused) and (panel.is_ancestor_of(focused) or history_panel.is_ancestor_of(focused)):
		focused.release_focus()
	history_panel.visible = not history_panel.visible
	history_text.text = _caption("对话记录", "Dialogue history", "会話履歴") + "\n\n" + "\n\n".join(history)
	if history_panel.visible:
		call_deferred("_scroll_history_to_latest")

func _scroll_history_to_latest() -> void:
	if history_panel.visible:
		history_text.scroll_to_line(maxi(0, history_text.get_line_count() - 1))

func on_panel_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		advance()
		panel.accept_event()

func _input(event: InputEvent) -> void:
	_toggle_fast_forward(event)
	if single_press_keyboard_continue:
		_handle_dialogue_key(event)

func _toggle_fast_forward(event: InputEvent) -> void:
	if Engine.is_editor_hint() or not active or bool(_setting("controls", "hold_fast_forward", true)): return
	if not event.is_action_pressed("fast_forward"): return
	var editor := get_viewport().gui_get_focus_owner()
	if editor is LineEdit or editor is TextEdit: return
	_fast_forward_latched = not _fast_forward_latched
	fast_forward_wait = 0.0

func _unhandled_input(event: InputEvent) -> void:
	# Other story hosts keep normal GUI response-button keyboard activation.
	if not single_press_keyboard_continue:
		_handle_dialogue_key(event)

func _handle_dialogue_key(event: InputEvent) -> void:
	if Engine.is_editor_hint(): return
	var editor := get_viewport().gui_get_focus_owner()
	if editor is LineEdit or editor is TextEdit:
		return
	if is_instance_valid(context) and context.has_method("dialogue_has_modal_input") and context.call("dialogue_has_modal_input"):
		return
	if not active or not (event is InputEventKey or event is InputEventMouseButton):
		return
	var is_history := event.is_action("dialogue_history")
	var is_choose := event.is_action("dialogue_choose")
	if not (is_history or is_choose or event.is_action("dialogue_continue")):
		return
	# Consume down, repeat and up before focused Buttons receive ui_accept.
	get_viewport().set_input_as_handled()
	if not event.is_pressed() or event.is_echo():
		return
	if is_history:
		toggle_history()
		return
	if busy or history_panel.visible:
		return
	if single_press_keyboard_continue and body.visible_characters >= 0:
		if line.responses.is_empty():
			await show_line(line.next_id)
		else:
			reveal()
		return
	if is_choose and choices.visible and choices.get_child_count() > 0:
		var focused := get_viewport().gui_get_focus_owner()
		var choice: Button
		if focused is Button and choices.is_ancestor_of(focused):
			choice = focused
		else:
			for button in choices.get_children():
				if button is Button and not button.disabled:
					choice = button
					break
		if choice == null or choice.disabled: return
		_visited_responses[str(choice.get_meta("dialogue_next_id"))] = true
		choose(str(choice.get_meta("dialogue_next_id")))
	else:
		advance()
