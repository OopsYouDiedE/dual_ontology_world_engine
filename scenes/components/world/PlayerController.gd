extends Node
const MAX_ROUTE_STEPS_PER_TICK := 32
var field: Node2D
var options: Array = []
var submitted := false
var _route := PackedVector2Array()
var _plan_generation := 0
var navigation_diagnostic: Dictionary = {}
var _site := ""
var _sequence := 0
var _walking := false
var _walk_id := ""
var _progress := Vector2.ZERO
var _stuck := 0.0
var _pending_door: Dictionary = {}
var _interception_pending := false
var _crossing_sequence := -1
var last_crossing: Dictionary = {}

func begin_rollback() -> void:
	submitted = true
	_stop_walk("interaction-rollback")

func finish_transaction(applied: bool) -> void:
	submitted = false
	if not applied: _stop_walk("interaction-failed")

func stop_for_shutdown() -> void:
	_stop_walk("host_shutdown")

func _control_room() -> String:
	return field.adapter.room_of(field.player.position)

func _option_for_room(room: String) -> Dictionary:
	var found: Dictionary = {}
	for option in options:
		if not bool(option.get("available", true)): continue
		var binding: Dictionary = option.get("interaction", {})
		var destination: Dictionary = binding.get("destination", {})
		if str(binding.get("kind", "")) == "doorway" and str(destination.get("map", "")) == field.adapter.map_key() and str(destination.get("execution_room", destination.get("room", ""))) == room:
			if not found.is_empty(): return {}
			found = option
	return found

func set_options(value: Array) -> void:
	options = value
	submitted = false

func enabled() -> bool:
	return is_instance_valid(field) and field.field_active() and bool(field.snapshot.get("player_control", false)) and not submitted and not field._daily.has(field.player.name)

func release_submission() -> void:
	submitted = false

func can_submit() -> bool:
	return enabled() and field.formal_selection_ready.call()

func _can_select(option: Dictionary) -> bool:
	if not field.formal_selection_ready.call(): return false
	var choice_id := str(option.get("id", ""))
	if choice_id == "": return false
	for current in field.snapshot.get("player_options", []):
		if str(current.get("id", "")) == choice_id: return true
	return false

func _stamp(stage: String, id: String, data: Dictionary = {}) -> void:
	field.field_event.emit({"kind": "timing", "stage": stage, "interaction_id": id, "details": data})

func _id() -> String:
	_sequence += 1
	return "spatial-%d-%d" % [Time.get_ticks_usec(), _sequence]

func _stop_walk(reason: String) -> void:
	_plan_generation += 1
	_route.clear()
	_site = ""
	_pending_door = {}
	if _walking:
		_stamp("local_motion_end", _walk_id, {"reason": reason, "game_time": field.snapshot.get("game_time", 0), "position": [field.player.position.x, field.player.position.y]})
	_walking = false
	if is_instance_valid(field.player) and not field.player.travelling:
		if is_instance_valid(field.player.physical_body): field.player.physical_body.set_movement_speed(0.0)
		field.player.pose("idle")

func _begin_walk() -> void:
	if _walking: return
	if field.player.seated: field.player.stand_up()
	_walking = true
	_progress = field.player.position
	_stuck = 0.0
	_walk_id = _id()
	_stamp("local_motion_begin", _walk_id, {"game_time": field.snapshot.get("game_time", 0), "position": [field.player.position.x, field.player.position.y]})

func _physics_process(delta: float) -> void:
	if not enabled() or not bool(field.snapshot.get("locomotion_enabled", true)):
		if _walking: _stop_walk("input-blocked")
		return
	var direction: Vector2 = field.input_direction.call() if field.input_direction.is_valid() else Input.get_vector("move_left", "move_right", "move_up", "move_down")
	direction = field.stage3d.screen_direction(direction, field.player.position)
	if direction != Vector2.ZERO:
		_plan_generation += 1 # Held movement supersedes a pending click plan.
		_route.clear()
		_site = ""
		_pending_door = {}
		_begin_walk()
		_step(direction * field.player.speed * delta, delta)
	elif not _route.is_empty():
		_begin_walk()
		var remaining: float = field.player.speed * delta
		for _segment in MAX_ROUTE_STEPS_PER_TICK:
			var difference: Vector2 = _route[0] - field.player.position
			if difference.length() > 1.0:
				if remaining <= 0.0: break
				var motion := difference.limit_length(remaining)
				_step(motion, delta)
				# A doorway can submit a formal action and clear the local route.
				if _route.is_empty() or not enabled(): return
				remaining = maxf(0.0, remaining - motion.length())
				if field.player.position.distance_to(_route[0]) > 1.0: break
			_route.remove_at(0)
			if _route.is_empty():
				var site := _site
				var door := _pending_door
				_stop_walk("arrived")
				if site != "": _interact(site)
				elif not door.is_empty(): _submit(door, "", "doorway-click")
				return
		if field.player.position.distance_to(_progress) >= 0.5:
			_progress = field.player.position
			_stuck = 0.0
		else:
			_stuck += delta
			if _stuck >= 0.5: _stop_walk("blocked")
	elif _walking:
		_stop_walk("released")

