@tool
extends VBoxContainer
## 证物 / 痕迹 / 条文 / 对照表卡片。所有章节共用同一张卡。
## show_data({
##   title:String, picture:String|Texture2D ("res://" 路径或空), intro:String, facts:String, source:String,
##   notes:[String]  (语义色由剧本标注),
##   items:[String]  (逐条列出的行，例如张贴的条文),
##   table:[[String]]  (首行为表头，列数取首行长度),
##   inline:[{id, label, available, lock_hint}]  (卡片内的选项按钮)
## })
## 信号 chosen(id) 来自 inline 选项。
const Paper := preload("res://scenes/UI/Components/Paper.gd")

signal chosen(id: String)

@export var table_has_header := true:
	set(value):
		table_has_header = value
		_render_preview_or_data()

@export_group("Editor Preview")
@export var preview_picture: Texture2D:
	set(value):
		preview_picture = value
		_render_preview_or_data()
@export_multiline var preview_intro := "":
	set(value):
		preview_intro = value
		_render_preview_or_data()
@export_multiline var preview_facts := "证物的可见事实。":
	set(value):
		preview_facts = value
		_render_preview_or_data()
@export var preview_source := "现场":
	set(value):
		preview_source = value
		_render_preview_or_data()
@export var preview_notes: PackedStringArray = PackedStringArray(["回忆：这个声音似曾相识。", "旁人没有提起过它。"]):
	set(value):
		preview_notes = value
		_render_preview_or_data()
@export var preview_items: PackedStringArray = PackedStringArray():
	set(value):
		preview_items = value
		_render_preview_or_data()
## 每行一行表格，单元格用「|」分隔，首行为表头。
@export var preview_table: PackedStringArray = PackedStringArray():
	set(value):
		preview_table = value
		_render_preview_or_data()
## 每行一个内嵌选项：「文案」或「文案|锁定提示」。
@export var preview_inline: PackedStringArray = PackedStringArray():
	set(value):
		preview_inline = value
		_render_preview_or_data()

var _data: Dictionary = {}

func _ready() -> void:
	$Inline.chosen.connect(func(id: String): chosen.emit(id))
	_render_preview_or_data()

func show_data(data: Dictionary) -> void:
	_data = data
	if is_node_ready():
		_render()

func _render_preview_or_data() -> void:
	if not is_node_ready():
		return
	if Engine.is_editor_hint():
		var table := []
		for line in preview_table:
			table.append(Array(line.split("|")))
		var inline := []
		for index in preview_inline.size():
			var parts := preview_inline[index].split("|")
			inline.append({"id": str(index), "label": parts[0], "available": parts.size() < 2, "lock_hint": parts[1] if parts.size() > 1 else ""})
		_data = {
			"title": "示例证物", "picture": preview_picture if preview_picture != null else "",
			"intro": preview_intro, "facts": preview_facts, "source": preview_source,
			"notes": Array(preview_notes), "items": Array(preview_items),
			"table": table, "inline": inline,
		}
	_render()

func _texture_of(picture: Variant) -> Texture2D:
	if picture is Texture2D:
		return picture
	var path := str(picture)
	if path != "" and ResourceLoader.exists(path):
		return load(path) as Texture2D
	return null

func _text_or_hide(node: RichTextLabel, text: String, prefix := "") -> void:
	node.visible = text != ""
	node.text = Paper.story_text(prefix + text) if text != "" else ""

func _render() -> void:
	# 报纸两栏（设计规范第六部分 §4.7）：左栏衬纸上的局部图与来源题注；右栏叠题（证物名）、说明、事实、笔记、条目；
	# 表格通栏。没有内容的栏目不出现。
	var texture := _texture_of(_data.get("picture", ""))
	$Row/Figure/Mount/Plate/Picture.texture = texture
	$Row/Figure.visible = texture != null
	$Row/Rule.visible = texture != null
	_text_or_hide($Row/Figure/Source, str(_data.get("source", "")), "来源　")
	if texture == null and str(_data.get("source", "")) != "":
		# 没有局部图时来源题注挪到正文末尾。
		_text_or_hide($Row/Figure/Source, "")
	var title := str(_data.get("title", ""))
	$Row/Texts/Title.text = title
	$Row/Texts/Title.visible = title != ""
	$Row/Texts/Dash.visible = title != ""
	_text_or_hide($Row/Texts/Intro, str(_data.get("intro", "")))
	_text_or_hide($Row/Texts/Facts, str(_data.get("facts", "")))
	var notes: VBoxContainer = $Row/Texts/Notes
	Paper.clear_children(notes)
	for note in _data.get("notes", []):
		notes.add_child(Paper.label(str(note), &"PaperLabelNote"))
	if texture == null and str(_data.get("source", "")) != "":
		notes.add_child(Paper.label("来源　" + str(_data.get("source", "")), &"PaperLabelRegister"))
	notes.visible = notes.get_child_count() > 0
	var items: VBoxContainer = $Row/Texts/Items
	Paper.clear_children(items)
	for item in _data.get("items", []):
		items.add_child(Paper.label(str(item)))
	items.visible = items.get_child_count() > 0
	var grid: GridContainer = $Table
	Paper.clear_children(grid)
	var rows: Array = _data.get("table", [])
	grid.visible = not rows.is_empty()
	if not rows.is_empty():
		grid.columns = maxi(1, (rows[0] as Array).size())
		for r in rows.size():
			for cell in rows[r]:
				if r == 0 and table_has_header:
					grid.add_child(Paper.label(str(cell), &"PaperLabelDeck"))
				else:
					grid.add_child(Paper.label(str(cell), &"PaperLabelNote"))
	var inline: Array = _data.get("inline", [])
	$Inline.visible = not inline.is_empty()
	$Inline.show_data({"prompt": "", "options": inline})
