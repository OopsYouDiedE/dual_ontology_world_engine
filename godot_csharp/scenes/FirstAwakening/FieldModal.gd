extends PanelContainer
## Owns the investigation panel's visual layout and temporary controls.

const EvidenceDiagram = preload("res://scenes/FirstAwakening/EvidenceDiagram.gd")
const EvidenceDetail = preload("res://scenes/FirstAwakening/EvidenceDetail.tscn")
@onready var content: VBoxContainer = $ModalScroll/ModalBox
@export var regular_bounds: Rect2
@export var detail_bounds: Rect2
@export var answer_bounds: Rect2
@export var flow_bounds: Rect2
@export var regular_panel_style: StyleBoxFlat
@export var flow_panel_style: StyleBox
@export var answer_panel_style: StyleBoxFlat

var current_mode := ""

func style(color: Color, border := Color("63706b")) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = color
	box.border_color = border
	box.set_border_width_all(2)
	box.set_corner_radius_all(4)
	box.set_content_margin_all(10)
	return box

func add_label(parent: Node, words: String, size := 20, color := Color("e6dfcc")) -> Label:
	var label := Label.new()
	label.text = tr(words)
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", _flow_text_color(color) if current_mode == "flow" else color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	parent.add_child(label)
	return label

func add_button(parent: Node, words: String, action: Callable, disabled := false) -> Button:
	var button := Button.new()
	button.text = tr(words)
	button.disabled = disabled
	button.custom_minimum_size = Vector2(0, 46)
	button.add_theme_font_size_override("font_size", 19)
	button.add_theme_color_override("font_color", Color("293a3d") if current_mode == "flow" else Color("e6dfcc"))
	button.add_theme_stylebox_override("normal", style(Color("e5e2d8"), Color("c9cbc4")) if current_mode == "flow" else style(Color("313a3d")))
	button.add_theme_stylebox_override("hover", style(Color("d8e2db"), Color("6f8f80")) if current_mode == "flow" else style(Color("495751"), Color("a5ad8e")))
	button.pressed.connect(action)
	parent.add_child(button)
	return button

func _flow_text_color(color: Color) -> Color:
	return Color("587667") if color == Color("cfba8d") else Color("293a3d")

func open_content(title: String, next_mode: String, locale_theme: Theme) -> void:
	for child in content.get_children():
		content.remove_child(child)
		child.queue_free()
	current_mode = next_mode
	if locale_theme != null:
		theme = locale_theme
	content.alignment = BoxContainer.ALIGNMENT_BEGIN
	content.add_theme_constant_override("separation", 22 if current_mode == "answer" else 12)
	var bounds := flow_bounds if current_mode == "flow" else answer_bounds if current_mode == "answer" else detail_bounds if current_mode == "detail" else regular_bounds
	position = bounds.position
	size = bounds.size
	show()
	var panel_style := answer_panel_style if current_mode == "answer" else flow_panel_style if current_mode == "flow" else regular_panel_style
	add_theme_stylebox_override("panel", panel_style)
	var scroll := $ModalScroll as ScrollContainer
	scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED if current_mode == "flow" else ScrollContainer.SCROLL_MODE_AUTO
	scroll.scroll_vertical = 0
	add_label(content, title, 29, Color("dfc995"))
	content.add_child(HSeparator.new())

func add_detail(body: String, diagram: String, close_action: Callable) -> void:
	content.alignment = BoxContainer.ALIGNMENT_CENTER
	var card := EvidenceDetail.instantiate()
	card.get_node("Picture").kind = diagram
	card.get_node("Picture").visible = not diagram.is_empty()
	card.get_node("Description").text = body
	content.add_child(card)
	add_button(content, "FA_UI_057", close_action)
