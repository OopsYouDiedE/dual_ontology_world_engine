extends SceneTree
## Deterministically rebuilds the story room's map props from the consolidated source and banner assets.

const OUT := "res://ShareAsset/Internal/Props/"
const ACADEMY_SOURCE := "res://AssetsSource/Internal/Props/AcademyTileMap/Academy_Inside_A2.png"
const COMPACT_STORY_PROPS := {
	"clock": Vector2i(96, 128),
	"portal": Vector2i(64, 128),
	"window": Vector2i(32, 128),
	"compass": Vector2i(128, 128),
	"bench": Vector2i(80, 48),
	"statue": Vector2i(40, 64),
	"tree": Vector2i(32, 64),
	"hearth": Vector2i(224, 128),
	"pantry": Vector2i(64, 96),
	"breakfast_table": Vector2i(224, 64),
	"prep_island": Vector2i(192, 64),
	"closed_door": Vector2i(64, 96),
	"bookcase": Vector2i(64, 96),
	"side_table": Vector2i(64, 64),
}

# STORY-HARDCODE[CSH-030][schema][tool]: 资产工具固定生成项目故事场景道具（钟、传送门、罗盘、早餐桌、书柜、客厅钢琴及横幅等）。
func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	if "--breakfast-exchange-only" in OS.get_cmdline_user_args():
		preload("res://tools/build_breakfast_exchange.gd").build()
		quit()
		return
	if "--parlor-only" in OS.get_cmdline_user_args():
		build_parlor_furniture()
		quit()
		return
	if "--breakfast-only" in OS.get_cmdline_user_args():
		build_compact_story_props(["breakfast_table", "prep_island", "closed_door", "side_table"])
		build_breakfast_bowl()
		preload("res://tools/build_breakfast_exchange.gd").build()
		quit()
		return
	if "--classroom-only" in OS.get_cmdline_user_args():
		build_classroom_props()
		quit()
		return
	build_classroom_props()
	build_parlor_furniture()
	build_compact_story_props()
	build_breakfast_bowl()
	preload("res://tools/build_breakfast_exchange.gd").build()
	var compact_only := "--compact-only" in OS.get_cmdline_user_args()
	if compact_only:
		quit()
		return
	var source := Image.load_from_file(ProjectSettings.globalize_path(ACADEMY_SOURCE))
	assert(source != null and not source.is_empty(), "Could not load Academy A2 source atlas")
	# Two by two 48px source slabs; preserve their seams in a 32px-map derivative.
	var slab := source.get_region(Rect2i(288, 432, 96, 96))
	slab.resize(64, 64, Image.INTERPOLATE_NEAREST)
	save(slab, "stone_slabs")
	for banner in [1, 2, 3]:
		var cloth := Image.load_from_file(ProjectSettings.globalize_path("res://ShareAsset/Internal/Props/shot01_banner_%d.png" % banner))
		assert(cloth != null and not cloth.is_empty(), "Could not load shot01 banner %d" % banner)
		cloth.resize(32, 64, Image.INTERPOLATE_NEAREST)
		save(cloth, "story_banner_%d" % banner)
	quit()

func build_compact_story_props(only: Array[String] = []) -> void:
	for asset_key in COMPACT_STORY_PROPS:
		var asset_name: String = str(asset_key)
		if not only.is_empty() and not asset_name in only:
			continue
		var source_path: String = OUT + asset_name + ".png"
		var source := Image.load_from_file(ProjectSettings.globalize_path(source_path))
		assert(source != null and not source.is_empty(), "Could not load Story prop source: " + source_path)
		var target_size: Vector2i = COMPACT_STORY_PROPS[asset_name]
		var scale := minf(float(target_size.x) / source.get_width(), float(target_size.y) / source.get_height())
		var fitted_size := Vector2i(
			maxi(1, mini(target_size.x, roundi(source.get_width() * scale))),
			maxi(1, mini(target_size.y, roundi(source.get_height() * scale))))
		var fitted := source.duplicate()
		fitted.resize(fitted_size.x, fitted_size.y, Image.INTERPOLATE_LANCZOS)
		var canvas := Image.create(target_size.x, target_size.y, false, Image.FORMAT_RGBA8)
		canvas.fill(Color(0, 0, 0, 0))
		var destination := Vector2i((target_size.x - fitted_size.x) / 2, target_size.y - fitted_size.y)
		canvas.blit_rect(fitted, Rect2i(Vector2i.ZERO, fitted_size), destination)
		var output_name: String = "story_" + asset_name
		assert(canvas.save_png(ProjectSettings.globalize_path(OUT + output_name + ".png")) == OK,
			"Could not save Story prop PNG: " + output_name)
		assert(ResourceSaver.save(ImageTexture.create_from_image(canvas), OUT + output_name + ".res") == OK,
			"Could not save Story prop resource: " + output_name)
		print("Built ", output_name, " from ", asset_name, ".png: ", source.get_width(), "x", source.get_height(),
			" -> ", target_size.x, "x", target_size.y, " (Lanczos, aspect-fit, transparent canvas)")

func save(image: Image, name: String) -> void:
	assert(image.save_png(ProjectSettings.globalize_path(OUT + name + ".png")) == OK)
	assert(ResourceSaver.save(ImageTexture.create_from_image(image), OUT + name + ".res") == OK)

