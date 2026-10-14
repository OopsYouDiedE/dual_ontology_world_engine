@tool
extends Control
## 计数（设计规范第六部分 §4.4，参照 Isotype）：total 个同尺寸小格，已有 filled 个填墨。

@export var total := 3:
	set(value):
		total = maxi(0, value)
		update_minimum_size()
		queue_redraw()
@export var filled := 1:
	set(value):
		filled = maxi(0, value)
		queue_redraw()

func _get_minimum_size() -> Vector2:
	var cell := get_theme_constant("cell", "PaperTally")
	var gap := get_theme_constant("gap", "PaperTally")
	return Vector2(maxi(0, total * (cell + gap) - gap), cell)

func _draw() -> void:
	var cell := float(get_theme_constant("cell", "PaperTally"))
	var gap := float(get_theme_constant("gap", "PaperTally"))
	var top := roundf((size.y - cell) / 2.0)
	for i in total:
		var r := Rect2(Vector2(i * (cell + gap), top), Vector2(cell, cell))
		if i < filled:
			draw_rect(r, get_theme_color("filled", "PaperTally"))
		else:
			draw_rect(r.grow(-0.5), get_theme_color("empty", "PaperTally"), false, 1.0)
