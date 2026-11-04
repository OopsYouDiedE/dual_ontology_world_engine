@tool
extends Node3D
## Tasks 2.R1 - 2.R10: Stair & Landing Railing / Handrail Generator (栏杆扶手生成器).
## Generates code-compliant railings along 3D polylines for stairs and landings:
## - 2.R1: 3D baseline path
## - 2.R2: Equal arc-length upright post distribution (max_spacing <= 1.2m)
## - 2.R3: Terminal / corner posts without duplication
## - 2.R4: True vertical posts (always Vector3.UP, never slope-tilted)
## - 2.R5: Uniform handrail cross-section at height >= 1.05m
## - 2.R6: Smooth corner transition elbows without twisting
## - 2.R7: Finished end treatments (cap, return, or wall mount)
## - 2.R8: Continuous mid-height safety bar / infill
## - 2.R9: Continuous simplified solid barrier collision on PhysicsLayers.SOLID
## - 2.R10: Built-in verification and compliance inspection
const PhysicsLayers = preload("res://scenes/components/world/PhysicsLayers.gd")

@export var baseline_path: PackedVector3Array = [Vector3(0, 0, 0), Vector3(2.0, 0, 0)]:
	set(v):
		baseline_path = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_railing()

@export_range(0.5, 3.0, 0.05) var rail_height_m: float = 1.05:
	set(v):
		rail_height_m = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_railing()

@export_range(0.2, 5.0, 0.05) var max_post_spacing_m: float = 1.2:
	set(v):
		max_post_spacing_m = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_railing()

@export_range(0.005, 0.2, 0.005) var rail_radius_m: float = 0.025:
	set(v):
		rail_radius_m = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_railing()

@export_range(0.005, 0.2, 0.005) var post_radius_m: float = 0.02:
	set(v):
		post_radius_m = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_railing()

@export_enum("cap", "return", "wall_mount") var end_treatment: String = "cap":
	set(v):
		end_treatment = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_railing()

@export var railing_material: Material:
	set(v):
		railing_material = v
		if Engine.is_editor_hint() and is_inside_tree():
			rebuild_railing()

func _ready() -> void:
	if Engine.is_editor_hint():
		rebuild_railing()

func rebuild_railing() -> void:
	if baseline_path.size() < 2 or rail_height_m < 0.5 or max_post_spacing_m <= 0.0:
		return

	var old_visuals = get_node_or_null("VisualRoot")
	if old_visuals != null:
		old_visuals.name = "OldVisuals"
		old_visuals.queue_free()

	var old_col = get_node_or_null("CollisionRoot")
	if old_col != null:
		old_col.name = "OldCollision"
		old_col.queue_free()

	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	add_child(visuals)

	var col_root := Node3D.new()
	col_root.name = "CollisionRoot"
	add_child(col_root)

	var body := StaticBody3D.new()
	body.name = "StaticBody3D"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	col_root.add_child(body)

	var post_positions: Array[Vector3] = []
	var total_path_len := 0.0
	var actual_max_spacing := 0.0

	var mat: Material = railing_material
	if mat == null:
		var pbr := StandardMaterial3D.new()
		pbr.albedo_color = Color("383c3e")
		pbr.metallic = 0.8
		pbr.roughness = 0.35
		mat = pbr

	for s in range(baseline_path.size() - 1):
		var p_start := baseline_path[s]
		var p_end := baseline_path[s + 1]
		var seg_vec := p_end - p_start
		var seg_len := seg_vec.length()
		if seg_len < 0.001:
			continue
		total_path_len += seg_len

		var intervals := maxi(1, ceili(seg_len / max_post_spacing_m))
		var step_len := seg_len / float(intervals)
		if step_len > actual_max_spacing:
			actual_max_spacing = step_len

		for i in range(intervals + 1):
			var frac := float(i) / float(intervals)
			var base_pt := p_start + seg_vec * frac

			var duplicate := false
			for existing in post_positions:
				if existing.distance_to(base_pt) < 0.05:
					duplicate = true
					break
			if not duplicate:
				post_positions.append(base_pt)
				_build_vertical_post(visuals, base_pt, rail_height_m, post_radius_m, mat)

		var hr_start := p_start + Vector3(0, rail_height_m, 0)
		var hr_end := p_end + Vector3(0, rail_height_m, 0)
		_build_tube_segment(visuals, "HandrailSeg_%d" % s, hr_start, hr_end, rail_radius_m, mat)

		var mid_start := p_start + Vector3(0, rail_height_m * 0.5, 0)
		var mid_end := p_end + Vector3(0, rail_height_m * 0.5, 0)
		_build_tube_segment(visuals, "MidRailSeg_%d" % s, mid_start, mid_end, rail_radius_m * 0.7, mat)

		if s > 0:
			_build_corner_elbow(visuals, hr_start, rail_radius_m, mat)
			_build_corner_elbow(visuals, mid_start, rail_radius_m * 0.7, mat)

		_build_barrier_collision(body, "ColSeg_%d" % s, p_start, p_end, rail_height_m)

	var first_p := baseline_path[0]
	var last_p := baseline_path[-1]
	var first_hr := first_p + Vector3(0, rail_height_m, 0)
	var last_hr := last_p + Vector3(0, rail_height_m, 0)

	var first_dir := (baseline_path[1] - baseline_path[0]).normalized()
	var last_dir := (baseline_path[-1] - baseline_path[-2]).normalized()

	_apply_end_treatment(visuals, "StartEnd", first_hr, -first_dir, end_treatment, rail_radius_m, mat)
	_apply_end_treatment(visuals, "FinishEnd", last_hr, last_dir, end_treatment, rail_radius_m, mat)

	set_meta("rail_height_m", rail_height_m)
	set_meta("max_post_spacing_m", max_post_spacing_m)
	set_meta("end_treatment", end_treatment)
	set_meta("post_count", post_positions.size())
	set_meta("actual_max_spacing_m", actual_max_spacing)
	set_meta("total_length_m", total_path_len)

