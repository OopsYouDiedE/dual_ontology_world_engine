extends SceneTree
## Physical passage checks for the published living_floor collision scene.
## Runs in the dedicated tools project. No DV session or gameplay access is opened.
## Optional user argument: --door=<contract door ID>. Without it, visit every door.
## Internal doors move the actual foot body both ways; exterior doors cover only
## the local approach to the exit, not destination-map transfer or action receipts.

const CONTRACT_PATH := "res://Contracts/geometry_contract.json"
const NAV_STEP := 16.0
const NAV_HALF := Vector2(8, 8)
const DIRECTIONS := [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]

var layout: Node2D
var walker: CharacterBody2D
var foot: RectangleShape2D
var map_data: Dictionary
var rows: Array
var terrain_size := 32.0
var chosen_door := ""

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--door="):
			chosen_door = argument.trim_prefix("--door=")
		else:
			push_error("Unknown living-floor argument: " + argument)
			quit(2)
			return
	_run.call_deferred()

func _run() -> void:
	var contract: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(CONTRACT_PATH))
	map_data = contract.maps.living_floor
	terrain_size = float(contract.cell_size)
	var packed := load(str(map_data.scene)) as PackedScene
	if packed == null:
		push_error("Cannot load living_floor scene")
		quit(1)
		return
	layout = packed.instantiate()
	root.add_child(layout)
	rows = layout.get_meta("terrain_rows")
	walker = CharacterBody2D.new()
	walker.name = "LivingFloorPassageBody"
	walker.collision_layer = 2
	walker.collision_mask = 1
	walker.safe_margin = 0.08
	foot = RectangleShape2D.new()
	foot.size = Vector2(16, 12)
	var collision := CollisionShape2D.new()
	collision.shape = foot
	collision.position = Vector2(0, -6)
	walker.add_child(collision)
	layout.add_child(walker)
	await physics_frame
	await physics_frame

	var results: Array[Dictionary] = []
	var passed := true
	for raw: Dictionary in map_data.doors:
		if chosen_door != "" and str(raw.id) != chosen_door:
			continue
		var result := await _exercise(raw)
		results.append(result)
		if not bool(result.passed):
			passed = false
	if results.is_empty():
		passed = false
		push_error("No contract door selected: " + chosen_door)
	print(JSON.stringify({
		"scope": "living-floor-published-collision-passages",
		"foot_size": [16, 12], "foot_offset": [0, -6],
		"world_entered": true, "physical_move_method": "CharacterBody2D.move_and_collide",
		"access_state": "published scene only; live access barriers not installed",
		"dv_session": "not executed", "cross_map_transfer": "not executed",
		"results": results, "passed": passed
	}))
	layout.queue_free()
	quit(0 if passed else 1)

func _mark(cell: Vector2i) -> String:
	if cell.y < 0 or cell.y >= rows.size():
		return ""
	var row: String = rows[cell.y]
	return row.substr(cell.x, 1) if cell.x >= 0 and cell.x < row.length() else ""

func _room(point: Vector2) -> String:
	for room: Dictionary in map_data.rooms:
		for rectangle: Array in room.rects:
			if Rect2(float(rectangle[0]), float(rectangle[1]), float(rectangle[2]), float(rectangle[3])).has_point(point):
				return str(room.id)
	return ""

func _clear(point: Vector2) -> bool:
	var query := PhysicsShapeQueryParameters2D.new()
	query.shape = foot
	query.transform = Transform2D(0.0, point + Vector2(0, -6))
	query.collision_mask = walker.collision_mask
	query.exclude = [walker.get_rid()]
	query.collide_with_areas = false
	query.collide_with_bodies = true
	return walker.get_world_2d().direct_space_state.intersect_shape(query, 1).is_empty()

func _door_component(point: Vector2) -> Dictionary:
	var first := Vector2i((point / terrain_size).floor())
	if _mark(first) != "D":
		return {}
	var seen := {first: true}
	var pending: Array[Vector2i] = [first]
	var cursor := 0
	while cursor < pending.size():
		var cell: Vector2i = pending[cursor]
		for direction: Vector2i in DIRECTIONS:
			var adjacent := cell + direction
			if _mark(adjacent) == "D" and not seen.has(adjacent):
				seen[adjacent] = true
				pending.append(adjacent)
		cursor += 1
	return seen

