@tool
extends CanvasLayer

signal dialogue_step
signal choice_selected(id: String)
signal dialogue_finished(last_choice: String)
signal presentation_completed(presentation_id: int, result: Dictionary)
signal history_closed
signal line_read(line: Dictionary)
signal live_advance_requested(line: Dictionary)
signal live_choice_requested(id: String, line: Dictionary)
signal inspection_closed(record: Dictionary)
signal evidence_presented(item_id: String, context: Dictionary)

const Transition := preload("res://scenes/UIpanel/CasebookUI/UITransition.gd")
const UIInput := preload("res://scenes/UIpanel/UIInput.gd")
const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")

@onready var dialogue_panel: Panel = %DialoguePanel
@onready var speaker_label: RichTextLabel = %SpeakerLabel
@onready var body_label: RichTextLabel = %BodyLabel
@onready var choices_container: VBoxContainer = %ChoicesContainer
@onready var next_prompt: HBoxContainer = %NextPrompt
@onready var next_button: Button = %NextButton
@onready var aside_label: Label = %AsideLabel
@onready var flash_label: Label = %FlashLabel
@onready var fader_rect: ColorRect = %FaderRect
@onready var portrait: TextureRect = %Portrait
@onready var decor: Control = %NewspaperDecor
@onready var history_panel: Panel = %HistoryPanel
@onready var history_body: RichTextLabel = %HistoryBody
@onready var history_button: Button = %HistoryButton
@onready var history_close_button: Button = %HistoryClose
@onready var content: Control = $DialoguePanel/Margin

var _live_mode := false
var _live_kind := "dialogue"
var _live_record: Dictionary = {}
var _live_signature := ""
var _live_can_advance := true
var _evidence_items: Array = []
var _dialogue_active := false
var _finishing := false
var _line_changing := false
var _modal_blocked := false
var _shutting_down := false
var _lines_queue: Array = []
var _lines_index := 0
var _lines_choices: Array = []
var _chosen_choice := ""
var _cast: Dictionary = {}
var _text_speed := 1.0
var _archived_line_serial := -1
var _reveal: Tween
var _portrait_tween: Tween
var _history: Array[String] = []
var _history_focus: WeakRef
var _reduce_motion := false
var _screen_flash := true
var _presentation_id := 0
var _line_serial := 0
var _completed_id := -1
var _completed_result: Dictionary = {}
var _panel_motion = Transition.new()
var _line_motion = Transition.new()
var _history_motion = Transition.new()
var _portrait_cache: Dictionary = {}
var _portrait_requests: Dictionary = {}
var _notice_tweens: Dictionary = {}
var _notice_deadlines: Dictionary = {}
var _notice_serials: Dictionary = {}
var _navigation = UIInput.new()

func _ready() -> void:
	layer = 20
	process_mode = Node.PROCESS_MODE_ALWAYS
	_apply_field_style()
	%EvidenceButton.pressed.connect(_toggle_evidence)
	%EvidenceButton.hide()
	%EvidenceDrawer.hide()
	body_label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	speaker_label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	decor.draw.connect(_draw_rules)
	dialogue_panel.resized.connect(_layout)
	if Engine.is_editor_hint():
		dialogue_panel.show()
		speaker_label.text = "苏泠"
		body_label.text = "走廊尽头还有灯。我们去看看吧。"
		_layout.call_deferred()
		return
	get_viewport().size_changed.connect(_layout)
	_panel_motion.configure(self, dialogue_panel, dialogue_panel, null, false, 0.985)
	_line_motion.configure(self, content, content, null, true, 1.0)
	_history_motion.configure(self, history_panel, history_panel, %HistoryBlocker)
	_panel_motion.completed.connect(_on_panel_transition)
	_line_motion.completed.connect(_on_line_transition)
	_history_motion.completed.connect(_on_history_transition)
	history_button.pressed.connect(toggle_history)
	history_close_button.pressed.connect(close_history)
	next_button.pressed.connect(advance_dialogue)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null:
		_text_speed = float(settings.get_value("accessibility", "text_speed"))
		_reduce_motion = bool(settings.get_value("accessibility", "reduce_motion"))
		_screen_flash = bool(settings.get_value("accessibility", "screen_flash"))
		settings.setting_changed.connect(_on_setting_changed)
	aside_label.hide()
	flash_label.hide()
	fader_rect.hide()
	_refresh_controls()
	_layout.call_deferred()

