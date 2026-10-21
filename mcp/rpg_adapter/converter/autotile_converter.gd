@tool
class_name AutotileConverter
extends RefCounted

## Quarter-tile reconstruction engine and derived atlas generator for RPG Maker autotiles.
## Reconstructs 48-shape floor tiles, 16-shape wall tiles, and 4-shape waterfall tiles.
## Generates derived atlas images and metadata for A1, A2, A3, and A4 tilesets.

const AutotileTables = preload("res://addons/rpg_asset_adapter/data/autotile_tables.gd")
const AssetDetectorScript = preload("res://addons/rpg_asset_adapter/detector/asset_detector.gd")
const SourceLoader = preload("res://addons/rpg_asset_adapter/source_loader.gd")

const ANIMATION_DURATION: float = 0.5


## Resolves input into an Image (supports Image, Texture2D, or file path String).
static func _resolve_image(source: Variant) -> Image:
	return SourceLoader.resolve_image(source)


## Takes a 2T × 3T source block from an image, slices it into 4×6 quarter-tiles (T/2 × T/2),
## and builds 48 full T×T Images according to AutotileTables.FLOOR_AUTOTILE_TABLE.
static func reconstruct_floor_autotile(
	src_img: Image,
	block_rect: Rect2i,
	tile_size: int
) -> Array[Image]:
	var result: Array[Image] = []
	if src_img == null or tile_size <= 0:
		return result

	var half := tile_size / 2
	var img_format := src_img.get_format()

	for shape_idx in range(AutotileTables.FLOOR_AUTOTILE_TABLE.size()):
		var quarter_coords: Array = AutotileTables.FLOOR_AUTOTILE_TABLE[shape_idx]
		var tile_img := Image.create(tile_size, tile_size, false, img_format)

		for k in range(4):
			var qx: int = quarter_coords[k][0]
			var qy: int = quarter_coords[k][1]
			var src_r := Rect2i(
				block_rect.position.x + qx * half,
				block_rect.position.y + qy * half,
				half,
				half
			)
			var dst_p := Vector2i((k % 2) * half, (k / 2) * half)
			tile_img.blit_rect(src_img, src_r, dst_p)

		result.append(tile_img)

	return result


## Takes a 2T × 2T source block, slices into 4×4 quarter-tiles,
## and builds 16 full T×T Images according to AutotileTables.WALL_AUTOTILE_TABLE.
static func reconstruct_wall_autotile(
	src_img: Image,
	block_rect: Rect2i,
	tile_size: int
) -> Array[Image]:
	var result: Array[Image] = []
	if src_img == null or tile_size <= 0:
		return result

	var half := tile_size / 2
	var img_format := src_img.get_format()

	for shape_idx in range(AutotileTables.WALL_AUTOTILE_TABLE.size()):
		var quarter_coords: Array = AutotileTables.WALL_AUTOTILE_TABLE[shape_idx]
		var tile_img := Image.create(tile_size, tile_size, false, img_format)

		for k in range(4):
			var qx: int = quarter_coords[k][0]
			var qy: int = quarter_coords[k][1]
			var src_r := Rect2i(
				block_rect.position.x + qx * half,
				block_rect.position.y + qy * half,
				half,
				half
			)
			var dst_p := Vector2i((k % 2) * half, (k / 2) * half)
			tile_img.blit_rect(src_img, src_r, dst_p)

		result.append(tile_img)

	return result


## Takes a waterfall block (2T × 1T or 2T × 2T), slices into quarter-tiles,
## and builds 4 full T×T Images according to AutotileTables.WATERFALL_AUTOTILE_TABLE.
static func reconstruct_waterfall_autotile(
	src_img: Image,
	block_rect: Rect2i,
	tile_size: int
) -> Array[Image]:
	var result: Array[Image] = []
	if src_img == null or tile_size <= 0:
		return result

	var half := tile_size / 2
	var img_format := src_img.get_format()

	for shape_idx in range(AutotileTables.WATERFALL_AUTOTILE_TABLE.size()):
		var quarter_coords: Array = AutotileTables.WATERFALL_AUTOTILE_TABLE[shape_idx]
		var tile_img := Image.create(tile_size, tile_size, false, img_format)

		for k in range(4):
			var qx: int = quarter_coords[k][0]
			var qy: int = quarter_coords[k][1]
			var src_r := Rect2i(
				block_rect.position.x + qx * half,
				block_rect.position.y + qy * half,
				half,
				half
			)
			var dst_p := Vector2i((k % 2) * half, (k / 2) * half)
			tile_img.blit_rect(src_img, src_r, dst_p)

		result.append(tile_img)

	return result


