extends SceneTree
# Independent automatic regression with injected feet positions; not blind play.
const MASK = preload("res://scenes/Shared/UI/Field/SightMask.gd")
const INDOOR = ["central_hall", "lecture", "logistics", "granary", "clinic", "greenhouse", "archive", "counterweight", "living_hall", "resonance", "zone_yi", "zone_jia", "zone_ding", "zone_bing"]
var checks := 0
var failures := 0
var out := ""
var visual := false
var capture_only := false
class Fixture extends Node2D:
	var fog := true
	var closed := false
	var feet := Vector2(48, 48)
	func sight_size() -> Vector2i: return Vector2i(40, 8)
	func sight_origin() -> Vector2: return feet - Vector2(0, 8)
	func sight_entry_origin() -> Vector2: return feet
	func sight_region(c: Vector2i) -> String: return "left" if c.x < 20 else "right"
	func sight_interior(c: Vector2i) -> bool: return c.y > 0 and c.y < 7 and c.x != 20
	func sight_blocks(c: Vector2i) -> bool: return closed and c.x == 20
	func sight_terrain_fog() -> bool: return fog
	func _draw() -> void: draw_rect(Rect2(0, 0, 1280, 256), Color.WHITE)
func _initialize() -> void:
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--out="): out = arg.substr(6)
		if arg == "--visual": visual = true
		if arg == "--capture-only": capture_only = true
	if out != "": DirAccess.make_dir_recursive_absolute(out)
	_run.call_deferred()
func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		var issue := FileAccess.open("C:/Users/iii/Desktop/dual_ontology_world_engine/tmp/cabinet-fog-verification-round2/issue.txt", FileAccess.WRITE)
		issue.store_string("Room fog automatic regression failed: " + label + "\nRepro: run check_room_fog.gd --visual; see current-fog.log.\n")
		issue.close()
		quit(1)
	print("PASS " if ok else "FAIL ", label)
func center(c: Vector2i) -> Vector2: return Vector2(c * 32) + Vector2(16, 16)
func region_all(game: Node, region: String) -> bool:
	var size: Vector2i = game.sight_size()
	for y in size.y:
		for x in size.x:
			var c := Vector2i(x, y)
			if game.sight_region(c) == region and not game.sight.seen_cells.has(c): return false
	return true
func move(game: Node, feet: Vector2) -> void:
	game.player.position = feet
	game.sight.process_mode = Node.PROCESS_MODE_ALWAYS
	for i in 4: await process_frame
	game.sight.process_mode = Node.PROCESS_MODE_DISABLED
	# Host is frozen to prevent plot advancement; still refresh actual prop/NPC rendering.
	game._process(0.0)
func render() -> Image:
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	return root.get_texture().get_image()
func shot(name: String) -> void:
	if not visual: return
	var img := await render()
	check(img.save_png(out.path_join(name + ".png")) == OK, "screenshot " + name)
func host_pixels(game: Node, key: String, floors: Array) -> void:
	if not visual: return
	var floor_cell: Vector2i = floors[0]
	for c in floors:
		if not game.sight.visible_cells.has(c): floor_cell = c; break
	if game.sight.visible_cells.has(floor_cell):
		print("LIMIT ", key, " single current room; no distinct blind room to sample")
		return
	game.camera.limit_left = -100000
	game.camera.limit_top = -100000
	game.camera.limit_right = 100000
	game.camera.limit_bottom = 100000
	game.camera.zoom = Vector2.ONE
	game.camera.global_position = center(floor_cell)
	game.camera.reset_smoothing()
	game.camera.force_update_scroll()
	game.sight.hide()
	var base := await render()
	game.sight.show()
	var fogged := await render()
	var canvas: Transform2D = root.get_canvas_transform()
	var screen := Vector2i(canvas * center(floor_cell))
	var a := base.get_pixelv(screen)
	var b := fogged.get_pixelv(screen)
	check(b.get_luminance() < a.get_luminance() * 0.7 and b.get_luminance() > a.get_luminance() * 0.2, key + " actual rendered known blind floor dims")
	var wall := Vector2i(-1, -1)
	var size: Vector2i = game.sight_size()
	for y in size.y:
		for x in size.x:
			var c := Vector2i(x, y)
			if not game.sight_interior(c) and game.sight.seen_cells.has(c) and not game.sight.visible_cells.has(c): wall = c; break
		if wall.x >= 0: break
	if wall.x >= 0:
		game.camera.global_position = center(wall)
		game.camera.reset_smoothing()
		game.camera.force_update_scroll()
		game.sight.hide()
		base = await render()
		game.sight.show()
		fogged = await render()
		canvas = root.get_canvas_transform()
		screen = Vector2i(canvas * center(wall))
		check(base.get_pixelv(screen).is_equal_approx(fogged.get_pixelv(screen)), key + " actual rendered blind architecture unchanged")
