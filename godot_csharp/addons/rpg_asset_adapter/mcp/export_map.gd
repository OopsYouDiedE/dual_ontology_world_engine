extends SceneTree
## Exports a validated JSON map build into the project's runtime asset and preview paths.

const OUTPUT_ROOT := "res://ShareAsset/Internal/Props/TileMapWorkflow/"
const PREVIEW_SCENE := "res://scenes/TileMapWorkflow/Preview.tscn"
const PreviewPlayerScript = preload("res://addons/rpg_asset_adapter/mcp/preview_player.gd")
const TILE_SIZE := 32
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
const LAYER_ORDER := ["Floor", "WallFaces", "WallCaps", "Overhead"]

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 1 or not _is_absolute_path(args[0]):
		_fail("Expected exactly one absolute build.json path after --")
		return
	var config_path: String = args[0]
	if not FileAccess.file_exists(config_path):
		_fail("Build JSON does not exist: " + config_path)
		return
	var json := JSON.new()
	var parse_error := json.parse(FileAccess.get_file_as_string(config_path))
	if parse_error != OK or not json.data is Dictionary:
		_fail("Invalid build JSON: " + json.get_error_message())
		return
	var build: Dictionary = json.data
	var validation := _validate_build(build)
	if not validation.is_empty():
		_fail(validation)
		return
	var result := _export(build)
	if not result.ok:
		_fail(result.error)
		return
	print(JSON.stringify({"ok": true, "scene": PREVIEW_SCENE, "tileset": result.tileset,
		"texture": result.texture, "tiles": build.tiles.size(), "cells": build.cells.size()}))
	quit(0)

func _is_absolute_path(path: String) -> bool:
	return path.is_absolute_path()

