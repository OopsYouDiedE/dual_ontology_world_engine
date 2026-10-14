@tool
extends VBoxContainer
## 章末卡（设计规范第六部分 §4.7）：报纸头版式。图形位（picture，可空）、通栏标题、双线、居中副题（outcome），
## 各行下一条淡规线。界面不内置章节图形。
## show_data({title:String, picture:String|Texture2D, outcome:String, rows:[String]})
const Paper := preload("res://scenes/UI/Components/Paper.gd")

@export_group("Editor Preview")
@export_multiline var preview_outcome := "示例结局正文。":
	set(value):
		preview_outcome = value
		_render_preview_or_data()
@export var preview_rows: PackedStringArray = PackedStringArray(["[color=match]语义色 match：[/color]示例一行", "[color=hand]语义色 hand：[/color]示例一行", "[color=muted]语义色 muted：示例一行[/color]"]):
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
		_data = {"title": "章末", "outcome": preview_outcome, "rows": Array(preview_rows)}
	_render()

func _render() -> void:
	var picture = _data.get("picture", "")
	var texture: Texture2D = picture if picture is Texture2D else (load(str(picture)) as Texture2D if str(picture) != "" and ResourceLoader.exists(str(picture)) else null)
	$Figure.texture = texture
	$Figure.visible = texture != null
	var title := str(_data.get("title", ""))
	$Title.text = title
	$Title.visible = title != ""
	$TitleRule.visible = title != ""
	var outcome := str(_data.get("outcome", ""))
	$Outcome.text = "[center]%s[/center]" % Paper.story_text(outcome)
	$Outcome.visible = outcome != ""
	var rows: VBoxContainer = $Rows
	Paper.clear_children(rows)
	for row in _data.get("rows", []):
		rows.add_child(Paper.label(str(row), &"PaperLabelNote"))
		rows.add_child(Paper.rule())