## Processes an A1 sheet (16T × 12T) containing animated water and waterfall tiles.
## Ocean/water autotiles (48 shapes) are reconstructed for Frames 0, 1, 2 and laid out
## horizontally as [Frame 0, Frame 1, Frame 2, Frame 1] with frames_count = 4.
## Waterfall autotiles (4 shapes) are laid out horizontally as [Frame 0, Frame 1, Frame 2] with frames_count = 3.
## Returns {"image": Image, "autotiles": Array}.
static func generate_a1_derived_atlas(a1_path_or_img: Variant, tile_size: int = 32) -> Dictionary:
	var src_img := _resolve_image(a1_path_or_img)
	if src_img == null:
		return {"image": null, "autotiles": []}

	if tile_size <= 0:
		tile_size = 32

	var autotiles: Array = []

	# In A1:
	# - 8 animated water autotiles (4 on left cols 0..2, 4 on right cols 4..6).
	# - 2 static/decorative water autotiles (col 3, row 0 and row 1).
	# Total water autotiles = 10.
	# Each water autotile is 24 tiles wide (6 shapes × 4 frame cols) × 8 tiles high.
	# 2 water autotiles per row = 48 tiles wide. 5 rows = 40 tiles high.
	#
	# - 6 waterfall autotiles (col 3 rows 2, 3; col 7 rows 0..3).
	# Each waterfall autotile is 12 tiles wide (4 shapes × 3 frame cols) × 1 tile high.
	# 4 waterfalls per row = 48 tiles wide. 2 rows = 2 tiles high.
	# Total atlas size: 48 tiles wide × 42 tiles high.

	var atlas_w_tiles := 48
	var atlas_h_tiles := 42
	var atlas_img := Image.create(
		atlas_w_tiles * tile_size,
		atlas_h_tiles * tile_size,
		false,
		src_img.get_format()
	)

	var water_definitions: Array[Dictionary] = []
	# Left animated water (4 rows, cols 0..2)
	for r in range(4):
		water_definitions.append({"col": 0, "row": r, "animated": true, "name": "water_left_%d" % r})
	# Right animated water (4 rows, cols 4..6)
	for r in range(4):
		water_definitions.append({"col": 4, "row": r, "animated": true, "name": "water_right_%d" % r})
	# Center static / decorative water (col 3, rows 0..1)
	for r in range(2):
		water_definitions.append({"col": 3, "row": r, "animated": false, "name": "water_center_%d" % r})

	# Reconstruct and place water autotiles
	for w_idx in range(water_definitions.size()):
		var w_def: Dictionary = water_definitions[w_idx]
		var col_start: int = w_def["col"]
		var row_idx: int = w_def["row"]
		var is_anim: bool = w_def["animated"]
		var at_name: String = w_def["name"]

		var rect0 := Rect2i(col_start * 2 * tile_size, row_idx * 3 * tile_size, 2 * tile_size, 3 * tile_size)
		var f0_shapes := reconstruct_floor_autotile(src_img, rect0, tile_size)

		var f1_shapes: Array[Image] = []
		var f2_shapes: Array[Image] = []
		if is_anim:
			var rect1 := Rect2i((col_start + 1) * 2 * tile_size, row_idx * 3 * tile_size, 2 * tile_size, 3 * tile_size)
			var rect2 := Rect2i((col_start + 2) * 2 * tile_size, row_idx * 3 * tile_size, 2 * tile_size, 3 * tile_size)
			f1_shapes = reconstruct_floor_autotile(src_img, rect1, tile_size)
			f2_shapes = reconstruct_floor_autotile(src_img, rect2, tile_size)
		else:
			f1_shapes = f0_shapes
			f2_shapes = f0_shapes

		# Grid position of this water autotile in the derived atlas
		var pair_col := w_idx % 2
		var pair_row := w_idx / 2
		var base_tile_x := pair_col * 24
		var base_tile_y := pair_row * 8

		var tile_list: Array[Dictionary] = []
		for s in range(48):
			var sc := s % 6
			var sr := s / 6
			var tile_coord := Vector2i(base_tile_x + sc * 4, base_tile_y + sr)

			# Lay out 4 frame columns: [Frame 0, Frame 1, Frame 2, Frame 1]
			var frame_images: Array[Image] = [
				f0_shapes[s],
				f1_shapes[s],
				f2_shapes[s],
				f1_shapes[s]
			]
			for f in range(4):
				var px := (tile_coord.x + f) * tile_size
				var py := tile_coord.y * tile_size
				atlas_img.blit_rect(frame_images[f], Rect2i(0, 0, tile_size, tile_size), Vector2i(px, py))

			tile_list.append({
				"shape_index": s,
				"atlas_coords": tile_coord,
				"peering_bits": AutotileTables.get_floor_peering_bits(s)
			})

		autotiles.append({
			"name": at_name,
			"rpg_tile_base": 2048 + [0, 1, 8, 10, 4, 6, 12, 14, 2, 3][w_idx] * 48,
			"terrain_set": 0,
			"terrain_mode": TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES,
			"peering_type": "floor",
			"animation_frames_count": 4,
			"animation_columns": 4,
			"animation_frame_duration": ANIMATION_DURATION,
			"tiles": tile_list,
			"custom_data": {
				"suggested_layer": "lower"
			}
		})

	# Reconstruct and place waterfall autotiles (6 waterfalls)
	var waterfall_definitions: Array[Dictionary] = [
		{"col": 3, "row": 2, "name": "waterfall_center_0"},
		{"col": 3, "row": 3, "name": "waterfall_center_1"},
		{"col": 7, "row": 0, "name": "waterfall_right_0"},
		{"col": 7, "row": 1, "name": "waterfall_right_1"},
		{"col": 7, "row": 2, "name": "waterfall_right_2"},
		{"col": 7, "row": 3, "name": "waterfall_right_3"},
	]

	for wf_idx in range(waterfall_definitions.size()):
		var wf_def: Dictionary = waterfall_definitions[wf_idx]
		var c: int = wf_def["col"]
		var r: int = wf_def["row"]
		var wf_name: String = wf_def["name"]

		var rect0 := Rect2i(c * 2 * tile_size, (r * 3 + 0) * tile_size, 2 * tile_size, tile_size)
		var rect1 := Rect2i(c * 2 * tile_size, (r * 3 + 1) * tile_size, 2 * tile_size, tile_size)
		var rect2 := Rect2i(c * 2 * tile_size, (r * 3 + 2) * tile_size, 2 * tile_size, tile_size)

		var wf0_shapes := reconstruct_waterfall_autotile(src_img, rect0, tile_size)
		var wf1_shapes := reconstruct_waterfall_autotile(src_img, rect1, tile_size)
		var wf2_shapes := reconstruct_waterfall_autotile(src_img, rect2, tile_size)

		var wf_col := wf_idx % 4
		var wf_row := wf_idx / 4
		var base_tile_x := wf_col * 12
		var base_tile_y := 40 + wf_row

		var tile_list: Array[Dictionary] = []
		for s in range(4):
			var tile_coord := Vector2i(base_tile_x + s * 3, base_tile_y)

			# Lay out 3 frame columns: [Frame 0, Frame 1, Frame 2]
			var frame_images: Array[Image] = [
				wf0_shapes[s],
				wf1_shapes[s],
				wf2_shapes[s]
			]
			for f in range(3):
				var px := (tile_coord.x + f) * tile_size
				var py := tile_coord.y * tile_size
				atlas_img.blit_rect(frame_images[f], Rect2i(0, 0, tile_size, tile_size), Vector2i(px, py))

			tile_list.append({
				"shape_index": s,
				"atlas_coords": tile_coord,
				"peering_bits": AutotileTables.get_waterfall_peering_bits(s)
			})

		autotiles.append({
			"name": wf_name,
			"rpg_tile_base": 2048 + [9, 11, 5, 7, 13, 15][wf_idx] * 48,
			"terrain_set": 1,
			"terrain_mode": TileSet.TERRAIN_MODE_MATCH_SIDES,
			"peering_type": "waterfall",
			"animation_frames_count": 3,
			"animation_columns": 3,
			"animation_frame_duration": ANIMATION_DURATION,
			"tiles": tile_list,
			"custom_data": {
				"suggested_layer": "lower"
			}
		})

	return {
		"image": atlas_img,
		"autotiles": autotiles
	}


