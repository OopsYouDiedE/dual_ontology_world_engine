extends Node2D

func _ready() -> void:
	if get_viewport() == get_tree().root:
		var window := get_window()
		window.title = "Shot 01 — Academy Central Hall"
		window.content_scale_size = Vector2i(1024, 640)
		window.size = Vector2i(1024, 640)
		window.move_to_center()
