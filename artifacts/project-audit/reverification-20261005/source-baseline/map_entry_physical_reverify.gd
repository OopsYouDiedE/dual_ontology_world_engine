extends SceneTree
## Independent targeted automatic regression with doorway injection and real host physical-key protocol; not blind play.
const OUT := "res://../artifacts/project-audit/reverification-20261005"
var failures := 0

func _initialize() -> void:
	_run.call_deferred()

func check(ok: bool, label: String) -> void:
	print(("PASS " if ok else "FAIL ") + label)
	if not ok: failures += 1

func shot(name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var path := ProjectSettings.globalize_path(OUT).path_join(name + ".png")
	check(root.get_texture().get_image().save_png(path) == OK, "shot " + path)

func _run() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	var game: Node = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	for i in 8: await process_frame
	game.load_map("living_hall", "north_door", Vector2(256, 88), true)
	game.stage = "field"
	game.story_lock = false
	for i in 8: await process_frame
	var start: Vector2 = game.player.position
	var initial_region: String = game.sight.active_region
	var initial_visited: int = game.sight.seen_cells.size()
	check(initial_region.is_empty() and initial_visited == 0, "doorway injection starts with no entered region or visited cells")
	var key := InputEventKey.new()
	key.keycode = KEY_S
	key.physical_keycode = KEY_S
	key.pressed = true
	Input.parse_input_event(key)
	await physics_frame
	check(Input.is_physical_key_pressed(KEY_S), "Input.parse_input_event exposes physical KEY_S to actual host movement")
	await create_timer(1.0).timeout
	key.pressed = false
	Input.parse_input_event(key)
	for i in 8: await process_frame
	var finish: Vector2 = game.player.position
	var foot := Vector2i((finish / 32.0).floor())
	print("ENTRY start=%s finish=%s initial_region=%s entered_region=%s initial_visited=%d visited=%d foot=%s interior=%s visible=%d" % [start, finish, initial_region, game.sight.active_region, initial_visited, game.sight.seen_cells.size(), foot, game.sight_interior(foot), game.sight.visible_cells.size()])
	check(finish.y > start.y + 32.0 and game.sight_interior(foot) and game.sight.active_region == "hall" and game.sight.seen_cells.size() > initial_visited, "physical movement enters legal hall and updates region/visited memory")
	check(not Input.is_physical_key_pressed(KEY_S), "physical key released")
	check(game.sight.terrain_shade(foot).a == 0.0, "current room interior is visible")
	var bath := Vector2i(7, 35)
	check(game.sight_interior(bath) and game.sight_region(bath) == "bath" and not game.sight.seen_cells.has(bath) and game.sight.terrain_shade(bath).a == 1.0, "unvisited bathroom floor remains fully masked")
	check(game.sight.terrain_shade(Vector2i(0, 35)).a == 0.0 and game.sight.terrain_shade(Vector2i(7, 31)).a == 0.0, "unknown bathroom wall and doorway remain visible")
	await shot("chapter2-entered-current-room")
	game.process_mode = Node.PROCESS_MODE_DISABLED
	game.camera.limit_left = -100000
	game.camera.limit_top = -100000
	game.camera.limit_right = 100000
	game.camera.limit_bottom = 100000
	game.camera.global_position = Vector2(256, 1056)
	game.camera.reset_smoothing()
	game.camera.force_update_scroll()
	await shot("chapter2-unvisited-room-wall-door")
	game.queue_free()
	for i in 4: await process_frame
	print("TARGETED_MAP_ENTRY failures=%d" % failures)
	quit(0 if failures == 0 else 1)
