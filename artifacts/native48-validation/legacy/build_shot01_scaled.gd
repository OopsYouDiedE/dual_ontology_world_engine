extends SceneTree

const TILESET := "res://ShareAsset/rpg2d/tilesets/academy_terrain_v1/AcademyTerrain48.tres"
const ASSETS := "res://ShareAsset/rpg2d/shot01/"
const SCENE := "res://scenes/Shot01/Shot01.tscn"
const OUTPUT := "res://../artifacts/shot01-validation/"
const NAMES := ["clock", "portal", "window", "statue", "compass", "bench", "detective_back", "musician_front"]
const REGIONS := [Rect2i(0,0,524,561), Rect2i(537,0,399,557), Rect2i(969,0,243,551), Rect2i(1267,49,227,518), Rect2i(9,563,491,414), Rect2i(521,719,431,192), Rect2i(991,606,179,359), Rect2i(1280,619,182,341)]
const SIZES := [Vector2i(336,336), Vector2i(192,288), Vector2i(96,288), Vector2i(96,192), Vector2i(288,288), Vector2i(192,72), Vector2i(60,112), Vector2i(56,104)]
var room: Node2D
var tileset: TileSet
var report: Array[String] = []
var finish: ShaderMaterial
const REVISION := ASSETS + "revision2/"

func _initialize() -> void:
	call_deferred("build")

func save_texture(img: Image, path: String) -> Texture2D:
	assert(img.save_png(path + ".png") == OK)
	assert(ResourceSaver.save(ImageTexture.create_from_image(img), path + ".res") == OK)
	return load(path + ".res")

