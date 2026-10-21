@tool
extends EditorPlugin

## RPG Asset Adapter EditorPlugin.
## Adds one explicit editor command backed by the saved Resource configuration.

const AdapterFacadeScript = preload("res://addons/rpg_asset_adapter/adapter_facade.gd")
const RPGAdapterConfigScript = preload("res://addons/rpg_asset_adapter/rpg_adapter_config.gd")
const MENU_LABEL := "RPG Asset Adapter: Convert Saved Config"


func _enter_tree() -> void:
	add_tool_menu_item(MENU_LABEL, _run_saved_config)


func _exit_tree() -> void:
	remove_tool_menu_item(MENU_LABEL)


func _run_saved_config() -> void:
	var config: Resource = null
	if ResourceLoader.exists(RPGAdapterConfigScript.DEFAULT_CONFIG_PATH):
		config = ResourceLoader.load(RPGAdapterConfigScript.DEFAULT_CONFIG_PATH)
	if config == null:
		config = RPGAdapterConfigScript.new()

	var result: Dictionary = AdapterFacadeScript.run_with_config(config)
	if not result.get("success", false):
		push_error("RPG Asset Adapter conversion failed: %s" % str(result.get("errors", [])))
	else:
		print("RPG Asset Adapter conversion complete: %s" % str(result.get("tileset_path", "")))
