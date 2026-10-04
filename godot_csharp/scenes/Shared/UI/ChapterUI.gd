extends Node

const Paper := preload("res://scenes/Shared/UI/Paper.gd")
const Sound := preload("res://scenes/Shared/Audio/Sound.gd")

var ui: CanvasLayer
var hud_title: Label
var hud_hint: Label
var hud_keys: Label
var hover_tip: Label
var aside: Label
var modal_layer: CanvasLayer
var modal_root: Control
var busy_label: Label

func build_hud(field: CanvasLayer) -> void:
	ui = field
	hud_title = field.get_node("Title")
	hud_hint = field.get_node("Hint")
	field.get_node("Time").hide()
	for label in [hud_title, hud_hint]:
		label.add_theme_font_override("font", Paper.font())
	hud_keys = Label.new()
	hud_keys.position = Vector2(700, 20)
	hud_keys.size = Vector2(556, 30)
	hud_keys.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	hud_keys.add_theme_font_override("font", Paper.font())
	hud_keys.add_theme_font_size_override("font_size", 17)
	hud_keys.add_theme_color_override("font_color", Color("e8d6a7"))
	hud_keys.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var plate := StyleBoxFlat.new()
	plate.bg_color = Color(0.05, 0.06, 0.06, 0.72)
	plate.set_corner_radius_all(6)
	plate.content_margin_left = 12
	plate.content_margin_right = 12
	hud_keys.add_theme_stylebox_override("normal", plate)
	ui.add_child(hud_keys)
	hover_tip = Label.new()
	hover_tip.add_theme_font_override("font", Paper.font())
	hover_tip.add_theme_font_size_override("font_size", 17)
	hover_tip.add_theme_color_override("font_color", Color("f1e4bd"))
	var tip := StyleBoxFlat.new()
	tip.bg_color = Color(0.07, 0.07, 0.08, 0.86)
	tip.border_color = Color(0.83, 0.74, 0.52, 0.7)
	tip.set_border_width_all(1)
	tip.set_corner_radius_all(4)
	tip.set_content_margin_all(5)
	hover_tip.add_theme_stylebox_override("normal", tip)
	hover_tip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hover_tip.hide()
	ui.add_child(hover_tip)
	aside = Label.new()
	aside.add_theme_font_override("font", Paper.font())
	aside.add_theme_font_size_override("font_size", 19)
	aside.add_theme_color_override("font_color", Color("f1e4bd"))
	aside.add_theme_color_override("font_outline_color", Color(0.04, 0.03, 0.05))
	aside.add_theme_constant_override("outline_size", 6)
	aside.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	aside.position = Vector2(240, 880)
	aside.size = Vector2(800, 30)
	aside.mouse_filter = Control.MOUSE_FILTER_IGNORE
	aside.hide()
	ui.add_child(aside)
	modal_layer = CanvasLayer.new()
	modal_layer.layer = 15
	add_child(modal_layer)
	busy_label = Label.new()
	busy_label.text = "……"
	busy_label.position = Vector2(700, 56)
	busy_label.size = Vector2(556, 30)
	busy_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	busy_label.add_theme_font_override("font", Paper.font())
	busy_label.add_theme_font_size_override("font_size", 17)
	busy_label.add_theme_color_override("font_color", Color("e8d6a7"))
	busy_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	busy_label.hide()
	ui.add_child(busy_label)

func configure_dialogue(dialogue: CanvasLayer, portraits: Dictionary) -> void:
	dialogue.layer = 20
	dialogue.use_compact_layout()
	dialogue.single_press_keyboard_continue = true
	dialogue.dim_visited_responses = true
	dialogue.apply_locale_font(Paper.font(), null)
	dialogue.set_character_portraits(portraits)
	dialogue.panel.hide()

func open_modal(title: String, size: Vector2, kind: String) -> VBoxContainer:
	close_modal()
	if kind != "ending":
		Sound.sfx("page", -6.0)
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.theme = Paper.theme()
	modal_layer.add_child(root)
	modal_root = root
	var dim := ColorRect.new()
	dim.color = Color(0.03, 0.03, 0.05, 0.55)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(dim)
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", Paper.sheet())
	panel.position = (Vector2(1280, 992) - size) / 2.0
	panel.size = size
	panel.custom_minimum_size = size
	root.add_child(panel)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 12)
	panel.add_child(column)
	if not title.is_empty():
		column.add_child(Paper.label("[b]" + title + "[/b]", 25))
		column.add_child(Paper.rule())
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	var body := VBoxContainer.new()
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.add_theme_constant_override("separation", 10)
	scroll.add_child(body)
	root.set_meta("column", column)
	return body

func add_footer(actions: Array) -> HBoxContainer:
	var column: VBoxContainer = modal_root.get_meta("column")
	column.add_child(Paper.rule())
	var footer := HBoxContainer.new()
	footer.add_theme_constant_override("separation", 12)
	footer.alignment = BoxContainer.ALIGNMENT_END
	column.add_child(footer)
	for action in actions:
		var b := Paper.button(action[0], action[1])
		if action.size() > 2 and action[2]:
			b.disabled = true
		footer.add_child(b)
	return footer

func close_modal() -> void:
	if is_instance_valid(modal_root):
		modal_root.queue_free()
	modal_root = null

func card_body(body: VBoxContainer, texture: Texture2D) -> VBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 22)
	body.add_child(row)
	if texture:
		var picture := TextureRect.new()
		picture.texture = texture
		picture.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		picture.custom_minimum_size = Vector2(288, 216)
		picture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		row.add_child(picture)
	var text := VBoxContainer.new()
	text.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	text.add_theme_constant_override("separation", 10)
	row.add_child(text)
	return text

func notebook_section(body: VBoxContainer, title: String, entries: Array) -> void:
	if not title.is_empty():
		body.add_child(Paper.label("[b]" + title + "[/b]", 21, Paper.SEAL))
	var grid := GridContainer.new()
	grid.columns = 3
	grid.add_theme_constant_override("h_separation", 10)
	grid.add_theme_constant_override("v_separation", 8)
	body.add_child(grid)
	for entry in entries:
		grid.add_child(Paper.button(entry[0], entry[1], 18))

func menu(title: String, items: Array, note: String, closed: Callable) -> VBoxContainer:
	var body := open_modal(title, Vector2(960, 720), "menu")
	if not note.is_empty():
		body.add_child(Paper.label(note, 19, Paper.MUTED))
	for item in items:
		var button := Paper.button(item[0], item[1])
		if item.size() > 2:
			button.disabled = bool(item[2])
		body.add_child(button)
	add_footer([["返回现场", closed]])
	return body

func key_hint(interact: bool, notebook: bool, replay: bool, actions: bool) -> String:
	var keys := []
	if interact:
		keys.append("E / 点击  检视")
	if notebook:
		keys.append("Tab  笔记本")
	if replay:
		keys.append("R  重演")
	if actions:
		keys.append("Q  行动")
	keys.append("H  回看")
	return "　　".join(keys)

static func closes_modal(key: int, kind: String) -> bool:
	return kind == "card" or key == KEY_ESCAPE or (key == KEY_TAB and kind == "notebook") or (key == KEY_R and kind == "replay") or (key in [KEY_ENTER, KEY_SPACE] and kind == "story_image")
