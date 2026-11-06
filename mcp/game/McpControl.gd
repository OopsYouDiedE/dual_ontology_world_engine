extends Node
## Opt-in localhost transport for the production window. Inactive in normal play.
var _server := TCPServer.new()
var _peer: StreamPeerTCP
var _buffer := PackedByteArray()
var _token := ""
var _busy := false
var _observation := 0
var _background_capture := false
var _frame_seconds: Array[float] = []
var _previous_frame_us := 0
var _play_frame_seconds: Array[float] = []
var _capture_interval_pending := false
var _gameplay_seconds: Array[float] = []
var _previous_gameplay_ready := false
var _gameplay_generation := -1
var _gameplay_start_us := 0
var _receipt_run := "%d-%d" % [OS.get_process_id(), int(Time.get_unix_time_from_system()*1000000)]

func _ready() -> void:
	_background_capture = "--dv-background-capture" in OS.get_cmdline_user_args()
	if not "--dv-visible-window" in OS.get_cmdline_user_args():
		for argument in OS.get_cmdline_user_args():
			if argument.begins_with("--dv-mcp-port="): _background_capture = true
	if _background_capture:
		get_window().mode = Window.MODE_MINIMIZED
		# A hidden MCP window must not consume the user's controller while
		# they use another application. Formal input_action remains available.
		for action in InputMap.get_actions():
			for event in InputMap.action_get_events(action):
				if event is InputEventJoypadButton or event is InputEventJoypadMotion:
					InputMap.action_erase_event(action, event)
	process_mode = Node.PROCESS_MODE_ALWAYS
	set_process(false)
	var port := 0
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--dv-mcp-port="): port = int(argument.trim_prefix("--dv-mcp-port="))
		elif argument.begins_with("--dv-mcp-token="): _token = argument.trim_prefix("--dv-mcp-token=")
	if port == 0: return
	if port < 1 or port > 65535 or _token.length() < 32:
		push_error("Invalid MCP localhost configuration")
		return
	var error := _server.listen(port, "127.0.0.1")
	if error != OK:
		push_error("MCP listen failed: " + error_string(error))
		return
	RenderingServer.viewport_set_measure_render_time(get_viewport().get_viewport_rid(), true)
	set_process(true)

func _process(_delta: float) -> void:
	var now := Time.get_ticks_usec()
	var scene := get_tree().current_scene
	var gameplay_ready := false
	if is_instance_valid(scene) and scene.has_method("session_is_idle"):
		gameplay_ready = scene.flow.started and not scene.flow.opening and is_instance_valid(scene.field) and is_instance_valid(scene.field.player)
		if _gameplay_generation != scene._session_generation:
			_gameplay_generation = scene._session_generation
			_gameplay_seconds.clear()
			_gameplay_start_us = 0
			_previous_gameplay_ready = false
	if gameplay_ready and _gameplay_start_us == 0: _gameplay_start_us = now
	if _previous_frame_us > 0:
		var interval := float(now-_previous_frame_us)/1000000.0
		_frame_seconds.append(interval)
		if not _capture_interval_pending:
			_play_frame_seconds.append(interval)
			if gameplay_ready and _previous_gameplay_ready: _gameplay_seconds.append(interval)
	_previous_gameplay_ready = gameplay_ready
	_capture_interval_pending = false
	_previous_frame_us = now
	if _frame_seconds.size() > 120: _frame_seconds.pop_front()
	if _play_frame_seconds.size() > 120: _play_frame_seconds.pop_front()
	if _background_capture:
		if get_window().mode != Window.MODE_MINIMIZED: get_window().mode = Window.MODE_MINIMIZED
		RenderingServer.force_draw(false)
	if _peer == null:
		if _server.is_connection_available(): _peer = _server.take_connection()
		return
	_peer.poll()
	if _peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		_peer = null
		_buffer.clear()
		return
	if _busy: return
	var available := _peer.get_available_bytes()
	if available > 0:
		var received := _peer.get_data(available)
		if received[0] == OK: _buffer.append_array(received[1])
	if _buffer.size() > 262144:
		_peer.disconnect_from_host()
		_buffer.clear()
		return
	var end := _buffer.find(10)
	if end < 0: return
	var request: Variant = JSON.parse_string(_buffer.slice(0, end).get_string_from_utf8())
	_buffer = _buffer.slice(end + 1)
	if not request is Dictionary:
		_reply(null, {}, "Expected a JSON object")
		return
	if str(request.get("token", "")) != _token:
		_reply(request.get("id"), {}, "Invalid MCP token")
		return
	_busy = true
	_handle.call_deferred(request)

