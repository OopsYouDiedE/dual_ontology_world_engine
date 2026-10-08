extends SceneTree
## 开发自检截图（有窗口）：经统一宿主（Chapter1.tscn）跳到关键状态，存 PNG 到 artifacts/remake-belief-replay/gallery/。
## 对白与表单由脚本代按；这是开发自检，不是盲玩。
##   godot --path godot_csharp --script res://scenes/Awakening/check/state_gallery.gd

const SCENE := "res://scenes/Awakening/Chapter1.tscn"
const Autoplay := preload("res://scenes/Shared/check/chapter_autoplay.gd")
var host: Node
var field: Node2D
var ui: Node
var auto
var out_dir := ""
var shots := 0

func _initialize() -> void:
	out_dir = ProjectSettings.globalize_path("res://").path_join("../artifacts/remake-belief-replay/gallery").simplify_path()
	DirAccess.make_dir_recursive_absolute(out_dir)
	call_deferred("_run")

func _finish() -> void:
	# 释放 _run 的局部引用后再删宿主；界面与 C# 章节会话的排队删除要在退出前完成。
	if is_instance_valid(host):
		host.begin_shutdown()
		host.queue_free()
	host = null
	await process_frame
	await process_frame
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(0)

func fresh(skip_intro := true) -> Node:
	if is_instance_valid(host):
		host.begin_shutdown()
		host.queue_free()
		await process_frame
		await process_frame
	host = (load(SCENE) as PackedScene).instantiate()
	if skip_intro:
		host.set_meta("skip_intro", true)
	root.add_child(host)
	for i in 4:
		await process_frame
	field = host.field
	ui = host.ui
	auto = Autoplay.new(host)
	return host

func jump(state: String) -> void:
	host.dispatch({"kind": "debug_jump", "state": state})
	await settle(6)

func settle(frames := 20) -> void:
	for i in frames:
		await process_frame

func shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var path := out_dir.path_join(name + ".png")
	root.get_texture().get_image().save_png(path)
	shots += 1
	print("shot " + path)

# ---------------------------------------------------------------- driving ---

## 每帧推进能推进的对白（回应取 choices 里排在前面的序号，没有就取第一项）与报名表。
func advance(choices: Array) -> void:
	var d: CanvasLayer = ui.dialogue
	if ui.modal_kind == "form":
		auto.step()
		return
	if not d.active or d.busy or d.line == null:
		return
	if d.body.visible_characters >= 0:
		d.reveal()
	elif d.line.responses.is_empty():
		d.show_line(d.line.next_id)
	else:
		d.choose(str(d.line.responses[choices.pop_front() if not choices.is_empty() else 0].next_id))

func line_shown(needle: String) -> bool:
	var d: CanvasLayer = ui.dialogue
	return d.active and not d.busy and d.line != null and d.body.visible_characters < 0 and str(d.line.text).contains(needle)

## 推进对白直到某一行（行文本包含 needle）完整显示；choices 依次回答遇到的选项。
func show_line(needle: String, choices: Array = [], limit := 1500) -> bool:
	for i in limit:
		if line_shown(needle):
			return true
		advance(choices)
		await process_frame
	var d: CanvasLayer = ui.dialogue
	push_warning("gallery: line never shown: %s (dialogue active=%s line=%s modal=%s stage=%s)" % [needle, d.active, str(d.line.text) if d.line != null else "-", ui.modal_kind, ui.snapshot.get("stage", "")])
	return line_shown(needle)

## 推进对白直到某种弹窗出现（如广播后自动呈现的公告框）。
func until_modal(kind: String, limit := 1500) -> bool:
	for i in limit:
		if ui.modal_kind == kind:
			return true
		advance([])
		await process_frame
	return ui.modal_kind == kind

func finish_dialogue(choices: Array = []) -> void:
	for i in 1500:
		await process_frame
		if not ui.dialogue.active and ui.modal_kind != "form":
			await settle(8)
			if not ui.dialogue.active and ui.modal_kind != "form":
				return
			continue
		advance(choices)

## 选当前弹窗里的一项（按 id 或 id 结尾）。
func pick(id: String) -> bool:
	return auto.select(id)

func close_modal() -> void:
	if ui.modal_kind in ["card", "menu"]:
		if not pick("close"):
			ui.close_modal()
	elif ui.modal_kind != "":
		ui.close_modal()
	await settle(4)

## 直接打开某张证物卡（不要求站在旁边）。
func open_card(id: String) -> void:
	host.dispatch({"kind": "card", "card_id": id})
	await settle(6)

## 打开又合上，只为记成“看过”。
func examine(id: String) -> void:
	await open_card(id)
	await finish_dialogue()
	await until_modal("card", 120)
	await close_modal()

func place(point: Vector2) -> void:
	field.player.stand_up()
	field.player.position = point
	for i in 10:
		await physics_frame

func interact(id: String) -> void:
	field.interact(id)
	await settle(6)

