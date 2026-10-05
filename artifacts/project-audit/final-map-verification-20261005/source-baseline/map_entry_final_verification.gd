extends SceneTree
## Independent, single targeted automatic check. Only the doorway start is injected.
const OUT := "res://../artifacts/project-audit/final-map-verification-20261005"
var failures := 0
var report := {"kind": "targeted automatic physical input regression; not blind play", "checks": []}

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	report.checks.append({"pass": ok, "label": label})
	print(("PASS " if ok else "FAIL ") + label)
	if not ok: failures += 1

func shot(game: Node, name: String, cells: Dictionary) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var canvas: Transform2D = root.get_canvas_transform()
	var locations := {}
	for label in cells:
		var world := (Vector2(cells[label]) + Vector2(0.5, 0.5)) * 32.0
		locations[label] = {"world": str(world), "screen": str(canvas * world)}
	report[name] = {"canvas_transform": str(canvas), "viewport": str(root.get_visible_rect()), "camera_global": str(game.camera.global_position), "camera_zoom": str(game.camera.zoom), "locations": locations}
	check(root.get_texture().get_image().save_png(ProjectSettings.globalize_path(OUT).path_join(name + ".png")) == OK, "screenshot " + name)

func _run() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	var game: Node = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	for i in 8: await process_frame
	# Archive has never been loaded by this new instance. Its south doorway is D.
	game.load_map("archive", "south", Vector2(256, 592), true)
	game.stage = "field"
	game.story_lock = false
	for i in 8: await process_frame
	var start: Vector2 = game.player.position
	var initial_region: String = game.sight.active_region
	var initial_visited: int = game.sight.seen_cells.size()
	var start_cell := Vector2i((start / 32.0).floor())
	check(String(game.map.name) == "archive" and not game.sight_interior(start_cell) and initial_region == "" and initial_visited == 0, "fresh archive doorway has no current room or visited cells")
	check(game.field_active() and game.player.collision_mask != 0, "host movement active with collision enabled")
	var key := InputEventKey.new()
	key.keycode = KEY_W
	key.physical_keycode = KEY_W
	key.pressed = true
	Input.parse_input_event(key)
	await physics_frame
	check(Input.is_physical_key_pressed(KEY_W), "physical KEY_W reaches host input")
	await create_timer(0.65).timeout
	key.pressed = false
	Input.parse_input_event(key)
	for i in 8: await process_frame
	var finish: Vector2 = game.player.position
	var foot := Vector2i((finish / 32.0).floor())
	report.entry = {"map": String(game.map.name), "start": str(start), "finish": str(finish), "start_cell": str(start_cell), "finish_cell": str(foot), "initial_region": initial_region, "current_region": game.sight.active_region, "initial_visited": initial_visited, "visited": game.sight.seen_cells.size(), "visible": game.sight.visible_cells.size()}
	print("ENTRY " + JSON.stringify(report.entry))
	check(String(game.map.name) == "archive" and finish.y < start.y - 32.0 and game.sight_interior(foot) and game.sight.active_region == "foyer" and game.sight.seen_cells.size() > initial_visited, "physical movement enters legal previously unvisited foyer and updates current/visited")
	check(not Input.is_physical_key_pressed(KEY_W), "physical key released")
	var current_ok := true
	var unknown_ok := true
	var architecture_ok := true
	var current_count := 0
	var unknown_count := 0
	var architecture_count := 0
	for y in range(game.sight_size().y):
		for x in range(game.sight_size().x):
			var cell := Vector2i(x, y)
			if not game.sight_interior(cell):
				architecture_count += 1
				architecture_ok = architecture_ok and game.sight.terrain_shade(cell).a == 0.0
			elif game.sight_region(cell) == "foyer":
				current_count += 1
				current_ok = current_ok and game.sight.seen_cells.has(cell) and game.sight.can_see((Vector2(cell) + Vector2(0.5, 0.5)) * 32.0) and game.sight.terrain_shade(cell).a == 0.0
			else:
				unknown_count += 1
				unknown_ok = unknown_ok and not game.sight.seen_cells.has(cell) and game.sight.terrain_shade(cell).a == 1.0
	report.mask_counts = {"current_interior": current_count, "unknown_interior": unknown_count, "architecture": architecture_count}
	check(current_count > 0 and current_ok, "all current foyer interiors visible")
	check(unknown_count > 0 and unknown_ok, "all unvisited vault/works interiors fully masked")
	check(architecture_count > 0 and architecture_ok, "all walls and doorways remain visible")
	check(not game.sight_interior(Vector2i(15, 14)) and game.sight.terrain_shade(Vector2i(15, 14)).a == 0.0, "works doorway visible beside masked works floor")
	await shot(game, "chapter2-entered-foyer", {"player_foot": foot, "works_floor": Vector2i(20, 15), "works_door": Vector2i(15, 14), "vault_floor": Vector2i(8, 5)})
	game.process_mode = Node.PROCESS_MODE_DISABLED
	game.camera.limit_left = -100000
	game.camera.limit_top = -100000
	game.camera.limit_right = 100000
	game.camera.limit_bottom = 100000
	game.camera.zoom = Vector2.ONE
	game.camera.global_position = Vector2(448, 304)
	game.camera.reset_smoothing()
	game.camera.force_update_scroll()
	await shot(game, "chapter2-archive-mask-overview", {"current_foyer_floor": Vector2i(8, 16), "works_floor": Vector2i(20, 15), "works_door": Vector2i(15, 14), "vault_floor": Vector2i(8, 5), "vault_door": Vector2i(8, 9), "vault_wall": Vector2i(1, 1)})
	report.failures = failures
	var file := FileAccess.open(ProjectSettings.globalize_path(OUT).path_join("verification-report.json"), FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "\t"))
	file.close()
	print("TARGETED_FINAL_MAP_ENTRY failures=%d" % failures)
	quit(0 if failures == 0 else 1)
