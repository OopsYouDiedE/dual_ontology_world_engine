extends SceneTree

## Build an editable map from the adapter's saved TileSet, then reload it for verification.
const TILESET_PATH := "res://ShareAsset/Victorian/tilesets/Victorian.tres"
const MAP_PATH := "res://scenes/Parlor/Parlor.tscn"
const OUTPUT := "res://../artifacts/parlor-validation"
var tileset: TileSet
var room: Node2D
var sources: Dictionary = {}
var report: Array[String] = []

func _initialize() -> void:
	call_deferred("build")

func build() -> void:
	seed(3)
	tileset = load(TILESET_PATH) as TileSet
	assert(tileset != null)
	for i in tileset.get_source_count():
		var id := tileset.get_source_id(i)
		var source := tileset.get_source(id) as TileSetAtlasSource
		if source:
			var sname: String = source.get_meta("source_filename", "")
			if sname == "" and source.texture != null:
				sname = source.texture.resource_path.get_file().get_basename()
			if sname != "":
				sources[sname] = id
	room = Node2D.new()
	room.name = "Parlor"
	room.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	room.set_meta("reference", "artifacts/theme-film/scenes/shot-03.png")
	room.set_meta("description", "公共娱乐室：壁炉、中央会客区、左下钢琴、两侧茶桌；32px 图块")
	var floor_layer := layer("Floor")
	floor_layer.modulate = Color(0.59, 0.54, 0.47)
	var floor_cells := cells(Rect2i(1, 3, 18, 8))
	floor_cells.append_array(cells(Rect2i(9, 11, 2, 2)))
	paint(floor_layer, floor_cells, 0, "a2_floor_r3_c3")
	var walls := layer("Walls")
	walls.modulate = Color(0.65, 0.59, 0.51)
	paint(walls, cells(Rect2i(1, 0, 18, 3)), 1, "a4_wall_side_p0_c5")
	var trim := layer("WallCaps")
	trim.modulate = Color(0.48, 0.44, 0.39)
	var outline := cells(Rect2i(0, 0, 1, 12))
	outline.append_array(cells(Rect2i(19, 0, 1, 12)))
	outline.append_array(cells(Rect2i(0, 11, 9, 1)))
	outline.append_array(cells(Rect2i(11, 11, 9, 1)))
	paint(trim, outline, 0, "a4_wall_top_p0_c5")
	var rugs := layer("Rugs")
	rugs.modulate = Color(0.75, 0.43, 0.45)
	paint(rugs, cells(Rect2i(7, 4, 6, 5)), 0, "a2_floor_r2_c2")
	paint(rugs, cells(Rect2i(1, 8, 5, 3)), 0, "a2_floor_r2_c2")
	paint(rugs, cells(Rect2i(1, 4, 4, 3)), 0, "a2_floor_r2_c2")
	paint(rugs, cells(Rect2i(15, 4, 4, 3)), 0, "a2_floor_r2_c2")
	paint(rugs, cells(Rect2i(9, 10, 2, 3)), 0, "a2_floor_r2_c2")
	var furnishings := layer("Furniture")
	var objects := layer("TabletopObjects")
	objects.position.y = -8
	# Atlas rectangles stay on the original 32px grid. Multi-cell objects remain editable.
	stamp(furnishings, "B", Rect2i(8, 0, 3, 2), Vector2i(8, 1)) # fireplace
	stamp(furnishings, "C", Rect2i(8, 8, 1, 1), Vector2i(9, 0)) # picture
	stamp(furnishings, "C", Rect2i(10, 0, 1, 2), Vector2i(17, 1)) # clock
	for x in [2, 5, 13]:
		stamp(furnishings, "C", Rect2i(8, 10, 2, 2), Vector2i(x, 1)) # cabinets
	stamp(furnishings, "B", Rect2i(9, 4, 3, 1), Vector2i(8, 4)) # sofa
	stamp(furnishings, "B", Rect2i(11, 5, 1, 1), Vector2i(7, 6))
	stamp(furnishings, "B", Rect2i(13, 4, 1, 2), Vector2i(12, 5))
	stamp(furnishings, "B", Rect2i(4, 7, 2, 1), Vector2i(9, 6)) # tea table
	stamp(objects, "C", Rect2i(3, 3, 1, 1), Vector2i(9, 6)) # tea service
	stamp(objects, "C", Rect2i(14, 4, 1, 1), Vector2i(10, 6)) # gramophone
	stamp(furnishings, "B", Rect2i(8, 7, 3, 2), Vector2i(2, 8)) # piano
	stamp(furnishings, "B", Rect2i(8, 11, 1, 1), Vector2i(3, 10)) # stool
	for x in [2, 15]:
		stamp(furnishings, "B", Rect2i(4, 7, 2, 1), Vector2i(x, 5))
		stamp(furnishings, "B", Rect2i(11, 5, 1, 1), Vector2i(x + 2, 5))
		stamp(objects, "C", Rect2i(3, 3, 1, 1), Vector2i(x, 5))
	stamp(furnishings, "B", Rect2i(4, 7, 2, 1), Vector2i(15, 9))
	stamp(objects, "C", Rect2i(13, 4, 1, 1), Vector2i(15, 9)) # globe
	stamp(objects, "C", Rect2i(14, 4, 1, 1), Vector2i(16, 9))
	for pos in [Vector2i(1, 3), Vector2i(18, 3), Vector2i(1, 10), Vector2i(18, 10)]:
		stamp(furnishings, "C", Rect2i(6, 9, 1, 1), pos)
	for x in [7, 11]:
		stamp(furnishings, "C", Rect2i(8, 2, 1, 2), Vector2i(x, 1))
		stamp(furnishings, "C", Rect2i(8, 2, 1, 2), Vector2i(x, 9))
	var camera := Camera2D.new()
	camera.name = "Camera2D"
	camera.position = Vector2(320, 192)
	camera.zoom = Vector2.ONE
	room.add_child(camera)
	camera.owner = room
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://scenes/Parlor"))
	var packed := PackedScene.new()
	assert(packed.pack(room) == OK)
	assert(ResourceSaver.save(packed, MAP_PATH) == OK)
	room.free()
	var reloaded := ResourceLoader.load(MAP_PATH, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE) as PackedScene
	room = reloaded.instantiate()
	verify()
	var out := ProjectSettings.globalize_path(OUTPUT)
	DirAccess.make_dir_recursive_absolute(out)
	var file := FileAccess.open(out.path_join("validation.txt"), FileAccess.WRITE)
	file.store_string("\n".join(report) + "\n")
	file.close()
	print("\n".join(report))
	if "--capture" in OS.get_cmdline_user_args():
		var viewport := SubViewport.new()
		viewport.size = Vector2i(960, 640)
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.add_child(room)
		var cam := room.get_node("Camera2D") as Camera2D
		cam.zoom = Vector2.ONE
		for i in 5:
			await process_frame
		await RenderingServer.frame_post_draw
		assert(viewport.get_texture().get_image().save_png(out.path_join("parlor.png")) == OK)
	else:
		room.free()
	quit()

