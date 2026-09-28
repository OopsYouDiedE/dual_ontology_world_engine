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
var speaker: Label
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
	"工程师": -1.5,
	"音乐家": -3.0,
	"铜口": -2.6,
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
		if is_instance_valid(next_button): next_button.text = tr("FA_UI_148" if compact else "FA_UI_149")
		if is_instance_valid(history_button): history_button.text = tr("FA_UI_067")
		if is_instance_valid(history_close_button): history_close_button.text = tr("FA_UI_161")
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
		history[index] = character + "\n" + refreshed.text

func _localized_speaker(canonical_name: String) -> String:
	var ids := {"侦探":"FA_NAME_001", "工程师":"FA_NAME_002", "厨师":"FA_NAME_003",
		"音乐家":"FA_NAME_004", "铜口":"FA_NAME_005", "侦探（心声）":"FA_NAME_006"}
	return tr(str(ids[canonical_name])) if ids.has(canonical_name) else canonical_name

func apply_locale_font(font: Font, _theme: Theme) -> void:
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
	speaker = label("", Vector2(36, 50), 22, PAPER)
	body = RichTextLabel.new()
	body.position = Vector2(206, 48)
	body.size = Vector2(1030, 84)
	body.bbcode_enabled = false
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
	history_text.position = Vector2(24, 24)
	history_text.size = Vector2(656, 378)
	history_panel.add_child(history_text)
	history_close_button = Button.new()
	history_close_button.text = tr("FA_UI_161")
	history_close_button.position = Vector2(470, 411)
	history_close_button.pressed.connect(toggle_history)
	history_panel.add_child(history_close_button)
	history_panel.hide()
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
	_setup_voice_player()
	idle()

func _setup_voice_player() -> void:
	if not is_instance_valid(voice_player):
		voice_player = get_node_or_null("VoicePlayer") as AudioStreamPlayer
		if not is_instance_valid(voice_player):
			voice_player = AudioStreamPlayer.new()
			voice_player.name = "VoicePlayer"
			voice_player.bus = &"Master"
			voice_player.volume_db = -6.0
			add_child(voice_player)

func use_compact_layout() -> void:
	compact = true
	heading.hide()
	hint.hide()
	panel.get_node("SpeakerDivider").hide()
	panel.position.x = 64
	panel.size.x = 1152
	var background := StyleBoxFlat.new()
	background.bg_color = Color("182723", 0.97)
	background.set_corner_radius_all(8)
	panel.add_theme_stylebox_override("panel", background)
	speaker.position = Vector2(32, 24)
	speaker.add_theme_font_size_override("font_size", 18)
	speaker.add_theme_color_override("font_color", Color("c4b798"))
	body.position = Vector2(32, 62)
	body.size = Vector2(1088, 110)
	body.add_theme_font_size_override("normal_font_size", 23)
	choices.position = Vector2(32, 174)
	next_button.text = tr("FA_UI_148")
	next_button.position.x = 1024
	next_button.size.x = 96
	for state in ["normal", "hover", "pressed", "focus"]:
		var style := StyleBoxFlat.new()
		style.bg_color = Color("31433b") if state in ["hover", "pressed"] else Color(0,0,0,0)
		style.set_content_margin_all(12)
		style.set_corner_radius_all(4)
		if state == "focus":
			style.border_color = GOLD
			style.border_width_bottom = 1
		panel.theme.set_stylebox(state, "Button", style)
	history_button.position = Vector2(1040, 14)

func layout_compact_line() -> void:
	if not compact: return
	var count := 0
	for response in line.responses:
		if response.is_allowed: count += 1
	var height := 224 + int(ceil(count / 2.0)) * 54 if count > 0 else 224
	panel.size.y = height
	panel.position.y = 992 - 32 - height
	next_button.position.y = height - 56

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
	elif char_name == "工程师":
		return VOICE_ENGINEER
	elif char_name == "厨师" or char_name == "主厨":
		return VOICE_CHEF
	elif char_name == "音乐家":
		return VOICE_MUSICIAN
	elif char_name == "铜口":
		return VOICE_TUBE
	return VOICE_DETECTIVE

func show_line(key: String) -> void:
	busy = true
	next_button.hide()
	for child in choices.get_children():
		choices.remove_child(child)
		child.queue_free()
	line = await manager.get_next_dialogue_line(resource, key, [context])
	busy = false
	if line == null:
		active = false
		finished.emit()
		return
	speaker.text = _localized_speaker(line.character) if not line.character.is_empty() else ""
	body.text = line.text
	body.visible_characters = 0
	elapsed = 0
	current_voice_stream = _get_character_voice(line.character)
	last_voice_char_index = -1
	history.append(speaker.text + "\n" + line.text)
	history_line_ids.append(line.id)
	line_shown.emit(line.character)
	hint.text = tr("FA_UI_143")
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
	choices.hide()

func choose(key: String) -> void:
	if busy or not active or history_panel.visible:
		return
	await show_line(key)

func _on_choice_pressed(button: Button) -> void:
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

func _process(delta: float) -> void:
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

func toggle_history() -> void:
	history_panel.visible = not history_panel.visible
	history_text.text = tr("FA_UI_081") + "\n\n" + "\n\n".join(history)

func on_panel_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		advance()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if active and event.keycode == KEY_H:
			toggle_history()
			get_viewport().set_input_as_handled()
		elif active and event.keycode in [KEY_SPACE, KEY_ENTER]:
			if event.keycode == KEY_ENTER and choices.visible and choices.get_child_count() > 0:
				var focused := get_viewport().gui_get_focus_owner()
				var choice: Button
				if focused is Button and choices.is_ancestor_of(focused):
					choice = focused
				else:
					choice = choices.get_child(0) as Button
				choose(str(choice.get_meta("dialogue_next_id")))
			else:
				advance()
			get_viewport().set_input_as_handled()
	elif active and event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT and panel.get_global_rect().has_point(event.position):
		advance()
		get_viewport().set_input_as_handled()
