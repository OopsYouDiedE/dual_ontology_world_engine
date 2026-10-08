@static_unload
extends RefCounted
## 现场用到的物件外观名与显示词，全部来自剧本编译物（story/生态/物件名.story、
## 第一章“精灵热点”），宿主代码不自备任何设定文字。

const CONTENT := "res://scenes/Awakening/Script/generated_awakening_content.json"
static var _data: Dictionary

static func table(key: String) -> Dictionary:
	if _data.is_empty():
		var parsed = JSON.parse_string(FileAccess.get_file_as_string(CONTENT))
		_data = parsed if parsed is Dictionary else {}
	var value = _data.get(key, {})
	return value if value is Dictionary else {}

static func word(key: String) -> String:
	return str(table("FIELD_WORDS").get(key, ""))
