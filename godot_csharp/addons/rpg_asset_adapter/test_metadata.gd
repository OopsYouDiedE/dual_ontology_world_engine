extends SceneTree

const Metadata = preload("res://addons/rpg_asset_adapter/converter/metadata_converter.gd")
const Tiles = preload("res://addons/rpg_asset_adapter/converter/tileset_converter.gd")
const Autotiles = preload("res://addons/rpg_asset_adapter/converter/autotile_converter.gd")
const Detector = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")

var failures: int = 0

func check(condition: bool, message: String) -> void:
	if not condition:
		push_error(message)
		failures += 1

func _init() -> void:
	check(Metadata.get_tile_id(Metadata.SLOT_B, 8, 0) == 128, "B right half starts at 128")
	check(Metadata.get_tile_id(Metadata.SLOT_C, 7, 15) == 383, "C left half ends at 383")
	check(Metadata.get_tile_id(Metadata.SLOT_E, 15, 15) == 1023, "E ends at 1023")
	check(Metadata.get_tile_id(Metadata.SLOT_A5, 7, 15) == 1663, "A5 ends at 1663")
	check(Metadata.match_tileset([null, {"tilesetNames": ["Town_A2"]}], ["Other_A2.png"]).is_empty(), "Unrelated metadata must not match")
	check(Metadata.match_tileset([{"tilesetNames": ["Town_A2"]}], ["Town_A2_extra.png"]).is_empty(), "Substring matches must not apply metadata")
	var ts := Tiles.create_tileset(16)
	Metadata.ensure_tileset_layers(ts)
	var image := Image.create(256, 256, false, Image.FORMAT_RGBA8)
	var normal := Tiles.add_normal_atlas_source(ts, image, Detector.AssetKind.RPG_B, 16)
	var tile := normal.get_tile_data(Vector2i(8, 0), 0)
	Metadata.apply_flag_to_tile_data(tile, 0x10ff, 16)
	check(tile.get_collision_polygons_count(0) == 0, "Star tiles must not create blocking polygons")
	Metadata.apply_flag_to_tile_data(tile, 15, 16)
	check(tile.get_collision_polygons_count(0) == 1, "Blocked tiles need one polygon")
	Metadata.apply_flag_to_tile_data(tile, 0, 16)
	check(tile.get_collision_polygons_count(0) == 0, "Open tiles clear collision")
	for key in ["upper_layer", "ladder", "bush", "counter", "damage_floor"]:
		check(not tile.get_custom_data(key), "Flag must reset: " + key)
	check(tile.get_custom_data("terrain_tag") == 0 and tile.get_custom_data("bush_depth") == 0.0, "Numeric metadata must reset")
	for key in ["pass_down", "pass_left", "pass_right", "pass_up"]:
		check(tile.get_custom_data(key), "Open tile must allow: " + key)
	var flags: Array = []
	flags.resize(8192)
	flags.fill(0)
	flags[128] = 1
	Metadata.apply_flags_to_source(normal, flags, Metadata.SLOT_B, 16)
	check(not tile.get_custom_data("pass_down") and tile.get_custom_data("pass_up"), "Normal atlas must use original flag index")
	normal.set_meta("slot_index", Metadata.SLOT_B)
	normal.set_meta("source_filename", "Town_B")
	check(Metadata.apply_metadata_to_tileset(ts, {"tilesetNames": ["", "", "", "", "", "Other_B"], "flags": flags}, 16) == 0, "Another B sheet must not receive matched flags")
	check(Metadata.apply_metadata_to_tileset(ts, {"tilesetNames": ["", "", "", "", "", "Town_B"], "flags": flags}, 16) == 255, "Matching B sheet receives flags")
	check(Metadata.get_slot_for_asset_kind(Detector.AssetKind.KR_NON_RM_A2) == -1, "Non-RM layouts have no standard metadata slot")
	var cases := [
		[Autotiles.generate_a1_derived_atlas(image, 16), Metadata.SLOT_A1, Vector2i(0, 0), 2048],
		[Autotiles.generate_a2_derived_atlas(image, 16), Metadata.SLOT_A2, Vector2i(6, 8), 3248],
		[Autotiles.generate_a3_derived_atlas(image, 16), Metadata.SLOT_A3, Vector2i(4, 4), 4784],
		[Autotiles.generate_a4_derived_atlas(image, 16), Metadata.SLOT_A4, Vector2i(4, 28), 7088],
	]
	for entry in cases:
		var source := Tiles.add_autotile_atlas_source(ts, entry[0]["image"], entry[0], 16)
		source.set_meta("slot_index", entry[1])
		var ids: Dictionary = source.get_meta("rpg_tile_ids")
		check(ids.get(entry[2], -1) == entry[3], "Autotile ID mismatch for slot %d" % entry[1])
		flags[entry[3]] = 15
		Metadata.apply_flags_to_source(source, flags, entry[1], 16)
		check(source.get_tile_data(entry[2], 0).get_collision_polygons_count(0) == 1, "Autotile flags must follow reconstruction mapping")
	var save_path := "res://.adapter_metadata_test_%d.res" % Time.get_ticks_usec()
	check(ResourceSaver.save(ts, save_path) == OK, "TileSet save")
	var loaded := ResourceLoader.load(save_path, "", ResourceLoader.CACHE_MODE_IGNORE) as TileSet
	check(loaded != null, "TileSet reload")
	if loaded != null:
		var source := loaded.get_source(loaded.get_source_id(4)) as TileSetAtlasSource
		check(source.get_meta("rpg_tile_ids").get(Vector2i(4, 28), -1) == 7088, "Mapping must survive resource reload")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(save_path))
	print("Metadata regression checks: %d failures" % failures)
	quit(0 if failures == 0 else 1)
