extends Node2D
## Flame flicker and floor traces are drawn from fixed nodes in the scene.

@export var kind := ""
var flames: Array[PointLight2D] = []
var clock := 0.0

func _ready() -> void:
	if kind == "traces":
		return
	for child in get_parent().get_node("Lighting").get_children():
		if child is PointLight2D and child.has_meta("base_energy"):
			flames.append(child)

func _process(delta: float) -> void:
	if flames.is_empty():
		return
	clock += delta
	for index in flames.size():
		var lamp := flames[index]
		var base: float = lamp.get_meta("base_energy")
		lamp.energy = base * (1.0 + 0.035 * sin(clock * 5.3 + index * 1.7) + 0.02 * sin(clock * 13.1 + index * 0.6))

func _draw() -> void:
	if kind != "traces":
		return
	var soot := Color("2b2630")
	for i in 40:
		draw_rect(Rect2(604 + (i * 13) % 78, 954 + (i * 7) % 5, 2, 1), soot)
	draw_rect(Rect2(606, 958, 76, 2), Color(0.17, 0.15, 0.19, 0.6))
	draw_colored_polygon(PackedVector2Array([Vector2(628, 960), Vector2(642, 960), Vector2(642, 953)]), Color("a49a88"))
	draw_arc(Vector2(628, 946), 9, 0, TAU, 20, Color(0.2, 0.18, 0.22, 0.75), 1.0)
	for i in 6:
		draw_rect(Rect2(588 + i * 9, 928 + i % 2, 12, 1), Color("7f8d86"))
		draw_rect(Rect2(590 + i * 9, 930 + i % 2, 8, 1), Color(0.5, 0.56, 0.54, 0.6))
