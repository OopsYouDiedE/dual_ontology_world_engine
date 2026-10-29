extends RefCounted
## Task 2.24: Exterior Roof and Enclosing Surface Generator (屋顶与外部封闭面).
## Generates building exterior roof slabs and weather-sealing eaves based on the exterior wall perimeter.
## Manages visibility and shadow-casting independently from interior ceilings, ensuring zero light leaks.
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")

## Creates an exterior roof slab with an overhang eave and downward fascia skirt.
## - perimeter: 2D polygon vertices of the exterior wall footprint (top floor).
## - elevation_m: Top elevation of the roof slab.
## - thickness_m: Structural thickness of the roof slab (default 0.25m).
## - overhang_m: Eave projection beyond the wall centerline (default 0.35m, guarantees >= wall_thickness/2).
## - fascia_skirt_depth_m: Downward projection of the eaves to seal light at exterior wall joints (default 0.20m).
## - material: Visual material for the roof surface.
static func create_roof(
	id: String,
	perimeter: PackedVector2Array,
	elevation_m: float,
	thickness_m: float = 0.25,
	overhang_m: float = 0.35,
	fascia_skirt_depth_m: float = 0.20,
	material: Material = null
) -> Node3D:
	if id.is_empty() or perimeter.size() < 3 or not is_finite(elevation_m) or thickness_m <= 0.0 or overhang_m < 0.0:
		push_error("ExteriorRoof3D " + id + ": invalid roof parameters")
		return null
	
	var root := Node3D.new()
	root.name = "ExteriorRoof_" + id
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "roof")
	root.set_meta("elevation_m", elevation_m)
	root.set_meta("thickness_m", thickness_m)
	root.set_meta("overhang_m", overhang_m)
	root.set_meta("fascia_skirt_depth_m", fascia_skirt_depth_m)
	root.set_meta("roof_visible", true)
	
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	root.add_child(visuals)
	
	var col_root := Node3D.new()
	col_root.name = "CollisionRoot"
	root.add_child(col_root)
	
	var body := StaticBody3D.new()
	body.name = "StaticBody3D"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	col_root.add_child(body)
	
	# 1. Expand perimeter outward by overhang_m to form the eave outline
	var eave_perimeter := _expand_polygon(perimeter, overhang_m)
	
	# 2. Build roof top slab mesh and collision
	_build_roof_slab(visuals, body, eave_perimeter, elevation_m, thickness_m, material)
	
	# 3. Build downward fascia eave skirt (prevents horizontal light leaks through wall-roof gap)
	if fascia_skirt_depth_m > 0.0:
		_build_fascia_skirt(visuals, body, eave_perimeter, elevation_m - thickness_m, fascia_skirt_depth_m, material)
		
	return root

## Toggles visual visibility of the roof while keeping shadow casting and collision active.
static func set_roof_visible(root: Node3D, visible: bool) -> void:
	if not is_instance_valid(root):
		return
	root.set_meta("roof_visible", visible)
	var visuals := root.get_node_or_null("VisualRoot") as Node3D
	if visuals != null:
		visuals.visible = visible

## Configures the roof for cutaway mode: keeps shadows and collision but hides direct visual rendering.
static func set_roof_cutaway(root: Node3D, cutaway: bool) -> void:
	if not is_instance_valid(root):
		return
	var visuals := root.get_node_or_null("VisualRoot") as Node3D
	if visuals == null:
		return
	for child in visuals.get_children():
		if child is MeshInstance3D:
			if cutaway:
				child.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_SHADOWS_ONLY
				child.visible = false
			else:
				child.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
				child.visible = true

