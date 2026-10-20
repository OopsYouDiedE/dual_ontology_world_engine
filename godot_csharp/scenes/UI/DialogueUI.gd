extends CanvasLayer

signal dialogue_step
signal choice_selected(id: String)
signal dialogue_finished(last_choice: String)

@onready var dialogue_panel: Panel = %DialoguePanel
@onready var speaker_label: RichTextLabel = %SpeakerLabel
@onready var body_label: RichTextLabel = %BodyLabel
@onready var choices_container: VBoxContainer = %ChoicesContainer
@onready var next_prompt: HBoxContainer = %NextPrompt
@onready var next_button: Button = %NextButton
@onready var aside_label: Label = %AsideLabel
@onready var flash_label: Label = %FlashLabel
@onready var fader_rect: ColorRect = %FaderRect

var _dialogue_active := false
var _lines_queue: Array = []
var _lines_index := 0
var _lines_choices: Array = []
var _chosen_choice := ""
var _speakers: Dictionary = {}
var _cast: Dictionary = {}
var _reveal: Tween
var _history: Array[String] = []
var _reduce_motion := false

@onready var portrait: TextureRect = %Portrait
@onready var decor: Control = %NewspaperDecor
@onready var history_panel: Panel = %HistoryPanel
@onready var history_body: RichTextLabel = %HistoryBody

func configure_cast(cast: Dictionary) -> void:
	_cast = cast.duplicate()

func _ready() -> void:
	layer = 20
	decor.draw.connect(_draw_rules)
	dialogue_panel.resized.connect(_layout)
	get_viewport().size_changed.connect(_layout)
	%HistoryButton.pressed.connect(func():
		history_body.text = "\n\n".join(_history)
		history_panel.visible = not history_panel.visible
	)
	%HistoryClose.pressed.connect(func(): history_panel.hide())
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null:
		_reduce_motion = bool(settings.get_value("accessibility", "reduce_motion"))
	_layout.call_deferred()
	next_button.pressed.connect(advance_dialogue)
	dialogue_panel.visible = false
	aside_label.visible = false
	flash_label.visible = false
	fader_rect.visible = false

func _input(event: InputEvent) -> void:
	if not _dialogue_active: return
	if history_panel.visible:
		if event.is_action_pressed("ui_cancel"):
			history_panel.hide()
			get_viewport().set_input_as_handled()
		return
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_SPACE or event.keycode == KEY_ENTER:
			advance_dialogue()
			get_viewport().set_input_as_handled()

func is_dialogue_active() -> bool:
	return _dialogue_active

func start_dialogue(speech: Dictionary) -> void:
	_lines_queue = speech.get("lines", [])
	_lines_choices = speech.get("choices", [])
	_lines_index = 0
	_chosen_choice = ""
	_dialogue_active = true
	history_panel.hide()
	dialogue_panel.visible = true
	_show_current_line()

func _show_current_line() -> void:
	if _lines_index >= _lines_queue.size():
		_finish_dialogue()
		return
	var line: Dictionary = _lines_queue[_lines_index]
	var speaker := str(line.get("speaker", ""))
	var custom_name := str(line.get("speaker_name", ""))
	if custom_name != "":
		_speakers[speaker] = custom_name
	elif not _speakers.has(speaker):
		_speakers[speaker] = speaker
	speaker_label.text = "[b]%s[/b]" % _speakers.get(speaker, speaker)
	body_label.text = str(line.get("text", ""))
	body_label.scroll_to_line(0)
	_history.append("%s\n%s" % [str(_speakers.get(speaker, speaker)), body_label.text])
	portrait.texture = null
	var texture = line.get("portrait")
	if texture is Texture2D:
		portrait.texture = texture
	else:
		var key := str(_cast.get(speaker, ""))
		if key != "":
			var path := "res://ShareAsset/Internal/Characters/DialogueCast/%s_calm.tres" % key
			if ResourceLoader.exists(path): portrait.texture = load(path)
	portrait.visible = portrait.texture != null
	if _reveal != null: _reveal.kill()
	body_label.visible_ratio = 1.0 if _reduce_motion else 0.0
	if not _reduce_motion:
		_reveal = create_tween()
		_reveal.tween_property(body_label, "visible_ratio", 1.0, clampf(body_label.get_total_character_count() * 0.022, 0.15, 1.2))

	# Clear old choices
	for child in choices_container.get_children():
		choices_container.remove_child(child)
		child.queue_free()

	# If last line, show choices
	var is_last: bool = _lines_index == _lines_queue.size() - 1
	if is_last and not _lines_choices.is_empty():
		next_prompt.visible = false
		for opt in _lines_choices:
			var btn := Button.new()
			btn.theme_type_variation = &"DialogueChoice"
			btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
			btn.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			btn.text = str(opt.get("label", ""))
			var cid := str(opt.get("id", ""))
			var available := bool(opt.get("available", true))
			btn.disabled = not available
			if not available:
				var hint := str(opt.get("lock_hint", ""))
				if hint != "": btn.tooltip_text = hint
			btn.pressed.connect(func():
				_chosen_choice = cid
				choice_selected.emit(cid)
				_lines_index += 1
				dialogue_step.emit()
				_finish_dialogue()
			)
			choices_container.add_child(btn)
	else:
		next_prompt.visible = true
	_layout.call_deferred()

