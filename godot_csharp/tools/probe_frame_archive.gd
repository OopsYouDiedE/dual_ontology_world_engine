extends SceneTree
## Neutral pixel/atomic-write diagnostic. Never loads a game or advances DV.
var archive: Node
var directory := ""

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--out="): directory = arg.trim_prefix("--out=")
	if directory == "" or DirAccess.dir_exists_absolute(directory):
		quit(2)
		return
	DirAccess.make_dir_recursive_absolute(directory)
	archive = load("res://scenes/Host/FrameArchive.cs").new()
	root.add_child(archive)
	archive.Configure(0)
	var pixels := PackedByteArray([255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0, 1, 2, 3, 4, 255, 255, 255, 255, 0, 0, 0, 255])
	var original := FileAccess.open(directory.path_join("existing.png"), FileAccess.WRITE)
	original.store_string("existing bytes must survive")
	original.close()
	archive.Enqueue(pixels, 3, 2, directory.path_join("pixels.png"), 0)
	archive.Enqueue(pixels, 3, 2, directory.path_join("existing.png"), 0)
	while not archive.IsIdle(): await process_frame
	var rows := FileAccess.open(directory.path_join("results.jsonl"), FileAccess.WRITE)
	for row in archive.PollCompleted(): rows.store_line(str(row))
	rows.close()
	quit()
