extends SceneTree
## Runtime acceptance harness for CharacterAnimationPreview.
## Missing resources are failures; no synthetic frames are accepted.

const PATHS := ["res://ShareAsset/Images/characters/animation_v2/detective.tres", "res://ShareAsset/Images/characters/animation_v2/placeholder.tres"]
const ACTIONS := {"idle":1,"walk":8,"talk":3,"inspect":4,"interact":4,"sit":4,"break":6,"cast":7}
const DIRS := ["front","back","left"]
var failures: Array[String] = []

func _init() -> void:
	call_deferred("_run")

func _run() -> void:
	var resources_ok := _check_resources()
	if resources_ok:
		await _check_preview_behavior()
	else:
		print("SKIP runtime behavior: required SpriteFrames resources are missing or invalid")
	await _capture()
	if failures.is_empty():
		print("PASS CharacterAnimationPreview acceptance")
	else:
		for failure in failures: push_error(failure)
		print("FAIL CharacterAnimationPreview acceptance: ", failures.size())
	quit(1 if not failures.is_empty() else 0)

func _check_resources() -> bool:
	var all_ok := true
	for path in PATHS:
		if not ResourceLoader.exists(path):
			failures.append("missing SpriteFrames resource: %s" % path)
			all_ok = false
			continue
		var frames = load(path)
		if not frames is SpriteFrames:
			failures.append("resource is not SpriteFrames: %s" % path)
			all_ok = false
			continue
		for action in ACTIONS:
			for direction in DIRS:
				var animation := "%s_%s" % [action, direction]
				if not frames.has_animation(animation):
					failures.append("missing animation %s in %s" % [animation, path])
					all_ok = false
					continue
				var expected: int = ACTIONS[action]
				var count: int = frames.get_frame_count(animation)
				if count != expected:
					failures.append("%s has %d frames, expected %d" % [animation, count, expected])
					all_ok = false
				for frame_index in count:
					var texture: Texture2D = frames.get_frame_texture(animation, frame_index)
					if texture == null:
						failures.append("%s frame %d is null" % [animation, frame_index]); all_ok = false; continue
					if texture.get_width() != 64 or texture.get_height() != 64:
						failures.append("%s frame %d is %dx%d, expected 64x64" % [animation, frame_index, texture.get_width(), texture.get_height()]); all_ok = false
					if not _check_pixels(texture, animation, frame_index): all_ok = false
	return all_ok

func _check_pixels(texture: Texture2D, animation: String, frame_index: int) -> bool:
	var image := texture.get_image()
	var non_empty := false
	var binary_alpha := true
	var border_clear := true
	for y in image.get_height():
		for x in image.get_width():
			var alpha := image.get_pixel(x, y).a
			if alpha > 0.0: non_empty = true
			if alpha > 0.0 and alpha < 1.0: binary_alpha = false
			if (x == 0 or y == 0 or x == image.get_width()-1 or y == image.get_height()-1) and alpha > 0.0: border_clear = false
	if not non_empty: failures.append("%s frame %d is empty" % [animation, frame_index])
	if not binary_alpha: failures.append("%s frame %d has non-binary alpha" % [animation, frame_index])
	if not border_clear: failures.append("%s frame %d touches 64x64 border" % [animation, frame_index])
	return non_empty and binary_alpha and border_clear

