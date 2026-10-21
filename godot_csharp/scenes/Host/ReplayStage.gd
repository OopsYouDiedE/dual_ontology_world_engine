extends Control
## The console reuses the production Main, WorldView, StoryActor and book UI
## in a private viewport. ReplaySession has no DV authority or persistence.
const SESSION := preload("res://scenes/Host/ReplaySession.gd")
const PRESENTATION := preload("res://scenes/Host/HostPresentation.gd")
const GEOMETRY := preload("res://scenes/Host/GeometryCatalog.gd")
const MAIN_PATH := "res://scenes/Host/Main.gd"
var _surface: TextureRect
var _viewport: SubViewport
var _host: Node
var _session: Node
var _queue: Array[Dictionary] = []
var _draining := false
var _resetting := false
var _generation := 0
var _playing := false
var _step_active := false
var _speed := 1.0
var _process_modes: Dictionary = {}
var _raw_inputs: Dictionary = {}
var _seen_bodies: Dictionary = {}
var _played_actions: Dictionary = {}
var _context_signature := ""
var _has_raw_input := false
var _dialogue_remaining := -1.0
var _last_duration := 0.3
var last_note := ""

func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_surface = TextureRect.new()
	_surface.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_surface.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_surface.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_surface.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_surface)
	set_meta("replay_surface", true)

func busy() -> bool:
	return _draining or _resetting or not _queue.is_empty()

func reset() -> void:
	_generation += 1
	_queue.clear()
	_raw_inputs.clear()
	_seen_bodies.clear()
	_played_actions.clear()
	_context_signature = ""
	_has_raw_input = false
	_dialogue_remaining = -1.0
	last_note = ""
	_retire()

func _retire() -> void:
	_resetting = true
	if is_instance_valid(_host):
		_resume_nodes()
		await _host.wait_for_shutdown()
	if is_instance_valid(_viewport):
		remove_child(_viewport)
		_viewport.queue_free()
	_host = null
	_session = null
	_viewport = null
	_process_modes.clear()
	_resetting = false

func set_playing(value: bool, speed: float) -> void:
	_playing = value
	_speed = speed
	_apply_process_mode()

func present(frame: Dictionary, animate: bool) -> void:
	if frame.is_empty() or (is_instance_valid(_host) and _host.flow.closing): return
	_queue.append({"frame": frame, "animate": animate})
	if not _draining: _drain()

func _ensure_host(viewport_size: Array = []) -> void:
	if is_instance_valid(_host): return
	_viewport = SubViewport.new()
	_viewport.name = "ReplayViewport"
	_viewport.size = Vector2i(int(viewport_size[0]), int(viewport_size[1])) if viewport_size.size() == 2 else Vector2i(1280, 720)
	_viewport.handle_input_locally = true
	_viewport.world_2d = World2D.new()
	_viewport.own_world_3d = true
	_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(_viewport)
	_surface.texture = _viewport.get_texture()
	_session = SESSION.new()
	_host = Node.new()
	_host.set_script(load(MAIN_PATH))
	_host.name = "ReplayMain"
	_host.replay_mode = true
	_host.session_override = _session
	_viewport.add_child(_host)
	_apply_process_mode()

func _drain() -> void:
	_draining = true
	var generation := _generation
	while not _queue.is_empty() and generation == _generation:
		while _resetting: await get_tree().process_frame
		var request: Dictionary = _queue.pop_front()
		_step_active = not bool(request.animate)
		var frame: Dictionary = request.frame
		var data: Dictionary = frame.get("data", {})
		_ensure_host(data.get("viewport_size", []))
		_apply_process_mode()
		_last_duration = maxf(0.01, float(frame.get("duration", 0.3)))
		await _apply_frame(frame, generation)
		if generation != _generation: break
		_step_active = false
		_apply_process_mode()
	_draining = false
	if not _queue.is_empty(): _drain.call_deferred()

