extends SceneTree
## Derives two fixed-foot introduction key poses from the preserved generated source.

const SOURCE := "res://AssetsSource/Internal/Characters/"
const OUT := "res://ShareAsset/Internal/Characters/"
const MASTER := "res://ShareAsset/Internal/Characters/characters-21-front-master.png"

func _initialize() -> void:
	var atlas := Image.load_from_file(ProjectSettings.globalize_path(MASTER))
	var standing := atlas.get_region(Rect2i(128, 0, 32, 48))
	var palette: Array[Color] = []
	for y in standing.get_height():
		for x in standing.get_width():
			var color := standing.get_pixel(x, y)
			if color.a > 0.5 and not color in palette:
				palette.append(color)
	for spec in [
		["maxwell_hand_chest", "maxwell-hand-chest-source.png", Vector2i(26, 44)],
		["maxwell_bow", "maxwell-bow-source.png", Vector2i(27, 40)],
	]:
		build_pose(spec[0], spec[1], spec[2], palette)
	quit()

# STORY-HARDCODE[CSH-029][schema][tool]: Maxwell素材工具将开场角色绑定为手按胸口/鞠躬姿态及白手套像素；不推进剧情或映射答案。
func build_pose(name: String, source_name: String, maximum: Vector2i, palette: Array[Color]) -> void:
	var source := Image.load_from_file(ProjectSettings.globalize_path(SOURCE + source_name))
	assert(source != null and not source.is_empty(), "Missing Maxwell pose source: " + source_name)
	var bounds := Rect2i()
	var found := false
	for y in source.get_height():
		for x in source.get_width():
			if source.get_pixel(x, y).a > 0.5:
				var pixel := Rect2i(x, y, 1, 1)
				bounds = bounds.merge(pixel) if found else pixel
				found = true
	assert(found, "Maxwell pose has no opaque pixels: " + source_name)
	var sprite := source.get_region(bounds)
	# The generated poses are narrower than the native identity frame; normalize the silhouette width.
	sprite.resize(maximum.x, maximum.y, Image.INTERPOLATE_LANCZOS)
	var canvas := Image.create_empty(32, 48, false, Image.FORMAT_RGBA8)
	for y in sprite.get_height():
		for x in sprite.get_width():
			var color := sprite.get_pixel(x, y)
			if color.a <= 0.5:
				continue
			var nearest := palette[0]
			var distance := INF
			for candidate in palette:
				var score := Vector3(color.r, color.g, color.b).distance_squared_to(Vector3(candidate.r, candidate.g, candidate.b))
				if score < distance:
					distance = score
					nearest = candidate
			canvas.set_pixel(floori((32 - sprite.get_width()) / 2.0) + x, 47 - sprite.get_height() + y, nearest)
	# Preserve the white-glove gesture after reduction to 32x48; it is a key action cue.
	var glove := Color8(217, 196, 181)
	var glove_pixels := [Vector2i(18, 21), Vector2i(19, 21), Vector2i(17, 22), Vector2i(18, 22), Vector2i(19, 22), Vector2i(20, 22), Vector2i(18, 23), Vector2i(19, 23)] if name == "maxwell_hand_chest" else [Vector2i(18, 25), Vector2i(19, 25), Vector2i(17, 26), Vector2i(18, 26), Vector2i(19, 26), Vector2i(20, 26), Vector2i(18, 27), Vector2i(19, 27)]
	for pixel in glove_pixels:
		canvas.set_pixelv(pixel, glove)
	assert(canvas.save_png(ProjectSettings.globalize_path(OUT + name + ".png")) == OK)
	assert(ResourceSaver.save(ImageTexture.create_from_image(canvas), OUT + name + ".res") == OK)
