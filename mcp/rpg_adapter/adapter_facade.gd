@tool
class_name AdapterFacade
extends RefCounted

## High-level facade for the RPG Asset Adapter plugin.
## Orchestrates directory scanning, profile selection, autotile reconstruction,
## TileSet creation, character/object slicing, scene generation, and Tilesets.json parsing.

const AutotileTables = preload("res://addons/rpg_asset_adapter/data/autotile_tables.gd")
const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")
const BaseProfileScript = preload("res://addons/rpg_asset_adapter/profiles/base_profile.gd")
const RpgMakerProfileScript = preload("res://addons/rpg_asset_adapter/profiles/rpg_maker_profile.gd")
const KrProfileScript = preload("res://addons/rpg_asset_adapter/profiles/kr_profile.gd")
const AutotileConverterScript = preload("res://addons/rpg_asset_adapter/converter/autotile_converter.gd")
const TileSetConverterScript = preload("res://addons/rpg_asset_adapter/converter/tileset_converter.gd")
const CharacterConverterScript = preload("res://addons/rpg_asset_adapter/converter/character_converter.gd")
const AnimationConverterScript = preload("res://addons/rpg_asset_adapter/converter/animation_converter.gd")
const MetadataConverterScript = preload("res://addons/rpg_asset_adapter/converter/metadata_converter.gd")
const RPGAdapterConfigScript = preload("res://addons/rpg_asset_adapter/rpg_adapter_config.gd")
const SourceLoader = preload("res://addons/rpg_asset_adapter/source_loader.gd")


## Executes batch conversion using an RPGAdapterConfig resource (.tres).
static func run_with_config(config: Resource) -> Dictionary:
	if config == null:
		push_error("AdapterFacade: Config is null.")
		return {"success": false, "errors": ["Config is null"]}

	var scanned := scan_directory(config.source_dir, AssetDetectorScript.DetectMode.AUTO, config.tile_size)
	var filtered_assets := []
	for info in scanned:
		if config.ignore_non_rm:
			if info.kind in [
				AssetDetectorScript.AssetKind.KR_NON_RM_A1,
				AssetDetectorScript.AssetKind.KR_NON_RM_A2,
				AssetDetectorScript.AssetKind.KR_NON_RM_A3,
				AssetDetectorScript.AssetKind.KR_NON_RM_A4
			]:
				continue
		filtered_assets.append(info)

	var options := {
		"output_dir": config.output_dir,
		"theme_name": config.theme_name,
		"tile_size": config.tile_size,
		"fps": config.fps,
		"apply_metadata": config.apply_metadata,
		"ignore_non_rm": config.ignore_non_rm
	}
	return convert_assets(filtered_assets, options)


## Scans a directory and returns an Array[AssetInfo] representing detected assets.
static func scan_directory(
	dir_path: String,
	mode: AssetDetectorScript.DetectMode = AssetDetectorScript.DetectMode.AUTO,
	manual_tile_size: int = 0
) -> Array:
	var results: Array = []
	var global_dir := ProjectSettings.globalize_path(dir_path) if dir_path.begins_with("res://") else dir_path

	if not DirAccess.dir_exists_absolute(global_dir):
		push_error("AdapterFacade: Directory does not exist: %s" % global_dir)
		return results

	_scan_dir_recursive(global_dir, results, mode, manual_tile_size)
	return results


static func _scan_dir_recursive(
	current_dir: String,
	out_results: Array,
	mode: AssetDetectorScript.DetectMode,
	manual_tile_size: int
) -> void:
	var dir := DirAccess.open(current_dir)
	if dir == null:
		return

	dir.list_dir_begin()
	var file_name := dir.get_next()

	while file_name != "":
		if file_name.begins_with("."):
			file_name = dir.get_next()
			continue

		var full_path := current_dir.path_join(file_name)
		if dir.current_is_dir():
			_scan_dir_recursive(full_path, out_results, mode, manual_tile_size)
		else:
			var ext := file_name.get_extension().to_lower()
			if ext == "png" or ext == "json":
				var info := AssetDetectorScript.detect_asset(full_path, mode, manual_tile_size)
				if info.kind != AssetDetectorScript.AssetKind.UNKNOWN:
					out_results.append(info)

		file_name = dir.get_next()
	dir.list_dir_end()


## Automatically determines the appropriate Profile for the detected asset list.
static func select_profile_for_assets(assets: Array) -> BaseProfileScript:
	for info in assets:
		var k: int = info.kind
		if k in [
			AssetDetectorScript.AssetKind.KR_NON_RM_A1,
			AssetDetectorScript.AssetKind.KR_NON_RM_A2,
			AssetDetectorScript.AssetKind.KR_NON_RM_A3,
			AssetDetectorScript.AssetKind.KR_NON_RM_A4,
			AssetDetectorScript.AssetKind.KR_F_EXTENSION
		]:
			return KrProfileScript.new()

	return RpgMakerProfileScript.new()


