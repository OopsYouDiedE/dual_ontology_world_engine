extends CanvasLayer

signal opened
signal closing
signal closed

const Transition := preload("res://scenes/UI/UITransition.gd")

var restore_previous_focus := true
var _motion = Transition.new()
var _return_focus: WeakRef
var _shutting_down := false
var _selection_pending := false
var _external_blocked := false

@onready var _modal_surface: Control = $CenterBox
@onready var _modal_dimmer: ColorRect = $Dimmer

func _ready() -> void:
	_motion.configure(self, self, _modal_surface, _modal_dimmer, false, 1.0)
	_motion.completed.connect(_on_transition_completed)
	Transition.set_interactive(_modal_surface, false)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.setting_changed.connect(_on_setting_changed)

func is_active() -> bool:
	return not _shutting_down and _motion.is_active()

func is_open() -> bool:
	return is_active() and _motion.wants_shown()

func transition_state() -> String:
	if not is_active(): return "closed"
	if _motion.is_transitioning(): return "entering" if _motion.wants_shown() else "closing"
	return "open"

func can_interact() -> bool:
	return is_open() and not _motion.is_transitioning() and not _selection_pending and not _external_blocked

func set_external_blocked(blocked: bool) -> void:
	if _external_blocked == blocked: return
	_external_blocked = blocked
	Transition.set_interactive(_modal_surface, can_interact())
	if can_interact(): _enable_panel()

func show_panel() -> void:
	if _shutting_down: return
	if not _motion.is_active():
		var focused := get_viewport().gui_get_focus_owner()
		_return_focus = weakref(focused) if focused != null else null
	_selection_pending = false
	Transition.set_interactive(_modal_surface, false)
	_motion.request(true)
	if not _motion.is_transitioning(): _enable_panel()

func close() -> void:
	if not is_active() or not _motion.wants_shown(): return
	_selection_pending = true
	Transition.set_interactive(_modal_surface, false)
	closing.emit()
	_motion.request(false)

func close_and_wait() -> bool:
	if not is_active(): return true
	close()
	var request_id: int = _motion.request(false)
	return await _motion.wait_for(request_id)

func begin_shutdown() -> void:
	if _shutting_down: return
	_shutting_down = true
	_return_focus = null
	Transition.set_interactive(_modal_surface, false)
	_motion.dispose()

func _exit_tree() -> void:
	_motion.dispose()

func _input(event: InputEvent) -> void:
	if not is_active() or not event.is_pressed() or event.is_echo(): return
	if _external_blocked:
		get_viewport().set_input_as_handled()
	elif _is_close_event(event):
		close()
		get_viewport().set_input_as_handled()
	elif not can_interact():
		# Keep the departing panel modal until its final frame, including keyboard input.
		get_viewport().set_input_as_handled()
	elif Transition.cycle_focus(_modal_surface, event):
		get_viewport().set_input_as_handled()

func _is_close_event(event: InputEvent) -> bool:
	return event.is_action_pressed("cancel") or event.is_action_pressed("ui_cancel")

func _enable_panel() -> void:
	if not is_open() or _external_blocked: return
	Transition.set_interactive(_modal_surface, true)
	var button := get_node_or_null("%ModalCloseButton") as Control
	if Transition.can_focus(button): button.grab_focus()

func _on_transition_completed(_request_id: int, shown: bool, cancelled: bool) -> void:
	if cancelled or _shutting_down: return
	if shown:
		_enable_panel()
		opened.emit()
	else:
		if restore_previous_focus and _return_focus != null:
			var control := _return_focus.get_ref() as Control
			if Transition.can_focus(control): control.grab_focus()
		_return_focus = null
		closed.emit()

func _on_setting_changed(section: String, key: String, value: Variant) -> void:
	if section == "accessibility" and key == "reduce_motion" and bool(value): _motion.finish_current()
