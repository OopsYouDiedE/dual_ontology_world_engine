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
