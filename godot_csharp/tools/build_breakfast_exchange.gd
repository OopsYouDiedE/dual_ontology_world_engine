extends RefCounted
## Imports the approved generated source; no runtime reads from AssetsSource.
const SOURCE := "res://AssetsSource/Internal/Characters/DemoAnimation/breakfast_exchange.png"
const OUT := "res://ShareAsset/Internal/Characters/animation_v2/breakfast_exchange.png"

static func bounds(image: Image) -> Rect2i:
	var result := Rect2i()
	var found := false
	for y in image.get_height():
		for x in image.get_width():
			if image.get_pixel(x, y).a >= 0.75:
				var pixel := Rect2i(x, y, 1, 1)
				result = result.merge(pixel) if found else pixel
				found = true
	return result

# STORY-HARDCODE[CSH-028][schema][tool]: 素材工具为早餐交换场景绑定人物姿态、桌面与汤碗资源；不推进剧情或映射答案。
static func build() -> void:
	var source := Image.load_from_file(ProjectSettings.globalize_path(SOURCE))
	var atlas := Image.create_empty(512, 128, false, Image.FORMAT_RGBA8)
	append_poses(source, 6, 0, atlas)
	var between := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Characters/DemoAnimation/breakfast_exchange_between.png"))
	append_poses(between, 2, 6, atlas)
	assert(atlas.save_png(ProjectSettings.globalize_path(OUT)) == OK)
	var table := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Props/breakfast_table_clear.png"))
	table = table.get_region(bounds(table))
	table.resize(224, 44, Image.INTERPOLATE_NEAREST)
	var canvas := Image.create_empty(224, 64, false, Image.FORMAT_RGBA8)
	canvas.blit_rect(table, Rect2i(Vector2i.ZERO, table.get_size()), Vector2i(0, 8))
	assert(ResourceSaver.save(ImageTexture.create_from_image(canvas), "res://ShareAsset/Internal/Props/breakfast_table_clear.res") == OK)
	var soup := Image.load_from_file(ProjectSettings.globalize_path("res://AssetsSource/Internal/Props/breakfast_soup_bowl_source.png"))
	soup = soup.get_region(bounds(soup))
	soup.resize(12, 9, Image.INTERPOLATE_NEAREST)
	var bowl := Image.create_empty(16, 16, false, Image.FORMAT_RGBA8)
	bowl.blit_rect(soup, Rect2i(Vector2i.ZERO, soup.get_size()), Vector2i(2, 3))
	assert(ResourceSaver.save(ImageTexture.create_from_image(bowl), "res://ShareAsset/Internal/Props/breakfast_exchange_bowl.res") == OK)
	print("PASS breakfast sources imported")

static func append_poses(source: Image, columns: int, column_offset: int, atlas: Image) -> void:
	var cell := Vector2i(source.get_width() / columns, source.get_height() / 2)
	for row in 2:
		var first := source.get_region(Rect2i(Vector2i(0, row * cell.y), cell))
		var factor := 39.0 / bounds(first).size.y
		for col in columns:
			var part := source.get_region(Rect2i(Vector2i(col * cell.x, row * cell.y), cell))
			var rect := bounds(part)
			# Register on the feet, not the reach-dependent outer bounding box.
			var feet_strip := part.get_region(Rect2i(0, rect.end.y - 20, cell.x, 20))
			var feet := bounds(feet_strip)
			var anchor_x := feet.position.x + feet.size.x / 2.0
			var sprite := part.get_region(rect)
			var size := Vector2i(roundi(rect.size.x * factor), roundi(rect.size.y * factor))
			sprite.resize(size.x, size.y, Image.INTERPOLATE_NEAREST)
			for y in size.y:
				for x in size.x:
					var color := sprite.get_pixel(x, y)
					color.a = 1.0 if color.a >= 0.75 else 0.0
					sprite.set_pixel(x, y, color)
			var at := Vector2i((col + column_offset) * 64 + 32 - roundi((anchor_x - rect.position.x) * factor), row * 64 + 55 - size.y)
			atlas.blit_rect(sprite, Rect2i(Vector2i.ZERO, size), at)
			print("pose ", row, "/", col, " size=", size, " at=", at)
