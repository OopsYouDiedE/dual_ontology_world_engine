extends RefCounted
## Shared authoring data; actual room/door identities are declared in DV.
static var _data: Dictionary = {}

static func openings(identity: String, dimensions: Vector2) -> Dictionary:
	if _data.is_empty():
		_data = preload("res://scenes/Host/DvPresentationContent.cs").new().ReadRecord("res://Content/world3d.dv", "layouts", "world3d")
	var result: Dictionary = {}
	for side in _data.get("apertures", {}).get(identity, {}):
		result[side] = []
		for fraction in _data.apertures[identity][side]:
			result[side].append(float(fraction) * (dimensions.x if side in ["north", "south"] else dimensions.y))
	return result

static func grid_openings(identity: String, dimensions: Vector2i) -> Dictionary:
	var result := openings(identity, Vector2(dimensions))
	for side in result:
		var count: int = dimensions.x if side in ["north", "south"] else dimensions.y
		var cells: Array[int] = []
		for center in result[side]:
			for index in count:
				if absf(float(index) + 0.5 - count * 0.5 - float(center)) <= 0.8 and index not in cells: cells.append(index)
		result[side] = cells
	return result

static func prop_position(identity: String, prop: String, fallback: Vector3) -> Vector3:
	if _data.is_empty():
		_data = preload("res://scenes/Host/DvPresentationContent.cs").new().ReadRecord("res://Content/world3d.dv", "layouts", "world3d")
	var coordinates: Array = _data.get("prop_positions", {}).get(identity, {}).get(prop, [])
	return Vector3(coordinates[0], coordinates[1], coordinates[2]) if coordinates.size() == 3 else fallback
