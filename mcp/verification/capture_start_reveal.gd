extends SceneTree
## Level-two UI observation of the title and first interactive field or dialogue.
## This tool sends menu inputs only. It never selects a world action or answer.
## Use a rendering-capable Godot .NET build; build the C# host before running.

const FRAME_INTERVAL_US := 80000
const SOFT_LIMIT_US := 60000000
const HARD_LIMIT_US := 80000000

var target: SubViewport
var host: Node
var title: Control
var archive: Node
var output := ""
var resolution := Vector2i(1280, 720)
var reduced_motion := false
var original_motion: Variant
var motion_saved := false
var origin := 0
var finishing := false
var hard_stopped := false
var host_closed := false
var archive_failed := false
var failures: Array[String] = []
var emitted: Array[Dictionary] = []
var captured_frames := 0
var pointer := Vector2.ZERO
var assembly_diagnostic_recorded := false

func _initialize() -> void:
	origin = Time.get_ticks_usec()
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): output = argument.trim_prefix("--out=")
		elif argument == "--reduce-motion": reduced_motion = true
		elif argument.begins_with("--resolution="):
			var parts := argument.trim_prefix("--resolution=").split("x")
			if parts.size() != 2 or not parts[0].is_valid_int() or not parts[1].is_valid_int():
				push_error("Use --resolution=<width>x<height>.")
				quit(2)
				return
			resolution = Vector2i(int(parts[0]), int(parts[1]))
	if resolution.x < 640 or resolution.y < 480 or resolution.x > 2560 or resolution.y > 1600:
		push_error("Capture resolution must be within 640x480 and 2560x1600.")
		quit(2)
		return
	if output.is_empty() or not output.is_absolute_path() or DirAccess.dir_exists_absolute(output):
		push_error("Use --out=<new absolute directory>; existing evidence is never overwritten.")
		quit(2)
		return
	if DirAccess.make_dir_recursive_absolute(output) != OK:
		push_error("Cannot create capture directory: " + output)
		quit(2)
		return
	root.size = resolution
	root.title = "安眠者有福 · 开卷衔接观察"
	_run.call_deferred()

func _process(_delta: float) -> bool:
	if hard_stopped: return false
	var elapsed := Time.get_ticks_usec() - origin
	if elapsed >= HARD_LIMIT_US:
		hard_stopped = true
		_restore_motion()
		push_error("Opening capture reached its 80-second hard limit; retained partial evidence.")
		quit(9)
	elif elapsed >= SOFT_LIMIT_US and not finishing:
		_fail("Opening capture reached its 60-second observation limit.")
		_finish(9)
	return false

func _record(data: Dictionary) -> void:
	if not is_instance_valid(archive) or hard_stopped: return
	data.real_us = Time.get_ticks_usec() - origin
	archive.AppendRecords(output.path_join("opening-ui.dvlog"), JSON.stringify(data) + "\n")

func _fail(message: String) -> void:
	failures.append(message)
	push_error(message)
	_record({"kind": "observation_failure", "message": message})

func _preview_motion() -> void:
	var settings := root.get_node_or_null("GameSettings")
	if settings == null: return
	original_motion = settings.get_value("accessibility", "reduce_motion")
	motion_saved = true
	# Exercise production presentation settings without writing the user's config.
	settings._values["accessibility"]["reduce_motion"] = reduced_motion
	settings.setting_changed.emit("accessibility", "reduce_motion", reduced_motion)
	_record({"kind": "temporary_presentation_setting", "reduce_motion": reduced_motion, "saved_preferences_changed": false})

func _restore_motion() -> void:
	if not motion_saved: return
	var settings := root.get_node_or_null("GameSettings")
	if settings != null:
		settings._values["accessibility"]["reduce_motion"] = original_motion
		settings.setting_changed.emit("accessibility", "reduce_motion", original_motion)
	motion_saved = false

func _on_title_event(event: Dictionary) -> void:
	emitted.append(event.duplicate(true))
	_record({"kind": "title_ui_event", "event": event})

