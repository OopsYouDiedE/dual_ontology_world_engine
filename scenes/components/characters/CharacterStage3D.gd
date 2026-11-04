extends Node3D
const QUERY_BUDGET = preload("res://scenes/components/characters/PhysicalQueryBudget.gd")
var _navigation_requests: Dictionary = {}
const PhysicsLayers = preload("res://scenes/components/world/PhysicsLayers.gd")
const GEOMETRY = preload("res://scenes/components/maps/GeometryCatalog.gd")
const BODY = preload("res://scenes/components/characters/StoryBody3D.gd")
const DV_MAP = preload("res://scenes/components/world/LegacyDvMap.gd")
var field: Node2D
var camera: Camera3D
var definitions: Dictionary
var cast: Dictionary
var map_port: Node3D
var legacy_grids: Dictionary = {}
var _legacy_grid_keys: Dictionary = {}
var _physical_grid_cache: Dictionary = {}
var _portal_pairs: Dictionary = {}
static var navigation_grid_builds := 0
static var navigation_grid_cache_hits := 0
var projection_frame: Dictionary = {}
var bodies: Dictionary = {}
var logical_bodies: Dictionary = {}
var current_room := ""
var spatial_participants: Node3D
var spatial_execution_profile: Dictionary = {}
var occlusion: Node
var route_failures: Dictionary = {}
var route_failure_details: Dictionary = {}
var occupancy: Dictionary = {}

var room_loader: Callable
var observer_changed: Callable

static var _prepared_cast := ""
static var _cast_resources: Dictionary = {}

static func prepare_cast(tree: SceneTree, parent: Node, cancelled: Callable) -> bool:
	var reader = preload("res://scenes/main/DvPresentationContent.cs").new()
	var art: Dictionary = reader.ReadRecord("res://assets/data/character_art.dv", "layouts", "character_art")
	var signature := str([art.get("models", {}), art.get("conversation_motion", {})])
	if _prepared_cast == signature: return true
	var spatial: Dictionary = reader.ReadPublishedData("95-configurable-building.data.dv", "space.configurable_building").value
	var height := float(spatial.host_actor_profile.nominal_actor_height_m)
	var paths: Array[String] = []
	for model: Dictionary in art.models.values():
		var path := str(model.get("path", ""))
		if not path.is_empty() and path not in paths: paths.append(path)
	var resources: Dictionary = await preload("res://scenes/components/world/FieldResources.gd").wait_for(paths, tree, cancelled)
	if resources.is_empty() or cancelled.call(): return false
	_cast_resources = resources
	for model: Dictionary in art.models.values():
		if cancelled.call(): return false
		var holder := Node3D.new()
		holder.visible = false
		parent.add_child(holder)
		var attached: bool = preload("res://scenes/components/characters/ImportedCharacter.gd").attach(holder, str(model.path), height, float(model.get("yaw_degrees", 0.0)), str(model.get("character_id", "")))
		if attached and holder.has_meta("animation_player"):
			var player = holder.get_meta("animation_player")
			if player is AnimationPlayer: preload("res://scenes/components/characters/ConversationAnimationLibrary.gd").install(player)
		if DisplayServer.get_name() != "headless": await RenderingServer.frame_post_draw
		holder.queue_free()
		if not attached: return false
		await tree.process_frame
	_prepared_cast = signature
	return true

func apply_world_projection(frame: Dictionary) -> void:
	projection_frame = frame

func apply_visibility_projection(frame: Dictionary) -> void:
	assert(projection_frame == frame, "Character projection frame mismatch")

func toggle_spatial_debug(enabled: bool, show_colliders: bool = true, show_slots: bool = true, show_ids: bool = true) -> Dictionary:
	var DebugOverlay = preload("res://scenes/components/world/SpatialDebugOverlay.gd")
	return DebugOverlay.toggle(self, enabled, show_colliders, show_slots, show_ids)


func room_at(point: Vector2) -> String:
	return map_port.logical_room_at(point, str(field.adapter.map_key()))

func to_world(point: Vector2, identity: String) -> Vector3:
	if ensure_room(identity).is_empty(): return Vector3.INF
	return map_port.logical_to_world(point, identity)

func to_logical(point: Vector3, identity: String) -> Vector2:
	return map_port.world_to_logical(point, identity)

func logical_heading(point: Vector2, direction: Vector2) -> Vector3:
	var identity := room_at(point)
	if identity.is_empty(): return Vector3.ZERO
	var heading := to_world(point + direction, identity) - to_world(point, identity)
	heading.y = 0.0
	return heading.normalized()

func screen_direction(direction: Vector2, point: Vector2) -> Vector2:
	if direction == Vector2.ZERO or not is_instance_valid(camera): return direction
	var identity := room_at(point)
	if identity.is_empty(): return Vector2.ZERO
	var right := camera.global_basis.x
	var down := camera.global_basis.z
	right.y = 0.0
	down.y = 0.0
	var world_heading := right.normalized() * direction.x + down.normalized() * direction.y
	var origin := to_world(point, identity)
	return (to_logical(origin + world_heading, identity) - point).normalized() * direction.length()

func get_spatial_participants() -> Node:
	var keys := ["speed_mps", "arrival_tolerance_m", "foot_offset_m", "stall_timeout_s", "minimum_progress_m", "gravity_mps2", "turn_speed_degrees_s", "turn_tolerance_degrees"]
	for key in keys:
		if not spatial_execution_profile.has(key): return null
	if not is_instance_valid(spatial_participants):
		spatial_participants = preload("res://scenes/components/characters/SpatialParticipants3D.gd").new()
		add_child(spatial_participants)
		spatial_participants.motion_sampled.connect(_native_spatial_motion)
	var actors: Array = field.actors.values()
	if is_instance_valid(field.player) and not actors.has(field.player): actors.append(field.player)
	for actor in actors:
		var id := str(actor.get_meta("actor_id", actor.name))
		var body := prepare_actor(actor)
		if body == null: continue
		if spatial_participants.resolve_spatial_actor(id) == body: continue
		if not spatial_participants.register_spatial_actor(id, body, body.visual, body.get_spatial_contact_slots(),
			float(spatial_execution_profile.speed_mps), float(spatial_execution_profile.arrival_tolerance_m),
			float(spatial_execution_profile.foot_offset_m), float(spatial_execution_profile.stall_timeout_s),
			float(spatial_execution_profile.minimum_progress_m), float(spatial_execution_profile.gravity_mps2),
			float(spatial_execution_profile.turn_speed_degrees_s), float(spatial_execution_profile.turn_tolerance_degrees)):
			return null
	if not map_port.register_spatial_objects(spatial_participants): return null
	return spatial_participants

