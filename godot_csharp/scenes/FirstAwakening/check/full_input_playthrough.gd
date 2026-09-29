extends SceneTree
## End-to-end regression through simulated keyboard and mouse input only.
## Scene state is read for assertions and routing; progression is never assigned.

const OUT := "res://../artifacts/first-awakening/"
const PLAYTHROUGH_LOG := "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/first-awakening/full-playthrough-run.log"
var scene: Node2D
var failed := false
var log_lines: Array[String] = []
var arrival_captured := false
var companion := "engineer"
var captured_dialogue: Dictionary = {}

func _initialize() -> void:
	call_deferred("run")

func note(message: String) -> void:
	log_lines.append(message)
	print("PLAYTHROUGH: ", message)

func check(ok: bool, message: String) -> bool:
	if not ok:
		failed = true
		push_error("PLAYTHROUGH: " + message)
		note("FAIL " + message)
	return ok

func wait_frames(count := 3) -> void:
	for i in count:
		await process_frame

func key(code: Key, down: bool, unicode := 0) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.unicode = unicode
	event.pressed = down
	event.ctrl_pressed = down if code == KEY_CTRL else Input.is_physical_key_pressed(KEY_CTRL)
	event.shift_pressed = down if code == KEY_SHIFT else Input.is_physical_key_pressed(KEY_SHIFT)
	event.alt_pressed = down if code == KEY_ALT else Input.is_physical_key_pressed(KEY_ALT)
	Input.parse_input_event(event)
	await process_frame

func tap(code: Key, unicode := 0) -> void:
	await key(code, true, unicode)
	await key(code, false, unicode)
	await wait_frames(2)

func click(control: Control) -> void:
	var ancestor := control.get_parent()
	while ancestor != null:
		if ancestor is ScrollContainer:
			for attempt in 80:
				var bounds: Rect2 = ancestor.get_global_rect()
				var target: Vector2 = control.get_global_rect().get_center()
				if bounds.grow(-12).has_point(target): break
				var hover := InputEventMouseMotion.new()
				hover.position = bounds.get_center()
				Input.parse_input_event(hover)
				await process_frame
				var wheel := InputEventMouseButton.new()
				wheel.position = bounds.get_center()
				wheel.button_index = MOUSE_BUTTON_WHEEL_DOWN if target.y > bounds.end.y - 12 else MOUSE_BUTTON_WHEEL_UP
				wheel.pressed = true
				Input.parse_input_event(wheel)
				await process_frame
				wheel.pressed = false
				Input.parse_input_event(wheel)
				await wait_frames(4)
		ancestor = ancestor.get_parent()
	await wait_frames(4)
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
	await wait_frames(3)

func button(caption: String, parent: Node = null) -> Button:
	var root_node := parent if parent != null else scene
	for candidate in root_node.find_children("*", "Button", true, false):
		var b := candidate as Button
		var shown_text := TranslationServer.translate(b.text)
		var shown_tip := TranslationServer.translate(b.tooltip_text)
		if (shown_text == caption or shown_tip == caption or shown_tip.begins_with(caption + "；")) and b.is_visible_in_tree() and not b.disabled:
			return b
	return null

func click_named(caption: String, parent: Node = null) -> bool:
	var b := button(caption, parent)
	if not check(b != null, "visible enabled button: " + caption):
		note("UI STATE mode=" + scene.mode + " disputed=" + str(scene.disputed) + " validation=" + str(scene._replay_submission_validation()))
		return false
	await click(b)
	return true

func select_trace(fragment: String) -> bool:
	for attempt in 6:
		var title := scene.modal.find_child("TraceTitle", true, false) as Label
		if title != null and title.is_visible_in_tree() and title.text.contains(fragment): return true
		if not await click_named("下一条批注", scene.modal): return false
	return check(false, "trace reached by annotation arrows: " + fragment)

func screenshot(name: String, wait_for_dialogue_text := true) -> void:
	await wait_frames(3)
	if OS.get_cmdline_user_args().has("--no-screenshots"):
		return
	if wait_for_dialogue_text and scene.dialogue.active and scene.dialogue.panel.visible:
		for frame in 1500:
			if not scene.dialogue.busy and scene.dialogue.body.visible_characters < 0: break
			await process_frame
		if not check(not scene.dialogue.busy and scene.dialogue.body.visible_characters < 0, "dialogue text finishes before screenshot: " + name): return
	await RenderingServer.frame_post_draw
	var path := OUT + "full-playthrough-" + name + ".png"
	if not check(root.get_texture().get_image().save_png(path) == OK, "screenshot saved: " + name): return
	note("SCREENSHOT " + path)

