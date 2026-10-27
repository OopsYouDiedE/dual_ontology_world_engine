extends Node2D

const LIFECYCLE := preload("res://scenes/Host/HostLifecycle.gd")
## 学园多地图的适配器：载入 Maps/ 下的地图，进门口自动换图；室外分区相互重叠，走出本区核心即换到所在区，
## 世界坐标连续。提供寻路网格（碰撞体栅格化，半格精度）、视线墙、房间区域、交互点标记、站位与遮挡淡出。
## 剧情与文字不在这里。

const RESOURCES := preload("res://scenes/World/Runtime/FieldResources.gd")
const GEOMETRY := preload("res://scenes/Host/GeometryCatalog.gd")
const MAX_RETAINED_MAPS := 3
const PREPARE_BUDGET_USEC := 2000
const MAX_LOCAL_NAV_GRIDS := 32

static func _fail_binding(node: Node, message: String) -> void:
	push_error(message)
	if node.is_inside_tree(): LIFECYCLE.binding_failure.call_deferred(node, message)

const SEAM := 36.0
## 只作装饰、不开放交互的标记。
const DECOR_MARKERS := ["transmitter", "isolation_gate", "lane", "harbor", "stele", "collapse", "counterweight"]
func _get_packed_map(map_name: String) -> PackedScene:
	var path := str(GEOMETRY.map_data(map_name).scene)
	var packed := RESOURCES.ready(path) as PackedScene
	# Compatibility for callers that deliberately construct a map synchronously.
	# The production host and cross-map executor await prepare_map first.
	return packed if packed != null else (load(path) as PackedScene if ResourceLoader.exists(path) else null)

var field: Node2D
var map: Node2D
var key := ""
var armed := true
var _switching := false
var occluders: Array[Sprite2D] = []
var _grids: Dictionary = {}
var _solid_regions: Dictionary = {}
var _blocked_rects: Dictionary = {}
var _blocked_cells: Dictionary = {}
var _walls: Dictionary = {}
var _seen: Dictionary = {}
var _access: Dictionary = {}
var _access_shapes: Dictionary = {}
var _access_refresh_pending := false
var _last_allowed_position := Vector2.ZERO
var presentation: Dictionary = {}
var _player_sprite := ""
var _route_target_next := ""
var _stage_bounds_cache: Dictionary = {}
var _stage_ready: Dictionary = {}
var _stage_light_cache: Dictionary = {}
var _local_grids: Dictionary = {}
var _doorway_cache: Dictionary = {}
var _map_records: Dictionary = {}
var _map_order: Array[String] = []
var _preparing: Dictionary = {}
var _room_candidates: Dictionary = {}
var _navigation_rooms: Dictionary = {}
var _closing := false
var _prefetching := false

func _cancelled() -> bool:
	return _closing or (is_instance_valid(field) and field._closing)

## May run before setup, when this adapter has no parent yet. The host supplies
## its SceneTree in that case. False means cancelled or unavailable, never arrived.
func prepare_map(next: String, value: Dictionary, tree: SceneTree = null) -> bool:
	if tree == null: tree = get_tree()
	if tree == null or next == "" or _cancelled(): return false
	var binding := _preparation_binding(next, value)
	if _prepared_for(next, binding): return true
	while _preparing.has(next):
		await tree.process_frame
		if _cancelled(): return false
		if _prepared_for(next, binding): return true
	_preparing[next] = true
	var scene_path := str(GEOMETRY.map_data(next).scene)
	var paths: Array[String] = [scene_path]
	var declaration: Dictionary = binding.geometry
	for prop in declaration.get("props", []):
		var path := str(prop.get("texture", ""))
		if path not in paths: paths.append(path)
	var resources: Dictionary = await RESOURCES.wait_for(paths, tree, _cancelled)
	if resources.is_empty():
		_preparing.erase(next)
		return false
	if not _map_records.has(next):
		var packed := resources.get(scene_path) as PackedScene
		if packed == null:
			_preparing.erase(next)
			return false
		var instantiated_layout := packed.instantiate() as Node2D
		if instantiated_layout == null:
			_preparing.erase(next)
			return false
		_map_records[next] = _new_map_record(next, instantiated_layout)
		_touch_map(next)
		_trim_maps(next)
	var record: Dictionary = _map_records[next]
	var layout: Node2D = record.node
	var architecture := layout.get_node_or_null("Architecture") as Sprite2D
	if architecture != null and architecture.texture != null and not bool(record.relief_ready):
		var relief := architecture.texture.resource_path.get_basename() + ".relief.tres"
		if ResourceLoader.exists(relief):
			var relief_resources: Dictionary = await RESOURCES.wait_for([relief], tree, _cancelled)
			if relief_resources.is_empty():
				_preparing.erase(next)
				return false
			architecture.texture = relief_resources[relief] as Texture2D
		record.relief_ready = true
	# Preparing an inactive layout does not change the visible field's geometry,
	# map, observer, bodies or live input/arrival state.
	if layout != map:
		_apply_record_geometry(next, record, value)
		var access = value.get("access", {})
		var maps: Dictionary = access.get("maps", {}) if access is Dictionary and access.get("maps") is Dictionary else {}
		_apply_record_access(next, record, maps)
	var ready := false
	while not _cancelled() and is_instance_valid(layout):
		# Access shapes are refreshed outside physics callbacks. Wait for that
		# already queued refresh before planning on an active map's grid.
		if layout == map and _access_refresh_pending:
			await tree.process_frame
			continue
		_build_grid_for(next, layout)
		ready = await _prepare_room_candidates(next, layout, tree)
		if ready: ready = await _prepare_stage_bounds(next, layout, tree)
		if ready and (layout != map or not _access_refresh_pending): break
		# A live binding can invalidate the grid between preparation slices.
		# Rebuild from the new geometry instead of reporting a physical failure.
	_preparing.erase(next)
	if not ready or _cancelled(): return false
	# Keep the batch's actor frames alive until this prepared map is used or
	# evicted; they may not have an instantiated body yet.
	record.resources = resources
	record.prepared_binding = binding.duplicate(true)
	_touch_map(next)
	_trim_maps(next)
	return true

static func _append_actor_resource(paths: Array[String], sprite: String) -> void:
	if sprite == "": return
	var path: String = RESOURCES.actor_path(sprite)
	if path == "":
		# Keep a missing binding visible to the normal preparation error path.
		path = "res://ShareAsset/Internal/Characters/PixelCast/%s.tres" % sprite
	if path not in paths: paths.append(path)

