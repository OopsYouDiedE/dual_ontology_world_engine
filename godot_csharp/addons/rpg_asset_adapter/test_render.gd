@tool
extends SceneTree

## Renders terrain connect test with actual GPU rendering, saves screenshot, exits.

const TileSetConverterScript = preload("res://addons/rpg_asset_adapter/converter/tileset_converter.gd")
const AutotileConverterScript = preload("res://addons/rpg_asset_adapter/converter/autotile_converter.gd")


func _init() -> void:
	print("--- TERRAIN RENDER TEST ---")
	var out_dir := ProjectSettings.globalize_path("res://imported_assets/test_victorian")
	DirAccess.make_dir_recursive_absolute(out_dir)

	var base := "res://AssetsSource/External/Victorian"
	var tile_size := 32

	var tileset := TileSetConverterScript.create_tileset(tile_size)

	# A2 (floor)
	var a2_res := AutotileConverterScript.generate_a2_derived_atlas(base + "/Victorian_Inside_A2.png", tile_size)
	if a2_res.get("image") != null:
		TileSetConverterScript.add_autotile_atlas_source(tileset, a2_res["image"], a2_res, tile_size)
		print("  A2 floor: %d terrains" % tileset.get_terrains_count(0))

	# A3 (wall)
	var a3_res := AutotileConverterScript.generate_a3_derived_atlas(base + "/Victorian_Inside_A3.png", tile_size)
	if a3_res.get("image") != null:
		TileSetConverterScript.add_autotile_atlas_source(tileset, a3_res["image"], a3_res, tile_size)
		print("  A3 wall: %d terrains in set 1" % tileset.get_terrains_count(1))

	# A4 (wall top + wall side)
	var a4_res := AutotileConverterScript.generate_a4_derived_atlas(base + "/Victorian_Inside_A4.png", tile_size)
	if a4_res.get("image") != null:
		TileSetConverterScript.add_autotile_atlas_source(tileset, a4_res["image"], a4_res, tile_size)
		print("  A4 wall top+side added")

	# A1 (water)
	var a1_res := AutotileConverterScript.generate_a1_derived_atlas(base + "/Victorian_Inside_A1.png", tile_size)
	if a1_res.get("image") != null:
		TileSetConverterScript.add_autotile_atlas_source(tileset, a1_res["image"], a1_res, tile_size)
		print("  A1 water: added")

	print("  Total: %d sources, TS0=%d terrains, TS1=%d terrains" % [
		tileset.get_source_count(),
		tileset.get_terrains_count(0),
		tileset.get_terrains_count(1)
	])

	# Create SubViewport for rendering
	var vp := SubViewport.new()
	vp.size = Vector2i(1440, 960)
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.transparent_bg = false
	root.add_child(vp)

	var cam := Camera2D.new()
	cam.position = Vector2(600, 420)
	cam.zoom = Vector2(1.0, 1.0)
	vp.add_child(cam)

	var tile_layer := TileMapLayer.new()
	tile_layer.tile_set = tileset
	vp.add_child(tile_layer)

	# Paint floor (TerrainSet 0, Terrain 0)
	var floor_cells: Array[Vector2i] = []
	for x in range(1, 7):
		for y in range(1, 7):
			floor_cells.append(Vector2i(x, y))
	for x in range(7, 13):
		floor_cells.append(Vector2i(x, 3))
		floor_cells.append(Vector2i(x, 4))
	for x in range(15, 21):
		floor_cells.append(Vector2i(x, 3))
		floor_cells.append(Vector2i(x, 4))
	for y in range(1, 7):
		floor_cells.append(Vector2i(17, y))
		floor_cells.append(Vector2i(18, y))
	floor_cells.append(Vector2i(23, 3))

	print("  Painting %d floor cells..." % floor_cells.size())
	tile_layer.set_cells_terrain_connect(floor_cells, 0, 0, false)

	# Paint wall (TerrainSet 1, Terrain 0)
	var wall_cells: Array[Vector2i] = []
	for x in range(1, 9):
		wall_cells.append(Vector2i(x, 10))
		wall_cells.append(Vector2i(x, 15))
	for y in range(11, 15):
		wall_cells.append(Vector2i(1, y))
		wall_cells.append(Vector2i(8, y))
	for x in range(8, 16):
		wall_cells.append(Vector2i(x, 12))
	for y in range(10, 16):
		wall_cells.append(Vector2i(12, y))
	for x in range(18, 23):
		wall_cells.append(Vector2i(x, 10))
	for y in range(10, 16):
		wall_cells.append(Vector2i(18, y))
	wall_cells.append(Vector2i(24, 12))

	print("  Painting %d wall cells..." % wall_cells.size())
	tile_layer.set_cells_terrain_connect(wall_cells, 1, 0, false)

	# Wait for rendering
	for f in range(10):
		await process_frame

	var render_img := vp.get_texture().get_image()
	if render_img != null:
		var save_path := out_dir.path_join("terrain_connect_test.png")
		var err := render_img.save_png(save_path)
		print("  Saved terrain_connect_test.png (error=%d, size=%dx%d)" % [err, render_img.get_width(), render_img.get_height()])
	else:
		print("  WARNING: Could not capture render (expected in headless mode)")

	vp.queue_free()
	print("--- DONE ---")
	quit(0)
