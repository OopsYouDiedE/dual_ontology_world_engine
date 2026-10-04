extends SceneTree
const DIR := "C:/Users/iii/Desktop/dual_ontology_world_engine/godot_csharp/tools/bin/world-scripts/"
var failures := 0
var count := 0
func _initialize() -> void: call_deferred("run")
func check(ok: bool, label: String) -> void:
	count += 1
	if not ok: failures += 1
	print(("PASS " if ok else "FAIL ") + label)
func until(condition: Callable, limit_seconds := 40.0) -> bool:
	var deadline := Time.get_ticks_msec() + int(limit_seconds * 1000)
	while Time.get_ticks_msec() < deadline:
		if condition.call(): return true
		await process_frame
	return condition.call()
func text_of(game: Node) -> String:
	var text := ""
	if is_instance_valid(game.modal_root):
		for node in game.modal_root.find_children("*", "RichTextLabel", true, false): text += node.get_parsed_text()
	return text
func advance_dialogue(game: Node) -> void:
	var d = game.dialogue
	if not d.active or d.busy or d.line == null: return
	if d.body.visible_characters >= 0: d.reveal()
	elif d.line.responses.is_empty(): d.advance()
	else: d.choose(str(d.line.responses[0].next_id))
func snapshot(name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var output := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/shared-runtime/queue-replay-" + name + ".png"
	root.get_texture().get_image().save_png(output)
func run() -> void:
	var holder: Node = load("res://scenes/Main2D.tscn").instantiate()
	var awakening: Node = holder.get_node("Awakening")
	root.add_child(holder)
	for i in 8: await process_frame
	check(await until(func(): return awakening.dialogue.active), "default chapter1 starts original opening dialogue")
	holder.queue_free()
	for i in 4: await process_frame
	holder = load("res://scenes/Main2D.tscn").instantiate()
	awakening = holder.get_node("Awakening")
	awakening.set_meta("skip_intro", true)
	root.add_child(holder)
	for i in 8: await process_frame
	awakening.run_script(DIR + "FinalAwakeningHostScript/output/WorldScript.FinalAwakeningHostScript.dll")
	check(await until(func(): return text_of(awakening).contains("VALIDATION BEFORE IMAGE")), "chapter1 external media first text displayed")
	await snapshot("chapter1-text")
	awakening._modal_choose(true)
	check(await until(func(): return is_instance_valid(awakening.modal_root) and not awakening.modal_root.find_children("*", "TextureRect", true, false).is_empty()), "chapter1 image displayed after first acknowledgement")
	check(not text_of(awakening).contains("VALIDATION AFTER IMAGE"), "chapter1 following text waits for image acknowledgement")
	await snapshot("chapter1-image")
	awakening._modal_choose(true)
	check(await until(func(): return text_of(awakening).contains("VALIDATION AFTER IMAGE")), "chapter1 following text displayed after image acknowledgement")
	awakening._modal_choose(true)
	await process_frame
	check(await until(func(): return is_instance_valid(awakening.modal_root) and not awakening.modal_root.find_children("*", "TextureRect", true, false).is_empty()), "chapter1 second media displayed independently")
	awakening._modal_choose(true)
	check(await until(func(): return text_of(awakening).contains("VALIDATION AFTER SECOND IMAGE")), "chapter1 text after second media displayed")
	awakening._modal_choose(true)
	await process_frame
	awakening.run_script(DIR + "ExampleAwakening/output/WorldScript.ExampleAwakening.dll")
	check(await until(func(): return text_of(awakening).contains("你准备怎样开始调查")), "chapter1 ordinary Example DLL loads in actual Godot context")
	var figures: Dictionary = awakening.script_actors.figures
	check(figures.has("witness") and figures.witness.standard.resource_path.ends_with("engineer.tres"), "chapter1 ScriptCharacter uses original engineer SpriteFrames")
	if figures.has("witness"):
		figures.witness.pose("inspect")
		check(figures.witness.sprite.animation == "inspect_" + figures.witness.facing, "chapter1 ScriptCharacter reuses original action animation")
	awakening._modal_choose("pocket")
	check(await until(func(): return text_of(awakening).contains("放大镜")), "chapter1 ordinary Example Core action continues to following text")
	awakening._modal_choose(true)
	holder.queue_free()
	for i in 4: await process_frame
	var game: Node = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	game.world_script_path = DIR + "FinalHostScript/output/WorldScript.FinalHostScript.dll"
	root.add_child(game)
	check(await until(func(): return not game.busy), "chapter2 actual bridge starts custom compiled script")
	var intro_started := Time.get_ticks_msec()
	while Time.get_ticks_msec() - intro_started < 40000:
		await process_frame
		advance_dialogue(game)
		if not game.dialogue.active and not game.busy: break
	check(not game.dialogue.active and not game.busy, "chapter2 intro finishes naturally before investigation")
	game.close_modal()
	game.send("查 中央基座", true)
	var investigation_started := Time.get_ticks_msec()
	var parallel_jobs := 0
	var action_screenshot := false
	while Time.get_ticks_msec() - investigation_started < 40000:
		parallel_jobs = maxi(parallel_jobs, game.action_jobs)
		if game.action_playing and game.action_jobs > 1 and not action_screenshot:
			await snapshot("chapter2-parallel-actions")
			action_screenshot = true
		if not game.busy and is_instance_valid(game.modal_root): break
		await process_frame
	check(not game.busy and is_instance_valid(game.modal_root), "chapter2 actual room info investigation returns modal")
	print("NATURAL INVESTIGATION SECONDS %.3f" % ((Time.get_ticks_msec() - investigation_started) / 1000.0))
	check(parallel_jobs > 1 and action_screenshot, "chapter2 independent actor jobs play concurrently in actual scene")
	print("MAXIMUM CONCURRENT ACTOR JOBS %d" % parallel_jobs)
	if game.busy:
		print("BLOCKED: investigation still busy; will not destroy playing actors with load_map")
		quit(1)
		return
	game.close_modal()
	game.load_map("clinic", "", Vector2(300, 300), true)
	check(await until(func(): return not game.busy and game.state.get("mode", "") == "story"), "chapter2 room trigger reaches synchronized choice input")
	check(game.bridge.Snapshot().det_place == "Clinic" and game.state.story_choice.id == "clinic-choice", "chapter2 actual Core room matches visible triggered choice")
	await snapshot("chapter2-choice")
	if not game.state.has("story_choice"):
		print("BLOCKED chapter2 remaining checks: no real synchronized story choice")
		game.queue_free()
		print("AUTO HOST REGRESSION ONLY; NOT BLIND PLAY")
		quit(1)
		return
	check(game.actors.values().any(func(a): return a.name == "验证学员" and a.standard.resource_path.ends_with("engineer.tres")), "chapter2 ScriptCharacter rendered with original engineer frames")
	game.send("record")
	var before_seen := false
	var media_started := Time.get_ticks_msec()
	while Time.get_ticks_msec() - media_started < 40000:
		await process_frame
		if game.dialogue.active and game.dialogue.line != null:
			before_seen = before_seen or str(game.dialogue.line.text).contains("VALIDATION BEFORE IMAGE")
		advance_dialogue(game)
		if game.modal_kind == "story_image": break
	check(before_seen and game.modal_kind == "story_image", "chapter2 text then image displayed in script order")
	await snapshot("chapter2-image")
	check(not game.dialogue.active and game.output_pending.size() > 0 and game.output_pending[0].contains("VALIDATION AFTER IMAGE"), "chapter2 following text remains queued behind image acknowledgement")
	var buttons: Array = game.modal_root.find_children("*", "Button", true, false)
	check(not buttons.is_empty(), "chapter2 image has actual acknowledgement control")
	if not buttons.is_empty(): buttons[-1].pressed.emit()
	check(await until(func(): return game.dialogue.active and game.dialogue.line != null and str(game.dialogue.line.text).contains("VALIDATION AFTER IMAGE")), "chapter2 following text appears after image acknowledgement")
	check(game.state.story_presentations.size() == 2 and game.state.story_presentations[0].sequence != game.state.story_presentations[1].sequence, "chapter2 multiple media carry unique sequence numbers")
	check(game.state.frames.any(func(f): return f.actor == "script:host-guest" and str(f.frames_path).ends_with("engineer.tres") and f.frame == "操作"), "chapter2 ScriptCharacter Core action emits frame with original SpriteFrames")
	var second_media_started := Time.get_ticks_msec()
	while Time.get_ticks_msec() - second_media_started < 40000:
		await process_frame
		advance_dialogue(game)
		if game.modal_kind == "story_image": break
	check(game.modal_kind == "story_image", "chapter2 second media waits for its own acknowledgement")
	if is_instance_valid(game.modal_root):
		var second: Array = game.modal_root.find_children("*", "Button", true, false)
		if not second.is_empty(): second[-1].pressed.emit()
	check(await until(func(): return game.dialogue.active and game.dialogue.line != null and str(game.dialogue.line.text).contains("VALIDATION AFTER SECOND IMAGE")), "chapter2 text continues after second media")
	game.queue_free()
	print("AUTO HOST REGRESSION ONLY; SCRIPT FIXTURE AND DIRECT CALLS; NOT BLIND PLAY")
	print("TOTAL %d checks, %d failed" % [count, failures])
	quit(0 if failures == 0 else 1)
