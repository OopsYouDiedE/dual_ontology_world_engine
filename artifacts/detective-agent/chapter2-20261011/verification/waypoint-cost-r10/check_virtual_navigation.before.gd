extends SceneTree
const PlayerNavigation := preload("res://scenes/Host/PilotNavigation.gd")
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")

class Walker extends RefCounted:
	var position := Vector2(128, 128)

# Reality is private to the adapter. Every query is checked against current sight.
class Map extends RefCounted:
	var dimensions := Vector2i(9, 9)
	var seen := {}
	var floors := {}
	var all_open := false
	var field
	var queried_points: Array[Vector2] = []
	var unknown_queries: Array[Vector2] = []
	var route_queries := 0
	func sight_size() -> Vector2i: return dimensions
	func seen_cells() -> Dictionary: return seen
	func walkable(point: Vector2, _walker) -> bool:
		queried_points.append(point)
		if not field.get_ref().in_sight(point): unknown_queries.append(point)
		return all_open or floors.has(Vector2i((point / 8.0).floor()))
	func nav_grid(_walker) -> AStarGrid2D:
		route_queries += 1
		push_error("whole-map navigation query is forbidden in this fixture")
		return null

class Field extends RefCounted:
	var player := Walker.new()
	var adapter := Map.new()
	var visible := {}
	var all_visible := false
	func _init() -> void: adapter.field = weakref(self)
	func in_sight(point: Vector2) -> bool:
		return all_visible or visible.has(Vector2i((point / 8.0).floor()))

var passed := 0
var failed := 0
func check(condition: bool, label: String) -> void:
	if condition:
		passed += 1
		print("PASS " + label)
	else:
		failed += 1
		push_error("FAIL " + label)

func _no_hidden_reads(field, label: String) -> void:
	check(field.adapter.unknown_queries.is_empty() and field.adapter.route_queries == 0, label)
	print("OBSERVATION query-boundary " + JSON.stringify({"label": label,
		"unknown_geometry_queries": field.adapter.unknown_queries, "actual_nav_grid_queries": field.adapter.route_queries}))

func _u_routes() -> void:
	var field := Field.new()
	for y in 36:
		for x in 36:
			# Neutral U: descend left, cross below, ascend right.
			if (x >= 4 and x <= 7 and y >= 4 and y <= 30) \
				or (x >= 24 and x <= 27 and y >= 4 and y <= 30) \
				or (x >= 4 and x <= 27 and y >= 27 and y <= 30):
				field.adapter.floors[Vector2i(x, y)] = true
	field.player.position = Vector2(44, 44)
	field.visible[Vector2i(5, 5)] = true
	var known := PlayerNavigation.remember_grid(field)
	check(not known.is_point_solid(Vector2i(5, 5)), "current visible ground enters memory")
	check(known.is_point_solid(Vector2i(5, 20)) and known.is_point_solid(Vector2i(25, 5)), "unseen U arms remain closed in memory")
	var places := {"neutral-desk": {"point": Vector2(204, 44), "reach": 12.0}}
	check(PlayerNavigation.approaches(field, places, known).is_empty(), "remembered place cannot use an unseen U route")
	check(is_equal_approx(PlayerNavigation.travel_distance(field, Vector2(204, 44), 12.0, known), 148.0),
		"unobserved route cost uses only known coordinate lower bound")
	field.all_visible = true
	known = PlayerNavigation.remember_grid(field, known)
	field.all_visible = false
	var original := PlayerNavigation.approaches(field, places, known)
	check(original.size() == 1 and original[0].value == "neutral-desk" and original[0].purpose == "approach",
		"observed U route offers a public destination identity")
	if original.size() == 1:
		var point := Vector2(original[0].location[0], original[0].location[1])
		check(point.y > field.player.position.y and point.distance_to(Vector2(204, 44)) > field.player.position.distance_to(Vector2(204, 44)),
			"U route first moves away from destination along remembered ground")
		check(field.player.position.distance_to(point) <= 128.01 and original[0].input.point == original[0].location,
			"remembered route keeps a short ordinary click waypoint")
		check(original[0].travel_distance > 400.0, "public route cost includes observed detour beyond first waypoint")
	var original_offers := PlayerNavigation.offers(field, known)
	var hidden_cell := Vector2i(5, 20)
	field.adapter.floors.erase(hidden_cell)
	field.adapter.floors[Vector2i(15, 5)] = true
	known = PlayerNavigation.remember_grid(field, known)
	check(not known.is_point_solid(hidden_cell) and known.is_point_solid(Vector2i(15, 5)),
		"hidden closure and hidden opening do not change remembered ground")
	check(PlayerNavigation.approaches(field, places, known) == original and PlayerNavigation.offers(field, known) == original_offers,
		"hidden geometry changes leave all public route candidates unchanged")
	_no_hidden_reads(field, "remembered navigation samples only current visible points")
	field.visible[hidden_cell] = true
	known = PlayerNavigation.remember_grid(field, known)
	check(known.is_point_solid(hidden_cell), "seeing changed passage updates remembered collision")
	field.visible[Vector2i(15, 5)] = true
	known = PlayerNavigation.remember_grid(field, known)
	check(not known.is_point_solid(Vector2i(15, 5)), "seeing newly open ground updates memory")
	print("OBSERVATION remembered-U " + JSON.stringify({"offers": original, "grid_cell": [known.cell_size.x, known.cell_size.y]}))

