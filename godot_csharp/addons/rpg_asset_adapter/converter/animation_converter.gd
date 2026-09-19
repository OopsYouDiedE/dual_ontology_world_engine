@tool
class_name AnimationConverter
extends RefCounted

## Converts RPG Maker and KR single object sheets (!$) and multi-object sheets into Godot 4 SpriteFrames
## and generates ready-to-use PackedScenes for Doors and Treasure Chests.

const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")
const BaseProfileScript = preload("res://addons/rpg_asset_adapter/profiles/base_profile.gd")
const RpgMakerProfileScript = preload("res://addons/rpg_asset_adapter/profiles/rpg_maker_profile.gd")
const KrProfileScript = preload("res://addons/rpg_asset_adapter/profiles/kr_profile.gd")

const DEFAULT_FPS: float = 6.0
const DEFAULT_OBJECT_ROW_NAMES: Array[String] = ["row_0", "row_1", "row_2", "row_3"]


## Resolves input into Texture2D and Image.
static func _resolve_texture_and_image(source: Variant) -> Dictionary:
	var texture: Texture2D = null
	var image: Image = null

	if source is String:
		var path: String = source
		if ResourceLoader.exists(path):
			var res = ResourceLoader.load(path)
			if res is Texture2D:
				texture = res
				image = texture.get_image()
		if image == null:
			var global_path := ProjectSettings.globalize_path(path) if path.begins_with("res://") else path
			if FileAccess.file_exists(global_path):
				image = Image.load_from_file(global_path)
				if image != null:
					texture = ImageTexture.create_from_image(image)
	elif source is Texture2D:
		texture = source
		image = texture.get_image()
	elif source is Image:
		image = source
		texture = ImageTexture.create_from_image(image)

	return {
		"texture": texture,
		"image": image
	}


## Resolves row animation names based on profile, asset kind, or custom row names array.
static func get_row_names(
	profile: BaseProfileScript = null,
	kind: AssetDetectorScript.AssetKind = AssetDetectorScript.AssetKind.UNKNOWN,
	custom_names: Array[String] = []
) -> Array[String]:
	if not custom_names.is_empty():
		return custom_names

	if profile != null:
		if kind == AssetDetectorScript.AssetKind.DOOR:
			return profile.get_door_animation_names()
		elif kind == AssetDetectorScript.AssetKind.TREASURE_CHEST:
			return profile.get_chest_animation_names()
		else:
			return DEFAULT_OBJECT_ROW_NAMES.duplicate()

	return DEFAULT_OBJECT_ROW_NAMES.duplicate()








## Slices a single object animation sheet !$ (3 cols x 4 rows) into a SpriteFrames resource.

## Each row becomes an animation named according to row_names or the profile.
static func slice_single_object(
	source: Variant,
	row_names: Array[String] = [],
	fps: float = DEFAULT_FPS,
	loop: bool = false
) -> SpriteFrames:
	var resolved := _resolve_texture_and_image(source)
	var image: Image = resolved["image"]
	var texture: Texture2D = resolved["texture"]
	if image == null or texture == null:
		push_error("AnimationConverter: Failed to load source image or texture.")
		return null

	var names: Array[String] = row_names if not row_names.is_empty() else DEFAULT_OBJECT_ROW_NAMES.duplicate()
	var frame_w := image.get_width() / 3
	var frame_h := image.get_height() / 4

	var sf := SpriteFrames.new()
	if sf.has_animation("default"):
		sf.remove_animation("default")

	for row in range(4):
		var anim_name: String = names[row] if row < names.size() else ("row_%d" % row)
		sf.add_animation(anim_name)
		sf.set_animation_loop(anim_name, loop)
		sf.set_animation_speed(anim_name, fps)

		for col in range(3):
			var atlas := AtlasTexture.new()
			atlas.atlas = texture
			atlas.region = Rect2(col * frame_w, row * frame_h, frame_w, frame_h)
			sf.add_frame(anim_name, atlas)

	sf.set_meta("frame_width", frame_w)
	sf.set_meta("frame_height", frame_h)
	sf.set_meta("is_single", true)

	return sf


