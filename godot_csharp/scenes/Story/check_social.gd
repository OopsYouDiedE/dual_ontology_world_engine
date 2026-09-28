extends SceneTree
## Exercises both life-story rooms through the same player input path as the game.

var failures: Array[String] = []
var room: Node2D
var room_scene := ""

func _initialize() -> void:
	call_deferred("run")

func expect(value: bool, message: String) -> void:
	if not value:
		failures.append(message)
		push_error(message)

func facing_toward(origin: Vector2, target: Vector2) -> String:
	var delta := target - origin
	if absf(delta.x) > absf(delta.y):
		return "right" if delta.x > 0.0 else "left"
	return "front" if delta.y > 0.0 else "back"

func check_line_presentation() -> void:
	if room.cast.is_empty() or not room.dialogue.active:
		return
	var speaker: Node2D = room.player if room.dialogue.line.character == "侦探" else null
	for actor in room.cast:
		if actor.get_meta("display_name", "") == room.dialogue.line.character:
			speaker = actor
			break
	if speaker == null:
		return
	if speaker != room.player:
		expect(room.player.facing == facing_toward(room.player.position, speaker.position), room_scene + ": 玩家对白时没有面向发言者")
		if not speaker.get_meta("seated", false):
			expect(speaker.facing == facing_toward(speaker.position, room.player.position), room_scene + ": 站立发言者没有面向玩家")
	for actor in room.cast:
		if actor.get_meta("seated", false):
			var direction: String = actor.get_meta("seated_facing", "back")
			var height := float(actor.get_meta("seat_height", 0.0))
			expect(actor.seated, room_scene + ": 坐姿角色被对白动作带回站立")
			expect(actor.position.distance_to(actor.get_meta("start_position", actor.position)) < 0.1, room_scene + ": 坐姿角色在对白中离开座位锚点")
			expect(actor.sprite.animation.begins_with("sit_"), room_scene + ": 坐姿角色没有保持坐姿动画")
			expect(not actor.sprite.is_playing(), room_scene + ": 坐姿定格帧仍在播放")
			expect(is_equal_approx(actor.sprite.position.y, -23.0 - height), room_scene + ": 坐姿精灵偏移未匹配座垫高度")
			expect(actor.facing == direction, room_scene + ": 坐姿角色朝向被对白动作改写")
		elif room.conversation and actor != speaker:
			var expected_facing := facing_toward(actor.position, speaker.position)
			expect(actor.facing == expected_facing, room_scene + ": 站立听众" + str(actor.get_meta("display_name", actor.name)) + "没有面向" + str(speaker.get_meta("display_name", speaker.name)) + "；实际=" + actor.facing + "，预期=" + expected_facing)

func key(code: Key, held := 0.08) -> void:
	var down := InputEventKey.new()
	down.keycode = code
	down.physical_keycode = code
	down.pressed = true
	Input.parse_input_event(down)
	await create_timer(held).timeout
	var up := InputEventKey.new()
	up.keycode = code
	up.physical_keycode = code
	up.pressed = false
	Input.parse_input_event(up)
	await process_frame

func walk_to(target: Vector2, tolerance: float = 6.0, timeout: float = 8.0) -> bool:
	var deadline := Time.get_ticks_msec() + int(timeout * 1000.0)
	var last_progress := Time.get_ticks_msec()
	while room.player.position.distance_to(target) > tolerance and Time.get_ticks_msec() < deadline:
		var before: Vector2 = room.player.position
		var delta := target - before
		var direction: Key = KEY_D if absf(delta.x) >= absf(delta.y) and delta.x > 0.0 else KEY_A
		if absf(delta.y) > absf(delta.x):
			direction = KEY_S if delta.y > 0.0 else KEY_W
		await key(direction, 0.02 if delta.length() < 12.0 else 0.08)
		if room.player.position.distance_to(before) > 0.1:
			last_progress = Time.get_ticks_msec()
		elif Time.get_ticks_msec() - last_progress > 650:
			return false
	return room.player.position.distance_to(target) <= tolerance

func walk_route(waypoints: Array[Vector2], label: String) -> bool:
	for waypoint in waypoints:
		var reached := await walk_to(waypoint)
		if not reached:
			expect(false, label + ": waypoint不可达 " + str(waypoint) + "，当前位置 " + str(room.player.position))
			return false
	return true

func click(button: Button) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = button.global_position + button.size * 0.5
	event.global_position = event.position
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event)
	await process_frame

func wait_line() -> bool:
	var deadline := Time.get_ticks_msec() + 12000
	while room.dialogue.active and (room.dialogue.busy or room.dialogue.body.visible_characters >= 0) and Time.get_ticks_msec() < deadline:
		await process_frame
		if not room.dialogue.busy and room.dialogue.body.visible_characters >= 0:
			var remaining: int = room.dialogue.body.get_total_character_count() - room.dialogue.body.visible_characters
			if remaining > 0:
				await create_timer(float(remaining) / 32.0 + 0.06).timeout
	return room.dialogue.active and not room.dialogue.busy

