extends "res://scenes/UI/ModalPanel.gd"

signal action_chosen(id: String)

const MAP_PANEL := preload("res://scenes/UI/CampusMapPanel.gd")

@onready var modal_box: PanelContainer = %ModalBox
@onready var modal_title: Label = %ModalTitle
@onready var modal_close_button: Button = %ModalCloseButton
@onready var modal_body: VBoxContainer = %ModalBody
@onready var modal_footer: HFlowContainer = %ModalFooter
@onready var modal_kicker: Label = %ModalKicker
@onready var modal_scroll: ScrollContainer = %ModalScroll

var _current_panel := ""
var _content_serial := 0
var _requested_size := Vector2(760, 520)
var _source_title := "调查信息"
var _restore_serial := 0
var _title_is_ui := true
var _ui_texts: Array[Dictionary] = []
var _option_placeholders: Array[Dictionary] = []
var _replay_confirmation: VBoxContainer

func _ready() -> void:
	super._ready()
	modal_title.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	modal_close_button.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	modal_kicker.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	modal_close_button.pressed.connect(close)
	closed.connect(func(): _current_panel = "")
	get_viewport().size_changed.connect(_fit_sheet)
	_localize()

func _notification(what: int) -> void:
	super._notification(what)
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_node_ready(): _localize()

func _localize() -> void:
	_refresh_chrome()
	for item: Dictionary in _ui_texts:
		var control: Control = item.control
		if not is_instance_valid(control): continue
		var text := _bound_text(item)
		control.set("text", "[b]%s[/b]" % text if bool(item.bold) else text)
	for item: Dictionary in _option_placeholders:
		var picker: OptionButton = item.control
		if is_instance_valid(picker): picker.set_item_text(0, _bound_text(item))

func _text(zh: String, en: String, ja: String) -> String:
	var locale := TranslationServer.get_locale()
	return ja if locale.begins_with("ja") else en if locale.begins_with("en") else zh

func _refresh_chrome() -> void:
	# Preserve the independent editorial chrome while supplied titles remain prose.
	modal_close_button.text = _text("合上", "Close", "閉じる")
	match _current_panel:
		"replay": modal_kicker.text = _text("案情研究", "INQUIRY", "推論手稿")
		"campus_map": modal_kicker.text = _text("学园图志", "CAMPUS ATLAS", "学園案内")
		"ending": modal_kicker.text = _text("阶段记述", "CHAPTER RECORD", "章の記録")
		"spatial": modal_kicker.text = _text("此时 · 此地", "HERE & NOW", "今、この場所で")
		_: modal_kicker.text = _text("现场札记", "FIELD RECORD", "調査覚書")
	modal_title.text = tr(_source_title) if _title_is_ui else _source_title

func _fit_sheet() -> void:
	if not is_node_ready(): return
	var viewport_size := get_viewport().get_visible_rect().size
	var available := Vector2(maxf(240.0, viewport_size.x - 64.0), maxf(240.0, viewport_size.y - 72.0))
	modal_box.custom_minimum_size = Vector2(minf(_requested_size.x, available.x), minf(_requested_size.y, available.y))
	modal_box.size = modal_box.custom_minimum_size

func capture_view_state() -> Dictionary:
	var focused := get_viewport().gui_get_focus_owner()
	return {
		"scroll": modal_scroll.scroll_vertical,
		"focus": str(modal_box.get_path_to(focused)) if focused != null and modal_box.is_ancestor_of(focused) else ""
	}

func restore_view_state(state: Dictionary) -> void:
	_restore_serial += 1
	_restore_view_after_layout(state, _content_serial, _restore_serial)