func wait_dialogue_ready() -> bool:
	for frame in 6000:
		if not scene.dialogue.active or not scene.dialogue.busy or scene.name_entry.visible: return true
		await physics_frame
	return check(false, "dialogue action completes within 6000 physics frames")

func capture_dialogue_moment() -> void:
	if scene.dialogue.line == null: return
	var text: String = scene.dialogue.line.text
	if text.begins_with("妈的！你们房间里翻出行李了吗") or text.begins_with("把两组查到的东西摊开"):
		for pair in [["工程师", "Engineer"], ["厨师", "Chef"], ["音乐家", "Musician"]]:
			check(not scene.actors[pair[0]].travelling and scene.actors[pair[0]].position.distance_to(scene.get_node("MeetingSpots/" + pair[1]).position) < 28, "companion at the table before reporting: " + pair[0])
	if scene.captain and text.begins_with("机柄掣动，气脉通畅"):
		for actor in scene.actors.values():
			check(not actor.travelling and actor.position.distance_to(scene.sites.transmitter.point) < 115, "party present for copper-mouth defense: " + actor.name)
	var moments := {
		"先别急着装聋作哑": "cook-questioning",
		"那我受邀之前的身份呢": "detective-identity-questioning",
		"妈的！你们房间里翻出行李了吗": "first-regrouping",
		"机柄掣动，气脉通畅": "transmitter-defense",
	}
	for prefix in moments:
		if prefix == "机柄掣动，气脉通畅" and not scene.captain: continue
		if text.begins_with(prefix) and not captured_dialogue.has(prefix):
			captured_dialogue[prefix] = true
			await screenshot(moments[prefix])

func close_detail() -> void:
	if scene.modal_open:
		await tap(KEY_ESCAPE)

func finish_dialogue(max_taps := 35) -> bool:
	for i in max_taps:
		if not await wait_dialogue_ready(): return false
		await capture_dialogue_moment()
		if not arrival_captured and scene.dialogue.line != null and scene.dialogue.line.text == "又出来一个。":
			arrival_captured = true
			await screenshot("arrival-discussion")
		if scene.name_entry.visible:
			await type_text("侦探")
			if not check(scene.name_entry.confirm.disabled and scene.name_entry.error_label.text == TranslationServer.translate("FA_UI_353"), "reserved detective name is rejected through keyboard input"): return false
			await key(KEY_CTRL, true)
			await tap(KEY_A)
			await key(KEY_CTRL, false)
			await tap(KEY_BACKSPACE)
			await type_text("林林林林林林林林林林林林林林林林林")
			if not check(scene.name_entry.confirm.disabled and scene.name_entry.error_label.text == TranslationServer.translate("FA_UI_352"), "17-character name is rejected through keyboard input"): return false
			await key(KEY_CTRL, true)
			await tap(KEY_A)
			await key(KEY_CTRL, false)
			await tap(KEY_BACKSPACE)
			await tap(KEY_SPACE, 0x20)
			await tap(KEY_NONE, 0x6797)
			await tap(KEY_SPACE, 0x20)
			await screenshot("name-entry")
			await click(scene.name_entry.confirm)
			if not await wait_dialogue_ready(): return false
			if not check(scene.player_name == "林" and not scene.name_entry.visible, "name confirmed through keyboard and mouse"):
				return false
			if not check(scene.dialogue.body.text.contains("林"), "confirmed name appears in the next dialogue line"):
				return false
			if not check(not scene.dialogue.history.any(func(entry: String): return entry.begins_with("侦探\n") or entry.begins_with("侦探（心声）\n")), "pre-naming history entries also use the confirmed name"):
				return false
			continue
		if not scene.dialogue.active:
			return true
		if scene.dialogue.choices.is_visible_in_tree() and scene.dialogue.choices.get_child_count() > 0:
			break
		if scene.dialogue.next_button.is_visible_in_tree():
			await click(scene.dialogue.next_button)
		else:
			await click(scene.dialogue.panel)
	note("DIALOGUE STUCK text=" + str(scene.dialogue.line.text if scene.dialogue.line else "") + " choices=" + str(scene.dialogue.choices.visible) + " buttons=" + str(scene.dialogue.choices.get_child_count()))
	await screenshot("dialogue-stuck")
	return check(not scene.dialogue.active, "dialogue closes after keyboard continue")

