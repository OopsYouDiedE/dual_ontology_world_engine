"""Bind reusable floor terrain to selected existing rooms, preserving geometry.

Only an external script and a presentation node are inserted. Runtime reads
the original published terrain_rows. Full scene regeneration uses attach_skin.
"""
import argparse
import re
from pathlib import Path
from chapter2_art import rooms

ROOT = Path(__file__).resolve().parents[2]
SCENES = ROOT / "godot_csharp/scenes/Chapter2/Maps"
SELECTED = {"central_hall": 0, "granary": 2}
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
    packed = ", ".join(str(v) for circle in circles for v in circle)
    node = ('[node name="RedrawnTerrainSkin" type="Node2D" parent="."]\n'
            'z_index = -9\nscript = ExtResource("redrawn_floor_skin")\n'
            f'floor_terrain = {SELECTED[key]}\n'
            f'preserved_discs = PackedVector3Array({packed})\n\n')
    before = '[node name="Objects" type="Node2D" parent="."]'
    if before not in text:
        raise ValueError(f"{key}: missing Objects insertion point")
    return text.replace(before, node + before, 1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--maps", nargs="+", choices=SELECTED, default=list(SELECTED))
    args = parser.parse_args()
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
