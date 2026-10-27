extends TileMapLayer
## Source-cut overlay; geometry and continuous base-floor sampling are separate.
const TERRAIN = preload("res://ShareAsset/Internal/Props/AstraTerrain/GalleryBorder.tres")

func configure(cells: Array[Vector2i]) -> void:
	tile_set = TERRAIN
	scale = Vector2.ONE * (32.0 / 120.0)
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	clear()
	# false matches explicit empty neighbors, including holes and concave turns.
	set_cells_terrain_connect(cells, 0, 0, false)
