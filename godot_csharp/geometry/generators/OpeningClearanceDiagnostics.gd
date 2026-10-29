extends RefCounted
## Runtime-only measurements. The centre and axis use design coordinates in metres.
## Rays start on the opening plane and stop at the first actual solid in each direction.
const Coordinates = preload("res://geometry/DesignCoordinates.gd")
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")

static func measure(space: PhysicsDirectSpaceState3D, opening_id: String, centre_design: Vector3, lateral_design: Vector3, heights_m: Array, ray_extent_m: float, declared_width_m: float, exclusions: Array[RID] = []) -> Dictionary:
	var report := {"opening_id":opening_id, "valid":false, "samples":[], "declared_clear_width_m":declared_width_m}
	if space == null or opening_id.is_empty() or not centre_design.is_finite() or not lateral_design.is_finite() or absf(lateral_design.z) > 0.000001 or lateral_design.length_squared() < 0.000001 or not is_finite(ray_extent_m) or not is_finite(declared_width_m) or ray_extent_m <= 0.0 or declared_width_m <= 0.0 or heights_m.is_empty():
		report["error"] = "invalid-measurement-parameters"
		return report
	var axis := Coordinates.to_godot(lateral_design.normalized())
	var all_measured := true
	for value in heights_m:
		if (typeof(value) != TYPE_INT and typeof(value) != TYPE_FLOAT) or not is_finite(value) or value <= 0.0:
			report["error"] = "invalid-sample-height"
			return report
		var height := float(value)
		var centre := Coordinates.to_godot(centre_design+Vector3(0.0,0.0,height))
		var sample := {"height_m":height}
		var sides: Array[Dictionary] = []
		for direction in [-1.0,1.0]:
			var query := PhysicsRayQueryParameters3D.create(centre,centre+axis*direction*ray_extent_m,PhysicsLayers.SOLID,exclusions)
			query.collide_with_areas = false
			var hit := space.intersect_ray(query)
			if hit.is_empty():
				all_measured = false
				sides.append({"hit":false})
			else:
				var collider: Object = hit.get("collider")
				var node := collider as Node
				var object_id := ""
				var ancestor := node
				while ancestor != null:
					if ancestor.has_meta("world_object_id"):
						object_id = str(ancestor.get_meta("world_object_id"))
						break
					ancestor = ancestor.get_parent()
				sides.append({"hit":true,"distance_m":centre.distance_to(hit.position),"object_id":object_id,"path":str(node.get_path()) if node != null else "unknown","shape":hit.get("shape",-1),"position_godot": [hit.position.x,hit.position.y,hit.position.z]})
		sample["negative"] = sides[0]
		sample["positive"] = sides[1]
		if sides[0].hit and sides[1].hit:
			var width: float = sides[0].distance_m + sides[1].distance_m
			sample["measured_width_m"] = width
			sample["difference_from_declared_m"] = width-declared_width_m
		report.samples.append(sample)
	report["valid"] = all_measured
	if not all_measured: report["error"] = "ray-missed-boundary"
	return report
