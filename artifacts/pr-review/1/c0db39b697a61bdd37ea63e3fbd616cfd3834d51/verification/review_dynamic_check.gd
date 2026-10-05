extends SceneTree
## Directed injected-state runtime checks; not a blind playtest.
var host: Node
var failures := 0
var leaf_open_observed := false
var out := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/pr-review/1/c0db39b697a61bdd37ea63e3fbd616cfd3834d51/verification"

func _initialize() -> void:
	call_deferred("_run")

func frames(n: int) -> void:
	for i in n: await physics_frame

func check(ok: bool, detail: String) -> void:
	print("PASS " if ok else "FAIL ", detail)
	if not ok: failures += 1

func screenshot(name: String) -> void:
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(out.path_join(name + ".png"))

func route_to(target: Vector2, detail: String) -> void:
	host._move_to_click(target)
	var initial: int = host.click_path.size()
	for i in 2400:
		await physics_frame
		if host.scene_items.get_node("DormitoryDoor1").leaf_collision.disabled: leaf_open_observed = true
		if host.click_path.is_empty(): break
	var distance: float = host.player.position.distance_to(target)
	check(initial > 0 and distance <= host.CLICK_STOP_RADIUS + 0.1, "%s route=%d remaining=%.2f pos=%s target=%s" % [detail, initial, distance, host.player.position, target])

func _run() -> void:
	host = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(host)
	current_scene = host
	await frames(20)
	var steps := 0
	while host.dialogue.active and steps < 100:
		await host.dialogue.advance()
		await host.dialogue.advance()
		await frames(2)
		steps += 1
	host._close_modal()
	host.red.call("IdentifyInvestigator", "自动回归")
	host.red.call("IntroduceCompanions")
	host.red.call("ReceiveCopperIntroduction")
	host.red_companions_met = true
	host.conversation_staged = true
	host.story_backlog.clear()
	host.investigation_phase = "investigation"
	host.name_entry_active = false
	host.story_lock = false
	host.player.stand_up()
	var door: Node = host.scene_items.get_node("DormitoryDoor1")
	# Place outside the opening radius; allow the live door to close itself.
	host.player.position = door.position + Vector2(100, 40)
	await frames(40)
	check(not door.leaf_collision.disabled and door.is_physics_processing(), "door initially solid and live openness=%.2f" % door.openness)
	print("fixture gates modal=", host.modal_open, " dialogue=", host.dialogue.active, " lock=", host.story_lock, " travelling=", host.player.travelling, " seated=", host.player.seated, " leave=", host.can_leave_detective_room())
	print("walkability origin=", host._walkable(host.player.position), " target=", host._walkable(door.position + Vector2(0, -136)))
	await route_to(door.position + Vector2(0, -136), "actual click movement enters own dorm with live leaf")
	check(leaf_open_observed, "live leaf opens during player approach")
	await route_to(door.position + Vector2(100, 40), "actual click movement exits own dorm")
	host.player.position = host.get_node("MeetingSpots/Detective").position
	await frames(2)
	var before: Vector2 = host.player.position
	var key := InputEventKey.new()
	key.physical_keycode = KEY_D
	key.pressed = true
	Input.parse_input_event(key)
	await frames(15)
	key = InputEventKey.new()
	key.physical_keycode = KEY_D
	key.pressed = false
	Input.parse_input_event(key)
	await frames(3)
	check(host.player.position.x - before.x > 5.0, "held physical D advances %.2f px" % (host.player.position.x - before.x))
	host.player.position = Vector2(248, 424)
	await frames(2)
	await route_to(Vector2(248, 568), "kitchen north opening inward")
	await route_to(Vector2(248, 424), "kitchen north opening outward")
	host.player.position = Vector2(528, 760)
	await frames(2)
	await route_to(Vector2(560, 920), "kitchen south opening outward")
	await route_to(Vector2(528, 760), "kitchen south opening inward")
	var blocker: Node = host.actors["厨师"]
	var old_blocker_position: Vector2 = blocker.position
	blocker.position = Vector2(248, 512)
	host.player.position = Vector2(248, 424)
	await frames(2)
	host._move_to_click(Vector2(248, 568))
	await frames(30)
	await route_to(Vector2(248, 568), "companion in north doorway and repeated click")
	blocker.position = old_blocker_position
	var inspection: Dictionary = host.red.call("Inspect", "E05")
	check(inspection.get("ok", false), "E05 Inspect obtains real evidence")
	host._accept_red_inspection("E05", inspection)
	var record: Dictionary = await host.ObservationReader.read(host.dialogue, host.RED_DIALOGUE, host.Content.observation_title("E05"), host)
	host.red_notes["E05"] = record.paragraphs
	host.story_backlog.clear()
	host._open_recorded_evidence("E05", false)
	await frames(8)
	await screenshot("e05-before-return")
	var book: Node = host.modal.get_node("EvidenceNotebook")
	var button: Button = book.get_node("%Actions").get_child(0)
	print("RETURN before mode=", host.modal.current_mode, " evidence=", host.last_recorded_evidence, " button=", button.text)
	# Use the rendered button's real input path.
	var point: Vector2 = button.get_global_rect().get_center()
	for down in [true, false]:
		var mouse := InputEventMouseButton.new()
		mouse.button_index = MOUSE_BUTTON_LEFT
		mouse.position = point
		mouse.pressed = down
		Input.parse_input_event(mouse)
		await frames(2)
	await frames(6)
	await screenshot("e05-after-return")
	var stuck: bool = host.modal.current_mode == "evidence" and host.last_recorded_evidence == "E05" and host.modal.get_node_or_null("EvidenceNotebook") != null
	print("RETURN after mode=", host.modal.current_mode, " evidence=", host.last_recorded_evidence, " same_page=", stuck)
	check(not stuck, "E05 返回检视 leaves current evidence page")
	host._close_modal()
	host.story_backlog.clear()
	host.ended = true
	host._begin_dialogue("S28")
	await frames(6)
	var exit_steps := 0
	while host.dialogue.active and exit_steps < 100:
		await host.dialogue.advance()
		await host.dialogue.advance()
		await frames(2)
		exit_steps += 1
	for i in 1800:
		await physics_frame
		if not host.story_director.busy: break
	for actor_name in ["侦探", "工程师", "厨师", "音乐家"]:
		var actor: Node = host.actors[actor_name]
		var target := Vector2({"侦探":580,"工程师":620,"厨师":660,"音乐家":700}[actor_name], 936)
		check(actor.position.distance_to(target) <= 8 and not actor.travelling, "S28 %s reached mark=%s actual=%s travelling=%s" % [actor_name, target, actor.position, actor.travelling])
	print("review_dynamic_check failures=", failures)
	quit(1 if failures else 0)
