extends SceneTree

func _initialize() -> void:
	assert(load("res://scenes/FirstAwakening/ReplayStage.gd") != null, "replay stage parses")
	for locale in ["zh_CN", "en", "ja"]:
		TranslationServer.add_translation(load("res://scenes/FirstAwakening/i18n/replay_ui.%s.translation" % locale))
		TranslationServer.set_locale(locale)
		var bridge := preload("res://scenes/FirstAwakening/HallReplayBridge.cs").new()
		root.add_child(bridge)
		var trace: Dictionary = bridge.Run(["BraceDoorWithWedge"])
		var expected := "木楔支撑" if locale == "zh_CN" else "The wedge props" if locale == "en" else "くさびが扉を支え"
		assert(str(trace.descriptions[0]).begins_with(expected), locale + " Core trace translation")
		var draft: Dictionary = bridge.CompileDraft(["ACT|侦探|GoTo|outside"], {})
		var caption := str(draft.states[1].caption)
		assert(caption.contains("侦探") if locale == "zh_CN" else caption.contains("Detective") if locale == "en" else caption.contains("探偵"), locale + " actor caption")
		assert(caption.contains("隔离门") if locale == "zh_CN" else caption.contains("The isolation door") if locale == "en" else caption.contains("隔離扉"), locale + " failed movement")
		var observation: Dictionary = bridge.CompileDraft(["WHEN|侦探|Smoke|door"], {"door": {"title": "隔离门"}})
		assert(not observation.has("error"), locale + " observation compile")
		if locale != "zh_CN":
			assert(not str(observation.events[0]).contains("门边"), locale + " observation caption")
		bridge.queue_free()
	print("REPLAY_I18N_CHECK_OK")
	quit()