## Slices a multi-object sheet (12 cols x 8 rows, 8 objects total) into Array[SpriteFrames].
## 4 objects across x 2 objects down.
static func slice_object_sheet(
	source: Variant,
	row_names: Array[String] = [],
	fps: float = DEFAULT_FPS,
	loop: bool = false
) -> Array[SpriteFrames]:
	var resolved := _resolve_texture_and_image(source)
	var image: Image = resolved["image"]
	var texture: Texture2D = resolved["texture"]
	var result: Array[SpriteFrames] = []
	if image == null or texture == null:
		push_error("AnimationConverter: Failed to load source image or texture.")
		return result

	var names: Array[String] = row_names if not row_names.is_empty() else DEFAULT_OBJECT_ROW_NAMES.duplicate()
	var frame_w := image.get_width() / 12
	var frame_h := image.get_height() / 8

	for obj_idx in range(8):
		var obj_col := obj_idx % 4
		var obj_row := obj_idx / 4
		var start_col := obj_col * 3
		var start_row := obj_row * 4

		var sf := SpriteFrames.new()
		if sf.has_animation("default"):
			sf.remove_animation("default")

		for dir_row in range(4):
			var anim_name: String = names[dir_row] if dir_row < names.size() else ("row_%d" % dir_row)
			sf.add_animation(anim_name)
			sf.set_animation_loop(anim_name, loop)
			sf.set_animation_speed(anim_name, fps)

			var row := start_row + dir_row
			for col_idx in range(3):
				var col := start_col + col_idx
				var atlas := AtlasTexture.new()
				atlas.atlas = texture
				atlas.region = Rect2(col * frame_w, row * frame_h, frame_w, frame_h)
				sf.add_frame(anim_name, atlas)

		sf.set_meta("frame_width", frame_w)
		sf.set_meta("frame_height", frame_h)
		sf.set_meta("is_single", false)
		sf.set_meta("object_index", obj_idx)
		result.append(sf)

	return result


## Generates a PackedScene for a Door (Door.tscn).
## Hierarchy:
## Node2D (name="Door")
## ├── AnimatedSprite2D (name="AnimatedSprite2D", sprite_frames=frames, animation="closed" or "row_0")
## ├── StaticBody2D (name="StaticBody2D")
## │   └── CollisionShape2D (name="CollisionShape2D", shape=RectangleShape2D(size=(tile_size, tile_size)))
## └── Area2D (name="TriggerArea")
##     └── CollisionShape2D (name="CollisionShape2D", shape=RectangleShape2D(size=(tile_size, tile_size)))
static func create_door_scene(frames: SpriteFrames, tile_size: int = 48) -> PackedScene:
	var root := Node2D.new()
	root.name = "Door"

	var anim_sprite := AnimatedSprite2D.new()
	anim_sprite.name = "AnimatedSprite2D"
	anim_sprite.sprite_frames = frames
	if frames != null:
		if frames.has_animation("closed"):
			anim_sprite.animation = "closed"
		elif frames.has_animation("row_0"):
			anim_sprite.animation = "row_0"
	root.add_child(anim_sprite)
	anim_sprite.owner = root

	var static_body := StaticBody2D.new()
	static_body.name = "StaticBody2D"
	root.add_child(static_body)
	static_body.owner = root

	var col_shape := CollisionShape2D.new()
	col_shape.name = "CollisionShape2D"
	var body_rect := RectangleShape2D.new()
	body_rect.size = Vector2(tile_size, tile_size)
	col_shape.shape = body_rect
	static_body.add_child(col_shape)
	col_shape.owner = root

	var trigger_area := Area2D.new()
	trigger_area.name = "TriggerArea"
	root.add_child(trigger_area)
	trigger_area.owner = root

	var trigger_col := CollisionShape2D.new()
	trigger_col.name = "CollisionShape2D"
	var area_rect := RectangleShape2D.new()
	area_rect.size = Vector2(tile_size, tile_size)
	trigger_col.shape = area_rect
	trigger_area.add_child(trigger_col)
	trigger_col.owner = root

	var scene := PackedScene.new()
	scene.pack(root)
	root.free()
	return scene


