"""Publish the Highlights 4x4 material sheet as 32x32 terrain tiles.

The source contains material swatches, not hand-authored terrain transitions.
This publisher extracts each complete swatch, resamples it to 32x32 with
nearest-neighbor, and derives floor/water blob47 and wall side-only Wang16.
Every alpha mask retains its16x16 centre; cardinal arms and independent8x8
corners carry the actual connectivity. The manifest records this derivation;
the source sheet does not contain authored transition variants.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import struct
import subprocess
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / "godot_csharp"
SOURCE_ARCHIVE = ROOT / "mcp/asset_pipeline/sources/Internal/Props/Highlights/Terrain/materials-4x4.png"
RUNTIME = GAME / "ShareAsset/Internal/Props/Highlights/Terrain"
CELL = 32
COLUMNS = 8

# Row-major order in Astra's 4x4 source sheet.
MATERIALS = [
    "ivory_tile", "teal_tile", "walnut", "nanmu",
    "basalt", "obsidian", "tuff", "iron",
    "tidal_water", "shallow_water", "source_essence", "wet_stone",
    "ivory_wall", "cinnabar_wall", "teal_wall", "stone_paving",
]
FAMILIES = {
    "floor": {"source_id": 0, "terrain_set": 0, "materials": ["ivory_tile", "teal_tile", "walnut", "nanmu", "basalt", "obsidian", "iron", "wet_stone", "stone_paving"]},
    "wall": {"source_id": 1, "terrain_set": 1, "materials": ["tuff", "ivory_wall", "cinnabar_wall", "teal_wall"]},
    "water": {"source_id": 2, "terrain_set": 2, "materials": MATERIALS[8:11]},
}

# TileSet.CellNeighbor numeric values in Godot 4: right, bottom-right,
# bottom, bottom-left, left, top-left, top, top-right.
GODOT_BITS = {
    "N": 12, "E": 0, "S": 4, "W": 8,
    "NE": 15, "SE": 3, "SW": 7, "NW": 11,
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def png_info(path: Path) -> dict:
    header = path.read_bytes()[:29]
    if len(header) != 29 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError(f"Not a valid PNG header: {path}")
    width, height, bit_depth, color_type, _, _, _ = struct.unpack(">IIBBBBB", header[16:29])
    color_names = {0: "grayscale", 2: "RGB", 3: "indexed", 4: "grayscale-alpha", 6: "RGBA"}
    return {"size_px": [width, height], "bit_depth": bit_depth, "color_type": color_names.get(color_type, str(color_type))}


def externalize_textures(resource_path: Path) -> None:
    """Keep atlases in their PNGs instead of embedding Image byte arrays."""
    text = resource_path.read_text(encoding="utf-8")
    blocks = re.split(r"\n(?=\[)", text)
    source_ids = dict(re.findall(r'sources/(\d+) = SubResource\("([^"]+)"\)', text))
    replacements = {}
    external = []
    for family, config in FAMILIES.items():
        atlas_id = source_ids.get(str(config["source_id"]))
        if atlas_id is None:
            raise ValueError(f"Missing {family} atlas in published TileSet")
        atlas = next((block for block in blocks if block.startswith('[sub_resource type="TileSetAtlasSource"') and f'id="{atlas_id}"' in block.splitlines()[0]), None)
        if atlas is None:
            raise ValueError(f"Missing atlas resource {atlas_id}")
        texture = re.search(r'texture = SubResource\("([^"]+)"\)', atlas)
        if texture is None:
            raise ValueError(f"Missing embedded texture for {family}")
        external_id = f"highlight_{family}"
        replacements[f'SubResource("{texture.group(1)}")'] = f'ExtResource("{external_id}")'
        path = (RUNTIME / f"{family}.png").relative_to(GAME).as_posix()
        external.append(f'[ext_resource type="Texture2D" path="res://{path}" id="{external_id}"]\n')
    kept = []
    for block in blocks[1:]:
        if block.startswith(('[sub_resource type="Image"', '[sub_resource type="ImageTexture"')):
            continue
        for old, new in replacements.items():
            block = block.replace(old, new)
        kept.append(block)
    header = re.sub(r' load_steps=\d+', '', blocks[0]).replace('[gd_resource type="TileSet"', '[gd_resource type="TileSet" load_steps=7')
    resource_path.write_text('\n'.join([header, *external, *kept]), encoding="utf-8")
    manifest_path = resource_path.parent / "asset-manifest.json"
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["texture_storage"] = "external-png"
        manifest["textures"] = {
            family: {
                "path": f"res://ShareAsset/Internal/Props/Highlights/Terrain/{family}.png",
                "sha256": sha256(RUNTIME / f"{family}.png"),
                **png_info(RUNTIME / f"{family}.png"),
            }
            for family in FAMILIES
        }
        manifest["tileset_sha256"] = sha256(resource_path)
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


def topology_masks() -> list[dict]:
    """Return all 47 blob masks: corners are legal only between matching sides."""
    sides = [("N", 1), ("E", 2), ("S", 4), ("W", 8)]
    corners = [("NE", 16, 1 | 2), ("SE", 32, 2 | 4), ("SW", 64, 4 | 8), ("NW", 128, 8 | 1)]
    masks = []
    for side_mask in range(16):
        active_sides = [name for name, bit in sides if side_mask & bit]
        eligible = [(name, bit) for name, bit, require in corners if side_mask & require == require]
        for corner_mask in range(1 << len(eligible)):
            active_corners = [name for index, (name, _) in enumerate(eligible) if corner_mask & (1 << index)]
            labels = active_sides + active_corners
            masks.append({"neighbors": labels, "godot_bits": [GODOT_BITS[label] for label in labels]})
    if len(masks) != 47:
        raise AssertionError(f"Internal 47-mask table is invalid: got {len(masks)}")
    return masks


def side_masks() -> list[dict]:
    """The wall family uses sixteen cardinal-only Wang connections."""
    sides = ["N", "E", "S", "W"]
    return [{"neighbors": [side for bit, side in enumerate(sides) if mask & (1 << bit)],
             "godot_bits": [GODOT_BITS[side] for bit, side in enumerate(sides) if mask & (1 << bit)]}
            for mask in range(16)]


def make_godot_publisher(source_path: Path, output_dir: Path, manifest_path: Path) -> str:
    source_json = json.dumps(str(source_path).replace("\\", "/"), ensure_ascii=False)
    output_json = json.dumps(str(output_dir).replace("\\", "/"), ensure_ascii=False)
    manifest_json = json.dumps(str(manifest_path).replace("\\", "/"), ensure_ascii=False)
    material_json = json.dumps(MATERIALS, ensure_ascii=False)
    family_json = json.dumps(FAMILIES, ensure_ascii=False)
    masks_json = json.dumps({"floor": topology_masks(), "wall": side_masks(), "water": topology_masks()}, ensure_ascii=False)
    samples = [[], ["N"], ["N", "E"], ["N", "S"], ["N", "E", "S", "W"], ["N", "E", "S", "W", "NE", "SE", "SW", "NW"]]
    preview_shapes = [next(index for index, mask in enumerate(topology_masks()) if mask["neighbors"] == neighbors) for neighbors in samples]
    preview_json = json.dumps(preview_shapes)
    return f'''extends SceneTree

const CELL := {CELL}
const COLUMNS := {COLUMNS}
const SOURCE_PATH := {source_json}
const OUTPUT_DIR := {output_json}
const MANIFEST_PATH := {manifest_json}
const MATERIALS := {material_json}
const FAMILIES := {family_json}
const FAMILY_MASKS := {masks_json}
const PREVIEW_SHAPES := {preview_json}

func _initialize() -> void:
	var source_image := Image.new()
	var err := source_image.load(SOURCE_PATH)
	if err != OK:
		_fail("Cannot load source image: %s (%s)" % [SOURCE_PATH, error_string(err)])
		return
	if source_image.get_width() < 4 * CELL or source_image.get_height() < 4 * CELL:
		_fail("Source is too small for a 4x4 material grid")
		return
	source_image.convert(Image.FORMAT_RGBA8)

	var crop_records := {{}}
	var material_samples := {{}}
	for material_index in range(MATERIALS.size()):
		var grid_x := material_index % 4
		var grid_y := material_index / 4
		var left := int(grid_x * source_image.get_width() / 4.0)
		var right := int((grid_x + 1) * source_image.get_width() / 4.0)
		var top := int(grid_y * source_image.get_height() / 4.0)
		var bottom := int((grid_y + 1) * source_image.get_height() / 4.0)
		var sample := Image.create(right - left, bottom - top, false, Image.FORMAT_RGBA8)
		sample.blit_rect(source_image, Rect2i(left, top, right - left, bottom - top), Vector2i.ZERO)
		sample.resize(CELL, CELL, Image.INTERPOLATE_NEAREST)
		var name: String = MATERIALS[material_index]
		material_samples[name] = sample
		crop_records[name] = {{"grid_cell": [grid_x, grid_y], "source_rect_px": [left, top, right - left, bottom - top], "source_cell_px": [right - left, bottom - top], "derived_size_px": [CELL, CELL], "resampling": "nearest-neighbor", "scale": [float(CELL) / float(right - left), float(CELL) / float(bottom - top)]}}

	var tile_set := TileSet.new()
	tile_set.tile_size = Vector2i(CELL, CELL)
	var textures := {{}}
	var family_records := {{}}
	for family_name in ["floor", "wall", "water"]:
		var family: Dictionary = FAMILIES[family_name]
		var family_materials: Array = family["materials"]
		var masks: Array = FAMILY_MASKS[family_name]
		var columns := 4 if family_name == "wall" else COLUMNS
		var rows_per_material := ceili(float(masks.size()) / columns)
		var atlas_image := Image.create(columns * CELL, family_materials.size() * rows_per_material * CELL, false, Image.FORMAT_RGBA8)
		atlas_image.fill(Color(0, 0, 0, 0))
		var terrain_names: Array[String] = []
		var material_records := []
		for terrain_index in range(family_materials.size()):
			var material_name: String = family_materials[terrain_index]
			terrain_names.append(material_name)
			var base: Image = material_samples[material_name]
			var tile_entries := []
			for shape_index in range(masks.size()):
				var atlas_x := shape_index % columns
				var atlas_y := terrain_index * rows_per_material + int(shape_index / columns)
				var mask: Dictionary = masks[shape_index]
				var active: Array = mask["neighbors"]
				var tile_rect := Rect2i(atlas_x * CELL, atlas_y * CELL, CELL, CELL)
				for py in range(CELL):
					for px in range(CELL):
						var core_x := px >= 8 and px < 24
						var core_y := py >= 8 and py < 24
						var filled := core_x and core_y
						filled = filled or (core_x and py < 8 and "N" in active)
						filled = filled or (core_x and py >= 24 and "S" in active)
						filled = filled or (core_y and px < 8 and "W" in active)
						filled = filled or (core_y and px >= 24 and "E" in active)
						var corner := ""
						if not core_x and not core_y:
							corner = ("N" if py < 8 else "S") + ("W" if px < 8 else "E")
							if family_name == "wall":
								filled = corner[0] in active and corner[1] in active
							else:
								filled = corner in active
						if filled:
							atlas_image.set_pixel(tile_rect.position.x + px, tile_rect.position.y + py, base.get_pixel(px, py))
				var entry := {{"shape": shape_index, "atlas_coords": [atlas_x, atlas_y], "neighbors": active, "godot_bits": mask["godot_bits"]}}
				tile_entries.append(entry)
			material_records.append({{"terrain_index": terrain_index, "material": material_name, "tiles": tile_entries}})
		var texture_path := OUTPUT_DIR.path_join(family_name + ".png")
		var save_err := atlas_image.save_png(texture_path)
		if save_err != OK:
			_fail("Could not save atlas: %s" % texture_path)
			return
		textures[family_name] = ImageTexture.create_from_image(atlas_image)
		family_records[family_name] = {{"source_id": family["source_id"], "terrain_set": family["terrain_set"], "format": "wang16" if family_name == "wall" else "blob47", "mode": "MATCH_SIDES" if family_name == "wall" else "MATCH_CORNERS_AND_SIDES", "columns": columns, "rows_per_material": rows_per_material, "materials": material_records}}

	for family_name in ["floor", "wall", "water"]:
		var family: Dictionary = FAMILIES[family_name]
		var set_index: int = family["terrain_set"]
		tile_set.add_terrain_set()
		tile_set.set_terrain_set_mode(set_index, TileSet.TERRAIN_MODE_MATCH_SIDES if family_name == "wall" else TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
		for material_name in family["materials"]:
			tile_set.add_terrain(set_index)
			var terrain_index := tile_set.get_terrains_count(set_index) - 1
			tile_set.set_terrain_name(set_index, terrain_index, material_name)
			var sample: Image = material_samples[material_name]
			var color := sample.get_pixel(16, 16)
			color.a = 1.0
			tile_set.set_terrain_color(set_index, terrain_index, color)

		var atlas := TileSetAtlasSource.new()
		atlas.texture = textures[family_name]
		atlas.texture_region_size = Vector2i(CELL, CELL)
		var terrain_index := 0
		var rows_per_material: int = family_records[family_name]["rows_per_material"]
		var masks: Array = FAMILY_MASKS[family_name]
		var columns: int = family_records[family_name]["columns"]
		for material_name in family["materials"]:
			for shape_index in range(masks.size()):
				var coords := Vector2i(shape_index % columns, terrain_index * rows_per_material + int(shape_index / columns))
				atlas.create_tile(coords)
				var data := atlas.get_tile_data(coords, 0)
				data.terrain_set = set_index
				data.terrain = terrain_index
				for bit_index in masks[shape_index]["godot_bits"]:
					data.set_terrain_peering_bit(bit_index, terrain_index)
			terrain_index += 1
		tile_set.add_source(atlas, family["source_id"])

	var preview := Image.create(6 * CELL * 4, 3 * CELL * 4, false, Image.FORMAT_RGBA8)
	for y in range(preview.get_height()):
		for x in range(preview.get_width()):
			var value := 0.52 if int(x / 32 / 4 + y / 32 / 4) % 2 == 0 else 0.68
			preview.set_pixel(x, y, Color(value, value, value, 1.0))
	for family_index in range(3):
		var family_name: String = ["floor", "wall", "water"][family_index]
		var atlas_image: Image = textures[family_name].get_image()
		var columns: int = family_records[family_name]["columns"]
		for column in range(PREVIEW_SHAPES.size()):
			var shape: int = PREVIEW_SHAPES[column]
			if family_name == "wall":
				shape = [0, 1, 3, 5, 15, 15][column]
			var source_rect := Rect2i((shape % columns) * CELL, int(shape / columns) * CELL, CELL, CELL)
			var sample := atlas_image.get_region(source_rect)
			sample.resize(CELL * 4, CELL * 4, Image.INTERPOLATE_NEAREST)
			preview.blit_rect(sample, Rect2i(Vector2i.ZERO, sample.get_size()), Vector2i(column * CELL * 4, family_index * CELL * 4))
	preview.save_png(OUTPUT_DIR.path_join("alpha-mask-preview.png"))

	var resource_path := OUTPUT_DIR.path_join("HighlightsTerrain.tres")
	var resource_error := ResourceSaver.save(tile_set, resource_path)
	if resource_error != OK:
		_fail("Could not save TileSet: %s" % error_string(resource_error))
		return
	var manifest := {{
		"schema": "highlight-terrain-publication/v1",
		"source": {{"path": SOURCE_PATH, "size_px": [source_image.get_width(), source_image.get_height()], "sha256": "{sha256(source_path)}", "grid": [4, 4], "materials": MATERIALS, "samples": crop_records}},
		"derivation": {{"formats": {{"floor": "blob47", "wall": "wang16", "water": "blob47"}}, "method": "full source swatch nearest-neighbor to 32px; permanent central16x16 core; four16px-wide side arms; independent8x8 corners for blob47; adjacent-side corners for wall Wang16", "input_contains_authored_47_variants": false, "resampling": "nearest-neighbor from complete source swatch; source bounds and scale listed per material", "redraw": false, "alpha_handling": "Source alpha preserved. Isolated tiles retain their central core; each legal diagonal bit independently controls its8x8 corner.", "seams": "Hard alpha edges at8px and24px; no invented edge texture or antialiasing."}},
		"tile_size_px": CELL,
		"tileset": "res://ShareAsset/Internal/Props/Highlights/Terrain/HighlightsTerrain.tres",
		"base_layer_requirement": "The derived alpha-cut tiles need a lower opaque base layer (floor/wall/water underlay) in cells where the mask is transparent.",
		"alpha_preview": "alpha-mask-preview.png; six representative masks per family on a checkerboard, enlarged nearest-neighbor for seam/alpha review",
		"neighbor_bits": {{"N": TileSet.CELL_NEIGHBOR_TOP_SIDE, "E": TileSet.CELL_NEIGHBOR_RIGHT_SIDE, "S": TileSet.CELL_NEIGHBOR_BOTTOM_SIDE, "W": TileSet.CELL_NEIGHBOR_LEFT_SIDE, "NE": TileSet.CELL_NEIGHBOR_TOP_RIGHT_CORNER, "SE": TileSet.CELL_NEIGHBOR_BOTTOM_RIGHT_CORNER, "SW": TileSet.CELL_NEIGHBOR_BOTTOM_LEFT_CORNER, "NW": TileSet.CELL_NEIGHBOR_TOP_LEFT_CORNER}},
		"families": family_records,
	}}
	var file := FileAccess.open(MANIFEST_PATH, FileAccess.WRITE)
	if file == null:
		_fail("Could not write manifest: %s" % MANIFEST_PATH)
		return
	file.store_string(JSON.stringify(manifest, "  ") + "\\n")
	file.close()
	print("Published 32x32 Highlights TileSet: " + resource_path)
	quit(0)

func _fail(message: String) -> void:
	printerr(message)
	quit(1)
'''


def make_sheet_publisher(source_paths: dict[str, Path], tile_format: str, output_dir: Path, manifest_path: Path) -> str:
    """Publish pre-authored blob47/Wang16/minimal3x3 family sheets unchanged."""
    source_json = json.dumps({name: str(path).replace("\\", "/") for name, path in source_paths.items()}, ensure_ascii=False)
    source_records = {name: {"path": str(path).replace("\\", "/"), "sha256": sha256(path), "pixel_resize": False} for name, path in source_paths.items()}
    source_records_json = json.dumps(source_records, ensure_ascii=False)
    output_json = json.dumps(str(output_dir).replace("\\", "/"), ensure_ascii=False)
    manifest_json = json.dumps(str(manifest_path).replace("\\", "/"), ensure_ascii=False)
    mask_data = topology_masks() if tile_format == "blob47" else None
    masks_json = json.dumps(mask_data, ensure_ascii=False) if mask_data is not None else "[]"
    grid_width, grid_height = {"blob47": (8, 6), "wang16": (4, 4), "minimal3x3": (3, 3)}[tile_format]
    tile_count = {"blob47": 47, "wang16": 16, "minimal3x3": 9}[tile_format]
    mode = "MATCH_CORNERS_AND_SIDES" if tile_format == "blob47" else "MATCH_SIDES" if tile_format == "wang16" else "MANUAL_MINIMAL"
    return f'''extends SceneTree

const CELL := {CELL}
const GRID_WIDTH := {grid_width}
const GRID_HEIGHT := {grid_height}
const TILE_COUNT := {tile_count}
const FORMAT := {json.dumps(tile_format)}
const MODE := {json.dumps(mode)}
const SOURCES := {source_json}
const SOURCE_RECORDS := {source_records_json}
const OUTPUT_DIR := {output_json}
const MANIFEST_PATH := {manifest_json}
const BLOB_MASKS := {masks_json}
const FAMILY_ORDER := ["floor", "wall", "water"]
const SOURCE_IDS := {{"floor": 0, "wall": 1, "water": 2}}
const SET_IDS := {{"floor": 0, "wall": 1, "water": 2}}
const WANG_SIDES := [["N", 12], ["E", 0], ["S", 4], ["W", 8]]

func _initialize() -> void:
	var tile_set := TileSet.new()
	tile_set.tile_size = Vector2i(CELL, CELL)
	var records := {{}}
	for family_name in FAMILY_ORDER:
		var source_path: String = SOURCES[family_name]
		var image := Image.new()
		var err := image.load(source_path)
		if err != OK:
			_fail("Cannot load %s source %s: %s" % [family_name, source_path, error_string(err)])
			return
		image.convert(Image.FORMAT_RGBA8)
		if image.get_width() != GRID_WIDTH * CELL or image.get_height() != GRID_HEIGHT * CELL:
			_fail("%s %s input must be exactly %dx%d px for %s" % [FORMAT, family_name, GRID_WIDTH * CELL, GRID_HEIGHT * CELL, source_path])
			return
		var target_path := OUTPUT_DIR.path_join(family_name + ".png")
		var save_error := image.save_png(target_path)
		if save_error != OK:
			_fail("Could not save %s" % target_path)
			return
		var set_index: int = SET_IDS[family_name]
		tile_set.add_terrain_set()
		if FORMAT == "blob47":
			tile_set.set_terrain_set_mode(set_index, TileSet.TERRAIN_MODE_MATCH_CORNERS_AND_SIDES)
		elif FORMAT == "wang16":
			tile_set.set_terrain_set_mode(set_index, TileSet.TERRAIN_MODE_MATCH_SIDES)
		else:
			tile_set.set_terrain_set_mode(set_index, TileSet.TERRAIN_MODE_MATCH_SIDES)
		tile_set.add_terrain(set_index)
		tile_set.set_terrain_name(set_index, 0, family_name)
		tile_set.set_terrain_color(set_index, 0, image.get_pixel(CELL / 2, CELL / 2))
		var atlas := TileSetAtlasSource.new()
		atlas.texture = ImageTexture.create_from_image(image)
		atlas.texture_region_size = Vector2i(CELL, CELL)
		var tiles := []
		for shape in range(TILE_COUNT):
			var coords := Vector2i(shape % GRID_WIDTH, int(shape / GRID_WIDTH))
			atlas.create_tile(coords)
			var neighbors: Array = []
			var godot_bits: Array = []
			if FORMAT == "blob47":
				neighbors = BLOB_MASKS[shape]["neighbors"]
				godot_bits = BLOB_MASKS[shape]["godot_bits"]
			elif FORMAT == "wang16":
				for side in WANG_SIDES:
					if shape & (1 << WANG_SIDES.find(side)):
						neighbors.append(side[0])
						godot_bits.append(side[1])
			if FORMAT != "minimal3x3":
				var data := atlas.get_tile_data(coords, 0)
				data.terrain_set = set_index
				data.terrain = 0
				for bit in godot_bits:
					data.set_terrain_peering_bit(bit, 0)
			tiles.append({{"shape": shape, "atlas_coords": [coords.x, coords.y], "neighbors": neighbors, "godot_bits": godot_bits, "terrain_connected": FORMAT != "minimal3x3"}})
		tile_set.add_source(atlas, SOURCE_IDS[family_name])
		records[family_name] = {{"source_id": SOURCE_IDS[family_name], "terrain_set": set_index, "terrain_index": 0, "mode": MODE, "source": SOURCE_RECORDS[family_name], "grid": [GRID_WIDTH, GRID_HEIGHT], "input_size_px": [image.get_width(), image.get_height()], "alpha_preserved": true, "tiles": tiles}}
	var resource_path := OUTPUT_DIR.path_join("HighlightsTerrain.tres")
	var resource_error := ResourceSaver.save(tile_set, resource_path)
	if resource_error != OK:
		_fail("Could not save TileSet: %s" % error_string(resource_error))
		return
	var manifest := {{"schema": "highlight-terrain-publication/v1", "format": FORMAT, "tile_size_px": CELL, "tileset": "res://ShareAsset/Internal/Props/Highlights/Terrain/HighlightsTerrain.tres", "auto_connect_complete": FORMAT != "minimal3x3", "format_limit": "3x3 minimal sheets are manual variants; they are not a complete autotile topology and have no terrain peering bits." if FORMAT == "minimal3x3" else "", "seams": "Pre-authored sheet pixels are preserved without resize; no seam repair is applied.", "families": records}}
	var file := FileAccess.open(MANIFEST_PATH, FileAccess.WRITE)
	if file == null:
		_fail("Could not write manifest: %s" % MANIFEST_PATH)
		return
	file.store_string(JSON.stringify(manifest, "  ") + "\\n")
	file.close()
	print("Published %s Highlights terrain TileSet: %s" % [FORMAT, resource_path])
	quit(0)

func _fail(message: String) -> void:
	printerr(message)
	quit(1)
'''


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--format", choices=["materials-4x4", "blob47", "wang16", "minimal3x3"], default="materials-4x4")
    parser.add_argument("--source", type=Path, default=SOURCE_ARCHIVE, help="4x4 material sheet for materials-4x4")
    for family in ("floor", "wall", "water"):
        parser.add_argument(f"--{family}-source", type=Path, help=f"Pre-authored {family} sheet for blob47/wang16/minimal3x3")
    default_godot = Path.home() / "AppData/Local/Programs/Godot/4.6.1-mono/Godot_v4.6.1-stable_mono_win64_console.exe"
    parser.add_argument("--godot", default=str(default_godot if default_godot.is_file() else "godot"), help="Godot 4.6.1 executable")
    args = parser.parse_args()
    RUNTIME.mkdir(parents=True, exist_ok=True)
    manifest_path = RUNTIME / "asset-manifest.json"
    scratch = ROOT / ".scratch"
    scratch.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="highlight-terrain-", dir=scratch) as temporary:
        work = Path(temporary)
        if args.format == "materials-4x4":
            source = args.source.resolve()
            if not source.is_file():
                raise FileNotFoundError(f"Missing 4x4 material sheet: {source}")
            if source != SOURCE_ARCHIVE.resolve():
                SOURCE_ARCHIVE.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, SOURCE_ARCHIVE)
                source = SOURCE_ARCHIVE
            script_text = make_godot_publisher(source, RUNTIME, manifest_path)
        else:
            archive_dir = SOURCE_ARCHIVE.parent / args.format
            archive_dir.mkdir(parents=True, exist_ok=True)
            source_paths = {}
            for family in ("floor", "wall", "water"):
                requested = getattr(args, f"{family}_source")
                if requested is None or not requested.is_file():
                    raise FileNotFoundError(f"--{family}-source is required for {args.format}")
                archived = archive_dir / f"{family}.png"
                if requested.resolve() != archived.resolve():
                    shutil.copyfile(requested, archived)
                source_paths[family] = archived.resolve()
            script_text = make_sheet_publisher(source_paths, args.format, RUNTIME, manifest_path)
        script_path = work / "publish_highlight_tiles.gd"
        script_path.write_text(script_text, encoding="utf-8")
        result = subprocess.run(
            [args.godot, "--headless", "--path", str(GAME), "--script", str(script_path)],
            cwd=ROOT,
            check=False,
        )
        if result.returncode:
            raise SystemExit(result.returncode)
    externalize_textures(RUNTIME / "HighlightsTerrain.tres")
    if args.format == "materials-4x4":
        print(f"Source: {SOURCE_ARCHIVE.relative_to(ROOT).as_posix()} ({sha256(SOURCE_ARCHIVE)})")
    print(f"Published: {(RUNTIME / 'HighlightsTerrain.tres').relative_to(ROOT).as_posix()}")


if __name__ == "__main__":
    main()
