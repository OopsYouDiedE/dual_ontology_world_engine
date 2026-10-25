extends Node2D
## Astra's source cuts only. No procedural painting; no collision or authority.
const ROOT := "res://ShareAsset/Internal/Props/ModernStage/"
var floor_tiles: TileMapLayer
var legacy_floor: TileMapLayer
var scheme := 1

func configure(kitchen: bool, old_floor: TileMapLayer) -> void:
	legacy_floor = old_floor
	var texture := load(ROOT + ("wood.png" if kitchen else "stone.png")) as Texture2D
	var size := Vector2i(texture.get_size())
	var tiles := TileSet.new()
	tiles.tile_size = size
	var source := TileSetAtlasSource.new()
	source.texture = texture
	source.texture_region_size = size
	source.create_tile(Vector2i.ZERO)
	tiles.add_source(source, 0)
	var mask := Polygon2D.new()
	mask.polygon = PackedVector2Array([Vector2(32, 96), Vector2(480, 96), Vector2(480, 376), Vector2(32, 376)])
	mask.clip_children = CanvasItem.CLIP_CHILDREN_ONLY
	add_child(mask)
	floor_tiles = TileMapLayer.new()
	floor_tiles.name = "Astra128pxMaterialModules"
	floor_tiles.tile_set = tiles
	floor_tiles.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	floor_tiles.position = Vector2(32, 96)
	floor_tiles.scale = Vector2.ONE * (128.0 / size.x)
	mask.add_child(floor_tiles)
	if kitchen:
		var facade := Sprite2D.new()
		facade.name = "AstraSourceFacade"
		facade.texture = load(ROOT + "facade.png")
		facade.scale = Vector2.ONE * (512.0 / facade.texture.get_width())
		facade.position = Vector2(256, 96 - facade.texture.get_height() * facade.scale.y * 0.5)
		facade.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
		add_child(facade)
	else:
		# Front wall uses smaller courses than the horizontal floor, with an
		# actual source-board edge cut as cap/skirting. No painted divider.
		var wall_texture := load(ROOT + "stone-wall.png") as Texture2D
		var wall_scale := 128.0 / wall_texture.get_width()
		for row in range(4):
			for column in range(4):
				var face := Sprite2D.new()
				face.texture = wall_texture
				face.centered = false
				face.scale = Vector2.ONE * wall_scale
				face.position = Vector2(column * 128, row * 24)
				face.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
				add_child(face)
		for y in [2, 94]:
			var trim := Sprite2D.new()
			trim.texture = load(ROOT + "stone-trim.png")
			trim.scale = Vector2.ONE * (512.0 / trim.texture.get_width())
			trim.position = Vector2(256, y)
			add_child(trim)
	set_scheme(scheme)

func set_scheme(value: int) -> void:
	scheme = value
	legacy_floor.visible = value == 0
	floor_tiles.visible = value != 0
	# C mirrors complete material modules, matching adjacent edges. B repeats
	# the unchanged source crop directly, exposing its non-seamless limitation.
	for y in range(4):
		for x in range(4):
			var flags := 0
			if value == 2:
				if x % 2 == 1: flags |= TileSetAtlasSource.TRANSFORM_FLIP_H
				if y % 2 == 1: flags |= TileSetAtlasSource.TRANSFORM_FLIP_V
			floor_tiles.set_cell(Vector2i(x, y), 0, Vector2i.ZERO, flags)
