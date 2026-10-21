extends RefCounted
## Tool-only scene/contract comparison for the map authoring workspace.
const GEOMETRY = preload("res://scenes/Host/GeometryCatalog.gd")

## Used by the authoring sandbox before bind_layout changes the scene. Reports
## the complete contract relation, including unexpected nodes and coordinates.
static func validate_layout(map_id: String, layout: Node2D) -> Array[String]:
	var errors: Array[String] = []
	var item := GEOMETRY.map_data(map_id)
	if Vector2(layout.get_meta("map_size")) != GEOMETRY.point(item.size): errors.append("map_size")
	var groups := {"sites": "Sites", "stations": "Stations", "stands": "Stands", "spawns": "Spawns"}
	for group in groups:
		var expected := GEOMETRY.markers(map_id, str(group))
		var root: Node = layout.get_node(str(groups[group]))
		for id in expected:
			var marker: Dictionary = expected[id]
			var path := str(marker.get("path", str(groups[group]) + "/" + str(id)))
			var node := layout.get_node_or_null(path) as Marker2D
			if node == null:
				errors.append("missing " + path)
				continue
			if node.position.distance_to(GEOMETRY.point(marker.point)) > 0.01: errors.append("point " + path)
			if marker.has("label") and str(node.get_meta("label", "")) != str(marker.label): errors.append("label " + path)
			if marker.has("room") and str(GEOMETRY.room_at(map_id, GEOMETRY.point(marker.point)).get("id", "")) != str(marker.room): errors.append("room " + path)
		for node in root.get_children():
			if node is Marker2D and not expected.has(str(node.name)): errors.append("unexpected " + str(groups[group]) + "/" + str(node.name))
	var exit_ids := {}
	for exit: Dictionary in item.exits:
		exit_ids[str(exit.id)] = true
		var area := layout.get_node_or_null(str(exit.path)) as Area2D
		if area == null:
			errors.append("missing " + str(exit.path))
			continue
		var target := str(area.get_meta("target", ""))
		var spawn := str(area.get_meta("spawn", ""))
		if target == "living_hall":
			target = "living_floor"
			spawn = "living_hall_" + spawn
		if target != str(exit.target) or spawn != str(exit.spawn): errors.append("route " + str(exit.id))
		if bool(area.get_meta("internal_gate", false)) != bool(exit.internal_gate): errors.append("internal_gate " + str(exit.id))
		if str(area.get_meta("gate", "")) != str(exit.get("gate", "")): errors.append("gate " + str(exit.id))
		var shape := area.get_node_or_null("Shape") as CollisionShape2D
		if shape == null or not shape.shape is RectangleShape2D:
			errors.append("shape " + str(exit.id))
		else:
			var bounds := Rect2(area.position + shape.position - shape.shape.size / 2.0, shape.shape.size)
			if not bounds.is_equal_approx(GEOMETRY.rect(exit.rect)) or area.position.distance_to(GEOMETRY.point(exit.point)) > 0.01: errors.append("exit geometry " + str(exit.id))
		if target != "" and not bool(exit.internal_gate):
			if not GEOMETRY.contract().maps.has(target): errors.append("unknown target " + target)
			elif not GEOMETRY.map_data(target).spawns.has(spawn): errors.append("unknown spawn " + target + "/" + spawn)
	for node in layout.get_node("Exits").get_children():
		if node is Area2D and not exit_ids.has(str(node.name)): errors.append("unexpected exit " + str(node.name))
	for key in ["outdoor_origin", "outdoor_core", "outdoor_cores", "outdoor_origins"]:
		if item.has(key) != layout.has_meta(key): errors.append("metadata " + key)
	if item.has("outdoor_origin"):
		if Vector2(layout.get_meta("outdoor_origin")) != GEOMETRY.point(item.outdoor_origin): errors.append("outdoor_origin")
		if Rect2(layout.get_meta("outdoor_core")) != GEOMETRY.rect(item.outdoor_core): errors.append("outdoor_core")
		for key in ["outdoor_cores", "outdoor_origins"]:
			var authored: Dictionary = layout.get_meta(key)
			if authored.size() != item[key].size(): errors.append(key + " inventory")
			for id in item[key]:
				var value: Variant = GEOMETRY.rect(item[key][id]) if key == "outdoor_cores" else GEOMETRY.point(item[key][id])
				if authored.get(id) != value: errors.append(key + "/" + str(id))
	var room_ids := {}
	var map_bounds := Rect2(Vector2.ZERO, GEOMETRY.point(item.size))
	for room: Dictionary in item.rooms:
		if room_ids.has(str(room.id)): errors.append("duplicate room " + str(room.id))
		room_ids[str(room.id)] = true
		var combined := Rect2()
		for raw: Array in room.rects:
			var part := GEOMETRY.rect(raw)
			if not part.has_area() or not map_bounds.encloses(part): errors.append("room bounds " + str(room.id))
			combined = part if combined.size == Vector2.ZERO else combined.merge(part)
		if combined != GEOMETRY.rect(room.rect): errors.append("room union " + str(room.id))
	for door: Dictionary in item.doors:
		if not room_ids.has(str(door.from)): errors.append("door source " + str(door.id))
		if not door.has("target_map") and not room_ids.has(str(door.to)): errors.append("door destination " + str(door.id))
		if not map_bounds.has_point(GEOMETRY.point(door.point)): errors.append("door bounds " + str(door.id))
		if door.has("target_map") and not exit_ids.has(str(door.id)): errors.append("door exit " + str(door.id))
		if not door.has("target_map"):
			var cell := Vector2i((GEOMETRY.point(door.point) / float(GEOMETRY.contract().cell_size)).floor())
			var rows: Array = layout.get_meta("terrain_rows", [])
			if cell.y < 0 or cell.y >= rows.size() or cell.x < 0 or cell.x >= str(rows[cell.y]).length() or str(rows[cell.y]).substr(cell.x, 1) != "D": errors.append("door terrain " + str(door.id))
	return errors
