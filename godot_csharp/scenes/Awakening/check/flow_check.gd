extends SceneTree

const Samples := preload("res://scenes/Shared/check/fixture_data.gd")
## 流程跳转回归（统一宿主 Main.tscn）：直接把局面摆到关键状态（醒来、开场广播、学园守则、门厅反转、协助后自动闪回、
## 重演全部吻合、始业之答、门开、认知前置条件），检查可达与无脚本错误。地图为第五轮修订后的乙区（中廊两侧各三间房）。
## 对白与表单由脚本代为推进；这不是游玩验证，不替代独立盲玩。失败不中止，末尾汇总全部失败项。
##   godot --headless --path godot_csharp --script res://scenes/Awakening/check/flow_check.gd

const Fixture := preload("res://scenes/Awakening/check/chapter1_fixture.gd")
const FieldNavigation := preload("res://scenes/Shared/FieldNavigation.gd")
## 恶魔在全章都不提契约第 9、10 条。
var FORBIDDEN := [Samples.text("flow-check", "text-001"), Samples.text("flow-check", "text-002"), Samples.text("flow-check", "text-003"), Samples.text("flow-check", "text-004"), Samples.text("flow-check", "text-005"), Samples.text("flow-check", "text-006"), Samples.text("flow-check", "text-007"), Samples.text("flow-check", "text-008"), Samples.text("flow-check", "text-009"), Samples.text("flow-check", "text-010"), Samples.text("flow-check", "text-011"), Samples.text("flow-check", "text-012")]
## 第五轮修订删去的说法：可数的刻痕、恶魔自报为恶魔或提导师与契约、门外是“主楼走廊”。
var RETIRED := [Samples.text("flow-check", "text-013"), Samples.text("flow-check", "text-014"), Samples.text("flow-check", "text-015"), Samples.text("flow-check", "text-016"), Samples.text("flow-check", "text-017"), Samples.text("flow-check", "text-018"), Samples.text("flow-check", "text-019"), Samples.text("flow-check", "text-020"), Samples.text("flow-check", "text-021")]
var checks := 0
var failures := 0
var failed_names: Array[String] = []
var fx: Fixture
var host: Node

func _initialize() -> void:
	call_deferred("_run")

func _finish(exit_code: int) -> void:
	await fx.shutdown()
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(exit_code)