func verify_host(game: Node, key: String) -> void:
	var regions := {}
	var floors: Array = []
	var excluded: Array = []
	var size: Vector2i = game.sight_size()
	var rows = game.layout.rows if key == "chapter1" else game.map.get_meta("terrain_rows", [])
	var native_ok: bool = rows.size() == size.y
	for y in size.y:
		for x in size.x:
			var c := Vector2i(x, y)
			if y < rows.size() and x < rows[y].length():
				var glyph: String = rows[y].substr(x, 1)
				native_ok = native_ok and game.sight_interior(c) == (glyph == "." if key == "chapter1" else glyph in [".", "S"])
			else: native_ok = false
			if game.sight_interior(c):
				floors.append(c)
				regions[game.sight_region(c)] = c
			else: excluded.append(c)
	check(native_ok, key + " interior API exactly follows native floor glyphs; D/#/W/w/G excluded")
	check(not floors.is_empty() and not regions.has(""), key + " native interior floors have valid regions")
	var architecture_clear := true
	game.sight.seen_cells.clear()
	for c in excluded: architecture_clear = architecture_clear and game.sight.terrain_shade(c).a == 0.0
	check(architecture_clear, key + " ALL unknown native walls/doors transparent")
	await verify_props(game, key, floors)
	game.sight.seen_cells.clear()
	for c in excluded:
		# Run every architectural cell through the actual discovery API.
		game.player.position = center(c)
		game.sight._cast(c)
	check(game.sight.seen_cells.is_empty(), key + " every non-interior cell cannot enter room")
	var boundary_tested := false
	for c in floors:
		var below: Vector2i = c + Vector2i(0, 1)
		if below.y < size.y and not game.sight_interior(below):
			game.sight.seen_cells.clear()
			await move(game, Vector2(c * 32) + Vector2(16, 35))
			check(game.sight.seen_cells.is_empty(), key + " head inside floor while actual feet on architecture does not discover")
			await move(game, Vector2(c * 32) + Vector2(16, 28))
			check(region_all(game, game.sight_region(c)), key + " feet enter while head stays same cell triggers natural discovery")
			boundary_tested = true
			break
	check(boundary_tested, key + " foot/head boundary exercised")
	game.sight.seen_cells.clear()
	for region in regions:
		var c: Vector2i = regions[region]
		await move(game, center(c))
		check(region_all(game, region), key + " entered region " + str(region) + " fully remembered")
		var room_visible := true
		for cell in game.sight.seen_cells:
			if game.sight_region(cell) == region: room_visible = room_visible and game.sight.can_see(center(cell)) and game.sight.terrain_shade(cell).a == 0.0
		check(room_visible, key + " current region " + str(region) + " wholly visible")
	var all_seen := true
	var correct_shade := true
	for y in size.y:
		for x in size.x:
			var c := Vector2i(x, y)
			if regions.has(game.sight_region(c)): all_seen = all_seen and game.sight.seen_cells.has(c)
			if game.sight.seen_cells.has(c) and not game.sight_interior(c): correct_shade = correct_shade and game.sight.terrain_shade(c).a == 0.0
	check(all_seen, key + " all enterable region partition cells remembered")
	check(correct_shade, key + " visited architecture never dimmed")
	await host_pixels(game, key, floors)
func verify_props(game: Node, key: String, floors: Array) -> void:
	var objects: Node = game.get_node("ObjectsAndPeople") if key == "chapter1" else game.map.get_node_or_null("Objects")
	if objects == null:
		print("LIMIT ", key, " no Objects container")
		return
	# Tall test prop crosses floor/wall boundary. Whole Sprite2D must be hidden.
	var prop := Sprite2D.new()
	var image := Image.create(32, 96, false, Image.FORMAT_RGBA8)
	image.fill(Color.MAGENTA)
	prop.texture = ImageTexture.create_from_image(image)
	prop.offset = Vector2(0, -48)
	prop.position = center(floors[0])
	objects.add_child(prop)
	game.sight.seen_cells.clear()
	game._process(0.0)
	var all_hidden := prop.self_modulate.a == 0.0
	for node in objects.get_children():
		if node is Sprite2D: all_hidden = all_hidden and node.self_modulate.a == 0.0
	check(all_hidden, key + " unknown whole props including tall synthetic sprite alpha0")
	prop.visible = false
	await move(game, prop.position)
	game._process(0.0)
	check(prop.self_modulate.a == 1.0 and not prop.visible, key + " known prop alpha restores while gameplay visible=false preserved")
	if key == "chapter1":
		game.sight.seen_cells.clear()
		var unknown := true
		for spec in game.decals.DECALS: unknown = unknown and not game.decals._known(spec[1])
		for shelf in game.layout.shelves: unknown = unknown and not game.decals._known(Vector2(shelf[0], shelf[1]))
		check(unknown, "TraceDecals runtime unknown decals and shelf traces gated off")
		await move(game, game.site_point("E11a") + Vector2(0, 72))
		check(game.decals._known(game.site_point("E11a")), "TraceDecals shelf becomes known only after room entry")
	prop.free()
	game.sight.seen_cells.clear()
