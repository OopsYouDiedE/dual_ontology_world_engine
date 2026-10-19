extends SceneTree
## Passive rendered capture plus ordinary viewport input; never selects a world action.
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

func _initialize() -> void:
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
	if directory == "" or DirAccess.dir_exists_absolute(directory):
		push_error("Require a new --out directory")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(directory)
	_write("run.json", {"mode": mode, "time_scale": Engine.time_scale, "capture_hz": 5, "duration_real_seconds": seconds, "input": "Input.parse_input_event; no desktop devices"})
	host = load("res://scenes/Host/Main.tscn").instantiate()
	root.add_child(host)
	host.engine.SetPlayerControlled(mode != "detective")
	archive = load("res://scenes/Host/FrameArchive.cs").new()
	root.add_child(archive)
	archive.Configure(host.engine.TimingClockUs())
	started = Time.get_ticks_msec()
	host.engine.EventsReady.connect(_host_events)
	RenderingServer.frame_post_draw.connect(_capture)
	if mode == "detective":
		await process_frame
		_key(KEY_ENTER, true)
		_key(KEY_ENTER, false)

func _process(_delta: float) -> bool:
	if not is_instance_valid(host) or finishing: return false
	_release_due_keys()
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
		DirAccess.rename_absolute(input_path, directory.path_join("input-%06d.json" % serial))
		serial += 1
		if packet is Dictionary:
			_input_packet(packet)
	if mode == "detective" and is_instance_valid(host.ui) and host.ui.dialogue.active:
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

func _input_packet(packet: Dictionary) -> void:
	_append("input-events.jsonl", {"real_ms": Time.get_ticks_msec() - started, "packet": packet})
	if str(packet.get("kind", "")) == "stop":
		_finish.call_deferred()
	elif str(packet.get("kind", "")) == "key":
		var code := OS.find_keycode_from_string(str(packet.get("key", "")))
		_key(code, bool(packet.get("pressed", true)))
		if packet.has("duration_ms"):
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
			event.button_index = MOUSE_BUTTON_LEFT
			event.pressed = down
			Input.parse_input_event(event)

func _release_due_keys() -> void:
	for code in held_keys.keys():
		if Time.get_ticks_msec() < int(held_keys[code]): continue
		_key(code, false)
		held_keys.erase(code)
		_append("input-events.jsonl", {"real_ms": Time.get_ticks_msec() - started, "packet": {"kind": "key-release", "key": OS.get_keycode_string(code)}})

func _poll_archive() -> void:
	for raw in archive.PollCompleted():
		var row: Dictionary = JSON.parse_string(str(raw))
		_append("archive-timing.jsonl", row)
		if row.error != null:
			archive_errors.append(row)
			push_error("Frame archive failed: " + str(row.error))
		else: _append("frames.jsonl", {"real_ms": int(str(row.file).trim_prefix("frame-").trim_suffix(".png")), "file": row.file, "saved_us": row.end_us})

func _key(code: int, down: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = down
	Input.parse_input_event(event)

func _host_events(events: Array) -> void:
	# Host delivery times are evidence bounds, not inferred DV milestone times.
	_append("host-events.jsonl", {"real_ms": Time.get_ticks_msec() - started, "events": events})

func _append(name: String, value: Variant) -> void:
	var path := directory.path_join(name)
	var file := FileAccess.open(path, FileAccess.READ_WRITE if FileAccess.file_exists(path) else FileAccess.WRITE)
	file.seek_end()
	file.store_line(JSON.stringify(value))
	file.close()

func _flush_timings() -> void:
	var rows: Array = host.engine.PollTimings()
	if rows.is_empty(): return
	host._timing("journal_begin")
	var path := directory.path_join("live-timing.jsonl")
	var file := FileAccess.open(path, FileAccess.READ_WRITE if FileAccess.file_exists(path) else FileAccess.WRITE)
	file.seek_end()
	for raw in rows:
		file.store_line(str(raw))
		var row: Dictionary = JSON.parse_string(str(raw))
		if str(row.stage) in ["physical_location", "actor_in_viewport", "frame_gap"]:
			highlights.append(row)
	file.close()
	host._timing("journal_end")

func _capture() -> void:
	if finishing or directory == "": return
	var now := Time.get_ticks_msec()
	if now - last_capture < 200: return
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
	host._timing("capture_png_end", {"file": filename})
	if not highlights.is_empty():
		_append("keyframes.jsonl", {"real_ms": elapsed, "file": filename, "events": highlights})
		highlights = []
	# This stream is for the main reviewer, never the blind player.
	var record := {"real_ms": elapsed, "time_scale": Engine.time_scale, "snapshot": host.engine.Snapshot()}
	var path := directory.path_join("measurement.jsonl")
	var file := FileAccess.open(path, FileAccess.READ_WRITE if FileAccess.file_exists(path) else FileAccess.WRITE)
	file.seek_end()
	file.store_line(JSON.stringify(record))
	file.close()
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
	var path := directory.path_join("render-state.jsonl")
	var file := FileAccess.open(path, FileAccess.READ_WRITE if FileAccess.file_exists(path) else FileAccess.WRITE)
	if file == null: return
	file.seek_end()
	file.store_line(JSON.stringify({"real_ms": elapsed, "file": filename, "actors": rows, "scope": "private-passive-render-diagnostic"}))
	file.close()

func _write(name: String, value: Variant) -> void:
	var file := FileAccess.open(directory.path_join(name), FileAccess.WRITE)
	file.store_string(JSON.stringify(value))
	file.close()

func _finish() -> void:
	if finishing: return
	finishing = true
	var ended := Time.get_ticks_msec() - started
	_write("end.json", {"real_ms": ended, "snapshot": host.engine.Snapshot()})
	for code in held_keys:
		_key(code, false)
		_append("input-events.jsonl", {"real_ms": Time.get_ticks_msec() - started, "packet": {"kind": "key-release", "key": OS.get_keycode_string(code), "reason": "capture-end"}})
	held_keys.clear()
	_flush_timings()
	host.shutdown_completed.connect(func(): host_closed = true)
	host.begin_shutdown()
	while not archive.IsIdle() or not host_closed:
		await process_frame
	_poll_archive()
	_write("capture-close.json", {"capture_end_real_ms": ended, "archive_and_host_closed_real_ms": Time.get_ticks_msec() - started, "archive_errors": archive_errors})
	quit(0 if archive_errors.is_empty() else 2)
