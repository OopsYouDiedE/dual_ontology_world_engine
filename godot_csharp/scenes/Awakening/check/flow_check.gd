extends SceneTree
## 流程跳转回归：直接把局面摆到关键状态（醒来、自报身份广播、公告框、门厅反转、同伴触物回忆两种分支、
## 重演全部吻合、答辩第 5 步、门开、退出考题），检查可达与无脚本错误。
## 对白由脚本代为推进；这不是游玩验证，不替代独立盲玩。
##   godot --headless --path godot_csharp --script res://scenes/Awakening/check/flow_check.gd

const SCENE := "res://scenes/Main2D.tscn"
const Content := preload("res://scenes/Awakening/AwakeningContent.gd")
## 恶魔在全章都不提契约第 9、10 条。
const FORBIDDEN := ["第九", "第 9", "第9", "第十条", "第 10", "九、", "十、", "尽忘", "归乡", "放逐", "十五名", "担保耗尽"]
var checks := 0
var failures := 0
var holder: Node
var seen_lines: Array[String] = []

func _initialize() -> void:
	call_deferred("_run")

func check(ok: bool, name: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print(("PASS " if ok else "FAIL ") + name)

func fresh(skip_intro := true) -> Node:
	if is_instance_valid(holder):
		holder.queue_free()
		await process_frame
		await process_frame
	var packed: PackedScene = load(SCENE)
	holder = packed.instantiate()
	var game: Node = holder.get_node("Awakening")
	if skip_intro:
		game.set_meta("skip_intro", true)
	root.add_child(holder)
	for i in 3:
		await process_frame
	return game

## 代为推进对白：揭示整行、推进；遇到选项按 choices 依次选择（默认第一个）。读过的行记入 seen_lines。
func drain(game: Node, choices: Array = [], limit := 900) -> void:
	var guard := 0
	var last_id := ""
	while guard < limit:
		guard += 1
		await process_frame
		var d = game.dialogue
		if game.modal_kind == "name":
			game._name_field.text = "试验"
			game._confirm_name()
			continue
		if not d.active:
			var idle := 0
			while idle < 8 and not d.active and game.modal_kind != "name":
				await process_frame
				idle += 1
			if not d.active and game.modal_kind != "name":
				return
			continue
		if d.busy or d.line == null:
			continue
		if str(d.line.id) != last_id:
			last_id = str(d.line.id)
			seen_lines.append(str(d.line.text))
		if d.body.visible_characters >= 0:
			d.reveal()
			continue
		var allowed: Array = d.line.responses.filter(func(r): return r.is_allowed)
		if allowed.is_empty():
			d.advance()
		else:
			var index: int = choices.pop_front() if not choices.is_empty() else 0
			d.choose(str(allowed[index].next_id))

## 触物回忆：同伴要先走过来，再决定；一直推进到回忆结束、证物卡重新打开为止。
func run_recall(game: Node, id: String, choices: Array = []) -> void:
	game._on_recall(id)
	for round in 80:
		await drain(game, choices)
		if game.modal_kind == "card" and not game.dialogue.active:
			return
		await process_frame

## 打开证物卡（第一次打开可回忆的物件时先有一句“不妨一试”），推进到卡片出现为止。
func open_card_settled(game: Node, id: String) -> void:
	game.open_card(id)
	for round in 40:
		await drain(game)
		if game.modal_kind == "card" and not game.dialogue.active:
			return
		await process_frame

func wait_until(condition: Callable, limit := 600) -> bool:
	for i in limit:
		if condition.call():
			return true
		await process_frame
	return condition.call()

func _run() -> void:
	# ---- 0. 对白标题齐全；全章文本不出现第 9、10 条
	var story: Resource = load("res://scenes/Awakening/awakening.dialogue")
	var titles: Array = story.titles.keys()
	var needed := ["s1_wake", "s2_meet", "s2_tea", "s3_broadcast", "s4_rewards", "s5_question", "s6_begin", "replay_unlock", "exp_E1", "exp_E2", "exp_E3",
		"s6_reversal", "help_E4", "help_E5", "help_E6", "help_E7", "help_E9", "help_E10", "help_E11", "nudge_E1", "nudge_E5", "nudge_E7", "nudge_E10",
		"recall_ask_detective", "recall_detective", "recall_su", "recall_huo", "recall_shen",
		"sound_test", "talk_huo", "talk_shen", "talk_su", "s8_reversal", "rest_confirm", "answer_open", "answer_q1", "answer_ok1", "answer_q2",
		"answer_ok2", "answer_need_test", "answer_q3", "answer_ok3", "answer_q4", "answer_ok4",
		"answer_wrong_show", "answer_wrong_say", "answer_pause", "answer_offer_reverse", "answer_reverse", "answer_reverse_wrong",
		"s10_steps", "s10_open", "ending_rest", "enter_foyer", "enter_pantry", "enter_R08", "enter_R06", "enter_R07", "replay_first", "replay_refused"]
	for who in ["su", "huo", "shen"]:
		for part in ["recall_offer_", "recall_shown_", "recall_touch_", "recall_refuse_", "recall_hesitate_"]:
			needed.append(part + who)
	var missing := needed.filter(func(t): return not titles.has(t))
	check(missing.is_empty(), "代码引用的对白标题全部存在 %s" % str(missing))
	var raw := FileAccess.get_file_as_string("res://scenes/Awakening/awakening.dialogue") + FileAccess.get_file_as_string("res://scenes/Awakening/AwakeningContent.gd")
	var hits := FORBIDDEN.filter(func(w): return raw.contains(w))
	check(hits.is_empty(), "台词与证物文本中没有契约第 9、10 条的字样 %s" % str(hits))

	# ---- 1. 醒来 → 相遇 → 自报身份广播 → 核验 → 始业之问 → 调查（完整开场，对白代为推进）
	var game := await fresh(false)
	check(await wait_until(func(): return game.dialogue.active), "开场：S1 醒来对白出现")
	check(game.player.seated and game._room_name(game.player.position) == "R-05", "醒来：侦探在 R-05 床上")
	await drain(game)
	check(game.stage == "roam", "醒来后可以自由行动")
	check(game.site_visible("E12") and game.site_visible("E11a") and not game.site_visible("E1"), "醒来时只看得到 R-05 里的东西")
	game.player.stand_up()
	game.player.position = Vector2(300, 600)
	check(await wait_until(func(): return game.stage == "meeting"), "走进活动厅触发 S2 相遇")
	seen_lines.clear()
	for round in 120:
		await drain(game, [0])
		if game.modal_kind == "card":
			game.close_modal()
		if game.stage == "investigate" and not game.dialogue.active and game.modal_kind == "" and game.follow_camera:
			break
		await process_frame
	check(game.stage == "investigate", "S2–S5 结束后进入调查")
	var opening := "\n".join(seen_lines)
	check(opening.contains("我是此地的代管者，也是诸位的教师") and opening.contains("恶魔亦可") and opening.contains("不得令诸位因无知而死"),
		"S3 开场广播：恶魔自报身份与契约要点")
	check(opening.find("恶魔亦可") < opening.find("此为始业之问"), "自报身份在始业之问之前")
	check(FORBIDDEN.filter(func(w): return opening.contains(w)).is_empty(), "开场全部台词不提第 9、10 条")
	check(int(game.engine.DesireStrength("huo")) == 2 and int(game.engine.DesireStrength("su")) == 2 and int(game.engine.DesireStrength("shen")) == 2,
		"S4 恶魔逐一点到三人的欲望（Corruption 来源，欲望各加强一级）")
	check(game.flags.has("board_read"), "S3 广播结束后，公告框第 1—8 条完整呈现过一次（修订三·2）")
	check(game.detective_name == "试验", "姓名由玩家输入")
	check(game.can_access_dorm("musician"), "调查阶段可进入其他宿舍")
	for dorm in [["R-06", Vector2(480, 200)], ["R-07", Vector2(800, 200)]]:
		var route: PackedVector2Array = game._route(Vector2(640, 424), dorm[1])
		check(not route.is_empty() and game._walkable(dorm[1]), "%s 可以走进去（修订三·1）" % dorm[0])

	# ---- 2. 公告框：只抄第 1—8 条
	game.open_board()
	var board: Array = game.engine.PostedClauses()
	var board_text := "\n".join(board)
	check(board.size() == 8 and board_text.contains("八、封存的记忆可凭对应之物恢复") and board_text.contains("不得令学员因无知而死"), "活动厅公告框抄录契约第 1—8 条")
	check(FORBIDDEN.filter(func(w): return board_text.contains(w)).is_empty(), "公告框没有第 9、10 条")
	game.close_modal()

	# ---- 3. 门厅反转
	game = await fresh()
	game.jump_to("investigate")
	await open_card_settled(game, "E1")
	check(game.modal_kind == "card" and game.examined.has("E1"), "检视隔离门：打开证物卡")
	game.close_modal()
	await drain(game)
	check(game.replay_open, "第一次看到闩上的门后，重演开放")
	game.open_card("E3")
	game._on_experiment("E3")
	await drain(game)
	check(game.flags.has("reversal_one") and game.experiments.has("E3"), "门厅反转：对照靴底后触发“……是我”")
	check(game.statements().has("me"), "陈述列表出现“是我闩的门”")

	# ---- 4. 声路试验（含 R-08 听点）
	game.last_sound = game.engine.SoundTest(0, {"detective": "foyer", "huo": "hall", "shen": "pantry", "su": "R08"})
	check(game.heard("detective").begins_with("……开门") and game.heard("detective").contains("从那边") and not game.heard("detective").contains("门外"),
		"声路试验：门边只听到“……开门”，同伴只说来处的方向（从那边，指向门），不说“像在门外”（修订三·5）")
	check(game.heard("su").begins_with("……开门") and game.heard("su").contains("墙那边") and not game.heard("su").contains("门外"), "声路试验：R-08 只听到“……开门”，从墙那边")
	check(game.heard("huo").begins_with("闩上门，不要开门。") and game.heard("huo").contains("从铜口里"), "声路试验：铜口旁听到整句")
	game.close_modal()

	# ---- 5. 触物回忆：三人倾向各不相同（未告知／已告知第 8 条），沈鼎犹豫时侦探的三种回答
	# [物件, 人, 是否先指给他看第 8 条, 犹豫时侦探的回答序号（-1 表示不会犹豫）, 期望结果, 期望心声片段]
	var cases := [
		["E7", "huo", false, -1, "touch", "碰一下"],
		["E7", "huo", true, -1, "touch", "图纸值这个价"],
		["E5", "su", false, -1, "touch", "碰一下"],
		["E5", "su", true, -1, "refuse", "第八条"],
		["E10", "shen", true, -1, "refuse", "第八条"],
		["E10", "shen", false, 0, "touch", "我信你"],
		["E10", "shen", false, 1, "refuse", "你说别碰"],
		["E10", "shen", false, 2, "refuse", "自己拿主意"],
	]
	for case in cases:
		game = await fresh()
		game.jump_to("investigate")
		game.engine.Amplify(case[1], "S4")
		game.open_board()
		game.close_modal()
		var choices := [0 if case[2] else 1]
		if case[3] >= 0:
			choices.append(case[3])
		seen_lines.clear()
		await open_card_settled(game, case[0])
		game.close_modal()
		await run_recall(game, case[0], choices)
		var touched: bool = game.recalls.has(case[1])
		var hesitated: bool = "
".join(seen_lines).contains("你说呢")
		var thought := str(game.recall_decision.get("thought", ""))
		var ok: bool = (touched == (case[4] == "touch")) and int(game.engine.Hooks(case[1])) == (1 if touched else 0) and thought.contains(case[5])
		var advice_text: String = ["劝碰", "劝别碰", "由他自己"][case[3]] if case[3] >= 0 else ""
		check(ok, "%s%s：%s%s → %s（%s）" % [Content.PERSON_NAMES[case[1]], "（已被告知第 8 条）" if case[2] else "（未被告知）",
			"犹豫并反问“你说呢？”，侦探" + advice_text + " " if case[3] >= 0 else "", "", case[4], thought])
		if case[1] == "shen" and not case[2]:
			check(hesitated, "沈鼎未被告知时先犹豫，反问侦探“你说呢？”")
		elif case[3] < 0:
			check(not hesitated, "%s不犹豫，直接自己决定" % Content.PERSON_NAMES[case[1]])
	game = await fresh()
	game.jump_to("investigate")
	game.engine.Amplify("huo", "S4")
	seen_lines.clear()
	await open_card_settled(game, "E7")
	check(game.modal_kind == "card" and "\n".join(seen_lines).contains("图纸就在主楼"), "第一次打开石板的证物卡：先听到恶魔朝向霍铎欲望的那句“不妨一试”，然后才出现回忆选项（修订三·3）")
	var labels := []
	for b in game.modal_root.find_children("*", "Button", true, false):
		labels.append(str(b.text))
	check(labels.any(func(t): return str(t).contains("契约第 8 条")), "回忆选项标注“（契约第 8 条）”（修订三·2）")
	game.close_modal()
	seen_lines.clear()
	await open_card_settled(game, "E7")
	check(not "\n".join(seen_lines).contains("不妨一试"), "同一件物件的“不妨一试”只说一次")
	game.close_modal()
	var nudges := {}
	for id in ["E1", "E5", "E7", "E10"]:
		var resource: Resource = load("res://scenes/Awakening/awakening.dialogue")
		nudges[id] = resource.titles.has("nudge_" + id)
	var nudge_text := FileAccess.get_file_as_string("res://scenes/Awakening/awakening.dialogue")
	check(nudge_text.contains("门闩记得你昨夜是什么") and nudge_text.contains("还有你从未听过的") and nudge_text.contains("图纸就在主楼") and nudge_text.contains("你想护住的"),
		"四件物件四句不同的话，各自朝向此人的欲望")
	await run_recall(game, "E7", [1])
	check(game.recall_marks.get(2, "") == "huo" and int(game.replay_vars[1]) == -1, "回忆把 V2 标为“回忆所证”，但不替玩家设定变量")
	game.close_modal()

	# ---- 6. 重演：素不相识时的拒绝，与全部吻合
	var refused: Dictionary = game.run_replay([0, 0, 0, 0, 0, 0])
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
	game = await fresh()
	game.jump_to("replay_match")
	check(bool(game.replay_result.full_match), "全部证物检视后，唯一正确组合全部吻合")
	check(game.replay_result.findings.all(func(f): return f.status == "match"), "对照结果全部为“吻合”")
	var moment: Dictionary = {}
	var basis := false
	for frame in game.replay_result.frames:
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
	for frame in game.replay_result.frames:
		for key in frame.thoughts:
			if RegEx.create_from_string("[0-9０-９]").search(str(frame.thoughts[key])) != null:
				digits = true
	check(not digits, "心声气泡不出现数值")
	game.full_match_seen = false
	game.play_replay(game.replay_result)
	await process_frame
	await process_frame
	game.stage_node.skipping = true
	for round in 80:
		# 全部吻合后三位同伴先走过来，再说第二次反转。
		await drain(game)
		if game.modal_kind == "replay":
			break
		await process_frame
	check(await wait_until(func(): return game.modal_kind == "replay"), "跳过回放后回到手稿")
	check(game.full_match_seen, "跳过与看完结算相同：全部吻合触发 S9（第二次反转）")
	game.close_modal()

	# ---- 7. 始业之答（陈述与出示成对，含第 5 步反问）→ 门开
	game = await fresh()
	game.jump_to("replay_match")
	game._apply_recall("E10", "shen")
	seen_lines.clear()
	game.answer()
	var picks := [{"say": "me", "show": "E9"}, {"say": "V4_0", "show": "E3"}, {"say": "me", "show": "E3"}, {"say": "V3_0", "show": "E8"},
		{"say": "V4_0", "show": "E7"}, {"say": "trust", "show": "E10"}, {"say": "", "show": "E11"}]
	var saw_reverse := false
	var saw_wrong_show := false
	var saw_wrong_say := false
	for step in 1800:
		await process_frame
		if game.ended:
			break
		var d = game.dialogue
		if d.active and d.line != null and not d.busy:
			for r in d.line.responses:
				if str(r.text).begins_with("反问"):
					saw_reverse = true
			if str(d.line.text).contains("杯子说明"):
				saw_wrong_show = true
			if str(d.line.text).contains("回答的是别的问题"):
				saw_wrong_say = true
			if seen_lines.is_empty() or seen_lines[-1] != str(d.line.text):
				seen_lines.append(str(d.line.text))
		if game.modal_kind == "answer" and not picks.is_empty():
			game._modal_choose(picks.pop_front())
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
	check(saw_wrong_show, "陈述对、出示不相干：得到指向性回应，不扣分、不重来")
	check(saw_wrong_say, "陈述不对：指出“回答的是别的问题”")
	check(saw_reverse, "答辩第 5 步：重演全部吻合后出现“反问”选项")
	check(game.flags.has("asked_reverse"), "反问出示无尘方框：恶魔承认“我从未说过诸位昨日方至”")
	check(game.flags.has("door_open") and game.ended, "门开：S11 结尾")
	check(game.get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, "门开：隔离门画面换为开启")
	var finale := "\n".join(seen_lines)
	check(game.get_node("ObjectsAndPeople/MechanicalIsolationDoor/DemonPrints").visible and finale.contains("鞋底窄"),
		"门开：主楼走廊烟灰上留下一行窄底皮鞋印（恶魔显形才能抬闩，修订三·8）")
	check(game._room_name(game.player.position) == "门厅", "结局时地名显示“门厅”")
	check(finale.contains("二十四道，我都想起来了") and finale.contains("我想先用自己的耳朵听这里"), "沈鼎（被钩）与苏泠（未被钩）的结尾台词按是否被钩变化（修订三·4）")
	var ending_text := ""
	for l in game.modal_root.find_children("*", "RichTextLabel", true, false):
		ending_text += str(l.get_parsed_text()) + "\n"
	check(ending_text.contains("留在恶魔那里的钩：沈鼎") and ending_text.contains("你没有交出的：你自己、霍铎、苏泠"), "结局列出“钩”与“你没有交出的”（修订三·4）")
	check(FileAccess.file_exists("user://chapter1_end.json"), "章节结束状态写入 user://chapter1_end.json")

	# ---- 7b. 对话刚结束时多按的空格不触发身边人物（修订三·1）
	game = await fresh()
	game.jump_to("investigate")
	game.player.position = game.actor_of("huo").position + Vector2(24, 0)
	for i in 4:
		await physics_frame
	game.say("exp_E2")
	# 一行行推进，对白一结束就在下一帧按空格（模拟玩家多按的一下）。
	await wait_until(func(): return game.dialogue.active)
	for i in 600:
		await process_frame
		var d = game.dialogue
		if not d.active:
			break
		if d.busy or d.line == null:
			continue
		if d.body.visible_characters >= 0:
			d.reveal()
		else:
			d.advance()
	game._pick_nearest()
	var key := InputEventKey.new()
	key.keycode = KEY_SPACE
	key.pressed = true
	game._unhandled_input(key)
	await process_frame
	check(not game.dialogue.active, "对话结束后多按的空格不会触发身边人物的对白")
	await create_timer(0.7).timeout
	game._pick_nearest()
	game._unhandled_input(key)
	await process_frame
	check(game.dialogue.active, "过一会儿再按空格，仍能与身边的人交谈")
	await drain(game)

	# ---- 8. 退出考题
	game = await fresh()
	game.jump_to("investigate")
	game._confirm_rest()
	await drain(game, [0])
	check(game.ended and game.flags.has("rest_chosen"), "退出考题：不作答，在生活翼歇息，以简短收尾结束")

	print("TOTAL %d checks, %d failed" % [checks, failures])
	quit(0 if failures == 0 else 1)
