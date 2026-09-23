extends SceneTree

var failed := false

func _initialize() -> void:
	call_deferred("verify")

func require(ok: bool, message: String) -> void:
	if not ok:
		failed = true
		push_error(message)

func frames(count: int) -> void:
	for i in range(count): await physics_frame

func key(code: Key, pressed: bool) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)

func screenshot(path: String) -> void:
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(path)

func verify() -> void:
	change_scene_to_file("res://scenes/Reasoning/Reasoning.tscn")
	await frames(20)
	await screenshot("res://../artifacts/tea-reasoning/study-room.png")
	var scene := current_scene
	require(scene.find_children("*", "TileMapLayer", true, false).size() >= 4, "研究室必须复用真实TileMap图层")
	require(not scene.find_child("ThoughtNotebook", true, false).visible, "初始现场不应常驻右侧札记")
	require(not scene.find_child("RuleReference", true, false).visible, "语法帮助不应挤占默认界面")
	var detective := scene.find_child("Detective", true, false) as CharacterBody2D
	require(detective != null, "侦探控制器没有进入场景")
	var sprite := detective.get_node("AnimatedSprite2D") as AnimatedSprite2D
	require(sprite.sprite_frames.resource_path.ends_with("detective.tres"), "侦探必须使用侦探图集")
	for i in range(4):
		var other := scene.find_child("Actor" + str(i), true, false)
		require(other.get_node("AnimatedSprite2D").sprite_frames.resource_path.ends_with("placeholder.tres"), "当事人必须使用占位图集")
	var notes := scene.find_child("EvidencePicker", true, false) as OptionButton
	require(notes.item_count == 1, "开场不能直接发放全部残局材料")
	var lead := scene.find_child("InvestigationLead", true, false) as Label
	require(lead.text.contains("主厨"), "开场缺少人物与位置指引")
	var offsets := [Vector2(0,32),Vector2(32,0),Vector2(-32,0),Vector2(0,-32)]
	var animations := ["talk_front","talk_left","talk_left","talk_back"]
	for i in range(4):
		var npc := scene.find_child("Actor"+str(i),true,false)
		detective.position = npc.position + offsets[i]
		await frames(2)
		key(KEY_E,true)
		key(KEY_E,false)
		await frames(2)
		var npc_sprite := npc.get_node("AnimatedSprite2D") as AnimatedSprite2D
		require(npc_sprite.animation == animations[i], "人物%d动画%s，预期%s" % [i,npc_sprite.animation,animations[i]])
		require(npc_sprite.flip_h == (i == 1), "交谈侧向镜像错误")
		require(str(sprite.animation).begins_with("talk_"), "与人物%d交谈时侦探动画%s" % [i,sprite.animation])
		var talk_position := detective.position
		key(KEY_RIGHT,true)
		await frames(16)
		key(KEY_RIGHT,false)
		require(detective.position == talk_position, "对话时侦探不应漂移")
		if i == 0: await screenshot("res://../artifacts/tea-reasoning/conversation.png")
		await frames(50)
		if i == 0:
			for button in scene.find_children("*","Button",true,false):
				if button.text == "我该先从哪里查？": button.pressed.emit()
			await frames(55)
			for button in scene.find_children("*","Button",true,false):
				if button.text == "先找茶去了哪里": button.pressed.emit()
			await frames(55)
			require(lead.text.contains("温瓶盆"), "选择疑问后指引未更新")
		else: scene.find_child("CloseConversation",true,false).pressed.emit()
		await frames(2)
	# Physical trace and the doctor's explanation are acquired through separate actions.
	detective.position = Vector2(144,304)
	await frames(2)
	key(KEY_F,true)
	key(KEY_F,false)
	await frames(40)
	var ids: Array[int] = []
	for n in range(notes.item_count): ids.append(notes.get_item_id(n))
	require(ids.has(4) and not ids.has(12), "检查盆不应自动获得医生证词")
	scene.find_child("CloseNotebook",true,false).pressed.emit()
	await frames(2)
	key(KEY_E,true)
	key(KEY_E,false)
	await frames(40)
	for button in scene.find_children("*","Button",true,false):
		if button.text == "温瓶盆里为什么有茶？": button.pressed.emit()
	await frames(18)
	ids.clear()
	for n in range(notes.item_count): ids.append(notes.get_item_id(n))
	require(ids.has(12), "实际追问没有收录医生解释")
	await screenshot("res://../artifacts/tea-reasoning/doctor-followup.png")
	await frames(30)
	scene.find_child("CloseConversation",true,false).pressed.emit()
	# Window inspection must not award witness statements.
	detective.position = Vector2(112,160)
	await frames(2)
	key(KEY_F,true)
	key(KEY_F,false)
	await frames(40)
	for i in range(notes.item_count):
		require(notes.get_item_id(i) != 7 and notes.get_item_id(i) != 8, "检查窗边错误地发放人物证词")
	scene.find_child("CloseNotebook",true,false).pressed.emit()
	detective.position = Vector2(320,384)
	await frames(2)
	key(KEY_UP, true)
	await frames(18)
	key(KEY_UP, false)
	var before := detective.position
	key(KEY_RIGHT, true)
	await frames(18)
	key(KEY_RIGHT, false)
	require(detective.position.x > before.x + 10, "真实移动输入未移动侦探")
	require(sprite.flip_h, "向右应使用左向镜像")
	# Walk into the worktable: the footprint should stop the feet at its lower edge.
	detective.position = Vector2(320, 256)
	key(KEY_UP, true)
	await frames(30)
	key(KEY_UP, false)
	require(detective.position.y >= 251, "侦探穿过了工作桌碰撞")
	key(KEY_E, true)
	key(KEY_E, false)
	await frames(38)
	var materials := scene.find_child("EvidencePicker", true, false) as OptionButton
	require(materials.item_count >= 2, "检查接触帧没有收录材料")
	await screenshot("res://../artifacts/tea-reasoning/investigation.png")
	var mind := scene.find_child("MindButton", true, false) as Button
	mind.pressed.emit()
	await frames(4)
	require(not detective.visible, "脑内重演时应隐藏现实侦探")
	var journal := scene.find_child("NotebookButton", true, false) as Button
	# Rules are already visible in the dedicated mind workspace.
	var editor := scene.find_child("TeaEditor", true, false) as CodeEdit
	var run := scene.find_child("RunTea", true, false) as Button
	var status := scene.find_child("RunStatus", true, false) as Label
	editor.text = "当 总是，则 让椅子变湿"
	run.pressed.emit()
	require(status.text.begins_with("未提交"), "非法结果指令不应提交")
	editor.text = "当 总是，则 查看 窗边"
	run.pressed.emit()
	await frames(35)
	var play := scene.find_child("PlayTea", true, false) as Button
	if play.text == "暂停": play.pressed.emit()
	await frames(3)
	await screenshot("res://../artifacts/tea-reasoning/reconstruction.png")
	var timeline := scene.find_child("TeaTimeline", true, false) as HSlider
	require(timeline.max_value > 0, "真实重演没有生成回看记录")
	var reality_room := scene.find_child("RealityRoom", true, false)
	var simulated_room := scene.find_child("ReconstructionRoom", true, false)
	require(reality_room != simulated_room, "现实与重演必须是不同场景实例")
	require(reality_room.get_world_2d() != simulated_room.get_world_2d(), "两个空间不得共享碰撞世界")
	var held_position := detective.position
	key(KEY_LEFT, true)
	await frames(8)
	key(KEY_LEFT, false)
	require(detective.position == held_position, "脑内输入不能移动现实侦探")
	var draft := editor.text
	timeline.value = 0
	await frames(2)
	require(status.text.contains("0.0"), "回看未回到起点")
	scene.find_child("ReturnReality", true, false).pressed.emit()
	require(detective.visible, "返回现实应恢复侦探")
	mind.pressed.emit()
	# Rules are already visible in the dedicated mind workspace.
	require(editor.text == draft, "切换现实/脑内不应丢失草稿")
	root.size = Vector2i(1280, 900)
	await frames(6)
	await screenshot("res://../artifacts/tea-reasoning/reconstruction-1280.png")
	if not failed: print("PASS: four NPC facing/talk gestures, movement lock, inquiry guidance, physical/testimony separation, investigation, replay and draft retention.")
	quit(1 if failed else 0)
