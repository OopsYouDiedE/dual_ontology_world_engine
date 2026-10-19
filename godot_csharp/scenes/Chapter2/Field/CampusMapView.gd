extends Node2D
## 学园多地图的适配器：载入 Maps/ 下的地图，进门口自动换图；室外分区相互重叠，走出本区核心即换到所在区，
## 世界坐标连续。提供寻路网格（碰撞体栅格化，半格精度）、视线墙、房间区域、交互点标记、站位与遮挡淡出。
## 剧情与文字不在这里。

const MAPS := "res://scenes/Chapter2/Maps/%s.tscn"
const Bindings := preload("res://scenes/Shared/Field/PresentationBindings.gd")
const SEAM := 36.0
## 地图里没有标记、由地图层补上的交互点。
const EXTRA_SITES := {"zone_jia": {"litter": Vector2(440, 240)}}
## 只作装饰、不开放交互的标记。
const DECOR_MARKERS := ["transmitter", "isolation_gate", "lane", "harbor", "stele", "collapse", "counterweight"]

var field: Node2D
var map: Node2D
var key := ""
var armed := true
var _switching := false
var occluders: Array[Sprite2D] = []
var _grids: Dictionary = {}
var _blocked_rects: Dictionary = {}
var _walls: Dictionary = {}
var _seen: Dictionary = {}
var _access: Dictionary = {}
var _access_shapes: Dictionary = {}
var _last_allowed_position := Vector2.ZERO
var presentation: Dictionary = {}
var _player_sprite := ""
var _routes: Dictionary = {}
var _route_target_next := ""
var _geometry_signature := ""
var _geometry_nodes: Array[Node] = []
var _stage_bounds_cache: Dictionary = {}

func apply_snapshot(snapshot: Dictionary) -> void:
	_apply_geometry_bindings(snapshot)
	var next: Dictionary = snapshot.get("access", {}).get("maps", {})
	if next == _access: return
	_access = next.duplicate(true)
	if map: _refresh_access_geometry.call_deferred()

## Native content provides geometric bindings; no story identity or rule is selected here.
func _apply_geometry_bindings(snapshot: Dictionary, rebuild := true) -> void:
	if map == null: return
	var declaration: Dictionary = snapshot.get("geometry_bindings", {}).get(key, {})
	var signature := JSON.stringify(declaration)
	if signature == _geometry_signature: return
	_geometry_signature = signature
	for node in _geometry_nodes:
		if is_instance_valid(node):
			node.get_parent().remove_child(node)
			node.queue_free()
	_geometry_nodes.clear()
	var textures: Dictionary = {}
	for prop in declaration.get("props", []):
		var texture := load(str(prop.get("texture", ""))) as Texture2D
		if texture == null:
			Bindings.fail(self, "Missing native geometry texture: " + str(prop.get("id", "")))
			return
		textures[str(prop.id)] = texture
	for site in declaration.get("sites", []):
		var id := str(site.get("id", ""))
		if id == "" or map.get_node("Sites").has_node(NodePath(id)):
			Bindings.fail(self, "Duplicate or empty native geometry site: " + id)
			continue
		var marker := Marker2D.new()
		marker.name = id
		marker.position = Vector2(float(site.point[0]), float(site.point[1]))
		marker.set_meta("label", str(site.get("label", id)))
		map.get_node("Sites").add_child(marker)
		_geometry_nodes.append(marker)
	for prop in declaration.get("props", []):
		var texture: Texture2D = textures[str(prop.id)]
		var sprite := Sprite2D.new()
		sprite.name = str(prop.id)
		sprite.texture = texture
		sprite.position = Vector2(float(prop.position[0]), float(prop.position[1]))
		sprite.centered = false
		sprite.offset = Vector2(-texture.get_width() / 2.0, -texture.get_height())
		map.get_node("Objects").add_child(sprite)
		_geometry_nodes.append(sprite)
		if prop.has("footprint"):
			var rect: Array = prop.footprint
			var shape := RectangleShape2D.new()
			shape.size = Vector2(float(rect[2]), float(rect[3]))
			var solid := CollisionShape2D.new()
			solid.shape = shape
			solid.position = sprite.position + Vector2(float(rect[0]), float(rect[1])) + shape.size / 2.0
			map.get_node("Solids").add_child(solid)
			_geometry_nodes.append(solid)
	_grids.clear()
	if rebuild: _build_grid()

