extends Node3D

## Angles are in degrees. Positive pitch places the camera above the target.
@export var target: Node3D
@export var target_offset := Vector3(0.0, 1.2, 0.0)
@export_range(0.0, 50.0, 0.1) var follow_speed := 8.0
@export var yaw := 0.0
@export_range(-85.0, 85.0, 0.1) var pitch := 20.0
@export var min_pitch := -60.0
@export var max_pitch := 75.0
@export var distance := 5.0
@export var min_distance := 1.0
@export var max_distance := 12.0
@export var input_enabled := true
@export var orbit_input_enabled := true
@export var zoom_input_enabled := true
@export var mouse_sensitivity := 0.2
@export var zoom_step := 0.6
@export_flags_3d_physics var collision_mask := 1
@export_range(0.05, 2.0, 0.01) var collision_radius := 0.2
@export_range(0.1, 30.0, 0.1) var recovery_speed := 5.0
@export_range(1.0, 179.0, 0.1) var field_of_view := 60.0
@export var near_clip := 0.05
@export var far_clip := 1000.0

@onready var spring_arm: SpringArm3D = $SpringArm3D
@onready var camera: Camera3D = $Camera3D
var _excluded_rids: Array[RID] = []
var _orbiting := false
var _exclusion_target: Node3D
var _camera_length := 0.0
var _length_initialized := false

func _ready() -> void:
	_apply_settings()
	_camera_length = distance
	if is_instance_valid(target):
		global_position = target.global_position + target_offset
	refresh_exclusions()

func _physics_process(delta: float) -> void:
	if is_instance_valid(target):
		var weight := 1.0 if follow_speed <= 0.0 else 1.0 - exp(-follow_speed * delta)
		global_position = global_position.lerp(target.global_position + target_offset, weight)
	if target != _exclusion_target:
		refresh_exclusions()
	_apply_settings()

func _process(delta: float) -> void:
	# SpringArm measures the full requested path every physics frame. Retract
	# immediately on contact and ease outward only when the path is clear.
	var available := clampf(spring_arm.get_hit_length(), 0.0, distance)
	if not _length_initialized:
		_camera_length = available
		_length_initialized = true
	elif available < _camera_length:
		_camera_length = available
	else:
		_camera_length = minf(available, lerpf(_camera_length, available, 1.0 - exp(-maxf(recovery_speed, 0.1) * delta)))
	camera.position = spring_arm.transform * Vector3(0, 0, _camera_length)
	camera.rotation = spring_arm.rotation

func _unhandled_input(event: InputEvent) -> void:
	if not input_enabled:
		_orbiting = false
		return
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_RIGHT:
			_orbiting = event.pressed and orbit_input_enabled
		elif event.pressed and zoom_input_enabled:
			if event.button_index == MOUSE_BUTTON_WHEEL_UP:
				zoom(-zoom_step * event.factor)
			elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
				zoom(zoom_step * event.factor)
	elif event is InputEventMouseMotion and _orbiting and orbit_input_enabled:
		if event.button_mask & MOUSE_BUTTON_MASK_RIGHT:
			orbit(-event.relative.x * mouse_sensitivity, -event.relative.y * mouse_sensitivity)
		else:
			_orbiting = false

## Add angle deltas in degrees; usable independently of mouse input.
func orbit(yaw_delta: float, pitch_delta: float) -> void:
	yaw = wrapf(yaw + yaw_delta, -180.0, 180.0)
	pitch = clampf(pitch + pitch_delta, minf(min_pitch, max_pitch), maxf(min_pitch, max_pitch))
	if is_node_ready():
		_apply_settings()

## Add a distance delta in world units; negative values zoom in.
func zoom(distance_delta: float) -> void:
	var lower := maxf(0.05, minf(min_distance, max_distance))
	var upper := maxf(lower, maxf(min_distance, max_distance))
	distance = clampf(distance + distance_delta, lower, upper)
	if is_node_ready():
		_apply_settings()

func _apply_settings() -> void:
	var pitch_low := clampf(minf(min_pitch, max_pitch), -89.0, 89.0)
	var pitch_high := clampf(maxf(min_pitch, max_pitch), -89.0, 89.0)
	pitch = clampf(pitch, pitch_low, pitch_high)
	var lower := maxf(0.05, minf(min_distance, max_distance))
	distance = clampf(distance, lower, maxf(lower, maxf(min_distance, max_distance)))
	spring_arm.rotation = Vector3(deg_to_rad(-pitch), deg_to_rad(yaw), 0.0)
	spring_arm.spring_length = distance
	spring_arm.collision_mask = collision_mask
	var sphere := spring_arm.shape as SphereShape3D
	if sphere:
		sphere.radius = maxf(collision_radius, 0.05)
	camera.fov = clampf(field_of_view, 1.0, 179.0)
	camera.near = maxf(near_clip, 0.01)
	camera.far = maxf(far_clip, camera.near + 0.01)

## Call after adding or removing collision objects under the current target.
func refresh_exclusions() -> void:
	if not is_node_ready():
		return
	_exclusion_target = target
	for rid in _excluded_rids:
		spring_arm.remove_excluded_object(rid)
	_excluded_rids.clear()
	if is_instance_valid(target):
		_exclude_tree(target)

func _exclude_tree(node: Node) -> void:
	if node is CollisionObject3D:
		var rid: RID = node.get_rid()
		spring_arm.add_excluded_object(rid)
		_excluded_rids.append(rid)
	for child in node.get_children():
		_exclude_tree(child)


