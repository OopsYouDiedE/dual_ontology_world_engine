extends RefCounted
## Read-only queries against registered floor bodies, after a physics-frame sync.
## Samples come from the published outline. They never move an actor or world state.
const Coordinates = preload("res://geometry/DesignCoordinates.gd")
const FloorSurface = preload("res://geometry/FloorSurface3D.gd")
const BOUNDARY_EPSILON_M := 0.00001

static func inspect(root: Node3D, outer: PackedVector2Array, holes: Array[PackedVector2Array], elevation_m: float, thickness_m: float, sample_spacing_m: float, edge_inset_m: float, tolerance_m: float, max_samples: int, floor_mask: int) -> Dictionary:
	var report := {"valid": false, "complete": false, "scope": "registered-floor-layer-only; component outline and holes", "samples": [], "issues": []}
	if not is_instance_valid(root) or not root.is_inside_tree():
		report.issues.append({"reason": "floor-not-in-live-world"})
		return report
	report["object_id"] = str(root.get_meta("world_object_id", ""))
	report["physics_frame"] = Engine.get_physics_frames()
	var basis := root.global_transform.basis
	if not root.global_transform.origin.is_finite() or not basis.x.is_finite() or not basis.y.is_finite() or not basis.z.is_finite() or basis.determinant() <= 0.0 or absf(basis.x.length_squared() - 1.0) > 0.00001 or absf(basis.y.length_squared() - 1.0) > 0.00001 or absf(basis.z.length_squared() - 1.0) > 0.00001 or absf(basis.x.dot(basis.y)) > 0.00001 or absf(basis.x.dot(basis.z)) > 0.00001 or absf(basis.y.dot(basis.z)) > 0.00001:
		report.issues.append({"reason": "non-finite-or-nonrigid-floor-transform"})
		return report
	var invalid := FloorSurface.validate(outer, holes, elevation_m, thickness_m)
	if not invalid.is_empty() or not is_finite(sample_spacing_m) or not is_finite(edge_inset_m) or not is_finite(tolerance_m) or sample_spacing_m <= 0.0 or edge_inset_m <= BOUNDARY_EPSILON_M or tolerance_m <= 0.0 or max_samples <= 0 or max_samples > 1000000 or floor_mask <= 0 or str(report.object_id).is_empty():
		report.issues.append({"reason": "invalid-diagnostic-parameters", "outline_error": invalid})
		return report
	var bounds := Rect2(outer[0], Vector2.ZERO)
	for point in outer: bounds = bounds.expand(point)
	var vertex_count := outer.size()
	for hole in holes: vertex_count += hole.size()
	var estimate := ceilf(bounds.size.x / sample_spacing_m) * ceilf(bounds.size.y / sample_spacing_m) + 2.0 * vertex_count + holes.size() + 1
	if not is_finite(estimate) or estimate > max_samples:
		report.issues.append({"reason": "sample-budget-exceeded", "estimated_maximum": estimate, "budget": max_samples})
		return report
	var probes: Array[Vector2] = []
	var columns := ceili(bounds.size.x / sample_spacing_m)
	var rows := ceili(bounds.size.y / sample_spacing_m)
	for row in range(rows):
		for column in range(columns):
			var point := bounds.position + Vector2((column + 0.5) * sample_spacing_m, (row + 0.5) * sample_spacing_m)
			if point.x < bounds.end.x and point.y < bounds.end.y and Geometry2D.is_point_in_polygon(point, outer) and not _near_boundary(point, outer):
				var on_hole_boundary := false
				for hole in holes:
					if _near_boundary(point, hole): on_hole_boundary = true
				if not on_hole_boundary: probes.append(point)
	var rings: Array[PackedVector2Array] = [outer]
	rings.append_array(holes)
	for ring in rings:
		var area := 0.0
		for index in range(ring.size()): area += ring[index].cross(ring[(index + 1) % ring.size()])
		for index in range(ring.size()):
			var a := ring[index]
			var b := ring[(index + 1) % ring.size()]
			var edge := b - a
			var inside := Vector2(-edge.y, edge.x).normalized() * signf(area)
			var middle := (a + b) * 0.5
			probes.append(middle + inside * edge_inset_m)
			probes.append(middle - inside * edge_inset_m)
		var triangles := Geometry2D.triangulate_polygon(ring)
		if triangles.size() < 3:
			report.issues.append({"reason": "ring-interior-sample-unavailable"})
			return report
		probes.append((ring[triangles[0]] + ring[triangles[1]] + ring[triangles[2]]) / 3.0)
	if probes.size() > max_samples:
		report.issues.append({"reason": "sample-budget-exceeded", "actual": probes.size(), "budget": max_samples})
		return report
	var normal := root.global_transform.basis.y.normalized()
	var distance := maxf(2.0 * tolerance_m, 0.02)
	var space := root.get_world_3d().direct_space_state
	var solid_count := 0
	var hole_count := 0
	var outside_count := 0
	for point in probes:
		var kind := _kind(point, outer, holes)
		if kind == "solid": solid_count += 1
		elif kind == "hole": hole_count += 1
		else: outside_count += 1
		var expected := root.global_transform * Coordinates.to_godot(Vector3(point.x, point.y, elevation_m))
		var query := PhysicsRayQueryParameters3D.create(expected + normal * distance, expected - normal * distance, floor_mask)
		query.collide_with_areas = false
		query.collide_with_bodies = true
		var hit := space.intersect_ray(query)
		var hit_id := "" if hit.is_empty() else _object_id(hit.get("collider"))
		var sample := {"design_point": [point.x, point.y], "kind": kind, "hit": not hit.is_empty(), "hit_object_id": hit_id}
		var reason := ""
		if kind == "solid":
			if hit.is_empty(): reason = "solid-floor-missed"
			elif hit_id != str(report.object_id): reason = "unexpected-floor-owner"
			else:
				var hit_position: Vector3 = hit.position
				var hit_normal: Vector3 = hit.normal
				var error := absf((hit_position - expected).dot(normal))
				sample["height_error_m"] = error
				if error > tolerance_m: reason = "floor-height-error"
				elif hit_normal.dot(normal) < 0.99: reason = "floor-normal-error"
		elif kind == "hole" and not hit.is_empty(): reason = "floor-inside-declared-hole"
		elif kind == "outside" and hit_id == str(report.object_id): reason = "floor-outside-declared-outline"
		if not reason.is_empty():
			sample["reason"] = reason
			report.issues.append({"sample": report.samples.size(), "reason": reason})
		report.samples.append(sample)
	report["solid_samples"] = solid_count
	report["hole_samples"] = hole_count
	report["outside_samples"] = outside_count
	report["tolerance_m"] = tolerance_m
	report["edge_probe_inset_m"] = edge_inset_m
	report["floor_mask"] = floor_mask
	if solid_count == 0: report.issues.append({"reason": "solid-sample-unavailable"})
	if not holes.is_empty() and hole_count == 0: report.issues.append({"reason": "hole-sample-unavailable"})
	report.complete = true
	report.valid = report.issues.is_empty() and solid_count > 0 and (holes.is_empty() or hole_count > 0)
	return report

