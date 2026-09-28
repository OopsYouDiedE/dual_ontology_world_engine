extends SceneTree
## Runtime checks for the generated TileMap workflow preview.

const BUILD_PATH := "res://ShareAsset/Internal/Props/TileMapWorkflow/build.json"
const PREVIEW_PATH := "res://scenes/TileMapWorkflow/Preview.tscn"
const TERRAIN_NEIGHBORS := [
	TileSet.CELL_NEIGHBOR_TOP_SIDE,
	TileSet.CELL_NEIGHBOR_RIGHT_SIDE,
	TileSet.CELL_NEIGHBOR_BOTTOM_SIDE,
	TileSet.CELL_NEIGHBOR_LEFT_SIDE,
	TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER,
	TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER,
	TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER,
	TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER,
]
const MASK_BITS := [1, 2, 4, 8, 16, 32, 64, 128]

var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var build = JSON.parse_string(FileAccess.get_file_as_string(BUILD_PATH))
	if not build is Dictionary:
		_fail("Could not read map build source")
		return
	root.size = Vector2i(int(build.size[0]) * 32, int(build.size[1]) * 32)
	root.content_scale_size = root.size
	var scene := load(PREVIEW_PATH) as PackedScene
	if scene == null:
		_fail("Could not load generated Preview.tscn")
		return
	var preview := scene.instantiate() as Node2D
	root.add_child(preview)
	await physics_frame
	_check_occlusion(preview, build)
	_check_terrain_variants(preview, build)
	_check_wall_blocking(preview, build)
	_check_door_passage(preview, build)
	_check_prop_footprint(preview, build)
	await process_frame
	await process_frame
	if DisplayServer.get_name() != "headless":
		var screenshot := get_root().get_texture().get_image()
		var screenshot_path := ProjectSettings.globalize_path("res://../artifacts/tilemap-workflow/runtime.png")
		var screenshot_error := screenshot.save_png(screenshot_path)
		_check(screenshot_error == OK, "Could not save the runtime viewport capture")
	if failures.is_empty():
		print(JSON.stringify({"ok": true, "checks": ["wall_blocking", "door_passage", "prop_footprint", "terrain_47_variants", "occlusion"], "terrain_variants": 47}))
		preview.queue_free()
		await process_frame
		quit(0)
	else:
		for failure in failures:
			push_error(failure)
		preview.queue_free()
		await process_frame
		quit(1)

func _check_occlusion(preview: Node2D, build: Dictionary) -> void:
	_check(preview.y_sort_enabled, "Preview root must y-sort its children")
	_check((preview.get_node("Props") as Node2D).y_sort_enabled, "Props container must y-sort props")
	_check((preview.get_node("Floor") as TileMapLayer).z_index == -3, "Floor z-index must be -3")
	_check((preview.get_node("WallFaces") as TileMapLayer).z_index == -2, "WallFaces z-index must be -2")
	_check((preview.get_node("WallCaps") as TileMapLayer).z_index == 2, "WallCaps z-index must be 2")
	_check((preview.get_node("Overhead") as TileMapLayer).z_index == 10, "Overhead z-index must be 10")
	_check((preview.get_node("PreviewPlayer") as CharacterBody2D).z_index == 0, "Preview character z-index must be 0")
	var props: Array = build.get("props", [])
	for i in props.size():
		var node := preview.get_node("Props/Prop_%d" % i) as Node2D
		var expected_z := -1 if bool(props[i].get("ground", false)) else (10 if props[i].overhead else 0)
		_check(node.z_index == expected_z, "Prop_%d has an incorrect occlusion z-index" % i)

func _check_terrain_variants(preview: Node2D, build: Dictionary) -> void:
	var tile_set := (preview.get_node("Floor") as TileMapLayer).tile_set
	var atlas := tile_set.get_source(0) as TileSetAtlasSource
	var variants: Array = build.tiles.filter(func(tile: Dictionary) -> bool: return int(tile.terrain) == 0)
	_check(variants.size() == 47, "Expected 47 wall-top terrain variants")
	for tile in variants:
		var coords := Vector2i(int(tile.atlas[0]), int(tile.atlas[1]))
		var data := atlas.get_tile_data(coords, 0)
		_check(data.terrain_set == 0 and data.terrain == 0, "Wall-top variant %d has no terrain" % tile.id)
		for i in TERRAIN_NEIGHBORS.size():
			var expected := 0 if (int(tile.mask) & MASK_BITS[i]) != 0 else -1
			_check(data.get_terrain_peering_bit(TERRAIN_NEIGHBORS[i]) == expected,
				"Wall-top variant %d has incorrect peering bit %d" % [tile.id, i])

