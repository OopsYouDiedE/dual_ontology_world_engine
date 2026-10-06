extends SceneTree
## 开发自检截图（有窗口）：跳到关键状态，存 PNG 到 artifacts/remake-belief-replay/gallery/。
## 这是开发自检，不是盲玩。
##   godot --path godot_csharp --script res://scenes/Awakening/check/state_gallery.gd

const SCENE := "res://scenes/Main2D.tscn"
var holder: Node
var out_dir := ""
var shots := 0

func _initialize() -> void:
	out_dir = ProjectSettings.globalize_path("res://").path_join("../artifacts/remake-belief-replay/gallery").simplify_path()
	DirAccess.make_dir_recursive_absolute(out_dir)
	call_deferred("_run")

func _finish() -> void:
	# Release _run's replay/dialogue locals before deleting the fixture and
	# its C# engine; queued UI deletion must complete before SceneTree exits.
	if is_instance_valid(holder):
		holder.queue_free()
	holder = null
	await process_frame
	await process_frame
	quit(0)

func fresh(skip_intro := true) -> Node:
	if is_instance_valid(holder):
		holder.queue_free()
		await process_frame
	holder = (load(SCENE) as PackedScene).instantiate()
	var game: Node = holder.get_node("Awakening")
	if skip_intro:
		game.set_meta("skip_intro", true)
	root.add_child(holder)
	for i in 4:
		await process_frame
	return game

func settle(frames := 20) -> void:
	for i in frames:
		await process_frame

func shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var path := out_dir.path_join(name + ".png")
	root.get_texture().get_image().save_png(path)
	shots += 1
	print("shot " + path)

## 推进对白直到某一行（行文本包含 needle）完整显示；choices 依次回答遇到的选项。
func show_line(game: Node, needle: String, choices: Array = [], limit := 1500) -> bool:
	for i in limit:
		await process_frame
		var d = game.dialogue
		if game.modal_kind == "name":
			game._name_field.text = "林"
			game._confirm_name()
			continue
		if not d.active or d.busy or d.line == null:
			continue
		if d.body.visible_characters >= 0:
			d.reveal()
			continue
		if str(d.line.text).contains(needle):
			return true
		if d.line.responses.is_empty():
			d.advance()
		else:
			d.choose(str(d.line.responses[choices.pop_front() if not choices.is_empty() else 0].next_id))
	return false

## 推进对白直到某种弹窗出现（如 S3 广播后自动呈现的公告框）。
func until_modal(game: Node, kind: String, limit := 1500) -> bool:
	for i in limit:
		await process_frame
		if game.modal_kind == kind:
			return true
		var d = game.dialogue
		if not d.active or d.busy or d.line == null:
			continue
		if d.body.visible_characters >= 0:
			d.reveal()
		elif d.line.responses.is_empty():
			d.advance()
		else:
			d.choose(str(d.line.responses[0].next_id))
	return false

## 第一次打开可回忆的物件：先截恶魔那句“不妨一试”，再推进到证物卡出现。
func open_with_nudge(game: Node, id: String, needle: String, name: String) -> void:
	game.open_card(id)
	await show_line(game, needle)
	await settle(10)
	await shot(name)
	await finish_dialogue(game)
	await until_modal(game, "card", 120)
	game.close_modal()

func finish_dialogue(game: Node, choices: Array = []) -> void:
	for i in 1500:
		await process_frame
		var d = game.dialogue
		if game.modal_kind == "name":
			game._name_field.text = "林"
			game._confirm_name()
			continue
		if not d.active:
			await settle(8)
			if not game.dialogue.active and game.modal_kind != "name":
				return
			continue
		if d.busy or d.line == null:
			continue
		if d.body.visible_characters >= 0:
			d.reveal()
		elif d.line.responses.is_empty():
			d.advance()
		else:
			d.choose(str(d.line.responses[choices.pop_front() if not choices.is_empty() else 0].next_id))

