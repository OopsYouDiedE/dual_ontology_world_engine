extends SceneTree

const ASSETS := "res://ShareAsset/Images/shot02/"
const CHARACTERS := "res://ShareAsset/Images/characters/atlas/"
const PREVIOUS := "res://ShareAsset/Images/shot01/"
const TILESET := "res://ShareAsset/Victorian/tilesets/Victorian.tres"
const SCENE := "res://scenes/Shot02/Shot02.tscn"
const OUTPUT := "res://../artifacts/shots/shot02-validation/"
var room: Node2D
var tileset: TileSet
var textures: Dictionary = {}
var finish: ShaderMaterial

func _initialize() -> void:
	call_deferred("build")

func texture(name: String, image: Image) -> void:
	var res_path := ASSETS + name + ".res"
	assert(image.save_png(ASSETS + name + ".png") == OK)
	var tex := ImageTexture.create_from_image(image)
	tex.take_over_path(res_path)
	assert(ResourceSaver.save(tex, res_path, ResourceSaver.FLAG_COMPRESS) == OK)
	textures[name] = load(res_path)

func extract(image: Image, name: String, region: Rect2i, size: Vector2i) -> void:
	var part := image.get_region(region)
	part = part.get_region(part.get_used_rect())
	fit32(part, size.x, size.y)
	texture(name, part)

func prepare() -> void:
	var image := Image.load_from_file(ASSETS + "source/kitchen_generated.png")
	extract(image, "hearth", Rect2i(61, 0, 1004, 450), Vector2i(384, 168))
	extract(image, "pantry", Rect2i(1175, 0, 274, 474), Vector2i(96, 168))
	extract(image, "breakfast_table", Rect2i(87, 470, 1359, 278), Vector2i(448, 128))
	extract(image, "prep_island", Rect2i(25, 742, 773, 253), Vector2i(352, 112))
	extract(image, "side_table", Rect2i(810, 788, 371, 204), Vector2i(160, 85))
	extract(image, "chair", Rect2i(1192, 780, 129, 210), Vector2i(32, 64))
	extract(image, "bread_barrel", Rect2i(1343, 779, 164, 215), Vector2i(48, 64))

	textures["detective"] = load(CHARACTERS + "06_detective.tres")
	textures["naturalist"] = load(CHARACTERS + "09_naturalist.tres")
	textures["chef"] = load(CHARACTERS + "11_chef.tres")
	textures["engineer"] = load(CHARACTERS + "10_mechanical_engineer.tres")

	image = Image.load_from_file("res://ShareAsset/TileMap/Academy/academy_objects_48.png")
	extract(image, "wall_lantern", Rect2i(576, 576, 192, 192), Vector2i(16, 37))

	image = Image.load_from_file(ASSETS + "source/joinery_generated.png")
	extract(image, "closed_door", Rect2i(142, 18, 581, 668), Vector2i(128, 168))
	extract(image, "supplies", Rect2i(860, 241, 630, 458), Vector2i(75, 64))
	extract(image, "front_gate", Rect2i(7, 722, 920, 253), Vector2i(576, 128))
	extract(image, "front_panel", Rect2i(88, 737, 173, 227), Vector2i(96, 128))
	extract(image, "bread_plate", Rect2i(969, 707, 550, 287), Vector2i(59, 32))

	image = Image.load_from_file("res://ShareAsset/Images/flags/five_emblems_48.png")
	var emblem := image.get_region(Rect2i(144, 0, 48, 48))
	emblem.resize(32, 32, Image.INTERPOLATE_LANCZOS)
	texture("academy_emblem", emblem)

	for n in ["window"]:
		textures[n] = load(PREVIOUS + n + ".res")
	for n in ["fern", "tree", "books", "candelabrum"]:
		textures[n] = load(PREVIOUS + "revision2/" + n + ".res")

	var light_img := Image.create(416, 384, false, Image.FORMAT_RGBA8)
	light_img.fill(Color.TRANSPARENT)
	for col in 6:
		for row in 3:
			var start := Vector2(13, 27) + Vector2(65, 39) * col + Vector2(0, 20) * row
			raster_polygon(light_img, PackedVector2Array([start, start + Vector2(59, 35), start + Vector2(59, 49), start + Vector2(0, 15)]), Color(1.0, 0.85, 0.58, 0.18))
	for col in 4:
		for row in 2:
			var start := Vector2(24, 232) + Vector2(51, 30) * col + Vector2(0, 19) * row
			raster_polygon(light_img, PackedVector2Array([start, start + Vector2(45, 27), start + Vector2(45, 41), start + Vector2(0, 14)]), Color(1.0, 0.85, 0.58, 0.15))
	texture("window_light", light_img)