func _reply(id: Variant, result: Dictionary, error := "") -> void:
	if _peer != null and _peer.get_status() == StreamPeerTCP.STATUS_CONNECTED:
		var response := {"id": id, "result": result} if error == "" else {"id": id, "error": error}
		_peer.put_data((JSON.stringify(response) + "\n").to_utf8_buffer())
	_busy = false

func _handle(request: Dictionary) -> void:
	var id: Variant = request.get("id")
	var kind := str(request.get("type", ""))
	var host := get_tree().current_scene
	var case_response: Dictionary = {}
	var book_camera_trace: Array = []
	var movement_trace: Array = []
	if kind.begins_with("authoring_"):
		if host==null or not host.has_method("authoring_control"):
			_reply(id,{},"This scene is not an authoring workbench")
			return
		var result: Dictionary=await host.authoring_control(request)
		if not bool(result.get("ok",false)):
			_reply(id,{},str(result.get("error","Authoring request failed")))
		else:
			_reply(id,result)
			if bool(result.get("closed",false)): get_tree().quit()
		return
	if kind == "close":
		if host != null and host.has_method("wait_for_shutdown"): await host.wait_for_shutdown()
		_reply(id, {"closed": true})
		get_tree().quit()
		return
	if kind in ["start","load"]:
		if host == null or not host.has_method("dispatch"):
			_reply(id, {}, "Current single scene has no campaign start operation")
			return
		if host.flow.started:
			_reply(id, {}, "Campaign already started")
			return
		host.dispatch({"kind": kind})
	elif kind == "character":
		if host == null or not host.has_method("native_character_mode") or not host.native_character_mode() or not is_instance_valid(host.native_character_host):
			_reply(id,{},"Start a native character session first");return
		var input_error: String=host.native_character_host.interface_command(request)
		if not input_error.is_empty(): _reply(id,{},input_error);return
	elif kind == "case_reasoning":
		if host == null or not host.has_method("request_case_reasoning"):
			_reply(id, {}, "This scene has no campaign proof interface")
			return
		case_response = await host.request_case_reasoning(request)
		if case_response.has("error"):
			_reply(id, {}, str(case_response.error))
			return
	elif kind == "reload_content":
		if host == null or not host.has_method("dispatch") or not host.flow.started:
			_reply(id, {}, "Start a campaign before reloading definitions")
			return
		var operation := {"kind": "reload_content"}
		for key in ["path", "content_source"]:
			if request.has(key): operation[key] = request[key]
		host.dispatch(operation)
	elif kind == "select":
		var selection_error := await _select_current_choice(host, str(request.get("choice_id", "")))
		if not selection_error.is_empty():
			_reply(id, {}, selection_error)
			return
	elif kind == "input_action":
		var action := str(request.get("action", ""))
		var frames := int(request.get("frames", 1))
		if not InputMap.has_action(action) or frames < 1 or frames > 120:
			_reply(id, {}, "Use an existing InputMap action and frames=1..120")
			return
		var press := InputEventAction.new()
		press.action = action
		press.pressed = true
		Input.parse_input_event(press)
		movement_trace.append(_player_presentation(host))
		book_camera_trace.append(_book_cameras())
		for frame in frames:
			await get_tree().process_frame
			movement_trace.append(_player_presentation(host))
			book_camera_trace.append(_book_cameras())
		var release := InputEventAction.new()
		release.action = action
		release.pressed = false
		Input.parse_input_event(release)
	elif kind == "click":
		var position := Vector2(float(request.get("x", -1)), float(request.get("y", -1)))
		if not get_viewport().get_visible_rect().has_point(position):
			_reply(id, {}, "Click is outside the current screenshot")
			return
		var motion := InputEventMouseMotion.new()
		motion.position = position
		motion.global_position = position
		get_viewport().push_input(motion, true)
		var press := InputEventMouseButton.new()
		press.position = position
		press.global_position = position
		press.button_index = MOUSE_BUTTON_LEFT
		press.pressed = true
		get_viewport().push_input(press, true)
		await get_tree().process_frame
		var release := press.duplicate() as InputEventMouseButton
		release.pressed = false
		get_viewport().push_input(release, true)
	elif kind != "observe":
		_reply(id, {}, "Unsupported Godot operation")
		return
	var wait_ms := clampi(int(request.get("wait_ms", 200)), 0, 10000)
	if wait_ms > 0: await get_tree().create_timer(float(wait_ms) / 1000.0, true).timeout
	await RenderingServer.frame_post_draw
	_capture_interval_pending = true
	var logical_size := Vector2i(get_viewport().get_visible_rect().size)
	var controls: Array = []
	_collect_controls(get_tree().root, controls)
	_observation += 1
	var observation := {"backend": "godot", "observation_id": _observation,
		"rendering_method": RenderingServer.get_current_rendering_method(),
		"rendering_driver": RenderingServer.get_current_rendering_driver_name(),
		"gpu": RenderingServer.get_video_adapter_name(),
		"scene": host.scene_file_path if host != null else "", "visible_ui": controls,
		"size": [logical_size.x, logical_size.y], "busy": false, "options": [],
		"window_mode": get_window().mode, "background_capture": _background_capture}
	var total_seconds := 0.0
	var maximum_seconds := 0.0
	for seconds in _frame_seconds:
		total_seconds += seconds
		maximum_seconds = maxf(maximum_seconds, seconds)
	observation.presentation_performance = {"sample_frames": _frame_seconds.size(),
		"mean_frame_ms": total_seconds * 1000.0 / maxi(1, _frame_seconds.size()),
		"maximum_frame_ms": maximum_seconds * 1000.0,
		"scope": "last 120 measured wall-clock process intervals in this live host, including pauses and capture overhead"}
	var play_samples := _play_frame_seconds.duplicate()
	play_samples.sort()
	var play_total := 0.0
	var slow_frames := 0
	for seconds in play_samples:
		play_total += seconds
		if seconds > 0.05: slow_frames += 1
	observation.presentation_performance["without_capture"] = {
		"sample_frames": play_samples.size(), "mean_frame_ms": play_total * 1000.0 / maxi(1, play_samples.size()),
		"p95_frame_ms": play_samples[int(floor((play_samples.size()-1)*0.95))] * 1000.0 if not play_samples.is_empty() else 0.0,
		"maximum_frame_ms": play_samples.back() * 1000.0 if not play_samples.is_empty() else 0.0,
		"over_50_ms": slow_frames, "scope": "last 120 process intervals excluding only intervals containing MCP screenshot/observation serialization"}
	var gameplay := _gameplay_seconds.duplicate()
	gameplay.sort()
	var gameplay_total := 0.0
	var gameplay_slow := 0
	var gameplay_severe := 0
	for seconds in gameplay:
		gameplay_total += seconds
		if seconds > 0.05: gameplay_slow += 1
		if seconds > 0.1: gameplay_severe += 1
	observation.presentation_performance["since_gameplay_reveal"] = {
		"sample_frames":gameplay.size(), "start_time_us":_gameplay_start_us,
		"mean_frame_ms":gameplay_total * 1000.0 / maxi(1,gameplay.size()),
		"p95_frame_ms":gameplay[int(floor((gameplay.size()-1)*0.95))] * 1000.0 if not gameplay.is_empty() else 0.0,
		"maximum_frame_ms":gameplay.back()*1000.0 if not gameplay.is_empty() else 0.0,
		"over_50_ms":gameplay_slow, "over_100_ms":gameplay_severe,
		"scope":"all measured process intervals after formal opening completes; excludes only MCP capture/serialization intervals; includes later resource loads, NPC work and dialogue"}
	observation.presentation_performance["engine_max_fps"] = Engine.max_fps
	observation.presentation_performance["pipeline_compilations"] = {
		"mesh":Performance.get_monitor(Performance.PIPELINE_COMPILATIONS_MESH),
		"surface":Performance.get_monitor(Performance.PIPELINE_COMPILATIONS_SURFACE),
		"draw":Performance.get_monitor(Performance.PIPELINE_COMPILATIONS_DRAW),
		"specialization":Performance.get_monitor(Performance.PIPELINE_COMPILATIONS_SPECIALIZATION)}
	observation.presentation_performance["process_ms"] = Performance.get_monitor(Performance.TIME_PROCESS) * 1000.0
	observation.presentation_performance["physics_ms"] = Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0
	observation.presentation_performance["render_cpu_ms"] = RenderingServer.viewport_get_measured_render_time_cpu(get_viewport().get_viewport_rid())
	observation.presentation_performance["render_gpu_ms"] = RenderingServer.viewport_get_measured_render_time_gpu(get_viewport().get_viewport_rid())
	observation.presentation_performance["render_setup_cpu_ms"] = RenderingServer.get_frame_setup_time_cpu()
	observation.player_presentation = _player_presentation(host)
	if host != null and host.has_method("streaming_observation"):
		observation["room_streaming"] = host.streaming_observation()
		observation.busy = not bool(observation.room_streaming.get("ready",false))
	var importer = preload("res://scenes/components/characters/ImportedCharacter.gd")
	var conversation = preload("res://scenes/components/characters/ConversationAnimationLibrary.gd")
	observation.animation_resources = {"retarget_builds": importer.retarget_builds,
		"retarget_cache_hits": importer.retarget_cache_hits, "retarget_build_ms": importer.retarget_build_usec / 1000.0,
		"conversation_builds": conversation.build_count, "conversation_cache_hits": conversation.cache_hits,
		"conversation_build_ms": conversation.build_usec / 1000.0}
	if not movement_trace.is_empty(): observation.movement_trace = movement_trace
	observation.book_cameras = _book_cameras()
	if not book_camera_trace.is_empty(): observation.book_camera_trace = book_camera_trace
	if not case_response.is_empty(): observation.case_reasoning = case_response.result
	var receipt_directory := ProjectSettings.globalize_path("res://.runtime/runs/godot-"+_receipt_run).simplify_path()
	var receipt_stem := "observation-%06d" % _observation
	var directory_error := DirAccess.make_dir_recursive_absolute(receipt_directory)
	var capture = null
	var screenshot_path := receipt_directory.path_join(receipt_stem + ".png")
	if bool(request.get("capture_image", true)):
		capture = preload("res://mcp/game/ViewportCapture.gd").new()
		capture.start(get_viewport().get_texture().get_rid(), logical_size, screenshot_path, directory_error == OK)
	if host != null and host.has_method("native_character_mode") and host.native_character_mode():
		observation["character_session"] = host.native_character_observation()
		observation.started = host.flow.started
		observation.busy = host.flow.opening
		observation.faulted = host.flow.opening_failed
		observation.state = observation.character_session.get("state",{})
	elif host != null and host.has_method("dispatch"):
		observation.busy = host.flow.opening or host.flow.draining or host.flow.completion_pending \
			or host.flow.background_execution or not host.session_is_idle()
		observation.reading = host.flow.reading
		observation.started = host.flow.started
		observation.faulted = host.flow.view_faulted or host.flow.opening_failed
		var snapshot: Dictionary = host.observe_session()
		observation.execution_status = {
			"opening": host.flow.opening,
			"draining": host.flow.draining,
			"completion_pending": host.flow.completion_pending,
			"background_execution": host.flow.background_execution,
			"session_idle": host.session_is_idle(),
			"physical_intent_active": not host._dv_intent.is_empty(),
			"pending_status": str(snapshot.get("pending", {}).get("status", "")),
			"world_ui_paused": host._world_ui_paused
		}
		observation.busy = observation.busy or not host._dv_intent.is_empty() \
			or str(snapshot.get("pending", {}).get("status", "")) == "in_flight"
		var state := {}
		for key in ["viewer", "viewer_title", "game_time", "chapter", "map", "room", "location_title", "state_version", "active", "content_hash", "content_error", "case_reasoning_info"]:
			if snapshot.has(key): state[key] = snapshot[key]
		observation.state = state
		if bool(snapshot.get("player_control", false)) and is_instance_valid(host.field):
			observation.busy = host.flow.blocks_input() or host.flow.reading or host.flow.book_reading or host.field.spatial.submitted 				or not bool(snapshot.get("locomotion_enabled", true))
		if is_instance_valid(host.field) and host._formal_selection_ready():
			for option: Dictionary in host.field.spatial.options:
				observation.options.append({"id": option.get("id", ""), "label": option.get("label", "")})
	if bool(request.get("debug", false)) and host != null and is_instance_valid(host.field):
		var debug := {"nearest": str(host.field.nearest), "sites": [], "spatial_options": [], "map_key": str(host.field.adapter.map_key()), "snapshot_sites": host.field.snapshot.get("sites", {}), "adapter_site_geometry": host.field.adapter.site_geometry(host.field.snapshot.get("sites", {}))}
		if is_instance_valid(host.field.player):
			debug.player_position = [host.field.player.position.x, host.field.player.position.y]
		for site_id in host.field.sites:
			var site: Dictionary = host.field.sites[site_id]
			var point: Vector2 = host.field.site_point(str(site_id))
			debug.sites.append({"id": str(site_id), "title": str(site.get("title", "")), "point": [point.x, point.y], "reach": float(site.get("reach", 0.0)), "visible": host.field.site_visible(str(site_id)), "distance": host.field.player.position.distance_to(point) if is_instance_valid(host.field.player) else -1.0})
		if is_instance_valid(host.field.spatial):
			for option: Dictionary in host.field.spatial.options:
				debug.spatial_options.append({"id": str(option.get("id", "")), "label": str(option.get("label", "")), "available": bool(option.get("available", true)), "interaction": option.get("interaction", {}), "reason": str(option.get("reason", ""))})
		observation.debug = debug
	if capture != null:
		while not capture.complete(): await get_tree().process_frame
		var captured: Dictionary = capture.result()
		if str(captured.message) != "":
			_reply(id, {}, str(captured.message))
			return
		observation.image_base64 = Marshalls.raw_to_base64(captured.png)
		observation.capture_performance = {"method": "rd_async", "readback_queue_ms": float(captured.readback_us)/1000.0, "readback_request_cpu_ms": float(captured.request_us)/1000.0, "png_encode_worker_ms": float(captured.encode_us)/1000.0, "png_write_worker_ms": float(captured.save_us)/1000.0, "encoding_count": 1, "texture_format": captured.texture_format}
		if directory_error == OK and captured.error == OK: observation.screenshot_path = screenshot_path
		elif captured.error != OK: push_warning("Could not save Godot observation screenshot")
	if directory_error == OK:
		var observation_path := receipt_directory.path_join(receipt_stem + ".json")
		var receipt := observation.duplicate(true)
		receipt.erase("image_base64")
		receipt["observation_path"] = observation_path
		receipt["process_id"] = OS.get_process_id()
		var receipt_file := FileAccess.open(observation_path, FileAccess.WRITE)
		if receipt_file != null:
			receipt_file.store_string(JSON.stringify(receipt, "  "))
			observation.observation_path = observation_path
		else:
			push_warning("Could not save Godot observation receipt")
	else:
		push_warning("Could not create Godot observation receipt directory")
	_reply(id, observation)

