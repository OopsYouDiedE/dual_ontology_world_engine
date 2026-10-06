@tool
extends Control
## 标题界面：选章预制件。允许在检视栏（Inspector）中配置标题文本与配色。

const Chapter2Engine := preload("res://scenes/Chapter2/Game/Chapter2Engine.cs")
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
			get_tree().change_scene_to_file("res://scenes/Main2D.tscn")
		)
	if is_instance_valid(ch2_button):
		var probe: Node = Chapter2Engine.new()
		var available: bool = probe.CanStart()
		probe.free()
		ch2_button.disabled = not available
		$MenuBox/Chapter2Hint.visible = not available
		ch2_button.pressed.connect(func():
			Sound.sfx("ui_click", -4.0)
			get_tree().change_scene_to_file("res://scenes/Chapter2/Game/Chapter2Game.tscn")
		)
	if is_instance_valid(quit_button):
		quit_button.pressed.connect(func():
			Sound.sfx("ui_click", -4.0)
			Sound.quit_cleanly()
		)