func _native_spatial_motion(actor_id: String, before: Vector3, after: Vector3) -> void:
	var body = spatial_participants.resolve_spatial_actor(actor_id)
	if not is_instance_valid(body): return
	var reference = body.get_meta("logical_actor", null)
	var actor = reference.get_ref() if reference is WeakRef else null
	if not is_instance_valid(actor): return
	var identity := str(body.get_meta("room", ""))
	if not map_port.has_room(identity): return
	actor.position = to_logical(after, identity)
	actor.measured_motion_m += before.distance_to(after)

func prepare_actor(actor: CharacterBody2D) -> CharacterBody3D:
	var identity := room_at(actor.position)
	if identity == "": return null
	var record := ensure_room(identity)
	if record.is_empty(): return null
	var key := actor.get_instance_id()
	var body: CharacterBody3D = bodies.get(key)
	if body == null:
		if is_instance_valid(actor.physical_body):
			body = actor.physical_body
			body.set_meta("logical_actor", weakref(actor))
			body.global_position = to_world(actor.position, identity)
			body.set_meta("room", identity)
			body.collision_layer = PhysicsLayers.ACTOR
			body.collision_mask = PhysicsLayers.ACTOR_MOVEMENT_MASK
			bodies[key] = body
			logical_bodies[key] = weakref(actor)
			return body
		var model: Dictionary = cast.get(str(actor.get_meta("actor_id", actor.name)), {})
		if model.is_empty(): return null
		body = BODY.new()
		body.name = str(actor.name)
		actor.add_child(body)
		if not body.configure(model, float(definitions.nominal_actor_height), float(definitions.collision_radius)):
			body.queue_free()
			return null
		body.global_position = to_world(actor.position, identity)
		body.set_meta("room",identity)
		body.set_meta("logical_actor", weakref(actor))
		actor.physical_body = body
		bodies[key] = body
		logical_bodies[key] = weakref(actor)
		_settle_initial.call_deferred(actor, body, identity)
	body.collision_layer = PhysicsLayers.ACTOR
	body.collision_mask = PhysicsLayers.ACTOR_MOVEMENT_MASK
	return body

func advance(actor: CharacterBody2D, motion: Vector2) -> bool:
	var body := prepare_actor(actor)
	if body == null: return false
	var identity := str(body.get_meta("room"))
	var next_room := room_at(actor.position + motion)
	if next_room != identity:
		if next_room == "" or not _authorized_crossing(actor, identity, next_room): return false
		var destination_record := ensure_room(next_room)
		if destination_record.is_empty(): return false
		var arrival := to_world(actor.position + motion, next_room)
		if bool(destination_record.get("dv_authored", false)) and map_port.room_view(identity).space_id == destination_record.space_id:
			# A continuous DV building shares one coordinate frame. Walk through
			# the actual aperture with the physical capsule, including wall depth.
			identity = next_room
		else:
			# Isolated legacy scenes need a landing check before their frame remap.
			# Continuous buildings use move_and_slide below; an inflated preview
			# capsule must not veto a real capsule sliding along a door jamb.
			if not _clear(arrival, false): return false
			body.global_position = arrival
			# Legacy isolated room scenes still use their explicit frame remap.
			actor.set("unmeasured_room_transitions", int(actor.get("unmeasured_room_transitions")) + 1)
			body.set_meta("room", next_room)
			actor.position += motion
			return true
	var before_motion := body.global_position
	var target := to_world(actor.position + motion, identity)
	var delta := get_physics_process_delta_time()
	var displacement := target - body.global_position
	var rise := displacement.y
	displacement.y = 0
	if rise > 0.02 and rise <= 0.19:
		var raised := body.global_transform.translated(Vector3.UP * (rise + 0.01))
		if not body.test_move(body.global_transform, Vector3.UP * (rise + 0.01)) and not body.test_move(raised, displacement):
			body.move_and_collide(Vector3.UP * (rise + 0.01))
	body.velocity = displacement / maxf(delta, 0.000001)
	body.velocity.y = -0.5 if body.is_on_floor() else -2.0
	body.move_and_slide()
	actor.set("measured_motion_m", float(actor.get("measured_motion_m")) + before_motion.distance_to(body.global_position))
	actor.position = to_logical(body.global_position, identity)
	if DV_MAP.supports(field.adapter.map_key()):
		var measured_room := room_at(actor.position)
		if not measured_room.is_empty(): body.set_meta("room", measured_room)
	var measured := body.global_position - before_motion
	measured.y = 0.0
	if measured.length_squared() > 0.000001: body.face_world_direction(measured)
	return displacement.length() > 0.0

func _process(_delta: float) -> void:
	for key in bodies.keys():
		var logical = logical_bodies[key].get_ref()
		if not is_instance_valid(logical) or logical.world_stage != self:
			bodies.erase(key)
			logical_bodies.erase(key)
	if is_instance_valid(field.execution_bridge): sync_occupancy(field.execution_bridge)
	if field == null or not is_instance_valid(field.player): return
	var identity := room_at(field.player.position)
	var record := ensure_room(identity)
	if record.is_empty(): return
	if identity != current_room:
		current_room = identity
		if not field.execution_only:
			for door in doorways():
				if str(door.from) == identity: ensure_room(str(door.to))
				elif str(door.to) == identity: ensure_room(str(door.from))
	var observer_body := prepare_actor(field.player)
	observer_changed.call(identity, observer_body.global_position if observer_body != null else to_world(field.player.position, identity))
	sync_sites()
	for actor in field.actors.values():
		var body := prepare_actor(actor)
		if body != null: body.visible = not field.execution_only and str(body.get_meta("room")) == current_room and actor.visible