func check(ok: bool, name: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		failed_names.append(name)
	print(("PASS " if ok else "FAIL ") + name)

func fresh(skip_intro := true) -> Node:
	host = await fx.fresh(skip_intro)
	return host

func seen() -> String: return "\n".join(fx.seen_lines)
func room_name(p: Vector2) -> String: return fx.map().room_name(p)
func route(a: Vector2, b: Vector2) -> PackedVector2Array: return FieldNavigation.route_on_grid(a, b, fx.map().nav_grid(fx.player()))
func walkable(p: Vector2) -> bool: return fx.map().walkable(p, fx.player())
## 读源码文本（章节会话的 C# 源与地图适配器），用于“不再有旧说法”的检查。
func source_text(path: String) -> String: return FileAccess.get_file_as_string(path)

## 抓一次重演：触发 → 代为推进（含重演之后的对白）→ 手稿重新打开 → 合上，返回结果。
func run_replay(vars: Array, skip := true) -> Dictionary:
	host.dispatch({"kind": "replay", "vars": vars, "skip": skip})
	for round in 40:
		await fx.drain()
		if host.ui.modal_kind == "replay": break
		await process_frame
	var result: Dictionary = fx.replay_result()
	return result

func _run() -> void:
	fx = Fixture.new(self)
	fx.on_error = func(message: String) -> void: check(false, message)
	var content: Dictionary = fx.content
	# ---- 0. 对白标题齐全；全章文本不出现第 9、10 条
	# 读真实生成的对白文档与编入程序集的内容（探针 content）。
	await fresh()
	var story: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Awakening/Script/generated_awakening_dialogue.json"))
	check(story.has("entries"), Samples.text("flow-check", "text-022"))
	var titles: Array = story.entries.keys()
	var needed := Samples.array("flow-check", "case-001")
	for who in Samples.array("flow-check", "case-002"):
		for part in Samples.array("flow-check", "case-003"):
			needed.append(part + who)
	var missing := needed.filter(func(t): return not titles.has(t))
	check(missing.is_empty(), Samples.text("flow-check", "text-023") % str(missing))
	var runtime_content: String = str(fx.probe("content"))
	var raw: String = JSON.stringify(story.entries) + runtime_content
	var hits := FORBIDDEN.filter(func(w): return raw.contains(w))
	check(hits.is_empty(), Samples.text("flow-check", "text-024") % str(hits))
	var retired_raw := raw + source_text("res://scenes/Awakening/AwakeningMap.gd") + source_text("res://scenes/Awakening/Script/Chapter1Session.cs") \
		+ source_text("res://scenes/Awakening/Script/Chapter1SessionFlows.cs") + source_text("res://scenes/Host/Main.gd")
	var retired := RETIRED.filter(func(w): return retired_raw.contains(w))
	check(retired.is_empty(), Samples.text("flow-check", "text-025") % str(retired))
	check(not titles.has("answer_offer_reverse") and not titles.has("answer_reverse"), Samples.text("flow-check", "text-026"))

	# ---- 1. 醒来 → 相遇 → 自报身份广播 → 核验 → 始业之问 → 调查（完整开场，对白代为推进）
	await fresh(false)
	check(await fx.wait_until(func(): return fx.dialogue_active()), Samples.text("flow-check", "text-027"))
	check(fx.player().seated and room_name(fx.player().position) == "R-05", Samples.text("flow-check", "text-028"))
	await fx.drain()
	check(fx.stage() == "roam", Samples.text("flow-check", "text-029"))
	check(host.field.site_visible("E12") and host.field.site_visible("E11a") and not host.field.site_visible("E1"), Samples.text("flow-check", "text-030"))
	fx.player().stand_up()
	fx.player().position = Vector2(440, 340)
	check(await fx.wait_until(func(): return fx.stage() == "meeting"), Samples.text("flow-check", "text-031"))
	fx.seen_lines.clear()
	var meeting_deadline := Time.get_ticks_msec() + 120000
	while Time.get_ticks_msec() < meeting_deadline:
		await fx.drain([0])
		if host.ui.modal_kind == "card": fx.close_modal()
		if fx.stage() == "investigate" and not fx.dialogue_active() and host.ui.modal_kind == "" and host.field.follow_camera: break
		await process_frame
	check(fx.stage() == "investigate", Samples.text("flow-check", "text-032"))
	var opening := seen()
	check(opening.contains(Samples.text("flow-check", "text-033")) and not opening.contains(Samples.text("flow-check", "text-034"))
		and not opening.contains(Samples.text("flow-check", "text-035")) and not opening.contains(Samples.text("flow-check", "text-036")) and opening.contains(Samples.text("flow-check", "text-037")),
		Samples.text("flow-check", "text-038"))
	check(opening.find(Samples.text("flow-check", "text-039")) < opening.find(Samples.text("flow-check", "text-040")), Samples.text("flow-check", "text-041"))
	check(FORBIDDEN.filter(func(w): return opening.contains(w)).is_empty(), Samples.text("flow-check", "text-042"))
	check(opening.contains(fx.line_text("s5_question.016")), "authored opening dismisses companions into investigation")
	check(opening.contains(fx.line_text("s4_rewards.020")), "authored broadcast retains the detective thought")
	check(int(fx.probe("desire", ["huo"])) == 2 and int(fx.probe("desire", ["su"])) == 2 and int(fx.probe("desire", ["shen"])) == 2,
		Samples.text("flow-check", "text-043"))
	check(not fx.progress("flags").has("board_read"), "opening does not mark the board as read")
	check(fx.state("detective_name") == Samples.text("flow-check", "text-044"), Samples.text("flow-check", "text-045"))
	check(fx.map().can_access_dorm("musician"), Samples.text("flow-check", "text-046"))
	for dorm in [["R-06", Vector2(930, 600)], ["R-07", Vector2(706, 600)], [Samples.text("flow-check", "text-047"), Vector2(1150, 200)]]:
		var path: PackedVector2Array = route(Vector2(800, 388), dorm[1])
		check(not path.is_empty() and walkable(dorm[1]) and room_name(dorm[1]) == dorm[0], Samples.text("flow-check", "text-048") % dorm[0])
	# 乙区布局（第五轮修订 7）：中廊两侧各三间房，四人入住，靠外端两间空着；门厅在西端，隔离门开向起居厅。
	var names := {}
	for p in [Vector2(706, 200), Vector2(930, 200), Vector2(1150, 200), Vector2(706, 600), Vector2(930, 600), Vector2(1150, 600)]:
		names[room_name(p)] = names.get(room_name(p), 0) + 1
	check(names.get("R-05", 0) == 1 and names.get("R-06", 0) == 1 and names.get("R-07", 0) == 1 and names.get("R-08", 0) == 1 and names.get(Samples.text("flow-check", "text-047"), 0) == 2,
		Samples.text("flow-check", "text-049") % str(names))
	check(room_name(Vector2(224, 390)) == Samples.text("flow-check", "text-050") and room_name(Vector2(64, 390)) == Samples.text("flow-check", "text-051") and not walkable(Vector2(144, 384)),
		Samples.text("flow-check", "text-052"))

	# ---- 2. 公告框：学园守则四条（第五轮修订 2）
	await fx.open_card("board")
	check(fx.progress("flags").has("board_read"), "reading the board records acknowledgement")
	var board: Array = fx.probe("posted_rules")
	var board_text := "\n".join(board)
	check(board.size() == 4 and board_text.contains(Samples.text("flow-check", "text-053")) and board_text.contains(Samples.text("flow-check", "text-054"))
		and not board_text.contains(Samples.text("flow-check", "text-036")) and not board_text.contains(Samples.text("flow-check", "text-034")), Samples.text("flow-check", "text-055"))
	check(FORBIDDEN.filter(func(w): return board_text.contains(w)).is_empty(), Samples.text("flow-check", "text-056"))
	fx.close_modal()

	# ---- 3. 门厅反转
	await fresh()
	await fx.jump("investigate")
	await fx.open_card("E1")
	check(host.ui.modal_kind == "card" and fx.progress("examined").has("E1"), Samples.text("flow-check", "text-057"))
	fx.close_modal()
	await fx.drain()
	check(fx.replay_open(), Samples.text("flow-check", "text-058"))
	await fx.open_card("E3")
	fx.seen_lines.clear()
	fx.select("experiment")
	await fx.wait_until(func(): return host.ui.modal_kind == "menu")
	host.ui._select(host.ui._modal_event, "huo")
	await fx.settle_card()
	check(not fx.progress("flags").has("reversal_one") and fx.progress("soles_compared").has("huo") and seen().contains(Samples.text("flow-check", "text-059")),
		Samples.text("flow-check", "text-060"))
	fx.seen_lines.clear()
	fx.select("experiment")
	await fx.wait_until(func(): return host.ui.modal_kind == "menu")
	host.ui._select(host.ui._modal_event, "detective")
	await fx.settle_card()
	check(fx.progress("flags").has("reversal_one") and fx.progress("experiments").has("E3"), Samples.text("flow-check", "text-061"))
	check(seen().contains(fx.line_text("s6_reversal.004")), Samples.text("flow-check", "text-062"))
	check(fx.probe("statements").has("me"), Samples.text("flow-check", "text-063"))

	# ---- 4. 声路试验（含 R-08 听点）
	var sound_result: Array = fx.probe("sound_test", Samples.array("flow-check", "case-004"))
	fx.set_progress("state", "last_sound", sound_result)
	var sound_by_person := {}
	for result in sound_result: sound_by_person[str(result.who)] = result
	check(sound_by_person.size() == 4 and sound_by_person.has_all(Samples.array("flow-check", "case-005")), Samples.text("flow-check", "text-064"))
	var detective_sound: Dictionary = sound_by_person.detective
	var su_sound: Dictionary = sound_by_person.su
	var huo_sound: Dictionary = sound_by_person.huo
	check(detective_sound.room == "foyer" and str(detective_sound.text).begins_with(Samples.text("flow-check", "text-065")) and str(detective_sound.direction).contains(Samples.text("flow-check", "text-066"))
		and not str(detective_sound.text).contains(Samples.text("flow-check", "text-067")) and not str(detective_sound.direction).contains(Samples.text("flow-check", "text-067")),
		Samples.text("flow-check", "text-068"))
	check(su_sound.room == "R08" and str(su_sound.text).begins_with(Samples.text("flow-check", "text-065")) and str(su_sound.direction).contains(Samples.text("flow-check", "text-069"))
		and not str(su_sound.text).contains(Samples.text("flow-check", "text-067")) and not str(su_sound.direction).contains(Samples.text("flow-check", "text-067")), Samples.text("flow-check", "text-070"))
	check(huo_sound.room == "hall" and str(huo_sound.text).begins_with(Samples.text("flow-check", "text-071")) and str(huo_sound.direction).contains(Samples.text("flow-check", "text-072")), Samples.text("flow-check", "text-073"))
	fx.close_modal()
	fx.seen_lines.clear()
	# E8 先有两种相反的说法，才出现试验；听位只移动侦探，同伴按真实所在处听。
	await fx.open_card("E8")
	check(not fx.select("experiment"), Samples.text("flow-check", "text-074"))
	fx.close_modal()
	await fx.open_card("E7")
	fx.close_modal()
	await fx.open_card("E8")
	check(not fx.select("experiment"), Samples.text("flow-check", "text-075"))
	fx.close_modal()
	await fx.open_card("E6")
	fx.close_modal()
	await fx.stand_at(fx.map().anchor("site:E8") + Vector2(0, 30))
	await fx.open_card("E8")
	for round in 2:
		check(fx.select("experiment"), "声路试验第 %d 轮：已看 E7/E6，E8 有试验选项" % (round + 1))
		if not await fx.wait_until(func(): return host.ui.modal_kind == "form"):
			check(false, Samples.text("flow-check", "text-076"))
			break
		var spot: String = Samples.array("flow-check", "case-006")[round]
		await fx.physics(4) # 等表单打开前的日常停步回执落定，再比较听位选择的影响。
		var trace_path: String = ProjectSettings.globalize_path("res://../artifacts/basic-ui-system/player-traces").path_join(str(host.ui._modal_event.run_id) + ".jsonl")
		var trace := FileAccess.open(trace_path, FileAccess.READ)
		var trace_offset := trace.get_length()
		trace = null
		var positions := {}
		for who in Samples.array("flow-check", "case-007"): positions[who] = fx.actor(who).position
		host.ui._form.select("sentence", "0")
		host.ui._form.select("spot", spot)
		fx.seen_lines.clear()
		for action in host.ui._modal_event.actions:
			if str(action.id) == "start": host.ui._form_action(host.ui._modal_event, action)
		await fx.drain()
		check(await fx.wait_until(func(): return host.ui.modal_kind == "card"), Samples.text("flow-check", "text-077"))
		var actual: Array = fx.state("last_sound", [])
		var context: Dictionary = host.field.context().actors
		var by_person := {}
		for heard in actual: by_person[str(heard.who)] = heard
		for who in positions:
			print("SOUND_POSITION round=%d who=%s before=%s after=%s delta=%.2f" % [round + 1, who, positions[who], fx.actor(who).position, fx.actor(who).position.distance_to(positions[who])])
		print("SOUND round=%d chosen=%s actual=%s" % [round + 1, spot, str(actual)])
		check(context.detective.entered == spot and by_person.get("detective", {}).get("room", "") == spot,
			Samples.text("flow-check", "text-078") + spot)
		# 现场让路会小幅挪动同伴；试验不得调度他们去预设听位。
		trace = FileAccess.open(trace_path, FileAccess.READ)
		trace.seek(trace_offset)
		var companion_moves := []
		var last_context := {}
		var hearing_context := {}
		while trace.get_position() < trace.get_length():
			var row: Dictionary = JSON.parse_string(trace.get_line())
			var output: Dictionary = row.get("data", {})
			if str(row.kind) == "input" and output.has("context"):
				last_context = output.context.get("actors", {})
			if str(row.kind) == "output" and str(output.get("kind", "")) == "sound" and str(output.get("id", "")) == "tuning_fork":
				hearing_context = last_context
			if str(row.kind) == "output" and str(output.get("kind", "")) == "stage" and str(output.get("op", "")) in Samples.array("flow-check", "case-008") 					and str(output.get("who", "")) in Samples.array("flow-check", "case-009"):
				companion_moves.append(output)
		trace = null
		print("SOUND_COMPANION_STAGE round=%d moves=%s" % [round + 1, str(companion_moves)])
		# 比较发声时的正式输入上下文；对白演出之后，日常或让路可能已改变站位。
		check(companion_moves.is_empty() and Samples.array("flow-check", "case-010").all(func(who):
			var at_sound: Dictionary = hearing_context.get(who, {})
			var room: String = str(at_sound.get("entered", ""))
			if room == "": room = str(at_sound.get("room", ""))
			return room != "" and by_person.get(who, {}).get("room", "") == room),
			Samples.text("flow-check", "text-079"))
		check(fx.seen_lines.size() > 4, Samples.text("flow-check", "text-080"))
	fx.close_modal()
	# E5 协助可代替 E6 检视这一前置条件。
	await fresh()
	await fx.jump("investigate")
	await fx.open_card("E7")
	fx.close_modal()
	await fx.open_card("board")
	fx.close_modal()
	await fx.open_card("E5")
	check(await fx.invite_help("E5", "su"), "E5 assistance starts from the person menu")
	fx.close_modal()
	await fx.open_card("E8")
	check(host.ui._modal_event.options.any(func(o): return str(o.id).ends_with(":experiment")), Samples.text("flow-check", "text-081"))
	fx.close_modal()

	# ---- 5. 卡片不再提议回忆；协助碰到本人的旧物之后自动闪回。
	var cases := Samples.array("flow-check", "case-011")
	for case in cases:
		await fresh()
		await fx.jump("investigate")
		fx.probe("amplify", [case[1], "S4"])
		await fx.open_card("board")
		fx.close_modal()
		if case[2]: fx.set_progress("flags", "price_heard", true)
		fx.seen_lines.clear()
		await fx.open_card(case[0])
		check(not fx.select("recall") and fx.seen_lines.is_empty(), Samples.text("flow-check", "text-082") % case[0])
		var choices := [0] if case[2] else []
		check(not fx.select("help"), "evidence card has no assistance option")
		check(await fx.invite_help(case[0], case[1], choices), "person invitation completes assistance: " + case[0])
		var touched: bool = fx.progress("recalls").has(case[1])
		var informed: bool = case[2] or case[1] == "shen"
		var expected: bool = case[1] == "huo" or (case[1] == "su" and not informed)
		print("FLASH id=%s person=%s informed=%s recall=%s hooks=%d thought=%s" % [case[0], case[1], informed, touched, fx.hooks(case[1]), str(fx.progress("recall_decision"))])
		check(fx.progress("helped").has(case[0]) and fx.progress("decided").has(case[1]), Samples.text("flow-check", "text-083") + case[0])
		check(touched == expected and fx.hooks(case[1]) == (1 if expected else 0), Samples.text("flow-check", "text-084") % [case[1], informed, expected])
		if case[1] == "shen": check(fx.progress("flags").has("price_heard"), Samples.text("flow-check", "text-085"))
		if touched and case[1] == "huo":
			check(str(fx.progress("recall_marks").get("2", "")) == "huo" and int(fx.state("replay_vars")[1]) == -1, Samples.text("flow-check", "text-086"))
		fx.close_modal()
	# E1 只在拔闩试验中触发身体闪回；选择是否往下想仍在对白里。
	for accept in [false, true]:
		await fresh()
		await fx.jump("investigate")
		await fx.open_card("E1")
		check(not fx.select("recall"), Samples.text("flow-check", "text-087"))
		fx.seen_lines.clear()
		check(await fx.card_action("experiment", [0 if accept else 1]), Samples.text("flow-check", "text-088"))
		check(fx.progress("experiments").has("E1") and fx.progress("recalls").has("detective") == accept 			and fx.hooks("detective") == (1 if accept else 0), Samples.text("flow-check", "text-089") % accept)
		fx.close_modal()
	check(not titles.any(func(t): return str(t).begins_with("nudge_")), Samples.text("flow-check", "text-090"))
	await fresh()
	await fx.jump("investigate")
	for id in Samples.array("flow-check", "case-012"):
		await fx.open_card(id)
		fx.close_modal()
		await fx.drain()
	await fx.stand_at(Vector2(930, 200))
	await fx.physics(40)
	print("ROOM_CHANGE life=%s nudged=%s recalls=%s" % [str(fx.progress("life")), str(fx.progress("nudged")), str(fx.progress("recalls"))])
	check(fx.progress("life").is_empty() and fx.progress("recalls").is_empty() and Samples.array("flow-check", "case-013").all(func(who): return not bool(fx.probe("whispered", [who]))), Samples.text("flow-check", "text-091"))

	# ---- 5c. 答辩陈述从检视认知开放，不要求先重演。
	await fresh()
	await fx.jump("investigate")
	check(fx.probe("statements").is_empty(), Samples.text("flow-check", "text-092"))
	await fx.open_card("E8")
	fx.close_modal()
	var statements: Array = fx.probe("statements")
	check(statements.has("V3_0") and statements.has("V3_1") and not statements.has("V2_0"), Samples.text("flow-check", "text-093"))
	await fx.open_card("E7")
	fx.close_modal()
	await fx.open_card("E12")
	fx.close_modal()
	statements = fx.probe("statements")
	check(statements.has("me") and statements.has("V2_0") and statements.has("V4_0") and int(fx.state("replay_count")) == 0, "检视 E7/E12 后可陈述闩门与指令，重演次数仍为零")
	await fx.open_card("E9")
	fx.close_modal()
	await fx.drain()
	statements = fx.probe("statements")
	check(statements.has("V6_0") and statements.has("V6_1") and statements.has("trust"), Samples.text("flow-check", "text-094"))

	# ---- 5c2. 手印先比自己的手，之后才列在场同伴。
	await fresh()
	await fx.jump("investigate")
	var shelf: Vector2 = host.field.site_point("E11a")
	await fx.stand_near(Vector2(930, 200), shelf, 64.0)
	host.field.execute({"kind": "stage", "op": "place", "who": "huo", "to": {"point": fx.player().position + Vector2(24, 0)}})
	await fx.physics(4)
	await fx.open_card("E11")
	check(host.ui._modal_event.options.all(func(o): return not str(o.id).ends_with(":huo")), Samples.text("flow-check", "text-095"))
	check(await fx.card_action("compare:detective"), Samples.text("flow-check", "text-096"))
	check(fx.progress("hand_comparisons").has("detective"), Samples.text("flow-check", "text-097"))
	check(host.ui._modal_event.options.any(func(o): return str(o.id).ends_with(":huo")), Samples.text("flow-check", "text-098"))
	fx.close_modal()

	# ---- 5d. 第六个问题起初不写出来（修订四·2）
	await fresh()
	await fx.jump("investigate")
	fx.set_progress("state", "replay_open", true)
	await fx.physics(4)
	host.ui.open_manuscript()
	await process_frame
	var page := fx.modal_text()
	check(not page.contains(Samples.text("flow-check", "text-099")) and not fx.probe("statements").has("V6_0"), Samples.text("flow-check", "text-100"))
	fx.close_modal()
	fx.seen_lines.clear()
	fx.probe("examine", ["E9"])
	await fx.drain()
	check(not fx.probe("v6_open"), Samples.text("flow-check", "text-101"))
	fx.probe("examine", ["E12"])
	await fx.drain()
	host.ui.open_manuscript()
	await process_frame
	page = fx.modal_text()
	check(fx.probe("v6_open") and page.contains(Samples.text("flow-check", "text-102")) and seen().contains(fx.line_text("v6_doubt.002")),
		Samples.text("flow-check", "text-103"))
	fx.close_modal()
	await fresh()
	await fx.jump("reversal_one")
	check(not fx.probe("v6_open"), Samples.text("flow-check", "text-104"))
	fx.seen_lines.clear()
	var refused_path: Dictionary = await run_replay([0, 0, 0, 0, 0, 0])
	var doubt_text: String = fx.text(Samples.text("flow-check", "text-105"))
	var refusal_thoughts: Array = refused_path.frames.map(func(frame): return str(frame.thoughts.detective)).filter(func(thought): return str(thought).contains(doubt_text))
	print("V6_REPLAY doubt=%s matched_thoughts=%s open=%s flags=%s seen=%s" % [doubt_text, str(refusal_thoughts), fx.probe("v6_open"), str(fx.progress("flags")), seen()])
	check(fx.probe("v6_open") and seen().contains(fx.line_text("replay_refused.003")), Samples.text("flow-check", "text-106"))
	fx.close_modal()

	# ---- 5e. 交互一致性与减少重复（修订四·7、8、9）
	await fresh()
	await fx.jump("investigate")
	check(fx.player().collision_mask == 1 and fx.actor("su").collision_mask == 1, Samples.text("flow-check", "text-107"))
	fx.player().position = host.field.site_point("E4") + Vector2(20, -20)
	await fx.physics(3)
	host.field._approach("E4")
	await fx.frames(3)
	check(host.ui.modal_kind == "card", Samples.text("flow-check", "text-108"))
	fx.seen_lines.clear()
	var helped: bool = await fx.invite_help("E4", "shen")
	var brief := fx.modal_text()
	check(helped and brief.contains(Samples.text("flow-check", "text-109")) and not brief.contains(Samples.text("flow-check", "text-110")), Samples.text("flow-check", "text-111"))
	fx.close_modal()
	await fx.physics(2)
	await fx.stand_near(Vector2(930, 600), host.field.site_point("L6"), 64.0)
	var l6_visible: bool = await fx.wait_until(func(): return host.field.site_visible("L6"), 2.0)
	await fx.stand_near(Vector2(706, 600), host.field.site_point("L7"), 64.0)
	var l7_visible: bool = await fx.wait_until(func(): return host.field.site_visible("L7"), 2.0)
	print("LIFE_TRACES L6=%s L7=%s player=%s" % [l6_visible, l7_visible, fx.player().position])
	check(l6_visible and l7_visible, Samples.text("flow-check", "text-112"))
	# 物件点可能在遮挡或碰撞内；从可见站位沿正式靠近通路发出交互。
	await fx.stand_near(Vector2(930, 600), host.field.site_point("L6"), 64.0)
	host.field._approach("L6")
	await fx.settle_card()
	var hook := fx.modal_text()
	check(hook.contains(Samples.text("flow-check", "text-113")) and hook.contains(Samples.text("flow-check", "text-114")), Samples.text("flow-check", "text-115"))
	fx.close_modal()
	await fx.physics(2)
	await fx.stand_near(Vector2(706, 600), host.field.site_point("L7"), 64.0)
	host.field._approach("L7")
	await fx.settle_card()
	var stone := fx.modal_text()
	check(stone.contains(Samples.text("flow-check", "text-116")) and stone.contains(Samples.text("flow-check", "text-117")), Samples.text("flow-check", "text-118"))
	fx.close_modal()
	await fx.physics(2)
	# 点到窗：现场认出被点中的家具，把点击报给章节，章节回一句心声。
	var window: Array = fx.map().layout.asides.filter(func(decor): return str(decor[1]).contains(Samples.text("flow-check", "text-119")))[0]
	var rect: Array = window[0]
	var hits_at: Dictionary = fx.map().furniture_hits(Vector2(rect[0] + rect[2] / 2.0, rect[1] + rect[3] / 2.0))
	host.dispatch({"kind": "furniture", "map": fx.map().map_key(), "nodes": hits_at.get("nodes", []), "texts": hits_at.get("texts", {})})
	await fx.frames(3)
	check(fx.aside().visible and fx.aside().text == str(window[1]), Samples.text("flow-check", "text-120"))

	# ---- 6. 重演：素不相识时的拒绝，与全部吻合
	fx.set_progress("state", "replay_open", true) # 新宿主：重演须先开放；原宿主的 run_replay 不设此门。
	await fx.physics(4)
	var refused: Dictionary = await run_replay([0, 0, 0, 0, 0, 0])
	fx.close_modal()
	var doubt := false
	for frame in refused.frames:
		if str(frame.thoughts.detective).contains(Samples.text("flow-check", "text-121")):
			doubt = true
	check(doubt and not bool(refused.full_match), Samples.text("flow-check", "text-122"))
	var unvisited: Array = refused.findings.filter(func(f): return f.status == "unvisited")
	check(not unvisited.is_empty() and str(unvisited[0].text).begins_with(Samples.text("flow-check", "text-123")) and str(unvisited[0].text).contains(Samples.text("flow-check", "text-124")), Samples.text("flow-check", "text-125"))
	var regions := []
	for f in unvisited:
		regions.append(str(f.text))
	check("".join(regions).contains(Samples.text("flow-check", "text-126")), Samples.text("flow-check", "text-127"))
	await fresh()
	await fx.jump("replay_match")
	var matched: Dictionary = fx.replay_result()
	check(bool(matched.full_match), Samples.text("flow-check", "text-128"))
	check(matched.findings.all(func(f): return f.status == "match"), Samples.text("flow-check", "text-129"))
	var moment: Dictionary = {}
	var basis := false
	for frame in matched.frames:
		if str(frame.get("event", {}).get("kind", "")) == "voice_tube":
			moment = frame
		if str(frame.thoughts.detective).contains(Samples.text("flow-check", "text-131")) and str(frame.thoughts.detective).contains(Samples.text("flow-check", "text-132")):
			basis = true
	var distinct := {}
	for key in moment.thoughts:
		distinct[str(moment.thoughts[key])] = true
	check(distinct.size() == 4, Samples.text("flow-check", "text-133"))
	check(basis, Samples.text("flow-check", "text-134"))
	var digits := false
	for frame in matched.frames:
		for key in frame.thoughts:
			if RegEx.create_from_string("[0-9０-９]").search(str(frame.thoughts[key])) != null:
				digits = true
	check(not digits, Samples.text("flow-check", "text-135"))
	fx.set_progress("state", "full_match_seen", false)
	# 在地图上播放回放，播放中按 Esc 跳过（与看完相同结算）。
	host.dispatch({"kind": "replay", "vars": [0, 0, 0, 0, 0, 1], "skip": false})
	var stage_node: Node = fx.map().stage_node
	await fx.wait_until(func(): return stage_node.playing, 10.0)
	await process_frame
	await process_frame
	var skip_key := InputEventKey.new()
	skip_key.keycode = KEY_ESCAPE
	skip_key.physical_keycode = KEY_ESCAPE
	skip_key.pressed = true
	stage_node._unhandled_input(skip_key)
	for round in 80:
		# 全部吻合后三位同伴先走过来，再说第二次反转。
		await fx.drain()
		if host.ui.modal_kind == "replay":
			break
		await process_frame
	check(await fx.wait_until(func(): return host.ui.modal_kind == "replay"), Samples.text("flow-check", "text-136"))
	check(bool(fx.state("full_match_seen")), Samples.text("flow-check", "text-137"))
	fx.close_modal()

	# ---- 7. 始业之答（陈述与出示成对；第五轮修订删去反问）→ 门开
	await fresh()
	await fx.jump("replay_match")
	check(await fx.compare_hands(), Samples.text("flow-check", "text-138"))
	fx.probe("apply_recall", Samples.array("flow-check", "case-014"))
	fx.seen_lines.clear()
	var end_started := Time.get_unix_time_from_system()
	host.dispatch({"kind": "interact", "site": "E8"})
	await fx.wait_until(func(): return host.ui.modal_kind == "menu", 5.0)
	fx.select("answer")
	var picks := Samples.array("flow-check", "case-015")
	var saw_reverse := false
	var saw_wrong_show := false
	var saw_wrong_say := false
	var answer_deadline := Time.get_ticks_msec() + 180000
	while Time.get_ticks_msec() < answer_deadline:
		await process_frame
		if fx.ended() and host.ui.modal_kind == "ending":
			break
		var d = host.ui.dialogue
		if d.active and d.line != null and not d.busy:
			for r in d.line.responses:
				if str(r.text).begins_with(Samples.text("flow-check", "text-139")):
					saw_reverse = true
			if str(d.line.text).contains(Samples.text("flow-check", "text-140")):
				saw_wrong_show = true
			if str(d.line.text).contains(Samples.text("flow-check", "text-141")):
				saw_wrong_say = true
			if fx.seen_lines.is_empty() or fx.seen_lines[-1] != str(d.line.text):
				fx.seen_lines.append(str(d.line.text))
		if host.ui.modal_kind == "form" and str(host.ui._modal_event.get("layout", "")) == "tabs" and not picks.is_empty():
			var pick: Dictionary = picks.pop_front()
			host.ui._form.select("say", pick.say)
			host.ui._form.select("show", pick.show)
			for action in host.ui._modal_event.actions:
				if str(action.id) == "submit":
					host.ui._form_action(host.ui._modal_event, action)
					break
			continue
		if d.active:
			if d.busy or d.line == null:
				continue
			if d.body.visible_characters >= 0:
				d.reveal()
			elif d.line.responses.is_empty():
				d.advance()
			else:
				d.choose(str(d.line.responses[0].next_id))
	await fx.frames(5)
	check(saw_wrong_show, Samples.text("flow-check", "text-142"))
	check(saw_wrong_say, Samples.text("flow-check", "text-143"))
	check(not saw_reverse and not fx.progress("flags").has("asked_reverse"), Samples.text("flow-check", "text-144"))
	check(fx.progress("flags").has("door_open") and fx.ended(), Samples.text("flow-check", "text-145"))
	check(fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, Samples.text("flow-check", "text-146"))
	var finale := seen()
	var early: bool = fx.progress("flags").has("quer_opened")
	check(not fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/LoungeCover").visible, Samples.text("flow-check", "text-147"))
	if early:
		check(not fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/DemonPrints").visible
			and finale.contains(fx.line_text("s10_open_early.001")) and finale.contains(fx.line_text("s10_open_early.004")),
			Samples.text("flow-check", "text-148"))
	else:
		check(fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/DemonPrints").visible
			and finale.contains(fx.line_text("s10_prints.003")) and finale.contains(fx.line_text("s10_prints.001")), Samples.text("flow-check", "text-149"))
	check(room_name(fx.player().position) == Samples.text("flow-check", "text-050"), Samples.text("flow-check", "text-150"))
	if not early:
		check(fx.progress("flags").has("asked_hand") and finale.contains(fx.line_text("s10_hand.006")), Samples.text("flow-check", "text-151"))
		check(finale.contains(fx.line_text("s10_open.003")), Samples.text("flow-check", "text-152"))
		check(finale.contains(fx.line_text("s10_open.007")) and finale.contains(fx.line_text("s10_open.006")), Samples.text("flow-check", "text-153"))
	var ending_text := fx.modal_text() + "\n" + "\n".join(host.ui._modal_event.get("rows", []))
	check(ending_text.contains(Samples.text("flow-check", "text-154")) and ending_text.contains(Samples.text("flow-check", "text-155")), Samples.text("flow-check", "text-156"))
	check(FileAccess.file_exists("user://chapter1_end.json") and FileAccess.get_modified_time("user://chapter1_end.json") >= int(end_started) - 1,
		"章节结束状态写入 user://chapter1_end.json（且是本次写入）")

	# ---- 7b. 对话刚结束时多按的空格不触发身边人物（修订三·1）
	await fresh()
	await fx.jump("investigate")
	fx.player().position = fx.actor("huo").position + Vector2(24, 0)
	await fx.physics(4)
	fx.probe("say", ["exp_E2"])
	# 一行行推进，对白一结束就在下一帧按空格（模拟玩家多按的一下）。
	await fx.wait_until(func(): return fx.dialogue_active(), 10.0)
	var advance_deadline := Time.get_ticks_msec() + 60000
	while Time.get_ticks_msec() < advance_deadline:
		await process_frame
		var d = host.ui.dialogue
		if not d.active:
			break
		if d.busy or d.line == null:
			continue
		if d.body.visible_characters >= 0:
			d.reveal()
		else:
			d.advance()
	# 原宿主在同一帧里就重新判定；新现场在下一帧 _process 记下“恢复可操作”的时刻，再按空格。
	await process_frame
	host.field._pick_nearest()
	var key := InputEventKey.new()
	key.keycode = KEY_SPACE
	key.physical_keycode = KEY_SPACE
	key.pressed = true
	host.field._unhandled_input(key)
	await process_frame
	check(not fx.dialogue_active(), Samples.text("flow-check", "text-157"))
	await create_timer(0.7).timeout
	host.field._pick_nearest()
	host.field._unhandled_input(key)
	await process_frame
	check(fx.dialogue_active(), Samples.text("flow-check", "text-158"))
	await fx.drain()

	# ---- 8. 铜口菜单不再提供歇息选择。
	await fresh()
	await fx.jump("investigate")
	host.dispatch({"kind": "interact", "site": "E8"})
	check(await fx.wait_until(func(): return host.ui.modal_kind == "menu", 5.0), Samples.text("flow-check", "text-159"))
	check(not fx.select("rest") and not fx.ended(), Samples.text("flow-check", "text-160"))
	fx.close_modal()

	print("TOTAL %d checks, %d failed" % [checks, failures])
	for name in failed_names: print("FAILED: " + name)
	call_deferred("_finish", 0 if failures == 0 else 1)
