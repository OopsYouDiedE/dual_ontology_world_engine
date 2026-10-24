extends Node2D
## The rail sorts at its floor contact, in front of occupants on the high deck.
const WOOD = preload("res://ShareAsset/Internal/Props/Highlights/Terrain/floor.png")
var span := 32

func _beam(rect: Rect2i) -> void:
	for x in range(rect.position.x, rect.end.x, 32):
		var width := mini(32, rect.end.x - x)
		draw_texture_rect_region(WOOD, Rect2(x, rect.position.y, width, rect.size.y), Rect2(192, 544, width, rect.size.y))

func _draw() -> void:
	for x in range(0, span + 1, 32):
		_beam(Rect2i(x, -28, 5, 28))
		_beam(Rect2i(x - 2, -30, 9, 4))
	_beam(Rect2i(0, -24, span, 5))
	_beam(Rect2i(0, -10, span, 3))
	draw_line(Vector2(0, -24), Vector2(span, -24), Color("b29b74"), 1)
