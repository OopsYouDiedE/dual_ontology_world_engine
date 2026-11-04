@tool
extends Control
## Ink ornament on the existing page surface; never intercepts book input.

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	resized.connect(queue_redraw)

func _draw() -> void:
	var ink := Color(0.25, 0.28, 0.19, 0.55)
	var gold := Color("ad8745")
	for side in [-1.0, 1.0]:
		var x := 9.0 if side < 0.0 else size.x - 9.0
		var base := size.y - 24.0
		var stem := PackedVector2Array()
		for i in range(25):
			var t := float(i) / 24.0
			stem.append(Vector2(x + sin(t * TAU) * 3.0, base - t * 112.0))
		draw_polyline(stem, ink, 0.8, true)
		for i in range(5):
			var y := base - 12.0 - i * 20.0
			var direction: float = -side if i % 2 == 0 else side
			var leaf := PackedVector2Array([Vector2(x, y), Vector2(x + direction * 5.0, y - 4.0), Vector2(x + direction * 6.0, y - 10.0), Vector2(x + direction * 1.5, y - 6.0)])
			draw_colored_polygon(leaf, ink)
		draw_circle(Vector2(x, base - 118.0), 1.8, gold)
