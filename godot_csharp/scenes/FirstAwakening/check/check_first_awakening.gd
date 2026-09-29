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
	# Camera follows the last physical movement during the hover frame.
	event.position = scene.get_viewport().get_canvas_transform() * destination
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame

func _click_control(control: Control) -> void:
	# New evidence rows need a layout pass before their scroll offsets exist.
	for frame in 3: await process_frame
	var ancestor := control.get_parent()
	while ancestor != null:
		if ancestor is ScrollContainer:
			ancestor.ensure_control_visible(control)
		ancestor = ancestor.get_parent()
	for frame in 3: await process_frame
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
	for i in 260:
		for frame in 2: await process_frame
		for frame in 6000:
			if not balloon.active or not balloon.busy or scene.name_entry.visible: break
			await physics_frame
		if balloon.busy and not scene.name_entry.visible:
			print("STAGING STATE phase=", scene.investigation_phase, " searches=", scene.npc_searches, " line=", balloon.line.text if balloon.line else "")
			for actor in scene.actors.values(): print(actor.name, " at=", actor.position, " target=", actor.target, " travelling=", actor.travelling, " seated=", actor.seated)
			_require(false, "dialogue action completes within 6000 physics frames")
			return
		var name_entry := scene.get("name_entry") as Control
		if name_entry != null and name_entry.visible:
			var name_input := name_entry.get_node("Center/Panel/Margin/Content/Name") as LineEdit
			var name_confirm := name_entry.get_node("Center/Panel/Margin/Content/Confirm") as Button
			_require(name_entry.has_method("validation_error"), "name entry exposes the shared validation used by confirmation")
			for invalid in ["", "   ", "霍铎", "沈鼎", "苏泠", "侦探", "铜口", "一二三四五六七八九十一二三四五六七"]:
				_require(not String(name_entry.validation_error(invalid)).is_empty(), "invalid or reserved name is rejected: " + invalid)
			for valid in ["林", " 林 ", "一二三四五六七八九十一二三四五六"]:
				_require(String(name_entry.validation_error(valid)).is_empty(), "name validation accepts trimmed length 1–16: " + valid)
			_require(name_entry.get_node_or_null("Center/Panel/Margin/Content/Suggested") != null, "suggested-name button is present")
			var player_start: Vector2 = scene.player.position
			_require(name_input.text.is_empty() and name_confirm.disabled, "empty name cannot be confirmed")
			var escape := InputEventKey.new()
			escape.keycode = KEY_ESCAPE
			escape.pressed = true
			Input.parse_input_event(escape)
			await process_frame
			var escape_release := InputEventKey.new()
			escape_release.keycode = KEY_ESCAPE
			Input.parse_input_event(escape_release)
			await process_frame
			_require(name_entry.visible, "Escape does not skip the required naming window")
			await _hold_physical_key(KEY_W, 4)
			_require(scene.player.position.is_equal_approx(player_start), "movement input does not pass through the naming window")
			var naming_mode: String = scene.mode
			await _hold_physical_key(KEY_J, 1)
			await _hold_physical_key(KEY_F, 1)
			_require(name_entry.visible and scene.mode == naming_mode and not scene.modal_open, "journal and reasoning shortcuts stay blocked while naming")
			name_input.grab_focus()
			name_input.clear()
			await _click_control(name_input)
			var typed_name := InputEventKey.new()
			typed_name.keycode = KEY_NONE
			typed_name.physical_keycode = KEY_NONE
			typed_name.unicode = 0x6797
			typed_name.pressed = true
			root.push_input(typed_name, true)
			await process_frame
			var name_release := InputEventKey.new()
			root.push_input(name_release, true)
			for frame in 3: await process_frame
			_require(name_input.text == "林" and not name_confirm.disabled, "real keyboard input enables name confirmation")
			await _capture_click("name-entry")
			if name_confirm.disabled: return
			await _hold_physical_key(KEY_ENTER, 1)
			for frame in 3: await process_frame
			for frame in 6000:
				if not balloon.busy: break
				await physics_frame
			_require(scene.player_name == "林" and not name_entry.visible, "confirmed name is saved and the naming window closes")
			_require(balloon.active and balloon.panel.visible, "dialogue resumes after name confirmation")
			continue
		if balloon.active and balloon.line != null and balloon.line.text == "又出来一个。":
			for actor in scene.actors.values():
				if actor == scene.player: continue
				var offset: Vector2 = scene.player.position - actor.position
				var expected_facing := "front" if offset.y > 0 else "back"
				if absf(offset.x) > absf(offset.y): expected_facing = "right" if offset.x > 0 else "left"
				_require(actor.facing == expected_facing, "opening group faces the detective during the detective's line")
		if balloon.active and balloon.line != null and balloon.line.text.begins_with("妈的！你们房间里翻出行李了吗"):
			_require(scene.actors.values().all(func(actor): return not actor.travelling), "first regrouping dialogue waits for everyone to arrive")
		if balloon.active and balloon.line != null and balloon.line.text.begins_with("把两组查到的东西摊开"):
			_require(scene.actors.values().all(func(actor): return not actor.travelling), "findings exchange waits for everyone to arrive")
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

