@tool
extends Control
## Reference-aligned double-page composition. Native controls are projected onto curved 3D paper.
const PaperStyle := preload("res://scenes/components/shared/MaterialControls.gd")
signal ui_event(event: Dictionary)
signal close_requested

var snapshot: Dictionary = {}
var archive: RefCounted
var left_page: VBoxContainer
var right_page: VBoxContainer
var left_body: VBoxContainer
var right_body: VBoxContainer
var _layout: MarginContainer
var _ink: Color = Color("2e2923")
var _muted: Color = Color("736552")
var _accent: Color = Color("565f4c")
var _red: Color = Color("924a3d")

func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	PaperStyle.apply(self)
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	size_flags_vertical = Control.SIZE_EXPAND_FILL
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_build_content()

func configure(data: Dictionary, book_data: RefCounted = null) -> void:
	var previous_viewer: String = str(snapshot.get("viewer", ""))
	if (not previous_viewer.is_empty() and previous_viewer != str(data.get("viewer", ""))) or (archive != null and archive != book_data): _reset_view()
	snapshot = data.duplicate(false)
	archive = book_data
	if is_node_ready(): _build_content()

func _reset_view() -> void:
	pass

func refresh() -> void:
	if is_node_ready(): _build_content()

func _build_content() -> void:
	_begin("档案", "")

func _begin(left_title: String, right_title: String, caption := "") -> void:
	if is_instance_valid(_layout):
		remove_child(_layout)
		_layout.queue_free()
	var settings: Node = get_node_or_null("/root/GameSettings")
	var contrast: bool = settings != null and bool(settings.get_value("accessibility", "high_contrast"))
	_ink = Color("191710") if contrast else Color("2e2923")
	_muted = Color("5e5142") if contrast else Color("736552")
	_layout = MarginContainer.new()
	_layout.name = "SpreadLayout"
	_layout.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_layout.add_theme_constant_override("margin_left", 130)
	_layout.add_theme_constant_override("margin_right", 100)
	_layout.add_theme_constant_override("margin_top", 86)
	_layout.add_theme_constant_override("margin_bottom", 100)
	add_child(_layout)
	var spread: HBoxContainer = HBoxContainer.new()
	spread.add_theme_constant_override("separation", 140)
	_layout.add_child(spread)
	left_page = _column(spread, 42)
	left_page.name = "LeftPage"
	right_page = _column(spread, 42)
	right_page.name = "RightPage"
	_heading(left_page, left_title, caption)
	_heading(right_page, right_title)
	left_body = _column(left_page, 24)
	left_body.name = "LeftBody"
	left_body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right_body = _column(right_page, 24)
	right_body.name = "RightBody"
	right_body.size_flags_vertical = Control.SIZE_EXPAND_FILL

func _heading(parent: Node, title: String, caption := "") -> void:
	var row: HBoxContainer = HBoxContainer.new()
	row.add_theme_constant_override("separation", 14)
	parent.add_child(row)
	var label: Label = _text(row, title, 52)
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	if not caption.is_empty():
		var small: Label = _text(row, caption, 15, true)
		small.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM

func _column(parent: Node, separation := 14) -> VBoxContainer:
	var result: VBoxContainer = VBoxContainer.new()
	result.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	result.add_theme_constant_override("separation", separation)
	parent.add_child(result)
	return result

func _row(parent: Node, separation := 12) -> HBoxContainer:
	var result: HBoxContainer = HBoxContainer.new()
	result.add_theme_constant_override("separation", separation)
	parent.add_child(result)
	return result

func _text(parent: Node, value: String, font_size := 40, muted := false) -> Label:
	var label: Label = Label.new()
	label.text = value
	label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.add_theme_font_size_override("font_size", _scaled(font_size))
	label.add_theme_color_override("font_color", _muted if muted else _ink)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(label)
	return label

func _prose(parent: Node, value: String, font_size := 40) -> RichTextLabel:
	var label: RichTextLabel = RichTextLabel.new()
	label.text = value
	label.bbcode_enabled = false
	label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	label.fit_content = true
	label.scroll_active = false
	label.selection_enabled = true
	label.add_theme_font_size_override("normal_font_size", _scaled(font_size))
	label.add_theme_color_override("default_color", _ink)
	label.add_theme_constant_override("line_separation", 18)
	parent.add_child(label)
	return label

