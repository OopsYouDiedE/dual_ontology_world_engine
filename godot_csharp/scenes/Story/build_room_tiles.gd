extends SceneTree
## Explicit source conversion, not native pixel painting. Never changes source slicing.
const OUT := "res://ShareAsset/Images/story_hall/"

func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(OUT)
	var source := Image.load_from_file("res://ShareAsset/TileMap/Academy/Academy_Inside_A2.png")
	# Two by two 48px source slabs; preserve their seams in a 32px-map derivative.
	var slab := source.get_region(Rect2i(288, 432, 96, 96))
	slab.resize(64, 64, Image.INTERPOLATE_NEAREST)
	save(slab, "stone_slabs")
	for banner in [1, 2, 3]:
		var cloth := Image.load_from_file("res://ShareAsset/Images/shot01/revision2/banner_%d.png" % banner)
		cloth.resize(32, 64, Image.INTERPOLATE_NEAREST)
		save(cloth, "banner_%d" % banner)
	quit()

func save(image: Image, name: String) -> void:
	assert(image.save_png(OUT + name + ".png") == OK)
	assert(ResourceSaver.save(ImageTexture.create_from_image(image), OUT + name + ".res") == OK)
