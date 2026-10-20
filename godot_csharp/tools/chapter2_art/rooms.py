"""Chapter-2 interior rooms as plain data: grid, regions, materials, decor, furniture, exits.

Scale follows chapter 1: 32px cells, a bedroom about 6x5 floor cells. Interiors
keep north up, except that a hall's south wall may be drawn as a front-wall
elevation ('G'), as in chapter 1. Positions are pixels; furniture positions
are bottom-centre floor anchors. Every exit names a target room and spawn.
"""
from __future__ import annotations

CELL = 32

# Stable text slots; geometry recipes below remain independent of their wording.
TEXT_SITES = {
    "living_hall": ('watch_point', 'hall_notice', 'fireplace'),
    "zone_jia": ('transmitter', 'zone_rules', 'isolation_gate'),
    "zone_yi": ('transmitter', 'zone_rules', 'isolation_gate', 'bed'),
    "zone_bing": ('transmitter', 'zone_rules', 'isolation_gate'),
    "zone_ding": ('transmitter', 'zone_rules', 'isolation_gate'),
    "central_hall": ('relay', 'mouthpiece', 'central_notice', 'watch_point'),
    "lecture": ('blackboard', 'timetable'),
    "archive": ('lock_core', 'seal_boxes', 'name_warning', 'red_ink', 'watch_point'),
    "counterweight": ('counterweight',),
    "resonance": ('step_marks', 'silent_door'),
    "clinic": ('reagent', 'empty_bowl'),
    "logistics": ('ration_counter', 'ledger', 'ration_notice', 'watch_point'),
    "granary": ('latch', 'stock'),
    "greenhouse": ('valve', 'gauge'),
}


class Plan:
    def __init__(self, w: int, h: int, fill: str = "#"):
        self.w, self.h = w, h
        self.g = [[fill] * w for _ in range(h)]

    def set(self, x, y, m):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.g[y][x] = m

    def room(self, x0, y0, x1, y1, face=True):
        """Interior cells x0..x1-1, y0..y1-1; the first two rows are the north wall face."""
        for y in range(y0, y1):
            for x in range(x0, x1):
                self.g[y][x] = "W" if face and y == y0 else "w" if face and y == y0 + 1 else "."

    def door_h(self, x, y, w=2, depth=3):
        """Doorway through a horizontal wall at row y and the face rows below it."""
        for dy in range(depth):
            for dx in range(w):
                if dy == 0 or self.g[y + dy][x + dx] in "Ww":
                    self.g[y + dy][x + dx] = "D"

    def door_v(self, x, y, h=2):
        for dy in range(h):
            self.g[y + dy][x] = "D"

    def fill(self, x0, y0, x1, y1, m):
        for y in range(y0, y1):
            for x in range(x0, x1):
                self.g[y][x] = m

    def rows(self):
        return ["".join(r) for r in self.g]


def P(cx, cy, dx=0, dy=0):
    """Pixel anchor from a cell coordinate."""
    return [int(cx * CELL + dx), int(cy * CELL + dy)]


def F(name, sprite, pos):
    return {"name": name, "sprite": sprite, "position": pos}


def exit_(name, x0, y0, x1, y1, target, spawn):
    return {"name": name, "cells": [x0, y0, x1, y1], "target": target, "spawn": spawn}


# ----------------------------------------------------------------- 寝区 ----

ZONES = {
    "甲": dict(numbers=["01", "02", "03", "04"], wall=("paper", "LINEN", "lattice", "CLARET"), floor="oak", east=False, extra="ink"),
    "乙": dict(numbers=["05", "06", "08", "07"], wall=("paper", "TEAL", "pinstripe", "OCHRE"), floor="oak", east=True, extra="brass"),
    "丙": dict(numbers=["09", "10", "11", "12"], wall=("paper", "PLUM", "damask", "OCHRE"), floor="herringbone", east=False, extra="drape"),
    "丁": dict(numbers=["13", "14", "15", "16"], wall=("tile",), floor="whitetile", east=True, extra="tile"),
}
ZONE_KEY = {"甲": "zone_jia", "乙": "zone_yi", "丙": "zone_bing", "丁": "zone_ding"}
HALL_DOOR = {"甲": "west_north", "丙": "west_south", "丁": "east_north", "乙": "east_south"}


def _bedroom(prefix, x0, top, occupied, south):
    """x0: first floor cell column; top: first floor pixel row."""
    bx = x0 * CELL
    items = [F(f"{prefix}Bed", "bed_0", [bx + 38, top + 70]),
             F(f"{prefix}Wardrobe", "wardrobe", [bx + 156, top + 18])]
    if occupied:
        items += [F(f"{prefix}Nightstand", "nightstand", [bx + 70, top + 26]),
                  F(f"{prefix}Desk", "desk_0", [bx + 150, top + 132]),
                  F(f"{prefix}Chair", "chair_back", [bx + 150, top + 150]),
                  F(f"{prefix}Coat", "coat_stand", [bx + 20, top + 150])]
    else:
        items += [F(f"{prefix}Crate", "crate", [bx + 156, top + 140])]
    return items


