extends SceneTree
## Drives actual mouse input through the scene's clickable objects.

var failed := false
const CAPTURE_DIR := "res://../artifacts/first-awakening/"

func _initialize() -> void:
	call_deferred("run")

func _require(ok: bool, message: String) -> void:
	if not ok:
		failed = true
		push_error("CLICK INTERACTION: " + message)

func _click_world(scene: Node2D, point: Vector2) -> void:
	var position := scene.get_viewport().get_canvas_transform() * point
	var motion := InputEventMouseMotion.new()
	motion.position = position
	Input.parse_input_event(motion)
	await process_frame
	var button := InputEventMouseButton.new()
	button.button_index = MOUSE_BUTTON_LEFT
	button.position = position
	button.pressed = true
	Input.parse_input_event(button)
	await process_frame
	button.pressed = false
	Input.parse_input_event(button)
	await process_frame

func _wait_until(predicate: Callable, frames: int) -> bool:
	for index in frames:
		if predicate.call():
			return true
		await physics_frame
	return predicate.call()

func _capture(name: String) -> void:
	if not OS.get_cmdline_user_args().has("--capture-click"):
		return
	await process_frame
	await RenderingServer.frame_post_draw
	var path := CAPTURE_DIR + "click-interaction-" + name + ".png"
	_require(root.get_texture().get_image().save_png(path) == OK, "screenshot saved: " + path)
	print("CLICK SCREENSHOT: " + path)

func run() -> void:
	TranslationServer.set_locale("zh_CN")
	var scene: Node2D = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for index in 5:
		await process_frame
	scene.dialogue.active = false
	scene.dialogue.panel.hide()
	scene.dialogue_after = ""
	await _capture("start")
	var drawer: Vector2 = scene.sites["drawer"].point
	await _click_world(scene, drawer + Vector2(0, -12))
	_require(scene.click_interaction_id == "drawer" and not scene.click_path.is_empty(), "distant object click creates an approach route")
	var drawer_opened := await _wait_until(func(): return scene.evidence.has("drawer") and scene.modal_open, 300)
	_require(drawer_opened, "detective walks to drawer and opens its detail card")
	await _capture("drawer")
	print("CLICK OBJECT: drawer_opened=" + str(drawer_opened) + " position=" + str(scene.player.position) + " distance=" + str(scene.player.position.distance_to(drawer)))
	scene._close_modal()
	await physics_frame
	_require(scene.hud_hint.text.begins_with("点击 / E · "), "nearby object HUD names the click and E controls")
	await _capture("hint")
	await _click_world(scene, drawer + Vector2(0, -12))
	_require(scene.modal_open and scene.click_path.is_empty(), "nearby object click interacts immediately without walking")
	scene._close_modal()
	var engineer: Vector2 = scene.actors["工程师"].position
	await _click_world(scene, engineer + Vector2(0, -36))
	_require(scene.click_interaction_id == "engineer" and not scene.click_path.is_empty(), "distant character click creates an approach route")
	var talked := await _wait_until(func(): return scene.dialogue.active and scene.npc_stage_seen.has("engineer"), 520)
	_require(talked, "detective walks through the doorway and talks to the engineer")
	for index in 45:
		await process_frame
	await _capture("engineer")
	print("CLICK CHARACTER: talked=" + str(talked) + " position=" + str(scene.player.position) + " distance=" + str(scene.player.position.distance_to(engineer)))
	scene.dialogue.active = false
	scene.dialogue.panel.hide()
	scene.dialogue_after = ""
	var transmitter: Vector2 = scene.sites["transmitter"].point
	await _click_world(scene, transmitter + Vector2(0, -12))
	_require(scene.click_interaction_id == "transmitter" and not scene.click_path.is_empty(), "wall fixture click creates an approach route")
	var rules_opened := await _wait_until(func(): return scene.dialogue.active or (scene.transmitter_rules and scene.modal_open), 420)
	_require(rules_opened, "detective reaches wall fixture and opens its rules")
	await _capture("transmitter")
	print("CLICK FIXTURE: rules_opened=" + str(rules_opened) + " position=" + str(scene.player.position) + " distance=" + str(scene.player.position.distance_to(transmitter)))
	print("CLICK INTERACTION: " + ("FAIL" if failed else "PASS"))
	quit(1 if failed else 0)
