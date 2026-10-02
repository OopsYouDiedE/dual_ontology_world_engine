"""Native pixel cast for the life-wing four: detective, Halvard, Shen Ding, Su Ling.

Every pixel is authored at 1x. Heads are hand-drawn templates per facing;
bodies, garments and limbs are posed per frame from a small skeleton so
all 111 frames of an atlas share one construction. Proportions follow the
1890-1910 dress brief at a semi-chibi head ratio of about 1:3.5, chosen
so faces stay expressive at 1x without anime-style eyes.

Atlas contract (unchanged from animation_v2, so the .tres files stay valid):
512x1536, 8 columns x 24 rows of 64x64 cells; rows 0-7 front, 8-15 back,
16-23 left; per facing idle, walk, talk, inspect, interact, sit, break, cast
with 1/8/3/4/4/4/6/7 frames. Last sole pixel on y=54, figure centred on x=32.
Right facing is the engine's mirror of left.
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
from PIL import Image

from .pixel import Canvas, hexc, shift

INK = hexc("#1a1418")

CELL = 64
GROUND = 54          # last sole row
CX = 32              # figure centre lies between x=31 and x=32
ACTIONS = (("idle", 1), ("walk", 8), ("talk", 3), ("inspect", 4),
           ("interact", 4), ("sit", 4), ("break", 6), ("cast", 7))
FACINGS = ("front", "back", "left")
HEAD_H = 12           # head template rows; the chin meets the collar
LEVELS = (-0.52, -0.27, 0.0, 0.17, 0.34)   # deep, shade, base, light, high


def ramp(base: str, warm: bool = False) -> list[tuple[int, int, int, int]]:
    """Five-step ramp. Skin shades drift toward warm umber instead of violet,
    so hands, necks and mouths do not read as blood-red at 1x."""
    c = hexc(base)
    if warm:
        return [shift(c, s, hue_toward=0.06) if s else c for s in LEVELS]
    return [shift(c, s) if s else c for s in LEVELS]


# ----------------------------------------------------------------------------
# Character sheets
# ----------------------------------------------------------------------------

@dataclass
class Look:
    name: str
    height: int
    shoulder: int          # shoulder width (even)
    waist: int
    hip: int
    leg_w: int
    leg_gap: int
    arm_w: int
    boot_h: int
    skirt: int             # rows of coat/apron below the crotch
    skirt_kind: str        # "coat" (open front), "apron", "jacket", "none"
    mats: dict[str, str]
    heads: dict[str, list[str]]
    cuff: str | None = None
    extra: dict = field(default_factory=dict)


# Head templates are 10 wide x 12 tall (semi-chibi, head about 1/3.5 of
# height) so eyes and brows can carry expression at 1x. Keys:
#  k h H L = hair deep/shade/base/light   g G = second hair (grey temples, pin)
#  s S T   = skin shade/base/light        e = eye  b = brow  m = mouth  n = nose shade
HEADS_DETECTIVE = {   # dark tousled hair, lean face, side-parted fringe
    "front": [
        "...kHHhk..",
        "..kHLHHHhk",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "kHHSHHSHhk",
        "hHSSSSSShh",
        "hSbbSSbbSh",
        "hSSeSSeSSh",
        ".SSeSSeSs.",
        ".SSSSnSSs.",
        "..SSSmSs..",
        "...sSSs...",
    ],
    "back": [
        "...kHHhk..",
        "..kHLHHHhk",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "kHHLHHHHhk",
        "kHHHHHHHhk",
        "kHHHHHHhhk",
        "khHHHHHhhk",
        ".khHHHhhk.",
        ".kshhhhsk.",
        "..sSSSSs..",
        "...sSSs...",
    ],
    "left": [
        "...kHHHk..",
        "..kHLHHHhk",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "kHSHHHHHhk",
        "hSSSHHHhhk",
        "bbSSSHhhhk",
        "SeSSSShhhk",
        "SeSSSsShhk",
        "SSSSSSShk.",
        ".SmSSSs...",
        "..sSSs....",
    ],
}

HEADS_ENGINEER = {   # light-brown crew cut, square clean jaw
    "front": [
        "..........",
        "..kkkkkk..",
        ".kHLLLHHk.",
        "kHLHHHHHHk",
        "hHSSSSSSHh",
        "hSSSSSSSSh",
        "SSbbSSbbSS",
        "SSSeSSeSSs",
        "SSSeSSeSSs",
        "sSSSSnSSSs",
        ".sSSmmSSs.",
        "..ssssss..",
    ],
    "back": [
        "..........",
        "..kkkkkk..",
        ".kHLLLHHk.",
        "kHLHHHHHHk",
        "kHHHHHHHHk",
        "hHHHHHHHHh",
        "hHHHHHHHhh",
        "shHHHHHhhs",
        "SshhhhhhsS",
        "sSSSSSSSSs",
        ".sSSSSSSs.",
        "..ssssss..",
    ],
    "left": [
        "..........",
        "..kkkkkk..",
        ".kHLLLHHk.",
        "kLHHHHHHHk",
        "SSSHHHHHhk",
        "SSSSHHHhhk",
        "bbSSSSShhk",
        "eSSSSSsShk",
        "eSSSSSsShk",
        "SSSSSSShk.",
        "SmSSSSSs..",
        "sSSSSss...",
    ],
}

HEADS_CHEF = {   # black short hair, greying temples, broad firm face
    "front": [
        "..........",
        "..kkHHkk..",
        ".kHLHHHHk.",
        "kHLHHHHHhk",
        "GHHSSSSHhG",
        "GSSSSSSSSG",
        "gSbbSSbbSg",
        "SSSeSSeSSs",
        "SSSeSSeSSs",
        "sSSSSnSSSs",
        ".sSSmmSSs.",
        "..sSSSSs..",
    ],
    "back": [
        "..........",
        "..kkHHkk..",
        ".kHLHHHHk.",
        "kHLHHHHHhk",
        "GHHHHHHHhG",
        "GHHHHHHHhG",
        "gHHHHHHHhg",
        "GhHHHHHhgG",
        "SgghhhhggS",
        "sSSSSSSSSs",
        ".sSSSSSSs.",
        "..sSSSSs..",
    ],
    "left": [
        "..........",
        "..kkHHkk..",
        ".kHLHHHHk.",
        "kHLHHHHHhk",
        "SSSHHHHGhk",
        "SSSSSHGGhk",
        "bbSSSSGghk",
        "eSSSSSsGgk",
        "eSSSSSsSg.",
        "SSSSSSSs..",
        "SmSSSSs...",
        "sSSSSs....",
    ],
}

HEADS_MUSICIAN = {   # black hair drawn back to a low knot, pale narrow face
    "front": [
        "...kHHk...",
        "..kHLLHk..",
        ".kHLHHHHk.",
        "kHLHHHHHhk",
        "kHHHSSHHhk",
        "hHSSSSSShh",
        "hSSbSSbSSh",
        "hSSeSSeSSh",
        ".SSeSSeSS.",
        ".SSSSnSS..",
        "..SSmSSs..",
        "...sSSs...",
    ],
    "back": [
        "...kHHk...",
        "..kHLLHk..",
        ".kHLHHHHk.",
        "kHLHHHHHhk",
        "kHHLHHHHhk",
        "kHHHHHHHhk",
        "khHHHHHHhk",
        ".khHHHHhk.",
        "..kHHHHk..",
        "..kHGHhk..",
        "...sSSs...",
        "...sSSs...",
    ],
    "left": [
        "...kHHHk..",
        "..kHLHHHk.",
        ".kHLHHHHhk",
        "kHLHHHHHhk",
        "kSSHHHHHhk",
        "SSSSHHHhhk",
        "bSSSSHhhk.",
        "eSSSShhkHk",
        "eSSSSsSkGk",
        "SSSSSs..k.",
        "SmSSs.....",
        ".sSs......",
    ],
}

LOOKS = {
    "detective": Look(
        "detective", height=41, shoulder=12, waist=9, hip=9, leg_w=3, leg_gap=1, arm_w=3,
        boot_h=3, skirt=8, skirt_kind="coat",
        mats={"hair": "#2b1f1c", "hair2": "#2b1f1c", "skin": "#c99a78", "eye": "#1a1216",
              "coat": "#5a4130", "inner": "#4a4636", "shirt": "#e2dccb", "tie": "#5b2a2c",
              "legs": "#2f2c30", "boot": "#5a3a26", "sole": "#241713"},
        heads=HEADS_DETECTIVE),
    # Before he takes his coat from the stand: shirt sleeves and waistcoat.
    "detective_shirt": Look(
        "detective_shirt", height=41, shoulder=11, waist=9, hip=9, leg_w=3, leg_gap=1, arm_w=3,
        boot_h=3, skirt=0, skirt_kind="none",
        mats={"hair": "#2b1f1c", "hair2": "#2b1f1c", "skin": "#c99a78", "eye": "#1a1216",
              "coat": "#d9d2c0", "inner": "#3f3b2e", "shirt": "#e2dccb", "tie": "#5b2a2c",
              "legs": "#2f2c30", "boot": "#5a3a26", "sole": "#241713"},
        heads=HEADS_DETECTIVE),
    "engineer": Look(
        "engineer", height=43, shoulder=14, waist=11, hip=10, leg_w=4, leg_gap=1, arm_w=3,
        boot_h=3, skirt=6, skirt_kind="apron", cuff="wrap",
        mats={"hair": "#6e5038", "hair2": "#6e5038", "skin": "#c8936c", "eye": "#1d1514",
              "coat": "#b0582c", "inner": "#b0582c", "shirt": "#b0582c", "tie": "#7a3a1c",
              "apron": "#7b5536", "legs": "#2f3b55", "boot": "#3a2c25", "sole": "#1c1512",
              "wrap": "#cfc4a6"},
        heads=HEADS_ENGINEER),
    "chef": Look(
        "chef", height=40, shoulder=13, waist=13, hip=12, leg_w=4, leg_gap=1, arm_w=3,
        boot_h=2, skirt=9, skirt_kind="apron",
        mats={"hair": "#211b1d", "hair2": "#8d8a86", "skin": "#c08a62", "eye": "#1a1214",
              "coat": "#e6e0d0", "inner": "#e6e0d0", "shirt": "#e6e0d0", "tie": "#9a8f7a",
              "apron": "#4f3a2c", "legs": "#2c2a2e", "boot": "#1e1b1d", "sole": "#141214"},
        heads=HEADS_CHEF),
    "musician": Look(
        "musician", height=40, shoulder=10, waist=7, hip=9, leg_w=3, leg_gap=0, arm_w=2,
        boot_h=3, skirt=5, skirt_kind="jacket",
        mats={"hair": "#1e1b24", "hair2": "#9aa3ad", "skin": "#e0bea2", "eye": "#17141b",
              "coat": "#262833", "inner": "#262833", "shirt": "#262833", "tie": "#9aa2aa",
              "legs": "#4a4a50", "boot": "#8a7560", "sole": "#3e3128"},
        heads=HEADS_MUSICIAN),
}


# ----------------------------------------------------------------------------
# Material buffer
# ----------------------------------------------------------------------------

class Paint:
    """Material-indexed canvas: each pixel keeps (material, level, layer)."""

    def __init__(self, look: Look):
        self.look = look
        self.mat = np.full((CELL, CELL), -1, dtype=np.int16)
        self.lvl = np.zeros((CELL, CELL), dtype=np.int8)
        self.lay = np.zeros((CELL, CELL), dtype=np.int8)
        self.names = list(look.mats)
        self.ramps = [ramp(look.mats[n], warm=n == "skin") for n in self.names]

    def put(self, x: int, y: int, mat: str, level: int, layer: int) -> None:
        if 0 <= x < CELL and 0 <= y < CELL:
            self.mat[y, x] = self.names.index(mat)
            self.lvl[y, x] = max(0, min(4, level))
            self.lay[y, x] = layer

    def has(self, x: int, y: int) -> bool:
        return 0 <= x < CELL and 0 <= y < CELL and self.mat[y, x] >= 0

    def shade(self, x: int, y: int, delta: int) -> None:
        if self.has(x, y):
            self.lvl[y, x] = max(0, min(4, self.lvl[y, x] + delta))

    def render(self) -> Canvas:
        # occlusion contour: a pixel touching a higher layer drops one level
        lv = self.lvl.copy()
        for y in range(CELL):
            for x in range(CELL):
                if self.mat[y, x] < 0:
                    continue
                for dx, dy in ((0, -1), (1, 0), (-1, 0)):
                    nx, ny = x + dx, y + dy
                    if self.has(nx, ny) and self.lay[ny, nx] > self.lay[y, x] + 0:
                        if self.mat[ny, nx] != self.mat[y, x] or self.lay[ny, nx] >= self.lay[y, x] + 2:
                            lv[y, x] = max(0, lv[y, x] - 1)
                            break
        c = Canvas(CELL, CELL)
        for y in range(CELL):
            for x in range(CELL):
                m = self.mat[y, x]
                if m >= 0:
                    c.a[y, x] = self.ramps[m][lv[y, x]]
        # contour: the darkest step of what it wraps, pulled toward one shared ink
        ink = np.array(INK[:3], dtype=float)
        out = c.a.copy()
        for y in range(CELL):
            for x in range(CELL):
                if self.mat[y, x] >= 0:
                    continue
                for dx, dy in ((0, 1), (0, -1), (-1, 0), (1, 0)):
                    nx, ny = x + dx, y + dy
                    if self.has(nx, ny):
                        base = np.array(self.ramps[self.mat[ny, nx]][0][:3], dtype=float)
                        out[y, x, :3] = (base * 0.45 + ink * 0.55).astype(np.uint8)
                        out[y, x, 3] = 255
                        break
        c.a = out
        c.harden()
        return c


# ----------------------------------------------------------------------------
# Pose
# ----------------------------------------------------------------------------

@dataclass
class Hand:
    out: float = 1.0       # lateral distance from shoulder (front view)
    down: float = 0.0      # vertical drop from shoulder, 0 = use arm length
    fwd: float = 0.0       # toward facing direction
    swing: float = 0.0     # walk swing along facing axis


@dataclass
class Pose:
    bob: int = 0
    crouch: int = 0        # hip drop with knees bending
    sit: bool = False
    lean: int = 0          # head/torso forward (side) or down (front)
    look_down: bool = False
    mouth: bool = False
    near: Hand = field(default_factory=Hand)   # screen-right arm in front view
    far: Hand = field(default_factory=Hand)
    stride: float = 0.0    # walk phase -1..1 (near leg forward when positive)
    lift_near: int = 0
    lift_far: int = 0


def walk_pose(i: int) -> Pose:
    a = 2 * math.pi * i / 8
    s = math.sin(a)
    lift = abs(math.cos(a))
    p = Pose(bob=1 if i % 4 in (1, 2) else 0, stride=s)
    p.lift_near = 1 if (s < 0 and lift > 0.6) else 0
    p.lift_far = 1 if (s > 0 and lift > 0.6) else 0
    p.near = Hand(swing=-2.6 * s)
    p.far = Hand(swing=2.6 * s)
    return p


def action_pose(action: str, i: int) -> Pose:
    if action == "idle":
        return Pose()
    if action == "walk":
        return walk_pose(i)
    if action == "talk":
        return [Pose(near=Hand(out=2, down=-8, fwd=3)),
                Pose(mouth=True, near=Hand(out=4, down=-11, fwd=4), far=Hand(out=1, down=-2, fwd=1)),
                Pose(near=Hand(out=2, down=-7, fwd=3))][i]
    if action == "inspect":
        return [Pose(lean=1, look_down=True, near=Hand(out=0, down=-4, fwd=2)),
                Pose(crouch=4, lean=2, look_down=True, near=Hand(out=1, down=-3, fwd=4), far=Hand(out=2, down=-3, fwd=2)),
                Pose(crouch=8, lean=3, look_down=True, near=Hand(out=1, down=-1, fwd=6), far=Hand(out=2, down=-4, fwd=3)),
                Pose(crouch=3, lean=1, look_down=True, near=Hand(out=1, down=-3, fwd=3))][i]
    if action == "interact":
        return [Pose(near=Hand(out=1, down=-3, fwd=2)),
                Pose(lean=1, near=Hand(out=0, down=-6, fwd=6)),
                Pose(lean=1, near=Hand(out=0, down=-7, fwd=7)),
                Pose(near=Hand(out=1, down=-3, fwd=3))][i]
    if action == "sit":
        return [Pose(crouch=2),
                Pose(crouch=5, lean=1, near=Hand(out=1, down=-2, fwd=2), far=Hand(out=1, down=-2, fwd=2)),
                Pose(sit=True, crouch=8, near=Hand(out=1, down=-4, fwd=3), far=Hand(out=1, down=-4, fwd=3)),
                Pose(sit=True, crouch=8, near=Hand(out=0, down=-5, fwd=4), far=Hand(out=0, down=-5, fwd=4))][i]
    if action == "break":
        return [Pose(crouch=1, near=Hand(out=3, down=-4, fwd=1), far=Hand(out=2, down=-3)),
                Pose(near=Hand(out=5, down=-20, fwd=1), far=Hand(out=2, down=-3)),
                Pose(bob=-1, near=Hand(out=0, down=-31, fwd=0), far=Hand(out=3, down=-4)),
                Pose(crouch=5, lean=2, near=Hand(out=0, down=-1, fwd=6), far=Hand(out=3, down=-3, fwd=2)),
                Pose(crouch=7, lean=3, look_down=True, near=Hand(out=0, down=2, fwd=6), far=Hand(out=3, down=-2, fwd=3)),
                Pose(crouch=1, near=Hand(out=2, down=-2, fwd=1))][i]
    if action == "cast":
        return [Pose(),
                Pose(near=Hand(out=-2, down=-6, fwd=3), far=Hand(out=-2, down=-6, fwd=3)),
                Pose(bob=-1, near=Hand(out=-1, down=-30, fwd=1), far=Hand(out=-1, down=-30, fwd=1)),
                Pose(bob=-1, mouth=True, near=Hand(out=12, down=-26, fwd=1), far=Hand(out=12, down=-26, fwd=1)),
                Pose(mouth=True, near=Hand(out=13, down=-16, fwd=1), far=Hand(out=13, down=-16, fwd=1)),
                Pose(near=Hand(out=8, down=-7), far=Hand(out=8, down=-7)),
                Pose(near=Hand(out=2, down=-1), far=Hand(out=2, down=-1))][i]
    raise ValueError(action)


# ----------------------------------------------------------------------------
# Drawing
# ----------------------------------------------------------------------------

L_FAR_ARM, L_FAR_LEG, L_LEG, L_SKIRT, L_TORSO, L_ARM, L_HEAD = 1, 2, 3, 4, 5, 7, 8


def stamp_line(p: Paint, x0: float, y0: float, x1: float, y1: float, w: int,
               mat: str, level: int, layer: int, shade_side: int = 1) -> None:
    """Thick limb along a line; `shade_side` +1 shades the right edge, -1 the left."""
    n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for k in range(n):
        t = k / max(n - 1, 1)
        cx, cy = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        left = int(math.floor(cx - w / 2 + 0.5))
        for dx in range(w):
            lv = level
            if w >= 3 and dx == (w - 1 if shade_side > 0 else 0):
                lv = level - 1
            if w >= 3 and dx == (0 if shade_side > 0 else w - 1):
                lv = level + 1
            p.put(left + dx, int(round(cy)), mat, lv, layer)


def draw_head(p: Paint, look: Look, facing: str, x0: int, y0: int, pose: Pose) -> None:
    rows = look.heads["front" if facing == "front" else facing]
    for r, line in enumerate(rows):
        for c, ch in enumerate(line):
            if ch == ".":
                continue
            m, lv = {
                "k": ("hair", 0), "h": ("hair", 1), "H": ("hair", 2), "L": ("hair", 3),
                "g": ("hair2", 1), "G": ("hair2", 2),
                "s": ("skin", 1), "S": ("skin", 2), "T": ("skin", 3), "n": ("skin", 1),
                "e": ("eye", 1), "b": ("hair", 0), "m": ("skin", 1),
            }[ch]
            if ch == "e" and pose.look_down:
                m, lv = ("skin", 0)
            if ch == "m" and pose.mouth:
                m, lv = ("eye", 2)
            p.put(x0 + c, y0 + r, m, lv, L_HEAD)
    if facing == "front":   # warm light on the forehead
        for c, ch in enumerate(rows[5]):
            if ch == "S":
                p.put(x0 + c, y0 + 5, "skin", 3, L_HEAD)
                break


def half_widths(look: Look, rows: int) -> list[float]:
    out = []
    for r in range(rows):
        t = r / max(rows - 1, 1)
        if r == 0:
            w = look.shoulder - 2
        elif t < 0.55:
            w = look.shoulder + (look.waist - look.shoulder) * (t / 0.55)
        else:
            w = look.waist + (look.hip - look.waist) * ((t - 0.55) / 0.45)
        out.append(w / 2)
    return out


def draw_figure(look: Look, facing: str, pose: Pose) -> Canvas:
    p = Paint(look)
    H = look.height
    leg_h = round(H * 0.42)
    torso_h = H - HEAD_H - leg_h
    drop = pose.crouch + pose.bob
    top = GROUND - H + 1 + drop
    shoulder_y = top + HEAD_H
    crotch_y = shoulder_y + torso_h
    side = facing == "left"
    lean = pose.lean if side else 0
    head_x = CX - 5 - lean
    head_y = top + (1 if (pose.look_down and not side and pose.lean >= 2) else 0)

    torso_cx = CX - (lean // 2 if side else 0)
    draw_legs(p, look, facing, pose, crotch_y, leg_h - pose.crouch, torso_cx)
    draw_torso(p, look, facing, pose, shoulder_y, torso_h, torso_cx)
    # neck
    draw_arms(p, look, facing, pose, shoulder_y, torso_h, torso_cx)
    draw_head(p, look, facing, head_x, head_y, pose)
    return p.render()


def draw_torso(p: Paint, look: Look, facing: str, pose: Pose, sy: int, th: int, cx: int) -> None:
    hw = half_widths(look, th)
    side = facing == "left"
    for r in range(th):
        half = hw[r] * (0.74 if side else 1.0)
        x0 = int(round(cx - half))
        x1 = int(round(cx + half))
        if side:
            x0 -= 1 if r > 1 else 0
        for x in range(x0, x1):
            u = (x - x0) / max(x1 - x0 - 1, 1)
            lv = 3 if u < 0.15 else (1 if u > 0.78 else 2)
            if r == 0:
                lv = min(lv + 1, 4)
            p.put(x, sy + r, "coat", lv, L_TORSO)
    GARMENTS[look.name](p, look, facing, pose, sy, th, cx, hw)


def skirt_rows(look: Look, pose: Pose) -> int:
    if look.skirt <= 0:
        return 0
    return max(2, look.skirt - pose.crouch - (4 if pose.sit else 0))


def fill_skirt(p: Paint, look: Look, facing: str, pose: Pose, y0: int, cx: int, mat: str,
               open_front: bool, flare: int = 1, layer: int = L_SKIRT) -> None:
    rows = skirt_rows(look, pose)
    side = facing == "left"
    base = look.hip / 2 * (0.78 if side else 1.0)
    for r in range(rows):
        half = base + flare * (r / max(rows - 1, 1)) + (0.5 if not side else 0)
        x0, x1 = int(round(cx - half)) - (1 if side else 0), int(round(cx + half))
        if side and pose.stride:
            x0 += int(round(-pose.stride * r / rows))
        for x in range(x0, x1):
            if open_front and facing == "front" and cx - 1 <= x <= cx and r > 0:
                continue
            u = (x - x0) / max(x1 - x0 - 1, 1)
            lv = 3 if u < 0.12 else (1 if u > 0.8 else 2)
            if r == rows - 1:
                lv -= 1
            p.put(x, y0 + r, mat, lv, layer)


def draw_legs(p: Paint, look: Look, facing: str, pose: Pose, crotch: int, leg_h: int, cx: int) -> None:
    side = facing == "left"
    lw, gap = look.leg_w, look.leg_gap
    if pose.sit:
        if side:   # thigh forward, shin down
            knee_x = cx - 8
            for leg, (mat_shift, layer) in enumerate(((0, L_LEG), (-1, L_FAR_LEG))):
                ox = leg * 1
                for x in range(knee_x + ox, cx + 2 + ox):
                    for y in range(crotch - 1, crotch + lw - 1):
                        p.put(x, y, "legs", 2 + mat_shift + (1 if y == crotch - 1 else 0), layer)
                shin_top = crotch + lw - 1
                for y in range(shin_top, GROUND + 1):
                    boot = y > GROUND - look.boot_h
                    for x in range(knee_x + ox, knee_x + ox + lw):
                        p.put(x, y, "boot" if boot else "legs", 2 + mat_shift - (1 if x == knee_x + ox + lw - 1 else 0), layer)
                p.put(knee_x + ox - 1, GROUND, "sole", 2, layer)
                p.put(knee_x + ox - 1, GROUND - 1, "boot", 2 + mat_shift, layer)
            return
        # front/back: foreshortened thighs then shins
        for k, sgn in enumerate((-1, 1)):
            x_in = cx + (0 if sgn > 0 else -1) + sgn * (gap // 2 + (1 if gap % 2 and sgn > 0 else 0))
            xs = [x_in + sgn * d for d in range(lw + 1)]
            for y in range(crotch - 1, crotch + 2):
                for x in xs:
                    p.put(x, y, "legs", 3 if y == crotch - 1 else 2, L_LEG)
            for y in range(crotch + 2, GROUND + 1):
                boot = y > GROUND - look.boot_h
                for j, x in enumerate(xs[:lw]):
                    lv = 2 - (1 if sgn > 0 else 0) + (1 if j == 0 and sgn < 0 else 0)
                    p.put(x, y, "sole" if y == GROUND else ("boot" if boot else "legs"), lv, L_LEG)
        return

    if side:
        stride = pose.stride
        for leg, sgn, lift, layer, dim in ((0, 1, pose.lift_near, L_LEG, 0), (1, -1, pose.lift_far, L_FAR_LEG, -1)):
            foot_dx = -sgn * stride * 3.5
            knee_dx = foot_dx * 0.5 - (pose.crouch * 0.6)
            hip_x = cx - 1 + (0 if leg == 0 else 1)
            knee_y = crotch + leg_h // 2
            foot_y = GROUND - lift
            stamp_line(p, hip_x, crotch, hip_x + knee_dx, knee_y, lw, "legs", 2 + dim, layer, 1)
            shin_end = foot_y - look.boot_h + 1
            stamp_line(p, hip_x + knee_dx, knee_y, hip_x + foot_dx, shin_end, lw, "legs", 2 + dim, layer, 1)
            fx = int(round(hip_x + foot_dx - lw / 2 + 0.5))
            for y in range(shin_end, foot_y + 1):
                for x in range(fx - (1 if y >= foot_y - 1 else 0), fx + lw):
                    sole = y == foot_y
                    p.put(x, y, "sole" if sole else "boot", (2 if not sole else 2) + dim + (1 if y == shin_end else 0), layer)
        return

    # front / back
    for sgn in (-1, 1):
        lift = pose.lift_near if sgn > 0 else pose.lift_far
        x_in = cx + (0 if sgn > 0 else -1) + sgn * ((gap + 1) // 2 if sgn > 0 else gap // 2)
        knee_out = (pose.crouch // 3) * sgn
        knee_y = crotch + leg_h // 2
        foot_y = GROUND - lift
        for y in range(crotch - 1, foot_y + 1):
            t = (y - crotch) / max(foot_y - crotch, 1)
            bend = knee_out * (1 - abs(2 * t - 1)) if pose.crouch else 0
            boot = y > foot_y - look.boot_h
            sole = y == foot_y
            for j in range(lw + (1 if (sole or y == foot_y - 1) and look.boot_h > 2 else 0)):
                x = x_in + sgn * j + int(round(bend))
                inner = j == 0
                lv = 2
                if sgn > 0:
                    lv = 1 if j >= lw - 1 else 2
                else:
                    lv = 3 if j >= lw - 1 else 2
                if inner and not boot:
                    lv -= 1
                mat = "sole" if sole else ("boot" if boot else "legs")
                if mat == "boot" and y == foot_y - look.boot_h + 1:
                    lv += 1
                p.put(x, y, mat, lv, L_LEG)


def arm_points(look: Look, facing: str, hand: Hand, sgn: int, sy: int, th: int, cx: int, shoulder_half: float):
    reach = th + 4
    if facing == "left":
        sx = cx + (0 if sgn > 0 else 1)
        down = hand.down if hand.down else 0
        hx = sx - hand.fwd - hand.swing
        hy = sy + 1 + reach + down
        if hand.down:
            hy = sy + 1 + reach + hand.down
        return sx, sy + 1, hx, hy
    out_sgn = sgn if facing == "front" else -sgn
    sx = cx + (shoulder_half - 0.5 if sgn > 0 else -shoulder_half - 0.5) - sgn * (look.arm_w // 2) + sgn * 1
    hx = sx + sgn * (hand.out - 0.5) - sgn * hand.fwd * 0.55
    hy = sy + 1 + reach + hand.down - hand.fwd * 0.25 + abs(hand.swing) * 0.3
    return sx, sy + 1, hx, hy


def draw_arms(p: Paint, look: Look, facing: str, pose: Pose, sy: int, th: int, cx: int) -> None:
    half = look.shoulder / 2
    for sgn, hand, near in ((1, pose.near, True), (-1, pose.far, False)):
        sx, sy1, hx, hy = arm_points(look, facing, hand, sgn, sy, th, cx, half)
        if facing == "left":
            layer = L_ARM if near else L_FAR_ARM
            dim = 0 if near else -1
        elif facing == "back":
            layer = L_FAR_ARM if hand.fwd > 2.5 else L_ARM
            dim = 0
        else:
            layer = L_ARM
            dim = 0
        # two-bone IK: upper arm and forearm each half the reach
        seg = (th + 4) / 2
        dx, dy = hx - sx, hy - sy1
        dist = math.hypot(dx, dy)
        if dist > 2 * seg:
            k = 2 * seg / dist
            hx, hy = sx + dx * k, sy1 + dy * k
            dx, dy, dist = hx - sx, hy - sy1, 2 * seg
        mx, my = sx + dx / 2, sy1 + dy / 2
        h = math.sqrt(max(seg * seg - (dist / 2) ** 2, 0.0))
        nx, ny = (-dy / dist, dx / dist) if dist else (1.0, 0.0)
        if facing == "left":
            want = -1.0 if hy < sy1 + 2 else 1.0          # raised: elbow forward, low: back
            pref_x = want
        else:
            pref_x = float(sgn)                            # elbows bow away from the body
        if nx * pref_x < 0 or (abs(nx) < 1e-6 and ny > 0):
            nx, ny = -nx, -ny
        ex, ey = mx + nx * h, my + ny * h
        shade = 1 if sgn > 0 else -1
        if facing == "back":
            shade = -shade
        stamp_line(p, sx, sy1, ex, ey, look.arm_w, "coat", 2 + dim, layer, shade)
        stamp_line(p, ex, ey, hx, hy - 1, look.arm_w, "coat", 2 + dim, layer, shade)
        # cuff and hand
        hxr, hyr = int(round(hx)), int(round(hy))
        cuff_mat = look.mats.get("wrap") and "wrap" or "shirt"
        hand_w = 2
        left = hxr - (1 if look.arm_w >= 3 else 0)
        if look.cuff or look.name != "musician":
            for dx in range(min(look.arm_w, 3)):
                p.put(left + dx - (0 if look.arm_w < 4 else 0), hyr - 1, cuff_mat, 2 + dim, layer)
        for dy in range(2):
            for dx in range(hand_w):
                p.put(left + dx, hyr + dy, "skin", max(1, 2 + dim - (1 if dx == 1 and dy == 1 else 0)), layer)


# ----------------------------------------------------------------------------
# Garments (front / back / left detailing on top of the coat silhouette)
# ----------------------------------------------------------------------------

def g_detective(p, look, facing, pose, sy, th, cx, hw):
    if facing == "front":
        # open coat: shirt, cravat and dark waistcoat in the front opening
        for r in range(1, th):
            w = 1 if r < 2 else 2
            for x in range(cx - w, cx + w):
                mat, lv = ("inner", 2 if x < cx else 1)
                if r <= 3 and x in (cx - 1, cx):
                    mat, lv = ("shirt", 3 if x < cx else 2)
                if r in (1, 2, 3) and x in (cx - 1, cx) and r >= 2:
                    mat, lv = ("tie", 2)
                p.put(x, sy + r, mat, lv, L_TORSO)
        # lapels catching light
        for r in range(1, 5):
            p.put(cx - 2 - (1 if r < 3 else 0), sy + r, "coat", 4, L_TORSO)
            p.put(cx + 1 + (1 if r < 3 else 0), sy + r, "coat", 1, L_TORSO)
        # shirt collar points
        p.put(cx - 1, sy, "shirt", 3, L_TORSO)
        p.put(cx, sy, "shirt", 2, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=True)
        # pocket flaps
        p.put(cx - 4, sy + th - 2, "coat", 1, L_TORSO)
        p.put(cx + 3, sy + th - 2, "coat", 1, L_TORSO)
    elif facing == "back":
        for r in range(2, th):
            p.put(cx - 1, sy + r, "coat", 1, L_TORSO)   # centre seam
        p.put(cx - 2, sy + th - 3, "coat", 1, L_TORSO)
        p.put(cx + 1, sy + th - 3, "coat", 1, L_TORSO)  # half belt
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False)
        rows = skirt_rows(look, pose)
        for r in range(2, rows):
            p.put(cx - 1, sy + th + r, "coat", 0, L_SKIRT)  # vent
    else:
        for r in range(1, 4):
            p.put(cx - 3 - (1 if r > 1 else 0), sy + r, "shirt", 3 if r == 1 else 2, L_TORSO)
        p.put(cx - 3, sy + 2, "tie", 2, L_TORSO)
        p.put(cx - 2, sy + 4, "coat", 4, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False)


def g_engineer(p, look, facing, pose, sy, th, cx, hw):
    if facing == "front":
        # open neck, placket and chest pockets on the heavy shirt
        p.put(cx - 1, sy, "skin", 1, L_TORSO)
        p.put(cx, sy, "skin", 1, L_TORSO)
        p.put(cx - 1, sy + 1, "skin", 2, L_TORSO)
        for r in range(2, th - 4):
            p.put(cx - 1 + (r % 2 == 0), sy + r, "tie", 2, L_TORSO)
        for x in (cx - 5, cx - 4, cx + 3, cx + 4):
            p.put(x, sy + 3, "tie", 2, L_TORSO)
        # leather waist apron with strap
        for x in range(int(cx - hw[th - 4]), int(cx + hw[th - 4])):
            p.put(x, sy + th - 4, "apron", 1, L_TORSO)
        for r in range(th - 3, th):
            for x in range(int(round(cx - hw[r])) + 1, int(round(cx + hw[r])) - 1):
                p.put(x, sy + r, "apron", 3 if x < cx - 2 else 2, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx, "apron", open_front=False, flare=0)
    elif facing == "back":
        # braces-free broad back; apron tie and waistband
        for x in range(int(cx - hw[th - 4]), int(cx + hw[th - 4])):
            p.put(x, sy + th - 4, "apron", 1, L_TORSO)
        p.put(cx - 1, sy + th - 3, "apron", 3, L_TORSO)
        p.put(cx, sy + th - 3, "apron", 2, L_TORSO)
        p.put(cx - 2, sy + th - 2, "apron", 2, L_TORSO)
        p.put(cx + 1, sy + th - 2, "apron", 2, L_TORSO)
        for r in range(1, 5):
            p.put(cx - 4, sy + r, "coat", 1, L_TORSO)
            p.put(cx + 3, sy + r, "coat", 1, L_TORSO)   # yoke seams
    else:
        p.put(cx - 4, sy + 1, "skin", 1, L_TORSO)
        x_front = int(round(cx - hw[th - 1] * 0.74)) - 1
        for x in range(x_front, cx + 3):
            p.put(x, sy + th - 4, "apron", 1, L_TORSO)          # waist strap round the body
        for r in range(th - 3, th):
            for x in range(x_front, x_front + 3):
                p.put(x, sy + r, "apron", 3 if x == x_front else 2, L_TORSO)
        rows = skirt_rows(look, pose)
        for r in range(rows):
            shift_x = int(round(-pose.stride * r / max(rows, 1)))
            for x in range(x_front + shift_x, x_front + shift_x + 3):
                p.put(x, sy + th + r, "apron", 1 if r == rows - 1 else (3 if x == x_front + shift_x else 2), L_SKIRT)


def g_chef(p, look, facing, pose, sy, th, cx, hw):
    if facing == "front":
        # stand collar, frog buttons; dark apron from the chest down
        for x in range(cx - 2, cx + 2):
            p.put(x, sy, "coat", 3, L_TORSO)
        p.put(cx - 1, sy + 1, "tie", 2, L_TORSO)
        bib_top = sy + 4
        for r in range(4, th):
            half = 3 if r < 6 else int(hw[r]) - 1
            for x in range(cx - half, cx + half):
                u = (x - (cx - half)) / max(2 * half - 1, 1)
                p.put(x, sy + r, "apron", 3 if u < 0.15 else (1 if u > 0.8 else 2), L_TORSO)
        for r in (2, 3):
            p.put(cx - 2, sy + r, "tie", 2, L_TORSO)
            p.put(cx + 1, sy + r, "tie", 2, L_TORSO)
        p.put(cx - 4, bib_top - 2, "apron", 1, L_TORSO)   # neck strap
        p.put(cx + 3, bib_top - 2, "apron", 1, L_TORSO)
        p.put(cx - 4, bib_top - 1, "apron", 1, L_TORSO)
        p.put(cx + 3, bib_top - 1, "apron", 1, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx, "apron", open_front=False, flare=1)
    elif facing == "back":
        for x in range(cx - 2, cx + 2):
            p.put(x, sy, "coat", 3, L_TORSO)
        # crossed apron straps and waist tie
        for r in range(1, th - 3):
            t = r / (th - 4)
            p.put(int(round(cx - 4 + 7 * t)), sy + r, "apron", 1, L_TORSO)
            p.put(int(round(cx + 3 - 7 * t)), sy + r, "apron", 1, L_TORSO)
        for x in range(int(cx - hw[th - 3]), int(cx + hw[th - 3])):
            p.put(x, sy + th - 3, "apron", 1, L_TORSO)
        p.put(cx - 1, sy + th - 2, "apron", 2, L_TORSO)
        p.put(cx, sy + th - 1, "apron", 2, L_TORSO)
    else:
        for r in range(4, th):
            for x in range(cx - 5, cx - 2):
                p.put(x, sy + r, "apron", 2 if x > cx - 5 else 3, L_TORSO)
        p.put(cx - 2, sy + 1, "tie", 2, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx - 1, "apron", open_front=False, flare=1)


def g_musician(p, look, facing, pose, sy, th, cx, hw):
    if facing == "front":
        # high collar with a fine silver edge; symmetric front closure
        for x in range(cx - 2, cx + 2):
            p.put(x, sy - 1, "coat", 3 if x < cx else 2, L_TORSO)
        p.put(cx - 2, sy, "tie", 3, L_TORSO)
        p.put(cx + 1, sy, "tie", 2, L_TORSO)
        for r in range(1, th):
            p.put(cx, sy + r, "coat", 0, L_TORSO)                 # centre opening
        for r in (2, 5, 8):                                       # paired frog fastenings
            p.put(cx - 1, sy + r, "tie", 3, L_TORSO)
            p.put(cx + 1, sy + r, "tie", 1, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False, flare=1)
        rows = skirt_rows(look, pose)
        for r in range(rows):
            p.put(cx, sy + th + r, "coat", 0, L_SKIRT)
        for side_x in (int(round(cx - look.hip / 2)) - 1, int(round(cx + look.hip / 2))):
            for r in range(max(0, rows - 3), rows):              # side slits
                p.put(side_x, sy + th + r, "legs", 1, L_SKIRT)
    elif facing == "back":
        for x in range(cx - 2, cx + 2):
            p.put(x, sy - 1, "coat", 2, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False, flare=1)
    else:
        p.put(cx - 2, sy - 1, "coat", 3, L_TORSO)
        p.put(cx - 1, sy - 1, "coat", 2, L_TORSO)
        p.put(cx - 3, sy, "tie", 3, L_TORSO)
        for r in (2, 5, 8):
            p.put(cx - 3, sy + r, "tie", 2, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False, flare=1)


def g_detective_shirt(p, look, facing, pose, sy, th, cx, hw):
    """Waistcoat over the shirt; the sleeves stay the shirt's linen."""
    side = facing == "left"
    for r in range(1, th):
        half = hw[r] * (0.74 if side else 1.0) - (1 if r < 3 else 0)
        x0, x1 = int(round(cx - half)), int(round(cx + half))
        if side:
            x0 -= 1 if r > 1 else 0
        for x in range(x0, x1):
            u = (x - x0) / max(x1 - x0 - 1, 1)
            if facing == "front" and r <= 3 and cx - 1 <= x <= cx:
                continue                                  # shirt front and cravat show
            p.put(x, sy + r, "inner", 3 if u < 0.15 else (1 if u > 0.8 else 2), L_TORSO)
    if facing == "front":
        p.put(cx - 1, sy, "shirt", 3, L_TORSO)
        p.put(cx, sy, "shirt", 2, L_TORSO)
        p.put(cx - 1, sy + 2, "tie", 2, L_TORSO)
        p.put(cx, sy + 2, "tie", 2, L_TORSO)
        for r in (5, 7, 9):
            p.put(cx - 1, sy + r, "tie", 1, L_TORSO)      # waistcoat buttons
    elif facing == "back":
        for x in range(cx - 2, cx + 2):
            p.put(x, sy + th - 3, "inner", 1, L_TORSO)    # back strap
    else:
        p.put(cx - 3, sy + 1, "shirt", 3, L_TORSO)
        p.put(cx - 3, sy + 2, "tie", 2, L_TORSO)


