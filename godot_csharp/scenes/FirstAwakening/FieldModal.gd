extends PanelContainer
## Owns the investigation panel's visual layout and temporary controls.
## Field cards share the mind notebook's paper, ink and quiet buttons.

const EvidenceDiagram = preload("res://scenes/FirstAwakening/EvidenceDiagram.gd")
const EvidenceDetail = preload("res://scenes/FirstAwakening/EvidenceDetail.tscn")
const PAPER_THEME: Theme = preload("res://ShareAsset/Internal/Props/FirstAwakeningInterior/mind_theme.tres")
const INK := Color("303930")
const MUTED := Color("6f705f")
const RULE := Color(0.19, 0.22, 0.19, 0.18)
@onready var content: VBoxContainer = $ModalScroll/ModalBox
@export var regular_bounds: Rect2
@export var detail_bounds: Rect2
@export var answer_bounds: Rect2
@export var flow_bounds: Rect2
@export var regular_panel_style: StyleBox
@export var flow_panel_style: StyleBox
@export var answer_panel_style: StyleBox

var current_mode := ""

func style(color: Color, border := Color("63706b")) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = color
	box.border_color = border
	box.set_border_width_all(2)
	box.set_corner_radius_all(4)
	box.set_content_margin_all(10)
	return box

func add_label(parent: Node, words: String, size := 20, color := INK) -> RichTextLabel:
	var label := RichTextLabel.new()
	label.text = tr(words)
	label.bbcode_enabled = true
	label.fit_content = true
	label.scroll_active = false
	label.add_theme_font_size_override("normal_font_size", size)
	label.add_theme_color_override("default_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(label)
	return label

func add_button(parent: Node, words: String, action: Callable, disabled := false) -> Button:
	# Buttons keep their text width; a row of full-width bars reads as a form.
	var button := Button.new()
	button.text = tr(words)
	button.disabled = disabled
	button.custom_minimum_size = Vector2(0, 38)
	button.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.add_theme_font_size_override("font_size", 19)
	for key in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color"]:
		button.add_theme_color_override(key, INK)
	button.pressed.connect(action)
	parent.add_child(button)
	return button

## Places actions at the card's foot; the last one is the primary action on the right.
func add_footer(actions: Array) -> Button:
	var rule := ColorRect.new()
	rule.color = RULE
	rule.custom_minimum_size = Vector2(0, 1)
	content.add_child(rule)
	var footer := HBoxContainer.new()
	footer.add_theme_constant_override("separation", 16)
	content.add_child(footer)
	var last: Button
	for index in actions.size():
		if index == actions.size() - 1 and actions.size() > 1:
			var space := Control.new()
			space.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			footer.add_child(space)
		last = add_button(footer, actions[index][0], actions[index][1])
	if actions.size() == 1:
		footer.alignment = BoxContainer.ALIGNMENT_END
	return last

func paper_field(control: Control) -> void:
	for state in ["normal", "hover", "pressed", "focus"]:
		control.add_theme_stylebox_override(state, PAPER_THEME.get_stylebox("focus" if state == "focus" else "normal", "TextEdit"))
	control.add_theme_color_override("font_color", INK)
	control.add_theme_color_override("font_hover_color", INK)
	control.add_theme_color_override("font_pressed_color", INK)
	if control is LineEdit:
		# Single-line fields need their own colors; TextEdit theme colors do not inherit.
		control.add_theme_color_override("font_placeholder_color", MUTED)
		control.add_theme_color_override("font_uneditable_color", MUTED)
		control.add_theme_color_override("caret_color", INK)
		control.add_theme_color_override("font_selected_color", INK)
		control.add_theme_color_override("selection_color", PAPER_THEME.get_color("selection_color", "TextEdit"))

func open_content(title: String, next_mode: String, locale_theme: Theme) -> void:
	if has_node("EvidenceNotebook"):
		var previous := get_node("EvidenceNotebook")
		remove_child(previous)
		previous.queue_free()
	$ModalScroll.visible = next_mode != "evidence"
	for child in content.get_children():
		content.remove_child(child)
		child.queue_free()
	current_mode = next_mode
	theme = _paper_theme(locale_theme)
	content.alignment = BoxContainer.ALIGNMENT_BEGIN
	content.add_theme_constant_override("separation", 18 if current_mode == "answer" else 12)
	var bounds := flow_bounds if current_mode == "flow" else answer_bounds if current_mode == "answer" else detail_bounds if current_mode == "detail" else regular_bounds
	var panel_style := answer_panel_style if current_mode == "answer" else flow_panel_style if current_mode == "flow" else regular_panel_style
	add_theme_stylebox_override("panel", panel_style)
	var scroll := $ModalScroll as ScrollContainer
	scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED if current_mode == "flow" else ScrollContainer.SCROLL_MODE_AUTO
	scroll.scroll_vertical = 0
	position = bounds.position
	size = bounds.size
	show()
	if current_mode == "evidence":
		# The book owns its body scroll and fixed actions; no outer scrolling or fit pass.
		position = Vector2(94, 96)
		size = Vector2(1092, 800)
		add_theme_stylebox_override("panel", StyleBoxEmpty.new())
		return
	if current_mode == "flow":
		return
	add_label(content, title, 24, INK)
	var rule := ColorRect.new()
	rule.color = RULE
	rule.custom_minimum_size = Vector2(0, 1)
	content.add_child(rule)
	if current_mode in ["message", "ending", "meeting", "meeting_evidence", "answer"]:
		_fit_height.call_deferred(bounds)

func add_detail(body: String, diagram: String, close_action: Callable) -> void:
	var card := EvidenceDetail.instantiate()
	card.get_node("Picture").kind = diagram
	card.get_node("Picture").visible = not diagram.is_empty()
	card.get_node("Description").text = body
	card.get_node("Description").add_theme_color_override("default_color", INK)
	content.add_child(card)
	add_footer([["FA_UI_057", close_action]])

func compact_name_card() -> void:
	# Naming is a short interruption within the ticket inspection.
	var card_size := Vector2(620, 284)
	$ModalScroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	size = card_size
	position = regular_bounds.get_center() - card_size / 2.0

func _paper_theme(locale_theme: Theme) -> Theme:
	# The notebook theme draws the paper; the locale theme only swaps the print face.
	var paper := PAPER_THEME.duplicate() as Theme
	if locale_theme != null and locale_theme.default_font != null:
		paper.default_font = locale_theme.default_font
	return paper

func _fit_height(bounds: Rect2) -> void:
	# Short cards shrink to their text and stay centred on the original bounds.
	# Wrapped labels report their height only after one layout pass at the card width.
	var mode := current_mode
	await get_tree().process_frame
	if not visible or mode != current_mode:
		return
	var margin := get_theme_stylebox("panel").get_minimum_size().y
	var wanted := minf(bounds.size.y, content.get_combined_minimum_size().y + margin)
	size.y = wanted
	position.y = bounds.position.y + roundf((bounds.size.y - wanted) / 2.0)
