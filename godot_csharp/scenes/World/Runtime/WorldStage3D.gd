extends Node3D
const PhysicsLayers = preload("res://geometry/PhysicsLayers.gd")
const GEOMETRY = preload("res://scenes/Host/GeometryCatalog.gd")
const BODY = preload("res://scenes/World/Actors/StoryBody3D.gd")
const DV_MAP = preload("res://geometry/LegacyDvMap.gd")
const COORDINATES = preload("res://geometry/DesignCoordinates.gd")
const DV_BUILDING = preload("res://geometry/BuildingFramework.gd")
var field: Node2D
var camera: Camera3D
var definitions: Dictionary
var cast: Dictionary
var rooms: Dictionary = {}
var bodies: Dictionary = {}
var logical_bodies: Dictionary = {}
var current_room := ""
var _dv_buildings: Dictionary = {}
var occlusion: Node
var route_failures: Dictionary = {}
var occupancy: Dictionary = {}
var lighting_minute := -1.0

# Third-person camera state (Task 4.06)
var camera_arm_length: float = 4.2
var camera_current_distance: float = 4.2
var camera_smoothed_target: Vector3 = Vector3.ZERO
var camera_initialized: bool = false

var _legacy_definitions_loaded := false

func _ready() -> void:
	var reader = preload("res://scenes/Host/DvPresentationContent.cs").new()
	var spatial_record: Dictionary = reader.ReadPublishedData("95-configurable-building.data.dv", "space.configurable_building")
	var spatial: Dictionary = spatial_record.get("value", {})
	var actor_profile: Dictionary = spatial.get("host_actor_profile", {})
	definitions = {
		"nominal_actor_height": float(actor_profile.get("nominal_actor_height_m", 0.0)),
		"collision_radius": float(actor_profile.get("collision_radius_m", 0.0)),
		"rooms": {}
	}
	if float(definitions.nominal_actor_height) <= 0.0 or float(definitions.collision_radius) <= 0.0:
		push_error("Selected DV building has no valid host actor profile.")
	var art: Dictionary = reader.ReadRecord("res://Content/character_art.dv", "layouts", "character_art")
	cast = {}
	for identity in art.cast:
		cast[str(art.cast[identity].character_id)] = art.models.get(identity, {})
	camera = Camera3D.new()
	camera.name = "WorldCamera3D"
	camera.projection = Camera3D.PROJECTION_PERSPECTIVE
	camera.fov = 58.0
	camera.near = 0.1
	camera.far = 500.0
	add_child(camera)
	camera.current = not field.execution_only
	occlusion = preload("res://rendering/CameraOcclusion.gd").new()
	add_child(occlusion)
	var environment := WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color("182326")
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color("d5dce0")
	environment.environment.ambient_light_energy = 0.45
	add_child(environment)

func _ensure_legacy_definitions() -> void:
	if _legacy_definitions_loaded: return
	_legacy_definitions_loaded = true
	var reader = preload("res://scenes/Host/DvPresentationContent.cs").new()
	var legacy: Dictionary = reader.ReadRecord("res://Content/world3d.dv", "layouts", "world3d")
	if legacy.is_empty() or not legacy.has("rooms"):
		push_error("Legacy room catalogue is unavailable for a non-DV map.")
		return
	definitions = legacy

