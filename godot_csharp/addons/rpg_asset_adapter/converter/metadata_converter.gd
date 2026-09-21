@tool
class_name MetadataConverter
extends RefCounted

## Parses RPG Maker Tilesets.json metadata and decodes 16-bit tile flags.
## Applies physics collision polygons, directional passability, and custom metadata
## (upper_layer, ladder, bush, counter, damage_floor, terrain_tag) to Godot 4 TileSets.

const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")

# RPG Maker Tileset Slot Indices:
# tilesetNames = [A1, A2, A3, A4, A5, B, C, D, E]
const SLOT_A1 := 0
const SLOT_A2 := 1
const SLOT_A3 := 2
const SLOT_A4 := 3
const SLOT_A5 := 4
const SLOT_B := 5
const SLOT_C := 6
const SLOT_D := 7
const SLOT_E := 8

# Base tile IDs in the 8192-element flags array
const BASE_ID_B := 0         # 0..255 (16x16)
const BASE_ID_C := 256       # 256..511 (16x16)
const BASE_ID_D := 512       # 512..767 (16x16)
const BASE_ID_E := 768       # 768..1023 (16x16)
const BASE_ID_A5 := 1536     # 1536..1663 (8x16)
const BASE_ID_A1 := 2048     # 2048..2815
const BASE_ID_A2 := 2816     # 2816..4351
const BASE_ID_A3 := 4352     # 4352..5887
const BASE_ID_A4 := 5888     # 5888..8191


## Ensures required physics layer and custom data layers exist on the TileSet.
static func ensure_tileset_layers(tileset: TileSet) -> void:
	if tileset == null:
		return

	if tileset.get_physics_layers_count() == 0:
		tileset.add_physics_layer()

	var custom_layers: Dictionary = {
		"upper_layer": TYPE_BOOL,
		"ladder": TYPE_BOOL,
		"bush": TYPE_BOOL,
		"bush_depth": TYPE_FLOAT,
		"counter": TYPE_BOOL,
		"damage_floor": TYPE_BOOL,
		"terrain_tag": TYPE_INT,
		"pass_down": TYPE_BOOL,
		"pass_left": TYPE_BOOL,
		"pass_right": TYPE_BOOL,
		"pass_up": TYPE_BOOL,
	}

	for layer_name in custom_layers:
		if not tileset.has_custom_data_layer_by_name(layer_name):
			tileset.add_custom_data_layer()
			var idx := tileset.get_custom_data_layers_count() - 1
			tileset.set_custom_data_layer_name(idx, layer_name)
			tileset.set_custom_data_layer_type(idx, custom_layers[layer_name])


## Decodes a 16-bit tile flag into individual flags and passability attributes.
static func decode_tile_flags(flag: int, tile_size: int = 48) -> Dictionary:
	var blocked_down := (flag & 0x01) != 0
	var blocked_left := (flag & 0x02) != 0
	var blocked_right := (flag & 0x04) != 0
	var blocked_up := (flag & 0x08) != 0
	var star := (flag & 0x10) != 0
	var ladder := (flag & 0x20) != 0
	var bush := (flag & 0x40) != 0
	var counter := (flag & 0x80) != 0
	var damage := (flag & 0x100) != 0
	var terrain_tag := (flag >> 12) & 0x0F

	var all_blocked := blocked_down and blocked_left and blocked_right and blocked_up
	var all_open := not blocked_down and not blocked_left and not blocked_right and not blocked_up
	var partially_blocked := not all_blocked and not all_open

	return {
		"flag": flag,
		"blocked_down": blocked_down,
		"blocked_left": blocked_left,
		"blocked_right": blocked_right,
		"blocked_up": blocked_up,
		"all_blocked": all_blocked,
		"all_open": all_open,
		"partially_blocked": partially_blocked,
		"star": star,
		"ladder": ladder,
		"bush": bush,
		"bush_depth": float(tile_size) / 4.0 if bush else 0.0,
		"counter": counter,
		"damage_floor": damage,
		"terrain_tag": terrain_tag,
		"pass_down": not blocked_down,
		"pass_left": not blocked_left,
		"pass_right": not blocked_right,
		"pass_up": not blocked_up
	}


