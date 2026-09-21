extends SceneTree

const Converter = preload("res://addons/rpg_asset_adapter/converter/autotile_converter.gd")
const OUT := "res://ShareAsset/rpg2d/tilesets/academy_terrain_v1/"
const ORIGINAL := "res://ShareAsset/victorian/48px+MVMZ/"
const BASE := "res://ShareAsset/rpg2d/tilesets/academy_v1/Academy48.tres"
const SCENES := "res://scenes/AcademyTerrain/"
const REPORT := "res://../artifacts/academy-terrain-validation/"
var report: Array[String] = []

func _initialize() -> void:
	call_deferred("build")

func build() -> void:
	var tileset := (load(BASE) as TileSet).duplicate(true)
	for kind in ["A2", "A4"]:
		var original := Image.load_from_file(ORIGINAL + "Victorian_Inside_" + kind + ".png")
		original.convert(Image.FORMAT_RGBA8)
		var generated := Image.load_from_file(OUT + "source/Academy_" + kind + "_generated.png")
		generated.convert(Image.FORMAT_RGBA8)
		if kind == "A2":
			# The generator repeated a partial row below y=1024 and padded the right.
			generated = generated.get_region(Rect2i(0, 0, 1365, 1024))
		generated.resize(original.get_width(), original.get_height(), Image.INTERPOLATE_NEAREST)
		var result := original.duplicate() as Image
		# Repaint indoor A2 blocks only. Keep outdoor overlays and their geometry untouched.
		var changed_width := 480 if kind == "A2" else 768
		for y in original.get_height():
			for x in changed_width:
				var color := generated.get_pixel(x, y)
				color.a = original.get_pixel(x, y).a
				result.set_pixel(x, y, color)
		if kind == "A2":
			# The generated floor's inner quadrants formed vertical strips. Use one
			# square paving sample at identical 48px phase throughout this block.
			var paving := result.get_region(Rect2i(336, 480, 48, 48))
			for row in 3:
				for col in 2:
					result.blit_rect(paving, Rect2i(0, 0, 48, 48), Vector2i(288 + col * 48, 432 + row * 48))
		var png_path: String = OUT + "48px+MVMZ/Academy_Inside_" + kind + ".png"
		assert(result.save_png(png_path) == OK)
		for y in original.get_height():
			for x in original.get_width():
				assert(result.get_pixel(x, y).a == original.get_pixel(x, y).a)
		var derived: Dictionary = Converter.generate_a2_derived_atlas(result) if kind == "A2" else Converter.generate_a4_derived_atlas(result)
		var atlas: Image = derived.image
		var atlas_path: String = OUT + "atlases/Academy_Inside_" + kind + "_derived"
		assert(atlas.save_png(atlas_path + ".png") == OK)
		assert(ResourceSaver.save(ImageTexture.create_from_image(atlas), atlas_path + ".res") == OK)
		var replaced := false
		for i in tileset.get_source_count():
			var source := tileset.get_source(tileset.get_source_id(i)) as TileSetAtlasSource
			if source and source.texture.resource_path.get_file().get_basename() == "Victorian_Inside_" + kind + "_derived":
				assert(source.texture.get_size() == Vector2(atlas.get_size()))
				source.texture = load(atlas_path + ".res")
				replaced = true
		assert(replaced, "Missing original atlas " + kind)
		report.append("%s: %dx%d; original alpha preserved at every pixel; %d autotile groups; source IDs and peering data unchanged" % [kind, result.get_width(), result.get_height(), derived.autotiles.size()])
	assert(ResourceSaver.save(tileset, OUT + "AcademyTerrain48.tres") == OK)
	for name in ["Parlor48", "Corridor48"]:
		var room := (load("res://scenes/AcademyTileset/" + name + ".tscn") as PackedScene).instantiate()
		for child in room.get_children():
			if child is TileMapLayer:
				child.tile_set = load(OUT + "AcademyTerrain48.tres")
				child.modulate = Color.WHITE
				if name == "Corridor48" and child.name == "Floor":
					child.modulate = Color(0.62, 0.68, 0.79)
				if name == "Corridor48" and child.name == "Rugs":
					repaint(child, 0, "a2_floor_r2_c0")
				if name == "Parlor48" and child.name == "Walls":
					repaint(child, 1, "a4_wall_side_p0_c6")
		var packed := PackedScene.new()
		assert(packed.pack(room) == OK)
		var path: String = SCENES + name + ".tscn"
		assert(ResourceSaver.save(packed, path) == OK)
		room.free()
		room = (ResourceLoader.load(path, "PackedScene", ResourceLoader.CACHE_MODE_IGNORE) as PackedScene).instantiate()
		var count := 0
		for child in room.get_children():
			if child is TileMapLayer:
				for cell in child.get_used_cells():
					assert(child.get_cell_tile_data(cell) != null)
					count += 1
		report.append("%s: %d valid cells after reload" % [name, count])
		if "--capture" in OS.get_cmdline_user_args():
			var viewport := SubViewport.new()
			viewport.size = Vector2i(1152, 768)
			viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
			root.add_child(viewport)
			viewport.add_child(room)
			await process_frame
			await process_frame
			await RenderingServer.frame_post_draw
			assert(viewport.get_texture().get_image().save_png(REPORT + name + ".png") == OK)
			viewport.queue_free()
		else:
			room.free()
	var file := FileAccess.open(REPORT + "validation.txt", FileAccess.WRITE)
	file.store_string("\n".join(report) + "\n")
	print("\n".join(report))
	quit()

func repaint(layer: TileMapLayer, set_index: int, terrain_name: String) -> void:
	for i in layer.tile_set.get_terrains_count(set_index):
		if layer.tile_set.get_terrain_name(set_index, i) == terrain_name:
			var cells := layer.get_used_cells()
			layer.clear()
			layer.set_cells_terrain_connect(cells, set_index, i, false)
			return
	assert(false, terrain_name)
