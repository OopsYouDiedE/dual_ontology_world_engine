@tool
extends SceneTree

## End-to-end test suite for the RPG Asset Adapter plugin.
## Verifies scanning, profile selection, autotile generation, TileSet building,
## animation slicing, scene generation, and Tilesets.json integration.

const AdapterFacadeScript = preload("res://addons/rpg_asset_adapter/adapter_facade.gd")
const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")
const TileSetConverterScript = preload("res://addons/rpg_asset_adapter/converter/tileset_converter.gd")
const AutotileConverterScript = preload("res://addons/rpg_asset_adapter/converter/autotile_converter.gd")
const CharacterConverterScript = preload("res://addons/rpg_asset_adapter/converter/character_converter.gd")
const AnimationConverterScript = preload("res://addons/rpg_asset_adapter/converter/animation_converter.gd")
const MetadataConverterScript = preload("res://addons/rpg_asset_adapter/converter/metadata_converter.gd")


func _init() -> void:
	print("==================================================")
	print("--- RPG ASSET ADAPTER: END-TO-END TEST SUITE ---")
	print("==================================================")

	var success := true
	success = success and _test_scanning()
	success = success and _test_full_conversion()
	success = success and await _test_visual_verification()

	if success:
		print("==================================================")
		print(">>> ALL END-TO-END TESTS PASSED SUCCESSFULLY! <<<")
		print("==================================================")
		quit(0)
	else:
		push_error("!!! END-TO-END TESTS FAILED !!!")
		quit(1)


func _test_scanning() -> bool:
	print("\n[TEST 1] Scanning Victorian 32px assets folder...")
	var test_dir := "res://AssetsSource/External/Victorian"
	var assets := AdapterFacadeScript.scan_directory(test_dir)

	print("  Scanned asset count: %d" % assets.size())
	if assets.is_empty():
		push_error("FAIL: No assets found in %s" % test_dir)
		return false

	var kind_counts: Dictionary = {}
	for info in assets:
		var k_name: String = AssetDetectorScript.AssetKind.keys()[info.kind]
		kind_counts[k_name] = kind_counts.get(k_name, 0) + 1

	print("  Detected asset summary:")
	for k in kind_counts:
		print("    - %s: %d" % [k, kind_counts[k]])

	# Verify essential Victorian assets were recognized
	var expected_keys := ["RPG_A1", "RPG_A2", "RPG_A3", "RPG_A4", "RPG_A5", "RPG_B", "RPG_C", "RPG_D", "RPG_E"]
	for exp_k in expected_keys:
		if not kind_counts.has(exp_k):
			push_error("FAIL: Missing expected asset kind: %s" % exp_k)
			return false

	# Verify tile size inference
	for info in assets:
		if info.kind in [AssetDetectorScript.AssetKind.RPG_A1, AssetDetectorScript.AssetKind.RPG_A2]:
			if info.tile_size != 32:
				push_error("FAIL: Expected tile size 32 for %s, got %d" % [info.path, info.tile_size])
				return false

	print("  PASSED: Scanning recognized all standard asset categories with correct 32px tile size.")
	return true


