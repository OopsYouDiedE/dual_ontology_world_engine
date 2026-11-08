extends Control

## 现场 GUI 主控：组合三层无底界面——
## 时间与房间（ClockRoomGUI）、任务进度（TaskProgressGUI）、物件可选动作（ActionChoiceGUI）。
## 主控只读 CampaignRoomHost.observation() 并分发给子组件，子组件不认识现场宿主。
## Main 进入现场后调用 configure(field)，离开时 release()，打开书本时 set_suppressed(true)。

signal action_requested(action_id: String)
signal cancel_requested
signal room_pressed(room_id: String)
signal suppressed_changed(suppressed: bool)

@export_group("驱动")
## 为真时由本界面直接调用现场宿主提交行动与取消；为假时只发信号，交给 Main 处理。
@export var drive_field_directly := true
@export_range(0.05, 1.0) var refresh_interval := 0.2

@export_group("跟随")
@export var follow_selected_target := true
## 动作列表锚在目标上方这一高度（米）的屏幕投影处。
@export var target_head_height := 1.7

@export_group("组件")
@export var show_clock_room := true
@export var show_task_progress := true
@export var show_action_choices := true
@export var walking_prefix := "走近 "

@onready var clock_room = $ClockRoomGUI
@onready var task_progress = $TaskProgressGUI
@onready var action_choices = $ActionChoiceGUI

var field: Node
var _elapsed := 0.0
var _action_signature := ""
var _suppressed := false

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	action_choices.action_chosen.connect(_on_action_chosen)
	task_progress.cancel_requested.connect(_on_cancel_requested)
	clock_room.room_pressed.connect(func(room_id: String): room_pressed.emit(room_id))
	_apply_visibility()
	hide()

# ================================================================ public ===

func configure(field_host: Node) -> void:
	field = field_host
	_action_signature = ""
	_suppressed = false
	_apply_visibility()
	show()
	refresh()

func release() -> void:
	field = null
	action_choices.dismiss()
	task_progress.clear_task()
	hide()

func set_suppressed(suppressed: bool) -> void:
	if suppressed == _suppressed: return
	_suppressed = suppressed
	visible = not suppressed and is_instance_valid(field)
	suppressed_changed.emit(suppressed)

func refresh() -> void:
	if not is_instance_valid(field) or not field.has_method("observation"): return
	var obs: Dictionary = field.observation()
	if show_clock_room: _refresh_clock(obs)
	if show_task_progress: _refresh_task(obs)
	_refresh_actions(obs)

# ============================================================== internal ===

func _process(delta: float) -> void:
	if not visible or not is_instance_valid(field): return
	_follow_target()
	_elapsed += delta
	if _elapsed < refresh_interval: return
	_elapsed = 0.0
	refresh()

func _apply_visibility() -> void:
	clock_room.visible = show_clock_room
	task_progress.visible = show_task_progress
	action_choices.visible = show_action_choices

func _refresh_clock(obs: Dictionary) -> void:
	var room := str(field.get("room_id"))
	clock_room.set_place(room, str(field.room_label(room)) if field.has_method("room_label") else room)
	var state: Variant = field.get("state")
	var seconds := float(state.get("game_seconds", 0.0)) if state is Dictionary else 0.0
	clock_room.set_time(seconds, bool(obs.get("paused", false)))

func _refresh_task(obs: Dictionary) -> void:
	var phase := str(field.get("phase"))
	var pending := str(obs.get("pending_approach", ""))
	var progress: Dictionary = obs.get("player_progress", {}) if obs.get("player_progress") is Dictionary else {}
	if phase == "walking" and not pending.is_empty():
		task_progress.show_task(walking_prefix + _focus_label(str(obs.get("selected_focus", ""))))
	elif phase == "working":
		task_progress.show_task(_progress_label(progress, obs), _progress_ratio(progress), _progress_remaining(progress))
	else:
		task_progress.clear_task()

func _refresh_actions(obs: Dictionary) -> void:
	var focus := str(obs.get("selected_focus", ""))
	if focus.is_empty() or not show_action_choices:
		action_choices.dismiss()
		_action_signature = ""
		return
	var options: Array = []
	var signature := focus
	for choice in obs.get("choices", []):
		if not choice is Dictionary or str(choice.get("focus", "")) != focus: continue
		var id := str(choice.get("id", choice.get("action_id", "")))
		if id.is_empty(): continue
		var enabled := bool(choice.get("available", true))
		options.append({"id": id, "label": str(choice.get("label", id)), "enabled": enabled,
			"hint": str(choice.get("hint", choice.get("description", "")))})
		signature += "|%s:%s" % [id, enabled]
	if signature != _action_signature or not action_choices.is_presented():
		_action_signature = signature
		action_choices.present(_focus_label(focus), options)
	action_choices.set_pending(str(obs.get("pending_approach", "")))

## 把动作列表锚在选中目标头顶的屏幕投影处；目标不在镜头前时退回固定位置。
func _follow_target() -> void:
	if not follow_selected_target or not action_choices.is_presented(): return
	var camera: Variant = field.get("camera")
	var focus := str(field.get("selected_focus"))
	var node: Variant = field._target_node(focus) if field.has_method("_target_node") and not focus.is_empty() else null
	if not camera is Camera3D or not node is Node3D:
		action_choices.clear_anchor()
		return
	var world: Vector3 = node.global_position + Vector3.UP * target_head_height
	if camera.is_position_behind(world): action_choices.clear_anchor()
	else: action_choices.set_anchor(camera.unproject_position(world))

func _on_action_chosen(action_id: String) -> void:
	action_requested.emit(action_id)
	if not drive_field_directly or not is_instance_valid(field) or not field.has_method("request_action"): return
	var error: String = field.request_action(action_id)
	if not error.is_empty(): action_choices.show_notice(error)
	_action_signature = ""
	refresh()

func _on_cancel_requested() -> void:
	cancel_requested.emit()
	if drive_field_directly and is_instance_valid(field) and field.has_method("interface_command"):
		field.interface_command({"command": "cancel"})

func _focus_label(focus: String) -> String:
	if is_instance_valid(field) and field.has_method("focus_label"): return str(field.focus_label(focus))
	return focus

static func _progress_label(progress: Dictionary, obs: Dictionary) -> String:
	for key in ["label", "action_label", "title"]:
		if not str(progress.get(key, "")).is_empty(): return str(progress[key])
	var feedback := str(obs.get("feedback", "")).trim_suffix("…")
	return feedback if not feedback.is_empty() else str(obs.get("player_action", ""))

static func _progress_ratio(progress: Dictionary) -> float:
	for key in ["ratio", "progress", "fraction", "completion"]:
		var value: Variant = progress.get(key)
		if value is float or value is int: return clampf(float(value), 0.0, 1.0)
	var duration := float(progress.get("duration", progress.get("duration_seconds", 0.0)))
	if duration <= 0.0: return -1.0
	if progress.has("elapsed"): return clampf(float(progress.elapsed) / duration, 0.0, 1.0)
	if progress.has("remaining"): return clampf(1.0 - float(progress.remaining) / duration, 0.0, 1.0)
	return -1.0

static func _progress_remaining(progress: Dictionary) -> float:
	for key in ["remaining_seconds", "remaining"]:
		var value: Variant = progress.get(key)
		if value is float or value is int: return float(value)
	var duration := float(progress.get("duration", progress.get("duration_seconds", 0.0)))
	if duration > 0.0 and progress.has("elapsed"): return maxf(0.0, duration - float(progress.elapsed))
	return -1.0
