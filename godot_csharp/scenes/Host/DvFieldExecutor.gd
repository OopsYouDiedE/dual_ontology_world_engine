extends Node
## One retained body per DV actor. Hidden executors use the same WorldView,
## authored maps, path follower and collision bodies as the visible field.
const WORLD_VIEW := preload("res://scenes/Shared/Field/WorldView.gd")

var _bodies: Dictionary = {}
var _worlds: Dictionary = {}
var _occupancy: Array[Dictionary] = []
var _parking: Node2D
var _closing := false
var _busy := false
var _viewer := ""

func configure_visible(field: Node2D, initial: Dictionary) -> void:
	if not field.execution_only: _viewer = str(initial.get("presentation", {}).get("player_actor", ""))
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
	result["anchor"] = str(result.get("initial_anchor", ""))
	var presentation: Dictionary = state.get("presentation", {})
	if str(presentation.get("player_actor", "")) == who:
		result.merge({"map": str(state.get("map", "")), "execution_room": str(state.get("execution_room", "")),
			"spawn": str(state.get("spawn", "")), "anchor": str(state.get("initial_anchor", "")),
			"sprite": str(presentation.get("player_sprite", ""))}, true)
	return result

func _initial_point(who: String, field: Node2D, binding: Dictionary):
	var room := str(binding.get("execution_room", ""))
	if room == "" or str(binding.get("map", "")) != str(field.adapter.map_key()): return null
	var declared_anchor := str(binding.get("anchor", ""))
	if declared_anchor != "":
		var point = field.adapter.anchor(declared_anchor)
		if point == null or field.adapter.entered_room(point) != room or not field.adapter.walkable(point): return null
		return null if _initial_occupied(who, field, point) else point
	var candidates: Array = []
	for placement in field.execution_binding.get("initial_placements", []):
		if str(placement.get("actor", "")) == who:
			var point = field.adapter.anchor(str(placement.get("anchor", "")))
			if point != null: candidates.append(point)
	var spawn := str(binding.get("spawn", ""))
	if spawn != "":
		var point = field.adapter.anchor(spawn)
		if point != null: candidates.append(point)
	candidates.append_array(field.adapter.stands())
	# Existing authored spawn markers can initialize a room with no stand markers.
	var layout: Node = field.adapter.get("map")
	var spawns: Node = layout.get_node_or_null("Spawns") if is_instance_valid(layout) else null
	if spawns != null:
		for marker in spawns.get_children():
			if marker is Marker2D: candidates.append(marker.position)
	for candidate in candidates:
		var point: Vector2 = candidate
		if field.adapter.entered_room(point) != room or not field.adapter.walkable(point): continue
		if not _initial_occupied(who, field, point): return point
	return null

func _initial_occupied(who: String, field: Node2D, point: Vector2) -> bool:
	for id in _bodies:
		var record: Dictionary = _bodies[id]
		var body: CharacterBody2D = record.body
		if id != who and is_instance_valid(body) and str(record.resource) == field.execution_resource and str(record.map) == field.adapter.map_key() and body.position.distance_to(point) < 24.0:
			return true
	return false

## All maps share retained coordinates even when their bodies are in another viewport.
func occupied_positions(walker: CharacterBody2D, resource: String, map_key: String) -> Array[Vector2]:
	var result: Array[Vector2] = []
	for record: Dictionary in _bodies.values():
		var body: CharacterBody2D = record.body
		if is_instance_valid(body) and body != walker and str(record.resource) == resource and str(record.map) == map_key:
			result.append(body.position)
	return result

func occupied_at(walker: CharacterBody2D, resource: String, map_key: String, point: Vector2) -> bool:
	for position: Vector2 in occupied_positions(walker, resource, map_key):
		var separation := (position - point).abs()
		if separation.x < 19.0 and separation.y < 15.0: return true
	return false

## Occupancy shapes represent bodies retained in other worlds.
## They have no actor, presentation, movement or DV state. Formal execution is serial.
func sync_obstacles(field: Node2D) -> void:
	var root: Node2D = field.adapter.objects_root()
	for node in root.get_children():
		if node.has_meta("retained_occupancy"):
			(node as StaticBody2D).collision_layer = 0
			root.remove_child(node)
			node.queue_free()
	for who in _bodies:
		var record: Dictionary = _bodies[who]
		var body: CharacterBody2D = record.body
		if not is_instance_valid(body) or body.get_parent() == root: continue
		if str(record.resource) != field.execution_resource or str(record.map) != field.adapter.map_key(): continue
		var obstacle := StaticBody2D.new()
		obstacle.set_meta("retained_occupancy", body.get_instance_id())
		obstacle.collision_layer = body.collision_layer
		obstacle.collision_mask = 0
		obstacle.position = body.position
		for child in body.get_children():
			if child is CollisionShape2D and not child.disabled:
				var shape := CollisionShape2D.new()
				shape.shape = child.shape
				shape.transform = child.transform
				obstacle.add_child(shape)
		root.add_child(obstacle)
		_occupancy.append({"obstacle": obstacle, "body": body, "field": field, "who": who})

