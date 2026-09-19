@tool
class_name AutotileTables
extends RefCounted

## RPG Maker standard autotile lookup tables and Godot 4 terrain peering bit mappings.

## 48 shapes for floor autotiles (A1 water / A2 ground / A4 wall top).
## Each shape consists of 4 quarter-tile offsets [top-left, top-right, bottom-left, bottom-right]
## in half-tile units across the standard 2x3 block (x in 0..3, y in 0..5).
const FLOOR_AUTOTILE_TABLE: Array = [
    [[2, 4], [1, 4], [2, 3], [1, 3]], # Shape 0
    [[2, 0], [1, 4], [2, 3], [1, 3]], # Shape 1
    [[2, 4], [3, 0], [2, 3], [1, 3]], # Shape 2
    [[2, 0], [3, 0], [2, 3], [1, 3]], # Shape 3
    [[2, 4], [1, 4], [2, 3], [3, 1]], # Shape 4
    [[2, 0], [1, 4], [2, 3], [3, 1]], # Shape 5
    [[2, 4], [3, 0], [2, 3], [3, 1]], # Shape 6
    [[2, 0], [3, 0], [2, 3], [3, 1]], # Shape 7
    [[2, 4], [1, 4], [2, 1], [1, 3]], # Shape 8
    [[2, 0], [1, 4], [2, 1], [1, 3]], # Shape 9
    [[2, 4], [3, 0], [2, 1], [1, 3]], # Shape 10
    [[2, 0], [3, 0], [2, 1], [1, 3]], # Shape 11
    [[2, 4], [1, 4], [2, 1], [3, 1]], # Shape 12
    [[2, 0], [1, 4], [2, 1], [3, 1]], # Shape 13
    [[2, 4], [3, 0], [2, 1], [3, 1]], # Shape 14
    [[2, 0], [3, 0], [2, 1], [3, 1]], # Shape 15
    [[0, 4], [1, 4], [0, 3], [1, 3]], # Shape 16
    [[0, 4], [3, 0], [0, 3], [1, 3]], # Shape 17
    [[0, 4], [1, 4], [0, 3], [3, 1]], # Shape 18
    [[0, 4], [3, 0], [0, 3], [3, 1]], # Shape 19
    [[2, 2], [1, 2], [2, 3], [1, 3]], # Shape 20
    [[2, 2], [1, 2], [2, 3], [3, 1]], # Shape 21
    [[2, 2], [1, 2], [2, 1], [1, 3]], # Shape 22
    [[2, 2], [1, 2], [2, 1], [3, 1]], # Shape 23
    [[2, 4], [3, 4], [2, 3], [3, 3]], # Shape 24
    [[2, 4], [3, 4], [2, 1], [3, 3]], # Shape 25
    [[2, 0], [3, 4], [2, 3], [3, 3]], # Shape 26
    [[2, 0], [3, 4], [2, 1], [3, 3]], # Shape 27
    [[2, 4], [1, 4], [2, 5], [1, 5]], # Shape 28
    [[2, 0], [1, 4], [2, 5], [1, 5]], # Shape 29
    [[2, 4], [3, 0], [2, 5], [1, 5]], # Shape 30
    [[2, 0], [3, 0], [2, 5], [1, 5]], # Shape 31
    [[0, 4], [3, 4], [0, 3], [3, 3]], # Shape 32
    [[2, 2], [1, 2], [2, 5], [1, 5]], # Shape 33
    [[0, 2], [1, 2], [0, 3], [1, 3]], # Shape 34
    [[0, 2], [1, 2], [0, 3], [3, 1]], # Shape 35
    [[2, 2], [3, 2], [2, 3], [3, 3]], # Shape 36
    [[2, 2], [3, 2], [2, 1], [3, 3]], # Shape 37
    [[2, 4], [3, 4], [2, 5], [3, 5]], # Shape 38
    [[2, 0], [3, 4], [2, 5], [3, 5]], # Shape 39
    [[0, 4], [1, 4], [0, 5], [1, 5]], # Shape 40
    [[0, 4], [3, 0], [0, 5], [1, 5]], # Shape 41
    [[0, 2], [3, 2], [0, 3], [3, 3]], # Shape 42
    [[0, 2], [1, 2], [0, 5], [1, 5]], # Shape 43
    [[0, 4], [3, 4], [0, 5], [3, 5]], # Shape 44
    [[2, 2], [3, 2], [2, 5], [3, 5]], # Shape 45
    [[0, 2], [3, 2], [0, 5], [3, 5]], # Shape 46
    [[0, 0], [1, 0], [0, 1], [1, 1]], # Shape 47
]