func build() -> void:
	prepare()
	tileset = load(TILESET)
	finish = ShaderMaterial.new()
	finish.shader = load("res://scenes/Shot01/material_finish.gdshader")
	room = Node2D.new()
	room.name = "Shot02"
	room.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	room.set_script(load("res://scenes/Shot02/Shot02.gd"))
	room.set_meta("reference", "artifacts/theme-film/scenes/shot-02.png")

	# 32x19 tiles at 32px = 1024x608
	var floor_layer := layer("StoneFloor")
	var stone := ShaderMaterial.new()
	stone.shader = load("res://scenes/Shot01/stone_finish.gdshader")
	floor_layer.material = stone
	paint(floor_layer, Rect2i(0, 4, 32, 15), 0, "a2_floor_r3_c3")

	var walls := layer("BackWall")
	walls.modulate = Color(0.94, 0.87, 0.75)
	paint(walls, Rect2i(0, 0, 32, 5), 1, "a4_wall_side_p0_c5")

	var wood := layer("Wainscot")
	wood.modulate = Color(0.82, 0.76, 0.66)
	paint(wood, Rect2i(0, 3, 32, 2), 1, "a4_wall_side_p0_c6")

	var rugs := layer("Rugs")
	rugs.modulate = Color(0.72, 0.75, 0.78)
	paint(rugs, Rect2i(8, 9, 16, 6), 0, "a2_floor_r2_c0")
	paint(rugs, Rect2i(27, 5, 4, 2), 0, "a2_floor_r2_c0")

	var kitchen := group("Kitchen")
	place(kitchen, "hearth", Vector2(504, 96))
	for x in [224, 779]:
		place(kitchen, "pantry", Vector2(x, 85))
	place(kitchen, "closed_door", Vector2(912, 84))
	place(kitchen, "prep_island", Vector2(512, 181))
	for x in [299, 821]:
		place(kitchen, "bread_barrel", Vector2(x, 164))
	place(kitchen, "supplies", Vector2(733, 158))
	for x in [24, 75]:
		place(kitchen, "window", Vector2(x, 92))
	for x in [159, 296, 729, 837, 995]:
		place(kitchen, "wall_lantern", Vector2(x, 75))

	var sides := group("SideFurniture")
	for pos in [Vector2(139, 340), Vector2(85, 497), Vector2(944, 324), Vector2(949, 495)]:
		place(sides, "chair", pos + Vector2(0, -53))
		place(sides, "side_table", pos)
		place(sides, "chair", pos + Vector2(0, 55))
		place(sides, "bread_plate", pos + Vector2(24, -16))
		place(sides, "books", pos + Vector2(-41, -16))
		var candle := place(sides, "candelabrum", pos + Vector2(-57, -23))
		candle.scale = Vector2(0.32, 0.32)

	for pos in [Vector2(133, 153), Vector2(1007, 179)]:
		place(sides, "tree", pos)
	for pos in [Vector2(32, 284), Vector2(29, 524)]:
		place(sides, "fern", pos)

	var buffet := place(sides, "prep_island", Vector2(61, 219))
	buffet.scale = Vector2(0.32, 0.62)
	place(sides, "academy_emblem", Vector2(61, 242)).scale = Vector2(0.7, 0.7)

	var breakfast := group("Breakfast")
	for x in [373, 469, 651]:
		place(breakfast, "chair", Vector2(x, 300))
	place(breakfast, "chef", Vector2(560, 250), true)
	place(breakfast, "breakfast_table", Vector2(523, 372))
	place(breakfast, "detective", Vector2(430, 464), true)
	place(breakfast, "naturalist", Vector2(622, 464), true)
	place(breakfast, "engineer", Vector2(736, 464), true)
	for x in [357, 448, 539, 629, 720]:
		place(breakfast, "chair", Vector2(x, 444))

	var sunlight := group("WindowLight")
	var light_sprite := Sprite2D.new()
	light_sprite.name = "WindowLightGrid"
	light_sprite.texture = textures["window_light"]
	light_sprite.position = Vector2(304, 352)
	add(sunlight, light_sprite)

	var rail := group("ForegroundRail")
	for x in [-16, 80, 176, 240, 784, 848, 944, 1040]:
		place(rail, "front_panel", Vector2(x, 597))
	place(rail, "front_gate", Vector2(512, 597))
	place(rail, "academy_emblem", Vector2(512, 580))

	var shade := ColorRect.new()
	shade.name = "PeripheralShade"
	shade.size = Vector2(1024, 608)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var shade_material := ShaderMaterial.new()
	shade_material.shader = load("res://scenes/Shot01/edge_shade.gdshader")
	shade.material = shade_material
	add(room, shade)

	var camera := Camera2D.new()
	camera.name = "Camera2D"
	camera.position = Vector2(512, 304)
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
	if "--capture" in OS.get_cmdline_user_args():
		var viewport := SubViewport.new()
		viewport.size = Vector2i(1024, 608)
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.add_child(room)
		await process_frame
		await process_frame
		await RenderingServer.frame_post_draw
		assert(viewport.get_texture().get_image().save_png(OUTPUT + "shot02.png") == OK)
		viewport.queue_free()
	else:
		room.free()
	var result := "Shot02: %d valid cells after reload; 32x19 map; 32px grid; 1024x608 render.\nFour independent character sprites and separate kitchen, dining, side furniture, light and foreground layers.\n" % count
	var file := FileAccess.open(OUTPUT + "validation.txt", FileAccess.WRITE)
	file.store_string(result)
	print(result)
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

