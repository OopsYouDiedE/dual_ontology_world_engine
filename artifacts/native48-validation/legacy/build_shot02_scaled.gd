extends SceneTree

const ASSETS := "res://ShareAsset/rpg2d/shot02/"
const PREVIOUS := "res://ShareAsset/rpg2d/shot01/"
const TILESET := "res://ShareAsset/rpg2d/tilesets/academy_terrain_v1/AcademyTerrain48.tres"
const SCENE := "res://scenes/Shot02/Shot02.tscn"
const OUTPUT := "res://../artifacts/shot02-validation/"
var room: Node2D
var tileset: TileSet
var textures: Dictionary = {}
var finish: ShaderMaterial

func _initialize() -> void:
	call_deferred("build")

func texture(name: String, image: Image) -> void:
	assert(image.save_png(ASSETS+name+".png")==OK)
	assert(ResourceSaver.save(ImageTexture.create_from_image(image),ASSETS+name+".res")==OK)
	textures[name]=load(ASSETS+name+".res")

func extract(image: Image, name: String, region: Rect2i, size: Vector2i) -> void:
	var part:=image.get_region(region)
	part=part.get_region(part.get_used_rect())
	part.resize(size.x,size.y,Image.INTERPOLATE_NEAREST)
	texture(name,part)

func prepare() -> void:
	var image:=Image.load_from_file(ASSETS+"source/kitchen_generated.png")
	extract(image,"hearth",Rect2i(61,0,1004,450),Vector2i(576,252))
	extract(image,"pantry",Rect2i(1175,0,274,474),Vector2i(144,252))
	extract(image,"breakfast_table",Rect2i(87,470,1359,278),Vector2i(672,192))
	extract(image,"prep_island",Rect2i(25,742,773,253),Vector2i(528,168))
	extract(image,"side_table",Rect2i(810,788,371,204),Vector2i(240,128))
	extract(image,"chair",Rect2i(1192,780,129,210),Vector2i(48,96))
	extract(image,"bread_barrel",Rect2i(1343,779,164,215),Vector2i(72,96))
	image=Image.load_from_file(ASSETS+"source/breakfast_characters_generated.png")
	extract(image,"detective",Rect2i(28,423,348,395),Vector2i(104,112))
	extract(image,"naturalist",Rect2i(413,428,355,391),Vector2i(112,112))
	extract(image,"chef",Rect2i(783,152,330,750),Vector2i(96,208))
	extract(image,"engineer",Rect2i(1156,420,355,398),Vector2i(104,112))
	image=Image.load_from_file("res://ShareAsset/rpg2d/tilesets/academy_v1/academy_objects_48.png")
	extract(image,"wall_lantern",Rect2i(576,576,192,192),Vector2i(24,56))
	image=Image.load_from_file(ASSETS+"source/joinery_generated.png")
	extract(image,"closed_door",Rect2i(142,18,581,668),Vector2i(192,252))
	extract(image,"supplies",Rect2i(860,241,630,458),Vector2i(112,96))
	extract(image,"front_gate",Rect2i(7,722,920,253),Vector2i(864,192))
	extract(image,"front_panel",Rect2i(88,737,173,227),Vector2i(144,192))
	extract(image,"bread_plate",Rect2i(969,707,550,287),Vector2i(88,48))
	image=Image.load_from_file("res://ShareAsset/rpg2d/flags/five_emblems_48.png")
	texture("academy_emblem",image.get_region(Rect2i(144,0,48,48)))
	for name in ["window"]:
		textures[name]=load(PREVIOUS+name+".res")
	for name in ["fern","tree","books","candelabrum"]:
		textures[name]=load(PREVIOUS+"revision2/"+name+".res")

