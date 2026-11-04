@tool
extends "res://scenes/UIpanel/CasebookUI/BookPageLayout.gd"
## Two actual documents on facing pages; no undiscovered rule is printed.
signal rule_selected(rule_id: String)
signal doubt_toggled(rule_id: String, is_doubtful: bool)
const CATEGORIES := {"rules": "规则", "concepts": "概念", "words": "词汇", "grammar": "语法"}
var _category := "rules"
var _selected_id := ""
var _selected: Dictionary = {}
var _rules: Array = []
var _page := 0
var _pinned: Dictionary = {}
var _comparison_line: VSeparator

func set_pinned(record: Dictionary) -> void:
	_pinned = record.duplicate(true)
	if is_node_ready(): _build_content()

func select_entry(id: String) -> void:
	_selected_id = id
	_collect()
	for item: Dictionary in _rules:
		if str(item.get("id", "")) == id:
			_category = str(item.get("book_category", "rules"))
			break
	if is_node_ready(): _build_content()

func _build_content() -> void:
	_collect()
	var entries: Array[Dictionary] = []
	for item: Dictionary in _rules:
		if str(item.get("book_category", "rules")) == _category: entries.append(item)
	if _selected.is_empty() and not entries.is_empty():
		_selected = entries[0]
		_selected_id = str(_selected.get("id", ""))
	var pinned := not _pinned.is_empty()
	_begin(_title(_pinned) if pinned else "知识与规则", _title(_selected, "规则"))
	if is_instance_valid(_comparison_line):
		remove_child(_comparison_line)
		_comparison_line.queue_free()
		_comparison_line = null
	if pinned:
		_comparison_line = VSeparator.new()
		_comparison_line.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_comparison_line.anchor_left = 0.5
		_comparison_line.anchor_right = 0.5
		_comparison_line.anchor_top = 0.18
		_comparison_line.anchor_bottom = 0.80
		_comparison_line.offset_left = -1
		_comparison_line.offset_right = 1
		var line := StyleBoxLine.new()
		line.color = Color("a7947f")
		line.thickness = 1
		line.vertical = true
		_comparison_line.add_theme_stylebox_override("separator", line)
		add_child(_comparison_line)
		_render_pinned_document()
	else:
		_render_index(entries)
	if _selected.is_empty():
		_spacer(right_body, 70)
		_text(right_body, "尚未收录", 36, true)
	else:
		_render_rule()

func _collect() -> void:
	_rules.clear()
	_selected = {}
	var seen: Dictionary = {}
	var book: Dictionary = _dict(snapshot.get("casebook", {}))
	var projected_viewer := str(book.get("viewer", snapshot.get("viewer", "")))
	if projected_viewer != str(snapshot.get("viewer", projected_viewer)): return
	for category in CATEGORIES:
		for value in _array(book.get(category, [])):
			if value is not Dictionary or not bool(value.get("acquired", value.get("known", false))): continue
			var item: Dictionary = value.duplicate(true)
			var id := str(item.get("id", ""))
			if id.is_empty() or seen.has(id): continue
			item["book_category"] = category
			_rules.append(item)
			seen[id] = true
	for value: Dictionary in _entries("evidence") + _entries("records"):
		var kind := str(value.get("kind", value.get("type", "")))
		if kind not in ["rule", "concept", "word", "grammar", "knowledge"]: continue
		var id := str(value.get("id", ""))
		if id.is_empty() or seen.has(id): continue
		var item := value.duplicate(true)
		item["book_category"] = {"rule": "rules", "knowledge": "rules", "concept": "concepts", "word": "words", "grammar": "grammar"}.get(kind, "rules")
		_rules.append(item)
		seen[id] = true
	for item: Dictionary in _rules:
		if str(item.get("id", "")) == _selected_id:
			_selected = item
			break

func _render_pinned_document() -> void:
	if left_page.get_child_count() > 0 and left_page.get_child(0) is HBoxContainer:
		var heading := left_page.get_child(0) as HBoxContainer
		var pin := Control.new()
		pin.custom_minimum_size = Vector2(42, 52)
		pin.mouse_filter = Control.MOUSE_FILTER_IGNORE
		pin.draw.connect(func():
			pin.draw_line(Vector2(14, 38), Vector2(29, 16), Color("79715e"), 7, true)
			pin.draw_line(Vector2(10, 44), Vector2(21, 29), Color("544e41"), 3, true)
			pin.draw_circle(Vector2(29, 15), 9, Color("514c40"))
			pin.draw_circle(Vector2(27, 13), 6, Color("96907b"))
		)
		heading.add_child(pin)
		heading.move_child(pin, 0)
	_spacer(left_body, 56)
	var content := _scroll(left_body)
	var original := _body(_pinned)
	if not original.is_empty(): _prose(content, original, 40)
	_text(left_body, _document_source(_pinned), 32)
	_spacer(left_body, 50)
	var release := _button(left_body, "取下", func():
		_pinned.clear()
		ui_event.emit({"kind": "unpin"})
		_build_content()
	)
	release.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	release.add_theme_font_size_override("font_size", _scaled(26))

