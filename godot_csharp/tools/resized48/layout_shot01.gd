extends SceneTree

const TILESET := "res://ShareAsset/Victorian/tilesets/Victorian.tres"
const ASSETS := "res://ShareAsset/Images/shot01/"
const SCENE := "res://scenes/Shot01/Shot01.tscn"
const OUTPUT := "res://../artifacts/shot01-validation/"
const NAMES := ["clock", "portal", "window", "statue", "compass", "bench", "detective_back", "musician_front"]
const REGIONS := [Rect2i(0,0,524,561), Rect2i(537,0,399,557), Rect2i(969,0,243,551), Rect2i(1267,49,227,518), Rect2i(9,563,491,414), Rect2i(521,719,431,192), Rect2i(991,606,179,359), Rect2i(1280,619,182,341)]
# Scaled for 32px (2/3 of 48px sizes)
const SIZES := [Vector2i(224,224), Vector2i(128,192), Vector2i(64,192), Vector2i(64,128), Vector2i(192,192), Vector2i(128,48), Vector2i(40,75), Vector2i(37,69)]
var room: Node2D
var tileset: TileSet
var report: Array[String] = []
var finish: ShaderMaterial
var texture_cache: Dictionary = {}
const REVISION := ASSETS + "revision2/"

func _initialize() -> void:
	call_deferred("build")

func save_texture(img: Image, path_prefix: String) -> Texture2D:
	var res_path := path_prefix + ".res"
	if texture_cache.has(res_path):
		return texture_cache[res_path]
	assert(img.save_png(path_prefix + ".png") == OK)
	var tex := ImageTexture.create_from_image(img)
	tex.take_over_path(res_path)
	assert(ResourceSaver.save(tex, res_path, ResourceSaver.FLAG_COMPRESS) == OK)
	var loaded: Texture2D = load(res_path)
	texture_cache[res_path] = loaded
	return loaded

