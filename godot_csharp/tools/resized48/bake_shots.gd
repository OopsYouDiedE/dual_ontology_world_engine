extends SceneTree

const OUT := "res://ShareAsset/rpg2d/resized48_shots/"
var room:Node2D
var tiles:TileSet
var shot:int
var serial:int

func _initialize() -> void:
	call_deferred("build")

func bake(node:Node) -> void:
	for child in node.get_children(): bake(child)
	if not node is Sprite2D: return
	var sprite:=node as Sprite2D
	var im:=sprite.texture.get_image()
	var size:=Vector2i((Vector2(im.get_size())*sprite.scale).round())
	size.x=maxi(1,size.x)
	size.y=maxi(1,size.y)
	if size!=im.get_size(): im.resize(size.x,size.y,Image.INTERPOLATE_LANCZOS)
	var top_left:Vector2=(sprite.position-Vector2(size)/2).round()
	var origin:Vector2=(top_left/48).floor()*48
	var offset:=Vector2i(top_left-origin)
	var extent:=size+offset
	var box:=Vector2i(ceili(extent.x/48.0)*48,ceili(extent.y/48.0)*48)
	var image:=Image.create(box.x,box.y,false,Image.FORMAT_RGBA8)
	image.fill(Color.TRANSPARENT)
	image.blit_rect(im,Rect2i(Vector2i.ZERO,size),offset)
	var path:=OUT+"shot%02d_%03d"%[shot,serial]
	serial+=1
	assert(image.save_png(path+".png")==OK)
	var texture:=ImageTexture.create_from_image(image)
	assert(ResourceSaver.save(texture,path+".res")==OK)
	var source:=TileSetAtlasSource.new()
	source.texture=texture
	source.texture_region_size=Vector2i(48,48)
	for y in box.y/48:
		for x in box.x/48: source.create_tile(Vector2i(x,y))
	var id:=tiles.add_source(source)
	var map:=TileMapLayer.new()
	map.name=sprite.name
	map.tile_set=tiles
	map.modulate=sprite.modulate
	map.material=sprite.material
	map.z_index=sprite.z_index
	for y in box.y/48:
		for x in box.x/48: map.set_cell(Vector2i(origin/48)+Vector2i(x,y),id,Vector2i(x,y))
	var parent:=sprite.get_parent()
	var index:=sprite.get_index()
	parent.remove_child(sprite)
	parent.add_child(map)
	parent.move_child(map,index)
	map.owner=room
	sprite.free()

func verify(node:Node) -> void:
	assert(not node is Sprite2D)
	assert(not node is Polygon2D)
	assert(not node is Line2D)
	if node is Node2D: assert(node.scale==Vector2.ONE)
	if node is Camera2D: assert(node.zoom==Vector2.ONE)
	if node is TileMapLayer:
		assert(node.tile_set.tile_size==Vector2i(48,48))
		for pos in node.get_used_cells(): assert(node.get_cell_tile_data(pos)!=null)
	for child in node.get_children(): verify(child)

func build() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("res://../artifacts/resized48-shots/"))
	for number in ([2] if "--shot02" in OS.get_cmdline_user_args() else [1,2]):
		shot=number
		serial=0
		var path:="res://scenes/Shot%02d/Shot%02d.tscn"%[shot,shot]
		room=load(path).instantiate()
		tiles=TileSet.new()
		tiles.tile_size=Vector2i(48,48)
		bake(room)
		assert(ResourceSaver.save(tiles,OUT+"Shot%02d.tres"%shot)==OK)
		room.set_meta("art_method","Generated original art reduced to 48px-multiple canvases; composed as TileMapLayers at unit scale")
		var packed:=PackedScene.new()
		assert(packed.pack(room)==OK)
		assert(ResourceSaver.save(packed, path)==OK)
		room.free()
		room=ResourceLoader.load(path,"PackedScene",ResourceLoader.CACHE_MODE_IGNORE).instantiate()
		verify(room)
		var viewport:=SubViewport.new()
		viewport.size=Vector2i(1536,960 if shot==1 else 912)
		viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewport.add_child(room)
		for frame in 5: await process_frame
		await RenderingServer.frame_post_draw
		assert(viewport.get_texture().get_image().save_png("res://../artifacts/resized48-shots/shot-%02d.png"%shot)==OK)
		print("PASS Shot%02d: %d object atlases; 48px cells; all node scales and camera zoom are 1; saved scene reloaded and rendered."%[shot,serial])
		viewport.queue_free()
		await process_frame
	quit()
