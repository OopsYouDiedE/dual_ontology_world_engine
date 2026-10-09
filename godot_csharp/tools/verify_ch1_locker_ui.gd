extends SceneTree
## Targeted development/UI verification of the production Main scene.
## Advances source-backed dialogue; this is not a playthrough or level-three evidence.
## Godot --path godot_csharp --script res://tools/verify_ch1_locker_ui.gd -- --round=ui-v01

const Fixture := preload("res://scenes/Awakening/check/chapter1_fixture.gd")
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")
const Vocabulary := preload("res://scenes/Shared/Field/FieldVocabulary.gd")
var fx: Fixture
var host: Node
var records: Array = []
var failures := 0
var out_dir := ""
var captures := {}
var capture_viewport: SubViewport
var pointer_only := false

func _initialize() -> void:
	fx = Fixture.new(self)
	fx.on_error = func(message: String) -> void: check(false, message)
	var round_name := "ui-v01"
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--round="): round_name = arg.trim_prefix("--round=")
		elif arg == "--pointer-only": pointer_only = true
	if not RegEx.create_from_string("^[A-Za-z0-9][A-Za-z0-9_-]*$").search(round_name):
		quit(2)
		return
	out_dir = ProjectSettings.globalize_path("res://../artifacts/ch1-locker/").path_join(round_name).simplify_path()
	DirAccess.make_dir_recursive_absolute(out_dir)
	call_deferred("_run")

func record(kind: String, data: Dictionary) -> void:
	data.kind = kind
	data.elapsed_msec = Time.get_ticks_msec()
	records.append(data)

func check(ok: bool, message: String, details: Dictionary = {}) -> void:
	print(("PASS " if ok else "FAIL ") + message + " " + JSON.stringify(details))
	record("check", {"ok": ok, "message": message, "details": details})
	if not ok: failures += 1

func shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var path := out_dir.path_join(name + ".png")
	var error := capture_viewport.get_texture().get_image().save_png(path)
	check(error == OK, "capture " + name)
	var actors := {}
	for who in host.field.actors:
		var actor: CharacterBody2D = host.field.actors[who]
		actors[who] = {"position": [actor.position.x, actor.position.y], "animation": str(actor.sprite.animation), "frame": actor.sprite.frame}
	record("screenshot", {"path": path, "stage": fx.stage(), "line_id": str(host.ui.dialogue.line.id) if host.ui.dialogue.line != null else "", "visible_text": fx.visible_text(), "actors": actors, "player_control": host.field.player_controlled(), "field_active": host.field.field_active(), "hover_text": host.field._hover_text})
	print("SHOT " + path)

func advance() -> void:
	var ui: Node = host.ui
	if ui.modal_kind == "form" and ui._modal_event.has("fields") and str(ui._modal_event.fields[0].get("id", "")) == "name":
		fx.name_form_answer("UI")
		return
	if ui.modal_kind == "card":
		fx.close_modal()
		return
	var d: CanvasLayer = ui.dialogue
	if not d.active or d.busy or d.line == null: return
	if d.body.visible_characters >= 0:
		d.reveal()
		return
	record("dialogue", {"line_id": str(d.line.id), "text": str(d.line.text)})
	var allowed: Array = d.line.responses.filter(func(response): return response.is_allowed)
	if allowed.is_empty(): d.advance()
	else: d.choose(str(allowed[0].next_id))

func mouse_at(point: Vector2) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = host.field.get_global_transform_with_canvas() * point
	motion.global_position = motion.position
	capture_viewport.push_input(motion, true)
	await fx.frames(5)

func click_at(point: Vector2) -> void:
	await mouse_at(point)
	var click := InputEventMouseButton.new()
	click.position = host.field.get_global_transform_with_canvas() * point
	click.global_position = click.position
	click.button_index = MOUSE_BUTTON_LEFT
	click.pressed = true
	click.button_mask = MOUSE_BUTTON_MASK_LEFT
	capture_viewport.push_input(click, true)
	await fx.frames(2)
	click.pressed = false
	click.button_mask = 0
	capture_viewport.push_input(click, true)

func move_player(point: Vector2, seconds := 20.0) -> bool:
	host.field._route_click(point)
	var reached := await fx.wait_until(func(): return host.field.click_path.is_empty(), seconds)
	await fx.physics(3)
	return reached and fx.player().position.distance_to(point) <= 8.1

func furniture_pointer(sprite: Sprite2D) -> Vector2:
	# Daily actors remain active in this UI sample. Pick an actual opaque furniture
	# pixel that is not currently occupied by a visible story/person hotspot.
	var rect := Rect2(sprite.position + sprite.offset, sprite.texture.get_size())
	var pixels := sprite.texture.get_image()
	for y in range(3, int(rect.size.y) - 3, 3):
		for x in range(3, int(rect.size.x) - 3, 3):
			if pixels.get_pixel(x, y).a <= 0.01: continue
			var point := rect.position + Vector2(x, y)
			var covered := false
			for id in host.field.sites:
				if host.field.site_visible(id) and host.field.site_box(id).grow(12).has_point(point):
					covered = true
					break
			if not covered: return point
	check(false, str(sprite.name) + " has an exposed pointer pixel")
	return rect.get_center()

