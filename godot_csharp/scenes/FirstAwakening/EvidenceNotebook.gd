extends VBoxContainer
## Recorded observations only. Acquisition and story state remain in the chapter host.

const ASSETS := "res://ShareAsset/Internal/Props/EvidenceNotebook/"
const ZH_FONT = preload("res://ShareAsset/External/Yozai/Yozai-Regular.ttf")
const JA_FONT = preload("res://ShareAsset/External/ZenKurenaido/ZenKurenaido-Regular.ttf")
const INK := Color("2b2016")
const ACTIONS := {
	"zh_CN": ["返回检视", "返回证物页", "返回现场", "返回推演"],
	"en": ["Inspection", "Evidence", "Scene", "Deduction"],
	"ja": ["調査へ", "証拠一覧へ", "現場へ", "推理へ"]
}

var _on_record_selected: Callable

func set_records(records: Array, current_id: String, on_selected: Callable) -> void:
	var selector: OptionButton = %RecordSelector
	selector.clear()
	_on_record_selected = on_selected
	if not selector.item_selected.is_connected(_select_record):
		selector.item_selected.connect(_select_record)
	var current_index := -1
	for record in records:
		var index := selector.item_count
		var record_id := str(record[0])
		selector.add_item(tr(str(record[1])))
		selector.set_item_metadata(index, record_id)
		if record_id == current_id:
			current_index = index
	selector.select(current_index)
	selector.visible = selector.item_count > 1

func _select_record(index: int) -> void:
	if _on_record_selected.is_valid():
		_on_record_selected.call(str(%RecordSelector.get_item_metadata(index)))

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
	%Number.text = evidence_id
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
