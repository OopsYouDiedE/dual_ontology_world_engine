extends SceneTree
## State regression only. The full playthrough separately exercises player input.
## Unicode key injection in the checks does not verify a Windows system IME.
const OUT := "res://../artifacts/first-awakening/"
var failed := false
var scene: Node2D

func _initialize() -> void:
	call_deferred("run")

func require(ok: bool, description: String) -> void:
	if not ok:
		failed = true
		push_error("USER ADJUSTMENTS: " + description)

func click(control: Control) -> void:
	for frame in 3: await process_frame
	var event := InputEventMouseButton.new()
	event.position = control.get_global_rect().get_center()
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = true
	Input.parse_input_event(event)
	await process_frame
	var release := event.duplicate() as InputEventMouseButton
	release.pressed = false
	Input.parse_input_event(release)
	for frame in 3: await process_frame

func capture(name: String) -> void:
	if not OS.get_cmdline_user_args().has("--capture-click"): return
	await RenderingServer.frame_post_draw
	require(root.get_texture().get_image().save_png(OUT + "adjustment-" + name + ".png") == OK, "capture saved: " + name)

func facing_to(actor: Node2D, point: Vector2) -> String:
	var offset := point - actor.position
	if absf(offset.x) > absf(offset.y): return "right" if offset.x > 0 else "left"
	return "front" if offset.y > 0 else "back"

func check_dorm_layout() -> void:
	var layout: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://ShareAsset/Internal/Props/FirstAwakeningInterior/layout.json"))
	var rooms: Array[Dictionary] = [{}, {}, {}, {}]
	for item in layout.furniture:
		var position := Vector2(item.position[0], item.position[1])
		if position.y >= 304 or position.x < 32 or position.x >= 1248: continue
		# Agreed exceptions: the player's coat chair, basket contents, door plaques.
		if String(item.name).begins_with("DoorNumber"): continue
		var shared_sprite: String = item.sprite
		if String(item.name).begins_with("DeskChair"): shared_sprite = "chair_back"
		if String(item.sprite).begins_with("paper_basket"): shared_sprite = "paper_basket_empty"
		var room_index := int(position.x / 320.0)
		var local_position := position - Vector2(room_index * 320, 0)
		var footprint = item.footprint
		if footprint != null:
			footprint = footprint.duplicate()
			footprint[0] -= room_index * 320
		rooms[room_index][shared_sprite + "@" + str(local_position)] = footprint
		var sprite := scene.scene_items.get_node_or_null(String(item.name)) as Sprite2D
		require(sprite != null, "runtime furniture exists: " + String(item.name))
		if sprite != null:
			require(sprite.texture != null and sprite.texture.resource_path.get_file() == String(item.sprite) + ".png", "runtime furniture matches layout texture: " + String(item.name))
			if sprite.texture != null:
				var texture_size := sprite.texture.get_size()
				var anchor := sprite.position + sprite.offset + Vector2(texture_size.x / 2.0, texture_size.y)
				if sprite.centered: anchor -= texture_size / 2.0
				require(anchor.is_equal_approx(position), "runtime painted bottom anchor matches layout position: " + String(item.name))
	for room_index in range(1, 4):
		require(rooms[room_index] == rooms[0], "relative dorm furniture, textures and footprints match: " + str(room_index))
	var room_lights: Array[Array] = [[], [], [], []]
	for light in layout.lights:
		if light.at[1] >= 304: continue
		var index := int(light.at[0] / 320)
		var normalized: Dictionary = light.duplicate(true)
		normalized.at[0] -= index * 320
		room_lights[index].append(normalized)
	for index in range(1, 4): require(room_lights[index] == room_lights[0], "dorm lighting recipes have identical relative positions and settings")
	for id in ["bed_1", "bed_2", "bed_3", "desk_1", "desk_2", "desk_3"]:
		var description := TranslationServer.translate(scene.sites[id].description_id)
		require(not description.contains("恶魔"), "inspection keeps the unknown cleaner unknown: " + id)

