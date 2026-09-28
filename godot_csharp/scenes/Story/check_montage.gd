extends SceneTree
## Tests the full montage through M, including its quiet spectators and replay.
var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func press_m() -> void:
	var key := InputEventKey.new()
	key.keycode = KEY_M
	key.physical_keycode = KEY_M
	key.pressed = true
	Input.parse_input_event(key)
	await process_frame
	key.pressed = false
	Input.parse_input_event(key)

func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error(message)

func capture(label: String) -> void:
	if DisplayServer.get_name() == "headless":
		return
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("res://../artifacts/first-three-scenes/daily-" + label + ".png")

func check_no_text(room: Node, label: String) -> void:
	for node in room.find_children("*", "Label", true, false):
		check(not node.is_visible_in_tree() or node.text.is_empty(), label + ": visible text during performance: " + node.text)

func run() -> void:
	for label in ["Breakfast", "SmallLesson", "ParlorGathering"]:
		change_scene_to_file("res://scenes/Story/" + label + ".tscn")
		await process_frame
		await process_frame
		var room := current_scene
		var duration := 7.2 if label == "Breakfast" else 4.0
		print("[montage] ", label)
		await press_m()
		check(room.montage_playing, label + ": M did not start")
		var spectators: Array = []
		if label == "SmallLesson":
			spectators = [room.occupants.get_node("Detective"), room.occupants.get_node("Poet")]
		elif label == "Breakfast":
			spectators = [room.player, room.cast[0]]
		else:
			spectators = [room.player, room.cast[1], room.cast[2]]
		var initial: Array = []
		for person in spectators:
			initial.append([person.position, person.sprite.animation, person.sprite.frame])
		for sample in 3:
			await create_timer(0.8).timeout
			check_no_text(room, label)
			for index in spectators.size():
				var person: Node2D = spectators[index]
				check(person.position.is_equal_approx(initial[index][0]), label + ": spectator moved: " + str(person.name))
				check(person.sprite.animation == initial[index][1] and person.sprite.frame == initial[index][2], label + ": spectator animated: " + str(person.name))
		await capture(label + "-action")
		await create_timer(duration - 2.4 + 0.35).timeout
		check(not room.montage_playing, label + ": performance did not finish")
		if label != "SmallLesson":
			check(room.dialogue.panel.visible, label + ": controls not restored")
		if label == "Breakfast":
			check(is_equal_approx(room.breakfast_performance.bowl.global_position.x, room.cast[1].position.x), "Breakfast: recipient did not keep bowl")
		elif label == "ParlorGathering":
			check(room.cast[3].seated, "Parlor: guest did not accept seat")
		await capture(label)
		await press_m()
		check(room.montage_playing, label + ": replay did not start")
		await create_timer(duration + 0.3).timeout
		check(not room.montage_playing, label + ": replay did not finish")
		if label == "ParlorGathering":
			check(room.cast[3].seated, "Parlor: second arrival failed")
	print("PASS: full wordless montage, quiet spectators, replay and control recovery" if failures.is_empty() else "FAIL: " + "; ".join(failures))
	quit(0 if failures.is_empty() else 1)