func _restore_view_after_layout(state: Dictionary, content_serial: int, restore_serial: int) -> void:
	await get_tree().process_frame
	# The inherited panel enables controls and assigns its default focus at the
	# end of entry. Restore after that, so it cannot overwrite the saved focus.
	while content_serial == _content_serial and restore_serial == _restore_serial and is_open() and transition_state() == "entering":
		await get_tree().process_frame
	if content_serial != _content_serial or restore_serial != _restore_serial or not is_open(): return
	modal_scroll.scroll_vertical = int(state.get("scroll", 0))
	var focus_path := str(state.get("focus", ""))
	if not focus_path.is_empty():
		var target := modal_box.get_node_or_null(NodePath(focus_path)) as Control
		if Transition.can_focus(target): target.grab_focus()

func open_modal(title: String, min_size := Vector2(760, 520), panel_id := "", title_is_ui := false) -> VBoxContainer:
	_content_serial += 1
	_restore_serial += 1
	_current_panel = panel_id
	_source_title = title
	_title_is_ui = title_is_ui
	_ui_texts.clear()
	_option_placeholders.clear()
	_replay_confirmation = null
	_requested_size = min_size
	for child in modal_body.get_children():
		modal_body.remove_child(child)
		child.queue_free()
	for child in modal_footer.get_children():
		modal_footer.remove_child(child)
		child.queue_free()
	modal_footer.hide()
	modal_scroll.scroll_vertical = 0
	_localize()
	_fit_sheet()
	show_panel()
	return modal_body

func _choose(id: String, content_serial: int) -> void:
	if content_serial != _content_serial or not can_interact(): return
	# close() locks every control before a callback can dispatch a second action.
	close()
	action_chosen.emit(id)

func open_manuscript(data: Dictionary) -> void:
	# This page presents only supplied observer data. It neither runs a replay
	# nor promotes personal writing into an admitted hypothesis or a prediction.
	var body := open_modal("推演工作台", Vector2(720, 500), "replay", true)
	_append_supplied_section(body, "推演摘要", data.get("summary"))
	var choices: Array = data.get("options", [])
	if not choices.is_empty():
		_ui_localized_text(body, "随身思考与重演", "Reflection and reconstruction", "考察と再現", "EditorialSubtitle")
		for option: Dictionary in choices:
			if option.has("replay_input"):
				_append_replay_form(body, option)
			else:
				body.add_child(_action_button(option, "ChoiceRow"))
	else:
		_ui_localized_text(body, "当前没有可进行的重演。调查现场、询问同伴后，可再到这里整理线索。", "No reconstruction is available yet. Examine the scene and question your companions, then return to your notes.", "今は実行できる再現がありません。現場を調べ、仲間に話を聞いてから手稿に戻れます。", "ManuscriptNote")
	_ui_text(body, "推演与假设", true)
	var hypotheses := _supplied_texts(data.get("hypotheses", []))
	if hypotheses.is_empty():
		_ui_text(body, "当前没有可显示的推演假设。")
	for text: String in hypotheses: _raw_text(body, text)
	_append_supplied_section(body, "冲突", data.get("conflicts"))
	_append_supplied_section(body, "预测", data.get("predictions"))
	body.add_child(HSeparator.new())
	_ui_text(body, "个人草稿", true)
	_ui_text(body, "草稿，不作为已证实事实。")
	var personal_notes := _supplied_texts(data.get("personal_notes", []))
	if personal_notes.is_empty():
		_ui_text(body, "尚无个人批注。")
	for text: String in personal_notes: _raw_text(body, text)

func _replay_cost_text(option: Dictionary) -> String:
	var cost: Variant = option.get("cost", null)
	if not cost is Dictionary or cost.is_empty():
		return _text("代价未提供，暂不能提交。", "Cost unavailable; submission is disabled.", "費用が不明のため送信できません。")
	var fields: Array[String] = []
	for key in ["time", "computation", "stamina"]:
		if not cost.has(key) or not (cost[key] is int or cost[key] is float):
			return _text("代价未完整提供，暂不能提交。", "Cost incomplete; submission is disabled.", "費用が不足しているため送信できません。")
		var name := _text("时间", "Time", "時間") if key == "time" else _text("计算力", "Computation", "計算力") if key == "computation" else _text("体力", "Stamina", "体力")
		fields.append(name + ": " + str(cost[key]))
	return _text("本次代价：", "Cost: ", "今回の費用：") + " · ".join(fields)

