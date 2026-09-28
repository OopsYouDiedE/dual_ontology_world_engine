"""Paint the life wing's floors, wall faces, caps and wall-mounted decor at 1x."""
from __future__ import annotations

from .layout import CELL, DORM_X, H, ROWS, W, mark, region, walkable
from .palette import (HONEY, SAGE_TILE, BOTTLE, BRASS, CAP, CHALK, CLARET, CREAM, DORMS, FOG, GRASS, IRON, LEAF, LINEN,
                      OAK, OCHRE, PLANK, STONE, TILE, TILE_GREEN, VOID, WALNUT)
from .pixel import Canvas, mix, rng, shift

MAP_W, MAP_H = W * CELL, H * CELL

# Wall-mounted decor lives in the architecture so it can never drift from its
# wall. The scene reads these anchors from layout.json for its light sources.
WINDOWS = [(32 + 320 * i + 76, 32, 28) for i in range(4)] + [(347, 480, 34), (863, 480, 34)]
SCONCES = [(92, 320), (282, 320), (412, 320), (602, 320), (732, 320), (922, 320), (1052, 320), (1216, 320), (594, 834), (686, 834), (544, 962)]
PAINTINGS = [(347, 320, "sea"), (667, 320, "cliff"), (987, 320, "lighthouse"),
             (640, 832, "lighthouse"), (32 + 184, 32, "sea"), (352 + 184, 32, "cliff"), (672 + 184, 32, "lighthouse"), (992 + 184, 32, "sea")]
DORM_DOORS = [(5 + 10 * i) * CELL for i in range(4)]
ARCHWAYS = [(7 * CELL, 480, "study"), (31 * CELL, 480, "prep")]
STAIRS = [16 * CELL, 22 * CELL]


def build() -> tuple[Canvas, dict]:
    c = Canvas(MAP_W, MAP_H, VOID)
    _exterior(c)
    _floors(c)
    _faces(c)
    _caps(c)
    _occlusion(c)
    for i, x in enumerate(DORM_DOORS):
        _dorm_door_frame(c, x, 320, DORMS[i]["name"])
    for x, y, kind in ARCHWAYS:
        _archway(c, x, y, kind)
    for x in STAIRS:
        _stairs(c, x)
    for x, y, w in WINDOWS:
        _window(c, x, y, w)
    for x, y, kind in PAINTINGS:
        if not (y == 32 and x == 216):  # R-05 carries the clock and sketch instead
            _painting(c, x, y + 10, kind)
    for x, y in SCONCES:
        _sconce(c, x, y)
    _blackboard(c, 373, 488)
    _pantry_shelf(c, 690, 492)
    _pan_rail(c, 896, 490)
    _exterior_windows(c)
    anchors = {
        "windows": [{"x": x, "y": y, "width": w} for x, y, w in WINDOWS],
        "sconces": [{"x": x, "y": y + 22} for x, y in SCONCES],
    }
    return c, anchors


# ---------------------------------------------------------------- floors ----

def _cells(test):
    for y in range(H):
        for x in range(W):
            if test(x, y):
                yield x, y


def _paste_cells(c: Canvas, source: Canvas, test) -> None:
    for x, y in _cells(test):
        c.a[y * CELL:(y + 1) * CELL, x * CELL:(x + 1) * CELL] = source.a[y * CELL:(y + 1) * CELL, x * CELL:(x + 1) * CELL]


def _boards(ramp, height: int, lengths: tuple[int, int], seed: int, grain: float = 0.35, tones=(3, 3, 4, 4, 3, 2), seam: int = 1) -> Canvas:
    """Staggered plank floor; each board keeps its own tone and grain."""
    g = rng(seed)
    t = Canvas(MAP_W, MAP_H)
    for top in range(0, MAP_H, height):
        x = -int(g.integers(0, lengths[1]))
        while x < MAP_W:
            length = int(g.integers(lengths[0], lengths[1]))
            tone = int(g.choice(tones))
            base = ramp[tone]
            t.rect(x, top, length, height - 1, base)
            # Soft bevel: lit leading edge, shaded trailing seam.
            t.hline(x, top, length, mix(base, ramp[tone + 1], 0.55))
            for _ in range(int(length * grain / 6)):
                gx = x + int(g.integers(2, max(3, length - 6)))
                gy = top + int(g.integers(1, height - 1))
                t.hline(gx, gy, int(g.integers(3, 9)), mix(base, ramp[tone - 1], 0.3))
            if g.random() < 0.04:
                kx, ky = x + int(g.integers(4, max(5, length - 4))), top + height // 2 - 1
                t.px(kx, ky, ramp[tone - 2])
                t.px(kx + 1, ky, ramp[tone - 1])
            t.hline(x, top + height - 1, length, ramp[seam])
            end = mix(ramp[seam], base, 0.45)
            t.vline(x + length - 1, top, height - 1, end)
            x += length
    return t


