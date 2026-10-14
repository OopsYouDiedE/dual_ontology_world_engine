@tool
extends Button
## 选项行（设计规范第六部分 §4.6）：左侧序号，右侧文字，下方淡规线；选中时文字下衬淡笔迹色底、
## 行尾画手写勾；不可用时淡墨删除线。按钮的 text 仍是纯选项文字，序号与勾画在样式盒左右边距里。
## 颜色、字体与边距取主题的 PaperCheck 与 Paper 条目。

@export var serial := "":
	set(value):
		serial = value
		queue_redraw()

func _init() -> void:
	toggle_mode = true
	alignment = HORIZONTAL_ALIGNMENT_LEFT
	if theme_type_variation == &"":
		theme_type_variation = &"PaperCheck"

func _ready() -> void:
	toggled.connect(func(_on: bool) -> void: queue_redraw())

func _notification(what: int) -> void:
	if what == NOTIFICATION_DRAW:
		_draw_marks()

func _text_width() -> float:
	var style := get_theme_stylebox("normal")
	var room := size.x - style.get_margin(SIDE_LEFT) - style.get_margin(SIDE_RIGHT)
	var width := get_theme_font("font").get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, get_theme_font_size("font_size")).x
	return minf(width, room)

func _draw_marks() -> void:
	var style := get_theme_stylebox("normal")
	var left := style.get_margin(SIDE_LEFT)
	var center_y := size.y / 2.0
	if serial != "":
		var font := get_theme_font("register", "Paper")
		var font_size := get_theme_font_size("font_size") - 4
		var ascent := font.get_ascent(font_size)
		var descent := font.get_descent(font_size)
		draw_string(font, Vector2(8, roundf(center_y + (ascent - descent) / 2.0)), serial, HORIZONTAL_ALIGNMENT_LEFT, left - 16, font_size,
			get_theme_color("serial", "PaperCheck"))
	var width := _text_width()
	if button_pressed and text != "":
		var line_h := float(get_theme_font_size("font_size"))
		draw_rect(Rect2(left - 2, roundf(center_y + line_h * 0.1), width + 4, roundf(line_h * 0.42)), get_theme_color("tint", "PaperCheck"))
		# 手写勾：行尾右边距里，略斜，像笔划过。
		var c := Vector2(size.x - style.get_margin(SIDE_RIGHT) + 16, center_y)
		draw_polyline(PackedVector2Array([c + Vector2(-7, 0), c + Vector2(-2, 6), c + Vector2(10, -10)]), get_theme_color("mark", "PaperCheck"), 2.5, true)
	if disabled and text != "":
		draw_line(Vector2(left, roundf(center_y) + 0.5), Vector2(left + width, roundf(center_y) + 0.5), get_theme_color("strike", "PaperCheck"), 1.0)
