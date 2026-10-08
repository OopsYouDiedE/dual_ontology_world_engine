extends SceneTree
# Independent automatic UI verification; this is not a blind playthrough.
var failures: Array[String] = []
var checks: Array[String] = []
var OUT := ProjectSettings.globalize_path("res://../artifacts/ui-prefabs/")
func check(ok: bool, message: String) -> void:
	checks.append(message)
	if not ok: failures.append(message)
func _initialize() -> void:
	call_deferred("run")
func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUT)
	check(Engine.is_editor_hint(), "actual --editor engine context")
	var paths: Array[String] = []
	for dir in _scene_dirs("res://scenes/UI"):
		for file in DirAccess.get_files_at(dir):
			if file.ends_with(".tscn"): paths.append(dir + "/" + file)
	paths.append("res://scenes/Chapter2/Field/FieldHUD.tscn")
	for path in paths:
		var scene = load(path) as PackedScene
		check(scene != null, "load " + path)
		if scene == null: continue
		var node = scene.instantiate()
		check(node != null, "instantiate " + path)
		root.add_child(node)
		await process_frame
		node.queue_free()
		await process_frame
	var modal = load("res://scenes/UI/Components/PaperModal.tscn").instantiate()
	root.add_child(modal)
	await process_frame
	modal.modal_title = "独立编辑器即时修改"
	modal.modal_size = Vector2(700, 500)
	check(modal.title_label.text.contains("独立编辑器"), "modal title setter immediately updates actual label")
	check(modal.panel.custom_minimum_size == Vector2(700, 500), "modal size setter immediately updates panel")
	check(modal.panel.get_theme_stylebox("panel").content_margin_left == modal.get_theme_stylebox("panel", "PaperSheet").content_margin_left or modal.panel.theme_type_variation != &"", "modal sheet style comes from the theme, not from code")
	check(modal.dim_rect.color == modal.get_theme_color("dim", "PaperModal"), "modal dim color comes from UITheme")
	var saved = PackedScene.new()
	check(saved.pack(modal) == OK, "pack modified editor modal")
	check(ResourceSaver.save(saved, OUT + "editor-modal-fixture.tscn") == OK, "save modified modal only to artifacts")
	modal.queue_free()
	await process_frame
	var reloaded = load(OUT + "editor-modal-fixture.tscn").instantiate()
	root.add_child(reloaded)
	await process_frame
	check(reloaded.modal_title == "独立编辑器即时修改" and reloaded.title_label.text.contains("独立编辑器"), "saved title survives reload and ready")
	check(reloaded.panel.custom_minimum_size == Vector2(700,500), "saved panel size survives reload")
	reloaded.queue_free()
	var hud = load("res://scenes/UI/Panels/HUD/FieldHUD.tscn").instantiate()
	root.add_child(hud)
	await process_frame
	hud.preview_title = "即时标题"
	hud.preview_time = "12:34"
	check(hud.get_node("Title").text == "即时标题" and hud.get_node("Time").text == "12:34", "FieldUI live preview updates")
	hud.queue_free()
	var manuscript = load("res://scenes/UI/Panels/Manuscript/ReplayManuscript.tscn").instantiate()
	root.add_child(manuscript)
	await process_frame
	manuscript.default_title = "保存测试"
	check(manuscript.get_node("LeftPage/Scroll/Column/Title").text.contains("保存测试"), "manuscript live title updates")
	check(manuscript.get_node("LeftPage").theme_type_variation != &"", "manuscript page style comes from a theme variation")
	manuscript.queue_free()
	var workbench = load("res://scenes/UI/Workbench/UIWorkbench.tscn").instantiate()
	root.add_child(workbench)
	await process_frame
	for tab in [0,1,3,4,14]:
		workbench.preview_page = tab
		await process_frame
		await process_frame
		await RenderingServer.frame_post_draw
		var vp = workbench.get_node("Pages").get_child(tab).get_node("Viewport") as SubViewport
		var image = vp.get_texture().get_image()
		check(not image.is_empty(), "normal rendered editor viewport " + str(tab))
		image.save_png(OUT + "editor-page-%02d.png" % tab)
	var report = {"editor_hint": Engine.is_editor_hint(), "loaded_prefabs": paths.size(), "checks": checks, "failures": failures}
	var file = FileAccess.open(OUT + "independent-editor-report.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "\t"))
	print("UI_PREFAB_CHECK: ", paths.size(), " scenes; ", checks.size(), " checks; failures=", failures)
	quit(0 if failures.is_empty() else 1)

func _scene_dirs(base: String) -> Array:
	var dirs: Array = [base]
	for sub in DirAccess.get_directories_at(base):
		dirs.append_array(_scene_dirs(base + "/" + sub))
	return dirs
