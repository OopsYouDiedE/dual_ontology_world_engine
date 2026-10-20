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
	populate_page(modal_body)

## Shared settings content: the title embeds this in the physical book leaf.
func populate_page(container: VBoxContainer) -> void:
	var settings := get_node_or_null("/root/GameSettings")
	if settings != null:
		for specification in [
			["audio", "master", "总音量"], ["audio", "music", "音乐"], ["audio", "sfx", "音效"],
			["accessibility", "reduce_motion", "减少动态效果"],
			["accessibility", "text_speed", "文字速度"], ["graphics", "window_mode", "窗口模式"],
			["graphics", "vsync", "垂直同步"]
		]:
			var section: String = specification[0]
			var key: String = specification[1]
			var label := Label.new()
			label.text = specification[2]
			container.add_child(label)
			var value = settings.get_value(section, key)
			if value is bool:
				var check := CheckButton.new()
				check.theme_type_variation = &"BookToggle"
				check.text = "启用"
				check.button_pressed = value
				check.toggled.connect(func(enabled: bool): settings.set_value(section, key, enabled))
				container.add_child(check)
			elif section == "audio":
				var slider := HSlider.new()
				slider.min_value = 0.0
				slider.max_value = 1.0
				slider.step = 0.01
				slider.value = float(value)
				slider.custom_minimum_size.y = 28
				slider.value_changed.connect(func(amount: float): settings.set_value(section, key, amount))
				container.add_child(slider)
			else:
				var option := Button.new()
				option.theme_type_variation = &"BookOption"
				option.alignment = HORIZONTAL_ALIGNMENT_LEFT
				var allowed: Array = [0.5, 1.0, 2.0, 0.0] if key == "text_speed" else ["windowed", "fullscreen", "borderless"]
				var captions: Array = ["缓慢", "正常", "快速", "立即显示"] if key == "text_speed" else ["窗口", "全屏", "无边框"]
				var selected := maxi(0, allowed.find(value))
				option.set_meta("selection", selected)
				option.text = str(captions[selected]) + "　›"
				option.pressed.connect(func():
					var index := (int(option.get_meta("selection")) + 1) % allowed.size()
					option.set_meta("selection", index)
					option.text = str(captions[index]) + "　›"
					settings.set_value(section, key, allowed[index])
				)
				container.add_child(option)
	var label := RichTextLabel.new()
	label.bbcode_enabled = true
	label.fit_content = true
	label.text = "[b]操作说明[/b]\n\n• [b]WASD / 方向键 / 鼠标左键[/b]：在当前房间内移动接近\n• [b]鼠标悬停 / 左键点击[/b]：交互现场热点、触发物件或对话\n• [b]空格键 / 回车键[/b]：继续对白\n• [b]ESC 键[/b]：关闭弹窗或返回"
	container.add_child(label)