## Converts a set of scanned AssetInfo items into Godot resources.
## options = {
##   "output_dir": "res://imported_assets/Victorian",
##   "theme_name": "Victorian",
##   "tile_size": 48, # 0 for auto
##   "profile": BaseProfile (optional),
##   "fps": 6.0,
##   "apply_metadata": true
## }
static func convert_assets(assets: Array, options: Dictionary = {}) -> Dictionary:
	var output_dir: String = options.get("output_dir", "res://ShareAsset/External/Victorian")
	var theme_name: String = options.get("theme_name", "Theme")
	var tile_size_opt: int = options.get("tile_size", 0)
	var fps: float = options.get("fps", 6.0)
	var apply_meta: bool = options.get("apply_metadata", true)
	var ignore_non_rm: bool = options.get("ignore_non_rm", false)

	var profile: BaseProfileScript = options.get("profile", null)
	if profile == null:
		profile = select_profile_for_assets(assets)

	# Globalize output directories
	var global_output := ProjectSettings.globalize_path(output_dir) if output_dir.begins_with("res://") else output_dir
	var atlases_dir := global_output.path_join("atlases")
	var tilesets_dir := global_output.path_join("tilesets")
	var anims_dir := global_output.path_join("animations")
	var chars_dir := global_output.path_join("characters")
	var scenes_dir := global_output.path_join("scenes")

	DirAccess.make_dir_recursive_absolute(atlases_dir)
	DirAccess.make_dir_recursive_absolute(tilesets_dir)
	DirAccess.make_dir_recursive_absolute(anims_dir)
	DirAccess.make_dir_recursive_absolute(chars_dir)
	DirAccess.make_dir_recursive_absolute(scenes_dir)

	var res_atlases_dir := output_dir.path_join("atlases")
	var res_tilesets_dir := output_dir.path_join("tilesets")
	var res_anims_dir := output_dir.path_join("animations")
	var res_chars_dir := output_dir.path_join("characters")
	var res_scenes_dir := output_dir.path_join("scenes")

	var result := {
		"success": true,
		"theme_name": theme_name,
		"tileset_path": "",
		"generated_atlases": [],
		"generated_scenes": [],
		"generated_sprite_frames": [],
		"warnings": [],
		"errors": []
	}

	# Determine primary tile size
	var primary_tile_size: int = tile_size_opt
	if primary_tile_size <= 0:
		for info in assets:
			if info.tile_size in [16, 32, 48, 64]:
				primary_tile_size = info.tile_size
				break
	if primary_tile_size <= 0:
		primary_tile_size = 32

	# Categorize assets
	var autotile_assets: Array = []
	var normal_tileset_assets: Array = []
	var character_assets: Array = []
	var object_assets: Array = []
	var metadata_assets: Array = []

	for info in assets:
		if info.warning != "":
			result["warnings"].append(info.warning)

		if ignore_non_rm and info.kind in [
			AssetDetectorScript.AssetKind.KR_NON_RM_A1,
			AssetDetectorScript.AssetKind.KR_NON_RM_A2,
			AssetDetectorScript.AssetKind.KR_NON_RM_A3,
			AssetDetectorScript.AssetKind.KR_NON_RM_A4
		]:
			continue

		match info.kind:
			AssetDetectorScript.AssetKind.RPG_A1, \
			AssetDetectorScript.AssetKind.RPG_A2, \
			AssetDetectorScript.AssetKind.RPG_A3, \
			AssetDetectorScript.AssetKind.RPG_A4:
				autotile_assets.append(info)

			AssetDetectorScript.AssetKind.RPG_A5, \
			AssetDetectorScript.AssetKind.RPG_B, \
			AssetDetectorScript.AssetKind.RPG_C, \
			AssetDetectorScript.AssetKind.RPG_D, \
			AssetDetectorScript.AssetKind.RPG_E, \
			AssetDetectorScript.AssetKind.KR_F_EXTENSION, \
			AssetDetectorScript.AssetKind.KR_NON_RM_A1, \
			AssetDetectorScript.AssetKind.KR_NON_RM_A2, \
			AssetDetectorScript.AssetKind.KR_NON_RM_A3, \
			AssetDetectorScript.AssetKind.KR_NON_RM_A4:
				normal_tileset_assets.append(info)

			AssetDetectorScript.AssetKind.RPG_CHARACTER_SINGLE, \
			AssetDetectorScript.AssetKind.RPG_CHARACTER_SHEET:
				character_assets.append(info)

			AssetDetectorScript.AssetKind.TAGGED_ANIMATION, \
			AssetDetectorScript.AssetKind.DOOR, \
			AssetDetectorScript.AssetKind.TREASURE_CHEST, \
			AssetDetectorScript.AssetKind.RPG_OBJECT_SINGLE, \
			AssetDetectorScript.AssetKind.RPG_OBJECT_SHEET, \
			AssetDetectorScript.AssetKind.GENERIC_ANIMATION:
				object_assets.append(info)

			AssetDetectorScript.AssetKind.RPG_TILESET_METADATA:
				metadata_assets.append(info)

	# 1. Build TileSet if any tile assets exist
	var tileset: TileSet = null
	var has_tiles := autotile_assets.size() > 0 or normal_tileset_assets.size() > 0
	if has_tiles:
		tileset = TileSetConverterScript.create_tileset(primary_tile_size)

		# Process Autotiles
		for info: AssetDetectorScript.AssetInfo in autotile_assets:
			var stem: String = info.path.get_file().get_basename()
			var derived: Dictionary = {}
			match info.kind:
				AssetDetectorScript.AssetKind.RPG_A1:
					derived = AutotileConverterScript.generate_a1_derived_atlas(info.path, primary_tile_size)
				AssetDetectorScript.AssetKind.RPG_A2:
					derived = AutotileConverterScript.generate_a2_derived_atlas(info.path, primary_tile_size)
				AssetDetectorScript.AssetKind.RPG_A3:
					derived = AutotileConverterScript.generate_a3_derived_atlas(info.path, primary_tile_size)
				AssetDetectorScript.AssetKind.RPG_A4:
					derived = AutotileConverterScript.generate_a4_derived_atlas(info.path, primary_tile_size)

			var d_img: Image = derived.get("image", null)
			if d_img != null:
				var src := TileSetConverterScript.add_autotile_atlas_source(
					tileset,
					d_img,
					derived,
					primary_tile_size
				)
				if src != null:
					# Save reconstructed atlas texture as compressed native binary resource (.res) to prevent massive plain-text .tres crash
					var texture_res_name := "%s_derived.res" % stem
					var texture_res_path := res_atlases_dir.path_join(texture_res_name)
					var texture_err := ResourceSaver.save(src.texture, texture_res_path, ResourceSaver.FLAG_COMPRESS)
					if texture_err == OK:
						src.texture.take_over_path(texture_res_path)
						result["generated_atlases"].append(texture_res_path)
					else:
						result["errors"].append("Failed to save atlas texture: %s (%d)" % [texture_res_path, texture_err])
					src.set_meta("source_filename", info.path.get_file().get_basename())
					src.set_meta("asset_kind", info.kind)
					src.set_meta("slot_index", MetadataConverterScript.get_slot_for_asset_kind(info.kind))
			else:
				result["errors"].append("Could not reconstruct autotile: %s" % info.path)

		# Process Normal Tileset Assets (A5, B..E, KR F, etc.) - runtime resources
		for info: AssetDetectorScript.AssetInfo in normal_tileset_assets:
			# Conversion reads source pixels, but the saved TileSet must only refer to
			# a byte-identical runtime copy, regardless of which converter entry point is used.
			var normal_res_path: String = output_dir.path_join("textures").path_join(info.path.get_file())
			var copy_err := SourceLoader.copy_file_to_runtime(info.path, normal_res_path)
			if copy_err != OK:
				result["errors"].append("Could not stage runtime texture %s: %s" % [info.path, error_string(copy_err)])
				continue

			var img: Image = SourceLoader.resolve_image(info.path)
			if img != null:
				var extra_cdata: Dictionary = info.extra.duplicate()
				if info.kind == AssetDetectorScript.AssetKind.KR_F_EXTENSION:
					var v_info := profile.extract_variant_info(info.path)
					extra_cdata.merge(v_info, true)

				var src := TileSetConverterScript.add_normal_atlas_source(
					tileset,
					img,
					info.kind,
					primary_tile_size,
					extra_cdata,
					normal_res_path
				)
				if src != null:
					src.set_meta("source_filename", info.path.get_file().get_basename())
					src.set_meta("asset_kind", info.kind)
					src.set_meta("slot_index", MetadataConverterScript.get_slot_for_asset_kind(info.kind))
			else:
				result["errors"].append("Could not load image file: %s" % info.path)

		# Apply metadata if Tilesets.json exists
		if apply_meta and metadata_assets.size() > 0:
			var meta_info: AssetDetectorScript.AssetInfo = metadata_assets[0]
			var parsed_list := MetadataConverterScript.parse_tilesets_json(meta_info.path)
			if parsed_list.size() > 0:
				var import_stems: Array = []
				for a in autotile_assets:
					import_stems.append(a.path)
				for a in normal_tileset_assets:
					import_stems.append(a.path)

				var matched_t := MetadataConverterScript.match_tileset(parsed_list, import_stems)
				if not matched_t.is_empty():
					var applied_count := MetadataConverterScript.apply_metadata_to_tileset(
						tileset,
						matched_t,
						primary_tile_size
					)
					result["applied_metadata_count"] = applied_count
				else:
					result["warnings"].append("No matching Tilesets.json entry; metadata was skipped.")

		# Save TileSet resource as .tres
		var ts_res_path := res_tilesets_dir.path_join("%s.tres" % theme_name)
		var ts_global_path := tilesets_dir.path_join("%s.tres" % theme_name)
		var save_err := ResourceSaver.save(tileset, ts_res_path)
		if save_err != OK:
			save_err = ResourceSaver.save(tileset, ts_global_path)

		if save_err == OK:
			result["tileset_path"] = ts_res_path
		else:
			result["errors"].append("Failed to save TileSet resource: error %d" % save_err)

	# 2. Process Characters
	for info: AssetDetectorScript.AssetInfo in character_assets:
		var stem: String = info.path.get_file().get_basename()
		var char_data := CharacterConverterScript.convert(info.path, primary_tile_size, fps)
		var is_single: bool = char_data.get("is_single", false)
		var characters: Array = char_data.get("characters", [])
		if characters.is_empty():
			result["errors"].append("Could not convert character: %s" % info.path)
			continue

		if is_single:
			var sf: SpriteFrames = char_data.get("sprite_frames", null)
			if sf != null:
				var sf_path := res_chars_dir.path_join("%s.tres" % stem)
				if ResourceSaver.save(sf, sf_path) == OK:
					result["generated_sprite_frames"].append(sf_path)
				else:
					result["errors"].append("Failed to save character frames: %s" % sf_path)
		else:
			for i in range(characters.size()):
				var sf: SpriteFrames = characters[i]
				if sf != null:
					var sf_path := res_chars_dir.path_join("%s_char%d.tres" % [stem, i])
					if ResourceSaver.save(sf, sf_path) == OK:
						result["generated_sprite_frames"].append(sf_path)
					else:
						result["errors"].append("Failed to save character frames: %s" % sf_path)

	# 3. Process Objects, Doors, Chests
	for info: AssetDetectorScript.AssetInfo in object_assets:
		var stem: String = info.path.get_file().get_basename()
		var obj_data := AnimationConverterScript.convert(info.path, profile, primary_tile_size, fps)
		var requires_prof: bool = obj_data.get("requires_animation_profile", false)
		if requires_prof:
			result["warnings"].append("Generic animation requires custom profile: %s" % info.path)
			continue

		if obj_data.get("sprite_frames") == null and obj_data.get("packed_scene") == null and obj_data.get("scenes", []).is_empty():
			result["errors"].append("Could not convert object: %s" % info.path)
			continue

		var sf: SpriteFrames = obj_data.get("sprite_frames", null)
		if sf != null:
			var sf_path := res_anims_dir.path_join("%s_frames.tres" % stem)
			if ResourceSaver.save(sf, sf_path) == OK:
				result["generated_sprite_frames"].append(sf_path)
			else:
				result["errors"].append("Failed to save object frames: %s" % sf_path)

		var scenes_list: Array = obj_data.get("scenes", [])
		for sc_item: Dictionary in scenes_list:
			var sc_name: String = sc_item.get("name", "")
			var sc_packed: PackedScene = sc_item.get("scene", null)
			if sc_packed != null and sc_name != "":
				var sc_path := res_scenes_dir.path_join("%s.tscn" % sc_name)
				if ResourceSaver.save(sc_packed, sc_path) == OK:
					result["generated_scenes"].append(sc_path)
				else:
					result["errors"].append("Failed to save scene: %s" % sc_path)

		if scenes_list.is_empty():
			var default_scene: PackedScene = obj_data.get("packed_scene", null)
			if default_scene != null:
				var sc_path := res_scenes_dir.path_join("%s.tscn" % stem)
				if ResourceSaver.save(default_scene, sc_path) == OK:
					result["generated_scenes"].append(sc_path)
				else:
					result["errors"].append("Failed to save scene: %s" % sc_path)

	result["success"] = result["errors"].is_empty()
	return result