func _scaled(value: int) -> int:
	var settings: Node = get_node_or_null("/root/GameSettings")
	var factor: float = 1.0
	if settings != null:
		var setting: Variant = settings.get_value("accessibility", "text_scale")
		if setting != null: factor = clampf(float(setting), 0.85, 1.5)
		var ui_scale: Variant = settings.get_value("graphics", "ui_scale")
		if ui_scale != null: factor *= clampf(float(ui_scale), 0.85, 1.25)
	return roundi(value * factor)

func _button(parent: Node, caption: String, action: Callable, selected := false) -> Button:
	var button: Button = Button.new()
	button.text = caption
	button.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.custom_minimum_size = Vector2(0, 46)
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.focus_mode = Control.FOCUS_ALL
	button.clip_text = true
	button.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	button.add_theme_font_size_override("font_size", _scaled(21))
	button.add_theme_color_override("font_color", _ink)
	button.add_theme_color_override("font_hover_color", _ink)
	button.add_theme_color_override("font_pressed_color", _accent)
	button.add_theme_color_override("font_disabled_color", Color(_muted, 0.6))
	button.add_theme_stylebox_override("normal", _style(Color(_accent, 0.10 if selected else 0.0), selected))
	button.add_theme_stylebox_override("hover", _style(Color(_accent, 0.08), false))
	button.add_theme_stylebox_override("pressed", _style(Color(_accent, 0.15), true))
	button.add_theme_stylebox_override("disabled", _style(Color.TRANSPARENT, false))
	var focus: StyleBoxFlat = _style(Color.TRANSPARENT, false)
	focus.border_width_bottom = 2
	focus.border_color = _accent
	button.add_theme_stylebox_override("focus", focus)
	if action.is_valid(): button.pressed.connect(action)
	parent.add_child(button)
	return button

func _style(color: Color, marked: bool) -> StyleBoxFlat:
	var style: StyleBoxFlat = StyleBoxFlat.new()
	style.bg_color = color
	style.content_margin_left = 12
	style.content_margin_right = 12
	style.content_margin_top = 7
	style.content_margin_bottom = 7
	style.border_width_left = 2 if marked else 0
	style.border_color = _accent
	return style

func _rule(parent: Node) -> void:
	var rule: HSeparator = HSeparator.new()
	var style: StyleBoxLine = StyleBoxLine.new()
	style.color = Color(_muted, 0.25)
	style.thickness = 1
	rule.add_theme_stylebox_override("separator", style)
	parent.add_child(rule)

func _spacer(parent: Node, height := 0.0, expand := false) -> Control:
	var control: Control = Control.new()
	control.custom_minimum_size.y = height
	control.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if expand: control.size_flags_vertical = Control.SIZE_EXPAND_FILL
	parent.add_child(control)
	return control

func _scroll(parent: Node) -> VBoxContainer:
	var scroll: ScrollContainer = ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	parent.add_child(scroll)
	return _column(scroll, 18)

func _pager(parent: Node, index: int, total: int, action: Callable) -> void:
	if total <= 1: return
	var row: HBoxContainer = _row(parent)
	var previous: Button = _button(row, "‹", func(): action.call(index - 1))
	previous.disabled = index <= 0
	previous.custom_minimum_size.x = 42
	previous.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	var count: Label = _text(row, "%d / %d" % [index + 1, total], 16, true)
	count.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	count.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var next: Button = _button(row, "›", func(): action.call(index + 1))
	next.disabled = index >= total - 1
	next.custom_minimum_size.x = 42
	next.size_flags_horizontal = Control.SIZE_SHRINK_END

func _entries(section: String) -> Array:
	if archive != null and archive.has_method("list_section"): return archive.list_section(section)
	var result: Array = []
	var projected: Dictionary = _dict(snapshot.get("casebook", {}))
	for value in _array(projected.get(section, [])):
		if value is Dictionary and bool(value.get("acquired", false)): result.append(value)
	return result

func _entry(id: String) -> Dictionary:
	if archive != null and archive.has_method("find_entry"): return archive.find_entry(id)
	return {}

func _dict(value: Variant) -> Dictionary:
	return value if value is Dictionary else {}

func _array(value: Variant) -> Array:
	return value if value is Array else []

func _title(value: Dictionary, fallback := "未题名") -> String:
	return str(value.get("title", value.get("name", fallback)))

func _body(value: Dictionary) -> String:
	return str(value.get("text", value.get("body", value.get("desc", value.get("notes", "")))))