def zone(label: str) -> dict:
    z = ZONES[label]
    p = Plan(36, 22)
    for x0 in (1, 8, 15):
        p.room(x0, 1, x0 + 6, 8)
        p.room(x0, 14, x0 + 6, 21)
    p.room(1, 9, 22, 13)              # 中廊
    p.room(22, 1, 30, 13)             # 活动厅
    p.room(22, 14, 30, 21)            # 备餐间
    p.room(31, 1, 35, 7)              # 厕
    p.room(31, 8, 35, 14)             # 门厅
    p.room(31, 15, 35, 21)            # 储物
    for x0 in (1, 8, 15):
        p.door_h(x0 + 2, 8)           # 北排房门 → 中廊立面
        p.door_h(x0 + 2, 13)          # 南排房门
    p.door_h(25, 13)                  # 活动厅 → 备餐间
    p.door_v(30, 11)                  # 活动厅 → 门厅
    p.door_h(32, 7)                   # 门厅 → 厕
    p.door_h(32, 14)                  # 门厅 → 储物
    p.door_v(35, 11)                  # 隔离门 → 起居厅
    regions = [("hall", 22, 1, 30, 13), ("prep", 22, 14, 30, 21), ("wc", 31, 1, 35, 7), ("foyer", 31, 8, 35, 14),
               ("store", 31, 15, 35, 21), ("corridor", 1, 9, 22, 13), ("room", 0, 0, 22, 22)]
    furniture, decor, floor_work = [], [], []
    numbers = iter(z["numbers"])
    for i, x0 in enumerate((1, 8, 15)):
        occupied = i > 0                                   # 靠外端（西）一间空着
        nn = next(numbers) if occupied else ""
        sn_ = ""
        furniture += _bedroom(f"N{i}", x0, 3 * CELL, occupied, False)
        decor.append(("door", (x0 + 2) * CELL, 9, nn))
        if occupied:
            sn_ = next(numbers)
        furniture += _bedroom(f"S{i}", x0, 16 * CELL, occupied, True)
        decor.append(("door", (x0 + 2) * CELL, 14, sn_))
        decor += [("window", (x0 + 3) * CELL, 1, 28)]
        floor_work.append(("rug", (x0 + 1) * CELL + 8, 3 * CELL + 30, 64, 72, 0 if label == "乙" else 1 if label == "甲" else 2 if label == "丙" else 3))
    decor += [("sconce", 6 * CELL + 16, 9), ("sconce", 13 * CELL + 16, 9), ("sconce", 20 * CELL + 16, 9),
              ("window", 26 * CELL, 1, 34), ("sconce", 23 * CELL + 16, 1), ("sconce", 29 * CELL, 1),
              ("door", 25 * CELL, 14, ""), ("door", 32 * CELL, 8, ""), ("door", 32 * CELL, 15, ""),
              ("side_door", 35, 11, 2, "e"), ("shelf", 23 * CELL + 6, 16 * CELL - 4)]
    furniture += [
        F("Transmitter", "transmitter", P(25, 3, 0, 2)), F("ZoneRules", "notice_board", P(28, 3, 0, 2)),
        F("StudyTable", "study_table", P(26, 9, 0, 8)), F("StudyChairNW", "chair_front", P(25, 7, 0, 30)),
        F("StudyChairNE", "chair_front", P(27, 7, 0, 30)), F("StudyChairSW", "chair_back", P(25, 9, 0, 30)),
        F("StudyChairSE", "chair_back", P(27, 9, 0, 30)), F("HallBookcase", "bookcase_0", P(23, 3, 4, 14)),
        F("HallLamp", "floor_lamp", P(29, 11, 10, 20)), F("Armchair", "armchair", P(29, 6, 0, 24)),
        F("Range", "range_stove", P(28, 16, 8, 22)), F("Sideboard", "sideboard", P(24, 16, 0, 24)),
        F("Worktable", "worktable", P(26, 19, 0, 10)), F("Barrel", "barrel", P(29, 19, 8, 30)),
        F("Washstand", "washstand", P(33, 3, 0, 22)),
        F("CoatStand", "coat_stand", P(31, 10, 12, 30)), F("ShoeBench", "shoe_bench", P(33, 13, 0, 26)),
        F("StoreCrate", "crate", P(32, 18, 0, 20)), F("StoreSack", "sack", P(33, 19, 10, 20)), F("StoreBarrel", "barrel", P(34, 18, 0, 20)),
    ]
    extra = {"ink": [F("InkShelf", "archive_shelf", P(23, 11, 30, 26))],
             "brass": [F("ToolBench", "worktable", P(26, 5, 0, 20))],
             "drape": [F("Screen", "folding_screen", P(28, 11, 0, 26)), F("MusicStand", "music_stand", P(24, 12, 0, 20))],
             "tile": [F("MedCabinet", "medicine_cabinet", P(23, 11, 30, 26))]}[z["extra"]]
    furniture += extra
    room = dict(key=ZONE_KEY[label], title_key=f"资源.地图.{ZONE_KEY[label]}.标题", rows=p.rows(), regions=regions,
                floor={"room": z["floor"], "corridor": "plank", "hall": z["floor"], "prep": "quarry", "wc": "whitetile",
                       "foyer": "flagstone", "store": "plank", "default": "plank"},
                wall={"room": z["wall"], "corridor": ("plaster", "CREAM"), "hall": z["wall"], "prep": ("tile",),
                      "wc": ("tile",), "foyer": ("plaster", "CREAM"), "store": ("plaster", "LINEN")},
                decor=decor, floor_work=floor_work, furniture=furniture,
                exits=[exit_("isolation_gate", 35, 11, 36, 13, "living_hall", HALL_DOOR[label])],
                spawns={"from_hall": P(34, 12, 0, 16)},
                sites=[{"id": "transmitter", "point": P(25, 4, 0, 8), "label_key": f"资源.场点.{ZONE_KEY[label]}.transmitter.标签"},
                       {"id": "zone_rules", "point": P(28, 4, 0, 8), "label_key": f"资源.场点.{ZONE_KEY[label]}.zone_rules.标签"},
                       {"id": "isolation_gate", "point": P(34, 12, 16, 0), "label_key": f"资源.场点.{ZONE_KEY[label]}.isolation_gate.标签"}])
    if z["east"]:
        room = mirror(room)
    if room["key"] == "zone_yi":
        room["sites"].append({"id": "bed", "point": (858, 196), "label_key": "资源.场点.zone_yi.bed.标签"})
        evidence = "res://ShareAsset/Internal/Props/AwakeningEvidence"
        interior = "res://ShareAsset/Internal/Props/FirstAwakeningInterior"
        room["furniture"] += [
            dict(F("InnerBolt", "inner_bolt", [22, 422]), texture=f"{evidence}/isolation_door_side.png",
                 texture_region=[74, 86, 8, 48], footprint=None),
            dict(F("AshFan", "ash_fan", [31, 378]), texture=f"{evidence}/ash_fan.png",
                 rotation_degrees=90, z_index=-1, footprint=None),
            dict(F("Wedge", "wedge", [62, 428]), texture=f"{evidence}/wedge.png", footprint=None),
            dict(F("Score", "score", [522, 228]), texture=f"{evidence}/score.png",
                 draw_offset=[-9, -34], footprint=None),
            dict(F("Slate", "slate", [368, 296]), texture=f"{evidence}/slate.png",
                 draw_offset=[-10, -44], footprint=None),
            dict(F("Jar", "jar", [368, 536]), texture=f"{evidence}/jar.png",
                 draw_offset=[-7, -48], footprint=None),
            dict(F("R05Shelf", "bookcase_empty", [800, 110]), texture=f"{interior}/bookcase_empty.png",
                 footprint=[780, 96, 40, 14]),
            dict(F("R05ShelfTrace", "shelf_dust_hand", [800, 110]), texture=f"{evidence}/shelf_dust_hand.png",
                 footprint=None),
        ]
        # Geometry identifiers only; native presentation supplies player-visible labels.
        room["geometry_sites"] = [
            {"id": "E1", "point": [26, 396]}, {"id": "E2", "point": [62, 422]},
            {"id": "E6", "point": [522, 202]}, {"id": "E7", "point": [368, 258]},
            {"id": "E10", "point": [368, 502]}, {"id": "E11", "point": [806, 110]},
            {"id": "hall_copper_seat", "point": [368, 240]},
            {"id": "hall_tea_side", "point": [320, 300]},
            {"id": "hall_window_side", "point": [260, 280]},
        ]
    return room