## Inspects the light-seal integrity of the roof over an exterior wall contour.
static func inspect_light_seal(root: Node3D, wall_perimeter: PackedVector2Array, wall_thickness_m: float) -> Dictionary:
	if not is_instance_valid(root):
		return {"valid": false, "error": "missing-root"}
	var overhang: float = float(root.get_meta("overhang_m", 0.0))
	var skirt_depth: float = float(root.get_meta("fascia_skirt_depth_m", 0.0))
	var col_root := root.get_node_or_null("CollisionRoot/StaticBody3D") as StaticBody3D
	var has_collision := col_root != null and col_root.get_child_count() > 0
	
	# Overhang must cover at least half the wall thickness (wall centerline to exterior face)
	var min_required_overhang := wall_thickness_m * 0.5
	var overhang_sufficient := overhang >= (min_required_overhang - 0.001)
	var skirt_sufficient := skirt_depth >= 0.10
	
	var is_sealed := overhang_sufficient and skirt_sufficient and has_collision
	return {
		"valid": is_sealed,
		"overhang_m": overhang,
		"min_required_overhang_m": min_required_overhang,
		"overhang_sufficient": overhang_sufficient,
		"fascia_skirt_depth_m": skirt_depth,
		"skirt_sufficient": skirt_sufficient,
		"has_solid_collision": has_collision,
		"zero_light_leak": is_sealed
	}

# -------------------------------------------------------------------------
# Internal Mesh & Collision Builders
# -------------------------------------------------------------------------

static func _expand_polygon(poly: PackedVector2Array, offset: float) -> PackedVector2Array:
	if offset <= 0.0001:
		return poly
	var res := PackedVector2Array()
	var n := poly.size()
	for i in range(n):
		var p_prev := poly[(i - 1 + n) % n]
		var p_curr := poly[i]
		var p_next := poly[(i + 1) % n]
		
		var v1 := (p_curr - p_prev).normalized()
		var v2 := (p_next - p_curr).normalized()
		
		var norm1 := Vector2(-v1.y, v1.x)
		var norm2 := Vector2(-v2.y, v2.x)
		var avg_norm := (norm1 + norm2).normalized()
		
		var dot := avg_norm.dot(norm1)
		var scale_dist := offset / maxf(dot, 0.3)
		res.append(p_curr + avg_norm * scale_dist)
	return res

static func _build_roof_slab(visuals: Node3D, body: StaticBody3D, poly: PackedVector2Array, elevation_m: float, thickness_m: float, material: Material) -> void:
	var triangles := Geometry2D.triangulate_polygon(poly)
	if triangles.is_empty():
		push_error("ExteriorRoof3D: triangulation failed for eave polygon")
		return
		
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_smooth_group(0)
	
	var top_y := elevation_m
	var btm_y := elevation_m - thickness_m
	
	# Top surface (normal UP)
	for i in range(0, triangles.size(), 3):
		var p0 := poly[triangles[i]]
		var p1 := poly[triangles[i + 1]]
		var p2 := poly[triangles[i + 2]]
		var v0 := Vector3(p0.x, top_y, p0.y)
		var v1 := Vector3(p1.x, top_y, p1.y)
		var v2 := Vector3(p2.x, top_y, p2.y)
		if (-(v1 - v0).cross(v2 - v0)).dot(Vector3.UP) < 0.0:
			var tmp := v1
			v1 = v2
			v2 = tmp
		st.set_normal(Vector3.UP)
		st.set_uv(Vector2(v0.x, v0.z))
		st.add_vertex(v0)
		st.set_normal(Vector3.UP)
		st.set_uv(Vector2(v1.x, v1.z))
		st.add_vertex(v1)
		st.set_normal(Vector3.UP)
		st.set_uv(Vector2(v2.x, v2.z))
		st.add_vertex(v2)
	
	# Bottom surface (normal DOWN)
	for i in range(0, triangles.size(), 3):
		var p0 := poly[triangles[i]]
		var p1 := poly[triangles[i + 1]]
		var p2 := poly[triangles[i + 2]]
		var v0 := Vector3(p0.x, btm_y, p0.y)
		var v1 := Vector3(p1.x, btm_y, p1.y)
		var v2 := Vector3(p2.x, btm_y, p2.y)
		if (-(v1 - v0).cross(v2 - v0)).dot(Vector3.DOWN) < 0.0:
			var tmp := v1
			v1 = v2
			v2 = tmp
		st.set_normal(Vector3.DOWN)
		st.set_uv(Vector2(v0.x, v0.z))
		st.add_vertex(v0)
		st.set_normal(Vector3.DOWN)
		st.set_uv(Vector2(v1.x, v1.z))
		st.add_vertex(v1)
		st.set_normal(Vector3.DOWN)
		st.set_uv(Vector2(v2.x, v2.z))
		st.add_vertex(v2)
		
	# Side edge faces
	var n := poly.size()
	for i in range(n):
		var p_curr := poly[i]
		var p_next := poly[(i + 1) % n]
		var edge_dir := (p_next - p_curr).normalized()
		var out_norm := Vector3(-edge_dir.y, 0, edge_dir.x)
		
		var v0 := Vector3(p_curr.x, top_y, p_curr.y)
		var v1 := Vector3(p_next.x, top_y, p_next.y)
		var v2 := Vector3(p_next.x, btm_y, p_next.y)
		var v3 := Vector3(p_curr.x, btm_y, p_curr.y)
		
		var t1_0 := v0
		var t1_1 := v1
		var t1_2 := v2
		if (-(t1_1 - t1_0).cross(t1_2 - t1_0)).dot(out_norm) < 0.0:
			var tmp := t1_1
			t1_1 = t1_2
			t1_2 = tmp
		st.set_normal(out_norm)
		st.add_vertex(t1_0)
		st.set_normal(out_norm)
		st.add_vertex(t1_1)
		st.set_normal(out_norm)
		st.add_vertex(t1_2)
		
		var t2_0 := v0
		var t2_1 := v2
		var t2_2 := v3
		if (-(t2_1 - t2_0).cross(t2_2 - t2_0)).dot(out_norm) < 0.0:
			var tmp := t2_1
			t2_1 = t2_2
			t2_2 = tmp
		st.set_normal(out_norm)
		st.add_vertex(t2_0)
		st.set_normal(out_norm)
		st.add_vertex(t2_1)
		st.set_normal(out_norm)
		st.add_vertex(t2_2)
		
	var mesh := st.commit()
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.name = "RoofSlabMesh"
	mesh_inst.mesh = mesh
	mesh_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	if material != null:
		mesh_inst.material_override = material
	visuals.add_child(mesh_inst)
	
	# Solid collision shape using concave or multiple convex shapes
	var shape := mesh.create_trimesh_shape()
	var col := CollisionShape3D.new()
	col.name = "RoofSlabCollision"
	col.shape = shape
	body.add_child(col)