## Generates a PackedScene for a Treasure Chest (TreasureChest.tscn).
## Hierarchy:
## Node2D (name="TreasureChest")
## ├── AnimatedSprite2D (name="AnimatedSprite2D", sprite_frames=frames, animation="closed" or "row_0")
## ├── StaticBody2D (name="StaticBody2D")
## │   └── CollisionShape2D (name="CollisionShape2D", shape=RectangleShape2D(size=(tile_size, tile_size)))
## └── Area2D (name="TriggerArea")
##     └── CollisionShape2D (name="CollisionShape2D", shape=RectangleShape2D(size=(tile_size, tile_size)))
static func create_chest_scene(frames: SpriteFrames, tile_size: int = 48) -> PackedScene:
	var root := Node2D.new()
	root.name = "TreasureChest"

	var anim_sprite := AnimatedSprite2D.new()
	anim_sprite.name = "AnimatedSprite2D"
	anim_sprite.sprite_frames = frames
	if frames != null:
		if frames.has_animation("closed"):
			anim_sprite.animation = "closed"
		elif frames.has_animation("row_0"):
			anim_sprite.animation = "row_0"
	root.add_child(anim_sprite)
	anim_sprite.owner = root

	var static_body := StaticBody2D.new()
	static_body.name = "StaticBody2D"
	root.add_child(static_body)
	static_body.owner = root

	var col_shape := CollisionShape2D.new()
	col_shape.name = "CollisionShape2D"
	var body_rect := RectangleShape2D.new()
	body_rect.size = Vector2(tile_size, tile_size)
	col_shape.shape = body_rect
	static_body.add_child(col_shape)
	col_shape.owner = root

	var trigger_area := Area2D.new()
	trigger_area.name = "TriggerArea"
	root.add_child(trigger_area)
	trigger_area.owner = root

	var trigger_col := CollisionShape2D.new()
	trigger_col.name = "CollisionShape2D"
	var area_rect := RectangleShape2D.new()
	area_rect.size = Vector2(tile_size, tile_size)
	trigger_col.shape = area_rect
	trigger_area.add_child(trigger_col)
	trigger_col.owner = root

	var scene := PackedScene.new()
	scene.pack(root)
	root.free()
	return scene


## Handles Generic Animation:
## Slices frames to SpriteFrames if a profile or explicit row names are provided;
## otherwise marks requires_animation_profile = true.
static func slice_generic_animation(
	source: Variant,
	profile: BaseProfileScript = null,
	row_names: Array[String] = [],
	cols: int = 3,
	rows: int = 4,
	fps: float = DEFAULT_FPS,
	loop: bool = false
) -> Dictionary:
	if profile == null and row_names.is_empty():
		return {
			"sprite_frames": null,
			"requires_animation_profile": true
		}

	var names: Array[String] = row_names
	if names.is_empty() and profile != null:
		names = DEFAULT_OBJECT_ROW_NAMES.duplicate()

	var resolved := _resolve_texture_and_image(source)
	var image: Image = resolved["image"]
	var texture: Texture2D = resolved["texture"]
	if image == null or texture == null:
		push_error("AnimationConverter: Failed to load generic animation image or texture.")
		return {
			"sprite_frames": null,
			"requires_animation_profile": false
		}

	var frame_w := image.get_width() / cols
	var frame_h := image.get_height() / rows

	var sf := SpriteFrames.new()
	if sf.has_animation("default"):
		sf.remove_animation("default")

	for r in range(rows):
		var anim_name: String = names[r] if r < names.size() else ("row_%d" % r)
		sf.add_animation(anim_name)
		sf.set_animation_loop(anim_name, loop)
		sf.set_animation_speed(anim_name, fps)

		for c in range(cols):
			var atlas := AtlasTexture.new()
			atlas.atlas = texture
			atlas.region = Rect2(c * frame_w, r * frame_h, frame_w, frame_h)
			sf.add_frame(anim_name, atlas)

	sf.set_meta("frame_width", frame_w)
	sf.set_meta("frame_height", frame_h)

	return {
		"sprite_frames": sf,
		"requires_animation_profile": false
	}


## Checks whether a rectangular region contains any non-transparent pixel content.
static func _is_region_empty(img: Image, rect: Rect2i) -> bool:
	if img == null:
		return true
	var max_x := mini(rect.position.x + rect.size.x, img.get_width())
	var max_y := mini(rect.position.y + rect.size.y, img.get_height())
	if rect.position.x >= max_x or rect.position.y >= max_y:
		return true
	for y in range(rect.position.y, max_y):
		for x in range(rect.position.x, max_x):
			if img.get_pixel(x, y).a > 0.05:
				return false
	return true