func _render_index(entries: Array[Dictionary]) -> void:
	_spacer(left_body, 30)
	if entries.is_empty():
		_text(left_body, "尚未收录", 36, true)
		_spacer(left_body, 0, true)
	else:
		var total := maxi(1, ceili(float(entries.size()) / 6.0))
		_page = clampi(_page, 0, total - 1)
		for i in range(_page * 6, mini(entries.size(), (_page + 1) * 6)):
			var entry: Dictionary = entries[i]
			var button := _button(left_body, _title(entry), func(): _select(entry), str(entry.get("id", "")) == _selected_id)
			button.add_theme_font_size_override("font_size", _scaled(36))
			button.custom_minimum_size.y = 64
		_spacer(left_body, 0, true)
		_pager(left_body, _page, total, func(index: int):
			_page = index
			_build_content()
		)
	var categories := _row(left_body, 12)
	for key in CATEGORIES:
		var category := str(key)
		var button := _button(categories, str(CATEGORIES[key]), func():
			_category = category
			_selected_id = ""
			_page = 0
			_build_content()
		, _category == category)
		button.add_theme_font_size_override("font_size", _scaled(26))
		button.custom_minimum_size.x = 78
		button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN

func _render_rule() -> void:
	_spacer(right_body, 56)
	var content := _scroll(right_body)
	var original := _body(_selected)
	if not original.is_empty(): _prose(content, original, 40)
	var formula := str(_selected.get("grammar", _selected.get("formula", "")))
	if not formula.is_empty() and formula != original:
		_spacer(content, 24)
		_prose(content, formula, 36)
	var basis: Variant = _selected.get("basis", _selected.get("cognitive_basis", ""))
	if basis is String and not basis.is_empty():
		_spacer(content, 28)
		_prose(content, str(basis), 32)
	elif basis is Array:
		for value in basis:
			_text(content, _title(value) if value is Dictionary else str(value), 32, true)
	var doubts: Array = _array(_selected.get("doubts", []))
	for doubt in doubts:
		var text := _text(content, "？ " + str(doubt), 32)
		text.add_theme_color_override("font_color", _red)
	_text(right_body, _document_source(_selected), 32)
	_spacer(right_body, 50)
	var footer := _row(right_body)
	var marked := _is_marked()
	var mark := _button(footer, "已存疑" if marked else "存疑", _toggle_doubt, marked)
	mark.add_theme_font_size_override("font_size", _scaled(26))
	mark.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	var source_refs: Array = _array(_selected.get("source_refs", _selected.get("related", [])))
	for reference in source_refs:
		var id := str(reference.get("id", "")) if reference is Dictionary else str(reference)
		var source := _entry(id)
		if source.is_empty(): continue
		var source_button := _button(footer, "查阅来源", func(): ui_event.emit({"kind": "book_page", "page": "B01", "entry_id": id}))
		source_button.add_theme_font_size_override("font_size", _scaled(26))
		source_button.size_flags_horizontal = Control.SIZE_SHRINK_END
		break

func _document_source(record: Dictionary) -> String:
	var source := str(record.get("source_label", record.get("origin", ""))).strip_edges()
	if source.is_empty(): source = _source(record)
	return "—— 来源：" + source

func _select(record: Dictionary) -> void:
	_selected_id = str(record.get("id", ""))
	_selected = record
	_build_content()
	rule_selected.emit(_selected_id)

func _is_marked() -> bool:
	var note := _entry("rule-doubt:" + _selected_id)
	return bool(note.get("rule_doubt", false))

func _toggle_doubt() -> void:
	if _selected_id.is_empty() or archive == null: return
	var marked := not _is_marked()
	var note_id := "rule-doubt:" + _selected_id
	if marked: archive.add_note("待核验：" + _title(_selected), [_selected_id], "存疑 · " + _title(_selected), note_id)
	var note := _entry(note_id)
	if not note.is_empty():
		note["rule_doubt"] = marked
		note["judgement"] = "doubt" if marked else "unmarked"
		archive.emit_signal("changed")
	doubt_toggled.emit(_selected_id, marked)
	_build_content()

func _reset_view() -> void:
	_selected_id = ""
	_selected.clear()
	_rules.clear()
	_pinned.clear()
	_page = 0
