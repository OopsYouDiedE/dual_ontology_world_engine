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
	# Go around the left end of the long table, then approach the chef's feet.
	await key(KEY_A, 3.25)
	await key(KEY_W, 1.95)
	await key(KEY_D, 1.6)
	await process_frame
	expect(room.player.position.distance_to(room.cast[0].position) < 68.0, "早餐：主厨接近点不可达")
	expect(room.nearest == room.cast[0], "早餐：主厨不是最近的可交互对象")
	await key(KEY_E)
	expect(room.dialogue.active, "早餐：E 未启动主厨对话")
	await finish_story(0)

func walk_to_breakfast_table() -> void:
	# From beside the chef, return below the table and approach its south edge.
	await key(KEY_A, 1.4)
	await key(KEY_S, 1.3)
	await key(KEY_D, 2.55)
	await key(KEY_W, 1.2)
	await process_frame
	var table: Node2D = room.items.get_node("BreakfastTable")
	expect(room.player.position.distance_to(table.position) < 68.0, "早餐：长桌交互点不可达")
	expect(room.nearest == table, "早餐：长桌不是最近的交互对象")
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
	expect(room.player.position.distance_to(Vector2(512, 520)) < 1.0, room_scene + ": R 没有重置玩家站位")
	if check_nav_lock and room.dialogue.active:
		var held_scene := room.scene_file_path
		await key(KEY_F8)
		expect(room.scene_file_path == held_scene and room.dialogue.active, room_scene + ": 对白期间 F8 错误切场")
	await finish_story(index, true)
	var combined := "\n".join(room.dialogue.history)
	expect(combined.contains(evidence), room_scene + ": 所选分支内容未出现在回看记录")

func walk_to_parlor_poet() -> void:
	# Route around the tea table and chairs; test the reachable interaction radius, not overlapping feet.
	await key(KEY_A, 3.0)
	await key(KEY_W, 2.0)
	await key(KEY_D, 2.4)
	await process_frame
	expect(room.player.position.distance_to(room.cast[0].position) < 68.0, "围谈：诗人接近点不可达")
	expect(room.nearest == room.cast[0], "围谈：诗人不是最近的可交互对象")
	await key(KEY_E)
	expect(room.dialogue.active, "围谈：E 未启动诗人对话")
	await finish_story(0)

func walk_to_parlor_table() -> void:
	# Circle below the chairs before approaching the central table.
	await key(KEY_A, 2.2)
	await key(KEY_S, 1.4)
	await key(KEY_D, 2.55)
	await process_frame
	var table: Node2D = room.items.get_node("TeaTable")
	expect(room.player.position.distance_to(table.position) < 68.0, "围谈：会客桌交互点不可达")
	expect(room.nearest == table, "围谈：会客桌不是最近的交互对象")
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
	expect(left <= 64.0 and right >= 960.0, label + ": 房间地面未覆盖约定有效宽度")
	expect(room.collision_rects.any(func(r: Rect2) -> bool: return r.position == Vector2(64, 544) and r.size.x == 416.0), label + ": 左侧底边碰撞异常")
	expect(room.collision_rects.any(func(r: Rect2) -> bool: return r.position == Vector2(544, 544) and r.size.x == 416.0), label + ": 右侧底边碰撞异常")

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
	var delta: Vector2 = entrance.position - room.player.position
	if absf(delta.x) > 18.0:
		await key(KEY_D if delta.x > 0.0 else KEY_A, absf(delta.x) / 90.0 + 0.06)
	if absf(delta.y) > 18.0:
		await key(KEY_S if delta.y > 0.0 else KEY_W, absf(delta.y) / 90.0 + 0.06)
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
