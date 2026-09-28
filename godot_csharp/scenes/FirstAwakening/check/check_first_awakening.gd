extends SceneTree

var failed := false
const CAPTURE_DIR := "res://../artifacts/first-awakening/"

func _initialize() -> void:
	call_deferred("run")

func _require(ok: bool, description: String) -> void:
	if not ok:
		failed = true
		push_error("FIRST AWAKENING: " + description)

func _dismiss(scene: Node2D) -> void:
	scene.call("_close_modal")
	var dialogue: CanvasLayer = scene.get("dialogue")
	dialogue.active = false
	dialogue.panel.hide()
	scene.set("dialogue_after", "")

func _hold_physical_key(key: Key, frames: int) -> void:
	var event := InputEventKey.new()
	event.keycode = key
	event.physical_keycode = key
	event.pressed = true
	Input.parse_input_event(event)
	for index in frames:
		await physics_frame
	event = InputEventKey.new()
	event.keycode = key
	event.physical_keycode = key
	event.pressed = false
	Input.parse_input_event(event)
	await physics_frame

func _click_world(scene: Node2D, destination: Vector2) -> void:
	var screen_position := scene.get_viewport().get_canvas_transform() * destination
	var motion := InputEventMouseMotion.new()
	motion.position = screen_position
	Input.parse_input_event(motion)
	await process_frame
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = screen_position
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame

func _click_control(control: Control) -> void:
	var at := control.get_global_rect().get_center()
	var motion := InputEventMouseMotion.new()
	motion.position = at
	Input.parse_input_event(motion)
	await process_frame
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = at
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event)
	for i in 3: await process_frame

func _advance_dialogue(scene: Node2D, want_choices := false) -> void:
	var balloon: CanvasLayer = scene.get("dialogue")
	for i in 60:
		for frame in 2: await process_frame
		if not balloon.active:
			_require(not want_choices, "dialogue remains active until requested choices")
			return
		if balloon.choices.is_visible_in_tree() and balloon.choices.get_child_count() > 0:
			_require(want_choices, "dialogue finishes without an unhandled choice")
			return
		await _click_control(balloon.next_button if balloon.next_button.is_visible_in_tree() else balloon.panel)
	_require(false, "dialogue advances within its bounded interaction budget")

func _choose_dialogue(scene: Node2D, caption: String) -> void:
	var choice := _find_button(scene.get("dialogue"), caption)
	_require(choice != null and choice.is_visible_in_tree() and not choice.disabled, "dialogue choice available: " + caption)
	if choice != null and choice.is_visible_in_tree() and not choice.disabled:
		await _click_control(choice)

func _capture_click(name: String) -> void:
	if not OS.get_cmdline_user_args().has("--capture-click"):
		return
	await process_frame
	await RenderingServer.frame_post_draw
	var path := CAPTURE_DIR + "click-move-" + name + ".png"
	_require(root.get_texture().get_image().save_png(path) == OK, "screenshot saved: " + path)
	print("DEBUG SCREENSHOT: " + path)

func _find_button(root_node: Node, caption: String) -> Button:
	if caption == "下一幕":
		var page := root_node.find_child("MindNotebook", true, false)
		if page != null: return page.get_node("Transport/Next") as Button
	for node in root_node.find_children("*", "Button", true, false):
		var button := node as Button
		var shown_text := TranslationServer.translate(button.text)
		var shown_tip := TranslationServer.translate(button.tooltip_text)
		if shown_text == caption or shown_tip == caption or shown_tip.begins_with(caption + "；"):
			return button
	return null

func _press_button(root_node: Node, caption: String) -> bool:
	var button := _find_button(root_node, caption)
	if button == null or button.disabled:
		return false
	button.emit_signal("pressed")
	return true

func _find_button_containing(root_node: Node, fragment: String) -> Button:
	for node in root_node.find_children("*", "Button", true, false):
		var button := node as Button
		if TranslationServer.translate(button.text).contains(fragment) or TranslationServer.translate(button.tooltip_text).contains(fragment):
			return button
	return null

func _press_button_containing(root_node: Node, fragment: String) -> bool:
	var button := _find_button_containing(root_node, fragment)
	if button == null or button.disabled:
		return false
	button.emit_signal("pressed")
	return true