func ensure_room(identity: String) -> Dictionary:
	if rooms.has(identity): return rooms[identity]
	if DV_MAP.supports(field.adapter.map_key()):
		return _ensure_dv_room(identity)
	_ensure_legacy_definitions()
	var spec: Dictionary = definitions.get("rooms", {}).get(identity, {})
	if spec.is_empty(): return {}
	var packed := load(str(spec.scene)) as PackedScene
	if packed == null: return {}
	var scene := packed.instantiate() as Node3D
	if scene == null: return {}
	var authored_environment := scene.get_node_or_null("WorldEnvironment") as WorldEnvironment
	if authored_environment != null and authored_environment.environment != null:
		scene.set_meta("presentation_environment", authored_environment.environment.duplicate(true))
	# Each room retains its metre-scale authored geometry in an isolated physical zone.
	scene.position = Vector3((rooms.size() % 8) * 512.0, 0, (rooms.size() / 8) * 512.0)
	if bool(spec.get("mirror_x", false)): scene.scale.x = -1.0
	_strip_presentation(scene)
	add_child(scene)
	var floor_bounds: Array[AABB] = []
	_floor_bounds(scene, Transform3D.IDENTITY, floor_bounds)
	var footprint := Rect2(-4,-4,8,8)
	if not floor_bounds.is_empty():
		var bounds: AABB = floor_bounds[0]
		for item in floor_bounds: bounds = bounds.merge(item)
		footprint = Rect2(bounds.position.x - scene.position.x, bounds.position.z - scene.position.z, bounds.size.x, bounds.size.z)
	var record := {"root":scene,"footprint":footprint,"grid":null}
	rooms[identity] = record
	scene.visible = false
	return record

func _ensure_dv_room(identity: String) -> Dictionary:
	var map_id: String = str(field.adapter.map_key())
	var spec: Dictionary = GEOMETRY.room_data(map_id, identity)
	if spec.is_empty(): return {}
	var building: Node3D = _dv_buildings.get(map_id)
	if not is_instance_valid(building):
		var source: Dictionary = DV_MAP.spatial_source(map_id)
		var selected: Dictionary = DV_MAP.spatial_floor(map_id)
		if source.is_empty() or selected.is_empty(): return {}
		# Each legacy input map receives exactly its DV-authored floor,
		# with no fallback to the old per-room PackedScenes.
		var published: Dictionary = source.duplicate(false)
		published["floors"] = [selected]
		building = DV_BUILDING.new()
		building.name = "DvBuilding_" + map_id
		add_child(building)
		building.visible = false
		building.call("build", published)
		if not bool(building.get("build_completed")):
			push_error("Selected DV geometry failed to build in legacy Godot host: " + map_id)
			building.queue_free()
			return {}
		_dv_buildings[map_id] = building
	var raw := GEOMETRY.rect(spec.rect)
	var a := DV_MAP.pixel_to_godot(map_id, raw.position)
	var b := DV_MAP.pixel_to_godot(map_id, raw.end)
	var footprint := Rect2(Vector2(minf(a.x,b.x),minf(a.z,b.z)),
		Vector2(absf(b.x-a.x),absf(b.z-a.z)))
	var record := {"root":building,"footprint":footprint,"grid":null,"dv_authored":true}
	rooms[identity] = record
	return record

## Task 4.11: Release visual world resources for inactive room while keeping Core state intact
func unload_room_visuals(identity: String) -> bool:
	if not rooms.has(identity):
		return false
	var record: Dictionary = rooms[identity]
	var root_node = record.get("root") as Node3D
	if root_node != null and is_instance_valid(root_node):
		root_node.visible = false
		record["visuals_unloaded"] = true
		return true
	return false

## Task 4.11: Reload visual resources upon re-entering room and re-synchronize with Core state
func reload_room_visuals(identity: String, core_state_override: Dictionary = {}) -> bool:
	if not rooms.has(identity):
		return false
	var record: Dictionary = rooms[identity]
	var root_node = record.get("root") as Node3D
	if root_node != null and is_instance_valid(root_node):
		root_node.visible = true
		record["visuals_unloaded"] = false
		if not core_state_override.is_empty():
			for obj_id in core_state_override:
				var state_val = core_state_override[obj_id]
				var obj = _find_world_object_recursive(root_node, str(obj_id))
				if obj != null and obj.has_method("set_angle_checked"):
					obj.call("set_angle_checked", float(state_val))
		return true
	return false

func _find_world_object_recursive(parent: Node, id_str: String) -> Node:
	if parent.has_meta("world_object_id") and str(parent.get_meta("world_object_id")) == id_str:
		return parent
	if str(parent.name) == id_str:
		return parent
	for child in parent.get_children():
		var found = _find_world_object_recursive(child, id_str)
		if found != null: return found
	return null

