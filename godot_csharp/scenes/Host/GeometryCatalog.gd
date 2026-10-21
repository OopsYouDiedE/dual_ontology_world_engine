extends RefCounted
## Semantic geometry has one published source. Scenes supply artwork/colliders;
## all maps, routes, room regions, exits and actor anchors use this catalogue.
const PATH := "res://Contracts/geometry_contract.json"
static var _contract: Dictionary = {}
static var _routes: Dictionary = {}

static func contract() -> Dictionary:
	if _contract.is_empty(): _contract = JSON.parse_string(FileAccess.get_file_as_string(PATH))
	return _contract

static func map_data(map_id: String) -> Dictionary:
	return contract().maps.get(map_id, {})

static func additions(map_id: String) -> Dictionary:
	return contract().additions.get(map_id, {})

static func point(raw: Array) -> Vector2:
	return Vector2(float(raw[0]), float(raw[1]))

static func rect(raw: Array) -> Rect2:
	return Rect2(float(raw[0]), float(raw[1]), float(raw[2]), float(raw[3]))

static func markers(map_id: String, group: String) -> Dictionary:
	var result: Dictionary = map_data(map_id).get(group, {}).duplicate(false)
	if group == "sites":
		for site: Dictionary in additions(map_id).get("sites", []): result[str(site.id)] = site
	return result

static func room_at(map_id: String, at: Vector2) -> Dictionary:
	var rooms: Array = map_data(map_id).get("rooms", [])
	if rooms.size() == 1: return rooms[0]
	for room: Dictionary in rooms:
		for raw: Array in room.rects:
			if rect(raw).has_point(at): return room
	return {}

static func room_data(map_id: String, room_id: String) -> Dictionary:
	for room: Dictionary in map_data(map_id).get("rooms", []):
		if str(room.id) == room_id: return room
	return {}

static func anchor(map_id: String, name: String) -> Variant:
	var groups: Array[String] = ["stations", "sites", "spawns"]
	if name.begins_with("slot:"):
		groups = ["stations", "sites", "stands"]
		name = name.trim_prefix("slot:")
	elif name.begins_with("site:"):
		groups = ["sites", "stations"]
		name = name.trim_prefix("site:")
	for group: String in groups:
		var items := markers(map_id, group)
		if items.has(name): return point(items[name].point)
	return null

static func routes() -> Dictionary:
	if not _routes.is_empty(): return _routes
	for map_id in contract().maps:
		var item: Dictionary = map_data(str(map_id))
		var edges: Array = []
		for exit: Dictionary in item.exits:
			if not bool(exit.internal_gate) and str(exit.target) != "":
				edges.append({"to": str(exit.target), "point": point(exit.point), "exit": str(exit.id)})
		# Outdoor cores tile the shared world. Their shared edge gives the
		# seam destination; this is derived from the same published rectangles.
		if item.has("outdoor_core"):
			var current := rect(item.outdoor_core)
			for next in item.outdoor_cores:
				if next == map_id: continue
				var target := rect(item.outdoor_cores[next])
				var overlap := current.grow(0.01).intersection(target.grow(0.01))
				if overlap.size.x <= 0.02 and overlap.size.y <= 0.02: continue
				if not overlap.has_area(): continue
				var heading := (target.get_center() - current.get_center()).normalized()
				var seam := overlap.get_center() + heading * 40.0 - point(item.outdoor_origin)
				edges.append({"to": str(next), "point": seam, "continuous": true})
		_routes[str(map_id)] = edges
	return _routes

static func bind_layout(map_id: String, layout: Node2D) -> void:
	var item := map_data(map_id)
	layout.set_meta("map_size", point(item.size))
	if item.has("outdoor_origin"):
		layout.set_meta("outdoor_origin", point(item.outdoor_origin))
		layout.set_meta("outdoor_core", rect(item.outdoor_core))
		var cores := {}
		var origins := {}
		for id in item.outdoor_cores: cores[id] = rect(item.outdoor_cores[id])
		for id in item.outdoor_origins: origins[id] = point(item.outdoor_origins[id])
		layout.set_meta("outdoor_cores", cores)
		layout.set_meta("outdoor_origins", origins)
	for exit: Dictionary in item.exits:
		var area: Area2D = layout.get_node(str(exit.path))
		var shape: CollisionShape2D = area.get_node("Shape")
		area.position = point(exit.point)
		area.set_meta("target", str(exit.target))
		area.set_meta("spawn", str(exit.spawn))
		area.set_meta("internal_gate", bool(exit.internal_gate))
		if exit.has("gate"): area.set_meta("gate", str(exit.gate))
		var bounds := rect(exit.rect)
		shape.position = bounds.get_center() - area.position
		var rectangle := RectangleShape2D.new()
		rectangle.size = bounds.size
		shape.shape = rectangle

