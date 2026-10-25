"""Publish the approved ImageGen facade as separately reusable 32px modules.

The master remains unchanged. Source rectangles, alpha handling and one uniform
scale per component are recorded; no wall/roof pattern is painted by this tool.
"""
from pathlib import Path
import hashlib
import json
import re
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "mcp/asset_pipeline/sources/Internal/Props/ReferenceFacade"
OUTPUT = ROOT / "godot_csharp/ShareAsset/Internal/Props/ReferenceFacade"
REFERENCE = "artifacts/campus-architecture-review/astra/12_settlement_material_refinement.png"
# Source is the actual 1254-square output, not the requested 1024-square canvas.
# Repeating surfaces exclude the image generator's outer swatch bevels.
COMPONENTS = {
    "ivory_wall": ("Wall", (20, 20, 275, 275), (32, 32)),
    "stone_base": ("Foundation", (327, 20, 280, 280), (32, 32)),
    "narrow_window": ("Openings", (405, 633, 132, 287), (32, 64)),
    "timber_door": ("Openings", (697, 633, 173, 292), (32, 64)),
    "eave_straight": ("Cornice", (5, 356, 311, 143), (64, 32)),
    "eave_left": ("Cornice", (334, 318, 289, 238), (64, 64)),
    "eave_right": ("Cornice", (635, 318, 284, 238), (64, 64)),
    "corner_pilaster": ("Corners", (1048, 318, 100, 307), (32, 64)),
    "floor_belt": ("Cornice", (5, 748, 308, 55), (32, 32)),
    "wall_base_junction": ("Foundation", (953, 638, 291, 286), (64, 64)),
    "ivory_wall_variant": ("Wall", (20, 959, 275, 275), (32, 32)),
    "black_roof": ("Roof", (960, 950, 274, 274), (32, 32)),
}


def attach_facade(text, room):
    """Keep the same lecture-hall surface in both overlapping outdoor scenes."""
    if room["key"] not in ("campus_n", "campus_nw") or '[node name="ReferenceFacade"' in text:
        return text
    parent = '[node name="讲堂" type="Sprite2D" parent="Objects"]'
    start = text.find(parent)
    if start < 0:
        return text
    end = text.find('\n[node ', start + len(parent))
    if end < 0:
        raise ValueError("Lecture hall must have its ordinary following scene nodes")
    section = text[start:end]
    offset = re.search(r'offset = Vector2\(([-\d.]+), ([-\d.]+)\)', section)
    if offset is None or 'texture = ExtResource("t_outdoor_bldg_000")' not in section:
        raise ValueError("Reference facade requires the original lecture-hall sprite binding")
    x, y = float(offset[1]) + 11, float(offset[2]) + 352
    node = (f'\n[node name="ReferenceFacade" type="Node2D" parent="Objects/讲堂"]\n'
            f'position = Vector2({x:g}, {y:g})\nscript = ExtResource("reference_facade")\n')
    text = text[:end] + node + text[end:]
    declaration = '[ext_resource type="Script" path="res://scenes/Chapter2/Field/ReferenceFacade.gd" id="reference_facade"]\n\n'
    at = text.index('[sub_resource')
    text = text[:at] + declaration + text[at:]
    return re.sub(r'load_steps=(\d+)', lambda match: f'load_steps={int(match[1]) + 1}', text, count=1)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def publish():
    master_path = SOURCE / "master.png"
    master = Image.open(master_path).convert("RGBA")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    records = {}
    resources, atlases, source_links = [], [], []
    for number, (key, (category, rect, canvas_size)) in enumerate(COMPONENTS.items()):
        x, y, width, height = rect
        crop = master.crop((x, y, x + width, y + height))
        ratio = min(canvas_size[0] / width, canvas_size[1] / height)
        size = (max(1, round(width * ratio)), max(1, round(height * ratio)))
        sample = crop.resize(size, Image.Resampling.NEAREST)
        canvas = Image.new("RGBA", canvas_size)
        offset = ((canvas_size[0] - size[0]) // 2, (canvas_size[1] - size[1]) // 2)
        canvas.alpha_composite(sample, offset)
        folder = OUTPUT / category
        folder.mkdir(exist_ok=True)
        output = folder / (key + ".png")
        canvas.save(output)
        res = "res://" + str(output.relative_to(ROOT / "godot_csharp")).replace("\\", "/")
        records[key] = {"source_rect_px": list(rect), "category": category, "texture": res, "source_id": number,
                        "canvas_px": list(canvas_size), "grid_cells": [canvas_size[0] // 32, canvas_size[1] // 32],
                        "uniform_scale": ratio, "sample_px": list(size), "offset_px": list(offset), "sha256": sha(output)}
        resources.append(f'[ext_resource type="Texture2D" path="{res}" id="tex_{number}"]')
        dimensions = '' if canvas_size == (32, 32) else f'0:0/size_in_atlas = Vector2i({canvas_size[0] // 32}, {canvas_size[1] // 32})\n'
        atlases.append(f'[sub_resource type="TileSetAtlasSource" id="atlas_{number}"]\ntexture = ExtResource("tex_{number}")\ntexture_region_size = Vector2i(32, 32)\n{dimensions}0:0/0 = 0\n')
        source_links.append(f'sources/{number} = SubResource("atlas_{number}")')
    tile_set = '[gd_resource type="TileSet" load_steps=%d format=3]\n\n' % (1 + 2 * len(COMPONENTS))
    tile_set += '\n'.join(resources) + '\n\n' + '\n'.join(atlases)
    tile_set += '\n[resource]\ntile_size = Vector2i(32, 32)\n' + '\n'.join(source_links) + '\n'
    (OUTPUT / "ReferenceFacade.tres").write_text(tile_set, encoding="utf-8")
    manifest = {"schema": "reference-facade/v1", "generator": "built-in image_gen", "reference": REFERENCE,
                "reference_sha256": sha(ROOT / REFERENCE), "source": str(master_path.relative_to(ROOT)).replace("\\", "/"),
                "source_sha256": sha(master_path), "source_size_px": list(master.size), "base_grid_px": 32,
                "publication": "explicit crop, nearest-neighbor uniform scale, transparent padding; alpha preserved; no recoloring or redrawing",
                "scope": "Main-building material and opening language from the approved reference. Existing campus building geometry is retained; this is not a claim of whole-building equivalence.",
                "unused_master_cells": "The two bottom-row perspective stone-corner illustrations are not published as orthographic tiles. Corners use the front-elevation cream pilaster.",
                "runtime_usage": {"scenes": ["res://scenes/Chapter2/Maps/campus_n.tscn", "res://scenes/Chapter2/Maps/campus_nw.tscn"], "node": "Objects/讲堂/ReferenceFacade", "surface": "existing south elevation only", "geometry_changed": False},
                "components": records}
    (OUTPUT / "publication.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for key in ("campus_n", "campus_nw"):
        scene = ROOT / "godot_csharp/scenes/Chapter2/Maps" / (key + ".tscn")
        original = scene.read_text(encoding="utf-8")
        updated = attach_facade(original, {"key": key})
        if updated != original:
            scene.write_text(updated, encoding="utf-8")


if __name__ == "__main__":
    publish()
