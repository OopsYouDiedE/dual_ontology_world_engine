extends RefCounted
## Builds defense controls; the scene retains drafts and the bridge validates submissions.
const Content = preload("res://scenes/FirstAwakening/RedTokenContent.gd")
var source: Node

func setup(scene: Node) -> void:
	source = scene

func _defense_option_text(options: Array, value: String) -> String:
	for option in options:
		if option[0] == value: return str(option[1])
	return "未选择"

func _open_red_defense() -> void:
	source._open_modal("向铜口答辩：前提、替代解释与未知", "red")
	source.modal_box.add_theme_constant_override("separation", 8)
	var answer: Dictionary = source.red_defense_answer
	var known: Array = source.red.call("State").get("known", [])
	var material_names: Dictionary = Content.MATERIAL_NAMES
	var material_scope: Dictionary = Content.MATERIAL_SCOPE
	source._label(source.modal_box, "独立选择三项；草稿持续保存。", 18)
	var tabs: HBoxContainer = HBoxContainer.new()
	source.modal_box.add_child(tabs)
	var active: Array = []
	for section in Content.defense_sections():
		var section_id: String = section[0]
		var tab: Button = source._button(tabs, section[1], func():
			source.red_defense_section = section_id
			source.red_defense_hints = false
			_open_red_defense())
		tab.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		tab.toggle_mode = true
		tab.set_pressed_no_signal(section_id == source.red_defense_section)
		if section_id == source.red_defense_section: active = section
	var key: String = active[0]
	var materials: Array = [["unknown","暂不选材料"]]
	for material in active[2]:
		var all_known: bool = true
		for id in material.split("+"): all_known = all_known and id in known
		if all_known: materials.append([material, "%s · %s" % [material_names[material], material]])
	var alternatives: Array = [["unknown","暂不选解释"]]+active[3]
	var unknowns: Array = [["unknown","暂不选范围"]]+active[4]
	_defense_choice(key+"_material", materials, answer, source.modal_box, "1 · 材料")
	_defense_choice(key+"_alternative", alternatives, answer, source.modal_box, "2 · 解释")
	_defense_choice(key+"_unknown", unknowns, answer, source.modal_box, "3 · 未知范围")
	var selected_material: String = str(answer.get(key+"_material", "unknown"))
	var summary: RichTextLabel = source._label(source.modal_box, "当前这卡：\n材料：%s\n解释：%s\n保留：%s" % [
		_defense_option_text(materials, selected_material),
		_defense_option_text(alternatives, str(answer.get(key+"_alternative", "unknown"))),
		_defense_option_text(unknowns, str(answer.get(key+"_unknown", "unknown")))], 18)
	summary.name = "RedDefenseSummary"
	var material_selector: OptionButton = source.modal_box.get_node(key+"_material/Choice")
	for index in material_selector.item_count:
		var material_value: String = str(material_selector.get_item_metadata(index))
		if material_scope.has(material_value): material_selector.get_popup().set_item_tooltip(index, material_scope[material_value])
	source._button(source.modal_box, "收起陈述提示" if source.red_defense_hints else "显示陈述提示（需主动选择才写入）", func():
		source.red_defense_hints = not source.red_defense_hints
		_open_red_defense())
	if source.red_defense_hints:
		source._label(source.modal_box, "以下是可供检查的组合，包含材料支持不足的说法；显示提示不会替你选择。", 17)
		for statement in Content.defense_statements()[key]:
			var all_known: bool = true
			for id in statement[0].split("+"): all_known = all_known and id in known
			if statement[2] == "identity_supported_E16" and not "E16" in known: all_known = false
			if not all_known: continue
			var words: String = "材料：%s · %s\n解释：%s\n保留：%s" % [material_names[statement[0]], statement[0], _defense_option_text(alternatives, statement[1]), _defense_option_text(unknowns, statement[2])]
			var chosen: bool = answer.get(key+"_material", "unknown") == statement[0] and answer.get(key+"_alternative", "unknown") == statement[1] and answer.get(key+"_unknown", "unknown") == statement[2]
			var option: Button = source._button(source.modal_box, ("已选择 · " if chosen else "选用提示组合 · ") + words, func():
				answer[key+"_material"] = statement[0]
				answer[key+"_alternative"] = statement[1]
				answer[key+"_unknown"] = statement[2]
				_open_red_defense())
			option.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			option.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			option.toggle_mode = true
			option.set_pressed_no_signal(chosen)
			_style_red_statement(option, chosen)
	var feedback: RichTextLabel = source._label(source.modal_box,"",18)
	feedback.name = "RedDefenseFeedback"
	feedback.visible = false
	var actions: Array = [["证物页",source._open_journal],["返回现场",source._close_modal],["提交有材料的三项答辩",func():
		var result: Dictionary = source.red.call("Defend",answer)
		if result.get("ok",false):
			source.ended = true
			source.red_dialogue_queue.assign(["S27", "S28"])
			source._begin_dialogue("S28_ADMISSION")
		else:
			feedback.text = str(result.get("message","请检查材料支持范围。"))
			feedback.visible = true]]
	var status := _red_defense_status()
	if source.can_use_mind_reasoning():
		actions.insert(1, ["返回推演", source._open_red_notebook])
		status = ("交接推演已完成。" if source.red.call("State").get("solved", false) else "交接推演尚未完成，可先记草稿。") + "\n" + status
	source._pin_red_footer(actions, status)