func place(parent: Node, label: String, pos: Vector2, character: bool = false) -> Sprite2D:
	var tex: Texture2D = textures[label]
	if label in ["pantry", "prep_island", "breakfast_table", "side_table", "chair", "fern", "tree", "bread_barrel"]:
		var shadow := Sprite2D.new()
		shadow.texture = tex
		shadow.position = pos + Vector2(5, tex.get_height() * 0.42)
		shadow.scale = Vector2(0.97, 0.15)
		shadow.modulate = Color(0.04, 0.03, 0.02, 0.22)
		add(parent, shadow)
	var node := Sprite2D.new()
	node.name = label.to_pascal_case()
	node.texture = tex
	node.position = pos
	if not character and label != "hearth":
		node.material = finish
	add(parent, node)
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

func fit32(im: Image, w: int, h: int) -> void:
	var ratio := minf(float(w) / im.get_width(), float(h) / im.get_height())
	var content := Vector2i(Vector2(im.get_size()) * ratio)
	im.resize(maxi(1, content.x), maxi(1, content.y), Image.INTERPOLATE_LANCZOS)
	var box := Vector2i(ceili(w / 32.0) * 32, ceili(h / 32.0) * 32)
	var padded := Image.create(box.x, box.y, false, Image.FORMAT_RGBA8)
	padded.fill(Color.TRANSPARENT)
	padded.blit_rect(im, Rect2i(Vector2i.ZERO, im.get_size()), (box - im.get_size()) / 2)
	im.copy_from(padded)
