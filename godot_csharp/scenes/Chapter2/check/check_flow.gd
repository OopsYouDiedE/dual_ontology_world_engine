extends SceneTree
## 第二章自动流程检查：章一终局续章、地图、正式台词、多日推演、重演、对账与议事。
## 用法：& $env:GODOT --headless --path godot_csharp --script res://scenes/Chapter2/check/check_flow.gd
## 经 Main.tscn 的 dispatch 与正式 UI 回执驱动；调试跳转与代按不替代游玩验收。

const Fixture := preload("res://scenes/Chapter2/check/chapter2_fixture.gd")
# STORY-HARDCODE[CH2-016][logic][tool]: 自动流程预写具体地图出生点、角色、案件操作顺序和终局检查。
const SPAWN_OF := {
	"central_hall": "south", "lecture": "from_central", "logistics": "west", "granary": "south", "clinic": "south", "greenhouse": "south",
	"archive": "south", "campus_s": "gate_inside", "living_hall": "north_door", "zone_yi": "from_hall", "zone_jia": "from_hall",
	"zone_bing": "from_hall", "zone_ding": "from_hall", "resonance": "south", "counterweight": "from_stairs",
}
var checks := 0
var failures := 0
var failed_names: Array[String] = []
var fx: Fixture
var host: Node
var planned_pairs: Array = []
var planned_pair_index := 0

func check(ok: bool, what: String) -> bool:
	checks += 1
	print(("  ok  " if ok else "  FAIL ") + what)
	if not ok:
		failures += 1
		failed_names.append(what)
	return ok

func _initialize() -> void:
	_run.call_deferred()

func _finish() -> void:
	print("TOTAL %d checks, %d passed, %d failed" % [checks, checks - failures, failures])
	for name in failed_names: print("FAILED: " + name)
	await fx.shutdown()
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(1 if failures > 0 else 0)

func send(input: Dictionary, resolve_choices := false) -> bool:
	return check(await fx.send(input, resolve_choices), "章节事件在有限时间内完成：" + str(input.kind))

func run_action(act: Dictionary) -> bool:
	return await send({"kind": "action", "act": act}, true)

func upcoming_day() -> int: return int(fx.probe("next_day"))
func upcoming_slot() -> int: return int(fx.probe("next_slot"))

func move_to_map(key: String) -> bool:
	return await fx.move_to_map(key, str(SPAWN_OF.get(key, "")))

# STORY-HARDCODE[CH2-017][logic][tool]: 自动验收按预定证词、证据、支持与分支策略代选。
func planned_choice(options: Array) -> String:
	var available: Array = options.filter(func(option): return bool(option.get("available", true)))
	if available.is_empty(): return ""
	var first_id := str(available[0].id)
	if first_id.begins_with("claim:") or first_id.begins_with("template:") or first_id.begins_with("evidence:"):
		var is_evidence := first_id.begins_with("evidence:")
		var wanted := "pause"
		if planned_pair_index < planned_pairs.size():
			var pair: Array = planned_pairs[planned_pair_index]
			if is_evidence:
				wanted = "evidence:" + str(pair[1])
			else:
				wanted = "claim:" + str(pair[0])
				if not available.any(func(o): return str(o.id) == wanted):
					wanted = str(fx.probe("confront_top_id", [int(pair[0])]))
		for option in available:
			if str(option.id) == wanted:
				if is_evidence: planned_pair_index += 1
				return wanted
		check(false, "原有效陈述/证据对出现在真实候选中：" + wanted)
		return ""
	if first_id.begins_with("support:"): return str(available[-1].id)
	return first_id

