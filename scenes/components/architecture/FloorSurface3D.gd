extends RefCounted
const PhysicsLayers = preload("res://scenes/components/world/PhysicsLayers.gd")
## Generic metre-scale slab. Input rings use design X east / Y north.
## Horizontal strips preserve concavities and holes without a fan triangulation.
const Coordinates = preload("res://scenes/components/world/DesignCoordinates.gd")
const EPSILON_M := 0.0000001

static func create(object_id: String, outer: PackedVector2Array, holes: Array[PackedVector2Array], elevation_m: float, thickness_m: float, material: Material) -> Node3D:
	var error := validate(outer, holes, elevation_m, thickness_m)
	if object_id.is_empty(): error = "missing-object-id"
	if not error.is_empty():
		push_error("FloorSurface3D " + object_id + ": " + error)
		return null
	var rings: Array[PackedVector2Array] = [outer]
	rings.append_array(holes)
	var levels: Array[float] = []
	for ring in rings:
		for point in ring:
			if not levels.has(point.y): levels.append(point.y)
	levels.sort()
	var faces := PackedVector3Array()
	var normals := PackedVector3Array()
	for band in range(levels.size() - 1):
		var low := levels[band]
		var high := levels[band + 1]
		var middle := (low + high) * 0.5
		var crossings: Array[Vector3] = []
		for ring in rings:
			for edge in range(ring.size()):
				var a := ring[edge]
				var b := ring[(edge + 1) % ring.size()]
				if middle <= minf(a.y, b.y) or middle >= maxf(a.y, b.y): continue
				crossings.append(Vector3(_edge_x(a, b, low), _edge_x(a, b, high), _edge_x(a, b, middle)))
		crossings.sort_custom(func(a: Vector3, b: Vector3) -> bool: return a.z < b.z)
		if crossings.size() % 2 != 0:
			push_error("FloorSurface3D " + object_id + ": odd intersection count")
			return null
		for span in range(0, crossings.size(), 2):
			var left := crossings[span]
			var right := crossings[span + 1]
			var polygon: Array[Vector2] = [Vector2(left.x, low), Vector2(right.x, low), Vector2(right.y, high), Vector2(left.y, high)]
			for layer in range(2):
				var height := elevation_m if layer == 0 else elevation_m - thickness_m
				var normal := Vector3.UP if layer == 0 else Vector3.DOWN
				var a := _point(polygon[0], height)
				var b := _point(polygon[1], height)
				var c := _point(polygon[2], height)
				var d := _point(polygon[3], height)
				_triangle(faces, normals, a, b, c, normal)
				_triangle(faces, normals, a, c, d, normal)
	for ring_index in range(rings.size()):
		var ring := rings[ring_index]
		var orientation := signf(_signed_area(ring)) * (1.0 if ring_index == 0 else -1.0)
		for edge in range(ring.size()):
			var a := ring[edge]
			var b := ring[(edge + 1) % ring.size()]
			var direction := b - a
			var normal := Vector3(direction.y, 0.0, direction.x).normalized() * orientation
			var top_a := _point(a, elevation_m)
			var top_b := _point(b, elevation_m)
			var bottom_a := _point(a, elevation_m - thickness_m)
			var bottom_b := _point(b, elevation_m - thickness_m)
			_triangle(faces, normals, top_a, bottom_a, bottom_b, normal)
			_triangle(faces, normals, top_a, bottom_b, top_b, normal)
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = faces
	arrays[Mesh.ARRAY_NORMAL] = normals
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var root := Node3D.new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id", object_id)
	var visual := MeshInstance3D.new()
	visual.name = "VisualRoot"
	visual.mesh = mesh
	visual.material_override = material
	visual.set_meta("world_object_id", object_id)
	root.add_child(visual)
	var collision_root := Node3D.new()
	collision_root.name = "CollisionRoot"
	root.add_child(collision_root)
	var body := StaticBody3D.new()
	body.name = "StaticBody3D"
	body.collision_layer = PhysicsLayers.SOLID | PhysicsLayers.FLOOR
	body.collision_mask = 0
	collision_root.add_child(body)
	var collision := CollisionShape3D.new()
	collision.name = "CollisionShape3D"
	var shape := ConcavePolygonShape3D.new()
	shape.backface_collision = false
	shape.set_faces(faces)
	collision.shape = shape
	body.add_child(collision)
	return root