func _time(value: Variant) -> String:
	if archive != null and archive.has_method("time_text"): return archive.time_text(value)
	var text: String = str(value)
	if text.is_valid_float():
		var minute: int = floori(float(text))
		return "%02d:%02d" % [posmod(minute / 60, 24), posmod(minute, 60)]
	return text if not text.is_empty() else "—"

func _source(value: Dictionary) -> String:
	var category: String = str(value.get("category", ""))
	var certainty: String = str(value.get("certainty", ""))
	var kind: String = "记录"
	if category == "testimony" or certainty == "subjective_statement": kind = "听闻"
	elif category == "hypotheses" or certainty == "inference" or bool(value.get("personal", false)): kind = "推测"
	elif str(value.get("voice", "")) == "visible-inspection" or category == "evidence": kind = "亲见"
	var pieces: Array[String] = [kind]
	var speaker: String = str(value.get("speaker_name", value.get("source_label", "")))
	if not speaker.is_empty(): pieces.append(speaker)
	var place: String = str(value.get("place", ""))
	if not place.is_empty(): pieces.append(place)
	var moment: String = _time(value.get("game_time", ""))
	if moment != "—": pieces.append(moment)
	return " · ".join(pieces)

func _texture(path: String) -> Texture2D:
	if path.begins_with("res://") and ResourceLoader.exists(path): return load(path) as Texture2D
	if path.begins_with("user://") and FileAccess.file_exists(path):
		var image: Image = Image.load_from_file(path)
		if image != null and not image.is_empty(): return ImageTexture.create_from_image(image)
	return null

func _picture(parent: Node, texture: Texture2D, height := 240.0) -> TextureRect:
	var image: TextureRect = TextureRect.new()
	image.texture = texture
	image.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	image.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	image.custom_minimum_size = Vector2(0, height)
	image.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(image)
	return image

func _empty(parent: Node, caption := "尚未收录") -> void:
	_spacer(parent, 26)
	_symbol(parent, "folio", 94)
	var label: Label = _text(parent, caption, 20, true)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_spacer(parent, 0, true)

func _symbol(parent: Node, kind: String, extent := 64.0) -> Control:
	var glyph: Control = Control.new()
	glyph.custom_minimum_size = Vector2(extent, extent)
	glyph.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	glyph.mouse_filter = Control.MOUSE_FILTER_IGNORE
	glyph.draw.connect(func(): _draw_symbol(glyph, kind))
	parent.add_child(glyph)
	return glyph

func _draw_symbol(glyph: Control, kind: String) -> void:
	var s: Vector2 = glyph.size
	var c: Color = Color(_accent, 0.62)
	if kind == "person":
		glyph.draw_circle(s * Vector2(0.5, 0.27), s.x * 0.14, c, false, 1.7, true)
		glyph.draw_polyline(PackedVector2Array([s * Vector2(0.22, 0.83), s * Vector2(0.26, 0.62), s * Vector2(0.40, 0.51), s * Vector2(0.50, 0.59), s * Vector2(0.60, 0.51), s * Vector2(0.74, 0.62), s * Vector2(0.78, 0.83)]), c, 1.7, true)
	elif kind == "seal":
		glyph.draw_arc(s * 0.5, s.x * 0.28, 0, TAU, 12, c, 1.6, true)
		glyph.draw_line(s * Vector2(0.36, 0.50), s * Vector2(0.46, 0.61), c, 1.7, true)
		glyph.draw_line(s * Vector2(0.46, 0.61), s * Vector2(0.67, 0.36), c, 1.7, true)
	else:
		var points: PackedVector2Array = PackedVector2Array([s * Vector2(0.26, 0.18), s * Vector2(0.64, 0.18), s * Vector2(0.78, 0.32), s * Vector2(0.78, 0.82), s * Vector2(0.26, 0.82), s * Vector2(0.26, 0.18)])
		glyph.draw_polyline(points, c, 1.6, true)
		glyph.draw_polyline(PackedVector2Array([s * Vector2(0.64, 0.18), s * Vector2(0.64, 0.32), s * Vector2(0.78, 0.32)]), c, 1.2, true)
		for y in [0.47, 0.57, 0.67]: glyph.draw_line(s * Vector2(0.36, y), s * Vector2(0.65, y), Color(c, 0.42), 1.1, true)

func _confirm(title: String, action: Dictionary, caption := "确认") -> void:
	ui_event.emit({"kind": "confirm", "title": title, "confirm_text": caption, "action": action})
