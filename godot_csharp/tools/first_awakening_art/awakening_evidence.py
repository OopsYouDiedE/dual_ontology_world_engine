"""Native 1x pixel recipes for chapter one 《门的这一侧》 evidence.

Field sprites sit on the 32px life-wing map at 2x zoom; card close-ups are
96x72 and shown at 3x on the evidence card. Every pixel is placed here; no
source image is scaled or quantized.
"""
from __future__ import annotations

from .palette import BRASS, CREAM, IRON, LINEN, OAK, STONE, WALNUT, INK
from .pixel import Canvas, hexc, mix
from . import props

SOOT = [hexc("#1c1a1f"), hexc("#2e2b31"), hexc("#46424a"), hexc("#615c63")]
CELADON = [hexc("#3f6a62"), hexc("#5f8f84"), hexc("#86b3a5"), hexc("#b7d6c8")]
PEWTER = [hexc("#3b4048"), hexc("#5d6470"), hexc("#868e99"), hexc("#b4bac2")]
WHITE = [hexc("#9a978f"), hexc("#c7c3b8"), hexc("#e6e2d6"), hexc("#f8f5ec")]
CHALK = hexc("#e4e1d3")
SLATE = [hexc("#1d2226"), hexc("#2b3238"), hexc("#3c454c"), hexc("#55606a")]
PAPER = [hexc("#b9ab88"), hexc("#d8ccac"), hexc("#ece3c8")]
INK_A = hexc("#2b2016")
INK_B = hexc("#3b4a7a")  # the second, bluer ink on the score
SEAL = hexc("#8a3324")


def _finish(c: Canvas) -> Canvas:
    c.outline(-0.68, 0.8)
    c.harden()
    return c


def ring(c: Canvas, cx: int, cy: int, r: int, col) -> None:
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if abs(d - r) < 0.6:
                c.px(x, y, col)


# ----------------------------------------------------------------- field ---

def speaking_tube() -> Canvas:
    """28x44: the old transmitter on its board, with a brass plate engraved ◎ below."""
    base = props.transmitter()
    c = Canvas(28, 44)
    c.blit(base, 0, 0)
    c.rect(7, 35, 14, 9, BRASS[1])
    c.rect(8, 36, 12, 7, BRASS[3])
    c.hline(8, 36, 12, BRASS[5])
    for x, y in ((8, 36), (19, 36), (8, 42), (19, 42)):
        c.px(x, y, BRASS[0])
    ring(c, 14, 39, 2, BRASS[0])
    c.px(14, 39, BRASS[0])
    return _finish(c)


def isolation_door(open_: bool = False) -> Canvas:
    """The existing gate recipe; the closed leaf gets a peephole and the inner bolt at its right edge."""
    c = props.isolation_gate(open_)
    if open_:
        return c
    gx, gy, gw = 18, 14, 60
    # Remove the old inspection hatch: repaint oak boards over it.
    for x in range(gx + 21, gx + 39):
        for y in range(gy + 11, gy + 22):
            c.px(x, y, WALNUT[2] if x % 10 in (8, 9) else WALNUT[3])
    # Peephole: brass ring around glass blackened by soot.
    cx, cy = gx + 30, gy + 16
    c.ellipse(cx + 0.5, cy + 0.5, 3.2, 3.2, BRASS[1])
    c.ellipse(cx + 0.5, cy + 0.5, 2.2, 2.2, SOOT[0])
    c.px(cx - 1, cy - 1, BRASS[4])
    c.px(cx + 1, cy, SOOT[2])
    # Inner bolt shot into its keeper on the right jamb.
    by = gy + 31
    c.rect(gx + gw - 18, by, 20, 4, IRON[3])
    c.hline(gx + gw - 18, by, 20, IRON[5])
    c.hline(gx + gw - 18, by + 3, 20, IRON[1])
    c.rect(gx + gw - 14, by - 3, 3, 4, IRON[4])
    c.rect(gx + gw + 1, by - 2, 4, 8, IRON[2])
    c.hline(gx + gw + 1, by - 2, 4, IRON[5])
    return c