func can_enter(next: String) -> bool:
	return bool(_access.get(next, true))

func blocked_entry() -> void:
	_switching = false
	if map: field.player.position = _last_allowed_position

## A closed destination blocks the same doorway for keyboard movement and navigation.
## The authored exit shape supplies geometry; availability comes from the live snapshot.
func _refresh_access_geometry() -> void:
	if not is_instance_valid(map): return
	var solids := map.get_node_or_null("Solids")
	if solids == null: return
	for exit in map.get_node("Exits").get_children():
		var barrier: CollisionShape2D = _access_shapes.get(exit.name)
		if not is_instance_valid(barrier):
			var source := exit.get_node("Shape") as CollisionShape2D
			barrier = CollisionShape2D.new()
			barrier.name = "Access_" + str(exit.name)
			barrier.shape = source.shape
			barrier.position = exit.position + source.position
			barrier.disabled = can_enter(str(exit.get_meta("target")))
			solids.add_child(barrier)
			_access_shapes[exit.name] = barrier
		else:
			barrier.disabled = can_enter(str(exit.get_meta("target")))
	_grids.clear()
	_build_grid()

func setup() -> bool:
	_player_sprite = str(presentation.get("player_sprite", ""))
	if _player_sprite == "":
		Bindings.fail(self, "Missing player portrait binding")
		return false
	return true
func player_key() -> String: return str(presentation.player_actor)
func player_sprite() -> String: return _player_sprite
func fixed_actors() -> Dictionary: return {}
func objects_root() -> Node2D: return map.get_node("Objects") if map else self
func map_key() -> String: return key
func switching() -> bool: return _switching
func uses_daypart_tint() -> bool: return true
func camera_smoothing() -> bool: return true
func stage_bounds(p: Vector2) -> Rect2:
	if map == null or not sight_terrain_fog(): return camera_limits()
	var region := sight_region(Vector2i((p / 32.0).floor()))
	var cache_key := key + ":" + region
	if _stage_bounds_cache.has(cache_key): return _stage_bounds_cache[cache_key]
	var rows: Array = map.get_meta("terrain_rows", [])
	var bounds := Rect2(p, Vector2.ONE)
	var found := false
	for y in rows.size():
		var row: String = rows[y]
		for x in row.length():
			if sight_region(Vector2i(x, y)) == region:
				var cell := Rect2(Vector2(x, y) * 32.0, Vector2(32, 32))
				bounds = bounds.merge(cell) if found else cell
				found = true
	_stage_bounds_cache[cache_key] = bounds
	return bounds

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
func set_prop(_id: String, _state: String) -> void: pass
func station(target: String, who: String) -> Dictionary:
	var actor: CharacterBody2D = field.actors.get(who)
	if actor == null: return {}
	if target.begins_with("site:"):
		var geometry := site_geometry(field.snapshot.get("sites", {}))
		var id := target.substr(5)
		if not geometry.has(id): return {}
		return {"point": geometry[id].point, "radius": float(geometry[id].reach)}
	var best := INF
	var chosen := Vector2.ZERO
	var open_best := INF
	var open_point := Vector2.ZERO
	var grid := nav_grid(actor)
	for y in range(grid.region.size.y):
		for x in range(grid.region.size.x):
			var cell := Vector2i(x, y)
			if grid.is_point_solid(cell): continue
			var point := grid.get_point_position(cell)
			if entered_room(point) != target: continue
			var distance := point.distance_squared_to(actor.position)
			if distance < best:
				best = distance
				chosen = point
			# Prefer room interior with turning space rather than a narrow door recess.
			var can_turn := true
			for offset in [Vector2.LEFT * 16, Vector2.RIGHT * 16, Vector2.UP * 16, Vector2.DOWN * 16]:
				if entered_room(point + offset) != target or not walkable(point + offset, actor):
					can_turn = false
					break
			if can_turn and distance < open_best:
				open_best = distance
				open_point = point
	return {} if best == INF else {"point": open_point if open_best != INF else chosen, "radius": 0.0}

