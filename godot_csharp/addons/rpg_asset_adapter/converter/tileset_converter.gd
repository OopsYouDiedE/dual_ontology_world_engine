@tool
class_name TileSetConverter
extends RefCounted

## Converts reconstructed autotiles and standard tile grids into Godot 4 TileSets.
## Manages TerrainSets (0: Corners & Sides, 1: Sides), tile animations, and 21 standard custom data layers.

const AutotileTables = preload("res://addons/rpg_asset_adapter/data/autotile_tables.gd")
const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")
const KrProfileScript = preload("res://addons/rpg_asset_adapter/profiles/kr_profile.gd")

const CUSTOM_DATA_LAYERS: Array[Dictionary] = [
	{"name": "suggested_layer", "type": TYPE_STRING},
	{"name": "upper_layer", "type": TYPE_BOOL},
	{"name": "rpg_object_alignment", "type": TYPE_BOOL},
	{"name": "rpg_type", "type": TYPE_STRING},
	{"name": "auto_shadow_right", "type": TYPE_BOOL},
	{"name": "rpg_a2_group", "type": TYPE_INT},
	{"name": "rpg_a2_overlay", "type": TYPE_BOOL},
	{"name": "counter", "type": TYPE_BOOL},
	{"name": "counter_visual_offset", "type": TYPE_FLOAT},
	{"name": "bush", "type": TYPE_BOOL},
	{"name": "bush_depth", "type": TYPE_FLOAT},
	{"name": "ladder", "type": TYPE_BOOL},
	{"name": "damage_floor", "type": TYPE_BOOL},
	{"name": "terrain_tag", "type": TYPE_INT},
	{"name": "pass_down", "type": TYPE_BOOL},
	{"name": "pass_left", "type": TYPE_BOOL},
	{"name": "pass_right", "type": TYPE_BOOL},
	{"name": "pass_up", "type": TYPE_BOOL},
	{"name": "variant_group", "type": TYPE_STRING},
	{"name": "variant", "type": TYPE_STRING},
	{"name": "requires_animation_profile", "type": TYPE_BOOL},
]


## Creates a new Godot 4 TileSet configured with two TerrainSets and 21 custom data layers.
static func create_tileset(tile_size: int = 48) -> TileSet:
	if tile_size <= 0:
		tile_size = 48

	var ts := TileSet.new()
	ts.tile_size = Vector2i(tile_size, tile_size)

	# TerrainSet 0: MATCH_CORNERS_AND_SIDES (A1 water, A2 floor, A4 wall top)
	ts.add_terrain_set(0)
	ts.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)

	# TerrainSet 1: MATCH_SIDES (A1 waterfall, A3 wall/roof, A4 wall side)
	ts.add_terrain_set(1)
	ts.set_terrain_set_mode(1, TileSet.TERRAIN_MODE_MATCH_SIDES)

	# Register all 21 custom data layers
	for i in range(CUSTOM_DATA_LAYERS.size()):
		ts.add_custom_data_layer(i)
		ts.set_custom_data_layer_name(i, CUSTOM_DATA_LAYERS[i]["name"])
		ts.set_custom_data_layer_type(i, CUSTOM_DATA_LAYERS[i]["type"])

	return ts


## Ensures TerrainSets and all required custom data layers exist on an existing TileSet.
static func ensure_tileset_layers(tileset: TileSet) -> void:
	if tileset == null:
		return

	# Ensure TerrainSet 0
	if tileset.get_terrain_sets_count() < 1:
		tileset.add_terrain_set(0)
		tileset.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
	else:
		tileset.set_terrain_set_mode(0, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)

	# Ensure TerrainSet 1
	if tileset.get_terrain_sets_count() < 2:
		tileset.add_terrain_set(1)
		tileset.set_terrain_set_mode(1, TileSet.TERRAIN_MODE_MATCH_SIDES)
	else:
		tileset.set_terrain_set_mode(1, TileSet.TERRAIN_MODE_MATCH_SIDES)

	# Ensure all 21 custom data layers
	for def in CUSTOM_DATA_LAYERS:
		var layer_name: String = def["name"]
		var layer_type: int = def["type"]
		if not tileset.has_custom_data_layer_by_name(layer_name):
			tileset.add_custom_data_layer()
			var idx := tileset.get_custom_data_layers_count() - 1
			tileset.set_custom_data_layer_name(idx, layer_name)
			tileset.set_custom_data_layer_type(idx, layer_type)