func pointer(screen: Vector2) -> Vector2:
	if current_room == "": return Vector2.INF
	var origin := camera.project_ray_origin(screen)
	var normal := camera.project_ray_normal(screen)
	var floor_y: float = to_world(field.player.position, current_room).y
	var ground: Variant = Plane(Vector3.UP, floor_y).intersects_ray(origin, normal)
	if ground == null: return Vector2.INF
	# Wall cutaways affect rendering, not collision. They must never redirect a
	# floor click to a hidden wall/roof elsewhere in the building.
	var floor_query := PhysicsRayQueryParameters3D.create(origin, ground + normal * 3.0, PhysicsLayers.FLOOR)
	var floor_hit := get_world_3d().direct_space_state.intersect_ray(floor_query)
	if not floor_hit.is_empty() and absf(float(floor_hit.position.y) - floor_y) < 2.0:
		ground = floor_hit.position
	var query := PhysicsRayQueryParameters3D.create(origin, ground, PhysicsLayers.SOLID | PhysicsLayers.ACTOR)
	var excluded: Array[RID] = []
	for attempt in 16:
		var picked := get_world_3d().direct_space_state.intersect_ray(query)
		if picked.is_empty(): break
		var collider: Node3D = picked.collider
		if collider.is_visible_in_tree() and collider.has_meta("logical_actor"):
			var actor = collider.get_meta("logical_actor").get_ref()
			if is_instance_valid(actor): return actor.position
		var node: Node = collider
		while node != null and node != self:
			if node.has_meta("source_prop"):
				var id := str(field.snapshot.get("sprite_sites", {}).get(str(node.get_meta("source_prop")), ""))
				if field.sites.has(id) and field.site_visible(id): return field.site_point(id)
			node = node.get_parent()
		excluded.append(picked.rid)
		query.exclude = excluded
	return to_logical(ground, current_room)

func native_passage_at(source: Vector2, attempted: Vector2, from_room: String, to_room: String, walker: CharacterBody2D) -> Dictionary:
	if not is_instance_valid(walker) or walker.position != source or room_at(source) != from_room or room_at(attempted) != to_room: return {}
	if source.distance_to(attempted) > 32.0: return {}
	for door in doorways():
		if not ((str(door.from) == from_room and str(door.to) == to_room) or (str(door.to) == from_room and str(door.from) == to_room)): continue
		# The actual short capsule sweep proves this crossing. Open circulation
		# boundaries are not constrained to a single arbitrary portal centre.
		if not _physical_segment_clear(to_world(source, from_room), to_world(attempted, to_room)): return {}
		return {"kind":"native-boundary", "map":field.adapter.map_key(), "from_room":from_room, "to_room":to_room,
			"source_point":source, "target_point":attempted, "landing":attempted}
	return {}

func native_doorway_rooms(point: Vector2) -> Array[String]:
	var result: Array[String] = []
	var identity := room_at(point)
	if identity.is_empty(): return result
	for door in doorways():
		var other := str(door.to) if str(door.from) == identity else str(door.from) if str(door.to) == identity else ""
		if other.is_empty() or point.distance_to(GEOMETRY.point(door.point)) > 40.0: continue
		# This is a near-door hint only. No room load or aperture search belongs
		# in the per-frame input probe; Prepare and native motion check clearance.
		if identity not in result: result.append(identity)
		if other not in result: result.append(other)
	return result

func _authorized_crossing(actor: CharacterBody2D, source: String, destination: String) -> bool:
	var actor_id := str(actor.get_meta("actor_id", actor.name))
	var authorized: bool = field._daily.has(actor_id)
	if actor == field.player and is_instance_valid(field.spatial): authorized = authorized or bool(field.snapshot.get("player_control", false))
	if not authorized and is_instance_valid(field.execution_bridge):
		for transaction in field.execution_bridge._transactions.values():
			if str(transaction.actor) == actor_id: authorized = true
	if not authorized: return false
	for door in doorways():
		if (str(door.from) == source and str(door.to) == destination) or (str(door.to) == source and str(door.from) == destination):
			if DV_MAP.supports(field.adapter.map_key()): return true
			var pair := _portal_pair(GEOMETRY.point(door.point), source, destination)
			if not pair.is_empty() and actor.position.distance_to(pair[0]) < 45.0: return true
	return false

func _clear(point: Vector3, require_floor := true, radius := 0.0) -> bool:
	var space := get_world_3d().direct_space_state
	if require_floor:
		var floor_y := _floor_height(point)
		if not is_finite(floor_y): return false
		point.y = floor_y
	return space.intersect_shape(_capsule_query(point,radius), 1).is_empty()

func _capsule_query(point: Vector3, radius := 0.0) -> PhysicsShapeQueryParameters3D:
	var shape := CapsuleShape3D.new()
	shape.radius = radius if radius > 0.0 else float(definitions.collision_radius) + 0.02
	shape.height = float(definitions.nominal_actor_height)
	var query := PhysicsShapeQueryParameters3D.new()
	query.shape = shape
	query.transform.origin = point + Vector3.UP * (shape.height * 0.5 + 0.04)
	query.collision_mask = PhysicsLayers.SOLID
	query.margin = 0.005
	return query

func _blocking_shapes(point: Vector3) -> Array:
	var result: Array = []
	for hit in get_world_3d().direct_space_state.intersect_shape(_capsule_query(point),8):
		var node = hit.get("collider")
		if node is Node:
			var source_id := ""
			var owner_node: Node = node
			while owner_node != null and source_id.is_empty():
				for key in ["world_object_id", "source_id", "object_id"]:
					source_id = str(owner_node.get_meta(key, ""))
					if not source_id.is_empty(): break
				owner_node = owner_node.get_parent()
			result.append({"node":str(node.get_path()),"shape":int(hit.get("shape",-1)),
				"source_id":source_id})
	return result