func _map_routes() -> Dictionary:
	if not _routes.is_empty(): return _routes
	for file in DirAccess.get_files_at("res://scenes/Chapter2/Maps"):
		if not file.ends_with(".tscn"): continue
		var name := file.get_basename()
		var layout: Node2D = (load(MAPS % name) as PackedScene).instantiate()
		var edges: Array = []
		for exit in layout.get_node("Exits").get_children():
			edges.append({"to": str(exit.get_meta("target")), "point": exit.position})
		if layout.has_meta("outdoor_core"):
			var core: Rect2 = layout.get_meta("outdoor_core")
			var cores: Dictionary = layout.get_meta("outdoor_cores")
			for next in cores:
				var other: Rect2 = cores[next]
				var shared := core.grow(1.0).intersection(other)
				if next == name or not shared.has_area(): continue
				var point := shared.get_center()
				point += (other.get_center() - point).normalized() * (SEAM + 24.0)
				edges.append({"to": str(next), "point": point - (layout.get_meta("outdoor_origin") as Vector2)})
		_routes[name] = edges
		layout.free()
	return _routes

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
			field._daily_report(who, serial, "blocked", room_of(actor.position), action)
			return
		var origin := key
		_route_target_next = str(edge.to)
		await field._follow_route(actor, edge.point)
		await get_tree().physics_frame
		await get_tree().process_frame
		_route_target_next = ""
		if field._closing or not is_instance_valid(actor): return
		if key == origin:
			field._daily_report(who, serial, "blocked", room_of(actor.position), action)
			return
	var anchor_name := str(destination.get("anchor", ""))
	if anchor_name != "":
		var point = anchor(anchor_name)
		if point != null: await field._follow_route(actor, point)
		if point == null or actor.position.distance_to(point) > 8.0:
			field._daily_report(who, serial, "blocked", room_of(actor.position), action)
			return
	var target := str(destination.get("departure_station", destination.get("room", "")))
	var execution_room := str(destination.get("execution_room", destination.get("room", "")))
	field.move_to_station(target, execution_room, who, serial, action)
func play_replay(_ev: Dictionary) -> void: pass

func load_map(next: String, _spawn: String) -> void:
	if not ResourceLoader.exists(MAPS % next): return
	var packed := load(MAPS % next) as PackedScene
	if packed == null: return
	if map:
		# 先停掉旧图出口的检测并断开信号，再释放旧图，避免物理回调里改动碰撞体。
		for exit in map.get_node("Exits").get_children():
			var area := exit as Area2D
			area.monitoring = false
			for c in area.body_entered.get_connections(): area.body_entered.disconnect(c.callable)
			for c in area.body_exited.get_connections(): area.body_exited.disconnect(c.callable)
		remove_child(map)
		map.queue_free()
	key = next
	_geometry_signature = ""
	_geometry_nodes.clear()
	_access_shapes.clear()
	map = packed.instantiate()
	add_child(map)
	occluders.clear()
	for node in map.get_node("Objects").get_children():
		if node is Sprite2D and node.has_meta("occluder"): occluders.append(node)
	_switching = false
	armed = false
	for exit in map.get_node("Exits").get_children():
		(exit as Area2D).body_entered.connect(_on_exit_entered.bind(exit))
		(exit as Area2D).body_exited.connect(_on_exit_left)
	_access = field.snapshot.get("access", {}).get("maps", {}).duplicate(true)
	_apply_geometry_bindings(field.snapshot, false)
	_refresh_access_geometry()
	_build_walls()

func spawn_point(spawn: String) -> Vector2:
	var marker := map.get_node_or_null("Spawns/" + spawn) as Marker2D
	if marker: return marker.position
	# 要的出生点这张图上没有：用图上自己的第一个出生点，不落到图外的固定坐标。
	var spawns := map.get_node_or_null("Spawns")
	if spawns and spawns.get_child_count() > 0: return (spawns.get_child(0) as Marker2D).position
	if map.has_meta("outdoor_core"):
		return (map.get_meta("outdoor_core") as Rect2).get_center() - (map.get_meta("outdoor_origin") as Vector2)
	return Vector2(64, 64)

