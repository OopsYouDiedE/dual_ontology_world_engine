extends Node2D
## Local visual cues; no island simulation, authoritative water height or result.
var kind := ""
var elapsed := 0.0
var active := false
var completed := false
var stones: Array[Vector3i] = []
var dumplings := 0
var tide_steps := 0

func _process(delta: float) -> void:
	elapsed += delta
	queue_redraw()

func add_stone(black: bool) -> void:
	# Four alternating legal, uncaptured opening placements. No score adjudication.
	var opening: Array[Vector2i] = [Vector2i(15, 3), Vector2i(3, 15), Vector2i(3, 3), Vector2i(15, 15)]
	if stones.size() >= opening.size():
		return
	if black != (stones.size() % 2 == 0):
		return
	var p := opening[stones.size()]
	stones.append(Vector3i(p.x, p.y, 1 if black else 0))
	queue_redraw()

func _draw() -> void:
	if kind == "go":
		var board := Rect2(-29, -54, 58, 34)
		draw_rect(board, Color("be935e"))
		draw_rect(board, Color("72543c"), false, 2)
		for i in range(19):
			var px := -27.0 + i * 54.0 / 18.0
			var py := -52.0 + i * 30.0 / 18.0
			draw_line(Vector2(px, -52), Vector2(px, -22), Color("514136"), 0.5)
			draw_line(Vector2(-27, py), Vector2(27, py), Color("514136"), 0.5)
		for x in [3, 9, 15]:
			for y in [3, 9, 15]:
				draw_circle(Vector2(-27 + x * 54.0 / 18.0, -52 + y * 30.0 / 18.0), 0.65, Color("514136"))
		for stone in stones:
			var at := Vector2(-27 + stone.x * 54.0 / 18.0, -52 + stone.y * 30.0 / 18.0)
			draw_set_transform(at, 0, Vector2(1, 0.65))
			draw_circle(Vector2(0, 0.5), 1.65, Color("5d503e"))
			draw_circle(Vector2.ZERO, 1.45, Color("282c33") if stone.z == 1 else Color("f2eedf"))
			draw_set_transform(Vector2.ZERO)
	elif kind == "steam" and active:
		for i in range(14):
			var phase := fmod(elapsed * 0.42 + i * 0.13, 1.0)
			var point := Vector2(sin(phase * 7 + i) * 10, -48 - phase * 65)
			draw_circle(point, 2 + phase * 5, Color(0.94, 0.92, 0.83, (1.0 - phase) * 0.45))
	elif kind == "dumplings":
		for i in range(dumplings):
			var at := Vector2(-25 + (i % 5) * 10, -26 - (i / 5) * 7)
			draw_circle(at, 3, Color("eee3c6"))
			draw_arc(at + Vector2(0, 1), 3.2, PI, TAU, 8, Color("baaa8d"), 1)
	elif kind == "tide_edge":
		# The local danger edge shares the water layer's moving transform.
		for i in range(28):
			var x := 40.0 + i * 16.0
			var lift := sin(elapsed * 1.6 + i * 0.8) * 3.0
			draw_circle(Vector2(x, 9 + lift), 11, Color(0.42, 0.25, 0.63, 0.16))
			draw_line(Vector2(x - 5, 10 + lift), Vector2(x + 4, 10 + lift), Color(0.78, 0.71, 0.34, 0.65), 1)
	elif kind == "miracle":
		for i in range(55):
			var x := 64 + fmod(i * 53.0, 384)
			var y := 256 + sin(i * 1.7 + elapsed * 0.6) * 36
			if active:
				var phase := fmod(elapsed * 0.12 + i * 0.07, 1.0)
				y = 344 - phase * 220
				var glow := Color("c8b456")
				glow.a = sin(phase * PI) * 0.85
				draw_line(Vector2(x - 2, y), Vector2(x + 2, y), glow, 1)
				draw_line(Vector2(x, y - 2), Vector2(x, y + 2), glow, 1)
			else:
				draw_circle(Vector2(x, y), 8, Color(0.42, 0.25, 0.63, 0.13))