static func _build_fascia_skirt(visuals: Node3D, body: StaticBody3D, poly: PackedVector2Array, top_y: float, skirt_depth: float, material: Material) -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	
	var btm_y := top_y - skirt_depth
	var n := poly.size()
	for i in range(n):
		var p_curr := poly[i]
		var p_next := poly[(i + 1) % n]
		var edge_dir := (p_next - p_curr).normalized()
		var out_norm := Vector3(-edge_dir.y, 0, edge_dir.x)
		
		var v0 := Vector3(p_curr.x, top_y, p_curr.y)
		var v1 := Vector3(p_next.x, top_y, p_next.y)
		var v2 := Vector3(p_next.x, btm_y, p_next.y)
		var v3 := Vector3(p_curr.x, btm_y, p_curr.y)
		
		var t1_0 := v0
		var t1_1 := v1
		var t1_2 := v2
		if (-(t1_1 - t1_0).cross(t1_2 - t1_0)).dot(out_norm) < 0.0:
			var tmp := t1_1
			t1_1 = t1_2
			t1_2 = tmp
		st.set_normal(out_norm)
		st.add_vertex(t1_0)
		st.set_normal(out_norm)
		st.add_vertex(t1_1)
		st.set_normal(out_norm)
		st.add_vertex(t1_2)
		
		var t2_0 := v0
		var t2_1 := v2
		var t2_2 := v3
		if (-(t2_1 - t2_0).cross(t2_2 - t2_0)).dot(out_norm) < 0.0:
			var tmp := t2_1
			t2_1 = t2_2
			t2_2 = tmp
		st.set_normal(out_norm)
		st.add_vertex(t2_0)
		st.set_normal(out_norm)
		st.add_vertex(t2_1)
		st.set_normal(out_norm)
		st.add_vertex(t2_2)
		
	var mesh := st.commit()
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.name = "FasciaSkirtMesh"
	mesh_inst.mesh = mesh
	mesh_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	if material != null:
		mesh_inst.material_override = material
	visuals.add_child(mesh_inst)
	
	var shape := mesh.create_trimesh_shape()
	var col := CollisionShape3D.new()
	col.name = "FasciaSkirtCollision"
	col.shape = shape
	body.add_child(col)
