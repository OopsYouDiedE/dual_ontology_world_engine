extends SceneTree
## 流程跳转回归：直接把局面摆到关键状态（醒来、门厅反转、重演全部吻合、答辩第 5 步、门开、退出考题），
## 检查可达与无脚本错误。对白由脚本代为推进；这不是游玩验证，不替代独立盲玩。
##   godot --headless --path godot_csharp --script res://scenes/Awakening/check/flow_check.gd

const SCENE := "res://scenes/Main2D.tscn"
var checks := 0
var failures := 0
var holder: Node

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

## 代为推进对白：揭示整行、推进；遇到选项按 choices 依次选择（默认第一个）。
func drain(game: Node, choices: Array = [], limit := 900) -> void:
	var guard := 0
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
		if d.body.visible_characters >= 0:
			d.reveal()
			continue
		var allowed: Array = d.line.responses.filter(func(r): return r.is_allowed)
		if allowed.is_empty():
			d.advance()
		else:
			var index: int = choices.pop_front() if not choices.is_empty() else 0
			d.choose(str(allowed[index].next_id))

func wait_until(condition: Callable, limit := 600) -> bool:
	for i in limit:
		if condition.call():
			return true
		await process_frame
	return condition.call()

func _run() -> void:
	# ---- 0. 对白标题齐全
	var story: Resource = load("res://scenes/Awakening/awakening.dialogue")
	var titles: Array = story.titles.keys()
	var needed := ["s1_wake", "s2_meet", "s2_tea", "s3_rewards", "s4_question", "s5_begin", "replay_unlock", "exp_E1", "exp_E2", "exp_E3",
		"s6_reversal", "help_E4", "help_E5", "help_E6", "help_E7", "help_E9", "help_E10", "help_E11", "nudge_E1", "nudge_E5", "nudge_E7", "nudge_E10",
		"recall_ask_su", "recall_su", "recall_ask_huo", "recall_huo", "recall_ask_shen", "recall_shen", "recall_ask_detective", "recall_detective",
		"sound_test", "talk_huo", "talk_shen", "talk_su", "s8_reversal", "rest_confirm", "answer_open", "answer_q1", "answer_ok1", "answer_q2",
		"answer_ok2", "answer_need_test", "answer_q3", "answer_ok3", "answer_q4", "answer_ok4", "answer_half4_trust", "answer_half4_shown",
		"answer_wrong_show", "answer_wrong_say", "answer_pause", "answer_offer_reverse", "answer_reverse", "answer_reverse_wrong",
		"s10_steps", "s10_open", "ending_rest"]
	var missing := needed.filter(func(t): return not titles.has(t))
	check(missing.is_empty(), "代码引用的对白标题全部存在 %s" % str(missing))

	# ---- 1. 醒来 → 相遇 → 始业之问 → 调查（完整开场，对白代为推进）
	var game := await fresh(false)
	check(await wait_until(func(): return game.dialogue.active), "开场：S1 醒来对白出现")
	check(game.player.seated and game._room_name(game.player.position) == "R-05", "醒来：侦探在 R-05 床上")
	await drain(game)
	check(game.stage == "roam", "醒来后可以自由行动")
	check(game.site_visible("E12") and game.site_visible("E11a") and not game.site_visible("E1"), "醒来时只看得到 R-05 里的东西")
	game.player.stand_up()
	game.player.position = Vector2(300, 600)
	check(await wait_until(func(): return game.stage == "meeting"), "走进活动厅触发 S2 相遇")
	for round in 12:
		await drain(game, [0])
		if game.stage == "investigate" and not game.dialogue.active:
			break
		await process_frame
	check(game.stage == "investigate", "S2–S4 结束后进入调查")
	check(game.detective_name == "试验", "姓名由玩家输入")
	check(game.can_access_dorm("musician"), "调查阶段可进入其他宿舍")

	# ---- 2. 门厅反转
	game = await fresh()
	game.jump_to("investigate")
	game.open_card("E1")
	check(game.modal_kind == "card" and game.examined.has("E1"), "检视隔离门：打开证物卡")
	game.close_modal()
	await drain(game)
	check(game.replay_open, "第一次看到闩上的门后，重演开放")
	game.open_card("E3")
	game._on_experiment("E3")
	await drain(game)
	check(game.flags.has("reversal_one") and game.experiments.has("E3"), "门厅反转：对照靴底后触发“……是我”")
	check(game.statements().has("me"), "陈述列表出现“是我闩的门”")

	# ---- 3. 声路试验与触物回忆
	game.last_sound = game.engine.SoundTest(0, {"detective": "foyer", "huo": "hall", "shen": "pantry", "su": "R05"})
	check(game.heard("detective").begins_with("……开门") and game.heard("detective").contains("门外"), "声路试验：门边只听到“……开门”，像在门外")
	check(game.heard("huo") == "闩上门，不要开门。", "声路试验：铜口旁听到整句")
	game.close_modal()
	game._on_recall("E5")
	await drain(game, [0])
	check(game.recalls.has("su") and int(game.engine.Hooks("su")) == 1 and int(game.engine.Hooks("huo")) == 0, "触物回忆：苏泠记一枚钩，他人不变")
	check(game.recall_marks.get(1, "") == "su", "回忆把 V1 标为“回忆所证”")
	check(int(game.replay_vars[0]) == -1, "回忆不替玩家设定变量")
	game.close_modal()

	# ---- 4. 重演：素不相识时的拒绝，与全部吻合
	var refused: Dictionary = game.run_replay([0, 0, 0, 0, 0, 0])
	var doubt := false
	for frame in refused.frames:
		if str(frame.thoughts.detective).contains("我凭什么信一个昨天才认识的人？"):
			doubt = true
	check(doubt and not bool(refused.full_match), "V5=霍铎、V6=素不相识：回放里侦探的心声是“我凭什么信一个昨天才认识的人？”")
	var unvisited: Array = refused.findings.filter(func(f): return f.status == "unvisited")
	check(not unvisited.is_empty() and str(unvisited[0].text).begins_with("你还没查过这里"), "未检视之处只给地点")
	game = await fresh()
	game.jump_to("replay_match")
	check(bool(game.replay_result.full_match), "全部证物检视后，唯一正确组合全部吻合")
	check(game.replay_result.findings.all(func(f): return f.status == "match"), "对照结果全部为“吻合”")
	var moment: Dictionary = {}
	for frame in game.replay_result.frames:
		if str(frame.label).contains("铜口说话"):
			moment = frame
	var distinct := {}
	for key in moment.thoughts:
		distinct[str(moment.thoughts[key])] = true
	check(distinct.size() == 4, "回放：铜口说话的同一刻，四个心声气泡各不相同")
	game.full_match_seen = false
	game.play_replay(game.replay_result)
	await process_frame
	await process_frame
	game.stage_node.skipping = true
	await drain(game)
	check(await wait_until(func(): return game.modal_kind == "replay"), "跳过回放后回到手稿")
	check(game.full_match_seen, "跳过与看完结算相同：全部吻合触发 S8")
	game.close_modal()

	# ---- 5. 始业之答（含第 5 步反问）→ 门开
	game = await fresh()
	game.jump_to("replay_match")
	game.answer()
	var picks := [{"kind": "show", "id": "E9"}, {"kind": "say", "id": "me"}, {"kind": "show", "id": "E8"}, {"kind": "show", "id": "E7"},
		{"kind": "say", "id": "V5_0"}, {"kind": "show", "id": "E10"}, {"kind": "show", "id": "E11"}]
	var saw_reverse := false
	var saw_wrong := false
	for step in 1500:
		await process_frame
		if game.ended:
			break
		var d = game.dialogue
		if d.active and d.line != null and not d.busy:
			for r in d.line.responses:
				if str(r.text).begins_with("反问"):
					saw_reverse = true
			if str(d.line.text).contains("说明了别的事") or str(d.line.text).contains("杯子说明"):
				saw_wrong = true
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
	check(saw_wrong, "无关证物得到指向性回应，不扣分、不重来")
	check(saw_reverse, "答辩第 5 步：重演全部吻合后出现“反问”选项")
	check(game.flags.has("asked_reverse"), "反问出示无尘方框：恶魔承认“我从未说过诸位昨日方至”")
	check(game.flags.has("door_open") and game.ended, "门开：S10 结尾")
	check(game.get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, "门开：隔离门画面换为开启")
	check(FileAccess.file_exists("user://chapter1_end.json"), "章节结束状态写入 user://chapter1_end.json")

	# ---- 6. 退出考题
	game = await fresh()
	game.jump_to("investigate")
	game._confirm_rest()
	await drain(game, [0])
	check(game.ended and game.flags.has("rest_chosen"), "退出考题：不作答，在生活翼歇息，以简短收尾结束")

	print("TOTAL %d checks, %d failed" % [checks, failures])
	quit(0 if failures == 0 else 1)
