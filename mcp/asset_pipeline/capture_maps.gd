extends SceneTree
## 从几何契约逐张载入正式地图，在首个出生点放侦探作比例。
## python mcp/godot_workspace.py --script res://asset_pipeline/capture_maps.gd -- --out=<新目录>
const ACTOR := preload("res://scenes/Story/StoryActor.gd")
const FRAMES := preload("res://ShareAsset/Internal/Characters/PixelCast/detective.tres")

func _initialize() -> void:
	root.size = Vector2i(1600, 1200)
	call_deferred("_run")

func _run() -> void:
	var out := ""
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--out="): out = arg.trim_prefix("--out=")
	if out == "":
		push_error("Supply --out=<new capture directory>")
		quit(1)
		return
	if not out.is_absolute_path():
		out = str(ProjectSettings.get_setting("duelview_tools/repository")).path_join(out)
	DirAccess.make_dir_recursive_absolute(out)
	var geometry: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://Contracts/geometry_contract.json"))
	for key in geometry.maps:
		var map: Node2D = (load(str(geometry.maps[key].scene)) as PackedScene).instantiate()
		root.add_child(map)
		var actor := CharacterBody2D.new()
		actor.set_script(ACTOR)
		actor.standard = FRAMES
		map.get_node("Objects").add_child(actor)
		var spawns := map.get_node("Spawns").get_children()
		actor.position = (spawns[0] as Marker2D).position
		var size: Vector2 = map.get_meta("map_size")
		var vp := SubViewport.new()
		vp.size = Vector2i(size)
		vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		vp.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST
		root.add_child(vp)
		map.reparent(vp)
		for i in 4:
			await process_frame
		await RenderingServer.frame_post_draw
		var img := vp.get_texture().get_image()
		img.save_png(out.path_join(key + ".png"))
		print("captured ", key, " ", size)
		vp.queue_free()
		await process_frame
	quit()
