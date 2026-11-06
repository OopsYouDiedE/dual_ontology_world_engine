extends RefCounted
## Closed individual leaves. Their stepped edges and gaps are real geometry.

static func build(dimensions: Vector3, curve_height: float, sheets: int = 18) -> ArrayMesh:
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var colors := PackedColorArray()
	var indices := PackedInt32Array()
	var pitch := dimensions.y / float(sheets)
	for sheet in range(sheets):
		var inset := 0.004 + (0.5 + 0.5 * sin(float(sheet) * 2.399)) * 0.018
		var shape := BoxMesh.new()
		shape.size = Vector3(dimensions.x - inset, pitch * 0.90, dimensions.z - inset * 2.0)
		shape.subdivide_width = 64
		var arrays := shape.get_mesh_arrays()
		var points: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var directions: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
		var source_indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		var offset := vertices.size()
		var center_y := -dimensions.y * 0.5 + pitch * (float(sheet) + 0.5)
		var tint := 0.95 + 0.05 * sin(float(sheet) * 1.73)
		for point_index in points.size():
			var p := points[point_index] + Vector3(0.0, center_y, 0.0)
			var n := directions[point_index]
			var u := (p.x + dimensions.x * 0.5) / dimensions.x
			var layer := (p.y + dimensions.y * 0.5) / dimensions.y
			var slope := curve_height * layer * PI / dimensions.x * cos(u * PI)
			var stretch := 1.0 + curve_height / dimensions.y * sin(u * PI)
			p.y += curve_height * layer * sin(u * PI)
			vertices.append(p)
			normals.append(Vector3(n.x - slope * n.y / stretch, n.y / stretch, n.z).normalized())
			colors.append(Color(tint, 1.0 if absf(n.y) < 0.5 else 0.0, 0.0, 1.0))
		for index in source_indices:
			indices.append(offset + index)
	var result_arrays: Array = []
	result_arrays.resize(Mesh.ARRAY_MAX)
	result_arrays[Mesh.ARRAY_VERTEX] = vertices
	result_arrays[Mesh.ARRAY_NORMAL] = normals
	result_arrays[Mesh.ARRAY_COLOR] = colors
	result_arrays[Mesh.ARRAY_INDEX] = indices
	var result := ArrayMesh.new()
	result.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, result_arrays)
	return result
