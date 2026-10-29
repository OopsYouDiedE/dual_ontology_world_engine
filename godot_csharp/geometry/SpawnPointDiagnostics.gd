extends RefCounted
## Queries an authored building-local foot point without moving or creating an actor.
const Coordinates = preload("res://geometry/DesignCoordinates.gd")

static func inspect(frame: Node3D, foot_design: Vector3, height_m: float, radius_m: float, clearance_m: float, floor_tolerance_m: float, solid_mask: int, exclusions: Array[RID]) -> Dictionary:
	var result := {"valid":false,"scope":"spawn-capsule-and-floor-sample","blockers":[]}
	if not is_instance_valid(frame) or not frame.is_inside_tree() or not foot_design.is_finite() or not is_finite(height_m) or not is_finite(radius_m) or not is_finite(clearance_m) or not is_finite(floor_tolerance_m) or radius_m <= 0.0 or height_m < 2.0 * radius_m or clearance_m <= 0.0 or floor_tolerance_m <= 0.0 or solid_mask <= 0:
		result["reason"] = "invalid-spawn-query"
		return result
	var basis := frame.global_transform.basis
	if not basis.x.is_finite() or not basis.y.is_finite() or not basis.z.is_finite() or absf(basis.x.length_squared()-1.0) > 0.00001 or absf(basis.y.length_squared()-1.0) > 0.00001 or absf(basis.z.length_squared()-1.0) > 0.00001 or absf(basis.x.dot(basis.y)) > 0.00001 or absf(basis.x.dot(basis.z)) > 0.00001 or absf(basis.y.dot(basis.z)) > 0.00001 or basis.determinant() <= 0.0:
		result["reason"] = "invalid-spawn-frame"
		return result
	var foot := frame.global_transform * Coordinates.to_godot(foot_design)
	var up := basis.y.normalized()
	var space := frame.get_world_3d().direct_space_state
	var capsule := CapsuleShape3D.new()
	capsule.radius = radius_m
	capsule.height = height_m
	var query := PhysicsShapeQueryParameters3D.new()
	query.shape = capsule
	query.transform = Transform3D(basis,foot + up * (height_m / 2.0 + clearance_m))
	query.collision_mask = solid_mask
	query.exclude = exclusions
	query.collide_with_areas = false
	var hits := space.intersect_shape(query,64)
	for hit in hits:
		var node := hit.get("collider") as Node
		var identity := ""
		var path := "" if node == null else str(node.get_path())
		while node != null:
			if node.has_meta("world_object_id"):
				identity = str(node.get_meta("world_object_id"))
				break
			node = node.get_parent()
		result.blockers.append({"object_id":identity,"node_path":path,"shape":hit.get("shape",-1)})
	var ray := PhysicsRayQueryParameters3D.create(foot + up * floor_tolerance_m,foot - up * floor_tolerance_m,solid_mask,exclusions)
	ray.collide_with_areas = false
	var floor_hit := space.intersect_ray(ray)
	result["floor_hit"] = not floor_hit.is_empty()
	var floor_node := floor_hit.get("collider") as Node
	var floor_id := ""
	while floor_node != null:
		if floor_node.has_meta("world_object_id"):
			floor_id = str(floor_node.get_meta("world_object_id"))
			break
		floor_node = floor_node.get_parent()
	result["floor_object_id"] = floor_id
	result["floor_height_error_m"] = absf((floor_hit.position-foot).dot(up)) if not floor_hit.is_empty() else null
	result["floor_normal_dot"] = floor_hit.normal.dot(up) if not floor_hit.is_empty() else null
	result["foot_design"] = [foot_design.x,foot_design.y,foot_design.z]
	result["capsule_height_m"] = height_m
	result["capsule_radius_m"] = radius_m
	result["clearance_m"] = clearance_m
	result["physics_frame"] = Engine.get_physics_frames()
	result.valid = hits.is_empty() and not floor_hit.is_empty() and float(result.floor_height_error_m) <= floor_tolerance_m and float(result.floor_normal_dot) >= 0.99
	return result
