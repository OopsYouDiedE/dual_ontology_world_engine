extends Control

## Material 3 Interaction Menu (Task 7.04, 7.16)
## Displays target object name, available actions, and explicit disabled reasons.
## Strictly follows Google Material 3 card/menu specifications.

signal action_selected(action_id: String, target_id: String)
signal menu_closed
signal blocked_feedback(reason: String)

const MaterialControls := preload("res://scenes/UI/Components/MaterialControls.gd")
const Snackbar := preload("res://scenes/UI/Components/Snackbar.gd")

@onready var card_panel: PanelContainer = %CardPanel
@onready var title_label: Label = %TitleLabel
@onready var subtitle_label: Label = %SubtitleLabel
@onready var action_list: VBoxContainer = %ActionList
@onready var close_button: Button = %CloseButton
@onready var snackbar: PanelContainer = %Snackbar

var _current_target_id: String = ""
var _is_open: bool = false
var _actions_data: Array = []

func _ready() -> void:
	visible = false
	MaterialControls.apply(self)
	MaterialControls.prepare_button(close_button)
	close_button.pressed.connect(close_menu)
	
func is_menu_open() -> bool:
	return _is_open

## Opens the interaction menu with structured target data:
## data: {
##   "target_id": "door_2f_01",
##   "target_name": "2F 走廊木门",
##   "location": "走廊西侧",
##   "actions": [
##     {"id": "open", "label": "推开", "available": false, "reason": "门锁着"},
##     {"id": "inspect", "label": "检视锁孔", "available": true, "reason": ""}
##   ]
## }
func open_for_target(data: Dictionary) -> void:
	_current_target_id = str(data.get("target_id", data.get("id", "")))
	var target_name: String = str(data.get("target_name", data.get("name", data.get("title", "未知物体"))))
	var location: String = str(data.get("location", data.get("room", "")))
	_actions_data = data.get("options", data.get("actions", [])).duplicate(true)
	
	title_label.text = target_name
	title_label.tooltip_text = target_name
	
	if location.is_empty():
		subtitle_label.visible = false
	else:
		subtitle_label.text = location
		subtitle_label.visible = true
		
	_rebuild_action_list()
	
	visible = true
	_is_open = true
	
	# Focus the first available action, or close button if none available
	_focus_initial_control()

## Rebuilds the action items using M3 button cards
func _rebuild_action_list() -> void:
	for child in action_list.get_children():
		action_list.remove_child(child)
		child.queue_free()
		
	if _actions_data.is_empty():
		var empty_label := Label.new()
		empty_label.text = "此时无可行动作"
		empty_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		empty_label.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
		action_list.add_child(empty_label)
		return
		
	for act in _actions_data:
		if not act is Dictionary:
			continue
		var act_id: String = str(act.get("id", ""))
		var act_label: String = str(act.get("label", act.get("text", act_id)))
		var available: bool = bool(act.get("available", true))
		var reason: String = str(act.get("reason", act.get("disabled_reason", "")))
		
		var btn := Button.new()
		MaterialControls.prepare_button(btn)
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		
		# Text with action and explicit blocking feedback (Task 7.16)
		if available:
			btn.text = act_label
			btn.tooltip_text = act_label
			btn.pressed.connect(func(): _on_action_clicked(act_id))
		else:
			var display_reason: String = reason if not reason.is_empty() else "当前条件不满足"
			btn.text = "%s  [%s]" % [act_label, display_reason]
			btn.tooltip_text = display_reason
			# In M3, disabled actions explain why and show a snackbar on click
			btn.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
			btn.pressed.connect(func(): _on_disabled_action_clicked(act_label, display_reason))
			
		action_list.add_child(btn)

func _focus_initial_control() -> void:
	await get_tree().process_frame
	for child in action_list.get_children():
		if child is Button and not child.text.contains("["):
			child.grab_focus()
			return
	close_button.grab_focus()

func _on_action_clicked(action_id: String) -> void:
	if not _is_open:
		return
	var target_id := _current_target_id
	close_menu()
	action_selected.emit(action_id, target_id)

func _on_disabled_action_clicked(action_label: String, reason: String) -> void:
	# Explicit blocking feedback without generic "rejected" (Task 7.16)
	var feedback := "%s不可执行：%s" % [action_label, reason]
	blocked_feedback.emit(feedback)
	if is_instance_valid(snackbar) and snackbar.has_method("show_message"):
		snackbar.show_message(feedback, "", 3.0)

## Updates or invalidates target if the object state changed (e.g., target moved away)
func update_target_state(target_id: String, still_valid: bool, new_data: Dictionary = {}) -> void:
	if not _is_open or target_id != _current_target_id:
		return
	if not still_valid:
		var msg := "目标已离开或当前不可用"
		blocked_feedback.emit(msg)
		if is_instance_valid(snackbar) and snackbar.has_method("show_message"):
			snackbar.show_message(msg, "", 2.5)
		close_menu()
	elif not new_data.is_empty():
		open_for_target(new_data)

func close_menu() -> void:
	if not _is_open:
		return
	_is_open = false
	visible = false
	menu_closed.emit()

func _gui_input(event: InputEvent) -> void:
	if not _is_open:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("cancel"):
		close_menu()
		get_viewport().set_input_as_handled()