func _preparation_binding(map_name: String, value: Dictionary) -> Dictionary:
	var geometry: Dictionary = GEOMETRY.additions(map_name)
	var access = value.get("access", {})
	var maps: Dictionary = access.get("maps", {}) if access is Dictionary and access.get("maps") is Dictionary else {}
	var sprites: Array[String] = []
	var viewer_sprite := str(value.get("presentation", {}).get("player_sprite", ""))
	if viewer_sprite != "": sprites.append(viewer_sprite)
	for person in value.get("people", []):
		if str(person.get("map", "")) != map_name or not bool(person.get("present", true)): continue
		var sprite := str(person.get("sprite", ""))
		if sprite != "" and sprite not in sprites: sprites.append(sprite)
	sprites.sort()
	return {"geometry": geometry, "access": maps, "sprites": sprites}

func _prepared_for(map_name: String, binding: Dictionary) -> bool:
	return _map_records.has(map_name) and _grids.has(map_name) and _room_candidates.has(map_name) and _stage_ready.has(map_name) and not (map_name == key and _access_refresh_pending) and _map_records[map_name].get("prepared_binding", {}) == binding

func _new_map_record(map_name: String, layout: Node2D) -> Dictionary:
	GEOMETRY.bind_layout(map_name, layout)
	preload("res://scenes/World/Runtime/RoomArtFinish.gd").attach(layout)
	return {"node": layout, "geometry": {}, "geometry_applied": false,
		"geometry_nodes": [], "access_shapes": {}, "relief_ready": false, "resources": {}}

func _touch_map(name: String) -> void:
	_map_order.erase(name)
	_map_order.append(name)

func _trim_maps(keep := "") -> void:
	for name in _map_order.duplicate():
		if _map_records.size() <= MAX_RETAINED_MAPS: break
		if str(name) == key or str(name) == keep or str(name) == _route_target_next or _preparing.has(name): continue
		var layout: Node2D = _map_records[name].node
		# Retained actor ownership is held by DvFieldExecutor, outside these layouts.
		if is_instance_valid(layout) and not layout.is_inside_tree(): layout.free()
		_map_records.erase(name)
		_map_order.erase(name)
		_invalidate_map(str(name))
		_stage_ready.erase(name)
		_stage_light_cache.erase(name)
		_doorway_cache.erase(name)
		for region in _stage_bounds_cache.keys():
			if str(region).begins_with(str(name) + ":"): _stage_bounds_cache.erase(region)

## Opportunistic preparation is sequential and bounded to adjacent map resources.
## It reads bindings only; access and formal movement still use the live snapshot.
func prefetch_snapshot(value: Dictionary) -> void:
	if _prefetching or _cancelled() or not is_inside_tree(): return
	_prefetching = true
	var origin := key
	var graph := _map_routes()
	var count := 0
	for edge in graph.get(origin, []):
		var next := str(edge.get("to", ""))
		if next == "" or next == key: continue
		if _cancelled() or key != origin: break
		await prepare_map(next, value, get_tree())
		count += 1
		if count >= MAX_RETAINED_MAPS - 1: break
	_prefetching = false

func begin_shutdown() -> void:
	if _closing: return
	_closing = true
	_switching = false
	_route_target_next = ""
	_access_refresh_pending = false
	for record: Dictionary in _map_records.values():
		var layout: Node2D = record.node
		if is_instance_valid(layout) and not layout.is_inside_tree(): layout.free()
	_map_records.clear()
	_map_order.clear()
	_preparing.clear()

func _exit_tree() -> void:
	begin_shutdown()

func _invalidate_map(map_name: String) -> void:
	_grids.erase(map_name)
	_solid_regions.erase(map_name)
	_blocked_rects.erase(map_name)
	_blocked_cells.erase(map_name)
	_room_candidates.erase(map_name)
	_navigation_rooms.erase(map_name)
	_local_grids.erase(map_name)
	_walls.erase(map_name)

func apply_snapshot(snapshot: Dictionary) -> void:
	_apply_geometry_bindings(snapshot)
	var raw_access = snapshot.get("access", {})
	var next: Dictionary = (raw_access.get("maps", {}) if raw_access is Dictionary and raw_access.get("maps") is Dictionary else {})
	if next == _access: return
	_access = next.duplicate(true)
	if map:
		_access_refresh_pending = true
		_refresh_access_geometry.call_deferred()

## Godot owns authored geometry; DV supplies semantic access and interaction identities.
func _apply_geometry_bindings(snapshot: Dictionary, rebuild := true) -> void:
	if map == null: return
	var changed := _apply_record_geometry(key, _map_records[key], snapshot)
	if changed and rebuild:
		_build_grid()
		_build_walls()

func _apply_record_geometry(map_name: String, record: Dictionary, snapshot: Dictionary) -> bool:
	var declaration: Dictionary = GEOMETRY.additions(map_name)
	if bool(record.geometry_applied) and declaration == record.geometry: return false
	var layout: Node2D = record.node
	var textures: Dictionary = {}
	for prop in declaration.get("props", []):
		var path := str(prop.get("texture", ""))
		var texture := RESOURCES.ready(path) as Texture2D
		if texture == null: texture = load(path) as Texture2D
		if texture == null:
			_fail_binding(self, "Missing native geometry texture: " + str(prop.get("id", "")))
			return false
		textures[str(prop.id)] = texture
	for node in record.geometry_nodes:
		if is_instance_valid(node):
			node.get_parent().remove_child(node)
			node.queue_free()
	record.geometry_nodes.clear()
	for site in declaration.get("sites", []):
		var id := str(site.get("id", ""))
		if id == "" or layout.get_node("Sites").has_node(NodePath(id)):
			_fail_binding(self, "Duplicate or empty native geometry site: " + id)
			continue
		var marker := Marker2D.new()
		marker.name = id
		marker.position = Vector2(float(site.point[0]), float(site.point[1]))
		marker.set_meta("label", str(site.get("label", id)))
		layout.get_node("Sites").add_child(marker)
		record.geometry_nodes.append(marker)
	for prop in declaration.get("props", []):
		var texture: Texture2D = textures[str(prop.id)]
		var sprite := Sprite2D.new()
		sprite.name = str(prop.id)
		sprite.texture = texture
		sprite.position = Vector2(float(prop.position[0]), float(prop.position[1]))
		sprite.centered = false
		sprite.offset = Vector2(-texture.get_width() / 2.0, -texture.get_height())
		layout.get_node("Objects").add_child(sprite)
		record.geometry_nodes.append(sprite)
		if prop.has("footprint"):
			var rect: Array = prop.footprint
			var shape := RectangleShape2D.new()
			shape.size = Vector2(float(rect[2]), float(rect[3]))
			var solid := CollisionShape2D.new()
			solid.shape = shape
			solid.position = sprite.position + Vector2(float(rect[0]), float(rect[1])) + shape.size / 2.0
			layout.get_node("Solids").add_child(solid)
			record.geometry_nodes.append(solid)
	record.geometry = declaration.duplicate(true)
	record.geometry_applied = true
	_invalidate_map(map_name)
	return true