func _apply_frame(frame: Dictionary, generation: int) -> void:
	var data: Dictionary = frame.get("data", {})
	if data.has("viewport_size"):
		var dimensions: Array = data.viewport_size
		_viewport.size = Vector2i(int(dimensions[0]), int(dimensions[1]))
	if data.has("snapshot"):
		_session.SetSnapshot(data.snapshot)
		var installed: bool = await _host.install_replay_snapshot(_session.Snapshot(), data.get("casebook", {}))
		if not installed or generation != _generation: return
		await _apply_bodies(data.get("bodies", []), false)
	elif str(frame.kind) == "move":
		await _move(frame)
	elif data.has("receipt") and data.receipt is Dictionary and data.receipt.get("field") is Dictionary:
		var receipt: Dictionary = data.receipt
		var physical: Dictionary = receipt.field
		if physical.has("trajectory") or physical.has("source_position"):
			var movement := frame.duplicate(true)
			movement.actor = str(physical.get("who", frame.actor))
			movement.action = str(physical.get("action", frame.action))
			movement.data.position = {"map": physical.get("map", physical.get("source_map", "")),
				"room": physical.get("room", physical.get("source_room", ""))}
			await _move(movement)
	elif data.has("bodies"):
		await _apply_bodies(data.bodies, false)
	if generation != _generation: return
	var context: Dictionary = data.get("context", {})
	if context.is_empty() and str(frame.kind) == "visible": context = data.get("visible", {})
	await _restore_context(context, data.get("casebook", {}))
	if generation != _generation: return
	match str(frame.kind):
		"input": await _input_record(data.get("input", {}), data.has("bodies"))
		"presentation", "dialogue", "investigate":
			var presentation: Dictionary = data.get("presentation", {})
			if presentation.is_empty():
				presentation = {"visible": true, "surface": str(frame.text), "lines": [],
					"actor": str(frame.actor), "speaker_name": "", "status": str(data.get("status", "recorded")),
					"execution_event": str(data.get("event_id", "replay-%s" % frame.sequence)),
					"presentation_id": str(data.get("presentation_id", "replay-%s" % frame.sequence)),
					"source_refs": [str(data.get("source", "recorded-event"))]}
			if is_instance_valid(_host.ui):
				await _finish_previous_text()
				_host._dv_events([{"kind": "dv_present", "presentation": presentation}])
				_dialogue_remaining = _last_duration
		"visible":
			# A render snapshot, when recorded, can install the actual view.
			# Plain text membership remains evidence text in the console.
			var visible: Dictionary = data.get("visible", {})
			var render: Variant = visible.get("render", visible.get("snapshot"))
			if render is Dictionary and render.has("viewer"):
				_session.SetSnapshot(render)
				await _host.install_replay_snapshot(_session.Snapshot(), data.get("casebook", {}))

func _restore_context(context: Dictionary, casebook: Dictionary) -> void:
	if not casebook.is_empty() and not _session.Snapshot().is_empty():
		_host._casebook.restore_data(casebook, str(_session.Snapshot().viewer))
	if context.is_empty(): return
	var signature := JSON.stringify(context)
	if signature == _context_signature: return
	_context_signature = signature
	if is_instance_valid(_host.title_layer):
		var title: Control = _host.title_layer.get_child(0)
		await title.restore_replay_state(context.get("book_state", {"page": context.get("page", "cover")}))
	elif is_instance_valid(_host.ui):
		await _host.ui.restore_replay_context(context)

