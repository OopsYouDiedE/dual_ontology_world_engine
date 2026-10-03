extends CanvasLayer
## Shared Dialogue Manager presentation; story content stays in .dialogue resources.
signal finished
signal line_shown(character: String)

const INK := Color("191e23")
const PAPER := Color("e8dfca")
const GOLD := Color("b59a68")

const VOICE_DETECTIVE: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_detective.wav")
const VOICE_ENGINEER: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_engineer.wav")
const VOICE_CHEF: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_chef.wav")
const VOICE_MUSICIAN: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_musician.wav")
const VOICE_TUBE: AudioStream = preload("res://ShareAsset/Internal/Characters/voices/voice_tube.wav")

var manager: Node
var resource: Resource
var context: Node
var line
var busy := false
var active := false
var speaker: RichTextLabel
var body: RichTextLabel
var choices: GridContainer
var next_button: Button
var history_button: Button
var history_close_button: Button
var hint: Label
var heading: Label
var panel: Panel
var history_panel: Panel
var history_text: RichTextLabel
var history: Array[String] = []
var history_line_ids: Array[String] = []
var elapsed := 0.0
var compact := false
var locale_refresh_key := ""
var dialogue_theme: Theme
var character_portraits: Dictionary = {}
var character_portrait: TextureRect
var portrait_frame: PanelContainer
## Full-length portraits are shown as a head-and-shoulders card (fractions of the image).
const PORTRAIT_BUST := Rect2(0.2, 0.02, 0.6, 0.4)
var portrait_character := ""
## Hosts may dim responses the player has already chosen once (opt-in).
var dim_visited_responses := false
var _visited_responses: Dictionary = {}
## Opening dialogue consumes keyboard input before focused GUI buttons.
var single_press_keyboard_continue := false

const BASE_VOICE_VOLUME_DB := -6.0

## ISO 226 / Fletcher-Munson equal-loudness baseline compensation (dB).
## Compensates for human ear frequency sensitivity differences across character voice pitches:
## Chef (~141 Hz, bass): ear sensitivity is lower, boost +2.4 dB.
## Detective (~200 Hz, reference): 0.0 dB.
## Engineer (~315 Hz, tenor): ear sensitivity rises, attenuate -1.5 dB.
## Musician (~489 Hz, treble): near high-sensitivity ear canal resonance, attenuate -3.0 dB.
## Tube (~468 Hz): attenuate -2.6 dB.
const CHARACTER_PITCH_LOUDNESS_COMPENSATION := {
	"侦探": 0.0,
	"侦探（心声）": 0.0,
	"厨师": 2.4,
	"主厨": 2.4,
	"沈鼎": 2.4,
	"工程师": -1.5,
	"霍铎": -1.5,
	"音乐家": -3.0,
	"苏泠": -3.0,
	"铜口": -2.6,
	"学院管理人？": -2.6,
	"？？？": -2.6,
}

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
var last_voice_char_index := -1
var voice_char_interval := 2

func _notification(what: int) -> void:
	if what == NOTIFICATION_TRANSLATION_CHANGED:
		if is_instance_valid(next_button): _refresh_next_caption()
		if is_instance_valid(history_button): history_button.text = tr("FA_UI_067")
		if is_instance_valid(history_close_button): history_close_button.text = tr("FA_UI_161")
		if is_instance_valid(fast_forward_label): fast_forward_label.text = _key_legend()
		if active: _update_continue_hint()
	if what == NOTIFICATION_TRANSLATION_CHANGED and active and not busy and line != null:
		locale_refresh_key = line.id
		call_deferred("_refresh_active_line_locale", locale_refresh_key)
		call_deferred("_refresh_history_locale")