func can_enter(next: String) -> bool:
	return bool(_access.get(next, true))

func blocked_entry() -> void:
	_switching = false
	if map: field.player.position = _last_allowed_position

func finish_transition() -> void:
	_switching = false

## A closed destination blocks the same doorway for keyboard movement and navigation.
## The authored exit shape supplies geometry; availability comes from the live snapshot.
func _refresh_access_geometry() -> void:
	_access_refresh_pending = false
	if _closing or not is_instance_valid(map) or not _map_records.has(key): return
	if _apply_record_access(key, _map_records[key], _access):
		_build_grid()
		_build_walls()

func _apply_record_access(map_name: String, record: Dictionary, access: Dictionary) -> bool:
	var layout: Node2D = record.node
	var solids := layout.get_node_or_null("Solids")
	if solids == null: return false
	var changed := false
	for exit in layout.get_node("Exits").get_children():
		var disabled := bool(access.get(str(exit.get_meta("gate", exit.get_meta("target", ""))), true))
		var barrier: CollisionShape2D = record.access_shapes.get(exit.name)
		if not is_instance_valid(barrier):
			var source := exit.get_node("Shape") as CollisionShape2D
			barrier = CollisionShape2D.new()
			barrier.name = "Access_" + str(exit.name)
			barrier.shape = source.shape
			barrier.position = exit.position + source.position
			barrier.disabled = disabled
			solids.add_child(barrier)
			record.access_shapes[exit.name] = barrier
			changed = true
		elif barrier.disabled != disabled:
			barrier.disabled = disabled
			changed = true
	if changed: _invalidate_map(map_name)
	return changed

func setup() -> bool:
	_player_sprite = str(presentation.get("player_sprite", ""))
	if _player_sprite == "":
		_fail_binding(self, "Missing player portrait binding")
		return false
	return true
func player_key() -> String: return str(presentation.player_actor)
func player_sprite() -> String: return _player_sprite
func fixed_actors() -> Dictionary: return {}
func objects_root() -> Node2D: return map.get_node("Objects") if map else self
func map_key() -> String: return key
func switching() -> bool: return _switching
func camera_smoothing() -> bool: return true
func stage_bounds(p: Vector2) -> Rect2:
	if map == null or not sight_terrain_fog(): return camera_limits()
	var region := sight_region(Vector2i((p / 32.0).floor()))
	var cache_key := key + ":" + region
	if _stage_bounds_cache.has(cache_key): return _stage_bounds_cache[cache_key]
	# All authored regions are prepared before the chapter is revealed. A
	# malformed/empty region must not introduce a full-map scan during walking.
	return Rect2(p - Vector2(16, 16), Vector2(32, 32))

func stage_lights(p: Vector2) -> Array:
	var region := sight_region(Vector2i((p / 32.0).floor()))
	return _stage_light_cache.get(key, {}).get(region, [])

func presentation_capacity() -> Vector2i:
	var result := Vector2i.ZERO
	for declarations: Array in _stage_light_cache.get(key, {}).values():
		var windows := 0
		for declaration in declarations:
			if str(declaration.get("kind", "")) == "window": windows += 1
		result.x = maxi(result.x, declarations.size())
		result.y = maxi(result.y, windows)
	return result

## Cache every room in one pass while the book still covers the chapter.
## Only static terrain labels enter this cache; access/occupancy stays live.
func _prepare_stage_bounds(map_name: String, layout: Node2D, tree: SceneTree) -> bool:
	if _stage_ready.has(map_name): return true
	var rows: Array = layout.get_meta("terrain_rows", [])
	var regions := {}
	var slice_start := Time.get_ticks_usec()
	for y in rows.size():
		var row: String = rows[y]
		for x in row.length():
			var region := _region_in_map(map_name, Vector2i(x, y))
			var cell := Rect2(Vector2(x, y) * 32.0, Vector2(32, 32))
			regions[region] = (regions[region] as Rect2).merge(cell) if regions.has(region) else cell
		if Time.get_ticks_usec() - slice_start >= PREPARE_BUDGET_USEC:
			await tree.process_frame
			if _cancelled(): return false
			slice_start = Time.get_ticks_usec()
	var lights := {}
	for region in regions:
		_stage_bounds_cache[map_name + ":" + str(region)] = regions[region]
		var declarations: Array = []
		for declaration in layout.get_meta("stage_lights", []):
			var point := Vector2(float(declaration.point[0]), float(declaration.point[1]))
			if (regions[region] as Rect2).grow(12).has_point(point): declarations.append(declaration)
		lights[region] = declarations
	_stage_light_cache[map_name] = lights
	_stage_ready[map_name] = true
	return true

func camera_point(p: Vector2) -> Vector2:
	if not sight_terrain_fog(): return p
	var bounds := stage_bounds(p)
	var view: Vector2 = field.get_viewport_rect().size / field.camera.zoom
	var focus := bounds.get_center()
	# Small rooms form one stage; larger halls track without leaving their bounds.
	for axis in 2:
		if bounds.size[axis] > view[axis]:
			focus[axis] = clampf(p[axis], bounds.position[axis] + view[axis] * 0.5, bounds.end[axis] - view[axis] * 0.5)
	return focus
func camera_limits() -> Rect2:
	var size: Vector2 = map.get_meta("map_size", Vector2(1024, 1024)) if map else Vector2(1024, 1024)
	return Rect2(Vector2.ZERO, size)
func seen_cells() -> Dictionary:
	if not _seen.has(key): _seen[key] = {}
	return _seen[key]