func configure_cast(cast: Dictionary) -> void:
	_cast = cast.duplicate()
	# Request portraits before a line needs them. Only completed threaded loads
	# are retrieved on the main thread; a late image cannot stall text or movement.
	for key in _cast.values():
		var path := "res://assets/characters/DialogueCast/%s_calm.tres" % str(key)
		_request_portrait(path)

## Opening waits for only the portraits already requested by configure_cast.
## Optional missing/failed images retain the existing text-only fallback.
func wait_for_cast_prepared() -> bool:
	var requested: Array = _portrait_requests.keys()
	while not _shutting_down and is_inside_tree():
		var waiting := false
		for value in requested:
			var path := str(value)
			if _portrait_cache.has(path): continue
			if not bool(_portrait_requests.get(path, false)):
				_portrait_cache[path] = null
				continue
			var status := ResourceLoader.load_threaded_get_status(path)
			if status == ResourceLoader.THREAD_LOAD_LOADED:
				_portrait_cache[path] = ResourceLoader.load_threaded_get(path) as Texture2D
			elif status == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
				waiting = true
			else:
				_portrait_cache[path] = null
				_portrait_requests[path] = false
		if not waiting: return true
		await get_tree().process_frame
	return false

func _request_portrait(path: String) -> void:
	if _portrait_cache.has(path) or _portrait_requests.has(path): return
	if not ResourceLoader.exists(path):
		_portrait_cache[path] = null
		return
	var error := ResourceLoader.load_threaded_request(path, "Texture2D")
	_portrait_requests[path] = error == OK
	if error != OK: push_warning("Portrait request failed: " + path)

func _await_portrait(path: String, line_serial: int) -> void:
	while not _shutting_down and _dialogue_active and line_serial == _line_serial:
		var status := ResourceLoader.load_threaded_get_status(path)
		if status == ResourceLoader.THREAD_LOAD_LOADED:
			var texture := ResourceLoader.load_threaded_get(path) as Texture2D
			_portrait_cache[path] = texture
			_assign_portrait(texture, line_serial)
			return
		if status != ResourceLoader.THREAD_LOAD_IN_PROGRESS: return
		await get_tree().process_frame

func _assign_portrait(texture: Texture2D, line_serial: int) -> void:
	if _shutting_down or not _dialogue_active or line_serial != _line_serial: return
	if _portrait_tween != null: _portrait_tween.kill()
	portrait.texture = texture
	portrait.visible = texture != null
	if texture == null: return
	portrait.modulate.a = 1.0 if _reduce_motion else 0.0
	if not _reduce_motion:
		_portrait_tween = Transition.real_tween(self)
		_portrait_tween.tween_property(portrait, "modulate:a", 1.0, 0.14)

func is_dialogue_active() -> bool:
	return _dialogue_active

func is_history_active() -> bool:
	return _history_motion.is_active()

func play_dialogue(speech: Dictionary) -> Dictionary:
	var id := start_dialogue(speech)
	while not _shutting_down and _dialogue_active and id == _presentation_id:
		await presentation_completed
	if _completed_id == id: return _completed_result.duplicate()
	return {"choice": "", "cancelled": true}

func start_dialogue(speech: Dictionary) -> int:
	if _shutting_down: return -1
	_live_mode = false
	_live_signature = ""
	_live_record.clear()
	%EvidenceDrawer.hide()
	if _dialogue_active: _complete_dialogue(true)
	_presentation_id += 1
	_lines_queue = speech.get("lines", [])
	_lines_choices = speech.get("choices", [])
	_lines_index = 0
	_chosen_choice = ""
	_dialogue_active = true
	_finishing = false
	_line_changing = false
	_history_motion.request(false, false)
	_line_motion.request(true, false)
	if _lines_queue.is_empty():
		# Even an empty presentation finishes after its caller can await it.
		_finish_empty.bind(_presentation_id).call_deferred()
	else:
		_render_current_line()
		_panel_motion.request(true, true, 0.16)
	_refresh_controls()
	return _presentation_id

