extends SceneTree

const Samples := preload("res://scenes/Shared/check/fixture_data.gd")
# Independent injection regression. Only invitation travel uses real physics frames.
const OUT := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/remake-chapter2/shared-runtime/"
var checks := 0
var failures := 0
var game: Node
func _initialize() -> void: run.call_deferred()
func check(ok: bool, label: String) -> void:
	checks += 1
	print("PASS " if ok else "FAIL ", label)
	if not ok:
		failures += 1
		var f := FileAccess.open("C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/cabinet-fog-verification-round2/issue.txt", FileAccess.WRITE)
		f.store_string("Cabinet automatic regression failed: " + label + "\nRepro: run res://scenes/Awakening/check/cabinet_fog_current_check.gd; current-cabinet.log records preceding checks.\n")
		f.close()
		quit(1)
func drain() -> void:
	for i in 160:
		await process_frame
		if game.dialogue.active: game.dialogue.advance()
		elif i > 3: return
	check(false, "dialogue drain finished within 160 frames")
func locate(p: Vector2) -> void:
	game.player.position = p
	game.sight._cast(Vector2i((p / 32).floor()))
	game._process(0.0)
func state(who: String) -> Array:
	var a = game.actor_of(who)
	return [a.position, a.path.duplicate(), a.speed, a.travelling, game.life.duplicate(true)]
func formal(who: String) -> Array:
	var result := []
	var key: int = game.engine.BeginExperiment("E11", who)
	for i in 20:
		var line: String = game.engine.NextMeeting(key)
		if line == "": break
		result.append(line)
		game.engine.Presented(key)
	return result