func _capture() -> void:
	for key in ["chapter1", "zone_yi"]:
		var path: String = "res://scenes/Awakening/Awakening.tscn" if key == "chapter1" else "res://scenes/Chapter2/Game/Chapter2Game.tscn"
		var host = load(path).instantiate()
		host.set_meta("skip_intro", true)
		root.add_child(host)
		for i in 4: await process_frame
		host.process_mode = Node.PROCESS_MODE_DISABLED
		if key != "chapter1": host.load_map(key, "from_hall")
		host.sight.seen_cells.clear()
		if key == "chapter1":
			await move(host, Vector2(400, 112))
			await move(host, Vector2(704, 112))
			await move(host, Vector2(640, 368))
		else:
			await move(host, Vector2(1040, 112))
			await move(host, Vector2(112, 368))
		host.camera.zoom = Vector2.ONE
		host.camera.limit_left = -100000
		host.camera.limit_top = -100000
		host.camera.limit_right = 100000
		host.camera.limit_bottom = 100000
		host.camera.global_position = Vector2(640 if key == "chapter1" else 576, 352)
		host.camera.reset_smoothing()
		host.camera.force_update_scroll()
		host.sight.queue_redraw()
		host.dialogue.visible = false
		host.dialogue.panel.hide()
		await shot("03_chapter1_shared_fog" if key == "chapter1" else "04_chapter2_shared_fog")
		host.free()
	quit()
