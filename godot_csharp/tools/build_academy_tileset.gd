extends SceneTree

const OUT := "res://ShareAsset/rpg2d/tilesets/academy_v1/"
const SCENES := "res://scenes/AcademyTileset/"
const BASE := "res://ShareAsset/victorian/tilesets/Victorian.tres"
const LABELS := ["bookcase", "lit_fireplace", "sofa", "research_desk", "piano", "clock", "banner", "telescope", "closed_door", "open_door", "vertical_pipe", "elbow_pipe", "inscription_pedestal", "alchemy_bench", "tea_table", "lantern"]
# Measured object boundaries in the generated source, not an assumed AI grid.
const REGIONS := [Rect2i(40, 30, 240, 290), Rect2i(318, 40, 304, 287), Rect2i(632, 118, 306, 202), Rect2i(954, 54, 279, 272), Rect2i(40, 337, 267, 292), Rect2i(398, 331, 149, 300), Rect2i(682, 332, 202, 299), Rect2i(992, 337, 225, 297), Rect2i(34, 639, 278, 279), Rect2i(330, 639, 278, 279), Rect2i(681, 636, 174, 289), Rect2i(939, 702, 296, 215), Rect2i(78, 922, 171, 298), Rect2i(313, 937, 305, 288), Rect2i(632, 1004, 308, 217), Rect2i(985, 942, 186, 278)]
var tileset: TileSet
var source_id: int
var report: Array[String] = []

func _initialize() -> void:
	call_deferred("build")

func build() -> void:
	var input := Image.load_from_file(OUT + "source/academy_objects_generated.png")
	var atlas := Image.create(768, 768, false, Image.FORMAT_RGBA8)
	atlas.fill(Color.TRANSPARENT)
	var entries: Array = []
	for i in LABELS.size():
		var piece := input.get_region(REGIONS[i])
		var bounds := piece.get_used_rect()
		piece = piece.get_region(bounds)
		var factor := minf(176.0 / piece.get_width(), 176.0 / piece.get_height())
		piece.resize(maxi(1, roundi(piece.get_width() * factor)), maxi(1, roundi(piece.get_height() * factor)), Image.INTERPOLATE_NEAREST)
		var origin := Vector2i((i % 4) * 192, (i / 4) * 192)
		atlas.blit_rect(piece, Rect2i(Vector2i.ZERO, piece.get_size()), origin + Vector2i((192 - piece.get_width()) / 2, 184 - piece.get_height()))
		entries.append({"name": LABELS[i], "atlas_cell": [origin.x / 48, origin.y / 48], "size_cells": [4, 4], "source_rect": [REGIONS[i].position.x, REGIONS[i].position.y, REGIONS[i].size.x, REGIONS[i].size.y]})
	assert(atlas.save_png(OUT + "academy_objects_48.png") == OK)
	var texture := ImageTexture.create_from_image(atlas)
	assert(ResourceSaver.save(texture, OUT + "academy_objects_48.res") == OK)
	tileset = (load(BASE) as TileSet).duplicate(true)
	var source := TileSetAtlasSource.new()
	source.texture = load(OUT + "academy_objects_48.res")
	source.texture_region_size = Vector2i(48, 48)
	for y in 16:
		for x in 16:
			source.create_tile(Vector2i(x, y))
	source_id = tileset.get_next_source_id()
	tileset.add_source(source, source_id)
	assert(ResourceSaver.save(tileset, OUT + "Academy48.tres") == OK)
	var manifest := FileAccess.open(OUT + "catalog.json", FileAccess.WRITE)
	manifest.store_string(JSON.stringify({"tile_size": 48, "atlas_size": [768, 768], "source_id": source_id, "provenance": "AI source adapted using nearest-neighbor; not native 48px authored art", "objects": entries}, "\t"))
	manifest.close()
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SCENES))
	for underground in [false, true]:
		var room := make_room(underground)
		var path := SCENES + ("Corridor48.tscn" if underground else "Parlor48.tscn")
		var packed := PackedScene.new()
		assert(packed.pack(room) == OK)
		assert(ResourceSaver.save(packed, path) == OK)
		room.free()
		var reloaded := (load(path) as PackedScene).instantiate()
		var count := 0
		for child in reloaded.get_children():
			if child is TileMapLayer:
				for cell in child.get_used_cells():
					assert(child.get_cell_tile_data(cell) != null)
					count += 1
		report.append("%s: %d valid cells after reload" % [path, count])
		if "--capture" in OS.get_cmdline_user_args():
			var viewport := SubViewport.new()
			viewport.size = Vector2i(1152, 768)
			viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
			root.add_child(viewport)
			viewport.add_child(reloaded)
			await process_frame
			await process_frame
			await RenderingServer.frame_post_draw
			assert(viewport.get_texture().get_image().save_png("res://../artifacts/academy-tileset-validation/" + ("corridor.png" if underground else "parlor.png")) == OK)
			viewport.queue_free()
		else:
			reloaded.free()
	# Every stamp has a transparent border; no neighboring object leaks into its cells.
	for i in 16:
		var origin := Vector2i((i % 4) * 192, (i / 4) * 192)
		for p in 192:
			assert(atlas.get_pixelv(origin + Vector2i(p, 0)).a == 0)
			assert(atlas.get_pixelv(origin + Vector2i(p, 191)).a == 0)
			assert(atlas.get_pixelv(origin + Vector2i(0, p)).a == 0)
			assert(atlas.get_pixelv(origin + Vector2i(191, p)).a == 0)
	report.append("768x768 RGBA; 256 cells at 48px; 16 isolated stamps; transparent borders verified")
	var file := FileAccess.open("res://../artifacts/academy-tileset-validation/validation.txt", FileAccess.WRITE)
	file.store_string("\n".join(report) + "\n")
	print("\n".join(report))
	quit()