## Task 4.12: Debug visual overlay toggle
func toggle_spatial_debug(enabled: bool, show_colliders: bool = true, show_slots: bool = true, show_ids: bool = true) -> Dictionary:
	var DebugOverlay = preload("res://geometry/SpatialDebugOverlay.gd")
	return DebugOverlay.toggle(self, enabled, show_colliders, show_slots, show_ids)


func _strip_presentation(node: Node) -> void:
	for child in node.get_children():
		if child is Camera3D or child is WorldEnvironment or str(child.name) in ["CameraOcclusion", "RenderDetailController"] or child.has_meta("actor_id"):
			node.remove_child(child)
			child.free()
		else: _strip_presentation(child)

func _floor_bounds(node: Node, inherited: Transform3D, result: Array[AABB]) -> void:
	var transform := inherited
	if node is Node3D: transform = inherited * node.transform
	if node is MeshInstance3D and node.mesh != null:
		var bounds: AABB = transform * node.get_aabb()
		if bounds.size.x > 1.0 and bounds.size.z > 1.0 and bounds.position.y < 0.08 and bounds.end.y < 0.2:
			result.append(bounds)
	if node is GridMap and "floor" in str(node.name).to_lower() and node.mesh_library != null:
		for cell in node.get_used_cells():
			var item: int = node.get_cell_item(cell)
			var mesh: Mesh = node.mesh_library.get_item_mesh(item)
			if mesh != null:
				result.append(transform * Transform3D(Basis.IDENTITY,node.map_to_local(cell)) * node.mesh_library.get_item_mesh_transform(item) * mesh.get_aabb())
	for child in node.get_children(): _floor_bounds(child, transform, result)

func room_at(point: Vector2) -> String:
	return str(GEOMETRY.room_at(field.adapter.map_key(), point).get("id", ""))

func to_world(point: Vector2, identity: String) -> Vector3:
	var record := ensure_room(identity)
	if record.is_empty(): return Vector3.INF
	if bool(record.get("dv_authored", false)):
		return DV_MAP.pixel_to_godot(field.adapter.map_key(), point)
	var logical := GEOMETRY.rect(GEOMETRY.room_data(field.adapter.map_key(), identity).rect)
	var local: Vector2 = record.footprint.position + (point - logical.position) / logical.size * record.footprint.size
	var point3d: Vector3 = record.root.position + Vector3(local.x, 0, local.y)
	var floor_y := _floor_height(point3d)
	if is_finite(floor_y): point3d.y = floor_y
	return point3d

