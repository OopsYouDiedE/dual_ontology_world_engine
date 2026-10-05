@tool
extends HBoxContainer
## 重演手稿（两章共用，设计总纲 §四.4）。左页：变量（处境与人心）；右页：对照结果（吻合／冲突／未查）。
## 玩家从不指定任何人“做了什么”；行动由 C# 引擎从各人的认知与习惯推出。
## 宿主提供：replay_vars、replay_result、last_run_vars、replay_questions()、replay_mark(i)、replay_is_new(i)、
## replay_title()、replay_hint()、replay_opened()、finding_title(f)、finding_card(f)、run_replay、play_replay、after_replay、close_modal。

const Paper := preload("res://scenes/Shared/UI/Paper.gd")

@export var page_separation: int = 26:
	set(value):
		page_separation = value
		add_theme_constant_override("separation", page_separation)

@export var page_bg_color: Color = Color("ebe2c8"):
	set(value):
		page_bg_color = value
		_update_page_styles()

@export var page_margin: int = 18:
	set(value):
		page_margin = value
		_update_page_styles()

@export var default_title: String = "重演推演"
@export var default_hint: String = "设定变量后，可与现场留下的痕迹对照。"

var host: Node
var selection: Array = []
var option_buttons: Array = []
var results_box: VBoxContainer
var run_buttons: Array = []
var stale_label: RichTextLabel
var _panels: Array[PanelContainer] = []

func _ready() -> void:
	add_theme_constant_override("separation", page_separation)
	if Engine.is_editor_hint() or host == null:
		_build_preview()
		return
	selection = host.replay_vars.duplicate()
	var left := _page()
	var right := _page()
	_build_variables(left)
	_build_results(right)
	_refresh()
	if host != null and host.has_method("replay_opened"):
		host.replay_opened()

func _update_page_styles() -> void:
	for panel in _panels:
		if is_instance_valid(panel):
			var page := StyleBoxFlat.new()
			page.bg_color = page_bg_color
			page.border_color = Color(Paper.INK, 0.25)
			page.border_width_right = 1
			page.set_content_margin_all(page_margin)
			panel.add_theme_stylebox_override("panel", page)

func _page() -> VBoxContainer:
	var panel := PanelContainer.new()
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	var page := StyleBoxFlat.new()
	page.bg_color = page_bg_color
	page.border_color = Color(Paper.INK, 0.25)
	page.border_width_right = 1
	page.set_content_margin_all(page_margin)
	panel.add_theme_stylebox_override("panel", page)
	add_child(panel)
	_panels.append(panel)
	var scroll := ScrollContainer.new()
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	panel.add_child(scroll)
	var column := VBoxContainer.new()
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	column.add_theme_constant_override("separation", 9)
	scroll.add_child(column)
	return column

func _build_preview() -> void:
	var left := _page()
	var right := _page()
	left.add_child(Paper.label("[b]%s[/b]" % default_title, 25))
	left.add_child(Paper.label(default_hint, 17, Paper.MUTED))
	left.add_child(Paper.rule())
	left.add_child(Paper.label("一　示例变量设定", 19))
	var row := HFlowContainer.new()
	row.add_child(Paper.button("选项 A", func(): pass, 17))
	row.add_child(Paper.button("选项 B", func(): pass, 17))
	left.add_child(row)
	right.add_child(Paper.label("[b]与现场对照[/b]", 25))
	right.add_child(Paper.rule())
	right.add_child(Paper.label("重演结果将在右侧呈现。", 18, Paper.MUTED))

