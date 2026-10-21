extends SceneTree
## UI-only capture of production TitleUI. Never loads Main or opens DV.
## Run with a rendering-capable Godot .NET build, without --headless.

const FRAME_INTERVAL_US := 40000
const SOFT_LIMIT_US := 23500000
const HARD_LIMIT_US := 25000000

var target: SubViewport
var title: Control
var archive: Node
var output := ""
var origin := 0
var emitted: Array[Dictionary] = []
var pressed_controls: Array[String] = []
var failures: Array[String] = []
var archive_failed := false
var finishing := false
var hard_stopped := false
var frame_count := 0
var pointer := Vector2(640, 496)

func _initialize() -> void:
	origin = Time.get_ticks_usec()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="):
			output = argument.trim_prefix("--out=")
	if output == "" or not output.is_absolute_path() or DirAccess.dir_exists_absolute(output):
		push_error("Use --out=<new absolute directory>; existing evidence is never overwritten")
		quit(2)
		return
	var error := DirAccess.make_dir_recursive_absolute(output)
	if error != OK:
		push_error("Cannot create capture directory: " + error_string(error))
		quit(2)
		return
	root.size = Vector2i(1280, 992)
	root.title = "安眠者有福 · 3D 书桌 UI 观察"
	_run.call_deferred()

func _process(_delta: float) -> bool:
	if hard_stopped:
		return false
	var elapsed := Time.get_ticks_usec() - origin
	if elapsed >= HARD_LIMIT_US:
		hard_stopped = true
		push_error("UI capture reached the 25-second hard deadline; retained existing evidence")
		quit(9)
	elif elapsed >= SOFT_LIMIT_US and not finishing:
		_fail("UI capture exceeded its time budget")
		_finish(9)
	return false

func _record(value: Dictionary) -> void:
	if archive == null or hard_stopped:
		return
	value.real_us = Time.get_ticks_usec() - origin
	# FrameArchive packages each record in the existing binary dvlog format.
	archive.AppendRecords(output.path_join("ui-input.dvlog"), JSON.stringify(value) + "\n")

func _fail(message: String) -> void:
	failures.append(message)
	push_error(message)
	_record({"kind": "failure", "message": message})

func _on_ui_event(event: Dictionary) -> void:
	emitted.append(event.duplicate(true))
	_record({"kind": "ui_event", "event": event})

func _on_pressed(control_name: String) -> void:
	pressed_controls.append(control_name)
	_record({"kind": "control_pressed", "control": control_name})

func _poll_archive() -> void:
	if archive == null:
		return
	for encoded in archive.PollCompleted():
		var result = JSON.parse_string(str(encoded))
		if not result is Dictionary:
			archive_failed = true
			push_error("Capture archive returned an invalid completion record")
			continue
		var has_error := result.get("error") != null and str(result.get("error")) != ""
		if has_error or result.get("kind", "") == "text-error":
			archive_failed = true
			push_error("Capture archive: " + str(result))
		if result.get("kind", "") != "text-error":
			_record({"kind": "archive_result", "result": result})

func _shot(name: String) -> bool:
	if finishing or hard_stopped:
		return false
	var capacity_deadline := Time.get_ticks_usec() + 1200000
	while not archive.HasCapacity():
		_poll_archive()
		if finishing or hard_stopped:
			return false
		if Time.get_ticks_usec() >= capacity_deadline:
			_fail("PNG archive backpressure exceeded 1.2 seconds at " + name)
			return false
		await process_frame
	await RenderingServer.frame_post_draw
	if finishing or hard_stopped:
		return false
	var captured_us := Time.get_ticks_usec() - origin
	var pixels := target.get_texture().get_image()
	if pixels == null or pixels.is_empty():
		_fail("No rendered image available at " + name)
		return false
	pixels.convert(Image.FORMAT_RGBA8)
	archive.Enqueue(pixels.get_data(), pixels.get_width(), pixels.get_height(), output.path_join(name + ".png"), captured_us)
	frame_count += 1
	var stage: Node3D = title._desk
	var light: OmniLight3D = stage.lamp_light
	var camera: Camera3D = stage.camera
	_record({
		"kind": "frame", "file": name + ".png", "captured_us": captured_us,
		"write_status": "queued", "page": title._current_page, "turning": title._turning,
		"opening": title._request_pending, "opening_kind": title._opening_kind,
		"menu_input_enabled": title._menu_input_enabled, "page_input_disabled": title.viewport.gui_disable_input,
		"open_amount": title._open_amount, "light_energy": light.light_energy,
		"light_position": [light.position.x, light.position.y, light.position.z],
		"dust_visible": stage.atmosphere.emitter.visible, "dust_speed_scale": stage.atmosphere.emitter.speed_scale,
		"wind": stage.wind, "light_intensity": stage.light_intensity,
		"pointer": [pointer.x, pointer.y],
		"stage_pointer": [stage.pointer.x, stage.pointer.y],
		"camera_position": [camera.position.x, camera.position.y, camera.position.z],
		"camera_rotation_degrees": [camera.rotation_degrees.x, camera.rotation_degrees.y, camera.rotation_degrees.z],
		"camera_fov_degrees": camera.fov
	})
	_poll_archive()
	print("CAPTURED ", name, " PAGE ", title._current_page, " TURNING ", title._turning)
	return not archive_failed

