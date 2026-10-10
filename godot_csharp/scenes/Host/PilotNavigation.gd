extends RefCounted
## Short directional inputs are ordinary attempts, including blocked steps.
## They reveal no unseen route or object; WorldView resolves the real movement.
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")

static func offers(field, known: AStarGrid2D = null) -> Array:
	var result := []
	var grid: AStarGrid2D = known
	# Thresholds may span several cells before the next region is revealed.
	# While standing on undiscovered ground, the ordinary short attempts continue
	# that exploration without looking up the terrain they would enter.
	var beyond_seen: bool = field.adapter.has_method("seen_cells") and not field.adapter.seen_cells().has(Vector2i((field.player.position / 32.0).floor()))
	for heading in [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]:
		# Longer steps only to points in plain sight, as a player clicks anywhere visible.
		for distance in [32.0, 64.0, 128.0, 256.0]:
			var destination: Vector2 = field.player.position + heading.normalized() * distance
			var visible: bool = field.in_sight(destination)
			if distance > 32.0 and not visible: continue
			if visible:
				if not field.adapter.walkable(destination, field.player): continue
				if grid == null: grid = remember_grid(field)
				if Navigation.route_on_grid(field.player.position, destination, grid).is_empty(): continue
			result.append({"label": "%d,%d" % [destination.x, destination.y], "kind": "move", "new": false,
				"purpose": "frontier" if beyond_seen and distance == 32.0 else "",
				"location": [destination.x, destination.y],
				"input": {"kind": "field_action", "action": "move", "point": [destination.x, destination.y]} if visible else _step_input(heading)})
	result.append_array(frontier(field, grid))
	return result

## Only presently visible terrain updates this actor's remembered geometry.
static func remember_grid(field, known: AStarGrid2D = null) -> AStarGrid2D:
	var region := Rect2i(Vector2i.ZERO, field.adapter.sight_size() * (32 / Navigation.ROUTE_CELL))
	if known == null or known.region != region:
		known = AStarGrid2D.new()
		known.region = region
		known.cell_size = Vector2(Navigation.ROUTE_CELL, Navigation.ROUTE_CELL)
		known.offset = known.cell_size / 2.0
		known.diagonal_mode = AStarGrid2D.DIAGONAL_MODE_NEVER
		known.update()
		for y in known.region.size.y:
			for x in known.region.size.x:
				known.set_point_solid(known.region.position + Vector2i(x, y), true)
	for y in known.region.size.y:
		for x in known.region.size.x:
			var cell := known.region.position + Vector2i(x, y)
			var point := known.get_point_position(cell)
			if field.in_sight(point):
				known.set_point_solid(cell, not field.adapter.walkable(point, field.player))
	return known

static func _step_input(heading: Vector2) -> Dictionary:
	var keys := []
	if heading.y < 0: keys.append("W")
	elif heading.y > 0: keys.append("S")
	if heading.x < 0: keys.append("A")
	elif heading.x > 0: keys.append("D")
	return {"kind": "field_action", "action": "move", "keys": keys}

static func _known_route(field, point: Vector2, reach: float, known: AStarGrid2D) -> PackedVector2Array:
	var route := Navigation.route_on_grid(field.player.position, point, known)
	if route.is_empty():
		for heading in [Vector2.DOWN, Vector2.UP, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]:
			route = Navigation.route_on_grid(field.player.position, point + heading.normalized() * reach * 0.75, known)
			if not route.is_empty(): break
	return route

## Cost is estimated from remembered ground; absent routes use the visible distance lower bound.
static func travel_distance(field, point: Vector2, reach: float, known: AStarGrid2D) -> float:
	if field.player.position.distance_to(point) <= reach + 6.0: return 0.0
	var route := _known_route(field, point, reach, known)
	if route.is_empty(): return maxf(0.0, field.player.position.distance_to(point) - reach)
	var distance := 0.0
	var previous: Vector2 = field.player.position
	for next in route:
		distance += previous.distance_to(next)
		previous = next
	return maxf(0.0, distance - reach)

## Offer a short waypoint along known ground toward a place previously observed.
## The controller decides whether that place serves its own question or goal.
static func approaches(field, places: Dictionary, known: AStarGrid2D) -> Array:
	var result := []
	for label in places:
		if result.size() >= 128: break
		var place: Dictionary = places[label]
		var point: Vector2 = place.point
		var reach: float = place.reach
		if field.player.position.distance_to(point) <= reach + 6.0: continue
		var route := _known_route(field, point, reach, known)
		if route.is_empty(): continue
		var waypoint: Vector2 = route[0]
		var distance: float = field.player.position.distance_to(waypoint)
		for next in route:
			var step: float = waypoint.distance_to(next)
			if distance + step > 128.0: break
			distance += step
			waypoint = next
		if field.player.position.distance_to(waypoint) < 12.0: continue
		result.append({"label": "%s %d,%d" % [label, waypoint.x, waypoint.y], "value": label,
			"kind": "move", "new": false, "purpose": "approach", "location": [waypoint.x, waypoint.y],
			# This input walks only the offered leg. Charging the remaining
			# journey here again discourages returning to distant remembered sources.
			"travel_distance": distance,
			"input": {"kind": "field_action", "action": "move", "point": [waypoint.x, waypoint.y]}})
	return result

## 已见地面的边缘：只验证本人已见的一侧，未知一侧由真实按键和碰撞探索。
static func frontier(field, grid: AStarGrid2D = null, limit := 6) -> Array:
	if not field.adapter.has_method("seen_cells"): return []
	var seen: Dictionary = field.adapter.seen_cells()
	var edges := []
	for cell in seen:
		var point := (Vector2(cell) + Vector2(0.5, 0.5)) * 32.0
		if not field.in_sight(point) or not field.adapter.walkable(point, field.player): continue
		for step in [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]:
			if not seen.has(cell + step):
				# Reaching the visible edge is only the first half of exploring it.
				# Nearby, offer the same ordinary short step through that edge.
				var target := point + Vector2(step) * 32.0 if point.distance_to(field.player.position) < 48.0 else point
				if not edges.any(func(edge) -> bool: return edge.target == target):
					edges.append({"target": target, "edge": point, "step": Vector2(step)})
	edges.sort_custom(func(a, b) -> bool: return a.target.distance_squared_to(field.player.position) < b.target.distance_squared_to(field.player.position))
	var result := []
	var checked := 0
	for edge in edges:
		if result.size() >= limit or checked >= 24: break
		var point: Vector2 = edge.target
		checked += 1
		var outward: bool = point != edge.edge
		if not outward:
			if grid == null: grid = remember_grid(field)
			if Navigation.route_on_grid(field.player.position, point, grid).is_empty(): continue
		result.append({"label": "%d,%d" % [point.x, point.y], "kind": "move", "new": false, "purpose": "frontier",
			"location": [point.x, point.y],
			"input": _step_input(edge.step) if outward else {"kind": "field_action", "action": "move", "point": [point.x, point.y]}})
	return result