func to_logical(point: Vector3, identity: String) -> Vector2:
	var record: Dictionary = rooms[identity]
	if bool(record.get("dv_authored", false)):
		return DV_MAP.design_to_pixel(field.adapter.map_key(), COORDINATES.to_design(point))
	var local: Vector3 = point - record.root.position
	var logical := GEOMETRY.rect(GEOMETRY.room_data(field.adapter.map_key(), identity).rect)
	return logical.position + (Vector2(local.x,local.z) - record.footprint.position) / record.footprint.size * logical.size

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
		if not _clear(arrival, false):
			if not body.has_meta("crossing_block_reported"):
				body.set_meta("crossing_block_reported", true)
				print("3D doorway blocked: ", identity, " -> ", next_room, " at ", arrival, " logical ", actor.position + motion)
			return false
		body.global_position = arrival
		body.set_meta("room", next_room)
		actor.position += motion
		return true
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
	actor.position = to_logical(body.global_position, identity)
	if displacement.length_squared() > 0.00001: body.visual.rotation.y = atan2(displacement.x,displacement.z)
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
		for item in rooms.values(): item.root.visible = false
		record.root.visible = not field.execution_only
		current_room = identity
		lighting_minute = -1.0
		camera.environment = record.root.get_meta("presentation_environment") if record.root.has_meta("presentation_environment") else null
		for door in doorways():
			if str(door.from) == identity: ensure_room(str(door.to))
			elif str(door.to) == identity: ensure_room(str(door.from))
		occlusion.configure(camera, [record.root], to_world(field.player.position, identity) + Vector3.UP * 0.8)
	sync_sites()
	var minute := float(field.snapshot.get("game_time", 720.0))
	if minute != lighting_minute:
		var lighting: Node = record.root.get_node_or_null("WorldTimeLighting")
		if lighting != null: lighting.apply_snapshot(field.snapshot)
		lighting_minute = minute
	for actor in field.actors.values():
		var body := prepare_actor(actor)
		if body != null: body.visible = not field.execution_only and str(body.get_meta("room")) == current_room and actor.visible
	# Task 4.06: Third-person perspective camera follow with smooth tracking and wall raycast anti-clipping
	var player_body := prepare_actor(field.player)
	var focus_height := 1.05
	if player_body != null:
		if bool(player_body.get("is_seated")):
			focus_height = 0.65
		elif bool(player_body.get("is_lying")):
			focus_height = 0.45

	var player_world: Vector3 = to_world(field.player.position, identity)
	var desired_focus: Vector3 = player_world + Vector3.UP * focus_height

	if not camera_initialized:
		camera_smoothed_target = desired_focus
		camera_current_distance = camera_arm_length
		camera_initialized = true
	else:
		camera_smoothed_target = camera_smoothed_target.lerp(desired_focus, clampf(_delta * 12.0, 0.0, 1.0))

	var pitch_rad := deg_to_rad(26.0)
	var arm_dir := Vector3(0.0, sin(pitch_rad), cos(pitch_rad)).normalized()
	var desired_cam_pos := camera_smoothed_target + arm_dir * camera_arm_length

	var ray_query := PhysicsRayQueryParameters3D.create(camera_smoothed_target, desired_cam_pos, PhysicsLayers.SOLID)
	if player_body != null:
		ray_query.exclude = [player_body.get_rid()]
	var ray_hit := get_world_3d().direct_space_state.intersect_ray(ray_query)
	if not ray_hit.is_empty():
		var hit_dist: float = (Vector3(ray_hit.position) - camera_smoothed_target).length()
		var safe_dist: float = maxf(0.85, hit_dist - 0.20)
		camera_current_distance = minf(camera_current_distance, safe_dist)
	else:
		camera_current_distance = move_toward(camera_current_distance, camera_arm_length, _delta * 3.5)

	camera.global_position = camera_smoothed_target + arm_dir * camera_current_distance
	camera.look_at(camera_smoothed_target, Vector3.UP)
	occlusion.focus = camera_smoothed_target

func pointer(screen: Vector2) -> Vector2:
	if current_room == "": return Vector2.INF
	var origin := camera.project_ray_origin(screen)
	var normal := camera.project_ray_normal(screen)
	var query := PhysicsRayQueryParameters3D.create(origin, origin + normal * camera.far, 3)
	var excluded: Array[RID] = []
	for attempt in 8:
		var picked := get_world_3d().direct_space_state.intersect_ray(query)
		if picked.is_empty(): break
		var collider: Node3D = picked.collider
		if collider.has_meta("owner_instance"):
			excluded.append(picked.rid)
			query.exclude = excluded
			continue
		if collider.has_meta("logical_actor"):
			var actor = collider.get_meta("logical_actor").get_ref()
			if is_instance_valid(actor): return actor.position
		var node: Node = collider
		var architectural := false
		while node != null and node != self:
			var label := str(node.name).to_lower()
			architectural = architectural or "wall" in label or "roof" in label or "ceiling" in label
			if node.has_meta("source_prop"):
				var id := str(field.snapshot.get("sprite_sites", {}).get(str(node.get_meta("source_prop")), ""))
				if field.sites.has(id): return field.site_point(id)
			node = node.get_parent()
		if not architectural: return to_logical(picked.position, current_room)
		excluded.append(picked.rid)
		query.exclude = excluded
	var hit = Plane(Vector3.UP,0).intersects_ray(origin,normal)
	if hit == null: return Vector2.INF
	return to_logical(hit,current_room)