## Adds a derived autotile atlas source to a TileSet, registers terrain(s), creates tiles,
## configures peering bits, sets up tile animations, and assigns custom data properties.
static func add_autotile_atlas_source(
	tileset: TileSet,
	atlas_img: Image,
	autotile_meta: Dictionary,
	tile_size: int = 48,
	tex_res_path: String = ""
) -> TileSetAtlasSource:
	if tileset == null or atlas_img == null:
		return null

	if tile_size <= 0:
		tile_size = 48

	ensure_tileset_layers(tileset)

	# Ensure image is decompressed if necessary
	if atlas_img.is_compressed():
		atlas_img = atlas_img.duplicate()
		atlas_img.decompress()

	var source := TileSetAtlasSource.new()
	var tex: Texture2D = null
	if tex_res_path != "" and ResourceLoader.exists(tex_res_path):
		tex = ResourceLoader.load(tex_res_path)
	if tex == null:
		var itex := ImageTexture.create_from_image(atlas_img)
		tex = itex
	source.texture = tex
	source.texture_region_size = Vector2i(tile_size, tile_size)
	tileset.add_source(source)

	var autotile_list: Array = []
	if autotile_meta.has("autotiles"):
		autotile_list = autotile_meta["autotiles"]
	elif autotile_meta.has("tiles"):
		autotile_list = [autotile_meta]

	var rpg_tile_ids: Dictionary = {}
	for at in autotile_list:
		var t_set: int = at.get("terrain_set", 0)
		var t_idx: int = -1

		if t_set >= 0:
			# Verify terrain_set bounds
			while tileset.get_terrain_sets_count() <= t_set:
				tileset.add_terrain_set()

			# Add a new terrain entry to this terrain_set
			tileset.add_terrain(t_set)
			t_idx = tileset.get_terrains_count(t_set) - 1

			var at_name: String = at.get("name", "autotile_%d_%d" % [t_set, t_idx])
			tileset.set_terrain_name(t_set, t_idx, at_name)

			# Determine representative color for terrain
			var terrain_color := Color.from_hsv(
				fmod(float(t_idx * 37 + t_set * 73) / 100.0, 1.0),
				0.7,
				0.85
			)
			var tiles_arr_sample: Array = at.get("tiles", [])
			if tiles_arr_sample.size() > 0:
				var first_tile_coords: Vector2i = tiles_arr_sample[0]["atlas_coords"]
				var sample_px := first_tile_coords.x * tile_size + tile_size / 2
				var sample_py := first_tile_coords.y * tile_size + tile_size / 2
				if sample_px < atlas_img.get_width() and sample_py < atlas_img.get_height():
					var c := atlas_img.get_pixel(sample_px, sample_py)
					if c.a > 0.1:
						c.a = 1.0
						terrain_color = c
			tileset.set_terrain_color(t_set, t_idx, terrain_color)

		var tiles_arr: Array = at.get("tiles", [])
		# Animation settings
		var anim_frames: int = at.get("animation_frames_count", 1)
		var anim_cols: int = at.get("animation_columns", anim_frames)
		var anim_dur: float = at.get("animation_frame_duration", 0.5)

		var at_cdata: Dictionary = at.get("custom_data", {})

		for ti in tiles_arr:
			var coords: Vector2i = ti["atlas_coords"]
			source.create_tile(coords)
			if at.has("rpg_tile_base"):
				rpg_tile_ids[coords] = int(at["rpg_tile_base"]) + int(ti["shape_index"])

			if anim_frames > 1:
				source.set_tile_animation_columns(coords, anim_cols)
				source.set_tile_animation_frames_count(coords, anim_frames)
				for f in range(anim_frames):
					source.set_tile_animation_frame_duration(coords, f, anim_dur)

			var tile_data := source.get_tile_data(coords, 0)
			if tile_data != null:
				if t_set >= 0 and t_idx >= 0:
					tile_data.terrain_set = t_set
					tile_data.terrain = t_idx

					for bit in ti.get("peering_bits", []):
						tile_data.set_terrain_peering_bit(bit, t_idx)

				# Apply autotile-level custom data
				for key in at_cdata:
					if tileset.has_custom_data_layer_by_name(key):
						tile_data.set_custom_data(key, at_cdata[key])

				# Apply tile-specific custom data if present
				var ti_cdata: Dictionary = ti.get("custom_data", {})
				for key in ti_cdata:
					if tileset.has_custom_data_layer_by_name(key):
						tile_data.set_custom_data(key, ti_cdata[key])

	source.set_meta("rpg_tile_ids", rpg_tile_ids)
	return source


