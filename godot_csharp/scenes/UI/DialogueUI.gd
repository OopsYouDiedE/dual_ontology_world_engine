extends CanvasLayer

signal dialogue_step
signal choice_selected(id: String)
signal dialogue_finished(last_choice: String)
signal presentation_completed(presentation_id: int, result: Dictionary)
signal history_closed
signal line_read(line: Dictionary)

const Transition := preload("res://scenes/UI/UITransition.gd")

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

var _dialogue_active := false
var _finishing := false
var _line_changing := false
var _modal_blocked := false
var _shutting_down := false
var _lines_queue: Array = []
var _lines_index := 0
var _lines_choices: Array = []
var _chosen_choice := ""
var _speakers: Dictionary = {}
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

func _ready() -> void:
	layer = 20
	body_label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	speaker_label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	decor.draw.connect(_draw_rules)
	dialogue_panel.resized.connect(_layout)
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
		var path := "res://ShareAsset/Internal/Characters/DialogueCast/%s_calm.tres" % str(key)
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
	var custom_name := str(line.get("speaker_name", ""))
	if custom_name != "":
		_speakers[speaker] = custom_name
	elif not _speakers.has(speaker):
		_speakers[speaker] = speaker
	speaker_label.text = "[b]%s[/b]" % _speakers.get(speaker, speaker)
	body_label.text = str(line.get("text", ""))
	body_label.scroll_to_line(0)
	body_label.visible_ratio = 1.0 if _reduce_motion or _text_speed <= 0.0 else 0.0
	_history.append("%s\n%s" % [str(_speakers.get(speaker, speaker)), body_label.text])
	_assign_portrait(null, _line_serial)
	var texture: Variant = line.get("portrait")
	if texture is Texture2D:
		_assign_portrait(texture, _line_serial)
	else:
		var key := str(_cast.get(speaker, ""))
		if key != "":
			var path := "res://ShareAsset/Internal/Characters/DialogueCast/%s_calm.tres" % key
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
	if not next_prompt.visible:
		for opt: Dictionary in _lines_choices:
			var button := Button.new()
			button.theme_type_variation = &"DialogueChoice"
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
	if not _can_advance(): return
	if _finish_reveal(): return
	_record_visible_line()
	var is_last := _lines_index == _lines_queue.size() - 1
	if is_last and not _lines_choices.is_empty(): return
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
	if line_serial != _line_serial or not _can_advance(): return
	if _finish_reveal(): return
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
	var focused := get_viewport().gui_get_focus_owner()
	_history_focus = weakref(focused) if focused != null else null
	history_body.text = "\n\n".join(_history)
	history_body.scroll_to_line(maxi(0, history_body.get_line_count() - 1))
	_history_motion.request(true)
	_refresh_controls()
	_update_reveal_pause()

func close_history() -> void:
	if not is_history_active(): return
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
	if not _dialogue_active or _modal_blocked or not event.is_pressed() or event.is_echo(): return
	if is_history_active():
		if event.is_action_pressed("cancel") or event.is_action_pressed("ui_cancel"):
			close_history()
			get_viewport().set_input_as_handled()
		elif _history_motion.is_transitioning() or Transition.cycle_focus(history_panel, event):
			get_viewport().set_input_as_handled()
		return
	if Transition.cycle_focus(dialogue_panel, event):
		get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("dialogue_continue") or event.is_action_pressed("ui_accept"):
		var focused := get_viewport().gui_get_focus_owner()
		# Let focused choice/history/next buttons receive their normal GUI action.
		if _can_advance() and focused is BaseButton and dialogue_panel.is_ancestor_of(focused) and event.is_action_pressed("ui_accept"): return
		advance_dialogue()
		get_viewport().set_input_as_handled()

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
	if section != "accessibility": return
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
	begin_shutdown()

func get_fader() -> ColorRect:
	return fader_rect
