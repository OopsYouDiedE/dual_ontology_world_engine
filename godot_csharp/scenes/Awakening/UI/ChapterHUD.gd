@tool
extends CanvasLayer
## HUD 节点与样式直接在场景中编辑；运行文字由章节宿主绑定。
@export var preview_title: String = "":
	set(value):
		preview_title = value
		_preview()
@export var preview_keys: String = "":
	set(value):
		preview_keys = value
		_preview()
func _ready() -> void:
	_preview()
func _preview() -> void:
	if not Engine.is_editor_hint() or not is_inside_tree():
		return
	if get_node_or_null("Title"): $Title.text = preview_title
	if get_node_or_null("Keys"): $Keys.text = preview_keys