func _corridor_frontiers() -> void:
	var field := Field.new()
	for x in range(2, 8):
		for fine_x in range(x * 4, x * 4 + 4):
			for fine_y in range(16, 20): field.adapter.floors[Vector2i(fine_x, fine_y)] = true
	for x in range(2, 5):
		for y in range(3, 6): field.adapter.seen[Vector2i(x, y)] = true
		for fine_x in range(x * 4, x * 4 + 4):
			for fine_y in range(16, 20): field.visible[Vector2i(fine_x, fine_y)] = true
	field.player.position = Vector2(144, 144)
	var nearby := PlayerNavigation.frontier(field)
	check(nearby.any(func(o) -> bool: return o.location == [176.0, 144.0] and o.input.get("keys") == ["D"] and not o.input.has("point")),
		"near visible boundary offers real outward keyboard input")
	field.player.position = Vector2(80, 144)
	var distant := PlayerNavigation.frontier(field)
	check(distant.any(func(o) -> bool: return o.location == [144.0, 144.0] and o.input.get("point") == o.location),
		"distant frontier approaches only observed edge by click")
	for x in [176.0, 208.0]:
		field.player.position = Vector2(x, 144)
		var threshold := PlayerNavigation.offers(field)
		var short_steps := threshold.filter(func(o) -> bool:
			return is_equal_approx(field.player.position.distance_to(Vector2(o.location[0], o.location[1])), 32.0))
		var all_unknown_directions := true
		for heading in [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]:
			var destination: Vector2 = field.player.position + heading.normalized() * 32.0
			if field.in_sight(destination): continue
			all_unknown_directions = all_unknown_directions and short_steps.any(func(o) -> bool:
				return o.location == [destination.x, destination.y] and o.purpose == "frontier" and o.input.has("keys") and not o.input.has("point"))
		check(all_unknown_directions, "unrevealed threshold retains every unknown keyboard direction at %d" % x)
		check(short_steps.any(func(o) -> bool: return o.location == [x + 32.0, 144.0] and o.input.keys == ["D"]),
			"multi-cell threshold offers further forward input at %d" % x)
	_no_hidden_reads(field, "frontier generation never samples unknown side")
	var baseline := PlayerNavigation.offers(field)
	field.adapter.floors.clear()
	for fine_x in range(8, 20):
		for fine_y in range(16, 20): field.adapter.floors[Vector2i(fine_x, fine_y)] = true
	check(PlayerNavigation.offers(field) == baseline, "hidden corridor removal leaves public threshold candidates unchanged")
	_no_hidden_reads(field, "changed hidden corridor still has no geometry queries")

func _initialize() -> void:
	_u_routes()
	_corridor_frontiers()
	var field := Field.new()
	field.adapter.all_open = true
	var attempts := PlayerNavigation.offers(field)
	check(attempts.size() == 8, "unknown boundary retains ordinary directional attempts")
	check(attempts.all(func(o) -> bool: return (is_equal_approx(field.player.position.distance_to(Vector2(o.location[0], o.location[1])), 32.0)
		and o.input.action == "move" and o.input.has("keys") and not o.input.has("point"))),
		"unknown directions are short real keyboard inputs")
	check(attempts.any(func(o) -> bool: return o.input.keys == ["W", "D"]), "unknown diagonal carries both ordinary keys")
	_no_hidden_reads(field, "unknown attempt generation reads no collision or route")
	field.all_visible = true
	var known := PlayerNavigation.offers(field)
	check(known.size() >= 16 and not field.adapter.queried_points.is_empty(), "longer visible steps use remembered visible navigation")
	_no_hidden_reads(field, "visible routes never request actual whole-map grid")
	check(PlayerNavigation.frontier(field).is_empty(), "no remembered seen cells means no frontier")
	var rectangular := AStarGrid2D.new()
	rectangular.region = Rect2i(2, 3, 5, 6)
	rectangular.cell_size = Vector2(8, 12)
	rectangular.offset = Vector2(4, 6)
	rectangular.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	rectangular.update()
	var start := rectangular.get_point_position(Vector2i(2, 3))
	var finish := rectangular.get_point_position(Vector2i(6, 8))
	var route := Navigation.route_on_grid(start, finish, rectangular)
	check(not route.is_empty() and route[0] == start and route[-1] == finish, "endpoint lookup respects grid frame")
	print("TOTAL %d checks, %d passed, %d failed" % [passed + failed, passed, failed])
	quit(1 if failed else 0)
