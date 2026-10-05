extends SceneTree
## Automatic live input check, not blind play: start on doorway, walk into hall.
func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var game: Node = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	for i in 8:
		await process_frame
	game.load_map("central_hall", "south")
	game.stage = "field"
	game.story_lock = false
	for i in 8:
		await process_frame
	var start: Vector2 = game.player.position
	var initial_region: String = game.sight.active_region
	Input.action_press("ui_up")
	await create_timer(2.0).timeout
	Input.action_release("ui_up")
	for i in 8:
		await process_frame
	var finish: Vector2 = game.player.position
	var region: String = game.sight.active_region
	var visible: int = game.sight.visible_cells.size()
	var ok: bool = finish.y < start.y - 32.0 and region != "" and visible > 0
	print(("PASS " if ok else "FAIL ") + "hall entry live input start=%s finish=%s initial_region=%s entered_region=%s visible_cells=%d" % [str(start), str(finish), initial_region, region, visible])
	await RenderingServer.frame_post_draw
	var path := ProjectSettings.globalize_path("res://../artifacts/project-audit/verification-20261005/chapter2-map-entered.png")
	var saved := root.get_texture().get_image().save_png(path)
	print("shot " + path)
	quit(0 if ok and saved == OK else 1)
