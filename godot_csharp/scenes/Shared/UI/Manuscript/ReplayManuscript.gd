@tool
extends HBoxContainer
## 重演手稿：左页设定变量，右页逐件对照现场痕迹。完全由数据驱动，不持有宿主引用。
## 运行时：宿主调用 show_data(data) 并监听信号；编辑器里由 Editor Preview 字段拼出同样的 Dictionary 走同一渲染路径。
##
## show_data({
##   title:String, hint:String,
##   questions:[{question:String, options:[String], mark:String, fresh:bool}],
##   vars:[int]  (每题当前所选选项序号，-1 为未设),
##   last_run_vars:[int]  (上一次重演时的设定),
##   result:{full_match:bool, findings:[{title, status:"match"|"conflict"|"unvisited", text, card_id}]}
## })
const Paper := preload("res://scenes/Shared/UI/Common/Paper.gd")

signal vars_changed(vars: Array)
signal run_requested(vars: Array, skip: bool)
signal open_card(card_id: String)
signal close_requested

@export var page_separation: int = 26:
	set(value):
		page_separation = value
		add_theme_constant_override("separation", value)
@export var page_bg_color: Color = Color("ebe2c8"):
	set(value):
		page_bg_color = value
		_update_page_styles()
@export var page_margin: int = 18:
	set(value):
		page_margin = value
		_update_page_styles()
@export var unset_hint: String = "还有变量没有设定。"
@export var stale_hint: String = "变量已改动：下面是上一次重演的结果，已过期。"
@export var status_labels: Dictionary = {"match": "[color=#3f5a3a]吻合[/color]", "conflict": "[color=#8a3324]冲突[/color]", "unvisited": "[color=#6b5d48]未查[/color]"}

@export_group("Editor Preview")
@export var default_title: String = "重演推演":
	set(value):
		default_title = value
		_update_preview()
@export var default_hint: String = "设定变量后，可与现场留下的痕迹对照。":
	set(value):
		default_hint = value
		_update_preview()
@export var preview_questions: Array[Dictionary] = [{"question": "示例变量设定", "options": ["选项 A", "选项 B"], "mark": "", "fresh": false}]:
	set(value):
		preview_questions = value
		_update_preview()
@export var preview_vars: Array[int] = []:
	set(value):
		preview_vars = value
		_update_preview()
@export var preview_last_run_vars: Array[int] = []:
	set(value):
		preview_last_run_vars = value
		_update_preview()
@export var preview_full_match := false:
	set(value):
		preview_full_match = value
		_update_preview()
## 每项：{title, status: match|conflict|unvisited, text, card_id}
@export var preview_findings: Array[Dictionary] = []:
	set(value):
		preview_findings = value
		_update_preview()

var selection: Array = []
var last_run_vars: Array = []
var option_buttons: Array = []
var run_buttons: Array = []
var _data: Dictionary = {}
@onready var results_box: VBoxContainer = $RightPage/Scroll/Column/Results
@onready var stale_label: RichTextLabel = $RightPage/Scroll/Column/Stale

func _ready() -> void:
	add_theme_constant_override("separation", page_separation)
	if page_bg_color != Color("ebe2c8") or page_margin != 18:
		_update_page_styles()
	var actions := $LeftPage/Scroll/Column/Actions
	run_buttons = [actions.get_node("RunPlayback"), actions.get_node("RunSkip")]
	_connect_button(run_buttons[0], func(): _run(false))
	_connect_button(run_buttons[1], func(): _run(true))
	_connect_button(actions.get_node("Close"), func(): close_requested.emit())
	if Engine.is_editor_hint():
		_update_preview()
	else:
		_render()

func show_data(data: Dictionary) -> void:
	_data = data
	if is_node_ready():
		_render()

func vars() -> Array:
	return selection.duplicate()

func _connect_button(button: Button, action: Callable) -> void:
	if Engine.is_editor_hint():
		return
	button.pressed.connect(func(): Paper.Sound.sfx("ui_click", -4.0))
	button.pressed.connect(action)

func _update_page_styles() -> void:
	if not is_node_ready():
		return
	for panel in [$LeftPage, $RightPage]:
		var page := panel.get_theme_stylebox("panel").duplicate() as StyleBoxFlat
		page.bg_color = page_bg_color
		page.set_content_margin_all(page_margin)
		panel.add_theme_stylebox_override("panel", page)