func _player_presentation(host: Node) -> Dictionary:
	if host != null and host.has_method("native_character_mode") and host.native_character_mode(): return host.native_character_physical()
	if host == null or not host.has_method("dispatch") or not is_instance_valid(host.field) or not is_instance_valid(host.field.player): return {}
	var field: Node = host.field
	var body: CharacterBody3D = field.player.physical_body
	if not is_instance_valid(body): return {}
	var result := {"time_us": Time.get_ticks_usec(), "physics_frame": Engine.get_physics_frames(),
		"position": [body.global_position.x, body.global_position.y, body.global_position.z],
		"yaw": body.visual.rotation.y, "target_yaw": body.target_yaw,
		"movement_speed_mps": body.movement_speed}
	if is_instance_valid(body.animation_player):
		result["animation"] = body.animation_player.current_animation
		result["animation_position"] = body.animation_player.current_animation_position
		result["animation_playing"] = body.animation_player.is_playing()
		result["animation_speed"] = body.animation_player.speed_scale
	if is_instance_valid(field.sight): result["sight_cast_max_ms"] = field.sight.cast_max_us / 1000.0
	if is_instance_valid(field.spatial):
		result["input_enabled"] = field.spatial.enabled()
		result["control"] = {"player_control":field.snapshot.get("player_control",false),
			"field_enabled":field.snapshot.get("field_enabled",false), "ui_busy":field.ui_busy.call(),
			"submitted":field.spatial.submitted, "player_daily":field._daily.has(field.player.name),
			"pending_actor":host._dv_intent.get("actor",""), "pending_action":host._dv_intent.get("action",{}).get("id",""),
			"locomotion":field.spatial.last_crossing.duplicate(true), "navigation":field.spatial.navigation_diagnostic.duplicate(true), "walking":field.spatial._walking,
			"route_points":field.spatial._route.size(),
			"travel_result":field.player.travel_result.duplicate(true), "room":field.player.physical_body.get_meta("room", "")}

	if is_instance_valid(field.adapter) and "candidate_sync_builds" in field.adapter:
		result["candidate_sync_builds"] = field.adapter.candidate_sync_builds
		result["candidate_sync_max_ms"] = field.adapter.candidate_sync_max_us / 1000.0
		result["candidate_row_updates"] = field.adapter.candidate_row_updates
		result["candidate_update_max_ms"] = field.adapter.candidate_update_max_us / 1000.0
		result["local_grid_builds"] = field.adapter.local_grid_builds
		result["local_grid_build_max_ms"] = field.adapter.local_grid_build_max_us / 1000.0
	var budget = preload("res://scenes/components/characters/PhysicalQueryBudget.gd")
	result["legacy_room_load_max_ms"] = 0.0
	result["legacy_room_instantiate_max_ms"] = 0.0
	result["legacy_room_registration_max_ms"] = 0.0
	result["physics_query_checkpoints"] = budget.checkpoints
	result["physics_query_yields"] = budget.waits
	result["navigation_grid_builds_all_stages"] = 0
	result["navigation_grid_cache_hits_all_stages"] = 0
	if is_instance_valid(field.stage3d):
		var map: Node = field.stage3d.map_port
		result["material_parameter_writes"] = map.material_parameter_writes
		result["observer_texture_updates"] = map.observer_texture_updates
		result["observer_update_ms"] = map.observer_update_us / 1000.0
		result["observer_update_max_ms"] = map.observer_update_max_us / 1000.0
		result["pending_cap_jobs"] = map._cap_jobs.size()
		result["cap_build_max_ms"] = map.cap_build_max_us / 1000.0
		result["material_view_changed_parameters"] = map.material_view_changed_parameters
		result["visible_surface_count"] = map.visible_surface_count
	return result

