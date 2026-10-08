@tool
extends VBoxContainer
## 表单：文本框与单选按钮组，所有章节共用（取名、声路实验、答辩选择……）。
## show_data({
##   prompt:String,
##   header:{question:String, instructions:String}  (可选；固定在表单顶部),
##   layout:"stack"|"tabs",   stack：全部字段依次堆叠；tabs：同时只显示一个字段（show_field 切换）,
##   fields:[{id, kind:"text"|"choice", heading, placeholder, value, max_length, flow:bool, empty_text,
##            options:[{id, label}]}],
##   summary:[{field:String, label:String}]  (头部逐行显示「label：所选项文案或 unset_label」),
##   unset_label:String
## })
const Paper := preload("res://scenes/UI/Components/Paper.gd")

signal changed
signal submit_requested

@export_group("Editor Preview")
@export_enum("stack", "tabs") var preview_layout := "stack":
	set(value):
		preview_layout = value
		_render_preview_or_data()
@export_multiline var preview_prompt := "":
	set(value):
		preview_prompt = value
		_render_preview_or_data()
@export var preview_question := "":
	set(value):
		preview_question = value
		_render_preview_or_data()
@export var preview_instructions := "":
	set(value):
		preview_instructions = value
		_render_preview_or_data()
## 每行一个字段：「标题|选项1,选项2,选项3」为单选组，只有「标题」（无竖线）为文本框。
@export_multiline var preview_fields_text := "选一句话|你好,谁在那里,再说一遍\n选一个位置|门口,窗边,桌边":
	set(value):
		preview_fields_text = value
		_render_preview_or_data()
## tabs 布局下预览显示第几个字段。
@export var preview_active_tab := 0:
	set(value):
		preview_active_tab = value
		_render_preview_or_data()

var _data: Dictionary = {}
var _fields: Array = []
var _selected: Dictionary = {}
var _buttons: Dictionary = {}
var _edits: Dictionary = {}
var _groups: Dictionary = {}
var _active := ""

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
		var fields := []
		var summary := []
		var lines := preview_fields_text.split("\n", false)
		for index in lines.size():
			var parts := lines[index].split("|")
			var id := "f%d" % index
			if parts.size() < 2:
				fields.append({"id": id, "kind": "text", "heading": parts[0], "placeholder": "在此输入"})
			else:
				var options := []
				for k in parts[1].split(","):
					options.append({"id": k, "label": k})
				fields.append({"id": id, "kind": "choice", "heading": parts[0], "options": options, "flow": index > 0})
				summary.append({"field": id, "label": parts[0]})
		var header := {}
		if preview_question != "" or preview_instructions != "":
			header = {"question": preview_question, "instructions": preview_instructions}
		_data = {"prompt": preview_prompt, "header": header, "layout": preview_layout, "fields": fields, "summary": summary if not header.is_empty() else [], "unset_label": "（未选）"}
		_render()
		if preview_layout == "tabs" and fields.size() > 0:
			show_field(fields[clampi(preview_active_tab, 0, fields.size() - 1)].id)
		return
	_render()