## Processes an A2 sheet (16T × 12T) containing 32 floor blocks (2T × 3T).
## Slices each block into 48 shapes into a compact derived atlas (48 tiles wide × 32 tiles high).
## Supports custom data: rpg_a2_group, rpg_a2_overlay, counter, counter_visual_offset.
## Returns {"image": Image, "autotiles": Array}.
static func generate_a2_derived_atlas(a2_path_or_img: Variant, tile_size: int = 32) -> Dictionary:
	var src_img := _resolve_image(a2_path_or_img)
	if src_img == null:
		return {"image": null, "autotiles": []}

	if tile_size <= 0:
		tile_size = 32

	var autotiles: Array = []
	# Left 4 columns (16 blocks of 2T x 3T) become 48-shape autotiles:
	# 4 blocks across * 6 tiles = 24 tiles wide; 4 blocks down * 8 tiles = 32 tiles high.
	# Right 4 columns (8T x 12T) are standard ground decorations/fences/1x1 tiles.
	# We place the 8T x 12T decorative tiles starting at x = 24 tiles.
	# Total atlas size: 32 tiles wide x 32 tiles high.
	var atlas_w_tiles := 32
	var atlas_h_tiles := 32
	var atlas_img := Image.create(
		atlas_w_tiles * tile_size,
		atlas_h_tiles * tile_size,
		false,
		src_img.get_format()
	)

	# 1. Left half (columns 0..3): 16 floor autotiles (4 rows x 4 cols of 2Tx3T)
	for r in range(4):
		for c in range(4):
			var block_rect := Rect2i(c * 2 * tile_size, r * 3 * tile_size, 2 * tile_size, 3 * tile_size)
			var shapes := reconstruct_floor_autotile(src_img, block_rect, tile_size)

			var block_tile_x := c * 6
			var block_tile_y := r * 8

			var a2_group := c
			var tile_list: Array[Dictionary] = []
			for s in range(48):
				var sc := s % 6
				var sr := s / 6
				var tile_coord := Vector2i(block_tile_x + sc, block_tile_y + sr)

				var px := tile_coord.x * tile_size
				var py := tile_coord.y * tile_size
				atlas_img.blit_rect(shapes[s], Rect2i(0, 0, tile_size, tile_size), Vector2i(px, py))

				tile_list.append({
					"shape_index": s,
					"atlas_coords": tile_coord,
					"peering_bits": AutotileTables.get_floor_peering_bits(s)
				})

			autotiles.append({
				"name": "a2_floor_r%d_c%d" % [r, c],
				"rpg_tile_base": 2816 + (r * 8 + c) * 48,
				"terrain_set": 0,
				"terrain_mode": TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES,
				"peering_type": "floor",
				"animation_frames_count": 1,
				"tiles": tile_list,
				"custom_data": {
					"rpg_a2_group": a2_group,
					"rpg_a2_overlay": false,
					"counter": false,
					"counter_visual_offset": 0.0,
					"suggested_layer": "lower"
				}
			})

	# 2. Right half (columns 4..7, 8T x 12T): standard 1x1 decorative & fence tiles
	var normal_tiles: Array[Dictionary] = []
	var right_src_rect := Rect2i(8 * tile_size, 0, 8 * tile_size, 12 * tile_size)
	atlas_img.blit_rect(src_img, right_src_rect, Vector2i(24 * tile_size, 0))

	for dy in range(12):
		for dx in range(8):
			var tile_coord := Vector2i(24 + dx, dy)
			# Column 6 and 7 in original sheet (dx >= 4) may have counter behavior
			var orig_col := 4 + dx / 2
			var is_counter := (orig_col in [6, 7])
			var counter_offset := float(tile_size) / 4.0 if is_counter else 0.0

			normal_tiles.append({
				"atlas_coords": tile_coord,
				"custom_data": {
					"rpg_a2_group": orig_col / 4,
					"rpg_a2_overlay": true,
					"counter": is_counter,
					"counter_visual_offset": counter_offset,
					"suggested_layer": "upper"
				}
			})

	# Add the normal tiles collection to autotiles list without terrain
	autotiles.append({
		"name": "a2_decorations",
		"terrain_set": -1, # No terrain peering bits
		"tiles": normal_tiles,
		"custom_data": {
			"suggested_layer": "upper"
		}
	})

	return {
		"image": atlas_img,
		"autotiles": autotiles
	}