func _floor_height(point: Vector3) -> float:
	return map_port.sample_ground(point)

func _room_route(start: Vector2, finish: Vector2, identity: String, walker: CharacterBody2D = null) -> PackedVector2Array:
	var record := ensure_room(identity)
	if record.is_empty(): return PackedVector2Array()
	if not _clear(to_world(finish, identity)):
		return PackedVector2Array()
	if bool(record.get("dv_authored", false)):
		return await _dv_physical_room_route(start, finish, identity, record, walker)
	var geometry_key: String = map_port.navigation_geometry_key(identity)
	var grid: AStarGrid2D = legacy_grids.get(identity) if _legacy_grid_keys.get(identity, "") == geometry_key else null
	if grid == null:
		grid = AStarGrid2D.new()
		var bounds: Rect2 = record.footprint
		grid.cell_size = Vector2.ONE * 0.12
		grid.region = Rect2i(Vector2i.ZERO, Vector2i((bounds.size / grid.cell_size).ceil()))
		grid.offset = bounds.position + grid.cell_size * 0.5
		grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
		grid.update()
		for y in grid.region.size.y:
			for x in grid.region.size.x:
				await QUERY_BUDGET.checkpoint(get_tree())
				if field._closing: return PackedVector2Array()
				var cell := Vector2i(x,y)
				var local := grid.get_point_position(cell)
				grid.set_point_solid(cell, not _clear(record.origin + Vector3(local.x,0,local.y)))
		legacy_grids[identity] = grid
		_legacy_grid_keys[identity] = geometry_key
	var a: Vector3 = to_world(start, identity) - record.origin
	var b: Vector3 = to_world(finish, identity) - record.origin
	var occupied: Array[Vector2i] = []
	var obstacles := bodies.duplicate()
	obstacles.merge(occupancy)
	for key in obstacles:
		var other: CollisionObject3D = obstacles[key]
		if not is_instance_valid(other) or str(other.get_meta("room", "")) != identity: continue
		if walker != null and key is int and key == walker.get_instance_id(): continue
		if walker != null and int(other.get_meta("owner_instance", 0)) == walker.get_instance_id(): continue
		if walker == null and to_logical(other.global_position, identity).distance_to(start) < 1.0: continue
		var local: Vector3 = other.global_position - record.origin
		var center := Vector2i(((Vector2(local.x, local.z) - grid.offset) / grid.cell_size).round())
		var clearance := float(definitions.collision_radius) * 2.0 + 0.06
		var cells := int(ceil(clearance / grid.cell_size.x))
		for y in range(-cells, cells + 1):
			for x in range(-cells, cells + 1):
				var cell := center + Vector2i(x,y)
				if not grid.region.has_point(cell) or grid.is_point_solid(cell): continue
				if grid.get_point_position(cell).distance_to(Vector2(local.x, local.z)) > clearance: continue
				grid.set_point_solid(cell, true)
				occupied.append(cell)
	var first := _nearest_cell(grid, Vector2(a.x,a.z))
	var last := _nearest_cell(grid, Vector2(b.x,b.z))
	var result := PackedVector2Array()
	if first.x >= 0 and last.x >= 0:
		for local in grid.get_point_path(first,last):
			result.append(to_logical(record.origin + Vector3(local.x,0,local.y), identity))
	for cell in occupied: grid.set_point_solid(cell, false)
	if result.is_empty() and not route_failures.has(identity + ":grid"):
		route_failures[identity + ":grid"] = true
		print("3D grid route unavailable: ", identity, " local=", a, " -> ", b, " cells=", first, " -> ", last, " footprint=", record.footprint, " occupied=", occupied.size())
	return result

# The floor-only logical grid cannot route around actual cabinets, tables or wall faces.
# Rebuild this bounded room grid from the current physics world, including moving doors.
func _dv_physical_room_route(start: Vector2, finish: Vector2, identity: String, record: Dictionary, walker: CharacterBody2D) -> PackedVector2Array:
	var source := to_world(start, identity)
	var target := to_world(finish, identity)
	var obstacles := _route_actor_centers(walker, source)
	var clearance := float(definitions.collision_radius) * 2.0 + 0.06
	var direct := _physical_segment_clear(source, target) and _actor_segment_clear(source, target, obstacles, clearance)
	if direct:
		var samples := maxi(1, int(ceil(source.distance_to(target) / 0.24)))
		for index in range(samples + 1):
			await QUERY_BUDGET.checkpoint(get_tree())
			if field._closing: return PackedVector2Array()
			var point := source.lerp(target, float(index) / float(samples))
			if room_at(to_logical(point, identity)) != identity or not _clear(point):
				direct = false
				break
	if direct: return PackedVector2Array([start, finish])
	var bounds: Rect2 = record.footprint
	var spacing := 0.24
	var grid := AStarGrid2D.new()
	grid.cell_size = Vector2.ONE * spacing
	grid.region = Rect2i(Vector2i.ZERO, Vector2i((bounds.size / grid.cell_size).ceil()))
	grid.offset = bounds.position + grid.cell_size * 0.5
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	if grid.region.get_area() > 65536:
		push_error("Physical room navigation bounds exceed supported size: " + identity)
		return PackedVector2Array()
	grid.update()
	var geometry_key: String = map_port.navigation_geometry_key(identity)
	var cached: Dictionary = _physical_grid_cache.get(identity, {})
	if str(cached.get("geometry", "")) == geometry_key:
		navigation_grid_cache_hits += 1
		for cell: Vector2i in cached.solid: grid.set_point_solid(cell, true)
	else:
		navigation_grid_builds += 1
		var solid: Array[Vector2i] = []
		for y in grid.region.size.y:
			for x in grid.region.size.x:
				await QUERY_BUDGET.checkpoint(get_tree())
				if field._closing: return PackedVector2Array()
				var cell := Vector2i(x, y)
				var at := grid.get_point_position(cell)
				var point := Vector3(at.x, source.y, at.y)
				if room_at(to_logical(point, identity)) != identity or not _clear(point):
					grid.set_point_solid(cell, true)
					solid.append(cell)
		if map_port.navigation_geometry_key(identity) != geometry_key: return PackedVector2Array()
		_physical_grid_cache[identity] = {"geometry":geometry_key, "solid":solid}
	# Dynamic bodies are a per-request overlay, never part of the static cache.
	obstacles = _route_actor_centers(walker, source)
	for center: Vector2 in obstacles:
		var cell := Vector2i(((center-grid.offset)/grid.cell_size).round())
		var radius := int(ceil(clearance/spacing))
		for y in range(-radius,radius+1):
			for x in range(-radius,radius+1):
				var occupied := cell + Vector2i(x,y)
				if grid.region.has_point(occupied) and grid.get_point_position(occupied).distance_to(center) < clearance:
					grid.set_point_solid(occupied,true)
	var first := _physical_endpoint_cell(grid, source)
	var last := _physical_endpoint_cell(grid, target)
	if first.x < 0 or last.x < 0: return PackedVector2Array()
	# Sweeps verify the full capsule between samples; a thin obstruction cannot be skipped.
	for attempt in 8:
		var cells := grid.get_id_path(first, last)
		if cells.is_empty(): break
		var result := PackedVector2Array([start])
		var previous := source
		var blocked := false
		for cell in cells:
			await QUERY_BUDGET.checkpoint(get_tree())
			if field._closing: return PackedVector2Array()
			var at := grid.get_point_position(cell)
			var point := Vector3(at.x, source.y, at.y)
			if not _physical_segment_clear(previous, point):
				grid.set_point_solid(cell, true)
				blocked = true
				break
			result.append(to_logical(point, identity))
			previous = point
		if blocked: continue
		if not _physical_segment_clear(previous, target): break
		result.append(finish)
		return result
	return PackedVector2Array()

