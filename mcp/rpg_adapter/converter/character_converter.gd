@tool
class_name CharacterConverter
extends RefCounted

## Converts RPG Maker single-character ($) and multi-character sheets into Godot 4 SpriteFrames.
## Supports directional animations (walk_down, walk_left, walk_right, walk_up) and object alignment offset.

const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")
const SourceLoader = preload("res://addons/rpg_asset_adapter/source_loader.gd")

const DEFAULT_FPS: float = 6.0
const WALK_SEQUENCE: Array[int] = [0, 1, 2, 1]
const DIRECTION_ANIM_NAMES: Array[String] = ["walk_down", "walk_left", "walk_right", "walk_up"]


## Calculates vertical alignment offset based on object alignment mode (! prefix).
## Normal characters: offset_y = -tile_size / 8.0 (e.g. -6.0 for 48px tile).
## With '!' prefix: offset_y = 0.0.
static func calculate_offset_y(tile_size: int, has_exclamation: bool) -> float:
	if has_exclamation:
		return 0.0
	return -float(tile_size) / 8.0


## Resolves input into a Texture2D and Image.
static func _resolve_texture_and_image(source: Variant) -> Dictionary:
	return SourceLoader.resolve_texture_and_image(source)


## Slices a single character ($) image (3 cols x 4 rows) into a SpriteFrames resource.
## Row 0 = walk_down, Row 1 = walk_left, Row 2 = walk_right, Row 3 = walk_up.
## Walk sequence per direction: 0 -> 1 -> 2 -> 1.
static func slice_single_character(
	source: Variant,
	tile_size: int = 32,
	has_exclamation: bool = false,
	fps: float = DEFAULT_FPS
) -> SpriteFrames:
	var resolved := _resolve_texture_and_image(source)
	var image: Image = resolved["image"]
	var texture: Texture2D = resolved["texture"]
	if image == null or texture == null:
		push_error("CharacterConverter: Failed to load source image or texture.")
		return null

	var frame_w := image.get_width() / 3
	var frame_h := image.get_height() / 4
	var offset_y := calculate_offset_y(tile_size, has_exclamation)

	var sf := SpriteFrames.new()
	if sf.has_animation("default"):
		sf.remove_animation("default")

	for row in range(4):
		var anim_name: String = DIRECTION_ANIM_NAMES[row]
		sf.add_animation(anim_name)
		sf.set_animation_loop(anim_name, true)
		sf.set_animation_speed(anim_name, fps)

		for col_step in WALK_SEQUENCE:
			var atlas := AtlasTexture.new()
			atlas.atlas = texture
			atlas.region = Rect2(col_step * frame_w, row * frame_h, frame_w, frame_h)
			sf.add_frame(anim_name, atlas)

	sf.set_meta("offset_y", offset_y)
	sf.set_meta("frame_width", frame_w)
	sf.set_meta("frame_height", frame_h)
	sf.set_meta("tile_size", tile_size)
	sf.set_meta("has_exclamation", has_exclamation)
	sf.set_meta("is_single", true)

	return sf


## Slices a multi-character sheet (12 cols x 8 rows, 8 characters total) into an Array[SpriteFrames].
## 4 characters across x 2 characters down.
static func slice_character_sheet(
	source: Variant,
	tile_size: int = 32,
	has_exclamation: bool = false,
	fps: float = DEFAULT_FPS
) -> Array[SpriteFrames]:
	var resolved := _resolve_texture_and_image(source)
	var image: Image = resolved["image"]
	var texture: Texture2D = resolved["texture"]
	var result: Array[SpriteFrames] = []
	if image == null or texture == null:
		push_error("CharacterConverter: Failed to load source image or texture.")
		return result

	var frame_w := image.get_width() / 12
	var frame_h := image.get_height() / 8
	var offset_y := calculate_offset_y(tile_size, has_exclamation)

	for char_idx in range(8):
		var char_col := char_idx % 4
		var char_row := char_idx / 4
		var start_col := char_col * 3
		var start_row := char_row * 4

		var sf := SpriteFrames.new()
		if sf.has_animation("default"):
			sf.remove_animation("default")

		for dir_row in range(4):
			var anim_name: String = DIRECTION_ANIM_NAMES[dir_row]
			sf.add_animation(anim_name)
			sf.set_animation_loop(anim_name, true)
			sf.set_animation_speed(anim_name, fps)

			var row := start_row + dir_row
			for col_step in WALK_SEQUENCE:
				var col := start_col + col_step
				var atlas := AtlasTexture.new()
				atlas.atlas = texture
				atlas.region = Rect2(col * frame_w, row * frame_h, frame_w, frame_h)
				sf.add_frame(anim_name, atlas)

		sf.set_meta("offset_y", offset_y)
		sf.set_meta("frame_width", frame_w)
		sf.set_meta("frame_height", frame_h)
		sf.set_meta("tile_size", tile_size)
		sf.set_meta("has_exclamation", has_exclamation)
		sf.set_meta("is_single", false)
		sf.set_meta("character_index", char_idx)
		result.append(sf)

	return result


## Unified conversion helper for characters using AssetDetector metadata or manual parameters.
static func convert(
	path: String,
	manual_tile_size: int = 0,
	fps: float = DEFAULT_FPS
) -> Dictionary:
	var info = AssetDetectorScript.detect_asset(path, AssetDetectorScript.DetectMode.AUTO, manual_tile_size)
	var is_single: bool = info.extra.get("is_single", false)
	var has_exclamation: bool = info.extra.get("has_exclamation", false)
	var tile_size: int = info.tile_size
	var offset_y := calculate_offset_y(tile_size, has_exclamation)

	if is_single:
		var frames := slice_single_character(path, tile_size, has_exclamation, fps)
		return {
			"is_single": true,
			"tile_size": tile_size,
			"offset_y": offset_y,
			"has_exclamation": has_exclamation,
			"sprite_frames": frames,
			"characters": [frames] if frames != null else [],
			"character_count": 1 if frames != null else 0
		}
	else:
		var characters := slice_character_sheet(path, tile_size, has_exclamation, fps)
		return {
			"is_single": false,
			"tile_size": tile_size,
			"offset_y": offset_y,
			"has_exclamation": has_exclamation,
			"sprite_frames": characters[0] if characters.size() > 0 else null,
			"characters": characters,
			"character_count": characters.size()
		}