## 照常走一遍重演（不跳过）；watch 在回放的每一帧上被调用，可暂停回放截图。
func run_replay(vars: Array, watch: Callable) -> void:
	host._on_ui_event({"kind": "replay", "vars": vars, "skip": false})
	var stage_node: Node = field.adapter.stage_node
	for i in 600:
		await process_frame
		if stage_node.playing:
			break
	for i in 6000:
		if not stage_node.playing:
			break
		await watch.call(stage_node)
		await process_frame
	await finish_dialogue()

func detective_thought(stage_node: Node) -> String:
	if stage_node.bubbles.has("detective") and is_instance_valid(stage_node.bubbles["detective"]):
		return str(stage_node.bubbles["detective"].get_meta("text").text)
	return ""

func _run() -> void:
	# 1 醒来
	await fresh(false)
	await show_line("我只记得上船")
	await settle(10)
	await shot("01-wake")
	# 2 开场广播（代管者兼教师）与欲望放大
	await finish_dialogue()
	await place(Vector2(440, 340))
	await show_line("我所立之规")
	await settle(10)
	await shot("02-broadcast-self-introduction")
	await until_modal("card")
	await settle(20)
	await shot("02b-board-presented-after-broadcast")
	await close_modal()
	await show_line("超越卡诺循环的那一张")
	await settle(10)
	await shot("03-desire-amplified")
	await show_line("是在试我们")
	await settle(10)
	await shot("03b-reactions-after-the-question")
	# 3 公告框
	await fresh()
	await jump("investigate")
	await place(Vector2(400, 140))
	await settle(10)
	await open_card("board")
	await settle(10)
	await shot("04-notice-board-school-rules")
	await close_modal()
	# 4 门厅现场、证物卡、比对鞋底由玩家选人、门厅反转
	await place(Vector2(206, 392))
	field.player.pose("idle", "left")
	await finish_dialogue()
	await settle(30)
	await shot("05-foyer")
	await open_card("E3")
	await settle(10)
	await shot("06-card-boot-print")
	await close_modal()
	await examine("E1")
	await open_card("E3")
	pick("experiment")
	await until_modal("menu", 120)
	await settle(10)
	await shot("06b-compare-soles-choose-whose")
	pick("detective")
	await show_line("……是我")
	await settle(10)
	await shot("07-reversal-one")
	await finish_dialogue()
	await close_modal()
	# 5 恶魔低语后，苏泠自己蹲向音叉；侦探在场，指出守则第 1 条，她放下
	host.engine.Probe("amplify", ["su", "S4"])
	await place(Vector2(230, 400))
	await settle(5)
	await open_card("E5")
	await show_line("后面还有你从未听过的")
	await settle(10)
	await shot("08a-whisper-su")
	await show_line("拾起了那把音叉")
	await settle(20)
	await show_line("它说的那句话")
	await settle(10)
	await shot("08b-su-reaches-for-fork-three-responses")
	await show_line("守则第一条写着", [1])
	await settle(10)
	await shot("08-su-refuses-after-rule-1")
	await finish_dialogue()
	await close_modal()
	# 5a 侦探不在场：霍铎独自碰了石板；回来时的神色与“我想起了一点”
	host.engine.Probe("amplify", ["huo", "S4"])
	await place(Vector2(930, 200))
	await settle(10)
	host.engine.Probe("whisper", ["huo", "S4"])
	host.engine.Probe("set_progress", ["nudged", "E7", true])
	await finish_dialogue()
	await settle(60)
	var huo: CharacterBody2D = field.actors.get("huo")
	await place(huo.position + Vector2(60, 40))
	await settle(30)
	await shot("09-huo-after-touching-alone")
	await interact("person:huo")
	await show_line("我想起了一点")
	await settle(10)
	await shot("09a-huo-i-remember-a-little")
	await finish_dialogue()
	await close_modal()
	# 5b 请沈鼎回忆（请求这一路）：犹豫，反问“你说呢？”
	host.engine.Probe("amplify", ["shen", "S4"])
	host.engine.Probe("amplify", ["shen", "nudge"])
	host.engine.Probe("set_progress", ["nudged", "E10", true])
	await place(Vector2(470, 600))
	await settle(5)
	await open_card("E10")
	await until_modal("card", 600)
	pick("recall")
	await show_line("你说呢", [1])
	await settle(10)
	await shot("09b-recall-shen-asks-you-decide")
	await finish_dialogue([0])
	await close_modal()
	# 5d 普通家具与窗：一句心声
	for node in field.adapter.get_node("ObjectsAndPeople").get_children():
		if str(node.name).begins_with("StudyBookcase") and node is Sprite2D:
			await place(node.position + Vector2(0, 56))
			await settle(20)
			host.dispatch({"kind": "furniture", "map": "awakening", "nodes": [str(node.name)], "texts": {}})
			break
	await settle(2)
	await shot("09e-furniture-aside")
	# 5e 悬停可交互物件：只描一件，显示名称
	var center: Vector2 = field.site_box("E7").get_center()
	await place(center + Vector2(0, 90))
	await settle(30)
	root.warp_mouse(field.get_global_transform_with_canvas() * center)
	await settle(4)
	await shot("09f-hover-name")
	root.warp_mouse(Vector2(4, 4))
	# 5g R-06 的生活痕迹
	await place(Vector2(948, 540))
	await settle(20)
	await interact("L6")
	await settle(10)
	await shot("09i-trace-oil-hook")
	await close_modal()
	# 5h 第六个问题起初不写出来
	await jump("reversal_one")
	ui.open_manuscript()
	await settle(20)
	await shot("09h-manuscript-five-questions")
	await close_modal()
	# 6 重演手稿与回放：先用陌生人的设想跑一遍，再换成对的设想
	await jump("replay_match")
	ui.open_manuscript()
	await settle(20)
	await shot("14-manuscript-all-match")
	await close_modal()
	var seen := {"voice": false, "doubt": false, "bolt": false}
	await run_replay([0, 0, 0, 0, 0, 0], func(stage_node: Node) -> void:
		if stage_node.caption == null:
			return
		var thought := detective_thought(stage_node)
		if not seen.voice and stage_node.caption.text.contains("铜口说话"):
			seen.voice = true
			stage_node.paused = true
			await settle(20)
			assert(ui.modal_kind == "", "Playback gallery must not have a foreground modal")
			await shot("11-playback-four-bubbles")
			stage_node.paused = false
		elif not seen.doubt and thought.contains("我凭什么信"):
			seen.doubt = true
			stage_node.paused = true
			await settle(50)
			assert(ui.modal_kind == "", "Playback gallery must not have a foreground modal")
			await shot("12-playback-why-trust")
			stage_node.paused = false)
	await until_modal("replay", 300)
	await settle(20)
	await shot("10-manuscript-strangers")
	await close_modal()
	await run_replay([0, 0, 0, 0, 0, 1], func(stage_node: Node) -> void:
		if stage_node.caption == null:
			return
		if not seen.bolt and detective_thought(stage_node).contains("我更信霍铎"):
			seen.bolt = true
			stage_node.paused = true
			await settle(40)
			assert(ui.modal_kind == "", "Playback gallery must not have a foreground modal")
			await shot("13-playback-decision-basis")
			stage_node.paused = false)
	print("playback captured: ", seen)
	await close_modal()
	# 6b 霍铎读到守则第 1 条也照样碰（请求这一路）
	await fresh()
	await jump("investigate")
	await place(Vector2(536, 300))
	host.engine.Probe("amplify", ["huo", "S4"])
	host.engine.Probe("amplify", ["huo", "nudge"])
	host.engine.Probe("set_progress", ["nudged", "E7", true])
	await open_card("board")
	await close_modal()
	await finish_dialogue()
	await open_card("E7")
	await until_modal("card", 600)
	pick("recall")
	await show_line("图纸值这个价", [0])
	await settle(10)
	await shot("09c-recall-huo-touches-despite-rule-1")
	await finish_dialogue()
	await close_modal()
	# 7 答辩（陈述与出示成对，到第 4 步为止）、门开
	await fresh()
	await jump("replay_match")
	host.engine.Probe("apply_recall", ["E10", "shen"])
	var picks := [{"say": "me", "show": "E3"}, {"say": "V3_0", "show": "E8"}, {"say": "V4_0", "show": "E7"}, {"say": "trust", "show": "E10"}]
	var captured := {"pair": false, "door": false, "prints": false, "hand": false, "hand_answer": false}
	auto.choose = func(_actions: Array) -> String:
		if ui.modal_kind != "form" or str(ui._modal_event.get("layout", "")) != "tabs" or picks.is_empty():
			return ""
		var chosen: Dictionary = picks.pop_front()
		ui._form.select("say", chosen.say)
		ui._form.select("show", chosen.show)
		return "submit"
	await place(field.adapter.anchor("site:E8") + Vector2(0, 30))
	await interact("E8")
	await until_modal("menu", 120)
	pick("answer")
	for i in 20000:
		await process_frame
		var d: CanvasLayer = ui.dialogue
		if ui.modal_kind == "form" and str(ui._modal_event.get("layout", "")) == "tabs" and not picks.is_empty():
			if not captured.pair and picks.size() == 1:
				ui._form.select("say", "trust")
				ui._form.select("show", "E10")
				await settle(8)
				await shot("15-answer-statement-and-evidence")
				captured.pair = true
			auto.step()
			continue
		if d.active and d.line != null and not d.busy and d.body.visible_characters < 0:
			var text := str(d.line.text)
			for spec in [["prints", "鞋底窄", "18a-door-demon-prints"], ["hand", "架上那半枚手印", "18b-ask-about-the-handprint"],
					["hand_answer", "是我取走的", "18c-demon-took-what-was-sealed"], ["door", "你听对了", "18-door-open"]]:
				if not captured[spec[0]] and text.contains(spec[1]):
					await settle(6)
					await shot(spec[2])
					captured[spec[0]] = true
		if ui.modal_kind == "ending":
			break
		auto.step()
	await settle(10)
	await shot("19-ending")
	print("captured: ", captured)
	print("TOTAL %d shots" % shots)
	call_deferred("_finish")
