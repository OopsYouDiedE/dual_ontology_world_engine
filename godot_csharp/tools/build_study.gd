extends SceneTree
## Builds only the playable study. Architecture remains 32px Terrain-connected TileMaps.
const ART := "res://ShareAsset/Internal/Props/ReasoningStudy/"
func _initialize() -> void: call_deferred("build")
func own(room: Node, parent: Node, child: Node) -> void:
	parent.add_child(child); child.owner = room
func terrain(ts: TileSet, set_id: int, label: String) -> int:
	for i in range(ts.get_terrains_count(set_id)):
		if ts.get_terrain_name(set_id, i) == label: return i
	assert(false, "Unknown terrain: " + label)
	return -1
func rect_cells(rect: Rect2i) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	for y in range(rect.position.y, rect.end.y):
		for x in range(rect.position.x, rect.end.x): cells.append(Vector2i(x,y))
	return cells
func layer(room: Node, ts: TileSet, label: String, set_id: int, material: String, cells: Array[Vector2i]) -> TileMapLayer:
	var map := TileMapLayer.new(); map.name = label; map.tile_set = ts
	own(room, room, map)
	map.set_cells_terrain_connect(cells, set_id, terrain(ts, set_id, material), false)
	return map
func polygon(room: Node, parent: Node, points: PackedVector2Array, color: Color) -> Polygon2D:
	var p := Polygon2D.new(); p.polygon = points; p.color = color; own(room,parent,p); return p
func shadow(room: Node, parent: Node, foot: Vector2, width: float) -> void:
	var points := PackedVector2Array()
	for i in range(24):
		var angle := TAU * i / 24.0
		points.append(foot + Vector2(cos(angle)*width*0.48, sin(angle)*5))
	polygon(room,parent,points,Color(0.10,0.08,0.07,0.25))
func prop(room: Node, parent: Node, name_: String, resource: String, foot: Vector2) -> void:
	var a := Node2D.new(); a.name = name_; a.position = foot; own(room,parent,a)
	var s := Sprite2D.new(); s.texture = load(ART + resource + ".tres"); s.position = Vector2(0,-s.texture.get_height()/2.0); own(room,a,s)
func old_prop(room: Node, parent: Node, label: String, sheet: String, region: Rect2i, foot: Vector2) -> void:
	var a := Node2D.new(); a.name = label; a.position = foot; own(room,parent,a)
	var s := Sprite2D.new(); var t := AtlasTexture.new()
	t.atlas = load("res://ShareAsset/External/Victorian/textures/Victorian_Inside_" + sheet + ".png"); t.region = region
	s.texture = t; s.position = Vector2(0,-region.size.y/2.0); s.modulate = Color(0.78,0.76,0.68); own(room,a,s)
func build() -> void:
	var room := Node2D.new(); room.name = "StudyRoom"; room.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	var ts: TileSet = load(ART + "Study.tres")
	room.set_meta("architecture", "32px TileMap terrains; independent Y-sorted furniture")
	var floor_cells := rect_cells(Rect2i(1,3,18,8)); floor_cells.append_array(rect_cells(Rect2i(9,11,2,2)))
	layer(room,ts,"Floor",0,"石板地面",floor_cells)
	layer(room,ts,"Walls",1,"石砌墙面",rect_cells(Rect2i(1,0,18,3)))
	var caps := rect_cells(Rect2i(0,0,1,12)); caps.append_array(rect_cells(Rect2i(19,0,1,12)))
	caps.append_array(rect_cells(Rect2i(1,11,8,1))); caps.append_array(rect_cells(Rect2i(11,11,8,1)))
	layer(room,ts,"WallCaps",0,"木质墙顶",caps)
	layer(room,ts,"Wainscot",1,"木护墙",rect_cells(Rect2i(1,2,18,1)))
	var rug := rect_cells(Rect2i(7,5,6,4)); rug.append_array(rect_cells(Rect2i(9,11,2,2)))
	layer(room,ts,"Rugs",0,"蓝金地毯",rug)
	var lighting := Node2D.new(); lighting.name = "FloorLightAndShadows"; own(room,room,lighting)
	# Light is a separate floor overlay. Mullion gaps remain dark; sprites keep their own forms.
	for x in [65,83,129,147]:
		var p := polygon(room,lighting,PackedVector2Array([Vector2(x,96),Vector2(x+13,96),Vector2(x+168,330),Vector2(x+149,330)]),Color.WHITE)
		p.vertex_colors = PackedColorArray([Color(1,0.88,0.62,0.25),Color(1,0.88,0.62,0.25),Color(1,0.88,0.62,0.04),Color(1,0.88,0.62,0.04)])
	for y in [150,205,260]:
		polygon(room,lighting,PackedVector2Array([Vector2(48+(y-96)*0.65,y),Vector2(180+(y-96)*0.65,y),Vector2(184+(y-96)*0.65,y+4),Vector2(52+(y-96)*0.65,y+4)]),Color(0.18,0.15,0.10,0.10))
	polygon(room,lighting,PackedVector2Array([Vector2(32,96),Vector2(608,96),Vector2(608,105),Vector2(32,105)]),Color(0.12,0.10,0.09,0.22))
	for spec in [[Vector2(320,239),160],[Vector2(112,143),64],[Vector2(480,143),96],[Vector2(176,351),96],[Vector2(400,351),64],[Vector2(528,351),96]]: shadow(room,lighting,spec[0],spec[1])
	var depth := Node2D.new(); depth.name = "Furnishings"; depth.y_sort_enabled = true; own(room,room,depth)
	prop(room,depth,"WorkTable","WorkTable",Vector2(320,240))
	prop(room,depth,"PreparationTable","Workbench",Vector2(480,144))
	prop(room,depth,"Washstand","Workbench",Vector2(528,352))
	prop(room,depth,"DoctorsTable","Workbench",Vector2(176,352))
	prop(room,depth,"WindowLeft","Window",Vector2(80,88)); prop(room,depth,"WindowRight","Window",Vector2(144,88))
	prop(room,depth,"BookcaseLeft","Bookcase",Vector2(288,96)); prop(room,depth,"BookcaseRight","Bookcase",Vector2(368,96))
	old_prop(room,depth,"WindowSeat","B",Rect2i(128,224,64,32),Vector2(112,144))
	old_prop(room,depth,"Armchair","B",Rect2i(352,160,32,32),Vector2(112,208))
	old_prop(room,depth,"LockedCabinet","C",Rect2i(256,320,64,64),Vector2(544,96))
	old_prop(room,depth,"PaintersTable","B",Rect2i(128,224,64,32),Vector2(400,352))
	old_prop(room,depth,"Clock","C",Rect2i(320,0,32,64),Vector2(432,88))
	for p in [Vector2(48,128),Vector2(592,128)]: old_prop(room,depth,"Plant"+str(p.x),"C",Rect2i(192,288,32,32),p)
	var packed := PackedScene.new(); assert(packed.pack(room) == OK)
	var save_err := ResourceSaver.save(packed,"res://scenes/Reasoning/StudyRoom.tscn")
	if save_err != OK:
		push_error("Could not save StudyRoom.tscn: " + error_string(save_err))
		room.free()
		quit(1)
		return
	room.free(); print("StudyRoom saved: terrain-connected architecture, new furniture, separate floor lighting."); quit()