func _book_cameras() -> Array:
	var result: Array = []
	for stage in get_tree().get_nodes_in_group("book_camera_stages"):
		if not is_instance_valid(stage.camera): continue
		var position: Vector3 = stage.camera.position
		result.append({"stage": str(stage.get_path()), "position": [position.x, position.y, position.z],
			"pitch_degrees": rad_to_deg(stage.camera.rotation.x), "fov": stage.camera.fov,
			"turn_lift": stage.turn_lift, "title_spread": stage.title_spread, "casebook_spread": stage.casebook_spread})
	return result

func _select_current_choice(host: Node, choice: String) -> String:
	if not is_instance_valid(host) or not host.has_method("hold_background_for_input") or not host.has_method("_formal_selection_ready"):
		return "This scene has no formal player selection interface"
	if host.flow.reading or host._world_ui_paused:
		return "Close the reading or paused interface before selecting a world action"
	# Do not race a newly queued NPC tick or pulse. Already accepted work still
	# finishes normally; this reservation neither pauses bodies nor fakes idle.
	host.hold_background_for_input(true)
	var error := ""
	var deadline := Time.get_ticks_msec() + 10000
	while is_instance_valid(host) and get_tree().current_scene == host:
		if host.flow.closing or host.flow.view_faulted or host.flow.opening_failed or host.flow.reading or host._world_ui_paused: break
		if host.session_is_idle() and host._formal_selection_ready(): break
		if Time.get_ticks_msec() >= deadline: break
		await get_tree().process_frame
	if not is_instance_valid(host) or get_tree().current_scene != host:
		error = "The game scene changed before the selection could be submitted"
	elif host.flow.reading or host._world_ui_paused:
		error = "The reading or paused interface must close before selecting a world action"
	elif not host.session_is_idle() or not host._formal_selection_ready():
		error = "The game is still busy or is not at a player selection boundary"
	elif not is_instance_valid(host.field) or not is_instance_valid(host.field.spatial):
		error = "The current scene has no spatial selection interface"
	else:
		var current: Dictionary = {}
		# Re-read the live options after draining work, never replay the observed row.
		for option: Dictionary in host.field.spatial.options:
			if str(option.get("id", "")) == choice:
				current = option
				break
		if current.is_empty() or not bool(current.get("available", true)):
			error = "choice_id is no longer available in the current observation"
		else:
			var interaction: Dictionary = current.get("interaction", {})
			var interaction_kind := str(interaction.get("kind", ""))
			var site := "self" if interaction_kind == "self" else "person:" + str(interaction.get("person", "")) if interaction_kind == "person" else str(interaction.get("site", ""))
			if host.field.spatial.submitted:
				error = "A spatial selection is already awaiting completion"
			else:
				host.field.spatial.select(choice, site)
				if not host.field.spatial.submitted:
					error = "The current spatial interface did not accept this choice; approach its interaction site first"
	if is_instance_valid(host): host.hold_background_for_input(false)
	return error