func _validate_build(build: Dictionary) -> String:
	for key in ["atlas", "output", "scene", "tiles", "cells", "spawn", "size", "rows"]:
		if not build.has(key):
			return "Missing build field: " + key
	if typeof(build.atlas) != TYPE_STRING or not _is_shareasset_png(str(build.atlas)):
		return "atlas must be a PNG under res://ShareAsset/"
	if typeof(build.output) != TYPE_STRING:
		return "output must be a res:// path"
	var output_path := str(build.output).trim_suffix("/") + "/"
	if not output_path.begins_with(OUTPUT_ROOT) or output_path.split("/").has(".."):
		return "output must be inside " + OUTPUT_ROOT
	if build.scene != PREVIEW_SCENE:
		return "scene must equal " + PREVIEW_SCENE
	if not build.tiles is Array or not build.cells is Array:
		return "tiles and cells must be arrays"
	if not build.rows is Array or build.rows.size() != int(build.size[1]):
		return "rows must contain one string per map row"
	for row in build.rows:
		if typeof(row) != TYPE_STRING or row.length() != int(build.size[0]):
			return "Each rows entry must be a string matching map width; spaces mark void"
	if not build.spawn is Array or build.spawn.size() != 2 or not build.size is Array or build.size.size() != 2:
		return "spawn and size must be two-element arrays"
	if not _is_number(build.spawn[0]) or not _is_number(build.spawn[1]) or not _is_number(build.size[0]) or not _is_number(build.size[1]):
		return "spawn and size entries must be numbers"
	if int(build.size[0]) <= 0 or int(build.size[1]) <= 0:
		return "size entries must be positive cell counts"
	var image := Image.load_from_file(ProjectSettings.globalize_path(build.atlas))
	if image == null or image.is_empty():
		return "Could not load atlas PNG: " + str(build.atlas)
	if image.get_width() % TILE_SIZE != 0 or image.get_height() % TILE_SIZE != 0:
		return "Atlas dimensions must be multiples of 32px"
	var tile_ids := {}
	var atlas_coords_seen := {}
	for tile in build.tiles:
		if not tile is Dictionary or not tile.has_all(["id", "atlas", "solid", "terrain", "mask"]):
			return "Each tile needs id, atlas, solid, terrain, and mask"
		if not _is_integer(tile.id) or not tile.atlas is Array or tile.atlas.size() != 2:
			return "Tile id and atlas coordinates must be integers"
		if not _is_integer(tile.atlas[0]) or not _is_integer(tile.atlas[1]) or int(tile.atlas[0]) < 0 or int(tile.atlas[1]) < 0:
			return "Atlas coordinates must be non-negative integers"
		if int(tile.atlas[0]) >= image.get_width() / TILE_SIZE or int(tile.atlas[1]) >= image.get_height() / TILE_SIZE:
			return "Tile atlas coordinate lies outside the atlas"
		if not _is_integer(tile.terrain) or int(tile.terrain) < -1 or int(tile.terrain) > 0 or not _is_integer(tile.mask) or int(tile.mask) < 0 or int(tile.mask) > 255:
			return "Tile terrain must be -1 or 0 and mask must be in 0..255"
		if typeof(tile.solid) != TYPE_BOOL or not _is_integer(tile.id):
			return "Tile solid must be boolean and id must be integer"
		if tile_ids.has(int(tile.id)):
			return "Duplicate tile id: " + str(tile.id)
		var tile_coords := Vector2i(int(tile.atlas[0]), int(tile.atlas[1]))
		if atlas_coords_seen.has(tile_coords):
			return "Duplicate atlas coordinates: " + str(tile_coords)
		tile_ids[int(tile.id)] = tile_coords
		atlas_coords_seen[tile_coords] = true
	var occupied := {}
	for cell in build.cells:
		if not cell is Dictionary or not cell.has_all(["layer", "x", "y", "tile"]):
			return "Each cell needs layer, x, y, and tile"
		if not LAYER_ORDER.has(str(cell.layer)) or not _is_integer(cell.x) or not _is_integer(cell.y) or not _is_integer(cell.tile):
			return "Cell layer, coordinates, or tile id are invalid"
		if int(cell.x) < 0 or int(cell.y) < 0 or int(cell.x) >= int(build.size[0]) or int(cell.y) >= int(build.size[1]):
			return "Cell coordinate lies outside size"
		if not tile_ids.has(int(cell.tile)):
			return "Cell references unknown tile id: " + str(cell.tile)
		var key := "%s:%d:%d" % [cell.layer, int(cell.x), int(cell.y)]
		if occupied.has(key):
			return "Duplicate cell: " + key
		occupied[key] = true
	if build.has("props") and not build.props is Array:
		return "props must be an array when provided"
	for prop in build.get("props", []):
		if not prop is Dictionary or not prop.has_all(["texture", "position", "anchor", "collision", "overhead"]):
			return "Each prop needs texture, position, anchor, collision, and overhead"
		if typeof(prop.texture) != TYPE_STRING or not _is_shareasset_png(str(prop.texture)):
			return "Prop texture must be a PNG under res://ShareAsset/"
		if not _pair_of_numbers(prop.position) or not _pair_of_numbers(prop.anchor):
			return "Prop position and anchor must be two-number arrays"
		if not prop.collision is Array or not (prop.collision.is_empty() or prop.collision.size() == 4):
			return "Prop collision must be empty or [x,y,width,height]"
		if not prop.collision.is_empty():
			for value in prop.collision:
				if not _is_number(value):
					return "Prop collision values must be numbers"
			if float(prop.collision[2]) <= 0.0 or float(prop.collision[3]) <= 0.0:
				return "Prop collision width and height must be positive"
		if typeof(prop.overhead) != TYPE_BOOL or (prop.has("ground") and typeof(prop.ground) != TYPE_BOOL):
			return "Prop overhead and optional ground must be boolean"
	return ""

func _export(build: Dictionary) -> Dictionary:
	var output_dir := str(build.output).trim_suffix("/") + "/"
	var output_absolute := ProjectSettings.globalize_path(output_dir)
	var scene_absolute := ProjectSettings.globalize_path(PREVIEW_SCENE)
	var scene_dir := scene_absolute.get_base_dir()
	var dir_error := DirAccess.make_dir_recursive_absolute(output_absolute)
	if dir_error != OK:
		return {"ok": false, "error": "Could not create output directory: " + error_string(dir_error)}
	dir_error = DirAccess.make_dir_recursive_absolute(scene_dir)
	if dir_error != OK:
		return {"ok": false, "error": "Could not create preview scene directory: " + error_string(dir_error)}
	var image := Image.load_from_file(ProjectSettings.globalize_path(build.atlas))
	var texture := ImageTexture.create_from_image(image)
	var texture_path := output_dir + "atlas_texture.res"
	var save_error := ResourceSaver.save(texture, texture_path)
	if save_error != OK:
		return {"ok": false, "error": "Could not save atlas texture: " + error_string(save_error)}
	var saved_texture := ResourceLoader.load(texture_path) as Texture2D
	var tileset := _create_tileset(build, saved_texture)
	var tileset_path := output_dir + "TileMapWorkflow.tres"
	save_error = ResourceSaver.save(tileset, tileset_path)
	if save_error != OK:
		return {"ok": false, "error": "Could not save TileSet: " + error_string(save_error)}
	var prop_textures: Array[Texture2D] = []
	var props: Array = build.get("props", [])
	for i in props.size():
		var prop_image := Image.load_from_file(ProjectSettings.globalize_path(props[i].texture))
		if prop_image == null or prop_image.is_empty():
			return {"ok": false, "error": "Could not load prop PNG: " + str(props[i].texture)}
		var prop_texture := ImageTexture.create_from_image(prop_image)
		var prop_texture_path := output_dir + "prop_%d.res" % i
		save_error = ResourceSaver.save(prop_texture, prop_texture_path)
		if save_error != OK:
			return {"ok": false, "error": "Could not save prop texture: " + error_string(save_error)}
		prop_textures.append(ResourceLoader.load(prop_texture_path) as Texture2D)
	var packed := _create_preview(build, tileset, prop_textures)
	save_error = ResourceSaver.save(packed, PREVIEW_SCENE)
	if save_error != OK:
		return {"ok": false, "error": "Could not save preview scene: " + error_string(save_error)}
	return {"ok": true, "texture": texture_path, "tileset": tileset_path}

