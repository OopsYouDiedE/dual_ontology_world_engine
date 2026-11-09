extends RefCounted
## Single authority for the player's manual locomotion.
## Simulation speed never scales walking speed: accelerated time is only applied
## to idle/waiting simulation. Locomotion animation is driven by real planar
## displacement, so wall contact cannot produce a walking-in-place loop.

const ARRIVE_RADIUS := 0.14
const WALK_STRIDE_MPS := 1.8
const STUCK_GRACE_SECONDS := 0.9
const STUCK_FAIL_SECONDS := 3.2

var host: Node
var body: CharacterBody3D
var camera: Camera3D
var path := PackedVector3Array()
var destination := Vector3.ZERO
var has_destination := false
var arrived := false
var phase := "idle"
var last_speed := 0.0
var blocked_reason := ""
var on_arrived: Callable
var on_blocked: Callable
var on_manual_override: Callable

var _stuck_seconds := 0.0
var _detour_seconds := 0.0
var _detour_sign := 1.0
var _last_position := Vector3.ZERO

func configure(p_host: Node, p_body: CharacterBody3D, p_camera: Camera3D) -> void:
	host = p_host
	body = p_body
	camera = p_camera
	_last_position = body.global_position if is_instance_valid(body) else Vector3.ZERO
	clear()

func clear() -> void:
	path.clear()
	has_destination = false
	arrived = false
	phase = "idle"
	blocked_reason = ""
	_stuck_seconds = 0.0
	_detour_seconds = 0.0
	if is_instance_valid(body):
		body.velocity = Vector3(0.0, -1.0, 0.0)
		body.set_movement_speed(0.0)

func set_path(points: PackedVector3Array, target: Vector3) -> void:
	path = points.duplicate()
	destination = target
	has_destination = true
	arrived = false
	blocked_reason = ""
	phase = "walking"
	_stuck_seconds = 0.0
	_detour_seconds = 0.0
	_last_position = body.global_position if is_instance_valid(body) else Vector3.ZERO

func snapshot() -> Dictionary:
	return {
		"active": has_destination or phase == "manual",
		"phase": phase,
		"path_points": path.size(),
		"destination": [destination.x, destination.y, destination.z],
		"speed_mps": snappedf(last_speed, 0.001),
		"blocked": blocked_reason
	}

static func camera_relative_direction(camera: Camera3D, input: Vector2) -> Vector3:
	if input.length_squared() < 0.01 or not is_instance_valid(camera):
		return Vector3.ZERO
	var right := camera.global_basis.x
	var forward := -camera.global_basis.z
	right.y = 0.0
	forward.y = 0.0
	if right.length_squared() < 0.000001 or forward.length_squared() < 0.000001:
		return Vector3.ZERO
	return (right.normalized() * input.x - forward.normalized() * input.y).normalized()

func tick(delta: float, input_enabled: bool) -> Dictionary:
	var result := {"moved": false, "arrived": false, "blocked": false, "speed": 0.0, "path_changed": false}
	if delta <= 0.0 or not is_instance_valid(body) or not is_instance_valid(host):
		return result
	if not input_enabled or bool(host.get("controller_ai")) or bool(host.get("player_paused")):
		clear()
		return result

	var input := Input.get_vector("move_left", "move_right", "move_up", "move_down")
	if input.length_squared() > 0.0025:
		if has_destination and on_manual_override.is_valid():
			on_manual_override.call()
		has_destination = false
		arrived = false
		path.clear()
		phase = "manual"
		blocked_reason = ""
		var direction := camera_relative_direction(camera, input)
		_stop_if_idle(direction, delta)
		result.moved = _move(direction, _walk_speed(), delta)
		result.speed = last_speed
		return result

	if not has_destination:
		phase = "idle"
		_stop_if_idle(Vector3.ZERO, delta)
		return result

	# Consume reached waypoints. The radius is larger than one frame's travel at
	# normal speed, which prevents the classic orbiting/oscillation failure.
	while not path.is_empty() and _planar_distance(body.global_position, path[0]) <= maxf(ARRIVE_RADIUS, last_speed * delta * 1.25):
		path.remove_at(0)
		result.path_changed = true
	if path.is_empty():
		if is_instance_valid(body):
			body.velocity = Vector3(0.0, -1.0, 0.0)
			body.set_movement_speed(0.0)
		has_destination = false
		arrived = true
		phase = "arrived"
		last_speed = 0.0
		result.arrived = true
		if on_arrived.is_valid():
			on_arrived.call(destination)
		return result

	var waypoint := path[0]
	var direction := _flat_direction(waypoint - body.global_position)
	if direction == Vector3.ZERO:
		direction = _flat_direction(destination - body.global_position)
	if _detour_seconds > 0.0:
		_detour_seconds -= delta
		direction = direction.rotated(Vector3.UP, _detour_sign * 1.05)
	var intended := _walk_speed()
	var moved := _move(direction, intended, delta)
	result.moved = moved
	result.speed = last_speed
	_update_stuck(delta, intended, result)
	result.blocked = not blocked_reason.is_empty()
	return result

func _update_stuck(delta: float, intended: float, result: Dictionary) -> void:
	var travelled := _planar_distance(body.global_position, _last_position)
	_last_position = body.global_position
	if intended <= 0.05 or travelled >= intended * delta * 0.35:
		_stuck_seconds = 0.0
		return
	_stuck_seconds += delta
	if _stuck_seconds >= STUCK_GRACE_SECONDS and _detour_seconds <= 0.0:
		_detour_seconds = 0.55
		_detour_sign = -_detour_sign
	if _stuck_seconds >= STUCK_FAIL_SECONDS:
		blocked_reason = "通路被挡住"
		clear()
		result.blocked = true
		if on_blocked.is_valid():
			on_blocked.call(blocked_reason)

func _stop_if_idle(direction: Vector3, delta: float) -> void:
	if direction == Vector3.ZERO:
		body.velocity.x = 0.0
		body.velocity.z = 0.0
		if body.is_on_floor():
			body.velocity.y = -1.0
		else:
			body.velocity.y -= 9.8 * delta
		body.move_and_slide()
		body.set_movement_speed(0.0)
		last_speed = 0.0

func _move(direction: Vector3, speed: float, delta: float) -> bool:
	var prior := body.global_position
	body.velocity.x = direction.x * speed
	body.velocity.z = direction.z * speed
	if body.is_on_floor():
		body.velocity.y = -1.0
	else:
		body.velocity.y -= 9.8 * delta
	body.face_world_direction(direction)
	body.move_and_slide()
	last_speed = _planar_distance(body.global_position, prior) / maxf(delta, 0.000001)
	body.set_movement_speed(last_speed)
	return last_speed > 0.01

func _walk_speed() -> float:
	return maxf(0.2, float(host.get("settings").get("walking_speed", 1.9)))

func _flat_direction(value: Vector3) -> Vector3:
	var flat := Vector3(value.x, 0.0, value.z)
	return flat.normalized() if flat.length_squared() > 0.000001 else Vector3.ZERO

func _planar_distance(a: Vector3, b: Vector3) -> float:
	return Vector2(a.x - b.x, a.z - b.z).length()