func station(target: String, who: String) -> Dictionary:
	var actor: CharacterBody2D = field.actors.get(who)
	if actor == null: return {}
	if target.begins_with("site:"):
		var geometry := site_geometry(field.snapshot.get("sites", {}))
		var id := target.substr(5)
		if not geometry.has(id): return {}
		if field.sites.has(id):
			return {"point": field.sites[id].point, "radius": float(geometry[id].reach)}
		return {"point": geometry[id].point, "radius": float(geometry[id].reach)}
	if is_instance_valid(field.stage3d):
		var candidates: Array[Vector2] = field.stage3d.station_candidates(target, actor)
		return {} if candidates.is_empty() else {"point": actor.position, "radius": 0.0, "room_target": true, "approach_groups": [candidates]}
	_ensure_room_candidates()
	var occupied: Array[Vector2] = field.occupied_positions(actor)
	var has_free_floor := false
	for point: Vector2 in _room_candidates[key].get(target, {}).get("fallback", []):
		if not _point_occupied(point, occupied, true):
			has_free_floor = true
			break
	if not has_free_floor: return {}
	var preferred: Array[Vector2] = []
	for point: Vector2 in stands():
		if entered_room(point) == target and walkable(point, actor) and not _point_occupied(point, occupied):
			preferred.append(point)
	preferred.sort_custom(func(a: Vector2, b: Vector2) -> bool: return a.distance_squared_to(actor.position) < b.distance_squared_to(actor.position))
	# Usually a retained Stand is reachable. Do not scan/sort every floor point
	# until the preferred group has actually failed its real navigation query.
	return {"point": actor.position, "radius": 0.0, "room_target": true,
		"approach_groups": [preferred,
			func(): return _ordered_room_candidates(target, "open", actor, occupied),
			func(): return _ordered_room_candidates(target, "fallback", actor, occupied)]}

func _point_occupied(point: Vector2, occupied: Array[Vector2], grid_cell := false) -> bool:
	for position in occupied:
		var separation := (position - point).abs()
		if grid_cell:
			if separation.x <= 19.0 and separation.y <= 15.0: return true
		elif separation.x < 19.0 and separation.y < 15.0:
			return true
	return false

func _ensure_room_candidates() -> void:
	if _room_candidates.has(key): return
	var rooms := {}
	var navigation := {}
	var grid: AStarGrid2D = _grids[key]
	for y in grid.region.size.y:
		_append_room_row(key, map, y, rooms, navigation)
	_room_candidates[key] = rooms
	_navigation_rooms[key] = navigation

## Initial placement shares the prepared geometry used by ordinary movement.
## Prefer room interiors with turning clearance; never offer doors or void cells.
## The host still checks live occupancy and never relocates an existing body.
func initial_room_candidates(room: String) -> Array[Vector2]:
	_ensure_room_candidates()
	var result: Array[Vector2] = []
	var candidates: Dictionary = _room_candidates[key].get(room, {})
	result.assign(candidates.get("open", []))
	result.append_array(candidates.get("fallback", []))
	return result

func _ordered_room_candidates(room: String, group: String, actor: CharacterBody2D, occupied: Array[Vector2]) -> Array[Vector2]:
	_ensure_room_candidates()
	var result: Array[Vector2] = []
	for point: Vector2 in _room_candidates[key].get(room, {}).get(group, []):
		if not _point_occupied(point, occupied, true): result.append(point)
	result.sort_custom(func(a: Vector2, b: Vector2) -> bool: return a.distance_squared_to(actor.position) < b.distance_squared_to(actor.position))
	return result

func _prepare_room_candidates(map_name: String, layout: Node2D, tree: SceneTree) -> bool:
	if _room_candidates.has(map_name): return true
	var grid: AStarGrid2D = _grids[map_name]
	var rooms := {}
	var navigation := {}
	var slice_start := Time.get_ticks_usec()
	for y in grid.region.size.y:
		if _cancelled() or _grids.get(map_name) != grid: return false
		_append_room_row(map_name, layout, y, rooms, navigation)
		if Time.get_ticks_usec() - slice_start >= PREPARE_BUDGET_USEC:
			await tree.process_frame
			if _cancelled(): return false
			slice_start = Time.get_ticks_usec()
	if _grids.get(map_name) != grid: return false
	_room_candidates[map_name] = rooms
	_navigation_rooms[map_name] = navigation
	return true

func _append_room_row(map_name: String, layout: Node2D, y: int, rooms: Dictionary, navigation: Dictionary) -> void:
	var grid: AStarGrid2D = _grids[map_name]
	var rows: Array = layout.get_meta("terrain_rows", [])
	var outdoor: bool = layout.has_meta("outdoor_core")
	for x in grid.region.size.x:
		var cell := Vector2i(x, y)
		var point := grid.get_point_position(cell)
		var room := _room_id(map_name, Vector2i((point / 32.0).floor()))
		if not navigation.has(room): navigation[room] = []
		navigation[room].append(cell)
		if grid.is_point_solid(cell): continue
		if room == "void": continue
		if not outdoor and _mark_in_rows(rows, Vector2i((point / 32.0).floor())) not in [".", "S"]: continue
		if not rooms.has(room): rooms[room] = {"open": [], "fallback": []}
		rooms[room].fallback.append(point)
		var can_turn := true
		for offset in [Vector2.LEFT * 16, Vector2.RIGHT * 16, Vector2.UP * 16, Vector2.DOWN * 16]:
			var near: Vector2 = point + offset
			var near_cell := Vector2i((near / 32.0).floor())
			if _room_id(map_name, near_cell) != room or not _walkable_on_map(map_name, near) or (not outdoor and _mark_in_rows(rows, near_cell) not in [".", "S"]):
				can_turn = false
				break
		if can_turn: rooms[room].open.append(point)

static func _mark_in_rows(rows: Array, cell: Vector2i) -> String:
	if cell.y < 0 or cell.y >= rows.size(): return ""
	var row: String = rows[cell.y]
	return row.substr(cell.x, 1) if cell.x >= 0 and cell.x < row.length() else ""

func _map_routes() -> Dictionary:
	return GEOMETRY.routes()

func _next_edge(destination: String) -> Dictionary:
	var graph := _map_routes()
	var queue: Array = [{"map": key, "first": {}}]
	var visited := {}
	visited[key] = true
	while not queue.is_empty():
		var item: Dictionary = queue.pop_front()
		for edge in graph.get(item.map, []):
			if visited.has(edge.to) or not can_enter(str(edge.to)): continue
			var first: Dictionary = edge if item.map == key else item.first
			if str(edge.to) == destination: return first
			visited[edge.to] = true
			queue.append({"map": edge.to, "first": first})
	return {}