def mirror(room: dict) -> dict:
    """Mirror a room left-right (east zones); faces stay on north walls."""
    W = len(room["rows"][0])
    MW = W * CELL
    flip = lambda x: MW - x
    room = dict(room)
    room["rows"] = [r[::-1] for r in room["rows"]]
    room["regions"] = [(n, W - x1, y0, W - x0, y1) for n, x0, y0, x1, y1 in room["regions"]]
    dec = []
    for d in room["decor"]:
        k = d[0]
        if k in ("door", "archway"):
            dec.append((k, MW - d[1] - 64, *d[2:]))
        elif k in ("window", "sconce", "painting"):
            dec.append((k, flip(d[1]), *d[2:]))
        elif k == "side_door":
            dec.append((k, W - 1 - d[1], d[2], d[3], "w" if d[4] == "e" else "e"))
        elif k == "shelf":
            dec.append((k, MW - d[1] - 70, d[2]))
        else:
            dec.append(d)
    room["decor"] = dec
    fw = []
    for f in room.get("floor_work", []):
        if f[0] in ("rug",):
            fw.append((f[0], MW - f[1] - f[3], *f[2:]))
        else:
            fw.append(f)
    room["floor_work"] = fw
    room["furniture"] = [dict(f, position=[flip(f["position"][0]), f["position"][1]]) for f in room["furniture"]]
    room["exits"] = [dict(e, cells=[W - e["cells"][2], e["cells"][1], W - e["cells"][0], e["cells"][3]]) for e in room["exits"]]
    room["spawns"] = {k: [flip(v[0]), v[1]] for k, v in room["spawns"].items()}
    room["sites"] = [dict(s, point=[flip(s["point"][0]), s["point"][1]]) for s in room["sites"]]
    return room


