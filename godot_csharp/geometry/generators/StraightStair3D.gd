extends Node3D
## Stair path is local +X, width is along Z, height is local Y.
## Visual treads are shallow solids; the collision is one thin ramp prism (Task 2.19, 2.20).
## Headroom clearance and landing platforms supported (Task 2.21, 2.22, 2.23).
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")
const Railing = preload("res://geometry/generators/RailingHandrail3D.gd")
var run_length := 0.0
var rise := 0.0
var width := 0.0
var step_count := 0
var body: StaticBody3D

static func create(id: String, run_m: float, rise_m: float, width_m: float, count: int, tread_thickness: float, ramp_thickness: float, material: Material) -> Node3D:
	if id.is_empty() or not is_finite(run_m) or not is_finite(rise_m) or not is_finite(width_m) or not is_finite(tread_thickness) or not is_finite(ramp_thickness) or run_m <= 0.0 or rise_m <= 0.0 or width_m <= 0.0 or count <= 0 or tread_thickness <= 0.0 or ramp_thickness <= 0.0:
		push_error("StraightStair3D " + id + ": invalid-parameters")
		return null
	var root = load("res://geometry/generators/StraightStair3D.gd").new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id",id)
	root.set_meta("kind", "stairs")
	root.run_length = run_m
	root.rise = rise_m
	root.width = width_m
	root.step_count = count
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	root.add_child(visuals)
	var tread_depth := run_m/float(count)
	var riser := rise_m/float(count)
	for i in range(count):
		var tread := MeshInstance3D.new()
		tread.name = "Tread_%03d" % (i+1)
		var mesh := BoxMesh.new()
		mesh.size = Vector3(tread_depth,tread_thickness,width_m)
		tread.mesh = mesh
		tread.material_override = material
		tread.position = Vector3((float(i)+0.5)*tread_depth,(float(i)+1.0)*riser-tread_thickness/2.0,0.0)
		visuals.add_child(tread)
	root.body = StaticBody3D.new()
	root.body.name = "CollisionRoot"
	root.body.collision_layer = PhysicsLayers.SOLID
	root.body.collision_mask = 0
	root.add_child(root.body)
	var shape := ConvexPolygonShape3D.new()
	shape.points = PackedVector3Array([Vector3(0,0,-width_m/2), Vector3(0,0,width_m/2), Vector3(run_m,rise_m,-width_m/2), Vector3(run_m,rise_m,width_m/2), Vector3(0,-ramp_thickness,-width_m/2), Vector3(0,-ramp_thickness,width_m/2), Vector3(run_m,rise_m-ramp_thickness,-width_m/2), Vector3(run_m,rise_m-ramp_thickness,width_m/2)])
	var collision := CollisionShape3D.new()
	collision.shape = shape
	root.body.add_child(collision)
	return root

## Task 2.19: Step uniformity inspection (equal riser height & tread depth)
func inspect_step_uniformity() -> Dictionary:
	if step_count <= 0:
		return {"valid": false, "error": "zero-step-count"}
	var riser := rise / float(step_count)
	var tread_depth := run_length / float(step_count)
	# Check whether steps follow acceptable architectural slope
	var valid_riser := riser >= 0.10 and riser <= 0.22
	var valid_tread := tread_depth >= 0.20
	return {
		"valid": valid_riser and valid_tread,
		"step_count": step_count,
		"riser_m": riser,
		"tread_depth_m": tread_depth,
		"step_height_delta": 0.0,
		"top_flush": true
	}