func _poll_archive() -> void:
	if not is_instance_valid(archive): return
	for encoded in archive.PollCompleted():
		var result: Variant = JSON.parse_string(str(encoded))
		if not result is Dictionary:
			archive_failed = true
			continue
		var has_error := result.get("error") != null and str(result.get("error")) != ""
		if has_error or result.get("kind", "") == "text-error":
			archive_failed = true
			push_error("Opening capture archive: " + str(result))
		# Text errors cannot be recursively written to the same failed stream.
		if result.get("kind", "") != "text-error":
			_record({"kind": "archive_completion", "result": result})

func _state() -> Dictionary:
	var data := {"title_present": is_instance_valid(title) and title.is_inside_tree()}
	if bool(data.title_present):
		data.page = str(title.get("_current_page"))
		data.turning = bool(title.get("_turning"))
		data.pending = bool(title.get("_request_pending"))
		data.menu_enabled = bool(title.get("_menu_input_enabled"))
		data.opening_error = str(title.get("_opening_error"))
	if is_instance_valid(host):
		data.opening = host.flow.opening
		data.opening_failed = host.flow.opening_failed
		data.field_present = is_instance_valid(host.get("field"))
		data.ui_present = is_instance_valid(host.get("ui"))
		data.host_closing = host.flow.closing
	return data

func _finish_if_opening_failed() -> bool:
	if not is_instance_valid(host) or not host.flow.opening_failed: return false
	var state := _state()
	var message := str(state.get("opening_error", ""))
	_record({"kind": "fatal_opening", "message": message, "state": state})
	if not assembly_diagnostic_recorded:
		assembly_diagnostic_recorded = true
		_record(_opening_assembly_diagnostic())
	_fail("The production host reported a fatal opening error" + (": " + message if not message.is_empty() else "."))
	# Keep one actual error frame, even if earlier samples filled the PNG queue.
	# A bounded wait preserves the existing capture and shutdown deadlines.
	var deadline := Time.get_ticks_usec() + 2000000
	while not archive.HasCapacity() and not finishing and not hard_stopped and Time.get_ticks_usec() < deadline:
		_poll_archive()
		await process_frame
	await _shot("05-opening-failed", "first fatal production opening error")
	await _finish(5)
	return true

func _opening_assembly_diagnostic() -> Dictionary:
	# Failure-only, read-only instrumentation. These queries do not install an
	# actor, choose an initial point, refresh navigation or advance the session.
	var record := {"kind": "fatal_opening_assembly", "read_only": true, "performance_comparison": false,
		"actors": [], "missing_people": []}
	var field: Node2D = host.get("field")
	if not is_instance_valid(field):
		record.field_present = false
		return record
	record.field_present = true
	var adapter: Node = field.get("adapter")
	var executor: Node = host.get("field_executor")
	var actors: Dictionary = field.get("actors")
	for actor_id in actors:
		var actor: Node2D = actors[actor_id]
		var row := {"id": str(actor_id), "valid": is_instance_valid(actor)}
		if is_instance_valid(actor):
			row.instance_id = actor.get_instance_id()
			row.position = [actor.position.x, actor.position.y]
			row.inside_tree = actor.is_inside_tree()
		record.actors.append(row)
	if not is_instance_valid(adapter) or not is_instance_valid(executor):
		record.adapter_present = is_instance_valid(adapter)
		record.executor_present = is_instance_valid(executor)
		return record
	var current_map := str(adapter.map_key())
	record.current_map = current_map
	record.access_refresh_pending = bool(adapter.get("_access_refresh_pending"))
	var grids: Dictionary = adapter.get("_grids")
	record.current_map_grid_present = grids.has(current_map)
	var layout: Node = adapter.get("map")
	var spawns: Node = layout.get_node_or_null("Spawns") if is_instance_valid(layout) else null
	for source in ["snapshot", "execution_binding"]:
		var state: Dictionary = field.get(source)
		for person in state.get("people", []):
			if not person is Dictionary or str(person.get("map", "")) != current_map or not bool(person.get("present", true)): continue
			var who := str(person.get("key", ""))
			if actors.has(who) and is_instance_valid(actors[who]): continue
			var binding: Dictionary = executor._actor_binding(who, state)
			var missing := {"source": source, "person": person.duplicate(true), "binding": binding,
				"actor_key_present": actors.has(who), "candidates": []}
			var anchor_name := str(binding.get("anchor", ""))
			missing.initial_anchor = _assembly_candidate(field, adapter, executor, who, "initial_anchor", anchor_name,
				adapter.anchor(anchor_name) if not anchor_name.is_empty() else null)
			var index := 0
			for point in adapter.stands():
				missing.candidates.append(_assembly_candidate(field, adapter, executor, who, "stand", str(index), point))
				index += 1
			if is_instance_valid(spawns):
				for marker in spawns.get_children():
					if marker is Marker2D:
						missing.candidates.append(_assembly_candidate(field, adapter, executor, who, "spawn_marker", str(marker.name), marker.position))
			var spawn_name := str(binding.get("spawn", ""))
			if not spawn_name.is_empty():
				missing.candidates.append(_assembly_candidate(field, adapter, executor, who, "binding_spawn", spawn_name, adapter.anchor(spawn_name)))
			for placement in field.execution_binding.get("initial_placements", []):
				if str(placement.get("actor", "")) != who: continue
				var name := str(placement.get("anchor", ""))
				missing.candidates.append(_assembly_candidate(field, adapter, executor, who, "initial_placement", name, adapter.anchor(name)))
			record.missing_people.append(missing)
	return record

