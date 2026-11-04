@tool
extends "res://scenes/UIpanel/CasebookUI/BookPageLayout.gd"

signal plate_selected(plate_id: String)
signal fullscreen_view_requested(plate_id: String)

var _plates: Array = []
var _selected_id: String = ""
var _selected: Dictionary = {}
var _page: int = 0
var _filter: String = "all"
const PAGE_SIZE := 6

func _build_content() -> void:
	_collect()
	var plates: Array = []
	for value: Dictionary in _plates:
		if _filter == "all" or str(value.get("plate_type", value.get("kind", ""))) == _filter: plates.append(value)
	_selected = {}
	for value: Dictionary in plates:
		if str(value.get("id", "")) == _selected_id: _selected = value
	if _selected.is_empty() and not plates.is_empty():
		_selected = plates[0]
		_selected_id = str(_selected.get("id", ""))
	_begin("图版收藏", _title(_selected, "图版"), "%02d" % _plates.size())
	var tabs: HBoxContainer = _row(left_body, 4)
	for value in [["all", "全部"], ["illustration", "插画"], ["document", "图录"]]:
		var kind: String = str(value[0])
		var tab: Button = _button(tabs, str(value[1]), func(): _filter = kind; _page = 0; _build_content(), _filter == kind)
		tab.add_theme_font_size_override("font_size", _scaled(18))
		tab.alignment = HORIZONTAL_ALIGNMENT_CENTER
	if plates.is_empty(): _empty(left_body, "尚无图版")
	else: _render_grid(plates)
	if _selected.is_empty():
		_empty(right_body)
		return
	var image_path: String = str(_selected.get("image", _selected.get("texture", "")))
	var texture: Texture2D = _texture(image_path)
	if texture != null:
		_picture(right_body, texture, 470)
	else:
		_symbol(right_body, "folio", 160)
		_text(right_body, "图像暂不可用", 18, true)
	var origin: String = str(_selected.get("origin", _selected.get("source_label", "")))
	if not origin.is_empty(): _text(right_body, origin, 17, true)
	var description: String = _body(_selected)
	if not description.is_empty(): _prose(_scroll(right_body), description, 20)
	else: _spacer(right_body, 0, true)
	_text(right_body, _source(_selected), 15, true)
	var fullscreen: Button = _button(right_body, "全屏检视", _fullscreen)
	fullscreen.disabled = texture == null

func _collect() -> void:
	_plates.clear()
	var seen: Dictionary = {}
	for value: Dictionary in _entries("gallery"):
		var id: String = str(value.get("id", ""))
		if id.is_empty() or seen.has(id): continue
		_plates.append(value)
		seen[id] = true
	for value in _array(_dict(snapshot.get("casebook", {})).get("gallery", [])):
		if not value is Dictionary or not bool(value.get("acquired", value.get("unlocked", false))): continue
		var id: String = str(value.get("id", ""))
		if not id.is_empty() and not seen.has(id):
			_plates.append(value)
			seen[id] = true

func _render_grid(plates: Array) -> void:
	var total: int = maxi(1, ceili(plates.size() / float(PAGE_SIZE)))
	_page = clampi(_page, 0, total - 1)
	var grid: GridContainer = GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 18)
	grid.add_theme_constant_override("v_separation", 20)
	left_body.add_child(grid)
	for index in range(_page * PAGE_SIZE, mini(plates.size(), (_page + 1) * PAGE_SIZE)):
		var value: Dictionary = plates[index]
		var id: String = str(value.get("id", ""))
		var tile: Button = _button(grid, "", func(): _selected_id = id; _build_content(); plate_selected.emit(id), id == _selected_id)
		tile.custom_minimum_size = Vector2(200, 172)
		tile.tooltip_text = _title(value)
		var margin: MarginContainer = MarginContainer.new()
		margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		margin.add_theme_constant_override("margin_left", 10)
		margin.add_theme_constant_override("margin_right", 10)
		margin.add_theme_constant_override("margin_top", 8)
		margin.add_theme_constant_override("margin_bottom", 8)
		margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
		tile.add_child(margin)
		var content: VBoxContainer = _column(margin, 6)
		content.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var texture: Texture2D = _texture(str(value.get("image", value.get("texture", ""))))
		if texture != null: _picture(content, texture, 108)
		else: _symbol(content, "folio", 108)
		var caption: Label = _text(content, _title(value), 17)
		caption.max_lines_visible = 2
		caption.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	var known_total: int = int(_dict(snapshot.get("casebook", {})).get("gallery_total", _plates.size()))
	if _filter == "all" and _page == total - 1:
		var placeholders: int = mini(PAGE_SIZE - (plates.size() % PAGE_SIZE if plates.size() % PAGE_SIZE != 0 else PAGE_SIZE), maxi(0, known_total - _plates.size()))
		for index in placeholders:
			var tile: Button = _button(grid, "未收录", Callable())
			tile.custom_minimum_size = Vector2(200, 172)
			tile.alignment = HORIZONTAL_ALIGNMENT_CENTER
			tile.disabled = true
	_spacer(left_body, 0, true)
	_pager(left_body, _page, total, func(index: int): _page = index; _build_content())

func _fullscreen() -> void:
	if _selected.is_empty(): return
	var path: String = str(_selected.get("image", _selected.get("texture", "")))
	if _texture(path) == null: return
	fullscreen_view_requested.emit(_selected_id)
	ui_event.emit({"kind": "gallery_fullscreen", "image": path, "title": _title(_selected), "source": _source(_selected)})

func _reset_view() -> void:
	_selected_id = ""
	_selected = {}
	_page = 0