## Universal frame extraction method based strictly on (x_tile, y_tile, orientation "h"/"v", frames).
static func extract_strip_frames(
	texture: Texture2D,
	start_pos: Vector2i,
	x_tile: int,
	y_tile: int,
	orientation: String = "v",
	frames_count: int = 4,
	tile_size: int = 48,
	fps: float = DEFAULT_FPS,
	is_interactive: bool = true
) -> SpriteFrames:
	var frame_w := x_tile * tile_size
	var frame_h := y_tile * tile_size
	var sf := SpriteFrames.new()
	if sf.has_animation("default"):
		sf.remove_animation("default")

	var step_x := frame_w if orientation == "h" else 0
	var step_y := frame_h if orientation == "v" else 0

	var frames_array: Array[AtlasTexture] = []
	for f in range(frames_count):
		var atlas := AtlasTexture.new()
		atlas.atlas = texture
		atlas.region = Rect2(
			start_pos.x + f * step_x,
			start_pos.y + f * step_y,
			frame_w,
			frame_h
		)
		frames_array.append(atlas)

	if is_interactive and frames_count == 4:
		# Standard 4-frame door/chest animation states
		sf.add_animation("closed")
		sf.set_animation_loop("closed", false)
		sf.add_frame("closed", frames_array[0])

		sf.add_animation("open")
		sf.set_animation_loop("open", false)
		sf.set_animation_speed("open", fps)
		for f in frames_array:
			sf.add_frame("open", f)

		sf.add_animation("opened")
		sf.set_animation_loop("opened", false)
		sf.add_frame("opened", frames_array[3])

		sf.add_animation("close")
		sf.set_animation_loop("close", false)
		sf.set_animation_speed("close", fps)
		for f_idx in [3, 2, 1, 0]:
			sf.add_frame("close", frames_array[f_idx])
	else:
		sf.add_animation("default")
		sf.set_animation_loop("default", true)
		sf.set_animation_speed("default", fps)
		for f in frames_array:
			sf.add_frame("default", f)

	sf.set_meta("frame_width", frame_w)
	sf.set_meta("frame_height", frame_h)
	sf.set_meta("x_tile", x_tile)
	sf.set_meta("y_tile", y_tile)
	sf.set_meta("orientation", orientation)
	sf.set_meta("frames", frames_count)
	return sf


