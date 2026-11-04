extends RefCounted
## Wall endpoints and openings are explicit design coordinates in metres.
## Opening offsets are measured from start along the centre line. End cuts are
## signed offsets along that line at the left/right wall faces, respectively.
const Coordinates = preload("res://scenes/components/world/DesignCoordinates.gd")
const PhysicsLayers = preload("res://scenes/components/world/PhysicsLayers.gd")
const EPS := 0.000001

static func create(id: String, start: Vector3, end: Vector3, thickness: float, height: float, openings: Array, start_cut: Vector2, end_cut: Vector2, material: Material) -> Node3D:
	var error := validate(id, start, end, thickness, height, openings, start_cut, end_cut)
	if not error.is_empty():
		push_error("WallSurface3D " + id + ": " + error)
		return null
	var direction := Vector2(end.x-start.x, end.y-start.y).normalized()
	var left := Vector2(-direction.y, direction.x)
	var length := Vector2(end.x-start.x, end.y-start.y).length()
	var xs: Array[float] = [0.0, length]
	var zs: Array[float] = [0.0, height]
	for hole in openings:
		for x in [float(hole.from), float(hole.to)]:
			_append_level(xs,x)
		for z in [float(hole.bottom), float(hole.top)]:
			_append_level(zs,z)
	xs.sort()
	zs.sort()
	var solid := []
	for i in range(xs.size()-1):
		var column := []
		for j in range(zs.size()-1):
			var x := (xs[i]+xs[i+1])*0.5
			var z := (zs[j]+zs[j+1])*0.5
			var occupied := true
			for hole in openings:
				if x > float(hole.from) and x < float(hole.to) and z > float(hole.bottom) and z < float(hole.top): occupied = false
			column.append(occupied)
		solid.append(column)
	var faces := PackedVector3Array()
	var normals := PackedVector3Array()
	var along := Coordinates.to_godot(Vector3(direction.x,direction.y,0.0))
	var across := Coordinates.to_godot(Vector3(left.x,left.y,0.0))
	for i in range(xs.size()-1):
		for j in range(zs.size()-1):
			if not solid[i][j]: continue
			var a := xs[i]
			var b := xs[i+1]
			var lo := zs[j]
			var hi := zs[j+1]
			var p0 := _point(start,direction,left,length,a,-thickness/2.0,lo,start_cut,end_cut)
			var p1 := _point(start,direction,left,length,b,-thickness/2.0,lo,start_cut,end_cut)
			var p2 := _point(start,direction,left,length,b,thickness/2.0,lo,start_cut,end_cut)
			var p3 := _point(start,direction,left,length,a,thickness/2.0,lo,start_cut,end_cut)
			var q0 := _point(start,direction,left,length,a,-thickness/2.0,hi,start_cut,end_cut)
			var q1 := _point(start,direction,left,length,b,-thickness/2.0,hi,start_cut,end_cut)
			var q2 := _point(start,direction,left,length,b,thickness/2.0,hi,start_cut,end_cut)
			var q3 := _point(start,direction,left,length,a,thickness/2.0,hi,start_cut,end_cut)
			_quad(faces,normals,p0,p1,q1,q0,-across)
			_quad(faces,normals,p2,p3,q3,q2,across)
			if i == 0 or not solid[i-1][j]: _quad(faces,normals,p3,p0,q0,q3,-along)
			if i == xs.size()-2 or not solid[i+1][j]: _quad(faces,normals,p1,p2,q2,q1,along)
			if j == 0 or not solid[i][j-1]: _quad(faces,normals,p3,p2,p1,p0,Vector3.DOWN)
			if j == zs.size()-2 or not solid[i][j+1]: _quad(faces,normals,q0,q1,q2,q3,Vector3.UP)
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = faces
	arrays[Mesh.ARRAY_NORMAL] = normals
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var root := Node3D.new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id", id)
	var visual := MeshInstance3D.new()
	visual.name = "VisualRoot"
	visual.mesh = mesh
	visual.material_override = material
	visual.set_meta("world_object_id", id)
	root.add_child(visual)
	var collision_root := Node3D.new()
	collision_root.name = "CollisionRoot"
	root.add_child(collision_root)
	var body := StaticBody3D.new()
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	collision_root.add_child(body)
	var collision := CollisionShape3D.new()
	var shape := ConcavePolygonShape3D.new()
	shape.set_faces(faces)
	collision.shape = shape
	body.add_child(collision)
	return root

static func _append_level(levels: Array[float], value: float) -> void:
	for existing: float in levels:
		if absf(existing-value) <= EPS: return
	levels.append(value)