func start_host() -> Node:
	# Main's _exit_tree shuts down the session, so create it in its final viewport.
	# Use the fixture's production scene and the same title-start/view-ready protocol.
	var packed := load(Fixture.SCENE) as PackedScene
	if packed == null:
		check(false, "production Main resource exists")
		return null
	var scene := packed.instantiate()
	fx.host = scene
	capture_viewport.add_child(scene)
	if not is_instance_valid(scene.engine) or not is_instance_valid(scene.title_layer):
		check(false, "production Main has engine and title")
		return null
	scene.title_layer.get_child(0).start_button.pressed.emit()
	await fx.frames(3)
	if not is_instance_valid(scene.ui) or not is_instance_valid(scene.field) or scene.field.player == null:
		check(false, "production Main view handshake")
		return null
	await fx.drain()
	return scene

func _run() -> void:
	if DisplayServer.get_name() == "headless":
		check(false, "renderer required")
		await finish()
		return
	root.size = Vector2i(1280, 992)
	capture_viewport = SubViewport.new()
	capture_viewport.size = Vector2i(1280, 992)
	capture_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(capture_viewport)
	host = await start_host()
	check(host != null and fx.stage() == "roam", "production Main startup")
	if host == null:
		await finish()
		return
	await fx.frames(5)
	if pointer_only:
		await fx.jump("investigate")
	else:
		host.field._route_click(Vector2(440, 340))
		check(await fx.wait_until(func(): return fx.stage() == "meeting", 30.0), "walk from wake into meeting")
	# STORY-HARDCODE[CSH-007][logic][fixture]: UI 检查定位第一章领取柜并跟踪固定角色的领取动画。
	var anchor: Vector2 = fx.map().anchor("领取柜")
	record("geometry", {"anchor": [anchor.x, anchor.y], "furniture": fx.map().layout.furniture})
	var deadline := Time.get_ticks_msec() + 150000
	var source_line := ""
	# STORY-HARDCODE[CSH-037][logic][fixture]: 依项目奖励对白行号定位开场与空柜台词，核对固定物证阶段的位置/输入状态。
	var source_targets := {
		"s4_rewards.004": fx.line_text("s4_rewards.004"),
		"s4_rewards.020": fx.line_text("s4_rewards.020"),
	}
	while Time.get_ticks_msec() < deadline and fx.stage() != "investigate":
		for who in ["shen", "huo", "su", "detective"]:
			var actor: CharacterBody2D = host.field.actors[who]
			if not captures.has(who) and actor.position.distance_to(anchor) <= 0.2 and str(actor.sprite.animation) == "interact_back":
				captures[who] = true
				if who == "detective": check(not host.field.player_controlled(), "detective claim snapshot disables player control")
				await shot("claim-" + who)
				record("animation", {"who": who, "name": "interact_back", "frame_count": actor.sprite.sprite_frames.get_frame_count("interact_back"), "fps": actor.sprite.sprite_frames.get_animation_speed("interact_back")})
				var reached_frame := await fx.wait_until(func(): return actor.position.distance_to(anchor) <= 0.2 and str(actor.sprite.animation) == "interact_back" and actor.sprite.frame == 1, 0.6)
				check(reached_frame, who + " naturally reaches interact_back frame 1")
				if reached_frame: await shot("claim-" + who + "-reach")
		var d: CanvasLayer = host.ui.dialogue
		if d.active and not d.busy and d.line != null:
			var id := str(d.line.id)
			if id != source_line:
				source_line = id
				for source_id in source_targets:
					if str(d.line.text) != source_targets[source_id]: continue
					d.reveal()
					await fx.frames(5)
					if source_id == "s4_rewards.004":
						check(not host.field.field_active(), "source opening locks field input")
						await mouse_at(Vector2(500, 104))
						check(host.field._hover_text == "", "locked opening suppresses furniture hover", {"actual": host.field._hover_text})
						await shot("north-wall-before-claims")
					else:
						var actor: CharacterBody2D = fx.player()
						check(actor.position.distance_to(anchor) <= 0.2, "source empty-locker line is staged at exact anchor", {"source_id": source_id, "position": [actor.position.x, actor.position.y], "animation": str(actor.sprite.animation), "travel_result": actor.travel_result})
						await shot("detective-source-empty-locker")
		advance()
		await process_frame
	check(fx.stage() == "investigate", "debug pointer context reaches investigate" if pointer_only else "source-driven opening reaches investigate")
	if not pointer_only: check(captures.size() == 4, "all four source-driven interact_back claims captured", captures)
	await fx.wait_until(func(): return not fx.busy(), 20.0)
	await fx.physics(5)
	check(Navigation.walkable(anchor, fx.map().floors, fx.map().obstacle_rects), "anchor passes shared navigation footprint test")
	check(fx.map().walkable(anchor, fx.player()), "anchor passes map walkability")
	check(await move_player(anchor), "player follows actual collision route to locker anchor", {"position": [fx.player().position.x, fx.player().position.y]})
	await fx.frames(3)
	await shot("anchor-front")
	check(fx.player().test_move(fx.player().global_transform, Vector2(0, -40)), "locker blocks movement north from anchor")
	check(await move_player(Vector2(544, 144)), "player can pass in front of cabinet and machine", {"position": [fx.player().position.x, fx.player().position.y]})
	check(await move_player(Vector2(560, 142)), "player reaches machine front without clipping")
	check(fx.player().test_move(fx.player().global_transform, Vector2(0, -40)), "machine blocks movement north from front")
	await shot("machine-front-and-lamp")
	check(await move_player(Vector2(500, 160)), "player returns to north-wall viewing position")
	for id in ["RewardLocker", "CylinderPlayer"]:
		var sprite: Sprite2D = fx.map().objects_root().get_node(id)
		var point := furniture_pointer(sprite)
		var expected := str(Vocabulary.table("OBJECT_NAMES").get(id, ""))
		check(host.field._player_object_title(sprite) == expected and expected != "", id + " resolves source vocabulary name")
		await mouse_at(point)
		check(host.field.get_global_mouse_position().distance_to(point) <= 0.1, id + " injected pointer maps to expected world point", {"expected": [point.x, point.y], "actual": [host.field.get_global_mouse_position().x, host.field.get_global_mouse_position().y]})
		record("pointer", {"world_expected": [point.x, point.y], "world_actual": [host.field.get_global_mouse_position().x, host.field.get_global_mouse_position().y], "field_active": host.field.field_active(), "busy": fx.busy(), "furniture_hit": fx.map().furniture_hits(host.field.get_global_mouse_position())})
		check(host.field._hover_text == expected, id + " mouse hover shows source name", {"expected": expected, "actual": host.field._hover_text})
		await shot("hover-" + id.to_snake_case())
		await click_at(point)
		await fx.frames(3)
		var aside_text := str(fx.aside().text)
		var expected_aside := str(fx.layout.furniture_asides.get(id, ""))
		check(aside_text == expected_aside and expected_aside != "", id + " click shows source furniture aside", {"expected": expected_aside, "actual": aside_text})
		await shot("aside-" + id.to_snake_case())
		await fx.wait_until(func(): return host.field.click_path.is_empty(), 10.0)
		await move_player(Vector2(500, 160))
	# STORY-HARDCODE[CSH-036][logic][fixture]: 用第一章铜口热点E8及黑板热点BOARD验证其相对领取柜的具体命中优先级。
	for site_id in ["E8", "BOARD"]:
		check(host.field.sites.has(site_id) and host.field.site_visible(site_id), site_id + " hotspot visible beside new furniture")
		if not host.field.sites.has(site_id): continue
		var point: Vector2 = host.field.site_box(site_id).get_center()
		if site_id == "E8":
			var locker: Sprite2D = fx.map().objects_root().get_node("RewardLocker")
			var overlap: Rect2 = host.field.site_box(site_id).grow(3).intersection(Rect2(locker.position + locker.offset, locker.texture.get_size()))
			check(overlap.has_area(), "copper hotspot and locker have an overlap to test priority")
			if overlap.has_area(): point = overlap.get_center()
		await mouse_at(point)
		var title := str(host.field.sites[site_id].title)
		var hits: Dictionary = fx.map().furniture_hits(point)
		if site_id == "E8": check(hits.get("nodes", []).has("RewardLocker"), "copper priority pointer also hits locker furniture", {"point": [point.x, point.y], "hits": hits})
		check(host.field.hovered == site_id and host.field._hover_text == title, site_id + " hotspot title retains priority", {"expected": title, "actual": host.field._hover_text, "hovered": host.field.hovered, "furniture_hits": hits})
		await shot("hover-hotspot-" + site_id.to_lower())
	check(fx.map().objects_root().y_sort_enabled, "furniture and actors use shared Y sorting")
	check(await move_player(Vector2(600, 368)), "east activity-room doorway remains traversable", {"position": [fx.player().position.x, fx.player().position.y]})
	await shot("activity-doorway")
	check(await move_player(Vector2(900, 368)), "player moves into corridor outside north-wall sight", {"position": [fx.player().position.x, fx.player().position.y]})
	for id in ["RewardLocker", "CylinderPlayer"]:
		var sprite: Sprite2D = fx.map().objects_root().get_node(id)
		var point := sprite.position + sprite.offset + sprite.texture.get_size() / 2.0
		check(not host.field.in_sight(sprite.position), id + " is outside current player sight")
		await mouse_at(point)
		check(host.field._hover_text == "", id + " outside sight suppresses hover name", {"actual": host.field._hover_text, "field_active": host.field.field_active(), "drawn": host.field._player_object_drawn(sprite)})
	await shot("north-wall-outside-sight")
	await finish()

func finish() -> void:
	var file := FileAccess.open(out_dir.path_join("evidence.json"), FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify({"acceptance": "level-two UI / development checks", "not_playthrough": true, "pointer_only_debug_context": pointer_only, "failures": failures, "records": records}, "\t"))
		file.close()
	await fx.shutdown()
	await preload("res://scenes/Shared/Audio/Sound.gd").quit_cleanly(1 if failures > 0 else 0)