## 同一 Main 会话完成终局、保存、按 C；不通过 publish 探针伪造章一结束。
# STORY-HARDCODE[CH2-018][logic][tool]: 测试构造章一继承状态并预置姓名、答案和章二交接。
func handoff() -> bool:
	host = await fx.prepare()
	fx.set_progress("state", "detective_name", "flow2-fixture")
	await fx.jump("answer_done")
	# answer_done 保留已答完的进度但禁用行走；恢复调查阶段后才可从现场打开菜单。
	await fx.jump("investigate")
	var ending_started := Time.get_unix_time_from_system()
	if not await send({"kind": "interact", "site": "E8"}): return false
	if not check(host.ui.modal_kind == "menu" and fx.select("answer"), "章一调试答题完成后从正式菜单进入终局"): return false
	if not check(await fx.settle(false, 60.0, func(): return fx.ended() and host.ui.modal_kind == "ending"), "章一开门演出在有限时间内完成"): return false
	if not check(fx.ended() and fx.progress("flags").has("door_open") and host.ui.modal_kind == "ending", "章一门开、结束状态与章末卡同时成立"): return false
	check(fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, "章一隔离门实际切换为开启")
	check(fx.seen_ids.has("s10_open.015"), "章一终局正式台词经过 UI")
	var saved = JSON.parse_string(FileAccess.get_file_as_string("user://chapter1_end.json")) if FileAccess.file_exists("user://chapter1_end.json") else null
	check(saved is Dictionary and str(saved.get("detective_name", "")) == "flow2-fixture" and not bool(saved.get("rested", true))
		and FileAccess.get_modified_time("user://chapter1_end.json") >= int(ending_started),
		"章一终局写入本轮 user://chapter1_end.json")
	var actions: Array = host.ui._modal_event.get("actions", [])
	var continuation := actions.filter(func(a): return str(a.id) == "continue" and bool(a.get("available", true)))
	if not check(continuation.size() == 1 and str(continuation[0].get("key", "")) == "C"
		and str(continuation[0].label) == fx.text("结局·继续按钮"), "章一章末卡提供继续进入章二（C）"): return false
	var engine_id: int = host.engine.get_instance_id()
	var old_field_id: int = host.field.get_instance_id()
	fx.clear_observed()
	var key := InputEventKey.new()
	key.keycode = KEY_C
	key.physical_keycode = KEY_C
	key.pressed = true
	host.ui._unhandled_input(key)
	if not await fx.wait_until(fx.campus_loaded, 10.0):
		check(false, "继续后加载第二章视图（失败即停止）：" + fx.stage())
		return false
	check(host.engine.get_instance_id() == engine_id and host.field.get_instance_id() != old_field_id, "同一 Main 引擎替换章一现场为章二视图")
	if not check(await fx.settle(true), "第二章 view_ready 后正式开场完成"): return false
	if not check(fx.stage() == "field" and bool(host.ui.snapshot.get("field_enabled", false)) and host.field.field_active()
		and not host.ui.is_busy() and not fx.ended(), "章一继续后到达章二第一个可操作状态"): return false
	check(str(host.ui.snapshot.get("detective_name", "")) == "flow2-fixture", "第二章继承本轮章一玩家状态")
	check(fx.saw_sequence("arrival") and fx.saw_sequence("lesson") and fx.saw_sequence("free"), "正式 .story 章节产生开场、首课与自由行动台词")
	var walkable: bool = host.field.adapter.walkable(fx.player().position)
	print("    handoff field: map=%s position=%s walkable=%s stage=%s" % [host.field.adapter.map_key(), fx.player().position, walkable, fx.stage()])
	check(host.field.adapter.map_key() == "lecture", "章二第一个可操作状态在讲堂")
	check(walkable, "章二第一个可操作状态的出生点可走")
	check(host.field.actors.size() > 3, "讲堂里有人（%d）" % host.field.actors.size())
	return true