func reveal_choices(max_taps := 40) -> bool:
	for i in max_taps:
		await wait_frames(2)
		if not await wait_dialogue_ready(): return false
		await capture_dialogue_moment()
		if scene.dialogue.active and scene.dialogue.choices.is_visible_in_tree() and scene.dialogue.choices.get_child_count() > 0:
			return true
		if not scene.dialogue.active: break
		if scene.dialogue.next_button.is_visible_in_tree():
			await click(scene.dialogue.next_button)
		else:
			await click(scene.dialogue.panel)
	return check(false, "dialogue reaches its choice menu")

func clear_point(point: Vector2) -> bool:
	var shape := RectangleShape2D.new()
	shape.size = Vector2(16, 12)
	var query := PhysicsShapeQueryParameters2D.new()
	query.shape = shape
	query.transform = Transform2D(0.0, point + Vector2(0, -6))
	query.collision_mask = 3
	query.exclude = [scene.player.get_rid()]
	return scene.get_world_2d().direct_space_state.intersect_shape(query, 1).is_empty()

func route_to_site(id: String) -> PackedVector2Array:
	var site: Dictionary = scene.sites[id]
	var candidates: Array[Vector2] = []
	var target: Vector2 = scene.actors[site.title].position if scene.actors.has(site.title) else site.point
	for radius in [24, 32, 40]:
		for offset in [Vector2(0,-1),Vector2(-1,0),Vector2(1,0),Vector2(0,1),Vector2(-1,-1),Vector2(1,-1),Vector2(-1,1),Vector2(1,1)]:
			var point: Vector2 = (target + offset.normalized() * radius).round()
			if scene.floors.has(Vector2i(point / 32.0)) and clear_point(point):
				candidates.append(point)
	candidates.sort_custom(func(a, b): return scene.player.position.distance_squared_to(a) < scene.player.position.distance_squared_to(b))
	for point in candidates:
		var path: PackedVector2Array = scene._route(scene.player.position, point)
		if path.size() > 0 and path[path.size() - 1].distance_to(target) < site.reach - 4.0:
			# Production routing includes current actor collision footprints.
			return path
	return PackedVector2Array()

func walk_to(point: Vector2) -> bool:
	var dx: float = point.x - scene.player.position.x
	var dy: float = point.y - scene.player.position.y
	if absf(dx) < 3 and absf(dy) < 3:
		return true
	var code: Key = (KEY_D if dx > 0 else KEY_A) if absf(dx) > absf(dy) else (KEY_S if dy > 0 else KEY_W)
	var target_axis := point.x if code in [KEY_A, KEY_D] else point.y
	await key(code, true)
	var previous: Vector2 = scene.player.position
	var stalled_frames := 0
	for i in 180:
		await physics_frame
		var axis: float = scene.player.position.x if code in [KEY_A, KEY_D] else scene.player.position.y
		if absf(axis - target_axis) <= 2.5 or (code in [KEY_D, KEY_S] and axis > target_axis) or (code in [KEY_A, KEY_W] and axis < target_axis):
			break
		stalled_frames = stalled_frames + 1 if previous.distance_to(scene.player.position) < 0.5 else 0
		if stalled_frames >= 30: break
		previous = scene.player.position
	await key(code, false)
	return scene.player.position.distance_to(point) < 7.0

func walk_via(point: Vector2) -> bool:
	var path: PackedVector2Array = scene._route(scene.player.position, point)
	if path.is_empty(): return false
	for waypoint in path.slice(1):
		if not await walk_to(waypoint):
			if scene.dialogue.active: return true
			note("walk_via stopped at " + str(scene.player.position) + " aiming " + str(waypoint))
			return false
	return true

func travel(id: String) -> bool:
	# Moving actors can occupy a waypoint after a route is planned. Steer around
	# their new positions with physical keys, without changing scene state.
	for attempt in 8:
		if scene.reachable.has(id): return true
		var path := route_to_site(id)
		if not check(path.size() > 0, "route exists to " + id): return false
		var blocked := false
		for waypoint in path.slice(1):
			if not await walk_to(waypoint):
				if scene.reachable.has(id): return true
				if scene.dialogue.active:
					note("walking interrupted by arrival dialogue toward " + id)
					return true
				blocked = true
				note("replan physical approach to " + id + " at " + str(scene.player.position))
				break
		await wait_frames(3)
		if not blocked: break
	return check(scene.reachable.has(id), "interaction in reach: " + id + " at " + str(scene.player.position))