func build() -> void:
	prepare()
	tileset=load(TILESET)
	finish=ShaderMaterial.new()
	finish.shader=load("res://scenes/Shot01/material_finish.gdshader")
	room=Node2D.new()
	room.name="Shot02"
	room.texture_filter=CanvasItem.TEXTURE_FILTER_NEAREST
	room.set_script(load("res://scenes/Shot02/Shot02.gd"))
	room.set_meta("reference","artifacts/theme-film/scenes/shot-02.png")
	var floor_layer:=layer("StoneFloor")
	var stone:=ShaderMaterial.new()
	stone.shader=load("res://scenes/Shot01/stone_finish.gdshader")
	floor_layer.material=stone
	paint(floor_layer,Rect2i(0,4,32,15),0,"a2_floor_r3_c3")
	var walls:=layer("BackWall")
	walls.modulate=Color(0.94,0.87,0.75)
	paint(walls,Rect2i(0,0,32,5),1,"a4_wall_side_p0_c5")
	var wood:=layer("Wainscot")
	wood.modulate=Color(0.82,0.76,0.66)
	paint(wood,Rect2i(0,3,32,2),1,"a4_wall_side_p0_c6")
	var rugs:=layer("Rugs")
	rugs.modulate=Color(0.72,0.75,0.78)
	paint(rugs,Rect2i(8,9,16,6),0,"a2_floor_r2_c0")
	paint(rugs,Rect2i(27,5,4,2),0,"a2_floor_r2_c0")
	var kitchen:=group("Kitchen")
	place(kitchen,"hearth",Vector2(756,120))
	for x in [336,1168]:
		place(kitchen,"pantry",Vector2(x,128))
	place(kitchen,"closed_door",Vector2(1368,126))
	place(kitchen,"prep_island",Vector2(768,272))
	for x in [448,1232]:
		place(kitchen,"bread_barrel",Vector2(x,246))
	place(kitchen,"supplies",Vector2(1100,237))
	for x in [36,112]:
		place(kitchen,"window",Vector2(x,138))
	for x in [238,444,1094,1256,1492]:
		place(kitchen,"wall_lantern",Vector2(x,112))
	var sides:=group("SideFurniture")
	for pos in [Vector2(208,510),Vector2(128,746),Vector2(1416,486),Vector2(1424,742)]:
		place(sides,"chair",pos+Vector2(0,-80))
		place(sides,"side_table",pos)
		place(sides,"chair",pos+Vector2(0,82))
		place(sides,"bread_plate",pos+Vector2(36,-24))
		place(sides,"books",pos+Vector2(-62,-24))
		var candle:=place(sides,"candelabrum",pos+Vector2(-86,-34))
		candle.scale=Vector2(0.32,0.32)
	for pos in [Vector2(200,230),Vector2(1510,268)]:
		place(sides,"tree",pos)
	for pos in [Vector2(48,426),Vector2(44,786)]:
		place(sides,"fern",pos)
	var buffet:=place(sides,"prep_island",Vector2(92,328))
	buffet.scale=Vector2(0.32,0.62)
	place(sides,"academy_emblem",Vector2(92,363)).scale=Vector2(0.7,0.7)
	var breakfast:=group("Breakfast")
	for x in [560,704,976]:
		place(breakfast,"chair",Vector2(x,450))
	place(breakfast,"chef",Vector2(840,408),true)
	place(breakfast,"breakfast_table",Vector2(784,558))
	place(breakfast,"detective",Vector2(560,438),true)
	place(breakfast,"naturalist",Vector2(704,438),true)
	place(breakfast,"engineer",Vector2(976,438),true)
	for x in [536,672,808,944,1080]:
		place(breakfast,"chair",Vector2(x,666))
	var sunlight:=group("WindowLight")
	for col in 6:
		for row in 3:
			var start:=Vector2(164,280)+Vector2(98,58)*col+Vector2(0,30)*row
			poly(sunlight,PackedVector2Array([start,start+Vector2(88,52),start+Vector2(88,74),start+Vector2(0,22)]),Color(1.0,0.85,0.58,0.18))
	# A second strip crosses the left aisle, leaving actors unobscured.
	for col in 4:
		for row in 2:
			var start:=Vector2(180,588)+Vector2(76,45)*col+Vector2(0,28)*row
			poly(sunlight,PackedVector2Array([start,start+Vector2(67,40),start+Vector2(67,61),start+Vector2(0,21)]),Color(1.0,0.85,0.58,0.15))
	var rail:=group("ForegroundRail")
	for x in [-24,120,264,1272,1416,1560]:
		place(rail,"front_panel",Vector2(x,896))
	place(rail,"front_gate",Vector2(768,896))
	place(rail,"academy_emblem",Vector2(768,870))
	var shade:=ColorRect.new()
	shade.name="PeripheralShade"
	shade.size=Vector2(1536,912)
	shade.mouse_filter=Control.MOUSE_FILTER_IGNORE
	var shade_material:=ShaderMaterial.new()
	shade_material.shader=load("res://scenes/Shot01/edge_shade.gdshader")
	shade.material=shade_material
	add(room,shade)
	var camera:=Camera2D.new()
	camera.name="Camera2D"
	camera.position=Vector2(768,456)
	add(room,camera)
	var packed:=PackedScene.new()
	assert(packed.pack(room)==OK)
	assert(ResourceSaver.save(packed,SCENE)==OK)
	room.free()
	room=(ResourceLoader.load(SCENE,"PackedScene",ResourceLoader.CACHE_MODE_IGNORE) as PackedScene).instantiate()
	var count:=0
	for child in room.get_children():
		if child is TileMapLayer:
			for cell in child.get_used_cells():
				assert(child.get_cell_tile_data(cell)!=null)
				count+=1
	if "--capture" in OS.get_cmdline_user_args():
		var viewport:=SubViewport.new()
		viewport.size=Vector2i(1536,912)
		viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.add_child(room)
		await process_frame
		await process_frame
		await RenderingServer.frame_post_draw
		assert(viewport.get_texture().get_image().save_png(OUTPUT+"shot02.png")==OK)
		viewport.queue_free()
	else:
		room.free()
	var result:="Shot02: %d valid cells after reload; 32x19 map; 48px grid; 1536x912 render.\nFour independent character sprites and separate kitchen, dining, side furniture, light and foreground layers.\n"%count
	var file:=FileAccess.open(OUTPUT+"validation.txt",FileAccess.WRITE)
	file.store_string(result)
	print(result)
	quit()

