extends RefCounted
## Geometry queries shared by pointer movement, party paths, and replay staging.

const CLICK_TARGET_WIDTH := 24.0
const ROUTE_CELL := 8

static func walkable(center: Vector2, floors: Dictionary, obstacles: Array[Rect2]) -> bool:
	for corner in [Vector2(-10,-14),Vector2(9,-14),Vector2(-10,1),Vector2(9,1)]:
		if not floors.has(Vector2i((center + corner) / 32.0)): return false
	for obstacle in obstacles:
		if obstacle.grow_individual(10,1,10,14).has_point(center): return false
	return true

static func route(start: Vector2, finish: Vector2, floors: Dictionary, obstacles: Array[Rect2], map_size: Vector2i) -> PackedVector2Array:
	return route_on_grid(start, finish, build_route_grid(floors, obstacles, map_size))

static func build_route_grid(floors: Dictionary, obstacles: Array[Rect2], map_size: Vector2i) -> AStarGrid2D:
	# Fine centres preserve narrow routes available to the continuous body.
	var grid := AStarGrid2D.new()
	grid.region = Rect2i(Vector2i.ZERO, map_size * (32 / ROUTE_CELL))
	grid.cell_size = Vector2(ROUTE_CELL, ROUTE_CELL)
	grid.offset = grid.cell_size / 2.0
	grid.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
	grid.update()
	for y in grid.region.size.y:
		for x in grid.region.size.x:
			var node := Vector2i(x, y)
			var center := grid.get_point_position(node)
			grid.set_point_solid(node, not walkable(center, floors, obstacles))
	return grid

static func route_on_grid(start: Vector2, finish: Vector2, grid: AStarGrid2D) -> PackedVector2Array:
	# A legal continuous position can round into a blocked grid centre near a chair.
	# Use a nearby free centre; never make furniture traversable for an endpoint.
	var from := _free_endpoint(grid, start)
	var to := _free_endpoint(grid, finish)
	if from.x < 0 or to.x < 0: return PackedVector2Array()
	return grid.get_point_path(from, to)

static func _free_endpoint(grid: AStarGrid2D, point: Vector2) -> Vector2i:
	var cell := Vector2i(((point - grid.offset) / grid.cell_size).round())
	var best := Vector2i(-1, -1)
	var distance := INF
	for y in range(-1, 2):
		for x in range(-1, 2):
			var candidate := cell + Vector2i(x, y)
			if not grid.region.has_point(candidate) or grid.is_point_solid(candidate): continue
			var offset := grid.get_point_position(candidate).distance_squared_to(point)
			if offset < distance:
				distance = offset
				best = candidate
	return best

static func site_point(id: String, sites: Dictionary, actors: Dictionary) -> Vector2:
	var site: Dictionary = sites[id]
	return actors[site.title].position if actors.has(site.title) else site.point

static func clicked_site(position: Vector2, sites: Dictionary, actors: Dictionary) -> String:
	var chosen := ""
	var best_distance := INF
	for id in sites:
		var point := site_point(id, sites, actors)
		var actor_site := actors.has(sites[id].title)
		var top := 52.0 if actor_site else 80.0 if id.begins_with("bed_") else 56.0
		if not Rect2(point + Vector2(-CLICK_TARGET_WIDTH, -top), Vector2(CLICK_TARGET_WIDTH * 2.0, top + 10.0)).has_point(position):
			continue
		var marker := point + Vector2(0, -46 if actor_site else -12)
		var distance := position.distance_squared_to(marker)
		if distance < best_distance:
			best_distance = distance
			chosen = id
	return chosen