func _authorized_crossing(actor: CharacterBody2D, source: String, destination: String) -> bool:
	var actor_id := str(actor.get_meta("actor_id", actor.name))
	var authorized: bool = field._daily.has(actor_id)
	if actor == field.player and is_instance_valid(field.spatial): authorized = authorized or not field.continuous_request().is_empty()
	if not authorized and is_instance_valid(field.execution_bridge):
		for transaction in field.execution_bridge._transactions.values():
			if str(transaction.actor) == actor_id: authorized = true
	if not authorized: return false
	for door in doorways():
		if (str(door.from) == source and str(door.to) == destination) or (str(door.to) == source and str(door.from) == destination):
			var pair := _portal_pair(GEOMETRY.point(door.point), source, destination)
			if not pair.is_empty() and actor.position.distance_to(pair[0]) < 45.0: return true
	return false

func _clear(point: Vector3, require_floor := true, radius := 0.0) -> bool:
	var space := get_world_3d().direct_space_state
	if require_floor:
		var floor_y := _floor_height(point)
		if not is_finite(floor_y): return false
		point.y = floor_y
	var shape := CapsuleShape3D.new()
	shape.radius = radius if radius > 0.0 else float(definitions.collision_radius) + 0.02
	shape.height = float(definitions.nominal_actor_height)
	var query := PhysicsShapeQueryParameters3D.new()
	query.shape = shape
	query.transform.origin = point + Vector3.UP * (shape.height * 0.5 + 0.04)
	query.collision_mask = PhysicsLayers.SOLID
	query.margin = 0.005
	return space.intersect_shape(query, 1).is_empty()

func _floor_height(point: Vector3) -> float:
	var dv_floor := DV_MAP.supports(field.adapter.map_key())
	var mask := PhysicsLayers.FLOOR if dv_floor else PhysicsLayers.SOLID
	var ray := PhysicsRayQueryParameters3D.create(point + Vector3.UP * 2.0, point - Vector3.UP * 2.0, mask)
	var excluded: Array[RID] = []
	for attempt in 12:
		var hit := get_world_3d().direct_space_state.intersect_ray(ray)
		if hit.is_empty(): return INF
		var collider: Node3D = hit.collider
		if hit.normal.y > 0.7 and (bool(collider.collision_layer & PhysicsLayers.FLOOR) if dv_floor else "floor" in str(collider.name).to_lower()):
			return float(hit.position.y)
		excluded.append(hit.rid)
		ray.exclude = excluded
	return INF

