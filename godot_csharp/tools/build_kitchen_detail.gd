extends SceneTree

const OUT := "res://../artifacts/kitchen-detail/"
var tiles:TileSet
var sources:Dictionary={}
var room:Node2D

func _initialize() -> void:
	call_deferred("build")

func layer(label:String) -> TileMapLayer:
	var result:=TileMapLayer.new()
	result.name=label
	result.tile_set=tiles
	room.add_child(result)
	result.owner=room
	return result

func stamp(target:TileMapLayer,sheet:String,area:Rect2i,pos:Vector2i) -> void:
	for y in area.size.y:
		for x in area.size.x:
			target.set_cell(pos+Vector2i(x,y),sources["Victorian_Inside_"+sheet],area.position+Vector2i(x,y))

func terrain(target:TileMapLayer,area:Rect2i,set_id:int,label:String) -> void:
	var cells:Array[Vector2i]=[]
	for y in range(area.position.y,area.end.y):
		for x in range(area.position.x,area.end.x): cells.append(Vector2i(x,y))
	for i in tiles.get_terrains_count(set_id):
		if tiles.get_terrain_name(set_id,i)==label:
			target.set_cells_terrain_connect(cells,set_id,i,false)
			return
	assert(false,label)

func build() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	tiles=load("res://ShareAsset/Victorian/tilesets/Victorian.tres")
	for i in tiles.get_source_count():
		var id:=tiles.get_source_id(i)
		var source:=tiles.get_source(id) as TileSetAtlasSource
		if source: sources[source.texture.resource_path.get_file().get_basename()]=id
	room=Node2D.new()
	room.name="KitchenDetail48"
	room.texture_filter=CanvasItem.TEXTURE_FILTER_NEAREST
	terrain(layer("StoneFloor"),Rect2i(0,0,10,8),0,"a2_floor_r3_c3")
	terrain(layer("StoneWall"),Rect2i(0,0,10,3),1,"a4_wall_side_p0_c5")
	var back:=layer("OriginalKitchenFurniture")
	stamp(back,"B",Rect2i(8,12,3,2),Vector2i(2,1))
	stamp(back,"B",Rect2i(14,12,2,2),Vector2i(6,1))
	stamp(back,"B",Rect2i(3,0,2,2),Vector2i(0,0))
	stamp(back,"C",Rect2i(0,7,1,2),Vector2i(0,2))
	stamp(back,"C",Rect2i(1,7,3,1),Vector2i(6,0))
	# Original continuous six-cell counter. Every source texel remains unchanged.
	stamp(layer("OriginalCounter"),"B",Rect2i(10,14,6,2),Vector2i(2,4))
	var props:=layer("FoodAndCookware")
	# Integer pixel placement on the countertop; still original-size 48px cells.
	props.position.y=-12
	stamp(props,"C",Rect2i(4,1,1,1),Vector2i(2,4))
	stamp(props,"C",Rect2i(6,2,1,1),Vector2i(3,4))
	stamp(props,"C",Rect2i(4,7,1,1),Vector2i(5,4))
	stamp(props,"C",Rect2i(3,6,1,1),Vector2i(7,4))
	stamp(layer("FloorPlant"),"C",Rect2i(6,9,1,1),Vector2i(8,5))
	var packed:=PackedScene.new()
	assert(packed.pack(room)==OK)
	assert(ResourceSaver.save(packed,"res://scenes/KitchenDetail48.tscn")==OK)
	var count:=0
	for child in room.get_children():
		assert(child is TileMapLayer and child.scale==Vector2.ONE)
		assert(child.tile_set.tile_size==Vector2i(48,48))
		count+=child.get_used_cells().size()
	var viewport:=SubViewport.new()
	viewport.size=Vector2i(480,384)
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	viewport.add_child(room)
	for frame in 5: await process_frame
	await RenderingServer.frame_post_draw
	assert(viewport.get_texture().get_image().save_png(OUT+"kitchen-native48.png")==OK)
	var f:=FileAccess.open(OUT+"verification.txt",FileAccess.WRITE)
	f.store_string("480x384 native render. %d cells. All layers: original Victorian TileSet, 48x48 regions, unit scale. No custom drawn substitutes, no resampling, no camera.\n"%count)
	print("PASS: original native48 kitchen detail, ",count," cells")
	quit()
