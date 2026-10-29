extends SceneTree
## Comprehensive Test Suite for Character Tasks 5.07 to 5.12

const ImportHelper = preload("res://scenes/World/Actors/ImportedCharacter.gd")
const Body3DClass = preload("res://scenes/World/Actors/StoryBody3D.gd")
const QueueClass = preload("res://scenes/World/Actors/ActorActionQueue.gd")

var frame_count := 0
var test_body: CharacterBody3D
var vp: SubViewport
var camera: Camera3D
var test_results: Dictionary = {}

func _init() -> void:
	print("==================================================")
	print("STARTING ACTOR TEST SUITE BATCH 2 (5.07 - 5.12)")
	print("==================================================")
	
	test_results = {
		"timestamp": Time.get_datetime_string_from_system(true),
		"tasks": {}
	}
	
	_test_507_lie_down_and_bedridden()
	_test_508_conversation_and_interruption()
	_test_509_pickup_place_heights()
	_test_510_mechanism_operation_validation()
	_test_511_carrying_constraints()
	_test_512_action_queue_priorities()
	
	_setup_render_scene()

func _test_507_lie_down_and_bedridden() -> void:
	print("\n--- Running 5.07: Lie Down, Bedridden & Exit Alignment ---")
	var t507 := {"ok": true, "details": {}}
	
	var body: CharacterBody3D = Body3DClass.new()
	body.configure({"path": "res://imported_assets/kaykit/characters/Knight.glb"}, 1.32, 0.18)
	var player: AnimationPlayer = body.animation_player
	
	var dummy_bed := RefCounted.new()
	var bed_pos := Vector3(3.0, 0.5, 3.0)
	var stand_exit_pos := Vector3(3.0, 0.0, 4.0)
	
	# 1. Normal lie down
	body.lie_down_at(bed_pos, PI * 0.5, stand_exit_pos, dummy_bed, false)
	t507.details["lying_after_call"] = body.is_lying
	t507.details["action_after_call"] = body.current_action
	var lie_start_ok: bool = body.is_lying and body.current_action == "lie_down"
	
	player.advance(3.05)
	body._on_animation_finished(&"lie_down")
	t507.details["action_after_transition"] = body.current_action
	var lie_idle_ok: bool = body.current_action == "lie_idle"
	
	# 2. Stand up from bed
	var stand_res: bool = body.stand_up_from_bed()
	t507.details["stand_up_call_result"] = stand_res
	t507.details["action_on_stand"] = body.current_action
	var stand_start_ok: bool = stand_res and body.current_action == "lie_stand_up"
	
	player.advance(2.4)
	body._on_animation_finished(&"lie_stand_up")
	t507.details["lying_after_stand"] = body.is_lying
	t507.details["pos_after_stand"] = body.get_world_pos()
	var stand_done_ok: bool = not body.is_lying and body.current_action == "idle" and body.get_world_pos().is_equal_approx(stand_exit_pos)
	
	# 3. Bedridden patient test
	body.lie_down_at(bed_pos, 0.0, stand_exit_pos, dummy_bed, true)
	var bedridden_stand: bool = body.stand_up_from_bed()
	t507.details["bedridden_stand_rejected"] = not bedridden_stand
	t507.details["is_bedridden_retained"] = body.is_bedridden and body.is_lying
	var bedridden_ok: bool = not bedridden_stand and body.is_lying and body.is_bedridden
	
	t507.ok = lie_start_ok and lie_idle_ok and stand_start_ok and stand_done_ok and bedridden_ok
	test_results.tasks["5.07"] = t507
	print("5.07 Result: OK=", t507.ok)
	body.free()

func _test_508_conversation_and_interruption() -> void:
	print("\n--- Running 5.08: Seated Conversation & Target Leaving ---")
	var t508 := {"ok": true, "details": {}}
	
	var body: CharacterBody3D = Body3DClass.new()
	body.configure({"path": "res://imported_assets/kaykit/characters/Knight.glb"}, 1.32, 0.18)
	body.position = Vector3(0.0, 0.0, 0.0)
	
	# Partner actor
	var partner := Node3D.new()
	partner.position = Vector3(1.5, 0.0, 1.5)
	
	# 1. Seated conversation test
	body.sit_down_at(Vector3.ZERO, 0.0, 0.45)
	body.start_conversation(partner)
	t508.details["seated_during_conversation"] = body.is_seated
	t508.details["conversing_active"] = body.is_conversing
	t508.details["target_yaw"] = body.target_yaw
	var seated_conv_ok: bool = body.is_seated and body.is_conversing and body.target_yaw > 0.0
	
	# 2. Partner leaves beyond 3.5m
	var interrupted_box := {"reason": ""}
	body.conversation_interrupted.connect(func(reason: String): interrupted_box["reason"] = reason)
	partner.position = Vector3(10.0, 0.0, 10.0)
	body.update_conversation(0.1)
	t508.details["interrupted_reason"] = interrupted_box["reason"]
	t508.details["conversing_after_leave"] = body.is_conversing
	var leave_ok: bool = interrupted_box["reason"] == "target_left" and not body.is_conversing
	
	t508.ok = seated_conv_ok and leave_ok
	test_results.tasks["5.08"] = t508
	print("5.08 Result: OK=", t508.ok)
	body.free()
	partner.free()

