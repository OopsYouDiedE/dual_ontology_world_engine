extends CharacterBody2D
## Logical DV coordinate adapter; all rendered geometry and collisions live in physical_body.
signal arrived

const ARRIVAL_RADIUS := 0.1
const STUCK_DISTANCE := 0.5
const STUCK_SECONDS := 0.5
const MAX_ROUTE_STEPS_PER_TICK := 32

var world_stage: Node3D
var physical_body: CharacterBody3D
var facing := "front"
var target := Vector2.ZERO
var travelling := false
var travel_result := {"ok": false, "reason": "idle"}
var _progress_point := Vector2.ZERO
var _stuck_time := 0.0
var _replans := 0
var speed := 90.0
var path := PackedVector2Array()
var waypoint := 0
var seated := false
var seat_direction := "front"
var seat_height := 0.0

func _ready() -> void:
	collision_layer = 0
	collision_mask = 0

func pose(action: String, direction: String = "") -> void:
	set_meta("requested_pose", action)
	if not direction.is_empty(): facing = direction
	_apply_facing()

func _apply_facing() -> void:
	if not is_instance_valid(physical_body) or not is_instance_valid(physical_body.visual): return
	physical_body.visual.rotation.y = {"front": 0.0, "back": PI, "left": -PI/2, "right": PI/2}.get(facing, 0.0)

func sit_at(direction: String, cushion_height: float) -> void:
	# The source models have no sitting clip. Preserve intent without deforming
	# their skeletons or claiming an animated pose has been performed.
	seated = true
	seat_direction = direction
	seat_height = cushion_height
	hold_seat()

func stand_up() -> void:
	seated = false
	pose("idle")

func hold_seat() -> void:
	pose("sit", seat_direction)

func face_point(point: Vector2) -> void:
	if not seated and position.distance_squared_to(point) > 1.0:
		face_vector(point - position)
		pose("idle")

func face_vector(direction: Vector2) -> void:
	if absf(direction.x) > absf(direction.y):
		facing = "right" if direction.x > 0 else "left"
	else:
		facing = "front" if direction.y > 0 else "back"

func follow_path(points: PackedVector2Array) -> Dictionary:
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
	if travelling or not path.is_empty():
		_finish_travel(false, reason)
	else:
		velocity = Vector2.ZERO
		pose("idle")

func _finish_travel(ok: bool, reason: String) -> void:
	travelling = false
	velocity = Vector2.ZERO
	path.clear()
	_stuck_time = 0.0
	travel_result = {"ok": ok, "reason": reason}
	pose("idle")
	arrived.emit()

func _physics_process(delta: float) -> void:
	if not travelling or delta <= 0.0:
		return
	# A waypoint does not consume a whole physics tick. Spend the remaining
	# distance on the next segment, keeping every corner and every collision.
	var remaining := speed * delta
	for _step in MAX_ROUTE_STEPS_PER_TICK:
		var difference := target - position
		if difference.length() > ARRIVAL_RADIUS:
			if remaining <= 0.0: break
			var motion := difference.limit_length(remaining)
			# move_and_slide integrates over the full physics delta on each call.
			# Dividing this segment's motion by that same delta avoids extra speed.
			velocity = motion / delta
			face_vector(difference)
			pose("walk")
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
				var revised: PackedVector2Array = world_stage.navigation_route(position, path[path.size()-1], self)
				if not revised.is_empty():
					revised.append(path[path.size()-1])
					path = revised
					waypoint = 0
					target = path[0]
					_stuck_time = 0.0
					return
			_finish_travel(false, "stuck")

func advance_world() -> void:
	advance_world_motion(velocity * get_physics_process_delta_time())

func advance_world_motion(motion: Vector2) -> void:
	if not is_instance_valid(world_stage):
		stop_travel("missing_3d_world")
		return
	world_stage.advance(self, motion)
