@tool
extends VBoxContainer
## 页面由真实场景节点组成；预览数据只在编辑器内写入指定文字节点。
@export var preview_fields: Dictionary = {}:
	set(value):
		preview_fields = value
		_apply_preview()

func _ready() -> void:
	_apply_preview()

func _apply_preview() -> void:
	if not Engine.is_editor_hint() or not is_inside_tree():
		return
	for path in preview_fields:
		var target := get_node_or_null(NodePath(str(path)))
		if target is RichTextLabel or target is Label or target is LineEdit:
			target.text = str(preview_fields[path])
