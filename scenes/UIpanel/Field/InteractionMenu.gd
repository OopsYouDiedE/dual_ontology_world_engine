extends Control

## 3D 目标切角交互短签（InteractionMenu.gd）
## 锚定于 3D 交互物旁的上下文动作抽屉与切角短签。

signal action_selected(action_id: String, target_id: String)
signal parameter_action_requested(action_id: String, target_id: String, parameters: Dictionary)
signal menu_closed

@onready var panel: PanelContainer = $Anchor/MenuPanel
@onready var target_title: Label = $Anchor/MenuPanel/MarginContainer/VBoxContainer/TargetTitle
@onready var target_location: Label = $Anchor/MenuPanel/MarginContainer/VBoxContainer/TargetLocation
@onready var actions_container: VBoxContainer = $Anchor/MenuPanel/MarginContainer/VBoxContainer/ActionsContainer
@onready var btn_close: Button = $Anchor/MenuPanel/MarginContainer/VBoxContainer/CloseButton
@onready var anchor: Control = $Anchor

var _target_id := ""

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	btn_close.pressed.connect(close_menu)
	hide()

func open_menu(target_id: String, target_name: String, actions: Array, screen_pos := Vector2(800, 450)) -> void:
	_target_id = target_id
	target_title.text = target_name
	target_location.text = "目标 ID: " + target_id

	for child in actions_container.get_children():
		child.queue_free()

	for act in actions:
		var act_id: String = ""
		var act_label: String = ""
		var act_cost := 0
		if act is Dictionary:
			act_id = str(act.get("id", ""))
			act_label = str(act.get("label", act.get("name", act_id)))
			act_cost = int(act.get("cost", 0))
		else:
			act_id = str(act)
			act_label = act_id

		var btn := Button.new()
		var text := act_label
		if act_cost > 0:
			text += " (%d AP)" % act_cost
		btn.text = text
		btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
		btn.pressed.connect(func():
			action_selected.emit(act_id, _target_id)
		)
		actions_container.add_child(btn)

	anchor.position = screen_pos
	show()

func close_menu() -> void:
	hide()
	menu_closed.emit()