func _finish_empty(presentation_id: int) -> void:
	if presentation_id == _presentation_id: _finish_dialogue()

func _complete_if_current(presentation_id: int, cancelled: bool) -> void:
	if presentation_id == _presentation_id: _complete_dialogue(cancelled)

func _render_current_line() -> void:
	_line_serial += 1
	_stop_reveal()
	var line: Dictionary = _lines_queue[_lines_index]
	var speaker := str(line.get("speaker", ""))
	var is_known: bool = bool(line.get("known", line.get("is_known", true)))
	var custom_name := str(line.get("speaker_name", ""))
	var display_name := ""
	if not is_known:
		display_name = str(line.get("unknown_title", line.get("unknown_name", "？？？")))
	elif not custom_name.is_empty():
		display_name = custom_name
	elif not speaker.is_empty() and _cast.has(speaker):
		display_name = str(_cast[speaker])
		
	speaker_label.text = "[b]%s[/b]" % display_name if not display_name.is_empty() else ""
	body_label.text = str(line.get("text", ""))
	%SourceLabel.text = str(line.get("source_label", ""))
	%SourceLabel.visible = not %SourceLabel.text.is_empty()
	body_label.scroll_to_line(0)
	body_label.visible_ratio = 1.0 if _reduce_motion or _text_speed <= 0.0 else 0.0
	_history.append("%s\n%s" % [display_name, body_label.text])
	_assign_portrait(null, _line_serial)
	var texture: Variant = line.get("portrait")
	if texture is Texture2D:
		_assign_portrait(texture, _line_serial)
	else:
		var key := str(_cast.get(speaker, ""))
		if key != "":
			var path := "res://assets/characters/DialogueCast/%s_calm.tres" % key
			_request_portrait(path)
			if _portrait_cache.has(path):
				_assign_portrait(_portrait_cache[path] as Texture2D, _line_serial)
			elif bool(_portrait_requests.get(path, false)):
				_await_portrait.bind(path, _line_serial).call_deferred()
	for child in choices_container.get_children():
		choices_container.remove_child(child)
		child.queue_free()
	var is_last := _lines_index == _lines_queue.size() - 1
	next_prompt.visible = not (is_last and not _lines_choices.is_empty())
	$DialoguePanel/Margin/VBox/ChoicesScroll.visible = not next_prompt.visible
	next_button.disabled = false
	if not next_prompt.visible:
		for opt: Dictionary in _lines_choices:
			var button := Button.new()
			Chrome.button(button)
			button.alignment = HORIZONTAL_ALIGNMENT_LEFT
			button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			button.text = str(opt.get("label", ""))
			button.disabled = not bool(opt.get("available", true))
			if button.disabled: button.tooltip_text = str(opt.get("lock_hint", ""))
			button.pressed.connect(_choose.bind(str(opt.get("id", "")), _line_serial))
			choices_container.add_child(button)
	_layout.call_deferred()

func _begin_reveal() -> void:
	if not _dialogue_active or _finishing: return
	_stop_reveal()
	if _reduce_motion or _text_speed <= 0.0:
		body_label.visible_ratio = 1.0
		_record_visible_line.call_deferred()
		return
	if body_label.visible_ratio >= 1.0:
		_record_visible_line.call_deferred()
		return
	_reveal = Transition.real_tween(self)
	_reveal.tween_property(body_label, "visible_ratio", 1.0, clampf(body_label.get_total_character_count() * 0.022, 0.15, 1.2) / maxf(_text_speed, 0.01) * (1.0 - body_label.visible_ratio))
	_reveal.finished.connect(_record_visible_line)
	if _modal_blocked or is_history_active(): _reveal.pause()

func _can_advance() -> bool:
	return _dialogue_active and not _finishing and not _line_changing \
		and not _panel_motion.is_transitioning() and not _modal_blocked and not is_history_active()

