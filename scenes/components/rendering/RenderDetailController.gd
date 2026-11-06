extends Node
## Presentation-only LOD for static primitive meshes. Call configure after each scene change.
signal detail_changed(statistics: Dictionary)
@export var auto_configure := true

const UPDATE_INTERVAL := 0.2
const HIGH_PIXELS := 96.0
const LOW_PIXELS := 28.0
const HYSTERESIS := 0.18

var _camera: WeakRef
var _entries: Array[Dictionary] = []
var _variants: Dictionary = {}
var _elapsed := 0.0
var _last_camera_transform := Transform3D()
var _last_camera_size := -1.0
var _last_camera_fov := -1.0
var _last_camera_projection := -1
var _last_viewport_size := Vector2.ZERO
var _camera_sampled := false

func _ready() -> void:
	if auto_configure:
		var view := get_parent().get_node_or_null("Camera3D") as Camera3D
		if view != null: configure(view, get_parent().get_children())

func restore_authored_meshes() -> void:
	for entry: Dictionary in _entries:
		var instance := (entry.node as WeakRef).get_ref() as MeshInstance3D
		if is_instance_valid(instance):
			var variants: Array = entry.variants
			if instance.mesh in variants: instance.mesh = variants[0]

## Force all registered meshes to a specific LOD level (0=high, 1=medium, 2=low)
## Retains 100% of collision shapes, doors, and interaction components (Task 4.10)
func force_lod(level: int) -> void:
	var target_lod := clampi(level, 0, 2)
	for entry: Dictionary in _entries:
		var instance: MeshInstance3D = (entry["node"] as WeakRef).get_ref() as MeshInstance3D
		if is_instance_valid(instance) and not instance.is_queued_for_deletion():
			var variants: Array = entry["variants"] as Array
			instance.mesh = variants[target_lod] as Mesh
			entry["lod"] = target_lod
	detail_changed.emit(snapshot())



func configure(camera: Camera3D, roots: Array) -> void:
	restore_authored_meshes()
	_camera = weakref(camera) if is_instance_valid(camera) else null
	_entries.clear()
	_variants.clear()
	_camera_sampled = false
	_elapsed = UPDATE_INTERVAL
	var visited: Dictionary = {}
	for root_value: Variant in roots:
		if root_value is Node and is_instance_valid(root_value):
			_register_tree(root_value as Node, visited)
	set_process(not _entries.is_empty() and is_instance_valid(camera))


func _process(delta: float) -> void:
	_elapsed += delta
	if _elapsed < UPDATE_INTERVAL:
		return
	_elapsed = 0.0
	var camera: Camera3D = _camera.get_ref() as Camera3D if _camera != null else null
	if not is_instance_valid(camera) or not camera.is_inside_tree():
		return
	var viewport_size: Vector2 = camera.get_viewport().get_visible_rect().size
	if _camera_sampled and camera.global_transform == _last_camera_transform \
			and camera.size == _last_camera_size and camera.fov == _last_camera_fov \
			and camera.projection == _last_camera_projection and viewport_size == _last_viewport_size:
		return
	_last_camera_transform = camera.global_transform
	_last_camera_size = camera.size
	_last_camera_fov = camera.fov
	_last_camera_projection = camera.projection
	_last_viewport_size = viewport_size
	_camera_sampled = true
	for entry: Dictionary in _entries:
		var instance: MeshInstance3D = (entry["node"] as WeakRef).get_ref() as MeshInstance3D
		if not is_instance_valid(instance) or not instance.is_inside_tree() or not instance.is_visible_in_tree():
			continue
		var variants: Array = entry["variants"] as Array
		var current_mesh: Mesh = instance.mesh
		if current_mesh != variants[0] and current_mesh != variants[1] and current_mesh != variants[2]:
			continue
		var pixels: float = _projected_diameter(camera, instance, variants[0] as Mesh)
		var next_lod: int = _select_lod(pixels, int(entry["lod"]))
		if next_lod != int(entry["lod"]):
			instance.mesh = variants[next_lod] as Mesh
			entry["lod"] = next_lod
	detail_changed.emit(snapshot())


func _register_tree(node: Node, visited: Dictionary) -> void:
	if _excluded(node):
		return
	var node_id: int = node.get_instance_id()
	if visited.has(node_id):
		return
	visited[node_id] = true
	if node is MeshInstance3D:
		var instance: MeshInstance3D = node as MeshInstance3D
		if instance.skin == null and _supported(instance.mesh):
			var source: Mesh = instance.mesh
			var variants: Array = _get_variants(source)
			_entries.append({"node": weakref(instance), "variants": variants, "lod": 0})
	for child: Node in node.get_children():
		_register_tree(child, visited)