def _herringbone(ramp, seed: int, unit: int = 8) -> Canvas:
    g = rng(seed)
    t = Canvas(MAP_W, MAP_H)
    tones: dict = {}
    for v in range(MAP_H // unit + 1):
        for u in range(MAP_W // unit + 1):
            d = (u - v) % 4
            if d == 0:
                key, rect = (u, v, "h"), (u * unit, v * unit, unit * 2, unit)
            elif d == 3:
                key, rect = (u, v, "v"), (u * unit, v * unit, unit, unit * 2)
            else:
                continue
            tone = tones.setdefault(key, int(g.choice([3, 3, 4])))
            x, y, w, h = rect
            t.rect(x, y, w, h, ramp[tone])
            t.hline(x, y + h - 1, w, ramp[2])
            t.vline(x + w - 1, y, h, ramp[2])
            if w > h and g.random() < 0.6:
                t.hline(x + 2 + int(g.integers(0, 4)), y + 2 + int(g.integers(0, 2)), 4, mix(ramp[tone], ramp[tone - 1], 0.4))
            elif h > w and g.random() < 0.6:
                t.vline(x + 2 + int(g.integers(0, 2)), y + 2 + int(g.integers(0, 4)), 4, mix(ramp[tone], ramp[tone - 1], 0.4))
    return t


def _checker(seed: int) -> Canvas:
    g = rng(seed)
    t = Canvas(MAP_W, MAP_H)
    size = 16
    for ty in range(0, MAP_H, size):
        for tx in range(0, MAP_W, size):
            dark = ((tx + ty) // size) % 2 == 1
            ramp = SAGE_TILE if dark else TILE
            tone = 3 if dark else int(g.choice([2, 2, 3]))
            t.rect(tx, ty, size, size, ramp[tone])
            t.hline(tx + 1, ty + 1, size - 2, ramp[min(tone + 1, len(ramp) - 1)])
            t.vline(tx + 1, ty + 1, size - 2, mix(ramp[tone], ramp[min(tone + 1, len(ramp) - 1)], 0.5))
            t.hline(tx, ty + size - 1, size, TILE[1])
            t.vline(tx + size - 1, ty, size, TILE[1])
            if g.random() < 0.18:
                cx, cy = tx + int(g.integers(3, 12)), ty + int(g.integers(3, 12))
                t.px(cx, cy, ramp[tone - 1])
    return t


def _flagstones(seed: int) -> Canvas:
    g = rng(seed)
    t = Canvas(MAP_W, MAP_H)
    y = 0
    while y < MAP_H:
        h = int(g.choice([16, 24, 24, 32]))
        x = -int(g.integers(0, 24))
        while x < MAP_W:
            w = int(g.choice([24, 32, 32, 40, 48]))
            tone = int(g.choice([3, 4, 4, 5]))
            base = STONE[tone]
            t.rect(x, y, w, h, base)
            t.hline(x + 1, y + 1, w - 2, STONE[tone + 1])
            t.vline(x + 1, y + 1, h - 2, mix(base, STONE[tone + 1], 0.5))
            t.hline(x + 1, y + h - 2, w - 2, mix(base, STONE[tone - 1], 0.6))
            for _ in range(w * h // 40):
                t.px(x + int(g.integers(2, w - 2)), y + int(g.integers(2, h - 2)), STONE[tone + int(g.choice([-1, 1]))])
            if g.random() < 0.25:
                cx, cy = x + int(g.integers(4, w - 8)), y + int(g.integers(4, h - 6))
                for step in range(int(g.integers(3, 7))):
                    t.px(cx + step, cy + (step // 2) * int(g.choice([-1, 1])), STONE[tone - 2])
            t.hline(x, y + h - 1, w, STONE[1])
            t.vline(x + w - 1, y, h, STONE[1])
            x += w
        y += h
    return t


def floor_materials() -> dict[str, Canvas]:
    """Shared material recipes for the painted map and MCP material inspection."""
    return {
        "oak": _boards(OAK, 6, (48, 120), 11, 0.22, (3, 3, 4, 4, 3), 2),
        "corridor": _boards(PLANK, 8, (48, 128), 12, 0.25),
        "study": _herringbone(HONEY, 13),
        "prep": _checker(14),
        "hall": _flagstones(15),
    }


def _floors(c: Canvas) -> None:
    materials = floor_materials()
    oak, corridor, study, prep, hall = (materials[key] for key in ("oak", "corridor", "study", "prep", "hall"))
    floor = lambda x, y: walkable(x, y)
    _paste_cells(c, oak, lambda x, y: floor(x, y) and region(x, y).startswith("dorm"))
    _paste_cells(c, corridor, lambda x, y: floor(x, y) and region(x, y) == "corridor")
    _paste_cells(c, study, lambda x, y: floor(x, y) and region(x, y) == "study")
    _paste_cells(c, prep, lambda x, y: floor(x, y) and region(x, y) == "prep")
    _paste_cells(c, hall, lambda x, y: floor(x, y) and region(x, y) == "hall")
    _runner(c, 40, 396, 1200, 40)
    for x, y, w in WINDOWS:
        _floor_glow(c, x, y + 64, w)


def _runner(c: Canvas, x: int, y: int, w: int, h: int) -> None:
    """Corridor runner: deep teal field, ochre border, brass rods at each end."""
    from .palette import TEAL
    c.rect(x, y, w, h, TEAL[2])
    c.rect(x + 3, y + 3, w - 6, h - 6, TEAL[3])
    c.frame(x + 2, y + 2, w - 4, h - 4, OCHRE[3])
    c.frame(x + 5, y + 5, w - 10, h - 10, CLARET[3])
    for gx in range(x + 12, x + w - 12, 16):
        c.px(gx, y + 3, OCHRE[4])
        c.px(gx, y + h - 4, OCHRE[4])
    for gx in range(x + 20, x + w - 20, 40):
        cy = y + h // 2
        for dy in range(-6, 7):
            span = 6 - abs(dy)
            c.hline(gx - span, cy + dy, span * 2 + 1, TEAL[4] if abs(dy) % 3 else OCHRE[2])
        c.rect(gx - 1, cy - 1, 3, 3, CLARET[4])
    c.hline(x, y + h, w, shift(TEAL[1], -0.2))
    for rx in (x, x + w - 1):
        c.vline(rx, y - 1, h + 2, BRASS[3])


def _floor_glow(c: Canvas, cx: int, top: int, width: int) -> None:
    """Pale window light pooled on the boards; dithered, never smooth."""
    for row in range(52):
        t = 1 - row / 52
        half = width // 2 + 4 + row // 3
        c.dither(cx - half, top + row, half * 2, 1, (232, 238, 244, 26), 0.3 * t + 0.08, row)


def _exterior(c: Canvas) -> None:
    g = rng(21)
    for x, y in _cells(lambda x, y: mark(x, y) == " " and y >= 26):
        for yy in range(y * CELL, (y + 1) * CELL):
            for xx in range(x * CELL, (x + 1) * CELL):
                tone = GRASS[int(g.choice([1, 2, 2, 3]))]
                c.a[yy, xx] = tone
        for _ in range(9):
            tx, ty = x * CELL + int(g.integers(1, 30)), y * CELL + int(g.integers(2, 30))
            c.vline(tx, ty - 2, 3, GRASS[4])
            c.px(tx + 1, ty - 1, GRASS[3])
        if g.random() < 0.3:
            sx, sy = x * CELL + int(g.integers(4, 22)), y * CELL + int(g.integers(4, 22))
            c.ellipse(sx + 4, sy + 3, 5, 3, STONE[1])
            c.ellipse(sx + 4, sy + 2, 4, 2, STONE[2])
    # Gravel apron along the building walls.
    for x in range(W):
        for y in range(H):
            if mark(x, y) == " " and y >= 26 and (mark(x, y - 1) in "w#" or mark(x - 1, y) == "#" or mark(x + 1, y) == "#"):
                x0, y0 = x * CELL, y * CELL
                for yy in range(y0, y0 + 10 if mark(x, y - 1) in "w#" else y0 + 32):
                    for xx in range(x0, x0 + 32):
                        near = (mark(x, y - 1) in "w#" and yy < y0 + 10) or (mark(x - 1, y) == "#" and xx < x0 + 8) or (mark(x + 1, y) == "#" and xx >= x0 + 24)
                        if near:
                            c.a[yy, xx] = STONE[int(g.choice([1, 2, 2, 3]))]


# ----------------------------------------------------------------- walls ----

def _face_runs():
    for y in range(H):
        x = 0
        while x < W:
            m = mark(x, y)
            if m in "WG":
                start = x
                while x < W and mark(x, y) == m and region(x, y) == region(start, y):
                    x += 1
                yield start, x, y, region(start, y), m
            else:
                x += 1


def _faces(c: Canvas) -> None:
    for x0, x1, y, room, m in _face_runs():
        px0, px1, top = x0 * CELL, x1 * CELL, y * CELL
        if m == "G":
            _ashlar(c, px0, px1, top, 64, STONE, 16, (28, 44))
            c.rect(px0, top + 58, px1 - px0, 6, STONE[1])
            c.hline(px0, top + 63, px1 - px0, CAP[0])
        elif room.startswith("dorm"):
            info = DORMS[int(room[-1])]
            _papered(c, px0, px1, top, info["paper"], info["motif"], info["accent"])
        elif room == "corridor":
            _papered(c, px0, px1, top, CLARET, "damask", OCHRE)
        elif room == "study":
            _papered(c, px0, px1, top, BOTTLE, "stars", OCHRE)
        elif room == "prep":
            _tiled(c, px0, px1, top)
        elif room == "hall":
            _ashlar(c, px0, px1, top, 64, STONE, 16, (24, 40))
        else:
            _exterior_wall(c, px0, px1, top)


def _crown(c: Canvas, x0: int, x1: int, top: int, ramp) -> None:
    w = x1 - x0
    c.hline(x0, top, w, CAP[0])
    c.hline(x0, top + 1, w, ramp[5])
    c.hline(x0, top + 2, w, ramp[4])
    c.hline(x0, top + 3, w, ramp[2])
    for x in range(x0, x1, 4):
        c.px(x, top + 2, ramp[3])


def _papered(c: Canvas, x0: int, x1: int, top: int, paper, motif: str, accent) -> None:
    w = x1 - x0
    paper_top, paper_bottom = top + 4, top + 38
    c.rect(x0, paper_top, w, paper_bottom - paper_top, paper[3])
    for x in range(x0, x1):
        for y in range(paper_top, paper_bottom):
            lx, ly = x - x0, y - paper_top
            if motif == "pinstripe":
                if lx % 8 == 0:
                    c.px(x, y, paper[4])
                if lx % 16 == 12 and ly % 12 in (5, 6) or (lx % 16 in (11, 13) and ly % 12 == 5):
                    c.px(x, y, accent[3])
            elif motif == "lattice":
                if (lx + ly) % 10 == 0 or (lx - ly) % 10 == 0:
                    c.px(x, y, paper[2])
                if (lx + ly) % 10 == 0 and (lx - ly) % 10 == 0:
                    c.px(x, y, accent[3])
            elif motif == "sprig":
                gx, gy = lx % 12, (ly + (6 if (lx // 12) % 2 else 0)) % 12
                if (gx, gy) in ((5, 4), (6, 3), (7, 4), (6, 5)):
                    c.px(x, y, paper[5])
                elif (gx, gy) == (6, 4):
                    c.px(x, y, accent[4])
                elif (gx, gy) in ((5, 6), (7, 7)):
                    c.px(x, y, paper[2])
            elif motif == "damask":
                gx, gy = lx % 12, (ly + (8 if (lx // 12) % 2 else 0)) % 16
                shape = abs(gx - 6) + abs(gy - 8) // 2
                if shape == 3 or (gx == 6 and gy in (4, 12)):
                    c.px(x, y, paper[4])
                elif shape <= 1:
                    c.px(x, y, paper[2])
            elif motif == "stars":
                gx, gy = lx % 14, (ly + (7 if (lx // 14) % 2 else 0)) % 14
                if (gx, gy) == (7, 7):
                    c.px(x, y, accent[4])
                elif (gx, gy) in ((6, 7), (8, 7), (7, 6), (7, 8)):
                    c.px(x, y, accent[2])
    # Ceiling shade and a deliberate top frieze band.
    c.dither(x0, paper_top, w, 5, shift(paper[2], -0.1), 0.55)
    c.hline(x0, paper_top + 5, w, paper[2])
    _crown(c, x0, x1, top, CREAM)
    _painted_wainscot(c, x0, x1, top + 38)


def _painted_wainscot(c: Canvas, x0: int, x1: int, top: int) -> None:
    w = x1 - x0
    # Chair rail
    c.hline(x0, top, w, CREAM[5])
    c.hline(x0, top + 1, w, CREAM[4])
    c.hline(x0, top + 2, w, CREAM[1])
    # Painted panels
    c.rect(x0, top + 3, w, 19, CREAM[3])
    for px in range(x0 + 4, x1 - 20, 32):
        pw = min(26, x1 - px - 4)
        c.rect(px, top + 6, pw, 13, CREAM[3])
        c.hline(px, top + 6, pw, CREAM[1])
        c.vline(px, top + 6, 13, CREAM[1])
        c.hline(px + 1, top + 7, pw - 1, CREAM[4])
        c.vline(px + 1, top + 7, 12, CREAM[4])
        c.hline(px, top + 18, pw, CREAM[5])
        c.vline(px + pw - 1, top + 6, 13, CREAM[5])
        c.rect(px + 3, top + 9, pw - 6, 7, mix(CREAM[3], CREAM[4], 0.5))
    c.dither(x0, top + 3, w, 3, CREAM[2], 0.4)
    # Skirting board meeting the floor
    c.hline(x0, top + 22, w, CREAM[5])
    c.rect(x0, top + 23, w, 2, WALNUT[3])
    c.hline(x0, top + 25, w, WALNUT[1])


def _tiled(c: Canvas, x0: int, x1: int, top: int) -> None:
    w = x1 - x0
    c.rect(x0, top + 4, w, 38, TILE[3])
    for row, y in enumerate(range(top + 4, top + 40, 5)):
        off = 5 if row % 2 else 0
        c.hline(x0, y + 4, w, TILE[1])
        for x in range(x0 - off, x1, 10):
            c.vline(x, y, 5, TILE[1])
            c.hline(x + 1, y, 8, TILE[5])
    c.dither(x0, top + 4, w, 6, TILE[1], 0.4)
    _crown(c, x0, x1, top, CREAM)
    # Dark green tiled dado and a narrow brass-coloured band.
    c.rect(x0, top + 38, w, 2, OCHRE[3])
    c.rect(x0, top + 40, w, 20, TILE_GREEN[3])
    for x in range(x0, x1, 8):
        c.vline(x, top + 40, 20, TILE_GREEN[1])
        c.vline(x + 1, top + 41, 18, TILE_GREEN[4])
    for y in (top + 49,):
        c.hline(x0, y, w, TILE_GREEN[1])
    c.hline(x0, top + 60, w, TILE_GREEN[4])
    c.rect(x0, top + 61, w, 2, TILE_GREEN[1])
    c.hline(x0, top + 63, w, TILE_GREEN[0])


def _ashlar(c: Canvas, x0: int, x1: int, top: int, height: int, ramp, course: int, widths) -> None:
    g = rng(x0 * 7 + top)
    w = x1 - x0
    c.rect(x0, top, w, height, ramp[3])
    for row, y in enumerate(range(top, top + height - 6, course)):
        x = x0 - (widths[0] // 2 if row % 2 else 0)
        while x < x1:
            bw = int(g.integers(widths[0], widths[1]))
            tone = int(g.choice([2, 3, 3, 4]))
            c.rect(max(x, x0), y, min(bw, x1 - max(x, x0)), course, ramp[tone])
            c.hline(max(x, x0), y, min(bw, x1 - max(x, x0)), ramp[tone + 1])
            c.hline(max(x, x0), y + course - 1, min(bw, x1 - max(x, x0)), ramp[1])
            if x >= x0:
                c.vline(x, y, course, ramp[1])
                c.vline(x + 1, y + 1, course - 2, mix(ramp[tone], ramp[tone + 1], 0.5))
            for _ in range(bw * course // 30):
                c.px(x + int(g.integers(1, bw)), y + int(g.integers(1, course - 1)), ramp[tone - 1])
            x += bw
    c.hline(x0, top, w, CAP[0])
    c.dither(x0, top + 1, w, 4, ramp[1], 0.5)
    plinth = top + height - 6
    c.rect(x0, plinth, w, 6, ramp[2])
    c.hline(x0, plinth, w, ramp[4])
    c.hline(x0, plinth + 5, w, ramp[0])


def _exterior_wall(c: Canvas, x0: int, x1: int, top: int) -> None:
    _ashlar(c, x0, x1, top, 64, STONE, 12, (14, 26))
    c.dither(x0, top, x1 - x0, 64, (24, 34, 44, 90), 0.5)
    g = rng(x0 + 99)
    for x in range(x0, x1, 3):
        if g.random() < 0.35:
            length = int(g.integers(6, 30))
            for y in range(top + 64 - length, top + 64):
                c.px(x + int(g.integers(-1, 2)), y, LEAF[int(g.choice([1, 2, 3]))])


def _caps(c: Canvas) -> None:
    g = rng(31)
    for x, y in _cells(lambda x, y: mark(x, y) == "#"):
        x0, y0 = x * CELL, y * CELL
        c.rect(x0, y0, CELL, CELL, CAP[1])
        for _ in range(10):
            c.px(x0 + int(g.integers(0, 32)), y0 + int(g.integers(0, 32)), CAP[2])
    for x, y in _cells(lambda x, y: mark(x, y) == "#"):
        x0, y0 = x * CELL, y * CELL
        open_n, open_s = mark(x, y - 1) != "#", mark(x, y + 1) != "#"
        open_w, open_e = mark(x - 1, y) != "#", mark(x + 1, y) != "#"
        if open_n:
            c.hline(x0, y0, CELL, CAP[0])
            c.hline(x0, y0 + 1, CELL, CAP[4])
            c.hline(x0, y0 + 2, CELL, CAP[3])
        if open_s:
            c.hline(x0, y0 + CELL - 3, CELL, CAP[3])
            c.hline(x0, y0 + CELL - 2, CELL, CAP[4])
            c.hline(x0, y0 + CELL - 1, CELL, CAP[0])
        if open_w:
            c.vline(x0, y0, CELL, CAP[0])
            c.vline(x0 + 1, y0, CELL, CAP[4])
            c.vline(x0 + 2, y0, CELL, CAP[3])
        if open_e:
            c.vline(x0 + CELL - 3, y0, CELL, CAP[3])
            c.vline(x0 + CELL - 2, y0, CELL, CAP[4])
            c.vline(x0 + CELL - 1, y0, CELL, CAP[0])
        # Inner corners keep the rim continuous around re-entrant joints.
        for dx, dy, cx, cy in ((-1, -1, 0, 0), (1, -1, CELL - 3, 0), (-1, 1, 0, CELL - 3), (1, 1, CELL - 3, CELL - 3)):
            if mark(x + dx, y) == "#" and mark(x, y + dy) == "#" and mark(x + dx, y + dy) != "#":
                c.rect(x0 + cx, y0 + cy, 3, 3, CAP[3])
                c.px(x0 + (0 if dx < 0 else CELL - 1), y0 + (0 if dy < 0 else CELL - 1), CAP[0])
        # Outer corners get their outline back after the rims overlap.
        if open_n and open_w:
            c.px(x0 + 1, y0 + 1, CAP[3])
        if open_n and open_e:
            c.px(x0 + CELL - 2, y0 + 1, CAP[3])


def _occlusion(c: Canvas) -> None:
    """Contact shadow where floors meet wall bases and cap edges."""
    for x, y in _cells(lambda x, y: walkable(x, y)):
        x0, y0 = x * CELL, y * CELL
        above = mark(x, y - 1)
        if above in "w" or (above == "#" and mark(x, y - 2) != "#"):
            depth = 7 if above == "w" else 4
            for i in range(depth):
                c.rect(x0, y0 + i, CELL, 1, (26, 18, 32, int(120 * (1 - i / depth) ** 1.6)))
        if mark(x - 1, y) == "#":
            for i in range(4):
                c.rect(x0 + i, y0, 1, CELL, (26, 18, 32, int(95 * (1 - i / 4))))
        if mark(x + 1, y) == "#":
            for i in range(4):
                c.rect(x0 + CELL - 1 - i, y0, 1, CELL, (26, 18, 32, int(95 * (1 - i / 4))))
        if mark(x, y + 1) == "#":
            c.rect(x0, y0 + CELL - 2, CELL, 2, (26, 18, 32, 70))
        if mark(x, y + 1) == "G":
            for i in range(3):
                c.rect(x0, y0 + CELL - 1 - i, CELL, 1, (26, 16, 30, 90 - i * 28))


# ----------------------------------------------------------------- decor ----

def _dorm_door_frame(c: Canvas, x: int, top: int, number: str) -> None:
    """Architrave, cornice and number plate; the moving leaves are drawn at runtime."""
    opening_top = top + 8
    # Passage seen through the open doorway.
    for y in range(opening_top, top + 64):
        t = (y - opening_top) / (64 - 8)
        c.hline(x, y, 64, mix(shift(OAK[2], -0.45), OAK[2], t))
    c.rect(x, opening_top, 3, 56, (20, 14, 22, 150))
    c.rect(x + 61, opening_top, 3, 56, (20, 14, 22, 150))
    # Architrave
    for side in (x - 6, x + 64):
        c.rect(side, top + 4, 6, 60, CREAM[3])
        c.vline(side, top + 4, 60, CREAM[5] if side < x else CREAM[1])
        c.vline(side + 5, top + 4, 60, CREAM[1] if side < x else CREAM[5])
        c.vline(side + 2, top + 6, 56, CREAM[4])
        c.rect(side, top + 58, 6, 6, CREAM[2])
    # Cornice
    c.rect(x - 8, top, 80, 8, CREAM[3])
    c.hline(x - 8, top, 80, CREAM[5])
    c.hline(x - 8, top + 1, 80, CREAM[4])
    c.hline(x - 8, top + 7, 80, CREAM[1])
    c.hline(x - 6, top + 8, 76, (20, 14, 22, 120))
    # Brass number plate
    px, py = x + 19, top
    c.rect(px, py, 26, 9, BRASS[1])
    c.rect(px + 1, py + 1, 24, 7, BRASS[3])
    c.hline(px + 1, py + 1, 24, BRASS[4])
    _digits(c, px + 3, py + 2, number, WALNUT[1])


FONT = {
    "R": ["110", "101", "110", "101", "101"], "-": ["000", "000", "111", "000", "000"],
    "0": ["111", "101", "101", "101", "111"], "5": ["111", "100", "111", "001", "111"],
    "6": ["111", "100", "111", "101", "111"], "7": ["111", "001", "010", "010", "010"],
    "8": ["111", "101", "111", "101", "111"],
}


def _digits(c: Canvas, x: int, y: int, text: str, color) -> None:
    for ch in text:
        glyph = FONT[ch]
        for gy, row in enumerate(glyph):
            for gx, bit in enumerate(row):
                if bit == "1":
                    c.px(x + gx, y + gy, color)
        x += 4


def _archway(c: Canvas, x: int, top: int, kind: str) -> None:
    for y in range(top + 6, top + 64):
        t = (y - top - 6) / 58
        base = OAK[2] if kind == "study" else TILE[2]
        c.hline(x, y, 64, mix(shift(base, -0.5), base, t))
    for side in (x - 6, x + 64):
        c.rect(side, top + 2, 6, 62, WALNUT[3])
        c.vline(side, top + 2, 62, WALNUT[5] if side < x else WALNUT[1])
        c.vline(side + 5, top + 2, 62, WALNUT[1] if side < x else WALNUT[5])
        c.vline(side + 2, top + 4, 58, WALNUT[4])
    c.rect(x - 8, top, 80, 7, WALNUT[3])
    c.hline(x - 8, top, 80, WALNUT[5])
    c.hline(x - 8, top + 6, 80, WALNUT[1])
    c.rect(x + 26, top + 1, 12, 5, BRASS[2])
    c.hline(x + 27, top + 2, 10, BRASS[4])


def _stairs(c: Canvas, x: int) -> None:
    """Stone flight descending from the public rooms into the hall (rows 26-27)."""
    top = 26 * CELL
    for step in range(5):
        y = top + step * 12 + 2
        shade = step * 0.07
        tread, riser = shift(STONE[5], -shade), shift(STONE[3], -shade)
        c.rect(x, y, 64, 8, tread)
        c.hline(x, y, 64, shift(STONE[6], -shade))
        c.dither(x, y + 1, 64, 6, shift(STONE[4], -shade), 0.18, step)
        c.rect(x, y + 8, 64, 4, riser)
        c.hline(x, y + 8, 64, shift(STONE[1], -shade))
    c.rect(x, top, 64, 2, STONE[2])
    # Side walls cast shade over the flight edges.
    for i in range(5):
        c.vline(x + i, top, 64, (20, 14, 22, 150 - i * 28))
        c.vline(x + 63 - i, top, 64, (20, 14, 22, 150 - i * 28))


def _window(c: Canvas, cx: int, top: int, width: int) -> None:
    x0, x1 = cx - width // 2, cx + width // 2
    wy0, wy1 = top + 7, top + 40
    # Fog glass with a cold gradient and drifting dithered banks.
    for y in range(wy0, wy1):
        t = (y - wy0) / (wy1 - wy0)
        c.hline(x0, y, width, mix(FOG[4], FOG[3], t))
    for band, (fy, dens) in enumerate(((wy0 + 6, 0.35), (wy0 + 16, 0.5), (wy0 + 25, 0.3))):
        c.dither(x0, fy, width, 4, FOG[5], dens, band)
    c.dither(x0, wy1 - 8, width, 8, FOG[2], 0.25)
    # Sash bars
    for gx in (x0 + width // 2 - 1,):
        c.vline(gx, wy0, wy1 - wy0, CREAM[3])
        c.vline(gx + 1, wy0, wy1 - wy0, CREAM[1])
    for gy in (wy0 + 11, wy0 + 22):
        c.hline(x0, gy, width, CREAM[3])
        c.hline(x0, gy + 1, width, CREAM[1])
    c.frame(x0 - 2, wy0 - 2, width + 4, wy1 - wy0 + 4, CREAM[3])
    c.frame(x0 - 3, wy0 - 3, width + 6, wy1 - wy0 + 6, CREAM[1])
    c.hline(x0 - 1, wy0 - 1, width + 2, CREAM[5])
    # Sill
    c.rect(x0 - 5, wy1 + 1, width + 10, 3, CREAM[4])
    c.hline(x0 - 5, wy1 + 1, width + 10, CREAM[5])
    c.hline(x0 - 5, wy1 + 4, width + 10, (20, 14, 22, 130))
    # Heavy curtains on a brass rod
    c.hline(x0 - 10, wy0 - 5, width + 20, BRASS[3])
    c.hline(x0 - 10, wy0 - 4, width + 20, BRASS[1])
    for x in (x0 - 11, x1 + 10):
        c.rect(x, wy0 - 6, 2, 3, BRASS[4])
    for side, sx in ((0, x0 - 9), (1, x1 + 2)):
        for i in range(7):
            fold = CLARET[4] if i in (1, 4) else CLARET[3] if i in (0, 2, 5) else CLARET[2]
            c.vline(sx + i, wy0 - 3, wy1 - wy0 + 10, fold)
        tie = wy0 + 20
        c.hline(sx, tie, 7, OCHRE[4])
        c.hline(sx, tie + 1, 7, OCHRE[2])
        # Drape flares below the tie-back
        c.rect(sx - (1 if side == 0 else 0), tie + 2, 8, wy1 - tie + 5, CLARET[3])
        for i in range(0, 8, 3):
            c.vline(sx + i - (1 if side == 0 else 0), tie + 2, wy1 - tie + 5, CLARET[4])
        c.hline(sx - 1, wy1 + 7, 9, CLARET[1])


def _painting(c: Canvas, cx: int, top: int, kind: str) -> None:
    w, h = 22, 16
    x0 = cx - w // 2
    c.rect(x0 - 2, top - 2, w + 4, h + 4, BRASS[2])
    c.frame(x0 - 2, top - 2, w + 4, h + 4, BRASS[1])
    c.hline(x0 - 1, top - 1, w + 2, BRASS[4])
    for y in range(top, top + h):
        t = (y - top) / h
        c.hline(x0, y, w, mix(FOG[3], FOG[1], t))
    if kind == "sea":
        c.rect(x0, top + 10, w, 6, shift(FOG[1], -0.2))
        c.hline(x0, top + 10, w, FOG[4])
        c.hline(x0 + 3, top + 12, 6, FOG[3])
    elif kind == "cliff":
        c.poly([(x0, top + 6), (x0 + 9, top + 4), (x0 + 12, top + 16), (x0, top + 16)], STONE[2])
        c.rect(x0, top + 12, w, 4, shift(FOG[1], -0.25))
    else:
        c.rect(x0 + 14, top + 4, 3, 9, LINEN[4])
        c.rect(x0 + 14, top + 4, 3, 2, CLARET[3])
        c.px(x0 + 15, top + 3, FLAMEISH)
        c.rect(x0, top + 12, w, 4, shift(FOG[1], -0.25))
    c.hline(x0 - 2, top + h + 2, w + 4, (20, 14, 22, 110))


FLAMEISH = (255, 214, 130, 255)


def _sconce(c: Canvas, cx: int, top: int) -> None:
    y = top + 14
    c.rect(cx - 3, y + 8, 7, 4, BRASS[2])
    c.hline(cx - 3, y + 8, 7, BRASS[4])
    c.vline(cx, y + 12, 3, BRASS[1])
    c.rect(cx - 1, y + 14, 3, 2, BRASS[3])
    # Frosted chimney with the flame glow
    c.rect(cx - 2, y, 5, 8, (238, 226, 196, 255))
    c.vline(cx - 2, y, 8, (206, 190, 160, 255))
    c.rect(cx - 1, y + 3, 3, 4, (255, 225, 150, 255))
    c.px(cx, y + 2, (255, 246, 214, 255))
    c.hline(cx - 2, y - 1, 5, BRASS[3])
    c.dither(cx - 7, y - 5, 15, 18, (255, 214, 140, 60), 0.3)


def _blackboard(c: Canvas, x: int, y: int) -> None:
    w, h = 60, 34
    c.rect(x - 2, y - 2, w + 4, h + 4, WALNUT[3])
    c.hline(x - 2, y - 2, w + 4, WALNUT[5])
    c.hline(x - 2, y + h + 1, w + 4, WALNUT[1])
    c.rect(x, y, w, h, CHALK[1])
    c.dither(x, y, w, h, CHALK[2], 0.25)
    chalk = (208, 206, 192, 255)
    faint = (126, 134, 124, 255)
    # A faint measured triangle and ruled lines; half wiped away.
    c.line(x + 6, y + 26, x + 26, y + 26, chalk)
    c.line(x + 6, y + 26, x + 6, y + 8, chalk)
    c.line(x + 6, y + 8, x + 26, y + 26, faint)
    for i, lx in enumerate(range(x + 34, x + 56, 4)):
        c.hline(lx, y + 8 + i * 3, 3, chalk if i % 2 else faint)
    c.dither(x + 30, y + 16, 26, 14, CHALK[2], 0.6)
    c.rect(x + 4, y + h + 1, w - 8, 2, WALNUT[4])
    c.rect(x + 40, y + h, 6, 1, (230, 228, 214, 255))


def _pantry_shelf(c: Canvas, x: int, y: int) -> None:
    for row in (0, 16):
        sy = y + row + 12
        c.rect(x, sy, 70, 3, WALNUT[4])
        c.hline(x, sy, 70, WALNUT[5])
        c.hline(x, sy + 3, 70, (20, 14, 22, 130))
        for i, jx in enumerate(range(x + 3, x + 66, 9)):
            tone = [OCHRE, BRASS, TILE, CLARET, LINEN, TILE_GREEN, OCHRE][i % 7]
            jh = 7 + (i * 3) % 4
            c.rect(jx, sy - jh, 6, jh, tone[2])
            c.vline(jx + 1, sy - jh + 1, jh - 2, tone[4] if len(tone) > 4 else tone[3])
            c.rect(jx, sy - jh - 2, 6, 2, LINEN[3] if i % 2 else WALNUT[3])


def _pan_rail(c: Canvas, x: int, y: int) -> None:
    c.rect(x, y, 64, 2, IRON[3])
    c.hline(x, y, 64, IRON[5])
    for i, px in enumerate(range(x + 6, x + 60, 14)):
        r = 5 + i % 2
        c.vline(px, y + 2, 4, IRON[2])
        c.ellipse(px, y + 7 + r, r, r, BRASS[2])
        c.ellipse(px - 1, y + 6 + r, r - 2, r - 2, BRASS[3])
        c.px(px - 2, y + 4 + r, BRASS[5])


def _exterior_windows(c: Canvas) -> None:
    for cx in (120, 300, 420, 880, 1000, 1160):
        y = 26 * CELL + 14
        c.rect(cx - 8, y - 2, 16, 26, STONE[5])
        c.rect(cx - 6, y, 12, 22, (70, 48, 32, 255))
        c.rect(cx - 5, y + 1, 10, 20, (74, 70, 86, 255))
        c.dither(cx - 5, y + 1, 10, 20, (150, 128, 104, 255), 0.3)
        for bx in (cx - 3, cx, cx + 3):
            c.vline(bx, y, 22, IRON[1])
        c.hline(cx - 9, y + 22, 18, STONE[6])


# ------------------------------------------------------------ floor work ----

def bake_floor_work(c: Canvas, furniture: list[dict], rugs, shoes) -> None:
    """Rugs, shoes and soft contact shadows, all at 1x on the painted floor."""
    for x, y, w, h, key in rugs:
        _rug(c, x, y, w, h, key)
    for x, y in shoes:
        _shoes(c, x, y)
    for item in furniture:
        fp = item.get("footprint")
        if not fp:
            continue
        x, y, w, h = fp
        # Light from the upper left: shade spills right and down.
        for i in range(4):
            a = int(70 * (1 - i / 4) ** 1.4)
            c.rect(x - 1 + i, y + h + i, w + 1, 1, (26, 16, 30, a))
            c.rect(x + w + i, y + 2 + i, 1, h - 1, (26, 16, 30, a))
        c.rect(x, y + h - 2, w, 2, (26, 16, 30, 90))


def _rug(c: Canvas, x: int, y: int, w: int, h: int, key) -> None:
    from .palette import TEAL, SAGE, ROSE, PLUM
    if key == "study":
        field, border, trim = BOTTLE, CLARET, OCHRE
    elif key == "pantry":
        field, border, trim = [LINEN[0], LINEN[1], LINEN[2], LINEN[3], LINEN[4], LINEN[5]], CLARET, OCHRE
    else:
        info = DORMS[key]
        field, border, trim = info["rug"], CLARET if key != 2 else BOTTLE, info["accent"]
    c.rect(x, y, w, h, border[3])
    c.frame(x, y, w, h, border[1])
    c.frame(x + 2, y + 2, w - 4, h - 4, trim[3])
    c.rect(x + 4, y + 4, w - 8, h - 8, field[2])
    c.frame(x + 6, y + 6, w - 12, h - 12, field[4])
    for gx in range(x + 4, x + w - 4, 3):
        c.px(gx, y + 3, trim[4])
        c.px(gx, y + h - 4, trim[2])
    # Central medallion and corner rosettes
    cx, cy = x + w // 2, y + h // 2
    rx, ry = min(w // 4, 22), min(h // 4, 16)
    for dy in range(-ry, ry + 1):
        span = int(rx * (1 - abs(dy) / (ry + 1)))
        c.hline(cx - span, cy + dy, span * 2 + 1, field[3])
        c.px(cx - span, cy + dy, trim[3])
        c.px(cx + span, cy + dy, trim[3])
    c.rect(cx - 2, cy - 2, 5, 5, border[4])
    c.px(cx, cy, trim[4])
    for ex, ey in ((x + 10, y + 10), (x + w - 11, y + 10), (x + 10, y + h - 11), (x + w - 11, y + h - 11)):
        c.rect(ex - 1, ey - 1, 3, 3, trim[3])
        c.px(ex, ey, border[4])
    c.dither(x + 4, y + 4, w - 8, h - 8, field[3], 0.08)
    # Fringe on the short ends
    for gx in range(x + 1, x + w - 1, 2):
        c.px(gx, y - 1, LINEN[4])
        c.px(gx, y + h, LINEN[3])
    c.rect(x + 1, y + h + 1, w, 1, (26, 16, 30, 80))


def _shoes(c: Canvas, x: int, y: int) -> None:
    for sx in (x, x + 5):
        c.rect(sx, y, 4, 6, WALNUT[1])
        c.rect(sx, y, 4, 2, WALNUT[3])
        c.px(sx + 1, y, WALNUT[4])
        c.hline(sx, y + 6, 4, (26, 16, 30, 120))
