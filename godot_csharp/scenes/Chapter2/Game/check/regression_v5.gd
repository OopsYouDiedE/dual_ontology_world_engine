extends SceneTree

var game: Node
var commands: Array[String] = []
var folder := "res://../artifacts/remake-chapter2/refactor-v5/auto-04/"
var replay_done := false
var confrontation_done := false

func check_navigation() -> void:
	var id := "person:霍铎" if game.sites.has("person:霍铎") else str(game.sites.keys()[0])
	for candidate in game.sites:
		if candidate.begins_with("person:") and game.player.position.distance_to(game.site_point(candidate)) > 180:
			id = candidate
			break
	game._approach_site(id)
	print("NAV target=", id, " start=", game.player.position, " destination=", game.site_point(id), " path=", game.click_path.size())
	assert(game.click_interaction_id == id and not game.ui_lock, "distant target interacted without approach")
	var key := InputEventKey.new()
	key.physical_keycode = KEY_D
	key.pressed = true
	Input.parse_input_event(key)
	await physics_frame
	await physics_frame
	key.pressed = false
	Input.parse_input_event(key)
	assert(game.click_interaction_id.is_empty() and game.click_path.is_empty(), "WASD did not cancel navigation")
	game._approach_site(id)
	var begin := Time.get_ticks_msec()
	while not game.ui_lock and Time.get_ticks_msec() - begin < 30000:
		await physics_frame
	if not game.ui_lock:
		push_error("approach did not open interaction target=%s position=%s destination=%s remaining_path=%d pending=%s" % [id, game.player.position, game.site_point(id), game.click_path.size(), game.click_interaction_id])
		quit(4)
		return
	assert(game.player.position.distance_to(game.site_point(id)) <= game.site_reach(id) + 8, "interaction opened before reaching target")
	print("PASS navigation approach and WASD cancel")
	clear_ui()

func _initialize() -> void:
	call_deferred("run")

func settle() -> void:
	await process_frame
	var begin := Time.get_ticks_msec()
	while game.busy and Time.get_ticks_msec() - begin < 90000:
		await process_frame
	await process_frame
	if game.busy:
		push_error("TIMEOUT")
		quit(2)

func capture(name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var status := root.get_texture().get_image().save_png(ProjectSettings.globalize_path(folder + name + ".png"))
	print("SCREENSHOT ", name, " status=", status)

func clear_ui() -> void:
	game.dialogue.active = false
	game.dialogue.panel.hide()
	game.close_modal()
	game.output_pending.clear()

func command(line: String, info := false) -> void:
	clear_ui()
	commands.append(line)
	game.send(line, info)
	await settle()

func run() -> void:
	game = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	root.add_child(game)
	await process_frame
	if game.stage_node == null or game.log_text == null or game.busy_label == null:
		push_error("Game initialization failed; no valid UI screenshots or transcript.")
		quit(3)
		return
	await settle()
	await create_timer(1.0).timeout
	game.dialogue.reveal()
	await capture("01-intro")
	clear_ui()
	await capture("02-field")
	await check_navigation()
	commands.append("规矩")
	game._pick_rule("测试讲规矩", "讲 工程师")
	await settle()
	if game.state.rules.size() > 0:
		await command("讲 工程师 " + str(game.state.rules[0].label))
	await command("笔记", true)
	await capture("03-notes")
	clear_ui()
	game._card_builder(func(_cells): pass)
	await capture("04-board-editor")
	clear_ui()
	var steps := 0
	while not game.ended and steps < 100:
		steps += 1
		var s: Dictionary = game.state
		print("STEP ",steps," day=",s.day," slot=",s.slot," mode=",s.mode)
		if s.mode == "council":
			await command("不表态")
		elif int(s.day) == 4 and int(s.slot) == 0 and not confrontation_done:
			confrontation_done = true
			clear_ui()
			game.open_manuscript()
			assert(game.replay_result.is_empty() and game.last_run_vars.is_empty(), "cross-night retained old valid result")
			print("PASS cross-night result reset")
			clear_ui()
			game._open_confront(false)
			await capture("07-confrontation")
			await command("对质")
			await command("完")
		elif int(s.day) == 3 and not replay_done:
			replay_done = true
			clear_ui()
			commands.append("规矩")
			game._pick_rule("测试讲规矩", "讲 苏泠")
			await settle()
			assert(game.state.rules.size() > 0, "no known rule to test")
			await command("讲 苏泠 " + str(game.state.rules[0].label))
			await command("查 粮仓")
			await command("重演", true)
			clear_ui()
			game.open_manuscript()
			await capture("05-replay-manuscript")
			if game.state.replay_options.size() > 0:
				game.manuscript._choose(0, 1)
				commands.append("重演 第%d夜 1" % int(game.state.replay_night))
				game.run_replay([1, 0, 0], true)
				await settle()
				assert(game.last_run_vars == [1, 0, 0], "host did not register replay variables")
				if game.state.has("replay"):
					var replay: Dictionary = game.state.replay
					var f := FileAccess.open(folder + "replay.json", FileAccess.WRITE)
					f.store_string(JSON.stringify(replay, "  "))
					f.close()
					clear_ui()
					game.stage_node.play(replay.frames)
					await process_frame
					game.stage_node.paused = true
					await capture("06-replay-stage")
					game.stage_node.skipping = true
					while game.stage_node.playing:
						await process_frame
					game.open_manuscript()
					assert(game.manuscript.stale_label.text.is_empty(), "unchanged selection marked stale")
					await capture("06b-replay-results")
					game.manuscript._choose(0, 0)
					assert(game.manuscript.stale_label.text.contains("已过期"), "changed selection retained valid result")
					await capture("06c-replay-stale")
		elif int(s.slot) == 5:
			await command("守夜 后勤院" if int(s.day) == 2 else "睡")
		else:
			await command("歇")
	clear_ui()
	game._show_ending()
	await capture("08-ending")
	for scroll in game.modal_root.find_children("*", "ScrollContainer", true, false):
		scroll.scroll_vertical = 100000
	await process_frame
	await capture("08b-ending-last")
	var inputs := FileAccess.open(folder + "inputs.txt", FileAccess.WRITE)
	for line in commands:
		inputs.store_line(line)
	inputs.close()
	var output := FileAccess.open(folder + "godot-visible-output.txt", FileAccess.WRITE)
	for line in game.visible_output:
		output.store_line(line)
	output.close()
	print("COMPLETE ended=",game.ended," commands=",commands.size()," output_entries=",game.visible_output.size())
	quit(0 if game.ended else 1)