func add(parent:Node,child:Node) -> void:
	parent.add_child(child)
	child.owner=room

func group(label:String) -> Node2D:
	var node:=Node2D.new()
	node.name=label
	add(room,node)
	return node

func layer(label:String) -> TileMapLayer:
	var node:=TileMapLayer.new()
	node.name=label
	node.tile_set=tileset
	add(room,node)
	return node

func place(parent:Node,label:String,pos:Vector2,character:bool=false) -> Sprite2D:
	var tex:Texture2D=textures[label]
	if label in ["pantry","prep_island","breakfast_table","side_table","chair","fern","tree","bread_barrel"]:
		var shadow:=Sprite2D.new()
		shadow.texture=tex
		shadow.position=pos+Vector2(8,tex.get_height()*0.42)
		shadow.scale=Vector2(0.97,0.15)
		shadow.modulate=Color(0.04,0.03,0.02,0.22)
		add(parent,shadow)
	var node:=Sprite2D.new()
	node.name=label.to_pascal_case()
	node.texture=tex
	node.position=pos
	if not character and label!="hearth":
		node.material=finish
	add(parent,node)
	return node

func paint(target:TileMapLayer,rect:Rect2i,set_index:int,label:String) -> void:
	var terrain:=-1
	for i in tileset.get_terrains_count(set_index):
		if tileset.get_terrain_name(set_index,i)==label:
			terrain=i
	assert(terrain>=0,label)
	var cells:Array[Vector2i]=[]
	for y in range(rect.position.y,rect.end.y):
		for x in range(rect.position.x,rect.end.x):
			cells.append(Vector2i(x,y))
	target.set_cells_terrain_connect(cells,set_index,terrain,false)

func poly(parent:Node,points:PackedVector2Array,color:Color) -> void:
	var node:=Polygon2D.new()
	node.polygon=points
	node.color=color
	add(parent,node)
