@tool
extends SceneTree

## Simplified visual verification test that generates derived atlas images
## and terrain connect test renders without reloading saved resources.

const AdapterFacadeScript = preload("res://addons/rpg_asset_adapter/adapter_facade.gd")
const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")
const TileSetConverterScript = preload("res://addons/rpg_asset_adapter/converter/tileset_converter.gd")
const AutotileConverterScript = preload("res://addons/rpg_asset_adapter/converter/autotile_converter.gd")
const AnimationConverterScript = preload("res://addons/rpg_asset_adapter/converter/animation_converter.gd")


func _init() -> void:
	print("==================================================")
	print("--- VISUAL VERIFICATION TEST ---")
	print("==================================================")

	var out_dir := "res://imported_assets/test_victorian"
	var global_out := ProjectSettings.globalize_path(out_dir)
	DirAccess.make_dir_recursive_absolute(global_out)

	var success := true
	success = success and _test_derived_atlases(global_out)
	success = success and _test_door_chest_slicing(global_out)
	success = success and await _test_terrain_connect(global_out)

	if success:
		print("==================================================")
		print(">>> ALL VISUAL TESTS PASSED <<<")
		print("==================================================")
		quit(0)
	else:
		push_error("!!! VISUAL TESTS FAILED !!!")
		quit(1)


func _test_derived_atlases(out_dir: String) -> bool:
	print("\n[TEST 1] Generating derived autotile atlases...")
	var base := "res://AssetsSource/External/Victorian"

	# A1
	var a1_path := base + "/Victorian_Inside_A1.png"
	var a1_res := AutotileConverterScript.generate_a1_derived_atlas(a1_path, 32)
	var a1_img: Image = a1_res.get("image", null)
	if a1_img != null:
		a1_img.save_png(out_dir.path_join("a1_derived.png"))
		print("  A1 derived atlas: %dx%d, autotiles=%d" % [a1_img.get_width(), a1_img.get_height(), a1_res["autotiles"].size()])
	else:
		push_error("FAIL: A1 derived atlas is null")
		return false

	# A2
	var a2_path := base + "/Victorian_Inside_A2.png"
	var a2_res := AutotileConverterScript.generate_a2_derived_atlas(a2_path, 32)
	var a2_img: Image = a2_res.get("image", null)
	if a2_img != null:
		a2_img.save_png(out_dir.path_join("a2_derived.png"))
		print("  A2 derived atlas: %dx%d, autotiles=%d" % [a2_img.get_width(), a2_img.get_height(), a2_res["autotiles"].size()])
	else:
		push_error("FAIL: A2 derived atlas is null")
		return false

	# A3
	var a3_path := base + "/Victorian_Inside_A3.png"
	var a3_res := AutotileConverterScript.generate_a3_derived_atlas(a3_path, 32)
	var a3_img: Image = a3_res.get("image", null)
	if a3_img != null:
		a3_img.save_png(out_dir.path_join("a3_derived.png"))
		print("  A3 derived atlas: %dx%d, autotiles=%d" % [a3_img.get_width(), a3_img.get_height(), a3_res["autotiles"].size()])
	else:
		push_error("FAIL: A3 derived atlas is null")
		return false

	# A4
	var a4_path := base + "/Victorian_Inside_A4.png"
	var a4_res := AutotileConverterScript.generate_a4_derived_atlas(a4_path, 32)
	var a4_img: Image = a4_res.get("image", null)
	if a4_img != null:
		a4_img.save_png(out_dir.path_join("a4_derived.png"))
		print("  A4 derived atlas: %dx%d, autotiles=%d" % [a4_img.get_width(), a4_img.get_height(), a4_res["autotiles"].size()])
	else:
		push_error("FAIL: A4 derived atlas is null")
		return false

	print("  PASSED: All derived atlases generated.")
	return true


func _test_door_chest_slicing(out_dir: String) -> bool:
	print("\n[TEST 2] Testing door & chest slicing...")

	# Find a door sheet
	var door_files: Array[String] = []
	var chest_files: Array[String] = []
	var scan_dir := ProjectSettings.globalize_path("res://AssetsSource/External/Victorian")
	var dir := DirAccess.open(scan_dir)
	if dir:
		dir.list_dir_begin()
		var fname := dir.get_next()
		while fname != "":
			var lower := fname.to_lower()
			if lower.ends_with(".png"):
				if lower.contains("door"):
					door_files.append(scan_dir.path_join(fname))
				elif lower.contains("chest"):
					chest_files.append(scan_dir.path_join(fname))
			fname = dir.get_next()
		dir.list_dir_end()

	if door_files.size() > 0:
		var d_res := AnimationConverterScript.slice_doors(door_files[0], 32, 6.0)
		var d_objs: Array = d_res.get("objects", [])
		print("  Door %s: %d objects extracted" % [door_files[0].get_file(), d_objs.size()])
		if d_objs.size() == 0:
			push_error("FAIL: No door objects extracted.")
			return false
		# Verify first door's SpriteFrames has correct animations
		var sf: SpriteFrames = d_objs[0]
		var anims := sf.get_animation_names()
		print("    Animations: %s" % str(anims))
		if not sf.has_animation("closed") or not sf.has_animation("open") or not sf.has_animation("opened"):
			push_error("FAIL: Door missing expected animations (closed/open/opened)")
			return false
		var fw: int = sf.get_meta("frame_width", 0)
		var fh: int = sf.get_meta("frame_height", 0)
		print("    Frame size: %dx%d" % [fw, fh])
	else:
		print("  SKIP: No door files found")

	if chest_files.size() > 0:
		var c_res := AnimationConverterScript.slice_chests(chest_files[0], 32, 6.0)
		var c_objs: Array = c_res.get("objects", [])
		print("  Chest %s: %d objects extracted" % [chest_files[0].get_file(), c_objs.size()])
		if c_objs.size() == 0:
			push_error("FAIL: No chest objects extracted.")
			return false
		var sf: SpriteFrames = c_objs[0]
		var anims := sf.get_animation_names()
		print("    Animations: %s" % str(anims))
		var fw: int = sf.get_meta("frame_width", 0)
		var fh: int = sf.get_meta("frame_height", 0)
		print("    Frame size: %dx%d" % [fw, fh])
	else:
		print("  SKIP: No chest files found")

	print("  PASSED: Door/Chest slicing verified.")
	return true


