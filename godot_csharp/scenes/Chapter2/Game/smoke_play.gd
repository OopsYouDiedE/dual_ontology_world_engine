extends SceneTree
## 冒烟：从开局一路“歇/睡”，第 4 日清晨空对质，第 7 日不表态，确认能走到结局。
## 运行：godot --headless --path godot_csharp --script res://scenes/Chapter2/Game/smoke_play.gd

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var game: Node2D = (load("res://scenes/Chapter2/Game/Chapter2Game.tscn") as PackedScene).instantiate()
	root.add_child(game)
	await process_frame
	if not ("intro_done" in game):
		quit(2)
		return
	game.intro_done = true
	game._close_modal()
	var t0 := Time.get_ticks_msec()
	var steps := 0
	while not game.ended and Time.get_ticks_msec() - t0 < 600000:
		await process_frame
		if game.busy:
			continue
		var s: Dictionary = game.state
		if s.is_empty():
			continue
		game._close_modal()
		steps += 1
		var mode := String(s.mode)
		print("D%d %s mode=%s map=%s actors=%d" % [s.day, s.slot_name, mode, game.map.name, game.actors.size()])
		if mode == "council":
			game.send("不表态")
		elif int(s.day) == 4 and int(s.slot) == 0:
			game.send_many(["对质", "完"])
		elif int(s.slot) == 5:
			game.send("睡")
		else:
			game.send("歇")
	print("ended=%s steps=%d ms=%d" % [game.ended, steps, Time.get_ticks_msec() - t0])
	print(game.log_text.get_parsed_text().right(1500))
	quit(0 if game.ended else 1)
