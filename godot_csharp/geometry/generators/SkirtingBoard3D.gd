extends RefCounted
## Skirting board generator along wall bases (Task 2.9).
## Follows wall baseline, automatically breaks at door openings (bottom < 0.1m)
## so doorways remain completely flat without any trip hazard or step.
## Continues uninterrupted under window openings (bottom >= 0.1m).
const Coordinates = preload("res://geometry/DesignCoordinates.gd")
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")

static func create(id: String, start: Vector3, end: Vector3, wall_thickness: float, openings: Array, height: float = 0.08, depth: float = 0.02, material: Material = null) -> Node3D:
	if id.is_empty() or not start.is_finite() or not end.is_finite() or not is_finite(wall_thickness) or not is_finite(height) or not is_finite(depth):
		push_error("SkirtingBoard3D " + id + ": non-finite-parameter")
		return null
	var direction := Vector2(end.x - start.x, end.y - start.y).normalized()
	var left := Vector2(-direction.y, direction.x)
	var length := Vector2(end.x - start.x, end.y - start.y).length()
	if length <= 0.001 or height <= 0.0 or depth <= 0.0:
		push_error("SkirtingBoard3D " + id + ": invalid-dimensions")
		return null
	
	# Extract door openings that cut the skirting board (floor level openings)
	var cuts: Array[Vector2] = []
	for hole in openings:
		if not hole is Dictionary: continue
		var b: float = float(hole.get("bottom", 0.0))
		if b < 0.10: # Door or floor level opening
			var from_x: float = clampf(float(hole.get("from", 0.0)), 0.0, length)
			var to_x: float = clampf(float(hole.get("to", 0.0)), 0.0, length)
			if to_x > from_x:
				cuts.append(Vector2(from_x, to_x))
	
	# Sort cuts by start position
	cuts.sort_custom(func(a: Vector2, b: Vector2) -> bool: return a.x < b.x)
	
	# Determine solid intervals
	var intervals: Array[Vector2] = []
	var cur_x := 0.0
	for cut in cuts:
		if cut.x > cur_x + 0.001:
			intervals.append(Vector2(cur_x, cut.x))
		cur_x = maxf(cur_x, cut.y)
	if cur_x < length - 0.001:
		intervals.append(Vector2(cur_x, length))
	
	var root := Node3D.new()
	root.name = "SkirtingBoard_" + id
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "skirting_board")
	root.set_meta("intervals_count", intervals.size())
	
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	root.add_child(visuals)
	
	var default_mat: Material = material
	if default_mat == null:
		var std := StandardMaterial3D.new()
		std.albedo_color = Color("4a3728")
		std.roughness = 0.8
		default_mat = std
	
	# Build skirting board segments for both faces of the wall
	for idx in range(intervals.size()):
		var span: Vector2 = intervals[idx]
		var seg_len := span.y - span.x
		var mid_x := (span.x + span.y) * 0.5
		
		for side in [-1.0, 1.0]:
			var face_name := "SidePos" if side > 0.0 else "SideNeg"
			var part := MeshInstance3D.new()
			part.name = "Segment_%d_%s" % [idx, face_name]
			
			var box := BoxMesh.new()
			box.size = Vector3(seg_len, height, depth)
			part.mesh = box
			part.material_override = default_mat
			
			# Calculate midpoint position in 3D
			var plan: Vector2 = Vector2(start.x, start.y) + direction * mid_x + left * (side * (wall_thickness * 0.5 + depth * 0.5))
			part.position = Coordinates.to_godot(Vector3(plan.x, plan.y, start.z + height * 0.5))
			
			# Orient along wall
			var along: Vector3 = Coordinates.to_godot(Vector3(direction.x, direction.y, 0.0)).normalized()
			part.rotation.y = atan2(-along.z, along.x)
			visuals.add_child(part)
			
	return root