func _finish_reveal() -> bool:
	if body_label.visible_ratio >= 1.0: return false
	_stop_reveal()
	body_label.visible_ratio = 1.0
	_record_visible_line.call_deferred()
	return true

func _record_visible_line() -> void:
	if not _dialogue_active or _finishing or _shutting_down or _modal_blocked: return
	if _lines_index < 0 or _lines_index >= _lines_queue.size() or body_label.visible_ratio < 1.0: return
	if _archived_line_serial == _line_serial: return
	_archived_line_serial = _line_serial
	line_read.emit(_lines_queue[_lines_index])

func advance_dialogue() -> void:
	if not _can_advance() or _finishing: return
	if _finish_reveal(): return
	_record_visible_line()
	if _live_mode:
		if not _live_can_advance: return
		if _live_kind == "inspection":
			var record := _live_record.duplicate(true)
			close_live_line()
			inspection_closed.emit(record)
		else:
			live_advance_requested.emit(_live_record.duplicate(true))
		return
	var is_last := _lines_index == _lines_queue.size() - 1
	if is_last and not _lines_choices.is_empty(): return
	next_button.disabled = true
	_lines_index += 1
	if _lines_index >= _lines_queue.size():
		_finish_dialogue()
		return
	_line_changing = true
	# Invalidate the departing line's image job before its fade starts.
	_line_serial += 1
	_refresh_controls()
	if _portrait_tween != null: _portrait_tween.kill()
	if portrait.visible and not _reduce_motion:
		_portrait_tween = Transition.real_tween(self)
		_portrait_tween.tween_property(portrait, "modulate:a", 0.0, 0.06)
	_line_motion.request(false, true, 0.06)

func _choose(id: String, line_serial: int) -> void:
	if line_serial != _line_serial or not _can_advance() or _finishing: return
	if _finish_reveal(): return
	if _live_mode:
		_record_visible_line()
		live_choice_requested.emit(id, _live_record.duplicate(true))
		return
	# Lock all choice buttons immediately to prevent repeated duplicate clicks
	for child in choices_container.get_children():
		if child is BaseButton: child.disabled = true
	_record_visible_line()
	_chosen_choice = id
	_finish_dialogue()
	choice_selected.emit(id)
	dialogue_step.emit()

func _finish_dialogue() -> void:
	if not _dialogue_active or _finishing: return
	_finishing = true
	_line_serial += 1
	_stop_reveal()
	if _portrait_tween != null: _portrait_tween.kill()
	close_history()
	_refresh_controls()
	if _panel_motion.is_active():
		_panel_motion.request(false, true, 0.12)
	else:
		_complete_if_current.bind(_presentation_id, false).call_deferred()

func _complete_dialogue(cancelled: bool) -> void:
	if not _dialogue_active: return
	_dialogue_active = false
	_finishing = false
	_line_changing = false
	_line_serial += 1
	_stop_reveal()
	if _portrait_tween != null: _portrait_tween.kill()
	_panel_motion.request(false, false)
	_history_motion.request(false, false)
	_completed_id = _presentation_id
	_completed_result = {"choice": _chosen_choice, "cancelled": cancelled}
	_refresh_controls()
	presentation_completed.emit(_completed_id, _completed_result)
	dialogue_finished.emit(_chosen_choice)

func _on_panel_transition(_request_id: int, shown: bool, cancelled: bool) -> void:
	if cancelled or _shutting_down: return
	if shown:
		_begin_reveal()
		_refresh_controls()
		restore_focus()
	elif _finishing:
		_complete_dialogue(false)

func _on_line_transition(_request_id: int, shown: bool, cancelled: bool) -> void:
	if cancelled or _shutting_down or not _dialogue_active or _finishing: return
	if not shown and _line_changing:
		_render_current_line()
		dialogue_step.emit()
		_line_motion.request(true, true, 0.10)
	elif shown and _line_changing:
		_line_changing = false
		_begin_reveal()
		_refresh_controls()
		restore_focus()

func set_modal_blocked(blocked: bool) -> void:
	if _modal_blocked == blocked: return
	_modal_blocked = blocked
	_navigation.reset()
	_refresh_controls()
	_update_reveal_pause()

