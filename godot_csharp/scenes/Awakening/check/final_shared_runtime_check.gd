extends SceneTree
## Independent targeted automatic regression; state injection and dialogue driving are not blind play.
var holder: Node
var checks := 0
var failures := 0
func _initialize() -> void: call_deferred("run")
func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok: failures += 1
	print(("PASS " if ok else "FAIL ") + label)
func fresh(skip := true) -> Node:
	if is_instance_valid(holder):
		holder.get_node("Awakening").begin_shutdown()
		holder.queue_free()
		await process_frame
		await process_frame
	holder = load("res://scenes/Main2D.tscn").instantiate()
	var game: Node = holder.get_node("Awakening")
	if skip: game.set_meta("skip_intro", true)
	root.add_child(holder)
	for i in 4: await process_frame
	return game
func wait_for(condition: Callable, limit := 1200) -> bool:
	for i in limit:
		if condition.call(): return true
		await process_frame
	return condition.call()
func drain(game: Node, limit := 1500) -> void:
	var idle := 0
	for i in limit:
		await process_frame
		if game.modal_kind == "name":
			game._name_field.text = "独立验证"
			game._confirm_name()
			idle = 0
			continue
		if game.modal_kind == "board":
			game._modal_choose(true)
			game.close_modal()
			idle = 0
			continue
		var d = game.dialogue
		if not d.active:
			idle += 1
			if idle > 12: return
			continue
		idle = 0
		if d.busy or d.line == null: continue
		if d.body.visible_characters >= 0: d.reveal()
		else:
			var allowed: Array = d.line.responses.filter(func(r): return r.is_allowed)
			if allowed.is_empty(): d.advance()
			else: d.choose(str(allowed[0].next_id))
func run() -> void:
	var game := await fresh(false)
	check(await wait_for(func(): return game.dialogue.active), "default scene starts opening dialogue")
	await drain(game)
	check(game.stage == "roam", "default opening reaches roam")
	game.engine.SetProgress("validation", "nested", {"count": 4, "ok": true, "values": [1, "two", {"x": 3}]})
	var nested: Dictionary = game.engine.Progress("validation")["nested"]
	check(nested.count == 4 and nested.ok and nested.values[2].x == 3, "nested Variant dictionary array round trip")
	game.engine.SetDetail("validation", "nested", "count", 5)
	check(game.engine.Progress("validation").nested.count == 5, "nested progress detail persists")
	game.engine.SetProgress("recall_marks", 2, "huo")
	check(game.recall_marks.get(2) == "huo", "numeric recall progress key preserved")
	game.player.stand_up()
	game.player.position = Vector2(440, 340)
	check(await wait_for(func(): return game.stage == "meeting"), "room entry triggers meeting")
	for i in 100:
		await drain(game)
		if game.modal_kind == "card": game.close_modal()
		if game.stage == "investigate" and not game.dialogue.active: break
		await process_frame
	check(game.stage == "investigate" and game.detective_name == "独立验证" and game.flags.has("board_read"), "meeting name and board acknowledgement reaches investigate")
	game = await fresh()
	game.jump_to("investigate")
	game._on_experiment("E2")
	await drain(game)
	check(game.experiments.has("E2"), "experiment completes into shared progress")
	game.close_modal()
	game._on_help("E4")
	for i in 30:
		await drain(game)
		if game.helped.has("E4"): break
		await process_frame
	check(game.helped.has("E4"), "help completes into shared progress")
	game.close_modal()
	game.engine.Amplify("huo", "S4")
	game.nudged = {"E7": true}
	game._on_recall("E7")
	for i in 50:
		await drain(game)
		if game.recalls.has("huo"): break
		await process_frame
	check(game.recalls.has("huo") and game.recall_marks.get(2, "") == "huo", "recall completes and preserves numeric marks")
	game.close_modal()
	game = await fresh()
	game.jump_to("investigate")
	game.engine.Amplify("huo", "S4")
	game.player.position = Vector2(930, 200)
	for i in 4: await physics_frame
	game.engine.Whisper("huo", "自动验证低语")
	game.engine.SetProgress("nudged", "E7", true)
	await game.present_dialogue_command("consider", ["huo"])
	check(await wait_for(func(): return str(game.life.get("huo", {}).get("state", "")) == "done", 3000), "autonomous companion reaches object and completes")
	check(game.engine.Hooks("huo") == 1 and game.engine.IsDisturbed("E7") and not game.recalls.has("huo"), "autonomous original Core action leaves hook without leaking recall")
	game = await fresh()
	game.jump_to("replay_match")
	game.answer()
	var picks := [{"say": "me", "show": "E3"}, {"say": "V3_0", "show": "E8"}, {"say": "V4_0", "show": "E7"}, {"say": "trust", "show": "E9"}]
	for i in 2200:
		await process_frame
		if game.ended: break
		if game.modal_kind == "answer" and not picks.is_empty(): game._modal_choose(picks.pop_front())
		var d = game.dialogue
		if not d.active or d.busy or d.line == null: continue
		if d.body.visible_characters >= 0: d.reveal()
		elif d.line.responses.is_empty(): d.advance()
		else: d.choose(str(d.line.responses[0].next_id))
	check(game.ended and game.flags.has("door_open"), "answer original four-step route reaches finale")
	print("AUTO REGRESSION ONLY; INJECTED STATE AND AUTOMATED DIALOGUE; NOT BLIND PLAY")
	print("TOTAL %d checks, %d failed" % [checks, failures])
	call_deferred("finish", 0 if failures == 0 else 1)

func finish(exit_code: int) -> void:
	if is_instance_valid(holder):
		holder.get_node("Awakening").begin_shutdown()
		holder.queue_free()
	holder = null
	await process_frame
	await process_frame
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(exit_code)
