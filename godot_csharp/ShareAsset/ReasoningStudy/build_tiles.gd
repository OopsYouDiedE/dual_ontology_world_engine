extends SceneTree
## Deterministic source slicing and RPG quarter-tile assembly; generated art is retained unchanged.
const ROOT := "res://ShareAsset/ReasoningStudy/"
const AUTO = preload("res://addons/rpg_asset_adapter/converter/autotile_converter.gd")
const TABLES = preload("res://addons/rpg_asset_adapter/data/autotile_tables.gd")
const CONVERT = preload("res://addons/rpg_asset_adapter/converter/tileset_converter.gd")
const L := TileSet.CELL_NEIGHBOR_LEFT_SIDE
const R := TileSet.CELL_NEIGHBOR_RIGHT_SIDE
const U := TileSet.CELL_NEIGHBOR_TOP_SIDE
const D := TileSet.CELL_NEIGHBOR_BOTTOM_SIDE
const CORNERS := [TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER]
var manifest := {}

func _initialize() -> void:
	call_deferred("build")

func fresh(w: int, h: int) -> Image:
	return Image.create(w, h, false, Image.FORMAT_RGBA8)

func store_image(img: Image, name: String) -> Texture2D:
	assert(img.save_png(ROOT + name + ".png") == OK)
	var tex := ImageTexture.create_from_image(img)
	assert(ResourceSaver.save(tex, ROOT + name + ".res") == OK)
	return load(ROOT + name + ".res")

func material(src: Image, quad: Vector2i, size: Vector2i) -> Image:
	var half := src.get_size() / 2
	var img := src.get_region(Rect2i(quad * half, half))
	img.resize(size.x, size.y, Image.INTERPOLATE_LANCZOS)
	return img

func base_tile(img: Image, col: int = 1, row: int = 1) -> Image:
	return img.get_region(Rect2i(col * 32, row * 32, 32, 32))

# Draw the same edge profile at all quarter orientations. Artwork is sampled at its
# final 32px pitch; only connection trim is authored here, never inferred from AI layout.
func quarter(base: Image, k: int, bits: Array, wall: bool, carpet: bool) -> Image:
	var q := fresh(16, 16)
	var right := k % 2 == 1
	var bottom := k >= 2
	var side_x: int = R if right else L
	var side_y: int = D if bottom else U
	var edge_x := not bits.has(side_x)
	var edge_y := not bits.has(side_y)
	var inner := not wall and not edge_x and not edge_y and not bits.has(CORNERS[k])
	for y in range(16):
		for x in range(16):
			var bx := x + (16 if right else 0)
			var by := y + (16 if bottom else 0)
			var dx := 15 - x if right else x
			var dy := 15 - y if bottom else y
			var c := base.get_pixel(bx, by)
			var distance := 99
			if edge_x: distance = mini(distance, dx)
			if edge_y: distance = mini(distance, dy)
			if inner: distance = mini(distance, maxi(dx, dy))
			if distance == 0: c = Color("3e342b") if not carpet else Color("302c28")
			elif distance == 1: c = Color("a4957d") if not carpet else Color("a08550")
			elif distance == 2: c = c.darkened(0.16) if not carpet else Color("645236")
			q.set_pixel(x, y, c)
	return q

func autotile_block(base: Image, wall: bool = false, carpet: bool = false) -> Image:
	var block := fresh(64, 64 if wall else 96)
	var lookup: Array = TABLES.WALL_AUTOTILE_TABLE if wall else TABLES.FLOOR_AUTOTILE_TABLE
	var written := {}
	for shape in range(lookup.size()):
		var bits: Array = TABLES.get_wall_peering_bits(shape) if wall else TABLES.get_floor_peering_bits(shape)
		for k in range(4):
			var p := Vector2i(lookup[shape][k][0], lookup[shape][k][1]) * 16
			var q := quarter(base, k, bits, wall, carpet)
			if written.has(p):
				assert(written[p] == q.get_data(), "Quarter orientation mismatch")
			else:
				written[p] = q.get_data()
				block.blit_rect(q, Rect2i(0, 0, 16, 16), p)
	return block

func filtered_atlas(ts: TileSet, data: Dictionary, names: Dictionary, name: String) -> void:
	var selected: Array = []
	for item in data.autotiles:
		if names.has(item.name):
			item.name = names[item.name]
			# RPG shapes 46 and 47 describe the same isolated connectivity. Keep
			# 47 in source/derived artwork, but register 46 only with the solver.
			item.tiles = item.tiles.filter(func(t): return t.get("shape_index", 0) != 47)
			selected.append(item)
	data.autotiles = selected
	var tex := store_image(data.image, name)
	var atlas := CONVERT.add_autotile_atlas_source(ts, data.image, data, 32)
	atlas.texture = tex

func standard_atlas(ts: TileSet, img: Image, name: String) -> void:
	var atlas := TileSetAtlasSource.new()
	atlas.texture = store_image(img, name)
	atlas.texture_region_size = Vector2i(32, 32)
	for y in range(img.get_height() / 32):
		for x in range(img.get_width() / 32):
			if img.get_region(Rect2i(x * 32, y * 32, 32, 32)).get_used_rect().has_area():
				atlas.create_tile(Vector2i(x, y))
	ts.add_source(atlas)

