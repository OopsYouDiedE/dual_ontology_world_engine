@tool
extends SceneTree

const AdapterFacadeScript = preload("res://addons/rpg_asset_adapter/adapter_facade.gd")
const RPGAdapterConfigScript = preload("res://addons/rpg_asset_adapter/rpg_adapter_config.gd")

func _init() -> void:
	print("--- STARTING BATCH CONVERSION WITH .TRES CONFIG ---")
	var config_path := RPGAdapterConfigScript.DEFAULT_CONFIG_PATH
	var config: Resource = null
	if ResourceLoader.exists(config_path):
		config = ResourceLoader.load(config_path)

	if config == null:
		config = RPGAdapterConfigScript.new()

	print("Using Config: source=%s, output=%s, size=%d" % [config.get("source_dir"), config.get("output_dir"), config.get("tile_size")])
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