func _refresh_active_line_locale(key: String) -> void:
	if not active or busy or line == null or line.id != key:
		return
	var revealed := body.visible_characters < 0
	var progress := 1.0 if revealed else float(body.visible_characters) / maxf(1.0, float(body.get_total_character_count()))
	busy = true
	var refreshed = await manager.get_next_dialogue_line(resource, key, [context], DMConstants.MutationBehaviour.Skip)
	if not active or line == null or line.id != key or refreshed == null:
		busy = false
		return
	line = refreshed
	busy = false
	speaker.text = _localized_speaker(line.character)
	_refresh_character_portrait()
	body.text = line.text
	if revealed:
		body.visible_characters = -1
	else:
		body.visible_characters = int(progress * body.get_total_character_count())
	history[history.size() - 1] = speaker.text + "\n" + line.text
	var choices_were_visible := choices.visible
	var continue_was_visible := next_button.visible
	for child in choices.get_children():
		choices.remove_child(child)
		child.queue_free()
	for response in line.responses:
		if not response.is_allowed:
			continue
		var button := Button.new()
		button.text = response.text
		button.custom_minimum_size = Vector2(538 if compact else 488, 44 if compact else 40)
		if compact: button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.set_meta("dialogue_next_id", response.next_id)
		button.pressed.connect(_on_choice_pressed.bind(button))
		choices.add_child(button)
	choices.visible = choices_were_visible
	next_button.visible = continue_was_visible

func _refresh_history_locale() -> void:
	for index in history_line_ids.size():
		if index >= history.size():
			return
		var key := history_line_ids[index]
		var refreshed = await manager.get_next_dialogue_line(resource, key, [context], DMConstants.MutationBehaviour.Skip)
		if refreshed == null:
			continue
		var character := _localized_speaker(refreshed.character)
		history[index] = _escape_rich_text(character) + "\n" + refreshed.text

func _localized_speaker(canonical_name: String) -> String:
	if is_instance_valid(context) and context.has_method("_localized_speaker"):
		return context.call("_localized_speaker", canonical_name)
	var ids := {"侦探":"FA_NAME_001", "工程师":"FA_NAME_002", "厨师":"FA_NAME_003",
		"音乐家":"FA_NAME_004", "铜口":"FA_NAME_005", "侦探（心声）":"FA_NAME_006",
		"霍铎":"FA_NAME_002", "沈鼎":"FA_NAME_003", "苏泠":"FA_NAME_004"}
	return tr(str(ids[canonical_name])) if ids.has(canonical_name) else canonical_name

## The last line of a conversation says so, so a quick extra press is not a surprise.
func _refresh_next_caption() -> void:
	if not compact:
		next_button.text = tr("FA_UI_149")
		return
	if not _line_ends_conversation():
		next_button.text = tr("FA_UI_148") + "  ▸"
		return
	var locale := TranslationServer.get_locale()
	next_button.text = ("Close" if locale.begins_with("en") else "閉じる" if locale.begins_with("ja") else "结束") + "  ■"

func _line_ends_conversation() -> bool:
	if line == null or resource == null or not ("lines" in resource) or not line.responses.is_empty(): return false
	var id := str(line.next_id)
	for guard in 8:
		# Runtime ids carry a "<resource>@" prefix that the compiled line table does not.
		if id.contains("@"): id = id.get_slice("@", id.get_slice_count("@") - 1)
		if id.is_empty() or id.begins_with("end"): return true
		var entry: Variant = resource.lines.get(id)
		if not entry is Dictionary or str(entry.get("type", "")) != "goto" or entry.get("is_snippet", false): return false
		id = str(entry.get("next_id", ""))
	return false

func _escape_rich_text(value: String) -> String:
	return value.replace("[", "[lb]").replace("]", "[rb]")

