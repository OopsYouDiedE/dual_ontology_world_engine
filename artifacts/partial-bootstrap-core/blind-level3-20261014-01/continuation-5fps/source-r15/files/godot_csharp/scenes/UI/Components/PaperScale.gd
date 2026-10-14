@tool
extends Range
## 刻度尺（设计规范第六部分 §4.7）：一道墨线、长短相间的刻度；当前位置画一个笔迹勾，右侧笔迹读数。
## 用于音量等连续值。点击或拖动定值，左右键按一格（十分之一）调。读数由 value_text（Callable(value) -> String）
## 格式化，缺省为百分数。尺寸、颜色与字体取主题 PaperScale 条目。

## 读数格式化：func(value: float) -> String。
var value_text: Callable

var _dragging := false

func _init() -> void:
	min_value = 0.0
	max_value = 1.0
	step = 0.05
	focus_mode = Control.FOCUS_ALL
	mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND

func _ready() -> void:
	value_changed.connect(func(_v: float) -> void: queue_redraw())
	focus_entered.connect(queue_redraw)
	focus_exited.connect(queue_redraw)

func _k(key: String) -> int:
	return get_theme_constant(key, "PaperScale")

func _get_minimum_size() -> Vector2:
	return Vector2(_k("width") + _k("value_gap") + _k("value_width"), _k("height"))

func _notification(what: int) -> void:
	if what == NOTIFICATION_THEME_CHANGED:
		update_minimum_size()
		queue_redraw()

func _draw() -> void:
	var width := float(_k("width"))
	var mid := roundf(size.y / 2.0)
	var line := get_theme_color("line", "PaperScale")
	draw_rect(Rect2(0, mid, width, 1), line)
	var intervals := maxi(1, _k("intervals"))
	for i in intervals + 1:
		var x := minf(roundf(width * float(i) / float(intervals)), width - 1.0)
		var h := float(_k("major_tick") if i % 2 == 0 else _k("minor_tick"))
		draw_rect(Rect2(x, mid - roundf(h / 2.0), 1, h), line)
	var mark := get_theme_color("mark", "PaperScale")
	# 笔迹勾：落在当前位置，略高出墨线，像随手一划。
	var c := Vector2(roundf(get_as_ratio() * (width - 1.0)), mid - 2.0)
	draw_polyline(PackedVector2Array([c + Vector2(-6, -1), c + Vector2(-1, 5), c + Vector2(9, -11)]), mark, 2.5, true)
	if has_focus():
		draw_rect(Rect2(0, size.y - 2, width, 2), get_theme_color("focus", "PaperScale"))
	var font := get_theme_font("mark", "PaperScale")
	var font_size := get_theme_font_size("value", "PaperScale")
	if font == null:
		return
	var baseline := mid + roundf((font.get_ascent(font_size) - font.get_descent(font_size)) / 2.0)
	draw_string(font, Vector2(width + _k("value_gap"), baseline), _text(), HORIZONTAL_ALIGNMENT_LEFT, _k("value_width"), font_size, mark)

func _text() -> String:
	if value_text.is_valid():
		return str(value_text.call(value))
	return "%d%%" % roundi(get_as_ratio() * 100.0)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		_dragging = event.pressed
		if event.pressed:
			grab_focus()
			_set_from_x(event.position.x)
		accept_event()
	elif event is InputEventMouseMotion and _dragging:
		_set_from_x(event.position.x)
		accept_event()
	elif event.is_action_pressed("ui_left", true):
		value -= (max_value - min_value) / 10.0
		accept_event()
	elif event.is_action_pressed("ui_right", true):
		value += (max_value - min_value) / 10.0
		accept_event()

func _set_from_x(x: float) -> void:
	var ratio := clampf(x / float(_k("width")), 0.0, 1.0)
	value = min_value + ratio * (max_value - min_value)