# --------------------------------------------------------------- 起居厅 ----

def living_hall() -> dict:
    p = Plan(16, 40)
    p.room(1, 1, 15, 31)
    p.fill(1, 15, 15, 16, "S")            # 北台与南台之间的台阶
    p.room(1, 32, 15, 39)                 # 浴室盥洗
    p.door_h(7, 0)                        # 北门
    p.door_h(7, 31)                       # → 浴室
    for x in (0, 15):
        p.door_v(x, 7)                    # 甲 / 丁
        p.door_v(x, 15)                   # 夹道侧门
        p.door_v(x, 23)                   # 丙 / 乙
    regions = [("bath", 1, 32, 15, 39), ("hall", 0, 0, 16, 32)]
    furniture = [
        F("Fireplace", "fireplace", P(4, 3, 0, 4)), F("BookcaseW", "bookcase_study", P(2, 3, 0, 14)),
        F("BookcaseE", "bookcase_study_b", P(12, 3, 0, 14)), F("NoticeBoard", "notice_board", P(11, 3, 0, 2)),
        F("ArmchairA", "armchair", P(3, 6, 0, 10)), F("ArmchairB", "armchair", P(6, 6, 0, 10)),
        F("FireSide", "side_table_plain", P(4, 6, 24, 6)), F("ReadingLamp", "floor_lamp", P(13, 7, 0, 10)),
        F("ShoeBenchN", "shoe_bench", P(10, 13, 0, 24)), F("CoatStandN", "coat_stand", P(13, 12, 0, 20)),
        F("BenchN", "long_bench", P(8, 20, 0, 4)), F("LongTable", "long_table", P(8, 22, 0, 4)),
        F("BenchS", "long_bench", P(8, 23, 0, 12)), F("LampW", "floor_lamp", P(2, 21, 8, 0)),
        F("Piano", "upright_piano", P(12, 27, 8, 20)), F("PianoStool", "piano_stool", P(12, 28, 8, 20)),
        F("Chess", "chess_table", P(3, 27, 8, 20)), F("ChessChairA", "chair_front", P(2, 27, 20, 0)),
        F("ChessChairB", "chair_back", P(3, 28, 8, 26)), F("Plant", "plant_stand", P(14, 30, 0, 20)),
        F("Basins", "basin_row", P(4, 34, 0, 12)), F("Tub", "bathtub", P(11, 36, 0, 24)),
        F("Heater", "water_heater", P(14, 34, 0, 14)), F("LaundryTub", "laundry_tub", P(3, 37, 0, 20)),
    ]
    decor = [("window", 3 * CELL, 1, 26), ("window", 13 * CELL, 1, 26), ("door", 7 * CELL, 1, ""),
             ("sconce", 6 * CELL + 8, 1), ("sconce", 10 * CELL - 8, 1), ("door", 7 * CELL, 32, ""),
             ("window", 12 * CELL, 32, 26), ("sconce", 4 * CELL, 32)]
    for y in (7, 15, 23):
        decor += [("side_door", 0, y, 2, "w"), ("side_door", 15, y, 2, "e")]
    floor_work = [("steps", CELL, 15 * CELL + 2, 14 * CELL, 3, True), ("rug", 2 * CELL, 5 * CELL + 16, 6 * CELL, 3 * CELL, "study"),
                  ("runner", 6 * CELL + 16, 17 * CELL, 4 * CELL - 32, 13 * CELL)]
    exits = [exit_("north_door", 7, 0, 9, 1, "campus", "living_north"),
             exit_("west_north", 0, 7, 1, 9, "zone_jia", "from_hall"), exit_("west_south", 0, 23, 1, 25, "zone_bing", "from_hall"),
             exit_("east_north", 15, 7, 16, 9, "zone_ding", "from_hall"), exit_("east_south", 15, 23, 16, 25, "zone_yi", "from_hall"),
             exit_("west_lane", 0, 15, 1, 17, "campus", "lane_west"), exit_("east_lane", 15, 15, 16, 17, "campus", "lane_east")]
    spawns = {"north_door": P(8, 3, 0, 8), "west_north": P(1, 8, 20, 16), "west_south": P(1, 24, 20, 16),
              "east_north": P(14, 8, 12, 16), "east_south": P(14, 24, 12, 16), "west_lane": P(1, 16, 20, 16),
              "east_lane": P(14, 16, 12, 16)}
    sites = [{"id": "watch_point", "point": P(8, 19, 0, 16), "label_key": "资源.场点.living_hall.watch_point.标签"}, {"id": "hall_notice", "point": P(11, 4, 0, 8), "label_key": "资源.场点.living_hall.hall_notice.标签"},
             {"id": "fireplace", "point": P(4, 4, 0, 8), "label_key": "资源.场点.living_hall.fireplace.标签"}]
    return dict(key="living_hall", title_key="资源.地图.living_hall.标题", rows=p.rows(), regions=regions,
                floor={"hall": "parquet", "bath": "whitetile"}, wall={"hall": ("plaster", "CREAM"), "bath": ("tile",)},
                decor=decor, floor_work=floor_work, furniture=furniture, exits=exits, spawns=spawns, sites=sites, geometry_sites=[{"id": "living_south_table", "point": [240, 572]}, {"id": "living_piano", "point": [160, 320]}])


