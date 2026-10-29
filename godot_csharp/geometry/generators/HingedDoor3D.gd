extends Node3D
## A hinged leaf with lock, latch, inner/outer interaction slots and sweep collision.
## Local +X spans the leaf; its pivot is the hinge at floor height (Tasks 2.14, 2.15, 2.16).
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

# Lock and latch state from Core (Task 2.15)
var is_locked := false
var locked_side := "both" # "both", "outer" (latched from inside)
var key_id := ""

static func create(id: String, width: float, height: float, thickness: float, minimum_degrees: float, maximum_degrees: float, material: Material) -> Node3D:
	if id.is_empty() or not is_finite(width) or not is_finite(height) or not is_finite(thickness) or not is_finite(minimum_degrees) or not is_finite(maximum_degrees) or width <= 0.0 or height <= 0.0 or thickness <= 0.0 or minimum_degrees > 0.0 or maximum_degrees < 0.0 or minimum_degrees >= maximum_degrees:
		push_error("HingedDoor3D " + id + ": invalid-parameters")
		return null
	var root = load("res://geometry/generators/HingedDoor3D.gd").new()
	root.name = "WorldObject3D"
	root.set_meta("world_object_id", id)
	root.set_meta("kind", "door_panel")
	root.leaf_width = width
	root.leaf_height = height
	root.leaf_thickness = thickness
	root.minimum_angle = minimum_degrees
	root.maximum_angle = maximum_degrees

	# 1. VisualRoot with Hinge hierarchy
	var visual_root := Node3D.new()
	visual_root.name = "VisualRoot"
	root.add_child(visual_root)

	root.hinge = Node3D.new()
	root.hinge.name = "Hinge"
	visual_root.add_child(root.hinge)

	var visual := MeshInstance3D.new()
	visual.name = "DoorLeafMesh"
	var mesh := BoxMesh.new()
	mesh.size = Vector3(width, height, thickness)
	visual.mesh = mesh
	visual.material_override = material
	visual.position = Vector3(width / 2.0, height / 2.0, 0.0)
	root.hinge.add_child(visual)

	# 2. Door hardware: Handles, Latch and Keyhole (Task 2.15)
	var metal_mat := StandardMaterial3D.new()
	metal_mat.albedo_color = Color("383a37")
	metal_mat.metallic = 0.8
	metal_mat.roughness = 0.3

	var handle_outer := MeshInstance3D.new()
	handle_outer.name = "Handle_Outer"
	var handle_mesh := BoxMesh.new()
	handle_mesh.size = Vector3(0.12, 0.03, 0.05)
	handle_outer.mesh = handle_mesh
	handle_outer.material_override = metal_mat
	handle_outer.position = Vector3(width * 0.85, 0.98, thickness / 2.0 + 0.025)
	root.hinge.add_child(handle_outer)

	var handle_inner := MeshInstance3D.new()
	handle_inner.name = "Handle_Inner"
	handle_inner.mesh = handle_mesh
	handle_inner.material_override = metal_mat
	handle_inner.position = Vector3(width * 0.85, 0.98, -thickness / 2.0 - 0.025)
	root.hinge.add_child(handle_inner)

	var latch_inner := MeshInstance3D.new()
	latch_inner.name = "Latch_Inner"
	var latch_mesh := BoxMesh.new()
	latch_mesh.size = Vector3(0.06, 0.04, 0.03)
	latch_inner.mesh = latch_mesh
	latch_inner.material_override = metal_mat
	latch_inner.position = Vector3(width * 0.85, 1.10, -thickness / 2.0 - 0.015)
	root.hinge.add_child(latch_inner)

	# 3. CollisionRoot under Hinge so collision sweeps with leaf
	var collision_root := Node3D.new()
	collision_root.name = "CollisionRoot"
	root.hinge.add_child(collision_root)

	root.body = StaticBody3D.new()
	root.body.name = "StaticBody3D"
	root.body.collision_layer = PhysicsLayers.SOLID
	root.body.collision_mask = 0
	collision_root.add_child(root.body)

	root.box = BoxShape3D.new()
	root.box.size = Vector3(width, height, thickness)
	var collider := CollisionShape3D.new()
	collider.name = "CollisionShape3D"
	collider.shape = root.box
	collider.position = Vector3(width / 2.0, height / 2.0, 0.0)
	root.body.add_child(collider)

	# 4. InteractionArea
	var area := Area3D.new()
	area.name = "InteractionArea"
	area.collision_layer = PhysicsLayers.INTERACTION
	area.collision_mask = PhysicsLayers.ACTOR
	root.add_child(area)
	var area_shape := CollisionShape3D.new()
	var area_box := BoxShape3D.new()
	area_box.size = Vector3(width + 0.6, height, 1.8)
	area_shape.shape = area_box
	area_shape.position = Vector3(width / 2.0, height / 2.0, 0.0)
	area.add_child(area_shape)

	# 5. InteractionSlots with inner/outer distinct operation faces (Task 2.15)
	var slots := Node3D.new()
	slots.name = "InteractionSlots"
	root.add_child(slots)

	var slot_defs := [
		["Approach", Vector3(width * 0.8, 0, 0.6)],
		["Approach_Inner", Vector3(width * 0.8, 0, -0.6)],
		["Approach_Outer", Vector3(width * 0.8, 0, 0.6)],
		["Stand", Vector3(width * 0.8, 0, 0.6)],
		["FaceTarget", Vector3(width * 0.85, 0.98, 0)],
		["Contact", Vector3(width * 0.85, 0.98, 0)],
		["Contact_Inner", Vector3(width * 0.85, 0.98, -thickness / 2.0 - 0.03)],
		["Contact_Outer", Vector3(width * 0.85, 0.98, thickness / 2.0 + 0.03)]
	]
	for pair in slot_defs:
		var marker := Marker3D.new()
		marker.name = pair[0]
		marker.position = pair[1]
		slots.add_child(marker)

	# 6. StateVisuals and LabelAnchor
	var states := Node3D.new()
	states.name = "StateVisuals"
	root.add_child(states)

	var label := Label3D.new()
	label.name = "LabelAnchor"
	label.text = ""
	label.position = Vector3(width * 0.85, 1.25, 0)
	label.visible = false
	root.add_child(label)

	return root

