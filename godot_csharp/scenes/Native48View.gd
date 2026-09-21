extends Node2D

func _ready() -> void:
	if get_viewport()==get_tree().root:
		get_window().title=name+" — Native 48px"
		get_window().content_scale_mode=Window.CONTENT_SCALE_MODE_DISABLED
		get_window().size=Vector2i(1248,768)
		get_window().move_to_center()
