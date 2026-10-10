extends SceneTree
const PlayerNavigation := preload("res://scenes/Host/PilotNavigation.gd")
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")

class Walker extends RefCounted:
	var position := Vector2(128, 128)

class Map extends RefCounted:
	var calls := 0
	func walkable(_point, _walker) -> bool:
		calls += 1
		return true
	func nav_grid(_walker) -> AStarGrid2D:
		calls += 1
		var floors := {}
		for y in 9:
			for x in 9:
				floors[Vector2i(x, y)] = true
		return Navigation.build_route_grid(floors, [], Vector2i(9, 9))

class SeenMap extends Map:
	var seen := {}
	var floors := {}
	var queried_points: Array[Vector2] = []
	func seen_cells() -> Dictionary: return seen
	func walkable(point, _walker) -> bool:
		calls += 1
		queried_points.append(point)
		return floors.has(Vector2i((point / 32.0).floor()))
	func nav_grid(_walker) -> AStarGrid2D:
		calls += 1
		return Navigation.build_route_grid(floors, [], Vector2i(9, 9))

class Field extends RefCounted:
	var player := Walker.new()
	var adapter := Map.new()
	var visible := false
	func in_sight(_point) -> bool: return visible

class RememberedMap extends RefCounted:
	var actual: AStarGrid2D
	func nav_grid(_walker) -> AStarGrid2D: return actual

class RememberedField extends RefCounted:
	var player := Walker.new()
	var adapter := RememberedMap.new()
	var visible := {}
	func in_sight(point: Vector2) -> bool:
		return visible.has(Vector2i(((point - adapter.actual.offset) / adapter.actual.cell_size).round()))

func _u_grid() -> AStarGrid2D:
	var grid := AStarGrid2D.new()
	grid.region = Rect2i(0, 0, 36, 36)
	grid.cell_size = Vector2(8, 8)
	grid.offset = Vector2(4, 4)
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	grid.update()
	for y in 36:
		for x in 36:
			# Neutral U: descend the left arm, cross below, ascend the right.
			var open: bool = (x >= 4 and x <= 7 and y >= 4 and y <= 30) \
				or (x >= 24 and x <= 27 and y >= 4 and y <= 30) \
				or (x >= 4 and x <= 27 and y >= 27 and y <= 30)
			grid.set_point_solid(Vector2i(x, y), not open)
	return grid

func _remembered_routes() -> void:
	var field := RememberedField.new()
	field.adapter.actual = _u_grid()
	field.player.position = Vector2(44, 44)
	field.visible[Vector2i(5, 5)] = true
	var known := PlayerNavigation.remember_grid(field)
	check(not known.is_point_solid(Vector2i(5, 5)), "current visible ground enters memory")
	check(known.is_point_solid(Vector2i(5, 20)) and known.is_point_solid(Vector2i(25, 5)), "unseen U arms remain closed in memory")
	check(PlayerNavigation.approaches(field, {"neutral-desk": {"point": Vector2(204, 44), "reach": 12.0}}, known).is_empty(),
		"remembered place cannot use an unseen U route")
	for y in 36:
		for x in 36: field.visible[Vector2i(x, y)] = true
	known = PlayerNavigation.remember_grid(field, known)
	field.visible.clear()
	field.visible[Vector2i(5, 5)] = true
	var original := PlayerNavigation.approaches(field, {"neutral-desk": {"point": Vector2(204, 44), "reach": 12.0}}, known)
	check(original.size() == 1 and original[0].value == "neutral-desk" and original[0].purpose == "approach",
		"observed U route offers a public destination identity")
	if original.size() == 1:
		var point := Vector2(original[0].location[0], original[0].location[1])
		check(point.y > field.player.position.y and point.distance_to(Vector2(204, 44)) > field.player.position.distance_to(Vector2(204, 44)),
			"U route first moves away from the destination along remembered ground")
		check(field.player.position.distance_to(point) <= 128.01 and original[0].input.point == original[0].location,
			"remembered route keeps a short ordinary click waypoint")
	var hidden_cell := Vector2i(5, 20)
	field.adapter.actual.set_point_solid(hidden_cell, true)
	field.adapter.actual.set_point_solid(Vector2i(15, 5), false)
	known = PlayerNavigation.remember_grid(field, known)
	check(not known.is_point_solid(hidden_cell) and known.is_point_solid(Vector2i(15, 5)),
		"hidden closure and hidden opening do not change remembered ground")
	check(PlayerNavigation.approaches(field, {"neutral-desk": {"point": Vector2(204, 44), "reach": 12.0}}, known) == original,
		"hidden passage changes leave the offered remembered waypoint unchanged")
	field.visible[hidden_cell] = true
	known = PlayerNavigation.remember_grid(field, known)
	check(known.is_point_solid(hidden_cell), "seeing a changed passage updates its remembered collision")
	field.visible[Vector2i(15, 5)] = true
	known = PlayerNavigation.remember_grid(field, known)
	check(not known.is_point_solid(Vector2i(15, 5)), "seeing newly open ground updates memory")
	# Endpoints are sampled in the grid's own frame, including non-square cells.
	var rectangular := AStarGrid2D.new()
	rectangular.region = Rect2i(2, 3, 5, 6)
	rectangular.cell_size = Vector2(8, 12)
	rectangular.offset = Vector2(4, 6)
	rectangular.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	rectangular.update()
	var start := rectangular.get_point_position(Vector2i(2, 3))
	var finish := rectangular.get_point_position(Vector2i(6, 8))
	var route := Navigation.route_on_grid(start, finish, rectangular)
	check(not route.is_empty() and route[0] == start and route[-1] == finish,
		"endpoint lookup respects grid cell size offset and nonzero region")
	print("OBSERVATION remembered-U " + JSON.stringify({"offers": original, "grid_cell": [known.cell_size.x, known.cell_size.y]}))

