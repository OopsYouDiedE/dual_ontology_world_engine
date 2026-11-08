"""Chapter 2 archive, resonance-room and clinic furniture. Native 1x pixels.

Same recipe conventions as first_awakening_art.props: key light from the
upper left, 3/4 top-down view (lit top surface above a shaded front face),
selective dark outline and alpha hardening via _finish(). The bottom-centre
of every canvas is the floor anchor; wall-mounted pieces anchor on the floor
line under their mounting point.

FOOTPRINTS gives the solid floor rectangle (w, h) in px, centred on the
anchor and extending upward from it; None means wall-mounted/non-blocking.
"""
from __future__ import annotations

import math

from first_awakening_art.palette import (BOTTLE, BRASS, CLARET, CREAM, FLAME, FOG, HONEY, INK, IRON, LINEN,
                                         OCHRE, PLUM, SLATE, STONE, WALNUT)
from first_awakening_art.pixel import Canvas, mix, rng
from first_awakening_art.props import _finish, drawer, leg, slab

SHADOW = (16, 12, 20, 255)
# Amber and green glass derived from existing ramps (no new hues).
AMBER = [OCHRE[0], WALNUT[2], OCHRE[1], mix(OCHRE[1], OCHRE[2], 0.5), OCHRE[2], OCHRE[4]]
GREEN = [BOTTLE[0], BOTTLE[1], BOTTLE[2], BOTTLE[4], BOTTLE[5], mix(BOTTLE[5], CREAM[4], 0.4)]
PORCELAIN = [CREAM[0], CREAM[1], CREAM[2], CREAM[4], CREAM[5], CREAM[5]]
GLASS_BACK = mix(WALNUT[1], FOG[0], 0.35)
GLINT = FOG[5][:3] + (90,)

FOOTPRINTS: dict[str, tuple[int, int] | None] = {
    "archive_shelf": (48, 14),
    "seal_rack": (56, 14),
    "seal_box": None,
    "box_pedestal": (14, 10),
    "vault_door": None,
    "counterweight_engine": (64, 22),
    "copy_desk": (56, 18),
    "plan_cabinet": (48, 16),
    "tuning_frame": (60, 14),
    "music_stand": (10, 8),
    "ward_bed": (30, 40),
    "medicine_cabinet": (46, 12),
    "reagent_cabinet": (46, 12),
    "wash_trough": (56, 14),
    "folding_screen": (46, 8),
    "doctor_desk": (52, 16),
}


# ---------------------------------------------------------------- helpers ---

LEDGER_HIDES = [
    (CLARET[1], CLARET[2], CLARET[3]),
    (BOTTLE[1], BOTTLE[2], BOTTLE[3]),
    (WALNUT[2], WALNUT[3], WALNUT[4]),
    (PLUM[1], PLUM[2], PLUM[3]),
    (OCHRE[1], OCHRE[2], OCHRE[3]),
    (SLATE[1], SLATE[2], SLATE[3]),
]


def ledger_row(c: Canvas, x: int, y: int, w: int, h: int, seed: int) -> None:
    """Upright bound ledgers: leather spines, gilt bands and a paper label."""
    g = rng(seed)
    bx = x
    while bx < x + w:
        bw = min(int(g.choice([3, 4, 4, 5])), x + w - bx)
        bh = h - int(g.choice([0, 0, 1, 2]))
        dark, mid, light = LEDGER_HIDES[int(g.integers(0, len(LEDGER_HIDES)))]
        top = y + h - bh
        c.rect(bx, top, bw, bh, mid)
        c.vline(bx, top, bh, light)
        if bw >= 3:
            c.vline(bx + bw - 1, top, bh, dark)
        c.hline(bx, top + 1, bw, OCHRE[3])
        c.hline(bx, y + h - 2, bw, OCHRE[2])
        if bw >= 4 and bh >= 7:
            c.rect(bx + 1, top + 3, bw - 2, 2, CREAM[3])
        bx += bw


def bundle(c: Canvas, x: int, y: int, w: int, h: int) -> None:
    """A tied stack of loose documents lying flat; sheet edges face out."""
    for i, yy in enumerate(range(y, y + h)):
        inset = 1 if i % 3 == 1 else 0
        c.hline(x + inset, yy, w - inset, CREAM[2] if i % 3 == 2 else CREAM[4])
    c.hline(x, y, w, CREAM[5])
    c.vline(x, y, h, CREAM[5])
    c.hline(x, y + h - 1, w, CREAM[1])
    tx = x + w // 2
    c.vline(tx, y, h, OCHRE[2])
    c.px(tx, y, OCHRE[4])
    c.px(tx + 1, y, OCHRE[3])


