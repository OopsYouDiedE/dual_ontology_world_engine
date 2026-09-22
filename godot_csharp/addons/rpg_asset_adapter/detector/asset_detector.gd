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
	RPG_TILESET_METADATA,
	TAGGED_ANIMATION
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

	# 0. Tagged slice specification from start: ^w_(\d+)_h_(\d+)_([vh])_(\d+)_(\d+)
	var tag_match := RegexRulesScript.regex_search(RegexRulesScript.REGEX_SLICE_TAG, stem)
	if tag_match != null:
		kind = AssetKind.TAGGED_ANIMATION
		var w_tile := int(tag_match.get_string(1))
		var h_tile := int(tag_match.get_string(2))
		var orientation := tag_match.get_string(3).to_lower()
		var frames := int(tag_match.get_string(4))
		var count := int(tag_match.get_string(5))
		var sub_name := tag_match.get_string(6) if tag_match.get_group_count() >= 6 else ""
		extra["slice_tag"] = {
			"w_tile": w_tile,
			"h_tile": h_tile,
			"orientation": orientation,
			"frames": frames,
			"count": count,
			"name": sub_name
		}
		extra["is_single"] = (count == 1)

	# 1. Tilesets metadata JSON
	elif file_name.to_lower() == "tilesets.json":
		kind = AssetKind.RPG_TILESET_METADATA
		return AssetInfo.new(kind, path, DEFAULT_TILE_SIZE, extra, "")

	# 2. Object or character with both ! and $ prefixes (!$, $!)
	elif stem.begins_with("!$") or stem.begins_with("$!"):
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

	# Enforce unified rule validation within the same image
	var consistency_warn := validate_asset_consistency(path, kind, tile_size)
	if consistency_warn != "":
		warning = (warning + "; " + consistency_warn).strip_edges().trim_prefix("; ")

	return AssetInfo.new(kind, path, tile_size, extra, warning)


## Enforces unified rule validation within the same image.
## Ensures image dimensions strictly adhere to the identified rule without mixed slicing.
static func validate_asset_consistency(path: String, kind: AssetKind, tile_size: int) -> String:
	if kind in [AssetKind.UNKNOWN, AssetKind.RPG_TILESET_METADATA]:
		return ""

	var global_path := ProjectSettings.globalize_path(path) if path.begins_with("res://") else path
	if not FileAccess.file_exists(global_path):
		return ""

	var img := Image.load_from_file(global_path)
	if img == null:
		return "Failed to load image for consistency check: %s" % path.get_file()

	var w := img.get_width()
	var h := img.get_height()

	# 1. Tile grid divisibility check
	if tile_size > 0:
		if w % tile_size != 0 or h % tile_size != 0:
			return "Image %s (%dx%d) does not align with tile_size %d" % [path.get_file(), w, h, tile_size]

	var cols := w / tile_size if tile_size > 0 else 0
	var rows := h / tile_size if tile_size > 0 else 0

	# 2. Strict grid ratios for standard tileset sheets
	match kind:
		AssetKind.RPG_A1, AssetKind.RPG_A2:
			if cols != 16 or rows != 12:
				return "A1/A2 expects 16x12 tiles, got %dx%d" % [cols, rows]
		AssetKind.RPG_A3:
			if cols != 16 or rows != 8:
				return "A3 expects 16x8 tiles, got %dx%d" % [cols, rows]
		AssetKind.RPG_A4:
			if cols != 16 or rows != 15:
				return "A4 expects 16x15 tiles, got %dx%d" % [cols, rows]
		AssetKind.RPG_A5:
			if cols != 8 or rows != 16:
				return "A5 expects 8x16 tiles, got %dx%d" % [cols, rows]
		AssetKind.RPG_B, AssetKind.RPG_C, AssetKind.RPG_D, AssetKind.RPG_E, AssetKind.KR_F_EXTENSION:
			if cols != 16 or rows != 16:
				return "B-F tileset expects 16x16 tiles, got %dx%d" % [cols, rows]

		# 3. Animation and character rules: must divide by 3 columns and 4 rows
		AssetKind.RPG_CHARACTER_SINGLE, AssetKind.RPG_OBJECT_SINGLE, AssetKind.DOOR, AssetKind.TREASURE_CHEST, AssetKind.GENERIC_ANIMATION:
			if cols % 3 != 0:
				return "Animation sheet %s columns (%d) must be multiple of 3" % [path.get_file(), cols]
			if rows % 4 != 0:
				return "Animation sheet %s rows (%d) must be multiple of 4" % [path.get_file(), rows]

		AssetKind.TAGGED_ANIMATION:
			var tag_m := RegexRulesScript.regex_search(RegexRulesScript.REGEX_SLICE_TAG, path.get_file().get_basename())
			if tag_m != null:
				var wt := int(tag_m.get_string(1))
				var ht := int(tag_m.get_string(2))
				if tile_size > 0 and (wt > 0 and w % (wt * tile_size) != 0):
					return "Tagged animation width %d not divisible by frame width %d" % [w, wt * tile_size]
				if tile_size > 0 and (ht > 0 and h % (ht * tile_size) != 0):
					return "Tagged animation height %d not divisible by frame height %d" % [h, ht * tile_size]

	return ""


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

	# Tagged animation derives tile size mathematically
	if kind == AssetKind.TAGGED_ANIMATION:
		var tag_m := RegexRulesScript.regex_search(RegexRulesScript.REGEX_SLICE_TAG, stem)
		if tag_m != null:
			var wt := int(tag_m.get_string(1))
			var ht := int(tag_m.get_string(2))
			var ori := tag_m.get_string(3).to_lower()
			var frm := int(tag_m.get_string(4))
			var cnt := int(tag_m.get_string(5))
			var global_p := ProjectSettings.globalize_path(path) if path.begins_with("res://") else path
			if FileAccess.file_exists(global_p):
				var img := Image.load_from_file(global_p)
				if img != null:
					var ih := img.get_height()
					var iw := img.get_width()
					var ts := 0
					for cand in [32, 48, 16, 64]:
						var sw: int = (wt * cand) if ori == "v" else (wt * cand * frm)
						var sh: int = (ht * cand * frm) if ori == "v" else (ht * cand)
						if sw > 0 and sh > 0 and iw % sw == 0 and ih % sh == 0:
							ts = cand
							break
					if ts > 0:
						return {"size": ts, "warning": ""}
					elif ht * frm > 0:
						return {"size": ih / (ht * frm), "warning": ""}

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
