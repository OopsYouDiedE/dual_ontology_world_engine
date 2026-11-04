extends CharacterBody2D
## Logical DV coordinate adapter; all rendered geometry and collisions live in physical_body.
signal arrived
signal conversation_ended(reason: String)

const ARRIVAL_RADIUS := 0.1
const STUCK_DISTANCE := 0.5
const STUCK_SECONDS := 0.5
const MAX_ROUTE_STEPS_PER_TICK := 32

var world_stage: Node3D
var physical_body: CharacterBody3D
var measured_motion_m := 0.0
var unmeasured_room_transitions := 0
var facing := "front"
var target := Vector2.ZERO
var travelling := false
var travel_result := {"ok": false, "reason": "idle"}
var _progress_point := Vector2.ZERO
var _stuck_time := 0.0
var _replans := 0
var _route_epoch := 0
var _replanning := false
var speed := 90.0
var path := PackedVector2Array()
var waypoint := 0
var seated := false
var seat_direction := "front"
var seat_height := 0.0

# 5.07 Lie down
var lying := false
var bedridden := false

# 5.11 Carrying
var carrying := false

func _ready() -> void:
	collision_layer = 0
	collision_mask = 0

func pose(action: String, direction: String = "") -> void:
	set_meta("requested_pose", action)
	if not direction.is_empty():
		facing = direction
		_apply_facing()
	if is_instance_valid(physical_body) and physical_body.has_method("play_action"):
		physical_body.play_action(action)

func _apply_facing() -> void:
	if not is_instance_valid(physical_body): return
	var target_rad: float = {"front": 0.0, "back": PI, "left": -PI/2, "right": PI/2}.get(facing, 0.0)
	if physical_body.has_method("set_target_facing"):
		physical_body.set_target_facing(target_rad)
	elif is_instance_valid(physical_body.visual):
		physical_body.visual.rotation.y = target_rad

# ------------------------------------------------------------------
# 5.06 Sitting
# ------------------------------------------------------------------

func sit_at(direction: String, cushion_height: float, seat_obj: Object = null) -> Dictionary:
	var target_rad: float = {"front": 0.0, "back": PI, "left": -PI/2, "right": PI/2}.get(direction, 0.0)
	if is_instance_valid(physical_body) and physical_body.has_method("sit_down_at"):
		var res = physical_body.sit_down_at(physical_body.global_position, target_rad, cushion_height, seat_obj)
		if res is Dictionary and not res.get("ok", true):
			return res
	seated = true
	seat_direction = direction
	seat_height = cushion_height
	facing = direction
	_apply_facing()
	if not is_instance_valid(physical_body):
		hold_seat()
	return {"ok": true}

func stand_up() -> void:
	seated = false
	if is_instance_valid(physical_body) and physical_body.has_method("stand_up_from_seat"):
		physical_body.stand_up_from_seat()
	else:
		pose("idle")

func hold_seat() -> void:
	pose("sit", seat_direction)

# ------------------------------------------------------------------
# 5.07 Lying down & Bed
# ------------------------------------------------------------------

func lie_at(bed_position: Vector3, bed_yaw: float, stand_pos: Vector3, bed_obj: Object = null, is_bedridden: bool = false) -> Dictionary:
	if is_instance_valid(physical_body) and physical_body.has_method("lie_down_at"):
		var res = physical_body.lie_down_at(bed_position, bed_yaw, stand_pos, bed_obj, is_bedridden)
		if res is Dictionary and not res.get("ok", true):
			return res
	lying = true
	bedridden = is_bedridden
	return {"ok": true}

func stand_up_from_bed() -> bool:
	if not lying or bedridden:
		return false
	lying = false
	if is_instance_valid(physical_body) and physical_body.has_method("stand_up_from_bed"):
		return physical_body.stand_up_from_bed()
	pose("idle")
	return true

# ------------------------------------------------------------------
# 5.08 Conversation
# ------------------------------------------------------------------

