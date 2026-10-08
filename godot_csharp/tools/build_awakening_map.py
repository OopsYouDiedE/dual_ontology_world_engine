"""Assemble the chapter-one map of 乙区 (《门的这一侧》) on the chapter-two zone geometry.

Design: docs/design-standard.md 第二部分 §11 第 7 条 and 第三部分 §3.2. The 乙区 grid, walls,
doors and paint come from chapter2_art.rooms.zone("乙") (the same geometry chapter two reuses);
this script shifts it four cells east to show a strip of the 起居厅 beyond the isolation door,
furnishes it for chapter one and rewrites the map nodes of Awakening.tscn in place.

Run from the repository root:  python godot_csharp/tools/build_awakening_map.py
Then import in Godot:          godot --headless --editor --path godot_csharp --import

Writes
  godot_csharp/ShareAsset/Internal/Props/AwakeningEvidence/life_wing_yi.png  painted architecture (1x)
  godot_csharp/scenes/Awakening/map_layout.tres                             floors, furniture footprints, decals, asides
  godot_csharp/scenes/Awakening/Awakening.tscn                              map nodes (lights, walls, furniture, doors)
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from PIL import Image

GAME = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(GAME / "tools"))

from chapter2_art import painter, rooms  # noqa: E402
from first_awakening_art import architecture as A  # noqa: E402

CELL = 32
SHIFT = 4                      # cells of 起居厅 shown west of the isolation door
OX = SHIFT * CELL
INTERIOR = "res://ShareAsset/Internal/Props/FirstAwakeningInterior"
EVIDENCE = "res://ShareAsset/Internal/Props/AwakeningEvidence"
ARCH_PNG = GAME / "ShareAsset/Internal/Props/AwakeningEvidence/life_wing_yi.png"
LAYOUT_TRES = GAME / "scenes/Awakening/map_layout.tres"
SCENE = GAME / "scenes/Awakening/Awakening.tscn"

# Room blocks (left pixel of the 6-cell room; floor rows 3-7 north, 16-20 south).
NORTH_TOP, SOUTH_TOP = 96, 512
ROOMS = {  # key: (left, north?, owner, label)
    "R08": (608, True, "musician", "R-08"), "R05": (832, True, "detective", "R-05"), "EN": (1056, True, "", "空房"),
    "R07": (608, False, "cook", "R-07"), "R06": (832, False, "engineer", "R-06"), "ES": (1056, False, "", "空房"),
}

# Footprint (w, h, d): the solid base under a sprite anchored at its bottom centre, d pixels above the anchor.
FOOT = {
    "bookcase_empty": (40, 14, 0), "bookcase_study": (40, 14, 0), "bed_0": (36, 64, 4), "bed_sheeted": (36, 64, 4),
    "nightstand": (18, 12, 2), "wardrobe": (40, 16, 0), "desk_0": (58, 26, 2), "chair_back": (14, 10, 2),
    "chair_front": (14, 10, 2), "coat_stand": (12, 8, 0), "coat_stand_coat": (12, 8, 0), "paper_basket": (12, 8, 0),
    "paper_basket_empty": (12, 8, 0), "study_table": (132, 46, 8), "floor_lamp": (10, 8, 0), "armchair": (28, 16, 2),
    "globe": (16, 8, 0), "washstand": (48, 20, 2), "sideboard": (82, 22, 2), "range_stove": (42, 20, 2),
    "worktable": (96, 30, 8), "barrel": (18, 12, 0), "crate": (22, 10, 0), "sack": (14, 8, 0),
}
# Ordinary furniture may sort by its anchor; beds sort 40px higher so a sleeper lies on top of the mattress.
SORT_LIFT = {"bed_0": 40, "bed_sheeted": 40}


def F(name, sprite, x, y):
    w, h, d = FOOT.get(sprite, (0, 0, 0))
    fp = [x - w // 2, y - h - d, w, h] if w else None
    return {"name": name, "sprite": sprite, "position": [x, y], "footprint": fp}


def furniture():
    items = []
    for key, (L, north, owner, _) in ROOMS.items():
        top = NORTH_TOP if north else SOUTH_TOP
        n = key
        if owner:
            items += [F(f"Bookcase{n}", "bookcase_empty", L + 30, top + 14),
                      F(f"Bed{n}", "bed_0", L + 150, top + 70),
                      F(f"Nightstand{n}", "nightstand", L + 180, top + 24),
                      F(f"Desk{n}", "desk_0", L + 40, top + 96 if north else top + 104),
                      F(f"DeskChair{n}", "chair_back", L + 40, top + 118 if north else top + 126),
                      F(f"CoatStand{n}", "coat_stand_coat" if key == "R05" else "coat_stand", L + 178, top + 154),
                      F(f"PaperBasket{n}", "paper_basket" if key == "R05" else "paper_basket_empty", L + 22, top + 140)]
            items.append({"name": f"WallClock{n}", "sprite": "wall_clock", "position": [L + 150, top - 16], "footprint": None})
        else:
            items += [F(f"EmptyBookcase{n}", "bookcase_empty", L + 30, top + 14),
                      F(f"SheetedBed{n}", "bed_sheeted", L + 150, top + 70),
                      F(f"EmptyWardrobe{n}", "wardrobe", L + 40, top + 120)]
    items += [
        # 活动厅
        F("StudyBookcase", "bookcase_study", 350, 110),
        F("StudyTable", "study_table", 448, 270),
        F("StudyChairNW", "chair_front", 416, 218), F("StudyChairNE", "chair_front", 480, 218),
        F("StudyChairSW", "chair_back", 416, 294), F("StudyChairSE", "chair_back", 480, 294),
        F("ReadingChair", "armchair", 346, 176), F("StudyLamp", "floor_lamp", 560, 168), F("Globe", "globe", 340, 322),
        # 备餐间
        F("Washstand", "washstand", 352, 538), F("Range", "range_stove", 394, 534), F("Sideboard", "sideboard", 528, 538),
        F("Worktable", "worktable", 448, 626), F("PantryLamp", "floor_lamp", 562, 600),
        F("PantryBarrel", "barrel", 554, 664), F("PantryCrate", "crate", 340, 664), F("PantrySack", "sack", 364, 668),
        # 门厅、厕、储物
        F("HallLamp", "floor_lamp", 272, 334),
        F("Basin", "washstand", 224, 118),
        F("StoreCrate", "crate", 184, 600), F("StoreSack", "sack", 206, 608), F("StoreBarrel", "barrel", 268, 600),
        F("StoreCrateB", "crate", 262, 660),
    ]
    items.append({"name": "CopperTransmitter", "sprite": "transmitter", "position": [500, 84], "footprint": None})
    return items


def decor_and_floor(base):
    decor = []
    for d in base["decor"]:
        kind = d[0]
        if kind in ("side_door", "shelf"):
            continue
        if kind == "sconce" and d[2] == 1:
            continue                       # hall sconces are re-placed below around the notice board and the tube
        if kind in ("door", "window", "sconce"):
            decor.append((kind, d[1] + OX, *d[2:]))
    decor += [("sconce", 336, 1), ("sconce", 540, 1)]
    floor_work = []
    for key, (L, north, owner, _) in ROOMS.items():
        if owner:
            floor_work.append(("rug", L + 72, (NORTH_TOP if north else SOUTH_TOP) + 40, 56, 72, 0))
    return decor, floor_work


def plan():
    base = rooms.zone("乙")
    paint_rows = [" " * SHIFT + r for r in base["rows"]]
    # Game rows: the isolation doorway is shut (the door node draws it); everything west of it is outside.
    game_rows = [list(r) for r in paint_rows]
    for y in (11, 12):
        game_rows[y][SHIFT] = "#"
    game_rows = ["".join(r) for r in game_rows]
    regions = [("foyer", SHIFT, 11, SHIFT + 1, 13)] + [(n, x0 + SHIFT, y0, x1 + SHIFT, y1) for n, x0, y0, x1, y1 in base["regions"]]
    decor, floor_work = decor_and_floor(base)
    room = dict(key="awakening_yi", title="乙区", rows=paint_rows, regions=regions, floor=base["floor"], wall=base["wall"],
                decor=decor, floor_work=floor_work, furniture=furniture())
    return room, game_rows


def paint(room):
    canvas, anchors = painter.paint(room)
    # Slippers by the R-05 bed (baked like the chapter-one floor work).
    A.bake_floor_work(canvas, [], [], [(832 + 136, 168)])
    # The 起居厅 strip: parquet under settled ash, unlit (the door node covers it until the door opens).
    parquet = painter.material("parquet")
    H = len(room["rows"]) * CELL
    strip = painter._tile_to(parquet, OX, H)
    canvas.a[:, :OX] = strip
    canvas.dither(0, 0, OX, H, (118, 113, 106, 255), 0.45)
    canvas.dither(0, 0, OX, H, (141, 136, 128, 255), 0.2)
    for i in range(6):                     # the zone's west wall casts onto the hall floor
        canvas.rect(OX - 1 - i, 0, 1, H, (26, 18, 32, int(110 * (1 - i / 6))))
    return canvas, anchors


def sprite_size(sprite):
    root = GAME / ("ShareAsset/Internal/Props/AwakeningEvidence" if sprite == "bed_sheeted" else "ShareAsset/Internal/Props/FirstAwakeningInterior")
    with Image.open(root / f"{sprite}.png") as im:
        return im.size


def texture_path(sprite):
    return f"{EVIDENCE}/{sprite}.png" if sprite == "bed_sheeted" else f"{INTERIOR}/{sprite}.png"


def wall_rects(rows):
    rects = []
    for y, row in enumerate(rows):
        x = 0
        while x < len(row):
            if row[x] in ".D":
                x += 1
                continue
            start = x
            while x < len(row) and row[x] not in ".D":
                x += 1
            rects.append([start * CELL, y * CELL, (x - start) * CELL, CELL])
    return rects


def layout_data(room, game_rows, anchors):
    items = room["furniture"]
    shelves = []
    for f in items:
        if f["sprite"] in ("bookcase_empty",):
            x, y = f["position"]
            kind = "hand" if f["name"] == "BookcaseR05" else "even" if f["name"].startswith("Empty") else "plain"
            shelves.append([x - 20, y - 60, kind])
    asides = []
    for d in room["decor"]:
        if d[0] == "window":
            cx, top, w = d[1], d[2] * CELL, d[3]
            asides.append([[cx - w // 2, top + 6, w, 36], "窗。外面是灰的，什么也看不清。" if cx > 600 else "窗。雾贴着玻璃。"])
    for fw in room["floor_work"]:
        if fw[0] == "rug":
            asides.append([[fw[1], fw[2], fw[3], fw[4]], "地毯。踩得发亮的地方，在床边。" if fw[1] == 832 + 72 else "地毯。"])
    asides.append([[832 + 136, 168, 9, 8], "一双拖鞋。摆得很正，鞋头朝外。"])
    return {
        "cell": CELL, "size": [len(game_rows[0]), len(game_rows)], "rows": game_rows,
        "furniture": [{"name": f["name"], "sprite": f["sprite"], "position": f["position"], "footprint": f["footprint"]} for f in items],
        "shelves": shelves, "asides": asides,
        "sconces": [[s["x"], s["y"]] for s in anchors["sconces"]],
        "windows": [[d[1], d[2] * CELL] for d in room["decor"] if d[0] == "window"],
        "rooms": {k: {"left": L, "north": north, "owner": owner, "label": label} for k, (L, north, owner, label) in ROOMS.items()},
    }


# ------------------------------------------------------------------ scene ----

def split_blocks(text):
    """A .tscn as [header, block, block, ...]; a block runs from its [tag] line to the next one."""
    parts = re.split(r"(?m)^(?=\[)", text)
    return [p for p in parts if p]


def block_tag(block):
    return block.split("\n", 1)[0]


def node_name(block):
    m = re.match(r'\[node name="([^"]+)"(?: type="[^"]+")?(?: parent="([^"]*)")?', block)
    if not m:
        return None
    parent = m.group(2)
    return m.group(1) if parent in (None, ".") else f"{parent}/{m.group(1)}"


def light(name, x, y, color, energy, tex, flicker):
    lines = [f'[node name="{name}" type="PointLight2D" parent="Lighting"]', f"position = Vector2({x}, {y})",
             f"color = Color({color})", f"energy = {energy}", f'texture = SubResource("{tex}")']
    if flicker:
        lines.append(f"metadata/base_energy = {energy}")
    return "\n".join(lines) + "\n\n"


WARM, COOL, AMBER, GAS, LAVENDER = "1, 0.788235, 0.541176, 1", "0.72549, 0.803922, 0.901961, 1", "1, 0.603922, 0.352941, 1", \
    "1, 0.760784, 0.478431, 1", "0.776471, 0.721569, 0.847059, 1"


def lighting(data):
    out = ['[node name="Lighting" type="Node2D" parent="."]\n\n']
    n = 0
    for key, (L, north, owner, _) in ROOMS.items():
        if owner:
            out.append(light(f"Lamp{n}", L + 180, (NORTH_TOP if north else SOUTH_TOP) - 6, WARM, 0.85, "light_220", True)); n += 1
    for cx, top in data["windows"]:
        out.append(light(f"Lamp{n}", cx, top + 72, COOL, 0.55, "light_190", False)); n += 1
    for x, y, c, e, t, f in [(360, 150, WARM, 0.9, "light_280", True), (560, 160, WARM, 0.9, "light_280", True),
                             (394, 524, AMBER, 0.5, "light_150", True), (562, 590, WARM, 0.9, "light_280", True),
                             (272, 326, WARM, 0.85, "light_240", True), (152, 384, LAVENDER, 0.4, "light_200", False),
                             (224, 140, WARM, 0.5, "light_150", True), (224, 600, WARM, 0.45, "light_150", True)]:
        out.append(light(f"Lamp{n}", x, y, c, e, t, f)); n += 1
    for i, (x, y) in enumerate(data["sconces"]):
        out.append(light(f"Sconce{i}", x, y, GAS, 0.55, "light_120", True))
    for i, (cx, top) in enumerate(data["windows"]):
        out.append("\n".join([f'[node name="WindowDust{i}" type="CPUParticles2D" parent="Lighting"]', f"position = Vector2({cx}, {top + 90})",
                              "z_index = 5", "amount = 10", "lifetime = 7.0", "preprocess = 7.0", "emission_shape = 3",
                              "emission_rect_extents = Vector2(16.8, 24)", "direction = Vector2(0.3, -1)", "spread = 40.0",
                              "gravity = Vector2(0, 0)", "initial_velocity_min = 1.5", "initial_velocity_max = 4.0",
                              'color_ramp = SubResource("dust_fade")']) + "\n\n")
    return "".join(out)


def rewrite_scene(data, items):
    text = SCENE.read_text(encoding="utf-8")
    blocks = split_blocks(text)
    header, rest = blocks[0], blocks[1:]
    ext, sub, nodes = [], [], []
    for b in rest:
        tag = block_tag(b)
        (ext if tag.startswith("[ext_resource") else sub if tag.startswith("[sub_resource") else nodes).append(b)

    # ext_resources: keep everything that is not a map texture; add the textures this map uses.
    def ext_id(b):
        return re.search(r'id="([^"]+)"', b).group(1)
    keep_ext = [b for b in ext if 'type="Texture2D"' not in b]
    sprites = sorted({f["sprite"] for f in items})
    tex_ids = {s: s for s in sprites}
    new_ext = keep_ext + [f'[ext_resource type="Texture2D" path="{texture_path(s)}" id="{s}"]\n' for s in sprites]
    new_ext += [f'[ext_resource type="Texture2D" path="res://ShareAsset/Internal/Props/AwakeningEvidence/{n}.png" id="{n}"]\n'
                for n in ("life_wing_yi", "isolation_door_side", "isolation_door_side_open", "demon_prints_side")]
    if new_ext:
        new_ext[-1] = new_ext[-1].rstrip("\n") + "\n\n"

    # sub_resources: drop old wall/foot shapes and the quilt atlas; regenerate them.
    keep_sub = [b for b in sub if not re.search(r'id="(wall_\d+|foot_\d+|bed_quilt)"', b)]
    walls = wall_rects(data["rows"])
    feet = [f for f in items if f["footprint"]]
    gen_sub = ['[sub_resource type="AtlasTexture" id="bed_quilt"]\natlas = ExtResource("bed_0")\nregion = Rect2(0, 30, 36, 34)\n\n']
    gen_sub += [f'[sub_resource type="RectangleShape2D" id="wall_{i}"]\nsize = Vector2({w}, {h})\n\n' for i, (_, _, w, h) in enumerate(walls)]
    gen_sub += [f'[sub_resource type="RectangleShape2D" id="foot_{i}"]\nsize = Vector2({f["footprint"][2]}, {f["footprint"][3]})\n\n'
                for i, f in enumerate(feet)]

    by_name = {node_name(b): b for b in nodes}

    def keep(name):
        return by_name[name]

    out = [keep("Awakening")]
    out.append('[node name="WakePosition" type="Marker2D" parent="."]\nposition = Vector2(982, 184)\n\n')
    out.append('[node name="MeetingSpots" type="Node2D" parent="."]\n\n')
    for n, (x, y) in {"Engineer": (362, 240), "Chef": (448, 318), "Musician": (536, 240), "Detective": (448, 200)}.items():
        out.append(f'[node name="{n}" type="Marker2D" parent="MeetingSpots"]\nposition = Vector2({x}, {y})\n\n')
    out.append(keep("Darkness"))
    out.append(keep("Ambient"))
    out.append(lighting(data))
    out.append('[node name="LifeWingArchitecture" type="Sprite2D" parent="."]\ntexture = ExtResource("life_wing_yi")\ncentered = false\n\n')
    out.append('[node name="WallSolids" type="StaticBody2D" parent="."]\n\n')
    for i, (x, y, w, h) in enumerate(walls):
        out.append(f'[node name="CollisionShape2D{i}" type="CollisionShape2D" parent="WallSolids"]\n'
                   f'position = Vector2({x + w / 2}, {y + h / 2})\nshape = SubResource("wall_{i}")\n\n')
    out.append('[node name="FurnitureSolids" type="Node2D" parent="."]\n\n')
    for i, f in enumerate(feet):
        x, y, w, h = f["footprint"]
        out.append(f'[node name="{f["name"]}" type="StaticBody2D" parent="FurnitureSolids"]\nposition = Vector2({x + w / 2}, {y + h / 2})\n\n'
                   f'[node name="Shape" type="CollisionShape2D" parent="FurnitureSolids/{f["name"]}"]\nshape = SubResource("foot_{i}")\n\n')
    out.append(keep("RoomDetails"))
    out.append(keep("ObjectsAndPeople"))
    for f in items:
        w, h = sprite_size(f["sprite"])
        x, y = f["position"]
        lift = SORT_LIFT.get(f["sprite"], 0)
        out.append(f'[node name="{f["name"]}" type="Sprite2D" parent="ObjectsAndPeople"]\nposition = Vector2({x}, {y - lift})\n'
                   f'offset = Vector2({-(w // 2)}, {lift - h})\ncentered = false\ntexture = ExtResource("{tex_ids[f["sprite"]]}")\n\n')
    # The isolation door in the foyer's west wall; the living hall behind it stays covered until it opens.
    out.append('[node name="MechanicalIsolationDoor" type="Node2D" parent="ObjectsAndPeople"]\nposition = Vector2(72, 288)\n\n')
    out.append('[node name="DoorFace" type="Sprite2D" parent="ObjectsAndPeople/MechanicalIsolationDoor"]\ncentered = false\n'
               'texture = ExtResource("isolation_door_side")\n\n')
    out.append('[node name="DoorGap" type="Sprite2D" parent="ObjectsAndPeople/MechanicalIsolationDoor"]\nvisible = false\ncentered = false\n'
               'texture = ExtResource("isolation_door_side_open")\n\n')
    out.append('[node name="DemonPrints" type="Sprite2D" parent="ObjectsAndPeople/MechanicalIsolationDoor"]\nvisible = false\nz_index = 1\n'
               'centered = false\noffset = Vector2(-64, 0)\ntexture = ExtResource("demon_prints_side")\n\n')
    out.append('[node name="LoungeCover" type="Polygon2D" parent="ObjectsAndPeople/MechanicalIsolationDoor"]\nz_index = 40\n'
               'color = Color(0.203922, 0.156863, 0.196078, 1)\npolygon = PackedVector2Array(-72, -288, 56, -288, 56, 416, -72, 416)\n\n')
    out.append('[node name="LoungeCoverInner" type="Polygon2D" parent="ObjectsAndPeople/MechanicalIsolationDoor"]\nz_index = 40\n'
               'color = Color(0.113725, 0.0862745, 0.121569, 1)\npolygon = PackedVector2Array(-72, -288, 54, -288, 54, 414, -72, 414)\n\n')
    starts = {"侦探": (982, 144), "工程师": (930, 590), "厨师": (706, 590), "音乐家": (706, 210)}
    for actor in ("侦探", "工程师", "厨师", "音乐家"):
        block = keep(f"ObjectsAndPeople/{actor}")
        x, y = starts[actor]
        out.append(re.sub(r"position = Vector2\([^)]*\)", f"position = Vector2({x}, {y})", block, count=1))
        for child in ("CollisionShape2D", "ContactShadow", "Sprite"):
            out.append(keep(f"ObjectsAndPeople/{actor}/{child}"))
    out.append('[node name="BedQuiltForeground" type="Sprite2D" parent="ObjectsAndPeople"]\nposition = Vector2(982, 166)\n'
               'offset = Vector2(-18, -34)\ncentered = false\ntexture = SubResource("bed_quilt")\n\n')
    for i, (key, (L, north, owner, _)) in enumerate(ROOMS.items(), start=1):
        top = NORTH_TOP if north else SOUTH_TOP
        door_y = 352 if north else 512
        bounds = f"Rect2({L}, 32, 192, 224)" if north else f"Rect2({L}, {top}, 192, 160)"
        out.append(f'[node name="DormitoryDoor{i}" type="Node2D" parent="ObjectsAndPeople"]\nposition = Vector2({L + 96}, {door_y})\n'
                   f'script = ExtResource("room_door")\ndorm_owner = "{owner or "detective"}"\ninterior_bounds = {bounds}\n\n'
                   f'[node name="Body" type="StaticBody2D" parent="ObjectsAndPeople/DormitoryDoor{i}"]\n\n'
                   f'[node name="LeafCollision" type="CollisionShape2D" parent="ObjectsAndPeople/DormitoryDoor{i}/Body"]\n'
                   f'position = Vector2(0, -4)\nshape = SubResource("dorm_leaf")\n\n')
    # 地图场景到宿舍门为止：现场、界面与镜头由共用的 Host/Main / WorldView / UISystem 提供。
    text = header + "".join(new_ext) + "".join(keep_sub) + "".join(gen_sub) + "".join(out)
    SCENE.write_text(text.rstrip("\n") + "\n", encoding="utf-8", newline="\n")
    return len(walls), len(feet)


def main():
    room, game_rows = plan()
    canvas, anchors = paint(room)
    ARCH_PNG.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(ARCH_PNG)
    data = layout_data(room, game_rows, anchors)
    body = json.dumps(data, ensure_ascii=False, indent=1)
    LAYOUT_TRES.write_text('[gd_resource type="Resource" format=3]\n\n[resource]\nmetadata/data = ' + body + "\n", encoding="utf-8", newline="\n")
    walls, feet = rewrite_scene(data, room["furniture"])
    for row in game_rows:
        print(row)
    print(f"map {canvas.w}x{canvas.h}, {walls} wall shapes, {feet} furniture solids, {len(room['furniture'])} sprites")


if __name__ == "__main__":
    main()
