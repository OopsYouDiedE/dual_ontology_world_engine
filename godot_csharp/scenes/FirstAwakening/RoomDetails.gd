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
	# Observed table packing and chair movement share the existing map coordinates.
	draw_colored_polygon(PackedVector2Array([Vector2(295, 687), Vector2(307, 687), Vector2(305, 682), Vector2(298, 683)]), Color("93948a"))
	for point in [Vector2(266, 660), Vector2(378, 660), Vector2(266, 712), Vector2(378, 712)]:
		draw_rect(Rect2(point, Vector2(2, 10)), Color("706b63"))
	var soot := Color("2b2630")
	# Thin fan-shaped deposits make the inward direction visible on the field map.
	for i in 68:
		var depth := 4 + (i * 7) % 42
		var spread := depth / 2
		draw_rect(Rect2(644 + (i * 13) % (spread * 2 + 1) - spread, 954 - depth, 2, 1), soot)
	# Arc endpoint is a measured trace, not a permanent mechanical opening limit.
	draw_arc(Vector2(604, 952), 40, -0.28, 0.0, 14, Color("8e877e"), 1.0)
	for i in 40:
		draw_rect(Rect2(604 + (i * 13) % 78, 954 + (i * 7) % 5, 2, 1), soot)
	draw_rect(Rect2(606, 958, 76, 2), Color(0.17, 0.15, 0.19, 0.6))
	draw_colored_polygon(PackedVector2Array([Vector2(628, 960), Vector2(642, 960), Vector2(642, 953)]), Color("a49a88"))
	draw_arc(Vector2(628, 946), 9, 0, TAU, 20, Color(0.2, 0.18, 0.22, 0.75), 1.0)
	for i in 6:
		draw_rect(Rect2(588 + i * 9, 928 + i % 2, 12, 1), Color("7f8d86"))
		draw_rect(Rect2(590 + i * 9, 930 + i % 2, 8, 1), Color(0.5, 0.56, 0.54, 0.6))
	# Physical tracks are present from the outset. Low light reveals their layers.
	var examined: bool = get_parent().state.evidence.has("heel_tracks")
	for room_index in 4:
		var room_x: float = [192.0, 512.0, 832.0, 1152.0][room_index]
		var path := PackedVector2Array([Vector2(638 + room_index * 4, 944), Vector2(638 + room_index * 4, 420), Vector2(room_x, 420), Vector2(room_x, 382)])
		var count := 0
		for leg in range(1, path.size()):
			var steps := maxi(1, int(path[leg - 1].distance_to(path[leg]) / 28))
			for foot in steps:
				var point := path[leg - 1].lerp(path[leg], float(foot) / steps).round()
				var opacity := maxf(0.04, (0.46 if examined else 0.12) - count * 0.01)
				draw_rect(Rect2(point + Vector2(-1, -2), Vector2(2, 4)), Color(0.15, 0.13, 0.16, opacity))
				count += 1