func _render() -> void:
	var prompt := str(_data.get("prompt", ""))
	$Prompt.text = Paper.story_text(prompt)
	$Prompt.visible = prompt != ""
	var header: Dictionary = _data.get("header", {})
	var summary: Array = _data.get("summary", [])
	$Header.visible = not header.is_empty() or not summary.is_empty()
	$Header/Question.text = Paper.story_text(str(header.get("question", "")))
	$Header/Question.visible = str(header.get("question", "")) != ""
	$Header/Instructions.text = Paper.story_text(str(header.get("instructions", "")))
	$Header/Instructions.visible = str(header.get("instructions", "")) != ""
	var fields_box: VBoxContainer = $Fields
	Paper.clear_children(fields_box)
	_fields = _data.get("fields", [])
	_selected.clear()
	_buttons.clear()
	_edits.clear()
	_groups.clear()
	_active = ""
	for field in _fields:
		var id := str(field.get("id", ""))
		var kind := str(field.get("kind", "choice" if field.has("options") else "text"))
		var group := VBoxContainer.new()
		group.theme_type_variation = &"FormGroup"
		var heading := str(field.get("heading", ""))
		if heading != "":
			group.add_child(Paper.label(heading, &"PaperLabelSubheading"))
		if kind == "text":
			var edit := LineEdit.new()
			edit.custom_minimum_size = Vector2(0, 46)
			edit.theme_type_variation = &"FormLineEdit"
			edit.placeholder_text = str(field.get("placeholder", ""))
			var limit := int(field.get("max_length", 0))
			if limit > 0:
				edit.max_length = limit
			edit.text = str(field.get("value", ""))
			edit.text_changed.connect(func(_t: String): _on_changed())
			edit.text_submitted.connect(func(_t: String): submit_requested.emit())
			group.add_child(edit)
			_edits[id] = edit
		else:
			var options: Array = field.get("options", [])
			_selected[id] = str(field.get("value", ""))
			var valid_default := false
			for option in options:
				if str(option.get("id", "")) == _selected[id] and bool(option.get("available", true)):
					valid_default = true
			if not valid_default: _selected[id] = ""
			_buttons[id] = {}
			if options.is_empty() and str(field.get("empty_text", "")) != "":
				group.add_child(Paper.label(str(field.get("empty_text", "")), &"PaperLabelNoteMuted"))
			var list: Container = HFlowContainer.new() if bool(field.get("flow", false)) else VBoxContainer.new()
			list.theme_type_variation = &"FormOptionFlow" if list is HFlowContainer else &"FormOptionColumn"
			for option in options:
				var option_id := str(option.get("id", ""))
				var b := Paper.button(str(option.get("label", "")), func(): select(id, option_id))
				if not bool(field.get("flow", false)): b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
				b.toggle_mode = true
				b.disabled = not bool(option.get("available", true))
				b.tooltip_text = str(option.get("lock_hint", ""))
				b.button_pressed = option_id == _selected[id]
				list.add_child(b)
				_buttons[id][option_id] = b
			group.add_child(list)
		if _active == "":
			_active = id
		fields_box.add_child(group)
		_groups[id] = group
	_apply_layout()
	_refresh_summary()

func _apply_layout() -> void:
	var tabs := str(_data.get("layout", "stack")) == "tabs"
	for id in _groups:
		_groups[id].visible = not tabs or id == _active

func _refresh_summary() -> void:
	var lines := PackedStringArray()
	var unset := str(_data.get("unset_label", "（未选）"))
	for entry in _data.get("summary", []):
		var id := str(entry.get("field", ""))
		var shown := unset
		var picked := str(_edits[id].text).strip_edges() if _edits.has(id) else str(_selected.get(id, ""))
		if picked != "":
			shown = picked if _edits.has(id) else _label_of(id, picked)
		lines.append("%s：%s" % [str(entry.get("label", "")), shown])
	$Header/Summary.text = Paper.story_text("\n".join(lines))
	$Header/Summary.visible = not lines.is_empty()

func _label_of(field_id: String, option_id: String) -> String:
	for field in _fields:
		if str(field.get("id", "")) == field_id:
			for option in field.get("options", []):
				if str(option.get("id", "")) == option_id:
					return str(option.get("label", option_id))
	return option_id

func _on_changed() -> void:
	_refresh_summary()
	changed.emit()

## 选中某个单选项（等同点击）。
func select(field_id: String, option_id: String) -> void:
	if not _buttons.has(field_id) or not _buttons[field_id].has(option_id):
		return
	if _buttons[field_id][option_id].disabled: return
	for key in _buttons[field_id]:
		_buttons[field_id][key].set_pressed_no_signal(key == option_id)
	if _selected[field_id] == option_id:
		return
	_selected[field_id] = option_id
	_on_changed()

## tabs 布局下切换显示的字段。
func show_field(id: String) -> void:
	if not _groups.has(id):
		return
	_active = id
	_apply_layout()

func active_field() -> String:
	return _active

func button_for(field_id: String, option_id: String) -> Button:
	return _buttons.get(field_id, {}).get(option_id)

func line_edit_for(field_id: String) -> LineEdit:
	return _edits.get(field_id)

func focus_text() -> void:
	for id in _edits:
		if _edits[id].is_visible_in_tree():
			_edits[id].grab_focus()
			return

## 字段 id → 所选选项 id（单选）或文本框内容（文本）。
func values() -> Dictionary:
	var result := {}
	for id in _selected:
		result[id] = _selected[id]
	for id in _edits:
		result[id] = _edits[id].text
	return result

## required 中的字段都已选择/填写（文本去掉首尾空白后非空）。
func is_complete(required: Array) -> bool:
	var current := values()
	for id in required:
		if str(current.get(str(id), "")).strip_edges() == "":
			return false
	return true
