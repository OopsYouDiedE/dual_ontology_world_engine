extends RefCounted
## Far-field movement uses published room adjacency, never a hidden physics world.
const GEOMETRY = preload("res://scenes/components/maps/GeometryCatalog.gd")
static var _edges: Dictionary = {}

static func invalidate_published() -> void:
	_edges.clear()

static func _connect_rooms(a: String, b: String, point: Vector2, target_point: Vector2) -> void:
	if a.is_empty() or b.is_empty() or a == b: return
	if not _edges.has(a): _edges[a] = []
	if not _edges.has(b): _edges[b] = []
	_edges[a].append({"to": b, "point": point, "target_point": target_point})
	_edges[b].append({"to": a, "point": target_point, "target_point": point})

static func _load() -> void:
	if not _edges.is_empty(): return
	for map_key in GEOMETRY.map_ids():
		var map_id := str(map_key)
		var data: Dictionary = GEOMETRY.map_data(map_id)
		for room: Dictionary in data.get("rooms", []): _edges[str(room.id)] = []
	var spatial: Dictionary = preload("res://scenes/main/DvPresentationContent.cs").new().ReadRecord("res://assets/data/world3d.dv", "layouts", "world3d")
	# All nodes exist before adding reverse edges.
	for map_key in GEOMETRY.map_ids():
		var map_id := str(map_key)
		var data: Dictionary = GEOMETRY.map_data(map_id)
		var passages: Array = data.get("doors", []).duplicate()
		passages.append_array(spatial.get("passages", {}).get(map_id, []))
		for door: Dictionary in passages:
			var a := str(door.get("from", ""))
			var b := str(door.get("to", ""))
			if _edges.has(a) and not GEOMETRY.room_data(map_id, b).is_empty():
				_connect_rooms(a, b, GEOMETRY.point(door.point), GEOMETRY.point(door.point))
			elif _edges.has(a) and door.has("target_map"):
				for exit: Dictionary in data.get("exits", []):
					if str(exit.id) != str(door.id): continue
					var target: Dictionary = GEOMETRY.map_data(str(exit.target)).get("spawns", {}).get(str(exit.spawn), {})
					if _edges.has(str(target.get("room", ""))):
						_connect_rooms(a, str(target.room), GEOMETRY.point(exit.point), GEOMETRY.point(target.point))
		# Continuous outdoor seams are declared by the shared outdoor cores.
		for route: Dictionary in GEOMETRY.routes().get(map_id, []):
			if route.has("stair_route"):
				var connection: Dictionary = route.stair_route
				var end: Array = connection.end_design
				var target_point := preload("res://scenes/components/world/LegacyDvMap.gd").design_to_pixel(str(connection.to_map),Vector3(end[0],end[1],end[2]))
				_connect_rooms(str(connection.from_room),str(connection.to_room),route.point,target_point)
				continue
			if not bool(route.get("continuous", false)): continue
			var from_point: Vector2 = route.point
			var target_point := from_point + GEOMETRY.point(data.outdoor_origin) - GEOMETRY.point(GEOMETRY.map_data(str(route.to)).outdoor_origin)
			var from_room := GEOMETRY.room_at(map_id, from_point)
			var to_room := GEOMETRY.room_at(str(route.to), target_point)
			_connect_rooms(str(from_room.get("id", "")), str(to_room.get("id", "")), from_point, target_point)

static func path(from_room: String, to_room: String) -> Array:
	_load()
	if not _edges.has(from_room) or not _edges.has(to_room): return []
	var queue: Array = [from_room]
	var parents := {from_room: ""}
	var cursor := 0
	while cursor < queue.size():
		var current: String = queue[cursor]
		cursor += 1
		if current == to_room: break
		for edge: Dictionary in _edges[current]:
			var next := str(edge.to)
			if parents.has(next): continue
			parents[next] = current
			queue.append(next)
	if not parents.has(to_room): return []
	var result: Array = [to_room]
	var node := to_room
	while node != from_room:
		node = str(parents[node])
		result.push_front(node)
	return result

static func approach(motion: Dictionary, map_id: String, room_id: String) -> Variant:
	var source: Dictionary = motion.get("from", {})
	var target: Dictionary = motion.get("to", {})
	if str(source.get("map", "")) != map_id or str(source.get("execution_room", "")) != room_id: return null
	var rooms := path(room_id, str(target.get("execution_room", "")))
	if rooms.size() < 2: return null
	var raw: Dictionary = GEOMETRY.room_data(map_id, room_id)
	if raw.is_empty(): return null
	var start := GEOMETRY.rect(raw.rects[0]).get_center()
	for edge: Dictionary in _edges[room_id]:
		if str(edge.to) == str(rooms[1]):
			return start.lerp(edge.point, clampf(float(motion.get("progress", 0)), 0.0, 1.0))
	return null