func _route(door: Dictionary) -> PackedVector2Array:
	var point := Vector2(float(door.point[0]), float(door.point[1]))
	var component := _door_component(point)
	if component.is_empty():
		return PackedVector2Array()
	var extent := Vector2(float(map_data.size[0]), float(map_data.size[1]))
	var radius := terrain_size * 4.0
	var first := Vector2i(((point - Vector2.ONE * radius) / NAV_STEP).floor()).max(Vector2i.ZERO)
	var last := Vector2i(((point + Vector2.ONE * radius) / NAV_STEP).ceil()).min(Vector2i((extent / NAV_STEP).floor()) - Vector2i.ONE)
	var grid := AStarGrid2D.new()
	grid.region = Rect2i(first, last - first + Vector2i.ONE)
	grid.cell_size = Vector2.ONE * NAV_STEP
	grid.offset = NAV_HALF
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	grid.update()
	grid.fill_solid_region(grid.region, true)
	var sources: Array[Vector2i] = []
	var targets: Array[Vector2i] = []
	var thresholds: Array[Vector2i] = []
	var exterior := door.has("target_map")
	for y in range(first.y, last.y + 1):
		for x in range(first.x, last.x + 1):
			var cell := Vector2i(x, y)
			var feet := grid.get_point_position(cell)
			var terrain := Vector2i((feet / terrain_size).floor())
			var threshold := component.has(terrain)
			var room := _room(feet)
			var interior := _mark(terrain) in [".", "S"]
			var admitted := threshold or (interior and (room == str(door.from) or (not exterior and room == str(door.to))))
			if not admitted or not _clear(feet):
				continue
			grid.set_point_solid(cell, false)
			if threshold:
				thresholds.append(cell)
			elif interior and room == str(door.from):
				sources.append(cell)
			elif interior and not exterior and room == str(door.to):
				targets.append(cell)
	var nearer := func(a: Vector2i, b: Vector2i) -> bool:
		return grid.get_point_position(a).distance_squared_to(point) < grid.get_point_position(b).distance_squared_to(point)
	sources.sort_custom(nearer)
	targets.sort_custom(nearer)
	thresholds.sort_custom(nearer)
	for threshold: Vector2i in thresholds:
		var approach := PackedVector2Array()
		for origin: Vector2i in sources:
			approach = grid.get_point_path(origin, threshold, false)
			if not approach.is_empty():
				break
		if approach.is_empty():
			continue
		if exterior:
			if _clear(point):
				approach.append(point)
				return approach
			return PackedVector2Array()
		for destination: Vector2i in targets:
			var departure := grid.get_point_path(threshold, destination, false)
			if not departure.is_empty():
				approach.append_array(departure.slice(1))
				return approach
	return PackedVector2Array()

func _move_path(path: PackedVector2Array) -> Dictionary:
	var moves := 0
	for index in range(1, path.size()):
		await physics_frame
		var motion := path[index] - walker.position
		var collision := walker.move_and_collide(motion)
		moves += 1
		if collision != null or walker.position.distance_to(path[index]) > 0.1:
			return {"passed": false, "moves": moves, "waypoint": index,
				"position": [walker.position.x, walker.position.y],
				"collision": str(collision.get_collider()) if collision != null else "incomplete motion"}
	return {"passed": true, "moves": moves, "position": [walker.position.x, walker.position.y]}

func _exercise(door: Dictionary) -> Dictionary:
	var path := _route(door)
	var scope := "exit-approach" if door.has("target_map") else "internal-door-round-trip"
	var result := {"id": door.id, "from": door.from, "to": door.to, "scope": scope, "passed": false}
	if path.size() < 2:
		result["reason"] = "No clear local route through the declared doorway"
		return result
	# Fixture placement precedes the measured motion; no endpoint is snapped.
	walker.position = path[0]
	walker.force_update_transform()
	await physics_frame
	result["start"] = [walker.position.x, walker.position.y]
	result["waypoints"] = path.size()
	var forward := await _move_path(path)
	result["forward"] = forward
	if not bool(forward.passed):
		return result
	if not door.has("target_map"):
		if _room(walker.position) != str(door.to):
			result["reason"] = "Movement did not arrive in the declared destination room"
			return result
		path.reverse()
		var reverse := await _move_path(path)
		result["reverse"] = reverse
		if not bool(reverse.passed) or _room(walker.position) != str(door.from):
			return result
	result["passed"] = true
	return result

