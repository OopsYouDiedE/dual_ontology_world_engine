extends SceneTree
## Exercise the authored branches through visible choice buttons, not progress flags.
var scene: Node2D
var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func expect(value: bool, description: String) -> void:
	if not value:
		failures.append(description)
		push_error(description)

func settle() -> void:
	for i in 4: await process_frame

func finish_lines() -> void:
	for i in 60:
		await settle()
		if not scene.dialogue.active: return
		if scene.dialogue.busy: continue
		scene.dialogue.reveal()
		if scene.dialogue.choices.get_child_count() > 0:
			await settle()
			return
		scene.dialogue.advance()
	expect(false, "Dialogue did not reach a choice or finish")

func choose(text: String) -> void:
	await finish_lines()
	for node in scene.dialogue.choices.get_children():
		if node.text != text: continue
		expect(node.is_visible_in_tree(), "Visible choice: " + text)
		expect(root.get_visible_rect().encloses(node.get_global_rect()), "Choice fits viewport: " + text)
		var at: Vector2 = node.get_global_rect().get_center()
		var motion := InputEventMouseMotion.new()
		motion.position = at
		Input.parse_input_event(motion)
		for down in [true, false]:
			var event := InputEventMouseButton.new()
			event.position = at
			event.button_index = MOUSE_BUTTON_LEFT
			event.pressed = down
			Input.parse_input_event(event)
			await process_frame
		await finish_lines()
		return
	expect(false, "Missing choice: " + text)

func capture(filename: String) -> void:
	if DisplayServer.get_name() == "headless": return
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("res://../artifacts/first-awakening/" + filename)

func run() -> void:
	scene = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	await settle()
	TranslationServer.set_locale("zh_CN")
	await finish_lines()
	# One use includes the instruction, connection, broadcast, and question menu.
	scene.player.position = scene.sites.transmitter.point + Vector2(0,48)
	scene.camera.position = scene.player.position
	scene._interact("transmitter")
	await finish_lines()
	expect(scene.broadcast_heard and scene.transmitter_connected, "First use connects and reaches conversation")
	await capture("interaction-transmitter.png")
	await choose("松开拉柄")
	expect(not scene.transmitter_connected and not scene.dialogue.active, "Release restores field controls")
	var before: int = scene.dialogue.history.size()
	scene._interact("engineer")
	await finish_lines()
	expect(scene.npc_stage_seen.get("engineer") == 1, "Late first encounter is current broadcast stage")
	var heard := "\n".join(scene.dialogue.history.slice(before))
	expect(heard.contains("刚才没顾上介绍") and not heard.contains("你也醒了"), "Late greeting acknowledges shared event")
	scene.player.position = scene.sites.door.point + Vector2(0,-48)
	scene.camera.position = scene.player.position
	scene._interact("door")
	await finish_lines()
	expect(not scene.evidence.has("slit"), "Door entry does not reveal outer bar")
	await choose("拔出内销")
	expect(not scene.inner_bolt, "Bolt is actually withdrawn")
	before = scene.dialogue.history.size()
	await choose("推门")
	heard = "\n".join(scene.dialogue.history.slice(before))
	expect(heard.contains("阻力在另一侧") and not heard.contains("横闩"), "Blocked push does not leak unseen bar")
	await choose("掀开检视盖")
	expect(scene.evidence.has("slit"), "Inspection acquires external lock evidence")
	await choose("检查铰链与配重")
	expect(scene.evidence.has("hinge") and scene.evidence.has("weight"), "Mechanics inspection records both fixtures")
	await choose("拉门")
	await choose("插回内销")
	expect(scene.inner_bolt, "Bolt can be restored without leaving the interaction")
	await capture("interaction-door.png")
	await choose("离开门边")
	expect(not scene.dialogue.active, "Door interaction returns control")
	scene._request_flow()
	await settle()
	expect(scene.modal.visible and scene.mode == "flow" and scene.dialogue.active, "Mind page is visible during its introduction")
	expect(scene.dialogue.layer > scene.ui.layer, "Introduction is drawn above the notebook")
	scene.dialogue.reveal()
	await capture("interaction-mind-intro.png")
	await finish_lines()
	expect(scene.modal.visible and scene.modal.process_mode == Node.PROCESS_MODE_INHERIT, "Introduction restores notebook input")
	scene._close_modal()
	scene._request_flow()
	expect(not scene.dialogue.active, "Mind introduction is not repeated")
	scene._close_modal()
	# Wrong evidence must not silently award a rebuttal.
	scene._interact("wash")
	scene._close_modal()
	scene._challenge("defense")
	await choose("洗盆里的灰垢")
	expect(not scene.disputed.defense, "Irrelevant evidence leaves claim unresolved")
	await choose("铰链、石止口和自闭配重")
	expect(scene.disputed.defense, "Relevant evidence advances the discussion")
	scene._close_modal()
	for id in ["desk_1", "desk_2", "desk_3"]:
		scene._interact(id)
		expect(scene.evidence.has(id), "Distinct bedroom desk is investigable: " + id)
		scene._close_modal()
	# A departure request cannot bypass the companions' explicit refusals.
	scene._open_transmitter()
	await choose("放弃探索")
	expect(not scene.ended, "Request to leave does not decide for companions")
	await finish_lines()
	var offered_departure := false
	for choice in scene.dialogue.choices.get_children():
		if choice.text == "确认共同离开": offered_departure = true
	expect(not offered_departure, "Unanimity is not offered after three refusals")
	await choose("先留下继续调查")
	expect(not scene.ended and scene.transmitter_connected, "Declining departure resumes the call")
	# A debated theory must not remain selectable as if the meeting had established it.
	scene.dialogue.active = false
	scene.dialogue.panel.hide()
	scene.disputed = {"prison": true, "defense": true, "escape": true}
	scene.captain = true
	scene.answer_slots = {"place": "长期生活研习", "door": "撬门越狱 · 被镇压", "outer": "外侧单方锁死"}
	scene._open_answer()
	await settle()
	expect(scene.answer_slots.door == "", "debated escape theory is cleared from an old answer")
	var door_menu := scene.modal_box.find_children("*", "MenuButton", true, false)[1] as MenuButton
	expect(door_menu.get_popup().item_count == 0, "refuted door theories are absent until replay evidence unlocks a supported account")
	var mind_button: Button = null
	for item in scene.modal_box.find_children("*", "Button", true, false):
		if item.text == "打开脑内推演": mind_button = item
	expect(mind_button != null and not mind_button.disabled, "answer page explains and opens the required replay")
	if mind_button != null:
		mind_button.pressed.emit()
		await settle()
		expect(scene.mode == "flow", "replay shortcut opens the actor draft notebook")
	print("INTERACTION FLOW: ", failures.size(), " failures")
	quit(0 if failures.is_empty() else 1)