## Cross-map placement is only the authored doorway transform or a continuous seam.
## Missing authored geometry blocks the transition before the source map is removed.
func valid_transition(next: String, spawn: String, at: Vector2, use_at: bool) -> bool:
	if _route_target_next != next or not ResourceLoader.exists(MAPS % next): return false
	var layout := (load(MAPS % next) as PackedScene).instantiate() as Node2D
	if layout == null: return false
	var marker: Marker2D = layout.get_node_or_null("Spawns/" + spawn) as Marker2D if spawn != "" else null
	var point := at if use_at else (marker.position if marker != null else Vector2.ZERO)
	var valid := use_at or marker != null
	if use_at:
		valid = map.has_meta("outdoor_origin") and layout.has_meta("outdoor_origin") and layout.has_meta("outdoor_core")
		if valid:
			var source_world: Vector2 = field.player.position + (map.get_meta("outdoor_origin") as Vector2)
			var target_world: Vector2 = at + (layout.get_meta("outdoor_origin") as Vector2)
			valid = source_world.distance_to(target_world) <= 0.01 and (layout.get_meta("outdoor_core") as Rect2).has_point(target_world)
	var size: Vector2 = layout.get_meta("map_size", Vector2.ZERO)
	valid = valid and Rect2(Vector2.ZERO, size).has_point(point)
	var solids := layout.get_node_or_null("Solids")
	if valid and solids != null:
		for node in solids.get_children():
			var shape := node as CollisionShape2D
			if shape == null or shape.disabled or shape.shape is not RectangleShape2D: continue
			var extent: Vector2 = (shape.shape as RectangleShape2D).size
			if Rect2(shape.position - extent / 2.0, extent).grow_individual(10, 1, 10, 14).has_point(point):
				valid = false
				break
	layout.free()
	return valid

func stands() -> Array:
	var result := []
	if map == null: return result
	var node := map.get_node_or_null("Stands")
	if node:
		for m in node.get_children(): result.append((m as Marker2D).position)
	return result

func door_point(fallback: Vector2) -> Vector2:
	if map == null: return fallback
	var spawns := map.get_node_or_null("Spawns")
	return (spawns.get_child(0) as Marker2D).position if spawns and spawns.get_child_count() > 0 else fallback

# ------------------------------------------------------------- movement ---

func _on_exit_entered(body: Node, exit: Area2D) -> void:
	if body != field.player or not armed or _route_target_next != str(exit.get_meta("target")): return
	armed = false
	_switching = true
	field._cancel_motion(field.player)
	field.call_deferred("load_map", String(exit.get_meta("target")), String(exit.get_meta("spawn")))

func _on_exit_left(body: Node) -> void:
	if body == field.player: armed = true

func physics_update(_delta: float) -> void:
	if map == null: return
	if not armed and not _inside_exit(): armed = true
	_check_region()
	if not _switching: _last_allowed_position = field.player.position
	var head: Vector2 = field.player.global_position + Vector2(0, -24)
	for s in occluders:
		var behind: bool = field.player.position.y < s.position.y and s.get_rect().has_point(s.to_local(head))
		s.modulate.a = move_toward(s.modulate.a, 0.42 if behind else 1.0, 0.12)

func _inside_exit() -> bool:
	for exit in map.get_node("Exits").get_children():
		if (exit as Area2D).overlaps_body(field.player): return true
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
	if _grids.has(key): return
	var size: Vector2 = map.get_meta("map_size", Vector2(1024, 1024))
	var grid := AStarGrid2D.new()
	grid.region = Rect2i(0, 0, int(size.x / 16.0), int(size.y / 16.0))
	grid.cell_size = Vector2(16, 16)
	grid.offset = Vector2(8, 8)
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	grid.update()
	_grids[key] = grid
	_blocked_rects[key] = []
	var solids := map.get_node_or_null("Solids")
	if solids == null: return
	for shape_node in solids.get_children():
		var cs := shape_node as CollisionShape2D
		if cs == null or cs.disabled or not (cs.shape is RectangleShape2D): continue
		var half: Vector2 = (cs.shape as RectangleShape2D).size / 2.0
		var rect := Rect2(cs.position - half, half * 2.0).grow_individual(10, 1, 10, 14)
		_blocked_rects[key].append(rect)
		var x0 := maxi(0, int(ceil((rect.position.x - 8.0) / 16.0)))
		var y0 := maxi(0, int(ceil((rect.position.y - 8.0) / 16.0)))
		var x1 := mini(grid.region.size.x - 1, int(floor((rect.end.x - 8.0) / 16.0)))
		var y1 := mini(grid.region.size.y - 1, int(floor((rect.end.y - 8.0) / 16.0)))
		if x1 >= x0 and y1 >= y0: grid.fill_solid_region(Rect2i(x0, y0, x1 - x0 + 1, y1 - y0 + 1), true)