## Applies decoded flag data to a TileData instance.
## Non-star tiles blocked in all directions get a square collision on physics layer 0.
## If all 4 directions open: no collision polygon.
## Always writes directional flags; the game layer handles partial blocking and star semantics.
static func apply_flag_to_tile_data(tile_data: TileData, flag: int, tile_size: int = 48) -> Dictionary:
	var decoded := decode_tile_flags(flag, tile_size)

	# Write false/zero values too, so reapplying metadata clears previous flags.
	tile_data.set_custom_data("upper_layer", decoded["star"])
	for key in ["ladder", "bush", "bush_depth", "counter", "damage_floor", "terrain_tag",
		"pass_down", "pass_left", "pass_right", "pass_up"]:
		tile_data.set_custom_data(key, decoded[key])

	# Clean up any existing collision polygons on physics layer 0
	while tile_data.get_collision_polygons_count(0) > 0:
		tile_data.remove_collision_polygon(0, 0)

	if decoded["all_blocked"] and not decoded["star"]:
		var half := float(tile_size) / 2.0
		var poly := PackedVector2Array([
			Vector2(-half, -half),
			Vector2(half, -half),
			Vector2(half, half),
			Vector2(-half, half)
		])
		tile_data.add_collision_polygon(0)
		tile_data.set_collision_polygon_points(0, 0, poly)

	return decoded


## Reads and parses Tilesets.json from a file path or JSON string.
## Returns an Array of tileset dictionaries.
static func parse_tilesets_json(json_or_path: Variant) -> Array:
	var content: String = ""
	if json_or_path is String:
		var s: String = json_or_path
		var global_path := ProjectSettings.globalize_path(s) if s.begins_with("res://") else s
		if FileAccess.file_exists(global_path):
			content = FileAccess.get_file_as_string(global_path)
		elif FileAccess.file_exists(s):
			content = FileAccess.get_file_as_string(s)
		else:
			content = s
	elif json_or_path is Array:
		return json_or_path
	else:
		push_error("MetadataConverter: Unsupported input type for parse_tilesets_json.")
		return []

	if content.is_empty():
		return []

	var json := JSON.new()
	var err := json.parse(content)
	if err != OK:
		push_error("MetadataConverter: JSON parse error: %s at line %d" % [json.get_error_message(), json.get_error_line()])
		return []

	if json.data is Array:
		return json.data
	elif json.data is Dictionary:
		return [json.data]

	return []


## Matches the tileset object whose tilesetNames matches the filenames in the current import set.
## Returns an empty dictionary when no filenames match.
static func match_tileset(tilesets_data: Array, import_filenames: Array) -> Dictionary:
	if tilesets_data.is_empty():
		return {}

	# Normalize import filenames to lowercase basenames
	var clean_import: Array[String] = []
	for item in import_filenames:
		var name_str := str(item).get_file().get_basename().to_lower()
		clean_import.append(name_str)

	var best_match: Dictionary = {}
	var highest_score := 0

	for item in tilesets_data:
		if not (item is Dictionary):
			continue
		var t_dict: Dictionary = item
		var names: Array = t_dict.get("tilesetNames", [])
		var score := 0
		for t_name in names:
			var s := str(t_name).get_file().get_basename().to_lower()
			if s.is_empty():
				continue
			for imp in clean_import:
				if imp == s:
					score += 1
					break
		if score > highest_score:
			highest_score = score
			best_match = t_dict

	if highest_score > 0:
		return best_match

	return {}