func _apply_bodies(samples: Array, checkpoint: bool) -> void:
	if not is_instance_valid(_host.field): return
	var field: Node2D = _host.field
	field.spatial.begin_rollback()
	var moving: Array[Dictionary] = []
	for sample: Dictionary in samples:
		if str(sample.get("map", "")) != str(field.adapter.map_key()): continue
		var actor := str(sample.actor)
		var body: CharacterBody2D = field.actors.get(actor)
		if not is_instance_valid(body): continue
		var target: Vector2 = GEOMETRY.point(sample.position)
		var key := actor + ":" + str(sample.map)
		if checkpoint or not _seen_bodies.has(key):
			field._cancel_motion(body)
			body.position = target
			body.velocity = Vector2.ZERO
			_seen_bodies[key] = true
		elif body.position.distance_to(target) > 1.0:
			var route: PackedVector2Array = field.navigation_route(body.position, target, field.adapter.nav_grid(body))
			route.append(target)
			var old_speed: float = body.speed
			var distance: float = field._route_cost(body.position, route, target)
			body.speed = maxf(1.0, distance / _last_duration * _speed / Engine.time_scale)
			field._cancel_motion(body)
			body.follow_path(route)
			moving.append({"body": body, "speed": old_speed})
	for item: Dictionary in moving:
		var body: CharacterBody2D = item.body
		while is_instance_valid(body) and body.travelling: await get_tree().physics_frame
		if is_instance_valid(body):
			body.speed = float(item.speed)
			if not bool(body.travel_result.get("ok", false)): last_note = "记录身体片段受当前碰撞阻挡：" + str(body.travel_result.get("reason", ""))
	if not is_instance_valid(field): return
	field.execution_bridge.note_map(field)
	field.execution_bridge.sync_obstacles(field)
	field.spatial.finish_transaction(true)

func _move(frame: Dictionary) -> void:
	if not is_instance_valid(_host.field):
		last_note = "记录缺少起始快照；保留该动作文本。"
		return
	var generation := _generation
	var data: Dictionary = frame.data
	var recorded_receipt: Dictionary = data.get("receipt", {})
	var physical: Dictionary = recorded_receipt.get("field", {})
	var action_event := str(recorded_receipt.get("action_event", data.get("action_event", "replay-move-%s" % frame.sequence)))
	if _played_actions.has(action_event): return
	var destination: Dictionary = data.get("position", {}).duplicate(true)
	if destination.is_empty(): return
	var visible_field: Node2D = _host.field
	var field: Node2D = visible_field
	var who := str(frame.actor)
	var body: CharacterBody2D = field.actors.get(who)
	var source: Dictionary = _session.Snapshot().duplicate(true)
	source.viewer = who
	source.presentation = {"player_actor": who}
	if is_instance_valid(body):
		source.map = str(field.adapter.map_key())
		source.room = str(field.adapter.departure_room(body.position))
	else:
		var person: Dictionary = {}
		for candidate: Dictionary in source.get("people", []):
			if str(candidate.key) == who: person = candidate
		source.map = str(physical.get("source_map", person.get("map", "")))
		source.room = str(physical.get("source_room", person.get("execution_room", person.get("room", ""))))
		if str(source.map) == "" or str(source.room) == "":
			last_note = "该主体的源位置未被记录；保留其记录终点。"
			return
	source.execution_room = source.room
	var adapted := PRESENTATION.snapshot(source)
	if not is_instance_valid(body):
		field = await _host.field_executor._hidden_field(who, adapted)
		if generation != _generation or not is_instance_valid(field): return
		body = field.actors[who]
	if is_instance_valid(field.spatial): field.spatial.begin_rollback()
	var source_key := who + ":" + str(source.map)
	if physical.has("source_position") and not _seen_bodies.has(source_key):
		body.position = GEOMETRY.point(physical.source_position)
		_seen_bodies[source_key] = true
	destination.execution_room = str(destination.get("execution_room", destination.get("room", "")))
	destination.room = destination.execution_room
	var old_speed: float = body.speed
	body.speed = maxf(old_speed, 900.0 * _speed / Engine.time_scale)
	var arrived := false
	var trajectory: Array = physical.get("trajectory", [])
	if not trajectory.is_empty():
		arrived = await _walk_recorded_trajectory(field, body, trajectory, adapted)
		if arrived and str(recorded_receipt.get("outcome", "applied")) != "applied":
			var trail: Array = []
			for sample: Dictionary in trajectory:
				var item := sample.duplicate(false)
				item.position = GEOMETRY.point(sample.position)
				trail.append(item)
			arrived = await field._walk_rollback_trail(body, trail, adapted)
			destination.map = source.map
			destination.room = source.room
			destination.execution_room = source.room
	else:
		var intent := {"actor": who, "action_event": action_event,
			"action": {"id": str(frame.action), "kind": "move"}, "destination": destination, "execution_cost": {}}
		var receipt: Dictionary = await _host.field_executor.execute(intent, visible_field, adapted, int(frame.sequence) + 1)
		if generation != _generation: return
		arrived = str(receipt.get("status", "")) == "arrived"
		await _host.field_executor.settle(action_event, "applied" if arrived else "rejected", visible_field, true)
		last_note = "旧记录只含逻辑终点；当前角色路径由正式几何重建。"
	if generation != _generation or not is_instance_valid(body): return
	body.speed = old_speed
	if is_instance_valid(field.spatial): field.spatial.finish_transaction(true)
	if not arrived:
		last_note = "记录路径受当前碰撞阻挡，原始终点保留在片段数据中。"
		return
	_played_actions[action_event] = true
	field.execution_bridge.note_map(field)
	field.execution_bridge.sync_obstacles(field)
	if who == str(_session.Snapshot().viewer):
		var current: Dictionary = _session.Snapshot()
		current.map = str(destination.map)
		current.room = str(destination.room)
		current.execution_room = str(destination.room)
		current.state_version = int(current.get("state_version", 0)) + 1
		_session.SetSnapshot(current)
		await _host.install_replay_snapshot(_session.Snapshot(), {})