func nav_grid(walker: CharacterBody2D) -> AStarGrid2D:
	var base: AStarGrid2D = _grids[key]
	var grid := AStarGrid2D.new()
	grid.region = base.region
	grid.cell_size = base.cell_size
	grid.offset = base.offset
	grid.diagonal_mode = base.diagonal_mode
	grid.update()
	for y in range(base.region.position.y, base.region.end.y):
		for x in range(base.region.position.x, base.region.end.x):
			var cell := Vector2i(x, y)
			if base.is_point_solid(cell): grid.set_point_solid(cell)
	for other in field.actors.values():
		if not is_instance_valid(other) or other == walker or other.get_parent() != walker.get_parent(): continue
		var first := Vector2i(((other.position - Vector2(19, 15) - grid.offset) / grid.cell_size).ceil())
		var last := Vector2i(((other.position + Vector2(19, 15) - grid.offset) / grid.cell_size).floor())
		for y in range(first.y, last.y + 1):
			for x in range(first.x, last.x + 1):
				var cell := Vector2i(x, y)
				if grid.region.has_point(cell): grid.set_point_solid(cell)
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

func walkable(p: Vector2, _walker: CharacterBody2D = null) -> bool:
	var grid: AStarGrid2D = _grids.get(key)
	var c := Vector2i(p / 16.0)
	if grid == null or not grid.region.has_point(c) or grid.is_point_solid(c): return false
	for rect: Rect2 in _blocked_rects.get(key, []):
		if rect.has_point(p): return false
	return true

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
	# 分区坐标直接对应 chapter2_art/rooms.py 的原生房间格；东区为水平镜像。
	if key in ["zone_yi", "zone_jia", "zone_ding", "zone_bing"]:
		var x := 35 - cell.x if key in ["zone_yi", "zone_ding"] else cell.x
		if x >= 30: return "wc" if cell.y < 7 else ("store" if cell.y >= 14 else "foyer")
		if x >= 22: return "hall" if cell.y < 14 else "prep"
		if cell.y >= 9 and cell.y < 14: return "corridor"
		return ("north_" if cell.y < 9 else "south_") + str(clampi(floori(float(x) / 7.0), 0, 2))
	match key:
		"living_hall": return "bath" if cell.y >= 32 else "hall"
		"archive":
			if cell.x >= 16: return "works"
			return "vault" if cell.y < 9 else "foyer"
		"resonance": return "silent" if cell.x >= 22 else "hall"
		"clinic": return "safety" if cell.x >= 20 else "ward"
		"logistics": return "kitchen" if cell.x < 13 else "dining"
	return key

func room_of(p: Vector2) -> String: return key + ":" + sight_region(Vector2i((p / 32.0).floor()))
## A source-side threshold belongs to its departure room, but is not arrival.
func departure_room(p: Vector2) -> String:
	var entered := entered_room(p)
	if entered != "": return entered
	if walkable(p) and _terrain_mark(Vector2i((p / 32.0).floor())) == "D": return room_of(p)
	return ""

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
	return result

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
	if map == null: return result
	var markers := map.get_node_or_null("Sites")
	if markers:
		for m in markers.get_children():
			var id := String(m.name)
			if not defs.has(id) and id in DECOR_MARKERS: continue
			var point := (m as Marker2D).position
			result[id] = {"point": point, "reach": 46.0, "box": Rect2(point + Vector2(-16, -26), Vector2(32, 30)),
				"marker": point + Vector2(0, -16), "title": str(m.get_meta("label", id)), "fallback": not defs.has(id)}
	for id in EXTRA_SITES.get(key, {}):
		var point: Vector2 = EXTRA_SITES[key][id]
		result[id] = {"point": point, "reach": 46.0, "box": Rect2(point + Vector2(-16, -26), Vector2(32, 30)), "marker": point + Vector2(0, -16)}
	return result

func anchor(name: String):
	if name.begins_with("site:"):
		var marker := map.get_node_or_null("Sites/" + name.substr(5)) as Marker2D
		return marker.position if marker else null
	var spawn := map.get_node_or_null("Spawns/" + name) as Marker2D
	return spawn.position if spawn else null

func furniture_hits(pos: Vector2) -> Dictionary:
	var names: Array = []
	for node in map.get_node("Objects").get_children():
		if node is not Sprite2D: continue
		var sprite := node as Sprite2D
		if sprite.texture == null or node.has_meta("occluder"): continue
		if sprite.get_rect().has_point(sprite.to_local(pos)): names.append(String(node.name))
	return {"nodes": names}
