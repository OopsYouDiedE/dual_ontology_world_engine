extends SceneTree
var destination: String
var diagnose := false
var zone_yi := false
var residences := false
func _initialize() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--out="): destination=arg.trim_prefix("--out=")
		if arg=="--diagnose": diagnose=true
		if arg=="--zone-yi": zone_yi=true
		if arg=="--residences": residences=true
	run.call_deferred()
func run() -> void:
	var view := SubViewport.new()
	view.size=Vector2i(1600,1600)
	view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(view)
	var scene: Node=load("res://scenes/ArtStyleTrials/E/library.tscn").instantiate()
	view.add_child(scene)
	await process_frame
	var ids: Array=scene.asset_ids()
	if zone_yi: ids=ids.filter(func(id): return str(id).begins_with("zone_yi:"))
	if residences: ids=ids.filter(func(id): return str(id).begins_with("zone_") or str(id).begins_with("living_hall:"))
	if diagnose: ids=["cast","cast-no-shadow","cast-no-ao","cast-no-ssil"]
	var observations: Array=[]
	for id in ids:
		view.size=Vector2i(1600,1000) if str(id).ends_with(":overview") and str(id).begins_with("zone_") else Vector2i(1600,1600)
		scene.select_asset("cast" if diagnose else id)
		if diagnose:
			scene.key_light.shadow_enabled=id!="cast-no-shadow"
			scene.get_node("WorldEnvironment").environment.ssao_enabled=id!="cast-no-ao"
			scene.get_node("WorldEnvironment").environment.ssil_enabled=id!="cast-no-ssil"
		await process_frame
		await process_frame
		await RenderingServer.frame_post_draw
		var path := destination.path_join(str(id).replace(":","--")+".png")
		var error := view.get_texture().get_image().save_png(path)
		observations.append({"id":id,"save_error":error,"metadata":scene.get_meta("render_metadata"),"settings":scene.render_settings_snapshot()})
		if error!=OK:
			push_error("Image save failed: "+path)
			quit(2)
			return
	if residences:
		var prefabs=load("res://scenes/ArtStyleTrials/E/prefabs/PrefabLibrary.gd")
		var folder: String=destination.path_join("prefabs")
		DirAccess.make_dir_recursive_absolute(folder)
		for key in prefabs.keys():
			var filename: String=String(key).validate_filename()+".tscn"
			if prefabs.save(key,folder.path_join(filename))!=OK:
				push_error("Prefab export failed: "+String(key))
				quit(3)
				return
	var payload := var_to_bytes(observations)
	var hash := HashingContext.new()
	hash.start(HashingContext.HASH_SHA256)
	hash.update(payload)
	var file := FileAccess.open(destination.path_join("observations.dvlog.partial"),FileAccess.WRITE)
	file.store_buffer("DVSTYLE1".to_ascii_buffer())
	file.store_64(payload.size())
	file.store_buffer(hash.finish())
	file.store_buffer(payload)
	file.close()
	DirAccess.rename_absolute(destination.path_join("observations.dvlog.partial"),destination.path_join("observations.dvlog"))
	quit()