func _route_actor_centers(walker: CharacterBody2D, source: Vector3) -> Array[Vector2]:
	var result: Array[Vector2] = []
	var obstacles := bodies.duplicate()
	obstacles.merge(occupancy)
	for key in obstacles:
		var other: CollisionObject3D = obstacles[key]
		if not is_instance_valid(other) or absf(other.global_position.y-source.y)>1.0: continue
		if walker != null and (other == walker.physical_body or int(other.get_meta("owner_instance",0)) == walker.get_instance_id()): continue
		if walker == null and other.global_position.distance_to(source)<0.03: continue
		result.append(Vector2(other.global_position.x,other.global_position.z))
	return result

func _actor_segment_clear(a: Vector3, b: Vector3, centers: Array[Vector2], clearance: float) -> bool:
	for center in centers:
		if Geometry2D.get_closest_point_to_segment(center,Vector2(a.x,a.z),Vector2(b.x,b.z)).distance_to(center)<clearance: return false
	return true

func _physical_endpoint_cell(grid: AStarGrid2D, point: Vector3) -> Vector2i:
	var center := Vector2i(((Vector2(point.x, point.z) - grid.offset) / grid.cell_size).round())
	var best := Vector2i(-1, -1)
	var distance := INF
	for y in range(-2, 3):
		for x in range(-2, 3):
			var cell := center + Vector2i(x, y)
			if not grid.region.has_point(cell) or grid.is_point_solid(cell): continue
			var at := grid.get_point_position(cell)
			var candidate := Vector3(at.x, point.y, at.y)
			var error := candidate.distance_squared_to(point)
			if error < distance and _physical_segment_clear(point, candidate):
				best = cell
				distance = error
	return best

func _physical_segment_clear(start: Vector3, finish: Vector3) -> bool:
	if not _clear(start) or not _clear(finish): return false
	var query := _capsule_query(start)
	query.motion = finish - start
	var fractions := get_world_3d().direct_space_state.cast_motion(query)
	return fractions.size() == 2 and fractions[0] >= 0.9999

func sync_occupancy(executor: Node) -> void:
	var desired: Dictionary = {}
	for who in executor._bodies:
		var record: Dictionary = executor._bodies[who]
		if str(record.resource) != field.execution_resource or str(record.map) != field.adapter.map_key(): continue
		if not is_instance_valid(record.body) or record.field == field: continue
		desired["actor:" + str(who)] = {"point": record.body.position, "owner": record.body}
	for event_id in executor._transactions:
		var transaction: Dictionary = executor._transactions[event_id]
		if str(transaction.resource) != field.execution_resource or str(transaction.map) != field.adapter.map_key(): continue
		var record: Dictionary = executor._bodies.get(str(transaction.actor), {})
		if record.is_empty() or not is_instance_valid(record.body): continue
		desired["return:" + str(event_id)] = {"point": transaction.position, "owner": record.body}
	for key in occupancy.keys():
		if not desired.has(key):
			occupancy[key].collision_layer = 0
			occupancy[key].queue_free()
			occupancy.erase(key)
	for key in desired:
		var item: Dictionary = desired[key]
		var identity := room_at(item.point)
		if identity == "" or not map_port.has_room(identity):
			if occupancy.has(key): occupancy[key].collision_layer = 0
			continue
		var obstacle: StaticBody3D = occupancy.get(key)
		if obstacle == null:
			obstacle = StaticBody3D.new()
			obstacle.collision_layer = PhysicsLayers.ACTOR
			obstacle.collision_mask = 0
			var shape := CapsuleShape3D.new()
			shape.radius = float(definitions.collision_radius)
			shape.height = float(definitions.nominal_actor_height)
			var collider := CollisionShape3D.new()
			collider.shape = shape
			collider.position.y = shape.height * 0.5
			obstacle.add_child(collider)
			add_child(obstacle)
			occupancy[key] = obstacle
		obstacle.position = to_world(item.point, identity)
		obstacle.collision_layer = PhysicsLayers.ACTOR
		obstacle.set_meta("room", identity)
		obstacle.set_meta("owner_instance", item.owner.get_instance_id())
		if is_instance_valid(item.owner.physical_body): item.owner.physical_body.add_collision_exception_with(obstacle)

