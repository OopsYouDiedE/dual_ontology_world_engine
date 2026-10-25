extends SceneTree
## Passive rendered capture plus ordinary viewport input; never selects a world action.
const LIFECYCLE := preload("res://scenes/Host/HostLifecycle.gd")
const CLOSE_BUDGET_MS := 120000
var host: Node
var directory := ""
var mode := "blind"
var seconds := 90.0
var started := 0
var last_capture := 0
var serial := 0
var finishing := false
var last_continue := 0
var dialogue_seen := false
var highlights: Array = []
var last_flush := 0
var archive: Node
var held_keys: Dictionary = {}
var host_closed := false
var archive_errors: Array = []
var capture_interval_ms := 200
var movement_samples := false
var held_joy_buttons: Dictionary = {}
var held_joy_axes: Dictionary = {}
var pending_decisions: Array = []

func _initialize() -> void:
	if "--window-mode=window" not in OS.get_cmdline_user_args():
		root.unfocusable = true
		root.position = Vector2i(10000, 10000)
	Engine.time_scale = 1.0
	_run.call_deferred()

func _run() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): directory = argument.substr(6)
		if argument.begins_with("--mode="): mode = argument.substr(7)
		if argument.begins_with("--seconds="): seconds = float(argument.substr(10))
		if argument.begins_with("--dv-speed="): Engine.time_scale = float(argument.trim_prefix("--dv-speed="))
		if argument.begins_with("--capture-ms="): capture_interval_ms = maxi(100, int(argument.trim_prefix("--capture-ms=")))
		if argument == "--movement-samples": movement_samples = true
	if directory == "" or DirAccess.dir_exists_absolute(directory):
		push_error("Require a new --out directory")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(directory)
	host = load("res://scenes/Host/Main.tscn").instantiate()
	root.add_child(host)
	host.engine.SetPlayerControlled(mode != "detective")
	archive = load("res://tools/FrameArchive.cs").new()
	root.add_child(archive)
	archive.Configure(host.engine.TimingClockUs())
	_write("run.dvlog", {"mode": mode, "time_scale": Engine.time_scale, "capture_interval_ms": capture_interval_ms, "movement_samples": movement_samples, "duration_real_seconds": seconds, "input": "Input.parse_input_event; no desktop devices"})
	started = Time.get_ticks_msec()
	host.engine.EventsReady.connect(_host_events)
	RenderingServer.frame_post_draw.connect(_capture)

func _process(_delta: float) -> bool:
	if not is_instance_valid(host) or finishing: return false
	_release_due_inputs()
	_poll_archive()
	if Time.get_ticks_msec() - last_flush >= 200:
		last_flush = Time.get_ticks_msec()
		_flush_timings()
	if seconds > 0 and Time.get_ticks_msec() - started >= seconds * 1000.0:
		_finish.call_deferred()
		return false
	var input_path := directory.path_join("input.json")
	if FileAccess.file_exists(input_path):
		var packet = JSON.parse_string(FileAccess.get_file_as_string(input_path))
		archive.WriteRecord(directory.path_join("input-%06d.dvlog" % serial), JSON.stringify(packet))
		DirAccess.remove_absolute(input_path) # Transient command packet; consumed evidence is binary.
		serial += 1
		if packet is Dictionary:
			_input_packet(packet)
	if mode == "detective" and is_instance_valid(host.ui) and is_instance_valid(host.ui.dialogue_ui) and host.ui.dialogue_ui.is_dialogue_active():
		if not dialogue_seen:
			dialogue_seen = true
			last_continue = Time.get_ticks_msec()
		# Retain dialogue on screen across capture frames before advancing it.
		if Time.get_ticks_msec() - last_continue >= 1500:
			last_continue = Time.get_ticks_msec()
			_key(KEY_SPACE, true)
			_key(KEY_SPACE, false)
	else:
		dialogue_seen = false
	return false