func build() -> void:
	var source := Image.load_from_file(ASSETS + "source/central_hall_generated.png")
	source.convert(Image.FORMAT_RGBA8)
	for i in NAMES.size():
		var piece := source.get_region(REGIONS[i])
		piece = piece.get_region(piece.get_used_rect())
		fit32(piece, SIZES[i].x, SIZES[i].y)
		save_texture(piece, ASSETS + NAMES[i])
	prepare_revision()
	prepare_drawn_assets()
	finish = ShaderMaterial.new()
	finish.shader = load("res://scenes/Shot01/material_finish.gdshader")
	tileset = load(TILESET)
	room = Node2D.new()
	room.name = "Shot01"
	room.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	room.set_script(load("res://scenes/Shot01/Shot01.gd"))
	room.set_meta("reference", "artifacts/theme-film/scenes/shot-01.png")

	# 32x20 tiles at 32px = 1024x640
	var floor_layer := layer("Floor")
	var stone := ShaderMaterial.new()
	stone.shader = load("res://scenes/Shot01/stone_finish.gdshader")
	floor_layer.material = stone
	paint(floor_layer, Rect2i(0, 6, 32, 14), 0, "a2_floor_r3_c3")

	var walls := layer("BackMasonry")
	walls.modulate = Color(0.79, 0.73, 0.62)
	paint(walls, Rect2i(0, 0, 32, 7), 1, "a4_wall_side_p0_c5")

	var wood := layer("Wainscot")
	wood.modulate = Color(0.86, 0.82, 0.75)
	paint(wood, Rect2i(0, 5, 32, 2), 1, "a4_wall_side_p0_c6")

	var entry := layer("EntranceRug")
	entry.modulate = Color(0.64, 0.70, 0.78)
	paint(entry, Rect2i(13, 16, 6, 4), 0, "a2_floor_r2_c0")

	var inlay := group("FloorInlay")
	sprite(inlay, "InlayPattern", load(ASSETS + "floor_inlay.res"), Vector2(512, 384))
	sprite(inlay, "CompassRose", load(ASSETS + "compass.res"), Vector2(512, 351))

	var architecture := group("Architecture")
	for x in [192, 336, 688, 832]:
		sprite(architecture, "Portal", load(ASSETS + "portal.res"), Vector2(x, 128))
	sprite(architecture, "MechanicalClockAndHorns", load(ASSETS + "clock.res"), Vector2(512, 112))
	for x in [32, 992]:
		sprite(architecture, "MorningWindow", load(ASSETS + "window.res"), Vector2(x, 123))

	var furniture := group("Furniture")
	for x in [149, 875]:
		sprite(furniture, "Bench", load(ASSETS + "bench.res"), Vector2(x, 397))
	for x in [224, 800]:
		sprite(furniture, "ScholarStatue", load(ASSETS + "statue.res"), Vector2(x, 480))
	for x in [52, 972]:
		sprite(furniture, "ResearchTable", load(REVISION + "research_table.res"), Vector2(x, 460))
		object(furniture, "Bookcase", 0, Vector2(x, 335), Vector2i(64, 96))
	for x in [75, 949]:
		sprite(furniture, "PottedTree", load(REVISION + "tree.res"), Vector2(x, 231))
	for x in [48, 976]:
		sprite(furniture, "PottedFern", load(REVISION + "fern.res"), Vector2(x, 385))
	for x in [107, 917]:
		sprite(furniture, "Candelabra", load(REVISION + "candelabrum.res"), Vector2(x, 288))
	for x in [117, 267, 395, 629, 757, 907]:
		object(furniture, "WallLantern", 15, Vector2(x, 161), Vector2i(21, 43))

	var foreground := layer("ForegroundWainscot")
	foreground.modulate = Color(0.83, 0.78, 0.69)
	paint(foreground, Rect2i(0, 17, 13, 3), 1, "a4_wall_side_p0_c6")
	paint(foreground, Rect2i(19, 17, 13, 3), 1, "a4_wall_side_p0_c6")

	var rail := group("ForegroundRail")
	var rail_tex := load(ASSETS + "foreground_rail.res")
	sprite(rail, "ForegroundRailLeft", rail_tex, Vector2(208, 543))
	sprite(rail, "ForegroundRailRight", rail_tex, Vector2(816, 543))

	var podium := AtlasTexture.new()
	podium.atlas = load(ASSETS + "statue.res")
	podium.region = Rect2(0, 85, 64, 43)
	var podium_path := ASSETS + "podium.tres"
	ResourceSaver.save(podium, podium_path)
	var saved_podium: Texture2D = load(podium_path)

	for x in [107, 363, 661, 917]:
		var post := sprite(rail, "ForecourtPost", saved_podium, Vector2(x, 571))
		post.scale.y = 2.0
		var candle := sprite(rail, "PostCandle", load(REVISION + "candelabrum.res"), Vector2(x, 504))
		candle.scale = Vector2(0.55, 0.55)

	var flags := group("IdentityBanners")
	banner(flags, "Academy", 3, Vector2(512, 187), Vector2(0.67, 0.67))
	for x in [192, 688]:
		banner(flags, "RoyalScience", 1, Vector2(x, 71), Vector2(0.84, 0.84))
	for x in [336, 832]:
		banner(flags, "MysticGuild", 2, Vector2(x, 71), Vector2(0.84, 0.84))
	banner(flags, "RoyalAuthority", 0, Vector2(80, 67))
	banner(flags, "CountPatron", 4, Vector2(944, 67))
	for x in [288, 736]:
		banner(flags, "AcademyEntrance", 3, Vector2(x, 580))

	var characters := group("Characters")
	sprite(characters, "Detective", load(ASSETS + "detective_back.res"), Vector2(512, 507))
	sprite(characters, "Musician", load(ASSETS + "musician_front.res"), Vector2(251, 211))

	var light := group("MorningLight")
	var light_sprite := Sprite2D.new()
	light_sprite.name = "MorningLightGrid"
	light_sprite.texture = load(ASSETS + "morning_light.res")
	light_sprite.position = Vector2(304, 384)
	add(light, light_sprite)

	var shade := ColorRect.new()
	shade.name = "PeripheralShade"
	shade.size = Vector2(1024, 640)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var shade_material := ShaderMaterial.new()
	shade_material.shader = load("res://scenes/Shot01/edge_shade.gdshader")
	shade.material = shade_material
	add(room, shade)

	var camera := Camera2D.new()
	camera.name = "Camera2D"
	camera.position = Vector2(512, 320)
	add(room, camera)

	var packed := PackedScene.new()
	assert(packed.pack(room) == OK)
	assert(ResourceSaver.save(packed, SCENE) == OK)
	room.free()

	room = (ResourceLoader.load(SCENE, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE) as PackedScene).instantiate()
	var count := 0
	for child in room.get_children():
		if child is TileMapLayer:
			for cell in child.get_used_cells():
				assert(child.get_cell_tile_data(cell) != null)
				count += 1
	report.append("Shot01 saved/reloaded: %d valid map cells; 32x20 cells; 32px; 1024x640 render" % count)
	report.append("9 architectural banners: five established emblems redrawn on subdued long cloth; static revision2 art")

	if "--capture" in OS.get_cmdline_user_args():
		var viewport := SubViewport.new()
		viewport.size = Vector2i(1024, 640)
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.add_child(room)
		await process_frame
		await process_frame
		await RenderingServer.frame_post_draw
		assert(viewport.get_texture().get_image().save_png(OUTPUT + "shot01.png") == OK)
		assert(room.get_node("IdentityBanners").get_child_count() == 9)
		viewport.queue_free()
	else:
		room.free()

	var f := FileAccess.open(OUTPUT + "validation.txt", FileAccess.WRITE)
	f.store_string("\n".join(report) + "\n")
	print("\n".join(report))
	quit()

