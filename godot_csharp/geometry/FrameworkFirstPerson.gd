extends CharacterBody3D
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")
## Inspection controller only; does not commit campaign actions or actor state.
var camera: Camera3D
var active := false
var yaw := 0.0
var pitch := 0.0
var speed := 3.0
var eye_offset := 0.72
var sensitivity := 0.002
var keyboard_look_radians_per_second := 1.8

func configure(radius: float, height: float) -> void:
	collision_layer = PhysicsLayers.ACTOR
	collision_mask = PhysicsLayers.SOLID
	floor_snap_length = 0.35
	floor_max_angle = deg_to_rad(45.0)
	var capsule := CapsuleShape3D.new()
	capsule.radius = radius
	capsule.height = height
	var shape := CollisionShape3D.new()
	shape.shape = capsule
	add_child(shape)
	eye_offset = height * 0.9 - height * 0.5
	camera = Camera3D.new()
	camera.name = "EyeCamera"
	camera.position.y = eye_offset
	camera.fov = 75
	camera.near = 0.05
	camera.far = 300
	add_child(camera)

func activate() -> void:
	active = true
	camera.make_current()
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED

func deactivate() -> void:
	active = false
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE

func _unhandled_input(event: InputEvent) -> void:
	if not active:return
	if event.is_action_pressed("ui_cancel"):
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
		get_viewport().set_input_as_handled()
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	elif event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		yaw -= event.relative.x * sensitivity
		pitch = clampf(pitch-event.relative.y*sensitivity,deg_to_rad(-85),deg_to_rad(85))
		rotation.y = yaw
		camera.rotation.x = pitch

func _physics_process(delta: float) -> void:
	var movement := Vector2.ZERO
	if active:
		yaw += (Input.get_action_strength("camera_left") - Input.get_action_strength("camera_right")) * keyboard_look_radians_per_second * delta
		pitch = clampf(pitch + (Input.get_action_strength("camera_up") - Input.get_action_strength("camera_down")) * keyboard_look_radians_per_second * delta, deg_to_rad(-85), deg_to_rad(85))
		rotation.y = yaw
		camera.rotation.x = pitch
		movement = Input.get_vector("ui_left","ui_right","ui_up","ui_down")
		if Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
			movement += Vector2(float(Input.is_physical_key_pressed(KEY_D))-float(Input.is_physical_key_pressed(KEY_A)),float(Input.is_physical_key_pressed(KEY_S))-float(Input.is_physical_key_pressed(KEY_W)))
		movement = movement.limit_length()
	var direction := global_basis * Vector3(movement.x,0,movement.y)
	velocity.x = direction.x * speed
	velocity.z = direction.z * speed
	if not is_on_floor():velocity.y -= 9.8*delta
	else:velocity.y = 0
	move_and_slide()