func _check_preview_behavior() -> void:
	var scene = load("res://scenes/CharacterAnimationPreview/CharacterAnimationPreview.tscn")
	if scene == null:
		failures.append("preview scene could not load")
		return
	var preview = scene.instantiate()
	get_root().add_child(preview)
	await process_frame
	await physics_frame
	if preview.actors.size() != 2 or preview.sprites.size() != 2:
		failures.append("preview did not create two actor/sprite pairs")
		return
	var sprite: AnimatedSprite2D = preview.sprites[0]
	for zoom in range(1,5):
		preview.set_zoom(zoom)
		if preview.world.scale != Vector2.ONE*zoom: failures.append("integer zoom mismatch")
	preview.set_zoom(3)
	preview.turn_to("right")
	if not sprite.flip_h or not sprite.centered or sprite.position != Vector2(0,-23): failures.append("right mirror anchor is not centered/(0,-23)")

	# Pause, single-frame, and resume are checked against real AnimatedSprite2D playback.
	preview.play_action("walk")
	await process_frame
	preview.toggle_pause()
	if not preview.paused or sprite.is_playing(): failures.append("pause did not stop animation")
	var paused_frame := sprite.frame
	preview.step_frame()
	if sprite.frame == paused_frame: failures.append("single-frame did not advance")
	preview.toggle_pause()
	if preview.paused or not sprite.is_playing(): failures.append("resume did not restart animation")

	# Door passage: move through the 32px opening at y=112, then verify the wall blocks y=176.
	var actor: CharacterBody2D = preview.actors[0]
	actor.position = Vector2(276,112)
	preview.turn_to("right")
	Input.action_press("ui_right")
	for i in 240: await physics_frame
	Input.action_release("ui_right")
	if actor.position.x <= 304.0: failures.append("actor did not pass through 32px door opening")
	actor.position = Vector2(276,176)
	Input.action_press("ui_right")
	for i in 240: await physics_frame
	Input.action_release("ui_right")
	if actor.position.x > 296.5: failures.append("actor crossed solid east wall")

	# Table sorting: actor Y is compared with the table visual layer.
	var table = preview.table_visual
	actor.position = Vector2(220,80); await physics_frame
	if actor.z_index >= table.z_index: failures.append("actor above table while in front/top position")
	actor.position = Vector2(220,150); await physics_frame
	if actor.z_index <= table.z_index: failures.append("actor did not sort in front of table")

	# Sit holds its last frame and reverses on the second sit action.
	actor.position = Vector2(234,181)
	preview.play_action("sit")
	await _wait_animation_finished(sprite)
	if not preview.seated[0] or sprite.is_playing() or sprite.frame != sprite.sprite_frames.get_frame_count(sprite.animation)-1: failures.append("sit did not hold final frame")
	if actor.position != preview.CHAIR+Vector2(8,0): failures.append("right-facing seat anchor mismatch")
	preview.play_action("sit")
	if not preview.rising[0] or sprite.get_playing_speed() >= 0.0: failures.append("sit did not start reverse rise")
	await _wait_animation_finished(sprite)
	if preview.rising[0] or preview.seated[0]: failures.append("rise did not finish and clear seat state")

	# Interact resolves only at frame 2 while facing back below the notice board.
	actor.position = Vector2(70,103); preview.turn_to("back"); preview.play_action("interact")
	await _wait_until_frame(sprite, 2)
	if not preview.touched: failures.append("interact did not resolve at frame 2")

	# Break resolves only at frame 3 and disables the crate collision afterward.
	preview.crate_broken = false; preview.crate_body.get_child(0).set_deferred("disabled", false)
	actor.position = Vector2(260,179); preview.turn_to("right"); preview.play_action("break")
	await _wait_until_frame(sprite, 2)
	if preview.crate_broken: failures.append("crate broke before frame 3")
	await _wait_until_frame(sprite, 3)
	await physics_frame
	if not preview.crate_broken or not preview.crate_body.get_child(0).disabled: failures.append("crate did not break/disable at frame 3")

func _wait_until_frame(sprite: AnimatedSprite2D, target: int) -> void:
	for i in 240:
		await create_timer(0.01).timeout
		if sprite.frame >= target: return
	failures.append("timed out waiting for frame %d of %s" % [target, sprite.animation])

func _wait_animation_finished(sprite: AnimatedSprite2D) -> void:
	for i in 240:
		await create_timer(0.01).timeout
		if not sprite.is_playing() or String(sprite.animation).begins_with("idle_"): return
	failures.append("timed out waiting for animation %s" % sprite.animation)

func _capture() -> void:
	if DisplayServer.get_name().to_lower() == "headless":
		print("SKIP screenshot: headless renderer")
		return
	await RenderingServer.frame_post_draw
	var path := ProjectSettings.globalize_path("res://../artifacts/character-animation-v2/preview.png")
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	get_root().get_viewport().get_texture().get_image().save_png(path)
	print("CAPTURE screenshot: ", path)