func _physics_process(delta: float) -> bool:
	if not movement_samples or finishing or not is_instance_valid(archive) or not is_instance_valid(host): return false
	if not is_instance_valid(host.field) or not is_instance_valid(host.field.player) or not is_instance_valid(host.field.spatial): return false
	var field: Node2D = host.field
	var direction := Input.get_vector("move_left", "move_right", "move_up", "move_down")
	var request: Dictionary = field.continuous_request()
	if direction == Vector2.ZERO and request.is_empty() and not field.spatial._walking: return false
	_append("movement-samples.dvlog", {"real_us": Time.get_ticks_usec(), "physics_delta": delta,
		"position": [field.player.position.x, field.player.position.y], "actor_instance_id": field.player.get_instance_id(),
		"direction": [direction.x, direction.y], "enabled": field.spatial.enabled(), "ui_busy": host._field_input_busy(),
		"map": field.adapter.map_key(), "entered_room": field.adapter.entered_room(field.player.position),
		"departure_room": field.adapter.departure_room(field.player.position), "authoritative_room": field.snapshot.get("execution_room", ""),
		"game_time": field.snapshot.get("game_time", 0), "request_id": request.get("request_id", ""), "phase": request.get("phase", "")})
	return false

func _input_packet(packet: Dictionary) -> void:
	_append("input-events.dvlog", {"real_ms": Time.get_ticks_msec() - started, "packet": packet})
	if str(packet.get("kind", "")) == "stop":
		_finish.call_deferred()
	elif str(packet.get("kind", "")) == "key":
		var code := OS.find_keycode_from_string(str(packet.get("key", "")))
		var down := bool(packet.get("pressed", true))
		_key(code, down)
		if down and packet.has("duration_ms"):
			held_keys[code] = Time.get_ticks_msec() + ceili(clampf(float(packet.duration_ms), 1.0, 5000.0))
		elif not packet.has("pressed"): _key(code, false)
	elif str(packet.get("kind", "")) == "click":
		var point := Vector2(float(packet.x), float(packet.y))
		var motion := InputEventMouseMotion.new()
		motion.position = point
		motion.global_position = point
		Input.parse_input_event(motion)
		for down in [true, false]:
			var event := InputEventMouseButton.new()
			event.position = point
			event.global_position = point
			event.button_index = MOUSE_BUTTON_RIGHT if str(packet.get("button", "")) == "right" else MOUSE_BUTTON_LEFT if str(packet.get("button", "")) == "left" else int(packet.get("button", MOUSE_BUTTON_LEFT))
			event.pressed = down
			Input.parse_input_event(event)
	elif str(packet.get("kind", "")) == "joy_button":
		var button := int(packet.get("button", 0))
		var down := bool(packet.get("pressed", true))
		_joy_button(button, down)
		if down and packet.has("duration_ms"):
			held_joy_buttons[button] = Time.get_ticks_msec() + ceili(clampf(float(packet.duration_ms), 1.0, 5000.0))
		elif not packet.has("pressed"): _joy_button(button, false)
	elif str(packet.get("kind", "")) == "joy_axis":
		var axis := int(packet.get("axis", 0))
		var value := clampf(float(packet.get("value", 0.0)), -1.0, 1.0)
		_joy_axis(axis, value)
		if not is_zero_approx(value) and packet.has("duration_ms"):
			held_joy_axes[axis] = Time.get_ticks_msec() + ceili(clampf(float(packet.duration_ms), 1.0, 5000.0))

func _joy_button(button: int, down: bool) -> void:
	if down: held_joy_buttons[button] = 0
	else: held_joy_buttons.erase(button)
	var event := InputEventJoypadButton.new()
	event.device = 0
	event.button_index = button
	event.pressed = down
	Input.parse_input_event(event)

func _joy_axis(axis: int, value: float) -> void:
	if is_zero_approx(value): held_joy_axes.erase(axis)
	else: held_joy_axes[axis] = 0
	var event := InputEventJoypadMotion.new()
	event.device = 0
	event.axis = axis
	event.axis_value = value
	Input.parse_input_event(event)