func _step(motion: Vector2, delta: float) -> void:
	var body: CharacterBody3D = field.player.physical_body
	if not is_instance_valid(body): return
	var before := body.global_position
	var from_room := _control_room()
	_move_body(motion, delta)
	var after := body.global_position
	if after.distance_squared_to(before) < 0.00000001:
		body.set_movement_speed(0.0)
		return
	var to_room := _control_room()
	if to_room == from_room or to_room.is_empty(): return
	if _crossing_sequence < 0: _crossing_sequence = int(field.snapshot.get("locomotion_sequence", 0))
	_crossing_sequence += 1
	last_crossing = {"kind":"locomotion", "actor":str(field._player_key), "map":field.adapter.map_key(),
		"from_room":from_room, "to_room":to_room, "sequence":_crossing_sequence,
		"from_position":[before.x,before.y,before.z], "to_position":[after.x,after.y,after.z],
		"basis":"godot-3d-physics-displacement", "content_hash":field.snapshot.get("content_hash", "")}
	field.field_event.emit(last_crossing.duplicate(true))

func _move_body(motion: Vector2, delta: float) -> void:
	if motion == Vector2.ZERO: return
	field.player.velocity = motion / maxf(delta, 0.000001)
	field.player.advance_world()
	field.player.velocity = Vector2.ZERO

func _unhandled_input(event: InputEvent) -> void:
	if not enabled() or event.is_echo(): return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		var point: Vector2 = field.stage3d.pointer(event.position)
		navigation_diagnostic = {"stage":"picked", "screen":[event.position.x,event.position.y], "point":[point.x,point.y]}
		var hit: Dictionary = field.pointer_hit(point)
		var site := str(hit.site)
		if site != "": _approach(site)
		else: _navigate(point)
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("interact") or event.is_action_pressed("dialogue_continue"):
		var site := str(field.nearest)
		if site != "": _interact(site)
		else:
			var door := _near_door()
			if not door.is_empty():
				# The interact key may use the ordinary formal movement route;
				# only held movement or a spatial destination keeps local control.
				_submit(door, "", "doorway-interact")
			elif can_submit():
				request_self_actions()
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("wait"):
		request_wait()
		get_viewport().set_input_as_handled()

func _navigate(point: Vector2) -> void:
	navigation_diagnostic = {"point":[point.x,point.y], "stage":"request"}
	_stop_walk("new-target")
	var door := _door_option(point)
	var destination_room: String = field.adapter.room_of(point)
	var own_room := _control_room()
	if not door.is_empty() and field.adapter.has_method("doorway_landing"):
		var destination: Dictionary = door.get("interaction", {}).get("destination", {})
		destination_room = str(destination.get("execution_room", destination.get("room", "")))
		var boundary: Dictionary = field.adapter.passage_at(field.player.position, point, own_room, destination_room, field.player) if field.adapter.has_method("passage_at") else {}
		var landing: Variant = boundary.get("landing") if not boundary.is_empty() else field.adapter.doorway_landing(point, destination_room, field.player)
		if landing == null: return
		point = landing as Vector2
	navigation_diagnostic["stage"] = "visibility"
	navigation_diagnostic["room"] = destination_room
	if not field.in_sight(point) and door.is_empty():
		var doorway_visible := false
		for direction in field.APPROACH:
			if field.in_sight(point + direction.normalized() * 32.0): doorway_visible = true
		var admitted := false
		for option in options:
			var binding: Dictionary = option.get("interaction", {})
			if str(binding.get("kind", "")) == "doorway" and str(binding.get("destination", {}).get("room", "")) == destination_room: admitted = true
		if not doorway_visible or (not admitted and door.is_empty()): return
	navigation_diagnostic["stage"] = "walkability"
	if not field.adapter.walkable(point, field.player): return
	navigation_diagnostic["stage"] = "planning"
	var generation := _plan_generation
	var planned: PackedVector2Array = await field.navigation_route(field.player.position, point, _local_grid(destination_room if destination_room != own_room else ""), field.player)
	if field._closing or generation != _plan_generation: return
	_route = planned
	navigation_diagnostic["stage"] = "no_route" if planned.is_empty() else "walking"
	if not _route.is_empty():
		_route.append(point)