func _physics_process(_delta: float) -> void:
	if _closing: return
	for i in range(_occupancy.size() - 1, -1, -1):
		var item: Dictionary = _occupancy[i]
		# Keep stale references as Variants until validity is checked: a typed
		# assignment itself throws after queue_free, before is_instance_valid.
		var obstacle = item.obstacle
		if not is_instance_valid(obstacle) or not obstacle.is_inside_tree():
			_occupancy.remove_at(i)
			continue
		var body = item.body
		var field = item.field
		var record: Dictionary = _bodies.get(item.who, {}) if is_instance_valid(body) else {}
		if not is_instance_valid(field) or record.is_empty() or str(record.map) != field.adapter.map_key() or str(record.resource) != field.execution_resource or body.get_parent() == obstacle.get_parent():
			obstacle.collision_layer = 0
			obstacle.queue_free()
			_occupancy.remove_at(i)
			continue
		# Local player walking need not wait for a DV action or another snapshot.
		obstacle.position = body.position

## A snapshot authorizes first materialization, never relocation of an existing body.
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
	else:
		var binding := _actor_binding(who, field.execution_binding)
		var point = _initial_point(who, field, binding)
		if point == null or sprite_key == "": return null
		body = field.create_body(who, sprite_key)
		if body == null: return null
		body.speed = 130.0 if who == _viewer else 110.0
		body.position = point
		_bodies[who] = {"body": body, "field": field, "map": str(field.adapter.map_key()), "resource": field.execution_resource}
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
			if field.adapter.map_key() != source_map: field.adapter.load_map(source_map, str(state.get("spawn", "")))
			field.execution_binding = state
			var binding := _actor_binding(who, state)
			if acquire_actor(who, str(binding.get("sprite", "")), field) == null: return null
		field.apply_snapshot(state)
		return field
	var packed := load(resource) as PackedScene
	if packed == null: return null
	var viewport := _new_viewport("ActorExecution")
	field = Node2D.new()
	field.set_script(WORLD_VIEW)
	field.execution_only = true
	configure_visible(field, state)
	field.hide()
	viewport.add_child(field)
	var adapter := packed.instantiate() as Node2D
	if adapter == null:
		viewport.queue_free()
		return null
	adapter.presentation = state.get("presentation", {})
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
	var who := str(intent.get("actor", ""))
	var raw_act = intent.get("action", {})
	var action := str(raw_act.get("id", "")) if raw_act is Dictionary else ""
	var destination: Dictionary = intent.get("destination", {})
	var source_map := str(execution_snapshot.get("map", ""))
	var source_room := str(execution_snapshot.get("execution_room", ""))
	if source_map == "" or source_room == "": return _blocked(intent, serial, "missing_source_geometry_binding")
	if _bodies.has(who) and (str(_bodies[who].map) != source_map or str(_bodies[who].resource) != str(execution_snapshot.get("resource", ""))):
		return _blocked(intent, serial, "retained_body_source_mismatch")
	var field: Node2D
	if is_instance_valid(visible_field) and visible_field.execution_resource == str(execution_snapshot.get("resource", "")) and visible_field.adapter.map_key() == source_map:
		var body: CharacterBody2D = visible_field.actors.get(who)
		if body != null and (body == visible_field.player or str(destination.get("map", "")) == source_map): field = visible_field
	if field == null: field = _hidden_field(who, execution_snapshot)
	if field == null or not field.actors.has(who): return _blocked(intent, serial, "missing_actor_geometry_binding")
	var body: CharacterBody2D = field.actors[who]
	if field.adapter.departure_room(body.position) != source_room:
		return _blocked(intent, serial, "retained_body_room_mismatch")
	# Access geometry is world data; private actor text is never applied to visible UI.
	if field.adapter.has_method("apply_snapshot"): field.adapter.apply_snapshot(execution_snapshot)
	sync_obstacles(field)
	var source_point := body.position
	var source_geometry := "interior" if field.adapter.entered_room(source_point) == source_room else "source-side-doorway"
	var instance := body.get_instance_id()
	_busy = true
	# Newly attached collision bodies and deferred access barriers must enter physics
	# before the existing follower starts, in visible and isolated worlds alike.
	await get_tree().physics_frame
	if _closing or not is_instance_valid(field) or field._closing:
		_busy = false
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
	_busy = false
	if received.is_empty(): return _blocked(intent, serial, "execution_interrupted")
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
	# If the actor now belongs on the visible map, hand back the same body and endpoint.
	if field != visible_field and is_instance_valid(visible_field) and field.execution_resource == visible_field.execution_resource and field.adapter.map_key() == visible_field.adapter.map_key():
		acquire_actor(who, "", visible_field)
	return received

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	for field in _worlds.values():
		if is_instance_valid(field): field.begin_shutdown()

## A new formal session owns a fresh registry; retained bodies never cross sessions.
func reset() -> void:
	begin_shutdown()
	for viewport in get_children():
		remove_child(viewport)
		viewport.queue_free()
	_bodies.clear()
	_worlds.clear()
	_occupancy.clear()
	_parking = null
	_busy = false
	_closing = false
	_viewer = ""
