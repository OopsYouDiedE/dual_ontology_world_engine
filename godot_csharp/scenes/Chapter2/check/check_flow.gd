extends SceneTree

const Samples := preload("res://scenes/Shared/check/fixture_data.gd")
## 第二章自动流程检查：章一终局续章、地图、正式台词、多日推演、重演、对账与议事。
## 用法：& $env:GODOT --headless --path godot_csharp --script res://scenes/Chapter2/check/check_flow.gd
## 经 Main.tscn 的 dispatch 与正式 UI 回执驱动；调试跳转与代按不替代游玩验收。

const Fixture := preload("res://scenes/Chapter2/check/chapter2_fixture.gd")
var SPAWN_OF := Samples.dictionary("chapter2-flow-check", "spawns")
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
	return check(await fx.send(input, resolve_choices), Samples.text("chapter2-flow-check", "text-001") + str(input.kind))

func run_action(act: Dictionary) -> bool:
	return await send({"kind": "action", "act": act}, true)

func upcoming_day() -> int: return int(fx.probe("next_day"))
func upcoming_slot() -> int: return int(fx.probe("next_slot"))

func move_to_map(key: String) -> bool:
	return await fx.move_to_map(key, str(SPAWN_OF.get(key, "")))

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
func handoff() -> bool:
	host = await fx.prepare()
	fx.set_progress("state", "detective_name", "flow2-fixture")
	await fx.jump("answer_done")
	# answer_done 保留已答完的进度但禁用行走；恢复调查阶段后才可从现场打开菜单。
	await fx.jump("investigate")
	var ending_started := Time.get_unix_time_from_system()
	if not await send({"kind": "interact", "site": "E8"}): return false
	if not check(host.ui.modal_kind == "menu" and fx.select("answer"), Samples.text("chapter2-flow-check", "text-002")): return false
	if not check(await fx.settle(false, 60.0, func(): return fx.ended() and host.ui.modal_kind == "ending"), Samples.text("chapter2-flow-check", "text-003")): return false
	if not check(fx.ended() and fx.progress("flags").has("door_open") and host.ui.modal_kind == "ending", Samples.text("chapter2-flow-check", "text-004")): return false
	check(fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, Samples.text("chapter2-flow-check", "text-005"))
	check(fx.seen_ids.has("s10_open.015"), Samples.text("chapter2-flow-check", "text-006"))
	var saved = JSON.parse_string(FileAccess.get_file_as_string("user://chapter1_end.json")) if FileAccess.file_exists("user://chapter1_end.json") else null
	check(saved is Dictionary and str(saved.get("detective_name", "")) == "flow2-fixture" and not bool(saved.get("rested", true))
		and FileAccess.get_modified_time("user://chapter1_end.json") >= int(ending_started),
		"章一终局写入本轮 user://chapter1_end.json")
	var actions: Array = host.ui._modal_event.get("actions", [])
	var continuation := actions.filter(func(a): return str(a.id) == "continue" and bool(a.get("available", true)))
	if not check(continuation.size() == 1 and str(continuation[0].get("key", "")) == "C"
		and str(continuation[0].label) == fx.text(Samples.text("chapter2-flow-check", "text-007")), Samples.text("chapter2-flow-check", "text-008")): return false
	var engine_id: int = host.engine.get_instance_id()
	var old_field_id: int = host.field.get_instance_id()
	fx.clear_observed()
	var key := InputEventKey.new()
	key.keycode = KEY_C
	key.physical_keycode = KEY_C
	key.pressed = true
	host.ui._unhandled_input(key)
	if not await fx.wait_until(fx.campus_loaded, 10.0):
		check(false, Samples.text("chapter2-flow-check", "text-009") + fx.stage())
		return false
	check(host.engine.get_instance_id() == engine_id and host.field.get_instance_id() != old_field_id, Samples.text("chapter2-flow-check", "text-010"))
	if not check(await fx.settle(true), Samples.text("chapter2-flow-check", "text-011")): return false
	if not check(fx.stage() == "field" and bool(host.ui.snapshot.get("field_enabled", false)) and host.field.field_active()
		and not host.ui.is_busy() and not fx.ended(), Samples.text("chapter2-flow-check", "text-012")): return false
	check(str(host.ui.snapshot.get("detective_name", "")) == "flow2-fixture", Samples.text("chapter2-flow-check", "text-013"))
	check(fx.saw_sequence("arrival") and not fx.saw_sequence("lesson") and not fx.saw_sequence("free"), Samples.text("chapter2-flow-check", "text-014"))
	var walkable: bool = host.field.adapter.walkable(fx.player().position)
	print("    handoff field: map=%s position=%s walkable=%s stage=%s" % [host.field.adapter.map_key(), fx.player().position, walkable, fx.stage()])
	check(host.field.adapter.map_key() == "living_hall", Samples.text("chapter2-flow-check", "text-015"))
	check(walkable, Samples.text("chapter2-flow-check", "text-016"))
	check(host.field.actors.size() > 3, Samples.text("chapter2-flow-check", "text-017") % host.field.actors.size())
	return true

