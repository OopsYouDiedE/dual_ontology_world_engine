extends Node
## Presentation only: never changes collision, room knowledge, or interaction access.
@export var auto_configure := false
@export var enabled := true
@export_range(0.5, 8.0) var radius := 2.2
@export var far_distance := 1000.0
var camera: Camera3D
@export var focus := Vector3.ZERO
var target: Node3D
var _materials: Array[ShaderMaterial] = []
var _copies: Dictionary = {}
var _strength := 0.0

func _ready() -> void:
	if auto_configure: call_deferred("_configure_scene")

func _configure_scene() -> void:
	var view_camera := get_parent().find_child("Camera3D", true, false) as Camera3D
	if view_camera != null: configure(view_camera, [get_parent()], focus)

func configure(view_camera: Camera3D, roots: Array, point: Vector3) -> void:
	for mat in _materials: mat.set_shader_parameter("occlusion_enabled", false)
	_materials.clear()
	camera = view_camera
	focus = point
	camera.near = 0.05
	camera.far = far_distance
	for root in roots:
		if is_instance_valid(root): _register(root, false)

func _material(source: Material) -> Material:
	if not source is ShaderMaterial or source.shader == null: return source
	var supported := false
	for spec in source.shader.get_shader_uniform_list():
		if spec.name == "occlusion_enabled": supported = true
	if not supported: return source
	var key := source.get_instance_id()
	var result: ShaderMaterial = _copies.get(key)
	if result == null:
		result = source.duplicate()
		_copies[key] = result
		_copies[result.get_instance_id()] = result
	if result not in _materials: _materials.append(result)
	return result

func _register(node: Node, architectural: bool) -> void:
	if node.has_meta("actor_id") or node.has_meta("model_source"): return
	var label := str(node.name).to_lower()
	var wall := architectural or bool(node.get_meta("camera_occluder", false)) or "wall" in label or "partition" in label or "ceiling" in label or "roof" in label
	if node is MeshInstance3D and wall and node.mesh != null:
		if node.material_override != null:
			node.material_override = _material(node.material_override)
		else:
			for index in node.mesh.get_surface_count():
				var source: Material = node.get_active_material(index)
				if source != null: node.set_surface_override_material(index, _material(source))
	if node is GridMap and wall and node.mesh_library != null:
		var library: MeshLibrary = node.mesh_library.duplicate()
		for id in library.get_item_list():
			var source: Mesh = library.get_item_mesh(id)
			if source == null: continue
			var mesh: Mesh = source.duplicate()
			for index in mesh.get_surface_count():
				var mat: Material = mesh.surface_get_material(index)
				if mat != null: mesh.surface_set_material(index, _material(mat))
			library.set_item_mesh(id, mesh)
		node.mesh_library = library
	for child in node.get_children(): _register(child, wall)

func _process(delta: float) -> void:
	if not is_instance_valid(camera): return
	if is_instance_valid(target): focus = target.global_position + Vector3.UP * 0.8
	_strength = move_toward(_strength, 1.0 if enabled else 0.0, delta * 4.0)
	for mat in _materials:
		mat.set_shader_parameter("occlusion_enabled", _strength > 0.0)
		mat.set_shader_parameter("occlusion_strength", _strength)
		mat.set_shader_parameter("occlusion_focus", focus)
		mat.set_shader_parameter("occlusion_camera", camera.global_position)
		mat.set_shader_parameter("occlusion_right", camera.global_basis.x.normalized())
		mat.set_shader_parameter("occlusion_up", camera.global_basis.y.normalized())
		mat.set_shader_parameter("occlusion_toward_camera", camera.global_basis.z.normalized())
		mat.set_shader_parameter("occlusion_radius", radius)
