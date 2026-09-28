extends SceneTree
## Validate the actual Godot dependency graph after importing or rebuilding assets.

const RESOURCE_EXTENSIONS := ["tscn", "tres", "res", "png"]
var failures: Array[String] = []
var checked: Dictionary = {}
var resource_count := 0

func _initialize() -> void:
	call_deferred("run")

func fail(message: String) -> void:
	failures.append(message)
	push_error(message)

func inspect_resource(path: String) -> void:
	if checked.has(path):
		return
	checked[path] = true
	if path.begins_with("res://AssetsSource/"):
		fail("Runtime dependency enters ignored source directory: " + path)
		return
	if not ResourceLoader.exists(path):
		fail("Missing runtime resource: " + path)
		return
	if path.get_extension() not in RESOURCE_EXTENSIONS:
		return
	resource_count += 1
	for dependency in ResourceLoader.get_dependencies(path):
		# Dependencies may be path-only or UID::type::fallback-path.
		var parts := dependency.split("::")
		var target: String = parts[parts.size() - 1]
		if target.begins_with("uid://"):
			var id := ResourceUID.text_to_id(target)
			if ResourceUID.has_id(id):
				target = ResourceUID.get_id_path(id)
		if target.begins_with("res://AssetsSource/"):
			fail(path + " depends on ignored source: " + target)
		else:
			inspect_resource(target)
	if ResourceLoader.load(path) == null:
		fail("Godot could not load: " + path)

func scan(path: String) -> void:
	var directory := DirAccess.open(path)
	if directory == null:
		fail("Missing resource directory: " + path)
		return
	for child in directory.get_directories():
		if child in ["bin", "obj"] or child.begins_with("."):
			continue
		scan(path.path_join(child))
	for file in directory.get_files():
		if file.get_extension() in RESOURCE_EXTENSIONS:
			inspect_resource(path.path_join(file))

func run() -> void:
	if not FileAccess.file_exists("res://AssetsSource/.gdignore"):
		fail("AssetsSource must contain .gdignore")
	for old_root in ["Images", "Victorian", "ReasoningStudy", "TileMap", "Props"]:
		if DirAccess.dir_exists_absolute("res://ShareAsset/" + old_root):
			fail("Retired asset root still exists: " + old_root)
	scan("res://ShareAsset")
	scan("res://scenes")
	var report := {
		"date": Time.get_datetime_string_from_system(),
		"resources_loaded": resource_count,
		"dependency_paths_checked": checked.size(),
		"failures": failures,
	}
	var output := "res://../artifacts/asset-consolidation"
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output))
	var file := FileAccess.open(output.path_join("resource-validation.json"), FileAccess.WRITE)
	if file == null:
		fail("Cannot write resource validation report")
	else:
		file.store_string(JSON.stringify(report, "\t") + "\n")
	print("Asset layout: ", resource_count, " resources, ", checked.size(), " dependency paths, ", failures.size(), " failures")
	quit(0 if failures.is_empty() else 1)