## Calculates the RPG Maker tile ID for a given slot index and tile coordinates.
static func get_tile_id(slot_index: int, col: int, row: int) -> int:
	match slot_index:
		SLOT_B:
			return BASE_ID_B + (col / 8) * 128 + row * 8 + col % 8
		SLOT_C:
			return BASE_ID_C + (col / 8) * 128 + row * 8 + col % 8
		SLOT_D:
			return BASE_ID_D + (col / 8) * 128 + row * 8 + col % 8
		SLOT_E:
			return BASE_ID_E + (col / 8) * 128 + row * 8 + col % 8
		SLOT_A5:
			return BASE_ID_A5 + row * 8 + col
		_:
			# Autotiles require the explicit mapping from the reconstruction step.
			return -1


## Maps an AssetKind to its corresponding RPG Maker slot index.
static func get_slot_for_asset_kind(kind: AssetDetectorScript.AssetKind) -> int:
	match kind:
		AssetDetectorScript.AssetKind.RPG_A1:
			return SLOT_A1
		AssetDetectorScript.AssetKind.RPG_A2:
			return SLOT_A2
		AssetDetectorScript.AssetKind.RPG_A3:
			return SLOT_A3
		AssetDetectorScript.AssetKind.RPG_A4:
			return SLOT_A4
		AssetDetectorScript.AssetKind.RPG_A5:
			return SLOT_A5
		AssetDetectorScript.AssetKind.RPG_B:
			return SLOT_B
		AssetDetectorScript.AssetKind.RPG_C:
			return SLOT_C
		AssetDetectorScript.AssetKind.RPG_D:
			return SLOT_D
		AssetDetectorScript.AssetKind.RPG_E:
			return SLOT_E
		_:
			return -1


## Applies tile flags to all tiles in a TileSetAtlasSource.
static func apply_flags_to_source(
	source: TileSetAtlasSource,
	flags: Array,
	slot_index: int,
	tile_size: int = 48
) -> int:
	var applied_count := 0
	var tile_ids: Dictionary = source.get_meta("rpg_tile_ids", {})
	var tiles_count := source.get_tiles_count()
	for i in range(tiles_count):
		var coords := source.get_tile_id(i)
		var tile_id: int = tile_ids.get(coords, get_tile_id(slot_index, coords.x, coords.y))
		if tile_id >= 0 and tile_id < flags.size():
			var flag := int(flags[tile_id])
			var tile_data := source.get_tile_data(coords, 0)
			if tile_data != null:
				apply_flag_to_tile_data(tile_data, flag, tile_size)
				applied_count += 1
	return applied_count


## Applies metadata from a matched tileset dictionary to an entire TileSet.
## Uses source_slot_mapping or metadata stored on atlas sources.
static func apply_metadata_to_tileset(
	tileset: TileSet,
	tileset_info: Dictionary,
	tile_size: int = 48,
	source_slot_mapping: Dictionary = {}
) -> int:
	ensure_tileset_layers(tileset)
	var flags: Array = tileset_info.get("flags", [])
	if flags.is_empty():
		return 0

	var applied_total := 0
	for source_idx in range(tileset.get_source_count()):
		var source_id := tileset.get_source_id(source_idx)
		var source = tileset.get_source(source_id)
		if source is TileSetAtlasSource:
			var slot := -1
			if source_slot_mapping.has(source_id):
				slot = int(source_slot_mapping[source_id])
			elif source.has_meta("slot_index"):
				slot = int(source.get_meta("slot_index"))
			elif source.has_meta("asset_kind"):
				slot = get_slot_for_asset_kind(source.get_meta("asset_kind"))

			if slot >= 0 and source.has_meta("source_filename"):
				var names: Array = tileset_info.get("tilesetNames", [])
				if slot >= names.size() or str(names[slot]).to_lower() != str(source.get_meta("source_filename")).to_lower():
					continue

			if slot >= 0:
				applied_total += apply_flags_to_source(source, flags, slot, tile_size)

	return applied_total