func _release_due_inputs() -> void:
	var now := Time.get_ticks_msec()
	for code in held_keys.keys():
		if int(held_keys[code]) == 0 or now < int(held_keys[code]): continue
		_key(code, false)
		_append("input-events.dvlog", {"real_ms": now - started, "packet": {"kind": "key-release", "key": OS.get_keycode_string(code), "reason": "duration"}})
	for button in held_joy_buttons.keys():
		if int(held_joy_buttons[button]) == 0 or now < int(held_joy_buttons[button]): continue
		_joy_button(button, false)
		_append("input-events.dvlog", {"real_ms": now - started, "packet": {"kind": "joy_button", "button": button, "pressed": false, "reason": "duration"}})
	for axis in held_joy_axes.keys():
		if int(held_joy_axes[axis]) == 0 or now < int(held_joy_axes[axis]): continue
		_joy_axis(axis, 0.0)
		_append("input-events.dvlog", {"real_ms": now - started, "packet": {"kind": "joy_axis", "axis": axis, "value": 0.0, "reason": "duration"}})

func _poll_archive() -> void:
	for raw in archive.PollCompleted():
		var row: Dictionary = JSON.parse_string(str(raw))
		if str(row.get("kind", "")) == "text-error":
			archive_errors.append(row)
			push_error("Text archive failed: " + str(row.error))
			continue
		_append("archive-timing.dvlog", row)
		if row.error != null:
			archive_errors.append(row)
			push_error("Frame archive failed: " + str(row.error))
		else: _append("frames.dvlog", {"real_ms": int(str(row.file).trim_prefix("frame-").trim_suffix(".png")), "file": row.file, "saved_us": row.end_us})

func _key(code: int, down: bool) -> void:
	if down: held_keys[code] = 0
	else: held_keys.erase(code)
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = down
	Input.parse_input_event(event)

func _host_events(events: Array) -> void:
	# Host delivery times are evidence bounds, not inferred DV milestone times.
	_append("host-events.dvlog", {"real_ms": Time.get_ticks_msec() - started, "events": events})
	for event in events:
		if str(event.get("kind", "")) == "dv_action":
			pending_decisions.append({"real_ms": Time.get_ticks_msec() - started, "intent": event.get("intent", {})})

func _append(name: String, value: Variant) -> void:
	archive.AppendRecords(directory.path_join(name), JSON.stringify(value) + "\n")

func _flush_timings(record_cost := true) -> void:
	var rows: Array = host.engine.PollTimings()
	if rows.is_empty(): return
	if record_cost: host._timing("journal_begin")
	var lines := PackedStringArray()
	for raw in rows:
		lines.append(str(raw))
		var row: Dictionary = JSON.parse_string(str(raw))
		if str(row.stage) in ["physical_location", "actor_in_viewport", "frame_gap"]:
			highlights.append(row)
	archive.AppendRecords(directory.path_join("live-timing.dvlog"), "\n".join(lines) + "\n")
	if record_cost: host._timing("journal_end")

func _capture() -> void:
	if finishing or directory == "": return
	var now := Time.get_ticks_msec()
	if pending_decisions.is_empty() and now - last_capture < capture_interval_ms: return
	last_capture = now
	if not archive.HasCapacity():
		host._timing("capture_backpressure", {"real_ms": now - started})
		return
	var elapsed := now - started
	var filename := "frame-%09d.png" % elapsed
	var captured_us: int = host.engine.TimingClockUs()
	host._timing("capture_begin", {"file": filename})
	var image := root.get_texture().get_image()
	host._timing("capture_readback_end", {"file": filename})
	if image.get_format() != Image.FORMAT_RGBA8: image.convert(Image.FORMAT_RGBA8)
	archive.Enqueue(image.get_data(), image.get_width(), image.get_height(), directory.path_join(filename), captured_us)
	for decision in pending_decisions:
		_append("decision-frames.dvlog", {"decision": decision, "file": filename, "capture_real_ms": elapsed})
	pending_decisions.clear()
	host._timing("capture_png_end", {"file": filename})
	if not highlights.is_empty():
		_append("keyframes.dvlog", {"real_ms": elapsed, "file": filename, "events": highlights})
		highlights = []
	# This stream is for the main reviewer, never the blind player.
	var record := {"real_ms": elapsed, "time_scale": Engine.time_scale, "snapshot": host.engine.Snapshot()}
	_append("measurement.dvlog", record)
	_capture_render_state(elapsed, filename)
	host._timing("capture_end", {"file": filename})