func converse_with(partner: Object) -> void:
	if is_instance_valid(physical_body) and physical_body.has_method("start_conversation"):
		physical_body.start_conversation(partner)

func end_conversation() -> void:
	if is_instance_valid(physical_body) and physical_body.has_method("end_conversation"):
		physical_body.end_conversation()

# ------------------------------------------------------------------
# 5.09 Item Interaction (Pick up / Place)
# ------------------------------------------------------------------

func pickup_item(item_node: Node, target_pos: Vector3, height_category: String = "table", side: String = "right") -> Dictionary:
	if is_instance_valid(physical_body) and physical_body.has_method("pickup_item_at"):
		return physical_body.pickup_item_at(item_node, target_pos, height_category, side)
	return {"ok": false, "reason": "no_physical_body"}

func place_item(target_parent: Node, target_pos: Vector3, height_category: String = "table", side: String = "right") -> Dictionary:
	if is_instance_valid(physical_body) and physical_body.has_method("place_item_at"):
		return physical_body.place_item_at(target_parent, target_pos, height_category, side)
	return {"ok": false, "reason": "no_physical_body"}

# ------------------------------------------------------------------
# 5.10 Door / Mechanism Operation
# ------------------------------------------------------------------

func operate_device(stand_slot: Vector3, contact_slot: Vector3, action_type: String = "operate", device_obj: Object = null) -> Dictionary:
	if is_instance_valid(physical_body) and physical_body.has_method("operate_mechanism"):
		return physical_body.operate_mechanism(stand_slot, contact_slot, action_type, device_obj)
	return {"ok": false, "reason": "no_physical_body"}

# ------------------------------------------------------------------
# 5.11 Carrying
# ------------------------------------------------------------------

func start_carry(item_node: Node) -> Dictionary:
	carrying = true
	if is_instance_valid(physical_body) and physical_body.has_method("start_carrying"):
		return physical_body.start_carrying(item_node)
	return {"ok": false, "reason": "no_physical_body"}

func drop_carry() -> Node:
	carrying = false
	if is_instance_valid(physical_body) and physical_body.has_method("stop_carrying"):
		return physical_body.stop_carrying()
	return null

func can_perform_action(action_name: String) -> Dictionary:
	if is_instance_valid(physical_body) and physical_body.has_method("can_perform_action"):
		return physical_body.can_perform_action(action_name)
	return {"allowed": true, "reason": "ok"}

# ------------------------------------------------------------------
# 5.12 Action Queue
# ------------------------------------------------------------------

func enqueue_action(action_id: String, priority: int, data: Dictionary = {}, on_exec: Callable = Callable(), on_cancel: Callable = Callable()) -> bool:
	if is_instance_valid(physical_body) and physical_body.action_queue != null:
		return physical_body.action_queue.enqueue(action_id, priority, data, on_exec, on_cancel)
	return false

func cancel_action(action_id: String) -> bool:
	if is_instance_valid(physical_body) and physical_body.action_queue != null:
		return physical_body.action_queue.cancel_by_id(action_id)
	return false

func clear_action_queue() -> void:
	if is_instance_valid(physical_body) and physical_body.action_queue != null:
		physical_body.action_queue.clear_all()

# ------------------------------------------------------------------
# Navigation & Movement
# ------------------------------------------------------------------

func face_point(point: Vector2) -> void:
	if not seated and not lying and position.distance_squared_to(point) > 1.0:
		face_vector(point - position)
		pose("idle")

func face_vector(direction: Vector2) -> void:
	if absf(direction.x) > absf(direction.y):
		facing = "right" if direction.x > 0 else "left"
	else:
		facing = "front" if direction.y > 0 else "back"
	if is_instance_valid(physical_body) and physical_body.has_method("set_target_facing"):
		var heading: Vector3 = world_stage.logical_heading(position, direction) if is_instance_valid(world_stage) else Vector3(direction.x, 0.0, direction.y)
		heading = physical_body.global_basis.inverse() * heading
		var angle := atan2(heading.x, heading.z)
		physical_body.set_target_facing(angle)