## Adds a normal (non-autotile) atlas source with regular T×T grid slicing.
static func add_normal_atlas_source(
	tileset: TileSet,
	img_or_tex: Variant,
	kind: AssetDetectorScript.AssetKind,
	tile_size: int = 48,
	extra: Dictionary = {},
	res_path: String = ""
) -> TileSetAtlasSource:
	if tileset == null or img_or_tex == null:
		return null

	if tile_size <= 0:
		tile_size = 48

	ensure_tileset_layers(tileset)

	var tex: Texture2D = null
	var img: Image = null

	if img_or_tex is Texture2D:
		tex = img_or_tex
		img = tex.get_image()
	elif res_path != "" and ResourceLoader.exists(res_path):
		tex = ResourceLoader.load(res_path)
		if tex != null:
			img = tex.get_image()

	if img == null and img_or_tex is Image:
		img = img_or_tex

	if img == null:
		return null

	if img.is_compressed():
		img = img.duplicate()
		img.decompress()

	if tex == null:
		var itex := ImageTexture.create_from_image(img)
		tex = itex

	var source := TileSetAtlasSource.new()
	source.texture = tex
	source.texture_region_size = Vector2i(tile_size, tile_size)
	tileset.add_source(source)

	var cols := img.get_width() / tile_size
	var rows := img.get_height() / tile_size

	var is_upper_sheet := kind in [
		AssetDetectorScript.AssetKind.RPG_B,
		AssetDetectorScript.AssetKind.RPG_C,
		AssetDetectorScript.AssetKind.RPG_D,
		AssetDetectorScript.AssetKind.RPG_E
	]

	var variant_group: String = extra.get("variant_group", "")
	var variant: String = extra.get("variant", "")

	for y in range(rows):
		for x in range(cols):
			var pos := Vector2i(x, y)

			# For B: Vector2i(0, 0) MUST NOT be created (keep empty per spec)
			if kind == AssetDetectorScript.AssetKind.RPG_B and pos == Vector2i.ZERO:
				continue

			source.create_tile(pos)
			var tile_data := source.get_tile_data(pos, 0)
			if tile_data == null:
				continue

			# Upper layer sheets (B, C, D, E)
			if is_upper_sheet:
				if tileset.has_custom_data_layer_by_name("suggested_layer"):
					tile_data.set_custom_data("suggested_layer", "upper")
				if tileset.has_custom_data_layer_by_name("upper_layer"):
					tile_data.set_custom_data("upper_layer", true)

			# KR F extension variant metadata
			if kind == AssetDetectorScript.AssetKind.KR_F_EXTENSION:
				if variant_group != "" and tileset.has_custom_data_layer_by_name("variant_group"):
					tile_data.set_custom_data("variant_group", variant_group)
				if variant != "" and tileset.has_custom_data_layer_by_name("variant"):
					tile_data.set_custom_data("variant", variant)

			# Apply any other custom data passed in extra
			for key in extra:
				if key not in ["variant_group", "variant", "path", "suggested_layer", "upper_layer"]:
					if tileset.has_custom_data_layer_by_name(key):
						tile_data.set_custom_data(key, extra[key])

	return source