func build() -> void:
	var source := Image.load_from_file(ASSETS + "source/central_hall_generated.png")
	source.convert(Image.FORMAT_RGBA8)
	for i in NAMES.size():
		var piece := source.get_region(REGIONS[i])
		piece = piece.get_region(piece.get_used_rect())
		piece.resize(SIZES[i].x, SIZES[i].y, Image.INTERPOLATE_NEAREST)
		save_texture(piece, ASSETS + NAMES[i])
	prepare_revision()
	finish=ShaderMaterial.new()
	finish.shader=load("res://scenes/Shot01/material_finish.gdshader")
	tileset = load(TILESET)
	room = Node2D.new()
	room.name = "Shot01"
	room.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	room.set_script(load("res://scenes/Shot01/Shot01.gd"))
	room.set_meta("reference", "artifacts/theme-film/scenes/shot-01.png")
	var floor_layer := layer("Floor")
	var stone:=ShaderMaterial.new()
	stone.shader=load("res://scenes/Shot01/stone_finish.gdshader")
	floor_layer.material=stone
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
	for y in [7,16]:
		line(inlay, PackedVector2Array([Vector2(144,y*48+12),Vector2(1392,y*48+12)]), Color(0.54,0.42,0.25), 3)
	for x in [3,29]:
		line(inlay, PackedVector2Array([Vector2(x*48,348),Vector2(x*48,780)]), Color(0.54,0.42,0.25),3)
	for y in range(7,17,2):
		for x in range(4,29,2):
			var c := Vector2(x*48,y*48+12)
			polygon(inlay, PackedVector2Array([c+Vector2(0,-7),c+Vector2(7,0),c+Vector2(0,7),c+Vector2(-7,0)]), Color(0.36,0.34,0.30))
	sprite(inlay,"CompassRose", load(ASSETS+"compass.res"), Vector2(768,526))
	var architecture := group("Architecture")
	for x in [288,504,1032,1248]:
		sprite(architecture,"Portal",load(ASSETS+"portal.res"),Vector2(x,192))
	sprite(architecture,"MechanicalClockAndHorns",load(ASSETS+"clock.res"),Vector2(768,168))
	for x in [48,1488]:
		sprite(architecture,"MorningWindow",load(ASSETS+"window.res"),Vector2(x,184))
	var furniture := group("Furniture")
	for x in [224,1312]:
		sprite(furniture,"Bench",load(ASSETS+"bench.res"),Vector2(x,596))
	for x in [336,1200]:
		sprite(furniture,"ScholarStatue",load(ASSETS+"statue.res"),Vector2(x,720))
	for x in [78,1458]:
		sprite(furniture,"ResearchTable",load(REVISION+"research_table.res"),Vector2(x,690))
		object(furniture,"Bookcase",0,Vector2(x,502),Vector2i(96,144))
	for x in [112,1424]:
		sprite(furniture,"PottedTree",load(REVISION+"tree.res"),Vector2(x,346))
	for x in [72,1464]:
		sprite(furniture,"PottedFern",load(REVISION+"fern.res"),Vector2(x,578))
	for x in [160,1376]:
		sprite(furniture,"Candelabra",load(REVISION+"candelabrum.res"),Vector2(x,432))
	for x in [176,400,592,944,1136,1360]:
		object(furniture,"WallLantern",15,Vector2(x,241),Vector2i(32,64))
	var foreground := layer("ForegroundWainscot")
	foreground.modulate = Color(0.83,0.78,0.69)
	paint(foreground,Rect2i(0,17,13,3),1,"a4_wall_side_p0_c6")
	paint(foreground,Rect2i(19,17,13,3),1,"a4_wall_side_p0_c6")
	var rail := group("ForegroundRail")
	for ends in [Vector2(0,624),Vector2(912,1536)]:
		polygon(rail,PackedVector2Array([Vector2(ends.x,806),Vector2(ends.y,806),Vector2(ends.y,824),Vector2(ends.x,824)]),Color(0.22,0.15,0.09))
		line(rail,PackedVector2Array([Vector2(ends.x,807),Vector2(ends.y,807)]),Color(0.54,0.39,0.22),3)
	for x in [160,544,992,1376]:
		var podium:=AtlasTexture.new()
		podium.atlas=load(ASSETS+"statue.res")
		podium.region=Rect2(0,128,96,64)
		var post:=sprite(rail,"ForecourtPost",podium,Vector2(x,856))
		post.scale.y=2.0
		var candle:=sprite(rail,"PostCandle",load(REVISION+"candelabrum.res"),Vector2(x,756))
		candle.scale=Vector2(0.55,0.55)
	var flags := group("IdentityBanners")
	banner(flags,"Academy",3,Vector2(768,280),Vector2(0.67,0.67))
	for x in [288,1032]:
		banner(flags,"RoyalScience",1,Vector2(x,106),Vector2(0.84,0.84))
	for x in [504,1248]:
		banner(flags,"MysticGuild",2,Vector2(x,106),Vector2(0.84,0.84))
	banner(flags,"RoyalAuthority",0,Vector2(120,85))
	banner(flags,"CountPatron",4,Vector2(1416,85))
	for x in [432,1104]:
		banner(flags,"AcademyEntrance",3,Vector2(x,870))
	var characters := group("Characters")
	for pos in [Vector2(768,813),Vector2(376,364)]:
		var shadow := PackedVector2Array()
		for i in 24:
			shadow.append(pos+Vector2(cos(i*TAU/24)*22,sin(i*TAU/24)*7))
		polygon(characters,shadow,Color(0,0,0,0.24))
	sprite(characters,"Detective",load(ASSETS+"detective_back.res"),Vector2(768,760))
	sprite(characters,"Musician",load(ASSETS+"musician_front.res"),Vector2(376,316))
	var light := group("MorningLight")
	# Architectural window projection, confined to the open floor.
	for col in 5:
		for row in 3:
			var start := Vector2(180,368)+Vector2(116,66)*col+Vector2(0,30)*row
			polygon(light,PackedVector2Array([start,start+Vector2(104,59),start+Vector2(104,82),start+Vector2(0,23)]),Color(1.0,0.86,0.60,0.20))
	var shade:=ColorRect.new()
	shade.name="PeripheralShade"
	shade.size=Vector2(1536,960)
	shade.mouse_filter=Control.MOUSE_FILTER_IGNORE
	var shade_material:=ShaderMaterial.new()
	shade_material.shader=load("res://scenes/Shot01/edge_shade.gdshader")
	shade.material=shade_material
	add(room,shade)
	var camera := Camera2D.new()
	camera.name="Camera2D"
	camera.position=Vector2(768,480)
	add(room,camera)
	var packed := PackedScene.new()
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
	report.append("Shot01 saved/reloaded: %d valid map cells; 32x20 cells; 48px; 1536x960 render"%count)
	report.append("9 architectural banners: five established emblems redrawn on subdued long cloth; static revision2 art")
	if "--capture" in OS.get_cmdline_user_args():
		var viewport:=SubViewport.new()
		viewport.size=Vector2i(1536,960)
		viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.add_child(room)
		await process_frame
		await process_frame
		await RenderingServer.frame_post_draw
		assert(viewport.get_texture().get_image().save_png(OUTPUT+"shot01.png")==OK)
		assert(room.get_node("IdentityBanners").get_child_count()==9)
		viewport.queue_free()
	else:
		room.free()
	var f:=FileAccess.open(OUTPUT+"validation.txt",FileAccess.WRITE)
	f.store_string("\n".join(report)+"\n")
	print("\n".join(report))
	quit()

