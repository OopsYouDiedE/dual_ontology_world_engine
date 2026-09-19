@tool
class_name AssetDetector
extends RefCounted

## Asset detector engine for RPG Maker and KR asset suites.
## Performs deterministic categorization and tile size inference.

const RegexRulesScript = preload("res://addons/rpg_asset_adapter/detector/regex_rules.gd")

enum TileSize {
	SIZE_16 = 16,
	SIZE_32 = 32,
	SIZE_48 = 48,
	SIZE_64 = 64
}

const DEFAULT_TILE_SIZE := TileSize.SIZE_48

enum DetectMode {
	AUTO,
	RPG_MAKER,
	KR_NON_RM,
	RPG_CHARACTER,
	KR_EXTENSION,
	GENERIC_ANIMATION,
	MANUAL
}

enum AssetKind {
	UNKNOWN,
	RPG_A1,
	RPG_A2,
	RPG_A3,
	RPG_A4,
	RPG_A5,
	RPG_B,
	RPG_C,
	RPG_D,
	RPG_E,
	KR_NON_RM_A1,
	KR_NON_RM_A2,
	KR_NON_RM_A3,
	KR_NON_RM_A4,
	RPG_CHARACTER_SINGLE,
	RPG_CHARACTER_SHEET,
	RPG_OBJECT_SINGLE,
	RPG_OBJECT_SHEET,
	DOOR,
	TREASURE_CHEST,
	KR_F_EXTENSION,
	GENERIC_ANIMATION,
	RPG_TILESET_METADATA
}


class AssetInfo extends RefCounted:
	var kind: AssetKind = AssetKind.UNKNOWN
	var path: String = ""
	var tile_size: int = 48
	var extra: Dictionary = {}
	var warning: String = ""

	func _init(
		p_kind: AssetKind = AssetKind.UNKNOWN,
		p_path: String = "",
		p_tile_size: int = 48,
		p_extra: Dictionary = {},
		p_warning: String = ""
	) -> void:
		kind = p_kind
		path = p_path
		tile_size = p_tile_size
		extra = p_extra
		warning = p_warning

	func _to_string() -> String:
		return "[AssetInfo kind=%d path=%s size=%d warning=%s extra=%s]" % [
			kind, path.get_file(), tile_size, warning, str(extra)
		]


