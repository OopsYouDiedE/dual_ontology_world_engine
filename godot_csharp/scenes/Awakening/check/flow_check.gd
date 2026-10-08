extends SceneTree
## 流程跳转回归（统一宿主 Chapter1.tscn）：直接把局面摆到关键状态（醒来、开场广播、学园守则、门厅反转、同伴触物回忆两种分支、
## 重演全部吻合、始业之答、门开、退出考题），检查可达与无脚本错误。地图为第五轮修订后的乙区（中廊两侧各三间房）。
## 对白与表单由脚本代为推进；这不是游玩验证，不替代独立盲玩。失败不中止，末尾汇总全部失败项。
##   godot --headless --path godot_csharp --script res://scenes/Awakening/check/flow_check.gd

const Fixture := preload("res://scenes/Awakening/check/chapter1_fixture.gd")
const FieldNavigation := preload("res://scenes/Shared/FieldNavigation.gd")
## 恶魔在全章都不提契约第 9、10 条。
const FORBIDDEN := ["第九", "第 9", "第9", "第十条", "第 10", "九、", "十、", "尽忘", "归乡", "放逐", "十五名", "担保耗尽"]
## 第五轮修订删去的说法：初涉斯地、可数的刻痕、恶魔自报为恶魔或提导师与契约、门外是“主楼走廊”。
const RETIRED := ["初涉斯地", "二十四", "刻痕", "恶魔亦可", "导师将我拘于", "契约第", "主楼走廊", "依第二条", "第 8 条", "第八条"]
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
	host._on_ui_event({"kind": "replay", "vars": vars, "skip": skip})
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
	var runtime_content: String = str(fx.probe("content"))
	var raw: String = JSON.stringify(story.entries) + runtime_content
	var hits := FORBIDDEN.filter(func(w): return raw.contains(w))
	check(hits.is_empty(), "台词与证物文本中没有契约第 9、10 条的字样 %s" % str(hits))
	var retired_raw := raw + source_text("res://scenes/Awakening/AwakeningMap.gd") + source_text("res://scenes/Awakening/Script/Chapter1Session.cs") \
		+ source_text("res://scenes/Awakening/Script/Chapter1SessionFlows.cs") + source_text("res://scenes/Shared/ChapterHost.gd")
	var retired := RETIRED.filter(func(w): return retired_raw.contains(w))
	check(retired.is_empty(), "第五轮修订：不再有“初涉斯地”、可数刻痕、契约条款与“主楼走廊” %s" % str(retired))
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
		and not opening.contains("导师") and not opening.contains("契约") and not opening.contains("初涉斯地"),
		"S3 开场广播：只称代管者与教师，不说自己是什么，不提导师与契约，没有“初涉斯地”（第五轮修订 1、3）")
	check(opening.find("代管者") < opening.find("此为始业之问"), "开场广播在始业之问之前")
	check(FORBIDDEN.filter(func(w): return opening.contains(w)).is_empty(), "开场全部台词不提第 9、10 条")
	check(opening.contains("又不是来断案的") and opening.contains("是在试我们") and opening.contains("我还不知道能不能信"),
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
	check(board.size() == 4 and board_text.contains("一、封存之物，可凭对应之物寻回；寻回者，自负其果。") and board_text.contains("四、我所立之规，皆张贴于此。")
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
	check(seen().contains("簌簌地落"), "“……是我”处，侦探有一句克制的心声（修订四·10）")
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
	check(detective_sound.room == "foyer" and str(detective_sound.text).begins_with("……开门") and str(detective_sound.direction).contains("从那边")
		and not str(detective_sound.text).contains("门外") and not str(detective_sound.direction).contains("门外"),
		"声路试验：门边只听到“……开门”，同伴只说来处的方向（从那边，指向门），不说“像在门外”（修订三·5）")
	check(su_sound.room == "R08" and str(su_sound.text).begins_with("……开门") and str(su_sound.direction).contains("墙那边")
		and not str(su_sound.text).contains("门外") and not str(su_sound.direction).contains("门外"), "声路试验：R-08 只听到“……开门”，从墙那边")
	check(huo_sound.room == "hall" and str(huo_sound.text).begins_with("闩上门，不要开门。") and str(huo_sound.direction).contains("从铜口里"), "声路试验：铜口旁听到整句")
	fx.close_modal()
	fx.seen_lines.clear()
	# 声路试验在 E8 证物卡的“试验”里：表单选句选位 → 开始。
	await fx.open_card("E8")
	for round in 2:
		check(fx.select("experiment"), "声路试验第 %d 轮：E8 证物卡有试验选项" % (round + 1))
		await fx.wait_until(func(): return host.ui.modal_kind == "form")
		host.ui._form.select("sentence", "0")
		for action in host.ui._modal_event.actions:
			if str(action.id) == "start": host.ui._form_action(host.ui._modal_event, action)
		await fx.drain()
		await fx.wait_until(func(): return host.ui.modal_kind == "card")
		if round == 1:
			var title := str(host.ui._modal_event.get("title", "")) + str(host.ui._modal_event.get("intro", "")) + fx.modal_text()
			check(title.contains("第 2 轮") and title.contains("从哪边来"), "声路试验第二轮起以一张小表汇总四处所闻（修订四·8）")
	check(seen().count("从管子里出来的声音") == 1 or fx.seen_lines.filter(func(l): return l.contains("从管子里出来的声音")).size() == 1, "两轮声路试验，苏泠的点评只说一次")
	fx.close_modal()

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
		await fresh()
		await fx.jump("investigate")
		fx.probe("amplify", [case[1], "S4"])
		# 请求回忆这一路单独检验：恶魔那句话这里只放大欲望，不让他先自己走过去（那一路见 5c）。
		fx.probe("amplify", [case[1], "nudge"])
		fx.set_progress("nudged", case[0], true)
		await fx.open_card("board")
		fx.close_modal()
		var choices := [0 if case[2] else 1]
		if case[3] >= 0:
			choices.append(case[3])
		fx.seen_lines.clear()
		await fx.open_card(case[0])
		var returned: bool = await fx.card_action("recall", choices)
		if not returned: check(false, "触物回忆没有返回证物卡：" + case[0])
		var touched: bool = fx.progress("recalls").has(case[1])
		var hesitated: bool = seen().contains("你说呢")
		var thought := str(fx.progress("recall_decision").get("thought", ""))
		var ok: bool = (touched == (case[4] == "touch")) and fx.hooks(case[1]) == (1 if touched else 0) and thought.contains(case[5])
		var advice_text: String = ["劝碰", "劝别碰", "由他自己"][case[3]] if case[3] >= 0 else ""
		check(ok, "%s%s：%s%s → %s（%s）" % [fx.person_name(case[1]), "（已被告知守则第 1 条）" if case[2] else "（未被告知）",
			"犹豫并反问“你说呢？”，侦探" + advice_text + " " if case[3] >= 0 else "", "", case[4], thought])
		if case[1] == "shen" and not case[2]:
			check(hesitated, "沈鼎未被告知时先犹豫，反问侦探“你说呢？”")
		elif case[3] < 0:
			check(not hesitated, "%s不犹豫，直接自己决定" % fx.person_name(case[1]))
	await fresh()
	await fx.jump("investigate")
	fx.seen_lines.clear()
	await fx.open_card("E1")
	check(host.ui.modal_kind == "card" and seen().contains("它记得你昨夜是什么"), "第一次打开插闩的证物卡：先听到恶魔朝向侦探欲望的那句话，然后才出现回忆选项（修订三·3）")
	check(fx.modal_buttons().any(func(t): return str(t).contains("守则第 1 条")), "回忆选项标注“（守则第 1 条）”（修订三·2、第五轮修订 2）")
	fx.close_modal()
	fx.seen_lines.clear()
	await fx.open_card("E1")
	check(not seen().contains("门闩记得"), "同一件物件，恶魔那句话只说一次")
	fx.close_modal()
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
	await fresh()
	await fx.jump("investigate")
	fx.probe("amplify", ["huo", "S4"])
	fx.set_progress("nudged", "E7", true)
	await fx.run_recall("E7", [1])
	check(str(fx.progress("recall_marks").get("2", "")) == "huo" and int(fx.state("replay_vars")[1]) == -1, "回忆把 V2 标为“回忆所证”，但不替玩家设定变量")
	fx.close_modal()

	# ---- 5c. 恶魔低语后，同伴自己走向旧物（修订四·1）：在场时三种回应
	# [人, 物件, 房间中心, 侦探的回应（0 劝阻，1 说明守则第 1 条，2 任其自便）, 期望]
	var rooms := {"huo": Vector2(440, 340), "su": Vector2(224, 390), "shen": Vector2(448, 652)}
	var expect := {"huo": ["touch", "touch", "touch"], "su": ["touch", "refuse", "touch"], "shen": ["refuse", "refuse", "touch"]}
	var reach_lines := {"huo": "拿起了那块石板", "su": "拾起了那把音叉", "shen": "端起酵种罐"}
	for who in ["huo", "su", "shen"]:
		for answer in 3:
			await fresh()
			await fx.jump("investigate")
			fx.probe("amplify", [who, "S4"])
			var obj: String = fx.own_object(who)
			await fx.stand_near(rooms[who], host.field.site_point(obj), 90.0)
			await fx.physics(40)
			var still: bool = not fx.progress("life").has(who)
			fx.seen_lines.clear()
			fx.probe("whisper", [who, "（低语）"])
			fx.set_progress("nudged", obj, true)
			fx.probe("consider", [who])
			# 沈鼎任其自便时会犹豫、反问“你说呢？”——这里答“碰吧”。
			var reached: bool = await fx.run_reach(who, [answer, 0])
			var touched: bool = fx.hooks(who) == 1
			var text := seen()
			var ok: bool = reached and still and text.contains(reach_lines[who]) and touched == (expect[who][answer] == "touch") \
				and bool(fx.probe("is_disturbed", [obj])) and fx.progress("decided").has(who)
			check(ok, "%s：低语之前不动；低语后自己走过去伸手；侦探%s → %s（钩 %d，%s）" % [fx.person_name(who), ["劝阻", "说明守则第 1 条", "任其自便"][answer],
				expect[who][answer], fx.hooks(who), str(fx.progress("recall_decision").get("thought", ""))])
			if who == "shen" and answer == 2:
				check(text.contains("你说呢"), "沈鼎被任其自便时犹豫，反问侦探“你说呢？”")
	# 不在场：只留下痕迹与事后的神色，回来时说一句
	for who in ["huo", "su", "shen"]:
		await fresh()
		await fx.jump("investigate")
		fx.probe("amplify", [who, "S4"])
		fx.player().position = Vector2(930, 200)
		await fx.physics(4)
		fx.probe("whisper", [who, "（低语）"])
		var obj: String = fx.own_object(who)
		fx.set_progress("nudged", obj, true)
		fx.probe("consider", [who])
		await fx.wait_until(func(): return str(fx.life(who).get("state", "")) == "done" and not fx.life(who).get("walking_back", false), 60.0)
		var touched: bool = fx.hooks(who) == 1
		var expected_touch: bool = who != "shen"
		check(touched == expected_touch and bool(fx.probe("is_disturbed", [obj])) and not fx.progress("recalls").has(who) and bool(fx.life(who).get("alone", false)),
			"%s：侦探不在场，自己%s（钩 %d）；侦探什么也没听见，物件上留下“被动过”的痕迹" % [fx.person_name(who), "碰了" if expected_touch else "放下了", fx.hooks(who)])
		await fx.wait_until(func(): return not fx.actor(who).travelling, 30.0)
		fx.player().position = fx.actor(who).position + Vector2(26, 0)
		await fx.physics(4)
		fx.seen_lines.clear()
		host.field.interact("person:" + who)
		await fx.drain()
		var said := seen()
		if expected_touch:
			check(said.contains("我想起了一点") and fx.progress("recalls").has(who), "%s回来后说“我想起了一点”，片段这时才到侦探手里" % fx.person_name(who))
		else:
			check(said.contains("又放回去了"), "沈鼎回来后说他把罐子端起来、又放回去了")
		await fx.open_card(obj)
		check(fx.modal_text().contains("被人拿起过"), "%s的证物卡上记着它被人拿起过" % fx.person_name(who))
		fx.close_modal()
	# 恶魔挑侦探不在的时候低语（玩家听不见）：调查过四件后，下一次换房间时，不在场的同伴会被低语
	await fresh()
	await fx.jump("investigate")
	for id in ["E1", "E2", "E3", "E4"]:
		fx.probe("examine", [id])
	await fx.drain()
	await fx.wait_until(func(): return fx.progress("life").has("huo"), 10.0)
	check(fx.progress("life").has("huo") and bool(fx.probe("whispered", ["huo"])) and fx.progress("nudged").has("E7") and not fx.progress("life").has("su"),
		"侦探在门厅：恶魔对不在场的霍铎低语，他自己走向石板（在场的苏泠没有被低语）")
	await fx.wait_until(func(): return str(fx.life("huo").get("state", "")) == "done" and not fx.actor("huo").travelling, 60.0)
	check(fx.hooks("huo") == 1 and not fx.progress("recalls").has("huo"), "玩家不作为也有后果：霍铎独自碰了石板，留下一枚钩，侦探还不知道")
	fx.player().position = Vector2(440, 340)
	await fx.wait_until(func(): return fx.aside().visible, 2.0)
	check(fx.aside().visible and fx.aside().text.contains("恍惚"), "走进活动厅：看见霍铎事后的神色")

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
	check(fx.probe("v6_open") and page.contains("昨夜之前，四人彼此") and seen().contains("我们真的是昨天才认识的吗"),
		"细看两件相熟旁证（杯子、放大镜）后，侦探起疑，手稿出现第六个问题")
	fx.close_modal()
	await fresh()
	await fx.jump("reversal_one")
	check(not fx.probe("v6_open"), "另一条路：起初没有第六个问题")
	fx.seen_lines.clear()
	await run_replay([0, 0, 0, 0, 0, 0])
	check(fx.probe("v6_open") and seen().contains("真是昨天才认识的吗"), "重演中侦探因信任不足拒绝转述后，手稿出现第六个问题")
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
	fx.player().position = host.field.site_point("L6") + Vector2(0, 16)
	await fx.physics(3)
	var l6_visible: bool = host.field.site_visible("L6")
	fx.player().position = host.field.site_point("L7") + Vector2(0, 16)
	await fx.physics(3)
	var l7_visible: bool = host.field.site_visible("L7")
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
	var hits_at: Dictionary = fx.map().furniture_hits(Vector2(704, 50))
	host.dispatch({"kind": "furniture", "map": fx.map().map_key(), "nodes": hits_at.get("nodes", []), "texts": hits_at.get("texts", {})})
	await fx.frames(3)
	check(fx.aside().visible and fx.aside().text.contains("窗"), "点到窗：也给一句心声")

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
	host._on_ui_event({"kind": "replay", "vars": [0, 0, 0, 0, 0, 1], "skip": false})
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
	check(fx.progress("flags").has("asked_hand") and finale.contains("诸位授权封存之物，是我取走的") and not finale.contains("第二条"),
		"细看过窄手印、又见门外窄底鞋印：可问“架上的手印，是你的吗？”，恶魔承认取物，不提“第二条”（修订四·5、第五轮修订 5）")
	check(finale.contains("我却没有马上迈出去"), "门开时侦探有一句克制的心声（修订四·10）")
	check(finale.contains("那罐子喂过的日子，我想起来一些了") and finale.contains("我想先用自己的耳朵听这里"), "沈鼎（被钩）与苏泠（未被钩）的结尾台词按是否被钩变化（修订三·4）")
	var ending_text := fx.modal_text() + "\n" + "\n".join(host.ui._modal_event.get("rows", []))
	check(ending_text.contains("留在恶魔那里的钩：沈鼎") and ending_text.contains("你没有交出的：你自己、霍铎、苏泠"), "结局列出“钩”与“你没有交出的”（修订三·4）")
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

	# ---- 8. 退出考题
	await fresh()
	await fx.jump("investigate")
	host.dispatch({"kind": "interact", "site": "E8"})
	await fx.wait_until(func(): return host.ui.modal_kind == "menu", 5.0)
	fx.select("rest")
	await fx.drain([0])
	await fx.wait_until(func(): return fx.ended(), 10.0)
	check(fx.ended() and fx.progress("flags").has("rest_chosen"), "退出考题：不作答，在生活翼歇息，以简短收尾结束")

	print("TOTAL %d checks, %d failed" % [checks, failures])
	for name in failed_names: print("FAILED: " + name)
	call_deferred("_finish", 0 if failures == 0 else 1)
