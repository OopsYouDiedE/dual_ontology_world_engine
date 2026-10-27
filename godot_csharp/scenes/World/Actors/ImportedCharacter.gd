extends RefCounted

static func attach(parent: Node3D, path: String, height: float = 1.32, yaw_degrees: float = 0.0) -> bool:
	var packed := load(path) as PackedScene
	if packed == null: return false
	var model := packed.instantiate() as Node3D
	if model == null: return false
	model.rotation.y = deg_to_rad(yaw_degrees)
	var bounds: Array[AABB] = []
	_collect(model, Transform3D.IDENTITY, bounds)
	if bounds.is_empty():
		model.free()
		return false
	var total: AABB = bounds[0]
	for item in bounds: total = total.merge(item)
	var factor := height / maxf(total.size.y, 0.001)
	model.scale *= factor
	model.position = -Vector3(total.get_center().x, total.position.y, total.get_center().z) * factor
	parent.add_child(model)
	parent.set_meta("model_source", path)
	parent.set_meta("presentation", "static-skinned-model")
	return true

static func _collect(node: Node, parent_transform: Transform3D, bounds: Array[AABB]) -> void:
	var current := parent_transform
	if node is Node3D: current = parent_transform * node.transform
	if node is MeshInstance3D and node.mesh != null:
		bounds.append(current * node.get_aabb())
	if node is AnimationPlayer:
		node.autoplay = ""
		node.stop()
	for child in node.get_children(): _collect(child, current, bounds)