func add(parent: Node, child: Node) -> void:
	parent.add_child(child)
	child.owner=room

func group(label: String) -> Node2D:
	var node:=Node2D.new()
	node.name=label
	add(room,node)
	return node

func layer(label: String) -> TileMapLayer:
	var node:=TileMapLayer.new()
	node.name=label
	node.tile_set=tileset
	add(room,node)
	return node

func paint(target: TileMapLayer, rect: Rect2i, set_index: int, label: String) -> void:
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

func sprite(parent:Node,label:String,texture:Texture2D,pos:Vector2) -> Sprite2D:
	if label in ["Bench","ScholarStatue","ResearchTable","PottedTree","PottedFern","Bookcase"]:
		var shadow:=Sprite2D.new()
		shadow.name=label+"Shadow"
		shadow.texture=texture
		shadow.position=pos+Vector2(10,texture.get_height()*0.44)
		shadow.scale=Vector2(0.95,0.18)
		shadow.modulate=Color(0.04,0.03,0.02,0.30)
		add(parent,shadow)
	var node:=Sprite2D.new()
	node.name=label
	node.texture=texture
	node.position=pos
	if label not in ["Detective","Musician"]:
		node.material=finish
	add(parent,node)
	return node

func object(parent:Node,label:String,index:int,pos:Vector2,size:Vector2i) -> void:
	var img:=Image.load_from_file("res://ShareAsset/rpg2d/tilesets/academy_v1/academy_objects_48.png").get_region(Rect2i((index%4)*192,(index/4)*192,192,192))
	img=img.get_region(img.get_used_rect())
	img.resize(size.x,size.y,Image.INTERPOLATE_NEAREST)
	sprite(parent,label,save_texture(img,ASSETS+label.to_snake_case()),pos)

func polygon(parent:Node,points:PackedVector2Array,color:Color) -> void:
	var node:=Polygon2D.new()
	node.polygon=points
	node.color=color
	add(parent,node)

func line(parent:Node,points:PackedVector2Array,color:Color,width:float) -> void:
	var node:=Line2D.new()
	node.points=points
	node.default_color=color
	node.width=width
	add(parent,node)

func banner(parent:Node,label:String,row:int,pos:Vector2,size_scale:Vector2=Vector2.ONE) -> void:
	var node:=sprite(parent,label,load(REVISION+"banner_%d.res"%row),pos)
	node.scale=size_scale

func prepare_revision() -> void:
	var source:=Image.load_from_file(REVISION+"hall_finish_generated.png")
	var rects:=[Rect2i(58,38,252,521),Rect2i(345,38,250,521),Rect2i(637,38,255,521),Rect2i(933,38,253,521),Rect2i(1228,38,252,521),Rect2i(18,632,299,337),Rect2i(333,553,241,415),Rect2i(570,632,425,339),Rect2i(1027,567,163,412),Rect2i(1236,725,279,233)]
	var names:=["banner_0","banner_1","banner_2","banner_3","banner_4","fern","tree","research_table","candelabrum","books"]
	var sizes:=[Vector2i(96,192),Vector2i(96,192),Vector2i(96,192),Vector2i(96,192),Vector2i(96,192),Vector2i(112,128),Vector2i(88,144),Vector2i(160,128),Vector2i(48,120),Vector2i(48,40)]
	for i in rects.size():
		var part:=source.get_region(rects[i])
		part=part.get_region(part.get_used_rect())
		part.resize(sizes[i].x,sizes[i].y,Image.INTERPOLATE_NEAREST)
		save_texture(part,REVISION+names[i])
