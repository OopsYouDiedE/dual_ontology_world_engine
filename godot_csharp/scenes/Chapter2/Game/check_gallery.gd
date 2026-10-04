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

func _run() -> void:
	game = (load("res://scenes/Chapter2/Game/Chapter2Game.tscn") as PackedScene).instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	await process_frame
	game.dialogue.panel.show()
	game.dialogue.start(game.STORY, "d2_wake", game)
	await shot("01_wake_dialogue")
	game.dialogue.panel.hide()
	game.dialogue.active = false
	game._gather_day2()
	await shot("02_central_hall_gather")
	game._lesson_day2()
	game.stage = "field"
	await shot("03_lecture_field")
	game.load_map("logistics", "west")
	game.player.position = Vector2(500, 300)
	await shot("04_logistics")
	game.sites["ledger"] = game.sites.get("ledger", {})
	game.open_thing("ration_notice")
	await shot("05_card")
	game.close_modal()
	var key: String = game.actors.keys()[0] if not game.actors.is_empty() else ""
	if key != "":
		game.talk(key)
		await shot("06_talk_menu")
		game.dialogue.panel.hide()
		game.dialogue.active = false
		game.story_lock = false
	game._run({"cmd": "collect"})
	game._run({"cmd": "stay", "place": "Logistics"})
	game._run({"cmd": "collect"})
	game._run({"cmd": "sleep"}, false)
	game._gather_day3()
	for thing in ["台账", "粮仓", "碗", "文契废纸篓"]:
		game.engine.Examine(thing)
	game.replay_open = true
	game.open_notebook()
	await shot("07_notebook")
	game.open_manuscript()
	await shot("08_manuscript")
	game.run_replay([0, 0, 0, 0])
	game.open_manuscript()
	await shot("09_manuscript_result")
	game.play_replay(game.replay_result)
	for i in 120:
		await process_frame
	await shot("10_replay_playback")
	game.close_modal()
	game.open_campus_map()
	await shot("11_campus_map")
	game.close_modal()
	game._confront_pick({"asker": "沃德", "q": "台账上的缺口，有一份，是谁取的？为何取？", "claim": 0})
	await shot("12_confront")
	quit()