static func _edge_x(a: Vector2, b: Vector2, y: float) -> float:
	return a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y)

static func _point(point: Vector2, height: float) -> Vector3:
	return Coordinates.to_godot(Vector3(point.x, point.y, height))

static func _triangle(faces: PackedVector3Array, normals: PackedVector3Array, a: Vector3, b: Vector3, c: Vector3, desired: Vector3) -> void:
	var cross := (b - a).cross(c - a)
	# At a strip endpoint a trapezoid may narrow to a triangle. Do not emit
	# the collapsed half as a zero-area mesh or collision face.
	if cross.length_squared() <= 1.0e-20: return
	if (-cross).dot(desired) < 0.0:
		var swap := b
		b = c
		c = swap
	faces.append_array(PackedVector3Array([a, b, c]))
	normals.append_array(PackedVector3Array([desired, desired, desired]))

static func _signed_area(ring: PackedVector2Array) -> float:
	var area := 0.0
	for index in range(ring.size()): area += ring[index].cross(ring[(index + 1) % ring.size()])
	return area * 0.5

static func _on_segment(point: Vector2, a: Vector2, b: Vector2) -> bool:
	return absf((b - a).cross(point - a)) <= EPSILON_M * (b - a).length() and point.x >= minf(a.x, b.x) - EPSILON_M and point.x <= maxf(a.x, b.x) + EPSILON_M and point.y >= minf(a.y, b.y) - EPSILON_M and point.y <= maxf(a.y, b.y) + EPSILON_M

static func _segments_touch(a: Vector2, b: Vector2, c: Vector2, d: Vector2) -> bool:
	if _on_segment(a, c, d) or _on_segment(b, c, d) or _on_segment(c, a, b) or _on_segment(d, a, b): return true
	return signf((b - a).cross(c - a)) != signf((b - a).cross(d - a)) and signf((d - c).cross(a - c)) != signf((d - c).cross(b - c))

static func validate(outer: PackedVector2Array, holes: Array[PackedVector2Array], elevation_m: float, thickness_m: float) -> String:
	if not is_finite(elevation_m) or not is_finite(thickness_m) or thickness_m <= 0.0: return "invalid-elevation-or-thickness"
	var rings: Array[PackedVector2Array] = [outer]
	rings.append_array(holes)
	for ring_index in range(rings.size()):
		var ring := rings[ring_index]
		if ring.size() < 3: return "ring-too-short:" + str(ring_index)
		for point in ring:
			if not point.is_finite(): return "non-finite-ring:" + str(ring_index)
		if absf(_signed_area(ring)) <= EPSILON_M * EPSILON_M: return "zero-area-ring:" + str(ring_index)
		for edge in range(ring.size()):
			var a := ring[edge]
			var b := ring[(edge + 1) % ring.size()]
			if a.distance_squared_to(b) <= EPSILON_M * EPSILON_M: return "zero-length-edge:" + str(ring_index)
			for other in range(edge + 1, ring.size()):
				if other == edge + 1 or (edge == 0 and other == ring.size() - 1): continue
				if _segments_touch(a, b, ring[other], ring[(other + 1) % ring.size()]): return "self-intersecting-ring:" + str(ring_index)
		if ring_index > 0 and not Geometry2D.is_point_in_polygon(ring[0], outer): return "hole-outside-floor:" + str(ring_index)
		for other_ring_index in range(ring_index):
			var other_ring := rings[other_ring_index]
			for edge in range(ring.size()):
				for other in range(other_ring.size()):
					if _segments_touch(ring[edge], ring[(edge + 1) % ring.size()], other_ring[other], other_ring[(other + 1) % other_ring.size()]): return "intersecting-rings:%s:%s" % [ring_index, other_ring_index]
			if other_ring_index > 0 and (Geometry2D.is_point_in_polygon(ring[0], other_ring) or Geometry2D.is_point_in_polygon(other_ring[0], ring)): return "overlapping-or-nested-holes"
	return ""
