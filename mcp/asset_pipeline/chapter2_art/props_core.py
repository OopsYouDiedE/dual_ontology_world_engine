"""Chapter 2 core furniture: living hall and academic core.

Native 1x recipes in the First Awakening style: key light from the upper left,
3/4 top-down (lit top above shaded front), selective dark outline via _finish.
Each canvas' bottom-centre is the floor anchor; wall-mounted pieces anchor at
the bottom of their mounting. FOOTPRINTS gives the solid floor rectangle
(width, height) centred on the anchor and extending upward, or None.
"""
from __future__ import annotations

import math

from first_awakening_art.palette import (BOTTLE, BRASS, CAP, CHALK, CLARET, CREAM, FLAME, FOG, HONEY, INK, IRON,
                                         LINEN, OAK, OCHRE, ROSE, SLATE, STONE, TILE, VOID, WALNUT)
from first_awakening_art.pixel import Canvas, mix, rng
from first_awakening_art.props import _finish, drawer, leg, slab

# Derived ramps (mixed from the palette, no new hex values).
COPPER = [mix(BRASS[i], ROSE[i], 0.5) for i in range(6)]
BRONZE = [mix(BRASS[i], WALNUT[i], 0.4) for i in range(6)]
MARBLE = [STONE[2], STONE[3], STONE[4], TILE[3], TILE[4], TILE[5]]
INKLINE = mix(INK, CREAM[2], 0.35)
FAINT_INK = mix(INK, CREAM[5], 0.55)


# ---------------------------------------------------------------- helpers ---

def cyl(c: Canvas, x: int, y: int, w: int, h: int, ramp) -> None:
    """Vertical cylinder lit from the left: highlight a third in, dark right rim."""
    for i in range(w):
        t = (i + 0.5) / w
        k = 3 if t < 0.1 else 4 if t < 0.2 else 5 if t < 0.3 else 4 if t < 0.48 else 3 if t < 0.72 else 2 if t < 0.9 else 1
        c.vline(x + i, y, h, ramp[k])


def rrect(c: Canvas, x: int, y: int, w: int, h: int, r: int, col) -> None:
    c.rect(x + r, y, w - 2 * r, h, col)
    c.rect(x, y + r, w, h - 2 * r, col)
    for cx, cy in ((x + r, y + r), (x + w - r, y + r), (x + r, y + h - r), (x + w - r, y + h - r)):
        c.ellipse(cx, cy, r, r, col)


def ring(c: Canvas, cx: float, cy: float, r: float, lit, dark, mid) -> None:
    """A one-pixel circle lit on its upper-left arc."""
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if r - 1.0 <= d <= r:
                facing = -(dx + dy) / max(d, 0.01)
                c.px(x, y, lit if facing > 0.5 else dark if facing < -0.5 else mid)


