"""Bind reusable floor terrain to selected existing rooms, preserving geometry.

Only an external script and a presentation node are inserted. Runtime reads
the original published terrain_rows. Full scene regeneration uses attach_skin.
"""
import argparse
import json
import re
from pathlib import Path
from chapter2_art import rooms

ROOT = Path(__file__).resolve().parents[2]
SCENES = ROOT / "godot_csharp/scenes/Chapter2/Maps"
SELECTED = {"central_hall": "central_hall", "granary": "granary"}
SCRIPT = "res://scenes/Chapter2/Field/RoomTerrainSkin.gd"


def attach_skin(text: str, room: dict) -> str:
    key = room["key"]
    if key not in SELECTED or '[node name="RedrawnTerrainSkin"' in text:
        return text
    declaration = f'[ext_resource type="Script" path="{SCRIPT}" id="redrawn_floor_skin"]\n'
    position = text.index("[sub_resource") if "[sub_resource" in text else text.index("[node ")
    text = text[:position] + declaration + "\n" + text[position:]
    text = re.sub(r"load_steps=(\d+)", lambda m: f"load_steps={int(m[1]) + 1}", text, count=1)
    circles = [entry[1:] for entry in room.get("floor_work", []) if entry[0] == "mosaic"]
    center = f'Vector2({circles[0][0]}, {circles[0][1]})' if circles else 'Vector2(-1, -1)'
    node = ('[node name="RedrawnTerrainSkin" type="Node2D" parent="."]\n'
            'z_index = -9\nscript = ExtResource("redrawn_floor_skin")\n'
            f'authored_room = "{SELECTED[key]}"\n'
            f'medallion_center = {center}\n\n')
    before = '[node name="Objects" type="Node2D" parent="."]'
    if before not in text:
        raise ValueError(f"{key}: missing Objects insertion point")
    return text.replace(before, node + before, 1)


def publish_art_finish_spec():
    """Publish explicit window crops and wall materials from authoring recipes."""
    from build_living_floor import PARTS
    from chapter2_art.painter import Grid, _face_runs
    recipe = {room["key"]: room for room in rooms.all_rooms()}
    spec = {}
    for key, room in recipe.items():
        windows = [[item[1], item[2] * 32, item[3]] for item in room.get("decor", []) if item[0] == "window"]
        panels = []
        for x0, x1, y, region, mark in _face_runs(Grid(room)):
            style = room.get("wall", {}).get(region, room.get("wall", {}).get("default", ("plaster", "CREAM")))
            if mark == "W" and style[0] in ("paper", "plaster", "panel"):
                panels += [[x * 32, (y + 1) * 32] for x in range(x0, x1)]
        spec[key] = [{"origin": [0, 0], "texture": f"res://ShareAsset/Internal/Props/Chapter2/rooms/{key}.png", "windows": windows, "panels": panels}]
    spec["living_floor"] = []
    for key, offset in PARTS.items():
        part = spec[key][0]
        if key == "living_hall":
            for lower in (False, True):
                spec["living_floor"].append(dict(part, origin=[offset[0], offset[1] + (192 if lower else 0)],
                    windows=[w for w in part["windows"] if (w[1] >= 544) == lower],
                    panels=[p for p in part["panels"] if (p[1] >= 544) == lower]))
        else:
            spec["living_floor"].append(dict(part, origin=list(offset)))
    target = ROOT / "godot_csharp/Contracts/room_art_finish.json"
    target.write_text(json.dumps(spec, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--maps", nargs="+", choices=SELECTED, default=list(SELECTED))
    args = parser.parse_args()
    publish_art_finish_spec()
    recipes = {room["key"]: room for room in rooms.all_rooms()}
    for key in args.maps:
        path = SCENES / (key + ".tscn")
        original = path.read_text(encoding="utf-8")
        updated = attach_skin(original, recipes[key])
        if updated != original:
            path.write_text(updated, encoding="utf-8")
        print(f"{key}: floor skin bound to published terrain_rows; original geometry retained")


if __name__ == "__main__":
    main()