func follow_path(points: PackedVector2Array) -> Dictionary:
	_route_epoch += 1
	_replanning = false
	stop_travel()
	if points.is_empty():
		travel_result = {"ok": false, "reason": "no_route"}
		return travel_result
	path = points.duplicate()
	waypoint = 0
	target = path[0]
	travelling = true
	travel_result = {"ok": false, "reason": "pending"}
	_progress_point = position
	_stuck_time = 0.0
	_replans = 0
	await arrived
	return travel_result

func stop_travel(reason := "cancelled") -> void:
	_route_epoch += 1
	_replanning = false
	if is_instance_valid(physical_body) and physical_body.has_method("interrupt_action"):
		if reason in ["cancelled", "interrupted", "missing_3d_world", "stuck"]:
			physical_body.interrupt_action()
	if travelling or not path.is_empty():
		_finish_travel(false, reason)
	else:
		velocity = Vector2.ZERO
		if is_instance_valid(physical_body) and physical_body.has_method("set_movement_speed"):
			physical_body.set_movement_speed(0.0)
		pose("idle")

func _finish_travel(ok: bool, reason: String) -> void:
	travelling = false
	velocity = Vector2.ZERO
	path.clear()
	_stuck_time = 0.0
	travel_result = {"ok": ok, "reason": reason}
	if is_instance_valid(physical_body) and physical_body.has_method("set_movement_speed"):
		physical_body.set_movement_speed(0.0)
	pose("idle")
	arrived.emit()

func _physics_process(delta: float) -> void:
	if not travelling or _replanning or delta <= 0.0:
		return
	var carry_mod: float = float(physical_body.get("carry_speed_modifier")) if (is_instance_valid(physical_body) and bool(physical_body.get("is_carrying"))) else 1.0
	var current_effective_speed: float = speed * carry_mod
	var remaining: float = current_effective_speed * delta
	for _step in MAX_ROUTE_STEPS_PER_TICK:
		var difference := target - position
		if difference.length() > ARRIVAL_RADIUS:
			if remaining <= 0.0: break
			var motion := difference.limit_length(remaining)
			velocity = motion / delta
			advance_world()
			remaining = maxf(0.0, remaining - motion.length())
			if position.distance_to(target) > ARRIVAL_RADIUS: break
		waypoint += 1
		if waypoint >= path.size():
			_finish_travel(true, "arrived")
			return
		target = path[waypoint]
	if position.distance_to(_progress_point) >= STUCK_DISTANCE:
		_progress_point = position
		_stuck_time = 0.0
	else:
		_stuck_time += delta
		if _stuck_time >= STUCK_SECONDS:
			if _replans < 2 and is_instance_valid(world_stage):
				_replans += 1
				_replanning = true
				_replan_travel(_route_epoch, path[path.size()-1])
				return
			_finish_travel(false, "stuck")

func _replan_travel(epoch: int, destination: Vector2) -> void:
	var revised: PackedVector2Array = await world_stage.navigation_route(position, destination, self)
	if epoch != _route_epoch or not travelling: return
	_replanning = false
	if revised.is_empty():
		_finish_travel(false, "stuck")
		return
	revised.append(destination)
	path = revised
	waypoint = 0
	target = path[0]
	_stuck_time = 0.0

func advance_world() -> void:
	advance_world_motion(velocity * get_physics_process_delta_time())

func advance_world_motion(motion: Vector2) -> void:
	if not is_instance_valid(world_stage):
		stop_travel("missing_3d_world")
		return
	var before := physical_body.global_position if is_instance_valid(physical_body) else Vector3.ZERO
	world_stage.advance(self, motion)
	if is_instance_valid(physical_body) and physical_body.has_method("set_movement_speed"):
		var speed_3d := (physical_body.global_position - before) / maxf(get_physics_process_delta_time(), 0.000001)
		speed_3d.y = 0.0
		physical_body.set_movement_speed(speed_3d.length())