func _test_full_conversion() -> bool:
	print("\n[TEST 2] Performing full batch conversion on Victorian assets...")
	var test_dir := "res://AssetsSource/External/Victorian"
	var assets := AdapterFacadeScript.scan_directory(test_dir)

	var output_dir := "res://imported_assets/test_victorian"
	var options := {
		"output_dir": output_dir,
		"theme_name": "Victorian",
		"tile_size": 32,
		"fps": 6.0,
		"apply_metadata": true
	}

	var res := AdapterFacadeScript.convert_assets(assets, options)
	print("  Conversion result status: %s" % ("SUCCESS" if res.get("success", false) else "FAILED"))

	if not res.get("success", false):
		push_error("FAIL: Conversion returned failure: %s" % str(res.get("errors", [])))
		return false

	# 1. Verify TileSet resource
	var ts_path: String = res.get("tileset_path", "")
	print("  Generated TileSet path: %s" % ts_path)
	if ts_path == "" or not ResourceLoader.exists(ts_path):
		push_error("FAIL: TileSet resource was not created at %s" % ts_path)
		return false

	var ts: TileSet = ResourceLoader.load(ts_path)
	if ts == null:
		push_error("FAIL: Could not load created TileSet resource.")
		return false

	if ts.tile_size != Vector2i(32, 32):
		push_error("FAIL: TileSet tile_size is %s, expected Vector2i(32, 32)" % str(ts.tile_size))
		return false

	if ts.get_terrain_sets_count() < 2:
		push_error("FAIL: TileSet terrain sets count is %d, expected >= 2" % ts.get_terrain_sets_count())
		return false

	var mode0 := ts.get_terrain_set_mode(0)
	var mode1 := ts.get_terrain_set_mode(1)
	if mode0 != TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES:
		push_error("FAIL: TerrainSet 0 mode is not MATCH_CORNERS_AND_SIDES.")
		return false
	if mode1 != TileSet.TERRAIN_MODE_MATCH_SIDES:
		push_error("FAIL: TerrainSet 1 mode is not MATCH_SIDES.")
		return false

	print("  TileSet sources count: %d" % ts.get_source_count())
	if ts.get_source_count() == 0:
		push_error("FAIL: TileSet has no atlas sources.")
		return false

	# Verify custom data layers
	var expected_layers := [
		"suggested_layer", "upper_layer", "counter", "counter_visual_offset",
		"bush", "ladder", "damage_floor", "pass_down", "pass_up", "variant_group", "variant"
	]
	for l_name in expected_layers:
		if not ts.has_custom_data_layer_by_name(l_name):
			push_error("FAIL: Missing custom data layer: %s" % l_name)
			return false

	print("  PASSED: TileSet validated with TerrainSets, Sources, and Custom Data Layers.")

	# 2. Verify Derived Atlases
	var atlases: Array = res.get("generated_atlases", [])
	print("  Generated derived atlas count: %d" % atlases.size())
	if atlases.is_empty():
		push_error("FAIL: Expected derived atlases for autotiles.")
		return false

	# 3. Verify Generated Scenes
	var scenes: Array = res.get("generated_scenes", [])
	print("  Generated scene count: %d" % scenes.size())
	if scenes.size() > 0:
		var scene_path: String = scenes[0]
		print("  Validating generated scene: %s" % scene_path)
		var packed: PackedScene = ResourceLoader.load(scene_path)
		if packed == null:
			push_error("FAIL: Could not load generated scene %s" % scene_path)
			return false

		var inst := packed.instantiate()
		if inst == null:
			push_error("FAIL: Could not instantiate scene %s" % scene_path)
			return false

		var anim_sprite := inst.get_node_or_null("AnimatedSprite2D")
		var static_body := inst.get_node_or_null("StaticBody2D")
		var trigger_area := inst.get_node_or_null("TriggerArea")

		if anim_sprite == null or static_body == null or trigger_area == null:
			push_error("FAIL: Scene node hierarchy incomplete. Nodes: Sprite=%s, Body=%s, Area=%s" % [
				anim_sprite != null, static_body != null, trigger_area != null
			])
			inst.free()
			return false

		inst.free()
		print("  PASSED: Scene hierarchy (AnimatedSprite2D, StaticBody2D, TriggerArea) validated.")

	# 4. Verify Generated SpriteFrames
	var frames_list: Array = res.get("generated_sprite_frames", [])
	print("  Generated SpriteFrames count: %d" % frames_list.size())
	if frames_list.size() > 0:
		var sf_path: String = frames_list[0]
		var sf: SpriteFrames = ResourceLoader.load(sf_path)
		if sf == null or sf.get_animation_names().is_empty():
			push_error("FAIL: SpriteFrames %s is invalid or has no animations." % sf_path)
			return false
		print("  PASSED: SpriteFrames validated with animations: %s" % str(sf.get_animation_names()))

	print("  PASSED: Full batch conversion verified successfully.")
	return true


