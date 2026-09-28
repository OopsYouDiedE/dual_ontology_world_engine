extends SceneTree
## Isolated transport regression using the actual scene replay methods and Core results.
var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func require(ok: bool, message: String) -> void:
	if not ok:
		failures.append(message)
		push_error("MIND TRANSPORT: " + message)

func run() -> void:
	root.size = Vector2i(1000,160)
	root.content_scale_size = Vector2i(1000,160)
	var scene: Node2D = load("res://scenes/FirstAwakening/FirstAwakening.gd").new()
	# Do not enter the full room: the real _replay_step/_start_replay methods need only their replay data.
	var bridge = load("res://scenes/FirstAwakening/HallReplayBridge.cs").new()
	root.add_child(bridge)
	var codes: Array[String] = ["BraceDoorWithWedge", "WAIT_SMOKE", "RemoveWedge", "SlideInnerBolt", "WrapWedge", "MoveStop", "WipeFloor", "OuterBar"]
	var program: Dictionary = bridge.Compile(codes)
	scene.replay_events = program.events
	scene.replay_states = program.states
	scene.sim_result = program.states.back()
	scene.mind_text = "侦探检查门底灰迹。"
	scene._build_replay_pause_points()
	var transport = load("res://scenes/FirstAwakening/MindTransport.gd").new()
	for pair in [["Reset","reset"],["Previous","previous"],["Play","play"],["Next","next"],["Fast","fast"]]:
		var button = load("res://scenes/FirstAwakening/PaperIconButton.gd").new()
		button.name = pair[0]
		button.glyph = pair[1]
		transport.add_child(button)
	var slider := HSlider.new()
	slider.name = "Seek"
	slider.custom_minimum_size = Vector2(300,32)
	slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	transport.add_child(slider)
	var clock_label := Label.new()
	clock_label.name = "Clock"
	clock_label.custom_minimum_size.x = 144
	transport.add_child(clock_label)
	transport.setup(scene)
	root.add_child(transport)
	transport.position = Vector2(20,20)
	transport.size = Vector2(840,44)
	for frame in 3: await process_frame
	require(slider.max_value == scene.replay_events.size(), "slider spans all real replay states")
	var total: float = scene.replay_events.size() * 0.65
	require(clock_label.text == transport.format_time(0.0) + " / " + transport.format_time(total), "clock uses 24-hour time format with 0.65 seconds per event")
	transport.get_node("Play").pressed.emit()
	require(scene.replay_playing and transport.get_node("Play").glyph == "pause", "play uses the real scene start method and updates its glyph")
	# Actual pointer drag on the HSlider must pause and land on a discrete existing state.
	var start: Vector2 = slider.global_position + Vector2(slider.size.x * 0.15, slider.size.y / 2.0)
	var finish: Vector2 = slider.global_position + Vector2(slider.size.x * 0.78, slider.size.y / 2.0)
	var mouse := InputEventMouseButton.new()
	mouse.button_index = MOUSE_BUTTON_LEFT
	mouse.position = start
	mouse.pressed = true
	Input.parse_input_event(mouse)
	await process_frame
	var motion := InputEventMouseMotion.new()
	motion.position = finish
	motion.relative = finish - start
	motion.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(motion)
	await process_frame
	mouse = InputEventMouseButton.new()
	mouse.button_index = MOUSE_BUTTON_LEFT
	mouse.position = finish
	mouse.pressed = false
	Input.parse_input_event(mouse)
	await process_frame
	require(not scene.replay_playing and scene.replay_accum == 0 and scene.replay_index > 0, "pointer dragging pauses playback at a real event: " + str([scene.replay_playing,scene.replay_index,slider.value,slider.get_global_rect(),root.size]))
	require(scene.replay_index == roundi(slider.value), "drag location and replay state agree")
	require(is_equal_approx(transport.elapsed_seconds(), scene.replay_index * 0.65), "seek updates elapsed time")
	var index: int = scene.replay_index
	slider.grab_focus()
	var key := InputEventKey.new()
	key.keycode = KEY_LEFT
	key.physical_keycode = KEY_LEFT
	key.pressed = true
	Input.parse_input_event(key)
	await process_frame
	key.pressed = false
	Input.parse_input_event(key)
	await process_frame
	require(scene.replay_index == maxi(0,index - 1), "focused slider supports keyboard step navigation")
	scene.mind_dirty = true
	transport.seek_to(scene.replay_events.size())
	scene._request_trace_check("门底遮蔽","door-mask","trace")
	require(scene.mind_dirty and scene.pending_inspections.is_empty() and scene.sim_checks.is_empty(), "seeking old results cannot re-enable evidence verification")
	transport.get_node("Reset").pressed.emit()
	require(scene.replay_index == 0 and scene.mind_text == "侦探检查门底灰迹。" and scene.mind_dirty, "reset preserves the draft and dirty state")
	transport.get_node("Fast").button_pressed = true
	require(scene.replay_fast and not scene.replay_playing, "fast toggle never starts playback")
	transport.seek_to(scene.replay_events.size())
	transport.get_node("Play").pressed.emit()
	require(scene.replay_index == 0 and scene.replay_playing and scene.replay_fast, "play at the end restarts at zero with the selected speed")
	scene.replay_accum = 0.65 / 8.0
	require(is_equal_approx(transport.elapsed_seconds(), 0.65 / 2.0), "fast clock reports timeline time at precisely four times wall time")
	transport.get_node("Fast").button_pressed = false
	require(is_equal_approx(scene.replay_accum,0.65/2.0) and is_equal_approx(transport.elapsed_seconds(),0.65/2.0), "switching speed preserves fractional progress")
	transport.get_node("Play").pressed.emit()
	require(not scene.replay_playing and transport.get_node("Play").glyph == "play", "pause returns to the current settled state")
	transport.queue_free()
	bridge.queue_free()
	scene.scene_items.free()
	scene.free()
	await process_frame
	print("MIND TRANSPORT: ", "PASS" if failures.is_empty() else "FAIL", " (", failures.size(), " failures)")
	quit(0 if failures.is_empty() else 1)

