extends Node2D
## Architectural trim and a visible receiving platform, in the same32px space.
var transfer := false
var gallery := false

func _draw() -> void:
	if gallery:
		# A narrow suspended gallery: solid north sill and south rail above the drop.
		draw_rect(Rect2(32, 156, 448, 6), Color("5c4435"))
		draw_rect(Rect2(32, 280, 448, 12), Color("443e37"))
		draw_line(Vector2(32, 280), Vector2(480, 280), Color("aa9271"), 3)
		for x in range(32, 481, 32):
			draw_rect(Rect2(x, 264, 4, 20), Color("5c5142"))
		# A pipe run joins the existing valve prop along the gallery's north wall.
		draw_line(Vector2(96, 138), Vector2(464, 138), Color("4e7268"), 5)
		draw_line(Vector2(96, 136), Vector2(464, 136), Color("b49361"), 1)
		for x in [112, 240, 464]:
			draw_rect(Rect2(x, 132, 4, 13), Color("a48960"))
		return
	# Front-facing rear wall, open doorway at32..96, and continuous side plinths.
	draw_rect(Rect2(0, 88, 32, 8), Color("5c4435"))
	draw_rect(Rect2(96, 88, 416, 8), Color("5c4435"))
	draw_line(Vector2(96, 88), Vector2(480, 88), Color("b49b70"), 2)
	draw_rect(Rect2(24, 96, 8, 280), Color("624638"))
	draw_rect(Rect2(480, 96, 8, 280), Color("624638"))
	draw_rect(Rect2(32, 92, 64, 4), Color("b5a080"))
	if transfer:
		# A continuous high receiving bay with a south-facing service stair.
		draw_rect(Rect2(352, 96, 128, 88), Color("a49b83"))
		for x in range(352, 481, 32):
			draw_line(Vector2(x, 96), Vector2(x, 184), Color("837c6c"), 1)
		for y in range(96, 185, 32):
			draw_line(Vector2(352, y), Vector2(480, y), Color("837c6c"), 1)
		draw_rect(Rect2(352, 184, 64, 16), Color("675d4e"))
		draw_line(Vector2(352, 184), Vector2(416, 184), Color("c2b395"), 2)
		for step in range(5):
			var y := 184 + step * 8
			draw_rect(Rect2(416, y, 48, 8), Color("93856c"))
			draw_line(Vector2(416, y), Vector2(464, y), Color("c3b496"), 2)
		draw_line(Vector2(408, 184), Vector2(408, 224), Color("5d564b"), 3)
		draw_line(Vector2(472, 184), Vector2(472, 224), Color("5d564b"), 3)