func _selected_replay_text(schema: Dictionary, selected: Dictionary) -> String:
	var parts: Array[String] = []
	for variable in schema.get("variables", []):
		if not variable is Dictionary: continue
		var id := str(variable.get("id", ""))
		if not selected.has(id): continue
		for item in variable.get("values", []):
			if item is Dictionary and item.get("value") == selected[id]:
				parts.append(str(variable.get("label", "")) + ": " + str(item.get("label", "")))
				break
	return " · ".join(parts)
func _confirm_replay(body: VBoxContainer, payload: String, description: String, cost_text: String, source: Button, serial: int) -> void:
	if serial != _content_serial or not can_interact(): return
	if is_instance_valid(_replay_confirmation): _replay_confirmation.queue_free()
	_replay_confirmation = VBoxContainer.new()
	body.add_child(_replay_confirmation)
	_raw_text(_replay_confirmation, description)
	_raw_text(_replay_confirmation, cost_text)
	_ui_localized_text(_replay_confirmation, "确认提交推演？", "Confirm this reconstruction?", "この推論を送信しますか？")
	var cancel := Button.new()
	cancel.text = _text("取消", "Cancel", "キャンセル")
	cancel.custom_minimum_size.y = 48
	_replay_confirmation.add_child(cancel)
	var confirm := Button.new()
	confirm.text = _text("确认", "Confirm", "確認")
	confirm.custom_minimum_size.y = 48
	_replay_confirmation.add_child(confirm)
	var panel := _replay_confirmation
	cancel.pressed.connect(func():
		if is_instance_valid(panel): panel.queue_free()
		if Transition.can_focus(source): source.grab_focus()
	)
	confirm.pressed.connect(func(): _choose(payload, serial))
	cancel.grab_focus()
	modal_scroll.ensure_control_visible.call_deferred(cancel)

