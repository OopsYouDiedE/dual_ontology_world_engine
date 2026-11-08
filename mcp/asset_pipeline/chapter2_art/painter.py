"""Generic 1x painter for chapter-2 rooms, built on the chapter-1 life-wing recipes.

A room is plain data (see rooms.py): a character grid in 32px cells, named
regions with floor materials and wall styles, wall-mounted decor and floor work.
Grid marks: '#' wall cap, 'W'/'w' upper/lower wall face (always a pair), '.'
floor, 'D' doorway, 'S' stair floor, 'G'/'g' front-wall elevation, ' ' outside.
The painter only reads the data; it never invents layout.
"""
from __future__ import annotations

import numpy as np

from first_awakening_art import architecture as A
from first_awakening_art.palette import (BOTTLE, BRASS, CAP, CHALK, CLARET, CREAM, FOG, GRASS, HONEY, IRON, LEAF,
                                         LINEN, OAK, OCHRE, PARQUET, PLANK, PLUM, QUARRY, ROSE, SAGE, SLATE, STONE,
                                         TEAL, TILE, TILE_GREEN, VOID, WALNUT)
from first_awakening_art.pixel import Canvas, mix, rng, shift

CELL = 32
RAMPS = dict(BOTTLE=BOTTLE, BRASS=BRASS, CLARET=CLARET, CREAM=CREAM, HONEY=HONEY, LINEN=LINEN, OAK=OAK, OCHRE=OCHRE,
             PLUM=PLUM, ROSE=ROSE, SAGE=SAGE, SLATE=SLATE, STONE=STONE, TEAL=TEAL, TILE=TILE, WALNUT=WALNUT, IRON=IRON,
             CHALK=CHALK, QUARRY=QUARRY)
WALKABLE = ".DS"


class Grid:
    def __init__(self, room: dict):
        self.rows = room["rows"]
        self.W, self.H = len(self.rows[0]), len(self.rows)
        assert all(len(r) == self.W for r in self.rows), room["key"]
        self.regions = room.get("regions", [])

    def mark(self, x: int, y: int) -> str:
        return self.rows[y][x] if 0 <= x < self.W and 0 <= y < self.H else " "

    def walkable(self, x: int, y: int) -> bool:
        return self.mark(x, y) in WALKABLE

    def region(self, x: int, y: int) -> str:
        for name, x0, y0, x1, y1 in self.regions:
            if x0 <= x < x1 and y0 <= y < y1:
                return name
        return "default"

    def cells(self, test):
        for y in range(self.H):
            for x in range(self.W):
                if test(x, y):
                    yield x, y


# ------------------------------------------------------------- materials ----

