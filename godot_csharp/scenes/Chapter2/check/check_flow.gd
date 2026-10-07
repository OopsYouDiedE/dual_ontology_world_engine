extends SceneTree
## 第二章自动事件检查：地图与寻路、正式章节台词、三日推演、重演、对账与议事。
## 用法：& $env:GODOT --headless --path godot_csharp --script res://scenes/Chapter2/check/check_flow.gd
## 这是注入状态的自动检查，不替代游玩。经统一宿主（ChapterHost / ChapterField / ChapterUI）驱动同一事件接口。

const MAP_PLACE := {
	"central_hall": "Hall", "lecture": "Lecture", "logistics": "Logistics", "granary": "Logistics", "clinic": "Clinic",
	"greenhouse": "Greenhouse", "archive": "ArchiveFront", "counterweight": "ArchiveFront", "campus_s": "Oath", "living_hall": "DormLife",
	"zone_yi": "DormLife", "zone_jia": "DormW", "zone_bing": "DormS", "zone_ding": "DormL", "resonance": "Resonance",
}
const SPAWN_OF := {
	"central_hall": "south", "lecture": "from_central", "logistics": "west", "granary": "south", "clinic": "south", "greenhouse": "south",
	"archive": "south", "campus_s": "gate_inside", "living_hall": "north_door", "zone_yi": "from_hall", "zone_jia": "from_hall",
	"zone_bing": "from_hall", "zone_ding": "from_hall", "resonance": "south", "counterweight": "from_stairs",
}
var replay_steps: Array = []

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
var game_ref: Node = null
var planned_pair_index := 0

func run_action(game: Node, act: Dictionary) -> Array:
	return dispatch(game, {"kind": "action", "act": act}, true)

func select_label(game: Node, packets: Array, needle: String) -> void:
	for packet in packets:
		if str(packet.kind) != "candidates": continue
		for option in packet.options:
			if str(option.label).contains(needle) and bool(option.available):
				var reply := dispatch(game, {"kind": "select", "choice_id": str(option.id), "token": str(option.token), "presentation_id": str(packet.get("presentation_id", ""))})
				select_id(game, reply, "cancel")
				return
	check(false, "实际候选含所需交谈入口：" + needle)

func select_id(game: Node, packets: Array, id: String) -> void:
	for packet in packets:
		if str(packet.kind) not in ["candidates", "card"]: continue
		for option in packet.options:
			if str(option.id) == id:
				dispatch(game, {"kind": "select", "choice_id": id, "token": str(option.token), "presentation_id": str(packet.get("presentation_id", ""))})
				return

func planned_choice(options: Array) -> Dictionary:
	var available: Array = options.filter(func(option): return bool(option.available))
	if available.is_empty(): return {}
	var first_id := str(available[0].id)
	if first_id.begins_with("claim:") or first_id.begins_with("template:") or first_id.begins_with("evidence:"):
		var is_evidence := first_id.begins_with("evidence:")
		var wanted := "pause"
		if planned_pair_index < planned_pairs.size():
			var pair: Array = planned_pairs[planned_pair_index]
			if is_evidence:
				wanted = "evidence:" + str(pair[1])
			else:
				# 第一层是“什么事”：模板主张先选模板项，第二层才是具体的人。
				wanted = "claim:" + str(pair[0])
				if not available.any(func(o): return str(o.id) == wanted):
					wanted = str(game_ref.engine.Probe("confront_top_id", [int(pair[0])]))
		for option in available:
			if str(option.id) == wanted:
				if is_evidence: planned_pair_index += 1
				return option
		check(false, "原有效陈述/证据对出现在真实候选中：" + wanted)
		return {}
	if str(available[0].id).begins_with("support:"): return available[-1]
	return available[0]

func send(game: Node, input: Dictionary) -> Array:
	var context: Dictionary = game.field.context()
	context.modal = ""
	input.context = context
	return game.engine.Dispatch(input)

func upcoming_day(game: Node) -> int: return int(game.engine.Probe("next_day", []))
func upcoming_slot(game: Node) -> int: return int(game.engine.Probe("next_slot", []))
func people(game: Node) -> Array: return game.ui.snapshot.get("people", [])

