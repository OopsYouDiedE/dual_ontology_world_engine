extends RefCounted

const PARQUET = preload("res://rendering/shaders/surfaces/parquet.gdshader")
const PLANK = preload("res://rendering/shaders/surfaces/interior_plank.gdshader")
const TILE = preload("res://rendering/shaders/surfaces/tile_floor.gdshader")
const STONE = preload("res://rendering/shaders/surfaces/interior_stone_floor.gdshader")

## DV supplies the floor identity; this maps that identity to reusable visual finishes.
static func floor(entry: Dictionary) -> Material:
	var material := ShaderMaterial.new()
	var floor_id: String = str(entry.get("floor", "parquet"))
	match floor_id:
		"parquet", "herringbone":
			material.shader = PARQUET
		"plank", "oak", "honey":
			material.shader = PLANK
			material.set_shader_parameter("wood_tone", 1 if floor_id == "honey" else 0)
		"whitetile", "tile":
			material.shader = TILE
			material.set_shader_parameter("base_color", Color("b5b19b") if floor_id == "whitetile" else Color("a6aa9a"))
			material.set_shader_parameter("diamond_insets", floor_id == "whitetile")
		"quarry", "flagstone", "slate", "stone", "gravel":
			material.shader = STONE
			var modes := {"quarry": 0, "flagstone": 1, "slate": 2, "stone": 3, "gravel": 4}
			material.set_shader_parameter("stone_kind", modes[floor_id])
		_:
			material.shader = PARQUET
	return material
