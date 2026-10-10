extends RefCounted
## Short directional inputs are ordinary attempts, including blocked steps.
## They reveal no unseen route or object; WorldView resolves the real movement.
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")

static func offers(field) -> Array:
	var result := []
	var grid: AStarGrid2D
	for heading in [Vector2.UP, Vector2.DOWN, Vector2.LEFT, Vector2.RIGHT, Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]:
		for distance in [32.0, 64.0]:
			var destination: Vector2 = field.player.position + heading.normalized() * distance
			if distance > 32.0:
				if not field.in_sight(destination) or not field.adapter.walkable(destination, field.player): continue
				if grid == null: grid = field.adapter.nav_grid(field.player)
				if Navigation.route_on_grid(field.player.position, destination, grid).is_empty(): continue
			result.append({"label": "%d,%d" % [destination.x, destination.y], "kind": "move", "new": false,
				"location": [destination.x, destination.y],
				"input": {"kind": "field_action", "action": "move", "point": [destination.x, destination.y]}})
	return result