func _update_reveal_pause() -> void:
	if _reveal == null or not _reveal.is_valid(): return
	if _modal_blocked or is_history_active(): _reveal.pause()
	else: _reveal.play()

func toggle_history() -> void:
	if is_history_active():
		close_history()
		return
	if not _can_advance(): return
	_navigation.reset()
	var focused := get_viewport().gui_get_focus_owner()
	_history_focus = weakref(focused) if focused != null else null
	history_body.text = "\n\n".join(_history)
	history_body.scroll_to_line(maxi(0, history_body.get_line_count() - 1))
	_history_motion.request(true)
	_refresh_controls()
	_update_reveal_pause()

func close_history() -> void:
	if not is_history_active(): return
	_navigation.reset()
	_history_motion.request(false)
	_refresh_controls()

func _on_history_transition(_request_id: int, shown: bool, cancelled: bool) -> void:
	if cancelled or _shutting_down: return
	_refresh_controls()
	_update_reveal_pause()
	if shown:
		if not _modal_blocked: history_close_button.grab_focus()
	else:
		if not _modal_blocked and _history_focus != null:
			var focused := _history_focus.get_ref() as Control
			if Transition.can_focus(focused): focused.grab_focus()
		_history_focus = null
		history_closed.emit()

func restore_focus() -> void:
	if not _can_advance(): return
	var focused := get_viewport().gui_get_focus_owner()
	if Transition.can_focus(focused) and dialogue_panel.is_ancestor_of(focused): return
	if next_prompt.visible:
		next_button.grab_focus()
	else:
		for child in choices_container.get_children():
			if child is Control and Transition.can_focus(child):
				child.grab_focus()
				return
		history_button.grab_focus()

func _refresh_controls() -> void:
	Transition.set_interactive(dialogue_panel, _can_advance())
	Transition.set_interactive(history_panel, is_history_active() and not _modal_blocked and not _history_motion.is_transitioning())

func _input(event: InputEvent) -> void:
	if Engine.is_editor_hint():
		return
	if not _dialogue_active or _modal_blocked: return
	if is_history_active():
		if UIInput.is_back(event, false):
			if UIInput.is_back(event): close_history()
			get_viewport().set_input_as_handled()
		elif _history_motion.is_transitioning():
			get_viewport().set_input_as_handled()
		elif event.is_action_pressed("dialogue_history") and not event.is_echo():
			close_history()
			get_viewport().set_input_as_handled()
		elif _navigation.handle(history_panel, event):
			get_viewport().set_input_as_handled()
		return
	if UIInput.is_back(event, false):
		if %EvidenceDrawer.visible:
			if UIInput.is_back(event):
				%EvidenceDrawer.hide()
				_layout()
			get_viewport().set_input_as_handled()
			return
		if _live_mode and _live_kind == "inspection" and UIInput.is_back(event):
			var record := _live_record.duplicate(true)
			close_live_line()
			inspection_closed.emit(record)
		# Back never silently skips an unread line or chooses an offered answer.
		get_viewport().set_input_as_handled()
		return
	if not _can_advance():
		if event.is_action("ui_accept") or event.is_action("dialogue_continue") or UIInput.is_gamepad(event):
			get_viewport().set_input_as_handled()
		return
	if _navigation.handle(dialogue_panel, event):
		get_viewport().set_input_as_handled()
		return
	if not event.is_pressed() or event.is_echo(): return
	if event.is_action_pressed("dialogue_history"):
		toggle_history()
		get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("dialogue_continue") or event.is_action_pressed("ui_accept"):
		var focused := get_viewport().gui_get_focus_owner()
		# Let focused choice/history/next buttons receive their normal GUI action.
		if _can_advance() and focused is BaseButton and dialogue_panel.is_ancestor_of(focused) and event.is_action_pressed("ui_accept"): return
		advance_dialogue()
		get_viewport().set_input_as_handled()

func _process(delta: float) -> void:
	if Engine.is_editor_hint():
		return
	if _modal_blocked or not _dialogue_active or _shutting_down:
		_navigation.reset()
	elif is_history_active() and not _history_motion.is_transitioning():
		_navigation.tick(history_panel, delta)
	elif _can_advance():
		_navigation.tick(dialogue_panel, delta)
	else:
		_navigation.reset()