GARMENTS = {"detective": g_detective, "detective_shirt": g_detective_shirt, "engineer": g_engineer, "chef": g_chef, "musician": g_musician}


# ----------------------------------------------------------------------------
# Atlas
# ----------------------------------------------------------------------------

def build_atlas(name: str) -> Image.Image:
    look = LOOKS[name]
    atlas = Image.new("RGBA", (8 * CELL, 24 * CELL), (0, 0, 0, 0))
    for d, facing in enumerate(FACINGS):
        for a, (action, count) in enumerate(ACTIONS):
            for i in range(count):
                frame = draw_figure(look, facing, action_pose(action, i)).image()
                atlas.alpha_composite(frame, (i * CELL, (d * 8 + a) * CELL))
    return atlas


def validate(atlas: Image.Image) -> list[str]:
    problems = []
    a = np.array(atlas)
    for d, facing in enumerate(FACINGS):
        for k, (action, count) in enumerate(ACTIONS):
            for i in range(8):
                cell = a[(d * 8 + k) * CELL:(d * 8 + k + 1) * CELL, i * CELL:(i + 1) * CELL, 3]
                filled = cell.any()
                if i < count and not filled:
                    problems.append(f"{facing}/{action}/{i} empty")
                if i >= count and filled:
                    problems.append(f"{facing}/{action}/{i} should be empty")
                if filled:
                    if set(np.unique(cell)) - {0, 255}:
                        problems.append(f"{facing}/{action}/{i} non-binary alpha")
                    if cell[0].any() or cell[-1].any() or cell[:, 0].any() or cell[:, -1].any():
                        problems.append(f"{facing}/{action}/{i} touches cell edge")
                    ys = np.where(cell.any(axis=1))[0]
                    if ys.max() != GROUND + 1:    # outline sits one row below the sole
                        problems.append(f"{facing}/{action}/{i} lowest row {ys.max()} != {GROUND + 1}")
    return problems