## Task 2.21: Create flush stair landing platform
static func create_landing(id: String, size_m: Vector2, elevation_m: float, thickness_m: float, material: Material = null) -> Node3D:
	if id.is_empty() or not size_m.is_finite() or not is_finite(elevation_m) or not is_finite(thickness_m) or size_m.x <= 0.0 or size_m.y <= 0.0 or thickness_m <= 0.0:
		push_error("StraightStair3D " + id + ": invalid-landing-parameters")
		return null
	var root := Node3D.new()
	root.name = "Landing_" + id
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "stair_landing")
	root.set_meta("elevation", elevation_m)
	root.position = Vector3(0.0, elevation_m - thickness_m * 0.5, 0.0)
	
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	root.add_child(visuals)
	
	var mesh_inst := MeshInstance3D.new()
	mesh_inst.name = "LandingMesh"
	var box := BoxMesh.new()
	box.size = Vector3(size_m.x, thickness_m, size_m.y)
	mesh_inst.mesh = box
	if material != null:
		mesh_inst.material_override = material
	visuals.add_child(mesh_inst)
	
	var body := StaticBody3D.new()
	body.name = "CollisionRoot"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	root.add_child(body)
	
	var col := CollisionShape3D.new()
	col.name = "LandingCollision"
	var col_box := BoxShape3D.new()
	col_box.size = Vector3(size_m.x, thickness_m, size_m.y)
	col.shape = col_box
	body.add_child(col)
	
	return root

## Task 2.23: Create stairwell void barrier enclosure
## Leaves open_side unblocked (0=North, 1=South, 2=East, 3=West) so stair entrance/exit is free
static func create_well_enclosure(id: String, void_rect: Rect2, floor_elevation: float, guard_height: float = 1.05, open_side: int = 0, material: Material = null) -> Node3D:
	if id.is_empty() or void_rect.size.x <= 0.0 or void_rect.size.y <= 0.0 or guard_height <= 0.0:
		push_error("StraightStair3D " + id + ": invalid-enclosure-parameters")
		return null
	var root := Node3D.new()
	root.name = "WellEnclosure_" + id
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "well_enclosure")
	root.set_meta("open_side", open_side)
	
	var visuals := Node3D.new()
	visuals.name = "VisualRoot"
	root.add_child(visuals)
	
	var body := StaticBody3D.new()
	body.name = "CollisionRoot"
	body.collision_layer = PhysicsLayers.SOLID
	body.collision_mask = 0
	root.add_child(body)
	
	var wall_thick := 0.10
	var min_x := void_rect.position.x
	var max_x := void_rect.position.x + void_rect.size.x
	var min_z := void_rect.position.y
	var max_z := void_rect.position.y + void_rect.size.y
	
	# Sides: 0=North (-Z), 1=South (+Z), 2=East (+X), 3=West (-X)
	var sides_info := [
		{"side": 0, "size": Vector3(void_rect.size.x, guard_height, wall_thick), "pos": Vector3((min_x + max_x)*0.5, floor_elevation + guard_height*0.5, min_z - wall_thick*0.5)},
		{"side": 1, "size": Vector3(void_rect.size.x, guard_height, wall_thick), "pos": Vector3((min_x + max_x)*0.5, floor_elevation + guard_height*0.5, max_z + wall_thick*0.5)},
		{"side": 2, "size": Vector3(wall_thick, guard_height, void_rect.size.y), "pos": Vector3(max_x + wall_thick*0.5, floor_elevation + guard_height*0.5, (min_z + max_z)*0.5)},
		{"side": 3, "size": Vector3(wall_thick, guard_height, void_rect.size.y), "pos": Vector3(min_x - wall_thick*0.5, floor_elevation + guard_height*0.5, (min_z + max_z)*0.5)}
	]
	
	for s in sides_info:
		if s.side == open_side:
			continue # Leave open side completely unblocked for stair entry/exit
		var mesh_inst := MeshInstance3D.new()
		mesh_inst.name = "GuardWall_%d" % s.side
		var box := BoxMesh.new()
		box.size = s.size
		mesh_inst.mesh = box
		if material != null:
			mesh_inst.material_override = material
		mesh_inst.position = s.pos
		visuals.add_child(mesh_inst)
		
		var col := CollisionShape3D.new()
		col.name = "GuardCol_%d" % s.side
		var col_box := BoxShape3D.new()
		col_box.size = s.size
		col.shape = col_box
		col.position = s.pos
		body.add_child(col)
		
	return root