## Detects asset kind, properties, and inferred tile size according to strict priority rules.
static func detect_asset(
	path: String,
	mode: DetectMode = DetectMode.AUTO,
	manual_tile_size: int = 0
) -> AssetInfo:
	var file_name := path.get_file()
	var stem := file_name.get_basename()
	var kind := AssetKind.UNKNOWN
	var extra: Dictionary = {}

	# 1. Tilesets metadata JSON
	if file_name.to_lower() == "tilesets.json":
		kind = AssetKind.RPG_TILESET_METADATA
		return AssetInfo.new(kind, path, DEFAULT_TILE_SIZE, extra, "")

	# 2. Object or character with both ! and $ prefixes (!$, $!)
	if stem.begins_with("!$") or stem.begins_with("$!"):
		var after_prefix := stem.substr(2)
		var is_multi := not after_prefix.begins_with("single") and (
			RegexRulesScript.regex_search(RegexRulesScript.REGEX_DOOR, stem) != null and (
				stem.findn("small") != -1 or stem.findn("large") != -1
			)
		)
		extra["is_single"] = not is_multi
		extra["has_exclamation"] = true
		extra["has_dollar"] = true
		extra["rpg_object_alignment"] = true
		if RegexRulesScript.regex_search(RegexRulesScript.REGEX_DOOR, stem):
			kind = AssetKind.DOOR
		elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_CHEST, stem):
			kind = AssetKind.TREASURE_CHEST
		elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_GENERIC_ANIMATION, stem):
			kind = AssetKind.GENERIC_ANIMATION
		else:
			kind = AssetKind.RPG_OBJECT_SINGLE if extra["is_single"] else AssetKind.RPG_OBJECT_SHEET

	# 3. Single character/object sheet $
	elif stem.begins_with("$"):
		extra["is_single"] = true
		extra["has_dollar"] = true
		if RegexRulesScript.regex_search(RegexRulesScript.REGEX_DOOR, stem):
			kind = AssetKind.DOOR
		elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_CHEST, stem):
			kind = AssetKind.TREASURE_CHEST
		elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_GENERIC_ANIMATION, stem):
			kind = AssetKind.GENERIC_ANIMATION
		else:
			kind = AssetKind.RPG_CHARACTER_SINGLE

	# 4. Object sheet !
	elif stem.begins_with("!"):
		extra["is_single"] = false
		extra["has_exclamation"] = true
		extra["rpg_object_alignment"] = true
		if RegexRulesScript.regex_search(RegexRulesScript.REGEX_DOOR, stem):
			kind = AssetKind.DOOR
		elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_CHEST, stem):
			kind = AssetKind.TREASURE_CHEST
		elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_GENERIC_ANIMATION, stem):
			kind = AssetKind.GENERIC_ANIMATION
		else:
			kind = AssetKind.RPG_OBJECT_SHEET

	# 5. KR non-rm A1
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_NON_RM_A1, stem):
		var m_non_rm_a1 := RegexRulesScript.regex_search(RegexRulesScript.REGEX_NON_RM_A1, stem)
		kind = AssetKind.KR_NON_RM_A1
		var sq := int(m_non_rm_a1.get_string(1))
		extra["square"] = sq
		extra["animation_frame"] = sq - 1

	# 6. KR non-rm A2
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_NON_RM_A2, stem):
		kind = AssetKind.KR_NON_RM_A2

	# 7. KR non-rm A3
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_NON_RM_A3, stem):
		kind = AssetKind.KR_NON_RM_A3

	# 8. KR non-rm A4
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_NON_RM_A4, stem):
		kind = AssetKind.KR_NON_RM_A4

	# 9. Standard RPG A1
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_A1, stem):
		kind = AssetKind.RPG_A1

	# 10. Standard RPG A2
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_A2, stem):
		kind = AssetKind.RPG_A2

	# 11. Standard RPG A3
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_A3, stem):
		kind = AssetKind.RPG_A3

	# 12. Standard RPG A4
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_A4, stem):
		kind = AssetKind.RPG_A4

	# 13. Standard RPG A5
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_A5, stem):
		kind = AssetKind.RPG_A5

	# 14. Standard RPG B..E
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_B, stem):
		kind = AssetKind.RPG_B
		extra["suggested_layer"] = "upper"
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_C, stem):
		kind = AssetKind.RPG_C
		extra["suggested_layer"] = "upper"
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_D, stem):
		kind = AssetKind.RPG_D
		extra["suggested_layer"] = "upper"
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_E, stem):
		kind = AssetKind.RPG_E
		extra["suggested_layer"] = "upper"

	# 15. Door keyword (with multi-character sheet fallback)
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_DOOR, stem):
		kind = AssetKind.DOOR
		extra["is_single"] = false

	# 16. Chest keyword (with object sheet fallback)
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_CHEST, stem):
		kind = AssetKind.TREASURE_CHEST
		extra["is_single"] = false

	# 17. KR F extension
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_F, stem):
		kind = AssetKind.KR_F_EXTENSION
		var m_var := RegexRulesScript.regex_search(RegexRulesScript.REGEX_VARIANT, stem)
		if m_var:
			var var_raw := m_var.get_string(1).to_lower()
			var variant := "Gray" if (var_raw == "grey" or var_raw == "gray") else var_raw.capitalize()
			var start := m_var.get_start()
			var group := stem.substr(0, start).rstrip("_- ")
			extra["variant_group"] = group
			extra["variant"] = variant
		else:
			extra["variant_group"] = stem
			extra["variant"] = ""

	# 18. Generic Animation
	elif RegexRulesScript.regex_search(RegexRulesScript.REGEX_GENERIC_ANIMATION, stem):
		kind = AssetKind.GENERIC_ANIMATION
		extra["requires_animation_profile"] = true

	# 19. UNKNOWN (or mode overrides)
	else:
		if mode == DetectMode.RPG_CHARACTER:
			kind = AssetKind.RPG_CHARACTER_SHEET
			extra["is_single"] = false
		elif mode == DetectMode.GENERIC_ANIMATION:
			kind = AssetKind.GENERIC_ANIMATION
			extra["requires_animation_profile"] = true
		else:
			kind = AssetKind.UNKNOWN

	# Infer or resolve tile size
	var size_info := detect_tile_size(path, kind, manual_tile_size)
	var tile_size: int = size_info["size"]
	var warning: String = size_info["warning"]

	return AssetInfo.new(kind, path, tile_size, extra, warning)


