extends RefCounted

const PATH := "res://Contracts/geometry_contract.json"
static var _contract: Dictionary = {}

static func contract() -> Dictionary:
	if _contract.is_empty():
		_contract = JSON.parse_string(FileAccess.get_file_as_string(PATH))
	return _contract

static func map_data(map_id: String) -> Dictionary:
	return contract().maps.get(map_id, {})

static func additions(map_id: String) -> Dictionary:
	return contract().additions.get(map_id, {})

static func room_at(map_id: String, point: Vector2) -> Dictionary:
	var rooms: Array = map_data(map_id).get("rooms", [])
	if rooms.size() == 1: return rooms[0]
	for room: Dictionary in rooms:
		for raw: Array in room.rects:
			if Rect2(float(raw[0]), float(raw[1]), float(raw[2]), float(raw[3])).has_point(point):
				return room
	return {}

static func room_data(map_id: String, room_id: String) -> Dictionary:
	for room: Dictionary in map_data(map_id).get("rooms", []):
		if str(room.id) == room_id: return room
	return {}