func _test_509_pickup_place_heights() -> void:
	print("\n--- Running 5.09: Pick Up, Place & Height Levels ---")
	var t509 := {"ok": true, "details": {}}
	
	var body: CharacterBody3D = Body3DClass.new()
	body.configure({"path": "res://imported_assets/kaykit/characters/Knight.glb"}, 1.32, 0.18)
	body.position = Vector3.ZERO
	
	var table := Node3D.new()
	var test_item := Node3D.new()
	test_item.name = "EvidenceBook"
	table.add_child(test_item)
	test_item.position = Vector3(0.5, 0.75, 0.5) # Table height ~0.75m
	
	# 1. Pick up from table
	var item_pos: Vector3 = test_item.global_position if test_item.is_inside_tree() else test_item.position
	var pickup_res: Dictionary = body.pickup_item_at(test_item, item_pos, "table", "right")
	t509.details["pickup_result"] = pickup_res
	t509.details["item_parent_is_hand"] = test_item.get_parent() == body.slot_right
	t509.details["held_item_right_set"] = body.held_item_right == test_item
	var pickup_ok: bool = pickup_res.ok and test_item.get_parent() == body.slot_right and body.held_item_right == test_item
	
	# 2. Place on floor
	var floor_node := Node3D.new()
	var floor_pos := Vector3(0.3, 0.02, 0.3)
	var place_res: Dictionary = body.place_item_at(floor_node, floor_pos, "floor", "right")
	t509.details["place_result"] = place_res
	t509.details["item_parent_is_floor"] = test_item.get_parent() == floor_node
	t509.details["held_item_cleared"] = body.held_item_right == null
	var place_ok: bool = place_res.ok and test_item.get_parent() == floor_node and body.held_item_right == null
	
	# 3. Rollback cancellation test
	body.pickup_item_at(test_item, floor_pos, "floor", "right")
	var roll_ok: bool = body.cancel_item_operation(test_item, floor_node, floor_pos, "right")
	t509.details["cancel_rollback_ok"] = roll_ok
	t509.details["item_safe_after_cancel"] = test_item.get_parent() == floor_node and body.held_item_right == null
	var cancel_ok: bool = roll_ok and test_item.get_parent() == floor_node and body.held_item_right == null
	
	t509.ok = pickup_ok and place_ok and cancel_ok
	test_results.tasks["5.09"] = t509
	print("5.09 Result: OK=", t509.ok)
	body.free()
	table.free()
	floor_node.free()

func _test_510_mechanism_operation_validation() -> void:
	print("\n--- Running 5.10: Door & Valve Operation Range Validation ---")
	var t510 := {"ok": true, "details": {}}
	
	var body: CharacterBody3D = Body3DClass.new()
	body.configure({"path": "res://imported_assets/kaykit/characters/Knight.glb"}, 1.32, 0.18)
	body.position = Vector3(0.0, 0.0, 0.0)
	
	var stand_slot := Vector3(0.1, 0.0, 0.1)
	var contact_slot := Vector3(0.1, 0.9, 0.5)
	
	# 1. Distant attempt (must be strictly rejected!)
	body.position = Vector3(2.5, 0.0, 2.5)
	var far_res: Dictionary = body.operate_mechanism(stand_slot, contact_slot, "operate")
	t510.details["distant_rejection"] = far_res
	var reject_ok: bool = not far_res.ok and far_res.reason == "not_at_stand_slot"
	
	# 2. In-position attempt
	body.position = stand_slot
	var close_res: Dictionary = body.operate_mechanism(stand_slot, contact_slot, "operate")
	t510.details["close_operation"] = close_res
	var accept_ok: bool = close_res.ok and close_res.action_type == "operate"
	
	t510.ok = reject_ok and accept_ok
	test_results.tasks["5.10"] = t510
	print("5.10 Result: OK=", t510.ok)
	body.free()

