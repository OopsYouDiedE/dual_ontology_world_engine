extends SceneTree
## Jump-to-state regression for Red Token staging and conversation wiring.
## Injects state directly, so it is an automated check, not a playtest.
##   godot --headless --path godot_csharp --script res://scenes/FirstAwakening/check/story_flow_check.gd

var host: Node
var failures: Array[String] = []
var checks := 0

func _initialize() -> void:
	call_deferred("_run")

func expect(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
		print("FAIL ", message)

func frames(count: int) -> void:
	for i in count:
		await process_frame

func play_out(title: String, max_steps := 200) -> Array:
	## Starts a conversation (unless already running) and advances it to the end.
	var spoken: Array = []
	if not host.dialogue.active:
		host._begin_dialogue(title)
	await frames(3)
	var steps := 0
	while host.dialogue.active and steps < max_steps:
		if host.dialogue.line != null:
			spoken.append([str(host.dialogue.line.character), str(host.dialogue.line.text)])
		await host.dialogue.advance()
		await host.dialogue.advance()
		await frames(2)
		steps += 1
	expect(not host.dialogue.active, "%s finished within %d steps" % [title, max_steps])
	return spoken

func _run() -> void:
	var scene: Node = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	host = scene
	await frames(20)

	# --- Opening: coat hangs on the stand, the detective wakes in shirt sleeves.
	var stand: Sprite2D = host.scene_items.get_node("CoatStand0")
	expect(not host.coat_worn, "detective starts without the coat")
	expect(stand.texture.resource_path.ends_with("coat_stand_coat.png"), "coat hangs on the R-05 stand at start")
	expect(host.player.sprite.sprite_frames.get_frame_texture("idle_front", 0).atlas.resource_path.ends_with("detective_shirt.png"), "detective sprite uses the shirt atlas")
	await play_out("S01")
	host._close_modal()
	host._dress_detective(true)
	expect(host.coat_worn and stand.texture.resource_path.ends_with("coat_stand.png"), "taking the coat empties the stand")
	expect(host.player.sprite.sprite_frames.get_frame_texture("idle_front", 0).atlas.resource_path.ends_with("detective.png"), "detective sprite switches to the coat atlas")

	# --- Jump past the introductions.
	var introduced: Dictionary = host.red.call("IntroduceCompanions")
	var copper: Dictionary = host.red.call("ReceiveCopperIntroduction")
	host.red_companions_met = true
	print("introductions: ", introduced.get("ok", false), " ", copper.get("ok", false), " ", copper.get("message", ""))
	expect(host.copper_introduction_complete, "copper introduction state reachable by jump")

	# --- Inspection triggers queue their conversation, which waits for open pages.
	host._open_modal("测试页", "red")
	host._accept_red_inspection("E15", {})
	await frames(4)
	expect("S05" in host.story_backlog and not host.dialogue.active, "S05 queued and waits while a page is open")
	host._close_modal()
	await frames(6)
	expect(host.dialogue.active, "S05 plays once the player is back in the field")
	var s05: Array = await play_out("S05")
	expect(s05.size() >= 5, "S05 spoke its lines (%d)" % s05.size())
	host._accept_red_inspection("E15", {})
	host._close_modal()
	await frames(6)
	expect(not host.dialogue.active, "S05 does not repeat")

	# --- Deduction results queue their scenes.
	host.verified = [false, true, false]
	host._queue_story("S16")
	host._queue_story("S17")
	host._close_modal()
	await frames(6)
	await play_out("S16")   # S17 starts from the backlog as S16 ends and is advanced too
	await frames(6)
	expect(host.story_played.has("S17") and host.story_backlog.is_empty() and not host.dialogue.active, "S17 followed S16 from the backlog")

	# --- Every authored conversation runs to its end.
	for title in ["S03", "S04", "S06", "S07", "S08", "S09", "S10", "S11", "S12", "S13", "S14", "S15", "S18", "S19", "S20", "S21", "S22", "S23", "S24", "S25", "S26", "S27"]:
		host.story_backlog.clear()
		var spoken: Array = await play_out(title)
		expect(spoken.size() > 0, "%s has spoken lines" % title)
		for row in spoken:
			expect(not str(row[0]).is_empty(), "%s line has a speaker: %s" % [title, row[1]])

	# --- A closed dormitory is not floor: clicking it never traps the detective in its doorway.
	var locked_door: Node2D = host.scene_items.get_node("DormitoryDoor2")
	host.player.position = locked_door.position + Vector2(-40, 40)
	await frames(2)
	host._move_to_click(locked_door.position + Vector2(0, -20))
	expect(host.click_path.is_empty(), "no route into a closed dormitory")
	host._move_to_click(locked_door.position + Vector2(0, 40))
	for i in 90: await physics_frame
	var meeting: Vector2 = host.get_node("MeetingSpots/Detective").position
	host._move_to_click(meeting)
	expect(not host.click_path.is_empty(), "the corridor still routes back to the common room")
	host._cancel_click_navigation()
	host.player.position = meeting

	# --- The last line of a conversation is marked as the end.
	host.story_backlog.clear()
	host._begin_dialogue("S04")
	await frames(3)
	var last_caption := ""
	var ended_early := 0
	while host.dialogue.active:
		if last_caption.begins_with("结束"): ended_early += 1
		last_caption = host.dialogue.next_button.text
		await host.dialogue.advance()
		await host.dialogue.advance()
		await frames(2)
	expect(last_caption.begins_with("结束"), "final line offers 结束 (%s)" % last_caption)
	expect(ended_early == 0, "earlier lines keep 继续")
	await frames(4)

	# --- Holding Ctrl fast-forwards a plain conversation to its end.
	host.story_backlog.clear()
	host._begin_dialogue("S04")
	await frames(3)
	expect(host.dialogue.active, "S04 starts before fast-forward")
	var ctrl := InputEventKey.new()
	ctrl.keycode = KEY_CTRL
	ctrl.physical_keycode = KEY_CTRL
	ctrl.pressed = true
	Input.parse_input_event(ctrl)
	var waited := 0
	while host.dialogue.active and waited < 900:
		await process_frame
		waited += 1
	ctrl = ctrl.duplicate()
	ctrl.pressed = false
	Input.parse_input_event(ctrl)
	expect(not host.dialogue.active, "holding Ctrl fast-forwards S04 (%d frames)" % waited)
	await frames(4)

	# --- Field UI: restart asks first, the hypothesis notebook opens cleanly.
	host.story_backlog.clear()
	host._confirm_restart()
	expect(host.restart_pending and host.modal_open, "R asks before restarting")
	host._close_modal()
	expect(not host.restart_pending and not host.modal_open, "closing the prompt keeps the run")
	for id in host.REASONING_RECORDS:
		host.red_notes[id] = ["record"]
		host.red_seen[id] = {}
	expect(host.can_use_mind_reasoning(), "seven records unlock the hypothesis notebook")
	host._open_red_notebook()
	await frames(4)
	expect(is_instance_valid(host.deduction_view) and host.red_notebook.visible, "hypothesis notebook opens")
	host._close_modal()
	host._open_recorded_evidence("E05", false)
	await frames(2)
	var book: Node = host.modal.get_node_or_null("EvidenceNotebook")
	var actions: Node = book.get_node("%Actions") if is_instance_valid(book) else null
	expect(is_instance_valid(actions) and not actions.get_child(0).visible, "E05 record offers no 返回检视 loop (PR1-F003)")
	expect(is_instance_valid(book) and book.step(1) and host.last_recorded_evidence != "E05", "evidence book turns to the next record")
	host._open_recorded_evidence("E09", false)
	await frames(2)
	book = host.modal.get_node_or_null("EvidenceNotebook")
	expect(is_instance_valid(book) and book.get_node("%Actions").get_child(0).visible, "E09 record keeps 返回检视 to its operation page")
	host._close_modal()
	host.story_backlog.clear()

	# --- Exit: the door stays shut until Halvard pulls the bolt.
	var door: Node = host.scene_items.get_node("MechanicalIsolationDoor")
	host.ended = true
	host._begin_dialogue("S28")
	await frames(4)
	expect(str(host.dialogue.line.character) == "霍铎", "S28 opens with Halvard's line")
	expect(door.get_node("DoorFace").visible and not host.red_exit_open, "door still closed while Halvard speaks")
	await host.dialogue.advance()
	await host.dialogue.advance()
	await frames(8)
	expect(host.red_exit_open and not door.get_node("DoorFace").visible, "door opens after the bolt line")

	print("story_flow_check: %d checks, %d failures" % [checks, failures.size()])
	quit(1 if failures.size() > 0 else 0)