func add(parent: Node, child: Node) -> void:
	parent.add_child(child)
	child.owner = room

func group(label: String) -> Node2D:
	var node := Node2D.new()
	node.name = label
	add(room, node)
	return node

func layer(label: String) -> TileMapLayer:
	var node := TileMapLayer.new()
	node.name = label
	node.tile_set = tileset
	add(room, node)
	return node

func paint(target: TileMapLayer, rect: Rect2i, set_index: int, label: String) -> void:
	var terrain := -1
	for i in tileset.get_terrains_count(set_index):
		if tileset.get_terrain_name(set_index, i) == label:
			terrain = i
	assert(terrain >= 0, label)
	var cells: Array[Vector2i] = []
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			cells.append(Vector2i(x, y))
	target.set_cells_terrain_connect(cells, set_index, terrain, false)

func sprite(parent: Node, label: String, texture: Texture2D, pos: Vector2) -> Sprite2D:
	if label in ["Bench", "ScholarStatue", "ResearchTable", "PottedTree", "PottedFern", "Bookcase", "Detective", "Musician"]:
		var shadow := Sprite2D.new()
		shadow.name = label + "Shadow"
		shadow.texture = texture
		shadow.position = pos + Vector2(0 if label in ["Detective", "Musician"] else 7, texture.get_height() * 0.46 if label in ["Detective", "Musician"] else texture.get_height() * 0.44)
		shadow.scale = Vector2(0.85 if label in ["Detective", "Musician"] else 0.95, 0.16 if label in ["Detective", "Musician"] else 0.18)
		shadow.modulate = Color(0.04, 0.03, 0.02, 0.26 if label in ["Detective", "Musician"] else 0.30)
		add(parent, shadow)
	var node := Sprite2D.new()
	node.name = label
	node.texture = texture
	node.position = pos
	if label not in ["Detective", "Musician"]:
		node.material = finish
	add(parent, node)
	return node

func object(parent: Node, label: String, index: int, pos: Vector2, size: Vector2i) -> void:
	var tex_path := ASSETS + label.to_snake_case()
	var tex: Texture2D
	if texture_cache.has(tex_path + ".res"):
		tex = texture_cache[tex_path + ".res"]
	else:
		var img := Image.load_from_file("res://ShareAsset/TileMap/Academy/academy_objects_48.png").get_region(Rect2i((index % 4) * 192, (index / 4) * 192, 192, 192))
		img = img.get_region(img.get_used_rect())
		fit32(img, size.x, size.y)
		tex = save_texture(img, tex_path)
	sprite(parent, label, tex, pos)

func banner(parent: Node, label: String, row: int, pos: Vector2, size_scale: Vector2 = Vector2.ONE) -> void:
	var node := sprite(parent, label, load(REVISION + "banner_%d.res" % row), pos)
	node.scale = size_scale

func prepare_drawn_assets() -> void:
	var inlay_img := Image.create(896, 320, false, Image.FORMAT_RGBA8)
	inlay_img.fill(Color.TRANSPARENT)
	var border_col := Color(0.54, 0.42, 0.25)
	raster_line(inlay_img, Vector2(31, 8), Vector2(865, 8), border_col, 2)
	raster_line(inlay_img, Vector2(31, 296), Vector2(865, 296), border_col, 2)
	raster_line(inlay_img, Vector2(32, 8), Vector2(32, 296), border_col, 2)
	raster_line(inlay_img, Vector2(864, 8), Vector2(864, 296), border_col, 2)
	var diamond_col := Color(0.36, 0.34, 0.30)
	for y in range(7, 17, 2):
		for x in range(4, 29, 2):
			var c := Vector2((x - 2) * 32, (y - 7) * 32 + 8)
			raster_polygon(inlay_img, PackedVector2Array([c + Vector2(0, -5), c + Vector2(5, 0), c + Vector2(0, 5), c + Vector2(-5, 0)]), diamond_col)
	save_texture(inlay_img, ASSETS + "floor_inlay")

	var rail_img := Image.create(416, 12, false, Image.FORMAT_RGBA8)
	rail_img.fill(Color(0.22, 0.15, 0.09))
	raster_line(rail_img, Vector2(0, 1), Vector2(416, 1), Color(0.54, 0.39, 0.22), 2)
	save_texture(rail_img, ASSETS + "foreground_rail")

	var light_img := Image.create(416, 320, false, Image.FORMAT_RGBA8)
	light_img.fill(Color.TRANSPARENT)
	for col in 5:
		for row in 3:
			var start := Vector2(24, 21) + Vector2(77, 44) * col + Vector2(0, 20) * row
			raster_polygon(light_img, PackedVector2Array([start, start + Vector2(69, 39), start + Vector2(69, 55), start + Vector2(0, 15)]), Color(1.0, 0.86, 0.60, 0.20))
	save_texture(light_img, ASSETS + "morning_light")

