@tool
extends "res://scenes/UIpanel/CasebookUI/BookPageLayout.gd"

signal hypothesis_selected(hypothesis_id: String)
signal status_changed(hypothesis_id: String, status: String)
signal deduction_requested(hypothesis_id: String)
signal note_saved(hypothesis_id: String, note_text: String)

var _selected_id: String = ""
var _draft_initialized: bool = false
var _draft_title: String = ""
var _draft_text: String = ""
var _judgement: String = "doubt"
var _related: Array = []
var _relations: Dictionary = {}
var _dirty: bool = false
var _editor: TextEdit
var _title_edit: LineEdit
var _save_button: Button
var _deduction_button: Button
var _save_status: Label
var _caret: Vector2i = Vector2i.ZERO
var _scroll_position: float = 0.0
var _writing: bool = false

func configure(data: Dictionary, book_data: RefCounted = null) -> void:
	var previous_viewer: String = str(snapshot.get("viewer", ""))
	var changed_viewer: bool = not previous_viewer.is_empty() and previous_viewer != str(data.get("viewer", ""))
	# The host may already have cleared the shared archive for the new viewpoint.
	if changed_viewer: _dirty = false
	elif is_node_ready(): save_draft()
	super.configure(data, book_data)
	var related: Array = _array(data.get("related", []))
	var requested: String = str(data.get("entry_id", ""))
	if not requested.is_empty():
		var note: Dictionary = _entry(requested)
		if not note.is_empty(): _load_note(note)
	elif not related.is_empty():
		_new_note()
		_related = related.duplicate()
	if is_node_ready(): _build_content()

func open_note(id: String = "", related: Array = []) -> void:
	save_draft()
	if not id.is_empty():
		var note: Dictionary = _entry(id)
		if not note.is_empty(): _load_note(note)
	else: _new_note()
	for value in related:
		if not _related.has(value):
			_related.append(value)
			_dirty = true
	save_draft()
	if is_node_ready(): _build_content()

func _capture_draft() -> void:
	if is_instance_valid(_editor) and _editor.is_inside_tree():
		_draft_text = _editor.text
		_caret = Vector2i(_editor.get_caret_column(), _editor.get_caret_line())
		_scroll_position = _editor.scroll_vertical
	if is_instance_valid(_title_edit) and _title_edit.is_inside_tree(): _draft_title = _title_edit.text

func _load_note(note: Dictionary) -> void:
	_selected_id = str(note.get("id", "")) if bool(note.get("personal", false)) else ""
	_draft_title = _title(note, "")
	_draft_text = _body(note)
	_related = _array(note.get("related", [])).duplicate()
	if not bool(note.get("personal", false)) and not _related.has(str(note.get("id", ""))): _related.append(str(note.get("id", "")))
	_relations = _dict(note.get("relations", {})).duplicate()
	_judgement = str(note.get("judgement", "doubt"))
	_draft_initialized = true
	_dirty = false
	_caret = Vector2i.ZERO
	_scroll_position = 0
	_editor = null
	_title_edit = null

func _new_note() -> void:
	_selected_id = ""
	_draft_title = ""
	_draft_text = ""
	_related = []
	_relations = {}
	_judgement = "doubt"
	_draft_initialized = true
	_dirty = false
	_caret = Vector2i.ZERO
	_scroll_position = 0
	_editor = null
	_title_edit = null

