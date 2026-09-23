extends SceneTree
var failed := false
func _initialize(): call_deferred("run")
func frames(n: int):
	for i in range(n): await physics_frame
func require(ok: bool, message: String):
	if not ok: failed = true; push_error(message)
func click(point: Vector2):
	var motion := InputEventMouseMotion.new(); motion.position = point; Input.parse_input_event(motion)
	for down in [true,false]:
		var event := InputEventMouseButton.new(); event.button_index = MOUSE_BUTTON_LEFT; event.position = point; event.pressed = down; Input.parse_input_event(event)
func world_click(scene: Node, point: Vector2):
	var stage := scene.find_child("RoomStage",true,false) as Control
	click(stage.global_position + point*2)
func run():
	change_scene_to_file("res://scenes/Reasoning/Reasoning.tscn")
	await frames(20)
	var scene := current_scene
	var detective := scene.find_child("Detective",true,false)
	var dialogue := scene.find_child("InvestigationDialogue",true,false)
	var before: Vector2 = detective.position
	world_click(scene,Vector2(480,140))
	await frames(10)
	require(detective.position.distance_to(before) > 1, "点击远处人物后没有行走")
	require(detective.position.distance_to(before) < 30, "正常点击不应瞬移")
	for i in range(650):
		if dialogue.visible: break
		await frames(1)
	print("NAV END: ", detective.position, " navigating=", detective.get("Navigating"))
	require(dialogue.visible, "到达人物后未自动交谈")
	var answer := scene.find_child("DialogueAnswer",true,false) as Label
	for button in scene.find_children("*","Button",true,false):
		if button.text == "我该先从哪里查？": button.pressed.emit()
	await frames(2)
	for button in scene.find_children("*","Button",true,false):
		if button.text == "先找茶去了哪里": button.pressed.emit()
	await frames(2)
	require(dialogue.visible and answer.text.contains("医生"), "选分支后回答被关闭或丢失")
	scene.find_child("CloseConversation",true,false).pressed.emit()
	await frames(40)
	# Debug travel and normal travel use the same valid approach points.
	var debug := scene.find_child("DebugTeleport",true,false) as CheckButton
	debug.button_pressed = true
	scene.find_child("ShowGuidance",true,false).button_pressed = false
	world_click(scene,Vector2(176,310))
	await frames(40)
	require(scene.find_child("ThoughtNotebook",true,false).visible, "点击器物未自动检查")
	require(not scene.find_child("InvestigationLead",true,false).visible, "无引导开关无效")
	scene.find_child("CloseNotebook",true,false).pressed.emit()
	await frames(2)
	world_click(scene,Vector2(176,284))
	await frames(2)
	require(dialogue.visible, "瞬移至医生后未交谈")
	for button in scene.find_children("*","Button",true,false):
		if button.text == "你看见杯子最初怎么摆的吗？": button.pressed.emit()
	await frames(2)
	require(dialogue.visible and answer.text.contains("速写"), "医生分支仍然无响应或直接退出")
	if DisplayServer.get_name() != "headless":
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png("res://../artifacts/tea-reasoning/mouse-dialogue.png")
	if not failed: print("PASS: real mouse walk-to-talk, branch persists, click-to-inspect, debug teleport, no-guidance and rapid followup")
	quit(1 if failed else 0)
