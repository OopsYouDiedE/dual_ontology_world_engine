extends SceneTree
## 第二章关键画面截图（开发自检，有窗口）：跳到状态后截图，PNG 写入 user_dir 参数所给目录。
## 用法：& $env:GODOT --path godot_csharp --script res://scenes/Chapter2/Game/check_gallery.gd -- --out=<目录>

var out := "user://ch2_gallery"
var game: Node2D

func _initialize() -> void:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--out="):
			out = a.substr(6)
	DirAccess.make_dir_recursive_absolute(out)
	_run.call_deferred()

func shot(name: String) -> void:
	for i in 20:
		await process_frame
	root.get_viewport().get_texture().get_image().save_png(out.path_join(name + ".png"))
	print("shot ", name)

func finish_events() -> void:
	var deadline := Time.get_ticks_msec() + 60000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if game.dialogue.active and not game.dialogue.busy:
			if game.dialogue.body.visible_characters >= 0: game.dialogue.reveal()
			else: game.dialogue.advance()
		elif game.modal_kind == "menu" and not game._draining_events:
			var buttons: Array = game.modal_root.find_children("*", "Button", true, false)
			var available: Array = buttons.filter(func(button): return not button.disabled)
			if available.is_empty():
				push_error("Gallery cannot continue: chapter supplied no available candidate")
				return
			available[0].pressed.emit()
		elif not game._draining_events and not game._replay_playing:
			return
	push_error("Gallery chapter event presentation timed out")

func send(input: Dictionary) -> void:
	game._dispatch(input)
	await finish_events()

# STORY-HARDCODE[CH2-021][logic][tool]: 画廊脚本注入章一继承状态并预写第二章场景、行动与日期路线。
func _run() -> void:
	# Inject a real chapter-one handoff for this standalone automatic fixture.
	# This is not a completed playthrough or a JSON reconstruction.
	var first: Node = preload("res://scenes/Awakening/AwakeningEngine.cs").new()
	root.add_child(first)
	first.ChapterEnd([], false, false, false)
	game = (load("res://scenes/Chapter2/Game/Chapter2Game.tscn") as PackedScene).instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	await process_frame
	if game.startup_error != "":
		push_error("Gallery chapter startup failed: " + game.startup_error)
		game.begin_shutdown()
		game.queue_free()
		first.queue_free()
		await process_frame
		await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(1)
		return
	game._begin()
	await shot("01_arrival_dialogue")
	await finish_events()
	game.load_map("central_hall", "south")
	await shot("02_central_hall_gather")
	game.load_map("lecture", "from_central")
	await shot("03_lecture_field")
	game.load_map("logistics", "west")
	game.player.position = Vector2(500, 300)
	await shot("04_logistics")
	game._dispatch({"kind": "interact", "map": "logistics", "site": "ration_notice", "target": ""})
	await shot("05_card")
	await finish_events()
	game._dismiss_modal()
	var key: String = str(game.actors.keys()[0]) if not game.actors.is_empty() else ""
	if key != "":
		game._dispatch({"kind": "interact", "map": "logistics", "site": "person:" + key, "target": key})
		await shot("06_talk_menu")
		game._dismiss_modal()
	for act in [{"cmd": "collect"}, {"cmd": "stay", "place": "Logistics"}, {"cmd": "collect"}, {"cmd": "sleep"}]:
		await send({"kind": "action", "act": act})
	var case_day: int = int(game.engine.CaseDay())
	var guard := 0
	while game.upcoming_day() < case_day and guard < 40:
		guard += 1
		await send({"kind": "action", "act": {"cmd": "sleep"} if game.upcoming_slot() == 5 else {"cmd": "stay", "place": "Logistics"}})
	for pair in [["logistics", "ledger"], ["granary", "latch"], ["clinic", "empty_bowl"], ["zone_jia", "litter"]]:
		await send({"kind": "move", "place": game.MAP_PLACE[pair[0]]})
		await send({"kind": "interact", "map": pair[0], "site": pair[1], "target": ""})
		game._dismiss_modal()
	game.open_notebook()
	await shot("07_notebook")
	game._dismiss_modal()
	game.open_manuscript()
	await shot("08_manuscript")
	game.run_replay([0, 0, 0, 0])
	game.open_manuscript()
	await shot("09_manuscript_result")
	game.play_replay(game.replay_result)
	for i in 120: await process_frame
	await shot("10_replay_playback")
	await finish_events()
	game._dismiss_modal()
	game.open_campus_map()
	await shot("11_campus_map")
	game._dismiss_modal()
	while game.upcoming_day() == case_day and game.upcoming_slot() < 4:
		await send({"kind": "action", "act": {"cmd": "stay", "place": "Hall"}})
	game._dispatch({"kind": "action", "act": {"cmd": "stay", "place": "Hall"}})
	await shot("12_confront")
	game.begin_shutdown()
	game.queue_free()
	first.queue_free()
	await process_frame
	await process_frame
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(0)
