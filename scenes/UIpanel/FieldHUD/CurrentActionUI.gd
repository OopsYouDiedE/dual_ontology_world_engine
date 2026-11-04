@tool
extends Control
## Progress is the host's measured value; no local completion clock is started.

signal action_cancel_requested(action_id: String)

const Chrome := preload("res://scenes/UIpanel/FieldHUD/FieldChrome.gd")
@export var auto_hide_when_idle := true
@onready var action_capsule: PanelContainer = %ActionCapsule
@onready var action_label: Label = %ActionLabel
@onready var cancel_btn: Button = %CancelBtn
@onready var progress_bar: ProgressBar = %ProgressBar
@onready var center_box: HBoxContainer = %CenterBox

var _current_action: Dictionary = {}
var _queued_actions: Array = []
var _screen_anchor := Vector2.ZERO
var _overlay_blocked := false

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	Chrome.apply(self, 15)
	var capsule_style := Chrome.box(Chrome.opacity(self, 0.84), Vector4(14, 2, 4, 2))
	capsule_style.corner_radius_top_left = 12
	capsule_style.corner_radius_top_right = 12
	capsule_style.corner_radius_bottom_left = 12
	capsule_style.corner_radius_bottom_right = 12
	action_capsule.add_theme_stylebox_override("panel", capsule_style)
	Chrome.button(cancel_btn, true)
	cancel_btn.pressed.connect(_on_cancel_pressed)
	action_capsule.gui_input.connect(func(event: InputEvent):
		if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
			_on_cancel_pressed()
			accept_event()
	)
	get_viewport().size_changed.connect(_place)
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null: settings.setting_changed.connect(_on_setting_changed)
	_refresh_display()

func apply_queue_snapshot(data: Dictionary) -> void:
	var current: Variant = data.get("current_action", {})
	_current_action = current.duplicate(true) if current is Dictionary else {}
	var queued: Variant = data.get("queued_actions", [])
	_queued_actions = queued.duplicate(true) if queued is Array else []
	if data.get("screen_position") is Vector2: _screen_anchor = data.screen_position
	elif data.get("screen_position") is Array and data.screen_position.size() >= 2:
		_screen_anchor = Vector2(float(data.screen_position[0]), float(data.screen_position[1]))
	_refresh_display()

func set_current_action(action_id: String, label: String, is_waiting := false, wait_reason := "") -> void:
	_current_action = {"id": action_id, "label": label, "is_waiting": is_waiting, "wait_reason": wait_reason}
	_refresh_display()

func set_screen_anchor(screen_position: Vector2) -> void:
	_screen_anchor = screen_position
	_place()

func clear_actions() -> void:
	_current_action.clear()
	_queued_actions.clear()
	_refresh_display()

func set_overlay_blocked(blocked: bool) -> void:
	_overlay_blocked = blocked
	_refresh_display()

func _on_setting_changed(section: String, key: String, _value: Variant) -> void:
	if section != "accessibility" and not (section == "graphics" and key == "ui_scale"): return
	Chrome.apply(self, 15)
	_place.call_deferred()

func _refresh_display() -> void:
	if not is_node_ready(): return
	var has_action := not _current_action.is_empty()
	visible = not _overlay_blocked and (has_action or not _queued_actions.is_empty() or not auto_hide_when_idle)
	if not visible: return
	cancel_btn.visible = has_action and not str(_current_action.get("id", "")).is_empty()
	progress_bar.hide()
	var text := ""
	if has_action:
		var label := str(_current_action.get("label", _current_action.get("name", "")))
		if label.is_empty(): label = "行动中"
		text = label
		if bool(_current_action.get("is_waiting", false)):
			var reason := str(_current_action.get("wait_reason", "等待"))
			text += "  " + reason
		var progress: Variant = _current_action.get("progress", _current_action.get("progress_ratio"))
		if (progress is int or progress is float) and float(progress) >= 0:
			progress_bar.show()
			progress_bar.value = clampf(float(progress), 0.0, 1.0)
	if not _queued_actions.is_empty():
		var first: Variant = _queued_actions[0]
		if first is Dictionary:
			var next := str(first.get("label", first.get("name", "")))
			if not next.is_empty():
				text += ("   ›   " if has_action else "") + next
				if _queued_actions.size() > 1: text += " +%d" % (_queued_actions.size() - 1)
	action_label.text = text
	action_label.tooltip_text = text
	_place.call_deferred()

func _place() -> void:
	if not is_node_ready() or not visible: return
	var screen := get_viewport_rect().size
	var font := action_label.get_theme_font("font")
	var font_size := action_label.get_theme_font_size("font_size")
	var width := font.get_string_size(action_label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x + 72
	var extent := Vector2(minf(maxf(140, width), minf(520 * Chrome.ui_scale(self), maxf(140, screen.x - 36))), maxf(44, font.get_height(font_size) + 12))
	center_box.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	center_box.size = extent
	var desired := Vector2((screen.x - extent.x) * 0.5, screen.y - extent.y - 28)
	if _screen_anchor != Vector2.ZERO: desired = _screen_anchor + Vector2(-extent.x * 0.5, 28)
	center_box.position = Chrome.bounded_position(center_box, desired)
	progress_bar.position = center_box.position + Vector2(14, extent.y - 3)
	progress_bar.size = Vector2(maxf(20, extent.x - 28), 2)

func _on_cancel_pressed() -> void:
	var id := str(_current_action.get("id", ""))
	if not id.is_empty(): action_cancel_requested.emit(id)