func _build_content() -> void:
	_capture_draft()
	var notes: Array = _entries("hypotheses")
	if not _draft_initialized:
		if not notes.is_empty(): _load_note(notes.back())
		else: _new_note()
	_begin("猜想与笔记", "个人笔记")
	var catalog: VBoxContainer = _scroll(left_body)
	_button(catalog, "+ 新笔记", func(): save_draft(); _new_note(); _build_content())
	for note: Dictionary in notes:
		var row: VBoxContainer = _column(catalog, 3)
		var id: String = str(note.get("id", ""))
		var button: Button = _button(row, _title(note), func(): save_draft(); _load_note(note); _build_content(); hypothesis_selected.emit(id), id == _selected_id)
		button.tooltip_text = _title(note)
		var judgement: String = str(note.get("judgement", "doubt"))
		var mark: String = "暂认" if judgement == "true" else "存疑" if judgement == "false" else "待证"
		_text(row, mark + " · " + _time(note.get("game_time", "")), 15, true)
	_rule(catalog)
	_text(catalog, "关联证据", 17, true)
	_render_relations(catalog)
	_render_editor()

func _render_relations(parent: Node) -> void:
	for value in _related:
		var id: String = str(value)
		var source: Dictionary = _entry(id)
		if source.is_empty(): continue
		var row: HBoxContainer = _row(parent, 3)
		var label: Label = _text(row, _title(source), 18)
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		var picker: OptionButton = OptionButton.new()
		picker.add_theme_font_size_override("font_size", _scaled(16))
		for text in ["关联", "支持", "冲突"]: picker.add_item(text)
		var relation: String = str(_relations.get(id, "related"))
		picker.select(1 if relation == "support" else 2 if relation == "conflict" else 0)
		picker.item_selected.connect(func(index: int): _relations[id] = ["related", "support", "conflict"][index]; _dirty = true; save_draft())
		row.add_child(picker)
		var remove: Button = _button(row, "×", func(): _capture_draft(); _related.erase(id); _relations.erase(id); _dirty = true; save_draft(); _build_content())
		remove.size_flags_horizontal = Control.SIZE_SHRINK_END
		remove.tooltip_text = "解除关联"
		remove.custom_minimum_size.x = 36
		_text(parent, _source(source), 15, true)
	var available: Array = []
	for source: Dictionary in _entries("evidence") + _entries("testimony") + _entries("records"):
		if not _related.has(str(source.get("id", ""))): available.append(source)
	if not available.is_empty():
		var picker: OptionButton = OptionButton.new()
		picker.custom_minimum_size.y = 44
		picker.add_theme_font_size_override("font_size", _scaled(18))
		picker.add_item("+ 关联证据")
		for source: Dictionary in available: picker.add_item(_title(source))
		picker.item_selected.connect(func(index: int):
			if index <= 0: return
			_capture_draft()
			_related.append(str(available[index - 1].get("id", "")))
			_dirty = true
			save_draft()
			_build_content()
		)
		parent.add_child(picker)
	elif _related.is_empty(): _text(parent, "尚未关联", 18, true)

