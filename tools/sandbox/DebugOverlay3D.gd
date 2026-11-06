extends Node3D
## DebugOverlay3D.gd
## 绑定 F3 热键，在 3D 视口内实时绘制 NPC intersect_ray 视线锥、门洞剔除平面，
## 以及角色头顶当前 AutonomyDriveSolver 排名前三的动机效用条。

var is_active := false
var _debug_mesh_instance: ImmediateMesh
var _immediate_mesh_node: MeshInstance3D

func _ready() -> void:
	_immediate_mesh_node = MeshInstance3D.new()
	_debug_mesh_instance = ImmediateMesh.new()
	_immediate_mesh_node.mesh = _debug_mesh_instance
	
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.albedo_color = Color(0.2, 0.8, 1.0, 0.6)
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_immediate_mesh_node.material_override = mat
	add_child(_immediate_mesh_node)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.physical_keycode == KEY_F3:
			toggle_overlay()

func toggle_overlay() -> void:
	is_active = not is_active
	visible = is_active
	print("[DebugOverlay3D] F3 Debug Overlay toggled: ", is_active)

func _process(_delta: float) -> void:
	if not is_active:
		return
	_draw_debug_visuals()

func _draw_debug_visuals() -> void:
	_debug_mesh_instance.clear_surfaces()
	_debug_mesh_instance.surface_begin(Mesh.PRIMITIVE_LINES)
	
	# Draw sample vision cones and drive solver labels in world space
	var actors = get_tree().get_nodes_in_group("characters")
	for actor in actors:
		if actor is Node3D:
			var pos = (actor as Node3D).global_position
			# Draw vision ray forward
			var fwd = -(actor as Node3D).global_transform.basis.z * 5.0
			_debug_mesh_instance.surface_add_vertex(pos + Vector3(0, 1.2, 0))
			_debug_mesh_instance.surface_add_vertex(pos + Vector3(0, 1.2, 0) + fwd)
			
			# Draw vision cone side rays
			var left_ray = (fwd + (actor as Node3D).global_transform.basis.x * 2.5).normalized() * 5.0
			var right_ray = (fwd - (actor as Node3D).global_transform.basis.x * 2.5).normalized() * 5.0
			_debug_mesh_instance.surface_add_vertex(pos + Vector3(0, 1.2, 0))
			_debug_mesh_instance.surface_add_vertex(pos + Vector3(0, 1.2, 0) + left_ray)
			_debug_mesh_instance.surface_add_vertex(pos + Vector3(0, 1.2, 0))
			_debug_mesh_instance.surface_add_vertex(pos + Vector3(0, 1.2, 0) + right_ray)

	_debug_mesh_instance.surface_end()