func run() -> void:
	TranslationServer.set_locale("zh_CN")
	scene = load("res://scenes/FirstAwakening/FirstAwakening.tscn").instantiate()
	root.add_child(scene)
	current_scene = scene
	for frame in 5: await process_frame
	check_dorm_layout()
	var name_entry: Control = scene.name_entry
	require(name_entry.has_method("validation_error"), "name validation is shared with confirmation")
	for invalid in ["", "   ", "霍铎", "沈鼎", "苏泠", "侦探", "铜口", "林林林林林林林林林林林林林林林林林"]:
		require(not String(name_entry.validation_error(invalid)).is_empty(), "invalid name rejected: " + invalid)
	require(name_entry.validation_error("   ") == TranslationServer.translate("FA_UI_351"), "empty-name feedback uses the localized validation message")
	require(name_entry.validation_error("林林林林林林林林林林林林林林林林林") == TranslationServer.translate("FA_UI_352"), "long-name feedback uses the localized validation message")
	require(name_entry.validation_error(" 林 ") == "" and name_entry.validation_error(" 侦探 ") == TranslationServer.translate("FA_UI_353"), "validation trims before checking reserved names")
	for valid in ["林", " 林 ", "林林林林林林林林林林林林林林林林"]:
		require(String(name_entry.validation_error(valid)).is_empty(), "trimmed 1–16 character name accepted: " + valid)
	require(name_entry.get_node_or_null("Center/Panel/Margin/Content/Suggested") != null, "suggested-name button is present")
	for tap in 10:
		if not scene.dialogue.active: break
		await click(scene.dialogue.next_button if scene.dialogue.next_button.visible else scene.dialogue.panel)
	scene.call("_leave_bed")
	var initial := {"工程师": Vector2(232, 680), "厨师": Vector2(320, 760), "音乐家": Vector2(408, 680)}
	var party := [scene.actors["工程师"], scene.actors["厨师"], scene.actors["音乐家"]]
	for actor_name in initial:
		require(scene.actors[actor_name].position.is_equal_approx(initial[actor_name]), "agreed discussion position: " + actor_name)
		var actor: Node2D = scene.actors[actor_name]
		var faces_companion: bool = actor.facing == facing_to(actor, Vector2(320, 680))
		for other in party:
			if other != actor and actor.facing == facing_to(actor, other.position): faces_companion = true
		require(faces_companion, "discussion faces a companion: " + actor_name)
	var ambient_before: int = scene.ambient_speaker_index
	for frame in 600:
		if scene.ambient_speaker_index != ambient_before: break
		await physics_frame
	require(scene.ambient_speaker_index != ambient_before, "ambient discussion rotates speakers before arrival")
	var ambient_party := [scene.actors["工程师"], scene.actors["厨师"], scene.actors["音乐家"]]
	for index in ambient_party.size():
		var action := "talk_" if index == scene.ambient_speaker_index else "idle_"
		require(String(ambient_party[index].sprite.animation).begins_with(action), "ambient orientation preserves the speaker and listener animations")
	scene.player.position = Vector2(320, 744)
	for frame in 4: await physics_frame
	require(not scene.dialogue.active, "old table-distance location cannot bypass doorway arrival")
	scene.player.position = Vector2(256, 532)
	for frame in 4: await physics_frame
	await capture("before-arrival")
	var engineer_before: String = scene.actors["工程师"].facing
	var musician_before: String = scene.actors["音乐家"].facing
	scene.player.position = Vector2(256, 552)
	for frame in 2: await physics_frame
	require(scene.dialogue.active, "doorway threshold automatically starts arrival")
	require(scene.ARRIVAL_THRESHOLD == Rect2(224, 544, 64, 48), "doorway threshold retains agreed geometry")
	require(scene.actors["厨师"].facing == facing_to(scene.actors["厨师"], scene.player.position), "cook reacts first upon doorway arrival")
	require(scene.actors["工程师"].facing == engineer_before and scene.actors["音乐家"].facing == musician_before, "listeners retain their discussion facing during the cook's first reaction")
	await capture("cook-turn")
	for frame in 18: await physics_frame
	require(scene.actors["工程师"].facing == facing_to(scene.actors["工程师"], scene.player.position) and scene.actors["音乐家"].facing == musician_before, "engineer reacts second while musician is still listening")
	await capture("engineer-turn")
	for frame in 18: await physics_frame
	require(scene.actors["音乐家"].facing == facing_to(scene.actors["音乐家"], scene.player.position), "musician reacts third")
	await capture("musician-turn")
	for frame in 6000:
		if not scene.dialogue.busy: break
		await physics_frame
	require(not scene.dialogue.busy, "successive reactions and detective blocking complete")
	require(scene.player.position.distance_to(Vector2(320, 600)) < 28, "detective reaches the northern gap at the table")
	for actor in scene.actors.values():
		var feet := Rect2(actor.position - Vector2(8, 12), Vector2(16, 12))
		for obstacle in scene.obstacle_rects:
			require(not feet.intersects(obstacle), "staged actor has furniture clearance: " + actor.name)
	require(scene.dialogue.line != null and scene.dialogue.line.character == "工程师", "engineer begins the arrival conversation")
	for actor_name in ["厨师", "音乐家"]:
		require(scene.actors[actor_name].facing == facing_to(scene.actors[actor_name], scene.actors["工程师"].position), "default listener faces the engineer's opening line: " + actor_name)
	for tap in 6:
		if scene.dialogue.line != null and scene.dialogue.line.text == "又出来一个。": break
		await click(scene.dialogue.next_button if scene.dialogue.next_button.visible else scene.dialogue.panel)
	require(scene.dialogue.line != null and scene.dialogue.line.text == "又出来一个。", "arrival reaches cook's discovery line through mouse input")
	require(scene.actors["厨师"].facing == facing_to(scene.actors["厨师"], scene.player.position), "cook addresses the detective on discovery")
	for frame in 600:
		if scene.dialogue.body.visible_characters < 0: break
		await physics_frame
	await capture("cook-notices-detective")
	for tap in 6:
		if scene.dialogue.line != null and scene.dialogue.line.text.begins_with("哎！你见到接我们的人了吗"): break
		await click(scene.dialogue.next_button if scene.dialogue.next_button.visible else scene.dialogue.panel)
	for actor_name in ["工程师", "音乐家"]:
		require(scene.actors[actor_name].facing == facing_to(scene.actors[actor_name], scene.player.position), "explicit discovery direction faces the detective: " + actor_name)
	var suggested_values: Array[String] = []
	name_entry.confirmed.connect(func(value: String): suggested_values.append(value))
	name_entry.show()
	for frame in 4: await process_frame
	scene.camera.force_update_scroll()
	var card: Rect2 = name_entry.get_node("Center/Panel").get_global_rect()
	require(card.end.y <= 350, "registration card stays in the upper part of the screen")
	for actor in scene.actors.values():
		var texture: Texture2D = actor.sprite.sprite_frames.get_frame_texture(actor.sprite.animation, actor.sprite.frame)
		var size := texture.get_size()
		var bounds: Rect2 = actor.sprite.get_global_transform_with_canvas() * Rect2(-size / 2, size)
		require(not card.intersects(bounds) and Rect2(Vector2.ZERO, root.get_visible_rect().size).encloses(bounds), "registration card leaves every actor visible and unobscured: " + actor.name)
	await click(name_entry.get_node("Center/Panel/Margin/Content/Suggested"))
	require(suggested_values == ["林"], "suggested-name button submits the agreed valid name")
	name_entry.hide()
	if OS.get_cmdline_user_args().has("--capture-click"):
		# Controlled art view; the full playthrough independently verifies travel.
		scene.dialogue.active = false
		scene.dialogue.panel.hide()
		scene.conversation_staged = false
		scene.investigation_phase = "regroup"
		scene.player.position = Vector2(192, 232)
		for frame in 4: await physics_frame
		await capture("dorm0-unobscured")
	print("USER ADJUSTMENTS: ", "FAIL" if failed else "PASS", "; state regression; full input and system IME are separate scopes")
	scene.queue_free()
	current_scene = null
	for frame in 3: await process_frame
	quit(1 if failed else 0)
