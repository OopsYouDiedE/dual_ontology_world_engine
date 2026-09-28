extends SceneTree
## Lossless publishing of the generated inspection art. Never reads the tool cache.
const SOURCE := "res://AssetsSource/Internal/Props/FirstAwakeningEvidence/"
const DESTINATION := "res://ShareAsset/Internal/Props/FirstAwakeningEvidence/"

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(DESTINATION)
	var count := 0
	for filename in DirAccess.get_files_at(SOURCE):
		if filename.ends_with(".png"):
			var error := DirAccess.copy_absolute(SOURCE + filename, DESTINATION + filename)
			if error != OK:
				push_error("Cannot publish evidence image: " + filename)
				quit(1)
				return
			count += 1
	print("Published %d generated inspection images without resizing." % count)
	quit()