var passed := 0
var failed := 0
func check(condition: bool, label: String) -> void:
	if condition:
		passed += 1
		print("PASS " + label)
	else:
		failed += 1
		push_error("FAIL " + label)

func _initialize() -> void:
	_remembered_routes()
	var field := Field.new()
	var attempts := PlayerNavigation.offers(field)
	check(attempts.size() == 8, "unknown boundary retains ordinary directional attempts")
	check(field.adapter.calls == 0, "unknown attempts do not inspect hidden collision or routes")
	for offer in attempts:
		var point := Vector2(offer.location[0], offer.location[1])
		check(is_equal_approx(field.player.position.distance_to(point), 32.0), "short ordinary step")
		check(offer.input.point == offer.location and offer.input.action == "move", "public point matches ordinary input")
	field.visible = true
	var known := PlayerNavigation.offers(field)
	check(known.size() >= 16 and field.adapter.calls > 0, "longer visible routes use formal navigation")
	check(PlayerNavigation.frontier(field).is_empty(), "no seen cells, no frontier")
	var boundary_map := SeenMap.new()
	for x in range(2, 8): boundary_map.floors[Vector2i(x, 4)] = true
	for x in range(2, 5): boundary_map.seen[Vector2i(x, 4)] = true
	field.adapter = boundary_map
	field.visible = false
	field.player.position = Vector2(144, 144)
	var nearby := PlayerNavigation.frontier(field)
	check(nearby.size() == 1 and nearby[0].location == [176.0, 144.0], "near visible boundary extends one cell outward")
	if nearby.size() == 1:
		check(nearby[0].kind == "move" and nearby[0].purpose == "frontier"
			and nearby[0].input.action == "move" and nearby[0].input.point == nearby[0].location,
			"outward frontier uses ordinary move input")
	field.player.position = Vector2(80, 144)
	var distant := PlayerNavigation.frontier(field)
	check(distant.size() == 1 and distant[0].location == [144.0, 144.0], "distant frontier retains visible edge approach")
	# The same seen corridor precedes a threshold spanning several unrevealed cells.
	# Existing frontier recognition may query its adjacent cell; ordinary short steps
	# farther into the threshold must not inspect their destination geometry.
	for x in [176.0, 208.0]:
		field.player.position = Vector2(x, 144)
		boundary_map.queried_points.clear()
		var threshold := PlayerNavigation.offers(field)
		var forward := Vector2(x + 32.0, 144)
		var short_steps := threshold.filter(func(offer) -> bool:
			return is_equal_approx(field.player.position.distance_to(Vector2(offer.location[0], offer.location[1])), 32.0))
		check(short_steps.size() == 8 and short_steps.all(func(offer) -> bool: return offer.purpose == "frontier"),
			"unrevealed threshold keeps eight short frontier attempts at %d" % x)
		var offered_forward := short_steps.any(func(offer) -> bool:
			return (offer.location == [forward.x, forward.y]
				and offer.input.point == offer.location and offer.input.action == "move"))
		check(offered_forward,
			"multi-cell threshold offers another ordinary forward step at %d" % x)
		check(not boundary_map.queried_points.has(forward), "ordinary threshold forward step does not query unseen destination at %d" % x)
	var empty_map := SeenMap.new()
	field.adapter = empty_map
	var undiscovered := PlayerNavigation.offers(field)
	check(undiscovered.size() == 8 and undiscovered.all(func(offer) -> bool: return offer.purpose == "frontier"),
		"undiscovered ground marks existing short attempts as frontier")
	check(empty_map.calls == 0, "short attempt generation does not read unknown collision or routes")
	var wall_map := SeenMap.new()
	wall_map.seen[Vector2i(4, 4)] = true
	wall_map.floors[Vector2i(4, 4)] = true
	field.adapter = wall_map
	field.player.position = Vector2(144, 144)
	check(PlayerNavigation.frontier(field).is_empty(), "visible walls do not produce frontier candidates")
	print("TOTAL %d checks, %d passed, %d failed" % [passed + failed, passed, failed])
	quit(1 if failed else 0)
