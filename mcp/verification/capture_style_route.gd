extends SceneTree
## Four art routes share this finite renderer. No formal session is created.
var destination := ""
var route := ""
var records: Array = []
var started := 0

func _initialize() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--out="): destination = arg.trim_prefix("--out=")
		if arg.begins_with("--route="): route = arg.trim_prefix("--route=")
	_run.call_deferred()

func _lights(node: Node, result: Array) -> void:
	if node is Light2D:
		result.append({"path":str(node.get_path()),"position":node.global_position,
			"color":node.color,"energy":node.energy,"enabled":node.enabled,"shadows":node.shadow_enabled})
	elif node is Light3D:
		result.append({"path":str(node.get_path()),"transform":node.global_transform,
			"color":node.light_color,"energy":node.light_energy,"visible":node.visible,"shadows":node.shadow_enabled})
	for child in node.get_children(): _lights(child, result)

func _picture(view: SubViewport, scene: Node, name: String) -> void:
	await process_frame
	await RenderingServer.frame_post_draw
	var image := view.get_texture().get_image()
	var error := image.save_png(destination.path_join(name + ".png"))
	var lights: Array = []
	_lights(scene, lights)
	records.append({"capture":name,"elapsed_us":Time.get_ticks_usec()-started,
		"size":image.get_size(),"save_error":error,"lights":lights,
		"metadata":scene.get_meta("render_metadata", {})})
	if error != OK: push_error("Screenshot save failed: " + str(error))
	if scene.has_method("render_settings_snapshot"):
		records.append({"capture":name,"render_settings":scene.render_settings_snapshot()})
	if scene.has_method("room_visibility_snapshot"):
		var state: Dictionary=scene.room_visibility_snapshot()
		records.append({"capture":name,"room_visibility":state})
		if state.visible_rooms != [state.active]: push_error("More than current room is visible")
		for id in state.lit_rooms:
			if id != state.active: push_error("Inactive room light remains enabled")

func _run() -> void:
	if not route in ["A","B","C","D","E"] or destination.is_empty():
		push_error("Require route A/B/C/D/E and absolute --out")
		quit(2)
		return
	started = Time.get_ticks_usec()
	root.size = Vector2i(1000,1000)
	var view := SubViewport.new()
	view.size = Vector2i(1600,1600)
	view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	view.transparent_bg = false
	view.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST if route == "B" else Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_LINEAR
	root.add_child(view)
	var packed = load("res://scenes/ArtStyleTrials/" + route + "/scene.tscn")
	if not packed is PackedScene:
		push_error("Missing route scene")
		quit(2)
		return
	var scene: Node = packed.instantiate()
	view.add_child(scene)
	await process_frame
	if not scene.has_method("configure_lighting"):
		push_error("Route lacks configure_lighting")
		quit(2)
		return
	scene.configure_lighting("normal")
	await create_timer(0.5).timeout
	await _picture(view, scene, "normal")
	await create_timer(0.5).timeout
	await _picture(view, scene, "normal-dynamic")
	for mode in ["off","shift"]:
		scene.configure_lighting(mode)
		await create_timer(0.5).timeout
		await _picture(view, scene, mode)
	if route == "E" and scene.has_method("show_room"):
		scene.configure_lighting("normal")
		scene.set_surface_effects(false)
		await create_timer(0.3).timeout
		await _picture(view, scene, "effects-off")
		scene.set_surface_effects(true)
		for id in ["jia","yi","bing","ding","bath","hall"]:
			if not scene.show_room(id): push_error("Room switch failed: " + id)
			await create_timer(0.2).timeout
			await _picture(view, scene, "room-"+id)
		var state: Dictionary=scene.room_visibility_snapshot()
		if state.hall_west != state.jia_east or state.hall_east != state.ding_west or state.hall_south != state.bath_north or state.bing_east != state.hall_west or state.yi_west != state.hall_east:
			push_error("Shared wall planes do not coincide")
		for detail in ["reading","gathering"]:
			scene.inspect_detail(detail)
			await _picture(view, scene, "detail-"+detail)
		scene.show_room("hall")
	var payload := var_to_bytes(records)
	var hash := HashingContext.new()
	hash.start(HashingContext.HASH_SHA256)
	hash.update(payload)
	var path := destination.path_join("observations.dvlog")
	var file := FileAccess.open(path + ".partial", FileAccess.WRITE)
	if file == null:
		push_error("Cannot seal route observations")
		quit(2)
		return
	file.store_buffer("DVSTYLE1".to_ascii_buffer())
	file.store_64(payload.size())
	file.store_buffer(hash.finish())
	file.store_buffer(payload)
	file.close()
	var result := DirAccess.rename_absolute(path + ".partial", path)
	scene.queue_free()
	view.queue_free()
	await process_frame
	print("Art route ",route,": normal, dynamic, off, shift captured at 1600x1600; no DV session")
	quit(0 if result == OK else 2)