func render() -> Image:
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	return root.get_texture().get_image()
func run() -> void:
	game = load("res://scenes/Awakening/Awakening.tscn").instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	for i in 8: await process_frame
	game.process_mode = Node.PROCESS_MODE_DISABLED
	game.jump_to("investigate")
	game.story_lock = false
	game.engine.ReplaceProgress("hand_comparisons", {})
	game.engine.SetProgress("experiments", "E11", true)
	check(not str(game.engine.CardNotes("E11")).contains(game.engine.Content().HELP_NOTES.E11), "legacy experiment flag cannot produce full host note")
	check(not str(game.engine.CardNotes("E11")).contains(game.engine.Content().HELP_NOTES.E11), "legacy experiment flag cannot produce full formal note")
	game.engine.ReplaceProgress("experiments", {})
	locate(Vector2(640, 368))
	var before := {}
	for who in Samples.array("cabinet-fog-check", "case-001"): before[who] = state(who)
	game._on_experiment("E11")
	await drain()
	for who in before: check(state(who) == before[who], "remote card compare preserves " + who + " position/path/speed/life")
	check(game.engine.Progress("hand_comparisons").is_empty(), "remote self compare rejected")
	var shelf: Vector2 = game.site_point("E11a")
	locate(shelf + Vector2(0, 72))
	game.open_card("E11")
	for who in before: check(state(who) == before[who], "opening local E11 card leaves absent actor unchanged " + who)
	game._compare_hand("huo")
	await drain()
	check(game.engine.Progress("hand_comparisons").is_empty(), "absent companion comparison rejected without record")
	for who in before: check(state(who) == before[who], "rejected comparison leaves absent actor unchanged " + who)
	game._compare_hand("detective")
	await drain()
	check(game.engine.Progress("hand_comparisons").size() == 1 and not game.engine.Progress("experiments").has("E11"), "actual shelf self comparison records only 1/4")
	check(str(game.engine.CardNotes("E11")).contains("1/4") and not str(game.engine.CardNotes("E11")).contains(game.engine.Content().HELP_NOTES.E11), "partial progress visible without all-four conclusion")
	for who in Samples.array("cabinet-fog-check", "case-002"): check(state(who) == before[who], "self comparison never summons " + who)
	game.close_modal()
	game.engine.Locate("detective", "R05")
	game.engine.Locate("huo", "Hall")
	check(formal("huo").is_empty(), "formal BeginExperiment rejects remote companion")
	game.engine.Locate("huo", "R05")
	game.engine.Locate("detective", "Hall")
	check(formal("detective").is_empty(), "formal self comparison rejects player outside R05")
	game.engine.Locate("detective", "R05")
	check(formal("outsider").is_empty(), "formal comparison rejects unspecified key")
	game.engine.SetProgress("life", "huo", {"state": "going"})
	check(formal("huo").is_empty(), "formal BeginExperiment rejects busy local companion")
	game.engine.ReplaceProgress("life", {})
	var lines := formal("huo")
	check(lines == ["help_E11_huo"] and game.engine.Progress("hand_comparisons").size() == 2, "formal local comparison emits individual dialogue with no summon (injected room snapshot)")
	game.engine.RemoveProgress("hand_comparisons", "huo")
	game.engine.SetProgress("hand_comparisons", "outsider", true)
	game.engine.SetProgress("hand_comparisons", "huo", true)
	game.engine.SetProgress("hand_comparisons", "shen", true)
	check(not str(game.engine.CardNotes("E11")).contains(game.engine.Content().HELP_NOTES.E11) and not str(game.engine.CardNotes("E11")).contains(game.engine.Content().HELP_NOTES.E11), "four dictionary entries missing su cannot produce full host/formal conclusion")
	for key in Samples.array("cabinet-fog-check", "case-003"): game.engine.RemoveProgress("hand_comparisons", key)
	for who in Samples.array("cabinet-fog-check", "case-004"):
		var actor = game.actor_of(who)
		actor.travelling = false
		actor.position = shelf + Vector2(32, 40)
		game._process(0.0)
		game._compare_hand(who)
		await drain()
		check(game.engine.Progress("hand_comparisons").has(who), "physically present individual comparison " + who + " (injected position, drained dialogue)")
		if who != "su": check(not game.engine.Progress("experiments").has("E11"), "incomplete group cannot set experiment after " + who)
		actor.position = before[who][0]
	check(game.engine.Progress("hand_comparisons").size() == 4 and game.engine.Progress("experiments").has("E11") and str(game.engine.CardNotes("E11")).contains(game.engine.Content().HELP_NOTES.E11), "all four individual records enable conclusion and experiment")
	game.open_card("E11")
	var card := await render()
	check(card.save_png(OUT + "current-hand-outline-card.png") == OK, "real rendered E11 result screenshot")
	game.close_modal()
	# Invitation: initial placement is a fixture; from invitation onward no position injection.
	var a = game.actor_of("huo")
	a.position = shelf + Vector2(0, 136)
	locate(a.position + Vector2(32, 0))
	game.engine.RemoveProgress("examined", "E11")
	game._invite_hand("huo")
	check(not a.travelling and not game.hand_invitations.has("huo"), "invitation unavailable before discovery")
	game.engine.SetProgress("examined", "E11", true)
	var idle := state("huo")
	game.engine.SetProgress("life", "huo", {"state": "holding"})
	game._invite_hand("huo")
	check(not a.travelling and not game.hand_invitations.has("huo") and game.aside.text.contains(Samples.text("cabinet-fog-check", "text-001")), "busy holding person receives rejection feedback")
	game.engine.RemoveProgress("life", "huo")
	a.travelling = true
	game._invite_hand("huo")
	check(not game.hand_invitations.has("huo") and game.aside.text.contains(Samples.text("cabinet-fog-check", "text-001")), "already travelling person receives busy rejection")
	a.travelling = false
	locate(Vector2(640, 368))
	game._invite_hand("huo")
	check(state("huo") == idle and not game.hand_invitations.has("huo") and game.aside.text.contains(Samples.text("cabinet-fog-check", "text-002")), "remote invisible person receives rejection without movement")
	locate(a.position + Vector2(64, 0))
	game._invite_hand("huo")
	check(not a.travelling and not game.hand_invitations.has("huo") and game.aside.text.contains(Samples.text("cabinet-fog-check", "text-002")), "visible person beyond 56px receives proximity rejection")
	locate(a.position + Vector2(32, 0))
	var saved_floors = game.floors
	game.floors = {}
	game._invite_hand("huo")
	check(state("huo") == idle and not game.hand_invitations.has("huo") and game.aside.text.contains(Samples.text("cabinet-fog-check", "text-003")), "injected no-walkable-route receives rejection without movement")
	game.floors = saved_floors
	var player_start: Vector2 = game.player.position
	game._invite_hand("huo")
	check(a.travelling and game.hand_invitations.has("huo") and not game.story_lock and game.field_active(), "local invitation starts asynchronous normal follow_path with player control")
	var start: Vector2 = a.position
	var previous: Vector2 = start
	var max_step := 0.0
	a.process_mode = Node.PROCESS_MODE_ALWAYS
	var started := Time.get_ticks_msec()
	while a.travelling and Time.get_ticks_msec() - started < 8000:
		await physics_frame
		max_step = maxf(max_step, a.position.distance_to(previous))
		previous = a.position
		game._update_hand_invitations(1.0 / 60.0)
	check(not a.travelling and game._at_hand_shelf(a) and a.position.distance_to(start) > 32.0 and max_step < 5.0, "real normal physics path arrives at shelf without teleport (max step %.3f)" % max_step)
	check(game.player.position == player_start and not game.story_lock and game.hand_invitations.is_empty() and game.aside.text.contains(Samples.text("cabinet-fog-check", "text-004")), "arrival feedback and invitation cleanup without forced player movement")
	game.engine.RemoveProgress("hand_comparisons", "huo")
	game.engine.Locate("detective", "R05")
	check(formal("huo") == ["help_E11_huo"], "actual invitation arrival synchronizes formal room R05 (no actor room injection)")
	# Stop a second actual accepted invitation; no fabricated invitation dictionary.
	a.process_mode = Node.PROCESS_MODE_DISABLED
	a.position = shelf + Vector2(0, 136)
	locate(a.position + Vector2(32, 0))
	game._invite_hand("huo")
	check(a.travelling and game.hand_invitations.has("huo") and game.hand_invitations.huo.has("room"), "stopped-actor fixture uses actual accepted invitation schema")
	var stopped: Vector2 = a.position
	game._update_hand_invitations(0.6)
	game._update_hand_invitations(0.6)
	check(not a.travelling and a.position == stopped and not game.hand_invitations.has("huo") and game.aside.text.contains(Samples.text("cabinet-fog-check", "text-005")), "accepted invitation with disabled actor cancels without snapping (injected stop)")
	print("RESULT automatic checks=", checks, " failures=", failures)
	quit(0 if failures == 0 else 1)