func build_breakfast_bowl() -> void:
	var source := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Props/breakfast_soup_bowl_source.png"))
	assert(source != null and not source.is_empty(), "Missing generated breakfast soup bowl source")
	var bounds := Rect2i()
	var found := false
	for y in source.get_height():
		for x in source.get_width():
			if source.get_pixel(x, y).a > 0.5:
				var pixel := Rect2i(x, y, 1, 1)
				bounds = bounds.merge(pixel) if found else pixel
				found = true
	assert(found, "Soup bowl source has no opaque pixels")
	var bowl := source.get_region(bounds)
	var factor := minf(18.0 / bowl.get_width(), 14.0 / bowl.get_height())
	bowl.resize(roundi(bowl.get_width() * factor), roundi(bowl.get_height() * factor), Image.INTERPOLATE_LANCZOS)
	var canvas := Image.create(32, 32, false, Image.FORMAT_RGBA8)
	canvas.fill(Color.TRANSPARENT)
	canvas.blit_rect(bowl, Rect2i(Vector2i.ZERO, bowl.get_size()), Vector2i((32 - bowl.get_width()) / 2, (32 - bowl.get_height()) / 2))
	save(canvas, "story_soup_bowl")

func build_parlor_furniture() -> void:
	var source := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Props/story_parlor_projection.png"))
	assert(source != null and not source.is_empty())
	var cell := Vector2i(source.get_width() / 3, source.get_height() / 2)
	var specs := [["sofa",Vector2i(0,0),Vector2i(96,64)], ["armchair",Vector2i(1,0),Vector2i(48,56)], ["tea_table",Vector2i(2,0),Vector2i(64,40)], ["fireplace",Vector2i(0,1),Vector2i(80,80)], ["parlor_bookcase",Vector2i(1,1),Vector2i(48,80)], ["parlor_piano",Vector2i(2,1),Vector2i(80,64)]]
	for spec in specs:
		var crop := Rect2i(spec[1]*cell,cell)
		if spec[0] == "sofa": crop = Rect2i(100,120,445,285)
		elif spec[0] == "armchair": crop = Rect2i(650,120,250,285)
		var quadrant := source.get_region(crop)
		# Generated alpha can contain near-transparent noise beyond the silhouette.
		var bounds := Rect2i()
		var found := false
		for y in quadrant.get_height():
			for x in quadrant.get_width():
				if quadrant.get_pixel(x,y).a > 0.75:
					var pixel := Rect2i(x,y,1,1)
					bounds = bounds.merge(pixel) if found else pixel
					found = true
		assert(found, "Missing furniture silhouette")
		var sprite := quadrant.get_region(bounds)
		var size: Vector2i = spec[2]
		var factor := minf(float(size.x)/sprite.get_width(),float(size.y)/sprite.get_height())
		sprite.resize(maxi(1,roundi(sprite.get_width()*factor)),maxi(1,roundi(sprite.get_height()*factor)),Image.INTERPOLATE_NEAREST)
		for y in sprite.get_height():
			for x in sprite.get_width():
				var color := sprite.get_pixel(x,y)
				color.a = 1.0 if color.a > 0.75 else 0.0
				sprite.set_pixel(x,y,color)
		var canvas := Image.create_empty(size.x,size.y,false,Image.FORMAT_RGBA8)
		canvas.blit_rect(sprite,Rect2i(Vector2i.ZERO,sprite.get_size()),Vector2i((size.x-sprite.get_width())/2,size.y-sprite.get_height()))
		save(canvas,"story_"+spec[0])

func build_classroom_props() -> void:
	var source := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Props/story_classroom_furniture.png"))
	assert(source != null and not source.is_empty(), "Missing classroom furniture source")
	# Source objects have independent alpha, but the blackboard crosses its notional sheet quadrant.
	var specs := [
		["classroom_board", Rect2i(0, 0, 800, 627), Vector2i(128, 64), Vector2i(112, 64), true],
		["classroom_lectern", Rect2i(800, 350, 454, 277), Vector2i(96, 64), Vector2i(64, 36), false],
		["classroom_desk", Rect2i(0, 627, 627, 627), Vector2i(64, 64), Vector2i(60, 40), false],
		["classroom_chair", Rect2i(627, 627, 627, 627), Vector2i(32, 64), Vector2i(30, 48), true],
	]
	for spec in specs:
		var region: Rect2i = spec[1]
		var part := source.get_region(region)
		var bounds := Rect2i()
		var found := false
		for y in part.get_height():
			for x in part.get_width():
				if part.get_pixel(x, y).a > 0.5:
					var pixel := Rect2i(x, y, 1, 1)
					bounds = bounds.merge(pixel) if found else pixel
					found = true
		assert(found, "Missing classroom prop silhouette: " + str(spec[0]))
		var sprite := part.get_region(bounds)
		var size: Vector2i = spec[2]
		var content: Vector2i = spec[3]
		if spec[4]:
			var factor := minf(float(content.x) / sprite.get_width(), float(content.y) / sprite.get_height())
			sprite.resize(maxi(1, roundi(sprite.get_width() * factor)), maxi(1, roundi(sprite.get_height() * factor)), Image.INTERPOLATE_LANCZOS)
		else:
			sprite.resize(content.x, content.y, Image.INTERPOLATE_LANCZOS)
		var canvas := Image.create_empty(size.x, size.y, false, Image.FORMAT_RGBA8)
		canvas.blit_rect(sprite, Rect2i(Vector2i.ZERO, sprite.get_size()), Vector2i((size.x - sprite.get_width()) / 2, size.y - sprite.get_height()))
		save(canvas, str(spec[0]))

