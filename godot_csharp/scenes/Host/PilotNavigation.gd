extends RefCounted
## Short directional inputs are ordinary attempts, including blocked steps.
## They reveal no unseen route or object; WorldView resolves the real movement.
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")

static func offers(field) -> Array:
	var result := []
	var grid: AStarGrid2D
	for heading in [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]:
		# Longer steps only to points in plain sight, as a player clicks anywhere visible.
		for distance in [32.0, 64.0, 128.0, 256.0]:
			var destination: Vector2 = field.player.position + heading.normalized() * distance
			if distance > 32.0:
				if not field.in_sight(destination) or not field.adapter.walkable(destination, field.player): continue
				if grid == null: grid = field.adapter.nav_grid(field.player)
				if Navigation.route_on_grid(field.player.position, destination, grid).is_empty(): continue
			result.append({"label": "%d,%d" % [destination.x, destination.y], "kind": "move", "new": false,
				"location": [destination.x, destination.y],
				"input": {"kind": "field_action", "action": "move", "point": [destination.x, destination.y]}})
	result.append_array(frontier(field, grid))
	return result

## 已见与未见的交界：本人见过、走得上去、紧挨着还没见过的地方的格子。
## 只用本人已见的格子，不预读未见处；门口、拐角自然在其中。
static func frontier(field, grid: AStarGrid2D = null, limit := 6) -> Array:
	if not field.adapter.has_method("seen_cells"): return []
	var seen: Dictionary = field.adapter.seen_cells()
	var edges := []
	for cell in seen:
		var point := (Vector2(cell) + Vector2(0.5, 0.5)) * 32.0
		if point.distance_to(field.player.position) < 48.0: continue
		for step in [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]:
			if not seen.has(cell + step):
				edges.append(point)
				break
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