func _room_route(start: Vector2, finish: Vector2, identity: String, walker: CharacterBody2D = null) -> PackedVector2Array:
	var record := ensure_room(identity)
	if record.is_empty(): return PackedVector2Array()
	if not _clear(to_world(finish, identity)):
		return PackedVector2Array()
	if bool(record.get("dv_authored", false)):
		# The existing host owns a 16 px navigation grid derived from this
		# exact DV floor. Do not allocate a second million-cell 3D room grid.
		if walker == null or not is_instance_valid(field.adapter): return PackedVector2Array()
		var nav: AStarGrid2D = field.adapter.nav_grid(walker, true)
		var from_cell := Vector2i((start / 16.0).floor())
		var to_cell := Vector2i((finish / 16.0).floor())
		if not nav.region.has_point(from_cell) or not nav.region.has_point(to_cell):
			return PackedVector2Array()
		if nav.is_point_solid(from_cell) or nav.is_point_solid(to_cell):
			return PackedVector2Array()
		var routed := PackedVector2Array()
		for at in nav.get_point_path(from_cell, to_cell):
			if room_at(at) != identity or not _clear(to_world(at, identity)):
				return PackedVector2Array()
			routed.append(at)
		return routed
	var grid: AStarGrid2D = record.grid
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
				var cell := Vector2i(x,y)
				var local := grid.get_point_position(cell)
				grid.set_point_solid(cell, not _clear(record.root.position + Vector3(local.x,0,local.y)))
		record.grid = grid
	var a: Vector3 = to_world(start, identity) - record.root.position
	var b: Vector3 = to_world(finish, identity) - record.root.position
	var occupied: Array[Vector2i] = []
	var obstacles := bodies.duplicate()
	obstacles.merge(occupancy)
	for key in obstacles:
		var other: CollisionObject3D = obstacles[key]
		if not is_instance_valid(other) or str(other.get_meta("room", "")) != identity: continue
		if walker != null and key == walker.get_instance_id(): continue
		if walker != null and int(other.get_meta("owner_instance", 0)) == walker.get_instance_id(): continue
		if walker == null and to_logical(other.global_position, identity).distance_to(start) < 1.0: continue
		var local: Vector3 = other.global_position - record.root.position
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
			result.append(to_logical(record.root.position + Vector3(local.x,0,local.y), identity))
	for cell in occupied: grid.set_point_solid(cell, false)
	if result.is_empty() and not route_failures.has(identity + ":grid"):
		route_failures[identity + ":grid"] = true
		print("3D grid route unavailable: ", identity, " local=", a, " -> ", b, " cells=", first, " -> ", last, " footprint=", record.footprint, " occupied=", occupied.size())
	return result

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
		if identity == "" or not rooms.has(identity):
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
	var source := room_at(start)
	var destination := room_at(finish)
	if source == "" or destination == "": return PackedVector2Array()
	if source == destination: return _room_route(start, finish, source, walker)
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
	if not previous.has(destination): return PackedVector2Array()
	var chain: Array = []
	var cursor := destination
	while cursor != source:
		chain.push_front(previous[cursor])
		cursor = str(previous[cursor].room)
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
		var section := _room_route(point, exit_point, cursor, walker)
		if section.is_empty(): return _route_blocked("door_approach", cursor, next)
		result.append_array(section)
		result.append(exit_point)
		point = pair[1]
		result.append(point)
		cursor = next
	var last := _room_route(point, finish, destination, walker)
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
	if current_room == "" or not rooms.has(current_room): return
	var root: Node3D = rooms[current_room].root
	for prop in field.snapshot.get("sprite_sites", {}):
		var id := str(field.snapshot.sprite_sites[prop])
		if not field.sites.has(id): continue
		var node := root.find_child(str(prop), true, false) as Node3D
		if node == null: continue
		var point := to_logical(node.global_position, current_room)
		field.sites[id].point = point
		field.sites[id].marker = point
		field.sites[id].erase("box")

func station_candidates(identity: String, actor: CharacterBody2D) -> Array[Vector2]:
	var result: Array[Vector2] = []
	var spacious: Array[Vector2] = []
	var record := ensure_room(identity)
	if record.is_empty(): return result
	if bool(record.get("dv_authored", false)):
		# Reuse the DV-derived 2D candidates. The 3D physical capsule remains
		# authoritative for accepting each candidate.
		for point in field.adapter.initial_room_candidates(identity):
			if field.position_occupied(actor, point) or not _clear(to_world(point, identity)):
				continue
			result.append(point)
			if result.size() >= 128: break
		return result
	var bounds: Rect2 = record.footprint
	for z in range(int(ceil(bounds.size.y / 0.24))):
		for x in range(int(ceil(bounds.size.x / 0.24))):
			var point: Vector3 = record.root.position + Vector3(bounds.position.x + (x+0.5)*0.24,0,bounds.position.y+(z+0.5)*0.24)
			var logical := to_logical(point, identity)
			if field.adapter.entered_room(logical) != identity or field.position_occupied(actor, logical): continue
			if _clear(point):
				result.append(logical)
				if _clear(point, true, 0.45): spacious.append(logical)
	if not spacious.is_empty(): result = spacious
	var center := to_logical(record.root.position + Vector3(bounds.get_center().x,0,bounds.get_center().y), identity)
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