func interact(id: String) -> bool:
	if not await travel(id):
		return false
	if scene.investigation_phase == "paired":
		var partner: CharacterBody2D = scene.actors[scene._paired_actor()]
		for i in 900:
			if partner.position.distance_to(scene.player.position) <= 90: break
			await physics_frame
		if not check(partner.position.distance_to(scene.player.position) <= 96, "companion reached inspection " + id + " at " + str(partner.position) + " detective=" + str(scene.player.position) + " travelling=" + str(partner.travelling) + " target=" + str(partner.target) + " remaining_waypoints=" + str(partner.path.size() - partner.waypoint)): return false
	for i in scene.reachable.size():
		if scene.nearest == id:
			break
		await tap(KEY_TAB)
	if not check(scene.nearest == id, "select " + id + " with Tab"):
		return false
	await tap(KEY_E)
	return true

func inspect(id: String) -> bool:
	if not await interact(id): return false
	var acquired: bool = scene.evidence.has(id)
	await close_detail()
	return check(acquired, "evidence collected: " + id)

func type_text(content: String) -> void:
	for ch in content:
		if ch == "\n":
			await tap(KEY_ENTER)
		else:
			await tap(KEY_NONE, ch.unicode_at(0))

func run() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--companion="): companion = arg.trim_prefix("--companion=")
	if not check(companion in ["engineer", "cook", "musician"], "supported companion: " + companion): finish(); return
	scene = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await wait_frames(8)
	await screenshot("01-opening")
	if not await finish_dialogue(): finish(); return
	# A movement key stands the detective up; all following travel is physical input.
	await key(KEY_S, true)
	for i in 8: await physics_frame
	await key(KEY_S, false)
	if not check(not scene.player.seated, "woke by movement key"): finish(); return
	if not check(await walk_to(Vector2(151, 230)), "move clear of opening bed at " + str(scene.player.position)): finish(); return
	# Enter through the actual study doorway, using keyboard movement throughout.
	if not check(await walk_via(Vector2(256, 520)), "walk to activity-hall doorway"): finish(); return
	if not check(not scene.dialogue.active, "arrival waits until the doorway threshold is crossed"): finish(); return
	await screenshot("before-arrival")
	await key(KEY_S, true)
	for frame in 180:
		if scene.dialogue.active: break
		await physics_frame
	await key(KEY_S, false)
	if not check(scene.dialogue.active, "physical doorway entry starts arrival dialogue"): finish(); return
	await screenshot("arrival-cook-turn", false)
	for frame in 18: await physics_frame
	await screenshot("arrival-engineer-turn", false)
	for frame in 18: await physics_frame
	await screenshot("arrival-musician-turn", false)
	if not await wait_dialogue_ready(): finish(); return
	if not check(scene.player.position.distance_to(scene.get_node("MeetingSpots/Detective").position) < 28.0, "arrival stages detective at the table after successive reactions"): finish(); return
	if not await finish_dialogue(500): finish(); return
	if not check(scene.broadcast_heard and scene.investigation_phase == "dorm_search", "hall arrival plays broadcast and assigns private room search"): finish(); return
	note("PHASE dorm_search after automatic hall broadcast")
	await screenshot("04-broadcast")
	for id in ["window_0", "bed_0", "drawer", "paper"]:
		if not await inspect(id): finish(); return
		if scene.dialogue.active and not await finish_dialogue(): finish(); return
	if not check(scene.investigation_phase == "regroup", "four personal inspections advance to regroup"): finish(); return
	note("PHASE regroup after own-room evidence")
	await screenshot("02-bedroom-evidence")
	for i in 900:
		if scene.actors.values().all(func(actor): return not actor.travelling): break
		await physics_frame
	for point in [Vector2(160, 760), Vector2(240, 760)]:
		if not check(await walk_via(point), "approach regroup from below table at " + str(point)): finish(); return
	if not await interact("meeting"): finish(); return
	if not await reveal_choices(90): finish(); return
	var pair_caption: String = {"engineer": "跟随工程师前往门厅，深入勘验隔离门与地面痕迹。", "cook": "跟随主厨前往备餐间，深入勘验生活器具与水渍抹布。", "musician": "跟随音乐家前往研习室，深入勘验案台图册与声学构造。"}[companion]
	if not await click_named(pair_caption, scene.dialogue): finish(); return
	if not await finish_dialogue(): finish(); return
	if not check(scene.investigation_phase == "paired" and scene.paired_companion == companion, "selected " + companion + " through dialogue choice"): finish(); return
	note("PHASE paired " + companion)
	for id in scene.PAIR_SITES[companion]:
		if not await interact(id): finish(); return
		if scene.dialogue.active:
			if not await reveal_choices(): finish(); return
			var leave_caption := "松开拉柄" if id == "transmitter" else "离开门边"
			if not await click_named(leave_caption, scene.dialogue): finish(); return
			if not await finish_dialogue(): finish(); return
		await close_detail()
		if not check(scene.deep_inspections.has(id), "paired physical inspection recorded: " + id): finish(); return
	await screenshot("05-hall-evidence")
	if not await interact("meeting"): finish(); return
	if not await finish_dialogue(): finish(); return
	if not check(scene.meeting_done and scene.investigation_phase == "discussion", "findings exchanged and round-table discussion begins"): finish(); return
	var reported_site := "trace" if companion == "cook" else "wash"
	if not check(scene.evidence.has(reported_site) and scene.evidence_sources.get(reported_site) == "同伴勘验后转述", "other pair's inspection retains testimony source"): finish(); return
	await screenshot("06-meeting")
	await close_detail()
	await tap(KEY_F)
	if not await finish_dialogue(): finish(); return
	var editor := scene.modal.find_child("DraftEditor", true, false) as TextEdit
	if not check(editor != null, "draft editor opened with F"): finish(); return
	var drafts := [
		"侦探走到隔离门。\n每当侦探看见热烟后，优先侦探取下木楔。\n每当侦探看见木楔撤下后，侦探用纸包起木楔。",
		"工程师走到隔离门。\n每当工程师看见隔离门关闭后，工程师插入生活翼内销。",
		"厨师走到隔离门。\n每当厨师看见止挡座移开后，厨师用湿布擦拭地面。",
		"音乐家用木楔撑开隔离门。\n每当音乐家看见热烟后，音乐家移开铁止挡座。",
		"每当门外的人看见隔离门关闭后，门外的人落下主楼侧横闩。",
	]
	for i in drafts.size():
		await click(editor)
		await type_text(drafts[i])
		if i < drafts.size() - 1:
			var next_person := scene.modal.find_child("NextPerson", true, false) as Button
			if not check(next_person != null, "next person's draft page available"): finish(); return
			await click(next_person)
	await screenshot("07-draft")
	if not await click_named("从头重演", scene.modal): finish(); return
	if not check(not scene.sim_result.is_empty() and scene.sim_result.get("conflicts", []).is_empty(), "Core replay produces compatible result"): finish(); return
	await screenshot("08-replay")
	# The manuscript uses Play and pauses at significant traces.
	var play := scene.modal.find_child("MindNotebook", true, false).get_node("Transport/Play") as Button
	for i in 80:
		if scene.replay_index >= scene.replay_events.size(): break
		if not scene.replay_playing:
			if not check(play.is_visible_in_tree() and not play.disabled, "replay Play control is available"): finish(); return
			await click(play)
		await wait_frames(50)
	if not check(scene.replay_index >= scene.replay_events.size(), "replay reaches its final state through Play"): finish(); return
	if not check(scene.sim_result.get("failures", []).is_empty(), "persistent-rule replay has no blocked actions"): finish(); return
	for trace_id in ["door-mask", "paper-inner-transfer", "inner-bolt", "outer-bar", "wipe-after-stop"]:
		if not check(scene.sim_result.get("ids", []).has(trace_id), "persistent-rule replay produces " + trace_id): finish(); return
	note("REPLAY complete events=" + str(scene.replay_events.size()) + " ids=" + str(scene.sim_result.get("ids", [])))
	await screenshot("09-replay-end")
	await close_detail()
	if not await interact("meeting"): finish(); return
	for pair in [["escape", "trace"], ["betrayal", "door"], ["fifth_person", "wash"]]:
		var claim: Dictionary = {}
		for candidate in scene.INVESTIGATION_CONTENT.meeting_hypotheses:
			if candidate.id == pair[0]: claim = candidate
		if not await click_named("出示证物 · " + TranslationServer.translate(claim.label_id), scene.modal): finish(); return
		if not await click_named(TranslationServer.translate(scene.sites[pair[1]].title_id), scene.modal): finish(); return
		if not await finish_dialogue(): finish(); return
		if not check(scene.disputed[pair[0]], "evidence response accepted: " + pair[0]): finish(); return
	if not check(scene.captain and scene.disputed.values().all(func(value): return value), "three claims answered using trace, door and wash after replay"): finish(); return
	await screenshot("10-evidence-response")
	await close_detail()
	# The companions remain gathered around the meeting table. Walk below and
	# around that cluster with keyboard input before asking the pathfinder again.
	if not await interact("transmitter"): finish(); return
	# Dialogue choice is selected by an actual mouse click.
	for i in 4:
		if not await wait_dialogue_ready(): finish(); return
		await capture_dialogue_moment()
		if scene.dialogue.choices.is_visible_in_tree() and scene.dialogue.choices.get_child_count() > 0: break
		await click(scene.dialogue.panel)
	if not check(scene.dialogue.choices.is_visible_in_tree() and scene.dialogue.choices.get_child_count() > 0, "transmitter choices revealed"): finish(); return
	if not await click_named("提交论证", scene.dialogue): finish(); return
	if not await finish_dialogue(): finish(); return
	if not check(scene.mode == "answer", "answer board opened through transmitter"): finish(); return
	await screenshot("11-answer")
	# Choose the unlocked answer words via the three native MenuButton popups.
	var desired := ["撑开窄缝探听主楼险情", "烟灰涌入后撤楔合门避险", "生活翼清理与内销 · 主楼侧另落外闩"]
	for index in 3:
		var menus: Array[Node] = scene.modal.find_children("*", "MenuButton", true, false)
		if not check(menus.size() == 3, "three answer menus"): finish(); return
		var menu := menus[index] as MenuButton
		await click(menu)
		await wait_frames(2)
		var popup := menu.get_popup()
		var item_index := -1
		for n in popup.item_count:
			if popup.get_item_text(n) == desired[index]: item_index = n
		if not check(item_index >= 0, "answer option unlocked: " + desired[index]): finish(); return
		var at: Vector2 = Vector2(popup.position) + Vector2(popup.size.x / 2.0, 12 + item_index * 30)
		var motion := InputEventMouseMotion.new(); motion.position = at; Input.parse_input_event(motion)
		var event := InputEventMouseButton.new(); event.button_index = MOUSE_BUTTON_LEFT; event.position = at; event.pressed = true; Input.parse_input_event(event)
		await process_frame
		event.pressed = false; Input.parse_input_event(event)
		await wait_frames(3)
	if not check(not scene.answer_slots.values().has(""), "all three answer words selected"): finish(); return
	if not await click_named("提交论证", scene.modal): finish(); return
	if not await finish_dialogue(): finish(); return
	if not scene.ended:
		note("ANSWER STATE mode=" + scene.mode + " slots=" + str(scene.answer_slots) + " connected=" + str(scene.transmitter_connected) + " position=" + str(scene.player.position) + " dialogue=" + str(scene.dialogue.line.text if scene.dialogue.line else ""))
		await screenshot("ending-blocked")
	if not check(scene.ended, "ending reached by player input"): finish(); return
	var gate: Node2D = scene.scene_items.get_node("MechanicalIsolationDoor")
	if not check(gate.get_node("DoorGap").visible and not gate.get_node("DoorFace").visible and not scene.bolt_visual.visible, "contract resolution visibly opens the isolation door"): finish(); return
	note("ENDING submitted through transmitter; contract visibly opens the isolation door")
	await screenshot("12-ending")
	await finish()

func finish() -> void:
	var result := "PLAYTHROUGH RESULT: " + ("FAIL" if failed else "PASS")
	log_lines.append(result)
	var log_file := FileAccess.open(PLAYTHROUGH_LOG, FileAccess.WRITE)
	if log_file == null:
		check(false, "playthrough log opened: " + PLAYTHROUGH_LOG + " error=" + str(FileAccess.get_open_error()))
	else:
		log_file.store_string("\n".join(log_lines) + "\n")
		log_file.close()
	print("PLAYTHROUGH RESULT: ", "FAIL" if failed else "PASS", " log=", PLAYTHROUGH_LOG)
	quit(1 if failed else 0)