func raster_polygon(img: Image, points: PackedVector2Array, color: Color) -> void:
	var min_x := int(img.get_width() - 1)
	var max_x := 0
	var min_y := int(img.get_height() - 1)
	var max_y := 0
	for p in points:
		min_x = clampi(mini(min_x, int(floor(p.x))), 0, img.get_width() - 1)
		max_x = clampi(maxi(max_x, int(ceil(p.x))), 0, img.get_width() - 1)
		min_y = clampi(mini(min_y, int(floor(p.y))), 0, img.get_height() - 1)
		max_y = clampi(maxi(max_y, int(ceil(p.y))), 0, img.get_height() - 1)
	var n := points.size()
	for y in range(min_y, max_y + 1):
		for x in range(min_x, max_x + 1):
			var pt := Vector2(x + 0.5, y + 0.5)
			var has_pos := false
			var has_neg := false
			for i in n:
				var p1 := points[i]
				var p2 := points[(i + 1) % n]
				var cross := (p2.x - p1.x) * (pt.y - p1.y) - (p2.y - p1.y) * (pt.x - p1.x)
				if cross > 0.0001:
					has_pos = true
				elif cross < -0.0001:
					has_neg = true
				if has_pos and has_neg:
					break
			if not (has_pos and has_neg):
				var bg := img.get_pixel(x, y)
				if bg.a <= 0.0:
					img.set_pixel(x, y, color)
				else:
					var a := color.a + bg.a * (1.0 - color.a)
					var inv := 1.0 - color.a
					var r := (color.r * color.a + bg.r * bg.a * inv) / maxf(0.0001, a)
					var g := (color.g * color.a + bg.g * bg.a * inv) / maxf(0.0001, a)
					var b := (color.b * color.a + bg.b * bg.a * inv) / maxf(0.0001, a)
					img.set_pixel(x, y, Color(r, g, b, a))

func raster_line(img: Image, p1: Vector2, p2: Vector2, color: Color, width: float) -> void:
	var dir := (p2 - p1).normalized()
	var normal := Vector2(-dir.y, dir.x) * (width * 0.5)
	raster_polygon(img, PackedVector2Array([p1 - normal, p2 - normal, p2 + normal, p1 + normal]), color)

func prepare_revision() -> void:
	var source := Image.load_from_file(REVISION + "hall_finish_generated.png")
	var rects := [Rect2i(58,38,252,521), Rect2i(345,38,250,521), Rect2i(637,38,255,521), Rect2i(933,38,253,521), Rect2i(1228,38,252,521), Rect2i(18,632,299,337), Rect2i(333,553,241,415), Rect2i(570,632,425,339), Rect2i(1027,567,163,412), Rect2i(1236,725,279,233)]
	var names := ["banner_0", "banner_1", "banner_2", "banner_3", "banner_4", "fern", "tree", "research_table", "candelabrum", "books"]
	var sizes := [Vector2i(64,128), Vector2i(64,128), Vector2i(64,128), Vector2i(64,128), Vector2i(64,128), Vector2i(75,85), Vector2i(59,96), Vector2i(107,85), Vector2i(32,80), Vector2i(32,27)]
	for i in rects.size():
		var part := source.get_region(rects[i])
		part = part.get_region(part.get_used_rect())
		fit32(part, sizes[i].x, sizes[i].y)
		save_texture(part, REVISION + names[i])

func fit32(im: Image, w: int, h: int) -> void:
	var ratio := minf(float(w) / im.get_width(), float(h) / im.get_height())
	var content := Vector2i(Vector2(im.get_size()) * ratio)
	im.resize(maxi(1, content.x), maxi(1, content.y), Image.INTERPOLATE_LANCZOS)
	var box := Vector2i(ceili(w / 32.0) * 32, ceili(h / 32.0) * 32)
	var padded := Image.create(box.x, box.y, false, Image.FORMAT_RGBA8)
	padded.fill(Color.TRANSPARENT)
	padded.blit_rect(im, Rect2i(Vector2i.ZERO, im.get_size()), (box - im.get_size()) / 2)
	im.copy_from(padded)
