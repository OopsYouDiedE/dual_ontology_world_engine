extends Node3D
## Stair path is local +X, width is along Z, height is local Y.
## Visual treads are shallow solids; the collision is one thin ramp prism.
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")
var run_length := 0.0
var rise := 0.0
var width := 0.0
var body: StaticBody3D

static func create(id: String, run_m: float, rise_m: float, width_m: float, count: int, tread_thickness: float, ramp_thickness: float, material: Material) -> Node3D:
	if id.is_empty() or not is_finite(run_m) or not is_finite(rise_m) or not is_finite(width_m) or not is_finite(tread_thickness) or not is_finite(ramp_thickness) or run_m <= 0.0 or rise_m <= 0.0 or width_m <= 0.0 or count <= 0 or tread_thickness <= 0.0 or ramp_thickness <= 0.0:
		push_error("StraightStair3D " + id + ": invalid-parameters")
		return null
	var root = load("res://geometry/generators/StraightStair3D.gd").new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id",id)
	root.run_length = run_m
	root.rise = rise_m
	root.width = width_m
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