# --------------------------------------------------------------- 中央厅 ----

def central_hall() -> dict:
    N, r, c = 26, 11.6, 13
    p = Plan(N, N)
    for y in range(N):
        for x in range(N):
            dx, dy = abs(x + 0.5 - c), abs(y + 0.5 - c)
            if dx <= 11 and dy <= 11 and dx + dy <= 15.5:      # 八角形：北面一段长直墙
                p.g[y][x] = "."
    tops = {x: min(y for y in range(N) if p.g[y][x] == ".") for x in range(N) if any(p.g[y][x] == "." for y in range(N))}
    north = min(tops.values())
    for x, t in tops.items():
        if t == north:
            p.g[t][x], p.g[t + 1][x] = "W", "w"      # 北侧平直一段画立面
        else:
            p.g[t][x] = "#"                           # 斜边只见墙顶
    for y in range(0, 4):                                  # 北门廊（往名录库）
        for x in (12, 13):
            p.g[y][x] = "D"
    for y in range(22, N):                                 # 南门廊（大道）
        for x in (12, 13):
            p.g[y][x] = "D"
    for x in range(0, 4):                                  # 西门廊（讲堂）
        for y in (12, 13):
            p.g[y][x] = "D"
    for x in range(22, N):                                 # 东门廊（白塔院）
        for y in (12, 13):
            p.g[y][x] = "D"
    furniture = [F("RelayColumn", "relay_column", P(13, 13, 0, 8)), F("Mouthpiece", "mouthpiece_stand", P(11, 14, 0, 20)),
                 F("NoticeW", "notice_board", P(6, 6, 0, 8)), F("NoticeE", "notice_board", P(20, 6, 0, 8)),
                 F("NoticeN", "timetable_board", P(9, 3, 8, 30)), F("PillarSW", "notice_pillar", P(7, 18, 0, 20)),
                 F("PillarSE", "notice_pillar", P(19, 18, 0, 20)), F("BenchW", "bench", P(5, 12, 0, 20)),
                 F("BenchE", "bench", P(21, 12, 0, 20)), F("BenchSW", "bench", P(8, 21, 0, 10)), F("BenchSE", "bench", P(18, 21, 0, 10))]
    return dict(key="central_hall", title_key="资源.地图.central_hall.标题", rows=p.rows(), regions=[],
                floor={"default": "flagstone"}, wall={"default": ("paper", "SAGE", "stars", "BRASS")},
                decor=[("sconce", 9 * CELL, 2), ("sconce", 17 * CELL, 2), ("painting", 10 * CELL + 16, 2, "lighthouse"), ("painting", 16 * CELL - 16, 2, "sea")],
                floor_work=[("mosaic", 13 * CELL, 13 * CELL + 8, 104)], furniture=furniture,
                exits=[exit_("north", 12, 0, 14, 1, "campus", "hall_north"), exit_("south", 12, 25, 14, 26, "campus", "hall_south"),
                       exit_("west", 0, 12, 1, 14, "lecture", "from_central"), exit_("east", 25, 12, 26, 14, "campus", "hall_east")],
                spawns={"north": P(13, 3, 0, 16), "south": P(13, 23, 0, 8), "west": P(2, 13, 16, 16), "east": P(24, 13, 0, 16)},
                sites=[{"id": "relay", "point": P(13, 14, 0, 8), "label_key": "资源.场点.central_hall.relay.标签"}, {"id": "mouthpiece", "point": P(11, 15, 0, 0), "label_key": "资源.场点.central_hall.mouthpiece.标签"},
                       {"id": "central_notice", "point": P(6, 7, 0, 0), "label_key": "资源.场点.central_hall.central_notice.标签"}, {"id": "watch_point", "point": P(13, 20, 0, 0), "label_key": "资源.场点.central_hall.watch_point.标签"}])


# ----------------------------------------------------------------- 讲堂 ----

def lecture() -> dict:
    p = Plan(22, 18)
    p.room(1, 1, 21, 17)
    p.door_v(21, 9)
    furniture = [F("Blackboard", "blackboard_large", P(10, 3, 16, 2)), F("Lectern", "lectern", P(10, 5, 16, 6)),
                 F("Timetable", "timetable_board", P(18, 3, 0, 2))]
    for i, row in enumerate((8, 10, 12, 14)):
        furniture.append(F(f"Desk{i}a", "lecture_desk_row", P(6, row, 0, 30)))
        furniture.append(F(f"Desk{i}b", "lecture_desk_row", P(15, row, 0, 30)))
    floor_work = [("stage", CELL, 3 * CELL, 20 * CELL, 2 * CELL + 8)]
    for row in (8, 10, 12, 14):
        floor_work.append(("steps", CELL, row * CELL + 34, 20 * CELL, 1, False))
    return dict(key="lecture", title_key="资源.地图.lecture.标题", rows=p.rows(), regions=[], floor={"default": "plank"},
                wall={"default": ("panel",)}, decor=[("sconce", 4 * CELL, 1), ("sconce", 17 * CELL, 1), ("side_door", 21, 9, 2, "e")],
                floor_work=floor_work, furniture=furniture,
                exits=[exit_("east", 21, 9, 22, 11, "central_hall", "west")],
                spawns={"from_central": P(20, 10, 0, 16)},
                sites=[{"id": "blackboard", "point": P(11, 5, 0, 8), "label_key": "资源.场点.lecture.blackboard.标签"}, {"id": "timetable", "point": P(18, 4, 0, 8), "label_key": "资源.场点.lecture.timetable.标签"}])


