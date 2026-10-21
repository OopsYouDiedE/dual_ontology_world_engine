extends SceneTree

var host: Node
var directory := ""

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): directory = argument.substr(6)
	if directory == "" or DirAccess.dir_exists_absolute(directory):
		push_error("Supply --out=<new absolute evidence directory>; existing evidence is never overwritten")
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(directory)
	host = load("res://scenes/Host/Main.tscn").instantiate()
	root.add_child(host)
	await process_frame
	host.dispatch({"kind": "start"})
	await create_timer(8.0).timeout
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(directory.path_join("startup.png"))
	var archive: Node = load("res://tools/FrameArchive.cs").new()
	root.add_child(archive)
	archive.WriteRecord(directory.path_join("snapshot.dvlog"), JSON.stringify(host.engine.Snapshot()))
	host.shutdown_completed.connect(func(): quit())
	host.begin_shutdown()