func _key(key: Key) -> void:
	var event := InputEventKey.new()
	event.keycode = key
	event.physical_keycode = key
	event.pressed = true
	root.push_input(event, true)
	await process_frame
	event.pressed = false
	root.push_input(event, true)
	await process_frame

func _check_locked_dorms(scene: Node2D, allowed := "") -> void:
	if scene.player.seated: scene.call("_leave_bed")
	for index in range(2, 5):
		var door: Node2D = scene.get_node("ObjectsAndPeople/DormitoryDoor" + str(index))
		var expected: bool = door.dorm_owner == allowed
		scene.player.position = door.position + Vector2(0, 28)
		if expected:
			var partner: CharacterBody2D = scene.actors[scene.call("_paired_actor")]
			partner.position = scene.player.position + Vector2(30, 0)
			partner.travelling = false
		for frame in 4: await physics_frame
		_require(door.access_allowed() == expected, "dorm access matches companion: " + door.dorm_owner)
		for cover in door.privacy_covers:
			_require(cover.visible == not expected, "private room is covered until owner accompanies: " + door.dorm_owner)
		if expected:
			_require(door.leaf_collision.disabled, "accompanied owner's door opens on approach")
		else:
			_require(not door.leaf_collision.disabled and is_zero_approx(door.openness), "locked dorm retains leaf collision: " + door.dorm_owner)
			await _hold_physical_key(KEY_W, 22)
			_require(scene.player.position.y >= door.position.y + 10, "real movement cannot cross locked dorm: " + door.dorm_owner)
			var target: Vector2 = door.interior_bounds.get_center()
			_require(scene.call("_route", scene.player.position, target).is_empty(), "navigation cannot route into locked private room")
			await _click_world(scene, target)
			_require(scene.mode == "message" and scene.click_path.is_empty(), "private-room click gives denial without a path")
			_dismiss(scene)
			var site_id := "room_" + str(index - 1)
			scene.call("_interact", site_id)
			_require(not scene.evidence.has(site_id), "inaccessible room cannot leak evidence: " + site_id)
			_dismiss(scene)

func _inspect(scene: Node2D, id: String) -> void:
	scene.player.position = scene.sites[id].point + Vector2(0, 18)
	if not scene.paired_companion.is_empty():
		var partner: CharacterBody2D = scene.actors[scene.call("_paired_actor")]
		partner.position = scene.player.position + Vector2(30, 0)
		partner.travelling = false
	for frame in 3: await physics_frame
	# Use the same interaction entry as E after selecting a dense fixture.
	scene.call("_interact", id)
	await process_frame
	if id in ["door", "transmitter"]:
		await _advance_dialogue(scene, true)
		await _choose_dialogue(scene, "松开拉柄" if id == "transmitter" else "离开门边")
		await _advance_dialogue(scene)
	_dismiss(scene)
	_require(scene.evidence.has(id), "inspection records physical evidence: " + id)