func _nearest_cell(grid: AStarGrid2D, point: Vector2) -> Vector2i:
	var center := Vector2i(((point-grid.offset)/grid.cell_size).round())
	var best := Vector2i(-1,-1)
	var distance := INF
	for y in range(-4,5):
		for x in range(-4,5):
			var cell := center + Vector2i(x,y)
			if not grid.region.has_point(cell) or grid.is_point_solid(cell): continue
			var value := grid.get_point_position(cell).distance_squared_to(point)
			if value < distance:
				best = cell
				distance = value
	return best

func navigation_route(start: Vector2, finish: Vector2, walker: CharacterBody2D = null) -> PackedVector2Array:
	if not is_instance_valid(walker): return PackedVector2Array()
	var id := walker.get_instance_id()
	var generation := int(_navigation_requests.get(id, 0)) + 1
	_navigation_requests[id] = generation
	var source_map: String = field.adapter.map_key()
	var result := await _plan_navigation_route(start, finish, walker)
	if field._closing or not is_instance_valid(walker) or walker.world_stage != self or field.adapter.map_key() != source_map or int(_navigation_requests.get(id, 0)) != generation or walker.position.distance_to(start) > 0.1:
		return PackedVector2Array()
	return result

func _plan_navigation_route(start: Vector2, finish: Vector2, walker: CharacterBody2D) -> PackedVector2Array:
	var source := room_at(start)
	var destination := room_at(finish)
	if source == "" or destination == "": return _route_blocked("unknown_region",source,destination)
	if source == destination:
		if not await map_port.prepare_rooms([source], field._is_closing): return PackedVector2Array()
		return await _room_route(start, finish, source, walker)
	var doors: Array = doorways()
	var pending := [source]
	var previous := {source: {}}
	while not pending.is_empty() and not previous.has(destination):
		var room: String = pending.pop_front()
		for door in doors:
			var next := str(door.to) if str(door.from) == room else str(door.from) if str(door.to) == room else ""
			if next == "" or previous.has(next): continue
			previous[next] = {"room":room,"door":door}
			pending.append(next)
	if not previous.has(destination): return _route_blocked("unconnected_regions",source,destination)
	var chain: Array = []
	var cursor := destination
	while cursor != source:
		chain.push_front(previous[cursor])
		cursor = str(previous[cursor].room)
	var needed: Array[String] = [source, destination]
	for link: Dictionary in chain:
		var room := str(link.room)
		if room not in needed: needed.append(room)
	if not await map_port.prepare_rooms(needed, field._is_closing): return PackedVector2Array()
	var result := PackedVector2Array()
	cursor = source
	var point := start
	for link in chain:
		var door: Dictionary = link.door
		var next := str(door.to) if str(door.from) == cursor else str(door.from)
		var portal := GEOMETRY.point(door.point)
		var pair := _portal_pair(portal, cursor, next)
		if pair.is_empty(): return _route_blocked("door_clearance", cursor, next)
		var exit_point: Vector2 = pair[0]
		var section := await _room_route(point, exit_point, cursor, walker)
		if section.is_empty(): return _route_blocked("door_approach", cursor, next)
		result.append_array(section)
		result.append(exit_point)
		point = pair[1]
		result.append(point)
		cursor = next
	var last := await _room_route(point, finish, destination, walker)
	if last.is_empty(): return _route_blocked("destination_approach", source, destination)
	result.append_array(last)
	return result

func _route_blocked(reason: String, source: String, destination: String) -> PackedVector2Array:
	var key := source + " -> " + destination + ": " + reason
	if not route_failures.has(key):
		route_failures[key] = true
		print("3D navigation blocked: ", key)
	return PackedVector2Array()

func sync_sites() -> void:
	if current_room == "" or not map_port.has_room(current_room): return
	var positions: Dictionary = map_port.site_positions(current_room, field.snapshot.get("sprite_sites", {}))
	for prop in positions:
		var id := str(field.snapshot.sprite_sites[prop])
		if not field.sites.has(id): continue
		var point := to_logical(positions[prop], current_room)
		field.sites[id].point = point
		field.sites[id].marker = point
		field.sites[id].erase("box")

func station_candidates(identity: String, actor: CharacterBody2D) -> Array[Vector2]:
	var result: Array[Vector2] = []
	var spacious: Array[Vector2] = []
	var record := ensure_room(identity)
	if record.is_empty(): return result
	if field.adapter.has_method("initial_room_candidates"):
		# Candidate positions are hints. Live 3D floor/capsule checks decide acceptance.
		var candidates: Array[Vector2] = field.adapter.initial_room_candidates(identity)
		var from := actor.position
		candidates.sort_custom(func(a: Vector2, b: Vector2): return a.distance_squared_to(from) < b.distance_squared_to(from))
		for point in candidates:
			await QUERY_BUDGET.checkpoint(get_tree())
			if field._closing or not is_instance_valid(actor) or actor.world_stage != self: return []
			if field.position_occupied(actor, point) or not _clear(to_world(point, identity)):
				continue
			result.append(point)
			if result.size() >= 128: break
		if not result.is_empty(): return result
	var bounds: Rect2 = record.footprint
	var elevation: float = DV_MAP.spatial_floor(field.adapter.map_key()).z if bool(record.get("dv_authored", false)) else record.origin.y
	for z in range(int(ceil(bounds.size.y / 0.24))):
		for x in range(int(ceil(bounds.size.x / 0.24))):
			await QUERY_BUDGET.checkpoint(get_tree())
			if field._closing or not is_instance_valid(actor) or actor.world_stage != self: return []
			var point: Vector3 = record.origin + Vector3(bounds.position.x + (x+0.5)*0.24,0,bounds.position.y+(z+0.5)*0.24)
			point.y = elevation
			var logical := to_logical(point, identity)
			if field.adapter.entered_room(logical) != identity or field.position_occupied(actor, logical): continue
			if _clear(point):
				result.append(logical)
				if _clear(point, true, 0.45): spacious.append(logical)
	if not spacious.is_empty(): result = spacious
	var center := to_logical(record.origin + Vector3(bounds.get_center().x,0,bounds.get_center().y), identity)
	result.sort_custom(func(a: Vector2, b: Vector2): return a.distance_squared_to(center) < b.distance_squared_to(center))
	return result