## 16 shapes for wall autotiles (A3 walls & roofs / A4 wall sides).
## Offsets in half-tile units across 2x2 wall block (x in 0..3, y in 0..3).
const WALL_AUTOTILE_TABLE: Array = [
	[[2, 2], [1, 2], [2, 1], [1, 1]], # Shape 0
	[[0, 2], [1, 2], [0, 1], [1, 1]], # Shape 1
	[[2, 0], [1, 0], [2, 1], [1, 1]], # Shape 2
	[[0, 0], [1, 0], [0, 1], [1, 1]], # Shape 3
	[[2, 2], [3, 2], [2, 1], [3, 1]], # Shape 4
	[[0, 2], [3, 2], [0, 1], [3, 1]], # Shape 5
	[[2, 0], [3, 0], [2, 1], [3, 1]], # Shape 6
	[[0, 0], [3, 0], [0, 1], [3, 1]], # Shape 7
	[[2, 2], [1, 2], [2, 3], [1, 3]], # Shape 8
	[[0, 2], [1, 2], [0, 3], [1, 3]], # Shape 9
	[[2, 0], [1, 0], [2, 3], [1, 3]], # Shape 10
	[[0, 0], [1, 0], [0, 3], [1, 3]], # Shape 11
	[[2, 2], [3, 2], [2, 3], [3, 3]], # Shape 12
	[[0, 2], [3, 2], [0, 3], [3, 3]], # Shape 13
	[[2, 0], [3, 0], [2, 3], [3, 3]], # Shape 14
	[[0, 0], [3, 0], [0, 3], [3, 3]], # Shape 15
]


## 4 shapes for waterfall autotiles (A1 waterfalls).
## Offsets in half-tile units across 2x1 waterfall block (x in 0..3, y in 0..1).
const WATERFALL_AUTOTILE_TABLE: Array = [
    [[2, 0], [1, 0], [2, 1], [1, 1]], # Shape 0
    [[0, 0], [1, 0], [0, 1], [1, 1]], # Shape 1
    [[2, 0], [3, 0], [2, 1], [3, 1]], # Shape 2
    [[0, 0], [3, 0], [0, 1], [3, 1]], # Shape 3
]

## Godot 4 peering bits for floor autotiles (TERRAIN_MODE_MATCH_CORNERS_AND_SIDES).
## Maps shape index 0..47 to an Array of TileSet.CELL_NEIGHBOR_* constants.
const FLOOR_PEERING_BITS: Array = [
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 0
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 1
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 2
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 3
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 4
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 5
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 6
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 7
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 8
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 9
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 10
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 11
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 12
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 13
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 14
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 15
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 16
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 17
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 18
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 19
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 20
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 21
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 22
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 23
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 24
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 25
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 26
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 27
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 28
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 29
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 30
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 31
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 32
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 33
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE], # Shape 34
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE], # Shape 35
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 36
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 37
    [TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 38
    [TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 39
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE, TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER], # Shape 40
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 41
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE], # Shape 42
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE], # Shape 43
    [TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 44
    [TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 45
    [], # Shape 46
    [], # Shape 47
]

## Godot 4 peering bits for wall autotiles (TERRAIN_MODE_MATCH_SIDES).
## Maps shape index 0..15 to an Array of TileSet.CELL_NEIGHBOR_* constants.
const WALL_PEERING_BITS: Array = [
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 0
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 1
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 2
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE], # Shape 3
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 4
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 5
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 6
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE], # Shape 7
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 8
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 9
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 10
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE], # Shape 11
    [TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 12
    [TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 13
    [TileSet.CELL_NEIGHBOR_LEFT_SIDE], # Shape 14
    [], # Shape 15
]

## Godot 4 peering bits for waterfall autotiles (TERRAIN_MODE_MATCH_SIDES).
## Maps shape index 0..3 to an Array of TileSet.CELL_NEIGHBOR_* constants.
const WATERFALL_PEERING_BITS: Array = [
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 0
    [TileSet.CELL_NEIGHBOR_RIGHT_SIDE, TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 1
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_LEFT_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 2
    [TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, TileSet.CELL_NEIGHBOR_TOP_SIDE], # Shape 3
]

static func get_floor_quarter_coords(shape: int) -> Array:
    if shape >= 0 and shape < FLOOR_AUTOTILE_TABLE.size():
        return FLOOR_AUTOTILE_TABLE[shape]
    return []


static func get_wall_quarter_coords(shape: int) -> Array:
    if shape >= 0 and shape < WALL_AUTOTILE_TABLE.size():
        return WALL_AUTOTILE_TABLE[shape]
    return []


static func get_waterfall_quarter_coords(shape: int) -> Array:
    if shape >= 0 and shape < WATERFALL_AUTOTILE_TABLE.size():
        return WATERFALL_AUTOTILE_TABLE[shape]
    return []


static func get_floor_peering_bits(shape: int) -> Array:
    if shape >= 0 and shape < FLOOR_PEERING_BITS.size():
        return FLOOR_PEERING_BITS[shape]
    return []


static func get_wall_peering_bits(shape: int) -> Array:
    if shape >= 0 and shape < WALL_PEERING_BITS.size():
        return WALL_PEERING_BITS[shape]
    return []


static func get_waterfall_peering_bits(shape: int) -> Array:
    if shape >= 0 and shape < WATERFALL_PEERING_BITS.size():
        return WATERFALL_PEERING_BITS[shape]
    return []
