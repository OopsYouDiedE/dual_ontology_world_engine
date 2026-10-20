extends SceneTree
## Geometry-only rendered inspection; no DV session or gameplay input.
func _initialize() -> void:
	root.unfocusable = true
	root.position = Vector2i(10000,10000)
	_run.call_deferred()
func _run() -> void:
	var directory := ""
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): directory = argument.substr(6)
	if directory == "" or FileAccess.file_exists(directory):
		quit(2)
		return
	var layout: Node2D = load("res://scenes/Chapter2/Maps/living_floor.tscn").instantiate()
	var vp := SubViewport.new()
	vp.size = Vector2i(layout.get_meta("map_size"))
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST
	root.add_child(vp)
	vp.add_child(layout)
	for i in 4: await process_frame
	await RenderingServer.frame_post_draw
	var error := vp.get_texture().get_image().save_png(directory)
	print("geometry-only overview save: ", error)
	quit(0 if error == OK else 1)