func _notification(what: int) -> void:
	if Engine.is_editor_hint():
		return
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT: _navigation.reset()

func _layout() -> void:
	if not is_node_ready(): return
	var screen := get_viewport().get_visible_rect().size
	var ui_scale := Chrome.ui_scale(self)
	var font_scale := Chrome.font_scale(self)
	var choices_height := minf(choices_container.get_combined_minimum_size().y, screen.y * 0.28) if not _lines_choices.is_empty() and _lines_index == _lines_queue.size() - 1 else 0.0
	var width := minf(860 * ui_scale, maxf(440, screen.x * 0.68 * sqrt(font_scale)))
	width = minf(width, maxf(240, screen.x - 40))
	var body_height := maxf(62, 62 * font_scale)
	var row_height := maxf(40, 30 * font_scale)
	var source_height := Chrome.scaled_font_size(self, 12) * 1.5 if %SourceLabel.visible else 0.0
	var drawer_height := maxf(56, 44 * font_scale) if %EvidenceDrawer.visible else 0.0
	body_label.custom_minimum_size.y = body_height
	$DialoguePanel/Margin/VBox/Header.custom_minimum_size.y = row_height
	$DialoguePanel/Margin/VBox/ChoicesScroll.custom_minimum_size.y = choices_height
	%EvidenceDrawer.custom_minimum_size.y = drawer_height
	var panel_height := maxf(184 * ui_scale, body_height + row_height + (row_height if next_prompt.visible else 0.0) + choices_height + drawer_height + source_height + 42)
	dialogue_panel.anchor_left = 0.5
	dialogue_panel.anchor_right = 0.5
	dialogue_panel.offset_left = -width * 0.5
	dialogue_panel.offset_right = width * 0.5
	dialogue_panel.offset_top = -24 - minf(panel_height, maxf(180, screen.y - 48))
	dialogue_panel.offset_bottom = -24
	portrait.offset_top = -65
	portrait.offset_left = -152
	portrait.offset_right = -12
	portrait.offset_bottom = -8
	portrait.visible = portrait.texture != null and screen.x >= 840 and _live_kind != "inspection"
	var history_width := minf(860 * ui_scale, maxf(240, screen.x - 40))
	history_panel.anchor_left = 0.5
	history_panel.anchor_right = 0.5
	history_panel.offset_left = -history_width * 0.5
	history_panel.offset_right = history_width * 0.5
	history_close_button.offset_left = -20 - maxf(62, history_close_button.get_combined_minimum_size().x)
	history_close_button.offset_bottom = 8 + maxf(40, history_close_button.get_combined_minimum_size().y)
	history_body.offset_top = history_close_button.offset_bottom + 10
	var notice_width := minf(640 * ui_scale, maxf(200, screen.x - 40))
	aside_label.offset_left = -notice_width * 0.5
	aside_label.offset_right = notice_width * 0.5
	aside_label.offset_bottom = dialogue_panel.offset_top - 16 if _dialogue_active else -100
	aside_label.offset_top = aside_label.offset_bottom - maxf(44, Chrome.scaled_font_size(self, 19) * 2.8)
	decor.queue_redraw()

func _draw_rules() -> void:
	var header: Control = $DialoguePanel/Margin/VBox/Header
	var y := header.get_global_rect().end.y - decor.global_position.y + 1
	decor.draw_line(Vector2(24, y), Vector2(minf(150, decor.size.x - 24), y), Color(Chrome.GOLD, 0.72), 1.0, true)

