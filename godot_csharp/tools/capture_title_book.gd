extends SceneTree
## UI-only production title capture. No Main, no DV opening, no world actions.
var target: SubViewport
var title: Control
var archive: Node
var output := ""
var origin := 0
var emitted: Array = []
var cast_keys := PackedStringArray()
var map_path := ""
var archive_failed := false

func _initialize() -> void:
	root.hide()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): output = argument.trim_prefix("--out=")
		elif argument.begins_with("--cast="): cast_keys = argument.trim_prefix("--cast=").split(",", false)
		elif argument.begins_with("--map="): map_path = argument.trim_prefix("--map=")
	if output == "" or DirAccess.dir_exists_absolute(output):
		push_error("Use a new --out directory")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(output)
	_run.call_deferred()

func _record(value: Dictionary) -> void:
	value.real_us = Time.get_ticks_usec() - origin
	archive.AppendRecords(output.path_join("ui-input.dvlog"), JSON.stringify(value) + "\n")

func _shot(name: String) -> void:
	while not archive.HasCapacity():
		await process_frame
		_poll_archive()
	await RenderingServer.frame_post_draw
	var captured_us := Time.get_ticks_usec() - origin
	var pixels := target.get_texture().get_image()
	pixels.convert(Image.FORMAT_RGBA8)
	archive.Enqueue(pixels.get_data(), pixels.get_width(), pixels.get_height(), output.path_join(name + ".png"), captured_us)
	_record({"kind": "frame", "file": name + ".png", "captured_us": captured_us, "write_status": "queued", "page": title._current_page, "wind": title._wind, "light_energy": title._lamp_light.energy, "light_position": [title._lamp_light.position.x, title._lamp_light.position.y], "tilt": [title._projection.tilt.x, title._projection.tilt.y]})
	_poll_archive()
	print("CAPTURED ", name, " PAGE ", title._current_page)

func _poll_archive() -> void:
	for encoded in archive.PollCompleted():
		var result: Dictionary = JSON.parse_string(str(encoded))
		if result.get("error") != null and str(result.get("error")) != "" or result.get("kind", "") == "text-error":
			archive_failed = true
			push_error("Capture archive: " + str(result))
		if result.get("kind", "") != "text-error": _record({"kind": "archive_result", "result": result})

func _move(position: Vector2) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = position
	motion.global_position = position
	_record({"kind": "mouse_motion", "position": [position.x, position.y]})
	target.push_input(motion, true)

func _click(button: Control) -> void:
	var stage_point: Vector2 = title.cover.get_global_transform_with_canvas() * button.get_global_rect().get_center()
	var point: Vector2 = title._projection.project(stage_point)
	_move(point)
	for pressed in [true, false]:
		var click := InputEventMouseButton.new()
		click.position = point
		click.global_position = point
		click.button_index = MOUSE_BUTTON_LEFT
		click.pressed = pressed
		_record({"kind": "mouse_button", "position": [point.x, point.y], "pressed": pressed, "control": button.name})
		target.push_input(click, true)
		await process_frame

func _run() -> void:
	origin = Time.get_ticks_usec()
	archive = load("res://scenes/Host/FrameArchive.cs").new()
	root.add_child(archive)
	archive.Configure(0)
	target = SubViewport.new()
	target.size = Vector2i(1280, 992)
	target.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(target)
	title = load("res://scenes/UI/TitleUI.tscn").instantiate()
	target.add_child(title)
	title.ui_event.connect(func(event: Dictionary):
		emitted.append(event)
		_record({"kind": "ui_event", "event": event})
	)
	await create_timer(0.5).timeout
	_move(Vector2(640, 496))
	await create_timer(0.5).timeout
	await _shot("cover-center")
	_move(Vector2(64, 96))
	await create_timer(0.7).timeout
	await _shot("cover-left")
	_move(Vector2(1216, 896))
	await create_timer(0.7).timeout
	await _shot("cover-right")
	# Keep the camera still while recording actual wind/light evolution.
	for index in range(12):
		await create_timer(0.1).timeout
		await _shot("wind-%02d" % index)
	await _click(title.settings_button)
	await create_timer(0.23).timeout
	await _shot("settings-turn")
	await create_timer(0.6).timeout
	await _shot("settings-page")
	if title._current_page != "settings":
		push_error("The projected settings click did not open its page")
		await _finish(4)
		return
	await _click(title._leaf_back)
	await create_timer(0.8).timeout
	await _click(title.continue_button)
	await create_timer(0.23).timeout
	await _shot("load-turn")
	await create_timer(0.6).timeout
	await _shot("load-page")
	await _click(title._leaf_back)
	await create_timer(0.8).timeout
	await _click(title.start_button)
	await create_timer(0.23).timeout
	await _shot("start-turn")
	await create_timer(0.6).timeout
	await _shot("start-page")
	if not cast_keys.is_empty() and map_path != "": await _cast_sample()
	await _finish(0)

func _cast_sample() -> void:
	# Explicit presentation fixture: no DV state, inputs, decisions or movement.
	var room := SubViewport.new()
	room.size = Vector2i(1280, 992)
	room.world_2d = World2D.new()
	room.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(room)
	var map: Node2D = load(map_path).instantiate()
	room.add_child(map)
	var architecture := map.get_node("Architecture") as Sprite2D
	var relief := architecture.texture.resource_path.get_basename() + ".relief.tres"
	if ResourceLoader.exists(relief): architecture.texture = load(relief)
	var camera := Camera2D.new()
	camera.position = Vector2(330, 340)
	camera.zoom = Vector2(2.5, 2.5)
	map.add_child(camera)
	camera.make_current()
	var ambient := CanvasModulate.new()
	ambient.color = Color("d8d4cc")
	map.add_child(ambient)
	for index in cast_keys.size():
		var actor := CharacterBody2D.new()
		actor.set_script(load("res://scenes/Story/StoryActor.gd"))
		actor.standard = load("res://ShareAsset/Internal/Characters/PixelCast/%s.tres" % cast_keys[index])
		actor.position = Vector2(240 + index * 60, 384)
		map.get_node("Objects").add_child(actor)
	for frame in range(8): await process_frame
	await RenderingServer.frame_post_draw
	var result := room.get_texture().get_image().save_png(output.path_join("cast-room.png"))
	_record({"kind": "presentation_fixture", "file": "cast-room.png", "cast": cast_keys, "resource": map_path, "save_error": result})
	room.queue_free()

func _finish(code: int) -> void:
	while true:
		while not archive.IsIdle():
			await process_frame
			_poll_archive()
		_poll_archive()
		if archive.IsIdle(): break
	var result := 6 if archive_failed else code
	_record({"kind": "close", "code": result, "emitted": emitted})
	while not archive.IsIdle(): await process_frame
	quit(result)
