extends SceneTree
## 第二章自动事件检查：地图与寻路、正式章节台词、三日推演、重演、对账与议事。
## 用法：& $env:GODOT --headless --path godot_csharp --script res://scenes/Chapter2/Game/check_flow.gd
## 这是注入状态的自动检查，不替代游玩。

var failures := 0

func check(ok: bool, what: String) -> void:
	print(("  ok  " if ok else "  FAIL ") + what)
	if not ok:
		failures += 1

func _initialize() -> void:
	_run.call_deferred()

func _finish(exit_code: int) -> void:
	# The driver has returned; allow both chapter fixture nodes to leave the tree.
	await process_frame
	await process_frame
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(exit_code)

var latest_lines: Array = []
var ending_result: Dictionary = {}
var planned_pairs: Array = []
var planned_pair_index := 0

func run_action(game: Node, act: Dictionary) -> Array:
	return dispatch(game, {"kind": "action", "act": act}, true)

func select_label(game: Node, packets: Array, needle: String) -> void:
	for packet in packets:
		if str(packet.kind) != "candidates": continue
		for option in packet.options:
			if str(option.label).contains(needle) and bool(option.available):
				var reply := dispatch(game, {"kind": "select", "choice_id": str(option.id), "token": str(option.token)})
				select_id(game, reply, "cancel")
				return
	check(false, "实际候选含所需交谈入口：" + needle)

func select_id(game: Node, packets: Array, id: String) -> void:
	for packet in packets:
		if str(packet.kind) not in ["candidates", "card"]: continue
		for option in packet.options:
			if str(option.id) == id:
				dispatch(game, {"kind": "select", "choice_id": id, "token": str(option.token)})
				return

func planned_choice(options: Array) -> Dictionary:
	var available: Array = options.filter(func(option): return bool(option.available))
	if available.is_empty(): return {}
	if str(available[0].id).begins_with("claim:") or str(available[0].id).begins_with("evidence:"):
		var is_claim := str(available[0].id).begins_with("claim:")
		var wanted := "pause"
		if planned_pair_index < planned_pairs.size():
			wanted = ("claim:" if is_claim else "evidence:") + str(planned_pairs[planned_pair_index][0 if is_claim else 1])
		for option in available:
			if str(option.id) == wanted:
				if not is_claim: planned_pair_index += 1
				return option
		check(false, "原有效陈述/证据对出现在真实候选中：" + wanted)
		return {}
	if str(available[0].id).begins_with("support:"): return available[-1]
	return available[0]

func dispatch(game: Node, input: Dictionary, resolve_choices := false) -> Array:
	var queue: Array = game.engine.Dispatch(input)
	var observed: Array = []
	latest_lines = []
	var iterations := 0
	while not queue.is_empty() and iterations < 256:
		iterations += 1
		var packet: Dictionary = queue.pop_front()
		observed.append(packet)
		match str(packet.kind):
			"state": game._apply_snapshot(packet.snapshot)
			"scene":
				game._applying_scene = true
				game.load_map(str(packet.map), str(packet.get("spawn", "")))
				game._applying_scene = false
			"lines":
				for line in packet.lines: latest_lines.append(str(line.text))
				queue.append_array(game.engine.Dispatch({"kind": "presented", "presentation_id": str(packet.presentation_id)}))
			"candidates":
				if resolve_choices:
					var chosen := planned_choice(packet.options)
					check(not chosen.is_empty(), "章节选择有实际可用候选")
					if not chosen.is_empty():
						queue.append_array(game.engine.Dispatch({"kind": "select", "choice_id": str(chosen.id), "token": str(chosen.token)}))
			"card":
				for option in packet.options:
					if str(option.id) == "close": queue.append_array(game.engine.Dispatch({"kind": "select", "choice_id": "close", "token": str(option.token)}))
			"end": ending_result = packet.result
			"error": check(false, "Core事件错误：" + str(packet.message))
	check(queue.is_empty(), "章节事件在有限步骤内完成")
	game.people = game.engine.People()
	game._spawn_actors(false)
	return observed