# ------------------------------------------------------------- 名录库 ----

def archive() -> dict:
    p = Plan(28, 19)
    p.room(1, 1, 15, 9)              # 内库
    p.room(1, 10, 15, 18)            # 前厅
    p.room(16, 10, 27, 18)           # 文献工房
    p.door_h(7, 9)                   # 库门
    p.door_v(15, 14)                 # 前厅 → 文献工房
    p.door_h(7, 18, depth=1)         # 南门
    p.fill(1, 14, 3, 18, "S")        # 下到配重室
    regions = [("vault", 1, 1, 15, 9), ("works", 16, 10, 27, 18), ("foyer", 0, 9, 16, 19)]
    furniture = [F("SealRackA", "seal_rack", P(3, 3, 0, 18)), F("SealRackB", "seal_rack", P(6, 3, 0, 18)),
                 F("SealRackC", "seal_rack", P(10, 3, 0, 18)), F("SealRackD", "seal_rack", P(13, 3, 0, 18)),
                 F("ShelfVW", "archive_shelf", P(2, 7, 0, 30)), F("ShelfVE", "archive_shelf", P(14, 7, 0, 30)),
                 F("Pedestal", "box_pedestal", P(8, 7, 0, 6)), F("VaultDoor", "vault_door", P(8, 10, 0, 62)),
                 F("Warning", "notice_board", P(5, 10, 0, 34)), F("FoyerBench", "bench", P(11, 15, 0, 20)),
                 F("FoyerLamp", "floor_lamp", P(13, 13, 0, 10)),
                 F("CopyDeskA", "copy_desk", P(19, 14, 0, 10)), F("CopyDeskB", "copy_desk", P(24, 14, 0, 10)),
                 F("ChairA", "chair_back", P(19, 14, 0, 26)), F("ChairB", "chair_back", P(24, 14, 0, 26)),
                 F("PlanCabinet", "plan_cabinet", P(18, 12, 0, 14)), F("WorksShelfA", "archive_shelf", P(22, 12, 0, 14)),
                 F("WorksShelfB", "archive_shelf", P(25, 12, 0, 14))]
    return dict(key="archive", title_key="资源.地图.archive.标题", rows=p.rows(), regions=regions,
                floor={"vault": "slate", "works": "honey", "foyer": "flagstone"},
                wall={"vault": ("panel",), "works": ("paper", "LINEN", "lattice", "CLARET"), "foyer": ("ashlar", "STONE")},
                decor=[("sconce", 4 * CELL, 10), ("sconce", 12 * CELL, 10), ("window", 21 * CELL, 10, 28), ("sconce", 25 * CELL, 10),
                       ("side_door", 15, 14, 2, "e")],
                floor_work=[("stairwell", CELL + 4, 14 * CELL + 4, 2 * CELL - 8, 4 * CELL - 8)], furniture=furniture,
                exits=[exit_("south", 7, 18, 9, 19, "campus", "archive_front"), exit_("down", 1, 16, 3, 18, "counterweight", "from_stairs")],
                spawns={"south": P(8, 17, 0, 16), "stairs": P(3, 15, 16, 16)},
                sites=[{"id": "lock_core", "point": P(8, 12, 0, 0), "label_key": "资源.场点.archive.lock_core.标签"}, {"id": "seal_boxes", "point": P(8, 5, 0, 0), "label_key": "资源.场点.archive.seal_boxes.标签"},
                       {"id": "name_warning", "point": P(5, 12, 0, 0), "label_key": "资源.场点.archive.name_warning.标签"}, {"id": "red_ink", "point": P(19, 15, 0, 0), "label_key": "资源.场点.archive.red_ink.标签"},
                       {"id": "watch_point", "point": P(10, 16, 0, 0), "label_key": "资源.场点.archive.watch_point.标签"}])


def counterweight() -> dict:
    p = Plan(14, 11)
    p.room(1, 1, 13, 10)
    p.fill(1, 7, 3, 10, "S")
    return dict(key="counterweight", title_key="资源.地图.counterweight.标题", rows=p.rows(), regions=[], floor={"default": "slate"},
                wall={"default": ("ashlar", "STONE")}, decor=[("sconce", 10 * CELL, 1)],
                floor_work=[("steps", CELL, 7 * CELL, 2 * CELL, 3, False)],
                furniture=[F("Engine", "counterweight_engine", P(7, 5, 0, 20))],
                exits=[exit_("up", 1, 7, 3, 8, "archive", "stairs")], spawns={"from_stairs": P(2, 8, 16, 16)},
                sites=[{"id": "counterweight", "point": P(7, 6, 0, 0), "label_key": "资源.场点.counterweight.counterweight.标签"}])