func _red_defense_status() -> String:
	var missing: Array[String] = []
	for section in Content.defense_sections():
		for entry in [["material","材料"],["alternative","解释"],["unknown","未知范围"]]:
			if source.red_defense_answer.get(section[0]+"_"+entry[0], "unknown") == "unknown":
				missing.append({"open":"开态", "token":"红筹", "memory":"空白"}[section[0]]+"·"+entry[1])
	return "九字段均已选择；支持范围仍由提交核验。" if missing.is_empty() else "未补："+"、".join(missing)

func _style_red_statement(button: Button, selected: bool) -> void:
	var paper: StyleBoxFlat = StyleBoxFlat.new()
	paper.bg_color = Color("344c39") if selected else Color("f5efdf")
	paper.border_color = Color("213c27") if selected else Color("8c7a62")
	paper.set_border_width_all(3 if selected else 1)
	paper.content_margin_left = 12
	paper.content_margin_right = 12
	paper.content_margin_top = 10
	paper.content_margin_bottom = 10
	var hover: StyleBoxFlat = paper.duplicate() as StyleBoxFlat
	if not selected: hover.bg_color = Color("ddd2bc")
	var pressed: StyleBoxFlat = paper.duplicate() as StyleBoxFlat
	pressed.bg_color = Color("344c39")
	pressed.border_color = Color("213c27")
	pressed.set_border_width_all(3)
	var focus: StyleBoxFlat = paper.duplicate() as StyleBoxFlat
	focus.bg_color = Color(0, 0, 0, 0)
	focus.border_color = Color("a76320")
	focus.set_border_width_all(2)
	button.add_theme_stylebox_override("normal", paper)
	button.add_theme_stylebox_override("hover", hover)
	button.add_theme_stylebox_override("pressed", pressed)
	button.add_theme_stylebox_override("hover_pressed", pressed)
	button.add_theme_stylebox_override("focus", focus)
	for key in ["font_color", "font_hover_color", "font_focus_color"]:
		button.add_theme_color_override(key, Color("fff8e8") if selected else Color("30281f"))
	for key in ["font_pressed_color", "font_hover_pressed_color"]:
		button.add_theme_color_override(key, Color("fff8e8"))

func _defense_choice(key: String, options: Array, answer: Dictionary, parent: Node = null, caption: String = "") -> void:
	# Compact independent selectors keep all three fields in view. The full
	# saved wording is mirrored by RedDefenseSummary, including long options.
	var box: HBoxContainer = HBoxContainer.new()
	box.name = key
	box.add_theme_constant_override("separation", 12)
	(parent if parent != null else source.modal_box).add_child(box)
	var label: RichTextLabel = source._label(box, caption, 18)
	label.custom_minimum_size.x = 122
	var selector: OptionButton = OptionButton.new()
	selector.name = "Choice"
	selector.fit_to_longest_item = false
	selector.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	selector.custom_minimum_size.y = 44
	selector.add_theme_font_size_override("font_size", 18)
	selector.get_popup().add_theme_font_size_override("font_size", 18)
	box.add_child(selector)
	var selected: int = 0
	for index in options.size():
		selector.add_item(options[index][1])
		selector.set_item_metadata(index, options[index][0])
		if options[index][0] == answer.get(key, "unknown"): selected = index
	selector.select(selected)
	selector.tooltip_text = options[selected][1]
	selector.item_selected.connect(func(index: int):
		answer[key] = str(selector.get_item_metadata(index))
		_open_red_defense())

