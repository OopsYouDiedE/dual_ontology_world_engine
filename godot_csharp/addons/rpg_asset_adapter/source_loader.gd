@tool
class_name RPGAssetSourceLoader
extends RefCounted

## Shared input loader for converter scripts.
## A res:// resource is preferred; raw image and text paths are supported for
## command-line conversion and editor-time tools.

static func resolve_image(source: Variant) -> Image:
	if source is Image:
		var image: Image = source
		return _decompress(image)
	if source is Texture2D:
		return _decompress(source.get_image())
	if source is String:
		var path: String = source
		if ResourceLoader.exists(path):
			var resource := ResourceLoader.load(path)
			if resource is Texture2D:
				return _decompress(resource.get_image())
		var global_path := ProjectSettings.globalize_path(path) if path.begins_with("res://") else path
		if FileAccess.file_exists(global_path):
			return _decompress(Image.load_from_file(global_path))
	return null


static func resolve_texture_and_image(source: Variant) -> Dictionary:
	var image := resolve_image(source)
	if image == null:
		return {"texture": null, "image": null}
	if source is Texture2D:
		return {"texture": source, "image": image}
	var texture := ImageTexture.create_from_image(image)
	return {"texture": texture, "image": image}


static func read_text(source: Variant) -> String:
	if source is String:
		var path: String = source
		var global_path := ProjectSettings.globalize_path(path) if path.begins_with("res://") else path
		if FileAccess.file_exists(global_path):
			return FileAccess.get_file_as_string(global_path)
		if FileAccess.file_exists(path):
			return FileAccess.get_file_as_string(path)
		return path
	return ""


static func _decompress(image: Image) -> Image:
	if image != null and image.is_compressed():
		image = image.duplicate()
		image.decompress()
	return image
