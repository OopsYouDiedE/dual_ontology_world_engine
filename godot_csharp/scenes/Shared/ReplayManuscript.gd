@tool
extends HBoxContainer
## Shared manuscript: authored page structure and repeatable question/finding templates.
const Paper := preload("res://scenes/Shared/UI/Paper.gd")

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
@export var default_title: String = "重演推演":
	set(value):
		default_title = value
		_update_preview()
@export var default_hint: String = "设定变量后，可与现场留下的痕迹对照。":
	set(value):
		default_hint = value
		_update_preview()
@export var preview_questions: Array[Dictionary] = [{"question": "示例变量设定", "options": ["选项 A", "选项 B"]}]:
	set(value):
		preview_questions = value
		_update_preview()

@export var preview_findings: Array[Dictionary] = []:
	set(value):
		preview_findings = value
		_update_preview()

@export var unset_hint: String = "还有变量没有设定。"
@export var stale_hint: String = "变量已改动：下面是上一次重演的结果，已过期。"
@export var status_labels: Dictionary = {"match": "[color=#3f5a3a]吻合[/color]", "conflict": "[color=#8a3324]冲突[/color]", "unvisited": "[color=#6b5d48]未查[/color]"}

var host: Node
var selection: Array = []
var option_buttons: Array = []
var run_buttons: Array = []
@onready var results_box: VBoxContainer = $RightPage/Scroll/Column/Results
@onready var stale_label: RichTextLabel = $RightPage/Scroll/Column/Stale

func _ready() -> void:
	add_theme_constant_override("separation", page_separation)
	if page_bg_color != Color("ebe2c8") or page_margin != 18:
		_update_page_styles()
	if Engine.is_editor_hint() or host == null:
		_update_preview()
		return
	selection = host.replay_vars.duplicate()
	$LeftPage/Scroll/Column/Title.text = "[b]%s[/b]" % (host.replay_title() if host.has_method("replay_title") else default_title)
	$LeftPage/Scroll/Column/Hint.text = host.replay_hint() if host.has_method("replay_hint") else default_hint
	_build_questions(host.replay_questions() if host.has_method("replay_questions") else [], false)
	var actions := $LeftPage/Scroll/Column/Actions
	run_buttons = [actions.get_node("RunPlayback"), actions.get_node("RunSkip")]
	_connect_button(run_buttons[0], _run.bind(false))
	_connect_button(run_buttons[1], _run.bind(true))
	_connect_button(actions.get_node("Close"), host.close_modal as Callable)
	_refresh()
	if host.has_method("replay_opened"):
		host.replay_opened()

func _connect_button(button: Button, action: Callable) -> void:
	button.pressed.connect(func(): Paper.Sound.sfx("ui_click", -4.0))
	if action.is_valid():
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
	if not is_node_ready() or (not Engine.is_editor_hint() and host != null):
		return
	$LeftPage/Scroll/Column/Title.text = "[b]%s[/b]" % default_title
	$LeftPage/Scroll/Column/Hint.text = default_hint
	_build_questions(preview_questions, true)
	for child in results_box.get_children():
		results_box.remove_child(child)
		child.queue_free()
	if preview_findings.is_empty():
		var empty: RichTextLabel = $Templates/EmptyResult.duplicate()
		empty.visible = true
		results_box.add_child(empty)
	for finding in preview_findings:
		var row: VBoxContainer = $Templates/Finding.duplicate()
		row.visible = true
		results_box.add_child(row)
		row.get_node("Heading").text = "%s　[b]%s[/b]" % [finding.get("status", "吻合"), finding.get("name", "痕迹")]
		row.get_node("Detail").text = str(finding.get("text", ""))
		row.get_node("OpenCard").visible = false

func _build_questions(questions: Array, preview: bool) -> void:
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
		var mark: String = host.replay_mark(index) if not preview and host.has_method("replay_mark") else ""
		var fresh := "　[color=#8a3324]（新）[/color]" if not preview and host.has_method("replay_is_new") and host.replay_is_new(index) else ""
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
			if not preview:
				_connect_button(button, _choose.bind(index, option))
			buttons.append(button)
		option_buttons.append(buttons)

func _choose(index: int, option: int) -> void:
	selection[index] = option
	if host != null:
		host.replay_vars = selection.duplicate()
	_refresh()

func _refresh() -> void:
	for index in option_buttons.size():
		for option in option_buttons[index].size():
			option_buttons[index][option].button_pressed = selection[index] == option
	var ready := not selection.slice(0, option_buttons.size()).has(-1)
	for b in run_buttons:
		b.disabled = not ready
	for child in results_box.get_children():
		child.queue_free()
	var result: Dictionary = host.replay_result if host != null and "replay_result" in host else {}
	if result.is_empty():
		var empty: RichTextLabel = $Templates/EmptyResult.duplicate()
		empty.visible = true
		results_box.add_child(empty)
		stale_label.text = "" if ready else unset_hint
		return
	var stale: bool = (host.last_run_vars != selection) if host != null and "last_run_vars" in host else false
	stale_label.text = stale_hint if stale else ""
	if bool(result.get("full_match", false)) and not stale:
		var match_label: RichTextLabel = $Templates/FullMatch.duplicate()
		match_label.visible = true
		results_box.add_child(match_label)
	var findings: Array = result.get("findings", [])
	for finding in findings:
		var status: String = finding.status
		var chip: String = str(status_labels.get(status, status))
		var name: String = host.finding_title(finding) if host != null and host.has_method("finding_title") else str(finding.get("name", ""))
		var row: VBoxContainer = $Templates/Finding.duplicate()
		row.visible = true
		results_box.add_child(row)
		var head: RichTextLabel = row.get_node("Heading")
		head.text = "%s　[b]%s[/b]" % [chip, name]
		head.add_theme_color_override("default_color", Color(Paper.INK, 0.55) if stale else Paper.INK)
		var detail: RichTextLabel = row.get_node("Detail")
		detail.visible = status != "match"
		detail.text = str(finding.text)
		detail.add_theme_color_override("default_color", Color(Paper.INK, 0.5) if stale else Paper.INK)
		var card: Callable = host.finding_card(finding) if host != null and host.has_method("finding_card") else Callable()
		var open: Button = row.get_node("OpenCard")
		open.visible = status != "unvisited" and card.is_valid()
		if open.visible:
			_connect_button(open, card)

func _run(skip: bool) -> void:
	if host == null or selection.slice(0, option_buttons.size()).has(-1):
		return
	var result: Dictionary = host.run_replay(selection)
	host.last_run_vars = selection.duplicate()
	if skip:
		host.after_replay(result)
	else:
		host.play_replay(result)