## Routes execute geometry only; DV owns the selected goal and the final state change.
func execute_destination(destination: Dictionary, who: String, serial: int, action: String) -> void:
	var actor: CharacterBody2D = field.actors.get(who)
	if actor == null or (actor != field.player and key != str(destination.get("map", ""))):
		field._daily_report(who, serial, "blocked", "", action)
		return
	while key != str(destination.get("map", "")):
		var edge := _next_edge(str(destination.get("map", "")))
		if edge.is_empty():
			field._daily_report(who, serial, "blocked", room_of(actor.position), action, {"reason": "no_map_route"})
			return
		var origin := key
		_route_target_next = str(edge.to)
		# Start the next map's I/O and layout preparation while walking toward
		# its authored exit. It has no effect on the body or receipt.
		prepare_map(str(edge.to), field.snapshot, get_tree())
		await field._follow_route(actor, edge.point)
		await get_tree().physics_frame
		await get_tree().process_frame
		while _switching and not field._closing:
			await get_tree().process_frame
		_route_target_next = ""
		if field._closing or not is_instance_valid(actor): return
		if key == origin:
			field._daily_report(who, serial, "blocked", room_of(actor.position), action, {"reason": "map_transition_not_completed"})
			return
	var anchor_name := str(destination.get("anchor", ""))
	if anchor_name != "":
		var point = anchor(anchor_name)
		if point != null: await field._follow_route(actor, point)
		if point == null or actor.position.distance_to(point) > 8.0:
			field._daily_report(who, serial, "blocked", room_of(actor.position), action, {"reason": "anchor_not_reached", "anchor": anchor_name})
			return
	var target := str(destination.get("departure_station", destination.get("room", "")))
	var execution_room := str(destination.get("execution_room", destination.get("room", "")))
	field.move_to_station(target, execution_room, who, serial, action)

func load_map(next: String, _spawn: String) -> void:
	if key == next and map != null: return
	if not _map_records.has(next):
		var packed := _get_packed_map(next)
		if packed == null: return
		_map_records[next] = _new_map_record(next, packed.instantiate() as Node2D)
	if map:
		# Keep the inactive layout detached: no collision world, area callbacks,
		# actors or processing run in a cached room.
		for exit in map.get_node("Exits").get_children():
			var area := exit as Area2D
			area.monitoring = false
			for c in area.body_entered.get_connections(): area.body_entered.disconnect(c.callable)
			for c in area.body_exited.get_connections(): area.body_exited.disconnect(c.callable)
		remove_child(map)
	key = next
	var record: Dictionary = _map_records[next]
	map = record.node
	_access_shapes = record.access_shapes
	add_child(map)
	var architecture := map.get_node_or_null("Architecture") as Sprite2D
	if architecture != null and architecture.texture != null and not bool(record.relief_ready):
		var relief := architecture.texture.resource_path.get_basename() + ".relief.tres"
		if ResourceLoader.exists(relief): architecture.texture = load(relief)
		record.relief_ready = true
	occluders.clear()
	for node in map.get_node("Objects").get_children():
		if node is Sprite2D and node.has_meta("occluder"): occluders.append(node)
	# WorldView releases an in-flight transition after the retained body and
	# collision proxies are attached, and after any visible fade has completed.
	armed = false
	for exit in map.get_node("Exits").get_children():
		if bool(exit.get_meta("internal_gate", false)): continue
		(exit as Area2D).monitoring = true
		(exit as Area2D).body_entered.connect(_on_exit_entered.bind(exit))
		(exit as Area2D).body_exited.connect(_on_exit_left)
	var raw_access = field.snapshot.get("access", {})
	var maps = raw_access.get("maps", {}) if raw_access is Dictionary else {}
	_access = maps.duplicate(true) if maps is Dictionary else {}
	_apply_geometry_bindings(field.snapshot, false)
	_refresh_access_geometry()
	_build_grid()
	_build_walls()
	_touch_map(next)
	_trim_maps()

func spawn_point(spawn: String) -> Vector2:
	var spawns: Dictionary = GEOMETRY.map_data(key).spawns
	var id := spawn if spawn != "" else str(spawns.keys()[0])
	return GEOMETRY.point(spawns[id].point)

## Cross-map placement is only the authored doorway transform or a continuous seam.
## Missing authored geometry blocks the transition before the source map is removed.
func valid_transition(next: String, spawn: String, at: Vector2, use_at: bool) -> bool:
	if _route_target_next != next or not _map_records.has(next): return false
	var layout: Node2D = _map_records[next].node
	var spawns: Dictionary = GEOMETRY.map_data(next).spawns
	if not use_at and not spawns.has(spawn): return false
	var point: Vector2 = at if use_at else GEOMETRY.point(spawns[spawn].point)
	if use_at:
		if not map.has_meta("outdoor_origin") or not layout.has_meta("outdoor_origin") or not layout.has_meta("outdoor_core"): return false
		var source_world: Vector2 = field.player.position + (map.get_meta("outdoor_origin") as Vector2)
		var target_world: Vector2 = at + (layout.get_meta("outdoor_origin") as Vector2)
		if source_world.distance_to(target_world) > 0.01 or not (layout.get_meta("outdoor_core") as Rect2).has_point(target_world): return false
	var size: Vector2 = layout.get_meta("map_size", Vector2.ZERO)
	if not Rect2(Vector2.ZERO, size).has_point(point): return false
	# A doorway transform has an authored continuous footpoint. Check the same
	# inflated physical footprints as before, without snapping it to a grid cell.
	for rect: Rect2 in _blocked_rects.get(next, []):
		if rect.has_point(point): return false
	if field.execution_bridge != null and field.execution_bridge.occupied_at(field.player, field.execution_resource, next, point): return false
	return true

func stands() -> Array:
	var result := []
	for marker: Dictionary in GEOMETRY.map_data(key).get("stands", {}).values(): result.append(GEOMETRY.point(marker.point))
	return result

# ------------------------------------------------------------- movement ---

func _on_exit_entered(body: Node, exit: Area2D) -> void:
	if body != field.player or not armed or _route_target_next != str(exit.get_meta("target", "")): return
	armed = false
	_switching = true
	field._cancel_motion(field.player)
	field.call_deferred("load_map", String(exit.get_meta("target")), String(exit.get_meta("spawn")))

func _on_exit_left(body: Node) -> void:
	if body == field.player: armed = true

