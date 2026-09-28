"""Native 1x furniture and object sprites. Key light from the upper left.

Each sprite is drawn on a transparent canvas whose bottom-centre is the floor
anchor. Evidence that never moves (notebooks, tools, cups, rations) is drawn
into the furniture that carries it so it can never sort apart from its table.
"""
from __future__ import annotations

from .palette import (BRASS, CLARET, CREAM, DORMS, FLAME, FOG, IRON, LEAF, LINEN, OCHRE, STONE, TEAL,
                      BOTTLE, TILE, WALNUT, INK, SAGE, ROSE, PLUM)
from .pixel import Canvas, mix, rng, shift

SHADE = (22, 14, 26, 255)


def _finish(c: Canvas) -> Canvas:
    c.outline(-0.68, 0.8)
    c.harden()
    return c


# ---------------------------------------------------------------- helpers ---

def slab(c: Canvas, x: int, y: int, w: int, top: int, front: int, ramp, grain: bool = True, seed: int = 1) -> None:
    """A 3/4 box: lit top surface above a shaded front face."""
    c.rect(x, y, w, top, ramp[4])
    c.hline(x, y, w, ramp[5])
    c.px(x, y, ramp[4])
    c.hline(x + 1, y + top - 1, w - 2, mix(ramp[4], ramp[3], 0.5))
    c.rect(x, y + top, w, front, ramp[3])
    c.hline(x, y + top, w, ramp[2])
    c.hline(x, y + top + front - 1, w, ramp[1])
    if grain:
        g = rng(seed)
        for _ in range(w * top // 18):
            gx, gy = x + int(g.integers(1, max(2, w - 5))), y + int(g.integers(1, max(2, top - 1)))
            c.hline(gx, gy, int(g.integers(2, 6)), mix(ramp[4], ramp[3], 0.55))


def drawer(c: Canvas, x: int, y: int, w: int, h: int, ramp) -> None:
    c.rect(x, y, w, h, ramp[3])
    c.hline(x, y, w, ramp[1])
    c.vline(x, y, h, ramp[1])
    c.hline(x + 1, y + h - 1, w - 1, ramp[4])
    c.vline(x + w - 1, y + 1, h - 1, ramp[4])
    kx = x + w // 2
    c.px(kx - 1, y + h // 2, BRASS[3])
    c.px(kx, y + h // 2, BRASS[4])


def leg(c: Canvas, x: int, y: int, h: int, ramp) -> None:
    c.vline(x, y, h, ramp[2])
    c.vline(x + 1, y, h, ramp[1])
    c.px(x, y + h - 1, ramp[1])


def book_row(c: Canvas, x: int, y: int, w: int, h: int, seed: int) -> None:
    g = rng(seed)
    spines = [CLARET, BOTTLE, OCHRE, TEAL, PLUM, LINEN, ROSE, SAGE]
    bx = x
    while bx < x + w:
        bw = int(g.choice([2, 2, 3, 3, 4]))
        bw = min(bw, x + w - bx)
        bh = h - int(g.choice([0, 0, 1, 2, 3]))
        ramp = spines[int(g.integers(0, len(spines)))]
        if g.random() < 0.12 and bw >= 3:
            # A leaning volume
            c.rect(bx, y + h - bh + 2, bw, bh - 2, ramp[3])
            c.px(bx + bw - 1, y + h - bh + 1, ramp[3])
        else:
            c.rect(bx, y + h - bh, bw, bh, ramp[3])
            c.vline(bx, y + h - bh, bh, ramp[4])
            c.hline(bx, y + h - bh + 2, bw, ramp[1] if bw > 2 else ramp[2])
            if bh > 5:
                c.hline(bx, y + h - 3, bw, OCHRE[4] if g.random() < 0.5 else ramp[5])
        bx += bw


# ------------------------------------------------------------------ rooms ---

def bed(room: int) -> Canvas:
    """36x64 walnut bed. The quilt carries the occupant's colour."""
    quilt = DORMS[room]["quilt"]
    c = Canvas(36, 64)
    # Headboard: carved panel with finials
    c.rect(1, 2, 34, 15, WALNUT[3])
    c.rect(3, 4, 30, 10, WALNUT[2])
    c.rect(4, 5, 28, 8, WALNUT[3])
    c.hline(4, 5, 28, WALNUT[4])
    c.poly([(8, 5), (18, 1), (28, 5)], WALNUT[3])
    c.line(9, 5, 18, 2, WALNUT[5])
    c.hline(1, 2, 34, WALNUT[5])
    for fx in (0, 33):
        c.rect(fx, 0, 3, 18, WALNUT[4])
        c.vline(fx, 0, 18, WALNUT[5])
        c.rect(fx, -1 + 1, 3, 2, BRASS[4])
    c.rect(16, 7, 4, 3, BRASS[3])
    c.px(17, 7, BRASS[5])
    # Mattress and sheet
    c.rect(2, 17, 32, 42, LINEN[4])
    c.vline(2, 17, 42, LINEN[2])
    c.vline(33, 17, 42, LINEN[2])
    # Pillow
    c.rect(6, 18, 24, 9, LINEN[5])
    c.hline(7, 26, 22, LINEN[3])
    c.vline(6, 19, 7, LINEN[3])
    c.vline(29, 19, 7, LINEN[3])
    c.hline(10, 21, 8, LINEN[3])
    # Turned-down sheet and quilt
    c.rect(2, 28, 32, 3, LINEN[5])
    c.hline(2, 31, 32, LINEN[2])
    c.rect(2, 32, 32, 26, quilt[3])
    c.hline(2, 32, 32, quilt[5])
    for qy in range(36, 57, 7):
        c.hline(3, qy, 30, quilt[2])
    for qx in range(9, 32, 8):
        c.vline(qx, 33, 24, quilt[2])
    for qy in range(36, 57, 7):
        for qx in range(9, 32, 8):
            c.px(qx, qy, DORMS[room]["accent"][4])
    c.vline(2, 32, 26, quilt[4])
    c.rect(2, 56, 32, 2, quilt[1])
    # Quilt drape over the side rails
    c.vline(1, 30, 26, quilt[2])
    c.vline(34, 30, 26, quilt[1])
    # Footboard
    c.rect(1, 57, 34, 6, WALNUT[3])
    c.hline(1, 57, 34, WALNUT[5])
    c.hline(1, 62, 34, WALNUT[1])
    for fx in (0, 33):
        c.rect(fx, 55, 3, 9, WALNUT[4])
        c.vline(fx, 55, 9, WALNUT[5])
    return _finish(c)


def nightstand() -> Canvas:
    c = Canvas(20, 36)
    slab(c, 1, 16, 18, 5, 14, WALNUT, seed=4)
    drawer(c, 3, 23, 14, 5, WALNUT)
    leg(c, 2, 34, 2, WALNUT)
    leg(c, 16, 34, 2, WALNUT)
    # Oil lamp: brass font, glass chimney, lit flame
    c.rect(7, 13, 6, 4, BRASS[3])
    c.hline(7, 13, 6, BRASS[5])
    c.rect(6, 16, 8, 1, BRASS[2])
    c.rect(8, 5, 4, 8, (232, 226, 206, 255))
    c.vline(8, 5, 8, (196, 186, 164, 255))
    c.rect(9, 8, 2, 4, FLAME[2])
    c.px(9, 7, FLAME[3])
    c.px(10, 8, FLAME[4])
    c.hline(8, 4, 4, BRASS[3])
    return _finish(c)


def desk(room: int) -> Canvas:
    """58x38 writing desk, green leather inset; the occupant's work lies on it."""
    c = Canvas(58, 38)
    slab(c, 1, 4, 56, 14, 16, WALNUT, seed=10 + room)
    c.rect(5, 6, 48, 10, BOTTLE[3])
    c.frame(5, 6, 48, 10, OCHRE[2])
    c.hline(6, 7, 46, BOTTLE[4])
    # Kneehole and drawers
    drawer(c, 3, 20, 16, 5, WALNUT)
    drawer(c, 3, 26, 16, 5, WALNUT)
    drawer(c, 39, 20, 16, 5, WALNUT)
    drawer(c, 39, 26, 16, 5, WALNUT)
    c.rect(20, 20, 18, 12, WALNUT[1])
    c.hline(20, 20, 18, WALNUT[0])
    for lx in (1, 55):
        c.rect(lx, 32, 2, 6, WALNUT[2])
    c.vline(1, 18, 16, WALNUT[4])
    # Ink stand
    c.rect(48, 3, 5, 3, INK)
    c.px(49, 2, BRASS[4])
    c.line(51, 1, 53, -1, CREAM[5])
    if room == 0:  # notebook with a pencil
        c.rect(12, 5, 14, 9, CREAM[5])
        c.vline(19, 5, 9, CREAM[2])
        for ly in (7, 9, 11):
            c.hline(13, ly, 5, FOG[2])
            c.hline(21, ly, 4, FOG[2])
        c.line(27, 12, 32, 8, OCHRE[4])
        c.rect(34, 8, 8, 5, CREAM[3])
    elif room == 1:  # engineer: callipers, set square, a folding rule
        c.poly([(9, 14), (9, 5), (19, 14)], CREAM[4])
        c.poly([(11, 12), (11, 9), (14, 12)], BOTTLE[3])
        c.hline(22, 9, 16, OCHRE[4])
        c.hline(22, 10, 16, OCHRE[2])
        for tx in range(23, 38, 3):
            c.px(tx, 9, OCHRE[1])
        c.line(32, 6, 40, 13, IRON[5])
        c.line(34, 6, 40, 11, IRON[4])
        c.rect(42, 10, 4, 3, BRASS[3])
    elif room == 2:  # a chipped cup and a wrapped sandwich of papers
        c.rect(14, 6, 16, 9, CREAM[4])
        c.rect(16, 5, 16, 9, CREAM[5])
        c.hline(17, 8, 12, FOG[2])
        c.hline(17, 10, 10, FOG[2])
        c.ellipse(39, 10, 4, 3, CREAM[5])
        c.ellipse(39, 9, 3, 1.5, (120, 84, 60, 255))
        c.px(42, 8, (0, 0, 0, 0))
        c.px(41, 7, (0, 0, 0, 0))
    else:  # musician: sheet music and a tuning fork
        c.rect(10, 5, 13, 10, CREAM[5])
        c.rect(24, 6, 13, 9, CREAM[4])
        for ly in (7, 9, 11, 13):
            c.hline(11, ly, 11, (70, 60, 72, 255))
            c.hline(25, ly, 11, (70, 60, 72, 255))
        for nx, ny in ((13, 8), (16, 10), (19, 7), (27, 11), (31, 9)):
            c.px(nx, ny, INK)
            c.px(nx + 1, ny - 1, INK)
        c.vline(41, 6, 7, IRON[5])
        c.vline(43, 6, 7, IRON[5])
        c.hline(41, 12, 3, IRON[4])
    return _finish(c)


def chair_back(coat: bool = False) -> Canvas:
    """Seen from behind: the backrest faces the viewer."""
    c = Canvas(18, 28)
    slab(c, 2, 14, 14, 4, 3, WALNUT, grain=False)
    c.rect(3, 13, 12, 3, CLARET[3])
    for lx in (2, 14):
        c.rect(lx, 21, 2, 7, WALNUT[2])
    c.rect(3, 2, 12, 12, WALNUT[3])
    c.rect(5, 4, 8, 8, CLARET[3])
    c.hline(5, 4, 8, CLARET[4])
    c.hline(3, 2, 12, WALNUT[5])
    c.rect(2, 1, 2, 14, WALNUT[4])
    c.rect(14, 1, 2, 14, WALNUT[2])
    c.px(2, 0, BRASS[4])
    c.px(15, 0, BRASS[3])
    if coat:
        # Detective's coat hung over the backrest, sleeve worn shiny.
        coat_ramp = [shift(WALNUT[3], -0.3), WALNUT[2], (92, 62, 46, 255), (112, 78, 56, 255), (134, 96, 68, 255)]
        c.rect(1, 1, 16, 5, coat_ramp[3])
        c.hline(1, 1, 16, coat_ramp[4])
        c.rect(1, 6, 5, 16, coat_ramp[2])
        c.rect(12, 6, 5, 17, coat_ramp[2])
        c.vline(1, 6, 16, coat_ramp[3])
        c.vline(16, 6, 17, coat_ramp[1])
        c.rect(12, 20, 5, 3, coat_ramp[4])
        c.px(3, 3, BRASS[4])
    return _finish(c)


def chair_front() -> Canvas:
    """Facing the viewer: seat, turned front legs and the backrest behind."""
    c = Canvas(18, 28)
    c.rect(3, 1, 12, 12, WALNUT[3])
    c.rect(5, 3, 8, 8, CLARET[4])
    c.hline(5, 3, 8, CLARET[5])
    c.hline(3, 1, 12, WALNUT[5])
    c.rect(2, 0, 2, 14, WALNUT[4])
    c.rect(14, 0, 2, 14, WALNUT[2])
    slab(c, 1, 13, 16, 6, 3, WALNUT, grain=False)
    c.rect(2, 13, 14, 5, CLARET[3])
    c.hline(2, 13, 14, CLARET[4])
    for lx in (2, 14):
        c.rect(lx, 21, 2, 7, WALNUT[3])
        c.px(lx, 24, WALNUT[5])
    return _finish(c)


def bookcase(seed: int) -> Canvas:
    c = Canvas(40, 60)
    c.rect(0, 2, 40, 58, WALNUT[3])
    c.rect(0, 0, 40, 4, WALNUT[4])
    c.hline(0, 0, 40, WALNUT[5])
    c.hline(0, 3, 40, WALNUT[2])
    c.rect(3, 5, 34, 47, WALNUT[1])
    for i, sy in enumerate((5, 17, 29, 41)):
        book_row(c, 3, sy + 1, 34, 10, seed * 13 + i)
        c.rect(3, sy + 11, 34, 1, WALNUT[4])
        c.hline(3, sy + 12, 34, WALNUT[2])
    # Ornaments
    c.rect(28, 41, 5, 7, BRASS[3])
    c.hline(28, 41, 5, BRASS[5])
    c.rect(6, 17, 4, 5, (220, 214, 200, 255))
    c.vline(1, 2, 56, WALNUT[5])
    c.vline(38, 2, 56, WALNUT[2])
    c.rect(0, 53, 40, 7, WALNUT[3])
    c.hline(0, 53, 40, WALNUT[5])
    drawer(c, 3, 54, 16, 5, WALNUT)
    drawer(c, 21, 54, 16, 5, WALNUT)
    return _finish(c)


def wardrobe() -> Canvas:
    c = Canvas(40, 64)
    c.rect(1, 4, 38, 56, WALNUT[3])
    # Cornice with a gentle pediment
    c.rect(0, 1, 40, 5, WALNUT[4])
    c.hline(0, 1, 40, WALNUT[5])
    c.hline(0, 5, 40, WALNUT[1])
    c.poly([(10, 1), (20, -3), (30, 1)], WALNUT[4])
    c.line(11, 1, 20, -2, WALNUT[5])
    for dx in (3, 21):
        c.rect(dx, 8, 16, 42, WALNUT[3])
        c.frame(dx, 8, 16, 42, WALNUT[1])
        c.hline(dx + 1, 9, 14, WALNUT[5])
        c.vline(dx + 1, 9, 40, WALNUT[4])
        c.rect(dx + 3, 11, 10, 14, WALNUT[2])
        c.rect(dx + 4, 12, 8, 12, WALNUT[3])
        c.rect(dx + 3, 29, 10, 18, WALNUT[2])
        c.rect(dx + 4, 30, 8, 16, WALNUT[3])
    c.vline(20, 8, 42, WALNUT[0])
    c.rect(17, 27, 2, 4, BRASS[4])
    c.rect(22, 27, 2, 4, BRASS[3])
    c.rect(1, 51, 38, 8, WALNUT[3])
    c.hline(1, 51, 38, WALNUT[5])
    drawer(c, 3, 52, 34, 6, WALNUT)
    c.rect(0, 59, 40, 3, WALNUT[2])
    c.hline(0, 59, 40, WALNUT[4])
    for lx in (1, 36):
        c.rect(lx, 62, 3, 2, WALNUT[1])
    c.vline(1, 4, 56, WALNUT[5])
    return _finish(c)


def linen_chest(room: int) -> Canvas:
    c = Canvas(38, 26)
    slab(c, 1, 6, 36, 7, 12, WALNUT, seed=20 + room)
    c.rect(1, 13, 36, 2, BRASS[2])
    c.hline(1, 13, 36, BRASS[4])
    for bx in (6, 30):
        c.rect(bx, 6, 2, 19, BRASS[2])
        c.vline(bx, 6, 19, BRASS[4])
    c.rect(17, 14, 4, 4, BRASS[3])
    c.px(18, 16, INK)
    c.rect(1, 24, 36, 2, WALNUT[1])
    # Folded blanket on the lid, the room's colour
    quilt = DORMS[room]["quilt"]
    c.rect(6, 2, 24, 7, quilt[3])
    c.hline(6, 2, 24, quilt[5])
    c.hline(6, 5, 24, quilt[2])
    c.hline(6, 8, 24, quilt[1])
    return _finish(c)


def fern(seed: int = 1) -> Canvas:
    g = rng(seed)
    c = Canvas(24, 34)
    # Glazed pot
    c.rect(6, 23, 12, 10, CLARET[3])
    c.rect(5, 22, 14, 3, CLARET[4])
    c.hline(5, 22, 14, CLARET[5])
    c.vline(7, 25, 7, CLARET[4])
    c.hline(7, 33, 10, CLARET[1])
    c.rect(7, 22, 10, 1, (60, 40, 30, 255))
    # Fronds rise from the crown, then arch and droop.
    import math
    for i in range(11):
        side = -1 if i % 2 else 1
        spread = 0.25 + (i // 2) * 0.28
        length = 10 + int(g.integers(0, 5)) - (i // 2)
        for s in range(length):
            t = s / length
            x = 12 + side * math.sin(spread) * s * (0.6 + t)
            y = 22 - math.cos(spread) * s * 1.1 + t * t * 9
            tone = LEAF[2] if t < 0.3 else LEAF[4] if (i + s) % 3 else LEAF[3]
            c.px(int(round(x)), int(round(y)), tone)
            if 0.2 < t < 0.95:
                c.px(int(round(x)) + side, int(round(y)) + 1, LEAF[3])
                if s % 2:
                    c.px(int(round(x)) - side, int(round(y)), LEAF[5] if side > 0 else LEAF[4])
    return _finish(c)


def paper_basket() -> Canvas:
    c = Canvas(14, 16)
    c.rect(2, 5, 10, 11, OCHRE[2])
    for x in range(2, 12, 2):
        c.vline(x, 5, 11, OCHRE[3])
    for y in (7, 10, 13):
        c.hline(2, y, 10, OCHRE[1])
    c.rect(1, 4, 12, 2, OCHRE[4])
    # A crumpled wrapper sticking out of the top.
    c.poly([(4, 5), (6, 1), (9, 2), (10, 5)], CREAM[5])
    c.px(6, 3, (70, 66, 62, 255))
    c.px(7, 2, (70, 66, 62, 255))
    return _finish(c)


def wall_clock() -> Canvas:
    c = Canvas(18, 34)
    c.rect(2, 3, 14, 29, WALNUT[3])
    c.poly([(2, 4), (9, -1), (16, 4)], WALNUT[4])
    c.ellipse(9, 10, 6, 6, CREAM[5])
    c.ellipse(9, 10, 6, 6, CREAM[5])
    c.frame(4, 5, 11, 11, BRASS[2])
    for a in range(12):
        import math
        c.px(int(9 + math.cos(a * math.pi / 6) * 4.5), int(10 + math.sin(a * math.pi / 6) * 4.5), INK)
    c.line(9, 10, 9, 6, INK)
    c.line(9, 10, 12, 11, INK)
    c.rect(5, 18, 8, 12, WALNUT[1])
    c.vline(9, 18, 8, BRASS[3])
    c.ellipse(9, 27, 2.5, 2.5, BRASS[4])
    c.rect(1, 31, 16, 3, WALNUT[4])
    return _finish(c)


def floor_lamp() -> Canvas:
    c = Canvas(18, 50)
    c.ellipse(9, 47, 6, 2.5, BRASS[2])
    c.hline(4, 46, 10, BRASS[4])
    c.vline(8, 16, 30, BRASS[3])
    c.vline(9, 16, 30, BRASS[1])
    for ky in (24, 34):
        c.rect(7, ky, 4, 2, BRASS[4])
    # Fringed shade, lit from inside
    c.poly([(3, 12), (6, 3), (12, 3), (15, 12)], (236, 206, 150, 255))
    c.hline(6, 3, 6, (250, 234, 196, 255))
    c.hline(3, 12, 13, OCHRE[3])
    for fx in range(3, 16, 2):
        c.px(fx, 13, OCHRE[2])
    c.rect(7, 13, 4, 3, FLAME[3])
    c.vline(4, 6, 6, (212, 170, 110, 255))
    return _finish(c)


# ---------------------------------------------------------------- public ----

def study_table() -> Canvas:
    """132x62 long library table with baize; the case papers lie on it."""
    c = Canvas(132, 64)
    slab(c, 2, 2, 128, 42, 10, WALNUT, seed=30)
    c.rect(8, 6, 116, 34, BOTTLE[3])
    c.frame(8, 6, 116, 34, OCHRE[2])
    c.frame(10, 8, 112, 30, BOTTLE[2])
    c.dither(8, 6, 116, 34, BOTTLE[4], 0.12)
    c.hline(9, 7, 114, BOTTLE[4])
    # Apron with carved panels
    for px in range(8, 124, 24):
        c.rect(px, 46, 18, 5, WALNUT[2])
        c.hline(px, 46, 18, WALNUT[1])
        c.hline(px, 50, 18, WALNUT[4])
    # Turned legs
    for lx in (3, 125):
        c.rect(lx, 54, 4, 10, WALNUT[3])
        c.hline(lx, 57, 4, WALNUT[5])
        c.hline(lx, 61, 4, WALNUT[1])
        c.px(lx, 54, WALNUT[5])
    # Study papers: stacked measurement sheets, a ruler and notched battens.
    for i, (px, py) in enumerate(((22, 12), (24, 14), (21, 17))):
        c.rect(px, py, 20, 14, CREAM[5] if i != 1 else CREAM[4])
        c.hline(px, py + 13, 20, CREAM[2])
    for ly in range(20, 30, 2):
        c.hline(24, ly, 14, FOG[2])
    c.line(24, 24, 36, 24, CLARET[3])
    c.hline(46, 30, 26, OCHRE[4])
    c.hline(46, 31, 26, OCHRE[2])
    for tx in range(47, 72, 3):
        c.px(tx, 30, OCHRE[1])
    c.rect(48, 13, 18, 4, WALNUT[5])
    c.rect(52, 13, 3, 2, WALNUT[2])
    c.rect(60, 13, 3, 2, WALNUT[2])
    c.rect(70, 16, 8, 8, WALNUT[4])
    c.px(72, 18, INK)
    c.px(75, 21, INK)
    # Old exercise book with a torn page edge.
    c.rect(82, 18, 22, 14, CREAM[4])
    c.vline(93, 18, 14, CREAM[1])
    for ly in range(21, 30, 2):
        c.hline(84, ly, 8, FOG[2])
        c.hline(95, ly, 7, FOG[2])
    for ty in range(18, 32, 2):
        c.px(104, ty, CREAM[1])
        c.px(103, ty + 1, (0, 0, 0, 0))
    # The musician's box and the pale scuff it leaves.
    c.rect(106, 9, 12, 8, WALNUT[5])
    c.hline(106, 9, 12, WALNUT[6])
    c.rect(106, 17, 12, 3, WALNUT[2])
    c.px(111, 12, BRASS[4])
    c.rect(106, 22, 12, 5, mix(BOTTLE[3], CREAM[3], 0.35))
    for sx in (107, 110, 114):
        c.px(sx, 23 + sx % 3, BOTTLE[1])
    # Candlestick
    c.rect(14, 30, 4, 3, BRASS[3])
    c.rect(15, 25, 2, 5, CREAM[5])
    c.px(15, 24, FLAME[3])
    return _finish(c)


def sideboard() -> Canvas:
    """82x60 dresser: plate rack above, cupboards below."""
    c = Canvas(82, 62)
    c.rect(4, 0, 74, 30, WALNUT[2])
    c.rect(2, 0, 78, 3, WALNUT[4])
    c.hline(2, 0, 78, WALNUT[5])
    for sy in (13, 26):
        c.rect(6, sy, 70, 2, WALNUT[4])
        c.hline(6, sy + 2, 70, WALNUT[1])
    for i, px in enumerate(range(9, 74, 10)):
        c.ellipse(px + 3, 8, 4, 4.5, TILE[5])
        c.ellipse(px + 3, 8, 2, 2.5, (126, 150, 176, 255))
        c.px(px + 1, 5, (255, 255, 255, 255))
        c.rect(px, 18, 7, 7, TILE[4] if i % 2 else (190, 206, 214, 255))
        c.rect(px + 1, 17, 5, 1, TILE[5])
        c.vline(px + 1, 19, 5, TILE[5])
    c.vline(4, 0, 30, WALNUT[4])
    c.vline(77, 0, 30, WALNUT[1])
    slab(c, 0, 30, 82, 6, 22, WALNUT, seed=33)
    for dx in (3, 42):
        c.rect(dx, 38, 37, 18, WALNUT[3])
        c.frame(dx, 38, 37, 18, WALNUT[1])
        c.hline(dx + 1, 39, 35, WALNUT[5])
        c.rect(dx + 4, 41, 29, 12, WALNUT[2])
        c.rect(dx + 5, 42, 27, 10, WALNUT[3])
        c.rect(dx + (33 if dx < 40 else 2), 45, 2, 3, BRASS[4])
    c.rect(1, 58, 80, 4, WALNUT[1])
    # A lidded jug on the counter
    c.rect(60, 25, 8, 7, CREAM[5])
    c.px(68, 27, CREAM[4])
    c.rect(20, 27, 10, 4, BRASS[3])
    return _finish(c)


def worktable() -> Canvas:
    """96x42 scrubbed kitchen table; four wrapped rations wait on it."""
    c = Canvas(96, 44)
    top = [shift(LINEN[1], -0.2), LINEN[0], LINEN[1], LINEN[2], LINEN[3], LINEN[4]]
    slab(c, 1, 2, 94, 26, 8, top, seed=40)
    for gx in range(6, 92, 11):
        c.vline(gx, 3, 24, mix(LINEN[3], LINEN[2], 0.5))
    c.rect(1, 36, 94, 2, WALNUT[2])
    for lx in (3, 89):
        c.rect(lx, 34, 4, 10, WALNUT[3])
        c.vline(lx, 34, 10, WALNUT[5])
    # Four rations in oiled paper, one label each.
    for i in range(4):
        rx, ry = 10 + i * 13, 8
        c.rect(rx, ry, 11, 8, OCHRE[4])
        c.hline(rx, ry, 11, OCHRE[5])
        c.hline(rx, ry + 7, 11, OCHRE[2])
        c.vline(rx + 5, ry, 8, OCHRE[3])
        c.rect(rx + 2, ry + 3, 7, 3, CREAM[5])
        c.hline(rx + 3, ry + 4, 4, (90, 80, 70, 255))
    # Bread board, knife, water jug
    c.ellipse(72, 13, 11, 6, WALNUT[5])
    c.ellipse(72, 12, 9, 4.5, WALNUT[6])
    c.ellipse(71, 12, 5, 3, OCHRE[4])
    c.hline(66, 19, 9, IRON[5])
    c.rect(84, 8, 6, 10, TILE[4])
    c.hline(84, 8, 6, TILE[5])
    c.rect(90, 11, 1, 4, TILE[3])
    return _finish(c)


def washstand() -> Canvas:
    """50x46 marble-topped sink cabinet with a brass tap."""
    c = Canvas(50, 48)
    marble = [STONE[2], STONE[3], STONE[4], TILE[3], TILE[4], TILE[5]]
    slab(c, 1, 10, 48, 14, 20, [WALNUT[0], WALNUT[1], WALNUT[2], WALNUT[3], TILE[4], TILE[5]], grain=False)
    c.rect(1, 10, 48, 14, marble[4])
    c.hline(1, 10, 48, marble[5])
    c.line(5, 20, 14, 13, marble[2])
    c.ellipse(25, 17, 13, 5.5, TILE[2])
    c.ellipse(25, 17, 11, 4.2, (150, 178, 190, 255))
    c.ellipse(24, 16, 7, 2.2, (186, 208, 214, 255))
    # Folded damp cloth with a grey-black stain
    c.rect(40, 12, 7, 9, LINEN[3])
    c.hline(40, 12, 7, LINEN[5])
    c.px(42, 17, (58, 60, 58, 255))
    c.px(43, 18, (74, 76, 72, 255))
    c.px(44, 17, (74, 76, 72, 255))
    # Brass tap on a tiled splashback
    c.rect(6, 0, 38, 11, TILE[4])
    for tx in range(6, 44, 6):
        c.vline(tx, 0, 11, TILE[2])
    c.hline(6, 5, 38, TILE[2])
    c.rect(23, 2, 4, 3, BRASS[3])
    c.rect(24, 5, 2, 7, BRASS[3])
    c.px(24, 5, BRASS[5])
    c.rect(22, 11, 4, 2, BRASS[4])
    c.rect(20, 1, 10, 2, BRASS[2])
    c.px(20, 1, BRASS[5])
    for dx in (4, 26):
        c.rect(dx, 26, 20, 16, WALNUT[3])
        c.frame(dx, 26, 20, 16, WALNUT[1])
        c.hline(dx + 1, 27, 18, WALNUT[5])
    c.px(22, 33, BRASS[4])
    c.px(28, 33, BRASS[4])
    c.rect(1, 44, 48, 4, WALNUT[1])
    return _finish(c)


def side_table() -> Canvas:
    """38x32 round side table; the cup and its broken shard."""
    c = Canvas(38, 34)
    c.ellipse(19, 10, 18, 9, WALNUT[4])
    c.ellipse(19, 9, 17, 8, WALNUT[5])
    c.ellipse(18, 8, 12, 5, mix(WALNUT[5], WALNUT[6], 0.4))
    c.rect(2, 10, 34, 3, WALNUT[3])
    c.ellipse(19, 13, 17, 3, WALNUT[2])
    c.rect(18, 15, 3, 15, WALNUT[3])
    c.vline(18, 15, 15, WALNUT[5])
    c.poly([(9, 32), (19, 27), (29, 32), (26, 33), (19, 30), (12, 33)], WALNUT[2])
    # Chipped cup and a curved shard beside it
    c.ellipse(14, 7, 4, 3, TILE[5])
    c.ellipse(14, 6, 3, 1.5, (112, 80, 60, 255))
    c.px(17, 5, (0, 0, 0, 0))
    c.px(16, 4, WALNUT[5])
    c.px(18, 7, TILE[4])
    c.px(19, 7, TILE[4])
    c.poly([(24, 8), (28, 7), (29, 9), (25, 10)], TILE[5])
    c.px(25, 9, TILE[3])
    return _finish(c)


def range_stove() -> Canvas:
    c = Canvas(44, 52)
    c.rect(18, 0, 8, 14, IRON[2])
    c.vline(18, 0, 14, IRON[4])
    slab(c, 1, 14, 42, 10, 24, IRON, grain=False)
    c.ellipse(12, 18, 6, 3, IRON[1])
    c.ellipse(30, 18, 6, 3, IRON[1])
    c.ellipse(30, 16, 6, 3, BRASS[3])
    c.rect(24, 12, 12, 4, BRASS[3])
    c.hline(24, 12, 12, BRASS[5])
    c.px(36, 13, BRASS[2])
    c.rect(5, 27, 16, 10, IRON[2])
    c.frame(5, 27, 16, 10, IRON[4])
    c.rect(8, 30, 10, 5, (180, 90, 40, 255))
    c.dither(8, 30, 10, 5, (255, 180, 90, 255), 0.4)
    c.rect(24, 27, 16, 10, IRON[2])
    c.frame(24, 27, 16, 10, IRON[4])
    c.hline(4, 39, 36, BRASS[4])
    c.rect(1, 48, 42, 4, IRON[1])
    return _finish(c)


def armchair() -> Canvas:
    c = Canvas(30, 32)
    c.rect(4, 0, 22, 16, CLARET[3])
    c.hline(5, 0, 20, CLARET[5])
    for bx in range(8, 24, 5):
        c.px(bx, 5, CLARET[1])
        c.px(bx + 2, 10, CLARET[1])
    c.rect(0, 10, 7, 16, CLARET[4])
    c.rect(23, 10, 7, 16, CLARET[2])
    c.hline(0, 10, 7, CLARET[5])
    c.rect(6, 15, 18, 9, CLARET[4])
    c.hline(6, 15, 18, CLARET[5])
    c.rect(1, 24, 28, 5, CLARET[2])
    for lx in (2, 26):
        c.rect(lx, 29, 2, 3, WALNUT[2])
    return _finish(c)


def transmitter() -> Canvas:
    """26x34 brass speaking tube with a pull handle, mounted on walnut."""
    c = Canvas(28, 36)
    c.rect(6, 2, 12, 32, WALNUT[3])
    c.frame(6, 2, 12, 32, WALNUT[1])
    c.vline(7, 3, 30, WALNUT[5])
    c.rect(10, 8, 4, 12, BRASS[3])
    c.vline(10, 8, 12, BRASS[5])
    # Horn flaring toward the room
    c.poly([(12, 18), (26, 12), (27, 30), (12, 24)], BRASS[3])
    c.poly([(13, 19), (25, 14), (25, 17), (13, 21)], BRASS[5])
    c.ellipse(25, 21, 3, 9, BRASS[1])
    c.ellipse(25, 21, 2, 7, INK)
    # Pull handle on a chain
    c.vline(4, 6, 16, IRON[4])
    c.rect(2, 22, 5, 3, CREAM[5])
    c.px(3, 22, (255, 255, 255, 255))
    c.rect(9, 27, 6, 4, BRASS[2])
    c.px(10, 28, BRASS[5])
    return _finish(c)


def instructions() -> Canvas:
    c = Canvas(16, 20)
    c.rect(1, 1, 14, 18, WALNUT[3])
    c.rect(3, 3, 10, 14, CREAM[5])
    for ly in range(5, 16, 2):
        c.hline(4, ly, 8 - (ly % 3), (74, 66, 60, 255))
    c.px(7, 0, BRASS[4])
    return _finish(c)


def mechanism_sketch() -> Canvas:
    """Framed drawing of the gate's pulley and weight."""
    c = Canvas(22, 18)
    c.rect(0, 0, 22, 18, WALNUT[3])
    c.hline(0, 0, 22, WALNUT[5])
    c.rect(2, 2, 18, 14, CREAM[5])
    ink = (76, 70, 76, 255)
    c.ellipse(15, 5, 2, 2, ink)
    c.vline(15, 7, 7, ink)
    c.rect(14, 12, 3, 3, ink)
    c.frame(4, 4, 8, 10, ink)
    c.line(11, 5, 14, 4, ink)
    return _finish(c)


def stop_base() -> Canvas:
    c = Canvas(22, 16)
    c.rect(2, 7, 18, 7, IRON[2])
    c.rect(1, 4, 20, 4, IRON[3])
    c.hline(1, 4, 20, IRON[5])
    c.rect(8, 0, 6, 5, IRON[3])
    c.hline(8, 0, 6, IRON[5])
    c.hline(2, 14, 18, IRON[0])
    c.px(4, 9, IRON[4])
    c.px(17, 10, IRON[4])
    return _finish(c)


def dresser() -> Canvas:
    """36x46 chest of drawers with a tilting mirror."""
    c = Canvas(36, 48)
    c.rect(8, 0, 20, 18, WALNUT[3])
    c.rect(10, 2, 16, 14, FOG[3])
    c.line(12, 12, 18, 4, FOG[5])
    c.line(14, 13, 20, 5, FOG[4])
    c.hline(10, 2, 16, FOG[4])
    for px in (6, 28):
        c.rect(px, 4, 2, 16, WALNUT[4])
    slab(c, 1, 18, 34, 6, 22, WALNUT, seed=51)
    for dy in (25, 31, 37):
        drawer(c, 3, dy, 30, 5, WALNUT)
    c.rect(1, 44, 34, 2, WALNUT[1])
    for lx in (2, 31):
        c.rect(lx, 46, 3, 2, WALNUT[2])
    c.rect(4, 15, 5, 4, TILE[5])
    c.px(6, 14, CLARET[4])
    return _finish(c)


def coat_stand() -> Canvas:
    c = Canvas(18, 50)
    c.ellipse(9, 47, 7, 2.5, WALNUT[2])
    c.vline(8, 8, 39, WALNUT[4])
    c.vline(9, 8, 39, WALNUT[2])
    for hx, hy in ((3, 8), (14, 8), (5, 12), (12, 12)):
        c.line(8 if hx < 9 else 9, hy + 3, hx, hy, WALNUT[3])
    c.ellipse(9, 6, 2, 2, BRASS[4])
    c.rect(11, 10, 4, 16, CLARET[3])
    c.vline(11, 10, 16, CLARET[4])
    c.hline(11, 25, 4, CLARET[2])
    # A bowler hat on the top hook
    c.rect(2, 3, 7, 4, INK)
    c.hline(1, 7, 9, (40, 32, 40, 255))
    c.hline(3, 3, 5, (70, 60, 72, 255))
    return _finish(c)


def globe() -> Canvas:
    c = Canvas(20, 34)
    c.poly([(4, 33), (10, 26), (16, 33)], WALNUT[3])
    c.vline(10, 20, 8, WALNUT[4])
    c.ellipse(10, 11, 8, 8, TEAL[3])
    c.ellipse(9, 10, 6, 6, TEAL[4])
    c.poly([(6, 7), (10, 5), (12, 9), (8, 12)], OCHRE[3])
    c.poly([(11, 13), (14, 12), (15, 16), (12, 17)], OCHRE[3])
    c.px(6, 6, (230, 240, 240, 255))
    c.line(2, 11, 10, 2, BRASS[3])
    c.line(10, 20, 18, 11, BRASS[3])
    return _finish(c)


def sofa() -> Canvas:
    c = Canvas(60, 32)
    c.rect(4, 0, 52, 16, CLARET[3])
    c.hline(5, 0, 50, CLARET[5])
    for bx in range(8, 54, 6):
        c.px(bx, 4, CLARET[1])
        c.px(bx + 3, 9, CLARET[1])
    c.rect(0, 8, 8, 18, CLARET[4])
    c.rect(52, 8, 8, 18, CLARET[2])
    c.hline(0, 8, 8, CLARET[5])
    c.hline(52, 8, 8, CLARET[3])
    c.rect(7, 15, 46, 9, CLARET[4])
    c.vline(30, 15, 9, CLARET[2])
    c.hline(7, 15, 46, CLARET[5])
    c.rect(1, 24, 58, 5, CLARET[2])
    for lx in (3, 55):
        c.rect(lx, 29, 2, 3, WALNUT[2])
    c.rect(38, 11, 10, 8, OCHRE[3])
    c.hline(38, 11, 10, OCHRE[5])
    return _finish(c)


def console_table(vase: bool = True) -> Canvas:
    c = Canvas(38, 34)
    slab(c, 1, 14, 36, 6, 5, WALNUT, seed=61)
    for lx in (3, 33):
        c.rect(lx, 25, 2, 9, WALNUT[3])
    c.hline(4, 29, 30, WALNUT[2])
    if vase:
        c.rect(15, 6, 8, 10, TILE[4])
        c.rect(16, 4, 6, 2, TILE[5])
        c.vline(16, 7, 8, TILE[5])
        for fx, fy in ((14, 1), (19, 0), (23, 2), (17, 3), (21, 3)):
            c.px(fx, fy, CLARET[4])
            c.px(fx, fy + 2, LEAF[3])
            c.vline(fx + (1 if fx < 19 else -1), fy + 1, 3, LEAF[2])
    else:
        c.rect(6, 10, 10, 4, CREAM[5])
        c.rect(24, 8, 6, 6, BRASS[3])
        c.hline(24, 8, 6, BRASS[5])
    return _finish(c)


def barrel() -> Canvas:
    c = Canvas(20, 26)
    c.ellipse(10, 5, 8, 3, WALNUT[5])
    c.ellipse(10, 5, 6, 2, WALNUT[3])
    c.rect(2, 5, 16, 18, WALNUT[4])
    c.rect(1, 9, 18, 10, WALNUT[4])
    for sx in range(3, 18, 3):
        c.vline(sx, 6, 17, WALNUT[3])
    for hy in (8, 18):
        c.hline(1, hy, 18, IRON[3])
        c.hline(1, hy + 1, 18, IRON[1])
    c.vline(2, 7, 14, WALNUT[5])
    c.ellipse(10, 23, 8, 2, WALNUT[2])
    return _finish(c)


def crate() -> Canvas:
    c = Canvas(24, 22)
    slab(c, 1, 2, 22, 7, 12, [WALNUT[1], WALNUT[2], WALNUT[3], WALNUT[4], OCHRE[2], OCHRE[3]], grain=False)
    c.frame(1, 9, 22, 12, WALNUT[2])
    c.line(2, 10, 21, 19, WALNUT[2])
    c.hline(1, 5, 22, OCHRE[1])
    for ox, oy, ramp in ((5, 3, LEAF), (11, 2, LEAF), (17, 4, OCHRE)):
        c.ellipse(ox + 2, oy + 2, 3, 2.5, ramp[4])
        c.px(ox + 1, oy + 1, ramp[5])
    return _finish(c)


def sack() -> Canvas:
    c = Canvas(18, 22)
    c.poly([(3, 21), (2, 10), (5, 4), (13, 4), (16, 10), (15, 21)], LINEN[3])
    c.poly([(6, 4), (9, 0), (12, 4)], LINEN[2])
    c.hline(5, 5, 8, OCHRE[2])
    c.vline(4, 9, 11, LINEN[4])
    c.vline(14, 9, 11, LINEN[1])
    c.rect(6, 12, 6, 4, CREAM[5])
    c.hline(7, 13, 4, (96, 86, 76, 255))
    return _finish(c)


def bench() -> Canvas:
    c = Canvas(52, 24)
    c.rect(2, 0, 48, 9, WALNUT[3])
    c.hline(2, 0, 48, WALNUT[5])
    for bx in range(6, 48, 8):
        c.vline(bx, 1, 8, WALNUT[2])
    slab(c, 0, 9, 52, 6, 3, WALNUT, grain=False)
    for lx in (3, 47):
        c.rect(lx, 18, 3, 6, WALNUT[2])
    return _finish(c)


def plant_stand() -> Canvas:
    c = Canvas(22, 44)
    c.rect(9, 18, 4, 24, WALNUT[3])
    c.vline(9, 18, 24, WALNUT[5])
    c.poly([(4, 43), (11, 38), (18, 43)], WALNUT[2])
    c.rect(4, 16, 14, 3, WALNUT[4])
    c.blit(fern(12), -1, -16)
    return _finish(c)


def side_table_plain() -> Canvas:
    c = side_table()
    c.a[0:14, 6:32] = 0
    c.ellipse(19, 10, 18, 9, WALNUT[4])
    c.ellipse(19, 9, 17, 8, WALNUT[5])
    c.ellipse(18, 8, 12, 5, mix(WALNUT[5], WALNUT[6], 0.4))
    c.rect(22, 5, 6, 3, CREAM[5])
    c.rect(12, 6, 5, 4, BRASS[3])
    c.px(14, 5, FLAME[3])
    return _finish(c)


# ------------------------------------------------------------------ doors ---

def room_door_leaf(side: int) -> Canvas:
    """One 32x56 leaf of a dormitory double door; side -1 hinges left."""
    c = Canvas(32, 56)
    wood = WALNUT
    c.rect(0, 0, 32, 56, wood[3])
    c.hline(0, 0, 32, wood[5])
    c.vline(0 if side < 0 else 31, 0, 56, wood[1])
    c.vline(31 if side < 0 else 0, 0, 56, wood[4])
    for py, ph in ((4, 22), (30, 22)):
        c.rect(4, py, 24, ph, wood[2])
        c.rect(5, py + 1, 22, ph - 2, wood[3])
        c.hline(5, py + 1, 22, wood[4])
        c.vline(5, py + 1, ph - 2, wood[4])
        c.rect(8, py + 4, 16, ph - 8, mix(wood[3], wood[4], 0.4))
    if side < 0:
        c.rect(27, 26, 2, 4, BRASS[4])
        c.px(27, 26, BRASS[5])
    else:
        c.rect(3, 26, 2, 4, BRASS[3])
        c.px(3, 26, BRASS[5])
    c.hline(0, 55, 32, wood[1])
    c.harden()
    return c


def isolation_gate(open_: bool = False) -> Canvas:
    """104x64 iron-bound oak gate in a stone surround, with pulley and weight.

    It fills the hall's 64px north wall face. The leaf carries iron straps,
    the inspection hatch and both bolt keepers; the inner bolt is separate.
    """
    c = Canvas(104, 64)
    ox, ow = 4, 88
    c.rect(ox, 0, ow, 64, STONE[4])
    for row, y in enumerate(range(0, 64, 8)):
        off = 6 if row % 2 else 0
        for x in range(ox - off, ox + ow, 14):
            c.vline(max(x, ox), y, 8, STONE[2])
        c.hline(ox, y, ow, STONE[2])
        c.hline(ox, y + 1, ow, STONE[5])
    c.vline(ox, 0, 64, STONE[6])
    c.vline(ox + ow - 1, 0, 64, STONE[1])
    # Flat lintel of voussoirs with a keystone
    gx, gy, gw, gh = 18, 14, 60, 50
    c.rect(gx - 4, gy - 10, gw + 8, 10, STONE[5])
    for vx in range(gx - 4, gx + gw + 4, 9):
        c.vline(vx, gy - 10, 10, STONE[3])
    c.hline(gx - 4, gy - 10, gw + 8, STONE[6])
    c.hline(gx - 4, gy - 1, gw + 8, STONE[2])
    c.rect(gx + gw // 2 - 5, gy - 12, 10, 12, STONE[6])
    c.vline(gx + gw // 2 - 5, gy - 12, 12, STONE[5])
    c.vline(gx + gw // 2 + 4, gy - 12, 12, STONE[3])
    if open_:
        for y in range(gy, gy + gh):
            t = (y - gy) / gh
            c.hline(gx, y, gw, mix((16, 14, 22, 255), (46, 42, 50, 255), t))
        c.dither(gx, gy + gh - 12, gw, 12, (68, 64, 70, 255), 0.3)
        # The leaf has swung outward; its edge shows against the far jamb.
        c.rect(gx + gw - 7, gy + 1, 7, gh - 1, WALNUT[2])
        c.vline(gx + gw - 7, gy + 1, gh - 1, WALNUT[4])
        for sy in (gy + 7, gy + 25, gy + 42):
            c.rect(gx + gw - 7, sy, 7, 3, IRON[3])
    else:
        c.rect(gx, gy, gw, gh, WALNUT[3])
        for bx in range(gx, gx + gw, 10):
            c.vline(bx, gy, gh, WALNUT[2])
            c.vline(bx + 1, gy, gh, WALNUT[4])
        c.dither(gx, gy, gw, gh, WALNUT[2], 0.1)
        for sy in (gy + 6, gy + 24, gy + 42):
            c.rect(gx, sy, gw, 4, IRON[2])
            c.hline(gx, sy, gw, IRON[4])
            for rx in range(gx + 3, gx + gw, 8):
                c.px(rx, sy + 2, IRON[5])
        c.rect(gx + 22, gy + 12, 16, 9, IRON[2])
        c.frame(gx + 22, gy + 12, 16, 9, IRON[4])
        c.rect(gx + 25, gy + 16, 10, 2, INK)
        c.rect(gx + 26, gy + 14, 8, 2, IRON[3])
        for kx in (gx - 3, gx + gw - 3):
            c.rect(kx, gy + 30, 6, 7, IRON[2])
            c.hline(kx, gy + 30, 6, IRON[5])
            c.rect(kx + 1, gy + 32, 4, 2, INK)
    c.vline(gx, gy, gh, (16, 12, 20, 255))
    c.hline(gx, gy, gw, (16, 12, 20, 255))
    c.vline(gx - 1, gy, gh, STONE[6])
    # Pulley, rope and counterweight in their channel beside the surround.
    px = ox + ow
    c.ellipse(px + 5, 6, 5, 5, IRON[3])
    c.ellipse(px + 5, 6, 2.5, 2.5, IRON[1])
    c.px(px + 3, 3, IRON[5])
    c.line(px + 1, 6, gx + gw - 2, gy + 4, OCHRE[3])
    drop = 34 if not open_ else 18
    c.vline(px + 9, 6, drop, OCHRE[3])
    c.vline(px + 10, 6, drop, OCHRE[1])
    wy = 6 + drop
    c.rect(px + 5, wy, 10, 14, IRON[2])
    c.hline(px + 5, wy, 10, IRON[5])
    c.vline(px + 5, wy, 14, IRON[4])
    c.hline(px + 5, wy + 13, 10, IRON[0])
    return _finish(c)


def inner_bolt() -> Canvas:
    c = Canvas(72, 8)
    c.rect(2, 2, 68, 4, IRON[3])
    c.hline(2, 2, 68, IRON[5])
    c.hline(2, 5, 68, IRON[1])
    for sx in (20, 34, 41):
        c.px(sx, 3, (212, 214, 206, 255))
        c.px(sx + 1, 3, (212, 214, 206, 255))
    c.rect(44, 0, 4, 8, IRON[4])
    c.hline(44, 0, 4, IRON[5])
    c.harden()
    return c


def build() -> dict[str, Canvas]:
    out: dict[str, Canvas] = {}
    for room in range(4):
        out[f"bed_{room}"] = bed(room)
        out[f"desk_{room}"] = desk(room)
        out[f"linen_chest_{room}"] = linen_chest(room)
        out[f"bookcase_{room}"] = bookcase(room + 1)
    out["bookcase_study"] = bookcase(7)
    out["bookcase_study_b"] = bookcase(9)
    out["nightstand"] = nightstand()
    out["chair_back"] = chair_back()
    out["chair_coat"] = chair_back(True)
    out["chair_front"] = chair_front()
    out["wardrobe"] = wardrobe()
    out["fern"] = fern(3)
    out["fern_b"] = fern(8)
    out["paper_basket"] = paper_basket()
    out["wall_clock"] = wall_clock()
    out["floor_lamp"] = floor_lamp()
    out["study_table"] = study_table()
    out["sideboard"] = sideboard()
    out["worktable"] = worktable()
    out["washstand"] = washstand()
    out["side_table"] = side_table()
    out["range_stove"] = range_stove()
    out["armchair"] = armchair()
    out["transmitter"] = transmitter()
    out["instructions"] = instructions()
    out["mechanism_sketch"] = mechanism_sketch()
    out["stop_base"] = stop_base()
    out["room_door_left"] = room_door_leaf(-1)
    out["room_door_right"] = room_door_leaf(1)
    out["isolation_gate"] = isolation_gate(False)
    out["isolation_gate_open"] = isolation_gate(True)
    out["inner_bolt"] = inner_bolt()
    out["side_table_plain"] = side_table_plain()
    out["dresser"] = dresser()
    out["coat_stand"] = coat_stand()
    out["globe"] = globe()
    out["sofa"] = sofa()
    out["console_vase"] = console_table(True)
    out["console_tray"] = console_table(False)
    out["barrel"] = barrel()
    out["crate"] = crate()
    out["sack"] = sack()
    out["bench"] = bench()
    out["plant_stand"] = plant_stand()
    return out