func set_character_portraits(mapping: Dictionary) -> void:
	character_portraits.clear()
	for canonical_name in mapping:
		var character := _portrait_character(str(canonical_name))
		if character.is_empty():
			continue
		var asset: Variant = mapping[canonical_name]
		var texture: Texture2D = null
		if asset is Texture2D:
			texture = asset
		elif asset is String and ResourceLoader.exists(asset):
			texture = load(asset) as Texture2D
		if texture != null:
			character_portraits[character] = _bust(texture)
	if not is_instance_valid(panel):
		return
	if not is_instance_valid(character_portrait):
		# A framed card sitting on the dialogue box's top edge, so the face
		# belongs to the box instead of floating over the map.
		portrait_frame = PanelContainer.new()
		portrait_frame.name = "PortraitFrame"
		portrait_frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var card := StyleBoxFlat.new()
		card.bg_color = Color("1b2b23")
		card.border_color = Color("b49a5e")
		card.set_border_width_all(2)
		card.set_corner_radius_all(3)
		card.set_content_margin_all(3)
		card.shadow_color = Color(0, 0, 0, 0.35)
		card.shadow_size = 6
		portrait_frame.add_theme_stylebox_override("panel", card)
		portrait_frame.size = Vector2(178, 178)
		portrait_frame.position = Vector2(28, -186)
		panel.add_child(portrait_frame)
		character_portrait = TextureRect.new()
		character_portrait.name = "CharacterPortrait"
		character_portrait.mouse_filter = Control.MOUSE_FILTER_IGNORE
		character_portrait.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		character_portrait.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		character_portrait.custom_minimum_size = Vector2(172, 172)
		portrait_frame.add_child(character_portrait)
	character_portrait.texture = null
	portrait_character = ""
	_refresh_character_portrait()
	layout_compact_line()

func _bust(texture: Texture2D) -> Texture2D:
	var size := texture.get_size()
	var crop := AtlasTexture.new()
	crop.atlas = texture
	crop.region = Rect2(size * PORTRAIT_BUST.position, size * PORTRAIT_BUST.size)
	return crop

func _portrait_character(canonical_name: String) -> String:
	match canonical_name:
		"侦探", "侦探（心声）", "侦探心声":
			return "侦探"
		"工程师", "霍铎":
			return "工程师"
		"厨师", "主厨", "沈鼎":
			return "厨师"
		"音乐家", "苏泠":
			return "音乐家"
	return ""

func _refresh_character_portrait() -> void:
	if not is_instance_valid(character_portrait):
		return
	var character := _portrait_character(str(line.character)) if active and not busy and line != null else ""
	if character != portrait_character:
		portrait_character = character
		character_portrait.texture = character_portraits.get(character) as Texture2D
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
	var changed := is_instance_valid(portrait_frame) and portrait_frame.visible != shown
	if is_instance_valid(portrait_frame): portrait_frame.visible = shown
	if changed and compact: layout_compact_line()

var portrait_monogram: Label

func _refresh_monogram() -> void:
	if not is_instance_valid(portrait_monogram):
		portrait_monogram = Label.new()
		portrait_monogram.mouse_filter = Control.MOUSE_FILTER_IGNORE
		portrait_monogram.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		portrait_monogram.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		portrait_monogram.add_theme_font_size_override("font_size", 56)
		portrait_monogram.add_theme_color_override("font_color", Color(GOLD, 0.75))
		portrait_frame.add_child(portrait_monogram)
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
	for control in [speaker, body, hint, next_button, history_text]:
		if is_instance_valid(control):
			control.theme = dialogue_theme
	for container in [choices, history_panel]:
		for child in container.get_children():
			if child is Control:
				child.theme = dialogue_theme