func _append_replay_form(body: VBoxContainer, option: Dictionary) -> void:
	var schema: Dictionary = option.replay_input
	var serial := _content_serial
	var cost_text := _replay_cost_text(option)
	_raw_text(body, cost_text)
	var cost: Variant = option.get("cost", null)
	var cost_known: bool = cost is Dictionary and cost.has("time") and cost.has("computation") and cost.has("stamina")
	if cost_known:
		for key in ["time", "computation", "stamina"]:
			if not (cost[key] is int or cost[key] is float): cost_known = false
	var named := OptionButton.new()
	named.theme_type_variation = &"ChoiceRow"
	named.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	named.add_item(_text("未选择假设", "Choose a hypothesis", "仮説を選択"))
	_option_placeholders.append({"control": named, "locales": ["未选择假设", "Choose a hypothesis", "仮説を選択"]})
	for row: Dictionary in schema.get("named", []): named.add_item(str(row.label))
	body.add_child(named)
	var named_submit := Button.new()
	named_submit.theme_type_variation = &"TextAction"
	named_submit.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	named_submit.text = _text("支付并重演所选假设", "Pay and replay selected hypothesis", "選択した仮説を有料で再現")
	_ui_texts.append({"control": named_submit, "locales": ["支付并重演所选假设", "Pay and replay selected hypothesis", "選択した仮説を有料で再現"], "bold": false})
	named_submit.disabled = true
	named.item_selected.connect(func(index: int): named_submit.disabled = index == 0 or not cost_known)
	named_submit.pressed.connect(func():
		var index := named.selected - 1
		var rows: Array = schema.get("named", [])
		if index >= 0 and index < rows.size():
			_confirm_replay(body, JSON.stringify({"action_id": option.id, "hypothesis_id": rows[index].id}), str(rows[index].get("label", "")), cost_text, named_submit, serial)
	)
	body.add_child(named_submit)
	var assumption := str(schema.get("initial_assumption", ""))
	if not assumption.is_empty(): _raw_text(body, assumption)
	var selected: Dictionary = {}
	var history_submit := Button.new()
	history_submit.theme_type_variation = &"TextAction"
	history_submit.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	history_submit.text = _text("支付并重演这份组合假设", "Pay and replay this combination", "この組合せ仮説を有料で再現")
	_ui_texts.append({"control": history_submit, "locales": ["支付并重演这份组合假设", "Pay and replay this combination", "この組合せ仮説を有料で再現"], "bold": false})
	history_submit.disabled = true
	var variables: Array = schema.get("variables", [])
	for variable: Dictionary in variables:
		_raw_text(body, str(variable.label))
		var picker := OptionButton.new()
		picker.theme_type_variation = &"ChoiceRow"
		picker.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
		picker.add_item(_text("未选择", "Unselected", "未選択"))
		_option_placeholders.append({"control": picker, "locales": ["未选择", "Unselected", "未選択"]})
		for item: Dictionary in variable.get("values", []): picker.add_item(str(item.label))
		picker.item_selected.connect(func(index: int):
			if index == 0: selected.erase(str(variable.id))
			else: selected[str(variable.id)] = variable.get("values", [])[index - 1].value
			history_submit.disabled = not cost_known or _manual_case(schema, selected).is_empty()
		)
		body.add_child(picker)
	history_submit.pressed.connect(func():
		var hypothesis := _manual_case(schema, selected)
		if not hypothesis.is_empty() and cost_known: _confirm_replay(body, JSON.stringify({"action_id": option.id, "hypothesis_id": hypothesis}), _selected_replay_text(schema, selected), cost_text, history_submit, serial)
	)
	if schema.get("cases", []).is_empty():
		_ui_localized_text(body, "完整组合假设尚未向本人开放；未选项目保持未知。", "The complete combination is not available to this subject yet. Unselected fields remain unknown.", "完全な組合せ仮説はまだ本人に開かれていません。未選択の項目は不明のままです。", "ManuscriptNote")
	body.add_child(history_submit)

func _manual_case(schema: Dictionary, selected: Dictionary) -> String:
	for row: Dictionary in schema.get("cases", []):
		var assignments: Array = row.get("assignments", [])
		var matches := not assignments.is_empty() and selected.size() == assignments.size()
		for assignment: Dictionary in assignments:
			var variable := str(assignment.variable)
			if not selected.has(variable) or selected[variable] != assignment.value: matches = false
		if matches: return str(row.id)
	return ""

func _append_supplied_section(body: VBoxContainer, source: String, value: Variant) -> void:
	var texts := _supplied_texts(value)
	if texts.is_empty(): return
	_ui_text(body, source, true)
	for text: String in texts: _raw_text(body, text)

func _supplied_texts(value: Variant) -> Array[String]:
	var result: Array[String] = []
	if value is String or value is StringName:
		var text := str(value)
		if not text.strip_edges().is_empty(): result.append(text)
	elif value is Array:
		for item in value: result.append_array(_supplied_texts(item))
	elif value is Dictionary:
		# Only explicit presentation fields are readable prose. Do not dump internal
		# predicates, scores, undisclosed links or arbitrary record fields into UI.
		var title_value: Variant = value.get("title", value.get("label", ""))
		var title := str(title_value) if title_value is String or title_value is StringName else ""
		var body_value: Variant = value.get("text", value.get("body", value.get("summary",
			value.get("description", value.get("reason", value.get("lines", []))))))
		var body := "\n\n".join(_supplied_texts(body_value))
		if not title.is_empty() and not body.is_empty(): result.append(title + "\n" + body)
		elif not body.is_empty(): result.append(body)
		elif not title.is_empty(): result.append(title)
	return result