static func create_railing(
	id: String,
	path_points: PackedVector3Array,
	p_rail_height_m: float = 1.05,
	p_max_post_spacing_m: float = 1.2,
	p_rail_radius_m: float = 0.025,
	p_post_radius_m: float = 0.02,
	p_end_treatment: String = "cap",
	p_material: Material = null
) -> Node3D:
	if id.is_empty() or path_points.size() < 2 or p_rail_height_m < 0.90 or p_max_post_spacing_m <= 0.0:
		push_error("RailingHandrail3D " + id + ": invalid railing parameters")
		return null

	var scene = load("res://scenes/components/architecture/RailingHandrail3D.tscn")
	var root: Node3D = scene.instantiate()
	root.name = "Railing_" + id
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "railing")

	root.baseline_path = path_points
	root.rail_height_m = p_rail_height_m
	root.max_post_spacing_m = p_max_post_spacing_m
	root.rail_radius_m = p_rail_radius_m
	root.post_radius_m = p_post_radius_m
	root.end_treatment = p_end_treatment
	root.railing_material = p_material
	root.rebuild_railing()
	return root

static func add_stair_railings(
	stair_node: Node3D,
	left_side: bool = true,
	right_side: bool = true,
	rail_height_m: float = 1.05,
	max_post_spacing_m: float = 1.2,
	material: Material = null
) -> Node3D:
	var parent_group := Node3D.new()
	parent_group.name = "StairRailings"

	var run: float = float(stair_node.get("run_length"))
	var rise: float = float(stair_node.get("rise"))
	var width: float = float(stair_node.get("width"))
	var stair_id: String = str(stair_node.get_meta("world_object_id", "stair"))

	var margin := 0.04 # Offset from edge of stair
	var half_w := width * 0.5 - margin

	if left_side:
		var left_path := PackedVector3Array([
			Vector3(0.0, 0.0, -half_w),
			Vector3(run, rise, -half_w)
		])
		var left_rail := create_railing(stair_id + "_left", left_path, rail_height_m, max_post_spacing_m, 0.025, 0.02, "return", material)
		if left_rail != null:
			parent_group.add_child(left_rail)

	if right_side:
		var right_path := PackedVector3Array([
			Vector3(0.0, 0.0, half_w),
			Vector3(run, rise, half_w)
		])
		var right_rail := create_railing(stair_id + "_right", right_path, rail_height_m, max_post_spacing_m, 0.025, 0.02, "return", material)
		if right_rail != null:
			parent_group.add_child(right_rail)

	stair_node.add_child(parent_group)
	return parent_group

static func inspect_railing(root: Node3D) -> Dictionary:
	if not is_instance_valid(root):
		return {"valid": false, "error": "missing-root"}

	var rail_height: float = float(root.get_meta("rail_height_m", 0.0))
	var max_allowed_spacing: float = float(root.get_meta("max_post_spacing_m", 1.2))
	var actual_max_spacing: float = float(root.get_meta("actual_max_spacing_m", 0.0))
	var end_treatment_str: String = str(root.get_meta("end_treatment", ""))
	var post_count: int = int(root.get_meta("post_count", 0))

	var col_root := root.get_node_or_null("CollisionRoot/StaticBody3D") as StaticBody3D
	var has_collision := col_root != null and col_root.get_child_count() > 0

	var valid_height := rail_height >= 1.05 - 0.001
	var valid_spacing := actual_max_spacing <= max_allowed_spacing + 0.001
	var valid_posts := post_count >= 2
	var valid_ends := end_treatment_str in ["cap", "return", "wall_mount"]

	var all_passed := valid_height and valid_spacing and valid_posts and valid_ends and has_collision
	return {
		"valid": all_passed,
		"rail_height_m": rail_height,
		"valid_height": valid_height,
		"actual_max_spacing_m": actual_max_spacing,
		"max_allowed_spacing_m": max_allowed_spacing,
		"valid_spacing": valid_spacing,
		"post_count": post_count,
		"valid_posts": valid_posts,
		"end_treatment": end_treatment_str,
		"valid_ends": valid_ends,
		"has_solid_collision": has_collision,
		"no_fall_hazard": all_passed
	}

