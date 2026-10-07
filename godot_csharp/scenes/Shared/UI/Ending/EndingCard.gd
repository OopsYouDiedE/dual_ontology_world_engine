@tool
extends VBoxContainer
## 结束卡：一段正文，后接若干行说明（每行一个 RichTextLabel，可含语义色）。
## show_data({outcome:String, rows:[String]})
const Paper := preload("res://scenes/Shared/UI/Common/Paper.gd")

@export var row_font_size := 18:
	set(value):
		row_font_size = value
		_render_preview_or_data()

@export_group("Editor Preview")
@export_multiline var preview_outcome := "天亮了。名录库的门在你身后合上。":
	set(value):
		preview_outcome = value
		_render_preview_or_data()
@export var preview_rows: PackedStringArray = PackedStringArray(["[color=seal]留在恶魔那里的钩：[/color]一枚旧钥匙", "[color=match]你没有交出的：[/color]自己的名字", "[color=muted]按空格或 Enter 回到标题。[/color]"]):
	set(value):
		preview_rows = value
		_render_preview_or_data()

var _data: Dictionary = {}

func _ready() -> void:
	_render_preview_or_data()

func show_data(data: Dictionary) -> void:
	_data = data
	if is_node_ready():
		_render()

func _render_preview_or_data() -> void:
	if not is_node_ready():
		return
	if Engine.is_editor_hint():
		_data = {"outcome": preview_outcome, "rows": Array(preview_rows)}
	_render()

func _render() -> void:
	var outcome := str(_data.get("outcome", ""))
	$Outcome.text = Paper.story_text(outcome)
	$Outcome.visible = outcome != ""
	var rows: VBoxContainer = $Rows
	Paper.clear_children(rows)
	for row in _data.get("rows", []):
		rows.add_child(Paper.label(str(row), row_font_size, Paper.INK))
