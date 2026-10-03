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

## 让当前对白行完整显示并停在某一行（行文本包含 needle）。
func show_line(game: Node, needle: String, limit := 600) -> bool:
	for i in limit:
		await process_frame
		var d = game.dialogue
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
			d.choose(str(d.line.responses[0].next_id))
	return false

func finish_dialogue(game: Node) -> void:
	for i in 900:
		await process_frame
		var d = game.dialogue
		if not d.active:
			await settle(6)
			if not game.dialogue.active:
				return
			continue
		if d.busy or d.line == null:
			continue
		if d.body.visible_characters >= 0:
			d.reveal()
		elif d.line.responses.is_empty():
			d.advance()
		else:
			d.choose(str(d.line.responses[0].next_id))

func _run() -> void:
	# 1 醒来
	var game := await fresh(false)
	await show_line(game, "我只记得上船")
	await settle(10)
	await shot("01-wake")
	# 2 门厅调查
	game = await fresh()
	game.jump_to("investigate")
	game.player.position = Vector2(640, 918)
	game.player.pose("idle", "front")
	await settle(30)
	await shot("02-foyer")
	game.open_card("E3")
	await settle(10)
	await shot("03-card-boot-print")
	game.close_modal()
	game.examine("E1")
	await finish_dialogue(game)
	game._on_experiment("E3")
	await show_line(game, "……是我")
	await settle(10)
	await shot("04-reversal-one")
	await finish_dialogue(game)
	game.close_modal()
	# 3 重演手稿：初识时的冲突
	for id in ["E2", "E4", "E5", "E6", "E7", "E8", "E9", "E10", "E11", "E12"]:
		game.examine(id)
	game.replay_vars = [0, 0, 0, 0, 0, 0]
	var refused: Dictionary = game.run_replay(game.replay_vars)
	game.last_run_vars = game.replay_vars.duplicate()
	game.open_manuscript()
	await settle(20)
	await shot("05-manuscript-strangers")
	game.close_modal()
	# 4 回放：铜口说话的同一刻，四个心声气泡；以及侦探拒绝转述的一刻
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
	await shot("06-playback-four-bubbles")
	stage._apply(doubt, voice)
	await settle(50)
	await shot("07-playback-why-trust")
	stage._teardown()
	await settle(5)
	# 5 全部吻合
	game.replay_vars = [0, 0, 0, 0, 0, 1]
	game.run_replay(game.replay_vars)
	game.last_run_vars = game.replay_vars.duplicate()
	game.full_match_seen = true
	game.open_manuscript()
	await settle(20)
	await shot("08-manuscript-all-match")
	game.close_modal()
	# 6 答辩第 5 步
	game = await fresh()
	game.jump_to("replay_match")
	game.answer()
	var picks := [{"kind": "say", "id": "me"}, {"kind": "show", "id": "E8"}, {"kind": "show", "id": "E7"}, {"kind": "say", "id": "V5_0"}, {"kind": "show", "id": "E10"}]
	var captured_reverse := false
	var captured_answer := false
	for i in 2400:
		await process_frame
		var d = game.dialogue
		if game.modal_kind == "answer" and not picks.is_empty():
			if not captured_answer and picks.size() == 2:
				game._answer_show_list("say")
				await settle(8)
				await shot("09-answer-statements")
				captured_answer = true
			game._modal_choose(picks.pop_front())
			continue
		if game.modal_kind == "answer" and picks.is_empty():
			game._modal_choose({"kind": "show", "id": "E11"})
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
				await shot("10-answer-step5-reverse")
				captured_reverse = true
			if str(d.line.text).contains("我从未说过诸位昨日方至"):
				await settle(6)
				await shot("11-demon-admits")
			if str(d.line.text).contains("你听对了"):
				await settle(6)
				await shot("12-door-open")
			if d.line.responses.is_empty():
				d.advance()
			else:
				d.choose(str(d.line.responses[0].next_id))
		if game.ended:
			break
	await settle(10)
	await shot("13-ending")
	print("TOTAL %d shots" % shots)
	quit(0)