func physics_update(_delta: float) -> void:
	if map == null: return
	if not armed and not _inside_exit(): armed = true
	if armed and not _switching and _route_target_next != "":
		for exit in map.get_node("Exits").get_children():
			if str(exit.get_meta("target", "")) == _route_target_next and _contains_exit(exit):
				_on_exit_entered(field.player, exit)
				break
	_check_region()
	if not _switching: _last_allowed_position = field.player.position
	var head: Vector2 = field.player.global_position + Vector2(0, -24)
	for s in occluders:
		var behind: bool = field.player.position.y < s.position.y and s.get_rect().has_point(s.to_local(head))
		s.modulate.a = move_toward(s.modulate.a, 0.42 if behind else 1.0, 0.12)

func _inside_exit() -> bool:
	for exit in map.get_node("Exits").get_children():
		if _contains_exit(exit): return true
	return false

func _contains_exit(exit: Area2D) -> bool:
	# The logical point is measured from the real 3D body; the authored region
	# is a portal trigger, never an additional collision/movement simulation.
	for child in exit.get_children():
		if child is CollisionShape2D and child.shape is RectangleShape2D:
			if Rect2(-child.shape.size * 0.5, child.shape.size).has_point(child.to_local(field.player.global_position)): return true
	return false

func _check_region() -> void:
	if _switching or _route_target_next == "" or not map.has_meta("outdoor_core"): return
	var g: Vector2 = field.player.position + (map.get_meta("outdoor_origin") as Vector2)
	if (map.get_meta("outdoor_core") as Rect2).grow(SEAM).has_point(g): return
	var cores: Dictionary = map.get_meta("outdoor_cores")
	var origins: Dictionary = map.get_meta("outdoor_origins")
	for next in cores:
		if str(next) == _route_target_next and (cores[next] as Rect2).has_point(g):
			_switching = true
			field._cancel_motion(field.player)
			field.call_deferred("load_map", String(next), "", g - (origins[next] as Vector2), true)
			return

# -------------------------------------------------------------- geometry ---

## 碰撞体（墙与家具）栅格化为寻路网格：半格精度，人物脚底留出余量。每张图只建一次。
func _build_grid() -> void:
	_build_grid_for(key, map)

func _build_grid_for(map_name: String, layout: Node2D) -> void:
	if _grids.has(map_name): return
	var size: Vector2 = layout.get_meta("map_size", Vector2(1024, 1024))
	var grid := AStarGrid2D.new()
	grid.region = Rect2i(0, 0, int(size.x / 16.0), int(size.y / 16.0))
	grid.cell_size = Vector2(16, 16)
	grid.offset = Vector2(8, 8)
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	grid.update()
	_grids[map_name] = grid
	_blocked_rects[map_name] = []
	var regions: Array[Rect2i] = []
	var solids := layout.get_node_or_null("Solids")
	if solids != null:
		for shape_node in solids.get_children():
			var cs := shape_node as CollisionShape2D
			if cs == null or cs.disabled or not (cs.shape is RectangleShape2D): continue
			var half: Vector2 = (cs.shape as RectangleShape2D).size / 2.0
			var rect := Rect2(cs.position - half, half * 2.0).grow_individual(10, 1, 10, 14)
			_blocked_rects[map_name].append(rect)
			var x0 := maxi(0, int(ceil((rect.position.x - 8.0) / 16.0)))
			var y0 := maxi(0, int(ceil((rect.position.y - 8.0) / 16.0)))
			var x1 := mini(grid.region.size.x - 1, int(floor((rect.end.x - 8.0) / 16.0)))
			var y1 := mini(grid.region.size.y - 1, int(floor((rect.end.y - 8.0) / 16.0)))
			if x1 >= x0 and y1 >= y0:
				var r := Rect2i(x0, y0, x1 - x0 + 1, y1 - y0 + 1)
				grid.fill_solid_region(r, true)
				regions.append(r)
	_solid_regions[map_name] = regions
	var buckets := {}
	for rect: Rect2 in _blocked_rects[map_name]:
		var first := Vector2i((rect.position / 16.0).floor())
		var last := Vector2i((rect.end / 16.0).floor())
		for y in range(maxi(0, first.y), mini(grid.region.end.y - 1, last.y) + 1):
			for x in range(maxi(0, first.x), mini(grid.region.end.x - 1, last.x) + 1):
				var cell := Vector2i(x, y)
				if grid.is_point_solid(cell): continue
				if not buckets.has(cell): buckets[cell] = []
				buckets[cell].append(rect)
	_blocked_cells[map_name] = buckets

func nav_grid(walker: CharacterBody2D, writable := false) -> AStarGrid2D:
	var base: AStarGrid2D = _grids[key]
	var other_cells: Array[Vector2i] = []
	for position: Vector2 in field.occupied_positions(walker):
		var first := Vector2i(((position - Vector2(19, 15) - base.offset) / base.cell_size).ceil())
		var last := Vector2i(((position + Vector2(19, 15) - base.offset) / base.cell_size).floor())
		for y in range(first.y, last.y + 1):
			for x in range(first.x, last.x + 1):
				var cell := Vector2i(x, y)
				if base.region.has_point(cell) and not base.is_point_solid(cell):
					other_cells.append(cell)
	if other_cells.is_empty() and not writable:
		return base
	var grid := AStarGrid2D.new()
	grid.region = base.region
	grid.cell_size = base.cell_size
	grid.offset = base.offset
	grid.diagonal_mode = base.diagonal_mode
	grid.update()
	for r: Rect2i in _solid_regions.get(key, []):
		grid.fill_solid_region(r, true)
	for cell in other_cells:
		grid.set_point_solid(cell, true)
	return grid

func manual_exit_target(point: Vector2) -> String:
	if not armed or map == null: return ""
	for exit in map.get_node("Exits").get_children():
		var shape := exit.get_node_or_null("Shape") as CollisionShape2D
		if shape == null or not shape.shape is RectangleShape2D: continue
		var target := str(exit.get_meta("target", ""))
		var rectangle := Rect2(exit.position + shape.position - shape.shape.size / 2.0, shape.shape.size)
		if can_enter(target) and rectangle.has_point(point): return target
	return ""

## Only authored exit rectangles supply near-door interaction geometry.
## This query does not move bodies, change access or start a transition.
func manual_exit_targets_near(point: Vector2, radius: float) -> Array[String]:
	var targets: Array[String] = []
	if not armed or map == null: return targets
	for exit in map.get_node("Exits").get_children():
		var shape := exit.get_node_or_null("Shape") as CollisionShape2D
		if shape == null or not shape.shape is RectangleShape2D: continue
		var target := str(exit.get_meta("target", ""))
		if target == "" or not can_enter(target): continue
		var rectangle := Rect2(exit.position + shape.position - shape.shape.size / 2.0, shape.shape.size)
		var nearest := point.clamp(rectangle.position, rectangle.end)
		if point.distance_to(nearest) <= radius and target not in targets: targets.append(target)
	return targets

