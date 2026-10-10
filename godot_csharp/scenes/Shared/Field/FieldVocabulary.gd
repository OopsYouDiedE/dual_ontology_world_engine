@static_unload
extends RefCounted
## 现场用到的物件外观名与显示词，全部来自剧本编译物（story/生态/物件名.story、
## 第一章“精灵热点”），宿主代码不自备任何设定文字。

const CONTENT := "res://scenes/Awakening/Script/generated_awakening_content.json"
const Bindings := preload("res://scenes/Shared/Field/PresentationBindings.gd")
static var _data: Dictionary

static func table(key: String) -> Dictionary:
	if _data.is_empty():
		var parsed = JSON.parse_string(FileAccess.get_file_as_string(CONTENT))
		_data = parsed if parsed is Dictionary else {}
	var value = _data.get(key, {})
	return value if value is Dictionary else {}

static func word(key: String) -> String:
	return str(table("FIELD_WORDS").get(key, ""))

static func sprite_parts(sprite: Sprite2D) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	var presentation := table("PRESENTATION")
	if not presentation.get("composites") is Dictionary:
		Bindings.fail(sprite, "Missing composite presentation declarations")
		return result
	var definitions: Dictionary = presentation.composites
	if not definitions.has(sprite.texture.resource_path): return result
	var parts: Dictionary = definitions[sprite.texture.resource_path]
	for key in parts:
		var spec: Dictionary = parts[key]
		var title := word(str(spec.word))
		if title == "":
			Bindings.fail(sprite, "Missing composite vocabulary binding: " + str(spec.word))
			return []
		var rect := Rect2(float(spec.x), float(spec.y), float(spec.width), float(spec.height))
		if not Rect2(Vector2.ZERO, sprite.texture.get_size()).encloses(rect):
			Bindings.fail(sprite, "Composite region exceeds texture: " + str(key))
			return []
		result.append({"rect": Rect2(sprite.get_rect().position + rect.position, rect.size), "title": title})
	return result
