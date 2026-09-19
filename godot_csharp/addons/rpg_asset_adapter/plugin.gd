@tool
extends EditorPlugin

## RPG Asset Adapter EditorPlugin entry point.
## Registers the batch importer UI dock in the Godot 4 editor.

const ImporterDockScene = preload("res://addons/rpg_asset_adapter/ui/importer_dock.tscn")

var _dock_instance: Control


func _enter_tree() -> void:
	if ImporterDockScene != null:
		_dock_instance = ImporterDockScene.instantiate()
		add_control_to_bottom_panel(_dock_instance, "RPG Asset Adapter")


func _exit_tree() -> void:
	if _dock_instance != null:
		remove_control_from_bottom_panel(_dock_instance)
		_dock_instance.queue_free()
		_dock_instance = null
