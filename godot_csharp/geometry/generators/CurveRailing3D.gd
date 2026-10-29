extends RefCounted
## Curve points and handles use design coordinates. Posts stay vertical.
## Curve3D baked lengths determine equal-arc post spacing per control span.
const Coordinates = preload("res://geometry/DesignCoordinates.gd")
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")

static func create(id: String, design_path: Curve3D, max_post_spacing: float, post_radius: float, rail_radius: float, guard_thickness: float, material: Material, arc_tolerance_m: float, control_heights: Array, own_start_post: bool, own_end_post: bool, support: Dictionary, control_post_heights: Array = [], start_guard_cut_m: Vector2 = Vector2.ZERO, end_guard_cut_m: Vector2 = Vector2.ZERO) -> Node3D:
	if id.is_empty() or design_path == null or design_path.point_count < 2 or not is_finite(max_post_spacing) or not is_finite(post_radius) or not is_finite(rail_radius) or not is_finite(guard_thickness) or max_post_spacing <= 0.0 or post_radius <= 0.0 or rail_radius <= 0.0 or guard_thickness <= 0.0 or not is_finite(arc_tolerance_m) or arc_tolerance_m <= 0.0 or not start_guard_cut_m.is_finite() or not end_guard_cut_m.is_finite():
		push_error("CurveRailing3D " + id + ": invalid-parameters")
		return null
	if control_heights.size() != design_path.point_count:
		push_error("CurveRailing3D " + id + ": height-count-mismatch")
		return null
	for value in control_heights:
		if (typeof(value) != TYPE_INT and typeof(value) != TYPE_FLOAT) or not is_finite(value) or value <= 0.0:
			push_error("CurveRailing3D " + id + ": invalid-control-height")
			return null
	if not control_post_heights.is_empty():
		if control_post_heights.size() != design_path.point_count:
			push_error("CurveRailing3D " + id + ": post-height-count-mismatch")
			return null
		for index in range(control_post_heights.size()):
			var value = control_post_heights[index]
			if (typeof(value) != TYPE_INT and typeof(value) != TYPE_FLOAT) or not is_finite(value) or value < float(control_heights[index]):
				push_error("CurveRailing3D " + id + ": invalid-control-post-height")
				return null
	if not support.has("enabled") or typeof(support.enabled) != TYPE_BOOL:
		push_error("CurveRailing3D " + id + ": support-enabled-required")
		return null
	if support.enabled:
		if not support.has_all(["size_design", "offset_design"]) or not support.size_design is Vector3 or not support.offset_design is Vector3:
			push_error("CurveRailing3D " + id + ": invalid-support")
			return null
		var size: Vector3 = support.size_design
		var offset: Vector3 = support.offset_design
		if not size.is_finite() or not offset.is_finite() or size.x <= 0.0 or size.y <= 0.0 or size.z <= 0.0:
			push_error("CurveRailing3D " + id + ": invalid-support")
			return null
	var path := Curve3D.new()
	path.bake_interval = minf(0.005,max_post_spacing/40.0)
	var coarse := Curve3D.new()
	coarse.bake_interval = minf(0.02,max_post_spacing/10.0)
	for i in range(design_path.point_count):
		if not design_path.get_point_position(i).is_finite() or not design_path.get_point_in(i).is_finite() or not design_path.get_point_out(i).is_finite():
			push_error("CurveRailing3D " + id + ": non-finite-path")
			return null
		path.add_point(Coordinates.to_godot(design_path.get_point_position(i)),Coordinates.to_godot(design_path.get_point_in(i)),Coordinates.to_godot(design_path.get_point_out(i)))
		coarse.add_point(Coordinates.to_godot(design_path.get_point_position(i)),Coordinates.to_godot(design_path.get_point_in(i)),Coordinates.to_godot(design_path.get_point_out(i)))
	var length := path.get_baked_length()
	if not is_finite(length) or length <= 0.0001:
		push_error("CurveRailing3D " + id + ": empty-path")
		return null
	var arc_error_estimate := absf(length-coarse.get_baked_length())
	if arc_error_estimate > arc_tolerance_m:
		push_error("CurveRailing3D " + id + ": bake-length-not-converged")
		return null
	var boundaries: Array[float] = [0.0]
	for i in range(1,path.point_count-1):
		var offset := path.get_closest_offset(path.get_point_position(i))
		if offset <= boundaries.back()+0.0001 or offset >= length-0.0001:
			push_error("CurveRailing3D " + id + ": non-monotone-control-point")
			return null
		boundaries.append(offset)
	boundaries.append(length)
	var offsets: Array[float] = [0.0]
	for i in range(boundaries.size()-1):
		var span := boundaries[i+1]-boundaries[i]
		var intervals := maxi(1,ceili(span/max_post_spacing))
		for j in range(1,intervals+1): offsets.append(boundaries[i]+span*float(j)/float(intervals))
	var maximum_arc_gap := 0.0
	for i in range(offsets.size()-1): maximum_arc_gap = maxf(maximum_arc_gap,offsets[i+1]-offsets[i])
	if maximum_arc_gap > max_post_spacing + arc_tolerance_m:
		push_error("CurveRailing3D " + id + ": post-spacing-exceeded")
		return null
	var points: Array[Vector3] = []
	# Additional guard and handrail samples follow bends without adding posts.
	var guard_offsets: Array[float] = [0.0]
	for i in range(offsets.size()-1):
		var span := offsets[i+1]-offsets[i]
		var intervals := maxi(1,ceili(span/0.20))
		for j in range(1,intervals+1): guard_offsets.append(offsets[i]+span*float(j)/float(intervals))
	for offset in guard_offsets: points.append(path.sample_baked(offset,true))
	var rail_points: Array[Vector3] = []
	for i in range(points.size()): rail_points.append(points[i]+Vector3.UP*_height_at(guard_offsets[i],boundaries,control_heights))
	for i in range(points.size()-1):
		for j in range(i+2,points.size()-1):
			if guard_offsets[j]-guard_offsets[i+1] <= 4.0*rail_radius: continue
			if _segment_distance_squared(rail_points[i],rail_points[i+1],rail_points[j],rail_points[j+1]) < 4.0*rail_radius*rail_radius:
				push_error("CurveRailing3D " + id + ": self-intersecting-path")
				return null
	var sides: Array[Vector3] = []
	for i in range(points.size()-1):
		var horizontal := points[i+1]-points[i]
		horizontal.y = 0.0
		if horizontal.length_squared() < 0.00000001:
			push_error("CurveRailing3D " + id + ": vertical-or-duplicate-span")
			return null
		sides.append(Vector3.UP.cross(horizontal.normalized()))
	var joins: Array[Vector3] = [sides[0]]
	for i in range(1,points.size()-1):
		var sum := sides[i-1]+sides[i]
		if sum.length_squared() < 0.01:
			push_error("CurveRailing3D " + id + ": reversing-path")
			return null
		var bisector := sum.normalized()
		var scale := 1.0/bisector.dot(sides[i])
		if scale > 4.0:
			push_error("CurveRailing3D " + id + ": corner-too-sharp")
			return null
		joins.append(bisector*scale)
	joins.append(sides.back())
	var first_direction := points[1]-points[0]
	first_direction.y = 0.0
	first_direction = first_direction.normalized()
	var last_direction := points[-1]-points[-2]
	last_direction.y = 0.0
	last_direction = last_direction.normalized()
	if maxf(absf(start_guard_cut_m.x),absf(start_guard_cut_m.y)) >= Vector2(points[1].x-points[0].x,points[1].z-points[0].z).length() or maxf(absf(end_guard_cut_m.x),absf(end_guard_cut_m.y)) >= Vector2(points[-1].x-points[-2].x,points[-1].z-points[-2].z).length():
		push_error("CurveRailing3D " + id + ": guard-cut-exceeds-end-segment")
		return null
	var start_minus := points[0]-joins[0]*guard_thickness/2.0+first_direction*start_guard_cut_m.x
	var start_plus := points[0]+joins[0]*guard_thickness/2.0+first_direction*start_guard_cut_m.y
	var end_minus := points[-1]-joins[-1]*guard_thickness/2.0+last_direction*end_guard_cut_m.x
	var end_plus := points[-1]+joins[-1]*guard_thickness/2.0+last_direction*end_guard_cut_m.y
	var root := Node3D.new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id",id)
	root.set_meta("planned_post_offsets_m",offsets)
	root.set_meta("path_length_m",length)
	root.set_meta("max_post_arc_gap_m",maximum_arc_gap)
	root.set_meta("max_post_arc_gap_basis", "planned offsets including shared endpoints; require assembly validation")
	root.set_meta("arc_error_estimate_m",arc_error_estimate)
	root.set_meta("arc_tolerance_m",arc_tolerance_m)
	root.set_meta("own_start_post",own_start_post)
	root.set_meta("own_end_post",own_end_post)
	root.set_meta("end_guard_cuts", "explicit")
	root.set_meta("start_guard_section", [start_minus,start_plus])
	root.set_meta("end_guard_section", [end_minus,end_plus])
	root.set_meta("start_guard_interior", points[1])
	root.set_meta("end_guard_interior", points[-2])
	root.set_meta("start_rail_height",float(control_heights[0]))
	root.set_meta("end_rail_height",float(control_heights[-1]))
	root.set_meta("guard_centres",points)
	var guard_heights: Array[float] = []
	for offset in guard_offsets: guard_heights.append(_height_at(offset,boundaries,control_heights))
	root.set_meta("guard_heights",guard_heights)
	root.set_meta("guard_thickness",guard_thickness)
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	root.add_child(visuals)
	var body := StaticBody3D.new()
	body.name = "CollisionRoot"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	root.add_child(body)
	var placed_offsets: Array[float] = []
	for index in range(offsets.size()):
		if index == 0 and not own_start_post: continue
		if index == offsets.size()-1 and not own_end_post: continue
		var offset := offsets[index]
		placed_offsets.append(offset)
		var post := path.sample_baked(offset,true)
		var post_height := _height_at(offset,boundaries,control_heights)
		if not control_post_heights.is_empty():
			for boundary_index in range(boundaries.size()):
				if absf(offset-boundaries[boundary_index]) < 0.000001:
					post_height = float(control_post_heights[boundary_index])
					break
		_cylinder(visuals,post,post+Vector3.UP*post_height,post_radius,material)
		if support.enabled:
			var support_size := Coordinates.size_to_godot(support.size_design)
			var support_centre := post + Coordinates.to_godot(support.offset_design)
			_box(visuals,body,support_centre,support_size,material)
	root.set_meta("placed_post_offsets_m",placed_offsets)
	# One low-sided, parallel-transported tube for the whole path. Shared
	# endpoints are capped by their single declared owner only.
	var rail_visual := MeshInstance3D.new()
	rail_visual.name = "ContinuousHandrail"
	rail_visual.mesh = _sweep_mesh(rail_points,rail_radius,own_start_post,own_end_post)
	rail_visual.material_override = material
	visuals.add_child(rail_visual)
	for i in range(points.size()-1):
		var h0 := _height_at(guard_offsets[i],boundaries,control_heights)
		var h1 := _height_at(guard_offsets[i+1],boundaries,control_heights)
		var a := points[i]
		var b := points[i+1]
		var da := joins[i]*guard_thickness/2.0
		var db := joins[i+1]*guard_thickness/2.0
		var a_minus := start_minus if i == 0 else a-da
		var a_plus := start_plus if i == 0 else a+da
		var b_minus := end_minus if i == points.size()-2 else b-db
		var b_plus := end_plus if i == points.size()-2 else b+db
		var shape := ConvexPolygonShape3D.new()
		shape.points = PackedVector3Array([a_minus,a_plus,b_minus,b_plus,a_minus+Vector3.UP*h0,a_plus+Vector3.UP*h0,b_minus+Vector3.UP*h1,b_plus+Vector3.UP*h1])
		var collision := CollisionShape3D.new()
		collision.shape = shape
		body.add_child(collision)
	return root

