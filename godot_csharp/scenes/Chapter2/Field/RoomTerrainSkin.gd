extends Node2D
## Floor presentation follows the published scene rows and owns no geometry.
@export var floor_terrain := 0
@export var preserved_discs := PackedVector3Array()

func _ready() -> void:
	var room := get_parent()
	var rows: Array = room.get_meta("terrain_rows", [])
	if rows.is_empty():
		push_error("RoomTerrainSkin requires terrain_rows: " + str(room.name))
		return
	var ground := TileMapLayer.new()
	ground.name = "RedrawnFloor"
	ground.tile_set = preload("res://ShareAsset/Internal/Props/Highlights/Terrain/HighlightsTerrain.tres")
	ground.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	ground.collision_enabled = false
	ground.navigation_enabled = false
	add_child(ground)
	var cells: Array[Vector2i] = []
	for y in range(rows.size()):
		var row := str(rows[y])
		for x in range(row.length()):
			if row[x] in ".DS":
				cells.append(Vector2i(x, y))
	ground.set_cells_terrain_connect(cells, 0, floor_terrain)
	var original := room.get_node_or_null("Architecture") as Sprite2D
	if original == null:
		return
	# Preserve authored mosaics using their original image pixels and coordinates.
	for disc in preserved_discs:
		var polygon := Polygon2D.new()
		polygon.name = "RetainedFloorMosaic"
		var points := PackedVector2Array()
		for index in range(96):
			var angle := TAU * index / 96.0
			points.append(Vector2(disc.x, disc.y) + Vector2(cos(angle), sin(angle)) * disc.z)
		polygon.polygon = points
		polygon.uv = points
		polygon.texture = original.texture
		polygon.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		add_child(polygon)
