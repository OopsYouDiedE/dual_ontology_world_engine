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
	if edited_root == null or not ResourceLoader.exists(prefab_path):
		push_warning("ArchitectPlugin: Open a scene and choose a valid prefab first.")
		return
	var scene := load(prefab_path) as PackedScene
	if scene == null: return
	var instance := scene.instantiate() as Node3D
	if instance == null:
		push_warning("ArchitectPlugin: Prefab root must be Node3D: " + prefab_path)
		return
	instance.rotation_degrees.y = rotation_y
	if not dv_slot_id.is_empty(): instance.set_meta("dv_slot_id", dv_slot_id)
	var parent := edited_root.get_node_or_null("Props")
	if parent == null: parent = edited_root
	var undo := get_undo_redo()
	undo.create_action("Place prefab: " + instance.name)
	undo.add_do_method(parent, &"add_child", instance, true)
	undo.add_do_method(instance, &"set_owner", edited_root)
	undo.add_do_reference(instance)
	undo.add_undo_method(parent, &"remove_child", instance)
	undo.commit_action()
	get_editor_interface().get_selection().clear()
	get_editor_interface().get_selection().add_node(instance)