func layer(label: String) -> TileMapLayer:
	var result := TileMapLayer.new()
	result.name = label
	result.tile_set = tileset
	room.add_child(result)
	result.owner = room
	return result

func cells(rect: Rect2i) -> Array[Vector2i]:
	var result: Array[Vector2i] = []
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			result.append(Vector2i(x, y))
	return result

func paint(target: TileMapLayer, positions: Array[Vector2i], terrain_set: int, terrain_name: String) -> void:
	var terrain := -1
	for i in tileset.get_terrains_count(terrain_set):
		if tileset.get_terrain_name(terrain_set, i) == terrain_name:
			terrain = i
	assert(terrain >= 0, terrain_name)
	target.set_cells_terrain_connect(positions, terrain_set, terrain, false)
	for pos in positions:
		assert(target.get_cell_tile_data(pos).terrain == terrain)

func stamp(target: TileMapLayer, sheet: String, rect: Rect2i, destination: Vector2i) -> void:
	var id: int = sources["Victorian_Inside_" + sheet]
	for pos in cells(rect):
		assert(tileset.get_source(id).has_tile(pos))
		target.set_cell(destination + pos - rect.position, id, pos)

func verify() -> void:
	var total := 0
	var terrain_checks := 0
	var deltas := {
		TileSet.CELL_NEIGHBOR_RIGHT_SIDE: Vector2i(1, 0),
		TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER: Vector2i(1, 1),
		TileSet.CELL_NEIGHBOR_BOTTOM_SIDE: Vector2i(0, 1),
		TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER: Vector2i(-1, 1),
		TileSet.CELL_NEIGHBOR_LEFT_SIDE: Vector2i(-1, 0),
		TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER: Vector2i(-1, -1),
		TileSet.CELL_NEIGHBOR_TOP_SIDE: Vector2i(0, -1),
		TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER: Vector2i(1, -1),
	}
	for child in room.get_children():
		if child is not TileMapLayer:
			continue
		assert(child.tile_set.resource_path == TILESET_PATH)
		var used: Array[Vector2i] = child.get_used_cells()
		for pos in used:
			var data: TileData = child.get_cell_tile_data(pos)
			assert(data != null)
			if data.terrain_set < 0:
				continue
			for bit in deltas:
				if not data.is_valid_terrain_peering_bit(bit):
					continue
				var delta: Vector2i = deltas[bit]
				var neighbor: TileData = child.get_cell_tile_data(pos + delta)
				var connected: bool = neighbor != null and neighbor.terrain == data.terrain
				if delta.x != 0 and delta.y != 0:
					connected = connected and child.get_cell_source_id(pos + Vector2i(delta.x, 0)) >= 0 and child.get_cell_source_id(pos + Vector2i(0, delta.y)) >= 0
				assert(data.get_terrain_peering_bit(bit) == (data.terrain if connected else -1), "%s at %s bit %s" % [child.name, pos, bit])
				terrain_checks += 1
		total += used.size()
		report.append("%s: %d saved cells" % [child.name, used.size()])
	report.append("PASS: saved scene reload; %d valid cells; %d terrain neighbor checks; shared external TileSet." % [total, terrain_checks])