## Control heights interpolate by sampled arc distance within each span.
static func _height_at(offset: float, boundaries: Array[float], heights: Array) -> float:
	for i in range(boundaries.size()-1):
		if offset <= boundaries[i+1] or i == boundaries.size()-2:
			var fraction := clampf((offset-boundaries[i])/(boundaries[i+1]-boundaries[i]),0.0,1.0)
			return lerpf(float(heights[i]),float(heights[i+1]),fraction)
	return float(heights.back())

static func _box(visuals: Node3D, body: StaticBody3D, centre: Vector3, size: Vector3, material: Material) -> void:
	var mesh := BoxMesh.new()
	mesh.size = size
	var visible := MeshInstance3D.new()
	visible.mesh = mesh
	visible.position = centre
	visible.material_override = material
	visuals.add_child(visible)
	var shape := BoxShape3D.new()
	shape.size = size
	var collision := CollisionShape3D.new()
	collision.shape = shape
	collision.position = centre
	body.add_child(collision)

## Validate declared cross-path joints using generated cut surfaces. The caller
## must invoke this before adding any member to the scene. Endpoints are matched
## in the common parent coordinate system and exactly one post must be owned.
static func validate_cut_assembly(roots: Array, junctions: Array, tolerance_m: float) -> String:
	if not is_finite(tolerance_m) or tolerance_m <= 0.0: return "invalid-joint-tolerance"
	var ends := {}
	for root in roots:
		if not root is Node3D or not root.has_meta("world_object_id") or not root.has_meta("start_guard_section") or not root.has_meta("end_guard_section") or not root.has_meta("start_guard_interior") or not root.has_meta("end_guard_interior") or not root.has_meta("own_start_post") or not root.has_meta("own_end_post") or not root.has_meta("start_rail_height") or not root.has_meta("end_rail_height") or not root.has_meta("guard_centres") or not root.has_meta("guard_heights") or not root.has_meta("guard_thickness"): return "invalid-member"
		var basis: Basis = root.transform.basis
		if absf(basis.x.length()-1.0) > tolerance_m or absf(basis.y.length()-1.0) > tolerance_m or absf(basis.z.length()-1.0) > tolerance_m or basis.y.distance_to(Vector3.UP) > tolerance_m or absf(basis.determinant()-1.0) > tolerance_m: return "unsupported-member-transform"
		if not roots.is_empty() and root.get_parent() != roots[0].get_parent(): return "different-parent-spaces"
		var id := str(root.get_meta("world_object_id"))
		if id.is_empty() or ends.has(id): return "duplicate-or-missing-member-id"
		ends[id] = root
	var used := {}
	var pairs := {}
	for joint in junctions:
		if not joint is Dictionary or not joint.has_all(["a","a_end","b","b_end"]): return "incomplete-junction"
		var aid := str(joint.a)
		var bid := str(joint.b)
		var ae := str(joint.a_end)
		var be := str(joint.b_end)
		if aid == bid or not ends.has(aid) or not ends.has(bid) or ae not in ["start","end"] or be not in ["start","end"]: return "invalid-junction-member"
		var ak := aid + ":" + ae
		var bk := bid + ":" + be
		if used.has(ak) or used.has(bk): return "duplicate-junction-end"
		used[ak] = true
		used[bk] = true
		pairs[ak] = bk
		pairs[bk] = ak
		var a: Node3D = ends[aid]
		var b: Node3D = ends[bid]
		var sa: Array = a.get_meta(ae + "_guard_section")
		var sb: Array = b.get_meta(be + "_guard_section")
		var a0: Vector3 = a.transform * sa[0]
		var a1: Vector3 = a.transform * sa[1]
		var b0: Vector3 = b.transform * sb[0]
		var b1: Vector3 = b.transform * sb[1]
		var paired := (a0.distance_to(b0) <= tolerance_m and a1.distance_to(b1) <= tolerance_m) or (a0.distance_to(b1) <= tolerance_m and a1.distance_to(b0) <= tolerance_m)
		if not paired: return "mismatched-joint-cut:" + ak + ":" + bk
		if absf(float(a.get_meta(ae+"_rail_height"))-float(b.get_meta(be+"_rail_height"))) > tolerance_m: return "mismatched-rail-height:" + ak + ":" + bk
		var ai: Vector3 = a.transform * a.get_meta(ae + "_guard_interior")
		var bi: Vector3 = b.transform * b.get_meta(be + "_guard_interior")
		var edge := Vector2(a1.x-a0.x,a1.z-a0.z)
		var aside := edge.cross(Vector2(ai.x-a0.x,ai.z-a0.z))
		var bside := edge.cross(Vector2(bi.x-a0.x,bi.z-a0.z))
		if aside*bside >= 0.0: return "overlapping-joint-interiors:" + ak + ":" + bk
		var owners := int(bool(a.get_meta("own_"+ae+"_post"))) + int(bool(b.get_meta("own_"+be+"_post")))
		if owners != 1: return "junction-post-owner-count:" + ak + ":" + bk
	for id in ends:
		var root: Node3D = ends[id]
		for endpoint in ["start","end"]:
			if not bool(root.get_meta("own_"+endpoint+"_post")) and not used.has(str(id)+":"+endpoint): return "unowned-terminal:" + str(id) + ":" + endpoint
	# Nearby endpoints require an explicit joint; otherwise separate guard
	# end shapes could silently overlap even when post ownership is correct.
	for i in range(roots.size()):
		for j in range(i+1,roots.size()):
			var a: Node3D = roots[i]
			var b: Node3D = roots[j]
			var aid := str(a.get_meta("world_object_id"))
			var bid := str(b.get_meta("world_object_id"))
			for ae in ["start","end"]:
				for be in ["start","end"]:
					var sa: Array = a.get_meta(ae + "_guard_section")
					var sb: Array = b.get_meta(be + "_guard_section")
					var a0: Vector3 = a.transform * sa[0]
					var a1: Vector3 = a.transform * sa[1]
					var b0: Vector3 = b.transform * sb[0]
					var b1: Vector3 = b.transform * sb[1]
					var amid := (a0+a1)/2.0
					var bmid := (b0+b1)/2.0
					var reach := (a0.distance_to(a1)+b0.distance_to(b1))/2.0 + tolerance_m
					var horizontal := Vector2(amid.x-bmid.x,amid.z-bmid.z).length()
					if absf(amid.y-bmid.y) <= tolerance_m and horizontal <= reach:
						if not used.has(aid+":"+ae) or not used.has(bid+":"+be): return "undeclared-near-junction:" + aid + ":" + ae + ":" + bid + ":" + be
	# Conservatively reject overlap of different members away from a validated
	# shared end. This uses the sampled centreline and vertical envelopes.
	for i in range(roots.size()):
		for j in range(i+1,roots.size()):
			var a: Node3D = roots[i]
			var b: Node3D = roots[j]
			var aid := str(a.get_meta("world_object_id"))
			var bid := str(b.get_meta("world_object_id"))
			var ap: Array = a.get_meta("guard_centres")
			var bp: Array = b.get_meta("guard_centres")
			var ah: Array = a.get_meta("guard_heights")
			var bh: Array = b.get_meta("guard_heights")
			var reach := (float(a.get_meta("guard_thickness"))+float(b.get_meta("guard_thickness")))/2.0
			for ai in range(ap.size()-1):
				for bi in range(bp.size()-1):
					var akeys: Array[String] = []
					var bkeys: Array[String] = []
					if ai == 0: akeys.append(aid+":start")
					if ai == ap.size()-2: akeys.append(aid+":end")
					if bi == 0: bkeys.append(bid+":start")
					if bi == bp.size()-2: bkeys.append(bid+":end")
					var joined := false
					for ak in akeys:
						for bk in bkeys:
							if pairs.get(ak,"") == bk: joined = true
					if joined: continue
					var a0: Vector3 = a.transform * ap[ai]
					var a1: Vector3 = a.transform * ap[ai+1]
					var b0: Vector3 = b.transform * bp[bi]
					var b1: Vector3 = b.transform * bp[bi+1]
					var atop := maxf(a0.y+float(ah[ai]),a1.y+float(ah[ai+1]))
					var btop := maxf(b0.y+float(bh[bi]),b1.y+float(bh[bi+1]))
					if minf(a0.y,a1.y) >= btop or minf(b0.y,b1.y) >= atop: continue
					var horizontal_distance := _segment_distance_squared(Vector3(a0.x,0,a0.z),Vector3(a1.x,0,a1.z),Vector3(b0.x,0,b0.z),Vector3(b1.x,0,b1.z))
					if horizontal_distance < reach*reach: return "cross-path-guard-overlap:" + aid + ":" + bid
	return ""