func positions_overlap(a: Vector2, b: Vector2, clearance := 0.85) -> bool:
	var identity := room_at(a)
	if identity == "" or room_at(b) != identity: return false
	var first := to_world(a, identity)
	var second := to_world(b, identity)
	return Vector2(first.x,first.z).distance_to(Vector2(second.x,second.z)) < clearance

func can_stand(point: Vector2) -> bool:
	var identity := room_at(point)
	return identity != "" and _clear(to_world(point, identity))

func transfer(actor: CharacterBody2D) -> bool:
	var identity := room_at(actor.position)
	if identity == "" or ensure_room(identity).is_empty(): return false
	var body := prepare_actor(actor)
	if body == null: return false
	body.global_position = to_world(actor.position,identity)
	body.set_meta("room",identity)
	body.velocity = Vector3.ZERO
	return true

## Walk a published flight/landing route in the existing World3D. Horizontal
## velocity, gravity and ramp collision alone change the physical footpoint.
func execute_floor_connection(actor: CharacterBody2D, connection: Dictionary) -> Dictionary:
	var body := prepare_actor(actor)
	if body == null or bool(body.get("is_seated")) or bool(body.get("is_lying")):
		return {"ok":false,"reason":"stair_actor_unavailable"}
	if not map_port.prepare_floor_connection(connection): return {"ok":false,"reason":"stair_geometry_unavailable"}
	await get_tree().physics_frame
	var coordinates = preload("res://scenes/components/world/DesignCoordinates.gd")
	var waypoints := PackedVector3Array()
	for raw: Array in connection.waypoints_design:
		waypoints.append(coordinates.to_godot(Vector3(raw[0],raw[1],raw[2])))
	var tolerance := float(spatial_execution_profile.get("arrival_tolerance_m",0.08))
	if waypoints.is_empty() or body.global_position.distance_to(waypoints[0])>maxf(tolerance,0.15):
		map_port.finish_floor_connection()
		return {"ok":false,"reason":"stair_entry_not_reached"}
	var original_snap := body.floor_snap_length
	body.floor_snap_length = maxf(original_snap,0.25)
	var travelled := 0.0
	var reason := ""
	actor.set_meta("native_floor_connection",str(connection.id))
	for target: Vector3 in waypoints:
		var best := body.global_position.distance_to(target)
		var stalled := 0.0
		while body.global_position.distance_to(target)>tolerance:
			await get_tree().physics_frame
			if field._closing or not is_instance_valid(actor) or not is_instance_valid(body): return {"ok":false,"reason":"stair_execution_cancelled"}
			if get_tree().paused: continue
			var delta := get_physics_process_delta_time()
			var difference := target-body.global_position
			var flat := Vector3(difference.x,0.0,difference.z)
			var speed := float(spatial_execution_profile.get("speed_mps",1.05))
			var velocity := flat.normalized()*minf(speed,flat.length()/maxf(delta,0.000001))
			velocity.y = -0.5 if body.is_on_floor() else body.velocity.y-float(spatial_execution_profile.get("gravity_mps2",9.8))*delta
			body.velocity = velocity
			if flat.length_squared()>0.000001: body.face_world_direction(flat)
			body.set_movement_speed(speed)
			var before := body.global_position
			body.move_and_slide()
			var measured := before.distance_to(body.global_position)
			travelled += measured
			actor.measured_motion_m += measured
			actor.position = DV_MAP.design_to_pixel(str(connection.from_map),coordinates.to_design(body.global_position))
			var remaining := body.global_position.distance_to(target)
			if remaining < best-float(spatial_execution_profile.get("minimum_progress_m",0.002)):
				best = remaining
				stalled = 0.0
			else: stalled += delta
			if stalled>float(spatial_execution_profile.get("stall_timeout_s",1.5)):
				reason = "stair_physical_path_blocked"
				break
		if not reason.is_empty(): break
	body.velocity = Vector3.ZERO
	body.set_movement_speed(0.0)
	body.floor_snap_length = original_snap
	actor.remove_meta("native_floor_connection")
	if not reason.is_empty(): map_port.finish_floor_connection()
	return {"ok":reason.is_empty(),"reason":reason,"actual_end":body.global_position,"measured_distance":travelled}

func adopt_floor_endpoint(actor: CharacterBody2D, connection: Dictionary) -> bool:
	var body: CharacterBody3D = actor.physical_body
	if not is_instance_valid(body): return false
	var raw: Array = connection.end_design
	var target := preload("res://scenes/components/world/DesignCoordinates.gd").to_godot(Vector3(raw[0],raw[1],raw[2]))
	if body.global_position.distance_to(target)>float(spatial_execution_profile.get("arrival_tolerance_m",0.08)): return false
	map_port.map_id = str(connection.to_map)
	if ensure_room(str(connection.to_room)).is_empty(): return false
	body.set_meta("room",str(connection.to_room))
	map_port.finish_floor_connection()
	return true