func walkable(p: Vector2, _walker: CharacterBody2D = null) -> bool:
	return _walkable_on_map(key, p)

func _walkable_on_map(map_name: String, p: Vector2) -> bool:
	var grid: AStarGrid2D = _grids.get(map_name)
	var cell := Vector2i((p / 16.0).floor())
	if grid == null or not grid.region.has_point(cell) or grid.is_point_solid(cell): return false
	for rect: Rect2 in _blocked_cells.get(map_name, {}).get(cell, []):
		if rect.has_point(p): return false
	return true

func local_nav_grid(walker: CharacterBody2D, room: String, destination_room: String) -> AStarGrid2D:
	var base: AStarGrid2D = _grids[key]
	if not _local_grids.has(key): _local_grids[key] = {}
	var cache: Dictionary = _local_grids[key]
	var token := room + "\n" + destination_room
	if not cache.has(token):
		# The room pair is a small geometric window. Avoid allocating the whole
		# residential floor for every mouse click or controller interaction.
		var bounds := Rect2()
		var found := false
		for candidate in [room, destination_room]:
			if candidate == "": continue
			var region: String = str(candidate) if key == "living_floor" else str(candidate).trim_prefix(key + ":")
			var cache_key: String = key + ":" + region
			if _stage_bounds_cache.has(cache_key):
				var part: Rect2 = _stage_bounds_cache[cache_key]
				bounds = bounds.merge(part) if found else part
				found = true
		var area := base.region
		if found:
			var start := Vector2i((bounds.position / 16.0).floor())
			var end := Vector2i((bounds.end / 16.0).ceil())
			area = Rect2i(start, end - start).intersection(base.region)
		var grid := AStarGrid2D.new()
		grid.region = area
		grid.cell_size = base.cell_size
		grid.offset = base.offset
		grid.diagonal_mode = base.diagonal_mode
		grid.update()
		for y in range(area.position.y, area.end.y):
			for x in range(area.position.x, area.end.x):
				var cell := Vector2i(x, y)
				grid.set_point_solid(cell, not _local_cell_admitted(base, cell, room, destination_room))
		if cache.size() >= MAX_LOCAL_NAV_GRIDS: cache.erase(cache.keys()[0])
		cache[token] = {"grid": grid, "occupied": []}
	var record: Dictionary = cache[token]
	var grid: AStarGrid2D = record.grid
	# Queries consume the grid synchronously and never mutate it. Restore only
	# the previous occupancy overlay; room masks and authored walls remain.
	for cell: Vector2i in record.occupied:
		grid.set_point_solid(cell, not _local_cell_admitted(base, cell, room, destination_room))
	var occupied: Array[Vector2i] = []
	var seen := {}
	for position: Vector2 in field.occupied_positions(walker):
		var first := Vector2i(((position - Vector2(19, 15) - grid.offset) / grid.cell_size).ceil())
		var last := Vector2i(((position + Vector2(19, 15) - grid.offset) / grid.cell_size).floor())
		for y in range(first.y, last.y + 1):
			for x in range(first.x, last.x + 1):
				var cell := Vector2i(x, y)
				if grid.region.has_point(cell) and not seen.has(cell):
					seen[cell] = true
					occupied.append(cell)
					grid.set_point_solid(cell, true)
	record.occupied = occupied
	return grid

func _local_cell_admitted(base: AStarGrid2D, cell: Vector2i, room: String, destination_room: String) -> bool:
	if not base.region.has_point(cell) or base.is_point_solid(cell): return false
	var candidate_room := room_of(base.get_point_position(cell))
	return candidate_room == room or candidate_room == destination_room

## 室外楼体挡人物视线；室内按当前房间显示。
func _build_walls() -> void:
	if not map.has_meta("outdoor_core") or _walls.has(key): return
	var walls := {}
	var solids := map.get_node_or_null("Solids")
	if solids:
		for shape_node in solids.get_children():
			var cs := shape_node as CollisionShape2D
			if cs == null or not (cs.shape is RectangleShape2D): continue
			var size: Vector2 = (cs.shape as RectangleShape2D).size
			var corner: Vector2 = cs.position - size / 2.0
			var wall := fmod(corner.x, 32.0) == 0.0 and fmod(corner.y, 32.0) == 0.0 and fmod(size.x, 32.0) == 0.0 and fmod(size.y, 32.0) == 0.0
			if not wall and not size.x * size.y >= 4096.0: continue
			for y in range(int(ceil((corner.y - 16.0) / 32.0)), int(floor((corner.y + size.y - 16.0) / 32.0)) + 1):
				for x in range(int(ceil((corner.x - 16.0) / 32.0)), int(floor((corner.x + size.x - 16.0) / 32.0)) + 1):
					walls[Vector2i(x, y)] = true
	_walls[key] = walls

func sight_origin() -> Vector2: return field.player.position + Vector2(0, -8)
func sight_entry_origin() -> Vector2: return field.player.position
func sight_interior(cell: Vector2i) -> bool:
	var rows: Array = map.get_meta("terrain_rows", [])
	if cell.y < 0 or cell.y >= rows.size(): return false
	var row: String = rows[cell.y]
	# S 是房间内部台阶；门道 D、墙顶 #、墙面 W/w 与 G 均不触发进入。
	return cell.x >= 0 and cell.x < row.length() and row.substr(cell.x, 1) in [".", "S"]
func sight_size() -> Vector2i:
	var size: Vector2 = map.get_meta("map_size", Vector2(1024, 1024))
	return Vector2i(ceili(size.x / 32.0), ceili(size.y / 32.0))
func sight_blocks(cell: Vector2i) -> bool: return _walls.get(key, {}).has(cell)
func sight_terrain_fog() -> bool: return not map.has_meta("outdoor_core")
func sight_point(p: Vector2) -> Vector2: return p if sight_terrain_fog() else p + Vector2(0, -8)

func sight_region(cell: Vector2i) -> String:
	return _region_in_map(key, cell)

static func _region_in_map(map_name: String, cell: Vector2i) -> String:
	var cell_size := float(GEOMETRY.contract().cell_size)
	var room: Dictionary = GEOMETRY.room_at(map_name, (Vector2(cell) + Vector2(0.5, 0.5)) * cell_size)
	return str(room.get("region", "void"))

