extends "res://scenes/UI/ModalPanel.gd"

signal reopen_requested

@onready var modal_title: Label = %ModalTitle
@onready var modal_close_button: Button = %ModalCloseButton
@onready var modal_body: VBoxContainer = %ModalBody

func _ready() -> void:
	super._ready()
	modal_close_button.pressed.connect(close)
	_setup_default_content()

func _is_close_event(event: InputEvent) -> bool:
	return super._is_close_event(event) or event.is_action_pressed("settings")

func _input(event: InputEvent) -> void:
	if is_active() and not is_open() and not _external_blocked \
		and event.is_pressed() and not event.is_echo() and event.is_action_pressed("settings"):
		# Let the owner reverse a closing panel without bypassing its exit lock.
		get_viewport().set_input_as_handled()
		reopen_requested.emit()
		return
	super._input(event)

func open(title := "凡例与操作") -> void:
	modal_title.text = title
	show_panel()

func _setup_default_content() -> void:
	for child in modal_body.get_children():
		modal_body.remove_child(child)
		child.queue_free()
	var label := RichTextLabel.new()
	label.bbcode_enabled = true
	label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	label.text = "[b]操作说明[/b]\n\n• [b]WASD / 方向键 / 鼠标左键[/b]：在当前房间内移动接近\n• [b]鼠标悬停 / 左键点击[/b]：交互现场热点、触发物件或对话\n• [b]空格键 / 回车键[/b]：继续对白\n• [b]ESC 键[/b]：关闭弹窗或返回"
	modal_body.add_child(label)