func _ready() -> void:
	layer = 10
	manager = get_node("/root/DialogueManager")
	if has_node("DialoguePanel"):
		_bind_existing()
		return
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Microsoft YaHei UI", "Noto Sans CJK SC"])
	dialogue_theme = Theme.new()
	dialogue_theme.default_font = font
	dialogue_theme.default_font_size = 18
	dialogue_theme.set_color("font_color", "Label", PAPER)
	dialogue_theme.set_color("default_color", "RichTextLabel", PAPER)
	for state in ["normal", "hover", "pressed", "focus"]:
		var box := StyleBoxFlat.new()
		box.bg_color = Color("353630") if state == "hover" else INK
		box.border_color = GOLD if state in ["hover", "focus"] else Color("555044")
		box.set_border_width_all(1)
		box.content_margin_left = 16
		box.content_margin_right = 16
		box.content_margin_top = 9
		box.content_margin_bottom = 9
		dialogue_theme.set_stylebox(state, "Button", box)
	dialogue_theme.set_color("font_color", "Button", PAPER)
	panel = Panel.new()
	panel.theme = dialogue_theme
	panel.position = Vector2(0, 768)
	panel.size = Vector2(1280, 224)
	var background := StyleBoxFlat.new()
	background.bg_color = INK
	background.border_color = GOLD
	background.border_width_top = 2
	panel.add_theme_stylebox_override("panel", background)
	panel.gui_input.connect(on_panel_input)
	add_child(panel)
	heading = label("FA_UI_087", Vector2(36, 18), 13, GOLD)
	speaker = RichTextLabel.new()
	speaker.position = Vector2(36, 50)
	speaker.size = Vector2(144, 32)
	speaker.bbcode_enabled = false
	speaker.mouse_filter = Control.MOUSE_FILTER_IGNORE
	speaker.add_theme_font_size_override("normal_font_size", 22)
	speaker.add_theme_color_override("default_color", PAPER)
	panel.add_child(speaker)
	body = RichTextLabel.new()
	body.position = Vector2(206, 48)
	body.size = Vector2(1030, 84)
	body.bbcode_enabled = true
	body.scroll_active = false
	body.mouse_filter = Control.MOUSE_FILTER_IGNORE
	body.add_theme_font_size_override("normal_font_size", 20)
	panel.add_child(body)
	var rule := ColorRect.new()
	rule.name = "SpeakerDivider"
	rule.position = Vector2(180, 52)
	rule.size = Vector2(1, 72)
	rule.color = Color("655943")
	rule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(rule)
	choices = GridContainer.new()
	choices.columns = 2
	choices.position = Vector2(206, 143)
	choices.add_theme_constant_override("h_separation", 12)
	choices.add_theme_constant_override("v_separation", 8)
	panel.add_child(choices)
	next_button = Button.new()
	next_button.text = tr("FA_UI_149")
	next_button.position = Vector2(1116, 151)
	next_button.size = Vector2(128, 40)
	next_button.pressed.connect(advance)
	panel.add_child(next_button)
	hint = label("", Vector2(36, 196), 12, Color("9b998d"))
	history_button = Button.new()
	history_button.text = tr("FA_UI_067")
	history_button.position = Vector2(1160, 10)
	history_button.pressed.connect(toggle_history)
	panel.add_child(history_button)
	history_panel = Panel.new()
	history_panel.theme = dialogue_theme
	history_panel.position = Vector2(288, 152)
	history_panel.size = Vector2(704, 464)
	history_panel.add_theme_stylebox_override("panel", background)
	add_child(history_panel)
	history_text = RichTextLabel.new()
	history_text.bbcode_enabled = true
	history_text.position = Vector2(24, 24)
	history_text.size = Vector2(656, 378)
	history_panel.add_child(history_text)
	history_close_button = Button.new()
	history_close_button.text = tr("FA_UI_161")
	history_close_button.position = Vector2(470, 411)
	history_close_button.pressed.connect(toggle_history)
	history_panel.add_child(history_close_button)
	history_panel.hide()
	_configure_auxiliary_focus()
	_setup_voice_player()
	idle()

func _bind_existing() -> void:
	panel = $DialoguePanel
	history_panel = $HistoryPanel
	dialogue_theme = panel.theme.duplicate()
	panel.theme = dialogue_theme
	history_panel.theme = dialogue_theme
	heading = $DialoguePanel/Heading
	speaker = $DialoguePanel/Speaker
	body = $DialoguePanel/Body
	choices = $DialoguePanel/Choices
	next_button = $DialoguePanel/Next
	hint = $DialoguePanel/Hint
	history_button = $DialoguePanel/HistoryButton
	history_text = $HistoryPanel/HistoryText
	history_close_button = $HistoryPanel/Close
	panel.gui_input.connect(on_panel_input)
	next_button.pressed.connect(advance)
	history_button.pressed.connect(toggle_history)
	history_close_button.pressed.connect(toggle_history)
	_configure_auxiliary_focus()
	_setup_voice_player()
	idle()

