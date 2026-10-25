extends SceneTree
## Finite presentation observation. Never instantiates a Core session.
var stage: Control
var destination := ""
var observations: Array = []

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): destination = argument.trim_prefix("--out=")
	if destination.is_empty():
		push_error("--out is required")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(destination)
	_run.call_deferred()

func _record(kind: String, data: Dictionary = {}) -> void:
	data["kind"] = kind
	data["elapsed_us"] = Time.get_ticks_usec()
	observations.append(data)

func _wait(seconds: float) -> void:
	await create_timer(seconds).timeout

func _key(code: Key) -> void:
	for pressed in [true, false]:
		var event := InputEventKey.new()
		event.keycode = code
		event.pressed = pressed
		_record("input", {"keycode": code, "pressed": pressed})
		Input.parse_input_event(event)
		await process_frame

func _picture(name: String) -> void:
	await RenderingServer.frame_post_draw
	var image: Image = stage.viewport.get_texture().get_image()
	var error := image.save_png(destination.path_join(name + ".png"))
	_record("capture", {"name": name, "error": error, "phase": stage.action_phase})
	if error != OK: push_error("Unable to save " + name)

func _settle(limit := 5.0) -> bool:
	var end := Time.get_ticks_msec() + int(limit * 1000)
	while stage.cue_busy and Time.get_ticks_msec() < end: await _wait(0.05)
	return not stage.cue_busy and not stage.cue_failed

func _run() -> void:
	root.size = Vector2i(1280, 992)
	stage = load("res://scenes/Highlights/Highlights.tscn").instantiate()
	root.add_child(stage)
	stage._select_stage(1)
	await _settle()
	for candidate in range(3):
		stage.painterly.set_scheme(candidate)
		await _picture("scholar-scheme-%d" % candidate)
	stage.painterly.set_scheme(1)
	for line in range(1, 7):
		await _key(KEY_SPACE)
		await _wait(0.1)
		if line in [2, 3, 4, 5]: await _picture("scholar-" + str(stage.action_phase))
		if not await _settle():
			_record("failure", {"stage": "scholar", "line": line})
			break
	await _picture("scholar-final")
	stage._select_stage(4)
	await _settle()
	for candidate in range(3):
		stage.painterly.set_scheme(candidate)
		await _picture("dining-scheme-%d" % candidate)
	stage.painterly.set_scheme(1)
	await _key(KEY_R)
	var deadline := Time.get_ticks_msec() + 20000
	var mid_saved := false
	while stage.cue_busy and Time.get_ticks_msec() < deadline:
		var actor: CharacterBody2D = stage.actors["detective"]
		_record("body", {"actor": "detective", "position": actor.position, "seated": actor.seated})
		if actor.position.y < 175 and not mid_saved:
			await _picture("dining-walk")
			mid_saved = true
		await _wait(0.05)
	_record("outcome", {"seated": stage.actors["detective"].seated,
		"blocked": stage.cue_failed, "phase": stage.action_phase,
		"position": stage.actors["detective"].position})
	await _picture("dining-seated")
	# Native variant bytes preserve exact fields; SHA-256 covers the whole payload.
	var payload := var_to_bytes(observations)
	var file := FileAccess.open(destination.path_join("observations.dvlog.partial"), FileAccess.WRITE)
	file.store_buffer("DVTILE01".to_utf8_buffer())
	file.store_64(payload.size())
	var checksum := HashingContext.new()
	checksum.start(HashingContext.HASH_SHA256)
	checksum.update(payload)
	file.store_buffer(checksum.finish())
	file.store_buffer(payload)
	file.close()
	DirAccess.rename_absolute(destination.path_join("observations.dvlog.partial"), destination.path_join("observations.dvlog"))
	print("PRESENTATION_RESULT seated=", stage.actors["detective"].seated, " blocked=", stage.cue_failed)
	var result := 0 if stage.actors["detective"].seated and not stage.cue_failed else 1
	stage.queue_free()
	await process_frame
	quit(result)