func _create_tileset(build: Dictionary, texture: Texture2D) -> TileSet:
	var tileset := TileSet.new()
	tileset.tile_size = Vector2i(TILE_SIZE, TILE_SIZE)
	tileset.add_terrain_set()
	tileset.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
	tileset.add_terrain(0)
	tileset.set_terrain_name(0, 0, "墙顶")
	tileset.add_physics_layer()
	tileset.set_physics_layer_collision_layer(0, 1)
	tileset.set_physics_layer_collision_mask(0, 1)
	var atlas := TileSetAtlasSource.new()
	atlas.texture = texture
	atlas.texture_region_size = Vector2i(TILE_SIZE, TILE_SIZE)
	# Attach the atlas before requesting TileData so it inherits TileSet physics layers.
	tileset.add_source(atlas)
	for tile in build.tiles:
		var coords := Vector2i(int(tile.atlas[0]), int(tile.atlas[1]))
		atlas.create_tile(coords)
		var data := atlas.get_tile_data(coords, 0)
		if int(tile.terrain) == 0:
			data.terrain_set = 0
			data.terrain = 0
			var mask := int(tile.mask)
			for i in TERRAIN_NEIGHBORS.size():
				data.set_terrain_peering_bit(TERRAIN_NEIGHBORS[i], 0 if mask & MASK_BITS[i] else -1)
		if bool(tile.solid):
			data.set_collision_polygons_count(0, 1)
			data.set_collision_polygon_points(0, 0, PackedVector2Array([
				Vector2(-16, -16), Vector2(16, -16), Vector2(16, 16), Vector2(-16, 16)]))
	return tileset

func _create_preview(build: Dictionary, tileset: TileSet, prop_textures: Array[Texture2D]) -> PackedScene:
	var root := Node2D.new()
	root.name = "TileMapPreview"
	root.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	root.y_sort_enabled = true
	root.set_meta("preview_size_cells", Vector2i(int(build.size[0]), int(build.size[1])))
	for layer_name in LAYER_ORDER:
		var layer := TileMapLayer.new()
		layer.name = layer_name
		layer.tile_set = tileset
		match layer_name:
			"Floor": layer.z_index = -3
			"WallFaces": layer.z_index = -2
			"WallCaps": layer.z_index = 2
			"Overhead": layer.z_index = 10
		root.add_child(layer)
		layer.owner = root
	for cell in build.cells:
		var layer := root.get_node(str(cell.layer)) as TileMapLayer
		var atlas_coords: Vector2i = _tile_coords(build.tiles, int(cell.tile))
		layer.set_cell(Vector2i(int(cell.x), int(cell.y)), 0, atlas_coords)
	_add_void_blockers(root, build.rows, Vector2i(int(build.size[0]), int(build.size[1])))
	_add_map_boundaries(root, Vector2i(int(build.size[0]), int(build.size[1])))
	var props_root := Node2D.new()
	props_root.name = "Props"
	props_root.y_sort_enabled = true
	root.add_child(props_root)
	props_root.owner = root
	var props: Array = build.get("props", [])
	for i in props.size():
		_add_prop(props_root, props[i], prop_textures[i], i)
	var player := CharacterBody2D.new()
	player.name = "PreviewPlayer"
	player.position = Vector2(float(build.spawn[0]), float(build.spawn[1]))
	player.set_script(PreviewPlayerScript)
	player.set("map_pixel_size", Vector2(int(build.size[0]) * TILE_SIZE, int(build.size[1]) * TILE_SIZE))
	root.add_child(player)
	player.owner = root
	var shape := CollisionShape2D.new()
	shape.name = "FootCollision"
	var rectangle := RectangleShape2D.new()
	rectangle.size = Vector2(16, 12)
	shape.shape = rectangle
	shape.position = Vector2(0, -6)
	player.add_child(shape)
	shape.owner = root
	var packed := PackedScene.new()
	packed.pack(root)
	root.free()
	return packed