func advance_dialogue() -> void:
	if not _dialogue_active: return
	if history_panel.visible: return
	if _reveal != null and _reveal.is_running():
		_reveal.kill()
		body_label.visible_ratio = 1.0
		return
	var is_last: bool = _lines_index == _lines_queue.size() - 1
	if is_last and not _lines_choices.is_empty():
		return # Must choose an option
	_lines_index += 1
	if _lines_index < _lines_queue.size():
		_show_current_line()
		dialogue_step.emit()
	else:
		_finish_dialogue()

func _finish_dialogue() -> void:
	if _reveal != null: _reveal.kill()
	_dialogue_active = false
	history_panel.hide()
	dialogue_panel.visible = false
	dialogue_finished.emit(_chosen_choice)

func _layout() -> void:
	if not is_node_ready(): return
	var screen := get_viewport().get_visible_rect().size
	var extra := minf(choices_container.get_combined_minimum_size().y, screen.y * 0.32) if not _lines_choices.is_empty() and _lines_index == _lines_queue.size() - 1 else 0.0
	dialogue_panel.offset_top = -200.0 - extra
	portrait.offset_top = -96.0
	portrait.offset_bottom = -8.0
	decor.queue_redraw()

func _draw_rules() -> void:
	var s := decor.size
	var ink := dialogue_panel.get_theme_color("rule", "Paper")
	decor.draw_rect(Rect2(Vector2(6, 6), s - Vector2(12, 12)), ink, false, 1.0)
	decor.draw_line(Vector2(28, 49), Vector2(s.x - 28, 49), ink * Color(1, 1, 1, 0.45), 1.0)
	decor.draw_line(Vector2(28, 46), Vector2(minf(170, s.x - 28), 46), ink, 1.0)
	for point in [Vector2(10, 10), Vector2(s.x - 10, 10), Vector2(10, s.y - 10), s - Vector2(10, 10)]:
		var d := Vector2(1 if point.x < s.x / 2 else -1, 1 if point.y < s.y / 2 else -1)
		decor.draw_line(point, point + Vector2(13 * d.x, 0), ink, 1.0)
		decor.draw_line(point, point + Vector2(0, 13 * d.y), ink, 1.0)
		decor.draw_arc(point + d * 8, 4, 0, TAU * 0.78, 12, ink, 1.0, true)

func show_aside(text: String, duration := 2.5) -> void:
	aside_label.text = text
	aside_label.visible = true
	var tween := create_tween()
	aside_label.modulate.a = 1.0
	tween.tween_interval(duration)
	tween.tween_property(aside_label, "modulate:a", 0.0, 0.5)
	tween.tween_callback(func(): aside_label.visible = false)

func show_flash(text: String, duration := 1.8) -> void:
	flash_label.text = text
	flash_label.visible = true
	var tween := create_tween()
	flash_label.modulate.a = 1.0
	tween.tween_interval(duration)
	tween.tween_property(flash_label, "modulate:a", 0.0, 0.4)
	tween.tween_callback(func(): flash_label.visible = false)

func get_fader() -> ColorRect:
	return fader_rect