func _walk_recorded_trajectory(field: Node2D, body: CharacterBody2D, trajectory: Array, state: Dictionary) -> bool:
	var generation := _generation
	var index := 0
	while index < trajectory.size():
		var current_map := str(field.adapter.map_key())
		var route := PackedVector2Array()
		while index < trajectory.size() and str(trajectory[index].map) == current_map:
			route.append(GEOMETRY.point(trajectory[index].position))
			index += 1
		if not route.is_empty():
			var result: Dictionary = await field._follow(body, route)
			if generation != _generation or not bool(result.get("ok", false)): return false
		if index == trajectory.size(): return true
		var next: Dictionary = trajectory[index]
		var crossing: Dictionary = next.get("entered_by", {})
		if str(crossing.get("from_map", "")) != current_map or str(crossing.get("to_map", "")) != str(next.map): return false
		if body.position.distance_to(GEOMETRY.point(crossing.from_position)) > 2.0: return false
		if not await field._restore_map_transform(body, str(next.map), GEOMETRY.point(crossing.to_position), state): return false
		if generation != _generation: return false
	return true

func _input_record(record: Dictionary, has_positions: bool) -> void:
	var device := str(record.get("device", ""))
	var cause := int(record.get("cause_input", record.get("seq", 0)))
	if device == "control_signal":
		if cause != 0 and _raw_inputs.has(cause): return
		var control := _host.get_node_or_null(str(record.get("path", "")))
		if control is BaseButton and not control.disabled: control.pressed.emit()
		return
	if device in ["text_control", "selection_control", "range_control"]:
		var control := _host.get_node_or_null(str(record.get("path", "")))
		if device == "text_control" and (control is LineEdit or control is TextEdit):
			control.text = str(record.get("value", ""))
			if control is LineEdit: control.text_changed.emit(control.text)
			else: control.text_changed.emit()
		elif device == "selection_control" and control is OptionButton:
			control.select(int(record.value))
			control.item_selected.emit(int(record.value))
		elif device == "range_control" and control is Range: control.value = float(record.value)
		return
	var event: InputEvent
	match device:
		"keyboard":
			var key := InputEventKey.new()
			key.keycode = int(record.get("keycode", OS.find_keycode_from_string(str(record.get("key", "")))))
			key.physical_keycode = int(record.get("physical_keycode", key.keycode))
			key.unicode = int(record.get("unicode", 0))
			key.pressed = bool(record.get("pressed", false))
			key.shift_pressed = bool(record.get("shift", false))
			key.ctrl_pressed = bool(record.get("ctrl", false))
			key.alt_pressed = bool(record.get("alt", false))
			key.meta_pressed = bool(record.get("meta", false))
			event = key
		"mouse":
			var mouse := InputEventMouseButton.new()
			mouse.position = GEOMETRY.point(record.position)
			mouse.global_position = mouse.position
			mouse.button_index = int(record.button)
			mouse.pressed = bool(record.pressed)
			mouse.double_click = bool(record.get("double_click", false))
			mouse.factor = float(record.get("factor", 1.0))
			mouse.button_mask = int(record.get("button_mask", 0))
			event = mouse
		"mouse_motion":
			var motion := InputEventMouseMotion.new()
			motion.position = GEOMETRY.point(record.position)
			motion.global_position = motion.position
			motion.relative = GEOMETRY.point(record.get("relative", [0, 0]))
			motion.button_mask = int(record.get("button_mask", 0))
			event = motion
		"joypad_button":
			var joy := InputEventJoypadButton.new()
			joy.device = int(record.get("device_id", 0))
			joy.button_index = int(record.button)
			joy.pressed = bool(record.pressed)
			joy.pressure = float(record.get("pressure", 0))
			event = joy
		"joypad_axis":
			var axis := InputEventJoypadMotion.new()
			axis.device = int(record.get("device_id", 0))
			axis.axis = int(record.axis)
			axis.axis_value = float(record.value)
			event = axis
		"action":
			var action := InputEventAction.new()
			action.action = str(record.action)
			action.pressed = bool(record.pressed)
			action.strength = float(record.get("strength", 1.0))
			event = action
	if event != null:
		_has_raw_input = true
		if cause != 0: _raw_inputs[cause] = true
		_session.ReplayInput(event, has_positions)
		_viewport.push_input(event, true)

