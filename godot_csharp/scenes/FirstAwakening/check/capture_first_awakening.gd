extends SceneTree

const OUTPUT := "res://../artifacts/first-awakening/"
const STRUCTURE_COLLIDERS := []

func _initialize() -> void:
	call_deferred("capture")

func _capture(scene: Node2D, filename: String) -> void:
	for i in 3:
		await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT + filename)

func _wait_physics(frames: int) -> void:
	for i in frames:
		await physics_frame

func _same_rect(a: Rect2, b: Rect2) -> bool:
	return a.position.is_equal_approx(b.position) and a.size.is_equal_approx(b.size)

func capture() -> void:
	var scene: Node2D = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for i in 12:
		await process_frame
	var furniture := []
	var excluded_structure := []
	for raw_rect in scene.obstacle_rects:
		var rect: Rect2 = raw_rect
		var is_structure := false
		for structural_rect in STRUCTURE_COLLIDERS:
			if _same_rect(rect, structural_rect):
				is_structure = true
				break
		if is_structure:
			excluded_structure.append([rect.position.x, rect.position.y, rect.size.x, rect.size.y])
			continue
		furniture.append({"name":"furniture-%d" % furniture.size(), "position":[0,0], "collision":[rect.position.x,rect.position.y,rect.size.x,rect.size.y]})
	var interactions := []
	for key in scene.sites:
		var site: Dictionary = scene.sites[key]
		interactions.append({"name":key,"position":[site.point.x,site.point.y],"radius":site.reach})
	var layout_file := FileAccess.open("res://../artifacts/first-awakening/runtime-layout.json",FileAccess.WRITE)
	layout_file.store_string(JSON.stringify({"footprints":furniture,"interaction_points":interactions,"excluded_structural_rects":excluded_structure},"\t"))
	scene.dialogue.reveal()
	await _capture(scene, "dialogue-preview.png")
	scene.dialogue.active = false
	scene.dialogue.panel.hide()
	scene._close_modal()
	await _capture(scene, "bedroom-preview.png")

	# Fixed overviews show each complete dorm, with the detective standing beside its bed.
	scene.player.stand_up()
	scene.set_physics_process(false)
	for index in 4:
		var base_x: int = int([1, 11, 21, 31][index]) * 32
		scene.player.position = Vector2(base_x + 150, 184)
		scene.player.pose("idle", "front")
		scene.camera.position = Vector2(clampf(base_x + 128, 320, 960), 248)
		await _capture(scene, "dormitory-%d-preview.png" % (index + 1))

	# Show the wake-up state with the original bed and its occupant now standing.
	scene.player.position = Vector2(154,184)
	scene.player.pose("idle", "front")
	scene.camera.position = Vector2(320,248)
	await _capture(scene, "after-waking-preview.png")

	# Exercise the actual auto-door collider and animation, capturing both states.
	var dorm_door: Node2D = scene.get_node("ObjectsAndPeople/DormitoryDoor1")
	scene.player.position = Vector2(120,432)
	scene.set_physics_process(true)
	await _wait_physics(24)
	var door_closed: bool = dorm_door.openness < 0.05 and dorm_door.leaf_collision.disabled == false
	var door_closed_openness: float = dorm_door.openness
	var door_closed_collision_disabled: bool = dorm_door.leaf_collision.disabled
	scene.set_physics_process(false)
	scene.camera.position = Vector2(192,256)
	await _capture(scene, "dormitory-door-closed-preview.png")

	scene.player.position = Vector2(192,342)
	scene.set_physics_process(true)
	await _wait_physics(24)
	var door_open: bool = dorm_door.openness > 0.95 and dorm_door.leaf_collision.disabled
	scene.set_physics_process(false)
	scene.camera.position = Vector2(192,256)
	await _capture(scene, "dormitory-door-open-preview.png")
	var door_state := FileAccess.open(OUTPUT + "dormitory-door-state.json", FileAccess.WRITE)
	door_state.store_string(JSON.stringify({"closed":door_closed,"closed_openness":door_closed_openness,"collision_disabled_when_closed":door_closed_collision_disabled,"open":door_open,"open_openness":dorm_door.openness,"collision_disabled_when_open":dorm_door.leaf_collision.disabled},"\t"))
	if not door_closed or not door_open:
		push_error("Dormitory door did not reach verified open and closed states")

	# Return to the real following camera and HUD before general room captures.
	scene.set_physics_process(true)
	scene.player.position = Vector2(660,944)
	await _wait_physics(4)

	# Review the real initial candidate pool before the staged evidence unlocks.
	scene.call("_open_flow")
	for i in 4: await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT + "replay-initial-preview.png")
	scene.call("_close_modal")
	scene.player.stand_up()
	for view in [{"name":"study", "point":Vector2(320,736)}, {"name":"preparation", "point":Vector2(930,736)}, {"name":"hall", "point":Vector2(660,944)}]:
		scene.player.position = view.point
		await _wait_physics(4)
		await _capture(scene, view.name + "-preview.png")
		if view.name == "hall":
			var original_inner_bolt: bool = scene.inner_bolt
			scene.call("_toggle_bolt")
			scene.call("_close_modal")
			var bolt_withdrawn: bool = not scene.inner_bolt
			await _wait_physics(2)
			await _capture(scene, "hall-bolt-withdrawn-preview.png")
			scene.call("_toggle_bolt")
			scene.call("_close_modal")
			var bolt_restored: bool = scene.inner_bolt == original_inner_bolt
			var bolt_state := FileAccess.open(OUTPUT + "hall-bolt-capture-state.json", FileAccess.WRITE)
			bolt_state.store_string(JSON.stringify({"withdrawn_before_capture":bolt_withdrawn,"restored_after_capture":bolt_restored,"inner_bolt_after_restore":scene.inner_bolt},"\t"))
			if not bolt_withdrawn or not bolt_restored:
				push_error("Hall bolt state was not restored after capture")
	scene._interact("water")
	await _capture(scene, "water-preview.png")
	scene._close_modal()
	scene._interact("paper")
	for i in 4: await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT + "evidence-preview.png")
	scene._close_modal()
	scene.call("_begin_dialogue", "broadcast")
	for i in 5: await process_frame
	scene.dialogue.reveal()
	await _capture(scene,"dialogue-broadcast-preview.png")
	if scene.dialogue.body.get_content_height() > scene.dialogue.body.size.y:
		push_error("Broadcast text exceeds dialogue body")
	scene.dialogue.active = false
	scene.dialogue.panel.hide()
	scene.call("_begin_dialogue", "transmitter")
	for i in 5:
		await process_frame
	scene.dialogue.reveal()
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT + "transmitter-preview.png")
	scene.dialogue.active = false
	scene.dialogue.panel.hide()
	scene.call("_interact", "hinge")
	scene.call("_interact", "weight")
	scene.call("_close_modal")
	var presentation = load("res://scenes/FirstAwakening/MindPresentation.gd")
	scene.call("_open_flow")
	for i in 4: await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT + "replay-empty-preview.png")
	for node in scene.modal.find_children("*","Button",true,false):
		if node.text == "词汇": node.pressed.emit()
	await _capture(scene,"replay-vocabulary-preview.png")
	scene.mind_text = "音乐家用木楔撑开门。\n音乐家见到热烟后，侦探撤下门底的木楔。\n工程师插入生活翼内销。\n侦探用纸包起木楔。\n音乐家移开铁止挡座。\n厨师用湿布擦拭地面。\n门外的人落下主楼侧横闩。"
	presentation.run_draft(scene)
	var first_pause: int = scene.get("replay_pause_points").keys().min()
	while scene.get("replay_index") < first_pause:
		scene.call("_replay_step", 1)
	await _capture(scene,"replay-preview.png")
	var draft_editor := scene.modal.find_child("DraftEditor",true,false) as TextEdit
	draft_editor.grab_focus()
	draft_editor.set_caret_line(1)
	draft_editor.set_caret_column(0)
	draft_editor.insert_text_at_caret("不确定")
	await _capture(scene,"replay-edit-preview.png")
	scene.mind_text = "侦探用纸包起木楔。\n音乐家走到门边观察。\n音乐家用木楔撑开门。\n侦探撤下门底的木楔。"
	scene.evidence["paper"] = true
	presentation.run_draft(scene)
	while scene.replay_index < scene.replay_events.size(): scene._replay_step(1)
	scene.mind_trace = 1
	presentation.refresh(scene)
	for i in 4: await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT + "replay-conflict-preview.png")
	# Staged answer states for layout review, not evidence of a played completion.
	scene.broadcast_heard = true
	scene.call("_open_answer")
	for i in 4: await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT + "answer-locked-preview.png")
	for key in scene.sites: scene.evidence[key] = true
	for key in ["门底遮蔽", "纸内转印", "双套锁止"]: scene.sim_checks[key] = true
	scene.answer_slots.place = "长期生活研习"
	scene.answer_slots.door = "先撑门进灰 → 撤楔合门 → 沾灰后包纸"
	scene.answer_slots.outer = "生活翼内销 + 主楼侧外闩"
	scene.call("_open_answer")
	for i in 4: await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT + "answer-filled-preview.png")
	scene.ui.hide()
	scene.set_physics_process(false)
	scene.camera.zoom = Vector2.ONE
	scene.camera.offset = Vector2.ZERO
	scene.camera.position = Vector2(640, 512)
	for i in 4:
		await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(OUTPUT + "map-preview.png")
	quit()
