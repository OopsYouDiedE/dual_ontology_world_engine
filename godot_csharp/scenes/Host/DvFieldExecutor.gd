extends Node

const CAMPUS_MAP_VIEW := preload("res://scenes/Chapter2/Field/CampusMapView.tscn")
const GEOMETRY := preload("res://scenes/Host/GeometryCatalog.gd")
const WORLD_VIEW := preload("res://scenes/Shared/Field/WorldView.gd")
const ROOM_GRAPH := preload("res://scenes/Host/RoomGraphMotion.gd")
var _bodies: Dictionary = {}
var _worlds: Dictionary = {}
var _occupancy: Array[Dictionary] = []
var _parking: Node2D
var _preparing_adapters: Array[Node2D] = []
var _closing := false
var _busy := false
var _viewer := ""
var _transactions: Dictionary = {}
var _graph_transactions: Dictionary = {}
var _rollback_obstacles: Array[Dictionary] = []
var _resync_targets: Dictionary = {}
var navigation_sampling := false
var timing_sink: Callable

func configure_visible(field: Node2D, initial: Dictionary) -> void:
	field.navigation_sampling = navigation_sampling
	if field.execution_only:
		field.field_event.connect(func(event: Dictionary):
			if str(event.get("kind", "")) == "timing" and timing_sink.is_valid(): timing_sink.call(event)
		)
	if not field.execution_only:
		_viewer = str(initial.get("presentation", {}).get("player_actor", ""))
		if bool(initial.get("development_resync", false)):
			_resync_targets[_viewer] = true
			for person: Dictionary in initial.get("people", []): _resync_targets[str(person.key)] = true
	field.execution_bridge = self
	field.execution_resource = str(initial.get("resource", ""))
	field.execution_binding = initial
	field.snapshot = initial

func bind_visible(field: Node2D, initial: Dictionary) -> void:
	field.apply_snapshot(initial)
	note_map(field)

func _ensure_parking() -> void:
	if is_instance_valid(_parking): return
	var viewport := _new_viewport("RetainedBodies")
	_parking = Node2D.new()
	_parking.process_mode = Node.PROCESS_MODE_DISABLED
	_parking.hide()
	viewport.add_child(_parking)

func _new_viewport(label: String) -> SubViewport:
	var viewport := SubViewport.new()
	viewport.name = label
	viewport.size = Vector2i(32, 32)
	viewport.disable_3d = true
	viewport.world_2d = World2D.new()
	viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	add_child(viewport)
	return viewport