func _settle_initial(actor: CharacterBody2D, body: CharacterBody3D, identity: String) -> void:
	await get_tree().physics_frame
	await get_tree().physics_frame
	if not is_instance_valid(actor) or not is_instance_valid(body) or actor.world_stage != self: return
	var initial_floor := _floor_height(body.global_position)
	if is_finite(initial_floor): body.global_position.y = initial_floor
	if _clear(body.global_position): return
	# DV compatibility levels do not live at Y=0. Reuse the legacy adapter's
	# prepared walkable candidates, then verify each against real 3D physics.
	if DV_MAP.supports(field.adapter.map_key()):
		var best_dv := Vector3.INF
		var best_error := INF
		var original_dv := body.global_position
		for logical_point in field.adapter.initial_room_candidates(identity):
			var candidate_dv := to_world(logical_point, identity)
			var measured_floor := _floor_height(candidate_dv)
			if not is_finite(measured_floor): continue
			candidate_dv.y = measured_floor
			var separation := candidate_dv.distance_squared_to(original_dv)
			if separation >= best_error or not _clear(candidate_dv): continue
			best_dv = candidate_dv
			best_error = separation
		if best_dv.is_finite():
			body.global_position = best_dv
			actor.position = to_logical(best_dv, identity)
		else:
			push_error("No collision-free DV building placement for " + str(actor.name))
		return
	var original := body.global_position
	var best := Vector3.INF
	var distance := INF
	var footprint: Rect2 = rooms[identity].footprint
	var origin: Vector3 = rooms[identity].root.position
	for z in range(int(ceil(footprint.size.y / 0.18))):
		for x in range(int(ceil(footprint.size.x / 0.18))):
			var candidate := origin + Vector3(footprint.position.x + (x+0.5)*0.18,0,footprint.position.y+(z+0.5)*0.18)
			var offset := candidate.distance_squared_to(original)
			if offset < distance and _clear(candidate):
				best = candidate
				distance = offset
	if best.is_finite():
		best.y = _floor_height(best)
		body.global_position = best
		actor.position = to_logical(best, identity)
	else:
		push_error("No collision-free 3D initial placement for " + str(actor.name))

func _portal_pair(portal: Vector2, source: String, destination: String) -> Array:
	var a := GEOMETRY.rect(GEOMETRY.room_data(field.adapter.map_key(),source).rect)
	var b := GEOMETRY.rect(GEOMETRY.room_data(field.adapter.map_key(),destination).rect)
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
	for step in range(-128,129):
		var offset := Vector2(step*2,0) if horizontal else Vector2(0,step*2)
		var start := first + offset
		var finish := second + offset
		if absf(step) >= best or not a.has_point(start) or not b.has_point(finish): continue
		if not _clear(to_world(start,source),false) or not _clear(to_world(finish,destination),false): continue
		result = [start,finish]
		best = absf(step)
	return result

func doorways() -> Array:
	var result: Array = GEOMETRY.map_data(field.adapter.map_key()).get("doors", []).duplicate()
	result.append_array(definitions.get("passages", {}).get(field.adapter.map_key(), []))
	return result

func visible_objects(occluded_regions: Array[Rect2]) -> Array[String]:
	var result: Array[String] = []
	if current_room == "" or not rooms.has(current_room): return result
	var root: Node3D = rooms[current_room].root
	for node in root.find_children("*", "Node3D", true, false):
		if not node.has_meta("source_prop") or not node.is_visible_in_tree(): continue
		var exposed := false
		for mesh in node.find_children("*", "MeshInstance3D", true, false):
			if not mesh.is_visible_in_tree(): continue
			var bounds: AABB = mesh.global_transform * mesh.get_aabb()
			var point := bounds.get_center()
			if camera.is_position_behind(point): continue
			var screen := camera.unproject_position(point)
			if not get_viewport().get_visible_rect().has_point(screen): continue
			var covered := false
			for region in occluded_regions:
				if region.has_point(screen): covered = true
			if covered: continue
			var query := PhysicsRayQueryParameters3D.create(camera.global_position, point, 1)
			var hit := get_world_3d().direct_space_state.intersect_ray(query)
			if hit.is_empty() or node.is_ancestor_of(hit.collider) or node == hit.collider:
				exposed = true
				break
		if not exposed: continue
		var title: String = field._player_object_title(node)
		if title != "" and not result.has(title): result.append(title)
	result.sort()
	return result
