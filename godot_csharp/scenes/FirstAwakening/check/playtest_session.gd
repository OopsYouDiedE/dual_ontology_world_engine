extends SceneTree

const DIR := "res://../artifacts/first-awakening/"
var scene: Node2D
var last_seq := -1

func _initialize() -> void:
	call_deferred("start")

func start() -> void:
	scene = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for i in 8: await process_frame
	await capture("start")
	while true:
		var file := FileAccess.open(DIR + "playtest-command.json", FileAccess.READ)
		if file:
			var content := file.get_as_text()
			file.close()
			var command = JSON.parse_string(content)
			if command is Dictionary and int(command.get("seq", -1)) > last_seq:
				last_seq = int(command.seq)
				await execute(command)
		await create_timer(0.05).timeout

func execute(command: Dictionary) -> void:
	for action in command.get("actions", []):
		match str(action.get("type", "")):
			"tap": await key(int(action.get("key", 0)), int(action.get("unicode", 0)))
			"hold": await hold(int(action.get("key", 0)), int(action.get("frames", 1)))
			"text":
				for ch in str(action.get("text", "")):
					await key(KEY_NONE, ch.unicode_at(0))
			"click": await click(Vector2(float(action.get("x", 0)), float(action.get("y", 0))))
			"wait":
				for i in int(action.get("frames", 1)): await process_frame
	await capture(str(command.get("name", "step")))
	var result := {
		"seq": last_seq,
		"name": str(command.get("name", "step")),
		"player": [scene.player.position.x, scene.player.position.y],
		"nearest_title": scene.sites[scene.nearest].title if scene.nearest != "" else "",
		"dialogue_active": scene.dialogue.active,
		"dialogue_speaker": scene.dialogue.line.character if scene.dialogue.active and scene.dialogue.line else "",
		"dialogue_text": scene.dialogue.line.text if scene.dialogue.active and scene.dialogue.line else "",
		"dialogue_options": visible_options(),
		"modal_open": scene.modal_open,
		"screenshot": str(command.get("name", "step")) + ".png"
	}
	var file := FileAccess.open(DIR + "playtest-status.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(result, "\t"))
	print("PLAYTEST_STEP ", JSON.stringify(result))

func visible_options() -> Array:
	var options := []
	if scene.dialogue.active and scene.dialogue.choices.visible:
		for child in scene.dialogue.choices.get_children():
			if child is Button: options.append(child.text)
	return options

func key(code: int, unicode := 0) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.unicode = unicode
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event.duplicate())
	await process_frame

func hold(code: int, frames: int) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	Input.parse_input_event(event)
	for i in frames: await physics_frame
	event.pressed = false
	Input.parse_input_event(event.duplicate())
	await physics_frame

func click(point: Vector2) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = point
	Input.parse_input_event(motion)
	await process_frame
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = point
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	event.pressed = false
	Input.parse_input_event(event.duplicate())
	for i in 3: await process_frame

func capture(name: String) -> void:
	for i in 2: await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(DIR + "session-" + name + ".png")
