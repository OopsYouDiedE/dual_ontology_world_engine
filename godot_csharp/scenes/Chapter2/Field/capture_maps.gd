extends SceneTree
## 开发自检：逐张载入第二章地图，在首个出生点放侦探作比例，整图截屏到 tmp/chapter2_capture/。
## 运行：godot --path godot_csharp --rendering-method gl_compatibility --script res://scenes/Chapter2/Field/capture_maps.gd

const MAPS := ["living_hall", "zone_jia", "zone_yi", "zone_bing", "zone_ding", "central_hall", "lecture", "archive",
	"counterweight", "resonance", "clinic", "logistics", "granary", "greenhouse", "campus"]
const ACTOR := preload("res://scenes/Story/StoryActor.gd")
const FRAMES := preload("res://ShareAsset/Internal/Characters/animation_v2/detective.tres")

func _initialize() -> void:
	root.size = Vector2i(1600, 1200)
	call_deferred("_run")

func _run() -> void:
	var out := ProjectSettings.globalize_path("res://").path_join("../tmp/chapter2_capture")
	DirAccess.make_dir_recursive_absolute(out)
	for key in MAPS:
		var map: Node2D = (load("res://scenes/Chapter2/Maps/%s.tscn" % key) as PackedScene).instantiate()
		root.add_child(map)
		var actor := CharacterBody2D.new()
		actor.set_script(ACTOR)
		actor.standard = FRAMES
		map.get_node("Objects").add_child(actor)
		var spawns := map.get_node("Spawns").get_children()
		actor.position = (spawns[0] as Marker2D).position
		var size: Vector2 = map.get_meta("map_size")
		var vp := SubViewport.new()
		vp.size = Vector2i(size)
		vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		vp.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST
		root.add_child(vp)
		map.reparent(vp)
		for i in 4:
			await process_frame
		await RenderingServer.frame_post_draw
		var img := vp.get_texture().get_image()
		img.save_png(out.path_join(key + ".png"))
		print("captured ", key, " ", size)
		vp.queue_free()
		await process_frame
	quit()