func _configure_auxiliary_focus() -> void:
	# Dialogue keys are handled once in _input; mouse utility buttons must not
	# retain native ui_accept focus after the history overlay closes.
	for button in [next_button, history_button, history_close_button]:
		button.focus_mode = Control.FOCUS_NONE

func _setup_voice_player() -> void:
	if not is_instance_valid(voice_player):
		voice_player = get_node_or_null("VoicePlayer") as AudioStreamPlayer
		if not is_instance_valid(voice_player):
			voice_player = AudioStreamPlayer.new()
			voice_player.name = "VoicePlayer"
			voice_player.bus = &"Master"
			voice_player.volume_db = -6.0
			add_child(voice_player)

## Compact box geometry: a floating card above the bottom edge with the portrait inside it.
const COMPACT_WIDTH := 1152.0
const COMPACT_HEIGHT := 172.0
const COMPACT_MARGIN := 20.0
const COMPACT_PORTRAIT := 140.0
const COMPACT_TEXT_X := 32.0

func use_compact_layout() -> void:
	compact = true
	heading.hide()
	hint.hide()
	panel.get_node("SpeakerDivider").hide()
	panel.position.x = (1280.0 - COMPACT_WIDTH) / 2.0
	panel.size.x = COMPACT_WIDTH
	var background := StyleBoxFlat.new()
	background.bg_color = Color("16231f", 0.96)
	background.border_color = Color(GOLD, 0.55)
	background.set_border_width_all(1)
	background.border_width_top = 2
	background.set_corner_radius_all(6)
	background.shadow_color = Color(0, 0, 0, 0.45)
	background.shadow_size = 10
	background.shadow_offset = Vector2(0, 3)
	panel.add_theme_stylebox_override("panel", background)
	speaker.size = Vector2(480, 28)
	speaker.add_theme_font_size_override("normal_font_size", 19)
	speaker.add_theme_color_override("default_color", Color("d9c28c"))
	body.add_theme_font_size_override("normal_font_size", 24)
	body.add_theme_constant_override("line_separation", 6)
	_refresh_next_caption()
	next_button.size = Vector2(116, 40)
	next_button.add_theme_color_override("font_color", Color("d9c28c"))
	for state in ["normal", "hover", "pressed", "focus"]:
		var style := StyleBoxFlat.new()
		style.bg_color = Color("31433b") if state in ["hover", "pressed"] else Color(0,0,0,0)
		style.set_content_margin_all(10)
		style.set_corner_radius_all(4)
		if state == "focus":
			style.border_color = GOLD
			style.border_width_bottom = 1
		panel.theme.set_stylebox(state, "Button", style)
	history_button.position = Vector2(COMPACT_WIDTH - 104, 10)
	history_button.add_theme_color_override("font_color", Color("9fa898"))
	fast_forward_label = label("", Vector2.ZERO, 14, Color("7f8a7c"))
	fast_forward_label.text = _key_legend()
	layout_compact_line()

func layout_compact_line() -> void:
	## Sizes the box to the line's choices and makes room for the speaker portrait.
	if not compact: return
	var count := 0
	if line != null:
		for response in line.responses:
			if response.is_allowed: count += 1
	var height := COMPACT_HEIGHT + (int(ceil(count / 2.0)) * 54 if count > 0 else 0)
	panel.size.y = height
	panel.position.y = 992.0 - COMPACT_MARGIN - height
	var portrait_shown := is_instance_valid(portrait_frame) and portrait_frame.visible
	var text_x := COMPACT_TEXT_X + (COMPACT_PORTRAIT + 8.0 if portrait_shown else 0.0)
	var text_width := COMPACT_WIDTH - text_x - 32.0
	speaker.position = Vector2(text_x, 16)
	body.position = Vector2(text_x, 48)
	body.size = Vector2(text_width - 112.0, 84)
	choices.position = Vector2(text_x, COMPACT_HEIGHT - 16)
	for button in choices.get_children():
		(button as Control).custom_minimum_size.x = (text_width - 12.0) / 2.0
	next_button.position = Vector2(COMPACT_WIDTH - next_button.size.x - 16, height - next_button.size.y - 14)
	if is_instance_valid(fast_forward_label):
		fast_forward_label.position = Vector2(text_x, height - 30)
		fast_forward_label.visible = count == 0
	if is_instance_valid(portrait_frame):
		portrait_frame.position = Vector2(16, 16)
		portrait_frame.size = Vector2.ONE * COMPACT_PORTRAIT
		character_portrait.custom_minimum_size = Vector2.ONE * (COMPACT_PORTRAIT - 6.0)


