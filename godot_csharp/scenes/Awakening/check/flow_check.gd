extends SceneTree
## 流程跳转回归（统一宿主 Main.tscn）：直接把局面摆到关键状态（醒来、开场广播、学园守则、门厅反转、协助后自动闪回、
## 重演全部吻合、始业之答、门开、认知前置条件），检查可达与无脚本错误。地图为第五轮修订后的乙区（中廊两侧各三间房）。
## 对白与表单由脚本代为推进；这不是游玩验证，不替代独立盲玩。失败不中止，末尾汇总全部失败项。
##   godot --headless --path godot_csharp --script res://scenes/Awakening/check/flow_check.gd

const Fixture := preload("res://scenes/Awakening/check/chapter1_fixture.gd")
const FieldNavigation := preload("res://scenes/Shared/FieldNavigation.gd")
## 恶魔在全章都不提契约第 9、10 条。
const FORBIDDEN := ["第九", "第 9", "第9", "第十条", "第 10", "九、", "十、", "尽忘", "归乡", "放逐", "十五名", "担保耗尽"]
## 第五轮修订删去的说法：可数的刻痕、恶魔自报为恶魔或提导师与契约、门外是“主楼走廊”。
const RETIRED := ["二十四", "刻痕", "恶魔亦可", "导师将我拘于", "契约第", "主楼走廊", "依第二条", "第 8 条", "第八条"]
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
	check(story.has("entries"), "真实生成对白文档存在")
	var titles: Array = story.entries.keys()
	var needed := ["s1_wake", "s2_meet", "s2_tea", "s3_broadcast", "s4_rewards", "s5_question", "s6_begin", "replay_unlock", "exp_E1", "exp_E2", "exp_E3",
		"s6_reversal", "help_E4", "help_E5", "help_E6", "help_E7", "help_E9", "help_E10", "help_E11",
		"recall_ask_detective", "recall_detective", "recall_su", "recall_huo", "recall_shen",
		"sound_test", "talk_huo", "talk_shen", "talk_su", "s8_reversal", "answer_open", "answer_q1", "answer_ok1", "answer_q2",
		"answer_ok2", "answer_need_test", "answer_q3", "answer_ok3", "answer_q4", "answer_ok4",
		"answer_wrong_show", "answer_wrong_say", "answer_pause", "s10_steps", "s10_prints", "s10_hand", "s10_open",
		"replay_first", "replay_refused"]
	for who in ["su", "huo", "shen"]:
		for part in ["recall_offer_", "recall_shown_", "recall_touch_", "recall_refuse_", "recall_hesitate_"]:
			needed.append(part + who)
	var missing := needed.filter(func(t): return not titles.has(t))
	check(missing.is_empty(), "代码引用的对白标题全部存在 %s" % str(missing))
	var runtime_content: String = str(fx.probe("content"))
	var raw: String = JSON.stringify(story.entries) + runtime_content
	var hits := FORBIDDEN.filter(func(w): return raw.contains(w))
	check(hits.is_empty(), "台词与证物文本中没有契约第 9、10 条的字样 %s" % str(hits))
	var retired_raw := raw + source_text("res://scenes/Awakening/AwakeningMap.gd") + source_text("res://scenes/Awakening/Script/Chapter1Session.cs") \
		+ source_text("res://scenes/Awakening/Script/Chapter1SessionFlows.cs") + source_text("res://scenes/Host/Main.gd")
	var retired := RETIRED.filter(func(w): return retired_raw.contains(w))
	check(retired.is_empty(), "第五轮修订：不再有可数刻痕、契约条款与“主楼走廊” %s" % str(retired))
	check(not titles.has("answer_offer_reverse") and not titles.has("answer_reverse"), "第五轮修订：删去答辩第 5 步“反问”")

	# ---- 1. 醒来 → 相遇 → 自报身份广播 → 核验 → 始业之问 → 调查（完整开场，对白代为推进）
	await fresh(false)
	check(await fx.wait_until(func(): return fx.dialogue_active()), "开场：S1 醒来对白出现")
	check(fx.player().seated and room_name(fx.player().position) == "R-05", "醒来：侦探在 R-05 床上")
	await fx.drain()
	check(fx.stage() == "roam", "醒来后可以自由行动")
	check(host.field.site_visible("E12") and host.field.site_visible("E11a") and not host.field.site_visible("E1"), "醒来时只看得到 R-05 里的东西")
	fx.player().stand_up()
	fx.player().position = Vector2(440, 340)
	check(await fx.wait_until(func(): return fx.stage() == "meeting"), "走进活动厅触发 S2 相遇")
	fx.seen_lines.clear()
	var meeting_deadline := Time.get_ticks_msec() + 120000
	while Time.get_ticks_msec() < meeting_deadline:
		await fx.drain([0])
		if host.ui.modal_kind == "card": fx.close_modal()
		if fx.stage() == "investigate" and not fx.dialogue_active() and host.ui.modal_kind == "" and host.field.follow_camera: break
		await process_frame
	check(fx.stage() == "investigate", "S2–S5 结束后进入调查")
	var opening := seen()
	check(opening.contains("我是此地的代管者，也是诸位的教师") and opening.contains("我所立之规") and not opening.contains("恶魔")
		and not opening.contains("导师") and not opening.contains("契约") and opening.contains("诸位初涉斯地"),
		"S3 开场广播：只称代管者与教师，不说自己是什么，不提导师与契约；“诸位初涉斯地”已恢复（2026-10-08 用户裁定）")
	check(opening.find("代管者") < opening.find("此为始业之问"), "开场广播在始业之问之前")
	check(FORBIDDEN.filter(func(w): return opening.contains(w)).is_empty(), "开场全部台词不提第 9、10 条")
	check(opening.contains("我连昨晚睡的哪张床都不记得") and opening.contains("是在试我们") and opening.contains("我还不知道能不能信"),
		"始业之问后，三人各有一句不同的抵触或怀疑，再开始调查（修订四·11）")
	check(opening.contains("揣进风衣口袋"), "报偿空格处，侦探有一句克制的心声（修订四·10）")
	check(int(fx.probe("desire", ["huo"])) == 2 and int(fx.probe("desire", ["su"])) == 2 and int(fx.probe("desire", ["shen"])) == 2,
		"S4 恶魔逐一点到三人的欲望（Corruption 来源，欲望各加强一级）")
	check(fx.progress("flags").has("board_read"), "S3 广播结束后，公告框上的学园守则完整呈现过一次（修订三·2、第五轮修订 2）")
	check(fx.state("detective_name") == "试验", "姓名由玩家输入")
	check(fx.map().can_access_dorm("musician"), "调查阶段可进入其他宿舍")
	for dorm in [["R-06", Vector2(930, 600)], ["R-07", Vector2(706, 600)], ["空房", Vector2(1150, 200)]]:
		var path: PackedVector2Array = route(Vector2(800, 388), dorm[1])
		check(not path.is_empty() and walkable(dorm[1]) and room_name(dorm[1]) == dorm[0], "%s 可以走进去（修订三·1）" % dorm[0])
	# 乙区布局（第五轮修订 7）：中廊两侧各三间房，四人入住，靠外端两间空着；门厅在西端，隔离门开向起居厅。
	var names := {}
	for p in [Vector2(706, 200), Vector2(930, 200), Vector2(1150, 200), Vector2(706, 600), Vector2(930, 600), Vector2(1150, 600)]:
		names[room_name(p)] = names.get(room_name(p), 0) + 1
	check(names.get("R-05", 0) == 1 and names.get("R-06", 0) == 1 and names.get("R-07", 0) == 1 and names.get("R-08", 0) == 1 and names.get("空房", 0) == 2,
		"中廊两侧各三间房：R-05—08 四间住人，外端两间空着 %s" % str(names))
	check(room_name(Vector2(224, 390)) == "门厅" and room_name(Vector2(64, 390)) == "起居厅" and not walkable(Vector2(144, 384)),
		"门厅在乙区西端，隔离门外是起居厅，门闩着走不过去")

	# ---- 2. 公告框：学园守则四条（第五轮修订 2）
	await fx.open_card("board")
	var board: Array = fx.probe("posted_rules")
	var board_text := "\n".join(board)
	check(board.size() == 4 and board_text.contains("一、入夜之后，诸位留于室内。") and board_text.contains("四、我所立之规，皆张贴于此。")
		and not board_text.contains("契约") and not board_text.contains("恶魔"), "活动厅公告框贴的是学园守则四条，不是契约")
	check(FORBIDDEN.filter(func(w): return board_text.contains(w)).is_empty(), "公告框没有第 9、10 条")
	fx.close_modal()

	# ---- 3. 门厅反转
	await fresh()
	await fx.jump("investigate")
	await fx.open_card("E1")
	check(host.ui.modal_kind == "card" and fx.progress("examined").has("E1"), "检视隔离门：打开证物卡")
	fx.close_modal()
	await fx.drain()
	check(fx.replay_open(), "第一次看到闩上的门后，重演开放")
	await fx.open_card("E3")
	fx.seen_lines.clear()
	fx.select("experiment")
	await fx.wait_until(func(): return host.ui.modal_kind == "menu")
	host.ui._select(host.ui._modal_event, "huo")
	await fx.settle_card()
	check(not fx.progress("flags").has("reversal_one") and fx.progress("soles_compared").has("huo") and seen().contains("粗齿纹"),
		"比对鞋底由玩家选择对象：比霍铎的鞋，对不上，不触发反转（修订四·4）")
	fx.seen_lines.clear()
	fx.select("experiment")
	await fx.wait_until(func(): return host.ui.modal_kind == "menu")
	host.ui._select(host.ui._modal_event, "detective")
	await fx.settle_card()
	check(fx.progress("flags").has("reversal_one") and fx.progress("experiments").has("E3"), "门厅反转：对照靴底后触发“……是我”")
	check(seen().contains(fx.line_text("s6_reversal.004")), "“……是我”处，侦探有一句克制的心声（修订四·10）")
	check(fx.probe("statements").has("me"), "陈述列表出现“是我闩的门”")

	# ---- 4. 声路试验（含 R-08 听点）
	var sound_result: Array = fx.probe("sound_test", [0, {"detective": "foyer", "huo": "hall", "shen": "pantry", "su": "R08"}])
	fx.set_progress("state", "last_sound", sound_result)
	var sound_by_person := {}
	for result in sound_result: sound_by_person[str(result.who)] = result
	check(sound_by_person.size() == 4 and sound_by_person.has_all(["detective", "huo", "shen", "su"]), "声路试验返回四人的独立听闻")
	var detective_sound: Dictionary = sound_by_person.detective
	var su_sound: Dictionary = sound_by_person.su
	var huo_sound: Dictionary = sound_by_person.huo
	check(detective_sound.room == "foyer" and str(detective_sound.text).begins_with("……开门") and str(detective_sound.direction).contains("门那边")
		and not str(detective_sound.text).contains("门外") and not str(detective_sound.direction).contains("门外"),
		"声路试验：门边只听到“……开门”，同伴只说来处的方向（门那边），不说“像在门外”（修订三·5）")
	check(su_sound.room == "R08" and str(su_sound.text).begins_with("……开门") and str(su_sound.direction).contains("墙里")
		and not str(su_sound.text).contains("门外") and not str(su_sound.direction).contains("门外"), "声路试验：R-08 只听到“……开门”，从墙里传来")
	check(huo_sound.room == "hall" and str(huo_sound.text).begins_with("闩上门，不要开门。") and str(huo_sound.direction).contains("从铜口里"), "声路试验：铜口旁听到整句")
	fx.close_modal()
	fx.seen_lines.clear()
	# E8 先有两种相反的说法，才出现试验；听位只移动侦探，同伴按真实所在处听。
	await fx.open_card("E8")
	check(not fx.select("experiment"), "没看石板与另一种说法时，E8 不提供声路试验")
	fx.close_modal()
	await fx.open_card("E7")
	fx.close_modal()
	await fx.open_card("E8")
	check(not fx.select("experiment"), "只看石板时，E8 仍不提供声路试验")
	fx.close_modal()
	await fx.open_card("E6")
	fx.close_modal()
	await fx.stand_at(fx.map().anchor("site:E8") + Vector2(0, 30))
	await fx.open_card("E8")
	for round in 2:
		check(fx.select("experiment"), "声路试验第 %d 轮：已看 E7/E6，E8 有试验选项" % (round + 1))
		if not await fx.wait_until(func(): return host.ui.modal_kind == "form"):
			check(false, "声路试验表单出现")
			break
		var spot: String = ["foyer", "R08"][round]
		await fx.physics(4) # 等表单打开前的日常停步回执落定，再比较听位选择的影响。
		var trace_path: String = ProjectSettings.globalize_path("res://../artifacts/basic-ui-system/player-traces").path_join(str(host.ui._modal_event.run_id) + ".jsonl")
		var trace := FileAccess.open(trace_path, FileAccess.READ)
		var trace_offset := trace.get_length()
		trace = null
		var positions := {}
		for who in ["huo", "shen", "su"]: positions[who] = fx.actor(who).position
		host.ui._form.select("sentence", "0")
		host.ui._form.select("spot", spot)
		fx.seen_lines.clear()
		for action in host.ui._modal_event.actions:
			if str(action.id) == "start": host.ui._form_action(host.ui._modal_event, action)
		await fx.drain()
		check(await fx.wait_until(func(): return host.ui.modal_kind == "card"), "声路试验完成后返回证物卡")
		var actual: Array = fx.state("last_sound", [])
		var context: Dictionary = host.field.context().actors
		var by_person := {}
		for heard in actual: by_person[str(heard.who)] = heard
		for who in positions:
			print("SOUND_POSITION round=%d who=%s before=%s after=%s delta=%.2f" % [round + 1, who, positions[who], fx.actor(who).position, fx.actor(who).position.distance_to(positions[who])])
		print("SOUND round=%d chosen=%s actual=%s" % [round + 1, spot, str(actual)])
		check(context.detective.entered == spot and by_person.get("detective", {}).get("room", "") == spot,
			"声路试验：侦探实际走到所选听位 " + spot)
		# 现场让路会小幅挪动同伴；试验不得调度他们去预设听位。
		trace = FileAccess.open(trace_path, FileAccess.READ)
		trace.seek(trace_offset)
		var companion_moves := []
		while trace.get_position() < trace.get_length():
			var row: Dictionary = JSON.parse_string(trace.get_line())
			var output: Dictionary = row.get("data", {})
			if str(row.kind) == "output" and str(output.get("kind", "")) == "stage" and str(output.get("op", "")) in ["walk", "place"] 					and str(output.get("who", "")) in ["huo", "shen", "su"]:
				companion_moves.append(output)
		trace = null
		print("SOUND_COMPANION_STAGE round=%d moves=%s" % [round + 1, str(companion_moves)])
		check(companion_moves.is_empty() and ["huo", "shen", "su"].all(func(who): return by_person.get(who, {}).get("room", "") == context[who].entered),
			"声路试验：不调度同伴换听位，按各自实际站位听")
		check(fx.seen_lines.size() > 4, "每轮声路试验都演出所说与所闻")
	fx.close_modal()
	# E5 协助可代替 E6 检视这一前置条件。
	await fresh()
	await fx.jump("investigate")
	await fx.open_card("E7")
	fx.close_modal()
	await fx.open_card("board")
	fx.close_modal()
	await fx.open_card("E5")
	check(await fx.card_action("help"), "E5 协助与自动闪回完成")
	fx.close_modal()
	await fx.open_card("E8")
	check(host.ui._modal_event.options.any(func(o): return str(o.id).ends_with(":experiment")), "已看 E7 且协助 E5 后，E8 提供声路试验")
	fx.close_modal()

	# ---- 5. 卡片不再提议回忆；协助碰到本人的旧物之后自动闪回。
	var cases := [["E7", "huo", false], ["E7", "huo", true], ["E5", "su", false], ["E5", "su", true], ["E10", "shen", false]]
	for case in cases:
		await fresh()
		await fx.jump("investigate")
		fx.probe("amplify", [case[1], "S4"])
		await fx.open_card("board")
		fx.close_modal()
		if case[2]: fx.set_progress("flags", "price_heard", true)
		fx.seen_lines.clear()
		await fx.open_card(case[0])
		check(not fx.select("recall") and fx.seen_lines.is_empty(), "检视 %s 不触发低语，也没有回忆按钮" % case[0])
		var choices := [0] if case[2] else []
		check(await fx.card_action("help", choices), "协助 %s 后，自动闪回并返回证物卡" % case[0])
		var touched: bool = fx.progress("recalls").has(case[1])
		var informed: bool = case[2] or case[1] == "shen"
		var expected: bool = case[1] == "huo" or (case[1] == "su" and not informed)
		print("FLASH id=%s person=%s informed=%s recall=%s hooks=%d thought=%s" % [case[0], case[1], informed, touched, fx.hooks(case[1]), str(fx.progress("recall_decision"))])
		check(fx.progress("helped").has(case[0]) and fx.progress("decided").has(case[1]), "协助与闪回后的本人取舍都有记录：" + case[0])
		check(touched == expected and fx.hooks(case[1]) == (1 if expected else 0), "自动闪回取舍：%s，已知代价=%s，取回=%s" % [case[1], informed, expected])
		if case[1] == "shen": check(fx.progress("flags").has("price_heard"), "沈鼎在闪回后自己向铜口问代价")
		if touched and case[1] == "huo":
			check(str(fx.progress("recall_marks").get("2", "")) == "huo" and int(fx.state("replay_vars")[1]) == -1, "回忆标记来源，不替玩家设定重演变量")
		fx.close_modal()
	# E1 只在拔闩试验中触发身体闪回；选择是否往下想仍在对白里。
	for accept in [false, true]:
		await fresh()
		await fx.jump("investigate")
		await fx.open_card("E1")
		check(not fx.select("recall"), "E1 卡片没有主动回忆按钮")
		fx.seen_lines.clear()
		check(await fx.card_action("experiment", [0 if accept else 1]), "E1 试验后自动闪回并返回卡片")
		check(fx.progress("experiments").has("E1") and fx.progress("recalls").has("detective") == accept 			and fx.hooks("detective") == (1 if accept else 0), "E1 身体闪回：是否往下想由玩家选择 %s" % accept)
		fx.close_modal()
	check(not titles.any(func(t): return str(t).begins_with("nudge_")), "主动低语 nudge_* 场面已移除")
	await fresh()
	await fx.jump("investigate")
	for id in ["E1", "E2", "E3", "E4"]:
		await fx.open_card(id)
		fx.close_modal()
		await fx.drain()
	await fx.stand_at(Vector2(930, 200))
	await fx.physics(40)
	print("ROOM_CHANGE life=%s nudged=%s recalls=%s" % [str(fx.progress("life")), str(fx.progress("nudged")), str(fx.progress("recalls"))])
	check(fx.progress("life").is_empty() and fx.progress("recalls").is_empty() and ["huo", "su", "shen"].all(func(who): return not bool(fx.probe("whispered", [who]))), "调查与换房不再触发离场低语或同伴自主回忆")

	# ---- 5c. 答辩陈述从检视认知开放，不要求先重演。
	await fresh()
	await fx.jump("investigate")
	check(fx.probe("statements").is_empty(), "尚未检视时没有答辩陈述")
	await fx.open_card("E8")
	fx.close_modal()
	var statements: Array = fx.probe("statements")
	check(statements.has("V3_0") and statements.has("V3_1") and not statements.has("V2_0"), "检视 E8 后开放这一问的各种陈述，不代选答案")
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
	check(statements.has("V6_0") and statements.has("V6_1") and statements.has("trust"), "两件相熟旁证开放第六问与信任陈述")

	# ---- 5c2. 手印先比自己的手，之后才列在场同伴。
	await fresh()
	await fx.jump("investigate")
	var shelf: Vector2 = host.field.site_point("E11a")
	await fx.stand_near(Vector2(930, 200), shelf, 64.0)
	host.field.execute({"kind": "stage", "op": "place", "who": "huo", "to": {"point": fx.player().position + Vector2(24, 0)}})
	await fx.physics(4)
	await fx.open_card("E11")
	check(host.ui._modal_event.options.all(func(o): return not str(o.id).ends_with(":huo")), "手印未比自己前，不列同伴")
	check(await fx.card_action("compare:detective"), "手印先比侦探自己的手")
	check(fx.progress("hand_comparisons").has("detective"), "侦探自己的手印比对有记录")
	check(host.ui._modal_event.options.any(func(o): return str(o.id).ends_with(":huo")), "比过自己的手之后，列出架旁同伴")
	fx.close_modal()

	# ---- 5d. 第六个问题起初不写出来（修订四·2）
	await fresh()
	await fx.jump("investigate")
	fx.set_progress("state", "replay_open", true)
	await fx.physics(4)
	host.ui.open_manuscript()
	await process_frame
	var page := fx.modal_text()
	check(not page.contains("昨夜之前") and not fx.probe("statements").has("V6_0"), "重演手稿起初只有五个问题，“素不相识”是不写出来的默认")
	fx.close_modal()
	fx.seen_lines.clear()
	fx.probe("examine", ["E9"])
	await fx.drain()
	check(not fx.probe("v6_open"), "细看一件相熟旁证，第六个问题还不出现")
	fx.probe("examine", ["E12"])
	await fx.drain()
	host.ui.open_manuscript()
	await process_frame
	page = fx.modal_text()
	check(fx.probe("v6_open") and page.contains("昨夜之前，四人彼此") and seen().contains(fx.line_text("v6_doubt.002")),
		"细看两件相熟旁证（杯子、放大镜）后，侦探起疑，手稿出现第六个问题")
	fx.close_modal()
	await fresh()
	await fx.jump("reversal_one")
	check(not fx.probe("v6_open"), "另一条路：起初没有第六个问题")
	fx.seen_lines.clear()
	var refused_path: Dictionary = await run_replay([0, 0, 0, 0, 0, 0])
	var doubt_text: String = fx.text("夜间·怀疑·生人")
	var refusal_thoughts: Array = refused_path.frames.map(func(frame): return str(frame.thoughts.detective)).filter(func(thought): return str(thought).contains(doubt_text))
	print("V6_REPLAY doubt=%s matched_thoughts=%s open=%s flags=%s seen=%s" % [doubt_text, str(refusal_thoughts), fx.probe("v6_open"), str(fx.progress("flags")), seen()])
	check(fx.probe("v6_open") and seen().contains(fx.line_text("replay_refused.003")), "重演中侦探因信任不足拒绝转述后，手稿出现第六个问题")
	fx.close_modal()

	# ---- 5e. 交互一致性与减少重复（修订四·7、8、9）
	await fresh()
	await fx.jump("investigate")
	check(fx.player().collision_mask == 1 and fx.actor("su").collision_mask == 1, "人与人不互相阻挡（只与墙和家具碰撞）")
	fx.player().position = host.field.site_point("E4") + Vector2(20, -20)
	await fx.physics(3)
	host.field._approach("E4")
	await fx.frames(3)
	check(host.ui.modal_kind == "card", "站在够得着的地方点物件：直接打开，不绕到别处")
	fx.seen_lines.clear()
	var helped: bool = await fx.card_action("help")
	var brief := fx.modal_text()
	check(helped and brief.contains("这布是我拧的") and not brief.contains("一条拧过的湿抹布"), "请人看过之后只给新得到的那一句，不重显整张原文")
	fx.close_modal()
	await fx.physics(2)
	await fx.stand_near(Vector2(930, 600), host.field.site_point("L6"), 64.0)
	var l6_visible: bool = await fx.wait_until(func(): return host.field.site_visible("L6"), 2.0)
	await fx.stand_near(Vector2(706, 600), host.field.site_point("L7"), 64.0)
	var l7_visible: bool = await fx.wait_until(func(): return host.field.site_visible("L7"), 2.0)
	print("LIFE_TRACES L6=%s L7=%s player=%s" % [l6_visible, l7_visible, fx.player().position])
	check(l6_visible and l7_visible, "R-06、R-07 各有一处本人的生活痕迹")
	fx.player().position = host.field.site_point("L6") + Vector2(0, 16)
	await fx.physics(3)
	host.field.interact("L6")
	await fx.frames(3)
	var hook := fx.modal_text()
	check(hook.contains("机油") and hook.contains("挂很多回"), "R-06 床头挂钩上的油渍：相熟与久居的旁证，不编号")
	fx.close_modal()
	await fx.physics(2)
	fx.player().position = host.field.site_point("L7") + Vector2(0, 16)
	await fx.physics(3)
	host.field.interact("L7")
	await fx.frames(3)
	var stone := fx.modal_text()
	check(stone.contains("磨刀石") and stone.contains("深槽"), "R-07 桌上磨得凹陷的磨刀石")
	fx.close_modal()
	await fx.physics(2)
	# 点到窗：现场认出被点中的家具，把点击报给章节，章节回一句心声。
	var window: Array = fx.map().layout.asides.filter(func(decor): return str(decor[1]).contains("窗"))[0]
	var rect: Array = window[0]
	var hits_at: Dictionary = fx.map().furniture_hits(Vector2(rect[0] + rect[2] / 2.0, rect[1] + rect[3] / 2.0))
	host.dispatch({"kind": "furniture", "map": fx.map().map_key(), "nodes": hits_at.get("nodes", []), "texts": hits_at.get("texts", {})})
	await fx.frames(3)
	check(fx.aside().visible and fx.aside().text == str(window[1]), "点到窗：也给一句心声（取当前地图区域）")

	# ---- 6. 重演：素不相识时的拒绝，与全部吻合
	fx.set_progress("state", "replay_open", true) # 新宿主：重演须先开放；原宿主的 run_replay 不设此门。
	await fx.physics(4)
	var refused: Dictionary = await run_replay([0, 0, 0, 0, 0, 0])
	fx.close_modal()
	var doubt := false
	for frame in refused.frames:
		if str(frame.thoughts.detective).contains("我凭什么信一个昨天才认识的人？"):
			doubt = true
	check(doubt and not bool(refused.full_match), "V5=霍铎、V6=素不相识：回放里侦探的心声是“我凭什么信一个昨天才认识的人？”")
	var unvisited: Array = refused.findings.filter(func(f): return f.status == "unvisited")
	check(not unvisited.is_empty() and str(unvisited[0].text).begins_with("你还没查过这里") and str(unvisited[0].text).contains("还有东西没看过"), "未检视之处只给地点")
	var regions := []
	for f in unvisited:
		regions.append(str(f.text))
	check("".join(regions).contains("门厅门框一带还有东西没看过"), "“未查”提示具体到区域，如“门厅门框一带还有东西没看过”（修订三·6）")
	await fresh()
	await fx.jump("replay_match")
	var matched: Dictionary = fx.replay_result()
	check(bool(matched.full_match), "全部证物检视后，唯一正确组合全部吻合")
	check(matched.findings.all(func(f): return f.status == "match"), "对照结果全部为“吻合”")
	var moment: Dictionary = {}
	var basis := false
	for frame in matched.frames:
		if str(frame.label).contains("铜口说话"):
			moment = frame
		if str(frame.thoughts.detective).contains("我认得他的字。我信他") and str(frame.thoughts.detective).contains("我更信霍铎"):
			basis = true
	var distinct := {}
	for key in moment.thoughts:
		distinct[str(moment.thoughts[key])] = true
	check(distinct.size() == 4, "回放：铜口说话的同一刻，四个心声气泡各不相同")
	check(basis, "回放：侦探闩门时的心声写出决策依据（认得他的字、我信他、两边比更信霍铎）")
	var digits := false
	for frame in matched.frames:
		for key in frame.thoughts:
			if RegEx.create_from_string("[0-9０-９]").search(str(frame.thoughts[key])) != null:
				digits = true
	check(not digits, "心声气泡不出现数值")
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
	check(await fx.wait_until(func(): return host.ui.modal_kind == "replay"), "跳过回放后回到手稿")
	check(bool(fx.state("full_match_seen")), "跳过与看完结算相同：全部吻合触发 S9（第二次反转）")
	fx.close_modal()

	# ---- 7. 始业之答（陈述与出示成对；第五轮修订删去反问）→ 门开
	await fresh()
	await fx.jump("replay_match")
	check(await fx.compare_hands(), "终局手印分支：实际完成四人的比对")
	fx.probe("apply_recall", ["E10", "shen"])
	fx.seen_lines.clear()
	var end_started := Time.get_unix_time_from_system()
	host.dispatch({"kind": "interact", "site": "E8"})
	await fx.wait_until(func(): return host.ui.modal_kind == "menu", 5.0)
	fx.select("answer")
	var picks := [{"say": "me", "show": "E9"}, {"say": "V4_0", "show": "E3"}, {"say": "me", "show": "E3"}, {"say": "V3_0", "show": "E8"},
		{"say": "V4_0", "show": "E7"}, {"say": "trust", "show": "E10"}]
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
				if str(r.text).begins_with("反问"):
					saw_reverse = true
			if str(d.line.text).contains("杯子说明"):
				saw_wrong_show = true
			if str(d.line.text).contains("回答的是别的问题"):
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
	check(saw_wrong_show, "陈述对、出示不相干：得到指向性回应，不扣分、不重来")
	check(saw_wrong_say, "陈述不对：指出“回答的是别的问题”")
	check(not saw_reverse and not fx.progress("flags").has("asked_reverse"), "答辩到第 4 步为止，不再出现“反问”（第五轮修订 3）")
	check(fx.progress("flags").has("door_open") and fx.ended(), "门开：S11 结尾")
	check(fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, "门开：隔离门画面换为开启")
	var finale := seen()
	check(fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/DemonPrints").visible and finale.contains("鞋底窄") and finale.contains("起居厅")
		and not fx.map().get_node("ObjectsAndPeople/MechanicalIsolationDoor/LoungeCover").visible,
		"门开：门外是起居厅，烟灰上留下一行窄底皮鞋印（恶魔显形才能抬闩，修订三·8、第五轮修订 6）")
	check(room_name(fx.player().position) == "门厅", "结局时地名显示“门厅”")
	check(fx.progress("flags").has("asked_hand") and finale.contains(fx.line_text("s10_hand.006")) and not finale.contains("第二条"),
		"细看过窄手印、又见门外窄底鞋印：可问“架上的手印，是你的吗？”，恶魔承认取物，不提“第二条”（修订四·5、第五轮修订 5）")
	check(finale.contains("我却没有马上迈出去"), "门开时侦探有一句克制的心声（修订四·10）")
	check(finale.contains(fx.line_text("s10_open.007")) and finale.contains(fx.line_text("s10_open.006")), "沈鼎（被钩）与苏泠（未被钩）的结尾台词按是否被钩变化（修订三·4）")
	var ending_text := fx.modal_text() + "\n" + "\n".join(host.ui._modal_event.get("rows", []))
	check(ending_text.contains("想起了一些事的人：沈鼎") and ending_text.contains("没有往下想的：你自己、霍铎、苏泠"), "结局列出取回者与没有往下想的人")
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
	check(not fx.dialogue_active(), "对话结束后多按的空格不会触发身边人物的对白")
	await create_timer(0.7).timeout
	host.field._pick_nearest()
	host.field._unhandled_input(key)
	await process_frame
	check(fx.dialogue_active(), "过一会儿再按空格，仍能与身边的人交谈")
	await fx.drain()

	# ---- 8. 铜口菜单不再提供歇息选择。
	await fresh()
	await fx.jump("investigate")
	host.dispatch({"kind": "interact", "site": "E8"})
	check(await fx.wait_until(func(): return host.ui.modal_kind == "menu", 5.0), "铜口菜单出现")
	check(not fx.select("rest") and not fx.ended(), "铜口菜单没有歇息选择，不结束章节")
	fx.close_modal()

	print("TOTAL %d checks, %d failed" % [checks, failures])
	for name in failed_names: print("FAILED: " + name)
	call_deferred("_finish", 0 if failures == 0 else 1)
