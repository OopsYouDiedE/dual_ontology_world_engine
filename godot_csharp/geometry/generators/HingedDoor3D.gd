extends Node3D
## A hinged leaf. Local +X spans the leaf; its pivot is the hinge at floor height.
## Motion is visual/physical only. Core decides whether an action is permitted.
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")
const MAX_SWEEP_STEP_M := 0.02
const MAX_SWEEP_STEP_RAD := 0.034906585
var leaf_width := 0.0
var leaf_height := 0.0
var leaf_thickness := 0.0
var minimum_angle := 0.0
var maximum_angle := 0.0
var current_angle := 0.0
var last_blocker: Dictionary = {}
var last_sweep_max_gap_m := 0.0
var moving := false
var motion_blocked := false
var motion_target := 0.0
var motion_speed_degrees_per_second := 0.0
var hinge: Node3D
var body: StaticBody3D
var box: BoxShape3D

static func create(id: String, width: float, height: float, thickness: float, minimum_degrees: float, maximum_degrees: float, material: Material) -> Node3D:
	if id.is_empty() or not is_finite(width) or not is_finite(height) or not is_finite(thickness) or not is_finite(minimum_degrees) or not is_finite(maximum_degrees) or width <= 0.0 or height <= 0.0 or thickness <= 0.0 or minimum_degrees > 0.0 or maximum_degrees < 0.0 or minimum_degrees >= maximum_degrees:
		push_error("HingedDoor3D " + id + ": invalid-parameters")
		return null
	var root = load("res://geometry/generators/HingedDoor3D.gd").new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id", id)
	root.leaf_width = width
	root.leaf_height = height
	root.leaf_thickness = thickness
	root.minimum_angle = minimum_degrees
	root.maximum_angle = maximum_degrees
	root.hinge = Node3D.new()
	root.hinge.name = "Hinge"
	root.add_child(root.hinge)
	var visual := MeshInstance3D.new()
	visual.name = "VisualRoot"
	var mesh := BoxMesh.new()
	mesh.size = Vector3(width,height,thickness)
	visual.mesh = mesh
	visual.material_override = material
	visual.position = Vector3(width/2.0,height/2.0,0.0)
	root.hinge.add_child(visual)
	root.body = StaticBody3D.new()
	root.body.name = "CollisionRoot"
	root.body.collision_layer = PhysicsLayers.SOLID
	root.body.collision_mask = 0
	root.hinge.add_child(root.body)
	root.box = BoxShape3D.new()
	root.box.size = Vector3(width,height,thickness)
	var collider := CollisionShape3D.new()
	collider.shape = root.box
	collider.position = Vector3(width/2.0,height/2.0,0.0)
	root.body.add_child(collider)
	return root

## Returns false without changing angle when the sweep encounters a solid or actor.
## Call only while inside the scene tree, after the physics world is available.
func set_angle_checked(degrees: float) -> bool:
	if not is_inside_tree() or not is_finite(degrees) or degrees < minimum_angle or degrees > maximum_angle: return false
	last_blocker = {}
	last_sweep_max_gap_m = 0.0
	var difference := deg_to_rad(degrees-current_angle)
	if absf(difference) < 0.000001: return true
	var furthest_radius := Vector2(leaf_width,leaf_thickness/2.0).length()
	var steps := maxi(1,ceili(maxf(absf(difference)/MAX_SWEEP_STEP_RAD,absf(difference)*furthest_radius/MAX_SWEEP_STEP_M)))
	last_sweep_max_gap_m = furthest_radius*absf(difference)/float(steps)
	var query := PhysicsShapeQueryParameters3D.new()
	query.shape = box
	query.collision_mask = PhysicsLayers.SOLID | PhysicsLayers.ACTOR
	query.collide_with_bodies = true
	query.collide_with_areas = false
	query.exclude = [body.get_rid()]
	var space := get_world_3d().direct_space_state
	for i in range(1,steps+1):
		var angle := deg_to_rad(lerpf(current_angle,degrees,float(i)/float(steps)))
		query.transform = global_transform * Transform3D(Basis(Vector3.UP,angle),Vector3.ZERO) * Transform3D(Basis.IDENTITY,Vector3(leaf_width/2.0,leaf_height/2.0,0.0))
		var hits := space.intersect_shape(query,1)
		if not hits.is_empty():
			var collider: Object = hits[0].get("collider")
			var owner_id := ""
			var ancestor := collider as Node
			while ancestor != null:
				if ancestor.has_meta("world_object_id"):
					owner_id = str(ancestor.get_meta("world_object_id"))
					break
				ancestor = ancestor.get_parent()
			last_blocker = {"angle_degrees": rad_to_deg(angle), "path": str(collider.get_path()) if collider is Node else "unknown", "object_id": owner_id, "shape": hits[0].get("shape",-1)}
			return false
	current_angle = degrees
	hinge.rotation.y = deg_to_rad(degrees)
	return true



## The caller supplies motion speed and decides when its Core action completes.
## Each physics step checks the next angular sweep before applying it.
func begin_motion(target_degrees: float, speed_degrees_per_second: float) -> bool:
	if not is_inside_tree() or moving or not is_finite(target_degrees) or not is_finite(speed_degrees_per_second) or speed_degrees_per_second <= 0.0 or target_degrees < minimum_angle or target_degrees > maximum_angle: return false
	motion_target = target_degrees
	motion_speed_degrees_per_second = speed_degrees_per_second
	motion_blocked = false
	moving = not is_equal_approx(current_angle,target_degrees)
	return true

func _physics_process(delta: float) -> void:
	if not moving: return
	var next_angle := move_toward(current_angle,motion_target,motion_speed_degrees_per_second*delta)
	if not set_angle_checked(next_angle):
		motion_blocked = true
		moving = false
	elif is_equal_approx(current_angle,motion_target):
		moving = false


## Cancel leaves the leaf at its last checked angle; no completion is emitted.
func cancel_motion() -> void:
	moving = false