func _build_variables(column: VBoxContainer) -> void:
	var title_text: String = host.replay_title() if host != null and host.has_method("replay_title") else default_title
	var hint_text: String = host.replay_hint() if host != null and host.has_method("replay_hint") else default_hint
	column.add_child(Paper.label("[b]%s[/b]" % title_text, 25))
	column.add_child(Paper.label(hint_text, 17, Paper.MUTED))
	column.add_child(Paper.rule())
	var questions: Array = host.replay_questions() if host != null and host.has_method("replay_questions") else []
	for index in questions.size():
		var variable: Dictionary = questions[index]
		var mark: String = host.replay_mark(index) if host.has_method("replay_mark") else ""
		var fresh := "　[color=#8a3324]（新）[/color]" if (host.has_method("replay_is_new") and host.replay_is_new(index)) else ""
		column.add_child(Paper.label("%s　%s%s%s" % [["一", "二", "三", "四", "五", "六"][index], variable.question, mark, fresh], 19))
		var row := HFlowContainer.new()
		row.add_theme_constant_override("h_separation", 8)
		row.add_theme_constant_override("v_separation", 6)
		column.add_child(row)
		var buttons := []
		for option in variable.options.size():
			var text: String = variable.options[option]
			var b := Paper.button(text, _choose.bind(index, option), 17)
			b.toggle_mode = true
			buttons.append(b)
			row.add_child(b)
		option_buttons.append(buttons)
	column.add_child(Paper.rule())
	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 10)
	column.add_child(actions)
	run_buttons = [Paper.button("重演，看回放", _run.bind(false)), Paper.button("重演，跳过回放", _run.bind(true))]
	for b in run_buttons:
		actions.add_child(b)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var close_fn: Callable = (host.close_modal as Callable) if (host != null and "close_modal" in host) else Callable()
	actions.add_child(Paper.button("合上（R）", close_fn))

func _build_results(column: VBoxContainer) -> void:
	column.add_child(Paper.label("[b]与现场对照[/b]", 25))
	stale_label = Paper.label("", 16, Paper.SEAL)
	column.add_child(stale_label)
	column.add_child(Paper.rule())
	results_box = VBoxContainer.new()
	results_box.add_theme_constant_override("separation", 8)
	column.add_child(results_box)

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
		results_box.add_child(Paper.label("按下重演后，这里逐件列出重演留下的痕迹与现场是否一致。", 18, Paper.MUTED))
		stale_label.text = "" if ready else "还有变量没有设定。"
		return
	var stale: bool = (host.last_run_vars != selection) if host != null and "last_run_vars" in host else false
	stale_label.text = "变量已改动：下面是上一次重演的结果，已过期。" if stale else ""
	if bool(result.get("full_match", false)) and not stale:
		results_box.add_child(Paper.label("[b]全部吻合。[/b]", 21, Paper.MATCH))
	var findings: Array = result.get("findings", [])
	for finding in findings:
		var status: String = finding.status
		var chip: String = {"match": "[color=#3f5a3a]吻合[/color]", "conflict": "[color=#8a3324]冲突[/color]", "unvisited": "[color=#6b5d48]未查[/color]"}[status]
		var name: String = host.finding_title(finding) if host != null and host.has_method("finding_title") else str(finding.get("name", ""))
		var row := VBoxContainer.new()
		row.add_theme_constant_override("separation", 2)
		results_box.add_child(row)
		var head := Paper.label("%s　[b]%s[/b]" % [chip, name], 18, Color(Paper.INK, 0.55) if stale else Paper.INK)
		row.add_child(head)
		if status != "match":
			row.add_child(Paper.label(str(finding.text), 17, Color(Paper.INK, 0.5) if stale else Paper.INK))
		var card: Callable = host.finding_card(finding) if host != null and host.has_method("finding_card") else Callable()
		if status != "unvisited" and card.is_valid():
			var open := Paper.button("看证物卡", card, 15)
			open.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
			row.add_child(open)

func _run(skip: bool) -> void:
	if host == null or selection.slice(0, option_buttons.size()).has(-1):
		return
	var result: Dictionary = host.run_replay(selection)
	host.last_run_vars = selection.duplicate()
	if skip:
		host.after_replay(result)
	else:
		host.play_replay(result)
