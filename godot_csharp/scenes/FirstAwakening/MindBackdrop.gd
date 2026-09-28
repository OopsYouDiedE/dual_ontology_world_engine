extends TextureRect
## The native 640x496 paper recipe is displayed at exactly two pixels per dot.

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	texture = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/mind-notebook.png")
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	custom_minimum_size = Vector2(1280, 992)
	size = custom_minimum_size