## Processes an A3 sheet (16T × 8T) containing 32 wall/roof blocks (2T × 2T).
## Slices each block into 16 shapes into a compact derived atlas (32 tiles wide × 16 tiles high).
## Supports custom data: rpg_type ("roof" or "wall"), auto_shadow_right = true.
## Returns {"image": Image, "autotiles": Array}.
static func generate_a3_derived_atlas(a3_path_or_img: Variant, tile_size: int = 32) -> Dictionary:
	var src_img := _resolve_image(a3_path_or_img)
	if src_img == null:
		return {"image": null, "autotiles": []}

	if tile_size <= 0:
		tile_size = 32

	var autotiles: Array = []
	var atlas_w_tiles := 32  # 8 blocks * 4 tiles
	var atlas_h_tiles := 16  # 4 blocks * 4 tiles
	var atlas_img := Image.create(
		atlas_w_tiles * tile_size,
		atlas_h_tiles * tile_size,
		false,
		src_img.get_format()
	)

	for r in range(4):
		for c in range(8):
			var block_rect := Rect2i(c * 2 * tile_size, r * 2 * tile_size, 2 * tile_size, 2 * tile_size)
			var shapes := reconstruct_wall_autotile(src_img, block_rect, tile_size)

			var block_tile_x := c * 4
			var block_tile_y := r * 4

			var rpg_type := "roof" if (r % 2 == 0) else "wall"

			var tile_list: Array[Dictionary] = []
			for s in range(16):
				var sc := s % 4
				var sr := s / 4
				var tile_coord := Vector2i(block_tile_x + sc, block_tile_y + sr)

				var px := tile_coord.x * tile_size
				var py := tile_coord.y * tile_size
				atlas_img.blit_rect(shapes[s], Rect2i(0, 0, tile_size, tile_size), Vector2i(px, py))

				tile_list.append({
					"shape_index": s,
					"atlas_coords": tile_coord,
					"peering_bits": AutotileTables.get_wall_peering_bits(s)
				})

			autotiles.append({
				"name": "a3_%s_r%d_c%d" % [rpg_type, r, c],
				"rpg_tile_base": 4352 + (r * 8 + c) * 48,
				"terrain_set": 1,
				"terrain_mode": TileSet.TERRAIN_MODE_MATCH_SIDES,
				"peering_type": "wall",
				"animation_frames_count": 1,
				"tiles": tile_list,
				"custom_data": {
					"rpg_type": rpg_type,
					"auto_shadow_right": true,
					"suggested_layer": "lower"
				}
			})

	return {
		"image": atlas_img,
		"autotiles": autotiles
	}