## Distinct uncut path end guards may overlap at a shared junction. Reject the
## assembly until an explicit non-overlapping joint generator is supplied.
static func validate_uncut_assembly(paths: Array) -> String:
	for i in range(paths.size()):
		if not paths[i] is Curve3D or paths[i].point_count < 2: return "invalid-path:%s" % i
		for j in range(i+1,paths.size()):
			if not paths[j] is Curve3D or paths[j].point_count < 2: return "invalid-path:%s" % j
			for a in [paths[i].get_point_position(0),paths[i].get_point_position(paths[i].point_count-1)]:
				for b in [paths[j].get_point_position(0),paths[j].get_point_position(paths[j].point_count-1)]:
					if a.distance_squared_to(b) < 0.00000001: return "uncut-shared-junction:%s:%s" % [i,j]
	return ""

const SWEEP_SIDES := 12

## Builds a capped tube with a continuously transported orthogonal frame.
## No sphere poles, repeated centre vertices or zero-area cap triangles.
static func _sweep_mesh(points: Array[Vector3], radius: float, cap_start: bool, cap_end: bool) -> ArrayMesh:
	var tangents: Array[Vector3] = []
	for i in range(points.size()):
		var tangent: Vector3
		if i == 0: tangent = (points[1]-points[0]).normalized()
		elif i == points.size()-1: tangent = (points[-1]-points[-2]).normalized()
		else:
			var before := (points[i]-points[i-1]).normalized()
			var after := (points[i+1]-points[i]).normalized()
			tangent = (before+after).normalized()
		tangents.append(tangent)
	var u: Vector3 = Vector3.UP.cross(tangents[0]).normalized()
	if u.length_squared() < 0.000001: u = Vector3.RIGHT
	var rings: Array = []
	for i in range(points.size()):
		if i > 0:
			var transport := Quaternion(tangents[i-1],tangents[i])
			u = transport*u
			u = (u-tangents[i]*u.dot(tangents[i])).normalized()
		var v := tangents[i].cross(u).normalized()
		var ring: Array[Vector3] = []
		for side in range(SWEEP_SIDES):
			var angle := TAU*float(side)/float(SWEEP_SIDES)
			ring.append(points[i]+radius*(u*cos(angle)+v*sin(angle)))
		rings.append(ring)
	var faces := PackedVector3Array()
	var normals := PackedVector3Array()
	for i in range(points.size()-1):
		for side in range(SWEEP_SIDES):
			var next := (side+1)%SWEEP_SIDES
			var a: Vector3 = rings[i][side]
			var b: Vector3 = rings[i][next]
			var c: Vector3 = rings[i+1][next]
			var d: Vector3 = rings[i+1][side]
			var outward := ((a-points[i])+(b-points[i])+(c-points[i+1])+(d-points[i+1])).normalized()
			_add_triangle(faces,normals,a,b,c,outward)
			_add_triangle(faces,normals,a,c,d,outward)
	for side in range(SWEEP_SIDES):
		var next := (side+1)%SWEEP_SIDES
		if cap_start: _add_triangle(faces,normals,points[0],rings[0][side],rings[0][next],-tangents[0])
		if cap_end: _add_triangle(faces,normals,points[-1],rings[-1][side],rings[-1][next],tangents[-1])
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = faces
	arrays[Mesh.ARRAY_NORMAL] = normals
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	return mesh