func _door_option(point: Vector2) -> Dictionary:
	if not field.adapter.has_method("doorway_rooms"): return {}
	var rooms: Array[String] = field.adapter.doorway_rooms(point)
	var own_room := _control_room()
	var found: Dictionary = {}
	for option in options:
		if not bool(option.get("available", true)): continue
		var binding: Dictionary = option.get("interaction", {})
		var target: Dictionary = binding.get("destination", {})
		var room := str(target.get("execution_room", target.get("room", "")))
		if str(binding.get("kind", "")) != "doorway" or str(target.get("map", "")) != field.adapter.map_key() or room == own_room: continue
		var open_passage: Dictionary = field.adapter.passage_at(field.player.position, point, own_room, room, field.player) if field.adapter.has_method("passage_at") else {}
		if (own_room in rooms and room in rooms) or not open_passage.is_empty():
			if not found.is_empty(): return {}
			found = option
	return found

func _near_door() -> Dictionary:
	var candidates: Dictionary = {}
	var own_room := str(field.snapshot.get("execution_room", ""))
	if field.adapter.has_method("doorway_rooms"):
		for offset in [Vector2.ZERO, Vector2.UP * 24, Vector2.DOWN * 24, Vector2.LEFT * 24, Vector2.RIGHT * 24]:
			var point: Vector2 = field.player.position + offset
			if not field.adapter.walkable(point, field.player): continue
			var rooms: Array[String] = field.adapter.doorway_rooms(point)
			for option: Dictionary in options:
				if not bool(option.get("available", true)): continue
				var binding: Dictionary = option.get("interaction", {})
				var target: Dictionary = binding.get("destination", {})
				var room := str(target.get("execution_room", target.get("room", "")))
				if str(binding.get("kind", "")) != "doorway" or str(target.get("map", "")) != field.adapter.map_key() or room == own_room: continue
				var open_passage: Dictionary = field.adapter.passage_at(field.player.position, point, own_room, room, field.player) if field.adapter.has_method("passage_at") else {}
				if (own_room in rooms and room in rooms) or not open_passage.is_empty():
					candidates[str(option.id)] = option
	if field.adapter.has_method("manual_exit_targets_near"):
		var targets: Array[String] = field.adapter.manual_exit_targets_near(field.player.position, 24.0)
		for option: Dictionary in options:
			if not bool(option.get("available", true)): continue
			var binding: Dictionary = option.get("interaction", {})
			var destination: Dictionary = binding.get("destination", {})
			if str(binding.get("kind", "")) == "doorway" and str(destination.get("map", "")) in targets:
				candidates[str(option.id)] = option
	# One currently admitted action across every nearby doorway, or no guess.
	return candidates.values()[0] if candidates.size() == 1 else {}

func _local_grid(destination_room := "") -> AStarGrid2D:
	# This room mask belongs to this request. Never write it into the map's
	# shared grid when there happen to be no other bodies in the room.
	var room := _control_room()
	return field.adapter.local_nav_grid(field.player, room, destination_room)

func _approach(site: String) -> void:
	site = _normalize_site_id(site)
	if not field.site_visible(site): return
	if _in_reach(site):
		_interact(site)
		return
	_stop_walk("new-target")
	var generation := _plan_generation
	var center: Vector2 = field.site_point(site)
	var station := {"point": center, "radius": float(field.sites[site].reach) - 4.0}
	var grid: AStarGrid2D = _local_grid()
	var best := INF
	var chosen := PackedVector2Array()
	for point in field._station_approaches(station, grid):
		if field.adapter.entered_room(point) != str(field.snapshot.get("execution_room", "")) or not field.adapter.walkable(point, field.player): continue
		if field.position_occupied(field.player, point): continue
		var route: PackedVector2Array = await field.navigation_route(field.player.position, point, grid, field.player)
		if field._closing or generation != _plan_generation: return
		if route.is_empty(): continue
		var distance: float = field._route_cost(field.player.position, route, point)
		if distance < best:
			best = distance
			chosen = route
			chosen.append(point)
	_stop_walk("new-target")
	_route = chosen
	_site = site

func _in_reach(site: String) -> bool:
	var normalized_site := _normalize_site_id(site)
	return field.sites.has(normalized_site) and field.site_visible(normalized_site) and field.player.position.distance_to(field.site_point(normalized_site)) <= float(field.sites[normalized_site].reach)

func _normalize_site_id(site: String) -> String:
	var normalized := site
	var aliases: Dictionary = field.snapshot.get("site_aliases", {})
	if aliases.has(normalized): normalized = str(aliases[normalized])
	if field.sites.has(normalized): return normalized
	if normalized.begins_with("site:"):
		var alias := normalized.trim_prefix("site:")
		if aliases.has(alias): alias = str(aliases[alias])
		if field.sites.has(alias): return alias
	return normalized

