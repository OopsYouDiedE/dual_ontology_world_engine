extends SceneTree
## 开发自检：以游玩时的镜头（960×640，2 倍缩放）在若干地点截图，侦探在画面中，用于美术检验。
## 运行：godot --path godot_csharp --rendering-method gl_compatibility --script res://scenes/Chapter2/Field/capture_views.gd
## 输出到仓库根目录 tmp/chapter2_views/（不提交）。地点写法：地图/出生点，或 地图@调查点，可加 +dx,dy 像素偏移。

const FIELD := preload("res://scenes/Chapter2/Field/Chapter2Field.tscn")
const VIEWS := [
	["01_gate_inside", "campus_s/gate_inside"],
	["02_gate_outside", "campus_s/gate_outside"],
	["03_oath_court", "campus_s@oath_court"],
	["04_hall_front", "campus_n/hall_south"],
	["05_behind_hall", "campus_n/hall_north"],
	["06_archive", "campus_n/archive_front"],
	["07_resonance", "campus_nw/resonance_front"],
	["08_lane", "campus_sw@lane"],
	["09_living_north", "campus_sw/living_north"],
	["10_logistics", "campus_s/logistics_east"],
	["11_drying_yard", "campus_s@drying_yard"],
	["12_spring", "campus_ne@spring"],
	["13_greenhouse", "campus_ne/greenhouse_front"],
	["14_clinic", "campus_s/clinic_front"],
	["15_road", "harbor@collapse+-260,-120"],
	["16_collapse", "harbor@collapse+0,-118"],
	["17_landing", "harbor/harbor_landing"],
	["18_interior_hall", "central_hall/south"],
	["19_interior_living", "living_hall/north_door"],
]

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var out := ProjectSettings.globalize_path("res://").path_join("../tmp/chapter2_views")
	DirAccess.make_dir_recursive_absolute(out)
	var field: Node2D = FIELD.instantiate()
	root.add_child(field)
	await process_frame
	for item in VIEWS:
		var spec: String = item[1]
		var offset := Vector2.ZERO
		if spec.contains("+"):
			var parts := spec.split("+")
			spec = parts[0]
			var o := parts[1].split(",")
			offset = Vector2(float(o[0]), float(o[1]))
		if spec.contains("@"):
			var p := spec.split("@")
			field.load_map(p[0], "")
			var site := field.map.get_node("Sites/" + p[1]) as Marker2D
			field.player.position = site.position + offset
		else:
			var p := spec.split("/")
			field.load_map(p[0], p[1])
			field.player.position += offset
		field.camera.reset_smoothing()
		for i in 30:
			await physics_frame
		await RenderingServer.frame_post_draw
		var img := root.get_texture().get_image()
		img.save_png(out.path_join(String(item[0]) + ".png"))
		print("view ", item[0], " ", field.map.name)
	quit()
