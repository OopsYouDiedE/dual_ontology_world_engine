extends VBoxContainer
## Recorded observations only. Acquisition and story state remain in the chapter host.

const ASSETS := "res://ShareAsset/Internal/Props/EvidenceNotebook/"
const ZH_FONT = preload("res://ShareAsset/External/Yozai/Yozai-Regular.ttf")
const JA_FONT = preload("res://ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf")
const INK := Color("2b2016")
const FieldModal = preload("res://scenes/FirstAwakening/FieldModal.gd")
const ACTIONS := {
	"zh_CN": ["返回检视", "返回证物页", "返回现场", "返回推演"],
	"en": ["Inspection", "Evidence", "Scene", "Deduction"],
	"ja": ["調査へ", "証拠一覧へ", "現場へ", "推理へ"]
}

var _on_record_selected: Callable
var _record_ids: Array[String] = []
var _current_index := -1
var _nav: HBoxContainer
var _page_label: Label
var _previous: Button
var _next: Button
var _contents: MenuButton

func set_records(records: Array, current_id: String, on_selected: Callable) -> void:
	# The title already names this page; the row below only turns pages and lists contents.
	%RecordSelector.hide()
	_on_record_selected = on_selected
	_record_ids.clear()
	_current_index = -1
	_build_nav()
	var menu := _contents.get_popup()
	menu.clear()
	for record in records:
		var record_id := str(record[0])
		if record_id == current_id: _current_index = _record_ids.size()
		menu.add_item(_display_number(record_id) + "  " + tr(str(record[1])), _record_ids.size())
		_record_ids.append(record_id)
	_nav.visible = _record_ids.size() > 1
	_page_label.text = "%d / %d" % [_current_index + 1, _record_ids.size()]
	_previous.disabled = _current_index <= 0
	_next.disabled = _current_index < 0 or _current_index >= _record_ids.size() - 1

## Turns to a neighbouring record; returns false at either end of the book.
func step(delta: int) -> bool:
	var target := _current_index + delta
	if _current_index < 0 or target < 0 or target >= _record_ids.size(): return false
	_select_record(target)
	return true

## Room notes have no case exhibit number; the player sees where they were written instead.
static func _display_number(evidence_id: String) -> String:
	return "五号房" if evidence_id.begins_with("ROOM_") else evidence_id

func _select_record(index: int) -> void:
	if _on_record_selected.is_valid() and index >= 0 and index < _record_ids.size():
		_on_record_selected.call(_record_ids[index])

func _build_nav() -> void:
	if is_instance_valid(_nav): return
	_nav = HBoxContainer.new()
	_nav.add_theme_constant_override("separation", 8)
	%RecordSelector.add_sibling(_nav)
	_previous = _nav_button("‹")
	_previous.tooltip_text = "上一页（←）"
	_previous.pressed.connect(func(): step(-1))
	_page_label = Label.new()
	_page_label.custom_minimum_size.x = 64
	_page_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_page_label.add_theme_color_override("font_color", INK)
	_page_label.add_theme_font_size_override("font_size", 18)
	_nav.add_child(_page_label)
	_next = _nav_button("›")
	_next.tooltip_text = "下一页（→）"
	_next.pressed.connect(func(): step(1))
	var space := Control.new()
	space.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_nav.add_child(space)
	_contents = MenuButton.new()
	_contents.text = "目录 ▾"
	_contents.flat = false
	_style_nav(_contents)
	_contents.get_popup().id_pressed.connect(_select_record)
	FieldModal.paper_popup(_contents.get_popup())
	_nav.add_child(_contents)

func _nav_button(glyph: String) -> Button:
	var button := Button.new()
	button.text = glyph
	button.custom_minimum_size = Vector2(40, 34)
	button.focus_mode = Control.FOCUS_NONE
	_style_nav(button)
	_nav.add_child(button)
	return button

func _style_nav(button: Button) -> void:
	var selector: OptionButton = %RecordSelector
	for state in ["normal", "hover", "pressed", "focus", "disabled"]:
		button.add_theme_stylebox_override(state, selector.get_theme_stylebox("normal" if state in ["normal", "disabled"] else "focus"))
	for color in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		button.add_theme_color_override(color, selector.get_theme_color(color))
	button.add_theme_color_override("font_disabled_color", Color(INK, 0.3))
	button.add_theme_font_size_override("font_size", 18)

func configure(evidence_id: String, title: String, paragraphs: Array, actions: Array) -> void:
	var locale: String = GameLocale.current_language()
	var english := JA_FONT.duplicate() as Font
	english.fallbacks = [ZH_FONT]
	var face: Font = JA_FONT if locale == "ja" else english if locale == "en" else ZH_FONT
	var notebook_theme := Theme.new()
	notebook_theme.default_font = face
	notebook_theme.default_font_size = 22
	notebook_theme.set_color("default_color", "RichTextLabel", INK)
	notebook_theme.set_font_size("normal_font_size", "RichTextLabel", 22)
	theme = notebook_theme
	%Number.text = _display_number(evidence_id)
	%Title.text = tr(title)
	# Never replace unread records with demonstration copy or infer new conclusions.
	var lines: PackedStringArray = []
	for paragraph in paragraphs:
		lines.append(tr(str(paragraph)))
	%Body.text = "\n\n".join(lines)
	%Coverage.visible = locale != "zh_CN"
	%Coverage.text = "Case record currently in Chinese." if locale == "en" else "事件の記録本文は現在中国語です。"
	%Body.scroll_to_line(0)
	var main_path := ASSETS + ("wedge-paper.png" if evidence_id == "E07" else evidence_id + ".png")
	_show_sketch(%Sketch, main_path, 2)
	_show_sketch(%Detail, ASSETS + "tear-detail.png", 2 if evidence_id == "E07" else 0)
	var captions: Array = ACTIONS.get(locale, ACTIONS.zh_CN)
	for button in %Actions.get_children():
		button.hide()
		# These dedicated buttons are rebound whenever the host reuses this notebook.
		for connection in button.pressed.get_connections():
			button.pressed.disconnect(connection["callable"])
	for action in actions:
		var index := int(action[0])
		var button: Button = %Actions.get_child(index)
		button.show()
		for state in ["normal", "hover", "pressed", "focus"]:
			button.add_theme_stylebox_override(state, %Actions.get_child(0).get_theme_stylebox(state))
		for color in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
			button.add_theme_color_override(color, %Actions.get_child(0).get_theme_color(color))
		button.text = str(captions[index])
		button.pressed.connect(action[1])

func _show_sketch(rect: TextureRect, path: String, scale_factor: int) -> void:
	rect.visible = scale_factor > 0 and ResourceLoader.exists(path)
	if not rect.visible: return
	var image := load(path) as Texture2D
	if image == null:
		rect.hide()
		return
	rect.texture = image
	# Integer scaling avoids interpolation and preserves the authored pixel grid.
	rect.custom_minimum_size = image.get_size() * scale_factor