func choices_for(site: String) -> Array:
	var result: Array = []
	var normalized_site := _normalize_site_id(site)
	for option in options:
		var binding: Dictionary = option.get("interaction", {})
		var bound_site := _normalize_site_id(str(binding.get("site", "")))
		if (str(binding.get("kind", "")) == "site" and bound_site == normalized_site) or (str(binding.get("kind", "")) == "person" and normalized_site == "person:" + str(binding.get("person", ""))): result.append(option)
	return result

func _interact(site: String) -> void:
	if _interception_pending: return
	site = _normalize_site_id(site)
	if not can_submit() or not _in_reach(site): return
	_stop_walk("interaction")
	if field.sites[site].has("person") and field.execution_bridge != null:
		_interception_pending = true
		var interruption: Dictionary = await field.execution_bridge.intercept_nearby_movement(str(field.sites[site].person), str(field._player_key), field, float(field.sites[site].reach))
		_interception_pending = false
		if bool(interruption.attempted):
			if not bool(interruption.accepted): return
			# Re-read the ordinary choices after Core releases the movement lock.
			while not field._closing and not can_submit(): await field.get_tree().process_frame
			if field._closing or not _in_reach(site): return
	var choices: Array = []
	for option: Dictionary in choices_for(site):
		if bool(option.get("available", true)) and _can_select(option): choices.append(option)
	if choices.is_empty():
		field.field_event.emit({"kind": "interaction_hint", "text": tr("此处暂时没有可进行的调查或交谈。")})
	elif choices.size() == 1: _submit(choices[0], site, "hotspot")
	elif choices.size() > 1:
		var id := _id()
		_stamp("input_request", id, {"site": site, "request": "local-choices"})
		field.field_event.emit({"kind": "spatial_choices", "interaction_id": id, "site": site, "title": field.sites[site].title, "options": choices, "screen_position": field.get_global_transform_with_canvas() * field.site_point(site)})

func request_self_actions() -> void:
	if not can_submit(): return
	var choices: Array = []
	for option in options:
		var binding: Dictionary = option.get("interaction", {})
		if str(binding.get("kind", "")) == "self": choices.append(option)
	if choices.is_empty():
		field.field_event.emit({"kind": "interaction_hint", "text": tr("此处暂时没有可执行的交互。")})
		return
	var id := _id()
	_stamp("input_request", id, {"site": "self", "request": "local-choices"})
	field.field_event.emit({"kind": "spatial_choices", "interaction_id": id, "site": "self", "target_id": "self", "title": tr("自身操作"), "options": choices, "screen_position": field.get_global_transform_with_canvas() * field.player.position})

func request_wait() -> void:
	if not can_submit(): return
	var choices: Array = []
	for option in options:
		var binding: Dictionary = option.get("interaction", {})
		if str(binding.get("kind", "")) == "self" and str(binding.get("purpose", "")) == "wait": choices.append(option)
	if choices.is_empty():
		field.field_event.emit({"kind": "interaction_hint", "text": tr("此刻没有已知的等待安排，可以先调查身边的物件或与人交谈。")})
		return
	var id := _id()
	_stamp("input_request", id, {"site": "self", "request": "local-choices"})
	field.field_event.emit({"kind": "spatial_choices", "interaction_id": id, "site": "self", "target_id": "self", "title": tr("在此等候"), "options": choices, "screen_position": field.get_global_transform_with_canvas() * field.player.position})

func select(id: String, site: String) -> void:
	site = _normalize_site_id(site)
	if submitted or (site != "self" and not _in_reach(site)): return
	var action_id := id
	if id.begins_with("{"):
		var envelope: Variant = JSON.parse_string(id)
		if envelope is not Dictionary or not envelope.get("action_id") is String or not envelope.get("hypothesis_id") is String: return
		action_id = str(envelope.action_id)
	for option in options:
		if str(option.id) == action_id and ((site == "self" and str(option.get("interaction", {}).get("kind", "")) == "self") or option in choices_for(site)):
			_submit(option, site, "local-choice", id)
			return

func _submit(option: Dictionary, site: String, source: String, selection := "") -> void:
	if not can_submit() or submitted or not _can_select(option): return
	var id := _id()
	var payload := str(option.id) if selection.is_empty() else selection
	_stamp("input_request", id, {"choice_id": payload, "action_id": option.id, "site": site, "source": source, "game_time": field.snapshot.get("game_time", 0)})
	submitted = true
	field.field_event.emit({"kind": "select", "choice_id": payload, "site": site, "interaction_id": id, "context": field.context()})

