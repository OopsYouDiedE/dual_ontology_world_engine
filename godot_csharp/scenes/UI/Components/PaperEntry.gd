@tool
extends Button
## 条目行（设计规范第六部分 §4.4）：左对齐、无框，左侧登记体序号；悬停加下划线并铺纸阴影底。
## 序号画在样式盒左边距里，text 仍是纯条目文字；不可用时淡墨加删除线。

@export var serial := "":
	set(value):
		serial = value
		queue_redraw()

func _init() -> void:
	alignment = HORIZONTAL_ALIGNMENT_LEFT
	if theme_type_variation == &"":
		theme_type_variation = &"PaperEntry"

func _notification(what: int) -> void:
	if what == NOTIFICATION_DRAW:
		_draw_marks()

func _draw_marks() -> void:
	var center_y := size.y / 2.0
	if serial != "":
		var font := get_theme_font("register", "Paper")
		var font_size := get_theme_font_size("font_size") - 4
		var ascent := font.get_ascent(font_size)
		var descent := font.get_descent(font_size)
		draw_string(font, Vector2(8, round(center_y + (ascent - descent) / 2.0)), serial, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size,
			get_theme_color("serial", "PaperEntry"))
	if disabled and text != "":
		var style := get_theme_stylebox("normal")
		var gutter := style.get_margin(SIDE_LEFT)
		var font := get_theme_font("font")
		var width := minf(font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, get_theme_font_size("font_size")).x,
			size.x - gutter - style.get_margin(SIDE_RIGHT))
		draw_line(Vector2(gutter, round(center_y) + 0.5), Vector2(gutter + width, round(center_y) + 0.5), get_theme_color("strike", "PaperEntry"), 1.0)
