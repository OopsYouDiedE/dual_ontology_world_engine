extends Control

## 推演浮层：列出推演规则、本人已确认 / 尚未确认的线索；玩家勾选支撑结论的线索后提交。
## 现场宿主在没有书本检视器时发出 deduction_requested，FieldGUI 取预览字典后调用 present()。
## 只发出意图信号 submitted(action_id, evidence_ids) / dismissed，不认识现场宿主与会话桥。

signal submitted(action_id: String, evidence_ids: Array)
signal dismissed

const FONT := preload("res://assets/external/NotoSerifSC/NotoSerifSC.ttf")
const INK := Color("efe4cc")
const MUTED := Color("a99c82")
const GOLD := Color("d9b56c")

var _action_id := ""
var _checks: Dictionary = {}
var _title: Label
var _rules: Label
var _evidence_box: VBoxContainer
var _conclusion: Label
var _notice: Label

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var shade := ColorRect.new()
	shade.color = Color(0, 0, 0, 0.5)
	shade.set_anchors_preset(Control.PRESET_FULL_RECT)
	shade.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(shade)
	var panel := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.11, 0.095, 0.08, 0.97)
	style.border_color = Color(GOLD, 0.6)
	style.set_border_width_all(1)
	style.set_corner_radius_all(12)
	style.content_margin_left = 26; style.content_margin_right = 26
	style.content_margin_top = 20; style.content_margin_bottom = 20
	style.shadow_color = Color(0, 0, 0, 0.5)
	style.shadow_size = 24
	panel.add_theme_stylebox_override("panel", style)
	panel.set_anchors_preset(Control.PRESET_CENTER)
	panel.custom_minimum_size = Vector2(660, 0)
	panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	add_child(panel)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 10)
	panel.add_child(column)
	_title = _label(column, 22, GOLD)
	var rules_title := _label(column, 14, MUTED)
	rules_title.text = "推演规则"
	_rules = _label(column, 15, INK)
	var evidence_title := _label(column, 14, MUTED)
	evidence_title.text = "选择支撑结论的线索（只能选择已亲自确认的）"
	_evidence_box = VBoxContainer.new()
	column.add_child(_evidence_box)
	_conclusion = _label(column, 15, INK)
	_notice = _label(column, 14, Color("e58f74"))
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_END
	row.add_theme_constant_override("separation", 10)
	column.add_child(row)
	var cancel := Button.new()
	cancel.text = "暂不推演（Esc）"
	cancel.pressed.connect(func(): dismissed.emit())
	_style_button(cancel)
	row.add_child(cancel)
	var confirm := Button.new()
	confirm.text = "推演"
	confirm.pressed.connect(_submit)
	_style_button(confirm)
	row.add_child(confirm)
	hide()

func present(action_id: String, preview: Dictionary) -> void:
	_action_id = action_id
	_title.text = "推演 · " + str(preview.get("title", action_id))
	var lines := PackedStringArray()
	var rules: Array = preview.get("rules", []) if preview.get("rules") is Array else []
	for rule in rules:
		if rule is Dictionary: lines.append("· " + str(rule.get("label", rule.get("id", ""))))
	_rules.text = "\n".join(lines) if not lines.is_empty() else "（本推演没有列出显式规则）"
	for child in _evidence_box.get_children(): child.queue_free()
	_checks.clear()
	var evidence: Array = preview.get("evidence", []) if preview.get("evidence") is Array else []
	for item in evidence:
		if not item is Dictionary: continue
		var known := bool(item.get("known", false))
		var check := CheckBox.new()
		check.text = str(item.get("label", item.get("id", "")))
		check.disabled = not known
		check.button_pressed = known
		check.focus_mode = Control.FOCUS_NONE
		check.add_theme_font_override("font", FONT)
		check.add_theme_font_size_override("font_size", 15)
		check.add_theme_color_override("font_color", INK)
		check.add_theme_color_override("font_disabled_color", Color(MUTED, 0.6))
		_evidence_box.add_child(check)
		if known: _checks[str(item.get("id", ""))] = check
	var qualification := str(preview.get("qualification", ""))
	_conclusion.text = "结论：" + str(preview.get("conclusion", "")) + ("\n限定：" + qualification if not qualification.is_empty() else "")
	_notice.text = "" if not _checks.is_empty() else "还没有确认任何相关线索：先去现场查看、询问或阅读。"
	show()

func show_error(text: String) -> void:
	_notice.text = text

func close() -> void:
	_action_id = ""
	hide()

func is_open() -> bool:
	return visible

func _submit() -> void:
	var ids: Array = []
	for id in _checks:
		if (_checks[id] as CheckBox).button_pressed: ids.append(id)
	if ids.is_empty():
		_notice.text = "至少选择一条已确认的线索。"
		return
	submitted.emit(_action_id, ids)

func _unhandled_key_input(event: InputEvent) -> void:
	if visible and event is InputEventKey and event.pressed and (event as InputEventKey).keycode == KEY_ESCAPE:
		dismissed.emit()
		get_viewport().set_input_as_handled()

func _label(parent: Node, size: int, color: Color) -> Label:
	var label := Label.new()
	label.add_theme_font_override("font", FONT)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	parent.add_child(label)
	return label

func _style_button(button: Button) -> void:
	button.focus_mode = Control.FOCUS_NONE
	button.custom_minimum_size = Vector2(120, 36)
	button.add_theme_font_override("font", FONT)
	button.add_theme_font_size_override("font_size", 15)
	button.add_theme_color_override("font_color", INK)
	button.add_theme_color_override("font_hover_color", GOLD)