def wedge() -> Canvas:
    """14x8 oak wedge: grey soot on the top face, clean underside, a fresh scuff at the tip."""
    c = Canvas(14, 8)
    c.poly([(0, 6), (13, 2), (13, 7), (0, 7)], OAK[2])
    c.line(0, 6, 13, 2, SOOT[2])
    c.line(1, 5, 12, 2, SOOT[3])
    c.hline(1, 7, 12, OAK[4])
    c.vline(13, 2, 6, OAK[1])
    c.px(1, 6, OAK[6])
    c.px(2, 6, OAK[5])
    return _finish(c)


def fork() -> Canvas:
    """14x6 silver tuning fork lying on the floor."""
    c = Canvas(14, 6)
    silver = [hexc("#5e6670"), hexc("#9aa3ad"), hexc("#d7dde2")]
    c.hline(0, 3, 6, silver[1])
    c.px(0, 3, silver[2])
    c.rect(6, 2, 2, 3, silver[1])
    c.hline(8, 1, 6, silver[1])
    c.hline(8, 4, 6, silver[1])
    c.hline(8, 1, 5, silver[2])
    c.hline(1, 2, 4, silver[2])
    return _finish(c)


def score() -> Canvas:
    """18x13 manuscript page on the desk: staves, two marks, a second ink below."""
    c = Canvas(18, 13)
    c.rect(0, 0, 18, 13, PAPER[2])
    c.hline(0, 12, 18, PAPER[0])
    for y in (2, 4, 6):
        c.hline(2, y, 14, PAPER[0])
    for x, y in ((4, 3), (7, 3), (10, 5), (13, 5)):
        c.px(x, y, INK_A)
        c.px(x + 1, y, INK_A)
    c.hline(3, 9, 9, INK_B)
    c.hline(3, 10, 6, INK_B)
    return _finish(c)


def slate() -> Canvas:
    """20x14 slate in a wooden frame with two chalk lines."""
    c = Canvas(20, 14)
    c.rect(0, 0, 20, 14, WALNUT[3])
    c.rect(1, 1, 18, 12, SLATE[1])
    c.dither(1, 1, 18, 12, SLATE[2], 0.15)
    # G：⊥门 ✕开 ◎
    c.px(3, 3, CHALK); c.hline(3, 4, 2, CHALK)
    c.vline(7, 3, 2, CHALK); c.hline(6, 5, 3, CHALK)
    c.px(10, 3, CHALK); c.px(11, 4, CHALK); c.px(10, 5, CHALK); c.px(12, 3, CHALK); c.px(12, 5, CHALK)
    ring(c, 16, 4, 1, CHALK)
    # second line: magnifier + ⊥✓
    ring(c, 4, 9, 1, CHALK); c.px(6, 11, CHALK)
    c.vline(10, 8, 2, CHALK); c.hline(9, 10, 3, CHALK)
    c.px(13, 9, CHALK); c.px(14, 10, CHALK); c.px(15, 9, CHALK); c.px(16, 8, CHALK)
    return _finish(c)


def cups() -> Canvas:
    """34x12: celadon cup, dented pewter mug marked H, chipped white cup, coarse big bowl-cup."""
    c = Canvas(34, 12)
    c.rect(0, 6, 7, 5, CELADON[1]); c.hline(0, 6, 7, CELADON[3]); c.hline(1, 10, 5, CELADON[0])
    c.rect(8, 3, 7, 8, PEWTER[2]); c.hline(8, 3, 7, PEWTER[3]); c.vline(15, 5, 4, PEWTER[1])
    c.px(10, 6, PEWTER[0]); c.vline(10, 5, 3, PEWTER[0]); c.vline(12, 5, 3, PEWTER[0]); c.px(11, 6, PEWTER[0])
    c.px(13, 9, PEWTER[1])
    c.rect(17, 5, 7, 6, WHITE[2]); c.hline(17, 5, 7, WHITE[3]); c.px(22, 5, WHITE[0]); c.px(23, 5, (0, 0, 0, 0))
    c.rect(25, 2, 9, 9, OAK[3]); c.hline(25, 2, 9, OAK[5]); c.dither(25, 3, 9, 8, OAK[1], 0.2)
    return _finish(c)


