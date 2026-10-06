extends SceneTree
## 流程跳转回归：直接把局面摆到关键状态（醒来、开场广播、学园守则、门厅反转、同伴触物回忆两种分支、
## 重演全部吻合、始业之答、门开、退出考题），检查可达与无脚本错误。地图为第五轮修订后的乙区（中廊两侧各三间房）。
## 对白由脚本代为推进；这不是游玩验证，不替代独立盲玩。
##   godot --headless --path godot_csharp --script res://scenes/Awakening/check/flow_check.gd

const SCENE := "res://scenes/Main2D.tscn"
const Content := preload("res://scenes/Awakening/AwakeningContent.gd")
## 恶魔在全章都不提契约第 9、10 条。
const FORBIDDEN := ["第九", "第 9", "第9", "第十条", "第 10", "九、", "十、", "尽忘", "归乡", "放逐", "十五名", "担保耗尽"]
## 第五轮修订删去的说法：初涉斯地、可数的刻痕、恶魔自报为恶魔或提导师与契约、门外是“主楼走廊”。
const RETIRED := ["初涉斯地", "二十四", "刻痕", "恶魔亦可", "导师将我拘于", "契约第", "主楼走廊", "依第二条", "第 8 条", "第八条"]
var checks := 0
var failures := 0
var holder: Node
var seen_lines: Array[String] = []

func _initialize() -> void:
	call_deferred("_run")

func _finish(exit_code: int) -> void:
	# _run has returned, releasing its Resource/DialogueLine locals first.
	# Free the fixture (including its C# engine), then flush queued deletion.
	if is_instance_valid(holder):
		holder.get_node("Awakening").begin_shutdown()
		holder.queue_free()
	holder = null
	await process_frame
	await process_frame
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(exit_code)