func dispatch(game: Node, input: Dictionary, resolve_choices := false) -> Array:
	var queue: Array = send(game, input)
	var observed: Array = []
	latest_lines = []
	var iterations := 0
	while not queue.is_empty() and iterations < 256:
		iterations += 1
		var packet: Dictionary = queue.pop_front()
		observed.append(packet)
		match str(packet.kind):
			"state": game._apply(packet.snapshot)
			"scene":
				game.field._applying_scene = true
				game.field.load_map(str(packet.map), str(packet.get("spawn", "")))
				game.field._applying_scene = false
			"lines":
				for line in packet.lines: latest_lines.append(str(line.text))
				queue.append_array(send(game, {"kind": "presented", "presentation_id": str(packet.presentation_id)}))
			"replay_play":
				replay_steps = packet.get("steps", [])
				queue.append_array(send(game, {"kind": "presented", "presentation_id": str(packet.presentation_id)}))
			"candidates":
				if resolve_choices:
					var chosen := planned_choice(packet.options)
					check(not chosen.is_empty(), "章节选择有实际可用候选")
					if not chosen.is_empty():
						queue.append_array(send(game, {"kind": "select", "choice_id": str(chosen.id), "token": str(chosen.token)}))
			"card":
				for option in packet.options:
					if str(option.id) == "close": queue.append_array(send(game, {"kind": "select", "choice_id": "close", "token": str(option.token)}))
			"ending": ending_result = packet
			"error": check(false, "Core事件错误：" + str(packet.message))
	check(queue.is_empty(), "章节事件在有限步骤内完成")
	return observed

