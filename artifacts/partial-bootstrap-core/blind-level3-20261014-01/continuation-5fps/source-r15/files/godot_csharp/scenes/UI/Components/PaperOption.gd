@tool
extends Button
## 圈选项（设计规范第六部分 §4.7）：印好的选项文字；选中时外圈一道笔迹椭圆，像用笔圈出；
## 不可用时淡墨删除线。键帽（PaperKeyCap 变体）也用本组件：改键中的那一个被圈出。
## 颜色与线宽取主题 PaperOption 条目。

func _init() -> void:
	toggle_mode = true
	if theme_type_variation == &"":
		theme_type_variation = &"PaperOption"

func _ready() -> void:
	toggled.connect(func(_on: bool) -> void: queue_redraw())

func _notification(what: int) -> void:
	if what == NOTIFICATION_DRAW:
		_draw_marks()

func _draw_marks() -> void:
	if text == "":
		return
	var center := size / 2.0
	if button_pressed:
		# 一笔圈：起笔处收笔略过头、半径渐大、整体略斜，像手画的圈。
		# 圈住文字本身：横向略宽于文字，纵向不出本行。
		var style := get_theme_stylebox("normal")
		var text_w := size.x - style.get_margin(SIDE_LEFT) - style.get_margin(SIDE_RIGHT)
		var radius := Vector2(maxf(text_w / 2.0 + 9.0, size.y / 2.0 + 2.0), size.y / 2.0 - 2.0)
		var tilt := deg_to_rad(float(posmod(text.hash(), 5) - 4))
		var steps := 36
		var points := PackedVector2Array()
		for i in steps + 4:
			var a := deg_to_rad(200.0) + TAU * float(i) / float(steps)
			var grow := 1.0 + 0.05 * float(i) / float(steps)
			points.append(center + Vector2(cos(a) * radius.x * grow, sin(a) * radius.y * grow).rotated(tilt))
		draw_polyline(points, get_theme_color("mark", "PaperOption"), float(get_theme_constant("mark_width", "PaperOption")), true)
	if disabled:
		var style := get_theme_stylebox("normal")
		var y := roundf(center.y) + 0.5
		draw_line(Vector2(style.get_margin(SIDE_LEFT), y), Vector2(size.x - style.get_margin(SIDE_RIGHT), y),
			get_theme_color("strike", "PaperOption"), 1.0)
