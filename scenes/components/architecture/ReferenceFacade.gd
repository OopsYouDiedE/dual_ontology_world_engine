extends Node2D
## A material-only assembly on the existing lecture-hall south elevation.
## The parent sprite owns the foot point, Y sorting and occluder modulation.
const ROOT := "res://assets/environment/ReferenceFacade/"
const TILE_SET = preload("res://assets/environment/ReferenceFacade/ReferenceFacade.tres")
const WIDTH := 15

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var wall := TileMapLayer.new()
	wall.name = "WallAndStoneBase"
	wall.tile_set = TILE_SET
	wall.collision_enabled = false
	wall.navigation_enabled = false
	add_child(wall)
	for y in range(4):
		for x in range(WIDTH):
			wall.set_cell(Vector2i(x, y), 1 if y == 3 else 0, Vector2i.ZERO)
	for column in [1, 3, 6, 8, 11, 13]:
		_piece("Openings/narrow_window", Vector2(column * 32, 32))
	# The existing lecture facade has six windows and no south-side door.
	# Door modules are published but do not create an entrance here.
	for x in range(WIDTH):
		_piece("Cornice/floor_belt", Vector2(x * 32, 80))
	_piece("Corners/corner_pilaster", Vector2(0, 0))
	_piece("Corners/corner_pilaster", Vector2(448, 0))
	for x in range(0, 7):
		_piece("Cornice/eave_straight", Vector2(x * 64, -16))
	# Caps are independent cross-grid modules; no whole-building image is used.
	_piece("Cornice/eave_left", Vector2(0, -48))
	_piece("Cornice/eave_right", Vector2(416, -48))

func _piece(key: String, at: Vector2) -> void:
	var sprite := Sprite2D.new()
	sprite.name = key.replace("/", "_")
	sprite.texture = load(ROOT + key + ".png")
	sprite.centered = false
	sprite.position = at
	add_child(sprite)