func _run() -> void:
	if capture_only:
		await _capture()
		return
	var fixture := Fixture.new()
	root.add_child(fixture)
	var mask = MASK.new()
	mask.host = fixture
	fixture.add_child(mask)
	mask.set_process(false)
	fixture.feet = center(Vector2i(19, 1))
	mask._cast(Vector2i(19, 1))
	check(mask.can_see(center(Vector2i(1, 1))), "current room far edge visible beyond old radius")
	check(not mask.visible_cells.has(Vector2i(22, 1)), "open doorway other room remains outside room visibility")
	check(not mask.can_see(center(Vector2i(22, 1))), "unknown other room cannot expose NPC/site")
	check(mask.terrain_shade(Vector2i(22, 1)).a == 1.0, "unknown other room fully opaque")
	check(mask.terrain_shade(Vector2i(22, 0)).a == 0.0 and mask.terrain_shade(Vector2i(20, 1)).a == 0.0, "never visited walls and doors always clear")
	if visual:
		var img := await render()
		check(img.get_pixel(720, 16).r > 0.95 and img.get_pixel(656, 48).r > 0.95, "rendered never visited wall and doorway unchanged")
		check(img.get_pixel(720, 48).get_luminance() < 0.04, "rendered unknown other room interior fully opaque")
		await shot("01_shared_unseen_open_door")
	fixture.feet = center(Vector2i(30, 1))
	mask._cast(Vector2i(30, 1))
	check(mask.seen_cells.size() == 320, "room entry reveals full partitions")
	check(mask.can_see(center(Vector2i(39, 1))) and mask.can_see(center(Vector2i(21, 6))), "current room visible wholly regardless distance or geometry")
	fixture.feet = center(Vector2i(20, 1))
	mask._cast(Vector2i(20, 1))
	check(mask.can_see(center(Vector2i(39, 1))) and not mask.can_see(center(Vector2i(19, 1))), "doorway retains previously entered current room")
	fixture.feet = center(Vector2i(30, 1))
	mask._cast(Vector2i(30, 1))
	check(mask.terrain_shade(Vector2i(1, 1)).a > 0.60 and mask.terrain_shade(Vector2i(1, 1)).a < 0.64, "known blind interior shade alpha .62")
	check(mask.terrain_shade(Vector2i(1, 0)).a == 0.0, "known blind wall unchanged")
	check(mask.terrain_shade(Vector2i(30, 1)).a == 0.0, "known current room interior clear")
	mask.queue_redraw()
	if visual:
		var img := await render()
		check(img.get_pixel(48, 48).r > 0.35 and img.get_pixel(48, 48).r < 0.43, "rendered known blind interior dim")
		check(img.get_pixel(48, 16).r > 0.95, "rendered known blind wall white unchanged")
		check(img.get_pixel(848, 48).r > 0.95, "rendered known current room interior white clear")
		await shot("02_shared_visited_blind_architecture")
	var memory: Dictionary = mask.seen_cells
	mask.reset()
	check(memory.size() == 320 and mask.seen_cells.is_empty(), "reset detaches saved memory")
	fixture.feet = center(Vector2i(20, 1))
	mask._cast(Vector2i(19, 1))
	check(mask.seen_cells.is_empty(), "feet on door cannot discover despite head interior")
	fixture.fog = false
	mask._cast(Vector2i(1, 1))
	check(mask.seen_cells.is_empty() and mask.terrain_shade(Vector2i(30, 1)).a == 0.0, "outdoor fog disabled")
	fixture.free()
	var awake = load("res://scenes/Awakening/Awakening.tscn").instantiate()
	awake.set_meta("skip_intro", true)
	root.add_child(awake)
	for i in 4: await process_frame
	awake.process_mode = Node.PROCESS_MODE_DISABLED
	await verify_host(awake, "chapter1")
	for door in awake.get_node("ObjectsAndPeople").get_children():
		if door.has_method("threshold_collision_rect"):
			check(door.get("privacy_covers") == null, "RoomDoor special privacy covers removed")
			var before: Dictionary = awake.sight.visible_cells.duplicate()
			door.openness = 0.0
			await move(awake, awake.player.position)
			check(awake.sight.visible_cells == before, "closed door does not change whole current room visibility")
			door.openness = 1.0
			await move(awake, awake.player.position)
			check(awake.sight.visible_cells == before, "open door does not expose other rooms")
			break
	awake.sight.seen_cells.clear()
	await move(awake, Vector2(400, 112))
	await move(awake, Vector2(640, 112))
	await move(awake, Vector2(640, 368))
	awake.camera.global_position = Vector2(640, 352)
	awake.camera.reset_smoothing()
	awake.camera.force_update_scroll()
	awake.sight.queue_redraw()
	awake.dialogue.visible = false
	awake.dialogue.panel.hide()
	await shot("03_chapter1_shared_fog")
	awake.free()
	var game = load("res://scenes/Chapter2/Game/Chapter2Game.tscn").instantiate()
	game.set_meta("skip_intro", true)
	root.add_child(game)
	for i in 4: await process_frame
	game.process_mode = Node.PROCESS_MODE_DISABLED
	for key in INDOOR:
		game.load_map(key, game.SPAWN_OF[key])
		check(game.map.has_meta("terrain_rows"), key + " native terrain metadata present")
		await verify_host(game, key)
		var remembered: Dictionary = game.sight.seen_cells.duplicate()
		game.load_map("campus_s", "gate_inside")
		game.load_map(key, game.SPAWN_OF[key])
		check(game.sight.seen_cells == remembered, key + " map roundtrip preserves memory")
	game.load_map("zone_yi", "from_hall")
	game.sight.seen_cells.clear()
	await move(game, Vector2(112, 368))
	if game.actors.is_empty(): game._make_actor("Huo")
	if not game.actors.is_empty():
		var npc: Node2D = game.actors.values()[0]
		npc.position = Vector2(1040, 112)
		game._process(0.0)
		check(not npc.visible and not game.in_sight(npc.position), "unknown dorm NPC cannot leak")
		await move(game, npc.position)
		game._process(0.0)
		check(npc.visible, "current room NPC visible")
		await move(game, Vector2(112, 368))
		game._process(0.0)
		check(not npc.visible and game.sight.terrain_known(npc.position), "known blind dorm NPC hidden")
	game.dialogue.visible = false
	game.dialogue.panel.hide()
	var sites_hidden := true
	for id in game.sites:
		if not game.in_sight(game.site_point(id)): sites_hidden = sites_hidden and not game.site_visible(id)
	check(sites_hidden, "other-room sites cannot expose hover targets")
	game.camera.global_position = Vector2(576, 352)
	game.camera.reset_smoothing()
	game.camera.force_update_scroll()
	game.sight.queue_redraw()
	await shot("04_chapter2_shared_fog")
	game.load_map("campus_s", "gate_inside")
	await move(game, game.player.position)
	check(not game.sight_terrain_fog() and game.sight.seen_cells.is_empty(), "outdoor map has no terrain fog")
	await shot("05_chapter2_outdoor")
	game.free()
	for i in 4: await process_frame
	print("RESULT automatic checks=", checks, " failures=", failures)
	quit(0 if failures == 0 else 1)