def jar() -> Canvas:
    """14x18 glass jar of live starter; the paper label carries 24 short strokes."""
    c = Canvas(14, 18)
    glass = hexc("#9fb3b0", 200)
    c.rect(1, 3, 12, 15, glass)
    c.rect(2, 7, 10, 10, CREAM[4])
    c.dither(2, 7, 10, 10, CREAM[2], 0.25)
    for x, y in ((4, 8), (8, 10), (6, 12), (10, 9)):
        c.px(x, y, CREAM[5])
    c.rect(1, 0, 12, 3, WALNUT[3]); c.hline(1, 0, 12, WALNUT[5])
    c.rect(3, 12, 8, 4, PAPER[2])
    for x in range(4, 10):
        c.px(x, 13, INK_A)
        c.px(x, 14, INK_A)
    c.px(10, 13, INK_A)
    return _finish(c)


def ash_fan() -> Canvas:
    """100x34 soot deposit fanning from the door gap into the hall."""
    c = Canvas(100, 34)
    apex_x, apex_y = 62, 33
    for y in range(34):
        for x in range(100):
            dx, dy = x - apex_x, apex_y - y
            d = (dx * dx + (dy * 1.9) ** 2) ** 0.5
            if dy < 0 or d > 44 or abs(dx) > 8 + dy * 1.45:
                continue
            t = d / 44
            if (x * 7 + y * 13) % 5 < (5 if t < 0.45 else 4 if t < 0.75 else 2):
                c.px(x, y, mix(SOOT[1], SOOT[3], t)[:3] + (int(210 - 120 * t),))
    return c


def boot_print() -> Canvas:
    """16x8 polo-boot sole print, toe pointing right (toward the bolt); heel at left."""
    c = Canvas(16, 8)
    dark = hexc("#2f2b2e")
    light = hexc("#5a5457")
    c.rect(0, 2, 4, 4, dark)            # heel
    c.rect(5, 2, 7, 4, dark)            # sole
    c.ellipse(13, 4, 3, 2.6, dark)      # toe
    for x in range(6, 12, 2):
        c.vline(x, 2, 4, light)
    c.hline(1, 3, 2, light)
    return c


def wet_cloth() -> Canvas:
    """60x7 wrung, wet dish cloth pressed in a straight line along the door bottom."""
    c = Canvas(60, 7)
    base = hexc("#8d8a7e")
    dark = hexc("#5f5d55")
    c.rect(0, 2, 60, 4, base)
    for x in range(0, 60, 6):
        c.line(x, 2, x + 4, 5, dark)
    c.hline(0, 6, 60, hexc("#3f3d38", 160))
    c.hline(0, 2, 60, hexc("#a9a699"))
    return _finish(c)


def pipe_joint() -> Canvas:
    """14x20 copper pipe coming down the wall with a loose, tilted flange."""
    c = Canvas(14, 20)
    c.rect(5, 0, 4, 20, BRASS[2]); c.vline(5, 0, 20, BRASS[4]); c.vline(8, 0, 20, BRASS[0])
    c.rect(2, 8, 10, 3, BRASS[1]); c.hline(2, 8, 10, BRASS[4])
    c.rect(3, 11, 9, 2, BRASS[1]); c.px(11, 12, BRASS[0])
    c.px(1, 10, IRON[3]); c.px(12, 9, IRON[3])
    return _finish(c)


def frame_dent() -> Canvas:
    c = Canvas(4, 4)
    c.px(1, 1, STONE[1]); c.px(2, 1, STONE[2]); c.px(1, 2, STONE[2]); c.px(2, 2, STONE[0])
    return c


def shelf_dust(hand: bool = False) -> Canvas:
    """40x60 overlay for the empty bookcase: dust on each board, clean rectangles where things stood."""
    c = Canvas(40, 60)
    dust = hexc("#b9b1a3", 150)
    for board in (16, 28, 40, 52):
        c.hline(3, board - 1, 34, dust)
        c.hline(3, board - 2, 34, hexc("#b9b1a3", 80))
    clean = {16: [(6, 9), (22, 6)], 28: [(9, 12)], 40: [(5, 5), (26, 8)], 52: [(14, 10)]}
    for board, gaps in clean.items():
        for x, w in gaps:
            for xx in range(x, x + w):
                c.px(xx, board - 1, (0, 0, 0, 0))
                c.px(xx, board - 2, (0, 0, 0, 0))
            # The back panel kept its colour where a frame leaned against it.
            c.rect(x + 1, board - 9, w - 2, 7, hexc("#2b1a16", 70))
    if hand:
        for x, y in ((24, 27), (25, 26), (26, 26), (27, 26), (28, 27), (25, 25), (27, 25)):
            c.px(x, y, hexc("#6d5f52", 220))
    return c