func is_closed() -> bool:
	return is_equal_approx(current_angle, 0.0)

## Sets lock state authorized by Core (Task 2.15).
func set_lock_state(locked: bool, side: String = "both", required_key: String = "") -> void:
	is_locked = locked
	locked_side = side
	key_id = required_key

## Evaluates whether operation from world position is allowed under Core lock rules (Task 2.15).
func can_operate_from_position(world_pos: Vector3, has_key: bool = false, held_key_id: String = "") -> Dictionary:
	var local_pos := to_local(world_pos) if is_inside_tree() else (transform.affine_inverse() * world_pos)
	var is_outer_side := local_pos.z >= 0.0
	if is_locked:
		if locked_side == "both" or (locked_side == "outer" and is_outer_side):
			if has_key and (held_key_id == key_id or key_id.is_empty()):
				return {"allowed": true, "operation": "unlock_and_open"}
			return {"allowed": false, "reason": "door_locked_needs_key"}
	if locked_side == "outer" and is_outer_side and is_closed():
		return {"allowed": false, "reason": "latched_from_inside"}
	var op := "close" if not is_closed() else "open"
	return {"allowed": true, "operation": op}

## Returns false without changing angle when the sweep encounters a solid or actor.
## Call only while inside the scene tree, after the physics world is available.
func set_angle_checked(degrees: float) -> bool:
	if not is_finite(degrees) or degrees < minimum_angle or degrees > maximum_angle: return false
	if not is_inside_tree():
		current_angle = clampf(degrees, minimum_angle, maximum_angle)
		if hinge != null:
			hinge.rotation.y = deg_to_rad(current_angle)
		return true
	last_blocker = {}
	last_sweep_max_gap_m = 0.0
	var difference := deg_to_rad(degrees - current_angle)
	if absf(difference) < 0.000001: return true
	var furthest_radius := Vector2(leaf_width, leaf_thickness / 2.0).length()
	var steps := maxi(1, ceili(maxf(absf(difference) / MAX_SWEEP_STEP_RAD, absf(difference) * furthest_radius / MAX_SWEEP_STEP_M)))
	last_sweep_max_gap_m = furthest_radius * absf(difference) / float(steps)
	var query := PhysicsShapeQueryParameters3D.new()
	query.shape = box
	query.collision_mask = PhysicsLayers.SOLID | PhysicsLayers.ACTOR
	query.collide_with_bodies = true
	query.collide_with_areas = false
	query.exclude = [body.get_rid()]
	var space := get_world_3d().direct_space_state
	for i in range(1, steps + 1):
		var angle := deg_to_rad(lerpf(current_angle, degrees, float(i) / float(steps)))
		query.transform = global_transform * Transform3D(Basis(Vector3.UP, angle), Vector3.ZERO) * Transform3D(Basis.IDENTITY, Vector3(leaf_width / 2.0, leaf_height / 2.0, 0.0))
		var hits := space.intersect_shape(query, 1)
		if not hits.is_empty():
			var collider: Object = hits[0].get("collider")
			var owner_id := ""
			var ancestor := collider as Node
			while ancestor != null:
				if ancestor.has_meta("world_object_id"):
					owner_id = str(ancestor.get_meta("world_object_id"))
					break
				ancestor = ancestor.get_parent()
			last_blocker = {"angle_degrees": rad_to_deg(angle), "path": str(collider.get_path()) if collider is Node else "unknown", "object_id": owner_id, "shape": hits[0].get("shape", -1)}
			return false
	current_angle = clampf(degrees, minimum_angle, maximum_angle)
	hinge.rotation.y = deg_to_rad(current_angle)
	return true

## The caller supplies motion speed and decides when its Core action completes.
## Each physics step checks the next angular sweep before applying it.
func begin_motion(target_degrees: float, speed_degrees_per_second: float) -> bool:
	if not is_inside_tree() or moving or not is_finite(target_degrees) or not is_finite(speed_degrees_per_second) or speed_degrees_per_second <= 0.0 or target_degrees < minimum_angle or target_degrees > maximum_angle: return false
	motion_target = target_degrees
	motion_speed_degrees_per_second = speed_degrees_per_second
	motion_blocked = false
	moving = not is_equal_approx(current_angle, target_degrees)
	return true

func _physics_process(delta: float) -> void:
	if not moving: return
	var next_angle := move_toward(current_angle, motion_target, motion_speed_degrees_per_second * delta)
	if not set_angle_checked(next_angle):
		motion_blocked = true
		moving = false
	elif is_equal_approx(current_angle, motion_target):
		moving = false

## Cancel leaves the leaf at its last checked angle; no completion is emitted.
func cancel_motion() -> void:
	moving = false


