extends Control
## 标题：选章。第一章结束时也可直接进入第二章。

const Paper := preload("res://scenes/Shared/UI/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")

func _ready() -> void:
	theme = Paper.theme()
	Sound.music("main_theme", 2.5)
	var bg := ColorRect.new()
	bg.color = Color("1d1a1f")
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(bg)
	var box := VBoxContainer.new()
	box.set_anchors_preset(Control.PRESET_CENTER)
	box.add_theme_constant_override("separation", 14)
	add_child(box)
	box.add_child(Paper.label("[center]安眠者有福[/center]", 44, Color("e6dcbf")))
	box.add_child(Paper.label("[center]Blessed Are the Sleepers[/center]", 16, Color("a89c84")))
	box.add_child(Control.new())
	box.add_child(Paper.button("第一章　门的这一侧", func(): get_tree().change_scene_to_file("res://scenes/Main2D.tscn"), 20))
	box.add_child(Paper.button("第二章", func(): get_tree().change_scene_to_file("res://scenes/Chapter2/Game/Chapter2Game.tscn"), 20))
	box.add_child(Paper.button("退出", func(): get_tree().quit(), 18))
	await get_tree().process_frame
	box.position = (get_viewport_rect().size - box.size) / 2