func _run() -> void:
	# 1 醒来
	var game := await fresh(false)
	await show_line(game, "我只记得上船")
	await settle(10)
	await shot("01-wake")
	# 2 开场广播（代管者兼教师）与欲望放大
	await finish_dialogue(game)
	game.player.stand_up()
	game.player.position = Vector2(440, 340)
	await show_line(game, "我所立之规")
	await settle(10)
	await shot("02-broadcast-self-introduction")
	await until_modal(game, "card")
	await settle(20)
	await shot("02b-board-presented-after-broadcast")
	game.close_modal()
	await show_line(game, "超越卡诺循环的那一张")
	await settle(10)
	await shot("03-desire-amplified")
	await show_line(game, "考人，不是问路")
	await settle(10)
	await shot("03b-reactions-after-the-question")
	# 3 公告框
	game = await fresh()
	game.jump_to("investigate")
	game.player.position = Vector2(400, 140)
	await settle(10)
	game.open_board()
	await settle(10)
	await shot("04-notice-board-school-rules")
	game.close_modal()
	# 4 门厅现场、证物卡、比对鞋底由玩家选人、门厅反转
	game.player.position = Vector2(206, 392)
	game.player.pose("idle", "left")
	await finish_dialogue(game)
	await settle(30)
	await shot("05-foyer")
	game.open_card("E3")
	await settle(10)
	await shot("06-card-boot-print")
	game.close_modal()
	game.examine("E1")
	await finish_dialogue(game)
	game._on_experiment("E3")
	await until_modal(game, "menu", 120)
	await settle(10)
	await shot("06b-compare-soles-choose-whose")
	game._modal_choose("detective")
	await show_line(game, "……是我")
	await settle(10)
	await shot("07-reversal-one")
	await finish_dialogue(game)
	game.close_modal()
	# 5 恶魔低语后，苏泠自己蹲向音叉；侦探在场，指出守则第 1 条，她放下
	game.engine.Amplify("su", "S4")
	game.player.position = Vector2(230, 400)
	await settle(5)
	game.open_card("E5")
	await show_line(game, "后面还有你从未听过的")
	await settle(10)
	await shot("08a-whisper-su")
	await show_line(game, "拾起了那把音叉")
	await settle(20)
	await show_line(game, "它说的那句话")
	await settle(10)
	await shot("08b-su-reaches-for-fork-three-responses")
	await show_line(game, "守则第一条写着", [1])
	await settle(10)
	await shot("08-su-refuses-after-rule-1")
	await finish_dialogue(game)
	# 5a 侦探不在场：霍铎独自碰了石板；回来时的神色与“我想起了一点”
	game.engine.Amplify("huo", "S4")
	game.player.position = Vector2(930, 200)
	await settle(10)
	game.engine.Whisper("huo")
	game.engine.SetProgress("nudged", "E7", true)
	await game.present_dialogue_command("consider", ["huo"])
	await finish_dialogue(game)
	for i in 1500:
		await process_frame
		if str(game.life.get("huo", {}).get("state", "")) == "done" and not game.actor_of("huo").travelling:
			break
	game.player.position = game.actor_of("huo").position + Vector2(60, 40)
	# 走进活动厅时看见他事后的神色（旁白只停 2.6 秒，有窗口截图时帧慢，少等几帧）。
	for i in 120:
		await process_frame
		if game.aside.visible:
			break
	await settle(2)
	await shot("09-huo-after-touching-alone")
	game.talk("huo")
	await show_line(game, "我想起了一点")
	await settle(10)
	await shot("09a-huo-i-remember-a-little")
	await finish_dialogue(game)
	# 5b 请沈鼎回忆（请求这一路）：犹豫，反问“你说呢？”
	game.engine.Amplify("shen", "S4")
	game.engine.Amplify("shen", "nudge")
	game.engine.SetProgress("nudged", "E10", true)
	game.player.position = Vector2(470, 600)
	await settle(5)
	game._on_recall("E10")
	await show_line(game, "你说呢", [1])
	await settle(10)
	await shot("09b-recall-shen-asks-you-decide")
	await finish_dialogue(game, [0])
	game.close_modal()
	# 5c 声路试验：第一轮同伴只报原字与方向；第二轮起一张小表
	game.last_sound = game.engine.SoundTest(0, {"detective": "foyer", "huo": "hall", "shen": "pantry", "su": "R08"})
	game.set_meta("sound_sentence", game.engine.Sentences()[0])
	game.say("sound_test")
	await show_line(game, "墙那边")
	await settle(10)
	await shot("09d-sound-test-directions")
	await finish_dialogue(game)
	game.sound_rounds = 2
	game.last_sound = game.engine.SoundTest(1, {"detective": "pantry", "huo": "hall", "shen": "foyer", "su": "R08"})
	game.set_meta("sound_sentence", game.engine.Sentences()[1])
	game._sound_table()
	await settle(10)
	await shot("09g-sound-test-second-round-table")
	game.close_modal()
	# 5d 普通家具与窗：一句心声
	for node in game.get_node("ObjectsAndPeople").get_children():
		if str(node.name).begins_with("StudyBookcase") and node is Sprite2D:
			game.player.position = node.position + Vector2(0, 56)
			await settle(20)
			game._furniture_aside(node.position + node.offset + node.texture.get_size() * 0.5)
			break
	# 心声只停留 2.6 秒；有窗口截图时帧慢，只等两帧。
	await settle(2)
	await shot("09e-furniture-aside")
	# 5e 悬停可交互物件：只描一件，显示名称
	var center: Vector2 = game.site_box("E7").get_center()
	game.player.position = center + Vector2(0, 90)
	await settle(30)
	root.warp_mouse(game.get_global_transform_with_canvas() * center)
	await settle(4)
	await shot("09f-hover-name")
	root.warp_mouse(Vector2(4, 4))
	# 5f 请人看过之后只给新得到的那一句
	game.player.position = Vector2(196, 392)
	await settle(5)
	game._on_help("E4")
	await finish_dialogue(game)
	await until_modal(game, "card", 300)
	await settle(10)
	await shot("09j-help-brief")
	game.close_modal()
	# 5g R-06 的生活痕迹
	game.player.position = Vector2(948, 540)
	await settle(20)
	game.interact("L6")
	await settle(10)
	await shot("09i-trace-oil-hook")
	game.close_modal()
	# 5h 第六个问题起初不写出来
	game.replay_open = true
	game.open_manuscript()
	await settle(20)
	await shot("09h-manuscript-five-questions")
	game.close_modal()
	# 6 重演手稿与回放
	for id in ["E2", "E4", "E5", "E6", "E7", "E8", "E9", "E10", "E11", "E12"]:
		game.examine(id)
	game.replay_vars = [0, 0, 0, 0, 0, 0]
	var refused: Dictionary = game.run_replay(game.replay_vars)
	game.last_run_vars = game.replay_vars.duplicate()
	game.open_manuscript()
	await settle(20)
	await shot("10-manuscript-strangers")
	game.close_modal()
	await finish_dialogue(game)
	game.close_modal()
	game.stage = "playback"
	var stage = game.stage_node
	stage._setup()
	var voice: Dictionary = {}
	var doubt: Dictionary = {}
	for frame in refused.frames:
		if voice.is_empty() and str(frame.label).contains("铜口说话"):
			voice = frame
		if doubt.is_empty() and str(frame.thoughts.detective).contains("我凭什么信"):
			doubt = frame
	stage._apply(voice, {})
	await settle(20)
	assert(game.modal_kind == "", "Playback gallery must not have a foreground modal")
	await shot("11-playback-four-bubbles")
	stage._apply(doubt, voice)
	await settle(50)
	assert(game.modal_kind == "", "Playback gallery must not have a foreground modal")
	await shot("12-playback-why-trust")
	stage._teardown()
	await settle(5)
	game.replay_vars = [0, 0, 0, 0, 0, 1]
	var right: Dictionary = game.run_replay(game.replay_vars)
	game.last_run_vars = game.replay_vars.duplicate()
	stage._setup()
	var bolt: Dictionary = {}
	for frame in right.frames:
		if str(frame.thoughts.detective).contains("我更信霍铎"):
			bolt = frame
			break
	stage._apply(bolt, {})
	await settle(40)
	assert(game.modal_kind == "", "Playback gallery must not have a foreground modal")
	await shot("13-playback-decision-basis")
	stage._teardown()
	await settle(5)
	game.stage = "investigate"
	game.full_match_seen = true
	game.open_manuscript()
	await settle(20)
	await shot("14-manuscript-all-match")
	game.close_modal()
	# 6b 霍铎读到守则第 1 条也照样碰（请求这一路）
	game = await fresh()
	game.jump_to("investigate")
	game.player.position = Vector2(536, 300)
	game.engine.Amplify("huo", "S4")
	game.engine.Amplify("huo", "nudge")
	game.engine.SetProgress("nudged", "E7", true)
	game.open_board()
	game.close_modal()
	await finish_dialogue(game)
	game._on_recall("E7")
	await show_line(game, "图纸值这个价", [0])
	await settle(10)
	await shot("09c-recall-huo-touches-despite-rule-1")
	await finish_dialogue(game)
	game.close_modal()
	# 7 答辩（陈述与出示成对，到第 4 步为止）、门开
	game = await fresh()
	game.jump_to("replay_match")
	game.engine.ApplyRecall("E10", "shen")
	game.answer()
	var picks := [{"say": "me", "show": "E3"}, {"say": "V3_0", "show": "E8"}, {"say": "V4_0", "show": "E7"}, {"say": "trust", "show": "E10"}]
	var captured_pair := false
	var captured_door := false
	var captured_prints := false
	var captured_hand := false
	var captured_hand_answer := false
	for i in 3000:
		await process_frame
		var d = game.dialogue
		if game.modal_kind == "answer" and not picks.is_empty():
			if not captured_pair and picks.size() == 1:
				game._answer_select("say", "trust")
				game._answer_show_list("show")
				game._answer_select("show", "E10")
				await settle(8)
				await shot("15-answer-statement-and-evidence")
				captured_pair = true
			game._modal_choose(picks.pop_front())
			continue
		if d.active and d.line != null and not d.busy:
			if d.body.visible_characters >= 0:
				d.reveal()
				continue
			if not captured_prints and str(d.line.text).contains("鞋底窄"):
				await settle(6)
				await shot("18a-door-demon-prints")
				captured_prints = true
			if not captured_hand and str(d.line.text).contains("架上那半枚手印"):
				await settle(6)
				await shot("18b-ask-about-the-handprint")
				captured_hand = true
			if not captured_hand_answer and str(d.line.text).contains("是我取走的"):
				await settle(6)
				await shot("18c-demon-took-what-was-sealed")
				captured_hand_answer = true
			if not captured_door and str(d.line.text).contains("你听对了"):
				await settle(6)
				await shot("18-door-open")
				captured_door = true
			if d.line.responses.is_empty():
				d.advance()
			else:
				d.choose(str(d.line.responses[0].next_id))
		if game.ended:
			break
	await settle(10)
	await shot("19-ending")
	print("TOTAL %d shots" % shots)
	call_deferred("_finish")
