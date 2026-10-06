extends SceneTree
var host: Control
var started := Time.get_ticks_msec()
func _initialize() -> void:
	call_deferred("run")
func fail(message: String) -> void:
	print("FAIL ", message)
	quit(1)
func run() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://../artifacts/python-story-refactor/world-preview-ui"))
	host = load("res://scenes/Shared/Story/Examples/WorldScript/Host/WorldScriptPreview.tscn").instantiate()
	host.compiled_document = ProjectSettings.globalize_path("res://scenes/Shared/Story/Examples/WorldScript/generated_world.json")
	root.add_child(host)
	await process_frame
	if not host.opened:
		fail("document did not open: " + host.last_error)
		return
	for chapter in ["arrival", "morning"]:
		var refresh: Button = host.get_node("Margin/Column/Actions/Refresh")
		refresh.pressed.emit()
		await process_frame
		var choices: VBoxContainer = host.get_node("Margin/Column/ChoiceScroll/Choices")
		if choices.get_child_count() == 0 or not choices.get_child(0) is Button:
			fail("no actual candidate button")
			return
		var chosen: Button = null
		for child in choices.get_children():
			if child is Button and child.tooltip_text.begins_with(chapter + ":"):
				chosen = child
				break
		if chosen == null:
			fail("missing chapter candidate")
			return
		print("UI CANDIDATE ", chapter, " ", chosen.tooltip_text)
		chosen.pressed.emit()
		while not host._idle():
			await process_frame
			if Time.get_ticks_msec() - started > 55000:
				fail("UI timed out")
				return
			if not host.presentation.is_empty():
				print("CORE PRESENTATION ", JSON.stringify(host.presentation))
				await RenderingServer.frame_post_draw
				root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://../artifacts/python-story-refactor/world-preview-ui/ui-" + chapter + "-" + str(Time.get_ticks_msec()) + ".png"))
				host.get_node("Margin/Column/Next").pressed.emit()
		if host.last_error != "":
			fail(host.last_error)
			return
		print("UI TRANSCRIPT ", chapter, " ", host.transcript.get_parsed_text())
		if chapter == "arrival":
			host.get_node("Margin/Column/Chapter2").pressed.emit()
			await process_frame
			if host.engine.CurrentChapter() != "morning":
				fail("second chapter did not load")
				return
	print("PASS injected automatic UI preview, two chapters, actual Core output")
	quit(0)
