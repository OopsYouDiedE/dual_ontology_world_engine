@tool
extends SceneTree

const AdapterFacadeScript = preload("res://addons/rpg_asset_adapter/adapter_facade.gd")
const RPGAdapterConfigScript = preload("res://addons/rpg_asset_adapter/rpg_adapter_config.gd")

func copy_source_textures(source_dir: String, output_dir: String) -> Array[String]:
	var source_abs := ProjectSettings.globalize_path(source_dir)
	var texture_dir := ProjectSettings.globalize_path(output_dir).path_join("textures")
	var errors: Array[String] = []
	var mkdir_err := DirAccess.make_dir_recursive_absolute(texture_dir)
	if mkdir_err != OK:
		errors.append("Could not create runtime texture directory: %s (%s)" % [texture_dir, error_string(mkdir_err)])
		return errors
	var dir := DirAccess.open(source_abs)
	if dir == null:
		errors.append("Could not open Victorian source directory: " + source_abs)
		return errors
	dir.list_dir_begin()
	while true:
		var filename := dir.get_next()
		if filename.is_empty():
			break
		if dir.current_is_dir() or filename.get_extension().to_lower() != "png":
			continue
		var source_path := source_abs.path_join(filename)
		var target_path := texture_dir.path_join(filename)
		if FileAccess.file_exists(target_path) and FileAccess.get_sha256(source_path) == FileAccess.get_sha256(target_path):
			continue
		var source_file := FileAccess.open(source_path, FileAccess.READ)
		var target_file := FileAccess.open(target_path, FileAccess.WRITE)
		if source_file == null or target_file == null:
			errors.append("Could not copy source texture: " + filename)
			continue
		target_file.store_buffer(source_file.get_buffer(source_file.get_length()))
		target_file.close()
		source_file.close()
	dir.list_dir_end()
	return errors

func _init() -> void:
	print("--- STARTING BATCH CONVERSION WITH .TRES CONFIG ---")
	var config_path := RPGAdapterConfigScript.DEFAULT_CONFIG_PATH
	var config: Resource = null
	if ResourceLoader.exists(config_path):
		config = ResourceLoader.load(config_path)

	if config == null:
		config = RPGAdapterConfigScript.new()

	print("Using Config: source=%s, output=%s, size=%d" % [config.get("source_dir"), config.get("output_dir"), config.get("tile_size")])
	var copy_errors := copy_source_textures(config.get("source_dir"), config.get("output_dir"))
	if not copy_errors.is_empty():
		push_error("Victorian runtime texture copy failed: %s" % str(copy_errors))
		quit(1)
		return
	var result: Dictionary = AdapterFacadeScript.run_with_config(config)

	print("Conversion Success: %s" % str(result.get("success", false)))
	print("Generated TileSet: %s" % str(result.get("tileset_path", "")))
	print("Generated Atlases: %d" % result.get("generated_atlases", []).size())
	print("Generated Scenes: %d" % result.get("generated_scenes", []).size())
	print("Generated SpriteFrames: %d" % result.get("generated_sprite_frames", []).size())
	if result.get("warnings", []).size() > 0:
		print("Warnings: %s" % str(result["warnings"]))
	if result.get("errors", []).size() > 0:
		print("Errors: %s" % str(result["errors"]))
	print("--- BATCH CONVERSION FINISHED ---")
	quit(0 if result.get("success", false) else 1)