func _check_replay_logic() -> void:
	var bridge = load("res://scenes/FirstAwakening/HallReplayBridge.cs").new()
	root.add_child(bridge)
	var valid: Array[String] = ["BraceDoorWithWedge", "WAIT_SMOKE", "RemoveWedge", "SlideInnerBolt", "WrapWedge", "MoveStop", "WipeFloor", "OuterBar"]
	var compiled: Dictionary = bridge.Compile(valid)
	_require(not compiled.has("error"), "explicit smoke wait compiles")
	if compiled.has("error"): return
	_require(compiled.states.size() == compiled.events.size() + 1, "each replay event has a Core state plus initial state")
	var end: Dictionary = compiled.states.back()
	_require(end.conflicts.is_empty() and end.failures.is_empty(), "valid manuscript has no contradictions or blocked actions")
	_require(end.closed and end.inner_bolt and end.outer_bar and end.wrapped, "automatic counterweight closure permits locks and wrapping")
	for id in ["door-mask", "paper-inner-transfer", "wipe-after-stop"]:
		_require(end.ids.has(id), "explicit wait reproduces " + id)
	for state in compiled.states:
		if state.action == "RemoveWedge":
			_require(state.closed and not state.braced, "withdrawal closes door in Core without an injected CloseDoor")
	for invalid in [["999"], ["0"], ["GOTO:3", "999", "GoToDoor"], ["BraceDoorWithWedge", "IF_SMOKE:9"], ["WAIT_SMOKE"]]:
		var codes: Array[String] = []
		codes.assign(invalid)
		var rejected: Dictionary = bridge.Compile(codes)
		_require(rejected.has("error") and not rejected.has("states") and not rejected.has("actions"), "invalid manuscripts reject atomically")
	for sequence in [["BraceDoorWithWedge", "OuterBar"], ["BraceDoorWithWedge", "SlideInnerBolt"], ["WrapWedge", "BraceDoorWithWedge"], ["BraceDoorWithWedge", "WrapWedge"]]:
		var codes: Array[String] = []
		codes.assign(sequence)
		var blocked: Dictionary = bridge.Compile(codes)
		var state: Dictionary = blocked.states.back()
		_require(not state.succeeded and not state.failures.is_empty(), "blocked actions report failure to the replay")
		_require(not state.outer_bar and not state.inner_bolt, "blocked actions cannot create locks")
	var early: Array[String] = ["BraceDoorWithWedge", "RemoveWedge", "WAIT_SMOKE", "WrapWedge"]
	var early_result: Dictionary = bridge.Compile(early)
	_require(not early_result.states.back().ids.has("door-mask"), "smoke arriving after withdrawal cannot create a wedge shadow")
	var numeric: Array[String] = ["123"]
	_require(bridge.Run(numeric).has("error"), "Run boundary rejects numeric enum input")
	bridge.queue_free()

