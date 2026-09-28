extends SceneTree

const DIALOGUE := preload("res://scenes/FirstAwakening/first_awakening.dialogue")
const LANGUAGE := preload("res://scenes/FirstAwakening/MindLanguage.gd")

func _initialize() -> void:
	call_deferred("_check")

func _check() -> void:
	var failures: Array[String] = []
	var manager: Node = root.get_node("DialogueManager")
	for entry in [["zh_CN", "脑内推演", "……喉咙有些发紧。脑袋像是刚从沉睡中苏醒，迟滞而沉重。"], ["en", "Mind Reconstruction", "My throat feels tight... My head is sluggish and heavy, as if only just waking from a deep sleep."], ["ja", "頭の中の推理", "……喉が少し詰まる。深い眠りから目覚めたばかりのように、頭が重く、動きが鈍い。"]]:
		TranslationServer.set_locale(entry[0])
		if TranslationServer.translate("FA_MIND_001") != entry[1]:
			failures.append(entry[0] + " mind UI translation")
		if TranslationServer.translate("FA_UI_050") == "FA_UI_050":
			failures.append(entry[0] + " game UI translation")
		var line = await manager.get_next_dialogue_line(DIALOGUE, "opening", [])
		if line == null or line.text != entry[2]:
			failures.append(entry[0] + " DialogueManager first line: " + (str(line.text) if line else "null"))
		var draft := "侦探观察水阀。"
		var result: Dictionary = LANGUAGE.compile_text(draft, {"water":{"title":"水阀"}}, "侦探", entry[0])
		if result.get("codes", []) != ["ACT|侦探|Inspect|water"]:
			failures.append(entry[0] + " draft parsing")
	for failure in failures: push_error(failure)
	print("I18N INTEGRATION: %d failures" % failures.size())
	quit(0 if failures.is_empty() else 1)