def paper(c: Canvas, x: int, y: int, w: int, h: int, sheet, ink, shadow, seed: int, pin=None) -> None:
    """A pinned sheet with ruled (illegible) lines and a drop shadow."""
    g = rng(seed)
    c.rect(x + 1, y + 1, w, h, shadow)
    c.rect(x, y, w, h, sheet)
    c.hline(x, y + h - 1, w, mix(sheet, shadow, 0.35))
    for ly in range(y + 3, y + h - 1, 2):
        c.hline(x + 1 + int(g.integers(0, 2)), ly, max(1, w - 3 - int(g.integers(0, 3))), ink)
    c.px(x + w // 2, y + 1, pin or BRASS[4])
    c.px(x + w // 2, y + 2, mix(sheet, shadow, 0.5))


# -------------------------------------------------------- living hall -------

def fireplace() -> Canvas:
    """Stone chimney breast, marble surround, low embers behind an iron grate."""
    c = Canvas(72, 64)
    # Ashlar chimney breast above the mantel
    c.rect(8, 2, 56, 17, STONE[3])
    c.hline(8, 2, 56, STONE[5])
    for row, jy in enumerate((8, 14)):
        c.hline(9, jy, 54, STONE[2])
        c.hline(9, jy + 1, 54, STONE[4])
        for jx in range(14 + (7 if row else 0), 62, 14):
            c.vline(jx, jy - 5 if row else 3, 5 if row else jy - 3, STONE[2])
    for jx in range(21, 62, 14):
        c.vline(jx, 15, 4, STONE[2])
    c.vline(8, 2, 17, STONE[5])
    c.vline(63, 2, 17, STONE[1])
    # Mantel clock and a pair of unlit candlesticks
    c.rect(31, 7, 10, 11, WALNUT[3])
    c.poly([(30, 8), (36, 4), (42, 8)], WALNUT[4])
    c.hline(31, 7, 10, WALNUT[5])
    c.ellipse(36, 12, 3, 3, BRASS[3])
    c.ellipse(36, 12, 2, 2, CREAM[5])
    c.px(36, 11, INK)
    c.px(37, 12, INK)
    c.rect(30, 17, 12, 1, WALNUT[2])
    for sx in (12, 58):
        c.rect(sx - 1, 15, 4, 2, BRASS[3])
        c.hline(sx - 1, 15, 4, BRASS[5])
        c.rect(sx, 11, 2, 4, BRASS[2])
        c.px(sx, 11, BRASS[4])
        c.rect(sx, 5, 2, 6, CREAM[5])
        c.px(sx + 1, 6, CREAM[3])
        c.px(sx, 4, INK)
    c.rect(47, 13, 5, 4, TILE[4])
    c.hline(47, 13, 5, TILE[5])
    c.px(51, 15, TILE[2])
    # Mantel shelf
    slab(c, 2, 17, 68, 4, 3, MARBLE, grain=False)
    c.hline(4, 24, 64, MARBLE[1])
    # Frieze with a central tablet
    c.rect(6, 24, 60, 5, MARBLE[3])
    c.hline(6, 25, 60, MARBLE[4])
    c.hline(6, 28, 60, MARBLE[1])
    c.rect(30, 25, 12, 3, MARBLE[4])
    c.hline(30, 25, 12, MARBLE[5])
    c.hline(30, 27, 12, MARBLE[2])
    # Jambs (pilasters with a fluting groove)
    for jx, lit in ((6, True), (54, False)):
        c.rect(jx, 29, 12, 27, MARBLE[3])
        c.vline(jx, 29, 27, MARBLE[5] if lit else MARBLE[4])
        c.vline(jx + 11, 29, 27, MARBLE[2] if lit else MARBLE[1])
        c.vline(jx + 5, 31, 22, MARBLE[2])
        c.vline(jx + 6, 31, 22, MARBLE[4])
        c.rect(jx - 1, 52, 14, 4, MARBLE[3])
        c.hline(jx - 1, 52, 14, MARBLE[5])
    c.vline(17, 29, 27, MARBLE[1])    # left jamb inner face, away from light
    c.vline(54, 29, 27, MARBLE[4])    # right jamb inner face, toward light
    # Firebox: soot-dark brick back, glow only at the bottom
    c.rect(18, 29, 36, 27, CAP[0])
    c.rect(21, 30, 30, 26, CAP[1])
    for by in range(32, 55, 3):
        c.hline(21, by, 30, CAP[0])
        for bx in range(22 + (3 if by % 2 else 0), 51, 6):
            c.px(bx, by + 1, CAP[0])
    c.poly([(18, 29), (21, 30), (21, 56), (18, 56)], CAP[2])
    c.poly([(54, 29), (51, 30), (51, 56), (54, 56)], CAP[0])
    glow = FLAME[0]
    c.dither(21, 42, 30, 6, mix(CAP[1], glow, 0.45), 0.25)
    c.dither(21, 47, 30, 8, mix(CAP[1], glow, 0.7), 0.4, phase=1)
    # Coal bed in the grate basket
    for cx, cy in ((27, 46), (33, 45), (40, 46), (46, 47)):
        c.ellipse(cx, cy, 4, 2, CAP[2])
        c.px(cx - 1, cy - 1, CAP[3])
    for ex, ey in ((25, 47), (30, 46), (35, 47), (38, 46), (43, 47), (47, 48), (32, 48)):
        c.px(ex, ey, FLAME[1])
    c.px(34, 46, FLAME[2])
    c.px(41, 47, FLAME[2])
    c.hline(23, 49, 26, FLAME[0])
    for gx in range(23, 50, 3):
        c.px(gx + 1, 50, FLAME[1])
        c.px(gx + 2, 51, FLAME[0])
    # Iron grate basket
    c.hline(22, 48, 28, IRON[4])
    for gx in range(22, 50, 3):
        c.vline(gx, 49, 5, IRON[3])
    c.hline(22, 53, 28, IRON[2])
    for lx in (23, 47):
        c.rect(lx, 54, 2, 2, IRON[2])
    c.px(22, 48, IRON[5])
    # Hearth slab
    slab(c, 1, 55, 70, 5, 4, STONE, grain=False)
    c.hline(2, 57, 68, mix(STONE[4], STONE[3], 0.5))
    # Brass fender with turned-up ends
    c.hline(13, 58, 46, BRASS[5])
    c.hline(13, 59, 46, BRASS[3])
    c.hline(13, 60, 46, BRASS[1])
    for fx in (12, 58):
        c.rect(fx, 55, 2, 5, BRASS[3])
        c.px(fx, 55, BRASS[5])
        c.px(fx + 1, 59, BRASS[2])
    return _finish(c)


def long_table() -> Canvas:
    """160x56 walnut refectory table; a ledger and inkwell at the east end."""
    c = Canvas(160, 56)
    slab(c, 2, 4, 156, 32, 8, WALNUT, seed=201)
    for by in (12, 20, 28):
        c.hline(3, by, 154, mix(WALNUT[4], WALNUT[3], 0.7))
    for bx in (8, 151):
        c.vline(bx, 5, 30, mix(WALNUT[4], WALNUT[3], 0.8))
    # Apron panels
    for px in range(8, 150, 36):
        c.rect(px, 38, 30, 4, WALNUT[2])
        c.hline(px, 38, 30, WALNUT[1])
        c.hline(px, 41, 30, WALNUT[4])
    # Three turned legs along the near side
    for lx in (5, 77, 149):
        c.rect(lx, 44, 6, 12, WALNUT[3])
        c.rect(lx - 1, 47, 8, 3, WALNUT[4])
        c.hline(lx - 1, 47, 8, WALNUT[5])
        c.hline(lx - 1, 49, 8, WALNUT[2])
        c.vline(lx, 44, 12, WALNUT[4])
        c.vline(lx + 5, 44, 12, WALNUT[1])
        c.hline(lx, 55, 6, WALNUT[1])
    # Ledger: claret boards, ruled pages with red money columns
    c.rect(116, 9, 32, 19, CLARET[2])
    c.hline(116, 9, 32, CLARET[4])
    c.rect(117, 10, 15, 16, CREAM[4])
    c.rect(132, 10, 15, 16, CREAM[3])
    c.hline(118, 10, 13, CREAM[5])
    c.hline(133, 10, 13, CREAM[5])
    c.vline(118, 11, 14, CREAM[5])
    c.line(143, 11, 146, 14, CREAM[1])
    # Porcelain cup and saucer, with a dark interior and a crescent highlight.
    c.ellipse(91, 23, 8, 3, WALNUT[1])
    c.ellipse(90, 21, 7, 3, TILE[3])
    c.ellipse(90, 20, 5, 2, TILE[5])
    c.rect(86, 15, 8, 6, TILE[3])
    c.ellipse(90, 15, 4, 2, TILE[5])
    c.ellipse(90, 15, 3, 1, WALNUT[1])
    c.vline(86, 16, 4, TILE[5])
    c.ellipse(95, 18, 2, 2, TILE[3])
    c.px(95, 18, WALNUT[2])
    c.vline(132, 10, 16, CREAM[1])
    c.hline(117, 26, 30, CREAM[2])
    for ly in range(13, 25, 2):
        c.hline(118, ly, 13, FOG[3])
        c.hline(133, ly, 13, FOG[3])
    for vx in (127, 129, 142, 144):
        c.vline(vx, 11, 14, CLARET[4])
    for ly, lw in ((13, 7), (15, 5), (17, 8), (19, 4), (21, 6)):
        c.hline(118, ly, lw, INKLINE)
    c.vline(133, 25, 4, CLARET[4])
    # Inkwell and a dip pen
    c.rect(104, 13, 7, 6, IRON[2])
    c.hline(104, 13, 7, IRON[4])
    c.vline(104, 13, 6, IRON[3])
    c.rect(106, 11, 3, 2, BRASS[3])
    c.px(106, 11, BRASS[5])
    c.hline(104, 19, 7, IRON[0])
    c.line(98, 26, 109, 21, WALNUT[1])
    c.px(110, 21, IRON[5])
    # A brass chamberstick midway down the board
    c.ellipse(70, 20, 4, 2, BRASS[2])
    c.ellipse(69.5, 19.5, 3, 1.4, BRASS[4])
    c.rect(69, 13, 2, 6, CREAM[5])
    c.px(70, 14, CREAM[3])
    c.px(69, 12, INK)
    return _finish(c)


def long_bench() -> Canvas:
    c = Canvas(128, 24)
    slab(c, 1, 4, 126, 8, 4, WALNUT, seed=202)
    c.hline(2, 8, 124, mix(WALNUT[4], WALNUT[3], 0.7))
    c.hline(10, 20, 108, WALNUT[2])
    c.hline(10, 21, 108, WALNUT[1])
    for lx in (5, 62, 119):
        c.rect(lx, 16, 4, 8, WALNUT[3])
        c.vline(lx, 16, 8, WALNUT[4])
        c.vline(lx + 3, 16, 8, WALNUT[1])
        c.hline(lx, 23, 4, WALNUT[1])
    return _finish(c)


def upright_piano() -> Canvas:
    """Upright piano against the wall, fallboard closed, brass candle sconces."""
    c = Canvas(60, 56)
    # Closed top lid and cornice
    c.rect(2, 1, 56, 6, WALNUT[4])
    c.hline(2, 1, 56, WALNUT[5])
    c.hline(3, 6, 54, mix(WALNUT[4], WALNUT[3], 0.5))
    c.rect(36, 2, 12, 3, CREAM[4])     # a few loose music sheets
    c.hline(36, 2, 12, CREAM[5])
    c.hline(37, 4, 11, CREAM[2])
    c.rect(1, 7, 58, 2, WALNUT[3])
    c.hline(1, 8, 58, WALNUT[1])
    # Upper case
    c.rect(3, 9, 54, 20, WALNUT[3])
    c.vline(3, 9, 20, WALNUT[4])
    c.vline(56, 9, 20, WALNUT[2])
    c.rect(15, 11, 30, 13, WALNUT[1])
    c.rect(16, 12, 28, 11, CLARET[2])
    c.dither(16, 12, 28, 11, CLARET[3], 0.25)
    for fx in (22, 30, 37):
        c.vline(fx, 12, 11, WALNUT[3])
    c.poly([(30, 13), (34, 17), (30, 21), (26, 17)], WALNUT[3])
    c.px(29, 14, WALNUT[5])
    c.hline(16, 12, 28, WALNUT[2])
    for px_ in (5, 47):
        c.rect(px_, 11, 8, 15, WALNUT[2])
        c.rect(px_ + 1, 12, 6, 13, WALNUT[3])
        c.hline(px_ + 1, 12, 6, WALNUT[4])
    # Brass sconces with unlit candles
    for sx in (8, 51):
        c.rect(sx - 1, 24, 3, 3, BRASS[2])
        c.px(sx - 1, 24, BRASS[4])
        c.px(sx, 23, BRASS[3])
        c.rect(sx - 3, 21, 7, 2, BRASS[3])
        c.hline(sx - 3, 21, 7, BRASS[5])
        c.hline(sx - 2, 22, 5, BRASS[1])
        c.rect(sx - 1, 14, 3, 7, CREAM[5])
        c.vline(sx + 1, 14, 7, CREAM[3])
        c.px(sx, 13, INK)
    # Keybed with the fallboard closed and locked
    for ky, tone in ((29, 3), (30, 5), (31, 4), (32, 4), (33, 3)):
        c.hline(4, ky, 52, WALNUT[tone])
    c.rect(4, 34, 52, 3, WALNUT[3])
    c.hline(4, 36, 52, WALNUT[1])
    c.px(29, 32, BRASS[3])
    c.px(30, 32, BRASS[4])
    c.px(30, 33, BRASS[2])
    for cx, lit in ((1, True), (55, False)):
        c.rect(cx, 27, 4, 10, WALNUT[4] if lit else WALNUT[2])
        c.hline(cx, 27, 4, WALNUT[5] if lit else WALNUT[3])
        c.hline(cx, 36, 4, WALNUT[1])
    # Lower case, legs, pedals and plinth
    c.rect(6, 37, 48, 15, WALNUT[2])
    c.rect(12, 39, 36, 11, WALNUT[3])
    c.frame(12, 39, 36, 11, WALNUT[1])
    c.hline(13, 40, 34, WALNUT[4])
    for lx, lit in ((2, True), (54, False)):
        c.rect(lx, 37, 4, 15, WALNUT[4] if lit else WALNUT[3])
        c.rect(lx - (0 if lit else 0), 41, 4, 3, WALNUT[5] if lit else WALNUT[4])
        c.hline(lx, 44, 4, WALNUT[2])
        c.vline(lx + 3, 37, 15, WALNUT[2] if lit else WALNUT[1])
    for px_ in (24, 32):
        c.rect(px_, 50, 4, 2, BRASS[3])
        c.hline(px_, 50, 4, BRASS[5])
    c.rect(1, 52, 58, 4, WALNUT[2])
    c.hline(1, 52, 58, WALNUT[4])
    c.hline(1, 55, 58, WALNUT[1])
    return _finish(c)


def piano_stool() -> Canvas:
    c = Canvas(18, 20)
    # Splayed legs
    c.line(7, 10, 3, 18, WALNUT[3])
    c.line(8, 10, 4, 18, WALNUT[2])
    c.line(10, 10, 14, 18, WALNUT[2])
    c.line(11, 10, 15, 18, WALNUT[1])
    c.vline(8, 10, 9, WALNUT[4])
    c.vline(9, 10, 9, WALNUT[2])
    for fx in (3, 8, 14):
        c.hline(fx, 19, 2, BRASS[3])
    # Screw column and drum
    c.rect(7, 8, 4, 3, BRASS[2])
    c.px(7, 8, BRASS[4])
    c.rect(2, 5, 14, 4, WALNUT[3])
    c.vline(2, 5, 4, WALNUT[4])
    c.vline(15, 5, 4, WALNUT[1])
    c.hline(2, 8, 14, WALNUT[1])
    # Buttoned cushion
    c.ellipse(9, 4.5, 7, 3, CLARET[3])
    c.ellipse(8.5, 4, 6, 2.2, CLARET[4])
    c.hline(6, 2, 5, CLARET[5])
    c.px(9, 4, CLARET[2])
    return _finish(c)


def chess_table() -> Canvas:
    """Square games table; a game abandoned mid-way."""
    c = Canvas(34, 34)
    slab(c, 2, 3, 30, 19, 4, WALNUT, grain=False)
    c.frame(4, 4, 26, 18, WALNUT[2])
    light, dark = OAK[5], WALNUT[2]
    for row in range(8):
        for col in range(8):
            c.rect(5 + col * 3, 5 + row * 2, 3, 2, light if (row + col) % 2 == 0 else dark)
    c.hline(5, 5, 24, mix(OAK[6], OAK[5], 0.3))
    # Legs and a low drawer
    drawer(c, 13, 23, 8, 3, WALNUT)
    leg(c, 4, 26, 8, WALNUT)
    leg(c, 28, 26, 8, WALNUT)
    c.vline(4, 26, 8, WALNUT[4])

    def piece(col: int, row: int, white: bool, h: int) -> None:
        x, y = 5 + col * 3 + 1, 5 + row * 2 + 1
        body, lit, base = ((CREAM[4], CREAM[5], CREAM[2]) if white else (IRON[1], IRON[3], IRON[0]))
        c.hline(x - 1, y, 3, base)
        c.vline(x, y - h + 1, h - 1, body)
        c.px(x, y - h, lit)
        c.px(x - 1, y - h + 1, lit if h > 3 else body)

    for col, row, white, h in ((2, 0, False, 4), (5, 1, False, 2), (1, 1, False, 2), (6, 2, False, 2),
                               (4, 3, False, 4), (3, 4, True, 2), (4, 5, False, 3), (2, 6, True, 2),
                               (5, 6, True, 2), (0, 7, True, 3), (6, 7, True, 4)):
        piece(col, row, white, h)
    return _finish(c)


def notice_board() -> Canvas:
    """Oak-framed cork board, pinned notices; one sheet in red ink."""
    c = Canvas(44, 40)
    c.px(6, 0, BRASS[4])
    c.px(37, 0, BRASS[4])
    c.rect(0, 1, 44, 38, OAK[2])
    c.hline(0, 1, 44, OAK[4])
    c.vline(0, 1, 38, OAK[3])
    c.vline(43, 1, 38, OAK[1])
    c.hline(1, 2, 42, OAK[3])
    c.rect(3, 4, 38, 31, HONEY[3])
    c.dither(3, 4, 38, 31, HONEY[4], 0.22)
    c.dither(3, 4, 38, 31, HONEY[2], 0.1, phase=2)
    c.hline(3, 4, 38, HONEY[1])
    c.vline(3, 4, 31, HONEY[2])
    # Bottom rail lip
    c.rect(0, 35, 44, 5, OAK[3])
    c.hline(0, 35, 44, OAK[5])
    c.hline(0, 39, 44, OAK[1])
    sheets = ((6, 7, 10, 13, CREAM[5], INKLINE, None),
              (18, 6, 9, 11, LINEN[4], INKLINE, CLARET[4]),
              (29, 8, 10, 12, CREAM[5], INKLINE, None),
              (7, 22, 12, 10, CREAM[4], INKLINE, IRON[3]),
              (21, 19, 9, 13, CREAM[5], CLARET[4], CLARET[4]),
              (32, 23, 7, 9, LINEN[5], INKLINE, None))
    for i, (x, y, w, h, sheet, ink, pin) in enumerate(sheets):
        paper(c, x, y, w, h, sheet, ink, HONEY[1], 300 + i, pin)
    # Dog-eared corner and a red underline on the red-ink notice
    c.px(39, 8, HONEY[3])
    c.px(38, 9, CREAM[3])
    c.hline(22, 21, 6, CLARET[3])
    return _finish(c)


def shoe_bench() -> Canvas:
    """Low bench with a row of boots tucked beneath."""
    c = Canvas(48, 26)
    slab(c, 1, 3, 46, 6, 3, WALNUT, seed=203)
    c.hline(2, 12, 44, WALNUT[1])
    c.rect(2, 12, 3, 14, WALNUT[3])
    c.vline(2, 12, 14, WALNUT[4])
    c.rect(43, 12, 3, 14, WALNUT[2])
    c.vline(45, 12, 14, WALNUT[1])
    c.rect(5, 22, 38, 1, WALNUT[2])          # low stretcher behind the boots
    # Four pairs of laced boots, toes toward the room
    leathers = (WALNUT, IRON, HONEY, CLARET)
    for i, ramp in enumerate(leathers):
        top = (14, 13, 16, 15)[i]
        for j in range(2):
            bx = 6 + i * 9 + j * 4
            c.rect(bx, top, 3, 21 - top, ramp[2])
            c.vline(bx, top + 1, 20 - top, ramp[3])
            c.hline(bx, top, 3, ramp[0])            # boot opening
            c.hline(bx, top + 1, 3, ramp[4])        # lit cuff
            for ly in range(top + 3, 20, 2):
                c.px(bx + 1, ly, ramp[4])           # lace eyelets
            c.rect(bx, 20, 4, 4, ramp[2])           # toe box
            c.hline(bx, 20, 3, ramp[3])
            c.rect(bx, 21, 3, 2, ramp[3])
            c.px(bx, 21, ramp[5])                   # polish highlight
            c.hline(bx, 24, 4, ramp[0])             # sole
    c.hline(1, 25, 46, WALNUT[1])
    return _finish(c)


def basin_row() -> Canvas:
    """Washroom stone counter with three enamel basins and brass taps."""
    c = Canvas(112, 44)
    # Splashback
    c.rect(2, 0, 108, 10, MARBLE[3])
    c.hline(2, 0, 108, MARBLE[5])
    c.hline(2, 1, 108, MARBLE[4])
    c.vline(2, 0, 10, MARBLE[4])
    c.vline(109, 0, 10, MARBLE[1])
    # Under-counter supports and waste pipes (drawn before the slab)
    slab(c, 0, 9, 112, 20, 6, MARBLE, grain=False)
    c.line(14, 26, 30, 14, mix(MARBLE[4], MARBLE[2], 0.4))
    c.line(70, 27, 82, 18, mix(MARBLE[4], MARBLE[2], 0.4))
    for sx in (2, 37, 73, 104):
        c.rect(sx, 35, 6, 9, MARBLE[3])
        c.vline(sx, 35, 9, MARBLE[5])
        c.vline(sx + 5, 35, 9, MARBLE[1])
        c.hline(sx, 43, 6, MARBLE[1])
    for bx in (20, 56, 92):
        c.rect(bx - 1, 35, 2, 4, IRON[3])
        c.px(bx, 35, IRON[1])
        c.rect(bx - 2, 39, 4, 2, IRON[3])
        c.hline(bx - 2, 40, 4, IRON[1])
        c.px(bx - 2, 39, IRON[5])
    # Enamel basins sunk into the counter
    for bx in (20, 56, 92):
        c.ellipse(bx, 20, 13, 7, MARBLE[1])
        c.ellipse(bx, 20, 12, 6, CREAM[5])
        c.ellipse(bx, 20.5, 10, 4.6, CREAM[2])
        c.ellipse(bx, 21.5, 8.5, 3.4, CREAM[3])
        c.ellipse(bx, 22, 6, 2.2, CREAM[4])
        c.px(bx, 22, IRON[1])
        c.px(bx + 1, 22, IRON[2])
        c.hline(bx - 6, 14, 6, TILE[5])
    # Brass taps on the splashback, spouts reaching over each bowl
    for bx in (20, 56, 92):
        c.rect(bx - 1, 3, 3, 9, BRASS[3])
        c.vline(bx - 1, 3, 9, BRASS[4])
        c.px(bx + 1, 11, BRASS[1])
        c.hline(bx - 4, 3, 9, BRASS[4])
        c.px(bx - 4, 2, BRASS[5])
        c.px(bx + 4, 2, BRASS[3])
        c.rect(bx - 1, 12, 3, 2, BRASS[2])
        c.px(bx, 14, BRASS[1])
    return _finish(c)


def bathtub() -> Canvas:
    """Cast-iron clawfoot tub, cream enamel inside, dry."""
    c = Canvas(70, 40)
    # Back feet peek out behind the body
    for fx in (8, 60):
        c.rect(fx, 28, 3, 5, BRASS[2])
    # Painted cast-iron body with a rolled bottom
    rrect(c, 3, 9, 64, 27, 10, IRON[3])
    c.rect(5, 26, 60, 4, IRON[2])
    rrect(c, 8, 28, 54, 8, 4, IRON[2])
    c.hline(14, 35, 42, IRON[1])
    c.vline(4, 16, 13, IRON[4])
    c.vline(65, 16, 13, IRON[1])
    # Rolled rim
    rrect(c, 2, 3, 66, 22, 10, CREAM[4])
    rrect(c, 2, 3, 66, 21, 10, CREAM[5])
    c.hline(12, 24, 46, CREAM[2])
    # Interior: shaded back wall, lit floor of the bath
    rrect(c, 4, 5, 62, 17, 8, CREAM[2])
    rrect(c, 8, 10, 54, 11, 5, CREAM[3])
    rrect(c, 12, 13, 46, 7, 3, CREAM[4])
    c.hline(16, 14, 30, CREAM[5])
    c.px(12, 16, BRASS[2])
    c.px(13, 16, IRON[1])
    # Front claw feet
    for fx in (11, 55):
        c.rect(fx, 32, 3, 4, BRASS[3])
        c.vline(fx, 32, 4, BRASS[4])
        c.rect(fx - 1, 36, 5, 3, BRASS[3])
        c.hline(fx - 1, 36, 5, BRASS[4])
        for tx in (fx - 1, fx + 1, fx + 3):
            c.px(tx, 39, BRASS[1])
    # Pillar taps and overflow at the head end
    for tx in (7, 11):
        c.rect(tx, 0, 2, 6, BRASS[3])
        c.px(tx, 0, BRASS[5])
        c.hline(tx - 1, 1, 4, BRASS[4])
    c.rect(8, 6, 4, 2, BRASS[2])
    c.px(9, 8, BRASS[1])
    return _finish(c)


def water_heater() -> Canvas:
    """Copper cylinder boiler with gauge and flue pipe on an iron stand."""
    c = Canvas(30, 60)
    # Stand
    c.rect(14, 47, 2, 10, IRON[1])
    for lx, lit in ((5, True), (23, False)):
        c.rect(lx, 47, 2, 12, IRON[3] if lit else IRON[2])
        c.vline(lx, 47, 12, IRON[4] if lit else IRON[2])
        c.rect(lx - 1, 58, 4, 2, IRON[2])
        c.hline(lx - 1, 58, 4, IRON[4])
    c.rect(9, 50, 12, 6, IRON[2])
    c.frame(9, 50, 12, 6, IRON[4])
    c.hline(11, 53, 8, IRON[0])
    c.px(12, 53, FLAME[0])
    c.px(16, 53, FLAME[0])
    # Copper cylinder
    c.ellipse(15, 44, 11, 2.5, COPPER[1])
    cyl(c, 4, 8, 22, 36, COPPER)
    c.ellipse(15, 8, 11, 4, COPPER[3])
    c.ellipse(14, 7, 9, 3, COPPER[4])
    c.ellipse(12, 6, 4, 1.5, COPPER[5])
    for by in (13, 39):
        c.hline(4, by, 22, COPPER[1])
        for rx in range(6, 26, 3):
            c.px(rx, by + 1, COPPER[5] if rx < 14 else COPPER[3])
    c.vline(19, 15, 23, COPPER[2])
    # Iron ring holding the cylinder
    c.rect(3, 43, 24, 4, IRON[3])
    c.hline(3, 43, 24, IRON[5])
    c.hline(3, 46, 24, IRON[1])
    c.vline(3, 43, 4, IRON[4])
    # Pressure gauge
    c.px(11, 26, BRASS[2])
    ring(c, 11, 22, 4, BRASS[5], BRASS[1], BRASS[3])
    c.ellipse(11, 22, 3, 3, CREAM[5])
    c.line(11, 22, 9, 20, INK)
    c.px(13, 21, CLARET[4])
    # Flue/outlet pipe up through the ceiling and a brass draw-off tap
    c.rect(25, 11, 4, 3, COPPER[3])
    c.hline(25, 11, 4, COPPER[5])
    c.vline(26, 0, 11, COPPER[4])
    c.vline(27, 0, 11, COPPER[2])
    c.rect(25, 4, 4, 2, BRASS[3])
    c.rect(13, 1, 4, 4, COPPER[3])
    c.vline(13, 1, 4, COPPER[5])
    c.rect(12, 0, 6, 1, BRASS[3])
    c.rect(18, 38, 3, 2, BRASS[3])
    c.px(20, 40, BRASS[1])
    return _finish(c)


def laundry_tub() -> Canvas:
    """Coopered wooden tub with a zinc washboard standing in it."""
    c = Canvas(40, 30)
    # Lugs
    c.rect(2, 6, 4, 6, HONEY[4])
    c.px(3, 7, HONEY[0])
    c.rect(34, 6, 4, 6, HONEY[2])
    c.px(36, 7, HONEY[0])
    # Staved body
    c.poly([(4, 10), (36, 10), (34, 29), (6, 29)], HONEY[3])
    for sx in range(8, 34, 4):
        c.line(sx, 11, sx + (1 if sx < 20 else -1 if sx > 20 else 0), 28, HONEY[2])
    c.line(5, 11, 7, 28, HONEY[4])
    c.line(35, 11, 33, 28, HONEY[1])
    for hy in (14, 24):
        inset = 4 + (hy - 10) // 9
        c.hline(inset, hy, 40 - 2 * inset, IRON[3])
        c.hline(inset, hy + 1, 40 - 2 * inset, IRON[1])
        c.px(inset + 1, hy, IRON[5])
    c.ellipse(20, 28.5, 14, 1.5, HONEY[1])
    # Rim and grey soap water
    c.ellipse(20, 10, 16, 4.5, HONEY[4])
    c.ellipse(20, 10, 14, 3.4, HONEY[1])
    c.ellipse(20, 11, 13, 2.4, mix(FOG[2], LINEN[2], 0.5))
    # Washboard rising out of the water
    c.rect(22, 1, 2, 11, OAK[4])
    c.rect(32, 1, 2, 11, OAK[2])
    c.rect(22, 0, 12, 3, OAK[3])
    c.hline(22, 0, 12, OAK[5])
    for ry in range(3, 11):
        c.hline(24, ry, 8, IRON[4] if ry % 2 else IRON[2])
    c.hline(21, 11, 14, mix(FOG[3], LINEN[3], 0.5))
    c.px(14, 11, LINEN[4])
    c.px(9, 12, LINEN[4])
    c.hline(5, 13, 30, HONEY[5])
    return _finish(c)


# ------------------------------------------------------- academic core ------

def lecture_desk_row() -> Canvas:
    """One long sloped desk with a bench attached on cast-iron standards."""
    c = Canvas(140, 40)
    # Book shelf under the desk
    c.rect(3, 15, 134, 6, WALNUT[1])
    c.hline(3, 20, 134, WALNUT[0])
    g = rng(205)
    spines = (CLARET, BOTTLE, OCHRE, SLATE, LINEN)
    for i in range(5):
        sx = 9 + i * 28 + int(g.integers(0, 8))
        for k in range(1 + int(g.integers(0, 2))):
            ramp = spines[int(g.integers(0, len(spines)))]
            bw = 9 + int(g.integers(0, 4))
            by = 18 - 2 * k
            c.rect(sx + k, by, bw, 2, ramp[2])
            c.hline(sx + k, by, bw, ramp[4])
            c.px(sx + k + bw - 1, by + 1, CREAM[3])
    # Cast-iron standards
    for sx in (2, 46, 91, 135):
        c.rect(sx, 14, 3, 25, IRON[2])
        c.vline(sx, 14, 25, IRON[4])
        c.vline(sx + 2, 14, 25, IRON[1])
        c.px(sx + 1, 22, IRON[5])
        c.rect(sx - 1, 37, 5, 3, IRON[2])
        c.hline(sx - 1, 37, 5, IRON[4])
    # Flat back strip with brass-ringed inkwell holes
    c.rect(1, 2, 138, 4, WALNUT[5])
    c.hline(1, 2, 138, WALNUT[6])
    for i in range(5):
        ix = 13 + 28 * i
        c.rect(ix - 1, 3, 4, 2, BRASS[2])
        c.hline(ix - 1, 3, 4, BRASS[4])
        c.px(ix, 4, INK)
        c.px(ix + 1, 4, INK)
        c.hline(ix + 6, 4, 8, WALNUT[3])          # pen groove
    c.hline(1, 6, 138, WALNUT[2])
    # Sloped hinged lids, one per seat
    c.rect(1, 7, 138, 7, WALNUT[4])
    for _ in range(26):
        gx, gy = 2 + int(g.integers(0, 130)), 8 + int(g.integers(0, 5))
        c.hline(gx, gy, int(g.integers(3, 8)), mix(WALNUT[4], WALNUT[3], 0.55))
    for i in range(1, 5):
        c.vline(28 * i, 7, 7, WALNUT[2])
        c.vline(28 * i + 1, 7, 7, WALNUT[5])
    c.hline(1, 7, 138, mix(WALNUT[5], WALNUT[4], 0.5))
    c.rect(1, 13, 138, 2, WALNUT[3])
    c.hline(1, 14, 138, WALNUT[2])
    # Attached bench
    slab(c, 0, 24, 140, 5, 3, WALNUT, seed=206)
    return _finish(c)


def lectern() -> Canvas:
    """Standing lectern with the class register lying open."""
    c = Canvas(28, 44)
    slab(c, 3, 37, 22, 3, 4, WALNUT, grain=False)
    c.rect(7, 18, 14, 19, WALNUT[3])
    c.vline(7, 18, 19, WALNUT[4])
    c.vline(20, 18, 19, WALNUT[1])
    c.rect(10, 21, 8, 13, WALNUT[2])
    c.rect(11, 22, 6, 11, WALNUT[3])
    c.hline(11, 22, 6, WALNUT[4])
    # Sloped top with a book lip
    c.rect(1, 3, 26, 13, WALNUT[4])
    c.hline(1, 3, 26, WALNUT[5])
    c.rect(1, 15, 26, 3, WALNUT[3])
    c.hline(1, 15, 26, WALNUT[5])
    c.hline(1, 17, 26, WALNUT[1])
    # Open register
    c.rect(3, 4, 22, 11, CLARET[2])
    c.rect(4, 5, 10, 9, CREAM[5])
    c.rect(14, 5, 10, 9, CREAM[4])
    c.vline(14, 5, 9, CREAM[1])
    for ly in range(7, 14, 2):
        c.hline(5, ly, 8, FOG[3])
        c.hline(15, ly, 8, FOG[3])
    c.vline(7, 6, 8, CLARET[4])
    c.vline(17, 6, 8, CLARET[4])
    for ly, lw in ((7, 4), (9, 3), (11, 5)):
        c.hline(8, ly, lw, INKLINE)
    c.vline(15, 14, 4, CLARET[4])
    return _finish(c)


def blackboard_large() -> Canvas:
    """Framed slate with a chalk tray; diagrams half wiped away."""
    c = Canvas(132, 60)
    c.rect(0, 0, 132, 54, WALNUT[3])
    c.hline(0, 0, 132, WALNUT[5])
    c.hline(1, 1, 130, WALNUT[4])
    c.vline(0, 0, 54, WALNUT[4])
    c.vline(131, 0, 54, WALNUT[1])
    slate = SLATE[1]
    c.rect(4, 4, 124, 47, slate)
    c.dither(4, 4, 124, 47, SLATE[2], 0.1)
    c.hline(4, 4, 124, SLATE[0])
    c.vline(4, 4, 47, SLATE[0])
    ghost = mix(slate, CHALK[3], 0.16)
    faint = mix(slate, CHALK[3], 0.42)
    bright = mix(slate, CHALK[3], 0.72)
    # Eraser sweeps
    for i, (x, y, w, h) in enumerate(((8, 7, 52, 12), (58, 20, 44, 13), (16, 30, 36, 10), (96, 34, 26, 12))):
        c.dither(x, y, w, h, ghost, 0.35, phase=i)
        c.dither(x + 2, y + 2, w - 4, h - 4, ghost, 0.55, phase=i + 1)
    # Illegible lines of chalk writing, the lower ones mostly wiped
    g = rng(207)
    for row, (ly, strength) in enumerate(((10, bright), (16, faint), (22, faint), (28, ghost))):
        x = 10
        while x < 54:
            wl = int(g.integers(3, 9))
            for k in range(wl):
                if g.random() < (0.9 if strength is bright else 0.6):
                    c.px(x + k, ly + int(round(math.sin((x + k) * 1.3 + row) * 0.8)), strength)
                if k % 3 == 1:
                    c.px(x + k, ly - 1, strength)
            x += wl + 2
    # Circle diagram with radius, broken by the wipe
    for a in range(0, 360, 6):
        if 140 < a < 210:
            continue
        r = math.radians(a)
        c.px(int(round(98 + math.cos(r) * 10)), int(round(17 + math.sin(r) * 7)), bright if a < 100 else faint)
    c.line(98, 17, 107, 13, faint)
    c.px(98, 17, bright)
    # Triangle and a long arrow
    c.line(64, 44, 78, 30, faint)
    c.line(78, 30, 90, 44, ghost)
    c.hline(64, 44, 27, faint)
    c.hline(30, 44, 26, ghost)
    c.line(52, 42, 56, 44, faint)
    c.line(52, 46, 56, 44, faint)
    # Chalk tray with sticks and a felt eraser
    slab(c, 2, 51, 128, 3, 6, WALNUT, grain=False)
    c.hline(20, 52, 5, CHALK[3])
    c.hline(27, 52, 3, CHALK[3])
    c.hline(33, 53, 2, mix(CHALK[3], OCHRE[4], 0.3))
    c.rect(100, 50, 9, 3, WALNUT[5])
    c.hline(100, 50, 9, WALNUT[6])
    c.hline(100, 53, 9, LINEN[2])
    return _finish(c)


def relay_column() -> Canvas:
    """Bronze relay column: bundled speaking tubes, valves, gauges, mouthpiece."""
    c = Canvas(52, 96)
    # Iron plinth (drum)
    c.ellipse(26, 92, 21, 3, IRON[1])
    cyl(c, 5, 84, 42, 9, IRON)
    c.ellipse(26, 84, 21, 5, IRON[4])
    c.ellipse(26, 84, 19, 4, IRON[3])
    c.ellipse(24, 83, 10, 2, IRON[4])
    c.hline(10, 79, 32, IRON[5])
    # Bundle of speaking tubes, bronze and brass alternating
    for i in range(9):
        x = 8 + 4 * i
        ramp = BRASS if i % 2 == 0 else BRONZE
        d = 0 if i < 3 else 1 if i < 6 else 2
        c.vline(x, 0, 86, ramp[max(4 - d, 1)])
        c.vline(x + 1, 0, 86, ramp[max(3 - d, 1)])
        c.vline(x + 2, 0, 86, ramp[max(2 - d, 1)])
        c.vline(x + 3, 0, 86, ramp[0])
        if i in (1, 2):
            for hy in range(3, 80, 9):
                c.vline(x, hy, 3, BRASS[5])
    # Iron collars
    for cy in (10, 38, 66):
        c.rect(6, cy, 40, 5, IRON[3])
        c.hline(6, cy, 40, IRON[5])
        c.hline(6, cy + 4, 40, IRON[1])
        c.vline(6, cy, 5, IRON[4])
        c.vline(45, cy, 5, IRON[1])
        for rx in range(9, 44, 5):
            c.px(rx, cy + 2, IRON[5] if rx < 26 else IRON[4])
    # Two dial gauges in dark iron bezels, on short stems
    for gx, gy, needle in ((16, 26, (-2, -2)), (36, 28, (2, -2))):
        c.rect(gx - 1, gy + 5, 3, 3, IRON[2])
        c.px(gx - 1, gy + 5, IRON[4])
        c.ellipse(gx, gy, 6, 6, IRON[0])
        ring(c, gx, gy, 5.2, IRON[5], IRON[1], IRON[3])
        c.ellipse(gx, gy, 4.2, 4.2, CREAM[5])
        c.ellipse(gx + 0.6, gy + 0.6, 3.6, 3.6, CREAM[4])
        c.ellipse(gx - 0.4, gy - 0.4, 3.2, 3.2, CREAM[5])
        for a in (200, 250, 290, 340):
            r = math.radians(a)
            c.px(int(round(gx - 0.5 + math.cos(r) * 3)), int(round(gy - 0.5 + math.sin(r) * 3)), FAINT_INK)
        c.px(gx + 2, gy - 3, CLARET[4])
        c.line(gx, gy, gx + needle[0], gy + needle[1], INK)
        c.px(gx, gy, BRASS[2])
    # Three red-painted valve wheels on iron spindles
    for vx, vy in ((7, 52), (45, 50), (13, 75)):
        c.ellipse(vx, vy, 6, 6, IRON[0])
        c.rect(vx - 1, vy - 1, 3, 3, IRON[3])
        c.hline(vx - 4, vy, 9, CLARET[3])
        c.vline(vx, vy - 4, 9, CLARET[3])
        ring(c, vx, vy, 5.4, CLARET[5], CLARET[2], CLARET[4])
        c.px(vx, vy, BRASS[4])
    # Mouthpiece horn at chest height, a dark iron flange behind the bell
    c.rect(23, 49, 7, 7, BRASS[2])
    c.vline(23, 49, 7, BRASS[4])
    c.vline(29, 49, 7, BRASS[1])
    c.ellipse(26.5, 62, 9.5, 8.5, IRON[0])
    c.ellipse(26.5, 61.5, 8.5, 7.5, BRASS[2])
    c.ellipse(26, 61, 8, 7, BRASS[4])
    c.ellipse(26.5, 62, 6, 5, BRASS[2])
    c.ellipse(26.5, 62.5, 4.4, 3.6, BRASS[1])
    c.ellipse(26.5, 63, 2.8, 2.2, IRON[0])
    for hx, hy in ((20, 58), (21, 56), (19, 60), (23, 55), (25, 54)):
        c.px(hx, hy, BRASS[5])
    return _finish(c)


def mouthpiece_stand() -> Canvas:
    """A single brass speaking-tube bell on a cast-iron pedestal."""
    c = Canvas(22, 46)
    c.ellipse(11, 43, 8, 2.5, IRON[1])
    c.ellipse(11, 42, 7.5, 2, IRON[3])
    c.ellipse(10, 41.5, 4, 1, IRON[4])
    c.rect(9, 37, 5, 4, IRON[3])
    c.hline(9, 37, 5, IRON[5])
    c.rect(10, 20, 3, 18, IRON[3])
    c.vline(10, 20, 18, IRON[4])
    c.vline(12, 20, 18, IRON[1])
    c.rect(9, 29, 5, 2, IRON[4])
    c.hline(9, 30, 5, IRON[2])
    # Brass neck and bell facing the speaker
    c.rect(9, 16, 5, 5, BRASS[2])
    c.vline(9, 16, 5, BRASS[4])
    c.ellipse(11, 12, 8, 7, BRASS[1])
    c.ellipse(11, 11.5, 7.5, 6.5, BRASS[4])
    c.ellipse(11, 12, 5.2, 4.6, BRASS[2])
    c.ellipse(11, 12.5, 3.6, 3.2, BRASS[1])
    c.ellipse(11, 13, 2.2, 1.9, IRON[0])
    for hx, hy in ((5, 9), (6, 7), (8, 6), (4, 11)):
        c.px(hx, hy, BRASS[5])
    # Whistle plug hanging on its chain
    for ky in range(18, 24, 2):
        c.px(17, ky, IRON[4])
        c.px(17, ky + 1, IRON[2])
    c.rect(16, 24, 3, 3, BRASS[3])
    c.px(16, 24, BRASS[5])
    return _finish(c)


def notice_pillar() -> Canvas:
    """Round advertising-column notice pillar with a small cap."""
    c = Canvas(34, 58)
    # Plinth
    c.ellipse(17, 56, 15, 2, STONE[1])
    cyl(c, 2, 51, 30, 6, STONE)
    c.ellipse(17, 51, 15, 2, STONE[5])
    # Painted iron drum
    cyl(c, 5, 12, 24, 40, BOTTLE)
    shade = mix(INK, VOID, 0.5)

    def tone(col, x: int):
        t = (x - 5 + 0.5) / 24
        k = 0.0 if 0.12 < t < 0.6 else 0.15 if t <= 0.12 else min((t - 0.6) * 1.2, 0.5)
        return mix(col, shade, k)

    sheets = ((7, 15, 10, 12, CREAM[5], INKLINE), (18, 14, 9, 10, OCHRE[5], INKLINE),
              (11, 29, 12, 9, LINEN[5], CLARET[4]), (6, 40, 9, 9, CREAM[4], INKLINE),
              (17, 39, 10, 10, CREAM[5], INKLINE), (24, 27, 4, 11, ROSE[5], INKLINE))
    g = rng(208)
    for x, y, w, h, sheet, ink in sheets:
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                col = sheet if yy < y + h - 1 else mix(sheet, CREAM[1], 0.5)
                c.px(xx, yy, tone(col, xx))
            c.px(x + w, yy, tone(BOTTLE[1], x + w))
        for ly in range(y + 3, y + h - 1, 2):
            lw = max(1, w - 3 - int(g.integers(0, 3)))
            for xx in range(x + 1, x + 1 + lw):
                c.px(xx, ly, tone(ink, xx))
        c.px(x + w // 2, y + 1, tone(BRASS[4], x + w // 2))
    # Cap: cornice ring, low dome and finial
    c.hline(5, 12, 24, BOTTLE[1])
    c.ellipse(17, 8, 11, 5, BOTTLE[3])
    c.ellipse(15, 6.5, 6, 2.5, BOTTLE[4])
    c.px(12, 5, BOTTLE[5])
    cyl(c, 3, 9, 28, 3, BOTTLE)
    c.hline(4, 9, 26, BOTTLE[5])
    c.rect(16, 0, 2, 4, BRASS[3])
    c.px(16, 0, BRASS[5])
    c.hline(15, 3, 4, BRASS[2])
    return _finish(c)


def timetable_board() -> Canvas:
    """Brass-framed timetable card: a ruled grid of five rows, no text."""
    c = Canvas(40, 48)
    c.line(20, 0, 7, 6, IRON[3])
    c.line(20, 0, 32, 6, IRON[3])
    c.px(20, 0, BRASS[5])
    c.rect(2, 6, 36, 42, BRASS[3])
    c.hline(2, 6, 36, BRASS[5])
    c.vline(2, 6, 42, BRASS[4])
    c.vline(37, 6, 42, BRASS[1])
    c.hline(2, 47, 36, BRASS[1])
    c.rect(4, 8, 32, 38, WALNUT[2])
    c.rect(5, 9, 30, 36, CREAM[5])
    c.hline(5, 9, 30, CREAM[3])
    c.vline(5, 9, 36, CREAM[4])
    # Header band (a ruled title bar, no glyphs)
    c.rect(6, 10, 28, 4, CREAM[4])
    c.hline(12, 11, 16, INKLINE)
    c.hline(14, 12, 12, FAINT_INK)
    # Grid: a narrow hour column and four day columns, five rows
    for gy in range(15, 46, 6):
        c.hline(6, gy, 28, FAINT_INK)
    for gx in (6, 11, 17, 23, 29, 33):
        c.vline(gx, 15, 30, FAINT_INK)
    g = rng(209)
    for r in range(5):
        cy = 18 + r * 6
        c.hline(7, cy, 3, INKLINE)
        for col, gx in enumerate((12, 18, 24, 30)):
            if g.random() < 0.8:
                ink = CLARET[4] if (r, col) == (2, 1) else INKLINE
                c.hline(gx, cy - 1, 3 + int(g.integers(0, 3)) if gx < 30 else 2, ink)
                if g.random() < 0.5:
                    c.hline(gx, cy + 1, 2 + int(g.integers(0, 2)), FAINT_INK)
    return _finish(c)


# ---------------------------------------------------------------- catalogue -

FOOTPRINTS: dict[str, tuple[int, int] | None] = {
    "fireplace": (64, 14),
    "long_table": (150, 34),
    "long_bench": (124, 10),
    "upright_piano": (56, 16),
    "piano_stool": (12, 8),
    "chess_table": (24, 12),
    "notice_board": None,
    "shoe_bench": (44, 10),
    "basin_row": (108, 16),
    "bathtub": (64, 22),
    "water_heater": (22, 12),
    "laundry_tub": (32, 12),
    "lecture_desk_row": (136, 18),
    "lectern": (18, 10),
    "blackboard_large": None,
    "relay_column": (36, 20),
    "mouthpiece_stand": (12, 8),
    "notice_pillar": (24, 12),
    "timetable_board": None,
}


def build() -> dict[str, Canvas]:
    out: dict[str, Canvas] = {
        "fireplace": fireplace(),
        "long_table": long_table(),
        "long_bench": long_bench(),
        "upright_piano": upright_piano(),
        "piano_stool": piano_stool(),
        "chess_table": chess_table(),
        "notice_board": notice_board(),
        "shoe_bench": shoe_bench(),
        "basin_row": basin_row(),
        "bathtub": bathtub(),
        "water_heater": water_heater(),
        "laundry_tub": laundry_tub(),
        "lecture_desk_row": lecture_desk_row(),
        "lectern": lectern(),
        "blackboard_large": blackboard_large(),
        "relay_column": relay_column(),
        "mouthpiece_stand": mouthpiece_stand(),
        "notice_pillar": notice_pillar(),
        "timetable_board": timetable_board(),
    }
    assert set(out) == set(FOOTPRINTS)
    return out