static func _room_id(map_name: String, cell: Vector2i) -> String:
	var region := _region_in_map(map_name, cell)
	return region if map_name == "living_floor" else map_name + ":" + region

func room_of(p: Vector2) -> String: return _room_id(key, Vector2i((p / 32.0).floor()))
## A source-side threshold belongs to its departure room, but is not arrival.
func departure_room(p: Vector2) -> String:
	var entered := entered_room(p)
	if entered != "": return entered
	if _terrain_mark(Vector2i((p / 32.0).floor())) == "D" and _departure_footpoint_clear(p): return room_of(p)
	return ""

## Source identity uses the retained body's continuous footpoint. Its nav-cell
## centre can lie inside a neighbouring inflated wall although the body is clear.
## Keep the original inflation and all destination/navigation checks unchanged.
func _departure_footpoint_clear(p: Vector2) -> bool:
	var grid: AStarGrid2D = _grids.get(key)
	if grid == null or not grid.region.has_point(Vector2i((p / 16.0).floor())): return false
	for rect: Rect2 in _blocked_rects.get(key, []):
		if rect.has_point(p): return false
	return true

func _terrain_mark(cell: Vector2i) -> String:
	var rows: Array = map.get_meta("terrain_rows", []) if map else []
	if cell.y < 0 or cell.y >= rows.size(): return ""
	var row: String = rows[cell.y]
	return row.substr(cell.x, 1) if cell.x >= 0 and cell.x < row.length() else ""

## Doorway components expose only geometric neighbours; DV still admits actions.
func doorway_rooms(p: Vector2) -> Array[String]:
	var start := Vector2i((p / 32.0).floor())
	var result: Array[String] = []
	if _terrain_mark(start) != "D": return result
	if not _doorway_cache.has(key): _doorway_cache[key] = {}
	var cache: Dictionary = _doorway_cache[key]
	if cache.has(start): return cache[start]
	var pending: Array[Vector2i] = [start]
	var seen := {start: true}
	while not pending.is_empty():
		var cell: Vector2i = pending.pop_back()
		for offset in [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]:
			var adjacent: Vector2i = cell + offset
			var mark := _terrain_mark(adjacent)
			if mark == "D" and not seen.has(adjacent):
				seen[adjacent] = true
				pending.append(adjacent)
			elif mark in [".", "S"]:
				var room := room_of(Vector2(adjacent) * 32.0 + Vector2(16,16))
				if room not in result: result.append(room)
	# Door components are immutable authored geometry. Cache all their cells;
	# formal availability and access checks remain in the live action path.
	for cell in seen: cache[cell] = result
	return result

## An authored open floor boundary is a passage without a D marker. This query
## proves only the current adjacent crossing; it never moves or admits an actor.
func passage_at(source: Vector2, attempted: Vector2, from_room: String, to_room: String, walker: CharacterBody2D) -> Dictionary:
	if map == null or not is_instance_valid(walker) or from_room == "" or to_room == "" or from_room == to_room: return {}
	if walker.position != source or departure_room(source) != from_room or entered_room(source) != from_room or entered_room(attempted) != to_room: return {}
	var source_cell := Vector2i((source / 32.0).floor())
	var target_cell := Vector2i((attempted / 32.0).floor())
	var difference := (target_cell - source_cell).abs()
	if difference.x + difference.y != 1: return {}
	if _terrain_mark(source_cell) not in [".", "S"] or _terrain_mark(target_cell) not in [".", "S"]: return {}
	# One existing terrain cell is the maximum near-boundary probe. Navigation,
	# inflated obstacles and the real body's collision remain unchanged.
	if source.distance_to(attempted) > 32.0 or not walkable(source, walker) or not walkable(attempted, walker): return {}
	if walker.test_move(walker.global_transform, attempted - source): return {}
	return {"kind": "open-boundary", "map": key, "from_room": from_room, "to_room": to_room,
		"source_point": source, "target_point": attempted, "landing": attempted}

## A click on a threshold ends just inside the requested neighbour, never at
## an arbitrary room station. This is geometry only, not entry permission.
func doorway_landing(p: Vector2, target_room: String, walker: CharacterBody2D) -> Variant:
	if target_room not in doorway_rooms(p): return null
	var best := INF
	var found: Variant = null
	for point: Vector2 in _room_candidates.get(key, {}).get(target_room, {}).get("fallback", []):
		var distance := point.distance_squared_to(p)
		# Authored thresholds may be three 32-pixel tiles deep. Include the
		# first interior navigation centre even when clicking the near edge.
		if distance > 16384.0 or distance >= best: continue
		if entered_room(point) != target_room or not walkable(point, walker): continue
		if field.position_occupied(walker, point): continue
		best = distance
		found = point
	return found

func entered_room(p: Vector2) -> String:
	if map == null: return ""
	if map.has_meta("outdoor_core"):
		var size: Vector2 = map.get_meta("map_size", Vector2.ZERO)
		return room_of(p) if Rect2(Vector2.ZERO, size).has_point(p) and walkable(p) else ""
	return room_of(p) if sight_interior(Vector2i((p / 32.0).floor())) else ""
func room_name(_p: Vector2) -> String: return str(map.get_meta("title", key)) if map else ""

# ----------------------------------------------------------------- sites ---

func site_geometry(defs: Dictionary) -> Dictionary:
	var result := {}
	for id in GEOMETRY.markers(key, "sites"):
		if not defs.has(id) and id in DECOR_MARKERS: continue
		var marker: Dictionary = GEOMETRY.markers(key, "sites")[id]
		var point: Vector2 = GEOMETRY.point(marker.point)
		result[id] = {"point": point, "reach": float(marker.get("reach", 46.0)),
			"box": Rect2(point + Vector2(-16, -26), Vector2(32, 30)),
			"marker": point + Vector2(0, -16), "title": str(marker.get("label", id)), "fallback": not defs.has(id)}
	return result

func anchor(name: String) -> Variant:
	return GEOMETRY.anchor(key, name)

func furniture_hits(pos: Vector2) -> Dictionary:
	var names: Array = []
	for node in map.get_node("Objects").get_children():
		if node is not Sprite2D: continue
		var sprite := node as Sprite2D
		if sprite.texture == null or node.has_meta("occluder"): continue
		if sprite.get_rect().has_point(sprite.to_local(pos)): names.append(String(node.name))
	return {"nodes": names}