# STORY-HARDCODE[CH2-019][logic][tool]: 测试硬编码第二章时间推进、规则转述、特定人物问话、证物和议事流程。
func _run() -> void:
	var t0 := Time.get_ticks_msec()
	fx = Fixture.new(self)
	fx.on_error = func(message: String) -> void: check(false, "夹具：" + message)
	fx.choose = planned_choice
	if not await handoff():
		await _finish()
		return
	# 保留多地图出生点检查；检查换图后恢复讲堂，不再第二次发送 begin。
	for key in ["zone_yi", "living_hall", "zone_jia", "zone_bing", "zone_ding", "central_hall", "lecture", "logistics", "granary", "clinic", "archive", "counterweight", "resonance", "greenhouse", "campus_s", "campus_n", "campus_sw"]:
		var t := Time.get_ticks_msec()
		if not await move_to_map(key):
			check(false, "地图切换完成：" + key)
			await _finish()
			return
		check(host.field.adapter.walkable(fx.player().position), "%s 载入，出生点可走（%d ms，%d 处可检视）" % [key, Time.get_ticks_msec() - t, host.field.sites.size()])
	await move_to_map("lecture")
	var document = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Chapter2/Script/generated_chapter_02.json"))
	if not check(document is Dictionary, "正式 .story 编译文件可读取"):
		await _finish()
		return
	var titles: Dictionary = {}
	if document is Dictionary:
		for chapter in document.chapters:
			for sequence in chapter.sequences: titles[str(sequence.id)] = true
	for title in ["arrival", "gather", "lesson", "free", "noon_dispute", "after", "replay_unlock", "confront_rule", "council", "box"]:
		check(titles.has(title), "正式章节段落 " + title)
	# 始业日余下的时段。
	await run_action({"cmd": "collect"})
	await move_to_map("logistics")
	await run_action({"cmd": "stay"})
	await send({"kind": "interact", "map": "logistics", "site": "ration_notice"})
	await move_to_map("central_hall")
	var first_guard := 0
	while upcoming_slot() < 5 and first_guard < 8:
		first_guard += 1
		if not await run_action({"cmd": "stay"}): break
	check(upcoming_day() == int(fx.probe("first_day")) and upcoming_slot() == 5, "始业日走到夜里")
	await run_action({"cmd": "sleep"})
	# 日常篇：就地等待，夜里睡下；规矩仍通过实际可用的规则编号转述。
	var case_day := int(fx.probe("case_day"))
	var guard := 0
	var told := false
	while upcoming_day() < case_day and guard < 40:
		guard += 1
		var rules: Array = fx.probe("known_rules")
		var tellable := rules.filter(func(r): return bool(r.tellable))
		if not told and not tellable.is_empty() and upcoming_slot() < 5:
			if await fx.move_to_person("Ward", SPAWN_OF):
				var before := upcoming_slot()
				await run_action({"cmd": "tell_rule", "who": "Ward", "rule": int(tellable[0].id)})
				told = upcoming_slot() != before
				if told: continue
		if upcoming_slot() == 5:
			if not await run_action({"cmd": "sleep"}): break
		else:
			await move_to_map("central_hall")
			if not await run_action({"cmd": "stay"}): break
	check(told, "日常篇里当面讲过一条规矩")
	if not check(upcoming_day() == case_day and upcoming_slot() == 1, "案发次日清晨已结算（第 %d 日）" % case_day):
		await _finish()
		return
	check(fx.saw_sequence("dawn", true), "清晨正式台账场面经过 UI")
	for pair in [["logistics", "ledger"], ["granary", "latch"], ["clinic", "empty_bowl"], ["zone_jia", "litter"], ["central_hall", "central_notice"]]:
		await move_to_map(pair[0])
		await send({"kind": "interact", "map": pair[0], "site": pair[1]})
	for who in ["Bertrand", "Margaret", "Mora", "Quer"]:
		if not check(await fx.move_to_person(who, SPAWN_OF), "%s 站在自己的去处" % who): continue
		await send({"kind": "interact", "site": "person:" + who, "target": who})
		if not check(fx.select("ask"), "实际候选含所需交谈入口：ask（%s）" % who): continue
		check(await fx.settle(), "%s 的正式问话完成" % who)
		if host.ui.modal_kind == "menu":
			check(fx.select("cancel"), "问话后返回现场：" + who)
			await fx.settle()
	if not await send({"kind": "replay", "vars": [0, 0, 0, 0]}):
		await _finish()
		return
	var r: Dictionary = host.ui.snapshot.get("replay_result", {})
	var steps: Array = r.get("steps", [])
	check(not steps.is_empty(), "重演有墨影步子（%d），全吻合=%s" % [steps.size(), str(r.get("full_match", false))])
	check(host.ui.modal_kind == "replay", "正式回放回执后回到重演手稿")
	fx.close_modal()
	if check(await fx.move_to_person("Ward", SPAWN_OF), "请看物证前与 Ward 同处一地"):
		var thing := str(host.ui.snapshot.get("examined", {}).get("logistics:ration_notice", {}).get("thing", ""))
		await run_action({"cmd": "escort", "who": "Ward", "thing": thing})
	await run_action({"cmd": "collect"})
	await run_action({"cmd": "stay"})
	check(upcoming_day() == case_day and upcoming_slot() == 4, "到案发次日傍晚")
	planned_pairs = fx.probe("valid_confront_pairs")
	planned_pair_index = 0
	await run_action({"cmd": "stay"})
	check(fx.saw_sequence("evening", true) and fx.saw_sequence("confront_begin", true), "对账发生（%d 对有效）" % planned_pairs.size())
	check(planned_pair_index == planned_pairs.size(), "有效陈述/证据对全部通过真实候选提交")
	# 对账结算后是夜晚，守夜必须先在现场走到对应空间。
	await move_to_map("archive")
	await run_action({"cmd": "watch", "place": "ArchiveFront"})
	check(bool(fx.probe("ended")) and fx.ended() and host.ui.modal_kind == "ending", "议事日：章节到议事与正式章末卡")
	var result: Dictionary = host.ui._modal_event
	var rows: Array = result.get("rows", [])
	var template := str(document.chapters[0].design.text["终局.名录库"])
	check(not rows.is_empty() and str(rows[0]).begins_with(template.get_slice("{", 0)), "议事结果行使用正式剧本文案")
	print("用时 %d ms" % [Time.get_ticks_msec() - t0])
	await _finish()
