@tool
extends VBoxContainer
## 选择菜单：可选的提示语 + 一列纸页按钮。所有章节共用。
## show_data({prompt:String, options:[{id:String, label:String, available:bool, lock_hint:String}]})
## 不可选的按钮禁用，lock_hint 作为悬停提示；点击发出 chosen(id)。
const Paper := preload("res://scenes/UI/Components/Paper.gd")

signal chosen(id: String)

@export_group("Editor Preview")
@export_multiline var preview_prompt := "你要做什么？":
	set(value):
		preview_prompt = value
		_render_preview_or_data()
## 每行一个选项：「文案」或「文案|锁定提示」（带锁定提示的选项禁用）。
@export var preview_options: PackedStringArray = PackedStringArray(["看一看这件东西", "问问旁边的人", "暂时还不能选|还缺一条线索"]):
	set(value):
		preview_options = value
		_render_preview_or_data()

var _data: Dictionary = {}
var buttons: Dictionary = {}

func _ready() -> void:
	_render_preview_or_data()

func show_data(data: Dictionary) -> void:
	_data = data
	if is_node_ready():
		_render()

func button_for(id: String) -> Button:
	return buttons.get(id)

func _render_preview_or_data() -> void:
	if not is_node_ready():
		return
	if Engine.is_editor_hint():
		var options := []
		for index in preview_options.size():
			var parts := preview_options[index].split("|")
			options.append({"id": str(index), "label": parts[0], "available": parts.size() < 2, "lock_hint": parts[1] if parts.size() > 1 else ""})
		_data = {"prompt": preview_prompt, "options": options}
	_render()

func _render() -> void:
	var prompt := str(_data.get("prompt", ""))
	$Prompt.text = Paper.story_text(prompt)
	$Prompt.visible = prompt != ""
	var list: VBoxContainer = $Choices
	Paper.clear_children(list)
	buttons.clear()
	# 条目行（设计规范第六部分 §4.6）：左对齐、登记体序号；不可用项淡墨删除线，并在下方写出原因。
	var options: Array = _data.get("options", [])
	for index in options.size():
		var option: Dictionary = options[index]
		var id := str(option.get("id", ""))
		var b := Paper.entry(str(option.get("label", "")), func(): chosen.emit(id), Paper.roman(index + 1), &"ChoiceMenuOption")
		b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		b.disabled = not bool(option.get("available", true))
		b.tooltip_text = str(option.get("lock_hint", ""))
		list.add_child(b)
		buttons[id] = b
		if b.disabled and b.tooltip_text != "":
			var reason := Paper.label(b.tooltip_text, &"PaperLabelRegister")
			reason.name = "Reason_" + id.validate_node_name()
			list.add_child(reason)