func _actor_binding(who: String, state: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for person in state.get("people", []):
		if str(person.get("key", "")) == who:
			result = person.duplicate(true)
			break
	# A place's navigation target can be shared by several people. Only the
	# authored initial anchor fixes a new body's position; otherwise use the
	# existing same-room, walkable and unoccupied Stand/Spawn candidates.
	result["anchor"] = str(result.get("anchor", "")) if _resync_targets.has(who) else str(result.get("initial_anchor", ""))
	var presentation: Dictionary = state.get("presentation", {})
	if str(presentation.get("player_actor", "")) == who:
		var init_a := str(state.get("anchor", "")) if _resync_targets.has(who) else str(state.get("initial_anchor", ""))
		result.merge({"map": str(state.get("map", "")), "execution_room": str(state.get("execution_room", "")),
			"spawn": str(state.get("spawn", "")), "anchor": init_a,
			"sprite": str(presentation.get("player_sprite", ""))}, true)
	return result

func _initial_point(who: String, field: Node2D, binding: Dictionary):
	var room := str(binding.get("execution_room", ""))
	if room == "" or str(binding.get("map", "")) != str(field.adapter.map_key()): return null
	var declared_anchor := str(binding.get("anchor", ""))
	if declared_anchor != "" and binding.get("motion", {}).is_empty():
		var point = field.adapter.anchor(declared_anchor)
		if point == null or field.adapter.entered_room(point) != room or not field.adapter.walkable(point): return null
		return null if _initial_occupied(who, field, point) else point
	var candidates: Array = []
	var projected = ROOM_GRAPH.approach(binding.get("motion", {}), str(binding.get("map", "")), room)
	if projected != null:
		# Select a legal foot point nearest the scheduled approach. The source
		# room remains authoritative until the original Complete is applied.
		if field.adapter.has_method("initial_room_candidates"):
			var legal: Array = field.adapter.initial_room_candidates(room)
			legal.sort_custom(func(a, b): return Vector2(a).distance_squared_to(projected) < Vector2(b).distance_squared_to(projected))
			candidates.append_array(legal)
	for placement in field.execution_binding.get("initial_placements", []):
		if str(placement.get("actor", "")) == who:
			var point = field.adapter.anchor(str(placement.get("anchor", "")))
			if point != null: candidates.append(point)
	candidates.append_array(field.adapter.stands())
	var spawn := str(binding.get("spawn", ""))
	if spawn != "":
		var point = field.adapter.anchor(spawn)
		if point != null: candidates.append(point)
	for marker: Dictionary in GEOMETRY.map_data(str(field.adapter.map_key())).get("spawns", {}).values():
		candidates.append(GEOMETRY.point(marker.point))
	for candidate in candidates:
		var point: Vector2 = candidate
		if field.adapter.entered_room(point) != room or not field.adapter.walkable(point): continue
		if not _initial_occupied(who, field, point): return point
	# Stand markers are preferred authored positions, not a population limit.
	# Only a body's first materialization may choose another legal room cell.
	if field.adapter.has_method("initial_room_candidates"):
		for point: Vector2 in field.adapter.initial_room_candidates(room):
			if field.adapter.entered_room(point) != room or not field.adapter.walkable(point): continue
			if not _initial_occupied(who, field, point): return point
	return null

func _initial_occupied(who: String, field: Node2D, point: Vector2) -> bool:
	for id in _bodies:
		var record: Dictionary = _bodies[id]
		var body: CharacterBody2D = record.body
		if id != who and is_instance_valid(body) and str(record.resource) == field.execution_resource and str(record.map) == field.adapter.map_key() and body.position.distance_to(point) < 24.0:
			return true
	for transaction: Dictionary in _transactions.values():
		if str(transaction.actor) != who and str(transaction.resource) == field.execution_resource and str(transaction.map) == field.adapter.map_key() and Vector2(transaction.position).distance_to(point) < 24.0:
			return true
	return false

func occupied_positions(walker: CharacterBody2D, resource: String, map_key: String) -> Array[Vector2]:
	var result: Array[Vector2] = []
	for record: Dictionary in _bodies.values():
		var body: CharacterBody2D = record.body
		if is_instance_valid(body) and body != walker and str(record.resource) == resource and str(record.map) == map_key:
			result.append(body.position)
	for transaction: Dictionary in _transactions.values():
		var body: CharacterBody2D = _bodies[str(transaction.actor)].body
		if body != walker and str(transaction.resource) == resource and str(transaction.map) == map_key:
			result.append(transaction.position)
	return result

func occupied_at(walker: CharacterBody2D, resource: String, map_key: String, point: Vector2) -> bool:
	# Station searches ask this for many candidates. Do not allocate a full
	# positions array for every point; inspect the same retained bodies.
	for record: Dictionary in _bodies.values():
		var body: CharacterBody2D = record.body
		if not is_instance_valid(body) or body == walker or str(record.resource) != resource or str(record.map) != map_key: continue
		var separation := (body.position - point).abs()
		if separation.x < 19.0 and separation.y < 15.0: return true
	for transaction: Dictionary in _transactions.values():
		var body: CharacterBody2D = _bodies[str(transaction.actor)].body
		if body == walker or str(transaction.resource) != resource or str(transaction.map) != map_key: continue
		var separation := (Vector2(transaction.position) - point).abs()
		if separation.x < 19.0 and separation.y < 15.0: return true
	return false

func sync_obstacles(field: Node2D) -> void:
	if _closing or not is_instance_valid(field) or not is_instance_valid(field.adapter): return
	_sync_rollback_obstacles(field)
	var root: Node2D = field.adapter.objects_root()
	var retained: Dictionary = {}
	for i in range(_occupancy.size() - 1, -1, -1):
		var item: Dictionary = _occupancy[i]
		if item.field != field: continue
		var obstacle: StaticBody2D = item.obstacle
		var record: Dictionary = _bodies.get(item.who, {})
		var body: CharacterBody2D = item.body
		if not is_instance_valid(obstacle) or obstacle.get_parent() != root or record.is_empty() or not is_instance_valid(body) or record.body != body or body.get_parent() == root or str(record.resource) != field.execution_resource or str(record.map) != field.adapter.map_key():
			_remove_occupancy(i)
			continue
		retained[item.who] = item
	for who in _bodies:
		var record: Dictionary = _bodies[who]
		var body: CharacterBody2D = record.body
		if not is_instance_valid(body) or body.get_parent() == root: continue
		if str(record.resource) != field.execution_resource or str(record.map) != field.adapter.map_key(): continue
		if retained.has(who):
			_update_occupancy(retained[who])
			continue
		var obstacle := StaticBody2D.new()
		obstacle.set_meta("retained_occupancy", body.get_instance_id())
		obstacle.collision_layer = body.collision_layer
		obstacle.collision_mask = 0
		obstacle.transform = body.transform
		var shapes: Array[Dictionary] = []
		for child in body.get_children():
			if child is CollisionShape2D:
				var shape := CollisionShape2D.new()
				shape.shape = child.shape
				shape.transform = child.transform
				shape.disabled = child.disabled
				obstacle.add_child(shape)
				shapes.append({"source": child, "proxy": shape})
		root.add_child(obstacle)
		_occupancy.append({"obstacle": obstacle, "body": body, "field": field, "who": who, "shapes": shapes})

func _sync_rollback_obstacles(field: Node2D) -> void:
	var root: Node2D = field.adapter.objects_root()
	var retained: Dictionary = {}
	for index in range(_rollback_obstacles.size() - 1, -1, -1):
		var item: Dictionary = _rollback_obstacles[index]
		if item.field != field: continue
		var obstacle: StaticBody2D = item.obstacle
		var transaction: Dictionary = _transactions.get(str(item.event), {})
		if not is_instance_valid(obstacle) or obstacle.get_parent() != root or transaction.is_empty() or str(transaction.map) != field.adapter.map_key():
			_remove_rollback_obstacle(index)
		else: retained[str(item.event)] = true
	for event_id in _transactions:
		var transaction: Dictionary = _transactions[event_id]
		if retained.has(event_id) or str(transaction.resource) != field.execution_resource or str(transaction.map) != field.adapter.map_key(): continue
		var body: CharacterBody2D = _bodies[str(transaction.actor)].body
		var obstacle := StaticBody2D.new()
		obstacle.position = transaction.position
		obstacle.collision_layer = body.collision_layer
		obstacle.collision_mask = 0
		obstacle.set_meta("rollback_anchor", event_id)
		for child in body.get_children():
			if child is CollisionShape2D:
				var shape := CollisionShape2D.new()
				shape.shape = child.shape
				shape.transform = child.transform
				shape.disabled = child.disabled
				obstacle.add_child(shape)
		root.add_child(obstacle)
		body.add_collision_exception_with(obstacle)
		_rollback_obstacles.append({"event": event_id, "field": field, "body": body, "obstacle": obstacle})

func _remove_rollback_obstacle(index: int) -> void:
	var item: Dictionary = _rollback_obstacles[index]
	var obstacle: StaticBody2D = item.obstacle
	if is_instance_valid(obstacle):
		var body: CharacterBody2D = item.body
		if is_instance_valid(body): body.remove_collision_exception_with(obstacle)
		obstacle.collision_layer = 0
		obstacle.queue_free()
	_rollback_obstacles.remove_at(index)

func _remove_occupancy(index: int) -> void:
	var obstacle: StaticBody2D = _occupancy[index].obstacle
	if is_instance_valid(obstacle):
		obstacle.collision_layer = 0
		obstacle.queue_free()
	_occupancy.remove_at(index)

func _update_occupancy(item: Dictionary) -> void:
	var obstacle: StaticBody2D = item.obstacle
	var body: CharacterBody2D = item.body
	obstacle.transform = body.transform
	obstacle.collision_layer = body.collision_layer
	for pair: Dictionary in item.shapes:
		var source: CollisionShape2D = pair.source
		var proxy: CollisionShape2D = pair.proxy
		if not is_instance_valid(proxy): continue
		if not is_instance_valid(source):
			proxy.disabled = true
			continue
		if proxy.shape != source.shape: proxy.shape = source.shape
		if proxy.transform != source.transform: proxy.transform = source.transform
		if proxy.disabled != source.disabled: proxy.disabled = source.disabled

func _physics_process(_delta: float) -> void:
	if _closing: return
	for transaction: Dictionary in _transactions.values():
		if not bool(transaction.get("rolling_back", false)): _sample_transaction(transaction)
	for i in range(_occupancy.size() - 1, -1, -1):
		var item: Dictionary = _occupancy[i]
		var obstacle = item.obstacle
		if not is_instance_valid(obstacle) or not obstacle.is_inside_tree():
			_remove_occupancy(i)
			continue
		var body = item.body
		var field = item.field
		var record: Dictionary = _bodies.get(item.who, {}) if is_instance_valid(body) else {}
		if not is_instance_valid(field) or not is_instance_valid(field.adapter) or record.is_empty() or str(record.map) != field.adapter.map_key() or str(record.resource) != field.execution_resource or body.get_parent() == obstacle.get_parent():
			_remove_occupancy(i)
			continue
		_update_occupancy(item)

func acquire_actor(who: String, sprite_key: String, field: Node2D) -> CharacterBody2D:
	if _closing: return null
	var body: CharacterBody2D
	var previous: Node2D
	if _bodies.has(who):
		var record: Dictionary = _bodies[who]
		body = record.body
		if not is_instance_valid(body) or str(record.resource) != field.execution_resource or str(record.map) != field.adapter.map_key(): return null
		previous = record.field
		if is_instance_valid(previous) and previous != field: _detach(who, previous)
		if not is_instance_valid(previous):
			var binding := _actor_binding(who, field.execution_binding)
			if not binding.get("motion", {}).is_empty():
				var projected = _initial_point(who, field, binding)
				if projected == null: return null
				body.position = projected
	else:
		var binding := _actor_binding(who, field.execution_binding)
		var point = _initial_point(who, field, binding)
		if point == null or sprite_key == "": return null
		body = field.create_body(who, sprite_key)
		if body == null: return null
		body.speed = 130.0 if who == _viewer else 110.0
		body.position = point
		field._cancel_motion(body)
		_bodies[who] = {"body": body, "field": field, "map": str(field.adapter.map_key()), "resource": field.execution_resource}
		_resync_targets.erase(who)
	if body.get_parent() != field.adapter.objects_root():
		var actual := body.position
		if body.get_parent() != null: body.get_parent().remove_child(body)
		field.adapter.objects_root().add_child(body)
		body.position = actual
	body.process_mode = Node.PROCESS_MODE_INHERIT
	field.actors[who] = body
	if who == str(field.adapter.player_key()):
		field.player = body
		field.sight.set_process(not field.execution_only)
	_bodies[who].field = field
	if is_instance_valid(previous) and previous != field: sync_obstacles(previous)
	sync_obstacles(field)
	return body

func _detach(who: String, field: Node2D) -> void:
	var body: CharacterBody2D = field.actors.get(who)
	if body == null: return
	field._daily_release(who)
	field._cancel_motion(body)
	field.actors.erase(who)
	if field.player == body:
		field.player = null
		field.sight.set_process(false)

func park_actor(who: String, field: Node2D) -> void:
	var body: CharacterBody2D = field.actors.get(who)
	if body == null: return
	_ensure_parking()
	_bodies[who] = {"body": body, "field": null, "map": str(field.adapter.map_key()), "resource": field.execution_resource}
	_detach(who, field)
	var actual := body.position
	body.get_parent().remove_child(body)
	_parking.add_child(body)
	body.position = actual

func retain_field(field: Node2D) -> void:
	if not is_instance_valid(field): return
	for who in field.actors.keys(): park_actor(str(who), field)

func note_map(field: Node2D) -> void:
	for who in field.actors:
		_bodies[who] = {"body": field.actors[who], "field": field, "map": str(field.adapter.map_key()), "resource": field.execution_resource}

func _hidden_field(who: String, state: Dictionary) -> Node2D:
	var field: Node2D = _worlds.get(who)
	var resource := str(state.get("resource", ""))
	var source_map := str(state.get("map", ""))
	if resource == "" or source_map == "": return null
	if is_instance_valid(field):
		if field.execution_resource != resource: return null
		if field.player == null:
			if field.adapter.has_method("prepare_map"):
				if not await field.adapter.prepare_map(source_map, state, get_tree()): return null
			if _closing or not is_instance_valid(field) or field._closing: return null
			if field.adapter.map_key() != source_map: field.adapter.load_map(source_map, str(state.get("spawn", "")))
			field.execution_binding = state
			var binding := _actor_binding(who, state)
			if acquire_actor(who, str(binding.get("sprite", "")), field) == null: return null
		field.apply_snapshot(state)
		return field
	var packed := CAMPUS_MAP_VIEW if resource == "res://scenes/Chapter2/Field/CampusMapView.tscn" else (load(resource) as PackedScene)
	if packed == null: return null
	var adapter := packed.instantiate() as Node2D
	if adapter == null: return null
	adapter.presentation = state.get("presentation", {})
	_preparing_adapters.append(adapter)
	var prepared := true
	if adapter.has_method("prepare_map"):
		prepared = await adapter.prepare_map(source_map, state, get_tree())
	_preparing_adapters.erase(adapter)
	if not prepared or _closing:
		if adapter.has_method("begin_shutdown"): adapter.begin_shutdown()
		adapter.free()
		return null
	var viewport := _new_viewport("ActorExecution")
	field = Node2D.new()
	field.set_script(WORLD_VIEW)
	field.execution_only = true
	configure_visible(field, state)
	field.hide()
	viewport.add_child(field)
	field.setup(adapter)
	field.apply_snapshot(state)
	_worlds[who] = field
	return field if field.player != null else null

func _blocked(intent: Dictionary, serial: int, reason: String) -> Dictionary:
	var who := str(intent.get("actor", ""))
	var raw_act = intent.get("action", {})
	var act_id := str(raw_act.get("id", "")) if raw_act is Dictionary else ""
	var result := {"kind": "daily", "who": who, "serial": serial,
		"action": act_id, "status": "blocked", "room": "", "reason": reason}
	if _bodies.has(who):
		var body: CharacterBody2D = _bodies[who].body
		if is_instance_valid(body):
			result["map"] = str(_bodies[who].map)
			result["actual_position"] = [body.position.x, body.position.y]
	return result

func execute(intent: Dictionary, visible_field: Node2D, execution_snapshot: Dictionary, serial: int) -> Dictionary:
	if _closing or _busy: return _blocked(intent, serial, "executor_unavailable")
	# Hold the same execution lock during asynchronous resource preparation.
	_busy = true
	var receipt := await _execute_pending(intent, visible_field, execution_snapshot, serial)
	_busy = false
	return receipt

func _execute_pending(intent: Dictionary, visible_field: Node2D, execution_snapshot: Dictionary, serial: int) -> Dictionary:
	var who := str(intent.get("actor", ""))
	var raw_act = intent.get("action", {})
	var action := str(raw_act.get("id", "")) if raw_act is Dictionary else ""
	var destination: Dictionary = intent.get("destination", {})
	var source_map := str(execution_snapshot.get("map", ""))
	var source_room := str(execution_snapshot.get("execution_room", ""))
	if source_map == "" or source_room == "": return _blocked(intent, serial, "missing_source_geometry_binding")
	if _is_far_movement(who, source_room, destination, visible_field):
		return _execute_room_graph(intent, execution_snapshot, serial)
	if _bodies.has(who) and (str(_bodies[who].map) != source_map or str(_bodies[who].resource) != str(execution_snapshot.get("resource", ""))):
		return _blocked(intent, serial, "retained_body_source_mismatch")
	var field: Node2D
	if is_instance_valid(visible_field) and visible_field.execution_resource == str(execution_snapshot.get("resource", "")) and visible_field.adapter.map_key() == source_map:
		var body: CharacterBody2D = visible_field.actors.get(who)
		if body != null and (body == visible_field.player or str(destination.get("map", "")) == source_map): field = visible_field
	if field == null: field = await _hidden_field(who, execution_snapshot)
	if _closing: return _blocked(intent, serial, "execution_interrupted")
	if field == null or not field.actors.has(who): return _blocked(intent, serial, "missing_actor_geometry_binding")
	var body: CharacterBody2D = field.actors[who]
	if field.adapter.departure_room(body.position) != source_room:
		return _blocked(intent, serial, "retained_body_room_mismatch")
	if field.adapter.has_method("apply_snapshot"): field.adapter.apply_snapshot(execution_snapshot)
	sync_obstacles(field)
	var source_point := body.position
	var source_geometry := "interior" if field.adapter.entered_room(source_point) == source_room else "source-side-doorway"
	var instance := body.get_instance_id()
	_transactions[str(intent.action_event)] = {"actor": who, "field": field,
		"map": source_map, "room": source_room, "position": source_point,
		"resource": field.execution_resource, "snapshot": execution_snapshot, "serial": serial,
		"trail": [{"map": source_map, "position": source_point}], "rolling_back": false}
	sync_obstacles(field)
	if is_instance_valid(visible_field) and visible_field != field: sync_obstacles(visible_field)
	await get_tree().physics_frame
	if _closing or not is_instance_valid(field) or field._closing:
		return _blocked(intent, serial, "execution_interrupted")
	var received: Dictionary = {}
	var listener := func(event: Dictionary) -> void:
		if str(event.get("kind", "")) == "daily" and int(event.get("serial", -1)) == serial and str(event.get("who", "")) == who and str(event.get("action", "")) == action:
			received.merge(event, true)
	field.field_event.connect(listener)
	field.execute_move(destination, who, serial, action)
	while received.is_empty() and not _closing and is_instance_valid(field) and not field._closing:
		await get_tree().process_frame
	if is_instance_valid(field) and field.field_event.is_connected(listener): field.field_event.disconnect(listener)
	if received.is_empty(): return _blocked(intent, serial, "execution_interrupted")
	var transaction: Dictionary = _transactions[str(intent.action_event)]
	_sample_transaction(transaction)
	var trajectory: Array = []
	for sample: Dictionary in transaction.trail:
		var point: Vector2 = sample.position
		var item := {"map": str(sample.map), "position": [point.x, point.y]}
		if sample.has("entered_by"): item.entered_by = sample.entered_by
		trajectory.append(item)
	received["trajectory"] = trajectory
	received["source_map"] = source_map
	received["source_room"] = source_room
	received["source_geometry"] = source_geometry
	received["source_position"] = [source_point.x, source_point.y]
	received["actor_instance_id"] = instance
	received["execution_world"] = "isolated-godot" if field.execution_only else "visible-godot"
	note_map(field)
	if str(received.get("status", "")) == "arrived":
		var expected_room := str(destination.get("execution_room", destination.get("room", "")))
		if str(received.get("room", "")) != expected_room or str(received.get("map", "")) != str(destination.get("map", "")):
			received.status = "blocked"
			received.reason = "arrival_identity_mismatch"
	var raw_travel = raw_act.get("travel", {}) if raw_act is Dictionary else {}
	if str(received.get("status", "")) == "arrived" and raw_travel is Dictionary and raw_travel.has("station"):
		var station := str(raw_travel.station)
		if str(received.get("station", "")) != station or not received.has("arrival_error") or float(received.arrival_error) > 4.0:
			received.status = "blocked"
			received.reason = "station_evidence_mismatch"
	return received

func _is_far_movement(who: String, source_room: String, destination: Dictionary, visible_field: Node2D) -> bool:
	if who == _viewer: return false
	if not is_instance_valid(visible_field) or not is_instance_valid(visible_field.player): return true
	var observer_room: String = visible_field.adapter.departure_room(visible_field.player.position)
	return source_room != observer_room and str(destination.get("execution_room", destination.get("room", ""))) != observer_room

func _execute_room_graph(intent: Dictionary, state: Dictionary, serial: int) -> Dictionary:
	var destination: Dictionary = intent.get("destination", {})
	var source_room := str(state.get("execution_room", ""))
	var target_room := str(destination.get("execution_room", destination.get("room", "")))
	var rooms := ROOM_GRAPH.path(source_room, target_room)
	if rooms.is_empty(): return _blocked(intent, serial, "missing_room_graph_route")
	var target_map := str(destination.get("map", ""))
	if GEOMETRY.room_data(target_map, target_room).is_empty(): return _blocked(intent, serial, "missing_room_graph_destination")
	var action: Dictionary = intent.get("action", {})
	var station := str(action.get("travel", {}).get("station", ""))
	if station != "" and GEOMETRY.anchor(target_map, station) == null and GEOMETRY.room_data(target_map, station).is_empty():
		return _blocked(intent, serial, "missing_room_graph_station")
	_graph_transactions[str(intent.action_event)] = {"actor": str(intent.actor)}
	# Prepare already selected the formal travel law. Complete still checks
	# its authority preconditions and charges the original execution cost.
	return {"kind": "daily", "who": str(intent.actor), "serial": serial,
		"action": str(action.get("id", "")), "status": "arrived",
		"map": target_map, "room": target_room, "station": station,
		"source_map": str(state.get("map", "")), "source_room": source_room,
		"execution_world": "room-graph", "room_path": rooms,
		"approximation": "scheduled-room-adjacency", "geometry_contact": false}

func settle(action_event: String, outcome: String, visible_field: Node2D, animate := true) -> bool:
	if _graph_transactions.has(action_event):
		var who := str(_graph_transactions[action_event].actor)
		if outcome == "applied" and _bodies.has(who):
			var record: Dictionary = _bodies[who]
			var previous: Node2D = record.field
			if is_instance_valid(previous): _detach(who, previous)
			if is_instance_valid(record.body): record.body.queue_free()
			_bodies.erase(who)
			if is_instance_valid(previous): sync_obstacles(previous)
		_graph_transactions.erase(action_event)
		return true
	if not _transactions.has(action_event): return true
	var transaction: Dictionary = _transactions[action_event]
	var who := str(transaction.actor)
	var field: Node2D = transaction.field
	_busy = true
	if outcome != "applied":
		_sample_transaction(transaction)
		transaction.rolling_back = true
		var restored: bool = await field.restore_movement_anchor(who, transaction, transaction.snapshot, animate)
		if not restored:
			_busy = false
			return false
		note_map(field)
		if is_instance_valid(field.spatial) and who == str(field._player_key): field.spatial.finish_transaction(false)
	else:
		if is_instance_valid(field.spatial) and who == str(field._player_key): field.spatial.finish_transaction(true)
	_transactions.erase(action_event)
	if field != visible_field and is_instance_valid(visible_field) and field.execution_resource == visible_field.execution_resource and field.adapter.map_key() == visible_field.adapter.map_key():
		acquire_actor(who, "", visible_field)
	sync_obstacles(field)
	if is_instance_valid(visible_field): sync_obstacles(visible_field)
	_busy = false
	return true

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	for adapter in _preparing_adapters:
		if is_instance_valid(adapter) and adapter.has_method("begin_shutdown"): adapter.begin_shutdown()
	for field in _worlds.values():
		if is_instance_valid(field): field.begin_shutdown()

func reset() -> void:
	begin_shutdown()
	for viewport in get_children():
		remove_child(viewport)
		viewport.queue_free()
	_bodies.clear()
	_transactions.clear()
	_graph_transactions.clear()
	_rollback_obstacles.clear()
	_resync_targets.clear()
	_worlds.clear()
	_occupancy.clear()
	_parking = null
	_busy = false
	_closing = false
	_viewer = ""

func set_navigation_sampling(active: bool) -> void:
	navigation_sampling = active
	for world: Node2D in _worlds.values(): world.navigation_sampling = active

## Keep the actual body path until Complete resolves the provisional movement.
## Sampling exists only for in-flight movement transactions, independent of UI audit.
func _sample_transaction(transaction: Dictionary) -> void:
	var field: Node2D = transaction.field
	var body: CharacterBody2D = field.actors[str(transaction.actor)]
	var map_id := str(field.adapter.map_key())
	var trail: Array = transaction.trail
	var previous: Dictionary = trail.back()
	if str(previous.map) != map_id:
		var crossing: Dictionary = field.execution_transitions.back()
		trail.append({"map": str(crossing.from_map), "position": GEOMETRY.point(crossing.from_position)})
		trail.append({"map": map_id, "position": GEOMETRY.point(crossing.to_position), "entered_by": crossing.duplicate(true)})
		previous = trail.back()
	if Vector2(previous.position).distance_to(body.position) >= 0.5:
		trail.append({"map": map_id, "position": body.position})