func furniture(src: Image, sheet: Image, name: String, fraction: Rect2, slot: Rect2i, extent: Vector2i) -> void:
	var rect := Rect2i(fraction.position * Vector2(src.get_size()), fraction.size * Vector2(src.get_size()))
	var cut := src.get_region(rect)
	cut = cut.get_region(cut.get_used_rect())
	var scale_factor := minf(float(extent.x) / cut.get_width(), float(extent.y) / cut.get_height())
	cut.resize(maxi(1, roundi(cut.get_width() * scale_factor)), maxi(1, roundi(cut.get_height() * scale_factor)), Image.INTERPOLATE_LANCZOS)
	var at := slot.position + Vector2i((slot.size.x - cut.get_width()) / 2, slot.size.y - cut.get_height())
	sheet.blit_rect(cut, Rect2i(Vector2i.ZERO, cut.get_size()), at)
	manifest[name] = {"region": [slot.position.x, slot.position.y, slot.size.x, slot.size.y], "foot": [slot.size.x / 2, slot.size.y]}

func build() -> void:
	var src := Image.load_from_file(ProjectSettings.globalize_path(ROOT + "source/materials.png"))
	src.convert(Image.FORMAT_RGBA8)
	var props := Image.load_from_file(ProjectSettings.globalize_path(ROOT + "source/furniture.png"))
	props.convert(Image.FORMAT_RGBA8)
	print("Unmodified generated sources: materials=", src.get_size(), "; furniture=", props.get_size())
	assert(props.get_pixel(0, 0).a < 0.01, "Furniture source must have true alpha")
	var stone := material(src, Vector2i(0, 0), Vector2i(128, 128))
	var masonry := material(src, Vector2i(1, 0), Vector2i(128, 128))
	var walnut := material(src, Vector2i(0, 1), Vector2i(128, 64))
	var carpet := material(src, Vector2i(1, 1), Vector2i(128, 128))
	var a2 := fresh(512, 384)
	for spec in [[base_tile(stone), 0, false], [base_tile(carpet), 64, true]]:
		a2.blit_rect(autotile_block(spec[0], false, spec[2]), Rect2i(0, 0, 64, 96), Vector2i(spec[1], 0))
	store_image(a2, "Study_A2")
	var wood_cap := src.get_region(Rect2i(int(src.get_width()*0.05),int(src.get_height()*0.65),int(src.get_width()*0.05),int(src.get_height()*0.15)))
	wood_cap.resize(32,32,Image.INTERPOLATE_LANCZOS)
	var a4 := fresh(512, 480)
	for col in range(2):
		var surface := base_tile(masonry) if col == 0 else base_tile(walnut, 1, 0)
		a4.blit_rect(autotile_block(base_tile(stone) if col == 0 else wood_cap), Rect2i(0, 0, 64, 96), Vector2i(col * 64, 0))
		a4.blit_rect(autotile_block(surface, true), Rect2i(0, 0, 64, 64), Vector2i(col * 64, 96))
	store_image(a4, "Study_A4")
	var a5 := fresh(256, 512)
	a5.blit_rect(stone, Rect2i(0, 0, 128, 128), Vector2i.ZERO)
	a5.blit_rect(masonry, Rect2i(0, 0, 128, 128), Vector2i(128, 0))
	a5.blit_rect(walnut, Rect2i(0, 0, 128, 64), Vector2i(0, 128))
	a5.blit_rect(carpet, Rect2i(0, 0, 128, 128), Vector2i(128, 128))
	var b := fresh(512, 512)
	furniture(props, b, "WorkTable", Rect2(0, 0.20, 0.60, 0.30), Rect2i(0, 0, 160, 96), Vector2i(160, 76))
	furniture(props, b, "Bookcase", Rect2(0.60, 0, 0.40, 0.50), Rect2i(192, 0, 96, 128), Vector2i(64, 96))
	furniture(props, b, "Workbench", Rect2(0, 0.60, 0.60, 0.40), Rect2i(0, 128, 128, 96), Vector2i(96, 64))
	furniture(props, b, "Window", Rect2(0.60, 0.50, 0.40, 0.50), Rect2i(320, 0, 64, 128), Vector2i(52, 84))
	var ts := CONVERT.create_tileset(32)
	filtered_atlas(ts, AUTO.generate_a2_derived_atlas(a2), {"a2_floor_r0_c0": "石板地面", "a2_floor_r0_c1": "蓝金地毯"}, "Study_A2_derived")
	filtered_atlas(ts, AUTO.generate_a4_derived_atlas(a4), {"a4_wall_top_p0_c0": "石质墙顶", "a4_wall_side_p0_c0": "石砌墙面", "a4_wall_top_p0_c1": "木质墙顶", "a4_wall_side_p0_c1": "木护墙"}, "Study_A4_derived")
	standard_atlas(ts, a5, "Study_A5")
	standard_atlas(ts, b, "Study_B")
	assert(ResourceSaver.save(ts, ROOT + "Study.tres") == OK)
	for name in manifest:
		var r: Array = manifest[name].region
		var texture := AtlasTexture.new()
		texture.atlas = load(ROOT + "Study_B.res")
		texture.region = Rect2(r[0], r[1], r[2], r[3])
		assert(ResourceSaver.save(texture, ROOT + name + ".tres") == OK)
	var file := FileAccess.open(ROOT + "manifest.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"tile_size": 32, "source_material_size": [src.get_width(), src.get_height()], "source_furniture_size": [props.get_width(), props.get_height()], "origin": "Generated high-resolution source; resampled game assets, not native 32px art", "source_ids": {"A2": 0, "A4": 1, "A5": 2, "B": 3}, "furniture": manifest}, "\t"))
	print("Built A2 512x384, A4 512x480, A5 256x512, B 512x512 and Study.tres")
	quit()
