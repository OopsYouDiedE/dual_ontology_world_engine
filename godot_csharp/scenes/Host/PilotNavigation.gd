extends RefCounted
## Short directional inputs are ordinary attempts, including blocked steps.
## They reveal no unseen route or object; WorldView resolves the real movement.
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")

static func offers(field) -> Array:
	var result := []
	var grid: AStarGrid2D
	# Thresholds may span several cells before the next region is revealed.
	# While standing on undiscovered ground, the ordinary short attempts continue
	# that exploration without looking up the terrain they would enter.
	var beyond_seen: bool = field.adapter.has_method("seen_cells") and not field.adapter.seen_cells().has(Vector2i((field.player.position / 32.0).floor()))
	for heading in [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]:
		# Longer steps only to points in plain sight, as a player clicks anywhere visible.
		for distance in [32.0, 64.0, 128.0, 256.0]:
			var destination: Vector2 = field.player.position + heading.normalized() * distance
			if distance > 32.0:
				if not field.in_sight(destination) or not field.adapter.walkable(destination, field.player): continue
				if grid == null: grid = field.adapter.nav_grid(field.player)
				if Navigation.route_on_grid(field.player.position, destination, grid).is_empty(): continue
			result.append({"label": "%d,%d" % [destination.x, destination.y], "kind": "move", "new": false,
				"purpose": "frontier" if beyond_seen and distance == 32.0 else "",
				"location": [destination.x, destination.y],
				"input": {"kind": "field_action", "action": "move", "point": [destination.x, destination.y]}})
	result.append_array(frontier(field, grid))
	return result

## Only presently visible terrain updates this actor's remembered geometry.
static func remember_grid(field, known: AStarGrid2D = null) -> AStarGrid2D:
	var actual: AStarGrid2D = field.adapter.nav_grid(field.player)
	if known == null or known.region != actual.region or known.cell_size != actual.cell_size:
		known = AStarGrid2D.new()
		known.region = actual.region
		known.cell_size = actual.cell_size
		known.offset = actual.offset
		known.diagonal_mode = actual.diagonal_mode
		known.update()
		for y in known.region.size.y:
			for x in known.region.size.x:
				known.set_point_solid(known.region.position + Vector2i(x, y), true)
	for y in actual.region.size.y:
		for x in actual.region.size.x:
			var cell := actual.region.position + Vector2i(x, y)
			if field.in_sight(actual.get_point_position(cell)):
				known.set_point_solid(cell, actual.is_point_solid(cell))
	return known

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
		var route := Navigation.route_on_grid(field.player.position, point, known)
		if route.is_empty():
			for heading in [Vector2.DOWN, Vector2.UP, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]:
				route = Navigation.route_on_grid(field.player.position, point + heading.normalized() * reach * 0.75, known)
				if not route.is_empty(): break
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
			"input": {"kind": "field_action", "action": "move", "point": [waypoint.x, waypoint.y]}})
	return result

## 已见与未见的交界：本人见过、走得上去、且地面明显通往未见处的格子（门洞、拐角）。
## 墙面看得见、不是去路，挨着墙的格子不算交界。
static func frontier(field, grid: AStarGrid2D = null, limit := 6) -> Array:
	if not field.adapter.has_method("seen_cells"): return []
	var seen: Dictionary = field.adapter.seen_cells()
	var edges := []
	for cell in seen:
		var point := (Vector2(cell) + Vector2(0.5, 0.5)) * 32.0
		for step in [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]:
			if not seen.has(cell + step) and field.adapter.walkable((Vector2(cell + step) + Vector2(0.5, 0.5)) * 32.0, field.player):
				# Reaching the visible edge is only the first half of exploring it.
				# Nearby, offer the same ordinary short step through that edge.
				var target := point + Vector2(step) * 32.0 if point.distance_to(field.player.position) < 48.0 else point
				if not edges.has(target): edges.append(target)
	edges.sort_custom(func(a: Vector2, b: Vector2) -> bool: return a.distance_squared_to(field.player.position) < b.distance_squared_to(field.player.position))
	var result := []
	var checked := 0
	for point in edges:
		if result.size() >= limit or checked >= 24: break
		if not field.adapter.walkable(point, field.player): continue
		checked += 1
		if grid == null: grid = field.adapter.nav_grid(field.player)
		if Navigation.route_on_grid(field.player.position, point, grid).is_empty(): continue
		result.append({"label": "%d,%d" % [point.x, point.y], "kind": "move", "new": false, "purpose": "frontier",
			"location": [point.x, point.y],
			"input": {"kind": "field_action", "action": "move", "point": [point.x, point.y]}})
	return result
