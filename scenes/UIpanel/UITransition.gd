extends RefCounted

## A cancellable presentation transition. Each accepted request completes once,
## including interrupted requests; callers never wait on a killed Tween.
signal completed(request_id: int, shown: bool, cancelled: bool)

var _owner: Node
var _root: Node
var _surface: Control
var _backdrop: CanvasItem
var _tween: Tween
var _serial := 0
var _running := false
var _target := false
var _shown := false
var _disposed := false
var _base_scale := Vector2.ONE
var _base_alpha := 1.0
var _backdrop_alpha := 1.0
var _hidden_scale := 0.98

func configure(owner_node: Node, root_node: Node, surface: Control, backdrop: CanvasItem = null, initially_shown := false, hidden_scale := 0.98) -> void:
	_owner = owner_node
	_root = root_node
	_surface = surface
	_backdrop = backdrop
	_base_scale = surface.scale
	_base_alpha = surface.modulate.a
	_hidden_scale = hidden_scale
	if is_instance_valid(_backdrop): _backdrop_alpha = _backdrop.modulate.a
	_target = initially_shown
	_shown = initially_shown
	_surface.resized.connect(_update_pivot)
	_update_pivot()
	_apply(initially_shown)
	_set_visible(initially_shown)

func is_active() -> bool:
	return not _disposed and (_running or _shown or _target)

func is_transitioning() -> bool:
	return _running

func wants_shown() -> bool:
	return _target and not _disposed

func request(shown: bool, animate := true, duration := -1.0) -> int:
	if _disposed: return -1
	if _serial > 0 and _target == shown and (_running or _shown == shown): return _serial
	var old_serial := _serial
	var old_target := _target
	var interrupted := _running
	_serial += 1
	_running = false
	_stop_tween()
	_target = shown
	if interrupted: completed.emit(old_serial, old_target, true)
	_running = true
	_set_visible(true)
	_update_pivot()
	var seconds := duration if duration >= 0.0 else (0.20 if shown else 0.16)
	if not animate or reduced_motion(_owner) or seconds <= 0.0:
		_apply(shown)
		_finish.bind(_serial, shown).call_deferred()
		return _serial
	_tween = real_tween(_owner)
	_tween.set_parallel(true).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT if shown else Tween.EASE_IN)
	_tween.tween_property(_surface, "modulate:a", _base_alpha if shown else 0.0, seconds)
	_tween.tween_property(_surface, "scale", _base_scale if shown else _base_scale * _hidden_scale, seconds)
	if is_instance_valid(_backdrop):
		_tween.tween_property(_backdrop, "modulate:a", _backdrop_alpha if shown else 0.0, seconds)
	_tween.finished.connect(_finish.bind(_serial, shown))
	return _serial

func wait_for(request_id: int) -> bool:
	while not _disposed and request_id == _serial and _running:
		await completed
	return not _disposed and request_id == _serial

func finish_current() -> void:
	if not _running or _disposed: return
	_stop_tween()
	_apply(_target)
	_finish.bind(_serial, _target).call_deferred()

func dispose() -> void:
	if _disposed: return
	_disposed = true
	var was_running := _running
	_running = false
	_stop_tween()
	_set_visible(false)
	if was_running: completed.emit(_serial, _target, true)
	if is_instance_valid(_surface) and _surface.resized.is_connected(_update_pivot):
		_surface.resized.disconnect(_update_pivot)

func _finish(request_id: int, shown: bool) -> void:
	if _disposed or request_id != _serial or not _running: return
	_running = false
	_shown = shown
	_tween = null
	_apply(shown)
	_set_visible(shown)
	completed.emit(request_id, shown, false)

func _apply(shown: bool) -> void:
	if is_instance_valid(_surface):
		_surface.modulate.a = _base_alpha if shown else 0.0
		_surface.scale = _base_scale if shown else _base_scale * _hidden_scale
	if is_instance_valid(_backdrop): _backdrop.modulate.a = _backdrop_alpha if shown else 0.0

func _set_visible(shown: bool) -> void:
	if is_instance_valid(_root): _root.set("visible", shown)
	if is_instance_valid(_backdrop): _backdrop.visible = shown

func _update_pivot() -> void:
	if is_instance_valid(_surface): _surface.pivot_offset = _surface.size * 0.5

func _stop_tween() -> void:
	if _tween != null: _tween.kill()
	_tween = null

static func real_tween(owner_node: Node) -> Tween:
	return owner_node.create_tween().set_ignore_time_scale(true).set_pause_mode(Tween.TWEEN_PAUSE_PROCESS)

static func reduced_motion(owner_node: Node) -> bool:
	var settings := owner_node.get_node_or_null("/root/GameSettings")
	return settings != null and bool(settings.get_value("accessibility", "reduce_motion"))

static func can_focus(control: Control) -> bool:
	if not is_instance_valid(control) or not control.is_inside_tree() or not control.is_visible_in_tree(): return false
	var mode := control.get_focus_mode_with_override()
	# Match native focus eligibility, including an explicitly enabled screen reader.
	if mode == Control.FOCUS_ACCESSIBILITY and not control.get_tree().is_accessibility_enabled(): return false
	return mode != Control.FOCUS_NONE and not (control is BaseButton and control.disabled)

static func focusable_controls(root: Node) -> Array[Control]:
	var result: Array[Control] = []
	if root is Control and can_focus(root): result.append(root)
	for child in root.get_children(): result.append_array(focusable_controls(child))
	return result

static func cycle_focus(root: Control, event: InputEvent) -> bool:
	var backwards := event.is_action_pressed("ui_focus_prev")
	if not backwards and not event.is_action_pressed("ui_focus_next"): return false
	var controls := focusable_controls(root)
	if controls.is_empty(): return true
	var index := controls.find(root.get_viewport().gui_get_focus_owner())
	if index < 0:
		index = controls.size() - 1 if backwards else 0
	else:
		index = (index + (-1 if backwards else 1) + controls.size()) % controls.size()
	controls[index].grab_focus()
	return true

static func set_interactive(root: Control, enabled: bool) -> void:
	root.mouse_behavior_recursive = Control.MOUSE_BEHAVIOR_INHERITED if enabled else Control.MOUSE_BEHAVIOR_DISABLED
	root.focus_behavior_recursive = Control.FOCUS_BEHAVIOR_INHERITED if enabled else Control.FOCUS_BEHAVIOR_DISABLED
