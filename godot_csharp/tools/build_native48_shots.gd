extends SceneTree

const OUT := "res://ShareAsset/rpg2d/native48/"
const SIZE := Vector2i(1248,768)
var tileset: TileSet
var sources := {}
var catalog := {}
var native_id: int
var room: Node2D

func _initialize() -> void:
	call_deferred("build")

func build() -> void:
	catalog=preload("res://tools/native48_art.gd").new().build()
	tileset=load("res://ShareAsset/victorian/tilesets/Victorian.tres").duplicate()
	for i in tileset.get_source_count():
		var id:=tileset.get_source_id(i)
		var atlas:=tileset.get_source(id) as TileSetAtlasSource
		if atlas: sources[atlas.texture.resource_path.get_file().get_basename()]=id
	var extension:=TileSetAtlasSource.new()
	extension.texture=load(OUT+"Academy_Native48.res")
	extension.texture_region_size=Vector2i(48,48)
	for y in 16:
		for x in 20: extension.create_tile(Vector2i(x,y))
	native_id=tileset.add_source(extension)
	assert(ResourceSaver.save(tileset,OUT+"AcademyNative48.tres")==OK)
	for shot in [1,2]:
		make_room(shot)
		var packed:=PackedScene.new()
		assert(packed.pack(room)==OK)
		var path:="res://scenes/Shot%02d/Shot%02d.tscn"%[shot,shot]
		assert(ResourceSaver.save(packed,path)==OK)
		room.free()
		room=load(path).instantiate()
		verify(room)
		var viewport:=SubViewport.new()
		viewport.size=SIZE
		viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.add_child(room)
		for frame in 5: await process_frame
		await RenderingServer.frame_post_draw
		assert(viewport.get_texture().get_image().save_png("res://../artifacts/native48-validation/shot-%02d.png"%shot)==OK)
		viewport.queue_free()
		await process_frame
	print("PASS: both saved scenes use 48px TileMapLayers; native coordinates; unit transforms; camera zoom 1; 1248x768 captures.")
	quit()

func layer(label:String) -> TileMapLayer:
	var result:=TileMapLayer.new()
	result.name=label
	result.tile_set=tileset
	room.add_child(result)
	result.owner=room
	return result

func stamp(target:TileMapLayer,sheet:String,area:Rect2i,pos:Vector2i) -> void:
	for y in area.size.y:
		for x in area.size.x:
			target.set_cell(pos+Vector2i(x,y),sources["Victorian_Inside_"+sheet],area.position+Vector2i(x,y))

func item(target:TileMapLayer,label:String,pos:Vector2i) -> void:
	var a:Array=catalog[label]
	for y in int(a[3]):
		for x in int(a[2]): target.set_cell(pos+Vector2i(x,y),native_id,Vector2i(a[0]+x,a[1]+y))

func paint(target:TileMapLayer,area:Rect2i,set_index:int,label:String) -> void:
	var cells:Array[Vector2i]=[]
	for y in range(area.position.y,area.end.y):
		for x in range(area.position.x,area.end.x): cells.append(Vector2i(x,y))
	for i in tileset.get_terrains_count(set_index):
		if tileset.get_terrain_name(set_index,i)==label:
			target.set_cells_terrain_connect(cells,set_index,i,false)
			return
	assert(false,label)

func table(target:TileMapLayer,pos:Vector2i,width:int) -> void:
	for y in 3:
		for x in width:
			var part:=0 if x==0 else (2 if x==width-1 else 1)
			target.set_cell(pos+Vector2i(x,y),native_id,Vector2i(3+part,4+y))