func _test_visual_verification() -> bool:
	if DisplayServer.get_name() == "headless":
		print("  SKIPPED: GPU screenshots require running without --headless.")
		return true
	print("\n[TEST 3] Generating visual verification renders...")
	var ts_path := "res://imported_assets/test_victorian/tilesets/Victorian.tres"
	var ts: TileSet = ResourceLoader.load(ts_path)
	if ts == null:
		push_error("FAIL: Could not load TileSet for visual verification: %s" % ts_path)
		return false

	var vp := SubViewport.new()
	vp.size = Vector2i(1200, 800)
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(vp)

	# 1. Paint Floor & Wall Autotiles using TileMapLayer and brush connect
	var tile_layer := TileMapLayer.new()
	tile_layer.tile_set = ts
	vp.add_child(tile_layer)

	var floor_cells: Array[Vector2i] = []
	# 4x4 block
	for x in range(1, 6):
		for y in range(1, 6):
			floor_cells.append(Vector2i(x, y))
	# corridor extending right
	for x in range(6, 11):
		floor_cells.append(Vector2i(x, 3))
	# cross / plus sign
	for x in range(13, 18):
		floor_cells.append(Vector2i(x, 3))
	for y in range(1, 6):
		floor_cells.append(Vector2i(15, y))
	# single isolated tile
	floor_cells.append(Vector2i(20, 3))

	print("  Connecting A2 floor terrain...")
	tile_layer.set_cells_terrain_connect(floor_cells, 0, _terrain_index(ts, 0, "a2_floor_r2_c2"), false)

	var wall_cells: Array[Vector2i] = []
	# hollow box
	for x in range(1, 8):
		wall_cells.append(Vector2i(x, 8))
		wall_cells.append(Vector2i(x, 12))
	for y in range(9, 12):
		wall_cells.append(Vector2i(1, y))
		wall_cells.append(Vector2i(7, y))
	# T-junction & corridor
	for x in range(7, 14):
		wall_cells.append(Vector2i(x, 10))
	for y in range(8, 13):
		wall_cells.append(Vector2i(11, y))
	# isolated single wall
	wall_cells.append(Vector2i(16, 10))

	print("  Connecting A3 wall terrain...")
	tile_layer.set_cells_terrain_connect(wall_cells, 1, _terrain_index(ts, 1, "a3_wall_r1_c0"), false)

	for f in range(5):
		await process_frame

	var img := vp.get_texture().get_image()
	if img == null or img.save_png("res://imported_assets/test_victorian/visual_verification_terrain.png") != OK:
		vp.queue_free()
		return false
	print("  Saved visual_verification_terrain.png")

	tile_layer.queue_free()

	# 2. Render Objects: Doors, Chests, Fountain
	var obj_root := Node2D.new()
	vp.add_child(obj_root)

	var door_scenes := [
		"res://imported_assets/test_victorian/scenes/$!Victorian_V2_Doors_Small_Lit_r0_c0_double.tscn",
		"res://imported_assets/test_victorian/scenes/$!Victorian_V2_Doors_Small_Lit_r0_c0_single.tscn",
		"res://imported_assets/test_victorian/scenes/$!Victorian_V2_Doors_Large_Lit_r0_c0_double.tscn",
		"res://imported_assets/test_victorian/scenes/$!Victorian_V2_Doors_Large_Lit_r0_c0_single.tscn",
	]

	var cur_x := 80
	for d_path in door_scenes:
		if not ResourceLoader.exists(d_path):
			push_error("Missing door scene: %s" % d_path)
			vp.queue_free()
			return false
		if ResourceLoader.exists(d_path):
			var sc: PackedScene = ResourceLoader.load(d_path)
			var inst_closed := sc.instantiate()
			inst_closed.position = Vector2(cur_x, 120)
			obj_root.add_child(inst_closed)
			var spr_c: AnimatedSprite2D = inst_closed.get_node_or_null("AnimatedSprite2D")
			if spr_c:
				spr_c.play("closed")

			var inst_open := sc.instantiate()
			inst_open.position = Vector2(cur_x, 300)
			obj_root.add_child(inst_open)
			var spr_o: AnimatedSprite2D = inst_open.get_node_or_null("AnimatedSprite2D")
			if spr_o:
				spr_o.play("opened")

			cur_x += 140

	var chest_scenes := [
		"res://imported_assets/test_victorian/scenes/w_1_h_1_v_4_2_Treasure_Chest_Animations_Front_Back_Lit_0.tscn",
		"res://imported_assets/test_victorian/scenes/w_1_h_1_v_4_2_Treasure_Chest_Animations_Front_Back_Lit_1.tscn",
		"res://imported_assets/test_victorian/scenes/w_1_h_2_v_4_2_Treasure_Chest_Animations_Left_Right_Lit_0.tscn",
	]
	var chest_x := 80
	for c_path in chest_scenes:
		if not ResourceLoader.exists(c_path):
			push_error("Missing chest scene: %s" % c_path)
			vp.queue_free()
			return false
		if ResourceLoader.exists(c_path):
			var sc: PackedScene = ResourceLoader.load(c_path)
			var inst_c := sc.instantiate()
			inst_c.position = Vector2(chest_x, 480)
			obj_root.add_child(inst_c)
			var spr: AnimatedSprite2D = inst_c.get_node_or_null("AnimatedSprite2D")
			if spr:
				if c_path.contains("Left_Right") and spr.sprite_frames.get_frame_texture("closed", 0).get_size() != Vector2(32, 64):
					push_error("Side-view chest must preserve its 32x64 frame.")
					vp.queue_free()
					return false
				spr.play("closed")

			var inst_o := sc.instantiate()
			inst_o.position = Vector2(chest_x + 60, 480)
			obj_root.add_child(inst_o)
			var spr2: AnimatedSprite2D = inst_o.get_node_or_null("AnimatedSprite2D")
			if spr2:
				spr2.play("opened")

			chest_x += 160

	var f_path := "res://imported_assets/test_victorian/scenes/w_3_h_3_h_3_2_fountainwater_0.tscn"
	if not ResourceLoader.exists(f_path):
		push_error("Missing fountain scene: %s" % f_path)
		vp.queue_free()
		return false
	if ResourceLoader.exists(f_path):
		var f_sc: PackedScene = ResourceLoader.load(f_path)
		var f_inst := f_sc.instantiate()
		f_inst.position = Vector2(720, 480)
		obj_root.add_child(f_inst)
		var spr: AnimatedSprite2D = f_inst.get_node_or_null("AnimatedSprite2D")
		if spr:
			spr.play("loop")

	for f in range(5):
		await process_frame

	var obj_img := vp.get_texture().get_image()
	if obj_img == null or obj_img.save_png("res://imported_assets/test_victorian/visual_verification_objects.png") != OK:
		vp.queue_free()
		return false
	print("  Saved visual_verification_objects.png")

	obj_root.queue_free()
	vp.queue_free()
	print("  PASSED: Visual verification renders generated.")
	return true


func _terrain_index(tileset: TileSet, terrain_set: int, terrain_name: String) -> int:
	for i in tileset.get_terrains_count(terrain_set):
		if tileset.get_terrain_name(terrain_set, i) == terrain_name:
			return i
	assert(false, "Missing terrain: %s" % terrain_name)
	return -1
