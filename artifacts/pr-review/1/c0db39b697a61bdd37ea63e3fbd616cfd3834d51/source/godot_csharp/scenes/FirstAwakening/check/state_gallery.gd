extends SceneTree
## Jump-to-state screenshot gallery for visual and UI review.
## Injects state directly, so it is a review aid, not a playtest.
##   godot --path godot_csharp --script res://scenes/FirstAwakening/check/state_gallery.gd -- --out=<dir> [--only=name,name]
## Windowed rendering is required; headless runs have no framebuffer.

var host: Node
var out := ""
var only: PackedStringArray = []

func _initialize() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--out="): out = arg.trim_prefix("--out=")
		elif arg.begins_with("--only="): only = arg.trim_prefix("--only=").split(",")
	if out.is_empty(): out = ProjectSettings.globalize_path("res://").path_join("../tmp/state_gallery").simplify_path()
	DirAccess.make_dir_recursive_absolute(out)
	call_deferred("_run")

func frames(count: int) -> void:
	for i in count:
		await process_frame

func shot(name: String) -> void:
	if not only.is_empty() and name not in only: return
	await frames(4)
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	image.save_png(out.path_join(name + ".png"))
	print("shot ", name)

func advance_to_end(max_steps := 200) -> void:
	var steps := 0
	while host.dialogue.active and steps < max_steps:
		await host.dialogue.advance()
		await host.dialogue.advance()
		await frames(2)
		steps += 1

func reveal_first_line(title: String) -> void:
	host._begin_dialogue(title)
	await frames(6)
	await host.dialogue.advance()
	await frames(4)

func jump_to_investigation() -> void:
	## Skip the wake-up and introductions: coat on, name set, companions met.
	await advance_to_end()
	host._close_modal()
	host._dress_detective(true)
	host.player_name = "林"
	host.red.call("IdentifyInvestigator", "林")
	host.red.call("IntroduceCompanions")
	host.red.call("ReceiveCopperIntroduction")
	host.red_companions_met = true
	host.conversation_staged = true
	host.investigation_phase = "investigation"
	host.player.position = host.get_node("MeetingSpots/Detective").position
	host.story_backlog.clear()
	await frames(4)

func inspect_all(ids: Array) -> void:
	## Records evidence without opening pages; several passes settle prerequisites.
	for pass_index in 3:
		for id in ids:
			if host.red_notes.has(id) and host.red_seen.has(id): continue
			var result: Dictionary = host.red.call("Inspect", id)
			if not result.get("ok", false):
				if pass_index == 2: print("inspect ", id, " blocked: ", result.get("message", ""))
				continue
			host._accept_red_inspection(id, result)
			var record: Dictionary = await host.ObservationReader.read(host.dialogue, host.RED_DIALOGUE, host.Content.observation_title(id), host)
			if record.status == "complete": host.red_notes[id] = record.paragraphs
			elif pass_index == 2: print("inspect ", id, " record: ", record.status)

func _run() -> void:
	var scene: Node = load("res://scenes/Main2D.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await frames(30)
	host = _find_host(scene)
	if host == null:
		push_error("state_gallery: RedTokenDemo host not found")
		quit(2)
		return
	await frames(10)
	await shot("01_opening_line")
	await host.dialogue.advance()
	await frames(10)
	await shot("02_opening_revealed")
	await jump_to_investigation()
	await shot("03_field_common_room")
	host.player.position = host._site_point("E01") + Vector2(0, 40)
	host._pick_nearest()
	await frames(4)
	await shot("04_field_near_cabinet")
	host._open_red_interaction("E01")
	await frames(6)
	await shot("05_cabinet_page")
	await host._verify_all_deliveries()
	var verified: int = host.PARTY.filter(func(person): return host.safe_done.get("reward:" + person, false)).size()
	print("batch deliveries verified: ", verified, " / 4")
	await frames(120)
	var cabinet_y: float = host._site_point("E01").y
	var hidden: int = host.actors.values().filter(func(actor): return actor.position.y < cabinet_y - 4.0 and actor.position.distance_to(host._site_point("E01")) < 80.0).size()
	print("verified companions standing behind the cabinet: ", hidden)
	await shot("05b_cabinet_verified")
	host._close_modal()
	for person in ["侦探", "工程师霍铎", "主厨沈鼎", "音乐家苏泠"]:
		if host.red.call("SafeTest", "reward:" + person).get("ok", false): host.safe_done["reward:" + person] = true
	await inspect_all(["E15", "E05", "E06", "E07", "E08", "E09", "E01"])
	host.story_backlog.clear()
	host._open_recorded_evidence("E15", false)
	await frames(6)
	await shot("06_evidence_notebook")
	host.modal.get_node("EvidenceNotebook").step(1)
	await frames(4)
	await shot("06b_evidence_next_page")
	host._close_modal()
	host.story_backlog.clear()
	await frames(4)
	host._update_status()
	await shot("07_field_with_reasoning_hud")
	host._request_flow()
	await frames(10)
	await shot("08_deduction_sequence")
	host.deduction_view._switch_mode("motivation")
	await frames(6)
	await shot("08b_deduction_motivation")
	host._open_red_defense()
	await frames(10)
	await shot("09_defense")
	host._close_modal()
	host.story_backlog.clear()
	await frames(4)
	await reveal_first_line("S05")
	await shot("10_dialogue_conversation")
	await advance_to_end()
	host.story_backlog.clear()
	host._close_modal()
	host._confirm_restart()
	await frames(6)
	await shot("11_restart_confirm")
	host._close_modal()
	print("state_gallery: done -> ", out)
	quit(0)

func _find_host(node: Node) -> Node:
	if node.has_method("_open_red_interaction"): return node
	for child in node.get_children():
		var found := _find_host(child)
		if found != null: return found
	return null

