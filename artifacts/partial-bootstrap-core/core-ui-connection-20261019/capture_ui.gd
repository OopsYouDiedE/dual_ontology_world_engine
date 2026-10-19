extends SceneTree
## Rendering capture; optional one-button engineering exercise is not play validation.
func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var destination := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--capture-dir="): destination = arg.trim_prefix("--capture-dir=")
	if destination == "" or DirAccess.dir_exists_absolute(destination):
		push_error("Capture output must be a new directory")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(destination)
	change_scene_to_file("res://scenes/Host/Main.tscn")
	var exercised := false
	for index in range(6):
		await create_timer(5.0).timeout
		await RenderingServer.frame_post_draw
		var image := root.get_texture().get_image()
		image.save_png(destination.path_join("frame-%02d.png" % index))
		var host := current_scene
		if host != null and host.get("engine") != null:
			var file := FileAccess.open(destination.path_join("state-%02d.json" % index), FileAccess.WRITE)
			file.store_string(JSON.stringify(host.engine.Snapshot()))
			file.close()
			var geometry := FileAccess.open(destination.path_join("geometry-%02d.json" % index), FileAccess.WRITE)
			geometry.store_string(JSON.stringify(host.field.context() if host.field != null else {}))
			geometry.close()
			if "--exercise-first-option" in OS.get_cmdline_user_args() and not exercised and host.ui != null and host.ui.modal_root != null:
				var menu: Control = _choice_menu(host.ui.modal_root)
				if menu != null and not menu.buttons.is_empty():
					var id = menu.buttons.keys()[0]
					var button: Button = menu.button_for(id)
					if button != null and not button.disabled:
						print(JSON.stringify({"engineering_button_activation": id, "label": button.text, "not_physical_input": true}))
						exercised = true
						button.pressed.emit()
	var host := current_scene
	if host != null:
		host.begin_shutdown.call_deferred()
		await host.shutdown_completed
	quit()

func _choice_menu(node: Node) -> Control:
	if node.has_method("button_for"): return node as Control
	for child in node.get_children():
		var found := _choice_menu(child)
		if found != null: return found
	return null
