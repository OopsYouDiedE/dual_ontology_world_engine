extends RefCounted
## Structural members generator: Beams and Columns with headroom verification (Task 2.12),
## and ceiling edge sealing to prevent light leaks (Task 2.11).
const Coordinates = preload("res://scenes/components/world/DesignCoordinates.gd")
const PhysicsLayers = preload("res://scenes/components/world/PhysicsLayers.gd")

## Create structural column
static func create_column(id: String, position_xz: Vector2, floor_y: float, height: float, size: Vector2, material: Material = null) -> Node3D:
	if id.is_empty() or not position_xz.is_finite() or not is_finite(floor_y) or not is_finite(height) or not size.is_finite() or height <= 0.0 or size.x <= 0.0 or size.y <= 0.0:
		push_error("StructuralMember3D " + id + ": invalid-column-parameters")
		return null
	var scene = load("res://scenes/components/architecture/StructuralMember3D.tscn")
	var root: Node3D = scene.instantiate()
	root.name = "Column_" + id
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "structural_column")
	root.set_meta("column_size", size)
	root.set_meta("column_height", height)
	root.position = Vector3(position_xz.x, floor_y, position_xz.y)
	
	var mesh_inst: MeshInstance3D = root.get_node("VisualRoot/ColumnMesh")
	var box := BoxMesh.new()
	box.size = Vector3(size.x, height, size.y)
	mesh_inst.mesh = box
	mesh_inst.position = Vector3(0.0, height * 0.5, 0.0)
	if material != null:
		mesh_inst.material_override = material
		
	var col: CollisionShape3D = root.get_node("CollisionRoot/ColumnCollision")
	var col_box := BoxShape3D.new()
	col_box.size = Vector3(size.x, height, size.y)
	col.shape = col_box
	col.position = Vector3(0.0, height * 0.5, 0.0)
	return root

## Create structural beam spanning between two supports
static func create_beam(id: String, start: Vector3, end: Vector3, width: float, depth: float, material: Material = null) -> Node3D:
	if id.is_empty() or not start.is_finite() or not end.is_finite() or not is_finite(width) or not is_finite(depth) or width <= 0.0 or depth <= 0.0:
		push_error("StructuralMember3D " + id + ": invalid-beam-parameters")
		return null
	var span_vec := end - start
	var span_len := span_vec.length()
	if span_len <= 0.001:
		push_error("StructuralMember3D " + id + ": zero-span-beam")
		return null
	
	var root := Node3D.new()
	root.name = "Beam_" + id
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "structural_beam")
	root.set_meta("beam_span", span_len)
	root.set_meta("beam_width", width)
	root.set_meta("beam_depth", depth)
	
	var center := (start + end) * 0.5
	# Beam origin placed at beam center top; underside is at center.y - depth
	root.position = center
	
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	root.add_child(visuals)
	
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.name = "BeamMesh"
	var box := BoxMesh.new()
	box.size = Vector3(width, depth, span_len)
	mesh_inst.mesh = box
	mesh_inst.position = Vector3(0.0, -depth * 0.5, 0.0)
	if material != null:
		mesh_inst.material_override = material
	visuals.add_child(mesh_inst)
	
	var body := StaticBody3D.new()
	body.name = "CollisionRoot"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	root.add_child(body)
	
	var col := CollisionShape3D.new()
	col.name = "BeamCollision"
	var col_box := BoxShape3D.new()
	col_box.size = Vector3(width, depth, span_len)
	col.shape = col_box
	col.position = Vector3(0.0, -depth * 0.5, 0.0)
	body.add_child(col)
	
	# Orient beam along span axis mathematically (scene-tree independent)
	var forward := span_vec.normalized()
	var up := Vector3.UP
	if absf(forward.dot(up)) > 0.99:
		up = Vector3.FORWARD
	var right := forward.cross(up).normalized()
	up = right.cross(forward).normalized()
	root.transform.basis = Basis(right, up, forward)
	
	return root

## Task 2.12: Check whether beam underside satisfies clear height requirement (default 2.0m)
static func check_clear_height(beam: Node3D, floor_elevation: float, required_headroom: float = 2.0) -> Dictionary:
	var depth: float = float(beam.get_meta("beam_depth", 0.3))
	var beam_top_y: float = beam.global_position.y if beam.is_inside_tree() else beam.position.y
	var beam_underside_y := beam_top_y - depth
	var actual_headroom := beam_underside_y - floor_elevation
	var is_ok := actual_headroom >= (required_headroom - 0.001)
	return {
		"ok": is_ok,
		"beam_id": str(beam.get_meta("world_object_id", "")),
		"underside_y": beam_underside_y,
		"floor_elevation": floor_elevation,
		"actual_headroom_m": actual_headroom,
		"required_headroom_m": required_headroom,
		"margin_m": actual_headroom - required_headroom
	}

## Task 2.11: Generate downward perimeter light-seal skirt for ceilings
static func create_ceiling_edge_seal(id: String, outer_polygon: PackedVector2Array, underside_elevation: float, skirt_depth: float = 0.06, material: Material = null) -> Node3D:
	if id.is_empty() or outer_polygon.size() < 3 or not is_finite(underside_elevation) or skirt_depth <= 0.0:
		return null
	var root := Node3D.new()
	root.name = "CeilingEdgeSeal_" + id
	root.set_meta("kind", "ceiling_edge_seal")
	root.set_meta("skirt_depth", skirt_depth)
	
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	root.add_child(visuals)
	
	var count := outer_polygon.size()
	for i in range(count):
		var p1 := outer_polygon[i]
		var p2 := outer_polygon[(i + 1) % count]
		var edge_vec := p2 - p1
		var edge_len := edge_vec.length()
		if edge_len < 0.01: continue
		
		var mid := (p1 + p2) * 0.5
		var dir := edge_vec.normalized()
		
		var part := MeshInstance3D.new()
		part.name = "Skirt_%d" % i
		var box := BoxMesh.new()
		box.size = Vector3(0.04, skirt_depth, edge_len)
		part.mesh = box
		if material != null:
			part.material_override = material
		part.position = Vector3(mid.x, underside_elevation - skirt_depth * 0.5, mid.y)
		part.rotation.y = atan2(-dir.y, dir.x)
		visuals.add_child(part)
		
	return root