func _check_composable_replay() -> void:
	var bridge = load("res://scenes/FirstAwakening/HallReplayBridge.cs").new()
	root.add_child(bridge)
	var context := {}
	for key in ["notebook", "study", "water", "door", "paper", "stop", "floor_wipe", "slit", "wash", "window_0", "meeting", "transmitter", "cup"]:
		context[key] = {"title":key, "description":""}
	context.cup.description = "已记录杯片与缺口吻合。"
	var codes: Array[String] = ["ACT|厨师|BraceDoorWithWedge|door", "WHEN|厨师|Smoke|door", "ACT|工程师|RemoveWedge|wedge", "ACT|侦探|SlideInnerBolt|inner_bolt", "ACT|音乐家|WrapWedge|wedge", "ACT|工程师|MoveStop|stop", "ACT|侦探|WipeFloor|floor", "ACT|门外的人|OuterBar|outer_bar"]
	var replay: Dictionary = bridge.CompileDraft(codes, context)
	_require(not replay.has("error"), "composed manuscripts compile")
	if replay.has("error"): return
	var end: Dictionary = replay.states.back()
	_require(end.conflicts.is_empty() and end.failures.is_empty(), "swapping every interior profession preserves the causal explanation")
	_require(end.inner_bolt and end.outer_bar and end.wrapped and end.stop_moved, "free actors still produce real Core state")
	_require(replay.states[1].actor == "厨师" and replay.states[1].positions["厨师"] == "door", "the submitted actor owns the action and destination")
	codes.assign(["ACT|侦探|PushDoor|water", "ACT|侦探|OuterBar|outer_bar", "ACT|门外的人|SlideInnerBolt|inner_bolt"])
	replay = bridge.CompileDraft(codes, context)
	_require(not replay.has("error") and not replay.states[1].succeeded and not replay.states[2].succeeded and not replay.states[3].succeeded, "valid words with impossible objects or sides are attempted failures, not syntax errors")
	codes.assign(["ACT|侦探|PushDoor|door", "ACT|侦探|GoTo|outside", "ACT|侦探|OuterBar|outer_bar", "ACT|侦探|WithdrawInnerBolt|inner_bolt"])
	replay = bridge.CompileDraft(codes, context)
	end = replay.states.back()
	_require(end.sides["侦探"] == "outside" and end.closed and end.outer_bar and not end.succeeded, "crossing an open door releases it and permits only exterior operations")
	codes.assign(["ACT|工程师|GoTo|outside", "ACT|侦探|PullDoor|door"])
	replay = bridge.CompileDraft(codes, context)
	_require(not replay.states[1].succeeded and not replay.states[2].succeeded, "closed-door crossing and pulling toward the stop fail")
	codes.assign(["ACT|侦探|BraceDoorWithWedge|door", "WHEN|工程师|Smoke|door", "ACT|侦探|RemoveWedge|wedge"])
	replay = bridge.CompileDraft(codes, context)
	_require(replay.states.back().braced and not replay.states.back().succeeded and not replay.states.back().smoke, "unobserved conditions skip the dependent action without injecting smoke")
	codes.assign(["ACT|工程师|Inspect|notebook", "ACT|侦探|Compare|cup", "ACT|厨师|OpenValve|water", "ACT|厨师|CloseValve|water", "ACT|工程师|WipeWindow|window_0"])
	replay = bridge.CompileDraft(codes, context)
	_require(not replay.states[1].succeeded and replay.states[2].succeeded, "unknown records stay unknown while known comparisons can be replayed")
	_require(replay.states[3].valve_open and not replay.states[4].valve_open and replay.states[5].clean_windows.has("window_0"), "valve and window actions change independent replay state")
	codes.assign(["ACT|侦探|WrapWedge|wedge", "ACT|侦探|UnwrapWedge|wedge", "ACT|厨师|MoveStop|stop", "ACT|工程师|ReturnStop|stop", "ACT|厨师|SlideInnerBolt|inner_bolt", "ACT|工程师|WithdrawInnerBolt|inner_bolt", "ACT|门外的人|OuterBar|outer_bar", "ACT|门外的人|LiftOuterBar|outer_bar"])
	replay = bridge.CompileDraft(codes, context)
	end = replay.states.back()
	_require(not end.wrapped and not end.stop_moved and not end.inner_bolt and not end.outer_bar and end.failures.is_empty(), "reverse physical actions update current state without erasing history")
	codes.assign(["ACT|侦探|GoTo|door", "ACT|侦探|999|door"])
	replay = bridge.CompileDraft(codes, context)
	_require(replay.has("error") and not replay.has("states"), "composed syntax errors reject the whole manuscript atomically")
	codes.assign(["ACT|侦探|GoTo|door", "WHEN|侦探|DoorOpen|door", "ACT|侦探|BraceDoorWithWedge|door"])
	replay = bridge.CompileDraft(codes, context)
	_require(replay.states.back().closed and not replay.states.back().braced and not replay.states.back().succeeded, "false observable conditions skip their action without inventing the requested state")
	codes.assign(["ACT|厨师|BraceDoorWithWedge|door", "WHEN|厨师|DoorOpen|door", "ACT|工程师|RemoveWedge|wedge"])
	replay = bridge.CompileDraft(codes, context)
	_require(replay.states.back().succeeded and replay.states.back().closed, "a visible true condition permits another actor's physical action")
	codes.assign(["WHEN|工程师|DoorClosed|door", "ACT|侦探|SlideInnerBolt|inner_bolt"])
	replay = bridge.CompileDraft(codes, context)
	_require(not replay.states.back().inner_bolt and not replay.states.back().succeeded, "true world conditions cannot be used by a remote observer")
	codes.assign(["ACT|厨师|OpenValve|water", "WHEN|厨师|WaterRunning|water", "ACT|厨师|CloseDoor|water", "ACT|工程师|WipeFloor|window_0", "ACT|侦探|SlideInnerBolt|inner_bolt", "ACT|侦探|RemoveWedge|inner_bolt", "ACT|侦探|Inspect|inner_bolt"])
	replay = bridge.CompileDraft(codes, context)
	_require(replay.states[3].action == "CloseValve" and not replay.states[3].valve_open and replay.states[4].action == "WipeWindow", "generic close and wipe wording dispatches by actual target")
	_require(replay.states[6].action == "WithdrawInnerBolt" and replay.states.back().caption.contains("处于拔出状态"), "generic removal and later inspection use the actual current inner-bolt state")
	context.transmitter.description = "PRIVATE STATIC DESCRIPTION"
	codes.assign(["ACT|侦探|Talk|transmitter"])
	replay = bridge.CompileDraft(codes, context)
	_require(not replay.states.back().succeeded and not replay.states.back().caption.contains("PRIVATE"), "talk cannot turn object descriptions into invented testimony")
	context.study.description = "已知研习材料。"
	context.meeting.known_dialogue = "已经讨论过的记录。"
	for departure in ["Wait", "Inspect"]:
		codes.assign(["ACT|音乐家|PushDoor|door", "ACT|音乐家|" + departure + "|study", "ACT|侦探|GoTo|outside"])
		replay = bridge.CompileDraft(codes, context)
		end = replay.states.back()
		_require(end.closed and end.positions["音乐家"] == "study" and end.sides["侦探"] == "inside" and not end.succeeded, "leaving a held door to " + departure + " releases it before another actor can cross")
	codes.assign(["ACT|音乐家|PushDoor|door", "ACT|音乐家|Wait|door", "ACT|音乐家|Inspect|door", "ACT|侦探|GoTo|outside"])
	replay = bridge.CompileDraft(codes, context)
	_require(not replay.states.back().closed and replay.states.back().sides["侦探"] == "outside", "waiting and inspecting at the held door preserve the hold")
	for departure in ["Wait", "Talk"]:
		var destination := "study" if departure == "Wait" else "meeting"
		codes.assign(["ACT|侦探|Connect|transmitter", "ACT|侦探|" + departure + "|" + destination])
		replay = bridge.CompileDraft(codes, context)
		_require(not replay.states.back().transmitter_connected and replay.states.back().positions["侦探"] == destination, "leaving the transmission handle to " + departure + " disconnects the circuit")
	codes.assign(["ACT|侦探|Connect|transmitter", "ACT|侦探|Wait|transmitter", "ACT|侦探|Inspect|transmitter"])
	replay = bridge.CompileDraft(codes, context)
	_require(replay.states.back().transmitter_connected, "waiting and inspecting beside the handle keep the circuit connected")
	codes.assign(["ACT|音乐家|PushDoor|door", "ACT|厨师|Convene|meeting", "ACT|侦探|GoTo|outside"])
	replay = bridge.CompileDraft(codes, context)
	_require(replay.states.back().closed and not replay.states.back().succeeded, "a summoned door holder releases the door when joining the meeting")
	bridge.queue_free()