func check(ok: bool, name: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print(("PASS " if ok else "FAIL ") + name)
	if not ok:
		# Later stages depend on these states. Stop at the first concrete failure
		# instead of running nested dialogue waits against an invalid fixture.
		print("TOTAL %d checks, %d failed" % [checks, failures])
		quit(1)

func fresh(skip_intro := true) -> Node:
	if is_instance_valid(holder):
		holder.get_node("Awakening").begin_shutdown()
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
			var index := 0
			if not choices.is_empty():
				index = int(choices.pop_front())
			else:
				# Current companion talks return to a persistent question menu.
				# These checks inspect the opening/account of the prior recall;
				# selecting the first question repeatedly never ends that menu.
				for option in allowed.size():
					if str(allowed[option].text) == "没事了。":
						index = option
						break
			if index < 0 or index >= allowed.size():
				check(false, "对白选项越界：%s，选择 %d，可选 %d" % [str(d.line.id), index, allowed.size()])
				return
			d.choose(str(allowed[index].next_id))
	check(false, "对白未在 %d 帧内结束：stage=%s modal=%s busy=%s line=%s" % [limit, game.stage, game.modal_kind, game.dialogue.busy, str(game.dialogue.line.id) if game.dialogue.line != null else "<waiting>"])

## 触物回忆：同伴要先走过来，再决定；一直推进到回忆结束、证物卡重新打开为止。
func run_recall(game: Node, id: String, choices: Array = []) -> void:
	game._on_recall(id)
	for round in 80:
		await drain(game, choices)
		if game.modal_kind == "card" and not game.dialogue.active:
			return
		await process_frame
	check(false, "触物回忆没有返回证物卡：" + id)

## 打开证物卡（第一次打开可回忆的物件时先有一句“不妨一试”），推进到卡片出现为止。
func open_card_settled(game: Node, id: String) -> void:
	game.open_card(id)
	for round in 40:
		await drain(game)
		if game.modal_kind == "card" and not game.dialogue.active:
			return
		await process_frame
	check(false, "打开证物卡没有完成：" + id)

## 让某位同伴自己走向旧物的整段过程推进完：对白代为推进，直到他放回旧物。
func run_reach(game: Node, who: String, choices: Array = []) -> void:
	for round in 120:
		await drain(game, choices)
		if str(game.life.get(who, {}).get("state", "")) == "done" and not game.dialogue.active:
			return
		await process_frame
	check(false, "同伴取物没有完成：" + who)

## 一个房间里够得着某个点、又走得到的位置。
func stand_near(game: Node, room_center: Vector2, point: Vector2, radius: float) -> void:
	game.player.position = room_center
	for i in 3:
		await physics_frame
	game.player.position = game._spot_near(point, radius, game.player)
	for i in 3:
		await physics_frame

func wait_until(condition: Callable, limit := 600) -> bool:
	for i in limit:
		if condition.call():
			return true
		await process_frame
	return condition.call()

func _run() -> void:
	# ---- 0. 对白标题齐全；全章文本不出现第 9、10 条
	# Inspect the actual embedded Chapter1Script dialogue compiled by the host.
	var game := await fresh()
	var story: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Awakening/Script/generated_awakening_dialogue.json"))
	check(story.has("entries"), "真实生成对白文档存在")
	var titles: Array = story.entries.keys()
	var needed := ["s1_wake", "s2_meet", "s2_tea", "s3_broadcast", "s4_rewards", "s5_question", "s6_begin", "replay_unlock", "exp_E1", "exp_E2", "exp_E3",
		"s6_reversal", "help_E4", "help_E5", "help_E6", "help_E7", "help_E9", "help_E10", "help_E11", "nudge_E1", "nudge_E5", "nudge_E7", "nudge_E10",
		"recall_ask_detective", "recall_detective", "recall_su", "recall_huo", "recall_shen",
		"sound_test", "talk_huo", "talk_shen", "talk_su", "s8_reversal", "rest_confirm", "answer_open", "answer_q1", "answer_ok1", "answer_q2",
		"answer_ok2", "answer_need_test", "answer_q3", "answer_ok3", "answer_q4", "answer_ok4",
		"answer_wrong_show", "answer_wrong_say", "answer_pause", "s10_steps", "s10_prints", "s10_hand", "s10_open", "ending_rest",
		"replay_first", "replay_refused"]
	for who in ["su", "huo", "shen"]:
		for part in ["recall_offer_", "recall_shown_", "recall_touch_", "recall_refuse_", "recall_hesitate_"]:
			needed.append(part + who)
	var missing := needed.filter(func(t): return not titles.has(t))
	check(missing.is_empty(), "代码引用的对白标题全部存在 %s" % str(missing))
	var raw: String = JSON.stringify(story.entries) + JSON.stringify(game.engine.Content())
	var hits := FORBIDDEN.filter(func(w): return raw.contains(w))
	check(hits.is_empty(), "台词与证物文本中没有契约第 9、10 条的字样 %s" % str(hits))
	var retired_raw := raw + FileAccess.get_file_as_string("res://scenes/Awakening/Awakening.gd")
	var retired := RETIRED.filter(func(w): return retired_raw.contains(w))
	check(retired.is_empty(), "第五轮修订：不再有“初涉斯地”、可数刻痕、契约条款与“主楼走廊” %s" % str(retired))
	check(not titles.has("answer_offer_reverse") and not titles.has("answer_reverse"), "第五轮修订：删去答辩第 5 步“反问”")

	# ---- 1. 醒来 → 相遇 → 自报身份广播 → 核验 → 始业之问 → 调查（完整开场，对白代为推进）
	game = await fresh(false)
	check(await wait_until(func(): return game.dialogue.active), "开场：S1 醒来对白出现")
	check(game.player.seated and game._room_name(game.player.position) == "R-05", "醒来：侦探在 R-05 床上")
	await drain(game)
	check(game.stage == "roam", "醒来后可以自由行动")
	check(game.site_visible("E12") and game.site_visible("E11a") and not game.site_visible("E1"), "醒来时只看得到 R-05 里的东西")
	game.player.stand_up()
	game.player.position = Vector2(440, 340)
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
	check(opening.contains("我是此地的代管者，也是诸位的教师") and opening.contains("我所立之规") and not opening.contains("恶魔")
		and not opening.contains("导师") and not opening.contains("契约") and not opening.contains("初涉斯地"),
		"S3 开场广播：只称代管者与教师，不说自己是什么，不提导师与契约，没有“初涉斯地”（第五轮修订 1、3）")
	check(opening.find("代管者") < opening.find("此为始业之问"), "开场广播在始业之问之前")
	check(FORBIDDEN.filter(func(w): return opening.contains(w)).is_empty(), "开场全部台词不提第 9、10 条")
	check(opening.contains("不是来给它断案的") and opening.contains("考人，不是问路") and opening.contains("我只信我听见的"),
		"始业之问后，三人各有一句不同的抵触或怀疑，再开始调查（修订四·11）")
	check(opening.contains("插进风衣口袋"), "报偿空格处，侦探有一句克制的心声（修订四·10）")
	check(int(game.engine.DesireStrength("huo")) == 2 and int(game.engine.DesireStrength("su")) == 2 and int(game.engine.DesireStrength("shen")) == 2,
		"S4 恶魔逐一点到三人的欲望（Corruption 来源，欲望各加强一级）")
	check(game.flags.has("board_read"), "S3 广播结束后，公告框上的学园守则完整呈现过一次（修订三·2、第五轮修订 2）")
	check(game.detective_name == "试验", "姓名由玩家输入")
	check(game.can_access_dorm("musician"), "调查阶段可进入其他宿舍")
	for dorm in [["R-06", Vector2(930, 600)], ["R-07", Vector2(706, 600)], ["空房", Vector2(1150, 200)]]:
		var route: PackedVector2Array = game._route(Vector2(800, 388), dorm[1])
		check(not route.is_empty() and game._walkable(dorm[1]) and game._room_name(dorm[1]) == dorm[0], "%s 可以走进去（修订三·1）" % dorm[0])
	# 乙区布局（第五轮修订 7）：中廊两侧各三间房，四人入住，靠外端两间空着；门厅在西端，隔离门开向起居厅。
	var names := {}
	for p in [Vector2(706, 200), Vector2(930, 200), Vector2(1150, 200), Vector2(706, 600), Vector2(930, 600), Vector2(1150, 600)]:
		names[game._room_name(p)] = names.get(game._room_name(p), 0) + 1
	check(names.get("R-05", 0) == 1 and names.get("R-06", 0) == 1 and names.get("R-07", 0) == 1 and names.get("R-08", 0) == 1 and names.get("空房", 0) == 2,
		"中廊两侧各三间房：R-05—08 四间住人，外端两间空着 %s" % str(names))
	check(game._room_name(Vector2(224, 390)) == "门厅" and game._room_name(Vector2(64, 390)) == "起居厅" and not game._walkable(Vector2(144, 384)),
		"门厅在乙区西端，隔离门外是起居厅，门闩着走不过去")

	# ---- 2. 公告框：学园守则四条（第五轮修订 2）
	game.open_board()
	var board: Array = game.engine.PostedRules()
	var board_text := "\n".join(board)
	check(board.size() == 4 and board_text.contains("一、封存之物，可凭对应之物寻回；寻回者，自负其果。") and board_text.contains("四、我所立之规，皆张贴于此。")
		and not board_text.contains("契约") and not board_text.contains("恶魔"), "活动厅公告框贴的是学园守则四条，不是契约")
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
	seen_lines.clear()
	game._on_experiment("E3")
	await wait_until(func(): return game.modal_kind == "menu")
	game._modal_choose("huo")
	for round in 60:
		await drain(game)
		if game.modal_kind == "card" and not game.dialogue.active:
			break
		await process_frame
	check(not game.flags.has("reversal_one") and game.soles_compared.has("huo") and "\n".join(seen_lines).contains("粗齿纹"),
		"比对鞋底由玩家选择对象：比霍铎的鞋，对不上，不触发反转（修订四·4）")
	seen_lines.clear()
	game._on_experiment("E3")
	await wait_until(func(): return game.modal_kind == "menu")
	game._modal_choose("detective")
	await drain(game)
	check(game.flags.has("reversal_one") and game.experiments.has("E3"), "门厅反转：对照靴底后触发“……是我”")
	check("\n".join(seen_lines).contains("簌簌地落"), "“……是我”处，侦探有一句克制的心声（修订四·10）")
	check(game.statements().has("me"), "陈述列表出现“是我闩的门”")

	# ---- 4. 声路试验（含 R-08 听点）
	var sound_result: Array = game.engine.SoundTest(0, {"detective": "foyer", "huo": "hall", "shen": "pantry", "su": "R08"})
	game.engine.SetProgress("state", "last_sound", sound_result)
	var sound_by_person := {}
	for result in sound_result: sound_by_person[str(result.who)] = result
	check(sound_by_person.size() == 4 and sound_by_person.has_all(["detective", "huo", "shen", "su"]), "声路试验返回四人的独立听闻")
	var detective_sound: Dictionary = sound_by_person.detective
	var su_sound: Dictionary = sound_by_person.su
	var huo_sound: Dictionary = sound_by_person.huo
	check(detective_sound.room == "foyer" and str(detective_sound.text).begins_with("……开门") and str(detective_sound.direction).contains("从那边")
		and not str(detective_sound.text).contains("门外") and not str(detective_sound.direction).contains("门外"),
		"声路试验：门边只听到“……开门”，同伴只说来处的方向（从那边，指向门），不说“像在门外”（修订三·5）")
	check(su_sound.room == "R08" and str(su_sound.text).begins_with("……开门") and str(su_sound.direction).contains("墙那边")
		and not str(su_sound.text).contains("门外") and not str(su_sound.direction).contains("门外"), "声路试验：R-08 只听到“……开门”，从墙那边")
	check(huo_sound.room == "hall" and str(huo_sound.text).begins_with("闩上门，不要开门。") and str(huo_sound.direction).contains("从铜口里"), "声路试验：铜口旁听到整句")
	game.close_modal()
	seen_lines.clear()
	for round in 2:
		game.sound_test()
		await wait_until(func(): return game.modal_kind == "sound")
		game._modal_choose(true)
		await drain(game)
		await wait_until(func(): return game.modal_kind == "card")
		if round == 1:
			var title := ""
			for l in game.modal_root.find_children("*", "RichTextLabel", true, false):
				title += str(l.get_parsed_text())
			check(title.contains("第 2 轮") and title.contains("从哪边来"), "声路试验第二轮起以一张小表汇总四处所闻（修订四·8）")
		game.close_modal()
	check(seen_lines.filter(func(l): return l.contains("声音走管子")).size() == 1, "两轮声路试验，苏泠的点评只说一次")

	# ---- 5. 触物回忆：三人倾向各不相同（未告知／已告知守则第 1 条），沈鼎犹豫时侦探的三种回答
	# [物件, 人, 是否先指给他看守则第 1 条, 犹豫时侦探的回答序号（-1 表示不会犹豫）, 期望结果, 期望心声片段]
	var cases := [
		["E7", "huo", false, -1, "touch", "碰一下"],
		["E7", "huo", true, -1, "touch", "图纸值这个价"],
		["E5", "su", false, -1, "touch", "碰一下"],
		["E5", "su", true, -1, "refuse", "守则第一条"],
		["E10", "shen", true, -1, "refuse", "守则第一条"],
		["E10", "shen", false, 0, "touch", "我信你"],
		["E10", "shen", false, 1, "refuse", "你说别碰"],
		["E10", "shen", false, 2, "refuse", "自己拿主意"],
	]
	for case in cases:
		game = await fresh()
		game.jump_to("investigate")
		game.engine.Amplify(case[1], "S4")
		# 请求回忆这一路单独检验：恶魔那句话这里只放大欲望，不让他先自己走过去（那一路见 5c）。
		game.engine.Amplify(case[1], "nudge")
		game.engine.SetProgress("nudged", case[0], true)
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
		var hesitated: bool = "\n".join(seen_lines).contains("你说呢")
		var thought := str(game.engine.Progress("recall_decision").get("thought", ""))
		var ok: bool = (touched == (case[4] == "touch")) and int(game.engine.Hooks(case[1])) == (1 if touched else 0) and thought.contains(case[5])
		var advice_text: String = ["劝碰", "劝别碰", "由他自己"][case[3]] if case[3] >= 0 else ""
		check(ok, "%s%s：%s%s → %s（%s）" % [Content.PERSON_NAMES[case[1]], "（已被告知守则第 1 条）" if case[2] else "（未被告知）",
			"犹豫并反问“你说呢？”，侦探" + advice_text + " " if case[3] >= 0 else "", "", case[4], thought])
		if case[1] == "shen" and not case[2]:
			check(hesitated, "沈鼎未被告知时先犹豫，反问侦探“你说呢？”")
		elif case[3] < 0:
			check(not hesitated, "%s不犹豫，直接自己决定" % Content.PERSON_NAMES[case[1]])
	game = await fresh()
	game.jump_to("investigate")
	seen_lines.clear()
	await open_card_settled(game, "E1")
	check(game.modal_kind == "card" and "\n".join(seen_lines).contains("它记得你昨夜是什么"), "第一次打开插闩的证物卡：先听到恶魔朝向侦探欲望的那句话，然后才出现回忆选项（修订三·3）")
	var labels := []
	for b in game.modal_root.find_children("*", "Button", true, false):
		labels.append(str(b.text))
	check(labels.any(func(t): return str(t).contains("守则第 1 条")), "回忆选项标注“（守则第 1 条）”（修订三·2、第五轮修订 2）")
	game.close_modal()
	seen_lines.clear()
	await open_card_settled(game, "E1")
	check(not "\n".join(seen_lines).contains("门闩记得"), "同一件物件，恶魔那句话只说一次")
	game.close_modal()
	var nudges := {}
	for id in ["E1", "E5", "E7", "E10"]:
		nudges[id] = story.entries.has("nudge_" + id)
	var nudge_text: String = JSON.stringify(story.entries)
	check(nudge_text.contains("门闩记得你昨夜是什么") and nudge_text.contains("后面还有你从未听过的") and nudge_text.contains("图纸就在主楼") and nudge_text.contains("那罐子都替你记着"),
		"四件物件四句不同的话，各自朝向此人的欲望")
	var endings := []
	for id in ["E1", "E5", "E7", "E10"]:
		var lines: Array = story.entries["nudge_" + id].filter(func(step): return step.op == "line" and step.speaker == "铜口")
		check(lines.size() == 1 and nudges[id], "诱导标题与铜口台词存在：" + id)
		endings.append(str(lines[0].text).right(6))
	var distinct_endings := {}
	for e in endings:
		distinct_endings[e] = true
	check(distinct_endings.size() == 4 and not endings.has("不妨一试。"), "四句诱导不以同一句收尾（修订四·6）%s" % str(endings))
	game = await fresh()
	game.jump_to("investigate")
	game.engine.Amplify("huo", "S4")
	game.engine.SetProgress("nudged", "E7", true)
	await run_recall(game, "E7", [1])
	check(game.recall_marks.get(2, "") == "huo" and int(game.replay_vars[1]) == -1, "回忆把 V2 标为“回忆所证”，但不替玩家设定变量")
	game.close_modal()

	# ---- 5c. 恶魔低语后，同伴自己走向旧物（修订四·1）：在场时三种回应
	# [人, 物件, 房间中心, 侦探的回应（0 劝阻，1 说明守则第 1 条，2 任其自便）, 期望]
	var rooms := {"huo": Vector2(440, 340), "su": Vector2(224, 390), "shen": Vector2(448, 652)}
	var expect := {"huo": ["touch", "touch", "touch"], "su": ["touch", "refuse", "touch"], "shen": ["refuse", "refuse", "touch"]}
	var reach_lines := {"huo": "拿起了那块石板", "su": "拾起了那把音叉", "shen": "端起酵种罐"}
	for who in ["huo", "su", "shen"]:
		for answer in 3:
			game = await fresh()
			game.jump_to("investigate")
			game.engine.Amplify(who, "S4")
			var obj: String = Content.OWN_OBJECTS[who]
			await stand_near(game, rooms[who], game.site_point(obj), 90.0)
			for i in 40:
				await physics_frame
			var still: bool = not game.life.has(who)
			seen_lines.clear()
			game.engine.Whisper(who, "（低语）")
			game.engine.SetProgress("nudged", Content.OWN_OBJECTS[who], true)
			await game.present_dialogue_command("consider", [who])
			# 沈鼎任其自便时会犹豫、反问“你说呢？”——这里答“碰吧”。
			await run_reach(game, who, [answer, 0])
			var touched: bool = int(game.engine.Hooks(who)) == 1
			var text := "\n".join(seen_lines)
			var ok: bool = still and text.contains(reach_lines[who]) and touched == (expect[who][answer] == "touch") \
				and bool(game.engine.IsDisturbed(obj)) and game.decided.has(who)
			check(ok, "%s：低语之前不动；低语后自己走过去伸手；侦探%s → %s（钩 %d，%s）" % [Content.PERSON_NAMES[who], ["劝阻", "说明守则第 1 条", "任其自便"][answer],
				expect[who][answer], int(game.engine.Hooks(who)), str(game.engine.Progress("recall_decision").get("thought", ""))])
			if who == "shen" and answer == 2:
				check(text.contains("你说呢"), "沈鼎被任其自便时犹豫，反问侦探“你说呢？”")
	# 不在场：只留下痕迹与事后的神色，回来时说一句
	for who in ["huo", "su", "shen"]:
		game = await fresh()
		game.jump_to("investigate")
		game.engine.Amplify(who, "S4")
		game.player.position = Vector2(930, 200)
		for i in 4:
			await physics_frame
		game.engine.Whisper(who, "（低语）")
		game.engine.SetProgress("nudged", Content.OWN_OBJECTS[who], true)
		await game.present_dialogue_command("consider", [who])
		await wait_until(func(): return str(game.life.get(who, {}).get("state", "")) == "done" and not game.life[who].get("walking_back", false), 1500)
		var obj: String = Content.OWN_OBJECTS[who]
		var touched: bool = int(game.engine.Hooks(who)) == 1
		var expected_touch: bool = who != "shen"
		check(touched == expected_touch and bool(game.engine.IsDisturbed(obj)) and not game.recalls.has(who) and bool(game.life[who].alone),
			"%s：侦探不在场，自己%s（钩 %d）；侦探什么也没听见，物件上留下“被动过”的痕迹" % [Content.PERSON_NAMES[who], "碰了" if expected_touch else "放下了", int(game.engine.Hooks(who))])
		await wait_until(func(): return not game.actor_of(who).travelling, 600)
		game.player.position = game.actor_of(who).position + Vector2(26, 0)
		seen_lines.clear()
		game.talk(who)
		await drain(game)
		var said := "\n".join(seen_lines)
		if expected_touch:
			check(said.contains("我想起了一点") and game.recalls.has(who), "%s回来后说“我想起了一点”，片段这时才到侦探手里" % Content.PERSON_NAMES[who])
		else:
			check(said.contains("又放回去了"), "沈鼎回来后说他把罐子端起来、又放回去了")
		await open_card_settled(game, obj)
		var notes := ""
		for l in game.modal_root.find_children("*", "RichTextLabel", true, false):
			notes += str(l.get_parsed_text())
		check(notes.contains("被人拿起过"), "%s的证物卡上记着它被人拿起过" % Content.PERSON_NAMES[who])
		game.close_modal()
	# 恶魔挑侦探不在的时候低语（玩家听不见）：调查过四件后，下一次换房间时，不在场的同伴会被低语
	game = await fresh()
	game.jump_to("investigate")
	for id in ["E1", "E2", "E3", "E4"]:
		game.examine(id)
	await drain(game)
	await wait_until(func(): return game.life.has("huo"), 300)
	check(game.life.has("huo") and bool(game.engine.Whispered("huo")) and game.nudged.has("E7") and not game.life.has("su"),
		"侦探在门厅：恶魔对不在场的霍铎低语，他自己走向石板（在场的苏泠没有被低语）")
	await wait_until(func(): return str(game.life.get("huo", {}).get("state", "")) == "done" and not game.actor_of("huo").travelling, 1500)
	check(int(game.engine.Hooks("huo")) == 1 and not game.recalls.has("huo"), "玩家不作为也有后果：霍铎独自碰了石板，留下一枚钩，侦探还不知道")
	game.player.position = Vector2(440, 340)
	await wait_until(func(): return game.aside.visible, 60)
	check(game.aside.visible and game.aside.text.contains("恍惚"), "走进活动厅：看见霍铎事后的神色")

	# ---- 5d. 第六个问题起初不写出来（修订四·2）
	game = await fresh()
	game.jump_to("investigate")
	game.engine.SetProgress("state", "replay_open", true)
	game.open_manuscript()
	await process_frame
	var page := ""
	for l in game.modal_root.find_children("*", "RichTextLabel", true, false):
		page += str(l.get_parsed_text())
	check(not page.contains("昨夜之前") and not game.statements().has("V6_0"), "重演手稿起初只有五个问题，“素不相识”是不写出来的默认")
	game.close_modal()
	seen_lines.clear()
	game.examine("E9")
	await drain(game)
	check(not game.engine.V6Open(), "细看一件相熟旁证，第六个问题还不出现")
	game.examine("E12")
	await drain(game)
	game.open_manuscript()
	await process_frame
	page = ""
	for l in game.modal_root.find_children("*", "RichTextLabel", true, false):
		page += str(l.get_parsed_text())
	check(game.engine.V6Open() and page.contains("昨夜之前，四人彼此") and "\n".join(seen_lines).contains("我们真的是昨天才认识的吗"),
		"细看两件相熟旁证（杯子、放大镜）后，侦探起疑，手稿出现第六个问题")
	game.close_modal()
	game = await fresh()
	game.jump_to("reversal_one")
	check(not game.engine.V6Open(), "另一条路：起初没有第六个问题")
	var first: Dictionary = game.run_replay([0, 0, 0, 0, 0, 0])
	seen_lines.clear()
	game.after_replay(first)
	for round in 20:
		await drain(game)
		if game.modal_kind == "replay":
			break
		await process_frame
	check(game.engine.V6Open() and "\n".join(seen_lines).contains("真是昨天才认识的吗"), "重演中侦探因信任不足拒绝转述后，手稿出现第六个问题")
	game.close_modal()

	# ---- 5e. 交互一致性与减少重复（修订四·7、8、9）
	game = await fresh()
	game.jump_to("investigate")
	check(game.player.collision_mask == 1 and game.actor_of("su").collision_mask == 1, "人与人不互相阻挡（只与墙和家具碰撞）")
	game.player.position = game.site_point("E4") + Vector2(20, -20)
	game._approach_site("E4")
	check(game.modal_kind == "card", "站在够得着的地方点物件：直接打开，不绕到别处")
	game.close_modal()
	seen_lines.clear()
	game._on_help("E4")
	for round in 60:
		await drain(game)
		if game.modal_kind == "card" and not game.dialogue.active:
			break
		await process_frame
	var brief := ""
	for l in game.modal_root.find_children("*", "RichTextLabel", true, false):
		brief += str(l.get_parsed_text())
	check(brief.contains("这布是我拧的") and not brief.contains("一条拧过的湿抹布"), "请人看过之后只给新得到的那一句，不重显整张原文")
	game.player.position = game.site_point("L6") + Vector2(0, 16)
	for i in 2:
		await process_frame
	var l6_visible: bool = game.site_visible("L6")
	game.player.position = game.site_point("L7") + Vector2(0, 16)
	for i in 2:
		await process_frame
	var l7_visible: bool = game.site_visible("L7")
	check(l6_visible and l7_visible, "R-06、R-07 各有一处本人的生活痕迹")
	game.interact("L6")
	var hook := ""
	for l in game.modal_root.find_children("*", "RichTextLabel", true, false):
		hook += str(l.get_parsed_text())
	check(hook.contains("机油") and hook.contains("挂很多回"), "R-06 床头挂钩上的油渍：相熟与久居的旁证，不编号")
	game.close_modal()
	game.interact("L7")
	var stone := ""
	for l in game.modal_root.find_children("*", "RichTextLabel", true, false):
		stone += str(l.get_parsed_text())
	check(stone.contains("磨刀石") and stone.contains("深槽"), "R-07 桌上磨得凹陷的磨刀石")
	game.close_modal()
	game._furniture_aside(Vector2(704, 50))
	check(game.aside.visible and game.aside.text.contains("窗"), "点到窗：也给一句心声")

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
	game.engine.SetProgress("state", "full_match_seen", false)
	game.play_replay(game.replay_result)
	await process_frame
	await process_frame
	var skip_key := InputEventKey.new()
	skip_key.keycode = KEY_ESCAPE
	skip_key.physical_keycode = KEY_ESCAPE
	skip_key.pressed = true
	game.stage_node._unhandled_input(skip_key)
	for round in 80:
		# 全部吻合后三位同伴先走过来，再说第二次反转。
		await drain(game)
		if game.modal_kind == "replay":
			break
		await process_frame
	check(await wait_until(func(): return game.modal_kind == "replay"), "跳过回放后回到手稿")
	check(game.full_match_seen, "跳过与看完结算相同：全部吻合触发 S9（第二次反转）")
	game.close_modal()

	# ---- 7. 始业之答（陈述与出示成对；第五轮修订删去反问）→ 门开
	game = await fresh()
	game.jump_to("replay_match")
	game.engine.ApplyRecall("E10", "shen")
	seen_lines.clear()
	game.answer()
	var picks := [{"say": "me", "show": "E9"}, {"say": "V4_0", "show": "E3"}, {"say": "me", "show": "E3"}, {"say": "V3_0", "show": "E8"},
		{"say": "V4_0", "show": "E7"}, {"say": "trust", "show": "E10"}]
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
	check(not saw_reverse and not game.flags.has("asked_reverse"), "答辩到第 4 步为止，不再出现“反问”（第五轮修订 3）")
	check(game.flags.has("door_open") and game.ended, "门开：S11 结尾")
	check(game.get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, "门开：隔离门画面换为开启")
	var finale := "\n".join(seen_lines)
	check(game.get_node("ObjectsAndPeople/MechanicalIsolationDoor/DemonPrints").visible and finale.contains("鞋底窄") and finale.contains("起居厅")
		and not game.get_node("ObjectsAndPeople/MechanicalIsolationDoor/LoungeCover").visible,
		"门开：门外是起居厅，烟灰上留下一行窄底皮鞋印（恶魔显形才能抬闩，修订三·8、第五轮修订 6）")
	check(game._room_name(game.player.position) == "门厅", "结局时地名显示“门厅”")
	check(game.flags.has("asked_hand") and finale.contains("诸位授权封存之物，是我取走的") and not finale.contains("第二条"),
		"细看过窄手印、又见门外窄底鞋印：可问“架上的手印，是你的吗？”，恶魔承认取物，不提“第二条”（修订四·5、第五轮修订 5）")
	check(finale.contains("比我以为的站得久"), "门开时侦探有一句克制的心声（修订四·10）")
	check(finale.contains("那罐子喂过的日子，我想起来一些了") and finale.contains("我想先用自己的耳朵听这里"), "沈鼎（被钩）与苏泠（未被钩）的结尾台词按是否被钩变化（修订三·4）")
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
	key.physical_keycode = KEY_SPACE
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
	call_deferred("_finish", 0 if failures == 0 else 1)
