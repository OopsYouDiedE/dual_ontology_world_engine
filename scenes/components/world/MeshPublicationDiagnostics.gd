extends RefCounted
## Read-only diagnostics. Call before publishing generated geometry.
## Godot triangle fronts use clockwise winding. This does not infer whether a
## closed mesh represents an interior or exterior; the generator owns that rule.

const AREA_EPSILON_SQUARED := 1.0e-16
const NORMAL_EPSILON_SQUARED := 1.0e-12
const BOUNDS_TOLERANCE_M := 0.0001

static func inspect(root: Node3D) -> Dictionary:
	var report := {"valid": true, "mesh_count": 0, "triangle_count": 0, "issues": []}
	if not is_instance_valid(root):
		_issue(report, "", -1, -1, "missing-root")
		return report
	_visit(root, str(root.name), Transform3D.IDENTITY, report)
	return report

static func _issue(report: Dictionary, object_id: String, surface: int, face: int, reason: String) -> void:
	report.valid = false
	report.issues.append({"object_id": object_id, "surface": surface, "face": face, "reason": reason})

static func _visit(node: Node3D, path: String, parent_transform: Transform3D, report: Dictionary, inherited_id: String = "") -> void:
	var transform := parent_transform * node.transform
	var declared_id := str(node.get_meta("world_object_id", ""))
	if declared_id.is_empty(): declared_id = str(node.get_meta("spatial_id", ""))
	var active_id := declared_id if not declared_id.is_empty() else inherited_id
	var object_id := active_id if not active_id.is_empty() else path
	if not transform.origin.is_finite() or not transform.basis.x.is_finite() or not transform.basis.y.is_finite() or not transform.basis.z.is_finite():
		_issue(report, object_id, -1, -1, "non-finite-transform")
	elif absf(transform.basis.determinant()) < 1.0e-12:
		_issue(report, object_id, -1, -1, "singular-transform")
	if node.scale.x < 0.0 or node.scale.y < 0.0 or node.scale.z < 0.0:
		_issue(report, object_id, -1, -1, "negative-local-scale")
	if node is MeshInstance3D:
		_inspect_mesh(node, object_id, report)
	for child in node.get_children():
		if child is Node3D:
			_visit(child, path + "/" + str(child.name), transform, report, active_id)

static func _inspect_mesh(instance: MeshInstance3D, object_id: String, report: Dictionary) -> void:
	var mesh := instance.mesh
	if mesh == null:
		_issue(report, object_id, -1, -1, "missing-mesh")
		return
	report.mesh_count += 1
	var bounds := mesh.get_aabb()
	if not bounds.position.is_finite() or not bounds.size.is_finite() or bounds.size.x < 0.0 or bounds.size.y < 0.0 or bounds.size.z < 0.0:
		_issue(report, object_id, -1, -1, "invalid-bounds")
		return
	if mesh.get_surface_count() == 0:
		_issue(report, object_id, -1, -1, "empty-mesh")
	for surface in range(mesh.get_surface_count()):
		if mesh is ArrayMesh and mesh.surface_get_primitive_type(surface) != Mesh.PRIMITIVE_TRIANGLES:
			_issue(report, object_id, surface, -1, "unsupported-non-triangle-surface")
			continue
		var arrays := mesh.surface_get_arrays(surface)
		if arrays.size() != Mesh.ARRAY_MAX or not arrays[Mesh.ARRAY_VERTEX] is PackedVector3Array:
			_issue(report, object_id, surface, -1, "missing-vertex-array")
			continue
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var normals := PackedVector3Array()
		if arrays[Mesh.ARRAY_NORMAL] is PackedVector3Array:
			normals = arrays[Mesh.ARRAY_NORMAL]
		var indices := PackedInt32Array()
		if arrays[Mesh.ARRAY_INDEX] is PackedInt32Array:
			indices = arrays[Mesh.ARRAY_INDEX]
		var element_count := indices.size() if not indices.is_empty() else vertices.size()
		if element_count == 0 or element_count % 3 != 0:
			_issue(report, object_id, surface, -1, "incomplete-triangle-array")
			continue
		var has_normals := normals.size() == vertices.size()
		if not has_normals:
			_issue(report, object_id, surface, -1, "missing-or-mismatched-normals")
		for vertex_index in range(vertices.size()):
			if not vertices[vertex_index].is_finite():
				_issue(report, object_id, surface, -1, "non-finite-vertex:" + str(vertex_index))
			elif not bounds.grow(BOUNDS_TOLERANCE_M).has_point(vertices[vertex_index]):
				_issue(report, object_id, surface, -1, "vertex-outside-bounds:" + str(vertex_index))
			if has_normals and (not normals[vertex_index].is_finite() or normals[vertex_index].length_squared() < NORMAL_EPSILON_SQUARED):
				_issue(report, object_id, surface, -1, "invalid-normal:" + str(vertex_index))
		for offset in range(0, element_count, 3):
			var ids: Array[int] = [offset, offset + 1, offset + 2]
			if not indices.is_empty():
				ids = [indices[offset], indices[offset + 1], indices[offset + 2]]
			var face := offset / 3
			if ids.any(func(index: int) -> bool: return index < 0 or index >= vertices.size()):
				_issue(report, object_id, surface, face, "index-out-of-range")
				continue
			report.triangle_count += 1
			var a := vertices[ids[0]]
			var b := vertices[ids[1]]
			var c := vertices[ids[2]]
			if not a.is_finite() or not b.is_finite() or not c.is_finite():
				continue
			var cross := (b - a).cross(c - a)
			if cross.length_squared() <= AREA_EPSILON_SQUARED:
				_issue(report, object_id, surface, face, "degenerate-triangle")
			elif has_normals:
				var mean_normal := normals[ids[0]] + normals[ids[1]] + normals[ids[2]]
				if mean_normal.is_finite() and mean_normal.length_squared() > NORMAL_EPSILON_SQUARED and (-cross).dot(mean_normal) < 0.0:
					_issue(report, object_id, surface, face, "normal-opposes-front-winding")

## Formats issues into human-readable messages (Task 4.04).
static func format_report(report: Dictionary) -> String:
	if bool(report.get("valid", false)):
		return "Valid: %d meshes, %d triangles inspected, 0 issues." % [int(report.get("mesh_count", 0)), int(report.get("triangle_count", 0))]
	var lines: Array[String] = ["Mesh publication diagnostics detected %d issue(s):" % report.issues.size()]
	for issue in report.issues:
		lines.append(" - Object '%s', Surface %d, Face %d: %s" % [issue.object_id, issue.surface, issue.face, issue.reason])
	return "\n".join(lines)

## Validates the scene tree mesh geometry and raises errors on any defect (Task 4.04).
static func assert_valid(root: Node3D) -> Dictionary:
	var report := inspect(root)
	if not bool(report.get("valid", false)):
		push_error(format_report(report))
	return report

