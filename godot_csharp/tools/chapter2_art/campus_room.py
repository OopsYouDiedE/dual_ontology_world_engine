"""Assemble the campus exterior into the same room format as the interiors.

Roofs become y-sorted sprites over the painted ground; doors get stone steps,
an exit trigger and a spawn point; the sea is unwalkable; the collapsed stretch
of the harbour road is a solid barrier.
"""
from __future__ import annotations

from first_awakening_art.palette import STONE
from .exterior import (CLOSED_DOORS, K, ROOF, W, H, _collapse_point, _inverse, paint, porch, px, roof_sprite)

DOORS = [  # (名称, u, v, 朝外方向, 目标房间, 目标出生点, 外景出生点名)
    ("生活翼北门", -47.8, -42.0, (0, 1), "living_hall", "north_door", "living_north"),
    ("夹道西", -52.8, -56.7, (-1, 0), "living_hall", "west_lane", "lane_west"),
    ("夹道东", -42.8, -56.7, (1, 0), "living_hall", "east_lane", "lane_east"),
    ("中央厅北", 0.0, 17.0, (0, 1), "central_hall", "north", "hall_north"),
    ("中央厅南", 0.0, -17.0, (0, -1), "central_hall", "south", "hall_south"),
    ("中央厅东", 17.0, 0.0, (1, 0), "central_hall", "east", "hall_east"),
    ("名录库", 0.0, 36.0, (0, -1), "archive", "south", "archive_front"),
    ("共鸣厅", -72.0, 6.0, (0, -1), "resonance", "south", "resonance_front"),
    ("诊疗所", 56.0, -39.0, (0, -1), "clinic", "south", "clinic_front"),
    ("后勤院西", 20.0, -67.0, (-1, 0), "logistics", "west", "logistics_west"),
    ("后勤院东", 48.0, -67.0, (1, 0), "logistics", "east", "logistics_east"),
    ("粮仓", 56.0, -64.0, (0, -1), "granary", "south", "granary_front"),
    ("温室", 100.0, -10.0, (0, -1), "greenhouse", "south", "greenhouse_front"),
]


