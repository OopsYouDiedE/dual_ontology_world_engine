extends SceneTree
## Legacy Shot01 asset extraction only. This script never writes a scene.

const ASSETS := "res://ShareAsset/Internal/Props/"
const CENTRAL_SOURCE := "res://AssetsSource/Internal/Props/central_hall_generated.png"
const FINISH_SOURCE := "res://AssetsSource/Internal/Props/hall_finish_generated.png"
const CENTRAL_NAMES := ["clock", "portal", "window", "statue", "compass", "bench"]
const CENTRAL_REGIONS := [
	Rect2i(0, 0, 524, 561), Rect2i(537, 0, 399, 557), Rect2i(969, 0, 243, 551),
	Rect2i(1267, 49, 227, 518), Rect2i(9, 563, 491, 414), Rect2i(521, 719, 431, 192),
]
const CENTRAL_SIZES := [
	Vector2i(224, 224), Vector2i(128, 192), Vector2i(64, 192),
	Vector2i(64, 128), Vector2i(192, 192), Vector2i(128, 48),
]
const FINISH_NAMES := [
	"shot01_banner_0", "shot01_banner_1", "shot01_banner_2", "shot01_banner_3", "shot01_banner_4",
	"fern", "tree", "research_table", "candelabrum", "books",
]
const FINISH_REGIONS := [
	Rect2i(58, 38, 252, 521), Rect2i(345, 38, 250, 521), Rect2i(637, 38, 255, 521),
	Rect2i(933, 38, 253, 521), Rect2i(1228, 38, 252, 521), Rect2i(18, 632, 299, 337),
	Rect2i(333, 553, 241, 415), Rect2i(570, 632, 425, 339), Rect2i(1027, 567, 163, 412),
	Rect2i(1236, 725, 279, 233),
]
const FINISH_SIZES := [
	Vector2i(64, 128), Vector2i(64, 128), Vector2i(64, 128), Vector2i(64, 128), Vector2i(64, 128),
	Vector2i(75, 85), Vector2i(59, 96), Vector2i(107, 85), Vector2i(32, 80), Vector2i(32, 27),
]

func _initialize() -> void:
	call_deferred("build_assets")

func build_assets() -> void:
	var central := load_image(CENTRAL_SOURCE)
	for i in CENTRAL_NAMES.size():
		extract(central, CENTRAL_NAMES[i], CENTRAL_REGIONS[i], CENTRAL_SIZES[i])
	var finish := load_image(FINISH_SOURCE)
	for i in FINISH_NAMES.size():
		extract(finish, FINISH_NAMES[i], FINISH_REGIONS[i], FINISH_SIZES[i])
	print("Shot01 source-derived prop textures rebuilt. No scene was written.")
	quit()

func load_image(resource_path: String) -> Image:
	var image := Image.load_from_file(ProjectSettings.globalize_path(resource_path))
	assert(image != null and not image.is_empty(), "Could not load source image: " + resource_path)
	image.convert(Image.FORMAT_RGBA8)
	return image

func extract(source: Image, name: String, region: Rect2i, target: Vector2i) -> void:
	assert(Rect2i(Vector2i.ZERO, source.get_size()).encloses(region), "Crop outside source for " + name)
	var image := source.get_region(region)
	var used := image.get_used_rect()
	assert(used.has_area(), "Empty crop for " + name)
	image = image.get_region(used)
	fit32(image, target.x, target.y)
	save_texture(image, name)

func fit32(image: Image, width: int, height: int) -> void:
	var ratio := minf(float(width) / image.get_width(), float(height) / image.get_height())
	var content := Vector2i(Vector2(image.get_size()) * ratio)
	image.resize(maxi(1, content.x), maxi(1, content.y), Image.INTERPOLATE_LANCZOS)
	var box := Vector2i(ceili(width / 32.0) * 32, ceili(height / 32.0) * 32)
	var padded := Image.create(box.x, box.y, false, Image.FORMAT_RGBA8)
	padded.fill(Color.TRANSPARENT)
	padded.blit_rect(image, Rect2i(Vector2i.ZERO, image.get_size()), (box - image.get_size()) / 2)
	image.copy_from(padded)

func save_texture(image: Image, name: String) -> void:
	var png_path := ASSETS + name + ".png"
	var res_path := ASSETS + name + ".res"
	assert(image.save_png(ProjectSettings.globalize_path(png_path)) == OK, "Could not save " + png_path)
	var texture := ImageTexture.create_from_image(image)
	texture.take_over_path(res_path)
	assert(ResourceSaver.save(texture, res_path, ResourceSaver.FLAG_COMPRESS) == OK, "Could not save " + res_path)
