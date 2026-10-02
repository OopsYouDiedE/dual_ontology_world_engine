extends SceneTree
## Author development preview; staged positioning is not independent acceptance or played completion.
var scene
var failures: Array[String] = []
const OUTPUT := "res://../artifacts/first-awakening/style-study/dev-"

func _initialize() -> void:
	call_deferred("run")

func frames(count: int = 5) -> void:
	for index in count:
		await process_frame

func capture(tag: String) -> void:
	await frames(3)
	await RenderingServer.frame_post_draw
	var result := root.get_texture().get_image().save_png(OUTPUT + tag + ".png")
	if result != OK:
		failures.append("capture failed: " + tag)
	print("VISUAL CAPTURE ", tag)

func stop_dialogue() -> void:
	scene.dialogue.active = false
	scene.dialogue.panel.hide()

func click_world(point: Vector2) -> void:
	var event := InputEventMouseButton.new()
	event.position = scene.get_viewport().get_canvas_transform() * point
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = true
	Input.parse_input_event(event)
	await frames(2)
	event.pressed = false
	Input.parse_input_event(event)
	await frames(2)

func move_key(code: Key, duration: float) -> void:
	var event := InputEventKey.new()
	event.keycode = code
	event.physical_keycode = code
	event.pressed = true
	Input.parse_input_event(event)
	await create_timer(duration).timeout
	event.pressed = false
	Input.parse_input_event(event)
	await frames(2)

func click_button_text(part: String) -> void:
	for button in scene.ui.find_children("*", "Button", true, false):
		if part in button.text:
			var event := InputEventMouseButton.new()
			event.position = button.get_global_rect().get_center()
			event.button_index = MOUSE_BUTTON_LEFT
			event.pressed = true
			Input.parse_input_event(event)
			await frames(2)
			event.pressed = false
			Input.parse_input_event(event)
			await frames(4)
			return
	failures.append("button not found: " + part)

func run() -> void:
	TranslationServer.set_locale("zh_CN")
	scene = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	await frames(15)
	await capture("opening")
	stop_dialogue()
	scene.player.stand_up()
	await frames()
	await capture("hud")
	for view in [{"name":"study", "point":Vector2(500,700)}, {"name":"kitchen", "point":Vector2(865,660)}]:
		scene.player.position = view.point
		await frames(12)
		await capture(view.name)
	scene.player.position = Vector2(424,748)
	await frames(8)
	var before: Vector2 = scene.player.position
	await move_key(KEY_W,0.35)
	print("VISUAL COLLISION start=", before, " end=", scene.player.position)
	await capture("handle-collision")
	await click_world(Vector2(424,710))
	await capture("handle-inspection")
	for button in scene.ui.find_children("*","Button",true,false):
		print("VISUAL BUTTON ", button.name, " ", button.text)
	await click_button_text("按住")
	stop_dialogue()
	await capture("handle-pressed")
	await click_button_text("松开")
	stop_dialogue()
	await capture("handle-released")
	scene._close_modal()
	scene.player.position = Vector2(548,758)
	await frames(8)
	await click_world(Vector2(548,720))
	await capture("return-inspection")
	for button in scene.ui.find_children("*","Button",true,false):
		print("VISUAL RETURN BUTTON ", button.name, " ", button.text)
	await click_button_text("筹试片")
	stop_dialogue()
	await capture("return-colored-sample")
	scene._close_modal()
	await click_world(Vector2(548,720))
	await click_button_text("推动筹")
	stop_dialogue()
	await capture("return-single-print")
	scene._close_modal()
	scene.player.position = Vector2(865,638)
	await frames(8)
	await click_world(Vector2(865,596))
	await capture("basin-inspection")
	scene._close_modal()
	scene._request_flow()
	await frames(8)
	await capture("manuscript")
	for button in scene.ui.find_children("*","Button",true,false):
		print("VISUAL PAGE BUTTON ", button.name, " ", button.text)
	await click_button_text("重演")
	await frames(30)
	await capture("manuscript-replay")
	for button in scene.ui.find_children("*","Button",true,false):
		if "重演" in button.text:
			button.grab_focus()
			await capture("button-focus")
			var motion := InputEventMouseMotion.new()
			motion.position = button.get_global_rect().get_center()
			Input.parse_input_event(motion)
			await capture("button-hover")
			break
	print("VISUAL BASE CAPTURE COMPLETE ", failures)
	quit(1 if not failures.is_empty() else 0)


