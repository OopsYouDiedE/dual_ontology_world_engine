@tool
extends RefCounted
## Presentation-only input shared by the physical book and ordinary modal sheets.
## Native Controls still own confirmation, text editing, slider signals and focus.
const Transition := preload("res://scenes/UIpanel/CasebookUI/UITransition.gd")
const STICK_PRESS := 0.55
const STICK_RELEASE := 0.35
const SCROLL_DEADZONE := 0.22
const REPEAT_DELAY_USEC := 380000
const REPEAT_INTERVAL_USEC := 115000
const SCROLL_PIXELS_PER_SECOND := 620.0

var _stick := Vector2.ZERO
var _dpad := Vector2i.ZERO
var _held := Vector2i.ZERO
var _scroll := 0.0
var _repeat_at := 0
var _last_tick := 0
var _device := -1
var _connection_bound := false

static func is_back(event: InputEvent, pressed_only := true) -> bool:
	var matches: bool = event.is_action("cancel") or event.is_action("ui_cancel") \
		or (event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT)
	return matches and (not pressed_only or (event.is_pressed() and not event.is_echo()))

static func is_gamepad(event: InputEvent) -> bool:
	return event is InputEventJoypadButton or event is InputEventJoypadMotion

func reset() -> void:
	_stick = Vector2.ZERO
	_dpad = Vector2i.ZERO
	_held = Vector2i.ZERO
	_scroll = 0.0
	_repeat_at = 0
	_last_tick = 0
	_device = -1

func handle(root: Control, event: InputEvent) -> bool:
	if not is_instance_valid(root) or not root.is_visible_in_tree(): return false
	if is_gamepad(event) and "--dv-background-capture" in OS.get_cmdline_user_args():
		reset()
		return true
	if is_gamepad(event):
		if not _connection_bound:
			Input.joy_connection_changed.connect(_on_joy_connection_changed)
			_connection_bound = true
		if _device != event.device:
			reset()
			_device = event.device
	if event is InputEventJoypadMotion:
		match event.axis:
			JOY_AXIS_LEFT_X: _stick.x = event.axis_value
			JOY_AXIS_LEFT_Y: _stick.y = event.axis_value
			JOY_AXIS_RIGHT_Y:
				_scroll = event.axis_value if absf(event.axis_value) > SCROLL_DEADZONE else 0.0
				return true
			_: return false
		_update_direction(root)
		return true
	if event is InputEventJoypadButton:
		var amount := 1 if event.pressed else 0
		match event.button_index:
			JOY_BUTTON_DPAD_LEFT: _dpad.x = -amount if event.pressed or _dpad.x < 0 else _dpad.x
			JOY_BUTTON_DPAD_RIGHT: _dpad.x = amount if event.pressed or _dpad.x > 0 else _dpad.x
			JOY_BUTTON_DPAD_UP: _dpad.y = -amount if event.pressed or _dpad.y < 0 else _dpad.y
			JOY_BUTTON_DPAD_DOWN: _dpad.y = amount if event.pressed or _dpad.y > 0 else _dpad.y
			_:
				if event.is_action_pressed("ui_accept"): _ensure_focus(root)
				return false
		_update_direction(root)
		return true
	if event.is_action_pressed("ui_focus_next") or event.is_action_pressed("ui_focus_prev"):
		return Transition.cycle_focus(root, event)
	return false

func tick(root: Control, _delta: float) -> void:
	if not is_instance_valid(root) or not root.is_visible_in_tree():
		reset()
		return
	var now := Time.get_ticks_usec()
	var real_delta := minf(float(now - _last_tick) / 1000000.0, 0.05) if _last_tick > 0 else 0.0
	_last_tick = now
	if _held != Vector2i.ZERO and now >= _repeat_at:
		_move_focus(root, _held)
		_repeat_at = now + REPEAT_INTERVAL_USEC
	if _scroll != 0.0:
		_scroll_content(root, _scroll * SCROLL_PIXELS_PER_SECOND * real_delta)

func _on_joy_connection_changed(device: int, connected: bool) -> void:
	if device == _device and not connected: reset()

func _update_direction(root: Control) -> void:
	var direction := _dpad
	if direction == Vector2i.ZERO:
		var threshold := STICK_RELEASE if _held != Vector2i.ZERO else STICK_PRESS
		if _stick.length() >= threshold:
			if absf(_stick.x) > absf(_stick.y): direction.x = 1 if _stick.x > 0.0 else -1
			else: direction.y = 1 if _stick.y > 0.0 else -1
	if direction.x != 0 and direction.y != 0: direction.x = 0
	if direction == _held: return
	_held = direction
	if direction == Vector2i.ZERO:
		_repeat_at = 0
		return
	_move_focus(root, direction)
	_repeat_at = Time.get_ticks_usec() + REPEAT_DELAY_USEC

func _ensure_focus(root: Control) -> Control:
	var focused := root.get_viewport().gui_get_focus_owner()
	if Transition.can_focus(focused) and (root == focused or root.is_ancestor_of(focused)): return focused
	var controls := Transition.focusable_controls(root)
	if controls.is_empty(): return null
	controls[0].grab_focus()
	return controls[0]

func _move_focus(root: Control, direction: Vector2i) -> void:
	var previous := root.get_viewport().gui_get_focus_owner()
	var focused := _ensure_focus(root)
	if focused == null or previous != focused: return
	# Horizontal movement edits a focused horizontal slider; vertical movement
	# leaves it. This keeps audio controls usable without sacrificing navigation.
	if focused is HSlider and direction.x != 0:
		focused.value += float(direction.x) * (focused.step if focused.step > 0.0 else (focused.max_value - focused.min_value) / 100.0)
		return
	if focused is VSlider and direction.y != 0:
		focused.value -= float(direction.y) * (focused.step if focused.step > 0.0 else (focused.max_value - focused.min_value) / 100.0)
		return
	var side := SIDE_LEFT if direction.x < 0 else SIDE_RIGHT if direction.x > 0 else SIDE_TOP if direction.y < 0 else SIDE_BOTTOM
	var next := focused.find_valid_focus_neighbor(side)
	if Transition.can_focus(next) and (root == next or root.is_ancestor_of(next)) and next != focused:
		next.grab_focus()
		return
	# Native neighbor search may point outside the current sheet. Wrap inside
	# this sheet only, including pages that have just replaced their controls.
	var controls := Transition.focusable_controls(root)
	if controls.is_empty(): return
	var backwards := direction.x < 0 or direction.y < 0
	var index := controls.find(focused)
	index = posmod(index + (-1 if backwards else 1), controls.size())
	controls[index].grab_focus()

func _scroll_content(root: Control, amount: float) -> void:
	var focused := root.get_viewport().gui_get_focus_owner()
	var cursor: Node = focused
	while cursor != null and (cursor == root or root.is_ancestor_of(cursor)):
		if cursor is ScrollContainer:
			var bar: VScrollBar = cursor.get_v_scroll_bar()
			bar.value += amount
			return
		if cursor is RichTextLabel and cursor.scroll_active:
			cursor.get_v_scroll_bar().value += amount
			return
		cursor = cursor.get_parent()
	for value in root.find_children("*", "ScrollContainer", true, false):
		var scroll := value as ScrollContainer
		if scroll.is_visible_in_tree():
			scroll.get_v_scroll_bar().value += amount
			return
	for value in root.find_children("*", "RichTextLabel", true, false):
		var label := value as RichTextLabel
		if label.is_visible_in_tree() and label.scroll_active:
			label.get_v_scroll_bar().value += amount
			return