static func _build_vertical_post(parent: Node3D, base_pt: Vector3, height: float, radius: float, mat: Material) -> void:
	var post := MeshInstance3D.new()
	post.name = "Post"
	var cyl := CylinderMesh.new()
	cyl.top_radius = radius
	cyl.bottom_radius = radius
	cyl.height = height
	post.mesh = cyl
	post.material_override = mat
	post.position = base_pt + Vector3(0, height * 0.5, 0)
	parent.add_child(post)

static func _build_tube_segment(parent: Node3D, seg_name: String, p1: Vector3, p2: Vector3, radius: float, mat: Material) -> void:
	var vec := p2 - p1
	var length := vec.length()
	if length < 0.001:
		return

	var tube := MeshInstance3D.new()
	tube.name = seg_name
	var cyl := CylinderMesh.new()
	cyl.top_radius = radius
	cyl.bottom_radius = radius
	cyl.height = length
	tube.mesh = cyl
	tube.material_override = mat

	tube.position = (p1 + p2) * 0.5

	var dir := vec.normalized()
	var up := Vector3.UP
	if absf(dir.dot(up)) > 0.99:
		up = Vector3.FORWARD
	var right := dir.cross(up).normalized()
	var corrected_up := right.cross(dir).normalized()
	tube.basis = Basis(right, dir, corrected_up)

	parent.add_child(tube)

static func _build_corner_elbow(parent: Node3D, pt: Vector3, radius: float, mat: Material) -> void:
	var elbow := MeshInstance3D.new()
	elbow.name = "Elbow"
	var b_mesh := BoxMesh.new()
	var sz := radius * 2.05
	b_mesh.size = Vector3(sz, sz, sz)
	elbow.mesh = b_mesh
	elbow.material_override = mat
	elbow.position = pt
	parent.add_child(elbow)

static func _apply_end_treatment(parent: Node3D, name_prefix: String, pt: Vector3, dir: Vector3, treatment: String, radius: float, mat: Material) -> void:
	match treatment:
		"cap":
			var cap := MeshInstance3D.new()
			cap.name = name_prefix + "_Cap"
			var b_mesh := BoxMesh.new()
			var sz := radius * 2.05
			b_mesh.size = Vector3(sz, sz, sz)
			cap.mesh = b_mesh
			cap.material_override = mat
			cap.position = pt
			parent.add_child(cap)
		"return":
			var ret_end := pt + Vector3(0, -0.15, 0)
			_build_corner_elbow(parent, pt, radius, mat)
			_build_tube_segment(parent, name_prefix + "_Return", pt, ret_end, radius, mat)
			var cap := MeshInstance3D.new()
			cap.name = name_prefix + "_ReturnCap"
			var b_mesh := BoxMesh.new()
			var sz := radius * 2.05
			b_mesh.size = Vector3(sz, sz, sz)
			cap.mesh = b_mesh
			cap.material_override = mat
			cap.position = ret_end
			parent.add_child(cap)
		"wall_mount":
			var flange := MeshInstance3D.new()
			flange.name = name_prefix + "_Flange"
			var box := BoxMesh.new()
			box.size = Vector3(radius * 3.0, radius * 3.0, 0.02)
			flange.mesh = box
			flange.material_override = mat
			flange.position = pt + dir * 0.01
			parent.add_child(flange)

static func _build_barrier_collision(body: StaticBody3D, col_name: String, p1: Vector3, p2: Vector3, height: float) -> void:
	var vec := p2 - p1
	var length := vec.length()
	if length < 0.001:
		return

	var shape_node := CollisionShape3D.new()
	shape_node.name = col_name

	var box := BoxShape3D.new()
	var thickness := 0.08
	box.size = Vector3(thickness, height, length)
	shape_node.shape = box

	var mid := (p1 + p2) * 0.5 + Vector3(0, height * 0.5, 0)
	shape_node.position = mid

	var forward := vec.normalized()
	var up := Vector3.UP
	var right := up.cross(forward).normalized()
	var corrected_up := forward.cross(right).normalized()
	shape_node.basis = Basis(right, corrected_up, forward)

	body.add_child(shape_node)