def build(prop_footprints: dict) -> dict:
    c, (S, site, O, BR, land) = paint()
    furniture, sprites, solids = [], {}, []
    LW = S.LW

    def rect_px(cu, cv, su, sv):
        x0, y0 = px(cu - su / 2, cv + sv / 2)
        return int(round(x0)), int(round(y0)), int(round(su * K)), int(round(sv * K))

    def add_roof(name, kind, form, cu, cv, su, sv, ridge, solid=True, clerestory=False):
        x0, y0, w, h = rect_px(cu, cv, su, sv)
        can = roof_sprite(kind, form, w, h, ridge, seed=len(sprites) + 3, clerestory=clerestory)
        key = f"campus_roof_{len(sprites):02d}"
        sprites[key] = can
        # 屋顶画布左上角即外廓左上角；锚点取画布底边中点
        furniture.append({"name": name, "sprite": key, "position": [x0 + can.w // 2, y0 + can.h],
                          "footprint": [x0, y0, w, h] if solid else None})

    skip = {"誓约庭", "晾晒场", "汲水泉", "门楼"}
    for b in S.B:
        nm = b["name"]
        if nm in skip or nm.startswith("生活翼"):
            continue
        kind, form = ROOF.get(nm, ("slate" if b["cat"] in ("core", "academy") else "tile", "gable"))
        if b["shape"] == "circ":
            add_roof(nm, kind, form, b["c"][0], b["c"][1], 2 * b["r"], 2 * b["r"], "u")
        else:
            su, sv = b["s"]
            add_roof(nm, kind, form, b["c"][0], b["c"][1], su, sv, "u" if su >= sv else "v")
    for nm, (cu, cv), (su, sv), grp in S.lw_blocks():
        if nm.startswith("起居厅"):
            continue
        add_roof("生活翼·" + nm, "tile", "hip" if nm == "浴室盥洗" else "gable", cu, cv, su, sv, "u")
    hall_u = LW["u0"] + LW["ZL"] + LW["HALL"] / 2
    hall_v = LW["v0"] - (2 * LW["ZD"] + LW["LANE"]) / 2
    add_roof("生活翼·起居厅", "slate", "gable", hall_u, hall_v, LW["HALL"], 2 * LW["ZD"] + LW["LANE"], "v", clerestory=True)
    gate = next(b for b in S.B if b["name"] == "门楼")
    for side in (-1, 1):
        add_roof("门楼塔", "slate", "hip", gate["c"][0] + side * 5, gate["c"][1], 4, 7, "v")
    add_roof("门楼门楣", "slate", "flat", gate["c"][0], gate["c"][1], 6, 5, "u", solid=False)

    exits, spawns = [], {}
    for name, u, v, side, target, spawn, own in DOORS:
        x, y = px(u, v)
        du, dv = side
        porch(c, x, y, (du, dv), False)
        tx, ty = px(u + du * 0.4, v + dv * 0.4)
        exits.append({"name": name, "rect_px": [int(tx - 18), int(ty - 18), 36, 36], "target": target, "spawn": spawn})
        sx, sy = px(u + du * 3.4, v + dv * 3.4)
        spawns[own] = [int(sx), int(sy + 10)]
    for nm, sides in CLOSED_DOORS.items():
        b = next(x for x in S.B if x["name"] == nm)
        for du, dv in sides:
            if b["shape"] == "circ":
                u, v = b["c"][0] + du * b["r"], b["c"][1] + dv * b["r"]
            else:
                u, v = b["c"][0] + du * b["s"][0] / 2, b["c"][1] + dv * b["s"][1] / 2
            porch(c, *px(u, v), (du, dv), True)

    def put(name, sprite, u, v):
        x, y = px(u, v)
        fp = prop_footprints.get(sprite)
        furniture.append({"name": name, "sprite": sprite, "position": [int(x), int(y)],
                          "footprint": None if fp is None else [int(x - fp[0] / 2), int(y - fp[1]), fp[0], fp[1]]})

    for i, v in enumerate(range(-80, -20, 14)):
        put(f"LampW{i}", "lamp_post", -5.5, v)
        put(f"LampE{i}", "lamp_post", 5.5, v)
    for i, (u, v) in enumerate(((-8, -94), (8, -94), (-8, -106), (8, -106))):
        put(f"OathBench{i}", "stone_bench", u, v)
    put("DryingA", "drying_lines", 72, -62)
    put("DryingB", "drying_lines", 72, -68)
    put("Spring", "spring_trough", 96, -29)
    put("Buckets", "bucket_pair", 99.5, -31)
    for tag, u in (("W", LW["u0"] - 0.6), ("E", LW["u0"] + 2 * LW["ZL"] + LW["HALL"] + 0.6)):
        put(f"LaneGate{tag}", "gate_leaf", u, LW["v0"] - LW["ZD"] - 0.3)

    cu, cv = _inverse(O, BR)(*_collapse_point())
    x, y = px(cu, cv)
    solids.append([int(x - 40), int(y - 30), 80, 60])

    oath = next(b for b in S.B if b["name"] == "誓约庭")
    x0, y0, w, h = rect_px(oath["c"][0], oath["c"][1], oath["s"][0], oath["s"][1])
    gap = 40
    for seg in ([x0, y0, 6, h], [x0 + w - 6, y0, 6, h], [x0, y0, w // 2 - gap, 6], [x0 + w // 2 + gap, y0, w // 2 - gap, 6],
                [x0, y0 + h - 6, w // 2 - gap, 6], [x0 + w // 2 + gap, y0 + h - 6, w // 2 - gap, 6]):
        solids.append(seg)
        c.rect(seg[0], seg[1], seg[2], seg[3], STONE[2])
        c.hline(seg[0], seg[1], seg[2], STONE[5])

    rows = []
    for gy in range(H // 32 + 1):
        row = ""
        for gx in range(W // 32 + 1):
            cell = land[gy * 32:gy * 32 + 32, gx * 32:gx * 32 + 32]
            row += "." if cell.size and cell.mean() > 0.5 else " "
        rows.append(row)
    sites = [{"id": "lane", "point": list(map(int, px(-47.8, -56.7))), "label": "夹道"},
             {"id": "collapse", "point": [int(x), int(y)], "label": "港路塌方处"},
             {"id": "oath_court", "point": list(map(int, px(0, -100))), "label": "誓约庭"},
             {"id": "spring", "point": list(map(int, px(96, -32))), "label": "汲水泉"}]
    return dict(key="campus", title="学园", rows=rows, size_px=[W, H], architecture=c, furniture=furniture,
                extra_sprites=sprites, solids_px=solids, exits=exits, spawns=spawns, sites=sites)
