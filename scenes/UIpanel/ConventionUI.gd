@tool
extends "res://scenes/UIpanel/CasebookUI/BookPageLayout.gd"

signal opinion_updated(clause_id: String, opinion: String)
signal convention_signed(viewer_id: String, version: String)
signal convention_closed
signal pin_requested(data: Dictionary)

var _is_open: bool = false
var _standalone: bool = false
var _standalone_data: Dictionary = {}
var _convention_data: Dictionary = {}
var _viewer_id: String = ""
var _filter: String = "all"
var _selected_id: String = ""
var _contracts: Array = []
var _pinned: Dictionary = {}
var _pending_signature: String = ""

func configure(data: Dictionary, book_data: RefCounted = null) -> void:
	_standalone = false
	_standalone_data = {}
	_viewer_id = str(data.get("viewer", ""))
	_pending_signature = ""
	visible = true
	super.configure(data, book_data)

func is_convention_open() -> bool:
	return _is_open

func open_convention(data: Dictionary) -> void:
	_standalone = true
	_standalone_data = data.duplicate(true)
	_viewer_id = str(data.get("viewer", ""))
	_selected_id = ""
	_is_open = true
	visible = true
	if is_node_ready(): _build_content()

func close_convention() -> void:
	if not _is_open: return
	_is_open = false
	visible = false
	convention_closed.emit()

func set_pinned(data: Dictionary) -> void:
	_pinned = data.duplicate(true)
	if is_node_ready(): _build_content()

func _identity(value: Dictionary) -> String:
	return str(value.get("id", value.get("convention_id", value.get("title", "")))) + "@" + str(value.get("version", ""))

func _collect_contracts() -> void:
	_contracts.clear()
	var sources: Array = []
	if _standalone and not _standalone_data.is_empty(): sources.append(_standalone_data)
	else:
		var active: Dictionary = _dict(snapshot.get("convention", {}))
		if not active.is_empty(): sources.append(active)
		for value in _array(_dict(snapshot.get("casebook", {})).get("contracts", [])):
			if value is Dictionary and bool(value.get("acquired", value.get("known", false))): sources.append(value)
		for value: Dictionary in _entries("evidence"):
			if str(value.get("kind", value.get("type", ""))) in ["contract", "convention"]:
				var record: Dictionary = value.duplicate(true)
				record["read_only"] = true
				sources.append(record)
	var seen: Dictionary = {}
	for value: Dictionary in sources:
		var current: Dictionary = value.duplicate(true)
		if not seen.has(_identity(current)):
			_contracts.append(current)
			seen[_identity(current)] = true
		for old in _array(value.get("versions", [])):
			if not old is Dictionary: continue
			var previous: Dictionary = old.duplicate(true)
			previous["id"] = str(value.get("id", value.get("convention_id", "")))
			previous["title"] = _title(value)
			previous["is_current"] = false
			previous["read_only"] = true
			if not seen.has(_identity(previous)):
				_contracts.append(previous)
				seen[_identity(previous)] = true

func _viewer_signed(value: Dictionary) -> bool:
	if _viewer_id.is_empty(): return false
	var version: String = str(value.get("version", ""))
	for person in _array(value.get("signatories", [])):
		if person is Dictionary and str(person.get("actor", "")) == _viewer_id:
			return str(person.get("status", "")) == "signed" and str(person.get("signed_version", "")) == version
	return bool(value.get("viewer_signed", false))

func _build_content() -> void:
	_collect_contracts()
	var filtered: Array = []
	for value: Dictionary in _contracts:
		var current: bool = bool(value.get("is_current", true))
		var signed: bool = _viewer_signed(value)
		if _filter == "history" and current: continue
		if _filter == "signed" and (not current or not signed): continue
		if _filter == "unsigned" and (not current or signed): continue
		filtered.append(value)
	_convention_data = {}
	for value: Dictionary in filtered:
		if _identity(value) == _selected_id: _convention_data = value
	if _convention_data.is_empty() and not filtered.is_empty():
		_convention_data = filtered[0]
		_selected_id = _identity(_convention_data)
	_begin("契约与公约", _title(_convention_data, "尚无契约"))
	var tabs: HFlowContainer = HFlowContainer.new()
	left_body.add_child(tabs)
	for value in [["all", "全部"], ["unsigned", "待签"], ["signed", "已签"], ["history", "旧版"]]:
		var key: String = str(value[0])
		var tab: Button = _button(tabs, str(value[1]), func(): _filter = key; _selected_id = ""; _build_content(), _filter == key)
		tab.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
		tab.add_theme_font_size_override("font_size", _scaled(18))
	if not _pinned.is_empty():
		_render_pinned()
		var picker: OptionButton = OptionButton.new()
		picker.custom_minimum_size.y = 42
		for value: Dictionary in filtered:
			picker.add_item(_title(value) + " · " + str(value.get("version", "")))
			if _identity(value) == _selected_id: picker.select(picker.item_count - 1)
		picker.item_selected.connect(func(index: int): _selected_id = _identity(filtered[index]); _build_content())
		right_body.add_child(picker)
	elif filtered.is_empty():
		_empty(left_body, "尚无契约记录")
	else:
		var catalog: VBoxContainer = _scroll(left_body)
		for value: Dictionary in filtered:
			var id: String = _identity(value)
			var row: VBoxContainer = _column(catalog, 4)
			_button(row, _title(value), func(): _selected_id = id; _build_content(), id == _selected_id)
			var state: String = "旧版" if not bool(value.get("is_current", true)) else "已签署" if _viewer_signed(value) else "未签署"
			_text(row, str(value.get("version", "")) + " · " + state, 16, true)
	if _convention_data.is_empty():
		_empty(right_body)
		return
	_render_contract()