func _excluded(node: Node) -> bool:
	return node is CharacterBody3D or node is Skeleton3D or node is AnimationPlayer \
		or node.has_meta("body_template") or node.has_meta("actor_id") \
		or node.has_meta("actor_identity")


func _supported(mesh: Mesh) -> bool:
	return mesh is SphereMesh or mesh is CylinderMesh or mesh is CapsuleMesh or mesh is TorusMesh


func _get_variants(source: Mesh) -> Array:
	var source_id: int = source.get_instance_id()
	if _variants.has(source_id):
		return _variants[source_id] as Array
	var medium: PrimitiveMesh = source.duplicate(false) as PrimitiveMesh
	var low: PrimitiveMesh = source.duplicate(false) as PrimitiveMesh
	for pair: Array in [[medium, 0.65], [low, 0.4]]:
		var mesh: PrimitiveMesh = pair[0] as PrimitiveMesh
		var factor: float = float(pair[1])
		if mesh is SphereMesh:
			(mesh as SphereMesh).radial_segments = maxi(4, roundi((source as SphereMesh).radial_segments * factor))
			(mesh as SphereMesh).rings = maxi(2, roundi((source as SphereMesh).rings * factor))
		elif mesh is CylinderMesh:
			(mesh as CylinderMesh).radial_segments = maxi(4, roundi((source as CylinderMesh).radial_segments * factor))
			(mesh as CylinderMesh).rings = maxi(1, roundi((source as CylinderMesh).rings * factor))
		elif mesh is CapsuleMesh:
			(mesh as CapsuleMesh).radial_segments = maxi(4, roundi((source as CapsuleMesh).radial_segments * factor))
			(mesh as CapsuleMesh).rings = maxi(1, roundi((source as CapsuleMesh).rings * factor))
		elif mesh is TorusMesh:
			(mesh as TorusMesh).rings = maxi(3, roundi((source as TorusMesh).rings * factor))
			(mesh as TorusMesh).ring_segments = maxi(3, roundi((source as TorusMesh).ring_segments * factor))
	var variants: Array = [source, medium, low]
	_variants[source_id] = variants
	return variants


func _projected_diameter(camera: Camera3D, instance: MeshInstance3D, source: Mesh) -> float:
	var bounds: AABB = source.get_aabb()
	var center: Vector3 = instance.global_transform * bounds.get_center()
	if camera.is_position_behind(center):
		return 0.0
	var half: Vector3 = bounds.size * 0.5
	var basis: Basis = instance.global_transform.basis
	var axes: Array[Vector3] = [basis.x * half.x, basis.y * half.y, basis.z * half.z]
	var maximum := 0.0
	for index: int in range(3):
		# Cylinder length is vertical; its circular cross-section determines faceting.
		if source is CylinderMesh and index == 1:
			continue
		var radial: Vector3 = axes[index]
		var diameter: float = camera.unproject_position(center + radial).distance_to(
			camera.unproject_position(center - radial))
		maximum = maxf(maximum, diameter)
	return maximum


func _select_lod(pixels: float, current: int) -> int:
	if current == 0:
		if pixels < LOW_PIXELS * (1.0 - HYSTERESIS):
			return 2
		return 1 if pixels < HIGH_PIXELS * (1.0 - HYSTERESIS) else 0
	if current == 2:
		if pixels > HIGH_PIXELS * (1.0 + HYSTERESIS):
			return 0
		return 1 if pixels > LOW_PIXELS * (1.0 + HYSTERESIS) else 2
	if pixels > HIGH_PIXELS * (1.0 + HYSTERESIS):
		return 0
	if pixels < LOW_PIXELS * (1.0 - HYSTERESIS):
		return 2
	return 1


func snapshot() -> Dictionary:
	var counts: Array[int] = [0, 0, 0]
	var registered := 0
	for entry: Dictionary in _entries:
		var instance: MeshInstance3D = (entry["node"] as WeakRef).get_ref() as MeshInstance3D
		if is_instance_valid(instance) and not instance.is_queued_for_deletion():
			registered += 1
			var variants: Array = entry["variants"] as Array
			for lod: int in range(3):
				if instance.mesh == variants[lod]:
					counts[lod] += 1
					break
	return {"registered": registered, "high": counts[0], "medium": counts[1], "low": counts[2],
		"algorithm": "maximum projected radial diameter in pixels; cylinder uses x/z; 96/28 px with 18% hysteresis"}
