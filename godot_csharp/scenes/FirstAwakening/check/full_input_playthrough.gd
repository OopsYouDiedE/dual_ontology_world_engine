extends SceneTree
## End-to-end regression through simulated keyboard and mouse input only.
## Scene state is read for assertions and routing; progression is never assigned.

const OUT := "res://../artifacts/first-awakening/"
var scene: Node2D
var failed := false
var log_lines: Array[String] = []

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
	Input.parse_input_event(event)
	await process_frame

func tap(code: Key, unicode := 0) -> void:
	await key(code, true, unicode)
	await key(code, false, unicode)
	await wait_frames(2)

func click(control: Control) -> void:
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
		return false
	await click(b)
	return true

func select_trace(fragment: String) -> bool:
	for attempt in 6:
		var title := scene.modal.find_child("TraceTitle", true, false) as Label
		if title != null and title.is_visible_in_tree() and title.text.contains(fragment): return true
		if not await click_named("下一条批注", scene.modal): return false
	return check(false, "trace reached by annotation arrows: " + fragment)

func screenshot(name: String) -> void:
	await wait_frames(3)
	if OS.get_cmdline_user_args().has("--no-screenshots"):
		return
	await RenderingServer.frame_post_draw
	var path := OUT + "full-playthrough-" + name + ".png"
	root.get_texture().get_image().save_png(path)
	note("SCREENSHOT " + path)

func close_detail() -> void:
	if scene.modal_open:
		await tap(KEY_ESCAPE)

func finish_dialogue(max_taps := 35) -> bool:
	for i in max_taps:
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
	for i in 50:
		await physics_frame
		var axis: float = scene.player.position.x if code in [KEY_A, KEY_D] else scene.player.position.y
		if absf(axis - target_axis) <= 2.5 or (code in [KEY_D, KEY_S] and axis > target_axis) or (code in [KEY_A, KEY_W] and axis < target_axis):
			break
		if i > 12 and previous.distance_to(scene.player.position) < 0.5:
			break
		previous = scene.player.position
	await key(code, false)
	return scene.player.position.distance_to(point) < 7.0

func travel(id: String) -> bool:
	var path := route_to_site(id)
	if not check(path.size() > 0, "route exists to " + id):
		return false
	for waypoint in path.slice(1):
		if not await walk_to(waypoint):
			return check(false, "walk to " + id + " blocked at " + str(scene.player.position) + " aiming " + str(waypoint))
	await wait_frames(3)
	return check(scene.reachable.has(id), "interaction in reach: " + id + " at " + str(scene.player.position))

