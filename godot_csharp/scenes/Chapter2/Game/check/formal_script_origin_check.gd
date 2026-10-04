extends SceneTree
## 默认正式来源的自动胶水验证；只读默认新会话，不是盲玩。

var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, label: String, value: Variant = "") -> void:
	print("ORIGIN_CHECK ", "PASS " if ok else "FAIL ", label, " = ", value)
	if not ok:
		failures.append(label)

func run() -> void:
	var chapter := "chapter1"
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--chapter="):
			chapter = argument.substr(10)
	var path := "res://scenes/Main2D.tscn" if chapter == "chapter1" else "res://scenes/Chapter2/Game/Chapter2Game.tscn"
	var game: Node = load(path).instantiate()
	root.add_child(game)
	current_scene = game
	await create_timer(5.0).timeout
	if chapter == "chapter1":
		game = game.get_node("Awakening")
		check(game.engine.ScriptId() == "chapter1", "ScriptId", game.engine.ScriptId())
		check(game.engine.ScriptAssembly() == "Chapter1Script", "world assembly", game.engine.ScriptAssembly())
		check(game.STORY.raw_text == game.engine.DialogueText(), "STORY raw_text equals formal DialogueText", game.STORY.raw_text.length())
		check(game.STORY.titles.has("s1_wake"), "original opening title exists", game.STORY.titles.has("s1_wake"))
	else:
		check(game.world_script_path == "", "default script path", game.world_script_path)
		check(game.bridge.ScriptId() == "chapter2", "ScriptId", game.bridge.ScriptId())
		check(game.bridge.ScriptAssembly() == "Chapter2Script", "world assembly", game.bridge.ScriptAssembly())
		check(not game.state.is_empty(), "default state available", game.state.size())
		if not game.state.is_empty():
			check(game.state.det_place == "Hall", "state det_place", game.state.det_place)
			check(game.state.room.place == "Hall", "state room", game.state.room.place)
			check(game.MAP_PLACE.get(str(game.map.name), "") == "Hall", "map matches logical room", str(game.map.name))
		check(game.dialogue.panel.visible and not game.dialogue.body.text.is_empty(), "visible opening", game.dialogue.body.text)
	await process_frame
	await RenderingServer.frame_post_draw
	var target := ProjectSettings.globalize_path("res://../artifacts/remake-chapter2/shared-runtime/formal-origin-%s.png" % chapter)
	check(root.get_texture().get_image().save_png(target) == OK, "screenshot", target)
	print("ORIGIN_RESULT ", chapter, " failures=", failures.size())
	quit(0 if failures.is_empty() else 1)
