extends TextEdit
## Ghost text is spelling assistance from the public vocabulary, never a plan.
const Language = preload("res://scenes/FirstAwakening/MindLanguage.gd")
var _tokens: Array[String] = []
var _default_person := ""
var _people: Array = []
var _locale := "zh"
var _example := ""
var _default_person_valid := false

func configure(catalog: Dictionary, default_person: String = "") -> void:
	_default_person = default_person
	_people = catalog.get("people", [])
	_locale = catalog.get("locale", "zh")
	_example = Language.example(_locale)
	_default_person_valid = _people.has(Language.display_person(default_person, _locale))
	placeholder_text = _example
	_tokens.clear()
	for person in _people: _add_token(person)
	for category in ["verbs", "targets"]:
		for entry in catalog.get(category, []):
			_add_token(entry.word)
			for alias in entry.aliases: _add_token(alias)
	for form in catalog.get("condition_forms", []):
		_add_token(form.word)
		for alias in form.aliases: _add_token(alias)
	for word in catalog.get("order_words", []): _add_token(word)
	_tokens.sort_custom(func(a: String, b: String): return a.length() > b.length())
	queue_redraw()

func _add_token(word: String) -> void:
	var token := word.strip_edges().replace("　", "")
	if _locale != "en": token = token.replace(" ", "")
	if not _tokens.has(token): _tokens.append(token)

func _ready() -> void:
	placeholder_text = _example
	add_theme_color_override("font_placeholder_color", Color("7a8076"))
	tab_input_mode = false
	text_changed.connect(queue_redraw)
	caret_changed.connect(queue_redraw)
	focus_entered.connect(queue_redraw)
	focus_exited.connect(queue_redraw)
	get_v_scroll_bar().value_changed.connect(func(_value): queue_redraw())
	get_h_scroll_bar().value_changed.connect(func(_value): queue_redraw())

func get_completion_suffix() -> String:
	if not editable or has_selection() or get_caret_count() != 1 or has_ime_text(): return ""
	if text.is_empty(): return _example if _default_person_valid else ""
	var line := get_line(get_caret_line())
	if get_caret_column() != line.length(): return ""
	if line.strip_edges().begins_with("#") or line.strip_edges().begins_with("＃"): return ""
	var fragment := line
	var boundaries := ["。", ".", "；", ";", "，", ",", "然后", "　", "\t"]
	if _locale != "en": boundaries.append(" ")
	for mark in boundaries:
		var boundary := fragment.rfind(mark)
		if boundary >= 0: fragment = fragment.substr(boundary + mark.length())
	# Consume only complete, published tokens. Unknown prose has no completion.
	while not fragment.is_empty():
		fragment = fragment.strip_edges()
		if _tokens.has(fragment): return ""
		var candidates: Array[String] = []
		for token in _tokens:
			if token.begins_with(fragment): candidates.append(token)
		if not candidates.is_empty():
			var common: String = candidates[0]
			for candidate in candidates:
				while not candidate.begins_with(common): common = common.left(-1)
			return common.substr(fragment.length())
		var consumed := false
		for token in _tokens:
			if fragment.begins_with(token):
				fragment = fragment.trim_prefix(token).strip_edges()
				consumed = true
				break
		if not consumed: return ""
	return ""

func _gui_input(event: InputEvent) -> void:
	if event is not InputEventKey or not event.pressed or event.echo: return
	if event.keycode != KEY_TAB or event.shift_pressed or event.ctrl_pressed or event.alt_pressed or event.meta_pressed: return
	var suffix := get_completion_suffix()
	if suffix.is_empty(): return
	if not text.is_empty() and not _completion_fits(suffix): return
	insert_text_at_caret(suffix)
	text_changed.emit()
	accept_event()
	queue_redraw()

func _draw() -> void:
	if not editable or not has_focus(): return
	var suffix := get_completion_suffix()
	if suffix.is_empty() or text.is_empty() or not _completion_fits(suffix): return
	var font := get_theme_font("font")
	var font_size := get_theme_font_size("font_size")
	var grey := Color("7a8076")
	var caret_rect := get_rect_at_line_column(get_caret_line(), get_caret_column())
	var at := Vector2(caret_rect.position) + Vector2(0, font.get_ascent(font_size))
	draw_string(font, at, suffix, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size, grey)

func _completion_fits(suffix: String) -> bool:
	var caret_rect := get_rect_at_line_column(get_caret_line(), get_caret_column())
	if caret_rect.position.x < 0 or caret_rect.position.y < 0: return false
	var font := get_theme_font("font")
	var font_size := get_theme_font_size("font_size")
	var width := size.x - caret_rect.position.x - get_theme_stylebox("normal").get_margin(SIDE_RIGHT) - 4.0
	if get_v_scroll_bar().visible: width -= get_v_scroll_bar().size.x
	# Tab must never accept a suggestion that was hidden beyond the page edge.
	return font.get_string_size(suffix, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x <= width