## Slices door sprite sheets into individual door scenes & frames based on explicit layout descriptors.
## Returns Dictionary with objects, scenes, sprite_frames, packed_scene.
static func slice_doors(source: Variant, tile_size: int = 48, fps: float = DEFAULT_FPS) -> Dictionary:
	var resolved := _resolve_texture_and_image(source)
	var image: Image = resolved["image"]
	var texture: Texture2D = resolved["texture"]
	var result := {
		"objects": [] as Array[SpriteFrames],
		"scenes": [] as Array[Dictionary],
		"sprite_frames": null,
		"packed_scene": null
	}
	if image == null or texture == null:
		return result

	var w := image.get_width()
	var h := image.get_height()
	var stem: String = ""
	if source is String:
		stem = source.get_file().get_basename()

	# Generate list of strip descriptors: {name, pos: Vector2i, x_tile, y_tile, orientation, frames}
	var descriptors: Array[Dictionary] = []

	# Case 1: XLarge Single Door (3T wide x 12T high -> 3x3 tiles per frame, 4 vertical frames)
	if w == tile_size * 3 and h == tile_size * 12:
		descriptors.append({
			"name": "%s_xlarge" % stem,
			"pos": Vector2i(0, 0),
			"x_tile": 3,
			"y_tile": 3,
			"orientation": "v",
			"frames": 4
		})

	# Case 2: Large Doors Sheet (12T wide x 16T high)
	elif w == tile_size * 12 and h == tile_size * 16:
		var block_w := tile_size * 3
		var block_h := tile_size * 8
		# Top 4 blocks (row 0): 2x2 double door + 1x2 single door
		for col in range(4):
			var bx := col * block_w
			descriptors.append({
				"name": "%s_r0_c%d_double" % [stem, col],
				"pos": Vector2i(bx, 0),
				"x_tile": 2,
				"y_tile": 2,
				"orientation": "v",
				"frames": 4
			})
			descriptors.append({
				"name": "%s_r0_c%d_single" % [stem, col],
				"pos": Vector2i(bx + tile_size * 2, 0),
				"x_tile": 1,
				"y_tile": 2,
				"orientation": "v",
				"frames": 4
			})
		# Bottom blocks (row 1): col 0 & 1 are 3x2 grand doors, col 2 is 2x2 arch door
		descriptors.append({
			"name": "%s_r1_c0_grand" % stem,
			"pos": Vector2i(0, block_h),
			"x_tile": 3,
			"y_tile": 2,
			"orientation": "v",
			"frames": 4
		})
		descriptors.append({
			"name": "%s_r1_c1_grand" % stem,
			"pos": Vector2i(block_w, block_h),
			"x_tile": 3,
			"y_tile": 2,
			"orientation": "v",
			"frames": 4
		})
		descriptors.append({
			"name": "%s_r1_c2_arch" % stem,
			"pos": Vector2i(block_w * 2, block_h),
			"x_tile": 2,
			"y_tile": 2,
			"orientation": "v",
			"frames": 4
		})

	# Case 3: Small Doors Sheet (12T wide x 8T high)
	elif w == tile_size * 12 and h == tile_size * 8:
		var block_w := tile_size * 3
		var block_h := tile_size * 4
		for row in range(2):
			for col in range(4):
				var bx := col * block_w
				var by := row * block_h
				descriptors.append({
					"name": "%s_r%d_c%d_double" % [stem, row, col],
					"pos": Vector2i(bx, by),
					"x_tile": 2,
					"y_tile": 1,
					"orientation": "v",
					"frames": 4
				})
				descriptors.append({
					"name": "%s_r%d_c%d_single" % [stem, row, col],
					"pos": Vector2i(bx + tile_size * 2, by),
					"x_tile": 1,
					"y_tile": 1,
					"orientation": "v",
					"frames": 4
				})

	# Case 4: Standard / Fallback grid
	else:
		var cols_count := maxi(1, w / (tile_size * 3))
		var rows_count := maxi(1, h / (tile_size * 4))
		for r in range(rows_count):
			for c in range(cols_count):
				descriptors.append({
					"name": "%s_r%d_c%d" % [stem, r, c],
					"pos": Vector2i(c * tile_size * 3, r * tile_size * 4),
					"x_tile": 1,
					"y_tile": 1,
					"orientation": "v",
					"frames": 4
				})

	# Slicing execution
	for desc in descriptors:
		var pos: Vector2i = desc["pos"]
		var xt: int = desc["x_tile"]
		var yt: int = desc["y_tile"]
		var orient: String = desc["orientation"]
		var f_cnt: int = desc["frames"]
		var d_name: String = desc["name"]

		var total_w := xt * tile_size * (f_cnt if orient == "h" else 1)
		var total_h := yt * tile_size * (f_cnt if orient == "v" else 1)
		if _is_region_empty(image, Rect2i(pos.x, pos.y, total_w, total_h)):
			continue

		var sf := extract_strip_frames(texture, pos, xt, yt, orient, f_cnt, tile_size, fps, true)
		result["objects"].append(sf)

		var sc := create_interactive_object_scene(sf, "Door", Vector2(xt * tile_size, yt * tile_size))
		result["scenes"].append({"name": d_name, "scene": sc, "frames": sf})

	if not result["objects"].is_empty():
		result["sprite_frames"] = result["objects"][0]
		result["packed_scene"] = result["scenes"][0]["scene"]

	return result