def bottle(c: Canvas, x: int, bottom: int, h: int, ramp, w: int = 3, label: bool = False) -> None:
    """A stoppered bottle standing on `bottom` (last row)."""
    y = bottom - h + 1
    c.rect(x, y + 3, w, h - 3, ramp[3])
    c.vline(x, y + 3, h - 3, ramp[5])
    c.vline(x + w - 1, y + 3, h - 3, ramp[1])
    c.hline(x, y + 2, w, ramp[2])
    c.px(x + w // 2, y + 1, ramp[2])
    c.px(x + w // 2, y, OCHRE[3])
    if label and h >= 7:
        c.hline(x, y + h - 3, w, CREAM[4])


def ring(c: Canvas, cx: float, cy: float, r_out: float, r_in: float, light, dark) -> None:
    """An annulus lit on its upper-left half."""
    for y in range(int(cy - r_out) - 1, int(cy + r_out) + 2):
        for x in range(int(cx - r_out) - 1, int(cx + r_out) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            if r_in * r_in < dx * dx + dy * dy <= r_out * r_out:
                c.px(x, y, light if dx + dy < 0 else dark)


def chain(c: Canvas, x: int, y0: int, y1: int) -> None:
    for yy in range(y0, y1):
        k = (yy - y0) % 4
        if k == 0:
            c.px(x, yy, IRON[5]); c.px(x + 1, yy, IRON[3])
        elif k == 1:
            c.px(x, yy, IRON[3]); c.px(x + 1, yy, IRON[1])
        elif k == 2:
            c.px(x, yy, IRON[4])
        else:
            c.px(x + 1, yy, IRON[2])


def rivet(c: Canvas, x: int, y: int) -> None:
    c.px(x, y, IRON[5])
    c.px(x, y + 1, IRON[1])


def seal_box_into(c: Canvas, x: int, y: int) -> None:
    """12x10 dark-oak box: lid, brass seal plate, wax-red ribbon and seal."""
    lid = 3
    c.rect(x, y, 12, lid, WALNUT[4])
    c.hline(x, y, 12, WALNUT[5])
    c.rect(x, y + lid, 12, 10 - lid, WALNUT[2])
    c.hline(x, y + lid, 12, WALNUT[1])
    c.vline(x, y + lid, 10 - lid, WALNUT[3])
    c.hline(x, y + 9, 12, WALNUT[0])
    for bx in (x, x + 11):
        c.px(bx, y + lid + 1, BRASS[3])
        c.px(bx, y + 8, BRASS[2])
    # Ribbon crosses the lid and runs down the front under the plate.
    c.hline(x, y + 1, 12, CLARET[4])
    c.vline(x + 6, y, lid, CLARET[5])
    c.vline(x + 6, y + lid, 10 - lid, CLARET[3])
    c.rect(x + 4, y + 4, 5, 4, BRASS[3])
    c.hline(x + 4, y + 4, 5, BRASS[5])
    c.hline(x + 4, y + 7, 5, BRASS[1])
    c.rect(x + 5, y + 5, 3, 2, CLARET[3])
    c.px(x + 5, y + 5, CLARET[5])
    c.px(x + 7, y + 6, CLARET[1])
    c.px(x + 5, y + 8, CLARET[4])
    c.px(x + 7, y + 9, CLARET[3])


def glass_glints(c: Canvas, x: int, y: int, w: int, h: int) -> None:
    for off in (2, 9):
        for i in range(min(w, h)):
            px_, py_ = x + off + i, y + h - 1 - i * 2
            if x <= px_ < x + w and y <= py_ < y + h and i < 6:
                c.px(px_, py_, GLINT)
                c.px(px_, py_ - 1, GLINT)


# ---------------------------------------------------------- archive group ---

def archive_shelf(seed: int = 3) -> Canvas:
    """52x72 tall walnut shelving: ledgers, tied bundles and box files."""
    c = Canvas(52, 72)
    c.rect(0, 2, 52, 70, WALNUT[3])
    c.rect(0, 0, 52, 4, WALNUT[4])
    c.hline(0, 0, 52, WALNUT[5])
    c.hline(0, 3, 52, WALNUT[2])
    c.rect(3, 5, 46, 60, WALNUT[1])
    for bx in range(11, 48, 9):
        c.vline(bx, 5, 60, WALNUT[0])
    rows = (5, 17, 29, 41, 53)
    # Row 0: a full run of ledgers.
    ledger_row(c, 3, 6, 46, 10, seed * 7 + 1)
    # Row 1: ledgers, then two bundles stacked flat.
    ledger_row(c, 3, 18, 26, 10, seed * 7 + 2)
    bundle(c, 30, 22, 18, 6)
    bundle(c, 32, 19, 14, 3)
    # Row 2: bundles at the left, ledgers to the right.
    bundle(c, 4, 34, 16, 6)
    bundle(c, 5, 31, 13, 3)
    ledger_row(c, 21, 30, 28, 10, seed * 7 + 3)
    # Row 3: ledgers with one volume pulled out and laid on top.
    ledger_row(c, 3, 42, 46, 10, seed * 7 + 4)
    c.rect(30, 42, 4, 10, WALNUT[1])
    c.rect(29, 41, 12, 2, CLARET[3])
    c.hline(29, 41, 12, CLARET[4])
    # Row 4: three pasteboard box files with labels and pull rings.
    for bx in (4, 19, 34):
        c.rect(bx, 55, 14, 9, LINEN[2])
        c.hline(bx, 55, 14, LINEN[4])
        c.vline(bx, 55, 9, LINEN[3])
        c.vline(bx + 13, 55, 9, LINEN[1])
        c.rect(bx + 4, 57, 6, 3, CREAM[5])
        c.hline(bx + 4, 59, 6, CREAM[3])
        c.px(bx + 7, 61, BRASS[3])
        c.px(bx + 6, 61, BRASS[4])
    for sy in rows:
        c.rect(3, sy + 11, 46, 1, WALNUT[4])
        c.hline(3, sy + 12, 46, WALNUT[2])
    # Paper slip tags hanging from the shelf lips.
    for tx, sy in ((10, 17), (38, 41), (24, 29)):
        c.vline(tx, sy + 12, 3, CREAM[5])
        c.px(tx + 1, sy + 13, CREAM[3])
    c.vline(1, 2, 64, WALNUT[5])
    c.vline(50, 2, 64, WALNUT[2])
    c.rect(0, 66, 52, 6, WALNUT[3])
    c.hline(0, 66, 52, WALNUT[5])
    c.hline(0, 67, 52, WALNUT[4])
    c.rect(1, 70, 50, 2, WALNUT[1])
    return _finish(c)


def seal_rack() -> Canvas:
    """60x64 iron-and-oak rack: two shelves, four identical sealed boxes."""
    c = Canvas(60, 64)
    c.rect(5, 5, 50, 50, HONEY[1])
    for bx in range(12, 54, 8):
        c.vline(bx, 5, 50, HONEY[0])
    c.hline(5, 5, 50, HONEY[0])
    shelves = ((26, 19), (49, 42))
    for sy, by in shelves:
        slab(c, 3, sy, 54, 5, 3, HONEY, seed=sy)
        for bx in (12, 36):
            seal_box_into(c, bx, by)
            # Small brass slot plates on the shelf lip.
            c.hline(bx + 4, sy + 6, 4, BRASS[3])
            c.px(bx + 4, sy + 6, BRASS[5])
    # Iron uprights, cap rail and a low stretcher.
    for ux in (2, 55):
        c.rect(ux, 2, 3, 60, IRON[3])
        c.vline(ux, 2, 60, IRON[5])
        c.vline(ux + 2, 2, 60, IRON[1])
        c.rect(ux - 1, 61, 5, 3, IRON[2])
        c.hline(ux - 1, 61, 5, IRON[4])
        for ry in (8, 28, 51):
            rivet(c, ux + 1, ry)
    c.rect(1, 1, 58, 4, IRON[3])
    c.hline(1, 1, 58, IRON[5])
    c.hline(1, 4, 58, IRON[1])
    c.px(3, 0, BRASS[4])
    c.px(56, 0, BRASS[3])
    c.rect(5, 57, 50, 2, IRON[2])
    c.hline(5, 57, 50, IRON[4])
    return _finish(c)


def seal_box() -> Canvas:
    """16x14 single sealed box, for a pedestal."""
    c = Canvas(16, 14)
    seal_box_into(c, 2, 4)
    return _finish(c)


def box_pedestal() -> Canvas:
    """20x28 dressed-stone pedestal with a cap to carry one sealed box."""
    c = Canvas(20, 28)
    slab(c, 1, 1, 18, 6, 3, STONE, grain=False)
    c.rect(4, 10, 12, 13, STONE[3])
    c.vline(4, 10, 13, STONE[5])
    c.vline(5, 10, 13, STONE[4])
    c.vline(15, 10, 13, STONE[1])
    c.vline(9, 11, 11, STONE[2])
    c.vline(11, 11, 11, STONE[2])
    c.hline(4, 10, 12, STONE[1])
    slab(c, 2, 23, 16, 2, 3, STONE, grain=False)
    return _finish(c)


def vault_door() -> Canvas:
    """76x80 wall-mounted riveted iron archive door in a dressed-stone frame."""
    c = Canvas(76, 80)
    gx, gy, gw, gh = 12, 13, 52, 63

    def block(x: int, y: int, w: int, h: int) -> None:
        c.rect(x, y, w, h, STONE[4])
        c.hline(x, y, w, STONE[5])
        c.vline(x, y, h, STONE[5])
        c.hline(x, y + h - 1, w, STONE[2])
        c.vline(x + w - 1, y, h, STONE[2])

    # Quoined jambs: alternating long and short blocks.
    for i, y in enumerate(range(13, 76, 9)):
        h = min(9, 76 - y)
        long_ = i % 2 == 0
        block(0 if long_ else 3, y, 12 if long_ else 9, h)
        block(64, y, 12 if long_ else 9, h)
    # Lintel of voussoirs and a raised keystone.
    for vx in range(0, 76, 11):
        block(vx, 2, min(11, 76 - vx), 11)
    c.rect(32, 0, 12, 15, STONE[5])
    c.hline(32, 0, 12, STONE[6])
    c.vline(32, 0, 15, STONE[6])
    c.vline(43, 0, 15, STONE[3])
    c.hline(32, 14, 12, STONE[2])
    # Door leaf.
    c.rect(gx, gy, gw, gh, IRON[3])
    c.frame(gx + 2, gy + 2, gw - 4, gh - 4, IRON[2])
    c.frame(gx + 3, gy + 3, gw - 6, gh - 6, IRON[4])
    for sy in (gy + 16, gy + 46):
        c.hline(gx + 3, sy, gw - 6, IRON[1])
        c.hline(gx + 3, sy + 1, gw - 6, IRON[4])
    for rx in range(gx + 4, gx + gw - 4, 5):
        for ry in (gy + 1, gy + gh - 3, gy + 13, gy + 48):
            rivet(c, rx, ry)
    for ry in range(gy + 5, gy + gh - 4, 6):
        rivet(c, gx + 1, ry)
        rivet(c, gx + gw - 2, ry)
    c.dither(gx + 4, gy + 4, gw - 8, gh - 8, IRON[2], 0.08)
    # Hinge straps on the left edge.
    for hy in (gy + 7, gy + 52):
        c.rect(gx, hy, 12, 4, IRON[2])
        c.hline(gx, hy, 12, IRON[5])
        c.hline(gx, hy + 3, 12, IRON[0])
        rivet(c, gx + 9, hy + 1)
    cx, cy = 38.0, 45.0
    # Three radial bolts running from the core into keepers in the frame.
    c.rect(gx - 2, 44, 14, 3, IRON[4])
    c.hline(gx - 2, 44, 14, IRON[5])
    c.hline(gx - 2, 46, 14, IRON[1])
    c.rect(51, 44, gx + gw + 2 - 51, 3, IRON[4])
    c.hline(51, 44, gx + gw + 2 - 51, IRON[5])
    c.hline(51, 46, gx + gw + 2 - 51, IRON[1])
    c.rect(37, gy - 2, 3, 34 - gy, IRON[4])
    c.vline(37, gy - 2, 34 - gy, IRON[5])
    c.vline(39, gy - 2, 34 - gy, IRON[1])
    for kx, ky, kw, kh in ((gx - 4, 42, 4, 7), (gx + gw, 42, 4, 7), (35, gy - 4, 7, 3)):
        c.rect(kx, ky, kw, kh, IRON[2])
        c.frame(kx, ky, kw, kh, IRON[1])
        c.px(kx, ky, IRON[5])
    # Brass lock-core boss.
    c.ellipse(cx, cy, 14, 14, BRASS[0])
    c.ellipse(cx - 0.5, cy - 0.5, 13, 13, BRASS[4])
    c.ellipse(cx + 0.5, cy + 0.5, 12.5, 12.5, BRASS[2])
    c.ellipse(cx, cy, 11.5, 11.5, BRASS[3])
    for a in range(12):
        t = a * math.pi / 6
        c.px(int(cx + math.cos(t) * 9.5), int(cy + math.sin(t) * 9.5), BRASS[1])
    for t in (math.pi, 0.0, -math.pi / 2):
        c.rect(int(cx + math.cos(t) * 11) - 1, int(cy + math.sin(t) * 11) - 1, 3, 3, BRASS[4])
    c.ellipse(cx, cy, 6, 6, BRASS[1])
    c.ellipse(cx - 0.5, cy - 0.5, 5, 5, BRASS[5])
    c.ellipse(cx, cy, 4.5, 4.5, BRASS[3])
    c.rect(37, 41, 2, 3, INK)
    c.rect(36, 42, 4, 1, INK)
    c.rect(37, 44, 2, 4, INK)
    c.px(33, 39, BRASS[5])
    c.px(32, 40, BRASS[5])
    # Shadow in the reveal and a worn stone threshold.
    c.vline(gx, gy, gh, SHADOW)
    c.hline(gx, gy, gw, SHADOW)
    c.rect(8, 76, 60, 4, STONE[4])
    c.hline(8, 76, 60, STONE[6])
    c.hline(8, 79, 60, STONE[2])
    c.dither(24, 77, 28, 2, STONE[5], 0.3)
    return _finish(c)


def counterweight_engine() -> Canvas:
    """72x92 basement counterweight machine: frame, weights, winch, lever."""
    c = Canvas(72, 92)
    # Stone bed.
    slab(c, 1, 76, 70, 6, 10, STONE, grain=False)
    for jx in (18, 36, 54):
        c.vline(jx, 82, 9, STONE[2])
    # Tall iron frame.
    for px_ in (3, 35):
        c.rect(px_, 6, 4, 72, IRON[3])
        c.vline(px_, 6, 72, IRON[5])
        c.vline(px_ + 3, 6, 72, IRON[1])
        c.rect(px_ - 1, 76, 6, 3, IRON[2])
        c.hline(px_ - 1, 76, 6, IRON[4])
        for ry in range(12, 74, 10):
            rivet(c, px_ + 1, ry)
    c.rect(1, 2, 42, 6, IRON[3])
    c.hline(1, 2, 42, IRON[5])
    c.hline(1, 7, 42, IRON[1])
    c.rect(7, 70, 28, 2, IRON[2])
    c.hline(7, 70, 28, IRON[4])
    # Sheaves on the head beam.
    for sx in (14.5, 27.5):
        c.ellipse(sx, 10.5, 4, 4, IRON[1])
        c.ellipse(sx - 0.5, 10, 3.5, 3.5, IRON[4])
        c.ellipse(sx, 10.5, 1.5, 1.5, IRON[0])
    # Two stacked cast-iron weights on chains.
    chain(c, 11, 11, 36)
    chain(c, 30, 11, 36)
    slab(c, 9, 36, 24, 3, 11, IRON, grain=False)
    c.hline(10, 43, 22, IRON[2])
    c.hline(10, 44, 22, IRON[4])
    for ex in (11, 30):
        c.px(ex, 35, IRON[5])
        c.px(ex + 1, 35, IRON[2])
    chain(c, 20, 50, 55)
    slab(c, 9, 55, 24, 3, 11, IRON, grain=False)
    c.hline(10, 62, 22, IRON[2])
    c.hline(10, 63, 22, IRON[4])
    c.px(20, 54, IRON[5])
    c.px(21, 54, IRON[2])
    # Winch stand.
    cx, cy = 52.0, 50.0
    c.line(44, 77, 51, 51, IRON[2])
    c.line(45, 77, 52, 51, IRON[4])
    c.line(60, 77, 53, 51, IRON[1])
    c.line(59, 77, 52, 51, IRON[3])
    c.rect(42, 75, 21, 2, IRON[2])
    c.hline(42, 75, 21, IRON[4])
    # Chain from the right sheave down to the winch drum.
    c.line(31, 7, 48, 42, IRON[4])
    c.line(32, 7, 49, 42, IRON[1])
    # Hand wheel: rim, six spokes, hub, ratchet and pawl.
    for a in range(6):
        t = a * math.pi / 3 + 0.3
        c.line(int(cx), int(cy), int(round(cx + math.cos(t) * 11)), int(round(cy + math.sin(t) * 11)), IRON[3])
    ring(c, cx, cy, 13, 10.5, IRON[4], IRON[2])
    ring(c, cx, cy, 13, 12.2, IRON[5], IRON[1])
    for a in range(10):
        t = a * math.pi / 5
        c.px(int(cx + math.cos(t) * 5.2), int(cy + math.sin(t) * 5.2), IRON[5])
    ring(c, cx, cy, 4.6, 0, IRON[3], IRON[1])
    c.ellipse(cx, cy, 2, 2, BRASS[3])
    c.px(51, 49, BRASS[5])
    c.line(44, 60, 48, 54, IRON[5])
    c.line(45, 60, 49, 54, IRON[2])
    c.px(44, 60, BRASS[4])
    # Crank grip on the rim.
    c.rect(41, 37, 3, 5, WALNUT[4])
    c.vline(41, 37, 5, WALNUT[5])
    c.vline(43, 37, 5, WALNUT[2])
    c.px(42, 42, IRON[4])
    # Release lever on a quadrant beside the winch.
    c.rect(64, 70, 6, 6, IRON[2])
    c.hline(64, 70, 6, IRON[4])
    c.line(66, 74, 68, 57, IRON[4])
    c.line(67, 74, 69, 57, IRON[1])
    c.ellipse(68.5, 56, 2, 2, WALNUT[4])
    c.px(67, 55, WALNUT[6])
    return _finish(c)


def copy_desk() -> Canvas:
    """60x44 copyist's desk: sloped leather top, gallery, papers, ink, red pen."""
    c = Canvas(60, 44)
    # Pigeonhole gallery at the back.
    c.rect(4, 1, 52, 10, WALNUT[3])
    c.hline(4, 1, 52, WALNUT[5])
    c.vline(4, 1, 10, WALNUT[4])
    for i in range(6):
        hx = 6 + i * 8
        c.rect(hx, 3, 7, 6, WALNUT[1])
        if i in (0, 2, 3, 5):
            c.rect(hx + 1, 5, 5, 4, CREAM[4] if i % 2 else CREAM[3])
            c.hline(hx + 1, 5, 5, CREAM[5])
    # Sloped writing top: the lower lip is the pen ledge.
    slab(c, 1, 10, 58, 17, 9, WALNUT, seed=71)
    c.rect(5, 12, 32, 10, BOTTLE[3])
    c.frame(5, 12, 32, 10, OCHRE[2])
    c.hline(6, 13, 30, BOTTLE[4])
    c.dither(6, 14, 30, 7, BOTTLE[2], 0.15)
    c.hline(3, 23, 36, WALNUT[2])
    c.hline(3, 24, 36, WALNUT[5])
    # Sheet being copied, ruled but unlettered.
    c.rect(10, 13, 15, 9, CREAM[5])
    c.hline(10, 21, 15, CREAM[2])
    for ly in range(15, 21, 2):
        c.hline(12, ly, 11, FOG[3])
    c.hline(12, 17, 6, CLARET[4])
    # Red-ink pen across the sheet.
    c.line(20, 22, 31, 15, CLARET[3])
    c.line(21, 22, 32, 15, CLARET[4])
    c.px(19, 23, IRON[5])
    c.px(18, 23, CLARET[5])
    # Inkwell: dark glass, brass cap.
    c.rect(39, 15, 5, 5, INK)
    c.vline(39, 15, 5, FOG[1])
    c.px(40, 16, FOG[4])
    c.rect(40, 14, 3, 1, BRASS[4])
    c.hline(39, 20, 5, WALNUT[1])
    # Stack of finished copies.
    for i, (sx, sy) in enumerate(((45, 15), (46, 14), (45, 13))):
        c.rect(sx, sy, 11, 8, CREAM[5] if i != 1 else CREAM[4])
        c.hline(sx, sy + 7, 11, CREAM[2])
    c.vline(51, 13, 8, OCHRE[2])
    # Front: drawers and kneehole, legs with a stretcher.
    drawer(c, 3, 28, 16, 6, WALNUT)
    drawer(c, 41, 28, 16, 6, WALNUT)
    c.rect(20, 28, 20, 8, WALNUT[1])
    c.hline(20, 28, 20, WALNUT[0])
    leg(c, 2, 36, 8, WALNUT)
    leg(c, 56, 36, 8, WALNUT)
    c.hline(4, 40, 52, WALNUT[2])
    c.vline(1, 27, 9, WALNUT[4])
    return _finish(c)


def plan_cabinet() -> Canvas:
    """52x40 flat-file cabinet: shallow drawers, a rolled plan on top."""
    c = Canvas(52, 40)
    slab(c, 1, 6, 50, 8, 22, WALNUT, seed=81)
    for yy in range(15, 34, 3):
        drawer(c, 3, yy, 46, 3, WALNUT)
        c.hline(21, yy + 1, 10, BRASS[3])
        c.px(21, yy + 1, BRASS[5])
        c.hline(6, yy + 1, 4, CREAM[4])
    c.vline(1, 14, 22, WALNUT[4])
    c.rect(1, 35, 50, 3, WALNUT[2])
    c.hline(1, 35, 50, WALNUT[4])
    for lx in (2, 46):
        c.rect(lx, 38, 4, 2, WALNUT[1])
    # A cyanotype sheet and a rolled drawing tied with tape.
    c.rect(26, 8, 20, 5, FOG[1])
    c.hline(26, 8, 20, FOG[2])
    c.hline(28, 10, 14, FOG[3])
    c.vline(33, 9, 3, FOG[3])
    c.vline(40, 9, 3, FOG[3])
    c.rect(6, 5, 26, 4, CREAM[4])
    c.hline(6, 5, 26, CREAM[5])
    c.hline(6, 8, 26, CREAM[2])
    c.rect(4, 5, 3, 4, CREAM[3])
    c.px(5, 6, CREAM[1])
    c.px(5, 7, CREAM[5])
    c.vline(18, 5, 4, OCHRE[3])
    return _finish(c)


# ------------------------------------------------ resonance / clinic group ---

def tuning_frame() -> Canvas:
    """68x64 walnut frame hanging a graded row of brass tuning tubes."""
    c = Canvas(68, 64)
    for px_ in (4, 60):
        c.rect(px_, 4, 4, 56, WALNUT[3])
        c.vline(px_, 4, 56, WALNUT[5])
        c.vline(px_ + 3, 4, 56, WALNUT[1])
        slab(c, px_ - 4, 57, 12, 3, 4, WALNUT, grain=False)
    c.rect(1, 2, 66, 6, WALNUT[3])
    c.hline(1, 2, 66, WALNUT[5])
    c.hline(1, 3, 66, WALNUT[4])
    c.hline(1, 7, 66, WALNUT[1])
    c.rect(4, 0, 4, 2, BRASS[3])
    c.hline(4, 0, 4, BRASS[5])
    c.rect(60, 0, 4, 2, BRASS[3])
    c.hline(60, 0, 4, BRASS[4])
    c.rect(8, 48, 52, 3, WALNUT[3])
    c.hline(8, 48, 52, WALNUT[4])
    c.hline(8, 50, 52, WALNUT[1])
    # Graded tubes: longest at the left.
    for i in range(8):
        x = 11 + i * 6
        length = int(round(35 - i * 2.6))
        c.vline(x + 1, 8, 2, LINEN[2])
        c.vline(x, 10, length, BRASS[5])
        c.vline(x + 1, 10, length, BRASS[3])
        c.vline(x + 2, 10, length, BRASS[1])
        c.hline(x, 10, 3, BRASS[2])
        c.hline(x, 11, 3, BRASS[4])
        c.hline(x, 10 + length - 1, 3, BRASS[1])
        c.px(x + 1, 10 + length - 2, BRASS[2])
    # Felt mallet laid along the stretcher.
    c.hline(46, 46, 12, WALNUT[5])
    c.hline(46, 47, 12, WALNUT[2])
    c.ellipse(43.5, 46, 3, 2.6, LINEN[1])
    c.ellipse(43, 45.5, 2.4, 2, LINEN[3])
    c.px(42, 44, LINEN[5])
    return _finish(c)


def music_stand() -> Canvas:
    """20x40 iron music stand holding a ruled sheet."""
    c = Canvas(20, 40)
    c.line(9, 31, 2, 39, IRON[4])
    c.line(10, 31, 3, 39, IRON[2])
    c.line(10, 31, 17, 39, IRON[2])
    c.line(11, 31, 18, 39, IRON[1])
    c.vline(10, 31, 6, IRON[3])
    c.vline(9, 14, 18, IRON[4])
    c.vline(10, 14, 18, IRON[2])
    c.rect(8, 24, 4, 2, BRASS[3])
    c.hline(8, 24, 4, BRASS[5])
    c.rect(1, 3, 18, 11, IRON[2])
    c.frame(1, 3, 18, 11, IRON[3])
    c.rect(1, 13, 18, 2, IRON[4])
    c.hline(1, 14, 18, IRON[1])
    c.rect(3, 1, 14, 12, CREAM[5])
    c.vline(16, 1, 12, CREAM[3])
    for sy in (3, 5, 8, 10):
        c.hline(4, sy, 11, FOG[3])
    for nx, ny in ((6, 4), (9, 3), (12, 5), (7, 9), (11, 8)):
        c.px(nx, ny, INK)
    c.px(16, 1, CREAM[2])
    return _finish(c)


def ward_bed() -> Canvas:
    """36x60 iron hospital bed, head up, white linen, grey blanket folded at foot."""
    c = Canvas(36, 60)
    # Iron side rails under the mattress edge.
    c.vline(2, 14, 42, IRON[2])
    c.vline(33, 14, 42, IRON[1])
    # Mattress and sheet.
    c.rect(3, 14, 30, 40, LINEN[4])
    c.vline(3, 14, 40, LINEN[3])
    c.vline(32, 14, 40, LINEN[2])
    c.rect(7, 15, 22, 8, LINEN[5])
    c.hline(8, 22, 20, LINEN[3])
    c.vline(7, 16, 6, LINEN[3])
    c.vline(28, 16, 6, LINEN[2])
    c.hline(11, 18, 8, LINEN[3])
    c.rect(3, 24, 30, 3, LINEN[5])
    c.hline(3, 27, 30, LINEN[2])
    for fy, fx, fw in ((31, 8, 12), (35, 18, 10), (39, 6, 9)):
        c.hline(fx, fy, fw, LINEN[3])
    # Folded grey blanket with a dark border stripe.
    c.rect(3, 42, 30, 10, STONE[4])
    c.hline(3, 42, 30, STONE[6])
    c.hline(3, 43, 30, STONE[5])
    c.hline(3, 45, 30, SLATE[2])
    c.hline(3, 47, 30, STONE[2])
    c.hline(3, 48, 30, STONE[5])
    c.hline(3, 51, 30, STONE[1])
    c.vline(3, 42, 10, STONE[5])
    c.vline(2, 43, 9, STONE[3])
    c.vline(33, 43, 9, STONE[1])
    # Headboard: tube rails and spindles with brass knobs.
    for px_ in (1, 33):
        c.rect(px_, 0, 2, 18, IRON[3])
        c.vline(px_, 0, 18, IRON[5])
        c.px(px_, 0, BRASS[4])
        c.px(px_ + 1, 0, BRASS[2])
    c.hline(3, 1, 30, IRON[4])
    c.hline(3, 2, 30, IRON[2])
    c.hline(3, 11, 30, IRON[3])
    c.hline(3, 12, 30, IRON[1])
    for sx in range(6, 32, 4):
        c.vline(sx, 3, 8, IRON[3])
        c.px(sx, 3, IRON[5])
    # Footboard.
    for px_ in (1, 33):
        c.rect(px_, 50, 2, 10, IRON[3])
        c.vline(px_, 50, 10, IRON[5])
        c.px(px_, 50, BRASS[4])
    c.hline(3, 53, 30, IRON[4])
    c.hline(3, 54, 30, IRON[2])
    for sx in range(6, 32, 4):
        c.vline(sx, 55, 3, IRON[3])
    c.hline(3, 58, 30, IRON[1])
    return _finish(c)


def _cabinet_bottles(c: Canvas, x: int, w: int, bottom: int, seed: int, gap_at: int | None = None,
                     even: bool = False) -> None:
    g = rng(seed)
    bx, i = x + 1, 0
    while bx < x + w - 3:
        if even:
            bw, bh = 3, 7
            ramp = (AMBER, GREEN, PORCELAIN)[i % 3]
        else:
            bw = int(g.choice([3, 3, 4]))
            bh = int(g.choice([5, 6, 7, 8]))
            ramp = (AMBER, AMBER, GREEN, GREEN, PORCELAIN)[int(g.integers(0, 5))]
        if gap_at is not None and i == gap_at:
            # An empty place: a clean ring in the dust where a bottle stood.
            c.hline(bx, bottom, bw, mix(GLASS_BACK, CREAM[3], 0.6))
            c.px(bx, bottom - 1, mix(GLASS_BACK, CREAM[3], 0.35))
            c.px(bx + bw - 1, bottom - 1, mix(GLASS_BACK, CREAM[3], 0.35))
        else:
            bottle(c, bx, bottom, bh, ramp, bw, label=g.random() < 0.6)
        bx += bw + (2 if even else int(g.choice([1, 1, 2])))
        i += 1


def medicine_cabinet() -> Canvas:
    """50x60 wall-backed glazed cabinet of brown and green bottles, locked."""
    c = Canvas(50, 60)
    c.rect(1, 4, 48, 52, WALNUT[3])
    c.rect(0, 0, 50, 5, WALNUT[4])
    c.hline(0, 0, 50, WALNUT[5])
    c.hline(0, 4, 50, WALNUT[1])
    c.rect(4, 8, 42, 30, GLASS_BACK)
    for i, sb in enumerate((16, 26, 36)):
        _cabinet_bottles(c, 4, 42, sb, 90 + i)
        c.hline(4, sb + 1, 42, WALNUT[4])
    glass_glints(c, 4, 8, 20, 30)
    glass_glints(c, 26, 8, 20, 30)
    for dx in (3, 25):
        c.frame(dx, 7, 22, 32, WALNUT[2])
        c.hline(dx, 7, 22, WALNUT[1])
        c.vline(dx, 7, 32, WALNUT[4])
    c.vline(24, 7, 32, WALNUT[1])
    c.vline(25, 7, 32, WALNUT[4])
    c.rect(24, 21, 2, 4, BRASS[3])
    c.px(24, 21, BRASS[5])
    c.px(25, 23, INK)
    drawer(c, 3, 41, 21, 6, WALNUT)
    drawer(c, 26, 41, 21, 6, WALNUT)
    drawer(c, 3, 48, 44, 6, WALNUT)
    c.vline(1, 4, 52, WALNUT[5])
    c.vline(48, 4, 52, WALNUT[1])
    c.rect(0, 56, 50, 4, WALNUT[2])
    c.hline(0, 56, 50, WALNUT[4])
    c.hline(0, 59, 50, WALNUT[0])
    return _finish(c)


def reagent_cabinet() -> Canvas:
    """50x60 iron reagent cabinet: placard, padlock, one bottle missing."""
    c = Canvas(50, 60)
    c.rect(1, 4, 48, 51, IRON[3])
    c.rect(0, 0, 50, 5, IRON[4])
    c.hline(0, 0, 50, IRON[5])
    c.hline(0, 4, 50, IRON[1])
    for ry in range(7, 54, 6):
        rivet(c, 2, ry)
        rivet(c, 47, ry)
    # Glazed upper section: two rows of identical reagent bottles.
    c.rect(4, 8, 42, 22, GLASS_BACK)
    _cabinet_bottles(c, 4, 42, 17, 101, even=True)
    _cabinet_bottles(c, 4, 42, 28, 102, gap_at=5, even=True)
    c.hline(4, 18, 42, IRON[4])
    c.hline(4, 29, 42, IRON[4])
    glass_glints(c, 4, 8, 20, 22)
    glass_glints(c, 26, 8, 20, 22)
    c.frame(3, 7, 44, 24, IRON[2])
    c.hline(3, 7, 44, IRON[1])
    c.vline(24, 7, 24, IRON[2])
    c.vline(25, 7, 24, IRON[4])
    # Solid lower doors.
    for dx in (3, 26):
        c.rect(dx, 32, 21, 22, IRON[3])
        c.hline(dx, 32, 21, IRON[5])
        c.vline(dx, 32, 22, IRON[4])
        c.hline(dx, 53, 21, IRON[1])
        c.vline(dx + 20, 32, 22, IRON[1])
        for rx, ry in ((dx + 2, 34), (dx + 18, 34), (dx + 2, 50), (dx + 18, 50)):
            rivet(c, rx, ry)
    # Painted hazard placard: skull and crossed bones on an ochre lozenge.
    c.poly([(13.5, 34), (21, 42.5), (13.5, 51), (6, 42.5)], INK)
    c.poly([(13.5, 35.5), (19.5, 42.5), (13.5, 49.5), (7.5, 42.5)], OCHRE[4])
    c.rect(12, 38, 3, 3, INK)
    c.px(12, 39, OCHRE[4])
    c.px(14, 39, OCHRE[4])
    c.px(13, 41, INK)
    c.line(10, 43, 16, 47, INK)
    c.line(10, 47, 16, 43, INK)
    # Hasp and padlock across the meeting stiles.
    c.rect(21, 39, 8, 2, IRON[5])
    c.hline(21, 40, 8, IRON[1])
    c.hline(23, 41, 1, IRON[4])
    c.vline(23, 41, 3, IRON[4])
    c.vline(27, 41, 3, IRON[2])
    c.hline(24, 41, 3, IRON[4])
    c.rect(22, 44, 7, 6, BRASS[3])
    c.hline(22, 44, 7, BRASS[5])
    c.vline(22, 44, 6, BRASS[4])
    c.hline(22, 49, 7, BRASS[1])
    c.px(25, 46, INK)
    c.px(25, 47, INK)
    c.rect(1, 55, 48, 2, IRON[1])
    for fx in (2, 44):
        c.rect(fx, 57, 4, 3, IRON[2])
        c.hline(fx, 57, 4, IRON[4])
    return _finish(c)


def wash_trough() -> Canvas:
    """60x30 stone wash trough: brass tap on the back rim, drain at one end."""
    c = Canvas(60, 30)
    slab(c, 1, 8, 58, 13, 9, STONE, grain=False)
    # Basin: shadowed rim lip, far wall, floor, side walls.
    c.rect(4, 10, 52, 9, STONE[2])
    c.rect(4, 10, 52, 3, STONE[3])
    c.hline(4, 10, 52, STONE[1])
    c.vline(4, 10, 9, STONE[1])
    c.vline(55, 11, 8, STONE[4])
    c.hline(4, 18, 52, STONE[3])
    c.ellipse(46, 16, 7, 1.8, mix(STONE[2], FOG[1], 0.45))
    c.hline(42, 15, 4, mix(STONE[3], FOG[3], 0.5))
    c.ellipse(50, 15.5, 2.5, 1.5, IRON[0])
    c.px(49, 15, IRON[3])
    c.px(51, 15, IRON[3])
    c.px(50, 16, IRON[3])
    for jx in (15, 30, 45):
        c.vline(jx, 22, 7, STONE[2])
        c.vline(jx + 1, 22, 7, STONE[4])
    c.hline(1, 21, 58, STONE[2])
    # Brass tap from the wall: valve wheel, riser and downturned spout.
    c.vline(18, 0, 10, BRASS[3])
    c.vline(19, 0, 10, BRASS[1])
    c.vline(17, 0, 10, BRASS[4])
    c.hline(15, 2, 7, BRASS[4])
    c.px(15, 2, BRASS[5])
    c.px(21, 2, BRASS[2])
    c.rect(17, 8, 7, 2, BRASS[3])
    c.hline(17, 8, 7, BRASS[5])
    c.rect(23, 9, 2, 3, BRASS[2])
    c.px(23, 9, BRASS[4])
    c.px(24, 12, FOG[4])
    return _finish(c)


def folding_screen() -> Canvas:
    """50x46 three-panel screen: linen panels in walnut frames, wings forward."""
    c = Canvas(50, 46)
    h = 38
    panels = ((1, 16, 5, 2, LINEN[2]), (16, 33, 2, 2, LINEN[3]), (33, 48, 2, 5, LINEN[4]))
    for x0, x1, t0, t1, cloth in panels:
        c.poly([(x0, t0), (x1, t1), (x1, t1 + h), (x0, t0 + h)], WALNUT[3])
        c.poly([(x0 + 2, t0 + 2), (x1 - 2, t1 + 2), (x1 - 2, t1 + h - 3), (x0 + 2, t0 + h - 3)], cloth)
        c.line(x0, t0, x1 - 1, t1, WALNUT[5])
        c.vline(x0, t0, h, WALNUT[4])
        c.vline(x1 - 1, t1, h, WALNUT[2])
        span = x1 - x0
        for x in range(x0 + 4, x1 - 2, 3):
            top = t0 + (t1 - t0) * (x - x0) / span
            c.vline(x, int(round(top)) + 3, h - 6, mix(cloth, LINEN[0], 0.25))
        # Cross rail at two-thirds height.
        rt0, rt1 = t0 + 26, t1 + 26
        c.line(x0, rt0, x1 - 1, rt1, WALNUT[3])
        c.line(x0, rt0 + 1, x1 - 1, rt1 + 1, WALNUT[1])
        for fx, ft in ((x0, t0), (x1 - 2, t1)):
            c.rect(fx, ft + h, 2, 3, WALNUT[2])
    for hx, hy in ((16, 10), (16, 30), (33, 10), (33, 30)):
        c.px(hx, hy, BRASS[4])
        c.px(hx, hy + 1, BRASS[2])
    return _finish(c)


def doctor_desk() -> Canvas:
    """56x40 plain desk: case register, stethoscope, oil lamp."""
    c = Canvas(56, 40)
    slab(c, 1, 8, 54, 14, 12, WALNUT, seed=111)
    # Open case register.
    c.rect(6, 10, 24, 10, CLARET[2])
    c.hline(6, 10, 24, CLARET[4])
    c.rect(7, 10, 22, 9, CREAM[5])
    c.rect(18, 10, 11, 9, CREAM[4])
    c.vline(18, 10, 9, CREAM[1])
    for ly in range(12, 18, 2):
        c.hline(8, ly, 9, FOG[3])
        c.hline(20, ly, 8, FOG[3])
    c.vline(23, 11, 7, FOG[2])
    c.vline(18, 19, 3, CLARET[4])
    # Stethoscope: metal binaurals, dark tube, chest piece.
    c.line(32, 10, 34, 13, IRON[5])
    c.line(37, 10, 35, 13, IRON[4])
    c.px(32, 10, IRON[2])
    c.px(37, 10, IRON[2])
    for (ax, ay), (bx, by) in zip(((34, 13), (34, 16), (33, 18), (35, 20)), ((34, 16), (33, 18), (35, 20), (38, 19))):
        c.line(ax, ay, bx, by, WALNUT[0])
    c.ellipse(39.5, 18.5, 2, 1.6, IRON[3])
    c.px(39, 17, IRON[5])
    # Oil lamp on the far corner.
    c.rect(44, 9, 6, 4, BRASS[3])
    c.hline(44, 9, 6, BRASS[5])
    c.rect(43, 12, 8, 1, BRASS[2])
    c.rect(45, 1, 4, 8, (232, 226, 206, 255))
    c.vline(45, 1, 8, (196, 186, 164, 255))
    c.rect(46, 4, 2, 4, FLAME[2])
    c.px(46, 3, FLAME[3])
    c.px(47, 4, FLAME[4])
    c.hline(45, 0, 4, BRASS[3])
    # Front: drawers, kneehole, legs.
    drawer(c, 3, 24, 16, 6, WALNUT)
    drawer(c, 37, 24, 16, 6, WALNUT)
    c.rect(20, 23, 16, 11, WALNUT[1])
    c.hline(20, 23, 16, WALNUT[0])
    for lx in (1, 52):
        c.rect(lx, 34, 3, 6, WALNUT[2])
        c.vline(lx, 34, 6, WALNUT[4])
    c.vline(1, 22, 12, WALNUT[4])
    return _finish(c)


def build() -> dict[str, Canvas]:
    out: dict[str, Canvas] = {
        "archive_shelf": archive_shelf(),
        "seal_rack": seal_rack(),
        "seal_box": seal_box(),
        "box_pedestal": box_pedestal(),
        "vault_door": vault_door(),
        "counterweight_engine": counterweight_engine(),
        "copy_desk": copy_desk(),
        "plan_cabinet": plan_cabinet(),
        "tuning_frame": tuning_frame(),
        "music_stand": music_stand(),
        "ward_bed": ward_bed(),
        "medicine_cabinet": medicine_cabinet(),
        "reagent_cabinet": reagent_cabinet(),
        "wash_trough": wash_trough(),
        "folding_screen": folding_screen(),
        "doctor_desk": doctor_desk(),
    }
    assert set(out) == set(FOOTPRINTS), set(out) ^ set(FOOTPRINTS)
    return out