func _apply_field_style() -> void:
	for control: Control in [dialogue_panel, history_panel, aside_label, flash_label]: Chrome.apply(control, 19)
	dialogue_panel.add_theme_stylebox_override("panel", Chrome.box(Chrome.opacity(dialogue_panel, 0.84), Vector4.ZERO))
	history_panel.add_theme_stylebox_override("panel", Chrome.box(0.97, Vector4.ZERO, true))
	for button: Button in [history_button, history_close_button, next_button, %EvidenceButton]: Chrome.button(button, true)
	speaker_label.add_theme_color_override("default_color", Chrome.GOLD)
	var body_size := Chrome.scaled_font_size(self, 20)
	body_label.add_theme_font_size_override("normal_font_size", body_size)
	body_label.add_theme_font_size_override("bold_font_size", body_size)
	%SourceLabel.add_theme_font_size_override("font_size", Chrome.scaled_font_size(self, 12))
	flash_label.add_theme_font_size_override("font_size", Chrome.scaled_font_size(self, 28))

func show_live_line(line: Dictionary, can_advance := true) -> void:
	if _shutting_down: return
	var normalized := line.duplicate(true)
	normalized.text = str(line.get("text", line.get("Text", "")))
	normalized.speaker_name = str(line.get("speaker_name", line.get("label", line.get("Label", ""))))
	normalized.speaker = str(line.get("speaker", line.get("Speaker", "")))
	var signature := JSON.stringify(normalized)
	_live_can_advance = can_advance
	if _live_mode and _dialogue_active and signature == _live_signature:
		next_button.disabled = not can_advance
		return
	start_dialogue({"lines": [normalized], "choices": line.get("choices", [])})
	_live_mode = true
	_live_kind = "dialogue"
	_live_record = line.duplicate(true)
	_live_signature = signature
	next_button.text = "继续  ›"
	next_button.disabled = not can_advance
	%EvidenceButton.visible = not _evidence_items.is_empty()

func show_inspection(data: Dictionary) -> void:
	var line := data.duplicate(true)
	line.text = str(data.get("text", data.get("body", data.get("description", ""))))
	line.speaker_name = str(data.get("title", data.get("name", "")))
	line.kind = "inspection"
	show_live_line(line)
	_live_kind = "inspection"
	_live_record = data.duplicate(true)
	next_button.text = "收起"
	%EvidenceButton.hide()
	portrait.hide()

func close_live_line() -> void:
	_live_signature = ""
	_live_mode = false
	_live_kind = "dialogue"
	_live_record.clear()
	%EvidenceDrawer.hide()
	%EvidenceButton.hide()
	if _dialogue_active: _complete_dialogue(false)

func configure_evidence(items: Array) -> void:
	_evidence_items = items.duplicate(true)
	%EvidenceButton.visible = not items.is_empty() and _live_kind != "inspection" and _dialogue_active
	if items.is_empty(): %EvidenceDrawer.hide()

func _toggle_evidence() -> void:
	%EvidenceDrawer.visible = not %EvidenceDrawer.visible
	for child in %EvidenceItems.get_children():
		%EvidenceItems.remove_child(child)
		child.queue_free()
	if %EvidenceDrawer.visible:
		for value in _evidence_items:
			if value is not Dictionary: continue
			var item: Dictionary = value
			var button := Button.new()
			button.text = str(item.get("title", item.get("label", item.get("name", ""))))
			Chrome.button(button)
			button.pressed.connect(func():
				%EvidenceDrawer.hide()
				evidence_presented.emit(str(item.get("id", "")), _live_record.duplicate(true))
				_layout()
			)
			%EvidenceItems.add_child(button)
	_layout()

func show_archive_flight(from_position := Vector2.ZERO, destination := Vector2(324, 44)) -> void:
	if _shutting_down: return
	var slip := Control.new()
	slip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	slip.size = Vector2(24, 30)
	add_child(slip)
	slip.position = from_position if from_position != Vector2.ZERO else dialogue_panel.position + Vector2(dialogue_panel.size.x - 36, 30)
	slip.draw.connect(func():
		slip.draw_colored_polygon(PackedVector2Array([Vector2(0, 0), Vector2(17, 0), Vector2(24, 7), Vector2(24, 30), Vector2(0, 30)]), Chrome.INK)
		slip.draw_line(Vector2(17, 0), Vector2(17, 7), Chrome.MUTED, 1)
		slip.draw_line(Vector2(17, 7), Vector2(24, 7), Chrome.MUTED, 1)
		for y in [12, 17, 22]: slip.draw_line(Vector2(5, y), Vector2(18, y), Chrome.SURFACE, 1)
	)
	var motion := create_tween().set_parallel(true)
	motion.tween_property(slip, "position", destination, 0.55).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN_OUT)
	motion.tween_property(slip, "scale", Vector2(0.35, 0.35), 0.55)
	motion.tween_property(slip, "modulate:a", 0.0, 0.2).set_delay(0.35)
	motion.chain().tween_callback(slip.queue_free)

