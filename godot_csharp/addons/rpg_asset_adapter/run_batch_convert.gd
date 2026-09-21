@tool
extends SceneTree

const AdapterFacadeScript = preload("res://addons/rpg_asset_adapter/adapter_facade.gd")

func _init() -> void:
	print("--- STARTING BATCH CONVERSION TO ShareAsset/victorian ---")
	var scan_dir := "res://ShareAsset/victorian/48px+MVMZ"
	var assets := AdapterFacadeScript.scan_directory(scan_dir)
	print("Scanned %d assets." % assets.size())

	var options := {
		"output_dir": "res://ShareAsset/victorian",
		"theme_name": "Victorian",
		"tile_size": 48,
		"fps": 6.0,
		"apply_metadata": true
	}

	var result := AdapterFacadeScript.convert_assets(assets, options)
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
