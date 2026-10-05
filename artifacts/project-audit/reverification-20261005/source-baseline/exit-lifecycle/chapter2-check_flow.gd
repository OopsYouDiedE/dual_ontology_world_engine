extends SceneTree
## 第二章宿主冒烟（跳到状态，不走游玩）：每张地图载入与寻路网格、三日推演、对白编译、重演、对账、议事。
## 用法：& $env:GODOT --headless --path godot_csharp --script res://scenes/Chapter2/Game/check_flow.gd
## 这是注入状态的自动检查，不替代游玩。

var failures := 0

func check(ok: bool, what: String) -> void:
	print(("  ok  " if ok else "  FAIL ") + what)
	if not ok:
		failures += 1

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var game: Node2D = (load("res://scenes/Chapter2/Game/Chapter2Game.tscn") as PackedScene).instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	await process_frame
	await process_frame
	var t0 := Time.get_ticks_msec()
	for key in ["zone_yi", "living_hall", "zone_jia", "zone_bing", "zone_ding", "central_hall", "lecture", "logistics", "granary", "clinic", "archive", "counterweight", "resonance", "greenhouse", "campus_s", "campus_n", "campus_sw"]:
		var t := Time.get_ticks_msec()
		game.load_map(key, game.SPAWN_OF.get(key, ""))
		await process_frame
		check(game.grid != null and game._walkable(game.player.position), "%s 载入，出生点可走（%d ms，%d 处可检视）" % [key, Time.get_ticks_msec() - t, game.sites.size()])
	# 对白：静态剧本的每个标题与一段动态文字都能编译。
	var dm: Node = root.get_node("/root/DialogueManager")
	var res: Resource = dm.create_resource_from_text("~ feed\n铜口: 诸位晨安。\n（你看见：沃德在中央厅张贴规则。）\n- 问问\n\tdo talk_pick(\"ask\")\n=> END\n")
	check(res != null and res.titles.has("feed"), "动态对白可编译")
	# The formal host _begin() opens with arrival, then gathering and the lesson.
	for title in ["d2_arrival", "d2_gather", "d2_lesson", "d2_free", "d2_noon_dispute", "d3_after", "replay_unlock", "confront_rule", "d4_council", "d4_box"]:
		check(game.STORY.titles.has(title), "剧本标题 " + title)
	# 第 2 日
	game._gather_day2()
	game._lesson_day2()
	check(game.actors.size() > 3, "讲堂里有人（%d）" % game.actors.size())
	game.stage = "field"
	game._run({"cmd": "collect"})
	game._run({"cmd": "stay", "place": "Logistics"})
	game.engine.Examine("配给台公告")
	game._run({"cmd": "tell_rule", "who": "Ward", "rule": int(game.engine.KnownRules()[0].id)})
	check(game.upcoming_slot() == 5, "第 2 日走到夜里")
	game._run({"cmd": "sleep"}, false)
	game._gather_day3()
	check(game.upcoming_day() == 3 and game.upcoming_slot() == 1, "第 3 日清晨已结算")
	var lines: Array = game._pending_lines
	check(lines.any(func(l): return str(l).contains("台账")), "清晨提到台账")
	for thing in ["台账", "粮仓", "碗", "文契废纸篓", "中央厅公告"]:
		game.engine.Examine(thing)
	for who in ["Bertrand", "Margaret", "Mora", "Quer"]:
		game.engine.Ask(who)
	var r: Dictionary = game.engine.RunCase([0, 0, 0, 0])
	check(r.steps.size() > 0, "重演有墨影步子（%d），全吻合=%s" % [r.steps.size(), str(r.full_match)])
	game._run({"cmd": "escort", "who": "Ward", "thing": "配给台公告"})
	game._run({"cmd": "collect"})
	game._run({"cmd": "stay", "place": "Hall"})
	check(game.upcoming_day() == 3 and game.upcoming_slot() == 4, "到第 3 日傍晚")
	var pairs := []
	for p in [[0, 0], [2, 4], [1, 2], [3, 5]]:
		if game.engine.PairValid(p[0], p[1]):
			pairs.append(p)
	game._run({"cmd": "confront", "pairs": pairs, "proposal": 0, "common": true}, false)
	check(game._pending_lines.any(func(l): return str(l).contains("中央厅")), "对账发生（%d 对有效）" % pairs.size())
	game._run({"cmd": "watch", "place": "ArchiveFront"}, false)
	check(game.engine.Ended(), "第 4 日：章节到议事")
	game._go_court()
	var plans: Array = game.engine.CouncilPlans(0)
	var result: Dictionary = game.engine.Council(plans.size() - 1)
	check(str(result.result_text) != "", "议事结果：" + str(result.result_text))
	print("用时 %d ms；失败 %d" % [Time.get_ticks_msec() - t0, failures])
	quit(1 if failures > 0 else 0)
