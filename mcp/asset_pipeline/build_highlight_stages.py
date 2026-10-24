"""Publish ImageGen highlight props and four 32x48 character atlases.

Only crop, transparent-bounds trim, and explicitly recorded nearest-neighbor
publication resizing are applied. Original masters are retained unchanged.
Terrain publication belongs to publish_highlight_tiles.py.
"""
from pathlib import Path
import hashlib
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "mcp/asset_pipeline/sources/Internal"
RUNTIME = ROOT / "godot_csharp/ShareAsset/Internal"
# Explicit source rectangles follow the authored sheet's unequal object widths.
OBJECTS = {
    "door": ((0, 0, 313, 405), (64, 80), "Architecture"),
    "window": ((313, 0, 626, 405), (48, 80), "Architecture"),
    "dumpling_table": ((626, 0, 930, 405), (96, 64), "Kitchen"),
    "stove": ((930, 0, 1254, 405), (64, 80), "Kitchen"),
    "stele": ((0, 405, 300, 795), (64, 96), "Relics"),
    "arch": ((300, 405, 626, 795), (96, 96), "Architecture"),
    "grain_crates": ((626, 405, 970, 795), (64, 64), "Logistics"),
    "seedlings": ((970, 405, 1254, 795), (64, 32), "Logistics"),
    "go_table": ((0, 795, 410, 1254), (96, 64), "Furniture"),
    "pump": ((410, 795, 687, 1254), (64, 80), "Mechanisms"),
    "screen": ((687, 795, 1098, 1254), (96, 96), "Furniture"),
    "sword": ((1098, 795, 1254, 1254), (12, 48), "Handheld"),
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def trimmed(image):
    box = image.getchannel("A").getbbox()
    if box is None:
        raise ValueError("Empty alpha crop in master")
    return image.crop(box), list(box)


def publish():
    source = SOURCE / "Props/Highlights/Objects/objects_master.png"
    original = Image.open(source).convert("RGBA")
    if original.size != (1254, 1254):
        raise ValueError("The explicit props crop contract requires the 1254x1254 master")
    record = {"schema": 1, "generator": "built-in image_gen", "source": str(source.relative_to(ROOT)),
              "source_sha256": digest(source), "publication": "crop, alpha bounds, nearest-neighbor resize",
              "visual_acceptance": "requires actual Godot inspection", "objects": {}}
    entries = list(OBJECTS.items()) + [("seal_anchor", (None, (64, 80), "Mechanisms"))]
    for name, (rect, size, family) in entries:
        item_source = source
        item_master = original
        if rect is None:
            item_source = SOURCE / "Props/Highlights/Objects/seal_anchor_master.png"
            item_master = Image.open(item_source).convert("RGBA")
            rect = (0, 0, item_master.width, item_master.height)
        item, alpha_box = trimmed(item_master.crop(rect))
        scale = min(size[0] / item.width, size[1] / item.height)
        target_size = (max(1, round(item.width * scale)), max(1, round(item.height * scale)))
        item = item.resize(target_size, Image.Resampling.NEAREST)
        canvas = Image.new("RGBA", size)
        canvas.alpha_composite(item, ((size[0] - item.width) // 2, size[1] - item.height))
        folder = RUNTIME / "Props/Highlights" / family
        folder.mkdir(parents=True, exist_ok=True)
        output = folder / (name + ".png")
        canvas.save(output)
        texture = "res://" + str(output.relative_to(ROOT / "godot_csharp")).replace("\\", "/")
        (folder / (name + ".tres")).write_text(
            '[gd_resource type="AtlasTexture" load_steps=2 format=3]\n\n'
            f'[ext_resource type="Texture2D" path="{texture}" id="source"]\n\n'
            '[resource]\natlas = ExtResource("source")\n'
            f'region = Rect2(0, 0, {size[0]}, {size[1]})\n', encoding="utf-8")
        record["objects"][name] = {"crop": list(rect), "alpha_crop": alpha_box, "size": list(size),
                                    "source": str(item_source.relative_to(ROOT)), "source_sha256": digest(item_source),
                                    "sample_size": list(target_size), "scale": scale, "texture": texture,
                                    "family": family, "sha256": digest(output), "origin": "bottom-center"}
    cast_source = SOURCE / "Characters/Highlights/cast_master.png"
    cast = Image.open(cast_source).convert("RGBA")
    cast_out = RUNTIME / "Characters/Highlights"
    cast_out.mkdir(parents=True, exist_ok=True)
    cast_record = {"source": str(cast_source.relative_to(ROOT)), "source_sha256": digest(cast_source),
                   "source_size": list(cast.size), "cell": [32, 48], "directions": ["front", "back", "left"],
                   "publication": "equal cell crop, alpha trim, uniform per-character nearest-neighbor scaling", "characters": {}}
    for row, name in enumerate(("poet", "scholar", "chess_player", "demon")):
        poses, boxes = [], []
        for col in range(3):
            rect = (col * cast.width // 3, row * cast.height // 4,
                    (col + 1) * cast.width // 3, (row + 1) * cast.height // 4)
            item, bounds = trimmed(cast.crop(rect))
            poses.append(item)
            boxes.append({"cell": list(rect), "alpha_crop": bounds})
        scale = min(30 / max(p.width for p in poses), 46 / max(p.height for p in poses))
        sheet = Image.new("RGBA", (96, 48))
        for col, pose in enumerate(poses):
            pose = pose.resize((max(1, round(pose.width * scale)), max(1, round(pose.height * scale))), Image.Resampling.NEAREST)
            sheet.alpha_composite(pose, (col * 32 + (32 - pose.width) // 2, 47 - pose.height))
        output = cast_out / (name + ".png")
        sheet.save(output)
        cast_record["characters"][name] = {"crops": boxes, "scale": scale, "sha256": digest(output)}
    (cast_out / "publication.json").write_text(json.dumps(cast_record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (RUNTIME / "Props/Highlights/objects.json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    # Retain the generated gallery's actual weather view inside the stage arch.
    gallery_source = SOURCE / "Props/Highlights/Backgrounds/shura_gallery.png"
    gallery = Image.open(gallery_source).convert("RGBA")
    weather_rect = (650, 40, 880, 290)
    weather = gallery.crop(weather_rect).resize((42, 64), Image.Resampling.NEAREST)
    weather_path = RUNTIME / "Props/Highlights/Architecture/gallery_weather.png"
    weather.save(weather_path)
    (weather_path.parent / "gallery_weather.publication.json").write_text(json.dumps({
        "source": str(gallery_source.relative_to(ROOT)), "source_sha256": digest(gallery_source),
        "source_rect": weather_rect, "output_size": [42, 64], "resampling": "nearest-neighbor",
        "purpose": "original gallery storm view behind the independent arch sprite",
        "output_sha256": digest(weather_path)}, indent=2) + "\n", encoding="utf-8")
    print("Published highlight object textures and character atlases; runtime visual review remains separate.")


if __name__ == "__main__":
    publish()
