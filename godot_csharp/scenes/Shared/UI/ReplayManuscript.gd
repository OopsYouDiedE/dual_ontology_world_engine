extends HBoxContainer
## 重演手稿。左页：六个变量（处境与人心）；右页：对照结果（吻合／冲突／未查）。
## 玩家从不指定任何人“做了什么”；行动由 C# 引擎从各人的认知与习惯推出。

const Paper := preload("res://scenes/Shared/UI/Paper.gd")

var host: Node
var data: Dictionary
var evidence: Dictionary
var selection: Array = []
var option_buttons: Array = []
var results_box: VBoxContainer
var run_buttons: Array = []
var stale_label: RichTextLabel

func _ready() -> void:
	add_theme_constant_override("separation", 26)
	data = host.replay_ui_data()
	evidence = host.replay_ui_evidence()
	selection = host.replay_vars.duplicate()
	var left := _page()
	var right := _page()
	_build_variables(left)
	_build_results(right)
	_refresh()
	host.replay_ui_opened()

func _page() -> VBoxContainer:
	var panel := PanelContainer.new()
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	var page := StyleBoxFlat.new()
	page.bg_color = Color("ebe2c8")
	page.border_color = Color(Paper.INK, 0.25)
	page.border_width_right = 1
	page.set_content_margin_all(18)
	panel.add_theme_stylebox_override("panel", page)
	add_child(panel)
	var scroll := ScrollContainer.new()
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	panel.add_child(scroll)
	var column := VBoxContainer.new()
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	column.add_theme_constant_override("separation", 9)
	scroll.add_child(column)
	return column

func _build_variables(column: VBoxContainer) -> void:
	column.add_child(Paper.label("[b]昨夜重演[/b]", 25))
	column.add_child(Paper.label("设定当时的处境与人心，让昨夜重演一次。", 17, Paper.MUTED))
	column.add_child(Paper.rule())
	var count: int = data.count
	for index in count:
		var variable: Dictionary = data.variables[index]
		var mark := ""
		if data.marks.has(str(index + 1)):
			mark = "　[color=#8a3324]✓ 回忆所证（%s）[/color]" % data.marks[str(index + 1)]
		var fresh := "　[color=#8a3324]（新）[/color]" if data.fresh.has(index) else ""
		var number: String = ["一", "二", "三", "四", "五", "六"][index] if index < 6 else str(index + 1)
		column.add_child(Paper.label("%s　%s%s%s" % [number, variable.question, mark, fresh], 19))
		if variable.has("context") and not String(variable.context).is_empty():
			column.add_child(Paper.label(String(variable.context), 16, Paper.MUTED))
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
	actions.add_child(spacer)
	actions.add_child(Paper.button("合上（R）", host.close_modal))

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
	host.replay_vars = selection.duplicate()
	_refresh()

func _refresh() -> void:
	for index in option_buttons.size():
		for option in option_buttons[index].size():
			option_buttons[index][option].button_pressed = selection[index] == option
	var ready: bool = host.replay_ui_ready(selection)
	for b in run_buttons:
		b.disabled = not ready
	for child in results_box.get_children():
		child.queue_free()
	var result: Dictionary = host.replay_result
	if result.is_empty():
		results_box.add_child(Paper.label("按下重演后，这里逐件列出重演留下的痕迹与现场是否一致。", 18, Paper.MUTED))
		stale_label.text = "" if ready else str(data.ready_hint)
		return
	var stale: bool = host.last_run_vars != selection
	stale_label.text = "变量已改动：下面是上一次重演的结果，已过期。" if stale else ""
	if bool(result.full_match) and not stale:
		results_box.add_child(Paper.label("[b]" + str(data.full_match_text) + "[/b]", 21, Paper.MATCH))
	for finding in result.findings:
		var status: String = finding.status
		var chip: String = {"match": "[color=#3f5a3a]吻合[/color]", "conflict": "[color=#8a3324]冲突[/color]", "unvisited": "[color=#6b5d48]未查[/color]"}[status]
		var name: String = evidence[finding.id].name if status != "unvisited" else str(finding.place)
		var row := VBoxContainer.new()
		row.add_theme_constant_override("separation", 2)
		results_box.add_child(row)
		var head := Paper.label("%s　[b]%s[/b]" % [chip, name], 18, Color(Paper.INK, 0.55) if stale else Paper.INK)
		row.add_child(head)
		if status != "match" and (bool(data.show_empty_finding) or not str(finding.text).is_empty()):
			row.add_child(Paper.label(str(finding.text), 17, Color(Paper.INK, 0.5) if stale else Paper.INK))
		if status != "unvisited":
			var open := Paper.button("看证物卡", host.open_card.bind(str(finding.id)), 15)
			open.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
			row.add_child(open)

func _run(skip: bool) -> void:
	if not host.replay_ui_ready(selection):
		return
	host.replay_ui_run(selection, skip)