func make_layer(room: Node2D, label: String) -> TileMapLayer:
	var result := TileMapLayer.new()
	result.name = label
	result.tile_set = load(OUT + "Academy48.tres")
	room.add_child(result)
	result.owner = room
	return result

func paint(layer: TileMapLayer, rect: Rect2i, set_index: int, label: String) -> void:
	var terrain := -1
	for i in tileset.get_terrains_count(set_index):
		if tileset.get_terrain_name(set_index, i) == label:
			terrain = i
	assert(terrain >= 0)
	var cells: Array[Vector2i] = []
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x):
			cells.append(Vector2i(x, y))
	layer.set_cells_terrain_connect(cells, set_index, terrain, false)

func stamp(layer: TileMapLayer, index: int, pos: Vector2i) -> void:
	for y in 4:
		for x in 4:
			layer.set_cell(pos + Vector2i(x, y), source_id, Vector2i((index % 4) * 4 + x, (index / 4) * 4 + y))

func make_room(underground: bool) -> Node2D:
	var room := Node2D.new()
	room.name = "Corridor48" if underground else "Parlor48"
	room.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var floor_layer := make_layer(room, "Floor")
	floor_layer.modulate = Color(0.43, 0.48, 0.56) if underground else Color(0.59, 0.54, 0.47)
	paint(floor_layer, Rect2i(0, 3, 24, 13), 0, "a2_floor_r3_c3")
	var walls := make_layer(room, "Walls")
	walls.modulate = floor_layer.modulate
	paint(walls, Rect2i(0, 0, 24, 4), 1, "a4_wall_side_p0_c5")
	var rugs := make_layer(room, "Rugs")
	rugs.modulate = Color(0.30, 0.39, 0.57) if underground else Color(0.65, 0.34, 0.37)
	paint(rugs, Rect2i(8, 5, 8, 9), 0, "a2_floor_r2_c2")
	var furniture := make_layer(room, "Furniture")
	if underground:
		for item in [[8, 4, 0], [9, 12, 0], [8, 16, 0], [10, 0, 0], [10, 20, 0], [11, 0, 6], [11, 20, 6], [12, 4, 10], [13, 16, 10], [6, 8, 0]]:
			stamp(furniture, item[0], Vector2i(item[1], item[2]))
	else:
		for item in [[0, 2, 0], [1, 10, 0], [0, 16, 0], [5, 20, 0], [6, 6, 0], [2, 10, 5], [14, 10, 9], [4, 1, 10], [3, 19, 10], [7, 19, 6]]:
			stamp(furniture, item[0], Vector2i(item[1], item[2]))
	var camera := Camera2D.new()
	camera.name = "Camera2D"
	camera.position = Vector2(576, 384)
	room.add_child(camera)
	camera.owner = room
	return room
