extends SceneTree

const OUTPUT := "res://../artifacts/first-awakening/"

func _initialize() -> void:
	call_deferred("capture")

func capture() -> void:
	var locale := "zh_CN"
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--locale="):
			locale = arg.trim_prefix("--locale=")
	TranslationServer.set_locale(locale)
	root.content_scale_size = Vector2i(1280, 720)
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	var scene: Node2D = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for i in 20:
		await process_frame
	scene.dialogue.reveal()
	for i in 4:
		await process_frame
	await RenderingServer.frame_post_draw
	var path := OUTPUT + "scene-i18n-" + locale + ".png"
	var error := root.get_texture().get_image().save_png(path)
	print("SCENE I18N CAPTURE: ", error, " ", path)
	quit(0 if error == OK else 1)
