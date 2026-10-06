extends SceneTree
const OUT = "C:/Users/iii/Desktop/dual_ontology_world_engine/artifacts/ui-prefabs/"
func _initialize():
	call_deferred("run")
func run():
	print("EDITOR_HINT=", Engine.is_editor_hint())
	for item in [["dialogue", "res://scenes/Shared/UI/ChapterDialogue.tscn"], ["manuscript", "res://scenes/Shared/ReplayManuscript.tscn"]]:
		var viewport = SubViewport.new()
		viewport.size = Vector2i(1280, 960)
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var background = ColorRect.new()
		background.color = Color("23262d")
		background.size = Vector2(1280,960)
		viewport.add_child(background)
		var ui = load(item[1]).instantiate()
		viewport.add_child(ui)
		if ui is Control:
			ui.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		await process_frame
		await process_frame
		RenderingServer.force_draw(false)
		var image = viewport.get_texture().get_image()
		print(item[0], " dimensions=", image.get_size(), " blank=", image.is_empty())
		image.save_png(OUT + "editor-" + item[0] + ".png")
		viewport.queue_free()
		await process_frame
	quit()
