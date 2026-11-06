@tool
extends EditorPlugin

## ArchitectPlugin: Godot EditorPlugin entry point for Native 3D Architect.
## Mounts 3D viewport handles (ArchitectGizmo3D) and the smart prefab palette dock.

const GizmoPlugin = preload("res://tools/godot_architect/ArchitectGizmo3D.gd")
const PaletteScene = preload("res://tools/godot_architect/PrefabPaletteDock.tscn")

var gizmo_plugin: ArchitectGizmo3D
var palette_dock: Control

func _enter_tree() -> void:
	# 1. Register Gizmo Plugin for 3D Viewport editing
	gizmo_plugin = GizmoPlugin.new()
	add_node_3d_gizmo_plugin(gizmo_plugin)
	
	# 2. Add Prefab Palette Dock to Editor
	palette_dock = PaletteScene.instantiate()
	add_control_to_dock(DOCK_SLOT_RIGHT_UL, palette_dock)
	palette_dock.prefab_spawn_requested.connect(_on_prefab_spawn_requested)

func _exit_tree() -> void:
	if gizmo_plugin != null:
		remove_node_3d_gizmo_plugin(gizmo_plugin)
		gizmo_plugin = null
	
	if palette_dock != null:
		remove_control_from_docks(palette_dock)
		palette_dock.queue_free()
		palette_dock = null

func _on_prefab_spawn_requested(prefab_path: String, rotation_y: float, dv_slot_id: String) -> void:
	var edited_root := get_editor_interface().get_edited_scene_root()
	if edited_root == null:
		push_warning("ArchitectPlugin: No edited scene root active to spawn prefab.")
		return
	
	var scene := load(prefab_path) as PackedScene
	if scene == null:
		return
	
	var instance: Node3D = scene.instantiate()
	instance.rotation_degrees.y = rotation_y
	if not dv_slot_id.is_empty():
		instance.set("dv_slot_id", dv_slot_id)
	
	# Add under Props node if present, otherwise directly under root
	var props_parent := edited_root.get_node_or_null("Props")
	if props_parent != null:
		props_parent.add_child(instance)
	else:
		edited_root.add_child(instance)
	
	instance.owner = edited_root
	print("ArchitectPlugin: Spawned prefab %s into scene %s" % [prefab_path, edited_root.name])
