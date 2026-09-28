extends SceneTree
## Legacy Shot02 asset extraction only. This script never writes a scene.

const ASSETS := "res://ShareAsset/Internal/Props/"
const KITCHEN_SOURCE := "res://AssetsSource/Internal/Props/kitchen_generated.png"
const JOINERY_SOURCE := "res://AssetsSource/Internal/Props/joinery_generated.png"
const OBJECTS_SOURCE := "res://AssetsSource/Internal/Props/AcademyTileMap/academy_objects_48.png"
const IDENTITY_SOURCE := "res://AssetsSource/Internal/Identity/five_emblems_48.png"

func _initialize() -> void:
	call_deferred("build_assets")

func build_assets() -> void:
	var kitchen := load_image(KITCHEN_SOURCE)
	extract(kitchen, "hearth", Rect2i(61, 0, 1004, 450), Vector2i(384, 168))
	extract(kitchen, "pantry", Rect2i(1175, 0, 274, 474), Vector2i(96, 168))
	extract(kitchen, "breakfast_table", Rect2i(87, 470, 1359, 278), Vector2i(448, 128))
	extract(kitchen, "prep_island", Rect2i(25, 742, 773, 253), Vector2i(352, 112))
	extract(kitchen, "side_table", Rect2i(810, 788, 371, 204), Vector2i(160, 85))
	extract(kitchen, "chair", Rect2i(1192, 780, 129, 210), Vector2i(32, 64))
	extract(kitchen, "bread_barrel", Rect2i(1343, 779, 164, 215), Vector2i(48, 64))

	var objects := load_image(OBJECTS_SOURCE)
	extract(objects, "kitchen_wall_lantern", Rect2i(576, 576, 192, 192), Vector2i(16, 37))

	var joinery := load_image(JOINERY_SOURCE)
	extract(joinery, "closed_door", Rect2i(142, 18, 581, 668), Vector2i(128, 168))
	extract(joinery, "supplies", Rect2i(860, 241, 630, 458), Vector2i(75, 64))
	extract(joinery, "front_gate", Rect2i(7, 722, 920, 253), Vector2i(576, 128))
	extract(joinery, "front_panel", Rect2i(88, 737, 173, 227), Vector2i(96, 128))
	extract(joinery, "bread_plate", Rect2i(969, 707, 550, 287), Vector2i(59, 32))

	var identities := load_image(IDENTITY_SOURCE)
	var emblem := identities.get_region(Rect2i(144, 0, 48, 48))
	emblem.resize(32, 32, Image.INTERPOLATE_LANCZOS)
	save_texture(emblem, "academy_emblem")
	print("Shot02 source-derived prop textures rebuilt. No scene was written.")
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