func finish_story(choice_index: int, require_choice := false) -> void:
	var chosen := false
	var steps := 0
	while room.dialogue.active and steps < 32:
		steps += 1
		expect(await wait_line(), room_scene + ": 对话行未能显示")
		if not room.dialogue.active or room.dialogue.busy:
			break
		check_line_presentation()
		if room.dialogue.body.visible_characters >= 0:
			await key(KEY_SPACE)
		if not room.dialogue.line.responses.is_empty():
			var buttons: Array[Node] = room.dialogue.choices.get_children()
			expect(buttons.size() > choice_index, room_scene + ": 分支选项缺失")
			if buttons.size() > choice_index:
				await click(buttons[choice_index])
				chosen = true
		else:
			await key(KEY_SPACE)
	await process_frame
	expect(steps < 32, room_scene + ": 对话没有结束")
	if require_choice:
		expect(chosen, room_scene + ": 没有触发分支按钮")
	expect(not room.dialogue.active, room_scene + ": 对话结束后仍占用输入")

func open_room(scene_path: String, label: String) -> void:
	room_scene = label
	room = load(scene_path).instantiate()
	root.add_child(room)
	current_scene = room
	await process_frame
	await process_frame
	expect(room.player != null and room.cast.size() > 0, label + ": 人物未初始化")

func close_room() -> void:
	room.queue_free()
	room = null
	await process_frame
	await process_frame

func walk_to_breakfast_chef() -> void:
	# Go around the west side of the dining table, then approach the chef from north.
	if not await walk_route([Vector2(168, 336), Vector2(168, 180)], "早餐：接近主厨"):
		return
	var reached_chef_side := await walk_to(Vector2(352, 180))
	expect(reached_chef_side, "早餐：主厨北侧接近点不可达，当前位置 " + str(room.player.position))
	expect(room.player.position.distance_to(room.cast[0].position) < 68.0, "早餐：主厨接近点不可达")
	if room.nearest != room.cast[0]:
		expect(false, "早餐：主厨不是最近的可交互对象")
		return
	await key(KEY_E)
	expect(room.dialogue.active, "早餐：E 未启动主厨对话")
	await finish_story(0)

func walk_to_breakfast_table() -> void:
	# Return west of the dining table, then pass below the chairs to its south edge.
	if not await walk_route([Vector2(184, 180), Vector2(184, 340), Vector2(320, 340)], "早餐：接近长桌"):
		return
	if not await walk_to(Vector2(320, 326)):
		expect(false, "早餐：长桌南侧接近点不可达，当前位置 " + str(room.player.position))
		return
	var table: Node2D = room.items.get_node("BreakfastTable")
	expect(room.player.position.distance_to(table.position) < 68.0, "早餐：长桌交互点不可达")
	if room.nearest != table:
		expect(false, "早餐：长桌不是最近的交互对象")
		return
	await key(KEY_E)
	expect(room.dialogue.active, "早餐：E 未启动长桌检查对白")
	await finish_story(0)

func test_history_and_movement(label: String) -> void:
	var start: Vector2 = room.player.position
	await key(KEY_D, 0.4)
	expect(room.player.position.x > start.x + 20.0, label + ": 对话后没有恢复移动")
	await key(KEY_H)
	expect(room.dialogue.history_panel.visible, label + ": H 没有打开回看")
	var held_scene := room.scene_file_path
	await key(KEY_F8)
	expect(room.scene_file_path == held_scene, label + ": 回看期间 F8 错误切场")
	var nav := room.get_node_or_null("DemoNavigation")
	if nav != null and nav.buttons.size() > 2:
		expect(nav.buttons[2].disabled, label + ": 回看期间娱乐室导航按钮未禁用")	
	var held: Vector2 = room.player.position
	await key(KEY_D, 0.25)
	expect(room.player.position.distance_to(held) < 1.0, label + ": 回看时移动未暂停")
	await key(KEY_H)
	expect(not room.dialogue.history_panel.visible, label + ": H 没有关闭回看")

func play_opening_branch(index: int, evidence: String, check_nav_lock := false) -> void:
	await key(KEY_R)
	expect(room.dialogue.active, room_scene + ": R 未开始或重演剧情")
	expect(room.player.position.distance_to(Vector2(320, 336)) < 1.0, room_scene + ": R 没有重置玩家站位")
	if check_nav_lock and room.dialogue.active:
		var held_scene := room.scene_file_path
		await key(KEY_F8)
		expect(room.scene_file_path == held_scene and room.dialogue.active, room_scene + ": 对白期间 F8 错误切场")
	await finish_story(index, true)
	var combined := "\n".join(room.dialogue.history)
	expect(combined.contains(evidence), room_scene + ": 所选分支内容未出现在回看记录")

