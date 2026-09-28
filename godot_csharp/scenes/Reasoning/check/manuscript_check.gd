extends SceneTree

var failed := false
func _initialize() -> void:
	call_deferred("verify")
func frames(n: int) -> void:
	for i in range(n): await process_frame
func require(ok: bool, message: String) -> void:
	if not ok:
		failed = true
		push_error(message)
func shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("res://../artifacts/tea-reasoning/" + name + ".png")
func verify() -> void:
	change_scene_to_file("res://scenes/Reasoning/Reasoning.tscn")
	await frames(20)
	var scene := current_scene
	scene.find_child("MindButton",true,false).pressed.emit()
	await frames(5)
	var editor := scene.find_child("TeaEditor",true,false) as CodeEdit
	var run := scene.find_child("RunTea",true,false) as Button
	var paper := scene.find_child("BehaviorWorkspace",true,false) as Control
	var shelf := scene.find_child("AvailableWords",true,false) as Control
	for width in [1280, 1586]:
		root.size = Vector2i(width, 992)
		await frames(8)
		require(paper.get_global_rect().end.x <= root.size.x, "Manuscript clips horizontally")
		require(paper.get_global_rect().end.y < shelf.position.y, "Paper overlaps word shelf")
		await shot("manuscript-" + str(width))
	editor.text = "当 总是，则 查看 窗边"
	editor.text_changed.emit()
	run.pressed.emit()
	await frames(40)
	var play := scene.find_child("PlayTea",true,false) as Button
	if play.text == "暂停": play.pressed.emit()
	var timeline := scene.find_child("TeaTimeline",true,false) as HSlider
	require(timeline.max_value > 0, "Simulation did not produce replay frames")
	var previous := timeline.value
	scene.find_child("BackTea",true,false).pressed.emit()
	require(timeline.value == maxf(0, previous - 1), "Back did not rewind one frame")
	editor.text = "当 总是，则 让椅子变湿"
	editor.text_changed.emit()
	await frames(2)
	require(run.disabled, "Invalid drafts must disable restart")
	await frames(5)
	await shot("manuscript-error")
	editor.text = ""
	for button in scene.find_child("WordChoices",true,false).get_children():
		if button.text == "查看": button.pressed.emit()
	require(editor.text == "查看 ", "Vocabulary insertion failed")
	editor.text = "当 总是，则 查看 窗边"
	editor.text_changed.emit()
	await frames(2)
	require(not run.disabled, "Correction did not reenable restart")
	scene.find_child("ReturnReality",true,false).pressed.emit()
	scene.find_child("MindButton",true,false).pressed.emit()
	require(editor.text == "当 总是，则 查看 窗边", "Draft lost across mode switch")
	root.size = Vector2i(1280,900)
	await frames(8)
	require(paper.get_global_rect().end.x <= root.size.x, "Paper clips at minimum width after editing")
	print("Minimum layout: window=", root.size, " paper=", paper.get_global_rect(), " shelf=", shelf.get_global_rect())
	require(shelf.get_global_rect().end.y <= root.size.y, "Word shelf clips at minimum height")
	await shot("manuscript-minimum")
	if not failed: print("PASS: manuscript layout, vocabulary insertion, invalid/corrected draft, replay, rewind and draft retention")
	quit(1 if failed else 0)

