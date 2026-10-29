extends "res://geometry/FrameworkFirstPerson.gd"
## Physical model inspection; never commits campaign state.
var step_height := 0.22

func _physics_process(delta: float) -> void:
	if active:
		yaw += (Input.get_action_strength("inspect_look_left") - Input.get_action_strength("inspect_look_right")) * delta * 1.3
		rotation.y = yaw
	var before := global_transform
	var grounded := is_on_floor()
	var intended := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down") if active else Vector2.ZERO
	if active and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		intended += Vector2(float(Input.is_physical_key_pressed(KEY_D))-float(Input.is_physical_key_pressed(KEY_A)),float(Input.is_physical_key_pressed(KEY_S))-float(Input.is_physical_key_pressed(KEY_W)))
	var horizontal := global_basis * Vector3(intended.x, 0, intended.y).limit_length() * speed * delta
	super._physics_process(delta)
	if grounded and horizontal.length() > 0.001 and is_on_wall():
		var raised := before
		raised.origin.y += step_height
		var landing := raised
		landing.origin += horizontal
		if not test_move(before,Vector3.UP*step_height) and not test_move(raised,horizontal) and test_move(landing,Vector3.DOWN*(step_height+0.08)):
			global_transform = landing
			velocity.y = 0
			apply_floor_snap()
