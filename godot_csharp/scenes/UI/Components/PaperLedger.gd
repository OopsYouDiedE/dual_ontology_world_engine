@tool
extends Control
## 空白账页横格：每 32 px 一条淡规线，用于尚未填写的对照页。

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	resized.connect(queue_redraw)

func _draw() -> void:
	var step := float(get_theme_constant("step", "PaperLedger"))
	var color := get_theme_color("line", "PaperLedger")
	var y := step
	while y < size.y:
		draw_line(Vector2(0, round(y) + 0.5), Vector2(size.x, round(y) + 0.5), color, 1.0)
		y += step
