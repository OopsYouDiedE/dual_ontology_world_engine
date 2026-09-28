extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://../artifacts/first-three-scenes"))
	var args := OS.get_cmdline_user_args()
	for label in ["CentralHall", "Breakfast", "ParlorGathering"]:
		if "--breakfast-only" in args and label != "Breakfast":
			continue
		var scene: Node2D = load("res://scenes/Story/" + label + ".tscn").instantiate()
		root.add_child(scene)
		current_scene = scene
		for i in 10:
			await process_frame
		await RenderingServer.frame_post_draw
		var suffix := "-review" if "--review" in args else ""
		root.get_texture().get_image().save_png("res://../artifacts/first-three-scenes/" + label + suffix + ".png")
		scene.queue_free()
		current_scene = null
		await process_frame
	quit()
