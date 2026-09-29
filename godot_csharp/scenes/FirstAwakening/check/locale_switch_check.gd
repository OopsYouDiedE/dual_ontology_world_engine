extends SceneTree

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var original: String = root.get_node("GameLocale").current_language()
	var failures: Array[String] = []
	var locale: Node = root.get_node("GameLocale")
	locale.set_language("zh_CN")
	var scene: Node2D = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for i in 6: await process_frame
	scene.player_name = "林"
	for entry in [["en", "I woke in an unfamiliar single room", "English"], ["ja", "見知らぬ一人部屋", "日本語"], ["zh_CN", "我醒在一间陌生的单人房", "中文"]]:
		locale.set_language(entry[0])
		for i in 5: await process_frame
		if not scene.dialogue.body.text.contains(entry[1]):
			failures.append(entry[0] + " current dialogue")
		if scene.language_selector.get_item_text(scene.language_selector.selected) != entry[2]:
			failures.append(entry[0] + " HUD selector")
		if not scene.dialogue.speaker.text.begins_with("林"):
			failures.append(entry[0] + " player name survives locale refresh")
	locale.set_language(original)
	for failure in failures: push_error(failure)
	print("LOCALE SWITCH: %d failures" % failures.size())
	quit(0 if failures.is_empty() else 1)