func walk_to_parlor_poet() -> void:
	# Walk around the room's west edge, then approach the poet from south of the sofa.
	if not await walk_route([Vector2(184, 336), Vector2(184, 244), Vector2(320, 244)], "围谈：接近诗人"):
		return
	expect(room.player.position.distance_to(room.cast[0].position) < 68.0, "围谈：诗人接近点不可达")
	if room.nearest != room.cast[0]:
		expect(false, "围谈：诗人不是最近的可交互对象")
		return
	await key(KEY_E)
	expect(room.dialogue.active, "围谈：E 未启动诗人对话")
	await finish_story(0)

func walk_to_parlor_table() -> void:
	# The player stands south of the sofa, aligned with the table's open approach.
	if not await walk_to(Vector2(320, 255), 2.0):
		expect(false, "围谈：会客桌北侧接近点不可达，当前位置 " + str(room.player.position))
		return
	var table: Node2D = room.items.get_node("TeaTable")
	expect(room.player.position.distance_to(table.position) < 68.0, "围谈：会客桌交互点不可达")
	if room.nearest != table:
		expect(false, "围谈：会客桌不是最近的交互对象")
		return
	await key(KEY_E)
	expect(room.dialogue.active, "围谈：E 未启动会客桌检查对白")
	await finish_story(0)

func check_map_extent(label: String) -> void:
	var floor: TileMapLayer = room.decoration.get_node("StoneFloor")
	var used := floor.get_used_cells()
	expect(not used.is_empty(), label + ": 地面未生成")
	var left := INF
	var right := -INF
	for cell in used:
		left = minf(left, float(cell.x * 32))
		right = maxf(right, float((cell.x + 1) * 32))
	expect(left <= 32.0 and right >= 608.0, label + ": 房间地面未覆盖约定有效宽度")
	expect(room.collision_rects.any(func(r: Rect2) -> bool: return r.position == Vector2(32, 352) and r.size.x == 256.0), label + ": 左侧底边碰撞异常")
	expect(room.collision_rects.any(func(r: Rect2) -> bool: return r.position == Vector2(352, 352) and r.size.x == 256.0), label + ": 右侧底边碰撞异常")

func wait_for_scene(path: String, label: String) -> void:
	var deadline := Time.get_ticks_msec() + 5000
	while (current_scene == null or current_scene.scene_file_path != path) and Time.get_ticks_msec() < deadline:
		await process_frame
	expect(current_scene != null and current_scene.scene_file_path == path, label + ": 切场没有到达 " + path)
	if current_scene != null and current_scene.scene_file_path == path:
		room = current_scene
		room_scene = label
		await process_frame
		await process_frame

func switch_by_key(code: Key, path: String, label: String) -> void:
	await key(code)
	await wait_for_scene(path, label)

func exit_social_room_to_hall(label: String) -> void:
	var entrance: Node2D = room.items.get_node("Entrance")
	# The actual E interaction on the south exit should return to the central hall.
	if not await walk_to(entrance.position, 6.0, 8.0):
		expect(false, label + ": 南门waypoint不可达，当前位置 " + str(room.player.position))
		return
	await process_frame
	expect(room.nearest == entrance, label + ": 南门 E 交互点不可达")
	await key(KEY_E)
	await wait_for_scene("res://scenes/Story/CentralHall.tscn", label + " 南门返回中央厅")

func run() -> void:
	root.size = Vector2i(1024, 800)
	await open_room("res://scenes/Story/Breakfast.tscn", "早餐")
	check_map_extent("早餐")
	await play_opening_branch(0, "花房里那几盆新苗", true)
	await play_opening_branch(1, "篮子里还有足够大家分")
	await test_history_and_movement("早餐")
	await walk_to_breakfast_chef()
	await walk_to_breakfast_table()
	await exit_social_room_to_hall("早餐")
	await switch_by_key(KEY_F8, "res://scenes/Story/ParlorGathering.tscn", "围谈")
	check_map_extent("围谈")
	await play_opening_branch(0, "送信的人发现信封一直在自己的书里")
	await play_opening_branch(1, "没有绕过任何人的决定")
	await test_history_and_movement("围谈")
	await walk_to_parlor_poet()
	await walk_to_parlor_table()
	await switch_by_key(KEY_F6, "res://scenes/Story/CentralHall.tscn", "中央厅")
	await switch_by_key(KEY_F7, "res://scenes/Story/Breakfast.tscn", "早餐")
	await exit_social_room_to_hall("早餐复测")
	await switch_by_key(KEY_F8, "res://scenes/Story/ParlorGathering.tscn", "围谈复测")
	await exit_social_room_to_hall("围谈")
	var report := "PASS: 两个场景双分支、R重演、E人物和家具交互、H回看锁移动、F6/F7/F8实际导航及对白/回看导航锁、早餐与围谈南门E返回中央厅、32px房间范围与出口通道检查。\n" if failures.is_empty() else "FAIL:\n" + "\n".join(failures) + "\n"
	var file := FileAccess.open("res://../artifacts/first-three-scenes/social-validation.txt", FileAccess.WRITE)
	if file:
		file.store_string(report)
	print(report)
	quit(0 if failures.is_empty() else 1)
