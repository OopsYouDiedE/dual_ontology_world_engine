extends SceneTree

var host: Node

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	host = load("res://scenes/Host/Main.tscn").instantiate()
	root.add_child(host)
	await process_frame
	host.dispatch({"kind": "start"})
	await create_timer(25.0).timeout
	await RenderingServer.frame_post_draw
	var directory := ProjectSettings.globalize_path("res://../artifacts/partial-bootstrap-core/host-integration-03")
	DirAccess.make_dir_recursive_absolute(directory)
	root.get_texture().get_image().save_png(directory.path_join("startup.png"))
	var output := FileAccess.open(directory.path_join("snapshot.json"), FileAccess.WRITE)
	output.store_string(JSON.stringify(host.engine.Snapshot()))
	output.close()
	host.shutdown_completed.connect(func(): quit())
	host.begin_shutdown()
