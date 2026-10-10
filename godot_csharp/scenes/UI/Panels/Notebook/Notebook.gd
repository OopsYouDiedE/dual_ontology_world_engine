@tool
extends VBoxContainer
## 笔记本：按小节列出看过、听过、记下的东西。所有章节共用。
## show_data({
##   empty_text:String  (没有任何小节时显示),
##   sections:[{heading:String (可含 BBCode), style:"grid"|"list",
##              entries:[{label:String, card_id:String (可选；有则为可点按钮), muted:bool}]}]
## })
## 信号 open_card(card_id)。
const Paper := preload("res://scenes/UI/Components/Paper.gd")

signal open_card(card_id: String)

@export var grid_columns := 2:
	set(value):
		grid_columns = value
		_render_preview_or_data()

@export_group("Editor Preview")
@export var preview_empty_text := "还没有记下任何东西。":
	set(value):
		preview_empty_text = value
		_render_preview_or_data()
@export var preview_sections: Array[Dictionary] = [
	{"heading": "地点一", "style": "grid", "entries": [{"label": "示例证物一", "card_id": "S1"}, {"label": "示例证物二", "card_id": "S2"}, {"label": "示例证物三", "card_id": "S3"}]},
	{"heading": "规矩", "style": "list", "entries": [{"label": "示例规矩一条。"}, {"label": "（尚未得知的一条）", "muted": true}]},
]:
	set(value):
		preview_sections = value
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
		_data = {"empty_text": preview_empty_text, "sections": preview_sections}
	_render()

## 上次翻到的分组；同一局里再打开笔记本时停在同一张索引签。
static var _current_heading := ""

func _render() -> void:
	# 卡盒（设计规范第六部分 §4.6）：每个分组一张侧边索引签，点签换组；条目用条目行。
	var sections: Array = _data.get("sections", [])
	var empty_text := str(_data.get("empty_text", ""))
	$Empty.text = Paper.story_text(empty_text)
	$Empty.visible = sections.is_empty() and empty_text != ""
	var box: VBoxContainer = $Sections
	Paper.clear_children(box)
	if sections.is_empty():
		return
	var current := 0
	for index in sections.size():
		if _plain(str(sections[index].get("heading", ""))) == _current_heading: current = index
	if sections.size() == 1:
		box.add_child(_page(sections[0]))
		return
	var layout := HBoxContainer.new()
	layout.theme_type_variation = &"NotebookLayout"
	box.add_child(layout)
	var tabs := VBoxContainer.new()
	tabs.theme_type_variation = &"NotebookTabs"
	tabs.custom_minimum_size.x = 192
	layout.add_child(tabs)
	var group := ButtonGroup.new()
	for index in sections.size():
		var heading := _plain(str(sections[index].get("heading", "")))
		var tab := Paper.button(heading if heading != "" else str(index + 1), func():
			_current_heading = heading
			_render(), &"PaperIndexTab")
		tab.toggle_mode = true
		tab.button_group = group
		tab.alignment = HORIZONTAL_ALIGNMENT_LEFT
		tab.set_pressed_no_signal(index == current)
		tabs.add_child(tab)
	var page := PanelContainer.new()
	page.theme_type_variation = &"NotebookPage"
	page.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	page.add_child(_page(sections[current]))
	layout.add_child(page)

func _page(section: Dictionary) -> Control:
	var column := VBoxContainer.new()
	column.theme_type_variation = &"PaperModalBody"
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var heading := str(section.get("heading", ""))
	if heading != "":
		var head := Paper.pixel(_plain(heading), &"PixelColumn")
		head.align = "left"
		column.add_child(head)
		column.add_child(Paper.rule())
	var entries: Array = section.get("entries", [])
	var holder: Container
	if str(section.get("style", "list")) == "grid":
		var grid := GridContainer.new()
		grid.columns = grid_columns
		grid.theme_type_variation = &"NotebookGrid"
		holder = grid
	else:
		var list := VBoxContainer.new()
		list.theme_type_variation = &"NotebookColumn"
		holder = list
	for index in entries.size():
		var entry: Dictionary = entries[index]
		var label := str(entry.get("label", ""))
		var card_id := str(entry.get("card_id", ""))
		if card_id != "":
			var b := Paper.entry(label, func(): open_card.emit(card_id), Paper.roman(index + 1), &"NotebookEntryButton")
			b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			holder.add_child(b)
		else:
			var l := Paper.label(label, &"PaperLabelNoteMuted" if bool(entry.get("muted", false)) else &"PaperLabelNote")
			holder.add_child(l)
	column.add_child(holder)
	return column

static func _plain(text: String) -> String:
	return RegEx.create_from_string("\\[/?[a-z_]+(=[^\\]]*)?\\]").sub(text, "", true)