static func _add_triangle(faces: PackedVector3Array, normals: PackedVector3Array, a: Vector3, b: Vector3, c: Vector3, desired: Vector3) -> void:
	if -(b-a).cross(c-a).dot(desired) < 0.0:
		var swap := b
		b = c
		c = swap
	faces.append_array(PackedVector3Array([a,b,c]))
	normals.append_array(PackedVector3Array([desired,desired,desired]))

static func _cylinder(parent: Node3D, a: Vector3, b: Vector3, radius: float, material: Material) -> void:
	var visual := MeshInstance3D.new()
	var line: Array[Vector3] = [a,b]
	visual.mesh = _sweep_mesh(line,radius,true,true)
	visual.material_override = material
	parent.add_child(visual)

## Closest points of finite segments. This detects crossings in the sampled
## polyline, including a conservative rail-radius proximity check outside local neighbours.
static func _segment_distance_squared(a: Vector3, b: Vector3, c: Vector3, d: Vector3) -> float:
	var u := b-a
	var v := d-c
	var w := a-c
	var aa := u.dot(u)
	var bb := u.dot(v)
	var cc := v.dot(v)
	var dd := u.dot(w)
	var ee := v.dot(w)
	var denominator := aa*cc-bb*bb
	var s := 0.0
	if aa <= 0.000000000001 and cc <= 0.000000000001: return a.distance_squared_to(c)
	if aa <= 0.000000000001: return a.distance_squared_to(c+v*clampf(ee/cc,0.0,1.0))
	if cc <= 0.000000000001: return (a+u*clampf(-dd/aa,0.0,1.0)).distance_squared_to(c)
	if denominator > 0.000000000001: s = clampf((bb*ee-cc*dd)/denominator,0.0,1.0)
	var t := (bb*s+ee)/cc
	if t < 0.0:
		t = 0.0
		s = clampf(-dd/aa,0.0,1.0)
	elif t > 1.0:
		t = 1.0
		s = clampf((bb-dd)/aa,0.0,1.0)
	return (w+u*s-v*t).length_squared()












