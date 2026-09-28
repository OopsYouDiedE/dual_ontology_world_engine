extends SceneTree
## Runs the actual scene, inputs, dialogue branches and physics; no story state is fabricated.
var room: Node2D
var failures: Array[String] = []
var capture := false

func _initialize() -> void:
	call_deferred("run")

func expect(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func facing_toward(origin: Vector2, target: Vector2) -> String:
	var delta := target - origin
	if absf(delta.x) > absf(delta.y):
		return "right" if delta.x > 0.0 else "left"
	return "front" if delta.y > 0.0 else "back"

func key(code: Key, held: float = 0.06) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	Input.parse_input_event(event)
	await create_timer(held).timeout
	event = InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame

func walk_to(target: Vector2, tolerance: float = 4.0, timeout: float = 8.0) -> bool:
	var deadline := Time.get_ticks_msec() + int(timeout * 1000.0)
	var last_progress := Time.get_ticks_msec()
	while room.player.position.distance_to(target) > tolerance and Time.get_ticks_msec() < deadline:
		var before: Vector2 = room.player.position
		var delta := target - before
		var direction: Key = KEY_D if absf(delta.x) >= absf(delta.y) and delta.x > 0.0 else KEY_A
		if absf(delta.y) > absf(delta.x):
			direction = KEY_S if delta.y > 0.0 else KEY_W
		await key(direction, 0.08)
		if room.player.position.distance_to(before) > 0.1:
			last_progress = Time.get_ticks_msec()
		elif Time.get_ticks_msec() - last_progress > 650:
			return false
	return room.player.position.distance_to(target) <= tolerance

func walk_route(waypoints: Array[Vector2], label: String) -> bool:
	for waypoint in waypoints:
		if not await walk_to(waypoint):
			expect(false, label + ": waypoint不可达 " + str(waypoint) + "，当前位置 " + str(room.player.position))
			return false
	return true

func settle() -> void:
	var deadline := Time.get_ticks_msec() + 15000
	while room.dialogue.busy and Time.get_ticks_msec() < deadline:
		await process_frame
	expect(not room.dialogue.busy, "Story mutation timed out (blocking or animation)")

func click(button: Button) -> void:
	print("[story-check] click target=", button.text, " rect=", button.get_global_rect())
	for down in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = MOUSE_BUTTON_LEFT
		event.position = button.global_position + button.size * 0.5
		event.global_position = event.position
		event.pressed = down
		Input.parse_input_event(event)
		await process_frame

func screenshot(label: String) -> void:
	if not capture:
		return
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("res://../artifacts/first-three-scenes/" + label + ".png")

func play_branch(index: int) -> void:
	await key(KEY_R)
	await settle()
	var selected_option := ""
	var history_choice_lock_checked := false
	var listen_facing_checked := false
	var limit := 0
	while room.dialogue.active and limit < 24 and failures.is_empty():
		limit += 1
		room.dialogue.reveal()
		await process_frame
		if not room.dialogue.line.responses.is_empty():
			await screenshot("choices")
			var button: Button = room.dialogue.choices.get_child(index)
			selected_option = button.text
			var choices_debug: Array[String] = []
			for response in room.dialogue.line.responses:
				choices_debug.append("%s=>%s" % [response.text, response.next_id])
			print("[story-check] options=", choices_debug, " selected=", index, ":", selected_option, " button_next_id=", button.get_meta("dialogue_next_id", "<missing>"), " focus=", room.get_viewport().gui_get_focus_owner())
			if index == 0 and not history_choice_lock_checked:
				var line_before: String = room.dialogue.line.text
				await key(KEY_H)
				expect(room.dialogue.history_panel.visible, "History should open over choices")
				await key(KEY_ENTER)
				await click(button)
				expect(room.dialogue.history_panel.visible, "Choice input should not close history")
				expect(room.dialogue.line.text == line_before and room.response.is_empty(), "History should suspend keyboard and mouse choice input")
				await key(KEY_H)
				history_choice_lock_checked = true
			if index == 0:
				await key(KEY_ENTER)
			else:
				await click(button)
		else:
			await screenshot("dialogue" if limit == 1 else "latest")
			await key(KEY_SPACE)
			if index == 0 and not listen_facing_checked and room.dialogue.line.character == "音乐家" and room.dialogue.line.text == "……先别说话。":
				await process_frame
				expect(room.musician.sprite.sprite_frames == room.musician.special, "Listening mutation should play musician's special action")
				expect(room.musician.facing == facing_toward(room.musician.position, room.clock_item.position), "Musician should face the clock during the listening action")
				listen_facing_checked = true
		await settle()
	expect(not room.dialogue.active, "Dialogue should return control")
	expect(room.completed, "Opening should finish after chosen response")
	var expected_response := "bell" if index == 0 else "heels"
	print("[story-check] choice index=", index, " text=", selected_option, " response=", room.response, " expected=", expected_response)
	expect(room.response == expected_response, "Response mutation did not run")

func run() -> void:
	capture = "--capture" in OS.get_cmdline_user_args()
	root.size = Vector2i(1024, 800)
	room = load("res://scenes/Story/CentralHall.tscn").instantiate()
	root.add_child(room)
	current_scene = room
	await process_frame
	await screenshot("hall")
	# Exercise a real boundary with physics, followed by a reachable prop interaction.
	var reached_north_wall := await walk_to(Vector2(320, 96))
	expect(not reached_north_wall and room.player.position.y >= 128.0, "Player crossed north wall at y=128")
	expect(room.nearest == room.clock_item, "Clock should be reachable from central aisle")
	await key(KEY_E)
	await settle()
	expect(room.dialogue.active, "E should inspect the clock")
	expect(room.player.facing == facing_toward(room.player.position, room.clock_item.position), "Detective should face the clock during its inspection dialogue")
	for i in 2:
		room.dialogue.reveal()
		await key(KEY_SPACE)
		await settle()
	expect(not room.dialogue.active, "Inspection must restore control")
	await play_branch(0)
	var previous: Vector2 = room.player.position
	await key(KEY_S, 0.16)
	expect(room.player.position.y > previous.y, "Walking not restored after dialogue")
	await key(KEY_H)
	expect(room.dialogue.history_panel.visible, "History key did not open history")
	previous = room.player.position
	await key(KEY_D, 0.2)
	expect(room.player.position == previous, "History should suspend movement")
	await screenshot("history")
	await key(KEY_H)
	await play_branch(1)
	# Approach from the far side of the NPC: the scripted route must go around them.
	await walk_route([Vector2(288, 144), Vector2(224, 144)], "approach musician from north")
	expect(room.nearest == room.musician, "Musician should be reachable from the north")
	await key(KEY_E)
	await settle()
	expect(room.player.facing == facing_toward(room.player.position, room.musician.position), "Detective should face the musician after approaching from the north")
	expect(room.musician.facing == facing_toward(room.musician.position, room.player.position), "Musician should face the detective after an E conversation")
	room.dialogue.reveal()
	await key(KEY_ENTER)
	await settle()
	expect(room.player.position.distance_to(Vector2(288, 200)) < 2, "Arrival route failed from north side")
	# End this replay through real dialogue input before geometry capture.
	for i in 20:
		if not room.dialogue.active:
			break
		room.dialogue.reveal()
		await key(KEY_ENTER if not room.dialogue.line.responses.is_empty() else KEY_SPACE)
		await settle()
	await key(KEY_F3)
	await screenshot("geometry")
	var report := "PASS: wall collision, reachable clock via E, keyboard/mouse dialogue choices, both branches, listen action, replay, restored movement, history input lock, approach/replay from north of NPC.\n" if failures.is_empty() else "FAIL:\n" + "\n".join(failures)
	FileAccess.open("res://../artifacts/first-three-scenes/story-validation.txt", FileAccess.WRITE).store_string(report)
	print(report)
	room.queue_free()
	room = null
	await process_frame
	quit(0 if failures.is_empty() else 1)