func _render_contract() -> void:
	var value: Dictionary = _convention_data
	var version: String = str(value.get("version", ""))
	var current: bool = bool(value.get("is_current", true))
	var row: HBoxContainer = _row(right_body)
	_text(row, version + (" · 旧版" if not current else ""), 17, true).size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var pin: Button = _button(row, "钉选", _pin_current)
	pin.size_flags_horizontal = Control.SIZE_SHRINK_END
	pin.add_theme_font_size_override("font_size", _scaled(17))
	if _standalone:
		var close: Button = _button(row, "合上", close_convention)
		close.size_flags_horizontal = Control.SIZE_SHRINK_END
	if bool(value.get("is_revised_since_last_sign", false)):
		_text(right_body, "条款已修订 · 待重新签署", 18).add_theme_color_override("font_color", _red)
	var content: VBoxContainer = _scroll(right_body)
	var body: String = _body(value)
	if not body.is_empty(): _prose(content, body)
	for clause in _array(value.get("clauses", [])):
		if not clause is Dictionary: continue
		_text(content, _title(clause, "条款"), 22)
		_prose(content, str(clause.get("content", clause.get("text", ""))), 21)
		if current and not bool(value.get("read_only", false)) and not _viewer_id.is_empty():
			var opinions: HBoxContainer = _row(content, 6)
			var selected: String = str(clause.get("viewer_opinion", ""))
			for choice in [["agree", "赞同"], ["reserved", "保留"], ["object", "反对"]]:
				var opinion: String = str(choice[0])
				var clause_id: String = str(clause.get("id", ""))
				var option: Button = _button(opinions, str(choice[1]), func(): _opine(clause_id, opinion), selected == opinion)
				option.add_theme_font_size_override("font_size", _scaled(17))
				option.disabled = clause_id.is_empty()
		_rule(content)
	var signatories: Array = _array(value.get("signatories", []))
	if not signatories.is_empty():
		_text(content, "各方签署", 17, true)
		for person in signatories:
			if not person is Dictionary: continue
			var signed_version: String = str(person.get("signed_version", ""))
			var state: String = "未签" if str(person.get("status", "")) != "signed" else "已签" if signed_version == version else "旧版签名"
			var actor: String = str(person.get("name", person.get("actor_name", person.get("actor", ""))))
			var line: HBoxContainer = _row(content)
			_text(line, actor, 19).size_flags_horizontal = Control.SIZE_EXPAND_FILL
			_text(line, state, 17, true)
	_text(right_body, _source(value), 15, true)
	var can_sign: bool = current and not bool(value.get("read_only", false)) and bool(value.get("can_sign", true)) and not _viewer_id.is_empty() and not _array(value.get("clauses", [])).is_empty()
	if can_sign:
		var signed: bool = _viewer_signed(value)
		var pending: bool = _pending_signature == _selected_id
		var button: Button = _button(right_body, "已签署" if signed else "等待确认" if pending else "签署本版", _on_self_sign_pressed, signed)
		button.disabled = signed or pending

func _opine(clause_id: String, opinion: String) -> void:
	if clause_id.is_empty(): return
	opinion_updated.emit(clause_id, opinion)
	ui_event.emit({"kind": "convention_opinion", "clause_id": clause_id, "opinion": opinion, "convention_id": str(_convention_data.get("id", _convention_data.get("convention_id", "")))})

func _on_self_sign_pressed() -> void:
	if _convention_data.is_empty() or _viewer_id.is_empty() or _viewer_signed(_convention_data) or _pending_signature == _selected_id: return
	_pending_signature = _selected_id
	var version: String = str(_convention_data.get("version", ""))
	convention_signed.emit(_viewer_id, version)
	ui_event.emit({"kind": "convention_signed", "viewer": _viewer_id, "version": version, "convention_id": str(_convention_data.get("id", _convention_data.get("convention_id", "")))})
	_build_content()

func _pin_current() -> void:
	if _convention_data.is_empty(): return
	var record: Dictionary = _convention_data.duplicate(true)
	var text: String = _body(record)
	for clause in _array(record.get("clauses", [])):
		if clause is Dictionary: text += ("\n\n" if not text.is_empty() else "") + _title(clause, "") + "\n" + str(clause.get("content", clause.get("text", "")))
	record["text"] = text
	record["title"] = _title(record) + " · " + str(record.get("version", ""))
	_pinned = record
	pin_requested.emit(record)
	_build_content()

func _render_pinned() -> void:
	var top: HBoxContainer = _row(left_body)
	_text(top, "钉选", 16, true).size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var release: Button = _button(top, "取下", func(): _pinned = {}; pin_requested.emit({}); _build_content())
	release.size_flags_horizontal = Control.SIZE_SHRINK_END
	_text(left_body, _title(_pinned), 25)
	_prose(_scroll(left_body), _body(_pinned), 21)
	_text(left_body, _source(_pinned), 15, true)

func _gui_input(event: InputEvent) -> void:
	if _is_open and (event.is_action_pressed("ui_cancel") or event.is_action_pressed("cancel")):
		close_convention()
		get_viewport().set_input_as_handled()

func _reset_view() -> void:
	_selected_id = ""
	_convention_data = {}
	_pinned = {}
	_pending_signature = ""