# --------------------------------------------------------------- 共鸣厅 ----

def resonance() -> dict:
    p = Plan(29, 16)
    p.room(1, 1, 21, 15)
    p.room(22, 1, 28, 8)
    p.door_v(21, 5)
    p.door_h(10, 15, depth=1)
    regions = [("silent", 22, 1, 28, 8), ("hall", 0, 0, 22, 16)]
    furniture = [F("FrameA", "tuning_frame", P(6, 4, 0, 8)), F("FrameB", "tuning_frame", P(15, 4, 0, 8)),
                 F("StandA", "music_stand", P(9, 6, 0, 0)), F("StandB", "music_stand", P(12, 6, 0, 0))]
    for row in (9, 11):
        for i, x in enumerate((4, 6, 8, 13, 15, 17)):
            furniture.append(F(f"Chair{row}{i}", "chair_front", P(x, row, 0, 20)))
    return dict(key="resonance", title_key="资源.地图.resonance.标题", rows=p.rows(), regions=regions,
                floor={"hall": "herringbone", "silent": "oak"}, wall={"hall": ("paper", "BOTTLE", "stars", "OCHRE"), "silent": ("plaster", "LINEN")},
                decor=[("window", 4 * CELL, 1, 30), ("window", 17 * CELL, 1, 30), ("sconce", 10 * CELL, 1), ("sconce", 11 * CELL, 1),
                       ("side_door", 21, 5, 2, "w")],
                floor_work=[("stage", CELL, 3 * CELL, 20 * CELL, 3 * CELL), ("chalk", 23 * CELL + 8, 3 * CELL + 8, 4 * CELL, 4 * CELL)],
                furniture=furniture, exits=[exit_("south", 10, 15, 12, 16, "campus", "resonance_front")],
                spawns={"south": P(11, 14, 0, 16)},
                sites=[{"id": "step_marks", "point": P(25, 5, 0, 0), "label_key": "资源.场点.resonance.step_marks.标签"}, {"id": "silent_door", "point": P(21, 6, 0, 0), "label_key": "资源.场点.resonance.silent_door.标签"}])


# ------------------------------------------------------- 诊疗所·安全端 ----

def clinic() -> dict:
    p = Plan(27, 11)
    p.room(1, 1, 19, 10)
    p.room(20, 1, 26, 10)
    p.door_v(19, 6)
    p.door_h(8, 10, depth=1)
    regions = [("safety", 20, 1, 26, 10), ("ward", 0, 0, 20, 11)]
    furniture = [F(f"Bed{i}", "ward_bed", P(2 + 3 * i, 3, 0, 62)) for i in range(5)] + [
        F("MedCabinet", "medicine_cabinet", P(17, 3, 0, 22)), F("DoctorDesk", "doctor_desk", P(15, 8, 0, 8)),
        F("Screen", "folding_screen", P(4, 8, 0, 12)), F("ReagentCabinet", "reagent_cabinet", P(22, 3, 16, 22)),
        F("Trough", "wash_trough", P(23, 7, 0, 10))]
    return dict(key="clinic", title_key="资源.地图.clinic.标题", rows=p.rows(), regions=regions,
                floor={"ward": "whitetile", "safety": "quarry"}, wall={"ward": ("tile",), "safety": ("tile",)},
                decor=[("window", 5 * CELL, 1, 28), ("window", 11 * CELL, 1, 28), ("sconce", 8 * CELL, 1), ("side_door", 19, 6, 2, "e")],
                floor_work=[], furniture=furniture, exits=[exit_("south", 8, 10, 10, 11, "campus", "clinic_front")],
                spawns={"south": P(9, 9, 0, 16)},
                sites=[{"id": "reagent", "point": P(22, 5, 16, 0), "label_key": "资源.场点.clinic.reagent.标签"}, {"id": "empty_bowl", "point": P(5, 6, 0, 0), "label_key": "资源.场点.clinic.empty_bowl.标签"}])


# --------------------------------------------------------------- 后勤院 ----