func _move(position_value: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = position_value
	event.global_position = position_value
	event.relative = position_value - pointer
	pointer = position_value
	_record({"kind": "mouse_motion", "position": [pointer.x, pointer.y], "relative": [event.relative.x, event.relative.y]})
	target.push_input(event, true)

func _click(button: Button) -> bool:
	if finishing or not is_instance_valid(button) or button.disabled or not button.is_visible_in_tree():
		_fail("Target button is not available for a native click")
		return false
	var point: Vector2 = title.project_button(button)
	_move(point)
	await create_timer(0.18).timeout
	if finishing or hard_stopped:
		return false
	point = title.project_button(button)
	_move(point)
	var previous_count := pressed_controls.size()
	# Both events use the recomputed point in one frame, before camera motion.
	for down in [true, false]:
		var event := InputEventMouseButton.new()
		event.position = point
		event.global_position = point
		event.button_index = MOUSE_BUTTON_LEFT
		event.button_mask = MOUSE_BUTTON_MASK_LEFT if down else 0
		event.pressed = down
		_record({"kind": "mouse_button", "position": [point.x, point.y], "pressed": down, "control": str(button.name)})
		target.push_input(event, true)
	if pressed_controls.size() != previous_count + 1 or pressed_controls.back() != str(button.name):
		_fail("Projected click did not emit exactly one pressed signal for " + str(button.name))
		return false
	return true

func _navigate(button: Button, expected_page: String, prefix: String, settled_name: String) -> bool:
	if not await _click(button):
		return false
	return await _observe_navigation("mouse:left:" + str(button.name), expected_page, prefix, settled_name)

func _navigate_gamepad(button_index: int, expected_page: String, prefix: String, settled_name: String) -> bool:
	if finishing or hard_stopped:
		return false
	var previous_count := pressed_controls.size()
	for down in [true, false]:
		var event := InputEventJoypadButton.new()
		event.device = 0
		event.button_index = button_index
		event.pressed = down
		_record({"kind": "joypad_button", "device": event.device, "button_index": button_index, "pressed": down})
		target.push_input(event, true)
	if pressed_controls.size() != previous_count:
		_fail("Book gamepad navigation also activated a native button: " + prefix)
		return false
	return await _observe_navigation("gamepad:" + str(button_index), expected_page, prefix, settled_name)

func _navigate_right_back(expected_page: String, prefix: String, settled_name: String) -> bool:
	if finishing or hard_stopped:
		return false
	# Aim at a real control; book-level back must precede Control consumption.
	var point: Vector2 = title.project_button(title._leaf_back)
	_move(point)
	var previous_count := pressed_controls.size()
	for down in [true, false]:
		var event := InputEventMouseButton.new()
		event.position = point
		event.global_position = point
		event.button_index = MOUSE_BUTTON_RIGHT
		event.button_mask = MOUSE_BUTTON_MASK_RIGHT if down else 0
		event.pressed = down
		_record({"kind": "mouse_button", "position": [point.x, point.y], "button_index": MOUSE_BUTTON_RIGHT, "pressed": down})
		target.push_input(event, true)
	if pressed_controls.size() != previous_count:
		_fail("Right-click back also activated a native button: " + prefix)
		return false
	return await _observe_navigation("mouse:right", expected_page, prefix, settled_name)

func _observe_navigation(source: String, expected_page: String, prefix: String, settled_name: String) -> bool:
	var animation_deadline := Time.get_ticks_usec() + 3500000
	var sequence := 0
	# The first frame can still be the old page while its GPU copy completes.
	while sequence == 0 or title._turning:
		if finishing or hard_stopped:
			return false
		if Time.get_ticks_usec() >= animation_deadline:
			_fail("Page turn did not finish: " + prefix)
			return false
		if not await _shot("%s-%03d" % [prefix, sequence]):
			return false
		sequence += 1
		var next_frame := Time.get_ticks_usec() + FRAME_INTERVAL_US
		while Time.get_ticks_usec() < next_frame and title._turning and not finishing:
			await process_frame
	# Navigation settles only settings/load/cover. Starting owns a separate
	# locked cover until Main has prepared the actual field.
	await process_frame
	await process_frame
	if title._current_page != expected_page or title._turning:
		_fail("Page did not settle on " + expected_page + " after " + prefix)
		return false
	_record({"kind": "navigation_result", "source": source, "expected_page": expected_page, "page": title._current_page,
		"page_history": title._page_history.duplicate(), "animation_frames": sequence})
	return await _shot(settled_name)

func _run() -> void:
	var archive_script := load("res://tools/FrameArchive.cs")
	var title_scene := load("res://scenes/UI/TitleUI.tscn") as PackedScene
	if archive_script == null or title_scene == null:
		_fail("Could not load production TitleUI or FrameArchive")
		await _finish(3)
		return
	archive = archive_script.new()
	if archive == null:
		_fail("FrameArchive could not be instantiated; build the .NET project before capture")
		await _finish(3)
		return
	root.add_child(archive)
	archive.Configure(0)
	_record({"kind": "capture_start", "scope": "production TitleUI only; no Main or DV session", "resolution": [1280, 992], "frame_interval_us": FRAME_INTERVAL_US, "hard_limit_us": HARD_LIMIT_US})
	target = SubViewport.new()
	target.name = "TitleCapture"
	target.size = Vector2i(1280, 992)
	target.handle_input_locally = true
	target.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(target)
	var presentation := TextureRect.new()
	presentation.texture = target.get_texture()
	presentation.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	presentation.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	presentation.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(presentation)
	root.show()
	title = title_scene.instantiate()
	title.ui_event.connect(_on_ui_event)
	target.add_child(title)
	for button: Button in [title.settings_button, title.continue_button, title.start_button, title._leaf_back, title._leaf_next]:
		button.pressed.connect(_on_pressed.bind(str(button.name)))
	var ready_deadline := Time.get_ticks_usec() + 4500000
	while not title._menu_input_enabled:
		if finishing or Time.get_ticks_usec() >= ready_deadline:
			_fail("Production title never enabled its menu")
			await _finish(4)
			return
		await process_frame
	_record({"kind": "capture_configuration", "reduce_motion": title._reduce_motion})
	if title._reduce_motion:
		_fail("Reduce-motion is enabled; animation capture requires normal motion and does not change saved preferences")
		await _finish(4)
		return
	_move(Vector2(640, 496))
	await create_timer(0.4).timeout
	if not await _shot("cover-page"):
		await _finish(6)
		return
	_move(Vector2(75, 100))
	await create_timer(0.5).timeout
	if not await _shot("pointer-left"):
		await _finish(6)
		return
	_move(Vector2(1205, 892))
	await create_timer(0.5).timeout
	if not await _shot("pointer-right"):
		await _finish(6)
		return
	_move(Vector2(640, 496))
	await create_timer(0.8).timeout
	for index in range(12):
		await create_timer(0.12).timeout
		if not await _shot("light-%02d" % index):
			await _finish(6)
			return
	# Exercise the production notification without changing saved user preferences.
	title._on_setting_changed("accessibility", "reduce_motion", true)
	await process_frame
	var dust: GPUParticles3D = title._desk.atmosphere.emitter
	if dust.visible or not is_zero_approx(dust.speed_scale):
		_fail("Reduce motion did not stop the spatial dust")
		title._on_setting_changed("accessibility", "reduce_motion", false)
		await _finish(7)
		return
	if not await _shot("reduced-motion-still"):
		await _finish(6)
		return
	title._on_setting_changed("accessibility", "reduce_motion", false)
	await process_frame
	if not dust.visible or not is_equal_approx(dust.speed_scale, 1.0):
		_fail("Spatial dust did not resume")
		await _finish(7)
		return
	_record({"kind":"motion_preference_observation", "saved_preferences_changed":false, "dust_resumed":dust.visible and is_equal_approx(dust.speed_scale,1.0)})
	if not await _navigate(title.settings_button, "settings", "settings-open", "settings-page"):
		await _finish(4)
		return
	if not await _navigate_gamepad(JOY_BUTTON_RIGHT_SHOULDER, "load", "rb-to-load", "load-after-rb"):
		await _finish(4)
		return
	if not await _navigate_gamepad(JOY_BUTTON_LEFT_SHOULDER, "settings", "lb-to-settings", "settings-after-lb"):
		await _finish(4)
		return
	# Back unwinds each visited paper in order: settings -> load -> settings -> cover.
	if not await _navigate_right_back("load", "right-back-to-load", "load-after-right-back"):
		await _finish(4)
		return
	if not await _navigate_gamepad(JOY_BUTTON_B, "settings", "b-back-to-settings", "settings-after-b-back"):
		await _finish(4)
		return
	if not await _navigate_right_back("cover", "right-back-to-cover", "cover-after-settings"):
		await _finish(4)
		return
	if not await _navigate(title.continue_button, "load", "load-open", "load-page"):
		await _finish(4)
		return
	if not await _navigate_gamepad(JOY_BUTTON_B, "cover", "b-close-load", "cover-after-load"):
		await _finish(4)
		return
	if not await _click(title.start_button):
		await _finish(4)
		return
	await process_frame
	if title._current_page != "cover" or not title._request_pending or title._opening_kind != "start" or title._menu_input_enabled or not title.viewport.gui_disable_input or title._turning:
		_fail("Start must retain the cover with opening ownership and locked input until the field is ready.")
		await _finish(4)
		return
	if not await _shot("cover-opening-locked"):
		await _finish(6)
		return
	_record({"kind": "opening_lock", "page": title._current_page, "opening": title._request_pending,
		"opening_kind": title._opening_kind, "menu_input_enabled": title._menu_input_enabled,
		"page_input_disabled": title.viewport.gui_disable_input, "scope": "UI request only; no Main field preparation"})
	if emitted.size() != 1 or str(emitted[0].get("kind", "")) != "start":
		_fail("Expected exactly one start ui_event; observed " + str(emitted))
		await _finish(5)
		return
	var expected_buttons: Array[String] = [str(title.settings_button.name), str(title.continue_button.name), str(title.start_button.name)]
	if pressed_controls != expected_buttons:
		_fail("Expected only settings, continue and start button activations; observed " + str(pressed_controls))
		await _finish(5)
		return
	await _finish(0)

func _drain(deadline: int) -> bool:
	while archive != null:
		_poll_archive()
		if archive.IsIdle():
			_poll_archive()
			if archive.IsIdle(): return true
		if Time.get_ticks_usec() >= deadline or hard_stopped:
			return false
		await process_frame
	return true

func _finish(code: int) -> void:
	if finishing:
		return
	finishing = true
	var deadline: int = mini(origin + HARD_LIMIT_US - 150000, Time.get_ticks_usec() + 2300000)
	var drained := await _drain(deadline)
	var result := code
	if not drained or archive_failed:
		result = 6 if result == 0 else result
	_record({"kind": "close", "code": result, "emitted": emitted, "pressed_controls": pressed_controls, "frames_queued": frame_count, "archive_drained_before_close": drained, "failures": failures})
	if not await _drain(deadline):
		result = 6 if result == 0 else result
	if archive_failed and result == 0:
		result = 6
	print("UI_CAPTURE_COMPLETE code=", result, " frames=", frame_count, " elapsed_us=", Time.get_ticks_usec() - origin)
	quit(result)
