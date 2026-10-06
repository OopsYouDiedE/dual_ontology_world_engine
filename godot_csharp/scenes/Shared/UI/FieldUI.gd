@tool
extends CanvasLayer
## Layout, colors, fonts, shadows and textures belong to the scene's native nodes.
@export_group("Editor Preview")
@export var preview_enabled := true:
	set(value):
		preview_enabled = value
		_refresh_preview()
@export var preview_title := "当前地点":
	set(value):
		preview_title = value
		_refresh_preview()
@export var preview_time := "07:20":
	set(value):
		preview_time = value
		_refresh_preview()
@export var preview_hint := "WASD 移动　E 检视　Tab 笔记本":
	set(value):
		preview_hint = value
		_refresh_preview()

func _ready() -> void:
	_refresh_preview()

func _refresh_preview() -> void:
	if not Engine.is_editor_hint() or not is_inside_tree() or not preview_enabled: return
	$Title.text = preview_title
	$Time.text = preview_time
	$Hint.text = preview_hint

func set_title(text: String) -> void:
	$Title.text = text

func set_time(text: String) -> void:
	$Time.text = text

func set_hint(text: String) -> void:
	$Hint.text = text