func _run() -> void:
	var t0 := Time.get_ticks_msec()
	fx = Fixture.new(self)
	fx.on_error = func(message: String) -> void: check(false, Samples.text("chapter2-flow-check", "text-018") + message)
	fx.choose = planned_choice
	if not await handoff():
		await _finish()
		return
	# 保留多地图出生点检查；检查换图后恢复续章起点，不再第二次发送 begin。
	for key in Samples.array("chapter2-flow-check", "case-001"):
		var t := Time.get_ticks_msec()
		if not await move_to_map(key):
			check(false, Samples.text("chapter2-flow-check", "text-019") + key)
			await _finish()
			return
		check(host.field.adapter.walkable(fx.player().position), Samples.text("chapter2-flow-check", "text-020") % [key, Time.get_ticks_msec() - t, host.field.sites.size()])
	await move_to_map("living_hall")
	var document = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Chapter2/Script/generated_chapter_02.json"))
	if not check(document is Dictionary, Samples.text("chapter2-flow-check", "text-021")):
		await _finish()
		return
	var titles: Dictionary = {}
	if document is Dictionary:
		for chapter in document.chapters:
			for sequence in chapter.sequences: titles[str(sequence.id)] = true
	for title in Samples.array("chapter2-flow-check", "case-002"):
		check(titles.has(title), Samples.text("chapter2-flow-check", "text-022") + title)
	# 始业日余下的时段。
	var first_guard := 0
	while upcoming_slot() < 5 and first_guard < 8:
		first_guard += 1
		if not await run_action({"cmd": "stay"}): break
	check(upcoming_day() == int(fx.probe("first_day")) and upcoming_slot() == 5, Samples.text("chapter2-flow-check", "text-023"))
	check(fx.saw_sequence("meet_jia") and fx.saw_sequence("supper") and fx.saw_sequence("first_night"), Samples.text("chapter2-flow-check", "text-024"))
	await run_action({"cmd": "sleep"})
	check(fx.saw_sequence("meet_bing", true) and host.field.adapter.map_key() == "living_hall", Samples.text("chapter2-flow-check", "text-025"))
	await run_action({"cmd": "stay"})
	check(fx.saw_sequence("gather", true) and fx.saw_sequence("lesson", true) and fx.saw_sequence("free", true), Samples.text("chapter2-flow-check", "text-026"))
	check(fx.saw_sequence("meet_ding", true) and fx.saw_sequence("noon_dispute", true) and fx.saw_sequence("open_archive_wing", true), Samples.text("chapter2-flow-check", "text-027"))
	await move_to_map("logistics")
	await send({"kind": "interact", "map": "logistics", "site": "ration_notice"})
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
	check(told, Samples.text("chapter2-flow-check", "text-028"))
	if not check(upcoming_day() == case_day and upcoming_slot() == 1, Samples.text("chapter2-flow-check", "text-029") % case_day):
		await _finish()
		return
	check(fx.saw_sequence("dawn", true), Samples.text("chapter2-flow-check", "text-030"))
	for pair in Samples.array("chapter2-flow-check", "case-003"):
		await move_to_map(pair[0])
		await send({"kind": "interact", "map": pair[0], "site": pair[1]})
	for who in Samples.array("chapter2-flow-check", "case-004"):
		if not check(await fx.move_to_person(who, SPAWN_OF), Samples.text("chapter2-flow-check", "text-031") % who): continue
		await send({"kind": "interact", "site": "person:" + who, "target": who})
		if not check(fx.select("ask"), Samples.text("chapter2-flow-check", "text-032") % who): continue
		check(await fx.settle(), Samples.text("chapter2-flow-check", "text-033") % who)
		if host.ui.modal_kind == "menu":
			check(fx.select("cancel"), Samples.text("chapter2-flow-check", "text-034") + who)
			await fx.settle()
	if not await send({"kind": "replay", "vars": [0, 0, 0, 0]}):
		await _finish()
		return
	var r: Dictionary = host.ui.snapshot.get("replay_result", {})
	var steps: Array = r.get("steps", [])
	check(not steps.is_empty(), Samples.text("chapter2-flow-check", "text-035") % [steps.size(), str(r.get("full_match", false))])
	check(host.ui.modal_kind == "replay", Samples.text("chapter2-flow-check", "text-036"))
	fx.close_modal()
	if check(await fx.move_to_person("Ward", SPAWN_OF), Samples.text("chapter2-flow-check", "text-037")):
		var thing := str(host.ui.snapshot.get("examined", {}).get("logistics:ration_notice", {}).get("thing", ""))
		check(thing != "", Samples.text("chapter2-flow-check", "text-038"))
		var before := upcoming_slot()
		await run_action({"cmd": "escort", "who": "Ward", "thing": thing})
		check(upcoming_slot() == before + 1, Samples.text("chapter2-flow-check", "text-039"))
	await run_action({"cmd": "collect"})
	# 傍晚边界立即演对账：先停在真实候选，核对时钟，再配置代选策略。
	await send({"kind": "action", "act": {"cmd": "stay"}})
	check(upcoming_day() == case_day and upcoming_slot() == 4, Samples.text("chapter2-flow-check", "text-040"))
	planned_pairs = fx.probe("valid_confront_pairs")
	planned_pair_index = 0
	check(await fx.settle(true), Samples.text("chapter2-flow-check", "text-041"))
	check(fx.saw_sequence("evening", true) and fx.saw_sequence("confront_begin", true), Samples.text("chapter2-flow-check", "text-042") % planned_pairs.size())
	check(planned_pair_index == planned_pairs.size(), "有效陈述/证据对全部通过真实候选提交")
	# 对账结算后是夜晚，守夜必须先在现场走到对应空间。
	await move_to_map("archive")
	await run_action({"cmd": "watch", "place": "ArchiveFront"})
	check(bool(fx.probe("ended")) and fx.ended() and host.ui.modal_kind == "ending", Samples.text("chapter2-flow-check", "text-043"))
	var result: Dictionary = host.ui._modal_event
	var rows: Array = result.get("rows", [])
	var template := str(document.chapters[0].design.text[Samples.text("chapter2-flow-check", "text-044")])
	check(not rows.is_empty() and str(rows[0]).begins_with(template.get_slice("{", 0)), Samples.text("chapter2-flow-check", "text-045"))
	print(Samples.text("chapter2-flow-check", "text-046") % [Time.get_ticks_msec() - t0])
	await _finish()