func _settle_initial(actor: CharacterBody2D, body: CharacterBody3D, identity: String) -> void:
	await get_tree().physics_frame
	await get_tree().physics_frame
	if not is_instance_valid(actor) or not is_instance_valid(body) or actor.world_stage != self: return
	if str(body.get_meta("room", "")) != identity: return
	var initial_floor := _floor_height(body.global_position)
	if is_finite(initial_floor): body.global_position.y = initial_floor
	if _clear(body.global_position, false) and is_finite(initial_floor): return
	var original := body.global_position
	var logical_origin := actor.position
	var candidates: Array[Vector2] = field.adapter.initial_room_candidates(identity)
	candidates.sort_custom(func(a: Vector2, b: Vector2) -> bool:
		return a.distance_squared_to(logical_origin) < b.distance_squared_to(logical_origin))
	for logical_point in candidates:
		await QUERY_BUDGET.checkpoint(get_tree())
		if not _placement_still_pending(actor, body, identity, original): return
		var candidate := to_world(logical_point, identity)
		var floor_y := _floor_height(candidate)
		if not is_finite(floor_y): continue
		candidate.y = floor_y
		if not _clear(candidate, false): continue
		body.global_position = candidate
		actor.position = logical_point
		return
	# Legacy authored rooms can have finer walkable gaps than the logical grid.
	# Visit nearby cells first and yield between bounded physics-query slices.
	var record: Dictionary = map_port.room_view(identity)
	if record.is_empty(): return
	var footprint: Rect2 = record.footprint
	var origin: Vector3 = record.origin
	var size := Vector2i((footprint.size / 0.18).ceil())
	var local := Vector2(original.x - origin.x, original.z - origin.z)
	var center := Vector2i(((local - footprint.position) / 0.18).floor()).clamp(Vector2i.ZERO, size - Vector2i.ONE)
	for ring in range(maxi(size.x, size.y)):
		for z in range(maxi(0, center.y - ring), mini(size.y, center.y + ring + 1)):
			for x in range(maxi(0, center.x - ring), mini(size.x, center.x + ring + 1)):
				if maxi(absi(x-center.x), absi(z-center.y)) != ring: continue
				await QUERY_BUDGET.checkpoint(get_tree())
				if not _placement_still_pending(actor, body, identity, original): return
				var candidate := origin + Vector3(footprint.position.x+(x+0.5)*0.18, original.y-origin.y, footprint.position.y+(z+0.5)*0.18)
				var floor_y := _floor_height(candidate)
				if not is_finite(floor_y): continue
				candidate.y = floor_y
				if not _clear(candidate, false): continue
				body.global_position = candidate
				actor.position = to_logical(candidate, identity)
				return
	push_error("No collision-free 3D initial placement for %s map=%s room=%s origin=%s footprint=%s floor=%s" % [actor.name, field.adapter.map_key(), identity, original, footprint, initial_floor])

func _placement_still_pending(actor: CharacterBody2D, body: CharacterBody3D, identity: String, original: Vector3) -> bool:
	return is_instance_valid(actor) and is_instance_valid(body) and actor.world_stage == self and str(body.get_meta("room", "")) == identity and body.global_position.is_equal_approx(original)

func _portal_region(identity: String, portal: Vector2) -> Rect2:
	var region: Dictionary = GEOMETRY.room_data(field.adapter.map_key(),identity)
	var selected := GEOMETRY.rect(region.rect)
	var distance := INF
	for bounds in region.get("rects", [region.rect]):
		var area := GEOMETRY.rect(bounds)
		var nearest := portal.clamp(area.position,area.end)
		var candidate := portal.distance_squared_to(nearest)
		if candidate < distance:
			selected = area
			distance = candidate
	return selected

func _portal_pair(portal: Vector2, source: String, destination: String) -> Array:
	if ensure_room(source).is_empty() or ensure_room(destination).is_empty(): return []
	var cache_key := str([source, destination, portal, map_port.navigation_geometry_key(source), map_port.navigation_geometry_key(destination)])
	if _portal_pairs.has(cache_key): return _portal_pairs[cache_key]
	var a := _portal_region(source,portal)
	var b := _portal_region(destination,portal)
	var first := portal.clamp(a.position + Vector2.ONE * 2,a.end-Vector2.ONE*2)
	var second := portal.clamp(b.position + Vector2.ONE * 2,b.end-Vector2.ONE*2)
	var horizontal := absf(first.y-second.y) >= absf(first.x-second.x)
	# Check the aperture itself, not an old marker placed inside the room:
	# an interior marker can miss the frame that the capsule must pass through.
	if horizontal:
		first.y = a.end.y - 0.05 if b.get_center().y > a.get_center().y else a.position.y + 0.05
		second.y = b.position.y + 0.05 if b.get_center().y > a.get_center().y else b.end.y - 0.05
	else:
		first.x = a.end.x - 0.05 if b.get_center().x > a.get_center().x else a.position.x + 0.05
		second.x = b.position.x + 0.05 if b.get_center().x > a.get_center().x else b.end.x - 0.05
	var best := INF
	var result: Array = []
	for index in 257:
		var step: int = 0 if index == 0 else ((index + 1) / 2) * (1 if index % 2 == 1 else -1)
		var offset := Vector2(step*2,0) if horizontal else Vector2(0,step*2)
		var start := first + offset
		var finish := second + offset
		if absf(step) >= best or not a.has_point(start) or not b.has_point(finish): continue
		if not _clear(to_world(start,source),false) or not _clear(to_world(finish,destination),false): continue
		result = [start,finish]
		best = absf(step)
		break
	if result.is_empty():
		route_failure_details[source+" -> "+destination+": door_clearance"] = {
			"portal_logical":[portal.x,portal.y],
			"source_blockers":_blocking_shapes(to_world(first,source)),
			"destination_blockers":_blocking_shapes(to_world(second,destination)),
			"basis":"actual-Godot-capsule-intersections-at-central-portal-candidate"}
	if _portal_pairs.size() >= 128: _portal_pairs.clear()
	_portal_pairs[cache_key] = result
	return result

func doorways() -> Array:
	return map_port.doorways()

func visible_objects(occluded_regions: Array[Rect2]) -> Array[String]:
	var result: Array[String] = []
	for node in map_port.visible_object_nodes(current_room, camera, occluded_regions):
		var title: String = field._player_object_title(node)
		if title != "" and not result.has(title): result.append(title)
	result.sort()
	return result

func ensure_room(identity: String) -> Dictionary:
	if map_port.map_id == field.adapter.map_key():
		var existing: Dictionary = map_port.room_view(identity)
		if not existing.is_empty(): return existing
	return room_loader.call(identity)


var room_unloader: Callable
var room_reloader: Callable

func unload_room_visuals(identity: String) -> bool:
	return room_unloader.call(identity)

func reload_room_visuals(identity: String, state: Dictionary = {}) -> bool:
	return room_reloader.call(identity, state)