## Slices treasure chest sheets strictly according to (x_tile=1, y_tile=1, orientation="v", frames=4).
static func slice_chests(source: Variant, tile_size: int = 48, fps: float = DEFAULT_FPS) -> Dictionary:
	var resolved := _resolve_texture_and_image(source)
	var image: Image = resolved["image"]
	var texture: Texture2D = resolved["texture"]
	var result := {
		"objects": [] as Array[SpriteFrames],
		"scenes": [] as Array[Dictionary],
		"sprite_frames": null,
		"packed_scene": null
	}
	if image == null or texture == null:
		return result

	var h := image.get_height()
	var stem: String = ""
	if source is String:
		stem = source.get_file().get_basename()

	var num_blocks := 1
	var block_h := h
	if h >= tile_size * 8:
		num_blocks = h / (tile_size * 4)
		block_h = tile_size * 4

	for b_idx in range(num_blocks):
		var by := b_idx * block_h

		# Front view (col 0: x=0, 1x1 tile, vertical 4 frames)
		if not _is_region_empty(image, Rect2i(0, by, tile_size, tile_size * 4)):
			var sf_front := extract_strip_frames(texture, Vector2i(0, by), 1, 1, "v", 4, tile_size, fps, true)
			result["objects"].append(sf_front)
			var sc_front := create_interactive_object_scene(sf_front, "TreasureChest_Front", Vector2(tile_size, tile_size))
			result["scenes"].append({"name": "%s_b%d_front" % [stem, b_idx], "scene": sc_front, "frames": sf_front})

		# Back view (col 1: x=tile_size, 1x1 tile, vertical 4 frames)
		if not _is_region_empty(image, Rect2i(tile_size, by, tile_size, tile_size * 4)):
			var sf_back := extract_strip_frames(texture, Vector2i(tile_size, by), 1, 1, "v", 4, tile_size, fps, true)
			result["objects"].append(sf_back)
			var sc_back := create_interactive_object_scene(sf_back, "TreasureChest_Back", Vector2(tile_size, tile_size))
			result["scenes"].append({"name": "%s_b%d_back" % [stem, b_idx], "scene": sc_back, "frames": sf_back})

	if not result["objects"].is_empty():
		result["sprite_frames"] = result["objects"][0]
		result["packed_scene"] = result["scenes"][0]["scene"]

	return result



## Slices fountain / animated water sheet (e.g. 432x576: 3 cols x 4 rows of 144x144).
static func slice_fountain(source: Variant, fps: float = DEFAULT_FPS) -> Dictionary:
	var resolved := _resolve_texture_and_image(source)
	var image: Image = resolved["image"]
	var texture: Texture2D = resolved["texture"]
	var result := {
		"objects": [] as Array[SpriteFrames],
		"scenes": [] as Array[Dictionary],
		"sprite_frames": null,
		"packed_scene": null
	}
	if image == null or texture == null:
		return result

	var stem: String = ""
	if source is String:
		stem = source.get_file().get_basename()

	var cols := 3
	var rows := 4
	var frame_w := image.get_width() / cols
	var frame_h := image.get_height() / rows

	for r in range(2):
		var sf := SpriteFrames.new()
		if sf.has_animation("default"):
			sf.remove_animation("default")

		sf.add_animation("loop")
		sf.set_animation_loop("loop", true)
		sf.set_animation_speed("loop", fps)

		for c in range(cols):
			var atlas := AtlasTexture.new()
			atlas.atlas = texture
			atlas.region = Rect2(c * frame_w, r * frame_h, frame_w, frame_h)
			sf.add_frame("loop", atlas)

		sf.set_meta("frame_width", frame_w)
		sf.set_meta("frame_height", frame_h)
		result["objects"].append(sf)

		var sc := create_interactive_object_scene(sf, "Fountain_r%d" % r, Vector2(frame_w, frame_h))
		result["scenes"].append({"name": "%s_fountain_r%d" % [stem, r], "scene": sc, "frames": sf})

	if not result["objects"].is_empty():
		result["sprite_frames"] = result["objects"][0]
		result["packed_scene"] = result["scenes"][0]["scene"]

	return result