func _check_mind_ui(scene: Node2D) -> void:
	var balloon: CanvasLayer = scene.get("dialogue")
	scene.set("sim_actions", [])
	scene.call("_request_flow")
	await process_frame
	_require(scene.modal_open and not balloon.active, "reopening mind workspace does not repeat its introduction")
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
	var cross_page_program: Dictionary = mind_script.compile_draft(scene)
	_require(not cross_page_program.has("error") and cross_page_program.get("codes", []).has("ACT|音乐家|GoTo|door"), "the detective's page may contain an action assigned to another actor")
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
	_require(verify_button == null or verify_button.disabled, "stale replay cannot verify traces")
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
	_require(scene.get("mode") == "detail" and scene.get("modal").size == scene.modal.detail_bounds.size, "evidence inspection restores its centered card dimensions after replay")
	_require((scene.get("modal").get_node("ModalScroll") as ScrollContainer).vertical_scroll_mode == ScrollContainer.SCROLL_MODE_AUTO, "evidence inspection restores scrolling after replay")
	_dismiss(scene)
	scene.call("_open_journal")
	var escape := InputEventKey.new()
	escape.keycode = KEY_ESCAPE
	escape.pressed = true
	root.push_input(escape, true)
	await process_frame
	_require(not scene.get("modal_open"), "Escape closes a standard modal")

func _check_answer_rules(scene: Node2D) -> void:
	var catalog: Resource = load("res://scenes/FirstAwakening/answers.tres")
	var rules: Script = load("res://scenes/FirstAwakening/AnswerRules.gd")
	var correct := {}
	for section in catalog.sections:
		for option in section.options:
			if not str(option.get("outcome", "")).begins_with("reject_"): correct[section.slot] = option.value
	_require(rules.resolution(catalog.sections, correct, scene.evidence, {}) == "ending_passed", "new three-slot answer has a single qualified ending")
	for section in catalog.sections:
		for option in section.options:
			var outcome: String = option.get("outcome", "")
			if not outcome.begins_with("reject_"): continue
			var wrong: Dictionary = correct.duplicate()
			wrong[section.slot] = option.value
			_require(rules.resolution(catalog.sections, wrong, scene.evidence, {}) == outcome, "specific rejection remains reachable: " + outcome)
	var missing: Dictionary = scene.evidence.duplicate()
	missing.erase("paper")
	_require(rules.resolution(catalog.sections, correct, missing, {}) == "reject_unsupported", "missing paper rejects an unsupported answer")
	missing = scene.evidence.duplicate()
	missing.erase("hinge")
	_require(rules.resolution(catalog.sections, correct, missing, {}) == "ending_passed", "weight may substitute for hinge evidence")
	_require(rules.resolution(catalog.sections, {}, scene.evidence, {}) == "reject_unsupported", "missing answer slots reject without crashing")