# ----------------------------------------------------------------- cards ---

def _card(bg) -> Canvas:
    c = Canvas(96, 72, bg)
    return c


def card_door() -> Canvas:
    c = _card(STONE[3])
    for y in range(0, 72, 9):
        c.hline(0, y, 96, STONE[2])
    c.rect(10, 6, 76, 66, WALNUT[3])
    for x in range(10, 86, 12):
        c.vline(x, 6, 66, WALNUT[2])
    c.rect(10, 20, 76, 5, IRON[2]); c.hline(10, 20, 76, IRON[4])
    # peephole, glass black with soot
    c.ellipse(48, 13, 6, 6, BRASS[1]); c.ellipse(48, 13, 4.2, 4.2, SOOT[0]); c.px(46, 11, SOOT[3])
    # inner bolt shot home into the keeper
    c.rect(52, 42, 38, 7, IRON[3]); c.hline(52, 42, 38, IRON[5]); c.hline(52, 48, 38, IRON[1])
    c.rect(60, 36, 5, 7, IRON[4])
    c.rect(86, 38, 10, 15, IRON[2]); c.hline(86, 38, 10, IRON[5])
    return c


def card_wedge() -> Canvas:
    c = _card(STONE[4])
    c.dither(0, 40, 96, 32, SOOT[2], 0.25)
    c.poly([(10, 50), (86, 30), (86, 58), (10, 58)], OAK[2])
    c.poly([(10, 50), (86, 30), (86, 34), (10, 53)], SOOT[2])
    c.dither(10, 30, 76, 24, SOOT[1], 0.12)
    c.hline(10, 58, 77, OAK[4])
    c.line(11, 51, 18, 50, OAK[6]); c.line(11, 52, 17, 51, OAK[5])
    return c


def card_print() -> Canvas:
    c = _card(STONE[4])
    c.dither(0, 0, 96, 72, SOOT[2], 0.55)
    dark = hexc("#4a4547")
    c.rect(12, 24, 16, 24, STONE[3]); c.rect(30, 24, 34, 24, STONE[3]); c.ellipse(72, 36, 14, 12, STONE[3])
    for x in range(32, 62, 5):
        c.rect(x, 26, 2, 20, dark)
    for y in range(28, 46, 5):
        c.hline(14, y, 12, dark)
    c.rect(0, 50, 30, 10, hexc("#8d8a7e"))
    c.line(76, 30, 92, 30, SEAL); c.line(88, 27, 92, 30, SEAL); c.line(88, 33, 92, 30, SEAL)
    return c


def card_cloth() -> Canvas:
    c = _card(STONE[4])
    c.rect(0, 0, 96, 22, WALNUT[3])
    for x in range(0, 96, 12):
        c.vline(x, 0, 22, WALNUT[2])
    c.rect(0, 22, 96, 14, hexc("#8d8a7e"))
    for x in range(0, 96, 8):
        c.line(x, 22, x + 6, 35, hexc("#5f5d55"))
    c.dither(40, 38, 56, 30, SOOT[2], 0.5)
    c.rect(66, 34, 22, 8, STONE[3])
    for x in range(68, 86, 4):
        c.vline(x, 36, 6, hexc("#4a4547"))
    return c


def card_fork() -> Canvas:
    c = _card(STONE[4])
    c.rect(70, 0, 26, 72, STONE[2]); c.vline(70, 0, 72, STONE[5])
    c.ellipse(80, 22, 2, 2, STONE[0])
    silver = [hexc("#5e6670"), hexc("#9aa3ad"), hexc("#d7dde2")]
    c.rect(6, 44, 30, 4, silver[1]); c.hline(6, 44, 30, silver[2])
    c.rect(36, 40, 6, 12, silver[1])
    c.rect(42, 38, 26, 4, silver[1]); c.rect(42, 50, 26, 4, silver[1])
    c.hline(42, 38, 26, silver[2]); c.hline(42, 50, 26, silver[2])
    c.px(12, 45, INK_A); c.px(14, 45, INK_A); c.px(17, 45, INK_A); c.px(19, 45, INK_A)
    return c