func interact(id: String) -> bool:
	if not await travel(id):
		return false
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
	for id in ["drawer", "paper", "room_0"]:
		if not await inspect(id): finish(); return
	await screenshot("02-bedroom-evidence")
	if not await interact("engineer"): finish(); return
	if not await finish_dialogue(): finish(); return
	for id in ["study", "tear"]:
		if not await inspect(id): finish(); return
	if not await interact("transmitter"): finish(); return
	await screenshot("03-transmitter-rules")
	if not await reveal_choices(): finish(); return
	if not check(scene.broadcast_heard, "heard broadcast through interaction"): finish(); return
	if not check(scene.transmitter_rules and scene.transmitter_connected, "first pull continues from rules into connected broadcast and choices"): finish(); return
	if not await click_named("松开拉柄", scene.dialogue): finish(); return
	if not await finish_dialogue(): finish(); return
	if not check(not scene.transmitter_connected, "releasing the handle disconnects and returns control"): finish(); return
	await screenshot("04-broadcast")
	# The three companions physically cross the narrow public-room thresholds.
	for i in 1200:
		await physics_frame
		if not scene.actors["工程师"].travelling and not scene.actors["厨师"].travelling and not scene.actors["音乐家"].travelling:
			break
	note("PARTY engineer=" + str(scene.actors["工程师"].position) + " chef=" + str(scene.actors["厨师"].position) + " musician=" + str(scene.actors["音乐家"].position))
	for id in ["rations", "wash"]:
		if not await inspect(id): finish(); return
	if not await interact("door"): finish(); return
	if not await reveal_choices(): finish(); return
	if not await click_named("拔出内销", scene.dialogue): finish(); return
	if not await reveal_choices(): finish(); return
	if not check(not scene.inner_bolt, "door dialogue withdraws the bolt and stays interactive"): finish(); return
	if not await click_named("推门", scene.dialogue): finish(); return
	if not await reveal_choices(): finish(); return
	if not await click_named("掀开检视盖", scene.dialogue): finish(); return
	if not await reveal_choices(): finish(); return
	if not await click_named("检查铰链与配重", scene.dialogue): finish(); return
	if not await reveal_choices(): finish(); return
	if not check(scene.evidence.has("slit") and scene.evidence.has("hinge") and scene.evidence.has("weight"), "continuous door dialogue collects observed mechanisms"): finish(); return
	if not await click_named("离开门边", scene.dialogue): finish(); return
	if not await finish_dialogue(): finish(); return
	await screenshot("05-hall-evidence")
	if not await interact("meeting"): finish(); return
	if not await finish_dialogue(): finish(); return
	await screenshot("06-meeting")
	for pair in [["临时牢房说", "抽屉内的新旧补纸"], ["外敌顶门说", "铰链、石止口和自闭配重"], ["撬门越狱说", "包纸撕口与接触面的灰迹"]]:
		var fragment: String = pair[0]
		var target: Button = null
		for b in scene.modal.find_children("*", "Button", true, false):
			if b.text.contains(fragment) and not b.disabled: target = b; break
		if not check(target != null, "meeting rebuttal available: " + fragment): finish(); return
		await click(target)
		if not await reveal_choices(): finish(); return
		if fragment == "临时牢房说":
			if not await click_named("门上的编号", scene.dialogue): finish(); return
			if not await reveal_choices(): finish(); return
			if not check(not scene.disputed.prison, "irrelevant evidence does not win the rebuttal"): finish(); return
		if not await click_named(pair[1], scene.dialogue): finish(); return
		if not await finish_dialogue(): finish(); return
	if not check(scene.captain, "detective nominated after three rebuttals"): finish(); return
	await close_detail()
	await tap(KEY_F)
	if not await finish_dialogue(): finish(); return
	var editor := scene.modal.find_child("DraftEditor", true, false) as TextEdit
	if not check(editor != null, "draft editor opened with F"): finish(); return
	var drafts := [
		"走到隔离门。\n侦探看见热烟后，撤下木楔。\n用纸包起木楔。",
		"走到隔离门。\n工程师看见木楔撤下后，插入生活翼内销。",
		"走到隔离门。\n厨师看见止挡座移开后，用湿布擦拭地面。",
		"用木楔撑开隔离门。\n音乐家看见热烟后，移开铁止挡座。",
		"门外的人看见隔离门关闭后，落下主楼侧横闩。",
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
	await screenshot("09-replay-end")
	# Verification may require returning to the physical evidence location.
	for pair in [["门底遮蔽", "门底"], ["纸内转印", "包纸"], ["双套锁止", "锁止"]]:
		var title: String = pair[0]
		if scene.sim_checks.has(title): continue
		if not await select_trace(pair[1]): finish(); return
		var action: Button = button("核对 · 十分钟", scene.modal)
		if action == null: action = button("标记位置", scene.modal)
		if not check(action != null, "verification control visible: " + title): finish(); return
		await click(action)
		if scene.pending_inspections.size() > 0:
			var site_id: String = scene.pending_inspections.keys()[0]
			await close_detail()
			if not await inspect(site_id): finish(); return
			await tap(KEY_F)
		else:
			await close_detail()
			await tap(KEY_F)
		if not check(scene.sim_checks.has(title), "verified trace: " + title): finish(); return
	await screenshot("10-three-checks")
	await close_detail()
	if not await interact("transmitter"): finish(); return
	# Dialogue choice is selected by an actual mouse click.
	for i in 4:
		if scene.dialogue.choices.is_visible_in_tree() and scene.dialogue.choices.get_child_count() > 0: break
		await click(scene.dialogue.panel)
	if not check(scene.dialogue.choices.is_visible_in_tree() and scene.dialogue.choices.get_child_count() > 0, "transmitter choices revealed"): finish(); return
	if not await click_named("提交论证", scene.dialogue): finish(); return
	if not await finish_dialogue(): finish(); return
	if not check(scene.mode == "answer", "answer board opened through transmitter"): finish(); return
	await screenshot("11-answer")
	# Choose the unlocked answer words via the three native MenuButton popups.
	var desired := ["长期生活研习", "先撑门进灰 → 撤楔合门 → 沾灰后包纸", "生活翼内销 + 主楼侧外闩"]
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
	if not check(scene.ended, "ending reached by player input"): finish(); return
	var gate: Node2D = scene.scene_items.get_node("MechanicalIsolationDoor")
	if not check(gate.get_node("DoorGap").visible and not gate.get_node("DoorFace").visible and not scene.bolt_visual.visible, "contract resolution visibly opens the isolation door"): finish(); return
	await screenshot("12-ending")
	await finish()

func finish() -> void:
	var path := OUT + "full-playthrough-result.txt"
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string("\n".join(log_lines) + "\n")
	file.close()
	print("PLAYTHROUGH RESULT: ", "FAIL" if failed else "PASS")
	quit(1 if failed else 0)