func run() -> void:
	TranslationServer.set_locale("zh_CN")
	_check_composable_replay()
	_check_replay_logic()
	if OS.get_cmdline_user_args().has("--replay-logic-only"):
		print("REPLAY LOGIC: ", "FAIL" if failed else "PASS")
		quit(1 if failed else 0)
		return
	var scene: Node2D = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for index in 5:
		await process_frame
	var balloon: CanvasLayer = scene.get("dialogue")
	_require(balloon.active and balloon.line != null and balloon.line.character == "侦探（心声）", "opening is delivered by Dialogue Manager")
	var manager: Node = root.get_node("DialogueManager")
	var script_resource: Resource = load("res://scenes/FirstAwakening/first_awakening.dialogue")
	for title in ["engineer_0", "chef_0", "musician_0", "engineer_1", "chef_1", "musician_1", "engineer_2", "chef_2", "musician_2", "broadcast", "transmitter", "meeting", "challenge_prison", "challenge_defense", "challenge_escape", "meeting_nomination", "reject_prison", "reject_defense", "reject_escape", "reject_one_side", "reject_unsupported", "ending_newcomer", "ending_history", "ending_exit"]:
		var first_line = await manager.get_next_dialogue_line(script_resource, title, [scene], DMConstants.MutationBehaviour.Skip)
		_require(first_line != null and not first_line.character.is_empty(), "Dialogue Manager loads branch: " + title)
	var transmitter_line = await manager.get_next_dialogue_line(script_resource, "transmitter", [scene])
	_require(transmitter_line.responses.size() == 7, "transmitter choices come from Dialogue Manager")
	for response in transmitter_line.responses:
		if response.text == "提交论证":
			await manager.get_next_dialogue_line(script_resource, response.next_id, [scene])
			_require(scene.get("dialogue_after") == "answer", "Dialogue Manager submit choice opens the answer board")
	_dismiss(scene)
	var floors: Dictionary = scene.get("floors")
	var sites: Dictionary = scene.get("sites")
	_require(sites.size() == 38, "all 38 investigation sites are registered")
	for id in sites:
		var actor_name: String = sites[id].title
		var trigger_path := "ObjectsAndPeople/%s/%s" % [actor_name, id] if scene.actors.has(actor_name) else "InvestigationSites/%s" % id
		var trigger := scene.get_node_or_null(trigger_path) as Area2D
		_require(trigger != null, "site has an Area2D trigger: " + id)
		if trigger != null:
			var shape := trigger.get_node("CollisionShape2D").shape as CircleShape2D
			_require(trigger.global_position.is_equal_approx(sites[id].point) and is_equal_approx(shape.radius, sites[id].reach), "site trigger matches its resource: " + id)
	var player: CharacterBody2D = scene.get("player")
	var architecture: Sprite2D = scene.get_node("LifeWingArchitecture")
	var camera: Camera2D = scene.get("camera")
	_require(architecture.texture.get_size() == Vector2(1280, 1024), "painted life-wing architecture covers the 40x32 map at 1x")
	_require(scene.get_node("WallSolids").get_child_count() > 60, "every non-walkable cell has wall collision")
	_require(floors.has(Vector2i(5, 9)) and not floors.has(Vector2i(19, 27)), "bedroom doorway is open and the hall gate wall is solid")
	_require(player.seated, "detective opens seated at the bed")
	_require(camera.offset == Vector2.ZERO, "world camera uses the full viewport without a vertical crop offset")
	_require(scene.get("hud_title").position.y < 48 and scene.get("hud_time").position.y < 48, "location and clock stay in the upper HUD")
	_require(scene.get("hud_hint").position.y > 900, "interaction help is a single lower-edge line")
	_require(is_equal_approx(Engine.time_scale, 1.0), "movement acceptance runs at normal time scale")
	await _capture_click("start")
	var down := InputEventKey.new()
	down.keycode = KEY_S
	down.physical_keycode = KEY_S
	down.pressed = true
	Input.parse_input_event(down)
	for index in 8:
		await physics_frame
	down.pressed = false
	Input.parse_input_event(down)
	_require(not player.seated and player.position.y > 184, "real movement input stands the detective up and moves him")
	var dorm_door: Node2D = scene.get_node("ObjectsAndPeople/DormitoryDoor1")
	await _hold_physical_key(KEY_D, 24)
	var bedside_clear := player.position.x > 176 and player.position.y < 280
	_require(bedside_clear, "physical right input clears the bed side: " + str(player.position))
	await _hold_physical_key(KEY_S, 140)
	var crossed_room_door: bool = player.position.x > 176 and player.position.y > 384 and dorm_door.leaf_collision.disabled
	_require(crossed_room_door, "physical down input crosses the auto-opening bedroom door: position=" + str(player.position) + ", door_open=" + str(dorm_door.leaf_collision.disabled))
	await _hold_physical_key(KEY_D, 40)
	var reached_hall_corner := player.position.x > 225 and player.position.y > 384 and player.position.y < 448
	_require(reached_hall_corner, "physical right input rounds the corridor corner: " + str(player.position))
	await _hold_physical_key(KEY_S, 90)
	var reached_public_study := player.position.x > 224 and player.position.x < 288 and player.position.y > 520 and player.position.y < 590
	_require(reached_public_study, "physical down input passes the public study doorway and reaches its circulation area: " + str(player.position))
	print("REAL KEY MOVEMENT (time_scale=1): bedside=" + str(bedside_clear) + " bedroom-door=" + str(crossed_room_door) + " corridor-corner=" + str(reached_hall_corner) + " public-study=" + str(reached_public_study) + " final=" + str(player.position))
	await _capture_click("before")
	var click_destination := Vector2(360, 570)
	_require(scene.call("_walkable", click_destination), "click destination in public study is walkable")
	await _click_world(scene, click_destination)
	for index in 100:
		await physics_frame
	_require(player.position.distance_to(click_destination) <= 8.0, "click movement reaches its destination within the stop radius: " + str(player.position))
	_require((scene.get("click_path") as PackedVector2Array).is_empty(), "click movement stops after arrival")
	await _capture_click("after")
	var settled := player.position
	await _click_world(scene, settled + Vector2(4, 0))
	for index in 12:
		await physics_frame
	_require(player.position.distance_to(settled) < 0.5 and (scene.get("click_path") as PackedVector2Array).is_empty(), "nearby clicks stay inside the deadzone")
	await _capture_click("deadzone")
	await _click_world(scene, Vector2.ZERO)
	_require((scene.get("click_path") as PackedVector2Array).is_empty(), "unwalkable clicks do not create a path")
	print("DEBUG MOUSE INPUT: reached=" + str(player.position) + " target=" + str(click_destination) + " deadzone_stable=" + str(player.position.distance_to(settled) < 0.5) + " blocked_click_ignored=" + str((scene.get("click_path") as PackedVector2Array).is_empty()))
	player.position = sites["drawer"].point + Vector2(-2, 10)
	player.pose("idle", "back")
	for index in 3:
		await physics_frame
	var inspect := InputEventKey.new()
	inspect.keycode = KEY_E
	inspect.physical_keycode = KEY_E
	inspect.pressed = true
	root.push_input(inspect, true)
	await process_frame
	var release := InputEventKey.new()
	release.keycode = KEY_E
	release.physical_keycode = KEY_E
	release.pressed = false
	root.push_input(release, true)
	_require(scene.get("evidence").has("drawer"), "real E input inspects the nearest drawer")
	_dismiss(scene)
	player.position = Vector2(154, 190)
	var reached := {Vector2i(player.position / 32.0): true}
	var frontier: Array[Vector2i] = [Vector2i(player.position / 32.0)]
	while not frontier.is_empty():
		var cell: Vector2i = frontier.pop_front()
		for direction in [Vector2i.UP, Vector2i.DOWN, Vector2i.LEFT, Vector2i.RIGHT]:
			var adjacent: Vector2i = cell + direction
			if floors.has(adjacent) and not reached.has(adjacent):
				reached[adjacent] = true
				frontier.append(adjacent)
	for id in sites:
		var site: Dictionary = sites[id]
		var accessible := false
		for cell in reached:
			if (Vector2(cell * 32) + Vector2(16, 16)).distance_to(site.point) < site.reach:
				accessible = true
				break
		_require(accessible, "site is not accessible from connected map: " + id)
	_require(reached.size() > 400, "north dorms and southern loop should share one walkable network")
	for id in ["drawer", "study", "paper", "tear", "hinge", "weight", "slit", "room_0", "rations"]:
		scene.call("_interact", id)
		if id == "drawer":
			var card_nodes: Array = scene.get("modal_box").get_children()
			var card: VBoxContainer = card_nodes[2]
			_require(card_nodes.size() >= 4 and card.get_node("Picture") is TextureRect and card.get_node("Description") is RichTextLabel and card.get_node("Picture").get_index() < card.get_node("Description").get_index(), "evidence card places its image above the description")
		_dismiss(scene)
	# Dense gate fixtures: facing the gate selects the door itself, and Tab
	# reaches every fixture within reach (the earlier playtest blocker).
	var resume_at := player.position
	for stand in [Vector2(644, 944), Vector2(622, 946), Vector2(668, 946)]:
		player.position = stand
		player.pose("idle", "front")
		for index in 2:
			await physics_frame
		var pool: Array = scene.get("reachable")
		_require(pool.has("door"), "gate door is reachable from " + str(stand) + ": " + str(pool))
		var seen := {}
		for index in pool.size():
			seen[scene.get("nearest")] = true
			var tab := InputEventKey.new()
			tab.keycode = KEY_TAB
			tab.physical_keycode = KEY_TAB
			tab.pressed = true
			root.push_input(tab, true)
			await process_frame
		_require(seen.size() == pool.size(), "Tab cycles every reachable gate fixture from " + str(stand))
	player.position = Vector2(644, 944)
	player.pose("idle", "front")
	for index in 2:
		await physics_frame
	_require(scene.get("nearest") == "door", "standing at the gate and facing it selects the door: " + str(scene.get("nearest")))
	player.position = resume_at
	scene.call("_interact", "engineer")
	await process_frame
	_require(scene.get("npc_stage_seen").engineer == 0, "engineer has an initial encounter branch")
	_require(balloon.active and balloon.line.character == "工程师", "NPC speech is delivered by Dialogue Manager")
	_dismiss(scene)
	scene.call("_interact", "door")
	scene.call("_toggle_bolt")
	_require(not scene.get("inner_bolt"), "pulling inner bolt changes mechanical state")
	_dismiss(scene)
	scene.call("_open_transmitter")
	await _advance_dialogue(scene, true)
	_require(scene.get("transmitter_rules") and scene.get("broadcast_heard") and scene.get("transmitter_connected"), "first pull continues from instructions to broadcast and conversation")
	await _choose_dialogue(scene, "松开拉柄")
	await _advance_dialogue(scene)
	_require(not scene.get("transmitter_connected"), "release choice disconnects the speaking tube")
	var cast: Dictionary = scene.get("actors")
	var destinations := {"工程师": Vector2(238, 704), "厨师": Vector2(394, 717), "音乐家": Vector2(481, 710)}
	for id in destinations:
		var route: PackedVector2Array = scene.call("_route", cast[id].position, destinations[id])
		_require(route.size() > 1, id + " can path from their discovery point to the common study")
	var engineer_start: Vector2 = cast["工程师"].position
	for index in 12:
		await process_frame
	_require(cast["工程师"].position.distance_to(engineer_start) > 0, "engineer actually starts walking after the broadcast")
	await create_timer(18.0).timeout
	for id in destinations:
		var distance: float = cast[id].position.distance_to(destinations[id])
		_require(distance < 32, id + " reaches the meeting area without being trapped by walls or furniture; position=" + str(cast[id].position) + ", target=" + str(destinations[id]) + ", distance=" + str(distance) + ", travelling=" + str(cast[id].travelling) + ", waypoint=" + str(cast[id].waypoint) + "/" + str(cast[id].path.size()))
	player.position = sites["meeting"].point + Vector2(0, 24)
	scene.call("_open_meeting")
	_require(scene.get("mode") == "message", "meeting does not reveal claims about uninspected wash evidence")
	_dismiss(scene)
	scene.call("_interact", "wash")
	_dismiss(scene)
	scene.call("_open_meeting")
	await process_frame
	_require(balloon.active and balloon.line.character == "工程师", "the four-person meeting starts through Dialogue Manager")
	_dismiss(scene)
	scene.call("_interact", "engineer")
	await process_frame
	_require(scene.get("npc_stage_seen").engineer == 1, "engineer has a new evidence-based debate branch")
	_dismiss(scene)
	scene.call("_hypothesis_test", "defense")
	_require(scene.get("neutral_actions_unlocked"), "Core geometry counterexample unlocks neutral actions")
	_require(not scene.get("disputed").defense, "sand-table failure does not silently win the group debate")
	_dismiss(scene)
	for pair in [["prison", "抽屉内的新旧补纸"], ["defense", "铰链、石止口和自闭配重"], ["escape", "包纸撕口与接触面的灰迹"]]:
		scene.call("_challenge", pair[0])
		await _advance_dialogue(scene, true)
		_require(not scene.disputed[pair[0]], "opening evidence selection does not decide the rebuttal")
		await _choose_dialogue(scene, pair[1])
		await _advance_dialogue(scene)
		_dismiss(scene)
	_require(scene.get("captain"), "three evidence-backed rebuttals nominate the detective")
	scene.call("_interact", "engineer")
	await process_frame
	_require(scene.get("npc_stage_seen").engineer == 2, "engineer acknowledges the nominated representative")
	_dismiss(scene)
	scene.set("sim_actions", [])
	scene.call("_request_flow")
	await process_frame
	_require(scene.modal_open and balloon.active, "first mind entry opens the workspace before its introduction")
	await _advance_dialogue(scene)
	_require(scene.modal.process_mode == Node.PROCESS_MODE_INHERIT, "mind introduction restores workspace input")
	var mind: Control = scene.get("modal")
	var option_buttons := mind.find_children("*", "OptionButton", true, false)
	_require(option_buttons.size() == 1 and option_buttons[0].name == "Language" and mind.find_children("*", "SpinBox", true, false).is_empty(), "mind presentation only uses a language selector, without action form pickers or numeric flow editors")
	var visible_text := ""
	for node in mind.find_children("*", "Label", true, false): visible_text += (node as Label).text + " "
	for node in mind.find_children("*", "Button", true, false): visible_text += (node as Button).text + " "
	_require(not visible_text.contains("STEP") and not visible_text.contains("GOTO"), "internal control-flow codes are hidden from the player")
	var editor := mind.find_child("DraftEditor",true,false) as TextEdit
	_require(editor != null, "manuscript provides direct text editing")
	editor.grab_focus()
	editor.insert_text_at_caret("音乐家走到门边观察。\n侦探撤下门底的木楔。")
	await process_frame
	var mind_script: Script = load("res://scenes/FirstAwakening/MindPresentation.gd")
	_require(mind_script.compile_draft(scene).has("error"), "another actor cannot be controlled from the detective's page")
	editor.text = "走到隔离门。\n侦探看见热烟后，撤下木楔。\n用纸包起木楔。"
	editor.text_changed.emit()
	scene.mind_drafts["音乐家"] = "用木楔撑开隔离门。"
	await process_frame
	var ui_program: Dictionary = mind_script.compile_draft(scene)
	var expected_codes: Array = ui_program.get("codes", [])
	_require(not ui_program.has("error") and expected_codes.has("WHEN|侦探|Smoke|door") and expected_codes.has("ACT|侦探|RemoveWedge|wedge") and not expected_codes.has("ACT|侦探|CloseDoor|door"), "person-owned condition waits for observed smoke; automatic closure belongs to Core")
	_require(_press_button(mind, "从头重演"), "typed manuscript can run")
	await process_frame
	_require(not scene.sim_result.is_empty() and not scene.replay_events.is_empty(), "direct writing produces an actual Core replay")
	_require(_press_button(mind, "下一幕"), "replay can single step")
	_require(scene.replay_index > 0, "single step advances the replay")
	var previous_result: Dictionary = scene.sim_result.duplicate(true)
	editor = mind.find_child("DraftEditor",true,false) as TextEdit
	editor.set_caret_line(editor.get_line_count()-1)
	editor.set_caret_column(editor.get_line(editor.get_caret_line()).length())
	editor.insert_text_at_caret("\n未知人物开门。")
	await process_frame
	_require(scene.mind_dirty and scene.sim_result == previous_result and not scene.replay_playing, "editing pauses playback and preserves the old result as stale")
	_require(_find_button(mind,"从头重演").disabled and mind_script.compile_draft(scene).line == 3, "invalid input blocks submission and identifies its line")
	while scene.replay_index < scene.replay_events.size(): scene._replay_step(1)
	await process_frame
	var verify_button := _find_button_containing(mind, "核对 ·")
	if verify_button == null: verify_button = _find_button_containing(mind, "标记位置")
	_require(verify_button != null and verify_button.disabled, "stale replay cannot verify traces")
	var retained_text: String = scene.mind_text
	_require(_press_button(mind, "返回现场"), "workspace returns to the scene")
	scene._open_flow()
	await process_frame
	editor = mind.find_child("DraftEditor",true,false) as TextEdit
	_require(editor.text == retained_text, "unsubmitted invalid draft survives leaving and reopening")
	editor.select(3,0,3,editor.get_line(3).length())
	editor.insert_text_at_caret("")
	await process_frame
	_require(not _find_button(mind,"从头重演").disabled, "repairing the invalid line restores submission")
	_require(_press_button(mind,"词汇"), "vocabulary expands on request")
	await process_frame
	editor.set_caret_line(3)
	editor.set_caret_column(0)
	_require(_press_button(mind,"人物"), "person catalogue opens")
	await process_frame
	_require(_press_button(mind,"侦探"), "person token inserts at caret")
	_require(_press_button(mind,"动作"), "verb catalogue opens")
	await process_frame
	_require(_press_button(mind,"用纸包起"), "verb token inserts at caret")
	_require(_press_button(mind,"对象"), "object catalogue opens")
	_require(_press_button(mind,"木楔"), "object token inserts at caret")
	await process_frame
	_require(editor.get_line(3) == "侦探用纸包起木楔" and editor.has_focus(), "vocabulary preserves editing focus and writes at the current caret")
	_require(_press_button(mind,"从头重演"), "revised direct draft can run")
	await process_frame
	_require(not scene.mind_dirty, "new replay clears the stale state")
	_require(_press_button(mind,"返回现场"), "direct-writing workspace closes")
	scene.call("_interact", "drawer")
	_require(scene.get("mode") == "detail" and scene.get("modal").size == Vector2(780, 700), "evidence inspection restores its centered card dimensions after replay")
	_require((scene.get("modal").get_node("ModalScroll") as ScrollContainer).vertical_scroll_mode == ScrollContainer.SCROLL_MODE_AUTO, "evidence inspection restores scrolling after replay")
	_dismiss(scene)
	scene.call("_open_journal")
	var escape := InputEventKey.new()
	escape.keycode = KEY_ESCAPE
	escape.pressed = true
	root.push_input(escape, true)
	await process_frame
	_require(not scene.get("modal_open"), "Escape closes a standard modal")
	scene.sim_actor_actions.clear()
	var correct: Array[String] = ["BraceDoorWithWedge", "IF_SMOKE:4", "GOTO:2", "RemoveWedge", "CloseDoor", "SlideInnerBolt", "WrapWedge", "MoveStop", "WipeFloor", "OuterBar"]
	scene.set("sim_actions", correct)
	var program: Dictionary = scene.call("_compile_program")
	_require(not program.has("error"), "IF / GOTO loop exits when smoke arrives")
	var actions: Array = program.get("actions", [])
	_require(actions.has("SmokeArrives") and actions.find("BraceDoorWithWedge") < actions.find("SmokeArrives"), "environmental smoke enters after the brace")
	scene.call("_run_replay")
	var result: Dictionary = scene.get("sim_result")
	var trace_ids: Array = result.get("ids", [])
	for id in ["door-mask", "paper-inner-transfer", "inner-bolt", "outer-bar", "wipe-after-stop"]:
		_require(trace_ids.has(id), "Core replay must produce " + id)
	_require(result.get("conflicts", []).is_empty(), "correct sequence has no causal contradictions")
	var pause_points: Dictionary = scene.get("replay_pause_points")
	_require(pause_points.size() >= 3, "replay contains key causal pauses")
	var first_pause: int = pause_points.keys().min()
	scene.set("replay_index", first_pause - 1)
	scene.call("_start_replay", false)
	scene.call("_replay_step", 1)
	_require(not scene.get("replay_playing") and scene.get("replay_index") == first_pause, "continuous replay pauses at a pending trace")
	scene.call("_request_trace_check", "纸内转印", "paper-inner-transfer", "paper")
	_require(not scene.get("sim_checks").has("纸内转印"), "a future replay event cannot be verified before playback reaches it")
	scene.call("_request_trace_check", "门底遮蔽", "door-mask", "trace")
	_require(scene.get("pending_inspections").has("trace") and not scene.get("evidence").has("trace"), "prediction stays separate from observed evidence")
	_require(not scene.get("sim_checks").has("门底遮蔽"), "prediction alone does not unlock the conjecture")
	_dismiss(scene)
	player.position = sites["trace"].point
	for index in 3:
		await physics_frame
	var inspect_trace := InputEventKey.new()
	inspect_trace.keycode = KEY_E
	inspect_trace.physical_keycode = KEY_E
	inspect_trace.pressed = true
	root.push_input(inspect_trace, true)
	await process_frame
	inspect_trace.pressed = false
	root.push_input(inspect_trace, true)
	_require(scene.get("evidence").has("trace") and scene.get("sim_checks").has("门底遮蔽"), "real E inspection verifies the predicted door mask")
	_dismiss(scene)
	while scene.get("replay_index") < scene.get("replay_events").size():
		scene.call("_replay_step", 1)
	for point in [["纸内转印", "paper-inner-transfer", "paper"], ["双套锁止", "inner-bolt", "slit"]]:
		scene.call("_verify_trace", point[0], point[1], point[2])
		_dismiss(scene)
	_require(scene.get("checks_this_run") == 3, "exactly three targeted checks used")
	scene.call("_verify_trace", "清理时序", "wipe-after-stop", "stop")
	_require(scene.get("checks_this_run") == 3, "fourth check is blocked until another run")
	var wrong: Array[String] = ["WrapWedge", "BraceDoorWithWedge", "WAIT", "CloseDoor", "SlideInnerBolt"]
	scene.set("sim_actions", wrong)
	var wrong_program: Dictionary = scene.call("_compile_program")
	var wrong_result: Dictionary = scene.get("bridge").call("Run", wrong_program.actions)
	_require(not wrong_result.get("conflicts", []).is_empty(), "early wrapping receives a physical contradiction")
	var impossible_wrap: Dictionary = scene.get("bridge").call("Run", ["BraceDoorWithWedge", "SmokeArrives", "WrapWedge"])
	_require(not impossible_wrap.get("conflicts", []).is_empty() and not impossible_wrap.get("ids", []).has("paper-inner-transfer"), "wedge cannot be wrapped while it still braces the door")
	scene.set("sim_actions", correct)
	scene.call("_run_replay")
	_require(scene.get("checks_this_run") == 0 and scene.get("sim_checks").has("门底遮蔽"), "new runs reset inspection budget while keeping verified conjectures")
	_dismiss(scene)
	player.position = sites["transmitter"].point + Vector2(-30, 30)
	scene.call("_open_transmitter")
	var slots := {"place": "临时牢房", "door": "先撑门进灰 → 撤楔合门 → 沾灰后包纸", "outer": "生活翼内销 + 主楼侧外闩"}
	scene.set("answer_slots", slots)
	scene.call("_submit_answer")
	_require(not scene.get("ended"), "prison explanation is rejected and remains retryable")
	slots.place = "新生入学考场 · 旧居未证实"
	scene.set("answer_slots", slots)
	scene.call("_submit_answer")
	_require(scene.get("ended"), "compatible newcomer explanation formally passes")
	scene.set("ended", false)
	slots.place = "长期生活研习"
	scene.set("answer_slots", slots)
	scene.call("_submit_answer")
	_require(scene.get("ended"), "historical long-use explanation also passes")
	balloon.active = false
	balloon.panel.hide()
	var restart := InputEventKey.new()
	restart.keycode = KEY_R
	restart.pressed = true
	root.push_input(restart, true)
	for index in 8:
		await process_frame
	var restarted: Node2D = current_scene
	_require(restarted != null and restarted != scene and not restarted.get("ended") and (restarted.get("dialogue") as CanvasLayer).active, "R reloads the ending and restarts at the opening")
	if not failed:
		print("FIRST AWAKENING PASS: connected map, interactions, Core replay, three checks, rebuttals and both accepted answers")
	quit(1 if failed else 0)