func _update_preview() -> void:
	if not is_node_ready() or not Engine.is_editor_hint():
		return
	var result := {}
	if not preview_findings.is_empty():
		result = {"full_match": preview_full_match, "findings": preview_findings}
	_data = {
		"title": default_title, "hint": default_hint,
		"questions": preview_questions,
		"vars": preview_vars, "last_run_vars": preview_last_run_vars,
		"result": result,
	}
	_render()

func _render() -> void:
	var questions: Array = _data.get("questions", [])
	selection = (_data.get("vars", []) as Array).duplicate()
	while selection.size() < questions.size():
		selection.append(-1)
	last_run_vars = (_data.get("last_run_vars", []) as Array).duplicate()
	$LeftPage/Scroll/Column/Title.text = "[b]%s[/b]" % str(_data.get("title", default_title))
	$LeftPage/Scroll/Column/Hint.text = str(_data.get("hint", default_hint))
	_build_questions(questions)
	_refresh()

func _build_questions(questions: Array) -> void:
	var column := $LeftPage/Scroll/Column/Questions
	for child in column.get_children():
		column.remove_child(child)
		child.queue_free()
	option_buttons.clear()
	for index in questions.size():
		var variable: Dictionary = questions[index]
		var question: VBoxContainer = $Templates/Question.duplicate()
		question.visible = true
		column.add_child(question)
		var mark := str(variable.get("mark", ""))
		var fresh := "　[color=#8a3324]（新）[/color]" if bool(variable.get("fresh", false)) else ""
		question.get_node("Prompt").text = Paper.story_text("%s　%s%s%s" % [["一", "二", "三", "四", "五", "六"][index] if index < 6 else str(index + 1), variable.get("question", ""), mark, fresh])
		var row: HFlowContainer = question.get_node("Options")
		var template: Button = row.get_node("Option")
		var buttons := []
		var options: Array = variable.get("options", [])
		for option in options.size():
			var button: Button = template.duplicate()
			button.visible = true
			button.text = str(options[option])
			row.add_child(button)
			_connect_button(button, _choose.bind(index, option))
			buttons.append(button)
		option_buttons.append(buttons)

func _choose(index: int, option: int) -> void:
	selection[index] = option
	vars_changed.emit(selection.duplicate())
	_refresh()

func _refresh() -> void:
	for index in option_buttons.size():
		for option in option_buttons[index].size():
			option_buttons[index][option].button_pressed = selection[index] == option
	var ready := not selection.slice(0, option_buttons.size()).has(-1)
	for b in run_buttons:
		b.disabled = not ready
	for child in results_box.get_children():
		results_box.remove_child(child)
		child.queue_free()
	var result: Dictionary = _data.get("result", {})
	if result.is_empty():
		var empty: RichTextLabel = $Templates/EmptyResult.duplicate()
		empty.visible = true
		results_box.add_child(empty)
		stale_label.text = "" if ready else unset_hint
		return
	var stale: bool = last_run_vars != selection
	stale_label.text = stale_hint if stale else ""
	if bool(result.get("full_match", false)) and not stale:
		var match_label: RichTextLabel = $Templates/FullMatch.duplicate()
		match_label.visible = true
		results_box.add_child(match_label)
	for finding in result.get("findings", []):
		var status := str(finding.get("status", "unvisited"))
		var chip: String = str(status_labels.get(status, status))
		var title := str(finding.get("title", finding.get("name", "")))
		var row: VBoxContainer = $Templates/Finding.duplicate()
		row.visible = true
		results_box.add_child(row)
		var head: RichTextLabel = row.get_node("Heading")
		head.text = "%s　[b]%s[/b]" % [chip, title]
		head.add_theme_color_override("default_color", Color(Paper.INK, 0.55) if stale else Paper.INK)
		var detail: RichTextLabel = row.get_node("Detail")
		detail.visible = status != "match"
		detail.text = Paper.story_text(str(finding.get("text", "")))
		detail.add_theme_color_override("default_color", Color(Paper.INK, 0.5) if stale else Paper.INK)
		var card_id := str(finding.get("card_id", ""))
		var open: Button = row.get_node("OpenCard")
		open.visible = status != "unvisited" and card_id != ""
		_connect_button(open, func(): open_card.emit(card_id))

func _run(skip: bool) -> void:
	if selection.slice(0, option_buttons.size()).has(-1):
		return
	run_requested.emit(selection.duplicate(), skip)