func _test_terrain_connect(out_dir: String) -> bool:
	if DisplayServer.get_name() == "headless":
		print("\n[TEST 3] Terrain connect visual test...")
		print("  SKIPPED: Viewport capture requires non-headless renderer.")
		return true

	print("\n[TEST 3] Terrain connect visual test...")
	var base := "res://AssetsSource/External/Victorian"
	var tile_size := 32

	# Build TileSet in-memory (no save/reload)
	var tileset := TileSetConverterScript.create_tileset(tile_size)

	# Add A2 (floor terrain)
	var a2_res := AutotileConverterScript.generate_a2_derived_atlas(base + "/Victorian_Inside_A2.png", tile_size)
	var a2_img: Image = a2_res.get("image", null)
	if a2_img != null:
		TileSetConverterScript.add_autotile_atlas_source(tileset, a2_img, a2_res, tile_size)
		print("  Added A2 floor autotile source (terrain_set=0)")

	# Add A3 (wall terrain)
	var a3_res := AutotileConverterScript.generate_a3_derived_atlas(base + "/Victorian_Inside_A3.png", tile_size)
	var a3_img: Image = a3_res.get("image", null)
	if a3_img != null:
		TileSetConverterScript.add_autotile_atlas_source(tileset, a3_img, a3_res, tile_size)
		print("  Added A3 wall autotile source (terrain_set=1)")

	# Add A4 (wall top + wall side)
	var a4_res := AutotileConverterScript.generate_a4_derived_atlas(base + "/Victorian_Inside_A4.png", tile_size)
	var a4_img: Image = a4_res.get("image", null)
	if a4_img != null:
		TileSetConverterScript.add_autotile_atlas_source(tileset, a4_img, a4_res, tile_size)
		print("  Added A4 wall top/side autotile source")

	print("  TileSet sources: %d, TerrainSet 0 terrains: %d, TerrainSet 1 terrains: %d" % [
		tileset.get_source_count(),
		tileset.get_terrains_count(0),
		tileset.get_terrains_count(1)
	])

	if tileset.get_terrains_count(0) == 0:
		push_error("FAIL: No terrains in TerrainSet 0")
		return false
	if tileset.get_terrains_count(1) == 0:
		push_error("FAIL: No terrains in TerrainSet 1")
		return false

	# Create viewport and TileMapLayer
	var vp := SubViewport.new()
	vp.size = Vector2i(1200, 900)
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(vp)

	var tile_layer := TileMapLayer.new()
	tile_layer.tile_set = tileset
	vp.add_child(tile_layer)

	# Paint floor terrain (TerrainSet 0, Terrain 0)
	var floor_cells: Array[Vector2i] = []
	# 6x6 block
	for x in range(1, 7):
		for y in range(1, 7):
			floor_cells.append(Vector2i(x, y))
	# corridor extending right
	for x in range(7, 13):
		floor_cells.append(Vector2i(x, 3))
		floor_cells.append(Vector2i(x, 4))
	# cross / plus sign
	for x in range(15, 21):
		floor_cells.append(Vector2i(x, 3))
		floor_cells.append(Vector2i(x, 4))
	for y in range(1, 7):
		floor_cells.append(Vector2i(17, y))
		floor_cells.append(Vector2i(18, y))
	# single isolated tile
	floor_cells.append(Vector2i(23, 3))

	print("  Painting %d floor cells (TerrainSet 0, Terrain 0)..." % floor_cells.size())
	tile_layer.set_cells_terrain_connect(floor_cells, 0, 0, false)

	# Paint wall terrain (TerrainSet 1, Terrain 0)
	var wall_cells: Array[Vector2i] = []
	# hollow box
	for x in range(1, 9):
		wall_cells.append(Vector2i(x, 10))
		wall_cells.append(Vector2i(x, 15))
	for y in range(11, 15):
		wall_cells.append(Vector2i(1, y))
		wall_cells.append(Vector2i(8, y))
	# T-junction + corridor
	for x in range(8, 16):
		wall_cells.append(Vector2i(x, 12))
	for y in range(10, 16):
		wall_cells.append(Vector2i(12, y))
	# L-shape
	for x in range(18, 23):
		wall_cells.append(Vector2i(x, 10))
	for y in range(10, 16):
		wall_cells.append(Vector2i(18, y))
	# isolated single wall
	wall_cells.append(Vector2i(24, 12))

	print("  Painting %d wall cells (TerrainSet 1, Terrain 0)..." % wall_cells.size())
	tile_layer.set_cells_terrain_connect(wall_cells, 1, 0, false)

	# Wait for rendering
	for f in range(5):
		await process_frame

	var render_img := vp.get_texture().get_image()
	if render_img != null:
		var save_path := out_dir.path_join("terrain_connect_test.png")
		var err := render_img.save_png(save_path)
		print("  Saved terrain_connect_test.png (error=%d)" % err)
	else:
		push_error("FAIL: Could not capture viewport render")
		vp.queue_free()
		return false

	tile_layer.queue_free()
	vp.queue_free()

	print("  PASSED: Terrain connect visual test completed.")
	return true
