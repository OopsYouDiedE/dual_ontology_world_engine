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
var _walls: Dictionary = {}
var _seen: Dictionary = {}
var _access: Dictionary = {}
var _access_shapes: Dictionary = {}
var _last_allowed_position := Vector2.ZERO
var presentation: Dictionary = {}
var _player_sprite := ""
var _routes: Dictionary = {}

func apply_snapshot(snapshot: Dictionary) -> void:
	var next: Dictionary = snapshot.get("access", {}).get("maps", {})
	if next == _access: return
	_access = next.duplicate(true)
	if map: _refresh_access_geometry.call_deferred()

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
func camera_point(p: Vector2) -> Vector2: return p
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
	var best := INF
	var chosen := Vector2.ZERO
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
	return {} if best == INF else {"point": chosen, "radius": 0.0}

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
		await field._follow_route(actor, edge.point)
		await get_tree().physics_frame
		await get_tree().process_frame
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
	field.execute({"kind": "stage", "op": "daily_go", "who": who, "target": str(destination.room), "room": str(destination.room), "serial": serial, "action": action})
func play_replay(_ev: Dictionary) -> void: pass

func load_map(next: String, _spawn: String) -> void:
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
	_access_shapes.clear()
	map = (load(MAPS % next) as PackedScene).instantiate()
	add_child(map)
	occluders.clear()
	for node in map.get_node("Objects").get_children():
		if node is Sprite2D and node.has_meta("occluder"): occluders.append(node)
	_switching = false
	armed = false
	for exit in map.get_node("Exits").get_children():
		(exit as Area2D).body_entered.connect(_on_exit_entered.bind(exit))
		(exit as Area2D).body_exited.connect(_on_exit_left)
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
	if body != field.player or not armed: return
	armed = false
	_switching = true
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
	if _switching or not map.has_meta("outdoor_core"): return
	var g: Vector2 = field.player.position + (map.get_meta("outdoor_origin") as Vector2)
	if (map.get_meta("outdoor_core") as Rect2).grow(SEAM).has_point(g): return
	var cores: Dictionary = map.get_meta("outdoor_cores")
	var origins: Dictionary = map.get_meta("outdoor_origins")
	for next in cores:
		if (cores[next] as Rect2).has_point(g):
			_switching = true
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
	var solids := map.get_node_or_null("Solids")
	if solids == null: return
	for shape_node in solids.get_children():
		var cs := shape_node as CollisionShape2D
		if cs == null or cs.disabled or not (cs.shape is RectangleShape2D): continue
		var half: Vector2 = (cs.shape as RectangleShape2D).size / 2.0
		var rect := Rect2(cs.position - half, half * 2.0).grow_individual(10, 1, 10, 14)
		var x0 := maxi(0, int(ceil((rect.position.x - 8.0) / 16.0)))
		var y0 := maxi(0, int(ceil((rect.position.y - 8.0) / 16.0)))
		var x1 := mini(grid.region.size.x - 1, int(floor((rect.end.x - 8.0) / 16.0)))
		var y1 := mini(grid.region.size.y - 1, int(floor((rect.end.y - 8.0) / 16.0)))
		if x1 >= x0 and y1 >= y0: grid.fill_solid_region(Rect2i(x0, y0, x1 - x0 + 1, y1 - y0 + 1), true)

func nav_grid(_walker: CharacterBody2D) -> AStarGrid2D: return _grids[key]

func walkable(p: Vector2, _walker: CharacterBody2D = null) -> bool:
	var grid: AStarGrid2D = _grids.get(key)
	var c := Vector2i(p / 16.0)
	return grid != null and grid.region.has_point(c) and not grid.is_point_solid(c)

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
func entered_room(p: Vector2) -> String: return room_of(p) if sight_interior(Vector2i((p / 32.0).floor())) else ""
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