func advance(seconds: float) -> void:
	if not _playing or _has_raw_input or _dialogue_remaining < 0.0: return
	_dialogue_remaining -= seconds * _speed
	if _dialogue_remaining <= 0.0 and is_instance_valid(_host) and is_instance_valid(_host.ui):
		if _host.ui.dialogue_ui.is_dialogue_active():
			_host.ui.dialogue_ui.advance_dialogue()
			_dialogue_remaining = 0.15
		else: _dialogue_remaining = -1.0

func _finish_previous_text() -> void:
	while is_instance_valid(_host.ui) and _host.ui.dialogue_ui.is_dialogue_active():
		_host.ui.dialogue_ui.advance_dialogue()
		await get_tree().process_frame

func _apply_process_mode() -> void:
	if not is_instance_valid(_host): return
	if _playing or _step_active: _resume_nodes()
	else: _freeze_node(_host)

func _freeze_node(node: Node) -> void:
	var id := node.get_instance_id()
	if not _process_modes.has(id): _process_modes[id] = {"node": weakref(node), "mode": node.process_mode}
	node.process_mode = Node.PROCESS_MODE_DISABLED
	for child in node.get_children(): _freeze_node(child)

func _resume_nodes() -> void:
	for entry: Dictionary in _process_modes.values():
		var node: Node = entry.node.get_ref()
		if is_instance_valid(node): node.process_mode = int(entry.mode)
	_process_modes.clear()

func _exit_tree() -> void:
	if is_instance_valid(_host): _host.begin_shutdown()

func stop_with_error(message: String) -> void:
	last_note = message
	tooltip_text = message
	_generation += 1
	_queue.clear()
	_playing = false
	_resume_nodes()
	_host.begin_shutdown()
