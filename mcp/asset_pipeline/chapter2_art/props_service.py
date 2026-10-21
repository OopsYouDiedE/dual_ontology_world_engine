"""Chapter 2 service-wing and yard sprites: kitchen, rations, water, greenhouse.

Native 1x pixel art in the same recipe as ``first_awakening_art.props``: key
light from the upper left, 3/4 top-down view, selective dark outline and a
hardened alpha. Each canvas's bottom-centre is its floor anchor; wall-mounted
pieces anchor at the bottom edge of their mount.

``FOOTPRINTS`` gives the solid floor rect (W x H px) centred on the anchor and
extending upward; ``None`` marks a non-blocking (wall-mounted or swinging) item.
"""
from __future__ import annotations

import math

from first_awakening_art.palette import (BRASS, CAP, CLARET, CREAM, FLAME, FOG, HONEY, INK, IRON, LEAF, LINEN,
                                         OAK, OCHRE, PLANK, SAGE, SLATE, STONE, TEAL, TILE, WALNUT)
from first_awakening_art.pixel import Canvas, mix, rng, shift
from first_awakening_art.props import _finish, slab

# ------------------------------------------------------------- materials ---

# Black island basalt: the stone ramp pushed toward cool slate.
BASALT = [shift(STONE[0], -0.35), STONE[0], mix(STONE[0], SLATE[1], 0.5), mix(STONE[1], SLATE[1], 0.4),
          mix(STONE[2], SLATE[2], 0.4), mix(STONE[3], SLATE[3], 0.35), STONE[4]]
# Unglazed red clay for pots: claret warmed with ochre.
TERRA = [mix(CLARET[i], OCHRE[i], 0.45) for i in range(6)]
# Garden soil and wet earth.
SOIL = [WALNUT[0], WALNUT[1], mix(WALNUT[1], PLANK[1], 0.5), PLANK[1], PLANK[2], mix(PLANK[2], OCHRE[2], 0.4)]
# Cistern water: fog glass tinted with teal.
WATER = [mix(TEAL[i], FOG[i], 0.45) for i in range(6)]
# Weathered bronze: dark brass with verdigris accents.
BRONZE = [BRASS[0], mix(BRASS[1], TEAL[2], 0.25), mix(BRASS[2], TEAL[3], 0.2), BRASS[3], BRASS[4]]
VERDIGRIS = mix(TEAL[4], SAGE[5], 0.4)
SOOT = [mix(LINEN[2], IRON[1], 0.35), mix(LINEN[1], IRON[1], 0.6), mix(IRON[1], CAP[1], 0.5)]
INK_LINE = (74, 66, 60, 255)


# -------------------------------------------------------------- helpers ---

