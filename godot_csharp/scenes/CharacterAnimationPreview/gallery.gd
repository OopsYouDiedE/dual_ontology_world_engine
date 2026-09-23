extends SceneTree
const ACTIONS := ["idle","walk","talk","inspect","interact","sit","break","cast"]
func _init() -> void:
	call_deferred("_run")
func _run() -> void:
	root.content_scale_size = Vector2i.ZERO
	root.size = Vector2i(1536,1152)
	var canvas := Node2D.new()
	canvas.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	root.add_child(canvas)
	var background := ColorRect.new()
	background.size = Vector2(1536,1152)
	background.color = Color("374447")
	canvas.add_child(background)
	for actor in 2:
		var frames: SpriteFrames = load("res://ShareAsset/Images/characters/animation_v2/"+(["detective","placeholder"][actor])+".tres")
		for direction in 3:
			for action in 8:
				var sprite := AnimatedSprite2D.new()
				sprite.sprite_frames = frames
				sprite.scale = Vector2(3,3)
				sprite.position = Vector2(action*192+96,(actor*3+direction)*192+80)
				canvas.add_child(sprite)
				var clip: String = ACTIONS[action]+"_"+(["front","back","left"][direction])
				sprite.play(clip)
				var label := Label.new()
				label.text = clip
				label.position = Vector2(action*192+12,(actor*3+direction)*192+164)
				canvas.add_child(label)
	for sample in 3:
		await create_timer(0.21).timeout
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://../artifacts/character-animation-v2/gallery-%d.png" % sample))
	quit()