func _check_wall_blocking(preview: Node2D, build: Dictionary) -> void:
	var solid_ids := _solid_tile_ids(build)
	var occupied := _solid_map_cells(build, solid_ids)
	var candidate: Dictionary = {}
	for cell in build.cells:
		if cell.layer != "WallFaces" or not solid_ids.has(int(cell.tile)):
			continue
		var x := int(cell.x)
		var y := int(cell.y)
		if y + 1 >= build.rows.size() or build.rows[y + 1].substr(x, 1) != "." or occupied.has(Vector2i(x, y + 1)):
			continue
		candidate = cell
		break
	_check(not candidate.is_empty(), "Could not find a wall with walkable space next to it in build source")
	if candidate.is_empty(): return
	var x := int(candidate.x)
	var y := int(candidate.y)
	var player := preview.get_node("PreviewPlayer") as CharacterBody2D
	var from := Vector2((x + 0.5) * 32.0, (y + 1.5) * 32.0)
	_check(_would_collide(player, from, Vector2(0, -32)), "Player should be blocked by a wall cell selected from the map source")

func _check_door_passage(preview: Node2D, build: Dictionary) -> void:
	if not build.get("doors", []).is_empty():
		var actor := preview.get_node("PreviewPlayer") as CharacterBody2D
		for door in build.doors:
			var rect: Array = door.rect
			var x := float(rect[0]) * 32.0
			var y := float(rect[1]) * 32.0
			var w := float(rect[2]) * 32.0
			var h := float(rect[3]) * 32.0
			var vertical := str(door.facing) in ["north", "south"]
			var start := Vector2(x + w * 0.5, y - 4) if vertical else Vector2(x - 16, y + h * 0.5 + 6)
			var motion := Vector2(0, h + 32) if vertical else Vector2(w + 32, 0)
			_check(not _would_collide(actor, start, motion), "Declared door blocked: " + str(door.name))
		return
	var solid_ids := _solid_tile_ids(build)
	var occupied := _solid_map_cells(build, solid_ids)
	var candidate := Vector2i(-1, -1)
	for y in build.rows.size():
		var row: String = build.rows[y]
		for x in row.length():
			if row.substr(x, 1) != "D" or x == 0 or x + 1 >= row.length():
				continue
			if row.substr(x - 1, 1) not in [".", "D"] or row.substr(x + 1, 1) not in [".", "D"]:
				continue
			if occupied.has(Vector2i(x - 1, y)) or occupied.has(Vector2i(x, y)) or occupied.has(Vector2i(x + 1, y)):
				continue
			candidate = Vector2i(x, y)
			break
		if candidate.x >= 0: break
	_check(candidate.x >= 0, "Could not find a traversable door opening in build source")
	if candidate.x < 0: return
	var player := preview.get_node("PreviewPlayer") as CharacterBody2D
	var from := Vector2((candidate.x - 0.5) * 32.0, (candidate.y + 0.5) * 32.0)
	_check(not _would_collide(player, from, Vector2(64, 0)), "Player should pass through the door opening")

func _check_prop_footprint(preview: Node2D, build: Dictionary) -> void:
	var props: Array = build.get("props", [])
	if props.is_empty():
		print("Architecture export has no furniture: gameplay furniture requires its own runtime review.")
		return
	for i in props.size():
		var prop: Dictionary = props[i]
		if prop.collision.is_empty(): continue
		var node := preview.get_node("Props/Prop_%d" % i) as Node2D
		var shape := node.get_node_or_null("Footprint/CollisionShape2D") as CollisionShape2D
		if shape == null:
			_check(false, "Prop_%d needs its declared collision shape" % i)
			continue
		var expected_size := Vector2(float(prop.collision[2]), float(prop.collision[3]))
		_check(shape.shape is RectangleShape2D and (shape.shape as RectangleShape2D).size == expected_size,
			"Prop_%d must use its footprint rather than image bounds" % i)
		var footprint_center := node.position + Vector2(float(prop.collision[0]) + expected_size.x * 0.5,
			float(prop.collision[1]) + expected_size.y * 0.5)
		var player := preview.get_node("PreviewPlayer") as CharacterBody2D
		_check(_would_collide(player, footprint_center + Vector2(0, 6), Vector2.ZERO),
			"Prop_%d footprint must block the actual actor" % i)

func _would_collide(player: CharacterBody2D, from: Vector2, motion: Vector2) -> bool:
	return player.test_move(Transform2D(0.0, from), motion)

func _solid_tile_ids(build: Dictionary) -> Dictionary:
	var result := {}
	for tile in build.tiles:
		if bool(tile.solid): result[int(tile.id)] = true
	return result

func _solid_map_cells(build: Dictionary, solid_ids: Dictionary) -> Dictionary:
	var result := {}
	for cell in build.cells:
		if solid_ids.has(int(cell.tile)):
			result[Vector2i(int(cell.x), int(cell.y))] = true
	return result

func _check(condition: bool, message: String) -> void:
	if not condition: failures.append(message)

func _fail(message: String) -> void:
	push_error(message)
	quit(1)
