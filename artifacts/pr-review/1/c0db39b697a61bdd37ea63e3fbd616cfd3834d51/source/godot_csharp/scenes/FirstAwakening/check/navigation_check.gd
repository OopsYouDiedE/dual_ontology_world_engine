extends SceneTree
## Grid-versus-physics audit for click navigation.
## Sweeps the detective's real collider along grid routes from the common-room
## table to sampled floor points and reports every segment the physics would block.
##   godot --headless --path godot_csharp --script res://scenes/FirstAwakening/check/navigation_check.gd

var host: Node

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var scene: Node = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	host = scene
	for i in 20: await physics_frame
	# Open the detective's own door path and the common rooms, as during investigation.
	host.red.call("IntroduceCompanions")
	host.red.call("ReceiveCopperIntroduction")
	var player: CharacterBody2D = host.player
	player.stand_up() if player.has_method("stand_up") else null
	# Doors the walker may use swing open on approach; treat their leaves as open.
	for door in host.scene_items.get_children():
		if door.has_method("access_allowed") and door.access_allowed():
			door.leaf_collision.disabled = true
			door.set_physics_process(false)
	await physics_frame
	var start: Vector2 = host.get_node("MeetingSpots/Detective").position
	var size := Vector2i(host.layout.rows[0].length(), host.layout.rows.size())
	var grid: AStarGrid2D = host.FieldNavigation.build_route_grid(host.floors, host._navigation_obstacles(start, player), size)
	var targets: Array[Vector2] = []
	for y in range(1, size.y * 2, 3):
		for x in range(1, size.x * 2, 3):
			var cell := Vector2i(x, y)
			if grid.is_point_solid(cell): continue
			targets.append(grid.get_point_position(cell))
	var blocked := {}
	var routes := 0
	var starts: Array[Vector2] = [start]
	for index in range(0, targets.size(), 5): starts.append(targets[index])
	for origin in starts:
		for target in targets:
			var path: PackedVector2Array = host.FieldNavigation.route_on_grid(origin, target, grid)
			if path.is_empty(): continue
			routes += 1
			var at := origin
			for point in path:
				var motion := point - at
				if motion.length() > 0.01 and player.test_move(Transform2D(0, at), motion):
					var key := Vector2i(point / 16.0)
					blocked[key] = blocked.get(key, 0) + 1
					break
				at = point
	var cells := blocked.keys()
	cells.sort_custom(func(a, b): return blocked[a] > blocked[b])
	for cell in cells:
		print("blocked waypoint cell ", cell, " world ", Vector2(cell * 16) + Vector2(8, 8), " routes ", blocked[cell])
	print("navigation_check: %d routes, %d blocked waypoint cells" % [routes, cells.size()])
	quit(1 if not cells.is_empty() else 0)