func label(text: String, at: Vector2, size: int, color: Color) -> Label:
	var result := Label.new()
	result.text = tr(text)
	result.mouse_filter = Control.MOUSE_FILTER_IGNORE
	result.position = at
	result.add_theme_font_size_override("font_size", size)
	result.add_theme_color_override("font_color", color)
	panel.add_child(result)
	return result

func idle(title: String = "", text: String = "") -> void:
	if is_instance_valid(character_portrait):
		character_portrait.hide()
		if is_instance_valid(portrait_frame): portrait_frame.hide()
	speaker.text = tr(title) if not title.is_empty() else ""
	body.text = tr(text) if not text.is_empty() else ""
	body.visible_characters = -1
	next_button.hide()
	hint.text = tr("FA_UI_010") if not text.is_empty() else ""

func start(story: Resource, title: String, state: Node) -> void:
	if active:
		return
	resource = story
	context = state
	active = true
	await show_line(title)

func _get_character_voice(char_name: String) -> AudioStream:
	if char_name == "侦探" or char_name == "侦探（心声）":
		return VOICE_DETECTIVE
	elif char_name == "工程师" or char_name == "霍铎":
		return VOICE_ENGINEER
	elif char_name == "厨师" or char_name == "主厨" or char_name == "沈鼎":
		return VOICE_CHEF
	elif char_name == "音乐家" or char_name == "苏泠":
		return VOICE_MUSICIAN
	elif char_name == "铜口" or char_name == "学院管理人？" or char_name == "？？？":
		return VOICE_TUBE
	return VOICE_DETECTIVE

func show_line(key: String) -> void:
	busy = true
	line = null
	_refresh_character_portrait()
	next_button.hide()
	# A new branch can await actor movement before producing its next line.
	# Do not leave an already-read sentence on screen as if it were awaiting input.
	speaker.text = ""
	body.text = ""
	body.visible_characters = -1
	hint.text = "…"
	hint.visible = not compact
	choices.hide()
	for child in choices.get_children():
		choices.remove_child(child)
		child.queue_free()
	line = await manager.get_next_dialogue_line(resource, key, [context])
	busy = false
	if line == null:
		active = false
		_refresh_character_portrait()
		finished.emit()
		return
	speaker.text = _localized_speaker(line.character) if not line.character.is_empty() else ""
	_refresh_character_portrait()
	body.text = line.text
	body.visible_characters = 0
	elapsed = 0
	current_voice_stream = _get_character_voice(line.character)
	last_voice_char_index = -1
	history.append(_escape_rich_text(speaker.text) + "\n" + line.text)
	history_line_ids.append(line.id)
	line_shown.emit(line.character)
	hint.hide()
	_update_continue_hint()
	for response in line.responses:
		if not response.is_allowed:
			continue
		var button := Button.new()
		button.text = response.text
		button.custom_minimum_size = Vector2(538 if compact else 488, 44 if compact else 40)
		if compact: button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.set_meta("dialogue_next_id", response.next_id)
		button.pressed.connect(_on_choice_pressed.bind(button))
		if dim_visited_responses and _visited_responses.has(str(response.next_id)):
			button.modulate = Color(1, 1, 1, 0.5)
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
	body.visible_characters = -1
	last_voice_char_index = -1
	choices.show()
	next_button.visible = line.responses.is_empty()
	if choices.get_child_count() > 0:
		choices.get_child(0).grab_focus()
	_update_continue_hint()

func _update_continue_hint() -> void:
	# Visible continue and history buttons already communicate these actions.
	hint.hide()