static func create_interactive_object_scene(frames: SpriteFrames, root_name: String, size: Vector2) -> PackedScene:
	var root := Node2D.new()
	root.name = root_name

	var anim_sprite := AnimatedSprite2D.new()
	anim_sprite.name = "AnimatedSprite2D"
	anim_sprite.sprite_frames = frames
	if frames != null:
		if frames.has_animation("closed"):
			anim_sprite.animation = "closed"
		elif frames.has_animation("loop"):
			anim_sprite.animation = "loop"
			anim_sprite.play("loop")
		elif frames.has_animation("row_0"):
			anim_sprite.animation = "row_0"
	root.add_child(anim_sprite)
	anim_sprite.owner = root

	var static_body := StaticBody2D.new()
	static_body.name = "StaticBody2D"
	root.add_child(static_body)
	static_body.owner = root

	var col_shape := CollisionShape2D.new()
	col_shape.name = "CollisionShape2D"
	var body_rect := RectangleShape2D.new()
	body_rect.size = size
	col_shape.shape = body_rect
	static_body.add_child(col_shape)
	col_shape.owner = root

	var trigger_area := Area2D.new()
	trigger_area.name = "TriggerArea"
	root.add_child(trigger_area)
	trigger_area.owner = root

	var trigger_col := CollisionShape2D.new()
	trigger_col.name = "CollisionShape2D"
	var area_rect := RectangleShape2D.new()
	area_rect.size = size
	trigger_col.shape = area_rect
	trigger_area.add_child(trigger_col)
	trigger_col.owner = root

	var scene := PackedScene.new()
	scene.pack(root)
	root.free()
	return scene


## Unified converter for object animations (doors, chests, generic animations).
static func convert(
	path: String,
	profile: BaseProfileScript = null,
	manual_tile_size: int = 0,
	fps: float = DEFAULT_FPS
) -> Dictionary:
	var info = AssetDetectorScript.detect_asset(path, AssetDetectorScript.DetectMode.AUTO, manual_tile_size)
	var is_single: bool = info.extra.get("is_single", true)
	var tile_size: int = info.tile_size
	var kind: AssetDetectorScript.AssetKind = info.kind
	var lower_stem := path.get_file().to_lower()

	if kind == AssetDetectorScript.AssetKind.DOOR:
		var d_res := slice_doors(path, tile_size, fps)
		return {
			"kind": kind,
			"is_single": false,
			"tile_size": tile_size,
			"sprite_frames": d_res["sprite_frames"],
			"objects": d_res["objects"],
			"packed_scene": d_res["packed_scene"],
			"scenes": d_res["scenes"],
			"requires_animation_profile": false
		}

	if kind == AssetDetectorScript.AssetKind.TREASURE_CHEST:
		var c_res := slice_chests(path, tile_size, fps)
		return {
			"kind": kind,
			"is_single": false,
			"tile_size": tile_size,
			"sprite_frames": c_res["sprite_frames"],
			"objects": c_res["objects"],
			"packed_scene": c_res["packed_scene"],
			"scenes": c_res["scenes"],
			"requires_animation_profile": false
		}

	if lower_stem.contains("fountain") or lower_stem.contains("water"):
		var f_res := slice_fountain(path, fps)
		return {
			"kind": kind,
			"is_single": false,
			"tile_size": tile_size,
			"sprite_frames": f_res["sprite_frames"],
			"objects": f_res["objects"],
			"packed_scene": f_res["packed_scene"],
			"scenes": f_res["scenes"],
			"requires_animation_profile": false
		}

	# If generic animation without profile
	if kind == AssetDetectorScript.AssetKind.GENERIC_ANIMATION and profile == null:
		return {
			"kind": kind,
			"tile_size": tile_size,
			"sprite_frames": null,
			"packed_scene": null,
			"requires_animation_profile": true
		}

	var row_names := get_row_names(profile, kind)
	var frames: SpriteFrames = null
	var frames_list: Array[SpriteFrames] = []

	if is_single:
		frames = slice_single_object(path, row_names, fps)
		if frames != null:
			frames_list.append(frames)
	else:
		frames_list = slice_object_sheet(path, row_names, fps)
		frames = frames_list[0] if not frames_list.is_empty() else null

	var scene: PackedScene = null
	if kind == AssetDetectorScript.AssetKind.DOOR:
		scene = create_door_scene(frames, tile_size)
	elif kind == AssetDetectorScript.AssetKind.TREASURE_CHEST:
		scene = create_chest_scene(frames, tile_size)

	return {
		"kind": kind,
		"is_single": is_single,
		"tile_size": tile_size,
		"sprite_frames": frames,
		"objects": frames_list,
		"packed_scene": scene,
		"requires_animation_profile": false
	}