def logistics() -> dict:
    p = Plan(31, 14)
    p.room(1, 1, 13, 13)
    p.room(14, 1, 30, 13)
    p.fill(13, 7, 14, 10, "D")       # 配给口
    p.door_v(0, 9)
    p.door_v(30, 9)
    regions = [("kitchen", 0, 0, 13, 14), ("dining", 13, 0, 31, 14)]
    furniture = [F("Range", "kitchen_range", P(5, 3, 0, 20)), F("Hood", "hood_chimney", P(5, 1, 0, 60)),
                 F("DishRack", "dish_rack", P(10, 3, 0, 20)), F("Pump", "hand_pump", P(2, 6, 8, 0)),
                 F("Worktable", "worktable", P(6, 8, 0, 16)), F("BarrelA", "barrel", P(2, 11, 8, 20)),
                 F("Sacks", "sack", P(3, 11, 16, 20)), F("CrateK", "crate", P(11, 11, 0, 20)),
                 F("RationCounter", "ration_counter", P(14, 8, 26, 34)), F("Ledger", "ledger_stand", P(16, 7, 0, 6)),
                 F("RationNotice", "notice_board", P(16, 3, 0, 2)),
                 F("TableA", "long_table", P(22, 6, 0, 10)), F("BenchA1", "long_bench", P(22, 5, 0, 8)), F("BenchA2", "long_bench", P(22, 7, 0, 4)),
                 F("TableB", "long_table", P(22, 10, 0, 10)), F("BenchB1", "long_bench", P(22, 9, 0, 8)), F("BenchB2", "long_bench", P(22, 11, 0, 4))]
    return dict(key="logistics", title_key="资源.地图.logistics.标题", rows=p.rows(), regions=regions,
                floor={"kitchen": "quarry", "dining": "plank"}, wall={"kitchen": ("tile",), "dining": ("plaster", "CREAM")},
                decor=[("window", 20 * CELL, 1, 30), ("window", 26 * CELL, 1, 30), ("sconce", 23 * CELL, 1), ("pans", 7 * CELL, 54),
                       ("side_door", 0, 9, 2, "w"), ("side_door", 30, 9, 2, "e")],
                floor_work=[], furniture=furniture,
                exits=[exit_("west", 0, 9, 1, 11, "campus", "logistics_west"), exit_("east", 30, 9, 31, 11, "campus", "logistics_east")],
                spawns={"west": P(1, 10, 20, 16), "east": P(29, 10, 12, 16)},
                sites=[{"id": "ration_counter", "point": P(14, 9, 16, 0), "label_key": "资源.场点.logistics.ration_counter.标签"}, {"id": "ledger", "point": P(16, 8, 0, 0), "label_key": "资源.场点.logistics.ledger.标签"},
                       {"id": "ration_notice", "point": P(16, 4, 0, 0), "label_key": "资源.场点.logistics.ration_notice.标签"}, {"id": "watch_point", "point": P(18, 9, 0, 0), "label_key": "资源.场点.logistics.watch_point.标签"}])


def granary() -> dict:
    p = Plan(10, 9)
    p.room(1, 1, 9, 8)
    p.door_h(4, 8, depth=1)
    return dict(key="granary", title_key="资源.地图.granary.标题", rows=p.rows(), regions=[], floor={"default": "plank"}, wall={"default": ("ashlar", "STONE")},
                decor=[("sconce", 2 * CELL, 1)], floor_work=[],
                furniture=[F("BinA", "granary_bin", P(3, 3, 0, 22)), F("BinB", "granary_bin", P(7, 3, 0, 22)),
                           F("SackA", "sack", P(2, 6, 8, 10)), F("SackB", "sack", P(3, 6, 0, 16)), F("Barrel", "barrel", P(7, 6, 8, 10))],
                exits=[exit_("south", 4, 8, 6, 9, "campus", "granary_front")], spawns={"south": P(5, 7, 0, 16)},
                sites=[{"id": "latch", "point": P(5, 7, 0, 8), "label_key": "资源.场点.granary.latch.标签"}, {"id": "stock", "point": P(5, 4, 0, 0), "label_key": "资源.场点.granary.stock.标签"}])


def greenhouse() -> dict:
    p = Plan(12, 31)
    p.room(1, 1, 11, 30)
    p.fill(1, 11, 11, 12, "S")
    p.fill(1, 20, 11, 21, "S")
    p.door_h(5, 30, depth=1)
    furniture = [F("Valve", "valve_station", P(6, 3, 0, 22))]
    for i, row in enumerate((6, 9, 15, 18, 24, 27)):
        furniture.append(F(f"Bed{i}", "planter_bed", P(5, row, 0, 20)))
    furniture += [F("Pots", "seedling_pots", P(9, 13, 0, 20)), F("Tools", "garden_tools", P(9, 22, 8, 20))]
    return dict(key="greenhouse", title_key="资源.地图.greenhouse.标题", rows=p.rows(), regions=[], floor={"default": "gravel"},
                wall={"default": ("glass",)}, decor=[], furniture=furniture,
                floor_work=[("steps", CELL, 11 * CELL, 10 * CELL, 3, True), ("steps", CELL, 20 * CELL, 10 * CELL, 3, True)],
                exits=[exit_("south", 5, 30, 7, 31, "campus", "greenhouse_front")], spawns={"south": P(6, 29, 0, 16)},
                sites=[{"id": "valve", "point": P(6, 4, 0, 0), "label_key": "资源.场点.greenhouse.valve.标签"}, {"id": "gauge", "point": P(7, 4, 0, 0), "label_key": "资源.场点.greenhouse.gauge.标签"}])


def all_rooms() -> list[dict]:
    return [living_hall()] + [zone(z) for z in ("甲", "乙", "丙", "丁")] + [
        central_hall(), lecture(), archive(), counterweight(), resonance(), clinic(), logistics(), granary(), greenhouse()]
