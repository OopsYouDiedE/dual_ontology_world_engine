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
	{"heading": "[b]看过的[/b]", "style": "grid", "entries": [{"label": "窗台的水渍", "card_id": "E1"}, {"label": "桌上的钥匙", "card_id": "E2"}, {"label": "门锁", "card_id": "E3"}]},
	{"heading": "[b]读过、听过的规矩[/b]", "style": "list", "entries": [{"label": "夜里不得离开房间。"}, {"label": "（还没听说第二条）", "muted": true}]},
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

func _render() -> void:
	var sections: Array = _data.get("sections", [])
	var empty_text := str(_data.get("empty_text", ""))
	$Empty.text = Paper.story_text(empty_text)
	$Empty.visible = sections.is_empty() and empty_text != ""
	var box: VBoxContainer = $Sections
	Paper.clear_children(box)
	for index in sections.size():
		var section: Dictionary = sections[index]
		if index > 0:
			box.add_child(Paper.rule())
		var heading := str(section.get("heading", ""))
		if heading != "":
			box.add_child(Paper.label(heading, &"PaperLabelHeading"))
		var entries: Array = section.get("entries", [])
		var is_grid := str(section.get("style", "list")) == "grid"
		var holder: Container
		if is_grid:
			var grid := GridContainer.new()
			grid.columns = grid_columns
			grid.theme_type_variation = &"NotebookGrid"
			holder = grid
		else:
			var column := VBoxContainer.new()
			column.theme_type_variation = &"NotebookColumn"
			holder = column
		for entry in entries:
			var label := str(entry.get("label", ""))
			var card_id := str(entry.get("card_id", ""))
			if card_id != "":
				var b := Paper.button(label, func(): open_card.emit(card_id), &"NotebookEntryButton")
				b.alignment = HORIZONTAL_ALIGNMENT_LEFT
				b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
				holder.add_child(b)
			else:
				var l := Paper.label(label, &"PaperLabelNoteMuted" if bool(entry.get("muted", false)) else &"PaperLabelNote")
				holder.add_child(l)
		box.add_child(holder)
