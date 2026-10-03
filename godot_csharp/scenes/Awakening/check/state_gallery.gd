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
	# 2 自报身份广播与欲望放大
	await finish_dialogue(game)
	game.player.stand_up()
	game.player.position = Vector2(300, 600)
	await show_line(game, "恶魔亦可")
	await settle(10)
	await shot("02-broadcast-self-introduction")
	await until_modal(game, "card")
	await settle(20)
	await shot("02b-board-presented-after-broadcast")
	game.close_modal()
	await show_line(game, "超越卡诺循环的那一张")
	await settle(10)
	await shot("03-desire-amplified")
	# 3 公告框
	game = await fresh()
	game.jump_to("investigate")
	game.player.position = Vector2(458, 590)
	await settle(10)
	game.open_board()
	await settle(10)
	await shot("04-notice-board-clauses-1-8")
	game.close_modal()
	# 4 门厅现场、证物卡、门厅反转
	game.player.position = Vector2(640, 918)
	game.player.pose("idle", "front")
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
	await show_line(game, "……是我")
	await settle(10)
	await shot("07-reversal-one")
	await finish_dialogue(game)
	game.close_modal()
	# 5 触物回忆：出示第 8 条后苏泠拒绝；霍铎接受
	game.engine.Amplify("su", "S4")
	game.engine.Amplify("huo", "S4")
	await open_with_nudge(game, "E5", "还有你从未听过的", "08a-nudge-su-before-recall")
	game._on_recall("E5")
	await show_line(game, "第八条写着", [0])
	await settle(10)
	await shot("08-recall-su-refuses-after-clause-8")
	await finish_dialogue(game)
	game.close_modal()
	game.player.position = Vector2(372, 700)
	await settle(5)
	await open_with_nudge(game, "E7", "图纸就在主楼", "08b-nudge-huo-before-recall")
	game._on_recall("E7")
	await show_line(game, "碰一下，就知道了", [1])
	await settle(10)
	await shot("09-recall-huo-touches")
	await finish_dialogue(game)
	game.close_modal()
	game.engine.Amplify("shen", "S4")
	game.player.position = Vector2(1100, 600)
	await settle(5)
	await open_with_nudge(game, "E10", "你想护住的", "08c-nudge-shen-before-recall")
	game._on_recall("E10")
	await show_line(game, "你说呢", [1])
	await settle(10)
	await shot("09b-recall-shen-asks-you-decide")
	await finish_dialogue(game, [0])
	game.close_modal()
	# 5b 声路试验：同伴只报原字与方向
	game.last_sound = game.engine.SoundTest(0, {"detective": "foyer", "huo": "hall", "shen": "pantry", "su": "R08"})
	game.set_meta("sound_sentence", game.engine.Sentences()[0])
	game.say("sound_test")
	await show_line(game, "墙那边")
	await settle(10)
	await shot("09d-sound-test-directions")
	await finish_dialogue(game)
	# 5c 普通家具的一句心声
	for node in game.get_node("ObjectsAndPeople").get_children():
		if str(node.name).begins_with("StudyBookcase") and node is Sprite2D:
			game.player.position = node.position + Vector2(0, 56)
			await settle(20)
			game._furniture_aside(node.position + node.offset + node.texture.get_size() * 0.5)
			break
	# 心声只停留 2.6 秒；有窗口截图时帧慢，只等两帧。
	await settle(2)
	await shot("09e-furniture-aside")
	# 5d 悬停可交互物件：高亮并显示名称
	var center: Vector2 = game.site_box("E7").get_center()
	game.player.position = center + Vector2(0, 90)
	await settle(30)
	root.warp_mouse(game.get_global_transform_with_canvas() * center)
	await settle(4)
	await shot("09f-hover-name")
	root.warp_mouse(Vector2(4, 4))
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
	await shot("11-playback-four-bubbles")
	stage._apply(doubt, voice)
	await settle(50)
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
	await shot("13-playback-decision-basis")
	stage._teardown()
	await settle(5)
	game.stage = "investigate"
	game.full_match_seen = true
	game.open_manuscript()
	await settle(20)
	await shot("14-manuscript-all-match")
	game.close_modal()
	# 6b 霍铎读到第 8 条也照样碰
	game = await fresh()
	game.jump_to("investigate")
	game.player.position = Vector2(372, 700)
	game.engine.Amplify("huo", "S4")
	game.open_board()
	game.close_modal()
	await finish_dialogue(game)
	game.open_card("E7")
	await finish_dialogue(game)
	await until_modal(game, "card", 120)
	game.close_modal()
	game._on_recall("E7")
	await show_line(game, "图纸值这个价", [0])
	await settle(10)
	await shot("09c-recall-huo-touches-despite-clause-8")
	await finish_dialogue(game)
	game.close_modal()
	# 7 答辩（陈述与出示成对）与第 5 步、门开
	game = await fresh()
	game.jump_to("replay_match")
	game._apply_recall("E10", "shen")
	game.answer()
	var picks := [{"say": "me", "show": "E3"}, {"say": "V3_0", "show": "E8"}, {"say": "V4_0", "show": "E7"}, {"say": "trust", "show": "E10"}]
	var captured_pair := false
	var captured_reverse := false
	var captured_admit := false
	var captured_door := false
	var captured_prints := false
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
		if game.modal_kind == "answer" and picks.is_empty():
			game._modal_choose({"say": "", "show": "E11"})
			continue
		if d.active and d.line != null and not d.busy:
			if d.body.visible_characters >= 0:
				d.reveal()
				continue
			var has_reverse := false
			for r in d.line.responses:
				if str(r.text).begins_with("反问"):
					has_reverse = true
			if has_reverse and not captured_reverse:
				await settle(8)
				await shot("16-answer-step5-reverse")
				captured_reverse = true
			if not captured_admit and str(d.line.text).contains("我从未说过诸位昨日方至"):
				await settle(6)
				await shot("17-demon-admits")
				captured_admit = true
			if not captured_prints and str(d.line.text).contains("鞋底窄"):
				await settle(6)
				await shot("18a-door-demon-prints")
				captured_prints = true
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
	quit(0)
