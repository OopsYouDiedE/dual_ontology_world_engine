@tool
extends Control
## 标题界面：选章预制件。允许在检视栏（Inspector）中配置标题文本与配色。

const Sound := preload("res://scenes/Shared/Audio/Sound.gd")
@onready var ch1_button: Button = $MenuBox/Chapter1Button
@onready var ch2_button: Button = $MenuBox/Chapter2Button
@onready var quit_button: Button = $MenuBox/QuitButton

func _ready() -> void:
	if Engine.is_editor_hint():
		return
	
	Sound.music("main_theme", 2.5)
	if is_instance_valid(ch1_button):
		ch1_button.pressed.connect(func():
			Sound.sfx("ui_click", -4.0)
			get_tree().change_scene_to_file("res://scenes/Awakening/Chapter1.tscn")
		)
	if is_instance_valid(ch2_button):
		# 第二章接手第一章运行中的世界；从标题直接进入时由第一章标准会话（含本地存档）显式初始化。
		$MenuBox/Chapter2Hint.visible = false
		ch2_button.pressed.connect(func():
			Sound.sfx("ui_click", -4.0)
			get_tree().change_scene_to_file("res://scenes/Chapter2/Chapter2.tscn")
		)
	if is_instance_valid(quit_button):
		quit_button.pressed.connect(func():
			Sound.sfx("ui_click", -4.0)
			Sound.quit_cleanly()
		)
