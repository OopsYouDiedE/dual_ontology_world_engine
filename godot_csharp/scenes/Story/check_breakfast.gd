extends SceneTree
## Exercises the breakfast performance through the same input path as the game.

const BREAKFAST_SCENE := "res://scenes/Story/Breakfast.tscn"
var failures: Array[String] = []
var room: Node
var completion_count := 0

func _initialize() -> void:
	call_deferred("run")

func expect(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error(message)

func send_key(code: Key, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)
	await process_frame

func tap_key(code: Key) -> void:
	await send_key(code, true)
	await send_key(code, false)

func wait_for_performance_end(label: String) -> void:
	var duration := float(room.breakfast_performance.DURATION)
	var deadline := Time.get_ticks_msec() + int((duration + 3.0) * 1000.0)
	while room.breakfast_performance.active and Time.get_ticks_msec() < deadline:
		await process_frame
	expect(not room.breakfast_performance.active, label + ": performance did not finish")
	if room.breakfast_performance.active:
		return
	await process_frame

func check_locked_inputs() -> void:
	var held_scene := room.scene_file_path
	var held_position: Vector2 = room.player.position
	var completed_before := completion_count
	await tap_key(KEY_M)
	await tap_key(KEY_R)
	await tap_key(KEY_E)
	await tap_key(KEY_F8)
	# Physical input is released on the next frame so it cannot repeat.
	await send_key(KEY_W, true)
	await process_frame
	await send_key(KEY_W, false)
	await process_frame
	expect(room.scene_file_path == held_scene, "performance input lock: F8 changed scene")
	expect(room.player.position.distance_to(held_position) < 0.1, "performance input lock: W moved the player")
	expect(room.montage_playing, "performance input lock: ignored input stopped montage state")
	expect(room.breakfast_performance.active, "performance input lock: M restarted or interrupted the performance")
	expect(completion_count == completed_before, "performance input lock: performance completed during ignored input")
	expect(not room.dialogue.panel.visible, "performance input lock: dialogue panel became visible")
	expect(not room.caption.visible, "performance input lock: interaction caption became visible")

func run_performance(label: String, exercise_lock: bool) -> void:
	var completed_before := completion_count
	await tap_key(KEY_M)
	expect(room.breakfast_performance.active, label + ": M did not start performance")
	var duration := float(room.breakfast_performance.DURATION)
	expect(is_equal_approx(duration, 7.2), label + ": expected performance duration to be 7.2 seconds")
	expect(room.breakfast_performance.elapsed >= 0.0 and room.breakfast_performance.elapsed < duration, label + ": performance elapsed is outside its duration")
	expect(room.breakfast_performance.stage is String and not room.breakfast_performance.stage.is_empty(), label + ": performance stage is missing")
	expect(room.breakfast_performance.bowl is Sprite2D, label + ": performance bowl reference is missing")
	expect(not room.dialogue.panel.visible, label + ": dialogue panel stayed visible")
	expect(not room.caption.visible, label + ": interaction caption stayed visible")
	if exercise_lock and room.breakfast_performance.active:
		var initial_stage: String = room.breakfast_performance.stage
		var initial_elapsed: float = room.breakfast_performance.elapsed
		await create_timer(0.25).timeout
		await check_locked_inputs()
		expect(room.breakfast_performance.elapsed > initial_elapsed, label + ": performance elapsed did not advance")
		expect(not room.breakfast_performance.stage.is_empty(), label + ": performance lost its stage during playback")
		# Record the stage boundary without coupling the check to a particular staging layout.
		var stage_changed: bool = room.breakfast_performance.stage != initial_stage
		if not stage_changed:
			var stage_deadline := Time.get_ticks_msec() + 2500
			while room.breakfast_performance.active and room.breakfast_performance.stage == initial_stage and Time.get_ticks_msec() < stage_deadline:
				await process_frame
			stage_changed = room.breakfast_performance.stage != initial_stage
		expect(stage_changed, label + ": performance did not advance to another stage")
	await wait_for_performance_end(label)
	expect(not room.montage_playing, label + ": montage state did not clear")
	expect(room.dialogue.panel.visible, label + ": dialogue panel did not return")
	expect(completion_count == completed_before + 1, label + ": completed signal count was not exactly one")

func run() -> void:
	change_scene_to_file(BREAKFAST_SCENE)
	await process_frame
	await process_frame
	room = current_scene
	expect(room != null, "Breakfast scene failed to load")
	if room == null:
		quit(1)
		return
	expect(room.player != null, "Breakfast player is missing")
	expect(room.breakfast_performance is Node, "Breakfast performance node is missing")
	if room.player == null or not room.breakfast_performance is Node:
		quit(1)
		return
	expect(room.breakfast_performance.has_signal("completed"), "Breakfast performance completed signal is missing")
	if room.breakfast_performance.has_signal("completed"):
		room.breakfast_performance.completed.connect(func() -> void: completion_count += 1)

	await run_performance("first play", true)
	await run_performance("replay", false)

	var start_position: Vector2 = room.player.position
	await send_key(KEY_W, true)
	await create_timer(0.35).timeout
	await send_key(KEY_W, false)
	await process_frame
	expect(room.player.position.distance_to(start_position) > 1.0, "W did not move the player after performance")
	await tap_key(KEY_R)
	expect(room.dialogue.active, "R did not start or replay dialogue after performance")
	expect(not room.montage_playing, "R left montage state active")
	print("PASS: breakfast performance input lock, replay, movement and dialogue recovery" if failures.is_empty() else "FAIL:\n" + "\n".join(failures))
	quit(0 if failures.is_empty() else 1)