func _process(delta: float) -> void:
	_refresh_character_portrait()
	_fast_forward(delta)
	if active and not busy and not history_panel.visible and body.visible_characters >= 0:
		elapsed += delta
		var loc := TranslationServer.get_locale()
		var speed := get_locale_typing_speed(loc)
		var target_chars: int = int(elapsed * speed)
		if target_chars != body.visible_characters:
			_play_typewriter_voice(body.visible_characters, target_chars)
			body.visible_characters = target_chars
		if body.visible_characters >= body.get_total_character_count():
			reveal()

## Holding Ctrl in the compact box reveals and advances plain lines quickly; choices still stop it.
const FAST_FORWARD_INTERVAL := 0.09
var fast_forward_wait := 0.0
var fast_forward_label: Label

func _key_legend() -> String:
	var locale := TranslationServer.get_locale()
	if locale.begins_with("en"): return "Space continue   H review   hold Ctrl to skip"
	if locale.begins_with("ja"): return "Space 続ける　H 履歴　Ctrl 長押しで早送り"
	return "Space 继续　H 回看　按住 Ctrl 快进"

func _fast_forward(delta: float) -> void:
	if not compact or not active or busy or line == null or history_panel.visible or not Input.is_key_pressed(KEY_CTRL):
		fast_forward_wait = 0.0
		return
	if not line.responses.is_empty() or get_viewport().gui_get_focus_owner() is LineEdit:
		return
	if is_instance_valid(context) and context.has_method("dialogue_has_modal_input") and context.call("dialogue_has_modal_input"):
		return
	fast_forward_wait -= delta
	if fast_forward_wait > 0.0: return
	fast_forward_wait = FAST_FORWARD_INTERVAL
	if body.visible_characters >= 0: reveal()
	else: show_line(line.next_id)

func _play_typewriter_voice(from_count: int, to_count: int) -> void:
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
			var char_comp: float = CHARACTER_PITCH_LOUDNESS_COMPENSATION.get(char_name, 0.0)
			var pitch_comp := calculate_pitch_loudness_compensation(pitch)
			voice_player.volume_db = BASE_VOICE_VOLUME_DB + char_comp + pitch_comp
			voice_player.play()
			break

func clear_history() -> void:
	history.clear()
	history_line_ids.clear()
	if is_instance_valid(history_text):
		history_text.text = ""

func toggle_history() -> void:
	var focused := get_viewport().gui_get_focus_owner()
	if is_instance_valid(focused) and (panel.is_ancestor_of(focused) or history_panel.is_ancestor_of(focused)):
		focused.release_focus()
	history_panel.visible = not history_panel.visible
	history_text.text = tr("FA_UI_081") + "\n\n" + "\n\n".join(history)
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
	if single_press_keyboard_continue:
		_handle_dialogue_key(event)

func _unhandled_input(event: InputEvent) -> void:
	# Other story hosts keep normal GUI response-button keyboard activation.
	if not single_press_keyboard_continue:
		_handle_dialogue_key(event)

func _handle_dialogue_key(event: InputEvent) -> void:
	var editor := get_viewport().gui_get_focus_owner()
	if editor is LineEdit or editor is TextEdit:
		return
	if is_instance_valid(context) and context.has_method("dialogue_has_modal_input") and context.call("dialogue_has_modal_input"):
		return
	if not active or not event is InputEventKey or event.keycode not in [KEY_H, KEY_SPACE, KEY_ENTER, KEY_KP_ENTER]:
		return
	# Consume down, repeat and up before focused Buttons receive ui_accept.
	get_viewport().set_input_as_handled()
	if not event.pressed or event.echo:
		return
	if event.keycode == KEY_H:
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
	if event.keycode in [KEY_ENTER, KEY_KP_ENTER] and choices.visible and choices.get_child_count() > 0:
		var focused := get_viewport().gui_get_focus_owner()
		var choice: Button
		if focused is Button and choices.is_ancestor_of(focused):
			choice = focused
		else:
			choice = choices.get_child(0) as Button
		_visited_responses[str(choice.get_meta("dialogue_next_id"))] = true
		choose(str(choice.get_meta("dialogue_next_id")))
	else:
		advance()
