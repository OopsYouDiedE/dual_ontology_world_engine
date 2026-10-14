@tool
extends CanvasLayer
## 回放抬头、操作提示与门状态；静态布局和样式直接编辑子节点。

@export_group("编辑器预览")
@export_multiline var preview_caption := "":
	set(value):
		preview_caption = value
		_update_preview()
@export var preview_door := "":
	set(value):
		preview_door = value
		_update_preview()

func _ready() -> void:
	_update_preview()

func _update_preview() -> void:
	if not is_inside_tree() or not Engine.is_editor_hint():
		return
	$Bar/Caption.text = preview_caption
	$Door.text = preview_door