func _run() -> void:
	# Inject a real chapter-one handoff for this standalone automatic fixture.
	# This is not a completed playthrough or a JSON reconstruction.
	var first: Node = preload("res://scenes/Awakening/AwakeningEngine.cs").new()
	root.add_child(first)
	first.ChapterEnd([], false, false, false)
	var game: Node2D = (load("res://scenes/Chapter2/Game/Chapter2Game.tscn") as PackedScene).instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	await process_frame
	await process_frame
	if game.startup_error != "":
		check(false, "第二章真实继承启动：" + game.startup_error)
		game.begin_shutdown()
		game.queue_free()
		first.queue_free()
		call_deferred("_finish", 1)
		return
	var t0 := Time.get_ticks_msec()
	for key in ["zone_yi", "living_hall", "zone_jia", "zone_bing", "zone_ding", "central_hall", "lecture", "logistics", "granary", "clinic", "archive", "counterweight", "resonance", "greenhouse", "campus_s", "campus_n", "campus_sw"]:
		var t := Time.get_ticks_msec()
		game.load_map(key, game.SPAWN_OF.get(key, ""))
		await process_frame
		check(game.grid != null and game._walkable(game.player.position), "%s 载入，出生点可走（%d ms，%d 处可检视）" % [key, Time.get_ticks_msec() - t, game.sites.size()])
	var document = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Chapter2/Script/generated_chapter_02.json"))
	check(document is Dictionary, "正式 .world 编译文件可读取")
	var titles: Dictionary = {}
	if document is Dictionary:
		for chapter in document.chapters:
			for sequence in chapter.sequences: titles[str(sequence.id)] = true
	for title in ["arrival", "gather", "lesson", "free", "noon_dispute", "after", "replay_unlock", "confront_rule", "council", "box"]:
		check(titles.has(title), "正式章节段落 " + title)
	# Drive actual chapter inputs and acknowledge semantic presentations; this is automation.
	var initial := dispatch(game, {"kind": "begin"}, true)
	check(initial.any(func(packet): return str(packet.kind) == "lines"), "正式 .world 章节产生语义台词")
	# 第 2 日
	check(game.actors.size() > 3, "讲堂里有人（%d）" % game.actors.size())
	game.stage = "field"
	run_action(game, {"cmd": "collect"})
	run_action(game, {"cmd": "stay", "place": "Logistics"})
	dispatch(game, {"kind": "interact", "map": "logistics", "site": "ration_notice", "target": ""})
	run_action(game, {"cmd": "tell_rule", "who": "Ward", "rule": int(game.engine.KnownRules()[0].id)})
	check(game.upcoming_slot() == 5, "第 2 日走到夜里")
	run_action(game, {"cmd": "sleep"})
	check(game.upcoming_day() == 3 and game.upcoming_slot() == 1, "第 3 日清晨已结算")
	var lines: Array = latest_lines
	check(lines.any(func(l): return str(l).contains("台账")), "清晨提到台账")
	for pair in [["logistics", "ledger"], ["granary", "latch"], ["clinic", "empty_bowl"], ["zone_jia", "litter"], ["central_hall", "central_notice"]]:
		dispatch(game, {"kind": "move", "place": game.MAP_PLACE[pair[0]]})
		dispatch(game, {"kind": "interact", "map": pair[0], "site": pair[1], "target": ""})
	for who in ["Bertrand", "Margaret", "Mora", "Quer"]:
		for person in game.people:
			if str(person.key) == who: dispatch(game, {"kind": "move", "place": str(person.place)})
		var menu := dispatch(game, {"kind": "interact", "map": "", "site": "person:" + who, "target": who})
		select_label(game, menu, "知道")
	dispatch(game, {"kind": "replay", "vars": [0, 0, 0, 0]})
	var r: Dictionary = game.replay_result
	check(r.steps.size() > 0, "重演有墨影步子（%d），全吻合=%s" % [r.steps.size(), str(r.full_match)])
	for person in game.people:
		if str(person.key) == "Ward": dispatch(game, {"kind": "move", "place": str(person.place)})
	run_action(game, {"cmd": "escort", "who": "Ward", "thing": "配给台公告"})
	run_action(game, {"cmd": "collect"})
	run_action(game, {"cmd": "stay", "place": "Hall"})
	check(game.upcoming_day() == 3 and game.upcoming_slot() == 4, "到第 3 日傍晚")
	var pairs := []
	for p in [[0, 0], [2, 4], [1, 2], [3, 5]]:
		if game.engine.PairValid(p[0], p[1]):
			pairs.append(p)
	planned_pairs = pairs
	planned_pair_index = 0
	run_action(game, {"cmd": "stay", "place": "Hall"})
	check(latest_lines.any(func(l): return str(l).contains("中央厅")), "对账发生（%d 对有效）" % pairs.size())
	dispatch(game, {"kind": "move", "place": "ArchiveFront"})
	run_action(game, {"cmd": "watch", "place": "ArchiveFront"})
	check(game.engine.Ended(), "第 4 日：章节到议事")
	var result: Dictionary = ending_result
	check(str(result.result_text) != "", "议事结果：" + str(result.result_text))
	print("用时 %d ms；失败 %d" % [Time.get_ticks_msec() - t0, failures])
	game.begin_shutdown()
	game.queue_free()
	first.queue_free()
	call_deferred("_finish", 1 if failures > 0 else 0)