def publish(game: Path, names=tuple(LOOKS)) -> None:
    source = game / "AssetsSource/Internal/Characters/NativeCast"
    runtime = game / "ShareAsset/Internal/Characters/animation_v2"
    source.mkdir(parents=True, exist_ok=True)
    for name in names:
        atlas = build_atlas(name)
        problems = validate(atlas)
        if problems:
            raise SystemExit(f"{name}: " + "; ".join(problems[:8]))
        atlas.save(source / f"{name}.png")
        atlas.save(runtime / f"{name}.png")
        print(f"{name}: 111 frames published")


def contact_sheet(path: Path, scale: int = 3) -> None:
    """Idle in three facings plus the walk cycle, for review."""
    tiles = []
    for name in LOOKS:
        look = LOOKS[name]
        row = [draw_figure(look, f, Pose()).image() for f in FACINGS]
        row += [draw_figure(look, "left", walk_pose(i)).image() for i in range(8)]
        tiles.append(row)
    sheet = Image.new("RGBA", (11 * CELL, len(tiles) * CELL), (96, 88, 80, 255))
    for r, row in enumerate(tiles):
        for c, im in enumerate(row):
            sheet.alpha_composite(im, (c * CELL, r * CELL))
    sheet.resize((sheet.width * scale, sheet.height * scale), Image.NEAREST).save(path)


if __name__ == "__main__":
    publish(Path(__file__).resolve().parents[2])