static func _point(start: Vector3, direction: Vector2, left: Vector2, length: float, x: float, depth: float, z: float, start_cut: Vector2, end_cut: Vector2) -> Vector3:
	var offset := 0.0
	if absf(x) < EPS: offset = start_cut.x if depth > 0.0 else start_cut.y
	elif absf(x-length) < EPS: offset = end_cut.x if depth > 0.0 else end_cut.y
	var plan := Vector2(start.x,start.y) + direction*(x+offset) + left*depth
	return Coordinates.to_godot(Vector3(plan.x,plan.y,start.z+z))

static func _quad(faces: PackedVector3Array, normals: PackedVector3Array, a: Vector3, b: Vector3, c: Vector3, d: Vector3, outward: Vector3) -> void:
	var normal := -(b-a).cross(c-a).normalized()
	if normal.dot(outward) < 0.0:
		var swap := b
		b = d
		d = swap
		normal = -normal
	faces.append_array(PackedVector3Array([a,b,c,a,c,d]))
	normals.append_array(PackedVector3Array([normal,normal,normal,normal,normal,normal]))

static func validate(id: String, start: Vector3, end: Vector3, thickness: float, height: float, openings: Array, start_cut: Vector2, end_cut: Vector2) -> String:
	if id.is_empty(): return "missing-id"
	if not start.is_finite() or not end.is_finite() or not start_cut.is_finite() or not end_cut.is_finite() or not is_finite(thickness) or not is_finite(height): return "non-finite-parameter"
	var length := Vector2(end.x-start.x,end.y-start.y).length()
	if length <= EPS or absf(end.z-start.z) > EPS or thickness <= EPS or height <= EPS: return "invalid-wall"
	if maxf(start_cut.x,start_cut.y) >= length + minf(end_cut.x,end_cut.y): return "overlapping-end-cuts"
	var ids := {}
	for index in range(openings.size()):
		var hole = openings[index]
		if not hole is Dictionary: return "opening[%s]: expected-record" % index
		if not hole.has("id") or not hole.id is String or hole.id.is_empty(): return "opening[%s].id: expected-nonempty-string" % index
		var opening_id: String = hole.id
		if ids.has(opening_id): return "opening[%s].id: duplicate" % opening_id
		ids[opening_id] = true
		for field in ["from","to","bottom","top"]:
			if not hole.has(field): return "opening[%s].%s: missing" % [opening_id,field]
			if typeof(hole[field]) != TYPE_INT and typeof(hole[field]) != TYPE_FLOAT: return "opening[%s].%s: expected-number" % [opening_id,field]
			if not is_finite(hole[field]): return "opening[%s].%s: non-finite" % [opening_id,field]
		var a: float = hole.from
		var b: float = hole.to
		var low: float = hole.bottom
		var high: float = hole.top
		if a < 0.0 or a >= length: return "opening[%s].from: outside-wall" % opening_id
		if b > length or b-a <= EPS: return "opening[%s].to: outside-wall-or-empty" % opening_id
		if low < 0.0 or low >= height: return "opening[%s].bottom: outside-wall" % opening_id
		if high > height or high-low <= EPS: return "opening[%s].top: outside-wall-or-empty" % opening_id
		if a <= maxf(start_cut.x,start_cut.y)+EPS: return "opening[%s].from: intersects-start-cut" % opening_id
		if b >= length+minf(end_cut.x,end_cut.y)-EPS: return "opening[%s].to: intersects-end-cut" % opening_id
	for i in range(openings.size()):
		var hole: Dictionary = openings[i]
		for j in range(i+1,openings.size()):
			var other: Dictionary = openings[j]
			if hole.from < other.to-EPS and hole.to > other.from+EPS and hole.bottom < other.top-EPS and hole.top > other.bottom+EPS: return "opening[%s]: overlaps-opening[%s]" % [hole.id,other.id]
	return ""

## Helper to construct a validated window opening spec (Task 2.8)
## Window openings declare sill_height, net_width, net_height, and ensure bottom >= sill_height
static func create_window_spec(id: String, from_x: float, width: float, sill_height: float, height: float) -> Dictionary:
	return {
		"id": id,
		"kind": "window",
		"from": from_x,
		"to": from_x + width,
		"bottom": sill_height,
		"top": sill_height + height,
		"is_window": true
	}

## Helper to construct a validated door opening spec (Task 2.7)
static func create_door_spec(id: String, from_x: float, width: float, height: float) -> Dictionary:
	return {
		"id": id,
		"kind": "door",
		"from": from_x,
		"to": from_x + width,
		"bottom": 0.0,
		"top": height,
		"is_window": false
	}



