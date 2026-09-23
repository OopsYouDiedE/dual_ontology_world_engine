extends SceneTree

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	root.size = Vector2i(1280, 800)
	change_scene_to_file("res://scenes/Reasoning/BreakfastExperiment.tscn")
	for frame in range(30):
		await process_frame
	root.get_texture().get_image().save_png("res://scenes/Reasoning/check/preview.png")
	var editor: CodeEdit = root.find_children("*", "CodeEdit", true, false)[0]
	editor.text = "校时\n火龙\n监听 999"
	await process_frame
	var colors = editor.syntax_highlighter.get_line_syntax_highlighting(1)
	assert(colors[0].color == Color("c43a42"), "未知词必须显示红色")
	var buttons = root.find_children("*", "Button", true, false)
	for button in buttons:
		if button.text == "重置实验": button.pressed.emit()
	for button in buttons:
		if button.text == "执行两人方案": button.pressed.emit()
	for step in range(24):
		for button in buttons:
			if button.text == "单步": button.pressed.emit()
	var unlocked := false
	for label in root.find_children("*", "Label", true, false):
		if label.text.begins_with("已掌握词汇：") and "检查" in label.text:
			unlocked = true
	assert(unlocked, "UI 执行应解锁检查词汇")
	print("PASS: 编辑器未知词着色、重置、双人物执行、单步与首阶段解锁。")
	quit()
