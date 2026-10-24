extends Node2D
## Native 32px wood and masonry assembled around the physical stair gap.
const FLOOR = preload("res://ShareAsset/Internal/Props/Highlights/Terrain/floor.png")
const WALL = preload("res://ShareAsset/Internal/Props/Highlights/Terrain/wall.png")
const RAIL = preload("res://scenes/Highlights/StageRailing.gd")
var transfer := false
var gallery := false

func install_foreground(parent: Node2D) -> void:
	if gallery:
		_rail(parent, Vector2(32, 292), 448)
	elif transfer:
		_rail(parent, Vector2(288, 200), 120)
		_rail(parent, Vector2(468, 200), 12)

func _rail(parent: Node2D, at: Vector2, span: int) -> void:
	var rail := RAIL.new()
	rail.position = at
	rail.span = span
	parent.add_child(rail)

func _patch(rect: Rect2i, material: int, masonry: bool = false) -> void:
	var texture: Texture2D = WALL if masonry else FLOOR
	var source := Vector2i(96, material * 128 + 96) if masonry else Vector2i(192, material * 192 + 160)
	for y in range(rect.position.y, rect.end.y, 32):
		for x in range(rect.position.x, rect.end.x, 32):
			var size := Vector2i(mini(32, rect.end.x - x), mini(32, rect.end.y - y))
			draw_texture_rect_region(texture, Rect2(Vector2(x, y), Vector2(size)), Rect2(Vector2(source), Vector2(size)))

func _draw() -> void:
	if gallery:
		_patch(Rect2i(32, 284, 448, 12), 2)
		return
	_patch(Rect2i(0, 88, 32, 8), 2)
	_patch(Rect2i(96, 88, 392, 8), 2)
	_patch(Rect2i(24, 96, 8, 280), 2)
	_patch(Rect2i(480, 96, 8, 280), 2)
	if transfer:
		# Wood deck and masonry retaining wall open only at the service stair.
		_patch(Rect2i(288, 96, 192, 88), 2)
		_patch(Rect2i(288, 184, 128, 16), 0, true)
		_patch(Rect2i(464, 184, 16, 16), 0, true)
		_patch(Rect2i(288, 180, 128, 5), 3)
		for y in range(104, 184, 16):
			draw_line(Vector2(296, y), Vector2(472, y), Color(0.19, 0.15, 0.12, 0.5), 1)
		for step in range(5):
			var y := 184 + step * 8
			_patch(Rect2i(416, y, 48, 6), 3)
			_patch(Rect2i(416, y + 6, 48, 2), 0, true)
			draw_line(Vector2(416, y + 6), Vector2(464, y + 6), Color(0.15, 0.17, 0.16, 0.6), 1)
		_patch(Rect2i(410, 192, 5, 32), 2)
		_patch(Rect2i(465, 192, 5, 32), 2)
		# Goods ropes hang over the receiving deck, clear of the staircase.
		for x in [300, 364]:
			draw_line(Vector2(x, 100), Vector2(x, 158), Color("ab9569"), 2)
			draw_circle(Vector2(x, 102), 3, Color("7e7053"))