func _assembly_candidate(field: Node2D, adapter: Node, executor: Node, who: String, source: String, name: String, point: Variant) -> Dictionary:
	var result := {"source": source, "name": name, "resolved": point is Vector2}
	if point is Vector2:
		result.position = [point.x, point.y]
		result.entered_room = str(adapter.entered_room(point))
		result.walkable = bool(adapter.walkable(point))
		result.occupied = bool(executor._initial_occupied(who, field, point))
	return result

func _shot(name: String, scope: String) -> bool:
	if finishing or hard_stopped: return false
	_poll_archive()
	if not archive.HasCapacity():
		_record({"kind": "capture_gap", "file": name + ".png", "reason": "archive_backpressure", "state": _state()})
		return true
	await RenderingServer.frame_post_draw
	if finishing or hard_stopped: return false
	var captured_us := Time.get_ticks_usec() - origin
	var image := target.get_texture().get_image()
	if image == null or image.is_empty():
		_fail("No rendered image available: " + name)
		return false
	image.convert(Image.FORMAT_RGBA8)
	var copied_us := Time.get_ticks_usec() - origin
	archive.Enqueue(image.get_data(), image.get_width(), image.get_height(), output.path_join(name + ".png"), captured_us)
	captured_frames += 1
	_record({"kind": "rendered_frame", "file": name + ".png", "scope": scope, "captured_us": captured_us,
		"gpu_readback_copy_us": copied_us - captured_us, "state": _state(), "write_status": "queued"})
	return true

