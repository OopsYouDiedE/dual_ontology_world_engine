extends SceneTree

const OUT := "res://ShareAsset/rpg2d/resized48_detail/"
const PREVIEW := "res://../artifacts/resized48-detail/"

func _initialize() -> void:
	call_deferred("build")

func build() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(PREVIEW))
	var original:=Image.load_from_file("res://ShareAsset/rpg2d/shot02/source/kitchen_generated.png")
	var atlas:=Image.create(768,576,false,Image.FORMAT_RGBA8)
	atlas.fill(Color.TRANSPARENT)
	var specs:=[
		["hearth",Rect2i(61,0,1004,450),Vector2i(576,288),Vector2i(0,0)],
		["prep_island",Rect2i(25,742,773,253),Vector2i(480,192),Vector2i(0,336)],
		["pantry",Rect2i(1175,0,274,474),Vector2i(144,288),Vector2i(624,0)]
	]
	var records:=[]
	for spec in specs:
		var crop:=original.get_region(spec[1])
		if spec[0]=="prep_island":
			# Exclude the neighboring dining-table foot in the source crop's top gutter.
			crop.fill_rect(Rect2i(100,0,673,18),Color.TRANSPARENT)
		var box:Vector2i=spec[2]
		# One uniform reduction preserves the original object's proportions.
		# Transparent padding aligns the result to the 48px tile grid.
		var ratio:=minf(float(box.x)/crop.get_width(),float(box.y)/crop.get_height())
		var size:=Vector2i(Vector2(crop.get_size())*ratio)
		crop.resize(size.x,size.y,Image.INTERPOLATE_LANCZOS)
		var part:=Image.create(box.x,box.y,false,Image.FORMAT_RGBA8)
		part.fill(Color.TRANSPARENT)
		var offset:=Vector2i((box.x-size.x)/2,box.y-size.y)
		part.blit_rect(crop,Rect2i(Vector2i.ZERO,size),offset)
		assert(part.save_png(OUT+spec[0]+".png")==OK)
		atlas.blit_rect(part,Rect2i(Vector2i.ZERO,box),spec[3])
		records.append({"name":spec[0],"source_rect":[spec[1].position.x,spec[1].position.y,spec[1].size.x,spec[1].size.y],"size":[box.x,box.y],"content_size":[size.x,size.y],"atlas_cell":[spec[3].x/48,spec[3].y/48]})
	assert(atlas.save_png(OUT+"KitchenDetail48.png")==OK)
	var texture:=ImageTexture.create_from_image(atlas)
	assert(ResourceSaver.save(texture,OUT+"KitchenDetail48.res")==OK)
	var tiles:=TileSet.new()
	tiles.tile_size=Vector2i(48,48)
	var source:=TileSetAtlasSource.new()
	source.texture=texture
	source.texture_region_size=Vector2i(48,48)
	for y in 12:
		for x in 16: source.create_tile(Vector2i(x,y))
	var id:=tiles.add_source(source)
	assert(ResourceSaver.save(tiles,OUT+"KitchenDetail48.tres")==OK)
	var room:=Node2D.new()
	room.name="Resized48Detail"
	room.texture_filter=CanvasItem.TEXTURE_FILTER_NEAREST
	var map:=TileMapLayer.new()
	map.name="OriginalArtTiles"
	map.tile_set=tiles
	room.add_child(map)
	map.owner=room
	for y in 12:
		for x in 16: map.set_cell(Vector2i(x,y),id,Vector2i(x,y))
	var scene:=PackedScene.new()
	assert(scene.pack(room)==OK)
	assert(ResourceSaver.save(scene,"res://scenes/Resized48Detail.tscn")==OK)
	var viewport:=SubViewport.new()
	viewport.size=Vector2i(768,576)
	viewport.render_target_update_mode=SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	viewport.add_child(room)
	for i in 5: await process_frame
	await RenderingServer.frame_post_draw
	assert(viewport.get_texture().get_image().save_png(PREVIEW+"detail.png")==OK)
	var manifest:=FileAccess.open(OUT+"provenance.json",FileAccess.WRITE)
	manifest.store_string(JSON.stringify({"source":"shot02/source/kitchen_generated.png","method":"User-authorized uniform Lanczos reduction; transparent padding to 48px multiples. These are resized generated assets, not originally authored native48 art.","objects":records},"\t"))
	print("PASS: 48px atlas; unit TileMapLayer transform; original proportions preserved; generated source resized once.")
	quit()
