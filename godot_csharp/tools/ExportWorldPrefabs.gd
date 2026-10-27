extends SceneTree
## Permanent authoring exporter. This builds assets; it is not a gameplay test.
const PREFABS = preload("res://scenes/World/Art/prefabs/PrefabLibrary.gd")

func _initialize() -> void:
	call_deferred("_export")

func _export() -> void:
	PREFABS.authoring = true
	var previous: Dictionary = {}
	if FileAccess.file_exists(PREFABS.DIRECTORY + "catalog.json"):
		previous = JSON.parse_string(FileAccess.get_file_as_string(PREFABS.DIRECTORY + "catalog.json"))
	var scene := load("res://scenes/World/Art/library.tscn") as PackedScene
	var kit := scene.instantiate()
	if not kit.has_method("asset_ids"):
		push_error("Library script failed to load; export aborted")
		quit(1)
		return
	root.add_child(kit)
	await process_frame
	var ids: Array = kit.asset_ids()
	var requested: Array[String] = []
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--map="): requested.append(argument.trim_prefix("--map="))
	if not requested.is_empty():
		PREFABS.authoring = false
		for id in requested:
			if id not in ids:
				push_error("Unknown map: " + id)
				quit(1)
				return
			kit.select_asset(id)
			await process_frame
			if not _save_requested_prefabs(previous):
				quit(1)
				return
			if not _save_map(kit, id):
				quit(1)
				return
			print("Exported requested map: " + id)
		kit.queue_free()
		await process_frame
		quit(0)
		return
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://scenes/World/Maps3D"))
	for id in ids:
		if id == "knowledge-demo" or id == "cast": continue
		kit.select_asset(id)
		await process_frame
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(PREFABS.DIRECTORY))
	var manifest := {}
	for key in PREFABS.keys():
		var path := PREFABS.resource_path(key)
		var error := PREFABS.save(key, path)
		if error != OK:
			push_error("Cannot export %s: %s" % [key, error])
			quit(1)
			return
		manifest[key] = path
	# A second pass writes actual editable scene trees after prefab files exist.
	for id in ids:
		if id == "knowledge-demo" or id == "cast": continue
		kit.select_asset(id)
		await process_frame
		if not _save_map(kit, str(id)):
			quit(1)
			return
	var file := FileAccess.open(PREFABS.DIRECTORY + "catalog.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(manifest, "\t", true))
	file.close()
	for obsolete in previous.values():
		if obsolete not in manifest.values() and str(obsolete).begins_with(PREFABS.DIRECTORY) and str(obsolete).get_base_dir() == PREFABS.DIRECTORY.trim_suffix("/"):
			print("Retained previous prefab for explicit per-file cleanup: " + str(obsolete))
	print("Exported %d reusable furniture scenes" % manifest.size())
	kit.queue_free()
	await process_frame
	quit(0)

func _save_requested_prefabs(manifest: Dictionary) -> bool:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(PREFABS.DIRECTORY))
	for key in PREFABS.keys():
		var path := PREFABS.resource_path(key)
		var error := PREFABS.save(key, path)
		if error != OK:
			push_error("Cannot export prefab %s: %s" % [key, error])
			return false
		manifest[key] = path
	var file := FileAccess.open(PREFABS.DIRECTORY + "catalog.json", FileAccess.WRITE)
	if file == null: return false
	file.store_string(JSON.stringify(manifest, "\t", true))
	file.close()
	return true

func _save_map(kit: Node3D, id: String) -> bool:
	kit.detail_controller.restore_authored_meshes()
	var world := Node3D.new()
	world.name = "WorldMap"
	world.set_meta("source_layout", "res://Content/presentation.dv")
	world.set_meta("layout_id", id)
	world.set_meta("scope", "authored-3d-map; formal-host-connection-pending")
	for node in [kit.camera, kit.key_light, kit.get_node("WorldEnvironment"), kit.atmosphere, kit.time_lighting]:
		world.add_child(node.duplicate())
	for room in kit.rooms.values(): world.add_child(room.duplicate())
	var detail_controller := preload("res://rendering/RenderDetailController.gd").new()
	detail_controller.name = "RenderDetailController"
	world.add_child(detail_controller)
	var occlusion := preload("res://rendering/CameraOcclusion.gd").new()
	occlusion.name = "CameraOcclusion"
	occlusion.auto_configure = true
	occlusion.focus = kit.occlusion.focus
	world.add_child(occlusion)
	_replace_prefabs(world)
	_own_map(world, world)
	var packed := PackedScene.new()
	var error := packed.pack(world)
	if error == OK:
		var path := "res://scenes/World/Maps3D/" + id.replace(":", "_") + ".tscn"
		error = ResourceSaver.save(packed, path)
	world.free()
	if error != OK: push_error("Map export failed: %s (%s)" % [id, error])
	return error == OK

func _replace_prefabs(parent: Node) -> void:
	for child in parent.get_children():
		var key: String = str(child.get_meta("prefab_id", ""))
		if not key.is_empty() and FileAccess.file_exists(PREFABS.resource_path(key)):
			var replacement := (load(PREFABS.resource_path(key)) as PackedScene).instantiate() as Node3D
			replacement.name = child.name
			replacement.transform = child.transform
			for metadata in child.get_meta_list(): replacement.set_meta(metadata, child.get_meta(metadata))
			parent.remove_child(child)
			child.free()
			parent.add_child(replacement)
		else:
			_replace_prefabs(child)

func _own_map(node: Node, owner_root: Node) -> void:
	for child in node.get_children():
		child.owner = owner_root
		# glTF material/shadow overrides are authoring changes, not the source defaults.
		if child.scene_file_path.get_extension().to_lower() in ["glb", "gltf"] and not child.scene_file_path.begins_with("res://ShareAsset/Internal/Characters/UserModels/"):
			child.scene_file_path = ""
		if child.scene_file_path.is_empty(): _own_map(child, owner_root)
