extends SceneTree

func _initialize() -> void:
	call_deferred("verify")

func press(caption: String) -> void:
	for button in root.find_children("*", "Button", true, false):
		if button.text == caption:
			button.pressed.emit()
			return
	assert(false, "按钮不存在：" + caption)

func settle() -> void:
	for step in range(10):
		press("单步")

func attempt(editor: CodeEdit, text: String) -> void:
	editor.text = text
	press("发声 · 2 行动值")
	settle()
	press("试声 · 观察结果")
	settle()

func verify() -> void:
	change_scene_to_file("res://scenes/Reasoning/Spellwork.tscn")
	for frame in range(30): await process_frame
	var editor: CodeEdit = root.find_children("UtteranceEditor", "CodeEdit", true, false)[0]
	editor.text = "ilo o jo e kalama sin."
	await process_frame
	var colors = editor.syntax_highlighter.get_line_syntax_highlighting(0)
	assert(colors[6].color == Color("246bc4"), "o 应为蓝色结构词")
	assert(colors[18].color == Color("c43a42"), "未解锁的 sin 应为红色")
	press("发声 · 2 行动值")
	settle()
	var empty_meaning: RichTextLabel = root.find_children("Meaning", "RichTextLabel", true, false)[0]
	assert("完成句法" in empty_meaning.text, "非法草稿不应显示有效解释")
	var block := "ilo kute pi tomo moku o weka e kalama."
	attempt(editor, block)
	attempt(editor, block + "\nilo kute pi tomo moku o jo e kalama nanpa wan.")
	attempt(editor, block + "\nkalama li sin la ilo kute pi tomo moku o jo e kalama nanpa wan.")
	var solved := false
	for label in root.find_children("*", "Label", true, false):
		if label.text.begins_with("课题完成"):
			solved = true
	assert(solved, "UI 应完成三轮言灵实验")
	for frame in range(5): await process_frame
	root.get_texture().get_image().save_png("res://scenes/Reasoning/check/spell-preview.png")
	press("重置课题")
	var words: RichTextLabel = root.find_children("Vocabulary", "RichTextLabel", true, false)[0]
	assert(not "sin  —" in words.text, "重置应收回时间词")
	print("PASS: 言灵编辑器着色、未知词拒绝、三轮操作解锁、最终结果、重置。")
	quit()
