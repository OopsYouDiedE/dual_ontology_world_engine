extends Node
## Opt-in localhost transport for the production window. Inactive in normal play.
var _server := TCPServer.new()
var _peer: StreamPeerTCP
var _buffer := PackedByteArray()
var _token := ""
var _busy := false
var _observation := 0

func _ready() -> void:
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
	set_process(true)

func _process(_delta: float) -> void:
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
	if kind == "close":
		if host != null and host.has_method("wait_for_shutdown"): await host.wait_for_shutdown()
		_reply(id, {"closed": true})
		get_tree().quit()
		return
	if kind == "start":
		if host == null or not host.has_method("dispatch"):
			_reply(id, {}, "Current single scene has no campaign start operation")
			return
		if host.flow.started:
			_reply(id, {}, "Campaign already started")
			return
		host.dispatch({"kind": "start"})
	elif kind == "reload_content":
		if host == null or not host.has_method("dispatch") or not host.flow.started:
			_reply(id, {}, "Start a campaign before reloading definitions")
			return
		var operation := {"kind": "reload_content"}
		for key in ["path", "content_source"]:
			if request.has(key): operation[key] = request[key]
		host.dispatch(operation)
	elif kind == "select":
		if host == null or not host.has_method("console_action") or not host.flow.allows_selection():
			_reply(id, {}, "The game is not at a player selection boundary")
			return
		var choice := str(request.get("choice_id", ""))
		var available := false
		if is_instance_valid(host.field):
			for option: Dictionary in host.field.spatial.options:
				if str(option.get("id", "")) == choice: available = true
		if not available:
			_reply(id, {}, "choice_id is not in the current observation")
			return
		host.console_action(choice)
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
		for frame in frames: await get_tree().process_frame
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
	var logical_size := Vector2i(get_viewport().get_visible_rect().size)
	var controls: Array = []
	_collect_controls(get_tree().root, controls)
	_observation += 1
	var observation := {"backend": "godot", "observation_id": _observation,
		"scene": host.scene_file_path if host != null else "", "visible_ui": controls,
		"size": [logical_size.x, logical_size.y], "busy": false, "options": []}
	if bool(request.get("capture_image", true)):
		var image := get_viewport().get_texture().get_image()
		if image == null or image.is_empty():
			_reply(id, {}, "No rendered viewport; run the graphical Godot host")
			return
		if image.get_size() != logical_size: image.resize(logical_size.x, logical_size.y)
		observation.image_base64 = Marshalls.raw_to_base64(image.save_png_to_buffer())
	if host != null and host.has_method("dispatch"):
		observation.busy = host.flow.opening or host.flow.draining or host.flow.completion_pending \
			or host.flow.background_execution or not host.engine.IsDispatchIdle()
		observation.reading = host.flow.reading
		observation.started = host.flow.started
		observation.faulted = host.flow.view_faulted or host.flow.opening_failed
		var snapshot: Dictionary = host.engine.Snapshot()
		var state := {}
		for key in ["viewer", "viewer_title", "game_time", "chapter", "map", "room", "location_title", "state_version", "active", "content_hash", "content_error"]:
			if snapshot.has(key): state[key] = snapshot[key]
		observation.state = state
		if is_instance_valid(host.field) and host.flow.player_options_open:
			for option: Dictionary in host.field.spatial.options:
				observation.options.append({"id": option.get("id", ""), "label": option.get("label", "")})
	_reply(id, observation)

func _collect_controls(node: Node, result: Array) -> void:
	if result.size() >= 160: return
	if node is CanvasLayer and not node.visible: return
	if node is Control and not node.is_visible_in_tree(): return
	var text := ""
	if node is Label or node is Button: text = node.text
	elif node is RichTextLabel: text = node.get_parsed_text()
	elif node is LineEdit: text = node.text
	if text.strip_edges() != "":
		result.append({"path": str(node.get_path()), "text": text.substr(0, 2000), "class": node.get_class()})
	for child in node.get_children(): _collect_controls(child, result)
