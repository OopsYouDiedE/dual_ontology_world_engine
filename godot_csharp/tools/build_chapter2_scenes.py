"""Build every chapter-2 location: painted maps, sprites and editable Godot scenes.

Run from the repository root:  python godot_csharp/tools/build_chapter2_scenes.py
Then import in Godot:          godot --headless --editor --path godot_csharp --import

Writes
  godot_csharp/ShareAsset/Internal/Props/Chapter2/rooms/<key>.png      painted architecture (1x)
  godot_csharp/ShareAsset/Internal/Props/Chapter2/props/<name>.png     chapter-2 sprites (1x)
  godot_csharp/scenes/Chapter2/Maps/<key>.tscn                         one scene per location
  godot_csharp/ShareAsset/Internal/Props/Chapter2/manifest.json        sizes, anchors, exits
Chapter-1 furniture is referenced from FirstAwakeningInterior, not duplicated.
Every pixel comes from the recipes in tools/first_awakening_art and tools/chapter2_art.
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

GAME = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(GAME / "tools"))

from first_awakening_art import props as ch1_props                               # noqa: E402
from chapter2_art import campus_room, painter, props_archive, props_core, props_service, rooms   # noqa: E402

OUT = GAME / "ShareAsset/Internal/Props/Chapter2"
CH1 = "res://ShareAsset/Internal/Props/FirstAwakeningInterior"
SCENES = GAME / "scenes/Chapter2/Maps"
CELL = 32

# Chapter-1 sprite footprints (w, h) measured from its placement table.
CH1_FP = {
    "bed_0": (36, 64), "desk_0": (58, 26), "chair_back": (14, 10), "chair_front": (14, 10), "wardrobe": (40, 16),
    "nightstand": (18, 12), "bookcase_0": (40, 14), "bookcase_study": (40, 14), "bookcase_study_b": (40, 14),
    "coat_stand": (12, 8), "study_table": (132, 46), "sideboard": (82, 22), "range_stove": (42, 20), "worktable": (96, 30),
    "washstand": (48, 20), "side_table_plain": (24, 12), "armchair": (28, 16), "floor_lamp": (10, 8), "barrel": (18, 12),
    "crate": (22, 10), "sack": (14, 8), "plant_stand": (16, 8), "bench": (40, 10), "transmitter": None,
}


def sprites_and_footprints():
    canvases, fps = {}, dict(CH1_FP)
    ch1 = ch1_props.build()
    for mod in (props_core, props_archive, props_service):
        canvases.update(mod.build())
        fps.update(mod.FOOTPRINTS)
    return canvases, ch1, fps


def resolve(furniture, fps):
    out = []
    for f in furniture:
        f = dict(f)
        if "footprint" not in f:
            fp = fps.get(f["sprite"])
            x, y = f["position"]
            f["footprint"] = None if fp is None else [int(x - fp[0] / 2), int(y - fp[1]), int(fp[0]), int(fp[1])]
        out.append(f)
    return out


def wall_rects(rows):
    """Solid rectangles for every non-walkable cell, merged into horizontal runs."""
    rects = []
    for y, row in enumerate(rows):
        x = 0
        while x < len(row):
            if row[x] in painter.WALKABLE:
                x += 1
                continue
            start = x
            while x < len(row) and row[x] not in painter.WALKABLE:
                x += 1
            rects.append([start * CELL, y * CELL, (x - start) * CELL, CELL])
    return rects


# ---------------------------------------------------------------- scenes ----

def tscn(room: dict, arch_path: str, sprite_paths: dict, sizes: dict) -> str:
    ext = [("Texture2D", arch_path, "arch")]
    used = sorted({f["sprite"] for f in room["furniture"]})
    for name in used:
        ext.append(("Texture2D", sprite_paths[name], "t_" + name))
    lines = [f'[gd_scene load_steps={len(ext) + 1} format=3]', '']
    lines += [f'[ext_resource type="{k}" path="{p}" id="{i}"]' for k, p, i in ext]
    lines.append('')
    shapes = []
    solids = wall_rects(room["rows"]) + [f["footprint"] for f in room["furniture"] if f.get("footprint")] + room.get("solids_px", [])
    for i, (x, y, w, h) in enumerate(solids):
        shapes.append((f"s{i}", x + w / 2, y + h / 2, w, h))
    exits = []
    for e in room["exits"]:
        if "rect_px" in e:
            x, y, w, h = e["rect_px"]
        else:
            x0, y0, x1, y1 = e["cells"]
            x, y, w, h = x0 * CELL, y0 * CELL, (x1 - x0) * CELL, (y1 - y0) * CELL
        exits.append((e, x + w / 2, y + h / 2, w, h))
    for sid, _, _, w, h in shapes:
        lines += [f'[sub_resource type="RectangleShape2D" id="{sid}"]', f'size = Vector2({w}, {h})', '']
    for i, (_, _, _, w, h) in enumerate(exits):
        lines += [f'[sub_resource type="RectangleShape2D" id="e{i}"]', f'size = Vector2({w}, {h})', '']
    W = len(room["rows"][0]) * CELL if "size_px" not in room else room["size_px"][0]
    H = len(room["rows"]) * CELL if "size_px" not in room else room["size_px"][1]
    lines += [f'[node name="{room["key"]}" type="Node2D"]', 'texture_filter = 1',
              f'metadata/title = "{room["title"]}"', f'metadata/map_size = Vector2({W}, {H})', '',
              '[node name="Architecture" type="Sprite2D" parent="."]', 'z_index = -10', 'texture = ExtResource("arch")', 'centered = false', '',
              '[node name="Objects" type="Node2D" parent="."]', 'y_sort_enabled = true', '']
    for f in room["furniture"]:
        w, h = sizes[f["sprite"]]
        x, y = f["position"]
        lines += [f'[node name="{f["name"]}" type="Sprite2D" parent="Objects"]', f'position = Vector2({x}, {y})',
                  f'texture = ExtResource("t_{f["sprite"]}")', 'centered = false', f'offset = Vector2({-(w // 2)}, {-h})', '']
    lines += ['[node name="Solids" type="StaticBody2D" parent="."]', 'collision_mask = 0', '']
    for sid, cx, cy, _, _ in shapes:
        lines += [f'[node name="{sid}" type="CollisionShape2D" parent="Solids"]', f'position = Vector2({cx}, {cy})', f'shape = SubResource("{sid}")', '']
    lines += ['[node name="Exits" type="Node2D" parent="."]', '']
    for i, (e, cx, cy, _, _) in enumerate(exits):
        lines += [f'[node name="{e["name"]}" type="Area2D" parent="Exits"]', f'position = Vector2({cx}, {cy})', 'collision_layer = 0',
                  'collision_mask = 2', f'metadata/target = "{e["target"]}"', f'metadata/spawn = "{e["spawn"]}"', '',
                  f'[node name="Shape" type="CollisionShape2D" parent="Exits/{e["name"]}"]', f'shape = SubResource("e{i}")', '']
    lines += ['[node name="Spawns" type="Node2D" parent="."]', '']
    for name, (x, y) in room["spawns"].items():
        lines += [f'[node name="{name}" type="Marker2D" parent="Spawns"]', f'position = Vector2({x}, {y})', '']
    lines += ['[node name="Sites" type="Node2D" parent="."]', '']
    for s in room.get("sites", []):
        lines += [f'[node name="{s["id"]}" type="Marker2D" parent="Sites"]', f'position = Vector2({s["point"][0]}, {s["point"][1]})',
                  f'metadata/label = "{s["label"]}"', '']
    return "\n".join(lines)


def main():
    canvases, ch1, fps = sprites_and_footprints()
    (OUT / "rooms").mkdir(parents=True, exist_ok=True)
    (OUT / "props").mkdir(parents=True, exist_ok=True)
    SCENES.mkdir(parents=True, exist_ok=True)
    manifest = {"source": "godot_csharp/tools/build_chapter2_scenes.py", "rooms": {}, "props": {}}
    sprite_paths, sizes = {}, {}
    for name, can in canvases.items():
        path = OUT / "props" / f"{name}.png"
        can.save(path)
        sprite_paths[name] = f"res://ShareAsset/Internal/Props/Chapter2/props/{name}.png"
        sizes[name] = (can.w, can.h)
        manifest["props"][name] = {"size": [can.w, can.h], "footprint": fps.get(name), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
    for name, can in ch1.items():
        if name not in sprite_paths:
            sprite_paths[name] = f"{CH1}/{name}.png"
            sizes[name] = (can.w, can.h)
    all_rooms = rooms.all_rooms()
    for room in all_rooms:
        room["furniture"] = resolve(room["furniture"], fps)
        missing = {f["sprite"] for f in room["furniture"]} - set(sprite_paths)
        assert not missing, (room["key"], missing)
        arch, anchors = painter.paint(room)
        arch.save(OUT / "rooms" / f"{room['key']}.png")
        (SCENES / f"{room['key']}.tscn").write_text(
            tscn(room, f"res://ShareAsset/Internal/Props/Chapter2/rooms/{room['key']}.png", sprite_paths, sizes), encoding="utf-8")
        manifest["rooms"][room["key"]] = {"title": room["title"], "cells": [len(room["rows"][0]), len(room["rows"])],
                                          "exits": room["exits"], "spawns": room["spawns"], "sites": room.get("sites", [])}
        print(room["key"], arch.w, arch.h, len(room["furniture"]))
    campus = campus_room.build(fps)
    for name, can in campus["extra_sprites"].items():
        can.save(OUT / "props" / f"{name}.png")
        sprite_paths[name] = f"res://ShareAsset/Internal/Props/Chapter2/props/{name}.png"
        sizes[name] = (can.w, can.h)
    campus["architecture"].save(OUT / "rooms" / "campus.png")
    (SCENES / "campus.tscn").write_text(tscn(campus, "res://ShareAsset/Internal/Props/Chapter2/rooms/campus.png", sprite_paths, sizes),
                                        encoding="utf-8")
    manifest["rooms"]["campus"] = {"title": campus["title"], "size_px": campus["size_px"], "exits": campus["exits"],
                                   "spawns": campus["spawns"], "sites": campus["sites"]}
    print("campus", *campus["size_px"], len(campus["furniture"]))
    # link check: every exit's target room and spawn exist
    spawns = {k: set(v["spawns"]) for k, v in manifest["rooms"].items()}
    bad = [(k, e["name"], e["target"], e["spawn"]) for k, v in manifest["rooms"].items() for e in v["exits"]
           if e["target"] not in spawns or e["spawn"] not in spawns[e["target"]]]
    assert not bad, bad
    (OUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=1), encoding="utf-8")
    print("ok", len(manifest["rooms"]), "rooms", len(canvases), "chapter-2 sprites")


if __name__ == "__main__":
    main()
