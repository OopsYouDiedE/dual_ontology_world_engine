"""Compose image-generated materials into editable 2x room surface layers.

The generator only crops, places and tiles the authored raster assets. Room
geometry, door labels and the seven exact chalk positions remain plain data.
No furniture is baked into the floor and no navigation state is changed here.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib
import json
import math
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

GAME = (Path(__file__).resolve().parents[2] / "godot_csharp")
SOURCE = GAME.parent / "mcp/asset_pipeline/sources/Internal/Props/SceneRenovation20261021"
OUT = GAME / "ShareAsset/Internal/Props/SceneRenovation20261021/surfaces"
MAPS = GAME / "scenes/Chapter2/Maps"
SCALE = 2
CELL = 32
MATERIALS = ["oak", "plank", "herringbone", "whitetile", "quarry", "flagstone", "slate", "gravel",
             "wallpaper_teal", "plaster", "ashlar", "iron", "wallpaper_burgundy", "wall_tile", "water", "volcanic"]
ALIASES = {"parquet": "herringbone", "honey": "oak", "quarry_dark": "quarry", "basalt": "volcanic",
           "paving": "cobblestone", "soil": "earth", "stone": "flagstone"}
# Atlas boxes are reviewed against the actual 1254px source, not a guessed grid.
ARCH_BOXES = {
    "window": (35, 15, 320, 345), "arched_window": (355, 8, 590, 345),
    "sconce": (640, 35, 870, 345), "clock": (945, 35, 1235, 345),
    "closed_door": (35, 345, 310, 695), "open_door": (340, 345, 610, 695),
    "isolation_door": (625, 345, 925, 695), "glass_panel": (925, 345, 1254, 695),
    "paper_panel": (0, 695, 324, 937), "wood_panel": (324, 695, 628, 937),
    "tile_panel": (628, 695, 927, 937), "stone_cap": (927, 765, 1254, 937),
    "painting": (18, 948, 315, 1228), "notice_frame": (323, 942, 528, 1232),
    "rug": (534, 949, 925, 1235), "steps": (928, 953, 1254, 1225),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class Art:
    def __init__(self):
        source = Image.open(SOURCE / "materials.png").convert("RGBA")
        self.material = {}
        for i, key in enumerate(MATERIALS):
            x, y = i % 4, i // 4
            box = (round(x * source.width / 4) + 2, round(y * source.height / 4) + 2,
                   round((x + 1) * source.width / 4) - 2, round((y + 1) * source.height / 4) - 2)
            self.material[key] = source.crop(box).resize((256, 256), Image.Resampling.LANCZOS)
        ground = Image.open(SOURCE / "ground.png").convert("RGBA")
        for i, key in enumerate(("grass", "earth", "cobblestone", "water")):
            x, y = i % 2, i // 2
            box = (round(x * ground.width / 2) + 3, round(y * ground.height / 2) + 3,
                   round((x + 1) * ground.width / 2) - 3, round((y + 1) * ground.height / 2) - 3)
            self.material[key] = ground.crop(box).resize((256, 256), Image.Resampling.LANCZOS)
        arch = Image.open(SOURCE / "architecture.png").convert("RGBA")
        self.arch = {}
        for key, box in ARCH_BOXES.items():
            box = tuple(round(v * arch.width / 1254) for v in box)
            sprite = arch.crop(box)
            bounds = sprite.getchannel("A").point(lambda p: 255 if p >= 24 else 0).getbbox()
            if bounds is None:
                raise ValueError(f"Empty architectural crop: {key}")
            self.arch[key] = sprite.crop(bounds)
        self.tiles = {}

    def tile(self, key, size):
        key = ALIASES.get(key, key)
        if key not in self.material:
            raise ValueError(f"No image-generated material for {key}")
        cache_key = (key, size)
        if cache_key not in self.tiles:
            # Mirrored repeat reuses the original painted pixels and matches edges.
            tile = self.material[key]
            pattern = Image.new("RGBA", (512, 512))
            pattern.paste(tile, (0, 0))
            pattern.paste(ImageOps.mirror(tile), (256, 0))
            pattern.paste(ImageOps.flip(tile), (0, 256))
            pattern.paste(ImageOps.flip(ImageOps.mirror(tile)), (256, 256))
            canvas = Image.new("RGBA", size)
            for y in range(0, size[1], 512):
                for x in range(0, size[0], 512):
                    canvas.paste(pattern, (x, y))
            self.tiles[cache_key] = canvas
        return self.tiles[cache_key]

    def place(self, canvas, key, x, y, w, h):
        sprite = self.arch[key].resize((max(1, round(w * SCALE)), max(1, round(h * SCALE))), Image.Resampling.LANCZOS)
        canvas.alpha_composite(sprite, (round(x * SCALE), round(y * SCALE)))


def region(room, x, y):
    for name, x0, y0, x1, y1 in room.get("regions", []):
        if x0 <= x < x1 and y0 <= y < y1:
            return name
    return "default"


def fill_material(canvas, art, key, box):
    rect = tuple(round(v * SCALE) for v in box)
    tile = art.tile(key, canvas.size)
    canvas.paste(tile.crop(rect), rect[:2])


def compose(room, art):
    rows = room["rows"]
    size = (len(rows[0]) * CELL * SCALE, len(rows) * CELL * SCALE)
    layers = {key: Image.new("RGBA", size) for key in ("floor", "walls", "fittings", "floor_detail")}
    floor, walls, fittings, detail = (layers[k] for k in layers)
    floor.paste((38, 40, 51, 255), (0, 0, *size))
    outside = room.get("outside", "void")
    if outside != "void":
        floor.paste(art.tile(outside, size))
    for y, row in enumerate(rows):
        for x, mark in enumerate(row):
            box = (x * CELL, y * CELL, (x + 1) * CELL, (y + 1) * CELL)
            reg = region(room, x, y)
            if mark in ".DS":
                material = room["floor"].get(reg, room["floor"].get("default", "plank"))
                fill_material(floor, art, material, box)
            elif mark == "#":
                fill_material(walls, art, "volcanic", box)
    for y, row in enumerate(rows):
        x = 0
        while x < len(row):
            if row[x] not in "WG":
                x += 1
                continue
            start, reg = x, region(room, x, y)
            while x < len(row) and row[x] == row[start] and region(room, x, y) == reg:
                x += 1
            style = room.get("wall", {}).get(reg, room.get("wall", {}).get("default", ("plaster",)))
            kind = style[0]
            panel = {"panel": "wood_panel", "tile": "tile_panel", "glass": "glass_panel"}.get(kind, "paper_panel")
            w = (x - start) * CELL
            if kind == "paper" and len(style) > 1 and style[1] in ("PLUM", "LINEN"):
                key = "wallpaper_burgundy" if style[1] == "PLUM" else "plaster"
                fill_material(walls, art, key, (start * CELL, y * CELL, x * CELL, (y + 2) * CELL))
                art.place(walls, "wood_panel", start * CELL, (y + 2) * CELL - 19, w, 19)
            elif kind in ("ashlar", "plaster"):
                fill_material(walls, art, "ashlar" if kind == "ashlar" else "plaster", (start * CELL, y * CELL, x * CELL, (y + 2) * CELL))
                art.place(walls, "stone_cap" if kind == "ashlar" else "wood_panel", start * CELL, (y + 2) * CELL - 17, w, 17)
            else:
                # Repeat painted panel sections, then crop the final partial section.
                strip = Image.new("RGBA", (w * SCALE, 64 * SCALE))
                for xx in range(0, w, 96):
                    art.place(strip, panel, xx, 0, 96, 64)
                walls.alpha_composite(strip, (start * CELL * SCALE, y * CELL * SCALE))
    for d in room.get("decor", []):
        kind = d[0]
        if kind == "window":
            w = max(30, d[3] + 10)
            art.place(fittings, "window", d[1] - w / 2, d[2] * CELL + 5, w, 51)
        elif kind == "sconce":
            art.place(fittings, "sconce", d[1] - 6, d[2] * CELL + 10, 12, 27)
        elif kind == "painting":
            art.place(fittings, "painting", d[1] - 20, d[2] * CELL + 9, 40, 32)
        elif kind in ("door", "archway"):
            art.place(fittings, "open_door", d[1] - 5, d[2] * CELL, 74, 64)
            if kind == "door" and len(d) > 3 and d[3]:
                text = "R-" + str(d[3])
                draw = ImageDraw.Draw(fittings)
                try:
                    font = ImageFont.truetype("C:/Windows/Fonts/georgia.ttf", 14)
                except OSError:
                    font = ImageFont.load_default()
                cx, yy = (d[1] + 32) * SCALE, d[2] * CELL * SCALE - 1
                bound = draw.textbbox((0, 0), text, font=font)
                tw = bound[2] - bound[0]
                draw.rectangle((cx - tw / 2 - 4, yy, cx + tw / 2 + 4, yy + 17), fill=(219, 215, 200, 255))
                draw.text((cx - tw / 2, yy - 1), text, font=font, fill=(53, 51, 55, 255))
        elif kind == "side_door":
            # Only the leaf at the jamb: do not draw a closed door over open floor.
            art.place(fittings, "wood_panel", d[1] * CELL + (2 if d[4] == "w" else 25), d[2] * CELL + 2, 5, d[3] * CELL - 4)
    for work in room.get("floor_work", []):
        kind, values = work[0], work[1:]
        if kind in ("rug", "runner"):
            x, y, w, h = values[:4]
            art.place(detail, "rug", x, y, w, h)
        elif kind == "stage":
            x, y, w, h = values[:4]
            fill_material(detail, art, "plank", (x, y, x + w, y + h))
        elif kind == "path":
            x, y, w, h, key = values
            fill_material(detail, art, key, (x, y, x + w, y + h))
        elif kind in ("steps", "stairwell"):
            x, y, w = values[:3]
            h = values[3] if kind == "stairwell" else max(8, values[3] * 10)
            art.place(detail, "steps", x, y, w, h)
        elif kind == "mosaic":
            cx, cy, r = values
            draw = ImageDraw.Draw(detail)
            for dr in (0, 5, 11):
                draw.ellipse(((cx-r+dr)*SCALE, (cy-(r-dr)*.72)*SCALE, (cx+r-dr)*SCALE, (cy+(r-dr)*.72)*SCALE), outline=(148,160,158,180), width=2)
        elif kind == "chalk":
            # Seven explicit stations; the old procedural image had only four.
            x, y, w, h = values[:4]
            points = [(x+w*.2,y+h*.25),(x+w*.5,y+h*.15),(x+w*.8,y+h*.25),(x+w*.3,y+h*.5),
                      (x+w*.7,y+h*.5),(x+w*.35,y+h*.8),(x+w*.65,y+h*.8)]
            draw = ImageDraw.Draw(detail)
            draw.line([(round(px*SCALE),round(py*SCALE)) for px,py in points], fill=(210,215,206,150), width=2)
            for i, (px,py) in enumerate(points):
                for dx in (-3, 3):
                    draw.ellipse(((px+dx-1)*SCALE,(py-4)*SCALE,(px+dx+1)*SCALE,(py+4)*SCALE), outline=(223,225,213,220), width=2)
    return layers


def wire_scene(path, files):
    text = path.read_text(encoding="utf-8")
    # Re-running is deterministic and does not accumulate layers or dependencies.
    text = re.sub(r'(?m)^\[ext_resource[^\n]*id="ren_[^"]+"\]\n', '', text)
    text = re.sub(r'(?ms)^\[node name="Renovation(?:Walls|Fittings|FloorDetail)"[^\n]*\].*?(?=^\[|\Z)', '', text)
    ext = []
    for key, file in files.items():
        ext.append(f'[ext_resource type="Texture2D" path="res://{file.relative_to(GAME).as_posix()}" id="ren_{key}"]')
    first = text.find("\n")
    text = text[:first+1] + "\n" + "\n".join(ext) + "\n" + text[first+1:]
    pattern = r'(?ms)^\[node name="Architecture"[^\n]*\](.*?)(?=^\[|\Z)'
    match = re.search(pattern, text)
    if not match:
        raise ValueError(f"No Architecture node: {path}")
    block = match[0]
    block = re.sub(r'(?m)^texture = .*$', 'texture = ExtResource("ren_floor")', block)
    block = re.sub(r'(?m)^z_index = .*$', 'z_index = -12', block)
    block = re.sub(r'(?m)^scale = .*\n', '', block)
    block = re.sub(r'(?m)^metadata/surface_layer = .*\n', '', block)
    block = block.rstrip() + '\nscale = Vector2(0.5, 0.5)\nmetadata/surface_layer = true\n\n'
    text = text[:match.start()] + block + text[match.end():]
    additions = []
    for key, name, z in (("floor_detail", "FloorDetail", -11), ("walls", "Walls", -10), ("fittings", "Fittings", -9)):
        additions.append(f'[node name="Renovation{name}" type="Sprite2D" parent="."]\nz_index = {z}\nscale = Vector2(0.5, 0.5)\ntexture = ExtResource("ren_{key}")\ncentered = false\nmetadata/surface_layer = true\n')
    text += "\n" + "\n".join(additions)
    # The old architecture image is no longer needed by this scene.
    if 'ExtResource("arch")' not in text:
        text = re.sub(r'(?m)^\[ext_resource[^\n]*id="arch"\]\n', '', text)
    count = len(re.findall(r'^\[(?:ext_resource|sub_resource)\b', text, re.M)) + 1
    text = re.sub(r'load_steps=\d+', f'load_steps={count}', text, count=1)
    path.write_text(text, encoding="utf-8", newline="\n")


def all_plans():
    from chapter2_art import rooms
    plans = rooms.all_rooms()
    try:
        future = importlib.import_module("chapter2_art.future_rooms")
    except ModuleNotFoundError as exc:
        if exc.name != "chapter2_art.future_rooms":
            raise
    else:
        plans += future.all_rooms()
    return {p["key"]: p for p in plans}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--maps", nargs="+")
    parser.add_argument("--no-wire", action="store_true")
    args = parser.parse_args()
    plans, art = all_plans(), Art()
    selected = args.maps or list(plans)
    OUT.mkdir(parents=True, exist_ok=True)
    records = []
    for key in selected:
        room = plans[key]
        if not room.get("rows"):
            continue
        layers = compose(room, art)
        files = {}
        for layer, im in layers.items():
            path = OUT / f"{key}.{layer}.png"
            im.save(path)
            files[layer] = path
        if not args.no_wire:
            wire_scene(MAPS / f"{key}.tscn", files)
        records.append({"map": key, "world_size": [len(room["rows"][0])*CELL,len(room["rows"])*CELL],
                        "texture_density": SCALE, "layers": {k: {"path": p.relative_to(GAME).as_posix(), "sha256": sha(p)} for k,p in files.items()}})
        print(f"surface {key}: {len(layers)} editable layers, density {SCALE}x")
    manifest = {"version": 1, "generator": "mcp/asset_pipeline/build_scene_surfaces.py", "source_material_sha256": sha(SOURCE/"materials.png"),
                "source_architecture_sha256": sha(SOURCE/"architecture.png"), "source_ground_sha256": sha(SOURCE/"ground.png"),
                "architecture_crop_boxes_at_1254": ARCH_BOXES,
                "materials": MATERIALS, "maps": records, "geometry_changed": False, "furniture_baked": False}
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
