extends Control

## Material 3 Action Queue Panel (Task 7.05)
## Shows current action, queued actions, cancel button per action, and wait reason.
## Strictly avoids fake completion progress; cancels only the specified action.

signal action_cancel_requested(action_id: String)
signal panel_toggled(expanded: bool)

const MaterialControls := preload("res://scenes/UI/Components/MaterialControls.gd")

@onready var container: PanelContainer = %PanelContainer
@onready var current_action_title: Label = %CurrentActionTitle
@onready var current_action_desc: Label = %CurrentActionDesc
@onready var wait_reason_label: Label = %WaitReasonLabel
@onready var cancel_current_btn: Button = %CancelCurrentBtn
@onready var queued_list: VBoxContainer = %QueuedList
@onready var queue_section: VBoxContainer = %QueueSection
@onready var toggle_btn: Button = %ToggleBtn

var _is_expanded: bool = true
var _current_action_id: String = ""
var _queued_actions: Array = []

func _ready() -> void:
	MaterialControls.apply(self)
	MaterialControls.prepare_button(cancel_current_btn)
	MaterialControls.prepare_button(toggle_btn)
	cancel_current_btn.pressed.connect(_on_cancel_current_pressed)
	toggle_btn.pressed.connect(toggle_expanded)
	_refresh_display()

## Updates the queue with observer data:
## data: {
##   "current_action": {
##     "id": "move_corridor",
##     "label": "移动向 2F 走廊",
##     "is_waiting": true,
##     "wait_reason": "等待前方人物通行"
##   },
##   "queued_actions": [
##     {"id": "open_door_r05", "label": "推开 R05 门"},
##     {"id": "inspect_table", "label": "检视桌面"}
##   ]
## }
func apply_queue_snapshot(data: Dictionary) -> void:
	var curr: Variant = data.get("current_action", null)
	if curr is Dictionary and not curr.is_empty():
		_current_action_id = str(curr.get("id", ""))
		current_action_title.text = str(curr.get("label", curr.get("name", _current_action_id)))
		var desc: String = str(curr.get("description", curr.get("detail", "")))
		current_action_desc.text = desc
		current_action_desc.visible = not desc.is_empty()
		
		var is_waiting: bool = bool(curr.get("is_waiting", false))
		var wait_reason: String = str(curr.get("wait_reason", ""))
		if is_waiting and not wait_reason.is_empty():
			wait_reason_label.text = "等待中：%s" % wait_reason
			wait_reason_label.visible = true
		else:
			wait_reason_label.visible = false
		cancel_current_btn.visible = not _current_action_id.is_empty()
	else:
		_current_action_id = ""
		current_action_title.text = "待命中"
		current_action_desc.visible = false
		wait_reason_label.visible = false
		cancel_current_btn.visible = false
		
	_queued_actions = data.get("queued_actions", []).duplicate(true)
	_rebuild_queued_items()
	visible = not _current_action_id.is_empty() or not _queued_actions.is_empty()

func _rebuild_queued_items() -> void:
	for child in queued_list.get_children():
		queued_list.remove_child(child)
		child.queue_free()
		
	if _queued_actions.is_empty():
		queue_section.visible = false
		return
		
	queue_section.visible = true
	for item in _queued_actions:
		if not item is Dictionary:
			continue
		var act_id: String = str(item.get("id", ""))
		var act_label: String = str(item.get("label", item.get("name", act_id)))
		var wait_reason: String = str(item.get("wait_reason", ""))
		
		var row := HBoxContainer.new()
		row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_theme_constant_override("separation", 8)
		
		var lbl_col := VBoxContainer.new()
		lbl_col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		
		var name_lbl := Label.new()
		name_lbl.text = act_label
		name_lbl.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
		lbl_col.add_child(name_lbl)
		
		if not wait_reason.is_empty():
			var wait_lbl := Label.new()
			wait_lbl.text = "排队等待：%s" % wait_reason
			wait_lbl.add_theme_font_size_override("font_size", 12)
			wait_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
			lbl_col.add_child(wait_lbl)
			
		row.add_child(lbl_col)
		
		var cancel_btn := Button.new()
		MaterialControls.prepare_button(cancel_btn)
		cancel_btn.text = "取消"
		cancel_btn.custom_minimum_size = Vector2(56, 36)
		cancel_btn.pressed.connect(func(): _on_cancel_queued_pressed(act_id))
		row.add_child(cancel_btn)
		
		queued_list.add_child(row)

func _on_cancel_current_pressed() -> void:
	if not _current_action_id.is_empty():
		action_cancel_requested.emit(_current_action_id)

func _on_cancel_queued_pressed(action_id: String) -> void:
	if not action_id.is_empty():
		action_cancel_requested.emit(action_id)

func toggle_expanded() -> void:
	_is_expanded = not _is_expanded
	toggle_btn.text = "▼" if _is_expanded else "▲"
	current_action_desc.visible = _is_expanded and not current_action_desc.text.is_empty()
	wait_reason_label.visible = _is_expanded and not wait_reason_label.text.is_empty()
	queue_section.visible = _is_expanded and not _queued_actions.is_empty()
	panel_toggled.emit(_is_expanded)

func _refresh_display() -> void:
	toggle_btn.text = "▼" if _is_expanded else "▲"