func _capture_render_state(elapsed: int, filename: String) -> void:
	if not is_instance_valid(host) or not is_instance_valid(host.field): return
	var rows: Array = []
	for key in host.field.actors:
		var actor = host.field.actors[key]
		if not is_instance_valid(actor) or not actor is Node2D: continue
		var screen: Vector2 = actor.get_global_transform_with_canvas().origin
		var row := {"actor": str(key), "visible": actor.visible, "visible_in_tree": actor.is_visible_in_tree(),
			"position": [actor.global_position.x, actor.global_position.y], "screen": [screen.x, screen.y],
			"alpha": actor.modulate.a, "self_alpha": actor.self_modulate.a, "sprites": []}
		for child in actor.find_children("*", "AnimatedSprite2D", true, false):
			if not is_instance_valid(child): continue
			var sprite: AnimatedSprite2D = child
			var frames := sprite.sprite_frames
			var texture: Texture2D = null
			if frames != null and frames.has_animation(sprite.animation):
				if sprite.frame >= 0 and sprite.frame < frames.get_frame_count(sprite.animation):
					texture = frames.get_frame_texture(sprite.animation, sprite.frame)
			row.sprites.append({"node": str(sprite.name), "visible": sprite.visible, "visible_in_tree": sprite.is_visible_in_tree(),
				"animation": str(sprite.animation), "frame": sprite.frame, "texture": texture.resource_path if texture != null else "",
				"alpha": sprite.modulate.a, "self_alpha": sprite.self_modulate.a})
		rows.append(row)
	_append("render-state.dvlog", {"real_ms": elapsed, "file": filename, "actors": rows, "scope": "private-passive-render-diagnostic"})

func _write(name: String, value: Variant) -> void:
	archive.WriteRecord(directory.path_join(name), JSON.stringify(value))

func _finish() -> void:
	if finishing: return
	finishing = true
	RenderingServer.frame_post_draw.disconnect(_capture)
	var ended := Time.get_ticks_msec() - started
	_write("end.dvlog", {"real_ms": ended, "snapshot": host.engine.Snapshot()})
	for code in held_keys.keys():
		_key(code, false)
		_append("input-events.dvlog", {"real_ms": Time.get_ticks_msec() - started, "packet": {"kind": "key-release", "key": OS.get_keycode_string(code), "reason": "capture-end"}})
	for button in held_joy_buttons.keys():
		_joy_button(button, false)
		_append("input-events.dvlog", {"real_ms": Time.get_ticks_msec() - started, "packet": {"kind": "joy_button", "button": button, "pressed": false, "reason": "capture-end"}})
	for axis in held_joy_axes.keys():
		_joy_axis(axis, 0.0)
		_append("input-events.dvlog", {"real_ms": Time.get_ticks_msec() - started, "packet": {"kind": "joy_axis", "axis": axis, "value": 0.0, "reason": "capture-end"}})
	host_closed = host.flow.shutdown_finished
	if not host_closed: host.shutdown_completed.connect(func(): host_closed = true, CONNECT_ONE_SHOT)
	host.begin_shutdown()
	var deadline := Time.get_ticks_msec() + CLOSE_BUDGET_MS
	while Time.get_ticks_msec() < deadline:
		_flush_timings(false)
		_poll_archive()
		if archive.IsIdle() and host_closed: break
		await process_frame
	var timed_out: bool = not archive.IsIdle() or not host_closed
	var complete: bool = not timed_out and archive_errors.is_empty()
	_write("capture-close.dvlog", {"complete": complete, "host_closed": host_closed,
		"archive_idle": archive.IsIdle(), "timeout": timed_out, "capture_end_real_ms": ended,
		"archive_and_host_closed_real_ms": Time.get_ticks_msec() - started, "archive_errors": archive_errors,
		"held_inputs_released": held_keys.is_empty() and held_joy_buttons.is_empty() and held_joy_axes.is_empty()})
	if timed_out:
		push_error("Capture shutdown exceeded its deadline; evidence is incomplete.")
		quit(2)
	else:
		await LIFECYCLE.quit(self, 0 if complete else 2)