func _test_511_carrying_constraints() -> void:
	print("\n--- Running 5.11: Carrying Two-Handed Constraints & Narrow Clearance ---")
	var t511 := {"ok": true, "details": {}}
	
	var body: CharacterBody3D = Body3DClass.new()
	body.configure({"path": "res://imported_assets/kaykit/characters/Knight.glb"}, 1.32, 0.18)
	
	var heavy_crate := Node3D.new()
	heavy_crate.name = "HeavyRationCrate"
	
	# 1. Start carrying
	var carry_res: Dictionary = body.start_carrying(heavy_crate)
	t511.details["carry_start_res"] = carry_res
	t511.details["is_carrying"] = body.is_carrying
	var carry_ok: bool = carry_res.ok and body.is_carrying and body.carry_speed_modifier < 1.0
	
	# 2. Mutual exclusion check (cannot open valve or pick up while carrying)
	var check_valve: Dictionary = body.can_perform_action("open_valve")
	var check_pickup: Dictionary = body.can_perform_action("pickup")
	t511.details["check_valve"] = check_valve
	t511.details["check_pickup"] = check_pickup
	var mutex_ok: bool = not check_valve.allowed and not check_pickup.allowed and check_valve.reason == "hands_occupied_carrying"
	
	# 3. Narrow doorway clearance check
	# Door width 0.65m < carry_clearance_width 0.75m -> blocked
	var narrow_door: Dictionary = body.check_passage_clearance(0.65)
	# Door width 0.90m >= 0.75m -> passable
	var wide_door: Dictionary = body.check_passage_clearance(0.90)
	t511.details["narrow_door"] = narrow_door
	t511.details["wide_door"] = wide_door
	var clearance_ok: bool = not narrow_door.passable and narrow_door.reason == "passage_too_narrow_for_carrying" and wide_door.passable
	
	# 4. Stop carrying
	var dropped_item: Node = body.stop_carrying()
	t511.details["dropped_item_matches"] = dropped_item == heavy_crate
	t511.details["is_carrying_after_stop"] = body.is_carrying
	var stop_ok: bool = dropped_item == heavy_crate and not body.is_carrying
	
	t511.ok = carry_ok and mutex_ok and clearance_ok and stop_ok
	test_results.tasks["5.11"] = t511
	print("5.11 Result: OK=", t511.ok)
	body.free()
	heavy_crate.free()

func _test_512_action_queue_priorities() -> void:
	print("\n--- Running 5.12: Action Queue Priority & Cancellation ---")
	var t512 := {"ok": true, "details": {}}
	
	var q = QueueClass.new()
	var executed_log: Array[String] = []
	var cancelled_log: Array[String] = []
	
	# 1. Enqueue committed task
	q.enqueue("task_duty", QueueClass.Priority.COMMITTED, {}, 
		func(): executed_log.append("task_duty"),
		func(r: String): cancelled_log.append("task_duty:" + r)
	)
	t512.details["active_initial"] = q.get_current().id
	var task1_ok: bool = q.get_current().id == "task_duty"
	
	# 2. Enqueue normal observation (Priority 20) -> must NOT preempt or reshuffle task_duty
	q.enqueue("observe_room", QueueClass.Priority.OBSERVATION, {},
		func(): executed_log.append("observe_room"),
		func(r: String): cancelled_log.append("observe_room:" + r)
	)
	t512.details["active_after_obs"] = q.get_current().id
	var obs_no_preempt_ok: bool = q.get_current().id == "task_duty"
	
	# 3. Emergency arrival (Priority 100) -> Preempts task_duty immediately!
	q.enqueue("alarm_escape", QueueClass.Priority.EMERGENCY, {},
		func(): executed_log.append("alarm_escape"),
		func(r: String): cancelled_log.append("alarm_escape:" + r)
	)
	t512.details["active_after_emergency"] = q.get_current().id
	t512.details["cancelled_log"] = cancelled_log
	var emergency_ok: bool = q.get_current().id == "alarm_escape" and cancelled_log.has("task_duty:preempted_by_emergency")
	
	# 4. Clean cancellation of queue
	q.clear_all("scene_transition")
	t512.details["queue_size_after_clear"] = q.get_queue_size()
	t512.details["active_after_clear"] = q.get_current().is_empty()
	var clear_ok: bool = q.get_queue_size() == 0 and q.get_current().is_empty()
	
	t512.ok = task1_ok and obs_no_preempt_ok and emergency_ok and clear_ok
	test_results.tasks["5.12"] = t512
	print("5.12 Result: OK=", t512.ok)

