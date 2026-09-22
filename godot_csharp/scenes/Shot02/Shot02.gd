extends Node2D

func _ready() -> void:
	if get_viewport() == get_tree().root:
		get_window().title = "Shot 02 — Academy Breakfast"
		get_window().content_scale_size = Vector2i(1024, 608)
		get_window().size = Vector2i(1024, 608)
		get_window().move_to_center()