func _add_void_blockers(root: Node2D, rows: Array, map_size: Vector2i) -> void:
	for y in map_size.y:
		var row: String = rows[y]
		for x in map_size.x:
			if row.substr(x, 1) != " ":
				continue
			_add_static_rect(root, "Void_%d_%d" % [x, y], Vector2(x * TILE_SIZE + 16, y * TILE_SIZE + 16), Vector2(TILE_SIZE, TILE_SIZE))

func _add_map_boundaries(root: Node2D, map_size: Vector2i) -> void:
	var world_size := Vector2(map_size) * TILE_SIZE
	_add_static_rect(root, "BoundaryTop", Vector2(world_size.x * 0.5, -16), Vector2(world_size.x + 64, 32))
	_add_static_rect(root, "BoundaryBottom", Vector2(world_size.x * 0.5, world_size.y + 16), Vector2(world_size.x + 64, 32))
	_add_static_rect(root, "BoundaryLeft", Vector2(-16, world_size.y * 0.5), Vector2(32, world_size.y))
	_add_static_rect(root, "BoundaryRight", Vector2(world_size.x + 16, world_size.y * 0.5), Vector2(32, world_size.y))

func _add_static_rect(parent: Node, label: String, center: Vector2, size: Vector2) -> void:
	var body := StaticBody2D.new()
	body.name = label
	body.position = center
	body.collision_layer = 1
	body.collision_mask = 1
	parent.add_child(body)
	body.owner = parent.owner if parent.owner != null else parent
	var shape_node := CollisionShape2D.new()
	var rectangle := RectangleShape2D.new()
	rectangle.size = size
	shape_node.shape = rectangle
	body.add_child(shape_node)
	shape_node.owner = body.owner

func _add_prop(parent: Node2D, prop: Dictionary, texture: Texture2D, index: int) -> void:
	var ground := Vector2(float(prop.position[0]), float(prop.position[1]))
	var node := Node2D.new()
	node.name = "Prop_%d" % index
	node.position = ground
	node.y_sort_enabled = true
	node.z_index = -1 if bool(prop.get("ground", false)) else (10 if prop.overhead else 0)
	parent.add_child(node)
	node.owner = parent.owner
	var sprite := Sprite2D.new()
	sprite.texture = texture
	sprite.offset = Vector2(texture.get_size()) * 0.5 - Vector2(float(prop.anchor[0]), float(prop.anchor[1]))
	node.add_child(sprite)
	sprite.owner = parent.owner
	if not prop.collision.is_empty():
		var body := StaticBody2D.new()
		body.name = "Footprint"
		body.position = Vector2(float(prop.collision[0]) + float(prop.collision[2]) * 0.5,
			float(prop.collision[1]) + float(prop.collision[3]) * 0.5)
		body.collision_layer = 1
		body.collision_mask = 1
		node.add_child(body)
		body.owner = parent.owner
		var collision := CollisionShape2D.new()
		collision.name = "CollisionShape2D"
		var rectangle := RectangleShape2D.new()
		rectangle.size = Vector2(float(prop.collision[2]), float(prop.collision[3]))
		collision.shape = rectangle
		body.add_child(collision)
		collision.owner = parent.owner

func _tile_coords(tiles: Array, id: int) -> Vector2i:
	for tile in tiles:
		if int(tile.id) == id:
			return Vector2i(int(tile.atlas[0]), int(tile.atlas[1]))
	return Vector2i(-1, -1)

func _is_integer(value: Variant) -> bool:
	return typeof(value) == TYPE_INT or (typeof(value) == TYPE_FLOAT and is_equal_approx(float(value), roundf(float(value))))

func _is_number(value: Variant) -> bool:
	return typeof(value) == TYPE_INT or typeof(value) == TYPE_FLOAT

func _pair_of_numbers(value: Variant) -> bool:
	if not value is Array or value.size() != 2:
		return false
	return _is_number(value[0]) and _is_number(value[1])

func _is_shareasset_png(path: String) -> bool:
	if not path.begins_with("res://ShareAsset/") or not path.ends_with(".png"):
		return false
	return not path.split("/").has("..")

func _fail(message: String) -> void:
	push_error(message)
	quit(1)