func make_room(shot:int) -> void:
	room=Node2D.new()
	room.name="Shot%02d"%shot
	room.set_script(load("res://scenes/Native48View.gd"))
	room.texture_filter=CanvasItem.TEXTURE_FILTER_NEAREST
	room.set_meta("native_tile_size",48)
	room.set_meta("reference","artifacts/theme-film/scenes/shot-%02d.png"%shot)
	var floor_layer:=layer("Floor")
	for y in 16:
		for x in 26: item(floor_layer,"floor_%d"%(1 if (x+y)%3==0 else 0),Vector2i(x,y))
	var walls:=layer("Walls")
	paint(walls,Rect2i(0,0,26,4),1,"a4_wall_side_p0_c5")
	paint(walls,Rect2i(0,3,26,2),1,"a4_wall_side_p0_c6")
	var rugs:=layer("Rugs")
	rugs.modulate=Color(0.48,0.62,0.72)
	if shot==1:
		paint(rugs,Rect2i(11,13,4,3),0,"a2_floor_r2_c2")
	else:
		paint(rugs,Rect2i(7,8,12,5),0,"a2_floor_r2_c2")
	var architecture:=layer("Architecture")
	var furniture:=layer("Furniture")
	var actors:=layer("Characters")
	var tops:=layer("Tabletops")
	var objects:=layer("Objects")
	if shot==1:
		for x in [3,7,16,20]: item(architecture,"portal",Vector2i(x,1))
		item(architecture,"clock",Vector2i(11,0))
		var banners:=layer("Banners")
		for i in 4: item(banners,"flag_%d"%i,Vector2i([3,7,17,21][i],0))
		item(banners,"flag_4",Vector2i(0,1))
		item(banners,"flag_3",Vector2i(24,1))
		item(furniture,"seal",Vector2i(12,9))
		for x in [1,22]:
			stamp(furniture,"C",Rect2i(8,10,2,2),Vector2i(x,5))
			stamp(furniture,"B",Rect2i(9,4,3,1),Vector2i(x,8))
			stamp(furniture,"B",Rect2i(4,7,2,1),Vector2i(x,11))
			stamp(objects,"C",Rect2i(3,3,1,1),Vector2i(x,11))
		for x in [6,19]:
			stamp(furniture,"D",Rect2i(8,3,1,2),Vector2i(x,11))
		item(actors,"actor_0",Vector2i(12,12))
		item(actors,"actor_4",Vector2i(6,5))
	else:
		for y in [0,2]: stamp(architecture,"B",Rect2i(3,0,2,2),Vector2i(0,y))
		stamp(architecture,"C",Rect2i(8,12,3,4),Vector2i(22,0))
		for x in [10,13]: stamp(architecture,"B",Rect2i(8,0,3,2),Vector2i(x,1))
		for x in [11,14]: item(layer("Hearth%d"%x),"fire",Vector2i(x,2))
		for x in [5,19]: stamp(architecture,"C",Rect2i(8,10,2,2),Vector2i(x,1))
		for x in [10,13]: item(furniture,"counter",Vector2i(x,4))
		for x in [10,14]: stamp(objects,"C",Rect2i(2,6,2,1),Vector2i(x,4))
		item(actors,"actor_3",Vector2i(12,7))
		item(actors,"actor_1",Vector2i(9,8))
		item(actors,"actor_2",Vector2i(11,8))
		item(actors,"actor_5",Vector2i(16,8))
		table(tops,Vector2i(8,9),10)
		for x in [8,10,12,14,16]: stamp(objects,"C",Rect2i(3,3,1,1),Vector2i(x,9))
		for x in [9,13,15]: stamp(objects,"C",Rect2i(4,1,1,1),Vector2i(x,10))
		var chairs:=layer("FrontChairs")
		for x in [8,10,12,14,16]: stamp(chairs,"B",Rect2i(11,5,1,1),Vector2i(x,12))
		for x in [2,22]:
			for y in [8,12]:
				stamp(furniture,"B",Rect2i(4,7,2,1),Vector2i(x,y))
				stamp(objects,"C",Rect2i(3,3,1,1),Vector2i(x,y))
	for x in [0,24]:
		for y in [5,13]: stamp(objects,"C",Rect2i(0,7,1,2),Vector2i(x,y))
	var foreground:=layer("Foreground")
	paint(foreground,Rect2i(0,15,10,1),1,"a4_wall_side_p0_c6")
	paint(foreground,Rect2i(16,15,10,1),1,"a4_wall_side_p0_c6")
	var camera:=Camera2D.new()
	camera.name="Camera2D"
	camera.position=Vector2(SIZE)/2
	room.add_child(camera)
	camera.owner=room

func verify(node:Node) -> void:
	assert(not node is Sprite2D and not node is TextureRect)
	if node is Node2D:
		assert(node.scale==Vector2.ONE)
		assert(node.position==node.position.round())
	if node is Camera2D: assert(node.zoom==Vector2.ONE)
	if node is TileMapLayer:
		assert(node.tile_set.tile_size==Vector2i(48,48))
		for cell in node.get_used_cells():
			var source:=node.tile_set.get_source(node.get_cell_source_id(cell)) as TileSetAtlasSource
			assert(source.texture_region_size==Vector2i(48,48))
			var path:=source.texture.resource_path
			assert(path.begins_with("res://ShareAsset/victorian/") or path==OUT+"Academy_Native48.res",path)
	for child in node.get_children(): verify(child)
