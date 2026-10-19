@tool
extends Control
## Double-rule geometry and colors come from the shared generated theme.
func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	resized.connect(queue_redraw)
	theme_changed.connect(queue_redraw)

func _draw() -> void:
	if size.x < 24 or size.y < 24: return
	draw_style_box(get_theme_stylebox("outer", "PaperFrame"), Rect2(Vector2(4, 4), size - Vector2(8, 8)))
	draw_style_box(get_theme_stylebox("inner", "PaperFrame"), Rect2(Vector2(9, 9), size - Vector2(18, 18)))
	var ink := get_theme_color("ornament", "PaperFrame")
	var extent := minf(22.0, size.y * 0.25)
	for corner in [Vector2(14, 14), Vector2(size.x-14, 14), Vector2(14, size.y-14), size-Vector2(14,14)]:
		var direction := Vector2(1 if corner.x < size.x * 0.5 else -1, 1 if corner.y < size.y * 0.5 else -1)
		var points := PackedVector2Array()
		for index in 17:
			var angle := float(index) / 16.0 * PI * 1.6
			var radius := extent * (1.0 - float(index) / 22.0)
			points.append(corner + Vector2(cos(angle), sin(angle)) * direction * radius * 0.42 + direction * extent * 0.35)
		draw_polyline(points, ink, 1.0, true)
		draw_line(corner, corner + Vector2(direction.x * extent, 0), ink, 1.0)
		draw_line(corner, corner + Vector2(0, direction.y * extent), ink, 1.0)