func _run() -> void:
	# Inject a real chapter-one handoff for this standalone automatic fixture.
	# This is not a completed playthrough or a JSON reconstruction.
	var first: Node = preload("res://scenes/Shared/ChapterEngine.cs").new()
	root.add_child(first)
	first.Open("chapter1")
	first.Probe("publish", [])
	var game: Node = (load("res://scenes/Chapter2/Chapter2.tscn") as PackedScene).instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	await process_frame
	await process_frame
	if not game.engine.Started():
		check(false, "第二章真实继承启动")
		game.begin_shutdown()
		game.queue_free()
		first.queue_free()
		call_deferred("_finish", 1)
		return
	var t0 := Time.get_ticks_msec()
	for key in ["zone_yi", "living_hall", "zone_jia", "zone_bing", "zone_ding", "central_hall", "lecture", "logistics", "granary", "clinic", "archive", "counterweight", "resonance", "greenhouse", "campus_s", "campus_n", "campus_sw"]:
		var t := Time.get_ticks_msec()
		game.field.load_map(key, SPAWN_OF.get(key, ""))
		await process_frame
		check(game.field.adapter.walkable(game.field.player.position), "%s 载入，出生点可走（%d ms，%d 处可检视）" % [key, Time.get_ticks_msec() - t, game.field.sites.size()])
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
	check(game.field.actors.size() > 3, "讲堂里有人（%d）" % game.field.actors.size())
	run_action(game, {"cmd": "collect"})
	run_action(game, {"cmd": "stay", "place": "Logistics"})
	dispatch(game, {"kind": "interact", "map": "logistics", "site": "ration_notice", "target": ""})
	# 始业日门开已近傍晚，余下几阵就地歇到夜里。
	var first_guard := 0
	while upcoming_slot(game) < 5 and first_guard < 8:
		first_guard += 1
		run_action(game, {"cmd": "stay", "place": "Hall"})
	check(upcoming_day(game) == int(game.engine.Probe("first_day", [])) and upcoming_slot(game) == 5, "始业日走到夜里")
	run_action(game, {"cmd": "sleep"})
	# 五日：始业日之后是日常篇，每一阵就地歇着（饭点领饭），夜里睡下，直到案发次日的清晨。
	var case_day: int = int(game.engine.Probe("case_day", []))
	var guard := 0
	var told := false
	while upcoming_day(game) < case_day and guard < 40:
		guard += 1
		# 日常篇里有人贴出规矩后，当面讲给沃德听（讲规矩要同处一地）。
		var tellable: Array = game.engine.Probe("known_rules", []).filter(func(r): return bool(r.tellable))
		if not told and tellable.size() > 0 and upcoming_slot(game) < 5:
			for person in people(game):
				if str(person.key) == "Ward": dispatch(game, {"kind": "move", "place": str(person.place)})
			run_action(game, {"cmd": "tell_rule", "who": "Ward", "rule": int(tellable[0].id)})
			told = true
			continue
		if upcoming_slot(game) == 5:
			run_action(game, {"cmd": "sleep"})
		else:
			run_action(game, {"cmd": "stay", "place": "Hall"})
	check(told, "日常篇里当面讲过一条规矩")
	check(upcoming_day(game) == case_day and upcoming_slot(game) == 1, "案发次日清晨已结算（第 %d 日）" % case_day)
	var lines: Array = latest_lines
	check(lines.any(func(l): return str(l).contains("台账")), "清晨提到台账")
	for pair in [["logistics", "ledger"], ["granary", "latch"], ["clinic", "empty_bowl"], ["zone_jia", "litter"], ["central_hall", "central_notice"]]:
		dispatch(game, {"kind": "move", "place": MAP_PLACE[pair[0]]})
		dispatch(game, {"kind": "interact", "map": pair[0], "site": pair[1], "target": ""})
	for who in ["Bertrand", "Margaret", "Mora", "Quer"]:
		# 问话要当面：走到此人所在的地图，站到他身边。
		for person in people(game):
			if str(person.key) == who and str(person.get("map", "")) != "":
				game.field.load_map(str(person.map), SPAWN_OF.get(str(person.map), ""))
		await process_frame
		var actor: CharacterBody2D = game.field.actors.get(who)
		check(actor != null, "%s 站在自己的去处" % who)
		if actor: game.field.player.position = actor.position + Vector2(24, 0)
		for i in 6: await process_frame
		var menu := dispatch(game, {"kind": "interact", "map": "", "site": "person:" + who, "target": who})
		select_label(game, menu, "知道")
	dispatch(game, {"kind": "replay", "vars": [0, 0, 0, 0]})
	var r: Dictionary = game.ui.snapshot.replay.result
	check(replay_steps.size() > 0, "重演有墨影步子（%d），全吻合=%s" % [replay_steps.size(), str(r.get("full_match", false))])
	for person in people(game):
		if str(person.key) == "Ward": dispatch(game, {"kind": "move", "place": str(person.place)})
	run_action(game, {"cmd": "escort", "who": "Ward", "thing": "配给台公告"})
	run_action(game, {"cmd": "collect"})
	run_action(game, {"cmd": "stay", "place": "Hall"})
	check(upcoming_day(game) == case_day and upcoming_slot(game) == 4, "到案发次日傍晚")
	# 现实里为真的主张各配一件手里相关的证据（谁做了什么按推演的结果）。
	var pairs: Array = game.engine.Probe("valid_confront_pairs", [])
	game_ref = game
	planned_pairs = pairs
	planned_pair_index = 0
	run_action(game, {"cmd": "stay", "place": "Hall"})
	check(latest_lines.any(func(l): return str(l).contains("中央厅")), "对账发生（%d 对有效）" % pairs.size())
	dispatch(game, {"kind": "move", "place": "ArchiveFront"})
	run_action(game, {"cmd": "watch", "place": "ArchiveFront"})
	check(bool(game.engine.Probe("ended", [])), "议事日：章节到议事")
	var result: Dictionary = ending_result
	check(not result.get("rows", []).is_empty() and str(result.rows[0]).begins_with("名录库："), "议事结果：" + str(result.get("rows", [""])[0]))
	print("用时 %d ms；失败 %d" % [Time.get_ticks_msec() - t0, failures])
	game.begin_shutdown()
	game.queue_free()
	first.queue_free()
	call_deferred("_finish", 1 if failures > 0 else 0)
