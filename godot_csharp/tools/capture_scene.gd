extends SceneTree
## Render observation only: ordinary input, wall-clock waits, no world mutation.
## Run windowed: --script res://tools/capture_scene.gd -- --capture-scene=...
## --capture-output=<absolute-directory> --capture-steps=<absolute-json-file>
## Static maps only: --capture-camera-at=x,y --capture-camera-zoom=1.6
const LIFECYCLE = preload("res://scenes/Host/HostLifecycle.gd")

var output := ""
var scene_path := ""
var steps_path := ""
var captures: Array = []
var started_utc := ""
var camera_at: Variant = null
var camera_zoom := 1.0

func _initialize() -> void:
	started_utc = Time.get_datetime_string_from_system(true) + "Z"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--capture-scene="):
			scene_path = argument.trim_prefix("--capture-scene=")
		elif argument.begins_with("--capture-output="):
			output = argument.trim_prefix("--capture-output=")
		elif argument.begins_with("--capture-steps="):
			steps_path = argument.trim_prefix("--capture-steps=")
		elif argument.begins_with("--capture-camera-at="):
			var coordinates := argument.trim_prefix("--capture-camera-at=").split(",")
			if coordinates.size() == 2 and coordinates[0].is_valid_float() and coordinates[1].is_valid_float():
				camera_at = Vector2(float(coordinates[0]), float(coordinates[1]))
		elif argument.begins_with("--capture-camera-zoom="):
			camera_zoom = argument.trim_prefix("--capture-camera-zoom=").to_float()
	_run.call_deferred()

func _run() -> void:
	if DisplayServer.get_name() == "headless":
		_fail("Capture requires a windowed rendering driver.")
		return
	if scene_path.is_empty() or not output.is_absolute_path():
		_fail("Provide --capture-scene and an absolute --capture-output directory.")
		return
	var steps: Array = [{"label": "scene", "wait": 0.5}]
	if not steps_path.is_empty():
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(steps_path))
		if not parsed is Array:
			_fail("Capture steps must be a JSON array.")
			return
		steps = parsed
	var packed := load(scene_path) as PackedScene
	if packed == null:
		_fail("Cannot load scene: " + scene_path)
		return
	var directory_error := DirAccess.make_dir_recursive_absolute(output)
	if directory_error != OK:
		_fail("Cannot create capture directory: " + str(directory_error))
		return
	root.size = Vector2i(1280, 992)
	root.content_scale_size = Vector2i(1280, 992)
	var scene := packed.instantiate()
	root.add_child(scene)
	current_scene = scene
	if camera_at != null:
		if not scene is Node2D or not is_finite(camera_zoom) or camera_zoom <= 0:
			_fail("Capture camera requires a static Node2D map and positive zoom.")
			return
		var camera := Camera2D.new()
		camera.position = camera_at
		camera.zoom = Vector2.ONE * camera_zoom
		root.add_child(camera)
		camera.make_current()
	await process_frame
	for step in steps:
		if not step is Dictionary:
			_fail("Every capture step must be an object.")
			return
		if step.has("key"):
			var key := OS.find_keycode_from_string(str(step.key))
			if key == KEY_NONE:
				_fail("Unknown key: " + str(step.key))
				return
			_key(key, true)
			await create_timer(maxf(0.04, float(step.get("hold", 0.04)))).timeout
			_key(key, false)
		if step.has("click"):
			var at: Array = step.click
			if at.size() != 2:
				_fail("click requires [x,y].")
				return
			var motion := InputEventMouseMotion.new()
			motion.position = Vector2(float(at[0]), float(at[1]))
			Input.parse_input_event(motion)
			for pressed in [true, false]:
				var click := InputEventMouseButton.new()
				click.position = motion.position
				click.button_index = MOUSE_BUTTON_LEFT
				click.pressed = pressed
				Input.parse_input_event(click)
				await process_frame
		await create_timer(maxf(0.05, float(step.get("wait", 0.25)))).timeout
		if step.has("label"):
			var label := str(step.label)
			if label.is_empty() or label.validate_filename() != label or label in [".", ".."]:
				_fail("Invalid capture label.")
				return
			await process_frame
			await RenderingServer.frame_post_draw
			var pixels := root.get_texture().get_image()
			var path := output.path_join(label + ".png")
			var saved := pixels.save_png(path)
			if saved != OK:
				_fail("save_png failed: " + path + " / " + str(saved))
				return
			var record := {"path": path, "savedUtc": Time.get_datetime_string_from_system(true) + "Z", "width": pixels.get_width(), "height": pixels.get_height(), "savePngCode": saved, "sha256": FileAccess.get_sha256(path), "input": step}
			captures.append(record)
			print(JSON.stringify(record))
	var manifest := FileAccess.open(output.path_join("capture-provenance.json"), FileAccess.WRITE)
	if manifest == null:
		_fail("Cannot write capture provenance: " + str(FileAccess.get_open_error()))
		return
	manifest.store_string(JSON.stringify({"scene": scene_path, "processStartUtc": started_utc, "engine": Engine.get_version_info(), "displayServer": DisplayServer.get_name(), "method": "ordinary-input-wall-clock-render", "camera": {"position": [camera_at.x, camera_at.y], "zoom": camera_zoom, "scope": "static-map-render-observation"} if camera_at != null else {}, "images": captures}, "  ") + "\n")
	manifest.close()
	await LIFECYCLE.quit(self)

func _key(code: Key, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)

func _fail(message: String) -> void:
	push_error(message)
	quit(1)