## Resolves tile size following the priority:
## Manual size -> Filename (px or WxH) -> Sheet ratio inference -> Default 48 with warning if unsupported.
static func detect_tile_size(path: String, kind: AssetKind, manual_size: int = 0) -> Dictionary:
	# 1. Manual size check
	if manual_size > 0:
		if manual_size in [16, 32, 48, 64]:
			return {"size": manual_size, "warning": ""}
		return {
			"size": DEFAULT_TILE_SIZE,
			"warning": "Unsupported manual tile size: %d, fallback to 48" % manual_size
		}

	# 2. Filename check
	var file_name := path.get_file()
	var stem := file_name.get_basename()

	var match_wxh := RegexRulesScript.regex_search(RegexRulesScript.REGEX_SIZE_WXH, stem)
	if match_wxh:
		var w_val := int(match_wxh.get_string(1))
		var h_val := int(match_wxh.get_string(2))
		if w_val == h_val:
			if w_val in [16, 32, 48, 64]:
				return {"size": w_val, "warning": ""}
			return {
				"size": DEFAULT_TILE_SIZE,
				"warning": "Unsupported tile size in filename: %dx%d, fallback to 48" % [w_val, h_val]
			}

	var match_px := RegexRulesScript.regex_search(RegexRulesScript.REGEX_SIZE_PX, stem)
	if match_px:
		var px_val := int(match_px.get_string(1))
		if px_val in [16, 32, 48, 64]:
			return {"size": px_val, "warning": ""}
		return {
			"size": DEFAULT_TILE_SIZE,
			"warning": "Unsupported tile size in filename: %dpx, fallback to 48" % px_val
		}

	# 3. Sheet ratio inference from Image dimensions
	var expected_w_tiles := 0
	var expected_h_tiles := 0

	match kind:
		AssetKind.RPG_A1, AssetKind.RPG_A2:
			expected_w_tiles = 16
			expected_h_tiles = 12
		AssetKind.RPG_A3:
			expected_w_tiles = 16
			expected_h_tiles = 8
		AssetKind.RPG_A4:
			expected_w_tiles = 16
			expected_h_tiles = 15
		AssetKind.RPG_A5:
			expected_w_tiles = 8
			expected_h_tiles = 16
		AssetKind.RPG_B, AssetKind.RPG_C, AssetKind.RPG_D, AssetKind.RPG_E:
			expected_w_tiles = 16
			expected_h_tiles = 16

	if expected_w_tiles > 0 and expected_h_tiles > 0:
		var global_path := ProjectSettings.globalize_path(path) if path.begins_with("res://") else path
		if FileAccess.file_exists(global_path):
			var img := Image.load_from_file(global_path)
			if img:
				var img_w := img.get_width()
				var img_h := img.get_height()
				if img_w % expected_w_tiles == 0 and img_h % expected_h_tiles == 0:
					var tw := img_w / expected_w_tiles
					var th := img_h / expected_h_tiles
					if tw == th:
						if tw in [16, 32, 48, 64]:
							return {"size": tw, "warning": ""}
						return {
							"size": DEFAULT_TILE_SIZE,
							"warning": "Unsupported tile size: %d, fallback to 48" % tw
						}

	# 4. Default fallback
	return {"size": DEFAULT_TILE_SIZE, "warning": ""}
