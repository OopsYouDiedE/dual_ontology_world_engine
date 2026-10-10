"""Native pixel cast: the life-wing four (detective, Halvard, Shen Ding, Su Ling)
and the twelve other candidates of the 21-figure identity sheet.

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
from dataclasses import dataclass, field, replace
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
    # contact frames (feet widest) sit one pixel low; the passing leg lifts
    p = Pose(bob=1 if abs(s) > 0.9 else 0, stride=s)
    p.lift_near = 2 if (s < 0 and lift > 0.6) else (1 if s < -0.5 else 0)
    p.lift_far = 2 if (s > 0 and lift > 0.6) else (1 if s > 0.5 else 0)
    p.near = Hand(swing=-3.6 * s)
    p.far = Hand(swing=3.6 * s)
    return p


def action_pose(action: str, i: int) -> Pose:
    if action == "idle":
        return Pose()
    if action == "walk":
        return walk_pose(i)
    if action == "talk":
        return [Pose(near=Hand(out=-1, down=-8, fwd=4)),
                Pose(mouth=True, near=Hand(out=2, down=-10, fwd=4), far=Hand(out=0, down=-2, fwd=1)),
                Pose(near=Hand(out=0, down=-7, fwd=4))][i]
    if action == "inspect":
        return [Pose(lean=1, look_down=True, near=Hand(out=-2, down=-11, fwd=4)),
                Pose(crouch=4, lean=2, look_down=True, near=Hand(out=1, down=-3, fwd=4), far=Hand(out=2, down=-3, fwd=2)),
                Pose(crouch=8, lean=3, look_down=True, near=Hand(out=1, down=-1, fwd=6), far=Hand(out=2, down=-4, fwd=3)),
                Pose(crouch=3, lean=1, look_down=True, near=Hand(out=1, down=-3, fwd=3))][i]
    if action == "interact":
        return [Pose(near=Hand(out=1, down=-3, fwd=2)),
                Pose(lean=1, near=Hand(out=0, down=-6, fwd=6)),
                Pose(lean=1, near=Hand(out=0, down=-7, fwd=7)),
                Pose(near=Hand(out=1, down=-3, fwd=3))][i]
    if action == "sit":
        return [Pose(crouch=2, near=Hand(out=1, down=-1, fwd=1), far=Hand(out=1, down=-1, fwd=1)),
                Pose(crouch=5, lean=1, near=Hand(out=0, down=-3, fwd=3), far=Hand(out=0, down=-3, fwd=3)),
                Pose(sit=True, crouch=8, near=Hand(out=-1, down=-4, fwd=5), far=Hand(out=-1, down=-4, fwd=5)),
                Pose(sit=True, crouch=8, near=Hand(out=-2, down=-4, fwd=6), far=Hand(out=-2, down=-4, fwd=6))][i]
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


def tail_behind(look: Look, facing: str) -> bool:
    """Hair rows below the chin (long hair, ponytails) normally lie over the
    shoulders; a look may send them behind the body in the front view."""
    return facing == "front" and look.extra.get("tail_front") == "behind"


def draw_head(p: Paint, look: Look, facing: str, x0: int, y0: int, pose: Pose, part: str = "head") -> None:
    rows = look.heads["front" if facing == "front" else facing]
    x0 += look.extra.get("head_ox", 0)      # wide hair templates start left of the face
    behind = tail_behind(look, facing)
    for r, line in enumerate(rows):
        below = r >= HEAD_H
        if (part == "tail") != (below and behind):
            continue
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
            p.put(x0 + c, y0 + r, m, lv, L_FAR_ARM if part == "tail" else L_HEAD)
    if part == "tail":
        return
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
    if tail_behind(look, facing):
        draw_head(p, look, facing, head_x, head_y, pose, part="tail")
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
        # front/back: foreshortened lap the width of the hips, then shins
        half = max(look.hip // 2, lw + 1)
        for y in range(crotch - 1, crotch + 3):
            for x in range(cx - half, cx + half):
                lv = 2
                if facing == "front" and y == crotch + 2:
                    lv = 3                                   # knee tops catch the light
                if x in (cx - 1, cx) and y > crotch - 1:
                    lv = 1                                   # gap between the knees
                p.put(x, y, "legs", lv, L_LEG)
        for k, sgn in enumerate((-1, 1)):
            x_in = cx + (0 if sgn > 0 else -1) + sgn * (gap // 2 + (1 if gap % 2 and sgn > 0 else 0))
            xs = [x_in + sgn * d for d in range(lw + 1)]
            for y in range(crotch + 3, GROUND + 1):
                boot = y > GROUND - look.boot_h
                for j, x in enumerate(xs[:lw]):
                    lv = 2 - (1 if sgn > 0 else 0) + (1 if j == 0 and sgn < 0 else 0)
                    p.put(x, y, "sole" if y == GROUND else ("boot" if boot else "legs"), lv, L_LEG)
        return

    if side:
        stride = pose.stride
        for leg, sgn, lift, layer, dim in ((0, 1, pose.lift_near, L_LEG, 0), (1, -1, pose.lift_far, L_FAR_LEG, -1)):
            foot_dx = -sgn * stride * 4.5
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
    toward = hand.swing if facing == "front" else -hand.swing
    hx -= sgn * max(toward, 0) * 0.35
    hx += sgn * max(-toward, 0) * 0.2
    hy = sy + 1 + reach + hand.down - hand.fwd * 0.25 - abs(hand.swing) * 0.45
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
        if facing == "left":
            ex, ey = mx + nx * h, my + ny * h
        else:
            ex, ey = mx + sgn * min(h * 0.3, 1.5), my + min(h * 0.25, 1.0)
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
        if look.cuff != "none" and (look.cuff or look.name != "musician"):
            for dx in range(min(look.arm_w, 3)):
                p.put(left + dx - (0 if look.arm_w < 4 else 0), hyr - 1, cuff_mat, 2 + dim, layer)
        hand_mat = "glove" if "glove" in look.mats else "skin"
        for dy in range(2):
            for dx in range(hand_w):
                p.put(left + dx, hyr + dy, hand_mat, max(1, 2 + dim - (1 if dx == 1 and dy == 1 else 0)), layer)


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
# The remaining twelve candidates (identity: characters-21-front-master.png)
# ----------------------------------------------------------------------------
# Same construction as the life-wing four. Rows past the twelfth in a head
# template are long hair below the chin; `head_ox` shifts templates wider
# than ten columns so the face stays on the shared head axis.

HEADS_POET = {   # long wavy blond hair to the shoulders, centre part
    "front": [
        "..kHLLHk..",
        ".kHLLHLHhk",
        "kHLHHHLHHk",
        "kHLHSSHLhk",
        "kHHSSSSHhk",
        "hHSSSSSShh",
        "HSSbSSbSSh",
        "HSSeSSeSSh",
        "hSSeSSeSsh",
        "HSSSSnSSsH",
        "hHSSmSSsHh",
        "kH.sSSs.Hk",
        "kH......Hk",
        "kh......hk",
        ".k......k.",
    ],
    "back": [
        "..kHLLHk..",
        ".kHLLHLHhk",
        "kHLHHHLHHk",
        "kHLHHHHLhk",
        "kHHLHHHHhk",
        "kHLHHHLHhk",
        "kHHHLHHHhk",
        "kHLHHHHLhk",
        "kHHHLHHHhk",
        "kHLHHHHLhk",
        "khHHHHHHhk",
        "khHHLHHHhk",
        "kHhHHHHhHk",
        ".khHHHHhk.",
        "..khhhhk..",
    ],
    "left": [
        "...kHLHk..",
        "..kHLLHHhk",
        ".kHLHHLHHk",
        "kHSHHHHLhk",
        "SSSSHHHHhk",
        "SSSSSHLHhk",
        "bSSSSHHLhk",
        "eSSSSsHHhk",
        "eSSSSsHLhk",
        "SSSSSsHHhk",
        "SmSSsHHLhk",
        ".sSs.kHHhk",
        "....kHHLhk",
        ".....kHhk.",
        "......kk..",
    ],
}

HEADS_ARCHAEOLOGIST = {   # grey, unruly, receding; grey moustache
    "front": [
        "..kHkHHk..",
        ".kHLHLHHhk",
        "kHLHHHHLHk",
        "kHHSSSSHhk",
        "hHSSSSSSHh",
        "hSSSSSSSSh",
        "hSbbSSbbSh",
        "SSSeSSeSSs",
        "sSSeSSeSSs",
        "sSSSSnSSSs",
        ".sGGGGGGs.",
        "..sSmSSs..",
    ],
    "back": [
        "..kHkHHk..",
        ".kHLHLHHhk",
        "kHLHHHHLHk",
        "kHHHLHHHhk",
        "kHLHHHHHhk",
        "kHHHHHLHhk",
        "hHHLHHHHhh",
        "hHHHHHHhhh",
        "shHHHHHhhs",
        "sShhhhhhSs",
        ".sSSSSSSs.",
        "..ssssss..",
    ],
    "left": [
        "..kHkHHk..",
        ".kHLHLHHhk",
        "kHLHHHHLHk",
        "kSSHHHHHhk",
        "SSSSSHHHhk",
        "SSSSSHHHhk",
        "bbSSSSHhhk",
        "eSSSSSsHhk",
        "eSSSSSsShk",
        "SSSSSSShk.",
        "GGSSSSSs..",
        "smSSSSs...",
    ],
}

HEADS_THIEF = {   # shaggy plum-black crop, fringe over one eye
    "front": [
        "...khHk...",
        "..kHLHHhk.",
        ".kHLHHHHhk",
        "kHLHHHLHhk",
        "kHHHHHHShk",
        "hHHHHSSShh",
        "hHHHSSbSSh",
        "hHhSSSeSSh",
        ".hHSSSeSs.",
        ".hSSSnSSs.",
        "..SSmSSs..",
        "...sSSs...",
    ],
    "back": [
        "...khHk...",
        "..kHLHHhk.",
        ".kHLHHHHhk",
        "kHLHHHLHhk",
        "kHHLHHHHhk",
        "kHHHHHHHhk",
        "kHHHHHHhhk",
        "khHHHHHhhk",
        ".khHHHhhk.",
        ".khhhhhhk.",
        "..sSSSSs..",
        "...sSSs...",
    ],
    "left": [
        "...kHHk...",
        "..kHLHHhk.",
        ".kHLHHHHhk",
        "HHLHHHHHhk",
        "hHHHHHHHhk",
        "SSHHSHHhhk",
        "bSSSSShhhk",
        "eSSSSShhhk",
        "eSSSSsShhk",
        "SSSSSSShk.",
        "SmSSSs....",
        ".sSSs.....",
    ],
}

HEADS_LAWYER = {   # navy-black hair combed back hard, heavy level brows
    "front": [
        "..kkHHkk..",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "kHHHHHHHhk",
        "hHSSSSSHhh",
        "hSSSSSSSSh",
        "SbbbSSbbbS",
        "SSSeSSeSSs",
        "SSSeSSeSSs",
        "sSSSSnSSSs",
        ".sSSmmSSs.",
        "..sSSSSs..",
    ],
    "back": [
        "..kkHHkk..",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "kHHLHHHHhk",
        "kHHHHHHHhk",
        "hHHHHHHHhh",
        "hHHHHHHhhh",
        "shHHHHHhhs",
        "SshhhhhhsS",
        "sSSSSSSSSs",
        ".sSSSSSSs.",
        "..ssssss..",
    ],
    "left": [
        "..kkHHkk..",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "SSHHHHHHhk",
        "SSSSHHHHhk",
        "SSSSSHHhhk",
        "bbSSSSShhk",
        "eSSSSSsShk",
        "eSSSSSsShk",
        "SSSSSSShk.",
        "SmSSSSSs..",
        "sSSSSss...",
    ],
}

HEADS_NATURALIST = {   # big loose copper curls past the jaw (12 wide)
    "front": [
        "..kH.kHk.k..",
        ".kHLHkHLHHk.",
        "kHLLHHHLHLHk",
        "kHLHHLHHHHhk",
        "HHHHSSSSHHhH",
        "kHHSSSSSSHhk",
        "hHSSbSSbSShk",
        "kHSSeSSeSShk",
        "hHSSeSSeSShH",
        "kHhSSnSSShhk",
        ".kHSSmSSsHk.",
        "..kHsSSsHk..",
        ".kHh....hHk.",
        "..k......k..",
    ],
    "back": [
        "..kH.kHk.k..",
        ".kHLHkHLHHk.",
        "kHLLHHHLHLHk",
        "kHLHHLHHHHhk",
        "HHHLHHHLHHhH",
        "kHHHHLHHHHhk",
        "hHLHHHHHLHhk",
        "kHHHLHHHHHhk",
        "hHHHHHLHHHhH",
        "kHhHHHHHHhhk",
        ".kHhHHHHhHk.",
        "..khHHHHhk..",
        ".kHhHHHHhHk.",
        "..khkhhkhk..",
    ],
    "left": [
        "..kH.kHk.k..",
        ".kHLHkHLHHk.",
        "kHLLHHHLHLHk",
        "kHLHHLHHHHhk",
        "HSSSHHHLHHhH",
        ".SSSSHHHHHhk",
        ".bSSSSHLHHhk",
        ".eSSSSsHHhhH",
        ".eSSSSsHHLhk",
        ".SSSSSSHHhk.",
        ".SmSSSHHhHk.",
        "..sSShkHhk..",
        "....kHhHHk..",
        ".....k.kk...",
    ],
}

HEADS_CHESSPLAYER = {   # straight silver bob with a blunt fringe
    "front": [
        "...kHHk...",
        "..kHLLHHk.",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "kHHHHHHHhk",
        "hHSSSSSShh",
        "hSSbSSbSSh",
        "hSSeSSeSSh",
        "hSSeSSeSSh",
        "hSSSSnSSsh",
        ".kSSmSSsk.",
        "...sSSs...",
    ],
    "back": [
        "...kHHk...",
        "..kHLLHHk.",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "kHHLHHHHhk",
        "kHHHHHHHhk",
        "kHHHHHHHhk",
        "kHHHHHHhhk",
        "khHHHHHhhk",
        ".khhhhhhk.",
        "..sSSSSs..",
        "...sSSs...",
    ],
    "left": [
        "...kHHHk..",
        "..kHLLHHhk",
        ".kHLHHHHhk",
        "kHLHHHHHhk",
        "HHHHHHHHhk",
        "SSSSHHHHhk",
        "bSSSSHHHhk",
        "eSSSSsHHhk",
        "eSSSSsHhhk",
        "SSSSSSkhk.",
        "SmSSSs....",
        ".sSSs.....",
    ],
}

HEADS_DANCER = {   # crimson hair in a high ponytail tied with a dark ribbon
    "front": [
        "...kHHk.kHk..",
        "..kHLLHhHLHk.",
        ".kHLHHHHhkHk.",
        "kHLHHHHHHkHk.",
        "kHHHHSHHhkHHk",
        "hHSSSSSSShkHk",
        "hSSbSSbSShkHk",
        "hSSeSSeSShkHk",
        ".SSeSSeSs.kHk",
        ".SSSSnSSs.kHk",
        "..SSmSSs..kHk",
        "...sSSs...kHk",
        "..........kHk",
        "..........kHk",
        ".........kHHk",
        ".........kHk.",
        ".........kHk.",
        ".........kHk.",
        "........kHk..",
        "........kk...",
    ],
    "back": [
        "...kHHk...",
        "..kHLHHk..",
        ".kHLHHHHk.",
        "kHLHHHHHhk",
        "kHHHGGHHhk",
        "kHHkHLkHhk",
        "kHHkHHkHhk",
        "khHkLHkhhk",
        ".khkHHkhk.",
        "..skHHks..",
        "..skHLks..",
        "...kHHk...",
        "...kHHk...",
        "...kHLk...",
        "...kHHk...",
        "....kHk...",
        "....kHk...",
        "....kk....",
    ],
    "left": [
        "...kHHk....",
        "..kHLHHk...",
        ".kHLHHHHhk.",
        "kHLHHHHHhGk",
        "kSSHHHHHhkHk",
        "SSSSHHHhhkHk",
        "bSSSSShhk.kHk",
        "eSSSSShhk.kHk",
        "eSSSSsShk.kHk",
        "SSSSSSShk..kHk",
        "SmSSSs.....kHk",
        ".sSSs......kHk",
        "...........kHk",
        "..........kHHk",
        "..........kHk",
        "..........kHk",
        "..........kHk",
        ".........kHk",
        ".........kk",
    ],
}

HEADS_PAINTER = {   # blue-black hair with a long fringe that hides the brows
    "front": [
        "...kHHk...",
        "..kHLHHHk.",
        ".kHLLHHHhk",
        "kHLHHLHHhk",
        "kHHLHHHLhk",
        "hHHHHLHHhh",
        "hHLHHHHLHh",
        "hShHSHhHSh",
        "hSSeSSeSSh",
        ".SSSSSSSs.",
        "..SSSmSs..",
        "..hsSSsh..",
    ],
    "back": [
        "...kHHk...",
        "..kHLHHHk.",
        ".kHLLHHHhk",
        "kHLHHLHHhk",
        "kHHLHHHHhk",
        "kHHHHHLHhk",
        "kHLHHHHHhk",
        "kHHHHHHHhk",
        "khHHLHHhhk",
        "khHHHHHhhk",
        ".khhhhhhk.",
        "..ksSSsk..",
    ],
    "left": [
        "...kHHk...",
        "..kHLHHHk.",
        ".kHLLHHHhk",
        "HHLHHLHHhk",
        "HHHHHHHLhk",
        "hHHHHLHHhk",
        "hhHSSHHHhk",
        "eSSSSsHHhk",
        "SSSSSsHhhk",
        "SSSSSShHhk",
        "SmSSSshhk.",
        ".sSSs.kk..",
    ],
}

HEADS_CHEMIST = {   # wild copper hair pinned up in a loose heap (12 wide)
    "front": [
        "...kHkHHk...",
        "..kHLHLHLHk.",
        ".kHLHHHLHHHk",
        "kHLHHHHHHLHk",
        "kHHHSSSSHHhk",
        ".kHSSSSSSHk.",
        ".hSSbSSbSSh.",
        ".hSSeSSeSSh.",
        ".HSSeSSeSSH.",
        ".kSSSnSSSsk.",
        "..kSSmSSsk..",
        "...ksSSsk...",
    ],
    "back": [
        "...kHkHHk...",
        "..kHLHLHLHk.",
        ".kHLHHHLHHHk",
        "kHLHHLHHHLHk",
        "kHHLHHHLHHhk",
        ".kHHHLHHHHk.",
        ".kHHHHHHLhk.",
        ".khHHLHHHhk.",
        "..khHHHHhk..",
        "..kshhhhsk..",
        "...sSSSSs...",
        "....sSSs....",
    ],
    "left": [
        "...kHkHHk...",
        "..kHLHLHLHk.",
        ".kHLHHHLHHHk",
        "kHSHHHHHHLHk",
        ".SSSSHHHLHhk",
        ".SSSSSHHHHk.",
        ".bSSSSHHLhk.",
        ".eSSSSsHHhk.",
        ".eSSSSsSHk..",
        ".SSSSSSShk..",
        ".SmSSSSs....",
        ".sSSSSs.....",
    ],
}

HEADS_MARKSMAN = {   # dark cropped hair, square jaw, shadow of stubble
    "front": [
        "..........",
        "..kHkHHk..",
        ".kHLHLHHk.",
        "kHLHHHHHhk",
        "hHSSSSSSHh",
        "hSSSSSSSSh",
        "SbbbSSbbbS",
        "SSSeSSeSSs",
        "SSSeSSeSSs",
        "sSSSSnSSSs",
        "ssSSmmSSss",
        ".gssssssg.",
    ],
    "back": [
        "..........",
        "..kHkHHk..",
        ".kHLHLHHk.",
        "kHLHHHHHhk",
        "kHHLHHHHhk",
        "hHHHHHHHhh",
        "hHHHHHHHhh",
        "shhHHHHhhs",
        "SshhhhhhsS",
        "sSSSSSSSSs",
        ".sSSSSSSs.",
        "..ssssss..",
    ],
    "left": [
        "..........",
        "..kHkHHk..",
        ".kHLHLHHk.",
        "kLHHHHHHhk",
        "SSSHHHHHhk",
        "SSSSHHHhhk",
        "bbSSSSShhk",
        "eSSSSSsShk",
        "eSSSSSsShk",
        "SSSSSSShk.",
        "SmSSSSSs..",
        "gsSSSss...",
    ],
}

HEADS_ARCHITECT = {   # straight fair bob cut at the jaw, side fringe
    "front": [
        "...kHHk...",
        "..kHLLHHk.",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "kHLHHSSHhk",
        "hHHSSSSShk",
        "hHSbSSbSSh",
        "hHSeSSeSSh",
        "hHSeSSeSHh",
        "hHSSSnSSHh",
        "kHhSmSSsHk",
        ".kh.sSs.hk",
    ],
    "back": [
        "...kHHk...",
        "..kHLLHHk.",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "kHLHHLHHhk",
        "kHHHHHHHhk",
        "kHHLHHHHhk",
        "kHHHHHLHhk",
        "kHHHHHHHhk",
        "kHHLHHHHhk",
        "khhhhhhhhk",
        "..ksSSsk..",
    ],
    "left": [
        "...kHHk...",
        "..kHLLHHk.",
        ".kHLLHHHhk",
        "kHLHHHHHhk",
        "HHHHHHHHhk",
        "SSSHHHLHhk",
        "bSSSSHHHhk",
        "eSSSSHHLhk",
        "eSSSSsHHhk",
        "SSSSSkHHhk",
        "SmSSSkhhhk",
        ".sSs..kk..",
    ],
}

HEADS_DOCTOR = {   # ash-brown waves with grey threads, low knot at the nape
    "front": [
        "...kHHk...",
        "..kHLHHHk.",
        ".kHLHGHHhk",
        "kHLHHHHHhk",
        "kHHSSSSHhk",
        "hHSSSSSSHh",
        "hSSbSSbSSh",
        "hSSeSSeSSh",
        "hSSeSSeSSh",
        "HSSSSnSSsH",
        "hhSSmSSshh",
        ".k.sSSs.k.",
    ],
    "back": [
        "...kHHk...",
        "..kHLHHHk.",
        ".kHLHGHHhk",
        "kHLHHHHHhk",
        "kHHGHHHHhk",
        "kHHHHHLHhk",
        "kHLHHHHHhk",
        "khHHHHHhhk",
        ".khHHHhhk.",
        "..kHLHHk..",
        "..kHHHhk..",
        "...kkkk...",
    ],
    "left": [
        "...kHHk...",
        "..kHLHHHk.",
        ".kHLHGHHhk",
        "kHLHHHHHhk",
        "kSSHHHHHhk",
        "SSSSHHHhhk",
        "bSSSSHhhk.",
        "eSSSSShkHk",
        "eSSSSsSkHHk",
        "SSSSSs.kHhk",
        "SmSSs...kk.",
        ".sSs......",
    ],
}


def _mats(**kw: str) -> dict[str, str]:
    kw.setdefault("hair2", kw["hair"])
    return kw


LOOKS.update({
    # 卢西安 Lucien, poet, 21: crimson frock coat, cream shirt, gold jabot pin
    "poet": Look(
        "poet", height=42, shoulder=12, waist=9, hip=10, leg_w=3, leg_gap=1, arm_w=3,
        boot_h=3, skirt=9, skirt_kind="coat",
        mats=_mats(hair="#c8955e", skin="#e2b896", eye="#2a1a18", coat="#7c2428",
                   inner="#d8c8a4", shirt="#e6dcc4", tie="#c9a04e",
                   legs="#5a3a2c", boot="#3e2a20", sole="#20140f"),
        heads=HEADS_POET, extra={"flare": 2}),
    # 顾平章 Gu Pingzhang, archaeologist, 59: tan scholar's long coat over waistcoat
    "archaeologist": Look(
        "archaeologist", height=41, shoulder=12, waist=10, hip=10, leg_w=3, leg_gap=1, arm_w=3,
        boot_h=3, skirt=9, skirt_kind="coat",
        mats=_mats(hair="#7f898c", hair2="#b4b8b4", skin="#cf9c74", eye="#1d1514",
                   coat="#94693f", inner="#5b4b36", shirt="#ddd3bc", tie="#3e4a3a",
                   legs="#5a4838", boot="#4a3022", sole="#22160f"),
        heads=HEADS_ARCHAEOLOGIST),
    # 雀儿 Que'er, thief, 19: small; plum short coat, dark gloves
    "thief": Look(
        "thief", height=38, shoulder=10, waist=7, hip=8, leg_w=3, leg_gap=0, arm_w=2,
        boot_h=3, skirt=6, skirt_kind="coat",
        mats=_mats(hair="#3e2230", skin="#d8ac8c", eye="#1a1216", coat="#5c2a3a",
                   inner="#36262e", shirt="#7a5a58", tie="#7a3040",
                   legs="#2e2a30", boot="#3a2a24", sole="#1a1412", glove="#2c2228", apron="#2a1e22"),
        heads=HEADS_THIEF),
    # 沃德 Ward, lawyer, 38: severe navy frock suit, grey cravat, dark gloves
    "lawyer": Look(
        "lawyer", height=43, shoulder=13, waist=10, hip=10, leg_w=3, leg_gap=1, arm_w=3,
        boot_h=3, skirt=6, skirt_kind="coat",
        mats=_mats(hair="#1f2742", skin="#e6c2a2", eye="#16141c", coat="#2b3352",
                   inner="#232a42", shirt="#e8e4dc", tie="#8a8f9c",
                   legs="#262b3c", boot="#1c1a20", sole="#121014", glove="#22202a"),
        heads=HEADS_LAWYER),
    # 赛尔玛 Selma, naturalist, 24: olive field jacket, belt, breeches, tall boots
    "naturalist": Look(
        "naturalist", height=40, shoulder=11, waist=8, hip=9, leg_w=3, leg_gap=1, arm_w=3,
        boot_h=5, skirt=4, skirt_kind="jacket",
        mats=_mats(hair="#c4481e", skin="#d9a47e", eye="#1d1514", coat="#626040",
                   inner="#c4a47e", shirt="#d6c09a", tie="#8a4a2a",
                   legs="#a2683e", boot="#5a3424", sole="#24150f", apron="#4a3020"),
        heads=HEADS_NATURALIST, extra={"head_ox": -1}),
    # 阮清越 Ruan Qingyue, chess player, 19: cream knitted jersey, brown trousers
    "chessplayer": Look(
        "chessplayer", height=38, shoulder=10, waist=8, hip=9, leg_w=3, leg_gap=1, arm_w=3,
        boot_h=2, skirt=2, skirt_kind="jacket",
        mats=_mats(hair="#c9c2be", skin="#e4c2a8", eye="#1e1a20", coat="#d4c4a0",
                   inner="#d4c4a0", shirt="#f0eadc", tie="#b89a7a",
                   legs="#5a3a32", boot="#4a2e26", sole="#20140f"),
        heads=HEADS_CHESSPLAYER),
    # 伊莲娜 Ilena, dancer, 26: wine day dress to the ankle, high ponytail
    "dancer": Look(
        "dancer", height=41, shoulder=10, waist=7, hip=9, leg_w=3, leg_gap=0, arm_w=2,
        boot_h=3, skirt=14, skirt_kind="dress", cuff="none",
        mats=_mats(hair="#6a1e2a", hair2="#2a1418", skin="#e0b498", eye="#1e1418",
                   coat="#5e1e2a", inner="#5e1e2a", shirt="#d8c4b0", tie="#8a3a40",
                   legs="#5e1e2a", boot="#2e1a1e", sole="#160c0e"),
        heads=HEADS_DANCER, extra={"tail_front": "behind"}),
    # 莫兰 Moran, painter, 23: long linen smock over dark clothes, loose blue bow
    "painter": Look(
        "painter", height=41, shoulder=11, waist=9, hip=10, leg_w=3, leg_gap=1, arm_w=3,
        boot_h=3, skirt=9, skirt_kind="coat",
        mats=_mats(hair="#2c3846", skin="#dcb498", eye="#161820", coat="#cbbfa6",
                   inner="#3a3539", shirt="#4a4448", tie="#4a5c78",
                   legs="#363238", boot="#2e292c", sole="#141214"),
        heads=HEADS_PAINTER),
    # 维罗妮卡 Veronica, chemist, 28: white laboratory coat over a black dress
    "chemist": Look(
        "chemist", height=40, shoulder=11, waist=8, hip=9, leg_w=3, leg_gap=0, arm_w=3,
        boot_h=3, skirt=9, skirt_kind="coat",
        mats=_mats(hair="#d0561c", skin="#e6c0a2", eye="#1e1418", coat="#e2dacd",
                   inner="#2e2a30", shirt="#efe9de", tie="#3a3036",
                   legs="#2e2a30", boot="#262228", sole="#121014"),
        heads=HEADS_CHEMIST, extra={"head_ox": -1, "under": 13}),
    # 贝尔特朗 Bertrand, marksman, 35: belted olive jacket, field boots
    "marksman": Look(
        "marksman", height=43, shoulder=14, waist=12, hip=11, leg_w=4, leg_gap=1, arm_w=3,
        boot_h=4, skirt=4, skirt_kind="jacket",
        mats=_mats(hair="#3a332d", hair2="#6a5a4c", skin="#d4a07a", eye="#1a1412",
                   coat="#5a5038", inner="#4a4232", shirt="#b8a888", tie="#9a8a5a",
                   legs="#4e4232", boot="#4a3022", sole="#20140f", apron="#3a2a1e",
                   glove="#5a3e2a"),
        heads=HEADS_MARKSMAN),
    # 玛格丽特 Margaret, architect, 32: navy coat piped in amber, waistcoat, trousers
    "architect": Look(
        "architect", height=40, shoulder=11, waist=8, hip=9, leg_w=3, leg_gap=1, arm_w=3,
        boot_h=3, skirt=7, skirt_kind="coat",
        mats=_mats(hair="#d4a266", skin="#efc8a6", eye="#1e1a22", coat="#2b3756",
                   inner="#5c4a3c", shirt="#e4dccb", tie="#2a3046",
                   legs="#252c42", boot="#2a2228", sole="#141016", trim="#c0823e"),
        heads=HEADS_ARCHITECT),
    # 茉拉 Mora, doctor, 46: white blouse, brown waistcoat, long dark skirt
    "doctor": Look(
        "doctor", height=40, shoulder=11, waist=8, hip=10, leg_w=3, leg_gap=0, arm_w=3,
        boot_h=3, skirt=13, skirt_kind="skirt",
        mats=_mats(hair="#5e4c40", hair2="#8f8478", skin="#d4a688", eye="#1a1412",
                   coat="#e0d6c4", inner="#4a3a30", shirt="#e8e0d0", tie="#6a3a34",
                   legs="#3a3234", boot="#2e2422", sole="#141010"),
        heads=HEADS_DOCTOR),
})


def _with_skirt(look: Look, rows: int) -> Look:
    return replace(look, skirt=rows)


def _belt(p, look, facing, sy, row, cx, hw, buckle=True):
    side = facing == "left"
    half = hw[row] * (0.74 if side else 1.0)
    x0, x1 = int(round(cx - half)) - (1 if side else 0), int(round(cx + half))
    for x in range(x0, x1):
        p.put(x, sy + row, "apron", 1 if x > cx + 1 else 2, L_TORSO)
    if buckle and facing == "front":
        p.put(cx - 1, sy + row, "tie", 3, L_TORSO)
    elif buckle and side:
        p.put(x0 + 1, sy + row, "tie", 3, L_TORSO)


def _open_coat(p, look, facing, pose, sy, th, cx, hw, *, tie=True, pockets=True,
               trim=None, belt_row=None):
    """Open-fronted coat in the detective's construction: shirt, cravat and
    waistcoat show in the opening; lapels catch the light; skirts part."""
    flare = look.extra.get("flare", 1)
    under = look.extra.get("under", 0)
    if under:   # a long dress or skirt beneath the coat
        fill_skirt(p, _with_skirt(look, under), facing, pose, sy + th, cx, "legs",
                   open_front=False, flare=1)
    if facing == "front":
        for r in range(1, th):
            w = 1 if r < 2 else 2
            for x in range(cx - w, cx + w):
                mat, lv = ("inner", 2 if x < cx else 1)
                if r <= 3 and x in (cx - 1, cx):
                    mat, lv = ("shirt", 3 if x < cx else 2)
                if tie and 2 <= r <= 3 and x in (cx - 1, cx):
                    mat, lv = ("tie", 2)
                p.put(x, sy + r, mat, lv, L_TORSO)
        for r in range(1, 5):
            p.put(cx - 2 - (1 if r < 3 else 0), sy + r, "coat", 4, L_TORSO)
            p.put(cx + 1 + (1 if r < 3 else 0), sy + r, "coat", 1, L_TORSO)
        p.put(cx - 1, sy, "shirt", 3, L_TORSO)
        p.put(cx, sy, "shirt", 2, L_TORSO)
        if belt_row is not None:
            _belt(p, look, facing, sy, belt_row, cx, hw)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=True, flare=flare)
        if trim:
            for r in range(1, th):
                lapel = r in (3, 4)
                p.put(cx - 2 if lapel else cx - 3, sy + r, trim, 3, L_TORSO)
                p.put(cx + 1 if lapel else cx + 2, sy + r, trim, 2, L_TORSO)
            for r in range(1, skirt_rows(look, pose)):
                p.put(cx - 2, sy + th + r, trim, 3, L_SKIRT)
                p.put(cx + 1, sy + th + r, trim, 2, L_SKIRT)
        if pockets:
            p.put(cx - 4, sy + th - 2, "coat", 1, L_TORSO)
            p.put(cx + 3, sy + th - 2, "coat", 1, L_TORSO)
    elif facing == "back":
        for r in range(2, th):
            p.put(cx - 1, sy + r, "coat", 1, L_TORSO)
        if belt_row is not None:
            _belt(p, look, facing, sy, belt_row, cx, hw, buckle=False)
        else:
            p.put(cx - 2, sy + th - 3, "coat", 1, L_TORSO)
            p.put(cx + 1, sy + th - 3, "coat", 1, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False, flare=flare)
        for r in range(2, skirt_rows(look, pose)):
            p.put(cx - 1, sy + th + r, "coat", 0, L_SKIRT)
    else:
        for r in range(1, 4):
            p.put(cx - 3 - (1 if r > 1 else 0), sy + r, "shirt", 3 if r == 1 else 2, L_TORSO)
        if tie:
            p.put(cx - 3, sy + 2, "tie", 2, L_TORSO)
        p.put(cx - 2, sy + 4, trim or "coat", 4 if not trim else 3, L_TORSO)
        if belt_row is not None:
            _belt(p, look, facing, sy, belt_row, cx, hw)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False, flare=flare)
        if trim:
            rows = skirt_rows(look, pose)
            for r in range(rows):
                x_front = int(round(cx - look.hip / 2 * 0.78)) - 1 + int(round(-pose.stride * r / rows))
                p.put(x_front, sy + th + r, trim, 3, L_SKIRT)


def _closed_coat(p, look, facing, pose, sy, th, cx, hw, *, double=False, belt_row=None,
                 skirt_open=True, chest_pockets=False):
    """Buttoned coat or jacket: a narrow V of shirt and tie at the throat."""
    flare = look.extra.get("flare", 1)
    if facing == "front":
        p.put(cx - 1, sy, "shirt", 3, L_TORSO)
        p.put(cx, sy, "shirt", 2, L_TORSO)
        for r in (1, 2, 3):
            p.put(cx - 1, sy + r, "shirt" if r == 1 else "tie", 3 if r == 1 else 2, L_TORSO)
            p.put(cx, sy + r, "shirt" if r == 1 else "tie", 2, L_TORSO)
            if r >= 2:
                p.put(cx - 2, sy + r, "coat", 4, L_TORSO)
                p.put(cx + 1, sy + r, "coat", 1, L_TORSO)
        for r in range(4, th):
            p.put(cx + (1 if double else 0), sy + r, "coat", 1, L_TORSO)
        for r in range(5, th - 1, 3):
            if double:
                p.put(cx - 3, sy + r, "coat", 0, L_TORSO)
                p.put(cx + 2, sy + r, "coat", 0, L_TORSO)
            else:
                p.put(cx - 1, sy + r, "tie", 3, L_TORSO)
        if chest_pockets:
            for x in (cx - 5, cx - 4, cx + 3, cx + 4):
                p.put(x, sy + 3, "coat", 1, L_TORSO)
        if belt_row is not None:
            _belt(p, look, facing, sy, belt_row, cx, hw)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=skirt_open, flare=flare)
        if not skirt_open:
            for r in range(skirt_rows(look, pose)):
                p.put(cx, sy + th + r, "coat", 1, L_SKIRT)
    elif facing == "back":
        for r in range(2, th):
            p.put(cx - 1, sy + r, "coat", 1, L_TORSO)
        if belt_row is not None:
            _belt(p, look, facing, sy, belt_row, cx, hw, buckle=False)
        else:
            p.put(cx - 2, sy + th - 3, "coat", 0, L_TORSO)
            p.put(cx + 1, sy + th - 3, "coat", 0, L_TORSO)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False, flare=flare)
        for r in range(1, skirt_rows(look, pose)):
            p.put(cx - 1, sy + th + r, "coat", 0, L_SKIRT)
    else:
        p.put(cx - 4, sy + 1, "shirt", 3, L_TORSO)
        p.put(cx - 3, sy, "shirt", 3, L_TORSO)
        p.put(cx - 4, sy + 2, "tie", 2, L_TORSO)
        for r in range(5, th - 1, 3):
            p.put(cx - 4, sy + r, "coat", 0 if double else 1, L_TORSO)
        if belt_row is not None:
            _belt(p, look, facing, sy, belt_row, cx, hw)
        fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False, flare=flare)


def g_poet(p, look, facing, pose, sy, th, cx, hw):
    _open_coat(p, look, facing, pose, sy, th, cx, hw, pockets=False)
    if facing == "front":   # gold pin in the cream jabot
        p.put(cx - 1, sy + 4, "tie", 3, L_TORSO)


def g_archaeologist(p, look, facing, pose, sy, th, cx, hw):
    _open_coat(p, look, facing, pose, sy, th, cx, hw)
    if facing == "front":   # watch chain across the waistcoat
        p.put(cx - 2, sy + th - 4, "shirt", 1, L_TORSO)
        p.put(cx + 1, sy + th - 4, "shirt", 1, L_TORSO)


def g_thief(p, look, facing, pose, sy, th, cx, hw):
    _open_coat(p, look, facing, pose, sy, th, cx, hw, pockets=False, belt_row=th - 2)


def g_lawyer(p, look, facing, pose, sy, th, cx, hw):
    _closed_coat(p, look, facing, pose, sy, th, cx, hw, double=True)


def g_naturalist(p, look, facing, pose, sy, th, cx, hw):
    _open_coat(p, look, facing, pose, sy, th, cx, hw, belt_row=th - 2)
    if facing == "front":   # bellows chest pockets
        for x in (cx - 4, cx + 3):
            p.put(x, sy + 4, "coat", 1, L_TORSO)
            p.put(x, sy + 5, "coat", 3, L_TORSO)


def g_chessplayer(p, look, facing, pose, sy, th, cx, hw):
    """Knitted jersey: rolled collar, two cable ribs, ribbed hem over the hips."""
    rows = skirt_rows(look, pose)
    fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False, flare=0)
    if facing == "front":
        for x in range(cx - 2, cx + 2):
            p.put(x, sy - 1, "coat", 3 if x < cx else 2, L_TORSO)
            p.put(x, sy, "coat", 1, L_TORSO)
        p.put(cx - 3, sy, "shirt", 3, L_TORSO)
        p.put(cx + 2, sy, "shirt", 2, L_TORSO)
        for r in range(2, th):
            p.put(cx - 3, sy + r, "coat", 3 if r % 2 else 1, L_TORSO)
            p.put(cx + 2, sy + r, "coat", 1 if r % 2 else 3, L_TORSO)
    elif facing == "back":
        for x in range(cx - 2, cx + 2):
            p.put(x, sy - 1, "coat", 2, L_TORSO)
    else:
        p.put(cx - 2, sy - 1, "coat", 3, L_TORSO)
        p.put(cx - 1, sy - 1, "coat", 2, L_TORSO)
        p.put(cx - 4, sy, "shirt", 3, L_TORSO)
    side = facing == "left"
    for r in range(rows):   # ribbing
        for x in range(cx - 6, cx + 6):
            if p.has(x, sy + th + r) and p.mat[sy + th + r, x] == p.names.index("coat") and (x + (1 if side else 0)) % 2 == 0:
                p.shade(x, sy + th + r, -1)


def g_dancer(p, look, facing, pose, sy, th, cx, hw):
    """Fitted bodice with a high collar, a sash at the waist, full skirt."""
    fill_skirt(p, look, facing, pose, sy + th, cx, "coat", open_front=False, flare=3)
    rows = skirt_rows(look, pose)
    if facing == "front":
        for x in range(cx - 2, cx + 2):
            p.put(x, sy - 1, "coat", 3 if x < cx else 2, L_TORSO)
        p.put(cx - 1, sy, "shirt", 3, L_TORSO)
        p.put(cx, sy, "shirt", 2, L_TORSO)
        for r in (2, 4, 6):
            p.put(cx - 1, sy + r, "coat", 0, L_TORSO)
        for r in range(1, rows - 1):   # pleats
            p.put(cx - 3, sy + th + r, "coat", 1, L_SKIRT)
            p.put(cx + 2, sy + th + r, "coat", 3, L_SKIRT)
    elif facing == "back":
        for x in range(cx - 2, cx + 2):
            p.put(x, sy - 1, "coat", 2, L_TORSO)
        for r in range(1, rows - 1):
            p.put(cx - 1, sy + th + r, "coat", 1, L_SKIRT)
    else:
        p.put(cx - 2, sy - 1, "coat", 3, L_TORSO)
        p.put(cx - 1, sy - 1, "coat", 2, L_TORSO)
        p.put(cx - 4, sy, "shirt", 3, L_TORSO)
    _belt_mat = "tie"
    side = facing == "left"
    half = hw[th - 2] * (0.74 if side else 1.0)
    for x in range(int(round(cx - half)) - (1 if side else 0), int(round(cx + half))):
        p.put(x, sy + th - 2, _belt_mat, 3 if x < cx else 2, L_TORSO)
    if facing == "back":   # sash bow
        p.put(cx - 2, sy + th - 1, _belt_mat, 2, L_TORSO)
        p.put(cx + 1, sy + th - 1, _belt_mat, 2, L_TORSO)
        p.put(cx - 1, sy + th, _belt_mat, 1, L_SKIRT)


def g_painter(p, look, facing, pose, sy, th, cx, hw):
    _open_coat(p, look, facing, pose, sy, th, cx, hw)
    if facing == "front":   # loose lavalliere bow spreading over the opening
        p.put(cx - 2, sy + 2, "tie", 3, L_TORSO)
        p.put(cx + 1, sy + 2, "tie", 2, L_TORSO)


def g_chemist(p, look, facing, pose, sy, th, cx, hw):
    _open_coat(p, look, facing, pose, sy, th, cx, hw, tie=False)


def g_marksman(p, look, facing, pose, sy, th, cx, hw):
    _closed_coat(p, look, facing, pose, sy, th, cx, hw, belt_row=th - 2, skirt_open=False,
                 chest_pockets=True)


def g_architect(p, look, facing, pose, sy, th, cx, hw):
    _open_coat(p, look, facing, pose, sy, th, cx, hw, trim="trim", pockets=False)
    if facing == "front":
        for r in (5, 7):
            p.put(cx - 1, sy + r, "tie", 1, L_TORSO)


def g_doctor(p, look, facing, pose, sy, th, cx, hw):
    """Waistcoat over a white blouse, long dark skirt; brooch at the collar."""
    fill_skirt(p, look, facing, pose, sy + th, cx, "legs", open_front=False, flare=2)
    rows = skirt_rows(look, pose)
    side = facing == "left"
    for r in range(1, th):
        half = hw[r] * (0.74 if side else 1.0) - (1 if r < 3 else 0)
        x0, x1 = int(round(cx - half)), int(round(cx + half))
        if side:
            x0 -= 1 if r > 1 else 0
        for x in range(x0, x1):
            u = (x - x0) / max(x1 - x0 - 1, 1)
            if facing == "front" and r <= 3 and cx - 1 <= x <= cx:
                continue
            p.put(x, sy + r, "inner", 3 if u < 0.15 else (1 if u > 0.8 else 2), L_TORSO)
    if facing == "front":
        for x in range(cx - 1, cx + 1):
            p.put(x, sy - 1, "shirt", 3 if x < cx else 2, L_TORSO)
        p.put(cx - 1, sy + 1, "tie", 2, L_TORSO)
        for r in (5, 7, 9):
            p.put(cx - 1, sy + r, "tie", 1, L_TORSO)
        for r in range(1, rows - 1):
            p.put(cx - 3, sy + th + r, "legs", 1, L_SKIRT)
    elif facing == "back":
        for x in range(cx - 2, cx + 2):
            p.put(x, sy + th - 3, "inner", 1, L_TORSO)
    else:
        p.put(cx - 3, sy, "shirt", 3, L_TORSO)
        p.put(cx - 3, sy + 1, "tie", 2, L_TORSO)


GARMENTS.update({
    "poet": g_poet, "archaeologist": g_archaeologist, "thief": g_thief, "lawyer": g_lawyer,
    "naturalist": g_naturalist, "chessplayer": g_chessplayer, "dancer": g_dancer,
    "painter": g_painter, "chemist": g_chemist, "marksman": g_marksman,
    "architect": g_architect, "doctor": g_doctor,
})


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


def sprite_frames_tres(name: str) -> str:
    """SpriteFrames resource over the atlas: 24 animations, 111 AtlasTextures."""
    tex = f"res://ShareAsset/Internal/Characters/animation_v2/{name}.png"
    out = ['[gd_resource type="SpriteFrames" load_steps=113 format=3]', "",
           f'[ext_resource type="Texture2D" path="{tex}" id="1"]', ""]
    anims = []
    for d, facing in enumerate(FACINGS):
        for a, (action, count) in enumerate(ACTIONS):
            ids = []
            for i in range(count):
                fid = f"f_{d}_{a}_{i}"
                ids.append(fid)
                out += [f'[sub_resource type="AtlasTexture" id="{fid}"]', 'atlas = ExtResource("1")',
                        f"region = Rect2({i * CELL}, {(d * 8 + a) * CELL}, {CELL}, {CELL})", ""]
            frames = ", ".join(f'{{"duration": 1.0, "texture": SubResource("{f}")}}' for f in ids)
            loop = "true" if action in ("idle", "walk") else "false"
            speed = "10.0" if action == "walk" else "7.0"
            anims.append(f'{{"frames": [{frames}], "loop": {loop}, "name": &"{action}_{facing}", "speed": {speed}}}')
    out += ["[resource]", "animations = [" + ",\n".join(anims) + "]", ""]
    return "\n".join(out)


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
        (runtime / f"{name}.tres").write_text(sprite_frames_tres(name), encoding="utf-8", newline="\n")
        print(f"{name}: 111 frames published")


def contact_sheet(path: Path, scale: int = 3, names=None) -> None:
    """Idle in three facings plus the walk cycle, for review."""
    tiles = []
    for name in names or LOOKS:
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
