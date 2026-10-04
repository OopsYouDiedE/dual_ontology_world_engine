extends SceneTree
## Targeted automatic default-host validation. Direct dialogue driving is not blind play.
const OUT := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/shared-runtime/"
var checks := 0
var failures := 0
func _initialize() -> void: call_deferred("run")
func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok: failures += 1
	print(("PASS " if ok else "FAIL ") + label)
func until(condition: Callable, seconds := 40.0) -> bool:
	var end := Time.get_ticks_msec() + int(seconds * 1000)
	while Time.get_ticks_msec() < end:
		if condition.call(): return true
		await process_frame
	return condition.call()
func advance(game: Node) -> void:
	var d = game.dialogue
	if not d.active or d.busy or d.line == null: return
	if d.body.visible_characters >= 0: d.reveal()
	else:
		var allowed: Array = d.line.responses.filter(func(r): return r.is_allowed)
		if allowed.is_empty(): d.advance()
		else: d.choose(str(allowed[0].next_id))
func drain(game: Node, seconds := 50.0) -> bool:
	var end := Time.get_ticks_msec() + int(seconds * 1000)
	var inactive := -1
	while Time.get_ticks_msec() < end:
		await process_frame
		if game.modal_kind == "name":
			game._name_field.text = "正式宿主验收"
			game._confirm_name()
		if game.modal_kind == "board":
			game._modal_choose(true)
			game.close_modal()
		advance(game)
		if not game.dialogue.active:
			if inactive < 0: inactive = Time.get_ticks_msec()
			if Time.get_ticks_msec() - inactive > 250: return true
		else: inactive = -1
	return false
func snapshot(name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUT + "formal-" + name + ".png")
func visible_text(node: Node) -> String:
	var text := ""
	for item in node.find_children("*", "Label", true, false): text += item.text + "\n"
	for item in node.find_children("*", "RichTextLabel", true, false): text += item.get_parsed_text() + "\n"
	return text
func run() -> void:
	var start := Time.get_ticks_msec()
	var holder: Node = load("res://scenes/Main2D.tscn").instantiate()
	var first: Node = holder.get_node("Awakening")
	root.add_child(holder)
	check(await until(func(): return first.dialogue.active and first.dialogue.line != null), "chapter1 default original opening active")
	print("CHAPTER1 SOURCE id=" + first.engine.ScriptId())
	check(first.engine.ScriptId() == "chapter1", "chapter1 default formal ScriptId")
	var original: Resource = load("res://scenes/Awakening/awakening.dialogue")
	var raw := FileAccess.get_file_as_string("res://scenes/Awakening/awakening.dialogue")
	check(first.STORY.raw_text == first.engine.DialogueText() and first.STORY.raw_text == raw, "chapter1 compiled dialogue raw text equals assembly and original file")
	check(first.STORY.titles == original.titles, "chapter1 original imported title map equals runtime compilation")
	check(first.STORY.lines == original.lines, "chapter1 original imported line graph equals runtime compilation")
	print("DIALOGUE EQUIVALENCE titles=%d lines=%d characters=%d" % [first.STORY.titles.size(), first.STORY.lines.size(), first.STORY.raw_text.length()])
	first.dialogue.reveal()
	print("CHAPTER1 OPENING " + str(first.dialogue.line.text))
	await snapshot("chapter1-opening")
	check(await drain(first) and first.stage == "roam", "chapter1 natural automated original opening reaches roam")
	first.player.stand_up()
	first.player.position = Vector2(440, 340)
	check(await until(func(): return first.stage == "meeting"), "chapter1 actual room entry triggers original meeting")
	var meeting_end := Time.get_ticks_msec() + 60000
	while Time.get_ticks_msec() < meeting_end:
		await process_frame
		if first.modal_kind == "name":
			first._name_field.text = "正式宿主验收"
			first._confirm_name()
		elif first.modal_kind == "board":
			first._modal_choose(true)
			first.close_modal()
		elif first.modal_kind == "card": first.close_modal()
		advance(first)
		if first.stage == "investigate" and not first.dialogue.active: break
	check(first.stage == "investigate" and first.detective_name == "正式宿主验收" and first.flags.has("board_read"), "chapter1 original choices and mutations reach investigate")
	if first.stage == "investigate":
		first._on_experiment("E2")
		check(await until(func():
			advance(first)
			return first.experiments.has("E2") and first.modal_kind == "card"
		), "chapter1 original E2 experiment commits and returns evidence card")
		print("CHAPTER1 CARD " + visible_text(first.modal_root))
		check(visible_text(first.modal_root).contains(first.engine.CardFacts("E2")), "chapter1 card shows original Core facts")
		await snapshot("chapter1-e2-card")
	holder.queue_free()
	for i in 4: await process_frame
	var second: Node = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	root.add_child(second)
	check(await until(func(): return not second.busy), "chapter2 default bridge starts")
	print("CHAPTER2 SOURCE id=" + second.bridge.ScriptId() + " assembly=" + second.bridge.ScriptAssembly())
	check(second.world_script_path == "" and second.bridge.ScriptId() == "chapter2" and second.bridge.ScriptAssembly() == "Chapter2Script", "chapter2 default formal assembly source")
	check(await until(func(): return second.dialogue.active and second.dialogue.line != null), "chapter2 actual original opening shown")
	second.dialogue.reveal()
	print("CHAPTER2 OPENING " + str(second.dialogue.line.text))
	await snapshot("chapter2-opening")
	check(await drain(second) and not second.busy and not second.action_playing, "chapter2 opening naturally reaches idle")
	second.close_modal()
	var investigation := Time.get_ticks_msec()
	second.send("查 中央基座", true)
	var jobs := [0]
	check(await until(func():
		jobs[0] = maxi(jobs[0], second.action_jobs)
		return not second.busy and not second.action_playing and is_instance_valid(second.modal_root)
	, 60.0), "chapter2 original central pedestal investigation naturally converges")
	print("CHAPTER2 INVESTIGATION seconds=%.3f max_jobs=%d" % [(Time.get_ticks_msec() - investigation) / 1000.0, jobs[0]])
	if second.busy or second.action_playing:
		print("BLOCKED busy/action remains; no forced map switch")
	else:
		print("CHAPTER2 CARD " + visible_text(second.modal_root))
		check(visible_text(second.modal_root).contains("中央基座"), "chapter2 original investigation card contains target")
		var current: Dictionary = second.bridge.Snapshot()
		check(second.MAP_PLACE.get(str(second.map.name), "") == current.det_place and second.state.det_place == current.det_place, "chapter2 Core and visible logical place synchronized")
		check(second.actors.size() > 0 and second.state.frames.size() > 0, "chapter2 original actors and Core action frames present")
		print("CHAPTER2 POSITION map=" + str(second.map.name) + " core=" + str(current.det_place) + " actors=%d frames=%d" % [second.actors.size(), second.state.frames.size()])
		await snapshot("chapter2-pedestal-card")
	second.queue_free()
	for i in 4: await process_frame
	print("AUTOMATIC TARGETED DEFAULT HOST VALIDATION; DIRECT DIALOGUE AND COMMAND CALLS; NOT BLIND PLAY")
	print("TOTAL %d checks, %d failed; wall seconds=%.3f" % [checks, failures, (Time.get_ticks_msec() - start) / 1000.0])
	quit(0 if failures == 0 else 1)
