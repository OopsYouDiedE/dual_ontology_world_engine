extends SceneTree
## Independent rendered capture: normal production scene, one public start request.
var capture_viewport: SubViewport

func _initialize() -> void:
	root.hide()
	_run.call_deferred()

func _run() -> void:
	capture_viewport = SubViewport.new()
	capture_viewport.size = Vector2i(1280, 992)
	capture_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(capture_viewport)
	var packed: PackedScene = load("res://scenes/BasicUISystem/BasicUISystem.tscn")
	if packed == null:
		push_error("Capture could not load the production main scene.")
		quit(2)
		return
	var scene: Node = packed.instantiate()
	capture_viewport.add_child(scene)
	for frame in range(12):
		await process_frame
	await RenderingServer.frame_post_draw
	var title_path := ProjectSettings.globalize_path("res://../artifacts/basic-ui-system/claude-title-actual.png")
	var title_error := capture_viewport.get_texture().get_image().save_png(title_path)
	print("TITLE_CAPTURE_PATH=" + title_path)
	print("TITLE_CAPTURE_SAVE_ERROR=" + str(title_error))
	if title_error != OK:
		quit(3)
		return
	var starts: Array[Button] = []
	_find_start(scene, starts)
	if starts.is_empty():
		push_error("No visible start control.")
		quit(4)
		return
	starts[0].emit_signal("pressed")
	await create_timer(2.0).timeout
	await RenderingServer.frame_post_draw
	var shot: Image = capture_viewport.get_texture().get_image()
	var target := ProjectSettings.globalize_path("res://../artifacts/basic-ui-system/claude-opening-actual.png")
	var error := shot.save_png(target)
	print("START_CAPTURE_PATH=" + target)
	print("START_CAPTURE_SAVE_ERROR=" + str(error))
	if error != OK:
		quit(3)
		return
	for step in range(40):
		var visible_text: Array[String] = []
		var continues: Array[Button] = []
		_collect_visible(scene, visible_text, continues)
		print("VISIBLE_STEP=" + str(step) + " TEXT=" + " | ".join(visible_text))
		var form_open := false
		for value in visible_text:
			if value.contains("如何称呼你"):
				form_open = true
		if form_open:
			await RenderingServer.frame_post_draw
			var form_path := ProjectSettings.globalize_path("res://../artifacts/basic-ui-system/claude-form-actual.png")
			var form_error := capture_viewport.get_texture().get_image().save_png(form_path)
			print("FORM_CAPTURE_PATH=" + form_path)
			print("FORM_CAPTURE_SAVE_ERROR=" + str(form_error))
			await _shutdown(scene)
			quit(0 if form_error == OK else 3)
			return
		if continues.is_empty():
			# A typewriter temporarily hides Continue; wait for the visible control.
			for attempt in range(60):
				await create_timer(0.1).timeout
				visible_text.clear()
				continues.clear()
				_collect_visible(scene, visible_text, continues)
				if not continues.is_empty() or visible_text.any(func(t: String) -> bool: return t.contains("如何称呼你")): break
			if visible_text.any(func(t: String) -> bool: return t.contains("如何称呼你")): continue
		if continues.is_empty():
			var stop_path := ProjectSettings.globalize_path("res://../artifacts/basic-ui-system/claude-stop-actual.png")
			var stop_error := capture_viewport.get_texture().get_image().save_png(stop_path)
			print("STOP_CAPTURE_PATH=" + stop_path)
			print("STOP_CAPTURE_SAVE_ERROR=" + str(stop_error))
			print("CAPTURE_STOP: dialogue finished; no visible continue control or naming form.")
			await _shutdown(scene)
			await _form_component_preview()
			quit(0)
			return
		continues[0].emit_signal("pressed")
		await create_timer(0.5).timeout
		await RenderingServer.frame_post_draw
	push_error("Visible naming form was not reached within the capture safety limit.")
	quit(5)

func _collect_visible(node: Node, texts: Array[String], continues: Array[Button]) -> void:
	if node is Control and not node.is_visible_in_tree():
		return
	if node is Label or node is Button or node is LineEdit:
		var value: String = node.text
		if not value.is_empty():
			texts.append(value)
		if node is Button and value.contains("继续") and not node.disabled:
			continues.append(node)
	elif node is RichTextLabel:
		if not node.get_parsed_text().is_empty():
			texts.append(node.get_parsed_text())
	for child in node.get_children():
		_collect_visible(child, texts, continues)

func _find_start(node: Node, buttons: Array[Button]) -> void:
	if node is Control and not node.is_visible_in_tree(): return
	if node is Button and node.text == "开始游戏" and not node.disabled: buttons.append(node)
	for child in node.get_children(): _find_start(child, buttons)

func _shutdown(scene: Node) -> void:
	scene.begin_shutdown()
	capture_viewport.remove_child(scene)
	scene.queue_free()
	for frame in range(5): await process_frame

# Presentation-only sample, explicitly separate from the game's naming flow.
func _form_component_preview() -> void:
	var ui: Node = load("res://scenes/Shared/UI/UISystem.tscn").instantiate()
	capture_viewport.add_child(ui)
	ui.present({"kind": "form", "title": "如何称呼你", "prompt": "你希望大家如何称呼你？",
		"fields": [{"id": "name", "kind": "text", "heading": "姓名", "value": "侦探", "max_length": 12}],
		"actions": [{"id": "confirm", "label": "确认", "default": true, "requires": ["name"]}]})
	for frame in range(12): await process_frame
	await RenderingServer.frame_post_draw
	var path := ProjectSettings.globalize_path("res://../artifacts/basic-ui-system/claude-form-component-preview.png")
	print("FORM_COMPONENT_PREVIEW_SAVE_ERROR=" + str(capture_viewport.get_texture().get_image().save_png(path)))
	ui.begin_shutdown()
	capture_viewport.remove_child(ui)
	ui.queue_free()
	for frame in range(5): await process_frame
