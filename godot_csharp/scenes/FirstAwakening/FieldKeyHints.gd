extends HBoxContainer
## Top-bar key legend drawn as small keycaps. Entries are [key, caption];
## an empty key draws a plain status chip. Rebuilt only when entries change.

const CAP_TEXT := Color("1d2420")
const CAP_FACE := Color("e8d6a7")
const CAPTION := Color("e8d6a7")
const CHIP_TEXT := Color("cfc3a2")

var _entries: Array = []

func _init() -> void:
	alignment = BoxContainer.ALIGNMENT_END
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_theme_constant_override("separation", 14)
	sort_children.connect(queue_redraw)

func _draw() -> void:
	# A quiet backing keeps the legend legible over busy wallpaper and props.
	if get_child_count() == 0: return
	var left := (get_child(0) as Control).position.x - 12.0
	var plate := StyleBoxFlat.new()
	plate.bg_color = Color(0.05, 0.06, 0.06, 0.62)
	plate.set_corner_radius_all(8)
	draw_style_box(plate, Rect2(left, -3, size.x - left + 12.0, size.y + 6))

func show_entries(entries: Array) -> void:
	if entries == _entries: return
	_entries = entries.duplicate(true)
	for child in get_children():
		remove_child(child)
		child.queue_free()
	for entry in entries:
		var group := HBoxContainer.new()
		group.mouse_filter = Control.MOUSE_FILTER_IGNORE
		group.add_theme_constant_override("separation", 6)
		add_child(group)
		if str(entry[0]).is_empty():
			group.add_child(_chip(str(entry[1])))
			continue
		group.add_child(_cap(str(entry[0])))
		group.add_child(_text(str(entry[1]), CAPTION, 17))

func _cap(key: String) -> Label:
	var cap := _text(key, CAP_TEXT, 15)
	var face := StyleBoxFlat.new()
	face.bg_color = CAP_FACE
	face.border_color = Color("8f7a4c")
	face.border_width_bottom = 2
	face.set_corner_radius_all(3)
	face.content_margin_left = 7
	face.content_margin_right = 7
	face.content_margin_top = 1
	face.content_margin_bottom = 1
	cap.add_theme_stylebox_override("normal", face)
	cap.custom_minimum_size.x = 24
	cap.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	return cap

func _chip(words: String) -> Label:
	var chip := _text(words, CHIP_TEXT, 16)
	var face := StyleBoxFlat.new()
	face.bg_color = Color(0.06, 0.08, 0.07, 0.55)
	face.border_color = Color(0.91, 0.84, 0.65, 0.35)
	face.set_border_width_all(1)
	face.set_corner_radius_all(10)
	face.content_margin_left = 10
	face.content_margin_right = 10
	chip.add_theme_stylebox_override("normal", face)
	return chip

func _text(words: String, color: Color, font_size: int) -> Label:
	var label := Label.new()
	label.text = words
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_color_override("font_color", color)
	label.add_theme_font_size_override("font_size", font_size)
	return label
