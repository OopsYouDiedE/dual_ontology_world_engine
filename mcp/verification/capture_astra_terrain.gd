extends SceneTree
## Finite real-renderer observations; no Core session and no world actions.
var output := ""
var observations: Array = []

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--out="): output = argument.trim_prefix("--out=")
	_run.call_deferred()

func _picture(viewport: SubViewport, name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var error := viewport.get_texture().get_image().save_png(output.path_join(name + ".png"))
	observations.append({"capture": name, "error": error, "usec": Time.get_ticks_usec()})

func _run() -> void:
	if output.is_empty():
		quit(2)
		return
	root.size = Vector2i(1100, 850)
	for action in ["move_left", "move_right", "move_up", "move_down"]:
		if not InputMap.has_action(action): InputMap.add_action(action)
	var stage = load("res://scenes/Highlights/Highlights.tscn").instantiate()
	root.add_child(stage)
	await process_frame
	stage._select_stage(0)
	await process_frame
	await _picture(stage.viewport, "gallery")
	var gallery_floor = stage.world.get_node("AuthoredGallery_floor")
	var ornament = stage.world.get_node("AuthoredGalleryBorderAndMedallion")
	var border_layer = ornament.get_node("AutomaticSourceBorder")
	var center_layer = ornament.get_node("SingleLargeDiamond")
	border_layer.hide()
	center_layer.hide()
	await _picture(stage.viewport, "gallery-base")
	border_layer.show()
	await _picture(stage.viewport, "gallery-border")
	center_layer.show()
	observations.append({"render_size": stage.viewport.size,"camera_zoom":stage.stage_camera.zoom,
		"border_source_to_screen":32.0 / 120.0 * stage.stage_camera.zoom.x,
		"floor_source_to_screen":7.0 / 24.0 * stage.stage_camera.zoom.x,
		"layers":[gallery_floor.name,border_layer.name,center_layer.name],
		"missing_material_channels":["authored normal","authored AO","authored emission mask"]})
	# The same runtime component, a larger camera for close inspection.
	var diagnostic := SubViewport.new()
	diagnostic.size = Vector2i(3840, 1280)
	diagnostic.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(diagnostic)
	var background := Polygon2D.new()
	background.polygon = PackedVector2Array([Vector2.ZERO, Vector2(960,0), Vector2(960,640), Vector2(0,640)])
	background.color = Color(0.24, 0.24, 0.22)
	diagnostic.add_child(background)
	var camera := Camera2D.new()
	camera.position = Vector2(480, 160)
	camera.zoom = Vector2(4, 4)
	diagnostic.add_child(camera)
	var all_shapes := []
	for index in 3:
		var cells: Array[Vector2i] = []
		for y in 6:
			for x in 8:
				var keep := true
				if index == 1: keep = x < 3 or y > 2
				if index == 2: keep = not (x >= 2 and x <= 5 and y >= 2 and y <= 3)
				if keep: cells.append(Vector2i(x, y))
		var border = load("res://scenes/Shared/Field/AuthoredTerrainBorder.gd").new()
		border.position = Vector2(48 + index * 300, 72)
		var floor_body = load("res://scenes/Shared/Field/AuthoredSurface.gd").new()
		diagnostic.add_child(floor_body)
		var areas: Array[Rect2] = []
		for cell in cells:
			areas.append(Rect2(border.position + Vector2(cell) * 32, Vector2(32,32)))
		floor_body.configure(load("res://ShareAsset/Internal/Props/AuthoredRooms/shura_gallery_floor.png"), 7.0 / 24.0, border.position, areas)
		diagnostic.add_child(border)
		border.configure(cells)
		await process_frame
		var resolved := []
		for cell in cells:
			resolved.append({"cell":cell,"atlas":border.get_cell_atlas_coords(cell),"source":border.get_cell_source_id(cell)})
		all_shapes.append({"layout":index,"cells":resolved})
	await _picture(diagnostic, "connections")
	observations.append({"topology_samples":all_shapes,"scope":"rectangle, L-shaped region and hole; source-cut overlay only"})
	var bytes := var_to_bytes(observations)
	var hash := HashingContext.new()
	hash.start(HashingContext.HASH_SHA256)
	hash.update(bytes)
	var path := output.path_join("observations.dvlog")
	var file := FileAccess.open(path + ".partial", FileAccess.WRITE)
	file.store_buffer("DVASTRA1".to_ascii_buffer())
	file.store_64(bytes.size())
	file.store_buffer(hash.finish())
	file.store_buffer(bytes)
	file.close()
	DirAccess.rename_absolute(path + ".partial", path)
	print("Captured actual gallery and three runtime terrain layouts; no Core session")
	stage.queue_free()
	diagnostic.queue_free()
	await process_frame
	quit()