def card_score() -> Canvas:
    c = _card(PAPER[2])
    for row in (8, 30):
        for y in range(row, row + 10, 2):
            c.hline(4, y, 88, PAPER[0])
        for x, filled in ((14, False), (28, False), (48, True), (66, True)):
            if filled:
                c.ellipse(x, row + 5, 3, 2, INK_A); c.vline(x + 3, row - 4, 9, INK_A)
            else:
                c.px(x, row + 2, INK_A); c.px(x + 1, row + 1, INK_A); c.px(x + 2, row + 2, INK_A); c.px(x + 1, row + 4, INK_A)
        c.hline(44, row + 12, 6, INK_A); c.hline(62, row + 12, 6, INK_A)
    c.hline(70, 26, 18, INK_A)
    for x in range(10, 70, 4):
        c.px(x, 58, INK_B); c.px(x + 1, 59, INK_B); c.px(x + 2, 58, INK_B)
    return c


def card_slate() -> Canvas:
    c = _card(WALNUT[3])
    c.rect(4, 4, 88, 64, SLATE[1]); c.dither(4, 4, 88, 64, SLATE[2], 0.12)
    # G
    c.rect(10, 12, 2, 10, CHALK); c.hline(10, 12, 7, CHALK); c.hline(10, 21, 7, CHALK); c.vline(16, 17, 5, CHALK); c.hline(14, 17, 3, CHALK)
    c.px(20, 14, CHALK); c.px(20, 19, CHALK)
    # ⊥门
    c.vline(28, 11, 9, CHALK); c.hline(24, 20, 9, CHALK)
    c.frame(36, 11, 8, 10, CHALK)
    # ✕开
    c.line(50, 11, 58, 20, CHALK); c.line(58, 11, 50, 20, CHALK)
    c.hline(61, 12, 8, CHALK); c.hline(61, 16, 8, CHALK); c.vline(63, 12, 9, CHALK); c.vline(67, 12, 9, CHALK)
    # ◎
    ring(c, 80, 16, 5, CHALK); ring(c, 80, 16, 2, CHALK)
    # magnifier ⊥✓
    ring(c, 16, 44, 5, CHALK); c.line(20, 48, 25, 53, CHALK)
    c.vline(38, 38, 10, CHALK); c.hline(34, 48, 9, CHALK)
    c.line(48, 44, 51, 48, CHALK); c.line(51, 48, 58, 38, CHALK)
    return c


def card_tube() -> Canvas:
    c = _card(WALNUT[2])
    c.ellipse(30, 30, 18, 18, BRASS[1]); c.ellipse(30, 30, 14, 14, BRASS[3]); c.ellipse(30, 30, 9, 9, IRON[0])
    c.rect(18, 54, 26, 14, BRASS[2]); c.hline(18, 54, 26, BRASS[5])
    ring(c, 31, 61, 4, BRASS[0]); c.px(31, 61, BRASS[0])
    c.rect(60, 0, 8, 72, BRASS[2]); c.vline(60, 0, 72, BRASS[4])
    c.rect(55, 40, 18, 6, BRASS[1]); c.hline(55, 40, 18, BRASS[4])
    c.rect(56, 46, 17, 4, BRASS[1]); c.line(72, 46, 74, 50, BRASS[0])
    c.line(78, 38, 86, 34, SEAL); c.line(78, 50, 86, 54, SEAL)
    return c