func _check_branch(owner: String, movement_check: bool) -> void:
	var scene: Node2D = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for frame in 5: await process_frame
	var balloon: CanvasLayer = scene.get("dialogue")
	_require(balloon.active and balloon.line != null and balloon.line.character == "侦探（心声）", "opening remains delivered by Dialogue Manager")
	await _advance_dialogue(scene)
	await _key(KEY_F)
	_require(scene.mode != "flow" and not scene.meeting_done, "F cannot open mind replay before the broadcast")
	_dismiss(scene)
	if movement_check: await _check_real_movement(scene)
	await _check_locked_dorms(scene)
	# The old distance trigger must no longer start arrival near the table.
	scene.player.position = Vector2(320, 744)
	for frame in 3: await physics_frame
	_require(not balloon.active, "standing inside the old table-distance trigger does not bypass doorway arrival")
	scene.player.position = Vector2(256, 532)
	for frame in 3: await physics_frame
	await _capture_click("before-arrival")
	await _hold_physical_key(KEY_S, 12)
	_require(balloon.active, "crossing the activity-hall doorway automatically starts arrival dialogue")
	await _capture_click("arrival-turn")
	await _advance_dialogue(scene)
	_require(scene.broadcast_heard and scene.investigation_phase == "dorm_search", "arrival dialogue runs broadcast and assigns own-room search")
	await _key(KEY_F)
	_require(scene.mode != "flow" and not scene.meeting_done, "F stays unavailable after broadcast and before roundtable")
	_dismiss(scene)
	scene.call("_interact", "hinge")
	_require(not scene.evidence.has("hinge"), "own-room search does not permit premature public evidence")
	_dismiss(scene)
	for id in ["window_0", "bed_0", "drawer", "paper"]: await _inspect(scene, id)
	await _capture_click("dorm-0")
	_require(scene.investigation_phase == "regroup" and not scene.meeting_done, "four own-room inspections advance to first reunion")
	for frame in 3600:
		if scene.npc_searches.is_empty(): break
		await physics_frame
	_require(scene.npc_searches.is_empty(), "NPC room searches finish before preparing first reunion")
	if not scene.npc_searches.is_empty():
		scene.queue_free()
		current_scene = null
		await process_frame
		return
	scene.player.position = scene.sites.meeting.point + Vector2(0, 24)
	scene.call("_interact", "meeting")
	await _advance_dialogue(scene, true)
	var caption: String = {"engineer": "跟随工程师前往门厅，深入勘验隔离门与地面痕迹。", "cook": "跟随主厨前往备餐间，深入勘验生活器具与水渍抹布。", "musician": "跟随音乐家前往研习室，深入勘验案台图册与声学构造。"}[owner]
	await _choose_dialogue(scene, caption)
	await _advance_dialogue(scene)
	_require(scene.paired_companion == owner and scene.investigation_phase == "paired", "actual dialogue choice selects companion: " + owner)
	var first_site: String = scene.PAIR_SITES[owner][0]
	scene.player.position = scene.sites[first_site].point
	var partner: CharacterBody2D = scene.actors[scene.call("_paired_actor")]
	partner.position = scene.player.position + Vector2(240, 0)
	partner.travelling = false
	_require(not scene.call("can_access_dorm", owner), "selecting a distant owner alone does not unlock the private dorm")
	scene.call("_interact", first_site)
	_require(not scene.deep_inspections.has(first_site), "deep inspection requires the selected companion physically nearby")
	_dismiss(scene)
	await _check_locked_dorms(scene, owner)
	var owned_suffix: String = {"engineer": "1", "cook": "2", "musician": "3"}[owner]
	await _inspect(scene, "room_" + owned_suffix)
	scene.player.position = Vector2(192 + int(owned_suffix) * 320, 232)
	partner.position = scene.player.position + Vector2(30, 0)
	partner.travelling = false
	for frame in 4: await physics_frame
	await _capture_click("dorm-" + owned_suffix)
	scene.player.position = scene.sites.meeting.point + Vector2(0, 24)
	scene.call("_open_meeting")
	_require(scene.mode == "message" and not scene.meeting_done, "returning before six inspections does not begin discussion")
	_dismiss(scene)
	for id in scene.PAIR_SITES[owner]: await _inspect(scene, id)
	_require(scene.deep_inspections.size() == 6, "all six companion-specific deep inspections recorded: " + owner)
	await _key(KEY_F)
	_require(scene.mode != "flow" and not scene.meeting_done, "F remains unavailable until findings are exchanged")
	_dismiss(scene)
	scene.player.position = scene.sites.meeting.point + Vector2(0, 24)
	scene.call("_interact", "meeting")
	await _advance_dialogue(scene)
	_require(scene.meeting_done and scene.investigation_phase == "discussion" and scene.mode == "meeting", "findings exchange starts roundtable before mind replay")
	for other in scene.PAIR_SITES:
		if other == owner: continue
		for id in scene.PAIR_SITES[other]:
			if id == "transmitter":
				_require(scene.evidence.has(id), "broadcast already records the speaking tube")
			else:
				_require(scene.evidence.has(id) and scene.evidence_sources.get(id) == "同伴勘验后转述", "other pair findings retain reported provenance: " + id)
	_dismiss(scene)
	await _key(KEY_F)
	await _advance_dialogue(scene)
	_require(scene.mode == "flow" and not scene.captain, "real F opens mind replay during discussion before nomination")
	for claim in scene.INVESTIGATION_CONTENT.meeting_hypotheses:
		var probe := _find_button(scene.modal, TranslationServer.translate(claim.label_id))
		_require(probe != null, "mind workspace offers hypothesis probe: " + claim.id)
		if probe != null: await _click_control(probe)
		_require(scene.mode == "message" and not scene.disputed[claim.id], "Core hypothesis probe explains a contradiction without winning the meeting: " + claim.id)
		var back := _find_button(scene.modal, "返回脑内推演")
		if back != null: await _click_control(back)
	_dismiss(scene)
	if movement_check: await _check_mind_ui(scene)
	scene.sim_actor_actions.clear()
	var correct: Array[String] = ["ACT|厨师|BraceDoorWithWedge|door", "PREFER|厨师|Smoke|door", "ACT|厨师|RemoveWedge|wedge", "ACT|侦探|SlideInnerBolt|inner_bolt", "ACT|音乐家|WrapWedge|wedge", "ACT|工程师|MoveStop|stop", "ACT|侦探|WipeFloor|floor", "ACT|门外的人|OuterBar|outer_bar"]
	scene.sim_actions = correct
	scene.call("_run_replay")
	scene.call("_start_replay", false)
	while scene.replay_index < scene.replay_events.size(): scene.call("_replay_step", 1)
	var validation: Dictionary = scene.call("_replay_submission_validation")
	_require(validation.ready, "complete current causal replay is compatible with collected evidence: " + str(validation.reasons))
	_dismiss(scene)
	for pair in [["escape", "trace"], ["betrayal", "door"], ["fifth_person", "wash"]]:
		scene.call("_challenge", pair[0])
		var wrong := _find_button(scene.modal, TranslationServer.translate(scene.sites.window_0.title_id))
		_require(wrong != null, "meeting offers a real wrong evidence choice")
		if wrong != null: await _click_control(wrong)
		await _advance_dialogue(scene)
		_require(not scene.disputed[pair[0]], "wrong evidence cannot win the meeting claim: " + pair[0])
		scene.call("_challenge", pair[0])
		var right := _find_button(scene.modal, TranslationServer.translate(scene.sites[pair[1]].title_id))
		_require(right != null, "correct meeting evidence is offered: " + pair[1])
		if right != null: await _click_control(right)
		await _advance_dialogue(scene)
		_require(scene.disputed[pair[0]], "correct physical evidence earns the companion's response: " + pair[0])
		_dismiss(scene)
	_require(scene.captain and scene.disputed.values().all(func(value): return value), "all three rebuttals nominate the detective")
	_check_answer_rules(scene)
	scene.player.position = scene.sites.transmitter.point + Vector2(-30, 30)
	scene.call("_interact", "transmitter")
	await _advance_dialogue(scene, true)
	await _choose_dialogue(scene, "提交论证")
	await _advance_dialogue(scene)
	_require(scene.mode == "answer" and scene.transmitter_connected, "live transmitter opens answer board while connected")
	# Popup signals select the actual offered options instead of assigning slots.
	for index in 3:
		var menus: Array[Node] = scene.modal.find_children("*", "MenuButton", true, false)
		_require(menus.size() == 3 and menus[index].get_popup().item_count == 1, "nominated answer board offers supported explanation for slot " + str(index))
		if menus.size() == 3: menus[index].get_popup().index_pressed.emit(0)
		await process_frame
	var submit := _find_button(scene.modal, TranslationServer.translate("FA_UI_100"))
	_require(submit != null and not submit.disabled, "current replay and three answers enable actual submit button")
	if submit != null and not submit.disabled: await _click_control(submit)
	await _advance_dialogue(scene)
	_require(scene.ended and scene.ending_title == "ending_passed", "three-slot causal answer passes through actual submission: " + owner)
	_require(not scene.get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorFace").visible and scene.get_node("ObjectsAndPeople/MechanicalIsolationDoor/DoorGap").visible, "qualified ending visually releases the gate")
	print("NEW FLOW BRANCH: ", owner, " ", "FAIL" if failed else "PASS")
	scene.queue_free()
	current_scene = null
	await process_frame

func _check_real_movement(scene: Node2D) -> void:
	var floors: Dictionary = scene.get("floors")
	var sites: Dictionary = scene.get("sites")
	_require(sites.size() == 43, "all 43 investigation sites are registered")
	for id in sites:
		var actor_name: String = sites[id].title
		var trigger_path := "ObjectsAndPeople/%s/%s" % [actor_name, id] if scene.actors.has(actor_name) else "InvestigationSites/%s" % id
		var trigger := scene.get_node_or_null(trigger_path) as Area2D
		_require(trigger != null, "site has an Area2D trigger: " + id)
		if trigger != null:
			var shape := trigger.get_node("CollisionShape2D").shape as CircleShape2D
			var target: Vector2 = scene.call("_site_point", id)
			_require(trigger.global_position.is_equal_approx(target) and is_equal_approx(shape.radius, sites[id].reach), "site trigger follows its actor or static resource: " + id)
	var player: CharacterBody2D = scene.get("player")
	var architecture: Sprite2D = scene.get_node("LifeWingArchitecture")
	var camera: Camera2D = scene.get("camera")
	_require(architecture.texture.get_size() == Vector2(1280, 1024), "painted life-wing architecture covers the 40x32 map at 1x")
	_require(scene.get_node("WallSolids").get_child_count() > 60, "every non-walkable cell has wall collision")
	_require(floors.has(Vector2i(5, 9)) and not floors.has(Vector2i(19, 27)), "bedroom doorway is open and the hall gate wall is solid")
	_require(player.seated, "detective opens seated at the bed")
	_require(camera.offset == Vector2.ZERO, "world camera uses the full viewport without a vertical crop offset")
	_require(scene.get("hud_title").position.y < 48 and scene.get("hud_time").position.y < 48, "location and clock stay in the upper HUD")
	_require(scene.get("hud_hint").position.y > 900, "nearby object name sits on the lower edge")
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
	# Stop outside the arrival threshold; its dialogue is exercised later.
	var approach := InputEventKey.new()
	approach.keycode = KEY_S
	approach.physical_keycode = KEY_S
	approach.pressed = true
	Input.parse_input_event(approach)
	for frame in 90:
		await physics_frame
		if player.position.y >= 528 or scene.dialogue.active: break
	var release := approach.duplicate() as InputEventKey
	release.pressed = false
	Input.parse_input_event(release)
	await physics_frame
	var reached_public_study := player.position.x > 224 and player.position.x < 288 and player.position.y > 520 and player.position.y < 544
	_require(reached_public_study and not scene.dialogue.active, "physical down input approaches the doorway without starting arrival: " + str(player.position))
	print("REAL KEY MOVEMENT (time_scale=1): bedside=" + str(bedside_clear) + " bedroom-door=" + str(crossed_room_door) + " corridor-corner=" + str(reached_hall_corner) + " public-study=" + str(reached_public_study) + " final=" + str(player.position))
	await _capture_click("before")
	var click_destination := Vector2(256, 416)
	_require(scene.call("_walkable", click_destination), "click destination in the outer corridor is walkable")
	await _click_world(scene, click_destination)
	for index in 100:
		await physics_frame
	_require(player.position.distance_to(click_destination) <= 8.0, "click movement reaches its destination within the stop radius: " + str(player.position))
	_require((scene.get("click_path") as PackedVector2Array).is_empty(), "click movement stops after arrival")
	_require(not scene.dialogue.active, "movement acceptance leaves doorway arrival for its dedicated check")
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

func run() -> void:
	TranslationServer.set_locale("zh_CN")
	_check_composable_replay()
	_check_replay_logic()
	if not OS.get_cmdline_user_args().has("--replay-logic-only"):
		for owner in ["engineer", "cook", "musician"]:
			await _check_branch(owner, owner == "engineer")
	print("FIRST AWAKENING: ", "FAIL" if failed else "PASS")
	quit(1 if failed else 0)