def _flag(ramp, seed: int) -> Canvas:
    """Flagstones like the chapter-1 hall, in any stone ramp."""
    g = rng(seed)
    t = Canvas(A.MAP_W, A.MAP_H)
    y = 0
    while y < A.MAP_H:
        h = int(g.choice([16, 24, 24, 32]))
        x = -int(g.integers(0, 24))
        while x < A.MAP_W:
            w = int(g.choice([24, 32, 32, 40, 48]))
            n = len(ramp)
            tone = int(g.choice([3, 4, 4, 5])) if n > 6 else int(g.choice([1, 2, 2, 3]))
            base = ramp[tone]
            t.rect(x, y, w, h, base)
            t.hline(x + 1, y + 1, w - 2, ramp[min(tone + 1, len(ramp) - 1)])
            t.hline(x + 1, y + h - 2, w - 2, mix(base, ramp[tone - 1], 0.6))
            for _ in range(w * h // 40):
                t.px(x + int(g.integers(2, w - 2)), y + int(g.integers(2, h - 2)), ramp[max(0, min(n - 1, tone + int(g.choice([-1, 1]))))])
            t.hline(x, y + h - 1, w, ramp[1])
            t.vline(x + w - 1, y, h, ramp[1])
            x += w
        y += h
    return t


def _squares(ramp, seed: int, size: int = 16) -> Canvas:
    """Quiet square tiles (quarry or glazed white), soft grout."""
    g = rng(seed)
    t = Canvas(A.MAP_W, A.MAP_H)
    hi = len(ramp) - 2
    for ty in range(0, A.MAP_H, size):
        for tx in range(0, A.MAP_W, size):
            base = mix(ramp[hi - 1], ramp[int(g.choice([hi - 2, hi]))], float(g.uniform(0.0, 0.3)))
            t.rect(tx, ty, size, size, base)
            t.dither(tx + 1, ty + 1, size - 2, size - 2, mix(base, ramp[hi - 2], 0.5), 0.1, int(g.integers(0, 4)))
            t.hline(tx + 1, ty + 1, size - 3, mix(base, ramp[hi], 0.5))
            grout = mix(ramp[1], base, 0.45)
            t.hline(tx, ty + size - 1, size, grout)
            t.vline(tx + size - 1, ty, size, grout)
    return t


def _ground(kind: str, seed: int) -> Canvas:
    """Outdoor ground at 1x: grass, gravel or paving."""
    g = rng(seed)
    t = Canvas(A.MAP_W, A.MAP_H)
    if kind == "grass":
        a = np.array([GRASS[int(v)] for v in g.choice([1, 2, 2, 3], size=A.MAP_W * A.MAP_H)], dtype=np.uint8)
        t.a[:, :] = a.reshape(A.MAP_H, A.MAP_W, 4)
        for _ in range(A.MAP_W * A.MAP_H // 90):
            x, y = int(g.integers(0, A.MAP_W)), int(g.integers(2, A.MAP_H))
            t.vline(x, y - 2, 3, GRASS[4])
            t.px(x + 1, y - 1, GRASS[3])
        for _ in range(A.MAP_W * A.MAP_H // 2400):
            x, y = int(g.integers(0, A.MAP_W)), int(g.integers(0, A.MAP_H))
            t.ellipse(x, y + 1, 4, 2, STONE[1])
            t.ellipse(x, y, 3, 2, STONE[2])
    elif kind == "gravel":
        a = np.array([STONE[int(v)] for v in g.choice([2, 3, 3, 4], size=A.MAP_W * A.MAP_H)], dtype=np.uint8)
        t.a[:, :] = a.reshape(A.MAP_H, A.MAP_W, 4)
        t.dither(0, 0, A.MAP_W, A.MAP_H, mix(STONE[3], OCHRE[2], 0.25), 0.18)
    elif kind == "paving":
        return _flag(STONE, seed)
    elif kind == "basalt":
        return _flag([shift(c, -0.18) for c in STONE], seed)
    elif kind == "soil":
        a = np.array([mix(WALNUT[int(v)], STONE[2], 0.25) for v in g.choice([1, 2, 2, 3], size=A.MAP_W * A.MAP_H)], dtype=np.uint8)
        t.a[:, :] = a.reshape(A.MAP_H, A.MAP_W, 4)
    return t


_MATERIAL_CACHE: dict = {}


def material(key: str) -> Canvas:
    if key in _MATERIAL_CACHE:
        return _MATERIAL_CACHE[key]
    base = A.floor_materials()
    table = {
        "oak": lambda: base["oak"], "plank": lambda: base["corridor"], "herringbone": lambda: base["study"],
        "quarry": lambda: base["prep"], "flagstone": lambda: base["hall"],
        "parquet": lambda: A._herringbone(PARQUET, 41),
        "honey": lambda: A._boards(HONEY, 7, (40, 112), 42, 0.25),
        "slate": lambda: _flag(SLATE, 43), "basalt": lambda: _ground("basalt", 44),
        "whitetile": lambda: _squares(TILE, 45), "quarry_dark": lambda: _squares(QUARRY, 46, 24),
        "grass": lambda: _ground("grass", 47), "gravel": lambda: _ground("gravel", 48),
        "paving": lambda: _ground("paving", 49), "soil": lambda: _ground("soil", 50),
    }
    _MATERIAL_CACHE[key] = table[key]()
    return _MATERIAL_CACHE[key]


def _tile_to(src: Canvas, w: int, h: int) -> np.ndarray:
    reps = (h // src.a.shape[0] + 1, w // src.a.shape[1] + 1, 1)
    return np.tile(src.a, reps)[:h, :w]


# ---------------------------------------------------------------- painting ----

def paint(room: dict) -> tuple[Canvas, dict]:
    G = Grid(room)
    MW, MH = G.W * CELL, G.H * CELL
    outside = room.get("outside", "void")
    c = Canvas(MW, MH, VOID)
    if outside != "void":
        c.a[:, :] = _tile_to(material(outside), MW, MH)
    # floors
    tiled: dict = {}
    for x, y in G.cells(lambda x, y: G.walkable(x, y)):
        key = room["floor"].get(G.region(x, y), room["floor"].get("default", "plank"))
        if key not in tiled:
            tiled[key] = _tile_to(material(key), MW, MH)
        c.a[y * CELL:(y + 1) * CELL, x * CELL:(x + 1) * CELL] = tiled[key][y * CELL:(y + 1) * CELL, x * CELL:(x + 1) * CELL]
    for item in room.get("floor_work", []):
        _floor_work(c, item)
    _faces(c, G, room)
    _caps(c, G)
    _occlusion(c, G)
    for item in room.get("decor", []):
        _decor(c, item)
    furniture = room.get("furniture", [])
    A.bake_floor_work(c, [f for f in furniture if f.get("footprint")], [], [])
    anchors = {"sconces": [{"x": d[1], "y": d[2] * CELL + 36} for d in room.get("decor", []) if d[0] == "sconce"]}
    return c, anchors


def _face_runs(G: Grid):
    for y in range(G.H):
        x = 0
        while x < G.W:
            m = G.mark(x, y)
            if m in "WG":
                start = x
                while x < G.W and G.mark(x, y) == m and G.region(x, y) == G.region(start, y):
                    x += 1
                yield start, x, y, G.region(start, y), m
            else:
                x += 1


def _plaster(c: Canvas, x0: int, x1: int, top: int, ramp) -> None:
    w = x1 - x0
    c.rect(x0, top + 4, w, 34, ramp[4])
    c.dither(x0, top + 4, w, 34, ramp[3], 0.16, x0 % 4)
    c.dither(x0, top + 4, w, 6, ramp[2], 0.5)
    c.hline(x0, top + 9, w, ramp[3])
    A._crown(c, x0, x1, top, CREAM)
    A._painted_wainscot(c, x0, x1, top + 38)


def _panelled(c: Canvas, x0: int, x1: int, top: int) -> None:
    """Dark oak boarding to the ceiling: archives and the lecture hall."""
    w = x1 - x0
    c.rect(x0, top + 4, w, 56, WALNUT[3])
    for x in range(x0, x1, 16):
        c.vline(x, top + 4, 56, WALNUT[1])
        c.vline(x + 1, top + 5, 54, WALNUT[4])
    c.hline(x0, top + 30, w, WALNUT[5])
    c.hline(x0, top + 31, w, WALNUT[1])
    c.dither(x0, top + 4, w, 6, WALNUT[1], 0.5)
    A._crown(c, x0, x1, top, OAK)
    c.rect(x0, top + 58, w, 2, WALNUT[2])
    c.hline(x0, top + 60, w, WALNUT[0])


def _glass(c: Canvas, x0: int, x1: int, top: int) -> None:
    """Greenhouse glazing: pale panes in iron glazing bars over a low stone sill."""
    w = x1 - x0
    for y in range(top, top + 46):
        t = (y - top) / 46
        c.hline(x0, y, w, mix(FOG[4], FOG[2], t))
    c.dither(x0, top + 4, w, 40, FOG[5], 0.12)
    for x in range(x0, x1, 16):
        c.vline(x, top, 46, IRON[2])
        c.vline(x + 1, top, 46, IRON[4])
    for y in (top + 14, top + 30):
        c.hline(x0, y, w, IRON[2])
    c.hline(x0, top, w, IRON[1])
    A._ashlar(c, x0, x1, top + 46, 18, STONE, 9, (18, 30))


def _faces(c: Canvas, G: Grid, room: dict) -> None:
    styles = room.get("wall", {})
    for x0, x1, y, reg, m in _face_runs(G):
        px0, px1, top = x0 * CELL, x1 * CELL, y * CELL
        style = styles.get(reg, styles.get("default", ("plaster", "CREAM")))
        if m == "G":
            A._ashlar(c, px0, px1, top, 64, STONE, 16, (28, 44))
            c.rect(px0, top + 58, px1 - px0, 6, STONE[1])
            c.hline(px0, top + 63, px1 - px0, CAP[0])
            continue
        kind = style[0]
        if kind == "paper":
            A._papered(c, px0, px1, top, RAMPS[style[1]], style[2], RAMPS[style[3]])
        elif kind == "tile":
            A._tiled(c, px0, px1, top)
        elif kind == "ashlar":
            A._ashlar(c, px0, px1, top, 64, RAMPS[style[1]], 16, (24, 40))
        elif kind == "panel":
            _panelled(c, px0, px1, top)
        elif kind == "exterior":
            A._exterior_wall(c, px0, px1, top)
        elif kind == "glass":
            _glass(c, px0, px1, top)
        else:
            _plaster(c, px0, px1, top, RAMPS[style[1]])


def _caps(c: Canvas, G: Grid) -> None:
    g = rng(31)
    for x, y in G.cells(lambda x, y: G.mark(x, y) == "#"):
        x0, y0 = x * CELL, y * CELL
        c.rect(x0, y0, CELL, CELL, CAP[2])
        c.dither(x0, y0, CELL, CELL, CAP[1], 0.35, (x + y) % 4)
        for _ in range(6):
            c.px(x0 + int(g.integers(0, 32)), y0 + int(g.integers(0, 32)), CAP[3])
    m = G.mark
    for x, y in G.cells(lambda x, y: m(x, y) == "#"):
        x0, y0 = x * CELL, y * CELL
        open_n, open_s, open_w, open_e = m(x, y - 1) != "#", m(x, y + 1) != "#", m(x - 1, y) != "#", m(x + 1, y) != "#"
        if open_n:
            c.hline(x0, y0, CELL, CAP[0]); c.hline(x0, y0 + 1, CELL, CAP[4]); c.hline(x0, y0 + 2, CELL, CAP[3])
        if open_s:
            c.hline(x0, y0 + CELL - 3, CELL, CAP[3]); c.hline(x0, y0 + CELL - 2, CELL, CAP[4]); c.hline(x0, y0 + CELL - 1, CELL, CAP[0])
        if open_w:
            c.vline(x0, y0, CELL, CAP[0]); c.vline(x0 + 1, y0, CELL, CAP[4]); c.vline(x0 + 2, y0, CELL, CAP[3])
        if open_e:
            c.vline(x0 + CELL - 3, y0, CELL, CAP[3]); c.vline(x0 + CELL - 2, y0, CELL, CAP[4]); c.vline(x0 + CELL - 1, y0, CELL, CAP[0])
        for dx, dy, cx, cy in ((-1, -1, 0, 0), (1, -1, CELL - 3, 0), (-1, 1, 0, CELL - 3), (1, 1, CELL - 3, CELL - 3)):
            if m(x + dx, y) == "#" and m(x, y + dy) == "#" and m(x + dx, y + dy) != "#":
                c.rect(x0 + cx, y0 + cy, 3, 3, CAP[3])
                c.px(x0 + (0 if dx < 0 else CELL - 1), y0 + (0 if dy < 0 else CELL - 1), CAP[0])


def _occlusion(c: Canvas, G: Grid) -> None:
    m = G.mark
    for x, y in G.cells(lambda x, y: G.walkable(x, y)):
        x0, y0 = x * CELL, y * CELL
        above = m(x, y - 1)
        if above == "w" or (above == "#" and m(x, y - 2) != "#"):
            depth = 7 if above == "w" else 4
            for i in range(depth):
                c.rect(x0, y0 + i, CELL, 1, (26, 18, 32, int(120 * (1 - i / depth) ** 1.6)))
        if m(x - 1, y) == "#":
            for i in range(4):
                c.rect(x0 + i, y0, 1, CELL, (26, 18, 32, int(95 * (1 - i / 4))))
        if m(x + 1, y) == "#":
            for i in range(4):
                c.rect(x0 + CELL - 1 - i, y0, 1, CELL, (26, 18, 32, int(95 * (1 - i / 4))))
        if m(x, y + 1) == "#":
            c.rect(x0, y0 + CELL - 2, CELL, 2, (26, 18, 32, 70))
        if m(x, y + 1) == "G":
            for i in range(3):
                c.rect(x0, y0 + CELL - 1 - i, CELL, 1, (26, 16, 30, 90 - i * 28))


# ------------------------------------------------------------------ decor ----

DIGITS = {
    "0": ["111", "101", "101", "101", "111"], "1": ["010", "110", "010", "010", "111"],
    "2": ["111", "001", "111", "100", "111"], "3": ["111", "001", "111", "001", "111"],
    "4": ["101", "101", "111", "001", "001"], "5": ["111", "100", "111", "001", "111"],
    "6": ["111", "100", "111", "101", "111"], "7": ["111", "001", "010", "010", "010"],
    "8": ["111", "101", "111", "101", "111"], "9": ["111", "101", "111", "001", "111"],
    "R": ["110", "101", "110", "101", "101"], "-": ["000", "000", "111", "000", "000"],
}


def digits(c: Canvas, x: int, y: int, text: str, color) -> None:
    for ch in text:
        for gy, row in enumerate(DIGITS.get(ch, DIGITS["-"])):
            for gx, bit in enumerate(row):
                if bit == "1":
                    c.px(x + gx, y + gy, color)
        x += 4


def _door_frame(c: Canvas, x: int, top: int, label: str) -> None:
    for y in range(top + 8, top + 64):
        t = (y - top - 8) / 56
        c.hline(x, y, 64, mix(shift(OAK[2], -0.45), OAK[2], t))
    for side in (x - 6, x + 64):
        c.rect(side, top + 4, 6, 60, CREAM[3])
        c.vline(side, top + 4, 60, CREAM[5] if side < x else CREAM[1])
        c.vline(side + 5, top + 4, 60, CREAM[1] if side < x else CREAM[5])
    c.rect(x - 8, top, 80, 8, CREAM[3])
    c.hline(x - 8, top, 80, CREAM[5])
    c.hline(x - 8, top + 7, 80, CREAM[1])
    if label:
        w = 4 * len(label) + 4
        px = x + 32 - w // 2
        c.rect(px, top, w, 9, BRASS[1])
        c.rect(px + 1, top + 1, w - 2, 7, BRASS[3])
        digits(c, px + 3, top + 2, label, WALNUT[1])


def _side_door(c: Canvas, x: int, y: int, h: int, side: str) -> None:
    """A heavy door leaf seen edge-on in a side-wall doorway, standing open against the jamb."""
    lx = x + (2 if side == "w" else CELL - 8)
    c.rect(lx, y + 2, 6, h - 4, WALNUT[2])
    c.vline(lx, y + 2, h - 4, WALNUT[4])
    c.vline(lx + 5, y + 2, h - 4, WALNUT[0])
    for by in (y + 8, y + h - 12):
        c.hline(lx, by, 6, IRON[3])
    c.rect(lx + 2, y + h // 2 - 2, 2, 4, BRASS[4])


def _steps(c: Canvas, x: int, y: int, w: int, n: int, down: bool = True) -> None:
    for i in range(n):
        shade = (i if down else n - 1 - i) * 0.07
        sy = y + i * 10
        c.rect(x, sy, w, 7, shift(STONE[5], -shade))
        c.hline(x, sy, w, shift(STONE[6], -shade))
        c.dither(x, sy + 1, w, 5, shift(STONE[4], -shade), 0.15, i)
        c.rect(x, sy + 7, w, 3, shift(STONE[3], -shade))
        c.hline(x, sy + 7, w, shift(STONE[1], -shade))


def _stairwell(c: Canvas, x: int, y: int, w: int, h: int) -> None:
    """A flight going down into the floor: treads darken into the basement."""
    n = h // 8
    for i in range(n):
        t = i / max(1, n - 1)
        c.rect(x, y + i * 8, w, 6, mix(STONE[4], VOID, t * 0.85))
        c.rect(x, y + i * 8 + 6, w, 2, mix(STONE[2], VOID, t * 0.9))
    c.frame(x - 2, y - 2, w + 4, h + 4, IRON[2])
    c.hline(x - 2, y - 2, w + 4, IRON[4])


def _stage(c: Canvas, x: int, y: int, w: int, h: int) -> None:
    c.rect(x, y, w, h, OAK[3])
    for by in range(y, y + h, 6):
        c.hline(x, by, w, OAK[2])
    c.hline(x, y, w, OAK[5])
    c.rect(x, y + h, w, 6, OAK[1])
    c.hline(x, y + h + 6, w, (26, 16, 30, 120))


def _mosaic(c: Canvas, cx: int, cy: int, r: int) -> None:
    for ring, ramp in ((r, BRASS), (r - 6, STONE), (r - 10, BRASS), (r - 18, SLATE)):
        c.ellipse(cx, cy, ring, ring * 0.72, ramp[2])
        c.ellipse(cx, cy, ring - 2, ring * 0.72 - 2, ramp[3] if ramp is not SLATE else ramp[2])
    for k in range(16):
        a = k * np.pi / 8
        c.line(int(cx + (r - 16) * np.cos(a)), int(cy + (r - 16) * 0.72 * np.sin(a)),
               int(cx + (r - 6) * np.cos(a)), int(cy + (r - 6) * 0.72 * np.sin(a)), BRASS[4])


def _chalk_marks(c: Canvas, x: int, y: int, w: int, h: int) -> None:
    """Ritual step marks inside the silent room: complete, unbroken."""
    chalk = (214, 210, 194, 255)
    c.frame(x, y, w, h, chalk)
    for i, (sx, sy) in enumerate(((x + w // 4, y + h // 3), (x + w // 2, y + h // 2), (x + 3 * w // 4, y + h // 3), (x + w // 2, y + 4 * h // 5))):
        c.rect(sx - 3, sy - 4, 3, 7, chalk)
        c.rect(sx + 1, sy - 3, 3, 7, chalk)
    c.line(x + w // 4, y + h // 3, x + 3 * w // 4, y + h // 3, (190, 186, 170, 255))


def _floor_work(c: Canvas, item) -> None:
    kind = item[0]
    if kind == "rug":
        A._rug(c, *item[1:])
    elif kind == "runner":
        A._runner(c, *item[1:])
    elif kind == "steps":
        _steps(c, *item[1:])
    elif kind == "stairwell":
        _stairwell(c, *item[1:])
    elif kind == "stage":
        _stage(c, *item[1:])
    elif kind == "mosaic":
        _mosaic(c, *item[1:])
    elif kind == "chalk":
        _chalk_marks(c, *item[1:])
    elif kind == "dew":
        x, y, w, h = item[1:]
        c.dither(x, y, w, h, (214, 226, 232, 60), 0.18)
    elif kind == "path":
        x, y, w, h, key = item[1:]
        c.a[y:y + h, x:x + w] = _tile_to(material(key), x + w, y + h)[y:y + h, x:x + w]


def _decor(c: Canvas, item) -> None:
    kind = item[0]
    if kind == "window":
        A._window(c, item[1], item[2] * CELL, item[3])
    elif kind == "sconce":
        A._sconce(c, item[1], item[2] * CELL)
    elif kind == "painting":
        A._painting(c, item[1], item[2] * CELL + 10, item[3])
    elif kind == "blackboard":
        A._blackboard(c, item[1], item[2])
    elif kind == "door":
        _door_frame(c, item[1], item[2] * CELL, item[3])
    elif kind == "archway":
        A._archway(c, item[1], item[2] * CELL, item[3])
    elif kind == "side_door":
        _side_door(c, item[1] * CELL, item[2] * CELL, item[3] * CELL, item[4])
    elif kind == "shelf":
        A._pantry_shelf(c, item[1], item[2])
    elif kind == "pans":
        A._pan_rail(c, item[1], item[2])