func show_aside(text: String, duration := 2.5) -> void:
	_show_notice("aside", text, duration)

func show_flash(text: String, duration := 1.8) -> void:
	_show_notice("flash", text, duration)

func _show_notice(kind: String, text: String, duration: float) -> void:
	if _shutting_down: return
	var label: Label = aside_label if kind == "aside" else flash_label
	_cancel_notice(kind)
	_notice_serials[kind] = int(_notice_serials.get(kind, 0)) + 1
	var serial: int = _notice_serials[kind]
	label.text = text
	if text.is_empty():
		label.hide()
		return
	var was_visible := label.visible
	label.show()
	if not was_visible: label.modulate.a = 0.0
	var fade := 0.0 if _reduce_motion or (kind == "flash" and not _screen_flash) else 0.14
	var hold := maxf(0.0, duration)
	_notice_deadlines[kind] = Time.get_ticks_msec() + int((fade + hold) * 1000.0)
	var tween := Transition.real_tween(self)
	_notice_tweens[kind] = tween
	if fade > 0.0: tween.tween_property(label, "modulate:a", 1.0, fade)
	else: label.modulate.a = 1.0
	tween.tween_interval(hold)
	if fade > 0.0: tween.tween_property(label, "modulate:a", 0.0, fade)
	tween.tween_callback(_hide_notice.bind(kind, serial))

func _hide_notice(kind: String, serial: int) -> void:
	if int(_notice_serials.get(kind, -1)) != serial: return
	var label: Label = aside_label if kind == "aside" else flash_label
	label.hide()
	_notice_tweens.erase(kind)
	_notice_deadlines.erase(kind)

func _cancel_notice(kind: String) -> void:
	var tween: Tween = _notice_tweens.get(kind)
	if tween != null: tween.kill()
	_notice_tweens.erase(kind)

func _on_setting_changed(section: String, key: String, value: Variant) -> void:
	if section == "graphics" and key == "ui_scale":
		_apply_field_style()
		_layout.call_deferred()
		return
	if section != "accessibility": return
	if key in ["text_scale", "high_contrast", "plain_fonts"]:
		_apply_field_style()
		_layout.call_deferred()
	if key == "text_speed":
		_text_speed = float(value)
		_begin_reveal()
		return
	if key == "reduce_motion":
		_reduce_motion = bool(value)
		if _reduce_motion:
			_panel_motion.finish_current()
			_line_motion.finish_current()
			_history_motion.finish_current()
			_stop_reveal()
			body_label.visible_ratio = 1.0
			if _portrait_tween != null: _portrait_tween.kill()
			portrait.modulate.a = 1.0
	elif key == "screen_flash":
		_screen_flash = bool(value)
	else:
		return
	if _reduce_motion or not _screen_flash:
		for kind in _notice_deadlines.keys():
			var label: Label = aside_label if kind == "aside" else flash_label
			var remaining := maxf(0.0, float(int(_notice_deadlines[kind]) - Time.get_ticks_msec()) / 1000.0)
			_show_notice(str(kind), label.text, remaining)

func _stop_reveal() -> void:
	if _reveal != null: _reveal.kill()
	_reveal = null

func begin_shutdown() -> void:
	if _shutting_down: return
	_shutting_down = true
	if _dialogue_active: _complete_dialogue(true)
	_panel_motion.dispose()
	_line_motion.dispose()
	_history_motion.dispose()
	_stop_reveal()
	if _portrait_tween != null: _portrait_tween.kill()
	for kind in _notice_tweens.keys(): _cancel_notice(str(kind))
	aside_label.hide()
	flash_label.hide()

func _exit_tree() -> void:
	if Engine.is_editor_hint():
		return
	begin_shutdown()