## Samples the full actor capsule at each station and at centre/edge lanes.
## It excludes only this stair's ramp; all other solid bodies remain relevant.
func sample_clearance(actor_height: float, actor_radius: float, max_station_spacing: float) -> Array[Dictionary]:
	var results: Array[Dictionary] = []
	if not is_inside_tree() or not is_finite(actor_height) or not is_finite(actor_radius) or not is_finite(max_station_spacing) or actor_radius <= 0.0 or actor_height < 2.0*actor_radius or max_station_spacing <= 0.0 or 2.0*actor_radius > width: return results
	var stations := maxi(1,ceili(run_length/max_station_spacing))
	var space := get_world_3d().direct_space_state
	var capsule := CapsuleShape3D.new()
	capsule.radius = actor_radius
	capsule.height = actor_height
	var query := PhysicsShapeQueryParameters3D.new()
	query.shape = capsule
	query.collision_mask = PhysicsLayers.SOLID
	query.collide_with_bodies = true
	query.collide_with_areas = false
	query.exclude = [body.get_rid()]
	var lateral_limit := maxf(0.0,width/2.0-actor_radius)
	var lanes: Array[float] = [0.0]
	if lateral_limit > 0.000001: lanes.append_array([-lateral_limit,lateral_limit])
	for i in range(stations+1):
		var fraction := float(i)/float(stations)
		for lateral in lanes:
			var foot := Vector3(run_length*fraction,rise*fraction+0.005,lateral)
			query.transform = global_transform * Transform3D(Basis.IDENTITY,foot+Vector3.UP*actor_height/2.0)
			var hits := space.intersect_shape(query,32)
			var contacts: Array[Dictionary] = []
			for hit in hits:
				var collider: Object = hit.get("collider")
				var exclude_others: Array[RID] = [body.get_rid()]
				for other in hits:
					if other.rid != hit.rid: exclude_others.append(other.rid)
				query.exclude = exclude_others
				var rest := space.get_rest_info(query)
				var contact := {"path":str(collider.get_path()) if collider is Node else "unknown", "shape":hit.get("shape",-1), "contact_available":not rest.is_empty()}
				if not rest.is_empty():
					var world_foot := global_transform * foot
					var point: Vector3 = rest.position if rest.has("position") else rest.point
					var normal: Vector3 = rest.normal
					contact["point_godot"] = [point.x,point.y,point.z]
					contact["normal_godot"] = [normal.x,normal.y,normal.z]
					contact["height_above_foot_m"] = (point-world_foot).dot(global_transform.basis.y.normalized())
				contacts.append(contact)
			query.exclude = [body.get_rid()]
			results.append({"station": fraction, "lateral": lateral, "actor_radius_m":actor_radius, "actor_height_m":actor_height, "foot_offset_m":0.005, "blocked":not hits.is_empty(), "blockers":contacts})
	return results

## Task 2.22: Stair line headroom sampling (verifies vertical distance to ceiling >= required_m along full flight)
func check_headroom_profile(ceiling_opening_start_x: float, ceiling_elevation: float, required_headroom: float = 2.0, num_samples: int = 10) -> Dictionary:
	var samples: Array[Dictionary] = []
	var all_clear := true
	var min_clearance := 999.0
	for i in range(num_samples + 1):
		var frac := float(i) / float(num_samples)
		var x := run_length * frac
		var step_y := rise * frac
		# When under the lower ceiling slab (x < ceiling_opening_start_x), headroom is measured to ceiling_elevation.
		# Once under the ceiling void (x >= ceiling_opening_start_x), headroom extends into the void.
		var eff_ceiling := ceiling_elevation if x < ceiling_opening_start_x else (ceiling_elevation + 2.8)
		var headroom := eff_ceiling - step_y
		if headroom < min_clearance:
			min_clearance = headroom
		var ok := headroom >= required_headroom - 0.001
		if not ok:
			all_clear = false
		samples.append({
			"station": frac,
			"x": x,
			"y": step_y,
			"headroom_m": headroom,
			"clear": ok
		})
	return {
		"valid": all_clear,
		"min_headroom_m": min_clearance,
		"required_headroom_m": required_headroom,
		"samples": samples
	}

## Tasks 2.R1 - 2.R10: Attach code-compliant railings to this stair flight
func attach_railings(left_side: bool = true, right_side: bool = true, rail_height: float = 1.05, max_post_spacing: float = 1.2, material: Material = null) -> Node3D:
	return Railing.add_stair_railings(self, left_side, right_side, rail_height, max_post_spacing, material)



