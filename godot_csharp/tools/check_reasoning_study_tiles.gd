extends SceneTree
const DIR := "res://ShareAsset/Internal/Props/ReasoningStudy/"
const SIDES := {TileSet.CELL_NEIGHBOR_LEFT_SIDE: Vector2i(-1,0), TileSet.CELL_NEIGHBOR_RIGHT_SIDE: Vector2i(1,0), TileSet.CELL_NEIGHBOR_TOP_SIDE: Vector2i(0,-1), TileSet.CELL_NEIGHBOR_BOTTOM_SIDE: Vector2i(0,1)}
const CORNERS := {TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER: Vector2i(-1,-1), TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER: Vector2i(1,-1), TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER: Vector2i(-1,1), TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER: Vector2i(1,1)}
var failed := false
var checked := 0
func _initialize() -> void: call_deferred("verify")
func require(ok: bool, text: String) -> void:
	if not ok:
		failed = true
		push_error(text)
func terrain(ts: TileSet, set_id: int, name: String) -> int:
	for i in range(ts.get_terrains_count(set_id)):
		if ts.get_terrain_name(set_id, i) == name: return i
	return -1
func pattern(kind: int) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	for y in range(5):
		for x in range(5):
			if (kind == 0 and x == 2 and y == 2) or (kind == 1) or (kind == 2 and (x == 0 or y == 4)) or (kind == 3 and (x == 0 or x == 4 or y == 0 or y == 4)) or (kind == 4 and (x == 2 or y == 2)):
				cells.append(Vector2i(x,y))
	return cells
func verify() -> void:
	var ts: TileSet = load(DIR + "Study.tres")
	root.content_scale_size = Vector2i.ZERO
	root.size = Vector2i(1280, 950)
	var preview := Node2D.new()
	preview.name = "StudyTerrainPreview"
	preview.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	root.add_child(preview)
	var backdrop := Polygon2D.new()
	backdrop.polygon = PackedVector2Array([Vector2.ZERO, Vector2(1280,0), Vector2(1280,950), Vector2(0,950)])
	backdrop.color = Color("171d21")
	preview.add_child(backdrop); backdrop.owner = preview
	var font := SystemFont.new(); font.font_names = PackedStringArray(["Microsoft YaHei"])
	var specs := [[0, "石板地面"], [0, "蓝金地毯"], [1, "石砌墙面"], [0, "木质墙顶"], [0,"石质墙顶"], [1,"木护墙"]]
	for row in range(specs.size()):
		var set_id: int = specs[row][0]
		var id := terrain(ts, set_id, specs[row][1])
		require(id >= 0, "Missing named terrain")
		for kind in range(5):
			var layer := TileMapLayer.new()
			layer.name = "Terrain_%d_%d" % [row,kind]
			layer.tile_set = ts
			layer.visible = row < 4
			layer.position = Vector2(36 + kind * 246, 46 + row * 222)
			preview.add_child(layer); layer.owner = preview
			var cells := pattern(kind)
			layer.set_cells_terrain_connect(cells, set_id, id, false)
			for p in cells:
				var tile := layer.get_cell_tile_data(p)
				require(tile != null, "No tile selected")
				if tile == null: continue
				require(tile.terrain == id and tile.terrain_set == set_id, "Wrong terrain selected")
				for bit in SIDES:
					var expected: int = id if cells.has(p + SIDES[bit]) else -1
					require(tile.get_terrain_peering_bit(bit) == expected, "%s pattern%d at%s side%d" % [specs[row][1],kind,p,bit])
				if set_id == 0:
					for bit in CORNERS:
						var d: Vector2i = CORNERS[bit]
						var connected := cells.has(p+d) and cells.has(p+Vector2i(d.x,0)) and cells.has(p+Vector2i(0,d.y))
						require(tile.get_terrain_peering_bit(bit) == (id if connected else -1), "Wrong inner/outer corner")
				checked += 1
			var label := Label.new()
			label.visible = row < 4
			label.text = specs[row][1] + " · " + ["单格", "连铺", "L形", "内孔", "交叉"][kind]
			label.position = layer.position - Vector2(0,29)
			label.add_theme_font_override("font",font)
			label.add_theme_font_size_override("font_size",16)
			preview.add_child(label); label.owner = preview
	# Ordinary tiles remain on their own layer; auto-connect never touches this layer.
	require(ts.get_source(2).get_tile_data(Vector2i(0,0),0).terrain_set == -1, "A5 must not claim autotile semantics")
	require(ts.get_source(3).get_tile_data(Vector2i(1,1),0).terrain_set == -1, "B must not claim autotile semantics")
	var scene := PackedScene.new(); require(scene.pack(preview) == OK, "Pack preview")
	require(ResourceSaver.save(scene, DIR + "TerrainPreview.tscn") == OK, "Save preview")
	if DisplayServer.get_name() != "headless":
		await process_frame
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png("res://../artifacts/tea-reasoning/terrain-preview.png")
	print("Terrain tiles checked: ", checked)
	if not failed: print("PASS: isolated, connected, L, hole, cross; cardinal/corner peering and standard tile separation")
	quit(1 if failed else 0)