func open_campus_map(data: Dictionary) -> void:
	var body := open_modal("地点与路线", Vector2(880, 620), "campus_map", true)
	_raw_text(body, str(data.current_label), true)
	var drawing := MAP_PANEL.new()
	drawing.configure(data)
	body.add_child(drawing)
	_ui_localized_text(body, "● 当前位置　列表：已到访地点与已走过的连接；不表示空间位置", "● Current location · List: visited places and travelled connections; not a floor plan", "● 現在地　一覧：訪問済みの場所と通行済みの経路。配置図ではありません", "ManuscriptNote")
	for connection: Dictionary in data.connections:
		_raw_text(body, str(connection.label))

func open_ending(data: Dictionary) -> void:
	var body := open_modal(str(data.get("title", "案情收卷")), Vector2(800, 560), "ending", not data.has("title"))
	_raw_text(body, str(data.get("text", "")), true)

func show_spatial_choices(data: Dictionary) -> void:
	var title := str(data.get("title", "可行动作"))
	var options: Array = data.get("options", [])
	var height := clampf(220.0 + options.size() * 60.0, 340.0, 620.0)
	var body := open_modal(title, Vector2(680, height), "spatial", not data.has("title"))
	if options.is_empty():
		_ui_localized_text(body, "此刻没有可执行的动作。", "No action is available at present.", "今は実行できる行動がありません。", "ManuscriptNote")
	else:
		for value in options:
			if value is Dictionary:
				body.add_child(_action_button(value, "ChoiceRow"))

func _paragraph(text: String, variant := "ManuscriptBody", allow_bbcode := true) -> RichTextLabel:
	var paragraph := RichTextLabel.new()
	paragraph.theme_type_variation = StringName(variant)
	paragraph.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	paragraph.bbcode_enabled = allow_bbcode
	paragraph.fit_content = true
	paragraph.scroll_active = false
	paragraph.selection_enabled = true
	paragraph.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	paragraph.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	paragraph.text = text
	return paragraph

func _action_button(data: Dictionary, variant: String) -> Button:
	var button := Button.new()
	var id := str(data.get("id", ""))
	button.name = "Action_" + id.validate_node_name()
	button.theme_type_variation = StringName(variant)
	button.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	button.text = str(data.get("label", data.get("text", id)))
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	button.custom_minimum_size = Vector2(0, 48)
	button.disabled = id.is_empty() or not bool(data.get("available", true))
	if button.disabled:
		var reason := str(data.get("reason", data.get("disabled_reason", "")))
		if not reason.is_empty():
			button.text += "\n" + reason
			button.tooltip_text = reason
	button.pressed.connect(_choose.bind(id, _content_serial))
	return button

func _bound_text(item: Dictionary) -> String:
	if item.has("locales"):
		var locales: Array = item.locales
		return _text(str(locales[0]), str(locales[1]), str(locales[2]))
	return tr(str(item.source))

func _ui_localized_text(parent: Node, zh: String, en: String, ja: String, variant := "ManuscriptBody") -> RichTextLabel:
	var label := _paragraph(_text(zh, en, ja), variant, false)
	_ui_texts.append({"control": label, "locales": [zh, en, ja], "bold": false})
	parent.add_child(label)
	return label

func _bind_ui_text(control: Control, source: String, bold := false) -> void:
	_ui_texts.append({"control": control, "source": source, "bold": bold})
	var text := tr(source)
	control.set("text", "[b]%s[/b]" % text if bold else text)

func _ui_text(parent: Node, source: String, bold := false) -> RichTextLabel:
	var label := _raw_text(parent, "", bold)
	_bind_ui_text(label, source, bold)
	return label

func _raw_text(parent: Node, text: String, allow_bbcode := false) -> RichTextLabel:
	var label := _paragraph(text, "ManuscriptBody", allow_bbcode)
	parent.add_child(label)
	return label




