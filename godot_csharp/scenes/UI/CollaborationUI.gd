extends Control

## Material 3 Collaboration Schedule Interface (Task 7.11)
## Shows participants, tasks, designated locations, invite acceptance state, and verified arrival.
## Sending an invitation is never equated with acceptance; arrival strictly reflects real spatial location.

signal invite_dispatched(participant: String, task: String, location: String)
signal arrangement_cancelled(participant: String)
signal collaboration_closed

const MaterialControls := preload("res://scenes/UI/Components/MaterialControls.gd")

@onready var modal_backdrop: ColorRect = %Backdrop
@onready var main_card: PanelContainer = %MainCard
@onready var title_label: Label = %TitleLabel
@onready var close_btn: Button = %CloseButton
@onready var schedule_list: VBoxContainer = %ScheduleList
@onready var empty_tip: Label = %EmptyTip

var _is_open: bool = false
var _arrangements: Array = []

func _ready() -> void:
	visible = false
	MaterialControls.apply(self)
	MaterialControls.prepare_button(close_btn)
	close_btn.pressed.connect(close_collaboration)

func is_collaboration_open() -> bool:
	return _is_open

## Opens collaboration UI with authoritative plan data:
## data format:
## {
##   "arrangements": [
##     {
##       "participant": "苏泠",
##       "task": "配膳台物资清点",
##       "location": "配膳台",
##       "invite_status": "accepted", # "accepted" | "declined" | "pending"
##       "decline_reason": "",
##       "arrival_status": "on_site" # "on_site" | "en_route" | "absent"
##     },
##     {
##       "participant": "沈鼎",
##       "task": "病房留守照看",
##       "location": "病房",
##       "invite_status": "pending",
##       "arrival_status": "absent"
##     }
##   ]
## }
func open_collaboration(data: Dictionary) -> void:
	_arrangements = data.get("arrangements", []).duplicate(true)
	_rebuild_schedule()
	
	visible = true
	_is_open = true
	
	await get_tree().process_frame
	close_btn.grab_focus()

func _rebuild_schedule() -> void:
	for child in schedule_list.get_children():
		schedule_list.remove_child(child)
		child.queue_free()
		
	if _arrangements.is_empty():
		empty_tip.visible = true
		return
	empty_tip.visible = false
	
	for plan in _arrangements:
		if not plan is Dictionary:
			continue
		var participant: String = str(plan.get("participant", plan.get("actor", "同伴")))
		var task: String = str(plan.get("task", "协作任务"))
		var location: String = str(plan.get("location", "约定地点"))
		var invite_status: String = str(plan.get("invite_status", "pending"))
		var decline_reason: String = str(plan.get("decline_reason", ""))
		var arrival_status: String = str(plan.get("arrival_status", "absent"))
		
		var card := PanelContainer.new()
		var box := StyleBoxFlat.new()
		box.bg_color = MaterialControls.COLOR_SURFACE_VARIANT
		box.corner_radius_top_left = 8
		box.corner_radius_top_right = 8
		box.corner_radius_bottom_right = 8
		box.corner_radius_bottom_left = 8
		box.content_margin_left = 16.0
		box.content_margin_top = 10.0
		box.content_margin_right = 16.0
		box.content_margin_bottom = 10.0
		card.add_theme_stylebox_override("panel", box)
		
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 16)
		
		var col := VBoxContainer.new()
		col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		col.add_theme_constant_override("separation", 4)
		
		var header_row := HBoxContainer.new()
		header_row.add_theme_constant_override("separation", 8)
		
		var name_lbl := Label.new()
		name_lbl.text = "%s  ·  任务: %s" % [participant, task]
		name_lbl.add_theme_font_size_override("font_size", 16)
		header_row.add_child(name_lbl)
		
		# Invite status badge (Task 7.11: invite is distinct from acceptance)
		var status_badge: PanelContainer
		match invite_status:
			"accepted":
				status_badge = MaterialControls.create_badge("已接受邀请", "safe")
			"declined":
				status_badge = MaterialControls.create_badge("已拒绝", "critical")
				if not decline_reason.is_empty():
					status_badge.tooltip_text = "拒绝原因: " + decline_reason
			_:
				status_badge = MaterialControls.create_badge("等待回复 (未接受)", "warning")
		header_row.add_child(status_badge)
		
		# Arrival badge (Strictly matches real spatial location)
		var arrival_badge: PanelContainer
		match arrival_status:
			"on_site":
				arrival_badge = MaterialControls.create_badge("已就位", "primary")
			"en_route":
				arrival_badge = MaterialControls.create_badge("在途中", "info")
			_:
				arrival_badge = MaterialControls.create_badge("未到达", "info")
		header_row.add_child(arrival_badge)
		col.add_child(header_row)
		
		var loc_lbl := Label.new()
		loc_lbl.text = "约定地点: %s" % location
		loc_lbl.add_theme_font_size_override("font_size", 13)
		loc_lbl.add_theme_color_override("font_color", MaterialControls.COLOR_OUTLINE)
		col.add_child(loc_lbl)
		
		row.add_child(col)
		
		# Action buttons
		if invite_status == "pending" or invite_status == "declined":
			var invite_btn := Button.new()
			MaterialControls.prepare_button(invite_btn)
			invite_btn.text = "发送邀请"
			invite_btn.custom_minimum_size = Vector2(80, 40)
			invite_btn.pressed.connect(func():
				invite_dispatched.emit(participant, task, location)
			)
			row.add_child(invite_btn)
			
		var cancel_btn := Button.new()
		MaterialControls.prepare_button(cancel_btn)
		cancel_btn.text = "取消"
		cancel_btn.custom_minimum_size = Vector2(60, 40)
		cancel_btn.pressed.connect(func():
			arrangement_cancelled.emit(participant)
		)
		row.add_child(cancel_btn)
		
		card.add_child(row)
		schedule_list.add_child(card)

func close_collaboration() -> void:
	if not _is_open:
		return
	_is_open = false
	visible = false
	collaboration_closed.emit()

func _gui_input(event: InputEvent) -> void:
	if not _is_open:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("cancel"):
		close_collaboration()
		get_viewport().set_input_as_handled()