def card_cups() -> Canvas:
    c = _card(STONE[5])
    c.rect(0, 50, 96, 22, hexc("#9fb3b0"))
    c.rect(4, 30, 16, 20, CELADON[1]); c.hline(4, 30, 16, CELADON[3]); c.ellipse(12, 46, 6, 2, CELADON[2])
    c.rect(24, 22, 18, 28, PEWTER[2]); c.hline(24, 22, 18, PEWTER[3]); c.ellipse(33, 46, 7, 2, hexc("#c9a76a"))
    c.vline(28, 28, 8, PEWTER[0]); c.vline(32, 28, 8, PEWTER[0]); c.hline(28, 32, 4, PEWTER[0]); c.px(38, 40, PEWTER[0]); c.px(39, 41, PEWTER[0])
    c.rect(46, 28, 18, 22, WHITE[2]); c.hline(46, 28, 18, WHITE[3]); c.rect(58, 28, 4, 3, STONE[5])
    c.rect(68, 18, 24, 32, OAK[3]); c.hline(68, 18, 24, OAK[5]); c.dither(68, 20, 24, 30, OAK[1], 0.2)
    return c


def card_jar() -> Canvas:
    c = _card(WALNUT[2])
    glass = hexc("#9fb3b0", 210)
    c.rect(20, 8, 56, 62, glass)
    c.rect(24, 24, 48, 44, CREAM[4]); c.dither(24, 24, 48, 44, CREAM[2], 0.2)
    for x, y in ((30, 30), (44, 40), (58, 34), (38, 52), (64, 56), (50, 28)):
        c.ellipse(x, y, 2, 2, CREAM[5])
    c.rect(20, 2, 56, 7, WALNUT[4])
    c.rect(26, 44, 44, 16, PAPER[2])
    for i in range(24):
        x = 28 + i * 2 - (i // 12) * 24
        y = 46 if i < 12 else 52
        c.vline(x, y, 5, INK_A)
    c.line(74 - 28, 52, 74 - 26, 57, INK_A)
    return c


def card_shelf() -> Canvas:
    c = _card(WALNUT[1])
    c.rect(0, 40, 96, 6, WALNUT[4])
    c.dither(0, 34, 96, 6, hexc("#b9b1a3"), 0.7)
    for x, w in ((8, 18), (40, 12), (64, 22)):
        c.rect(x, 34, w, 6, WALNUT[1])
        c.rect(x + 2, 8, w - 4, 26, hexc("#2b1a16"))
    for x, y in ((52, 44), (53, 43), (54, 42), (55, 42), (56, 42), (57, 43), (55, 41), (53, 41)):
        c.px(x, y, hexc("#6d5f52"))
    c.dither(0, 0, 96, 34, hexc("#b9b1a3", 60), 0.15)
    return c


def card_coat() -> Canvas:
    c = _card(hexc("#4a3b33"))
    c.rect(10, 0, 50, 72, hexc("#5a3d2b")); c.dither(10, 0, 50, 72, hexc("#47301f"), 0.2)
    c.rect(24, 30, 26, 22, hexc("#47301f"))
    ring(c, 72, 30, 9, BRASS[3]); ring(c, 72, 30, 8, BRASS[2])
    c.ellipse(72, 30, 7, 7, hexc("#a9c1c6", 160))
    c.rect(70, 39, 4, 16, WALNUT[3])
    c.rect(82, 10, 3, 40, hexc("#3a3a3a"))
    return c


FIELD = {
    "speaking_tube": speaking_tube, "isolation_door": isolation_door,
    "isolation_door_open": lambda: isolation_door(True), "wedge": wedge, "fork": fork, "score": score,
    "slate": slate, "cups": cups, "jar": jar, "ash_fan": ash_fan, "boot_print": boot_print,
    "wet_cloth": wet_cloth, "pipe_joint": pipe_joint, "frame_dent": frame_dent,
    "shelf_dust": shelf_dust, "shelf_dust_hand": lambda: shelf_dust(True),
}
CARDS = {
    "card_door": card_door, "card_wedge": card_wedge, "card_print": card_print, "card_cloth": card_cloth,
    "card_fork": card_fork, "card_score": card_score, "card_slate": card_slate, "card_tube": card_tube,
    "card_cups": card_cups, "card_jar": card_jar, "card_shelf": card_shelf, "card_coat": card_coat,
}


def build() -> dict[str, Canvas]:
    out = {name: recipe() for name, recipe in FIELD.items()}
    out.update({name: recipe() for name, recipe in CARDS.items()})
    return out
