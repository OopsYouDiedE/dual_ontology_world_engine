extends SceneTree
## Independent automatic regression; original baselines extracted verbatim from saved host sources.
const Paper = preload("res://scenes/Shared/UI/Paper.gd")
const Baseline1 = preload("res://scenes/Shared/check/modal_baseline_ch1.gd")
const Baseline2 = preload("res://scenes/Shared/check/modal_baseline_ch2.gd")
var failures := 0
var checks := 0
var output := ""

func _initialize() -> void:
	output = ProjectSettings.globalize_path("res://../artifacts/project-audit/verification-20261005")
	_run.call_deferred()

func check(ok: bool, message: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print(("PASS " if ok else "FAIL ") + message)

func style_snapshot(style: Resource) -> Dictionary:
	var result := {}
	for prop in style.get_property_list():
		if int(prop.usage) & PROPERTY_USAGE_STORAGE and not str(prop.name) in ["resource_name", "resource_local_to_scene", "script"]:
			var value = style.get(prop.name)
			if not value is Object:
				result[str(prop.name)] = str(value)
	return result

func snapshot(node: Node) -> Dictionary:
	var result := {"class": node.get_class(), "properties": {}, "children": []}
	for key in ["position", "size", "custom_minimum_size", "anchor_left", "anchor_right", "anchor_top", "anchor_bottom", "offset_left", "offset_right", "offset_top", "offset_bottom", "size_flags_horizontal", "size_flags_vertical", "mouse_filter", "horizontal_scroll_mode", "vertical_scroll_mode", "scroll_horizontal", "scroll_vertical", "text", "color", "bbcode_enabled", "fit_content", "scroll_active", "autowrap_mode"]:
		if key in node:
			result.properties[key] = str(node.get(key))
	if node is Control:
		for key in ["separation", "normal_font_size", "bold_font_size"]:
			if node.has_theme_constant_override(key):
				result.properties[key] = str(node.get_theme_constant(key))
			if node.has_theme_font_size_override(key):
				result.properties[key] = str(node.get_theme_font_size(key))
		if node.has_theme_stylebox_override("panel"):
			result.properties.panel_style = style_snapshot(node.get_theme_stylebox("panel"))
		if node.theme != null:
			result.properties.theme_font_size = node.theme.default_font_size
	for child in node.get_children():
		result.children.append(snapshot(child))
	return result

func settle() -> void:
	for i in 8:
		await process_frame

func shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var path := output.path_join(name + ".png")
	check(root.get_texture().get_image().save_png(path) == OK, "screenshot " + path)

func _run() -> void:
	root.size = Vector2i(1280, 992)
	for chapter in [1, 2]:
		var old: Node = Baseline1.new() if chapter == 1 else Baseline2.new()
		var old_layer := CanvasLayer.new()
		var new_layer := CanvasLayer.new()
		root.add_child(old_layer)
		root.add_child(new_layer)
		old.modal_layer = old_layer
		for title in ["", "滚动标题"]:
			for dimensions in [Vector2(860, 420), Vector2(1020, 820)]:
				var before_body: VBoxContainer = old.open_modal(title, dimensions, "ending")
				var after: Control = Paper.modal(new_layer, title, dimensions)
				var after_body: VBoxContainer = after.get_meta("body")
				for line in 45:
					before_body.add_child(Paper.label("原版正文第 %d 行" % line))
					after_body.add_child(Paper.label("原版正文第 %d 行" % line))
				await settle()
				var before_scroll: ScrollContainer = before_body.get_parent()
				var after_scroll: ScrollContainer = after_body.get_parent()
				before_scroll.scroll_vertical = 120
				after_scroll.scroll_vertical = 120
				await settle()
				check(snapshot(old.modal_root) == snapshot(after), "original ch%d tree/layout/style/scroll title=%s size=%s" % [chapter, title, str(dimensions)])
				check(after_scroll.scroll_vertical > 0, "long content actually scrolls")
				old.close_modal()
				after.queue_free()
				await process_frame
		old.free()
		old_layer.queue_free()
		new_layer.queue_free()
		await process_frame
	# Actual hosts: scene loading, click cancellation, replacement, replay close state.
	for chapter in [1, 2]:
		var scene_path := "res://scenes/Awakening/Awakening.tscn" if chapter == 1 else "res://scenes/Chapter2/Game/Chapter2Game.tscn"
		var game: Node = load(scene_path).instantiate()
		game.set_meta("skip_intro", true)
		root.add_child(game)
		await settle()
		check(game.engine != null and game.player != null, "chapter%d scene loaded" % chapter)
		if chapter == 1:
			game.jump_to("investigate")
		else:
			game.load_map("central_hall", game.SPAWN_OF.get("central_hall", ""))
		await settle()
		await shot("chapter%d-map" % chapter)
		game.click_path = PackedVector2Array([Vector2(10, 20)])
		game.click_waypoint = 7
		if chapter == 1:
			game.click_interaction_id = "E3"
			game.click_stall_seconds = 3.0
		else:
			game.click_target = "test"
		game.open_modal("状态清理", Vector2(860, 420), "menu")
		check(game.click_path.is_empty() and game.click_waypoint == 0, "chapter%d opening cancels path" % chapter)
		if chapter == 1:
			check(game.click_interaction_id.is_empty() and game.click_stall_seconds == 0.0 and game.click_last_position == Vector2(INF, INF), "chapter1 interaction/stall cleared")
		else:
			check(game.click_target.is_empty(), "chapter2 target cleared")
		var previous: Control = game.modal_root
		game.open_modal("替换", Vector2(860, 420), "replay")
		check(previous.is_queued_for_deletion() and game.modal_kind == "replay", "chapter%d replacement queues previous modal" % chapter)
		if chapter == 1:
			game.manuscript = Control.new()
			game.modal_root.add_child(game.manuscript)
		game.close_modal()
		check(game.modal_root == null and game.modal_kind == "", "chapter%d close clears root and kind" % chapter)
		if chapter == 1:
			check(game.manuscript == null, "chapter1 close clears replay manuscript")
			game.open_card("E3")
		else:
			game.open_notebook()
		await settle()
		check(is_instance_valid(game.modal_root), "chapter%d card/notebook opens" % chapter)
		await shot("chapter%d-card-notebook" % chapter)
		game.close_modal()
		game.replay_open = true
		game.open_manuscript()
		await settle()
		check(is_instance_valid(game.modal_root), "chapter%d replay manuscript opens" % chapter)
		await shot("chapter%d-replay" % chapter)
		game.close_modal()
		game.queue_free()
		await process_frame
	print("TOTAL %d checks, %d failures; automatic state injection and visual verification, not blind play" % [checks, failures])
	quit(1 if failures else 0)