## Processes an A4 sheet (16T × 15T) containing 3 vertical pairs of Wall Top (3T) + Wall Side (2T).
## Wall Top: 48 shapes via FLOOR_AUTOTILE_TABLE (TerrainSet 0, MATCH_CORNERS_AND_SIDES).
## Wall Side: 16 shapes via WALL_AUTOTILE_TABLE (TerrainSet 1, MATCH_SIDES).
## Returns {"image": Image, "autotiles": Array}.
static func generate_a4_derived_atlas(a4_path_or_img: Variant, tile_size: int = 32) -> Dictionary:
	var src_img := _resolve_image(a4_path_or_img)
	if src_img == null:
		return {"image": null, "autotiles": []}

	if tile_size <= 0:
		tile_size = 32

	var autotiles: Array = []
	# Wall Top: 8 cols * 6 tiles = 48 tiles wide; 3 rows * 8 tiles = 24 tiles high.
	# Wall Side: 8 cols * 4 tiles = 32 tiles wide; 3 rows * 4 tiles = 12 tiles high (placed at y=24).
	# Total atlas size: 48 tiles wide × 36 tiles high.
	var atlas_w_tiles := 48
	var atlas_h_tiles := 36
	var atlas_img := Image.create(
		atlas_w_tiles * tile_size,
		atlas_h_tiles * tile_size,
		false,
		src_img.get_format()
	)

	for p in range(3):
		for c in range(8):
			# 1. Wall Top (3T high)
			var top_rect := Rect2i(c * 2 * tile_size, p * 5 * tile_size, 2 * tile_size, 3 * tile_size)
			var top_shapes := reconstruct_floor_autotile(src_img, top_rect, tile_size)

			var top_base_x := c * 6
			var top_base_y := p * 8

			var top_tile_list: Array[Dictionary] = []
			for s in range(48):
				var sc := s % 6
				var sr := s / 6
				var tile_coord := Vector2i(top_base_x + sc, top_base_y + sr)

				var px := tile_coord.x * tile_size
				var py := tile_coord.y * tile_size
				atlas_img.blit_rect(top_shapes[s], Rect2i(0, 0, tile_size, tile_size), Vector2i(px, py))

				top_tile_list.append({
					"shape_index": s,
					"atlas_coords": tile_coord,
					"peering_bits": AutotileTables.get_floor_peering_bits(s)
				})

			autotiles.append({
				"name": "a4_wall_top_p%d_c%d" % [p, c],
				"rpg_tile_base": 5888 + (p * 16 + c) * 48,
				"terrain_set": 0,
				"terrain_mode": TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES,
				"peering_type": "floor",
				"is_wall_top": true,
				"animation_frames_count": 1,
				"tiles": top_tile_list,
				"custom_data": {
					"rpg_type": "wall_top",
					"suggested_layer": "lower"
				}
			})

			# 2. Wall Side (2T high)
			var side_rect := Rect2i(c * 2 * tile_size, (p * 5 + 3) * tile_size, 2 * tile_size, 2 * tile_size)
			var side_shapes := reconstruct_wall_autotile(src_img, side_rect, tile_size)

			var side_base_x := c * 4
			var side_base_y := 24 + p * 4

			var side_tile_list: Array[Dictionary] = []
			for s in range(16):
				var sc := s % 4
				var sr := s / 4
				var tile_coord := Vector2i(side_base_x + sc, side_base_y + sr)

				var px := tile_coord.x * tile_size
				var py := tile_coord.y * tile_size
				atlas_img.blit_rect(side_shapes[s], Rect2i(0, 0, tile_size, tile_size), Vector2i(px, py))

				side_tile_list.append({
					"shape_index": s,
					"atlas_coords": tile_coord,
					"peering_bits": AutotileTables.get_wall_peering_bits(s)
				})

			autotiles.append({
				"name": "a4_wall_side_p%d_c%d" % [p, c],
				"rpg_tile_base": 5888 + (p * 16 + 8 + c) * 48,
				"terrain_set": 1,
				"terrain_mode": TileSet.TERRAIN_MODE_MATCH_SIDES,
				"peering_type": "wall",
				"is_wall_top": false,
				"animation_frames_count": 1,
				"tiles": side_tile_list,
				"custom_data": {
					"rpg_type": "wall",
					"auto_shadow_right": true,
					"suggested_layer": "lower"
				}
			})

	return {
		"image": atlas_img,
		"autotiles": autotiles
	}