func _move(point: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = point
	event.global_position = point
	event.relative = point - pointer
	pointer = point
	_record({"kind": "mouse_motion", "position": [point.x, point.y], "relative": [event.relative.x, event.relative.y]})
	target.push_input(event, true)

func _button(point: Vector2, button_index: MouseButton, purpose: String) -> void:
	for down in [true, false]:
		var event := InputEventMouseButton.new()
		event.position = point
		event.global_position = point
		event.button_index = button_index
		event.button_mask = (MOUSE_BUTTON_MASK_LEFT if button_index == MOUSE_BUTTON_LEFT else MOUSE_BUTTON_MASK_RIGHT) if down else 0
		event.pressed = down
		_record({"kind": "mouse_button", "button_index": button_index, "pressed": down, "position": [point.x, point.y], "purpose": purpose})
		target.push_input(event, true)

func _key(keycode: Key, purpose: String) -> void:
	for down in [true, false]:
		var event := InputEventKey.new()
		event.keycode = keycode
		event.pressed = down
		_record({"kind": "key_input", "keycode": keycode, "pressed": down, "purpose": purpose})
		target.push_input(event, true)

func _click(control: Button) -> bool:
	if finishing or hard_stopped: return false
	if not is_instance_valid(control) or control.disabled or not control.is_visible_in_tree():
		_fail("Requested title control is unavailable.")
		return false
	var point: Vector2 = title.project_button(control)
	_move(point)
	await create_timer(0.12, true, false, true).timeout
	if finishing or hard_stopped or not is_instance_valid(title): return false
	point = title.project_button(control)
	_move(point)
	_button(point, MOUSE_BUTTON_LEFT, str(control.name))
	return true

func _wait_page(page: String) -> bool:
	var deadline := Time.get_ticks_usec() + 5000000
	while not finishing and Time.get_ticks_usec() < deadline:
		if is_instance_valid(title) and not bool(title.get("_turning")) and str(title.get("_current_page")) == page:
			await process_frame
			return true
		await process_frame
	if not finishing: _fail("The book did not settle on " + page + ".")
	return false

func _interactive_field_ready() -> bool:
	if not is_instance_valid(host) or host.flow.opening or host.flow.opening_failed: return false
	var field: Node = host.get("field")
	var ui: Node = host.get("ui")
	if not is_instance_valid(field) or not is_instance_valid(ui) or ui.is_busy() or host.flow.draining: return false
	var spatial: Node = field.get("spatial")
	return is_instance_valid(spatial) and spatial.enabled() and not spatial.options.is_empty()

func _run() -> void:
	var archive_script := load("res://tools/FrameArchive.cs")
	var host_scene := load("res://scenes/Host/Main.tscn") as PackedScene
	if archive_script == null or host_scene == null:
		push_error("Build the .NET host before running this capture.")
		quit(3)
		return
	archive = archive_script.new()
	if archive == null:
		push_error("FrameArchive could not be instantiated.")
		quit(3)
		return
	root.add_child(archive)
	archive.Configure(0)
	_record({"kind": "capture_start", "scope": "real menu inputs, first interactive field or natural dialogue; no world choices",
		"resolution": [resolution.x, resolution.y], "sample_interval_us": FRAME_INTERVAL_US,
		"soft_limit_us": SOFT_LIMIT_US, "hard_limit_us": HARD_LIMIT_US,
		"performance_comparison": false, "readback_instrumentation": true})
	_preview_motion()
	target = SubViewport.new()
	target.name = "OpeningUIObservation"
	target.size = resolution
	target.handle_input_locally = true
	target.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(target)
	var screen := TextureRect.new()
	screen.texture = target.get_texture()
	screen.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	screen.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(screen)
	root.show()
	host = host_scene.instantiate()
	host.shutdown_completed.connect(func(): host_closed = true)
	target.add_child(host)
	if not is_instance_valid(host.get("title_layer")):
		_fail("The production host did not create a title.")
		await _finish(4)
		return
	title = host.title_layer.get_child(0)
	title.ui_event.connect(_on_title_event)
	var deadline := Time.get_ticks_usec() + 8000000
	while not bool(title.get("_menu_input_enabled")) and not finishing and Time.get_ticks_usec() < deadline:
		await process_frame
	if finishing: return
	if not bool(title.get("_menu_input_enabled")):
		_fail("The production title did not enable its menu.")
		await _finish(4)
		return
	await _shot("01-cover", "production title")
	if not await _click(title.settings_button) or not await _wait_page("settings"):
		await _finish(4)
		return
	await _shot("02-settings", "production book settings")
	if not await _click(title._leaf_back) or not await _wait_page("cover"):
		await _finish(4)
		return
	await _shot("03-cover-return", "production title after settings")
	if not await _click(title.start_button):
		await _finish(4)
		return
	# Repeat inputs in the same frame, while the title owns the gesture. There
	# is no timed input which could accidentally advance the opening dialogue.
	_button(pointer, MOUSE_BUTTON_LEFT, "duplicate start gesture")
	_key(KEY_ENTER, "duplicate confirm during loading")
	_key(KEY_ESCAPE, "cancel during loading")
	_button(pointer, MOUSE_BUTTON_RIGHT, "back during loading")
	_record({"kind": "loading_input_observation", "state": _state(), "title_events": emitted.duplicate(true)})
	if str(title.get("_current_page")) != "cover":
		_fail("Start left the cover before field preparation completed.")
	var sequence := 0
	var next_capture := Time.get_ticks_usec()
	deadline = Time.get_ticks_usec() + 35000000
	while is_instance_valid(host.get("title_layer")) and not finishing and Time.get_ticks_usec() < deadline:
		if await _finish_if_opening_failed(): return
		if Time.get_ticks_usec() >= next_capture:
			await _shot("04-opening-%03d" % sequence, "production loading and page reveal")
			sequence += 1
			next_capture = Time.get_ticks_usec() + FRAME_INTERVAL_US
		await process_frame
	if finishing: return
	if await _finish_if_opening_failed(): return
	if is_instance_valid(host.get("title_layer")) or not is_instance_valid(host.get("field")) or not is_instance_valid(host.get("ui")):
		_fail("The initial field was not revealed within the observation window.")
		await _shot("05-unavailable", "production opening deadline")
		await _finish(5)
		return
	await _shot("05-first-field", "first revealed production field")
	var start_events := 0
	for event in emitted:
		if str(event.get("kind", "")) == "start": start_events += 1
	_record({"kind": "revealed_field", "state": _state(), "start_ui_events": start_events})
	if start_events != 1: _fail("Repeated loading inputs emitted " + str(start_events) + " start requests.")
	deadline = Time.get_ticks_usec() + 15000000
	while not host.ui.dialogue_ui.is_dialogue_active() and not _interactive_field_ready() and not finishing and Time.get_ticks_usec() < deadline:
		await process_frame
	if finishing: return
	if host.ui.dialogue_ui.is_dialogue_active():
		await create_timer(0.4, true, false, true).timeout
		await _shot("06-first-dialogue", "first production dialogue; no continue input")
		_record({"kind": "first_dialogue", "presentation": host.ui._trace_display.duplicate(true), "world_inputs_selected": false})
	elif _interactive_field_ready():
		# Let the already-requested contextual hint settle without sending input.
		await create_timer(0.4, true, false, true).timeout
		await _shot("06-first-input", "first interactive production field; no world choice")
		_record({"kind": "first_player_input", "input_enabled": host.field.spatial.enabled(),
			"options_count": host.field.spatial.options.size(), "ui_busy": host.ui.is_busy(),
			"draining": host.flow.draining, "world_inputs_selected": false})
	else:
		_fail("Neither an interactive field nor natural dialogue became ready within 15 seconds of reveal.")
		await _shot("06-input-unavailable", "production input readiness deadline")
	await _finish(0 if failures.is_empty() else 5)

func _finish(code: int) -> void:
	if finishing: return
	finishing = true
	_record({"kind": "observation_end", "state": _state(), "code": code, "failures": failures, "title_events": emitted})
	if is_instance_valid(host):
		host.begin_shutdown()
		var close_deadline := mini(origin + HARD_LIMIT_US - 4000000, Time.get_ticks_usec() + 15000000)
		while not host_closed and Time.get_ticks_usec() < close_deadline and not hard_stopped:
			_poll_archive()
			await process_frame
		_record({"kind": "host_shutdown", "completed": host_closed})
		if not host_closed and code == 0: code = 7
	_restore_motion()
	var archive_deadline := mini(origin + HARD_LIMIT_US - 200000, Time.get_ticks_usec() + 3500000)
	var drained := await _drain_archive(archive_deadline)
	if (not drained or archive_failed) and code == 0: code = 6
	_record({"kind": "capture_close", "code": code, "host_shutdown_completed": host_closed,
		"archive_drained_before_close": drained, "frames_queued": captured_frames,
		"failures": failures, "saved_preferences_changed": false})
	if not await _drain_archive(archive_deadline) and code == 0: code = 6
	print("OPENING_UI_CAPTURE_COMPLETE code=", code, " frames=", captured_frames,
		" elapsed_us=", Time.get_ticks_usec() - origin, " host_closed=", host_closed)
	quit(code)

func _drain_archive(deadline: int) -> bool:
	while is_instance_valid(archive) and not hard_stopped:
		_poll_archive()
		if archive.IsIdle():
			_poll_archive()
			if archive.IsIdle(): return true
		if Time.get_ticks_usec() >= deadline: return false
		await process_frame
	return not hard_stopped