static func _kind(point: Vector2, outer: PackedVector2Array, holes: Array[PackedVector2Array]) -> String:
	if not Geometry2D.is_point_in_polygon(point, outer): return "outside"
	for hole in holes:
		if Geometry2D.is_point_in_polygon(point, hole): return "hole"
	return "solid"

## A real assembly void may lie between several separate slab components.
## A positive same-level floor control is mandatory before absence can pass.
static func inspect_void(frame: Node3D, void_id: String, ring: PackedVector2Array, elevation_m: float, sample_spacing_m: float, edge_inset_m: float, tolerance_m: float, max_samples: int, floor_mask: int, reference_point: Vector2, reference_object_id: String) -> Dictionary:
	var report := {"valid": false, "complete": false, "scope": "same-level-floor-void-with-positive-control", "void_id": void_id, "samples": [], "issues": []}
	if not is_instance_valid(frame) or not frame.is_inside_tree():
		report.issues.append({"reason": "frame-not-in-live-world"})
		return report
	var basis := frame.global_transform.basis
	if not frame.global_transform.origin.is_finite() or not basis.x.is_finite() or not basis.y.is_finite() or not basis.z.is_finite() or basis.determinant() <= 0.0 or absf(basis.x.length_squared() - 1.0) > 0.00001 or absf(basis.y.length_squared() - 1.0) > 0.00001 or absf(basis.z.length_squared() - 1.0) > 0.00001 or absf(basis.x.dot(basis.y)) > 0.00001 or absf(basis.x.dot(basis.z)) > 0.00001 or absf(basis.y.dot(basis.z)) > 0.00001:
		report.issues.append({"reason": "non-finite-or-nonrigid-frame-transform"})
		return report
	var no_holes: Array[PackedVector2Array] = []
	var invalid := FloorSurface.validate(ring, no_holes, elevation_m, 1.0)
	if not invalid.is_empty() or void_id.is_empty() or reference_object_id.is_empty() or not reference_point.is_finite() or not is_finite(sample_spacing_m) or not is_finite(edge_inset_m) or not is_finite(tolerance_m) or sample_spacing_m <= 0.0 or edge_inset_m <= BOUNDARY_EPSILON_M or tolerance_m <= 0.0 or max_samples <= 0 or max_samples > 1000000 or floor_mask <= 0:
		report.issues.append({"reason": "invalid-void-diagnostic-parameters", "outline_error": invalid})
		return report
	if Geometry2D.is_point_in_polygon(reference_point, ring):
		report.issues.append({"reason": "positive-control-inside-void"})
		return report
	var bounds := Rect2(ring[0], Vector2.ZERO)
	var area := 0.0
	for index in range(ring.size()):
		bounds = bounds.expand(ring[index])
		area += ring[index].cross(ring[(index + 1) % ring.size()])
	var estimate := ceilf(bounds.size.x / sample_spacing_m) * ceilf(bounds.size.y / sample_spacing_m) + ring.size() + 2.0
	if not is_finite(estimate) or estimate > max_samples:
		report.issues.append({"reason": "sample-budget-exceeded", "estimated_maximum": estimate, "budget": max_samples})
		return report
	var normal := basis.y.normalized()
	var distance := maxf(2.0 * tolerance_m, 0.02)
	var space := frame.get_world_3d().direct_space_state
	var reference := frame.global_transform * Coordinates.to_godot(Vector3(reference_point.x, reference_point.y, elevation_m))
	var control_query := PhysicsRayQueryParameters3D.create(reference + normal * distance, reference - normal * distance, floor_mask)
	control_query.collide_with_areas = false
	var control := space.intersect_ray(control_query)
	var control_id := "" if control.is_empty() else _object_id(control.get("collider"))
	report["reference"] = {"design_point": [reference_point.x, reference_point.y], "expected_object_id": reference_object_id, "hit_object_id": control_id}
	if control.is_empty() or control_id != reference_object_id:
		report.issues.append({"reason": "positive-control-floor-not-confirmed"})
		return report
	var control_position: Vector3 = control.position
	var control_normal: Vector3 = control.normal
	if absf((control_position - reference).dot(normal)) > tolerance_m or control_normal.dot(normal) < 0.99:
		report.issues.append({"reason": "positive-control-height-or-normal-error"})
		return report
	var probes: Array[Vector2] = []
	for row in range(ceili(bounds.size.y / sample_spacing_m)):
		for column in range(ceili(bounds.size.x / sample_spacing_m)):
			var point := bounds.position + Vector2((column + 0.5) * sample_spacing_m, (row + 0.5) * sample_spacing_m)
			if point.x < bounds.end.x and point.y < bounds.end.y and Geometry2D.is_point_in_polygon(point, ring) and not _near_boundary(point, ring): probes.append(point)
	for index in range(ring.size()):
		var a := ring[index]
		var b := ring[(index + 1) % ring.size()]
		var direction := b - a
		var inward := Vector2(-direction.y, direction.x).normalized() * signf(area)
		var point := (a + b) * 0.5 + inward * edge_inset_m
		if Geometry2D.is_point_in_polygon(point, ring) and not _near_boundary(point, ring): probes.append(point)
	var triangles := Geometry2D.triangulate_polygon(ring)
	if triangles.size() < 3:
		report.issues.append({"reason": "void-interior-sample-unavailable"})
		return report
	probes.append((ring[triangles[0]] + ring[triangles[1]] + ring[triangles[2]]) / 3.0)
	for point in probes:
		var expected := frame.global_transform * Coordinates.to_godot(Vector3(point.x, point.y, elevation_m))
		var query := PhysicsRayQueryParameters3D.create(expected + normal * distance, expected - normal * distance, floor_mask)
		query.collide_with_areas = false
		var hit := space.intersect_ray(query)
		var sample := {"design_point": [point.x, point.y], "hit": not hit.is_empty(), "hit_object_id": "" if hit.is_empty() else _object_id(hit.get("collider"))}
		if not hit.is_empty(): report.issues.append({"sample": report.samples.size(), "reason": "floor-inside-declared-void"})
		report.samples.append(sample)
	report["physics_frame"] = Engine.get_physics_frames()
	report["tolerance_m"] = tolerance_m
	report["edge_probe_inset_m"] = edge_inset_m
	report["floor_mask"] = floor_mask
	report.complete = true
	report.valid = report.issues.is_empty() and not probes.is_empty()
	return report

static func _object_id(collider: Object) -> String:
	var node := collider as Node
	while node != null:
		if node.has_meta("world_object_id"): return str(node.get_meta("world_object_id"))
		node = node.get_parent()
	return ""

static func _near_boundary(point: Vector2, ring: PackedVector2Array) -> bool:
	for index in range(ring.size()):
		var a := ring[index]
		var edge := ring[(index + 1) % ring.size()] - a
		var amount := clampf((point - a).dot(edge) / edge.length_squared(), 0.0, 1.0)
		if point.distance_squared_to(a + amount * edge) <= BOUNDARY_EPSILON_M * BOUNDARY_EPSILON_M: return true
	return false