func _render_editor() -> void:
	_title_edit = LineEdit.new()
	_title_edit.name = "NoteTitle"
	_title_edit.text = _draft_title
	_title_edit.placeholder_text = "无题"
	_title_edit.custom_minimum_size.y = 50
	_title_edit.add_theme_font_size_override("font_size", _scaled(26))
	_title_edit.add_theme_color_override("font_color", _ink)
	_title_edit.add_theme_stylebox_override("normal", _style(Color.TRANSPARENT, false))
	_title_edit.add_theme_stylebox_override("focus", _style(Color(_accent, 0.035), false))
	_title_edit.text_changed.connect(func(_text: String): _on_edit())
	right_body.add_child(_title_edit)
	var judgement_row: HBoxContainer = _row(right_body, 4)
	for value in [["doubt", "待证"], ["true", "倾向为真"], ["false", "倾向为假"]]:
		var judgement: String = str(value[0])
		var button: Button = _button(judgement_row, str(value[1]), func(): _capture_draft(); _judgement = judgement; _dirty = true; save_draft(); _build_content(); status_changed.emit(_selected_id, judgement), _judgement == judgement)
		button.alignment = HORIZONTAL_ALIGNMENT_CENTER
		button.add_theme_font_size_override("font_size", _scaled(17))
	_editor = TextEdit.new()
	_editor.name = "NoteText"
	_editor.text = _draft_text
	_editor.placeholder_text = "写下你的猜想……"
	_editor.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	_editor.custom_minimum_size.y = 350
	_editor.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_editor.add_theme_font_size_override("font_size", _scaled(22))
	_editor.add_theme_color_override("font_color", _ink)
	_editor.add_theme_color_override("caret_color", _accent)
	_editor.add_theme_stylebox_override("normal", _style(Color.TRANSPARENT, false))
	_editor.add_theme_stylebox_override("focus", _style(Color(_accent, 0.025), false))
	_editor.add_theme_constant_override("line_spacing", 8)
	_editor.text_changed.connect(_on_edit)
	right_body.add_child(_editor)
	_editor.set_caret_line(_caret.y)
	_editor.set_caret_column(_caret.x)
	_editor.set_deferred("scroll_vertical", _scroll_position)
	var footer: HBoxContainer = _row(right_body)
	_save_status = _text(footer, "未保存" if _dirty else "已记下" if not _selected_id.is_empty() else "", 16, true)
	_save_status.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_save_button = _button(footer, "保存", func(): save_draft(); _build_content())
	_save_button.size_flags_horizontal = Control.SIZE_SHRINK_END
	_save_button.disabled = _draft_text.strip_edges().is_empty() or archive == null
	_deduction_button = _button(footer, "推演  ›", _deduce)
	_deduction_button.size_flags_horizontal = Control.SIZE_SHRINK_END
	_deduction_button.disabled = _draft_text.strip_edges().is_empty()

func _on_edit() -> void:
	_dirty = true
	if is_instance_valid(_save_status): _save_status.text = "未保存"
	if is_instance_valid(_save_button) and is_instance_valid(_editor): _save_button.disabled = _editor.text.strip_edges().is_empty() or archive == null
	if is_instance_valid(_deduction_button) and is_instance_valid(_editor): _deduction_button.disabled = _editor.text.strip_edges().is_empty()
	# Update only the in-memory casebook here. Disk writes remain host checkpoints.
	save_draft()

func save_draft() -> void:
	if _writing or not _draft_initialized: return
	_capture_draft()
	if not _dirty or archive == null: return
	if _draft_text.strip_edges().is_empty():
		var existing: Dictionary = _entry(_selected_id)
		if not existing.is_empty() and bool(existing.get("personal", false)):
			existing["text"] = ""
			existing["title"] = _draft_title.strip_edges() if not _draft_title.strip_edges().is_empty() else "无题"
			existing["related"] = _related.duplicate()
			existing["judgement"] = _judgement
			existing["relations"] = _relations.duplicate()
			archive.emit_signal("changed")
			_dirty = false
			if is_instance_valid(_save_status): _save_status.text = "已记下"
			note_saved.emit(_selected_id, "")
		return
	_writing = true
	var previous: Dictionary = _entry(_selected_id).duplicate(false)
	var id: String = archive.add_note(_draft_text, _related, _draft_title.strip_edges(), _selected_id)
	if not id.is_empty():
		_selected_id = id
		var note: Dictionary = _entry(id)
		note["judgement"] = _judgement
		note["relations"] = _relations.duplicate()
		if previous.has("rule_doubt"): note["rule_doubt"] = previous.rule_doubt
		archive.emit_signal("changed")
		_dirty = false
		if is_instance_valid(_save_status): _save_status.text = "已记下"
		note_saved.emit(id, _draft_text)
	_writing = false

func _deduce() -> void:
	save_draft()
	if _draft_text.strip_edges().is_empty(): return
	deduction_requested.emit(_selected_id)
	ui_event.emit({"kind": "open_reasoning", "hypothesis_id": _selected_id, "hypothesis": _draft_text, "related": _related.duplicate(), "relations": _relations.duplicate()})

func _exit_tree() -> void:
	save_draft()

func _reset_view() -> void:
	_new_note()
	_draft_initialized = false
