extends SceneTree

var game: Node
var commands: Array[String] = []
var folder := "res://../artifacts/remake-chapter2/refactor-v5/auto-03/"
var replay_done := false
var confrontation_done := false

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
			game._open_confront(false)
			await capture("07-confrontation")
			await command("对质")
			await command("完")
		elif int(s.day) == 3 and not replay_done:
			replay_done = true
			await command("查 粮仓")
			await command("重演", true)
			clear_ui()
			game.open_manuscript()
			await capture("05-replay-manuscript")
			if game.state.replay_options.size() > 0:
				await command("重演 第2夜 1", true)
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
					game.skip_replay = true
					game.accept_replay(replay)
					await capture("06b-replay-results")
		elif int(s.slot) == 5:
			await command("守夜 后勤院" if int(s.day) == 2 else "睡")
		else:
			await command("歇")
	clear_ui()
	game._show_ending()
	await capture("08-ending")
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