func _collect_controls(node: Node, result: Array) -> void:
	if result.size() >= 160: return
	if node is SubViewport and node.has_meta("physical_book_view"):
		var physical_book: Control = node.get_meta("physical_book_view")
		if not is_instance_valid(physical_book) or not physical_book.is_active(): return
	if node is CanvasLayer and not node.visible: return
	if node is Control and not node.is_visible_in_tree(): return
	var text := ""
	if node is Label or node is Button: text = node.text
	elif node is RichTextLabel: text = node.get_parsed_text()
	elif node is LineEdit: text = node.text
	elif node is TextEdit: text = node.text
	if text.strip_edges() != "" or node is BaseButton or node is Range or node is TextEdit or node is LineEdit:
		var item := {"path": str(node.get_path()), "text": text.substr(0, 2000), "class": node.get_class()}
		if node is BaseButton:
			item["disabled"] = node.disabled
			item["pressed"] = node.button_pressed
		if node is Range:
			item["value"] = node.value
			item["min"] = node.min_value
			item["max"] = node.max_value
		if node is Control:
			var rect: Rect2 = node.get_global_rect()
			var source_viewport: Viewport = node.get_viewport()
			if source_viewport.has_meta("physical_book_view"):
				var book_view: Control = source_viewport.get_meta("physical_book_view")
				if is_instance_valid(book_view): rect = book_view.screen_rect_for_control(node)
			item.rect = [rect.position.x, rect.position.y, rect.size.x, rect.size.y]
		result.append(item)
	for child in node.get_children(): _collect_controls(child, result)