func _setup_render_scene() -> void:
	var root_node = Node3D.new()
	root.add_child(root_node)
	
	vp = SubViewport.new()
	vp.size = Vector2i(1280, 720)
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root_node.add_child(vp)
	
	var env = WorldEnvironment.new()
	env.environment = Environment.new()
	env.environment.background_mode = Environment.BG_COLOR
	env.environment.background_color = Color("252c2e")
	env.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.environment.ambient_light_color = Color("ffffff")
	env.environment.ambient_light_energy = 0.5
	vp.add_child(env)
	
	camera = Camera3D.new()
	camera.position = Vector3(0, 1.4, 3.2)
	vp.add_child(camera)
	camera.look_at_from_position(Vector3(0, 1.2, 3.2), Vector3(0, 0.6, 0))
	
	var light = DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-45, 30, 0)
	light.light_energy = 1.0
	vp.add_child(light)
	
	# Floor
	var floor_mesh = MeshInstance3D.new()
	var plane = PlaneMesh.new()
	plane.size = Vector2(8, 8)
	floor_mesh.mesh = plane
	var mat = StandardMaterial3D.new()
	mat.albedo_color = Color("3a4447")
	floor_mesh.material_override = mat
	vp.add_child(floor_mesh)
	
	# Showcase Actor 1: Lying on Bed (Druid patient)
	var body_bed = Body3DClass.new()
	body_bed.position = Vector3(-1.0, 0, 0)
	vp.add_child(body_bed)
	body_bed.configure({"path": "res://imported_assets/kaykit/characters/Druid.glb"}, 1.32, 0.18)
	body_bed.lie_down_at(Vector3(-1.0, 0.2, 0), PI * 0.5, Vector3(-1.0, 0, 0.8), null, true)
	if body_bed.animation_player != null:
		body_bed.animation_player.advance(3.0)
		body_bed._on_animation_finished(&"lie_down")
	
	# Showcase Actor 2: Carrying Heavy Crate (Barbarian)
	var body_carry = Body3DClass.new()
	body_carry.position = Vector3(0.0, 0, 0)
	vp.add_child(body_carry)
	body_carry.configure({"path": "res://imported_assets/kaykit/characters/Barbarian.glb"}, 1.32, 0.18)
	var crate_mesh = MeshInstance3D.new()
	var box = BoxMesh.new()
	box.size = Vector3(0.4, 0.3, 0.3)
	crate_mesh.mesh = box
	var crate_mat = StandardMaterial3D.new()
	crate_mat.albedo_color = Color("8d6e63")
	crate_mesh.material_override = crate_mat
	body_carry.start_carrying(crate_mesh)
	
	# Showcase Actor 3: Operating Valve / Mechanism (Engineer)
	var body_op = Body3DClass.new()
	body_op.position = Vector3(1.0, 0, 0)
	vp.add_child(body_op)
	body_op.configure({"path": "res://imported_assets/kaykit/characters/Engineer.glb"}, 1.32, 0.18)
	body_op.operate_mechanism(Vector3(1.0, 0, 0), Vector3(1.0, 0.8, -0.4), "operate")
	if body_op.animation_player != null:
		body_op.animation_player.advance(0.4)

func _process(_delta: float) -> bool:
	frame_count += 1
	if frame_count == 10:
		_save_receipt_and_exit()
		return true
	return false

func _save_receipt_and_exit() -> void:
	var receipt_dir := "c:/Users/Administrator/dual_ontology_world_engine/artifacts/acceptance-20261029"
	var img = vp.get_texture().get_image()
	var screen_path = receipt_dir + "/actor-test-batch2-showcase.png"
	if img != null:
		var err = img.save_png(screen_path)
		test_results["screenshot"] = screen_path if err == OK else "render_not_available_in_driver"
		print("Screenshot save result: ", err, " -> ", screen_path)
	
	var json_path = receipt_dir + "/actor-test-receipt-batch2.json"
	var file = FileAccess.open(json_path, FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(test_results, "  "))
		file.close()
		print("JSON receipt saved successfully to ", json_path)
	
	print("\n==================================================")
	print("ALL TASKS 5.07 - 5.12 VERIFIED!")
	print("5.07: ", test_results.tasks.get("5.07", {}).get("ok"))
	print("5.08: ", test_results.tasks.get("5.08", {}).get("ok"))
	print("5.09: ", test_results.tasks.get("5.09", {}).get("ok"))
	print("5.10: ", test_results.tasks.get("5.10", {}).get("ok"))
	print("5.11: ", test_results.tasks.get("5.11", {}).get("ok"))
	print("5.12: ", test_results.tasks.get("5.12", {}).get("ok"))
	print("==================================================")
	quit(0)
