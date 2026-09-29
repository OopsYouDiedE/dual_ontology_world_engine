extends SceneTree

const DIALOGUE := preload("res://scenes/FirstAwakening/first_awakening.dialogue")
const LANGUAGE := preload("res://scenes/FirstAwakening/MindLanguage.gd")
const SITES := preload("res://scenes/FirstAwakening/sites.tres")

func _initialize() -> void:
	call_deferred("_check")

func _check() -> void:
	var failures: Array[String] = []
	var manager: Node = root.get_node("DialogueManager")
	for entry in [["zh_CN", "脑内推演", "我醒在一间陌生的单人房。走廊下方传来了说话声。"], ["en", "Mind Reconstruction", "I woke in an unfamiliar single room. Voices came from the hall down the corridor."], ["ja", "頭の中の推理", "見知らぬ一人部屋で目が覚めた。廊下を下った先の広間から話し声が聞こえる。"]]:
		TranslationServer.set_locale(entry[0])
		if TranslationServer.translate("FA_MIND_001") != entry[1]:
			failures.append(entry[0] + " mind UI translation")
		if TranslationServer.translate("FA_UI_050") == "FA_UI_050":
			failures.append(entry[0] + " game UI translation")
		for key in ["FA_UI_269", "FA_UI_270", "FA_UI_271", "FA_UI_272", "FA_UI_351", "FA_UI_352", "FA_UI_353", "FA_UI_354", "FA_UI_356", "FA_UI_357", "FA_UI_358", "FA_UI_359", "FA_UI_360", "FA_UI_361"]:
			if TranslationServer.translate(key) == key:
				failures.append(entry[0] + " name window translation: " + key)
		var id_pattern := RegEx.create_from_string("^FA_UI_[0-9]+$")
		for site in SITES.entries:
			for field in ["title_id", "description_id"]:
				var key: String = site[field]
				if id_pattern.search(key) == null or TranslationServer.translate(key) == key:
					failures.append(entry[0] + " site translation: " + site.id + "/" + field)
		var name_lines := {"FA_NAME_INTRO": "原来是 {{player_name}}。我是霍铎，这两位是沈鼎和苏泠。你也是签了协议，坐船来的？", "FA_NAME_PAIR": "我没意见。两个人一组，效率最高。{{player_name}}，你想先跟谁去哪间？"}
		for key in name_lines:
			var source: String = name_lines[key]
			var translated := TranslationServer.translate(source, key)
			if entry[0] != "zh_CN" and (translated == source or not translated.contains("{{player_name}}")):
				failures.append(entry[0] + " player-name placeholder: " + key)
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