def ring(c: Canvas, cx: float, cy: float, ro: float, ri: float, col, lit=None, dark=None) -> None:
    """Annulus lit on its upper-left arc and shaded on its lower-right arc."""
    for y in range(int(cy - ro) - 1, int(cy + ro) + 2):
        for x in range(int(cx - ro) - 1, int(cx + ro) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if ri <= d <= ro:
                k = (dx + dy) / max(d, 0.01)
                tone = lit if lit is not None and k < -0.75 else dark if dark is not None and k > 0.75 else col
                c.px(x, y, tone)


def pipe_v(c: Canvas, x: int, y: int, w: int, h: int, ramp=IRON) -> None:
    c.rect(x, y, w, h, ramp[3])
    c.vline(x, y, h, ramp[4])
    c.vline(x + 1, y, h, ramp[5])
    c.vline(x + w - 1, y, h, ramp[1])
    c.vline(x + w - 2, y, h, ramp[2])


def pipe_h(c: Canvas, x: int, y: int, w: int, h: int, ramp=IRON) -> None:
    c.rect(x, y, w, h, ramp[3])
    c.hline(x, y, w, ramp[4])
    c.hline(x, y + 1, w, ramp[5])
    c.hline(x, y + h - 1, w, ramp[1])
    c.hline(x, y + h - 2, w, ramp[2])


def flange_v(c: Canvas, x: int, y: int, w: int) -> None:
    """A bolted flange collar across a vertical pipe."""
    c.rect(x - 1, y, w + 2, 2, IRON[4])
    c.hline(x - 1, y, w + 2, IRON[5])
    c.px(x + w, y + 1, IRON[1])


def tin_can(c: Canvas, x: int, y: int, label) -> None:
    """6x7 ration tin standing upright, label band in the given ramp."""
    c.rect(x, y + 1, 6, 6, IRON[4])
    c.rect(x, y + 3, 6, 2, label[3])
    c.px(x, y + 3, label[4])
    c.vline(x, y + 1, 6, IRON[5])
    c.vline(x + 5, y + 1, 6, IRON[2])
    c.px(x + 5, y + 3, label[1])
    c.px(x + 5, y + 4, label[1])
    c.hline(x + 1, y, 4, IRON[5])
    c.hline(x, y + 6, 6, IRON[2])


def masonry(c: Canvas, x: int, y: int, w: int, h: int, ramp, course: int = 6, unit: int = 9, seed: int = 1) -> None:
    """Rough-coursed basalt blocks with vesicular speckle."""
    c.rect(x, y, w, h, ramp[3])
    for row, yy in enumerate(range(y, y + h, course)):
        off = unit // 2 if row % 2 else 0
        c.hline(x, yy, w, ramp[1])
        c.hline(x, yy + 1, w, ramp[4])
        for xx in range(x - off, x + w, unit):
            if xx > x:
                c.vline(xx, yy, min(course, y + h - yy), ramp[1])
                c.vline(xx + 1, yy + 1, min(course, y + h - yy) - 1, ramp[4])
    g = rng(seed)
    for _ in range(w * h // 14):
        c.px(x + int(g.integers(0, w)), y + int(g.integers(0, h)), ramp[2])


def speckle(c: Canvas, x: int, y: int, w: int, h: int, col, count: int, seed: int) -> None:
    g = rng(seed)
    for _ in range(count):
        px, py = x + int(g.integers(0, w)), y + int(g.integers(0, h))
        if c.a[py, px, 3] if c.inside(px, py) else False:
            c.px(px, py, col)


def vgrain(c: Canvas, x: int, y: int, w: int, h: int, col, seed: int) -> None:
    """Short vertical grain streaks for upright planks."""
    g = rng(seed)
    for _ in range(w * h // 24):
        gx, gy = x + int(g.integers(0, w)), y + int(g.integers(0, h - 3))
        c.vline(gx, gy, int(g.integers(2, 5)), col)


# ------------------------------------------------- kitchen / dining / rations

def kitchen_range() -> Canvas:
    """100x60 cast-iron range: hot plates, firebox, twin ovens, brass rail, stock pot."""
    c = Canvas(100, 60)
    # Splashback with a warming shelf and the flue collar.
    c.rect(44, 0, 12, 4, IRON[2])
    c.vline(44, 0, 4, IRON[4])
    c.vline(55, 0, 4, IRON[1])
    c.rect(4, 4, 92, 14, IRON[2])
    c.hline(4, 4, 92, IRON[3])
    for rx in range(8, 94, 10):
        c.px(rx, 9, IRON[4])
        c.px(rx, 14, IRON[1])
    c.rect(2, 2, 96, 3, IRON[3])
    c.hline(2, 2, 96, IRON[5])
    c.hline(2, 4, 96, IRON[1])
    for bx in (6, 93):
        c.rect(bx, 5, 2, 4, IRON[3])
    # Range body
    slab(c, 1, 16, 98, 12, 28, IRON, grain=False)
    c.vline(1, 16, 40, IRON[5])
    c.vline(98, 28, 28, IRON[1])
    # Three hot-plate rings on the top
    for hx in (16, 38, 60):
        c.ellipse(hx, 22, 8, 3.6, IRON[1])
        c.ellipse(hx, 22, 6.5, 2.7, IRON[3])
        c.ellipse(hx, 22, 4.5, 1.8, IRON[2])
        c.ellipse(hx, 22, 2, 0.9, IRON[1])
        c.hline(hx - 5, 20, 4, IRON[5])
    # A faint ember glow round the plate over the firebox
    c.px(44, 23, FLAME[0])
    c.px(32, 23, FLAME[0])
    # Stock pot on the right hot plate area
    c.ellipse(82, 24, 10, 3, IRON[0])
    c.rect(72, 10, 19, 14, IRON[4])
    c.vline(72, 10, 14, IRON[5])
    c.vline(73, 10, 14, IRON[5])
    c.vline(89, 10, 14, IRON[2])
    c.vline(90, 10, 14, IRON[1])
    c.hline(72, 14, 19, IRON[3])
    c.hline(72, 22, 19, IRON[2])
    c.ellipse(81.5, 23, 9.5, 2, IRON[2])
    c.ellipse(81.5, 10, 9.5, 3, IRON[5])
    c.ellipse(81.5, 10, 7.5, 2, IRON[3])
    c.rect(80, 7, 3, 2, IRON[1])
    c.px(80, 7, IRON[5])
    for hx in (70, 91):
        c.rect(hx, 12, 2, 2, IRON[1])
    # Brass rail along the front edge on three standoffs
    for sx in (4, 49, 95):
        c.rect(sx, 29, 2, 3, BRASS[2])
    c.hline(3, 31, 94, BRASS[4])
    c.hline(3, 32, 94, BRASS[2])
    c.px(4, 31, BRASS[5])
    c.px(20, 31, BRASS[5])
    # Twin oven doors flanking the firebox
    for ox in (5, 66):
        c.rect(ox, 35, 29, 17, IRON[2])
        c.hline(ox, 35, 29, IRON[4])
        c.vline(ox, 35, 17, IRON[4])
        c.hline(ox, 51, 29, IRON[0])
        c.vline(ox + 28, 35, 17, IRON[0])
        c.rect(ox + 3, 38, 23, 11, IRON[3])
        c.frame(ox + 3, 38, 23, 11, IRON[1])
        c.hline(ox + 4, 39, 21, IRON[4])
        c.rect(ox + 9, 42, 11, 2, BRASS[3])
        c.hline(ox + 9, 42, 11, BRASS[5])
        c.px(ox + 19, 43, BRASS[1])
        for hy in (37, 49):
            c.rect(ox + (1 if ox < 50 else 26), hy - 1, 2, 3, IRON[1])
    # A tea towel hung over the rail
    c.rect(26, 31, 7, 8, LINEN[4])
    c.hline(26, 31, 7, LINEN[5])
    c.vline(32, 32, 7, LINEN[2])
    c.hline(26, 36, 7, CLARET[3])
    # Firebox door with glowing draught slots, ash pan beneath
    c.rect(38, 35, 24, 11, IRON[2])
    c.frame(38, 35, 24, 11, IRON[1])
    c.hline(39, 36, 22, IRON[4])
    for sx in range(41, 59, 4):
        c.rect(sx, 38, 2, 5, FLAME[0])
        c.px(sx, 39, FLAME[1])
        c.px(sx + 1, 40, FLAME[2])
    c.rect(47, 44, 6, 1, BRASS[4])
    c.rect(38, 47, 24, 6, IRON[1])
    c.hline(38, 47, 24, IRON[3])
    c.rect(46, 49, 8, 2, IRON[4])
    c.hline(46, 49, 8, IRON[5])
    # Plinth and feet
    c.rect(2, 56, 96, 2, IRON[1])
    for fx in (3, 93):
        c.rect(fx, 57, 4, 3, IRON[2])
        c.px(fx, 57, IRON[4])
    return _finish(c)


def hood_chimney() -> Canvas:
    """100x40 whitewashed plaster hood over the range, sooted along its lip."""
    c = Canvas(100, 40)
    # Chimney breast
    c.rect(32, 0, 36, 16, LINEN[3])
    c.vline(32, 0, 16, LINEN[4])
    c.vline(33, 0, 16, LINEN[4])
    c.vline(67, 0, 16, LINEN[1])
    c.vline(66, 0, 16, LINEN[2])
    # Flaring hood with a lit left facet and shaded right facet
    c.poly([(32, 14), (68, 14), (98, 32), (2, 32)], LINEN[3])
    c.poly([(32, 14), (40, 14), (14, 32), (2, 32)], LINEN[4])
    c.poly([(60, 14), (68, 14), (98, 32), (86, 32)], LINEN[2])
    c.line(32, 14, 2, 32, LINEN[5])
    c.line(68, 14, 98, 32, LINEN[1])
    # Moulded lip and its dark underside
    c.rect(1, 32, 98, 3, LINEN[4])
    c.hline(1, 32, 98, LINEN[5])
    c.rect(1, 35, 98, 3, LINEN[2])
    c.hline(1, 37, 98, LINEN[1])
    c.rect(2, 38, 96, 2, CAP[2])
    c.px(1, 32, LINEN[5])
    # Soot: an ordered-dither gradient, densest at the lip and the centre,
    # with a narrow plume climbing the breast.
    bayer = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
    for y in range(0, 38):
        for x in range(1, 99):
            if not c.a[y, x, 3]:
                continue
            centre = 1.0 - min(abs(x - 50) / 50.0, 1.0)
            depth = max(0.0, (y - 14) / 22.0)
            p = min(1.0, depth * (0.7 + 0.5 * centre))
            if y < 16:
                p = max(p, (1.0 - abs(x - 50) / 8.0) * 0.35 * (y / 16))
            level = int(p * 4.2 + (bayer[y % 4][x % 4] - 7.5) / 16 * 0.9)
            if level > 0:
                c.px(x, y, mix(c.get(x, y), SOOT[2], (0.0, 0.22, 0.4, 0.58, 0.74)[min(level, 4)]))
    # Hand-wiped streaks where the cook reaches up
    c.hline(10, 35, 14, SOOT[2])
    c.hline(70, 36, 18, SOOT[2])
    return _finish(c)


def ration_counter() -> Canvas:
    """104x48 oak serving counter: balance scale, account book, tins and loaves."""
    c = Canvas(104, 48)
    counter = [WALNUT[1], HONEY[1], HONEY[2], HONEY[3], OAK[4], OAK[5]]
    slab(c, 1, 16, 102, 13, 16, counter, seed=71)
    c.hline(1, 28, 102, HONEY[1])
    # Panelled front
    for px in range(4, 100, 25):
        c.rect(px, 31, 21, 11, HONEY[2])
        c.hline(px, 31, 21, HONEY[1])
        c.vline(px, 31, 11, HONEY[1])
        c.hline(px + 1, 41, 20, HONEY[4])
        c.vline(px + 20, 32, 10, HONEY[4])
        c.rect(px + 3, 33, 15, 7, HONEY[3])
    c.vline(1, 29, 16, HONEY[4])
    c.rect(2, 45, 100, 3, WALNUT[1])
    c.hline(2, 45, 100, WALNUT[3])
    # Brass balance: pillar, beam, two hanging pans, one weight
    c.ellipse(16, 25, 7, 2, OAK[2])
    c.rect(10, 21, 13, 4, BRASS[2])
    c.hline(10, 21, 13, BRASS[4])
    c.hline(10, 24, 13, BRASS[1])
    c.vline(15, 5, 16, BRASS[4])
    c.vline(16, 5, 16, BRASS[2])
    c.vline(17, 5, 16, BRASS[1])
    c.rect(14, 3, 4, 2, BRASS[3])
    c.px(15, 2, BRASS[5])
    c.hline(5, 6, 23, BRASS[4])
    c.hline(5, 7, 23, BRASS[1])
    for ex in (5, 27):
        c.line(ex, 8, ex - 3, 15, BRASS[2])
        c.line(ex, 8, ex + 3, 15, BRASS[2])
        c.ellipse(ex + 0.5, 16, 5, 1.8, BRASS[3])
        c.hline(ex - 3, 15, 7, BRASS[5])
        c.hline(ex - 3, 17, 7, BRASS[1])
    c.rect(25, 12, 4, 3, BRASS[4])
    c.hline(25, 12, 4, BRASS[5])
    c.px(28, 14, BRASS[1])
    # Open account book with ruled columns, inkwell and pen
    c.rect(32, 17, 24, 11, CLARET[2])
    c.hline(32, 27, 24, CLARET[1])
    c.rect(33, 17, 11, 9, CREAM[5])
    c.rect(44, 17, 11, 9, CREAM[4])
    c.vline(44, 17, 9, CREAM[1])
    c.vline(43, 17, 9, CREAM[3])
    for ly in range(19, 25, 2):
        c.hline(34, ly, 7 - (ly % 3), INK_LINE)
        c.hline(46, ly, 6 + (ly % 2), INK_LINE)
    c.vline(40, 17, 9, CLARET[4])
    c.vline(52, 17, 9, CLARET[4])
    c.rect(58, 21, 4, 4, INK)
    c.hline(58, 21, 4, IRON[3])
    c.px(58, 22, IRON[4])
    c.line(60, 20, 63, 15, OCHRE[4])
    c.px(60, 20, IRON[1])
    # Stacked ration tins, three-two-one
    labels = (CLARET, OCHRE, TEAL)
    for i, tx in enumerate((64, 70, 76)):
        tin_can(c, tx, 20, labels[i % 3])
    for i, tx in enumerate((67, 73)):
        tin_can(c, tx, 13, labels[(i + 1) % 3])
    tin_can(c, 70, 6, CLARET)
    # Loaves: a scored crust behind, one wrapped in cloth and baker's twine
    c.ellipse(95, 18, 6, 3.2, OAK[2])
    c.ellipse(94, 16.5, 6, 3, OCHRE[2])
    c.ellipse(93, 15.5, 4.5, 1.8, OCHRE[4])
    for sx in (91, 94, 97):
        c.line(sx, 15, sx + 1, 17, OCHRE[1])
    c.ellipse(90, 24, 8, 3, OAK[2])
    c.ellipse(89, 22, 8, 3.8, LINEN[2])
    c.ellipse(88, 21, 6.5, 2.6, LINEN[3])
    c.hline(84, 20, 5, LINEN[5])
    c.hline(83, 25, 12, LINEN[0])
    c.vline(89, 18, 8, CLARET[3])
    c.hline(81, 22, 16, CLARET[3])
    c.px(89, 22, CLARET[5])
    c.px(90, 21, CLARET[4])
    c.px(91, 20, CLARET[3])
    return _finish(c)


def ledger_stand() -> Canvas:
    """24x40 slanted lectern carrying the stores ledger, pen on a string."""
    c = Canvas(24, 40)
    # Splayed feet and turned post
    c.rect(3, 36, 18, 3, WALNUT[2])
    c.hline(3, 36, 18, WALNUT[4])
    c.hline(3, 38, 18, WALNUT[1])
    c.rect(10, 19, 4, 18, WALNUT[3])
    c.vline(10, 19, 18, WALNUT[5])
    c.vline(13, 19, 18, WALNUT[1])
    c.rect(9, 27, 6, 2, WALNUT[4])
    c.hline(9, 28, 6, WALNUT[2])
    # Slanted desk board with a ledge at its foot
    c.poly([(3, 4), (21, 4), (22, 17), (2, 17)], WALNUT[3])
    c.line(3, 4, 2, 16, WALNUT[5])
    c.rect(1, 16, 22, 3, WALNUT[4])
    c.hline(1, 16, 22, WALNUT[5])
    c.hline(1, 18, 22, WALNUT[1])
    # The ledger, open: claret boards, ruled pages
    c.poly([(3, 3), (21, 3), (22, 16), (2, 16)], CLARET[2])
    c.poly([(4, 3), (12, 3), (12, 15), (3, 15)], CREAM[5])
    c.poly([(12, 3), (20, 3), (21, 15), (12, 15)], CREAM[4])
    c.vline(12, 3, 12, CREAM[1])
    for ly in range(5, 14, 2):
        c.hline(5, ly, 6 - (ly % 3), INK_LINE)
        c.hline(13, ly, 6 - ((ly + 1) % 3), INK_LINE)
    c.vline(9, 3, 12, CLARET[4])
    c.vline(17, 3, 12, CLARET[4])
    c.hline(3, 15, 19, CLARET[1])
    # Ribbon marker
    c.vline(15, 15, 4, CLARET[4])
    # Pen on a string hanging from the board's right corner
    c.vline(21, 19, 6, LINEN[2])
    c.line(20, 25, 22, 31, OCHRE[3])
    c.line(19, 25, 21, 31, OCHRE[1])
    c.px(22, 32, IRON[4])
    return _finish(c)


def dish_rack() -> Canvas:
    """56x52 standing plate rack: enamel plates, hooked tin mugs, base shelf."""
    c = Canvas(56, 52)
    c.rect(5, 3, 46, 41, WALNUT[1])
    c.rect(1, 0, 54, 4, WALNUT[4])
    c.hline(1, 0, 54, WALNUT[5])
    c.hline(1, 3, 54, WALNUT[2])
    for row, (cy, rx, ry) in enumerate(((10, 4.4, 5.0), (25, 4.2, 4.8))):
        for i, px in enumerate(range(10 + row * 2, 48, 7)):
            dark = (i + row) % 4 == 2
            rim = FOG[1] if not dark else FOG[0]
            face = TILE[5] if not dark else FOG[2]
            c.ellipse(px, cy, rx, ry, shift(rim, -0.3))
            c.ellipse(px - 0.3, cy - 0.3, rx - 0.6, ry - 0.6, rim)
            c.ellipse(px, cy, rx - 1.4, ry - 1.4, face)
            c.ellipse(px + 0.4, cy + 0.4, rx - 2.6, ry - 2.6, TILE[3] if not dark else FOG[1])
            c.px(px - 2, cy - 2, (255, 255, 255, 255) if not dark else FOG[4])
        # Plate rail in front of the row, then the shelf it stands on
        c.rect(5, cy + 4, 46, 2, WALNUT[4])
        c.hline(5, cy + 4, 46, WALNUT[5])
        c.rect(3, cy + 6, 50, 2, WALNUT[3])
        c.hline(3, cy + 6, 50, WALNUT[5])
        c.hline(3, cy + 8, 50, WALNUT[0])
    # Hooks and tin mugs under the second shelf
    for i, mx in enumerate(range(9, 48, 8)):
        c.vline(mx + 2, 35, 2, IRON[4])
        enamel = i == 2
        body = [TILE[3], TILE[4], TILE[5]] if enamel else [IRON[2], IRON[4], IRON[5]]
        c.rect(mx, 37, 5, 5, body[1])
        c.vline(mx, 37, 5, body[2])
        c.vline(mx + 4, 37, 5, body[0])
        c.hline(mx, 37, 5, FOG[1] if enamel else IRON[5])
        c.hline(mx, 41, 5, body[0])
        c.vline(mx + 5, 38, 3, body[0])
        c.px(mx + 6, 39, body[0])
    # Side uprights
    c.rect(1, 2, 4, 44, WALNUT[3])
    c.vline(1, 2, 44, WALNUT[5])
    c.rect(51, 2, 4, 44, WALNUT[2])
    c.vline(54, 2, 44, WALNUT[1])
    # Base shelf on short legs
    slab(c, 1, 43, 54, 3, 5, WALNUT, grain=False)
    for lx in (2, 51):
        c.rect(lx, 49, 3, 3, WALNUT[2])
    # A folded drying cloth on the base shelf
    c.rect(36, 41, 10, 3, LINEN[4])
    c.hline(36, 41, 10, LINEN[5])
    c.hline(36, 43, 10, LINEN[2])
    return _finish(c)


def granary_bin() -> Canvas:
    """52x48 iron-bound grain bin with a hinged lid and a padlocked hasp."""
    c = Canvas(52, 48)
    slab(c, 2, 6, 48, 13, 25, PLANK, seed=81)
    # Lid boards and the lid's overhanging front edge
    for ly in (9, 13):
        c.hline(3, ly, 46, PLANK[3])
    c.rect(1, 17, 50, 3, PLANK[4])
    c.hline(1, 17, 50, PLANK[5])
    c.hline(1, 19, 50, PLANK[1])
    # Body boards and corner posts
    for by in (26, 33, 40):
        c.hline(2, by, 48, PLANK[2])
        c.hline(2, by + 1, 48, PLANK[4])
    c.rect(2, 20, 4, 25, PLANK[4])
    c.vline(2, 20, 25, PLANK[5])
    c.rect(46, 20, 4, 25, PLANK[2])
    c.vline(49, 20, 25, PLANK[1])
    # Strap hinges running over the lid from the back
    for hx in (10, 38):
        c.rect(hx, 5, 4, 13, IRON[2])
        c.vline(hx, 5, 13, IRON[4])
        c.px(hx + 1, 8, IRON[5])
        c.px(hx + 1, 13, IRON[5])
        c.rect(hx - 1, 4, 6, 2, IRON[1])
    # Iron corner brackets with rivets
    for bx, lit in ((2, True), (44, False)):
        c.rect(bx, 21, 6, 2, IRON[3] if lit else IRON[2])
        c.rect(bx + (0 if lit else 4), 21, 2, 8, IRON[3] if lit else IRON[2])
        c.rect(bx, 41, 6, 2, IRON[3] if lit else IRON[2])
        c.rect(bx + (0 if lit else 4), 36, 2, 7, IRON[3] if lit else IRON[2])
        c.px(bx + (1 if lit else 4), 22, IRON[5])
        c.px(bx + (1 if lit else 4), 41, IRON[5])
    # Hasp: hinged iron tongue from the lid over a staple, padlock through it
    c.rect(22, 16, 8, 3, IRON[1])
    c.rect(23, 17, 6, 12, IRON[3])
    c.vline(23, 17, 12, IRON[5])
    c.vline(28, 17, 12, IRON[1])
    c.hline(23, 28, 6, IRON[1])
    c.px(24, 18, IRON[5])
    c.rect(25, 24, 2, 3, INK)                                     # slot over the staple
    c.rect(24, 24, 1, 3, IRON[1])
    # Staple loop standing proud of the hasp
    c.rect(25, 23, 2, 1, IRON[5])
    c.px(25, 25, IRON[4])
    # Padlock hanging from the staple
    ring(c, 26, 29.5, 2.6, 1.4, IRON[4], IRON[5], IRON[1])
    c.rect(23, 30, 7, 6, BRASS[3])
    c.hline(23, 30, 7, BRASS[5])
    c.vline(23, 30, 6, BRASS[4])
    c.vline(29, 30, 6, BRASS[1])
    c.hline(23, 35, 7, BRASS[1])
    c.px(26, 32, INK)
    c.px(26, 33, INK)
    # Feet and a few spilled grains on the lid seam
    for fx in (2, 45):
        c.rect(fx, 45, 5, 3, PLANK[1])
        c.hline(fx, 45, 5, PLANK[2])
    for gx, gy in ((16, 16), (19, 15), (33, 16), (35, 15), (31, 16)):
        c.px(gx, gy, OCHRE[5])
    return _finish(c)


def granary_door() -> Canvas:
    """48x60 wall-mounted ledged plank door barred with a heavy iron latch."""
    c = Canvas(48, 60)
    # Timber frame and lintel
    c.rect(0, 0, 48, 60, WALNUT[2])
    c.vline(0, 0, 60, WALNUT[4])
    c.vline(47, 0, 60, WALNUT[0])
    c.rect(0, 0, 48, 5, WALNUT[3])
    c.hline(0, 0, 48, WALNUT[5])
    c.hline(0, 4, 48, WALNUT[0])
    # Vertical planks
    c.rect(4, 5, 40, 54, PLANK[3])
    for px in range(4, 44, 8):
        c.vline(px, 5, 54, PLANK[1])
        c.vline(px + 1, 5, 54, PLANK[4])
    vgrain(c, 4, 5, 40, 54, PLANK[2], 151)
    c.vline(5, 5, 54, PLANK[5])
    c.hline(4, 5, 40, CAP[1])
    # Ledges and diagonal brace
    for ly in (11, 47):
        c.rect(4, ly, 40, 4, PLANK[4])
        c.hline(4, ly, 40, PLANK[5])
        c.hline(4, ly + 3, 40, PLANK[1])
    for t in range(4):
        c.line(7, 46 - t, 40, 15 - t, (PLANK[1], PLANK[4], PLANK[4], PLANK[5])[t])
    # Strap hinges on the ledges, pintles in the frame
    for hy in (12, 48):
        c.rect(2, hy, 22, 2, IRON[2])
        c.hline(2, hy, 22, IRON[4])
        c.poly([(23, hy - 1), (27, hy + 1), (23, hy + 3)], IRON[2])
        for rx in (8, 15, 21):
            c.px(rx, hy, IRON[5])
        c.rect(1, hy - 1, 3, 4, IRON[1])
    # Heavy bar latch: pivot boss, bar resting in a keeper on the frame
    c.ellipse(9, 30.5, 3, 3, IRON[1])
    c.ellipse(9, 30, 2, 2, IRON[3])
    c.rect(8, 29, 39, 4, IRON[3])
    c.hline(8, 29, 39, IRON[5])
    c.hline(8, 30, 39, IRON[4])
    c.hline(8, 32, 39, IRON[1])
    c.px(9, 29, IRON[5])
    c.px(9, 30, INK)
    # Lifting grip at the bar's end
    c.rect(41, 33, 3, 4, IRON[2])
    c.px(41, 33, IRON[4])
    # Keeper hook bolted to the frame, over the bar
    c.rect(44, 27, 4, 8, IRON[2])
    c.vline(44, 27, 8, IRON[4])
    c.hline(44, 27, 4, IRON[5])
    c.hline(44, 34, 4, IRON[0])
    c.rect(36, 27, 3, 8, IRON[2])
    c.vline(36, 27, 8, IRON[4])
    # Threshold
    c.rect(0, 58, 48, 2, STONE[2])
    c.hline(0, 58, 48, STONE[4])
    return _finish(c)


def hand_pump() -> Canvas:
    """24x44 cast-iron cistern pump on a basalt block, spout and bucket hook."""
    c = Canvas(24, 44)
    slab(c, 3, 33, 18, 5, 6, BASALT, grain=False)
    c.rect(4, 42, 16, 2, BASALT[1])
    speckle(c, 4, 38, 16, 4, BASALT[2], 14, 152)
    # Barrel of the pump
    c.rect(8, 11, 8, 23, IRON[2])
    c.vline(8, 11, 23, IRON[4])
    c.vline(9, 11, 23, IRON[3])
    c.vline(15, 11, 23, IRON[1])
    for by, bh in ((30, 4), (19, 2)):
        c.rect(7, by, 10, bh, IRON[3])
        c.hline(7, by, 10, IRON[5])
        c.hline(7, by + bh - 1, 10, IRON[1])
    # Domed cap and finial
    c.ellipse(12, 11, 5, 2.6, IRON[3])
    c.hline(9, 9, 5, IRON[5])
    c.ellipse(12, 7.5, 1.6, 1.6, IRON[4])
    # Lever handle angled up and back from its pivot
    c.rect(6, 10, 3, 3, IRON[2])
    c.line(7, 10, 1, 2, IRON[3])
    c.line(8, 10, 2, 2, IRON[2])
    c.line(7, 11, 3, 5, IRON[4])
    c.rect(1, 1, 2, 3, IRON[4])
    c.px(1, 1, IRON[5])
    # Spout with a drip of water
    c.rect(16, 20, 5, 3, IRON[3])
    c.hline(16, 20, 5, IRON[5])
    c.rect(19, 23, 3, 3, IRON[2])
    c.px(19, 23, IRON[4])
    c.px(20, 26, IRON[0])
    c.px(20, 27, WATER[4])
    c.px(20, 29, WATER[3])
    # Bucket hook on the barrel's front
    c.rect(11, 24, 2, 2, IRON[1])
    c.vline(12, 26, 3, IRON[4])
    c.px(11, 29, IRON[4])
    c.px(10, 28, IRON[4])
    return _finish(c)


# ------------------------------------------------- outdoor / water / greenhouse

def spring_trough() -> Canvas:
    """64x34 basalt spring basin fed from a bronze spout in a low wall."""
    c = Canvas(64, 34)
    # Low back wall with a whitewashed coping
    masonry(c, 18, 2, 28, 14, BASALT, course=5, unit=8, seed=101)
    c.rect(16, 0, 32, 3, LINEN[3])
    c.hline(16, 0, 32, LINEN[5])
    c.hline(16, 2, 32, LINEN[1])
    c.vline(18, 3, 13, BASALT[5])
    c.vline(45, 3, 13, BASALT[1])
    # Bronze mask boss with a short downturned spout, verdigris in the hollows
    c.ellipse(32, 6.5, 4, 3.6, BRONZE[0])
    c.ellipse(31.6, 6.1, 3.2, 2.8, BRONZE[2])
    c.hline(30, 4, 3, BRONZE[4])
    c.px(30, 7, BRONZE[0])
    c.px(33, 7, BRONZE[0])
    c.px(34, 9, VERDIGRIS)
    c.px(29, 8, VERDIGRIS)
    c.rect(31, 9, 3, 3, BRONZE[1])
    c.vline(31, 9, 3, BRONZE[3])
    c.hline(31, 12, 3, BRONZE[0])
    c.px(33, 11, VERDIGRIS)
    # Basin
    slab(c, 2, 14, 60, 12, 9, BASALT, grain=False)
    speckle(c, 2, 26, 60, 7, BASALT[2], 70, 153)
    speckle(c, 2, 26, 60, 7, BASALT[4], 30, 154)
    for jx in (17, 34, 50):
        c.vline(jx, 26, 8, BASALT[1])
        c.vline(jx + 1, 26, 8, BASALT[4])
    # Water surface inset in the rim
    c.rect(6, 16, 52, 8, WATER[3])
    c.hline(6, 16, 52, WATER[1])
    c.vline(6, 16, 8, WATER[2])
    c.hline(6, 23, 52, WATER[4])
    c.dither(7, 17, 50, 6, WATER[4], 0.12, 1)
    for wx, wy, ww in ((10, 19, 6), (44, 21, 7), (22, 22, 4), (50, 18, 4)):
        c.hline(wx, wy, ww, WATER[5])
    # Falling stream and its ripple
    c.vline(32, 13, 6, (232, 240, 240, 255))
    c.vline(33, 13, 6, WATER[5])
    ring(c, 32.5, 19.5, 3.6, 2.7, WATER[5])
    c.px(32, 19, (255, 255, 255, 255))
    # Moss on the shaded rim corner
    for mx, my in ((57, 15), (58, 16), (56, 24), (3, 25)):
        c.px(mx, my, SAGE[3])
    return _finish(c)


def _bucket(c: Canvas, x: int, top: int = 10) -> None:
    bot = 21
    c.poly([(x, top), (x + 12, top), (x + 11, bot), (x + 1, bot)], PLANK[3])
    for sx in range(x + 3, x + 11, 3):
        c.vline(sx, top + 1, bot - top - 1, PLANK[2])
    c.vline(x + 1, top + 1, bot - top - 1, PLANK[5])
    c.vline(x + 11, top + 1, bot - top - 1, PLANK[1])
    for hy in (top + 3, bot - 3):
        c.hline(x, hy, 12, IRON[2])
        c.px(x + 1, hy, IRON[4])
    c.hline(x + 1, bot, 10, PLANK[1])
    c.ellipse(x + 6, top, 6, 2.2, PLANK[4])
    c.ellipse(x + 6, top + 0.3, 4.6, 1.3, WATER[3])
    c.hline(x + 3, top, 3, WATER[5])
    # Iron bail arched over the mouth
    c.px(x, top + 1, IRON[3])
    c.px(x + 12, top + 1, IRON[1])
    c.line(x, top, x + 3, top - 4, IRON[3])
    c.line(x + 12, top, x + 9, top - 4, IRON[2])
    c.hline(x + 4, top - 5, 5, IRON[3])


def bucket_pair() -> Canvas:
    """30x22 two water buckets with a shoulder yoke laid across their bails."""
    c = Canvas(30, 22)
    _bucket(c, 1)
    _bucket(c, 16)
    # Yoke: ends turned up, a neck notch at the centre, resting on both bails
    for x in range(1, 29):
        t = (x - 14.5) / 13.5
        y = 5 - int(round(t * t * 3))
        if 12 <= x <= 17:
            y += 1
        c.px(x, y, WALNUT[5])
        c.px(x, y + 1, WALNUT[4])
        c.px(x, y + 2, WALNUT[2])
    c.vline(1, 2, 3, WALNUT[5])
    c.vline(28, 2, 3, WALNUT[2])
    # Hook chains from the yoke ends to the bails
    for hx in (6, 22):
        c.px(hx, 7 - (0 if hx < 14 else 0), IRON[4])
    return _finish(c)


def _sheet(c: Canvas, x0: int, x1: int, line_y, length: int, ramp, sway: int, seed: int, stripe=None) -> None:
    g = rng(seed)
    pts_top = [(x, line_y(x)) for x in range(x0, x1 + 1)]
    bottom = [(x + sway, line_y(x) + length + int(round(math.sin((x - x0) * 0.7) * 1.2))) for x in range(x1, x0 - 1, -1)]
    c.poly(pts_top + bottom, ramp[4])
    w = x1 - x0
    # Vertical folds, lit edge on the left, shadow on the right
    for fx in range(x0 + 3, x1 - 1, 4 + int(g.integers(0, 2))):
        for y in range(line_y(fx) + 2, line_y(fx) + length - 1):
            dx = int(round(sway * (y - line_y(fx)) / length))
            c.px(fx + dx, y, ramp[2])
            c.px(fx + dx - 1, y, ramp[5])
    for y in range(line_y(x0) + 1, line_y(x0) + length):
        c.px(x0 + int(round(sway * (y - line_y(x0)) / length)), y, ramp[5])
    for y in range(line_y(x1) + 1, line_y(x1) + length):
        c.px(x1 + int(round(sway * (y - line_y(x1)) / length)), y, ramp[1])
    # Hem shadow
    for x in range(x0, x1 + 1):
        by = line_y(x) + length + int(round(math.sin((x - x0) * 0.7) * 1.2))
        c.px(x + sway, by - 1, ramp[2])
        if stripe is not None:
            c.px(x + int(round(sway * 0.7)), by - 4, stripe)
    # Wooden pegs
    for px in (x0 + 1, x0 + w // 2, x1 - 1):
        c.vline(px, line_y(px) - 1, 3, WALNUT[4])


def drying_lines() -> Canvas:
    """100x52 two posts, a sagging line and linen hung out in the sea wind."""
    c = Canvas(100, 52)

    def line_y(x: int) -> int:
        t = (x - 50) / 46
        return 7 + int(round((1 - t * t) * 5))

    # Posts with stone footings
    for px, lit in ((2, True), (94, False)):
        c.rect(px, 3, 4, 46, PLANK[3])
        c.vline(px, 3, 46, PLANK[5] if lit else PLANK[4])
        c.vline(px + 3, 3, 46, PLANK[1])
        c.rect(px, 2, 4, 2, PLANK[4])
        c.hline(px, 2, 4, PLANK[5])
        c.rect(px - 1, 47, 6, 4, BASALT[3])
        c.hline(px - 1, 47, 6, BASALT[5])
        c.hline(px - 1, 50, 6, BASALT[1])
        c.rect(px + (3 if lit else -1), 6, 2, 2, IRON[3])
    # Line
    prev = None
    for x in range(6, 95):
        y = line_y(x)
        c.px(x, y, LINEN[1])
        if prev is not None and prev != y:
            c.px(x, min(prev, y), LINEN[1])
        prev = y
    # Linen: a bed sheet, a pillow slip, and a striped towel
    sheet = [LINEN[0], LINEN[1], LINEN[2], LINEN[3], LINEN[4], LINEN[5]]
    cream = [CREAM[0], CREAM[1], CREAM[2], CREAM[3], CREAM[4], CREAM[5]]
    _sheet(c, 11, 40, line_y, 30, sheet, 2, 111)
    _sheet(c, 45, 62, line_y, 16, cream, 1, 112)
    _sheet(c, 68, 86, line_y, 22, sheet, 2, 113, stripe=FOG[2])
    return _finish(c)


def valve_station() -> Canvas:
    """44x52 iron pipe manifold: big red wheel valve, gauge, small brass valve."""
    c = Canvas(44, 52)
    slab(c, 2, 40, 40, 5, 7, BASALT, grain=False)
    speckle(c, 3, 46, 38, 4, BASALT[2], 36, 155)
    # Risers and manifold
    pipe_v(c, 8, 16, 6, 27)
    pipe_v(c, 30, 16, 6, 27)
    pipe_h(c, 3, 13, 38, 6)
    for fx in (3, 37):
        c.rect(fx - 1, 12, 3, 8, IRON[4])
        c.vline(fx - 1, 12, 8, IRON[5])
        c.vline(fx + 1, 12, 8, IRON[1])
    for x in (8, 30):
        flange_v(c, x, 19, 6)
        flange_v(c, x, 39, 6)
        c.px(x, 40, IRON[5])
    # Gauge on a short brass stem above the manifold
    c.vline(23, 9, 4, BRASS[2])
    c.px(22, 11, BRASS[4])
    ring(c, 23, 6, 5.5, 4.3, BRASS[3], BRASS[5], BRASS[1])
    c.ellipse(23, 6, 4.3, 4.3, CREAM[5])
    for a in range(-210, 31, 40):
        r = math.radians(a)
        c.px(int(23 + math.cos(r) * 3.2), int(6 + math.sin(r) * 3.2), INK_LINE)
    for a in (-20, -5, 10):
        r = math.radians(a)
        c.px(int(23 + math.cos(r) * 3.2), int(6 + math.sin(r) * 3.2), CLARET[4])
        c.px(int(23 + math.cos(r) * 2.4), int(6 + math.sin(r) * 2.4), CLARET[4])
    c.line(23, 6, 20, 4, INK)
    c.px(23, 6, BRASS[2])
    # Big red wheel valve on the left riser
    c.rect(9, 25, 4, 6, IRON[2])
    ring(c, 11, 28, 8.2, 6.4, CLARET[3], CLARET[5], CLARET[1])
    for a in range(0, 360, 60):
        r = math.radians(a + 15)
        c.line(11, 28, int(round(11 + math.cos(r) * 6.4)), int(round(28 + math.sin(r) * 6.4)), CLARET[2])
    c.ellipse(11, 28, 2, 2, IRON[3])
    c.px(10, 27, IRON[5])
    for a in (200, 230, 250):
        r = math.radians(a)
        c.px(int(round(11 + math.cos(r) * 7.3)), int(round(28 + math.sin(r) * 7.3)), CLARET[5])
    # Small brass gate valve on the right riser
    c.rect(29, 27, 8, 6, IRON[2])
    c.hline(29, 27, 8, IRON[4])
    c.vline(32, 23, 4, IRON[4])
    ring(c, 33, 22, 3.6, 2.2, BRASS[3], BRASS[5], BRASS[1])
    c.px(33, 22, BRASS[2])
    # Drip stain on the plinth
    c.px(33, 41, WATER[2])
    c.px(34, 42, WATER[1])
    return _finish(c)


def planter_bed() -> Canvas:
    """96x34 raised timber bed of dark soil with three rows of seedlings."""
    c = Canvas(96, 34)
    # Rim boards
    c.rect(1, 2, 94, 20, PLANK[4])
    c.hline(1, 2, 94, PLANK[5])
    c.vline(1, 2, 20, PLANK[5])
    # Soil, shaded under the far rim
    c.rect(4, 4, 88, 16, SOIL[3])
    c.hline(4, 4, 88, SOIL[0])
    c.hline(4, 5, 88, SOIL[1])
    c.vline(4, 4, 16, SOIL[1])
    speckle(c, 5, 6, 86, 14, SOIL[4], 160, 121)
    speckle(c, 5, 6, 86, 14, SOIL[2], 120, 122)
    # Rows: furrow, ridge, seedlings
    g = rng(123)
    for row, ry in enumerate((9, 13, 17)):
        c.hline(5, ry + 1, 86, SOIL[2])
        c.hline(5, ry, 86, SOIL[4])
        for i, sx in enumerate(range(8 + row * 2, 90, 6)):
            big = g.random() < 0.55
            c.px(sx, ry, LEAF[2])
            c.px(sx - 1, ry - 1, LEAF[4])
            c.px(sx + 1, ry - 1, LEAF[5])
            if big:
                c.px(sx, ry - 1, LEAF[3])
                c.px(sx, ry - 2, LEAF[4])
                c.px(sx - 1, ry - 2, LEAF[5])
                c.px(sx + 1, ry - 2, LEAF[3])
    # A marker stake with a blank cream tag at one row end
    c.vline(88, 4, 7, WALNUT[3])
    c.rect(86, 3, 5, 3, CREAM[5])
    c.hline(86, 5, 5, CREAM[2])
    # Front boards and corner posts
    c.rect(1, 22, 94, 10, PLANK[3])
    c.hline(1, 22, 94, PLANK[2])
    c.hline(1, 26, 94, PLANK[1])
    c.hline(1, 27, 94, PLANK[4])
    c.hline(1, 31, 94, PLANK[1])
    c.rect(1, 20, 4, 14, PLANK[4])
    c.vline(1, 20, 14, PLANK[5])
    c.rect(91, 20, 4, 14, PLANK[2])
    c.vline(94, 20, 14, PLANK[1])
    c.rect(46, 22, 4, 10, PLANK[2])
    c.vline(46, 22, 10, PLANK[4])
    for px in (2, 47, 92):
        c.px(px, 24, IRON[4])
        c.px(px, 29, IRON[4])
    return _finish(c)


def _pot(c: Canvas, cx: int, by: int, w: int, h: int, seed: int) -> None:
    g = rng(seed)
    x0 = cx - w // 2
    c.poly([(x0, by - h + 1), (x0 + w, by - h + 1), (x0 + w - 1, by), (x0 + 1, by)], TERRA[3])
    c.vline(x0 + 1, by - h + 2, h - 2, TERRA[4])
    c.vline(x0 + w - 2, by - h + 2, h - 2, TERRA[1])
    c.hline(x0 + 1, by - 1, w - 2, TERRA[1])
    c.rect(x0 - 1, by - h, w + 2, 2, TERRA[4])
    c.hline(x0 - 1, by - h, w + 2, TERRA[5])
    c.px(x0 + w, by - h + 1, TERRA[2])
    c.hline(x0, by - h, w, SOIL[1])
    # Sprig
    top = by - h - 1
    hgt = 2 + int(g.integers(0, 3))
    c.vline(cx, top - hgt + 1, hgt, LEAF[2])
    c.px(cx - 1, top - hgt + 1, LEAF[4])
    c.px(cx + 1, top - hgt + 1, LEAF[5])
    c.px(cx - 1, top - hgt + 3, LEAF[3])
    c.px(cx + 1, top - hgt + 2, LEAF[4])
    c.px(cx, top - hgt, LEAF[5])


def seedling_pots() -> Canvas:
    """34x20 a huddle of clay pots, each with a seedling."""
    c = Canvas(34, 20)
    _pot(c, 11, 12, 8, 6, 131)
    _pot(c, 23, 11, 8, 6, 132)
    _pot(c, 6, 18, 8, 7, 133)
    _pot(c, 17, 19, 8, 7, 134)
    _pot(c, 28, 18, 7, 6, 135)
    return _finish(c)


def garden_tools() -> Canvas:
    """30x46 wall rack holding a spade and rake; a zinc watering can at its foot."""
    c = Canvas(30, 46)
    # Rack frame
    for px, lit in ((3, True), (24, False)):
        c.rect(px, 5, 3, 39, PLANK[3])
        c.vline(px, 5, 39, PLANK[5] if lit else PLANK[4])
        c.vline(px + 2, 5, 39, PLANK[1])
    for ry in (7, 28):
        c.rect(2, ry, 26, 3, PLANK[4])
        c.hline(2, ry, 26, PLANK[5])
        c.hline(2, ry + 2, 26, PLANK[1])
    c.rect(1, 43, 28, 3, PLANK[2])
    c.hline(1, 43, 28, PLANK[4])
    # Spade, blade down: D-grip, shaft, iron blade
    c.rect(8, 1, 5, 3, WALNUT[3])
    c.a[2, 9:12] = 0
    c.hline(8, 1, 5, WALNUT[5])
    c.vline(10, 4, 27, OAK[4])
    c.vline(11, 4, 27, HONEY[2])
    c.poly([(7, 31), (14, 31), (14, 39), (10, 42), (7, 39)], IRON[3])
    c.vline(7, 31, 8, IRON[5])
    c.hline(7, 31, 8, IRON[4])
    c.vline(13, 32, 7, IRON[1])
    c.rect(9, 30, 4, 2, IRON[2])
    # Rake, head up, tines up
    c.vline(18, 6, 36, OAK[4])
    c.vline(19, 6, 36, HONEY[2])
    c.rect(14, 4, 10, 2, IRON[3])
    c.hline(14, 4, 10, IRON[5])
    for tx in range(14, 24, 2):
        c.vline(tx, 1, 3, IRON[4])
    # Zinc watering can in front
    c.rect(17, 35, 9, 8, IRON[4])
    c.vline(17, 35, 8, IRON[5])
    c.vline(25, 35, 8, IRON[2])
    c.hline(17, 42, 9, IRON[2])
    c.ellipse(21.5, 35, 4.5, 1.4, IRON[5])
    c.hline(19, 37, 5, IRON[3])
    c.line(26, 40, 28, 34, IRON[3])
    c.line(25, 40, 27, 34, IRON[4])
    c.rect(27, 32, 2, 2, IRON[2])
    c.line(18, 34, 21, 31, IRON[2])
    c.line(21, 31, 24, 34, IRON[2])
    return _finish(c)


def stone_bench() -> Canvas:
    """56x24 basalt slab bench on two blocks, a little lichen."""
    c = Canvas(56, 24)
    for bx, lit in ((6, True), (39, False)):
        c.rect(bx, 14, 11, 9, BASALT[2] if lit else BASALT[1])
        c.vline(bx, 14, 9, BASALT[3] if lit else BASALT[2])
        c.vline(bx + 10, 14, 9, BASALT[1])
        c.hline(bx, 22, 11, BASALT[1])
    slab(c, 1, 4, 54, 8, 5, BASALT, grain=False)
    c.rect(1, 12, 54, 5, BASALT[2])
    c.hline(1, 12, 54, BASALT[1])
    c.hline(1, 16, 54, BASALT[0])
    c.hline(2, 4, 52, BASALT[6])
    speckle(c, 2, 5, 52, 6, BASALT[5], 26, 156)
    speckle(c, 1, 13, 54, 3, BASALT[2], 30, 157)
    speckle(c, 1, 4, 54, 18, BASALT[1], 40, 141)
    for lx, ly in ((4, 6), (5, 7), (48, 12), (49, 13), (8, 20), (44, 9)):
        c.px(lx, ly, mix(SAGE[4], OCHRE[4], 0.4))
    return _finish(c)


def lamp_post() -> Canvas:
    """16x64 iron yard lamp post with a lit oil lantern."""
    c = Canvas(16, 64)
    # Stepped base
    c.rect(3, 60, 10, 3, IRON[2])
    c.hline(3, 60, 10, IRON[4])
    c.hline(3, 62, 10, IRON[1])
    c.rect(5, 54, 6, 6, IRON[3])
    c.vline(5, 54, 6, IRON[4])
    c.vline(10, 54, 6, IRON[1])
    c.hline(5, 54, 6, IRON[5])
    # Shaft with collars and a ladder bar
    c.vline(7, 20, 34, IRON[4])
    c.vline(8, 20, 34, IRON[2])
    for cy in (36, 46):
        c.rect(6, cy, 4, 2, IRON[3])
        c.hline(6, cy, 4, IRON[5])
    c.rect(3, 22, 10, 2, IRON[3])
    c.hline(3, 22, 10, IRON[5])
    c.px(3, 21, IRON[4])
    c.px(12, 21, IRON[2])
    # Lantern: glazed body, flame, hipped roof, finial
    c.rect(5, 18, 6, 3, IRON[2])
    c.hline(5, 18, 6, IRON[4])
    glass = mix(FLAME[3], CREAM[5], 0.4)
    c.rect(4, 8, 8, 10, glass)
    c.dither(4, 8, 8, 10, FLAME[2], 0.3)
    c.rect(7, 12, 2, 4, FLAME[1])
    c.px(7, 11, FLAME[2])
    c.px(8, 12, FLAME[4])
    c.vline(4, 8, 10, IRON[2])
    c.vline(11, 8, 10, IRON[1])
    c.vline(8, 8, 3, IRON[2])
    c.hline(4, 17, 8, IRON[2])
    c.poly([(8, 2), (13, 8), (3, 8)], IRON[3])
    c.line(8, 2, 3, 7, IRON[5])
    c.hline(3, 7, 10, IRON[2])
    c.hline(2, 8, 12, IRON[1])
    c.vline(8, 0, 2, IRON[4])
    return _finish(c)


def gate_leaf() -> Canvas:
    """36x52 ledged-and-braced lane gate leaf with iron straps."""
    c = Canvas(36, 52)
    c.rect(1, 3, 34, 47, PLANK[3])
    for px in range(1, 35, 6):
        c.rect(px, 2, 6, 2, PLANK[3])
        c.hline(px, 2, 6, PLANK[5])
        c.vline(px, 2, 48, PLANK[1])
        c.vline(px + 1, 2, 48, PLANK[4])
    vgrain(c, 1, 3, 34, 47, PLANK[2], 158)
    # Ledges and a diagonal brace
    for ly in (8, 42):
        c.rect(1, ly, 34, 4, PLANK[4])
        c.hline(1, ly, 34, PLANK[5])
        c.hline(1, ly + 3, 34, PLANK[1])
    for t in range(4):
        c.line(4, 41 - t, 31, 12 - t, (PLANK[1], PLANK[4], PLANK[4], PLANK[5])[t])
    # Iron straps over the ledges, hinge knuckles at the left
    for sy in (9, 43):
        c.rect(0, sy, 30, 2, IRON[2])
        c.hline(0, sy, 30, IRON[4])
        c.poly([(29, sy - 1), (33, sy + 1), (29, sy + 3)], IRON[2])
        for rx in (6, 14, 22):
            c.px(rx, sy, IRON[5])
        c.rect(0, sy - 1, 2, 4, IRON[1])
    # Ring latch on the meeting stile
    ring(c, 30, 27, 2.6, 1.4, IRON[3], IRON[5], IRON[1])
    c.rect(29, 23, 3, 2, IRON[1])
    c.hline(1, 49, 34, PLANK[1])
    return _finish(c)


# ------------------------------------------------------------------ export ---

FOOTPRINTS: dict[str, tuple[int, int] | None] = {
    "kitchen_range": (96, 18),
    "hood_chimney": None,
    "ration_counter": (100, 16),
    "ledger_stand": (14, 10),
    "dish_rack": (52, 10),
    "granary_bin": (48, 18),
    "granary_door": None,
    "hand_pump": (14, 10),
    "spring_trough": (60, 18),
    "bucket_pair": (24, 8),
    # Only the two posts block; this span-wide strip is the requested stand-in.
    "drying_lines": (100, 6),
    "valve_station": (36, 14),
    "planter_bed": (92, 20),
    "seedling_pots": (26, 8),
    "garden_tools": (20, 8),
    "stone_bench": (52, 10),
    "lamp_post": (6, 6),
    "gate_leaf": None,
}


def build() -> dict[str, Canvas]:
    return {
        "kitchen_range": kitchen_range(),
        "hood_chimney": hood_chimney(),
        "ration_counter": ration_counter(),
        "ledger_stand": ledger_stand(),
        "dish_rack": dish_rack(),
        "granary_bin": granary_bin(),
        "granary_door": granary_door(),
        "hand_pump": hand_pump(),
        "spring_trough": spring_trough(),
        "bucket_pair": bucket_pair(),
        "drying_lines": drying_lines(),
        "valve_station": valve_station(),
        "planter_bed": planter_bed(),
        "seedling_pots": seedling_pots(),
        "garden_tools": garden_tools(),
        "stone_bench": stone_bench(),
        "lamp_post": lamp_post(),
        "gate_leaf": gate_leaf(),
    }
