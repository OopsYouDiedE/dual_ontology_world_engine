"""Outdoor building models for the three-quarter view: walls, roofs, domes, towers, porches.

Styles follow the brief (1890-1910 island college, Azorean vernacular): service and living
buildings are lime-washed with black basalt quoins and plinths under red tile; the research
academies are dressed basalt under slate; the public core is pale trachyte ashlar under slate,
with a copper dome on the central hall. Closed buildings keep their shutters shut and carry an
outside bolt with a posted notice (rule 3).
"""
from __future__ import annotations

import math

import numpy as np

from first_awakening_art.pixel import hexc
from .oblique import Face, bayer, hash2, quad

K = 24


def R(*hexes):
    return [hexc(h) for h in hexes]


WHITE = R("#5f5b55", "#7a756d", "#958f85", "#b0aa9e", "#c9c4b8", "#dcd8cd", "#ebe8df", "#f6f4ee")
BASALT = R("#141518", "#1d1e22", "#27292e", "#32353b", "#3f434a", "#4e535b", "#62686f", "#7a8087")
TRACHYTE = R("#4f483f", "#665d51", "#7d7363", "#938875", "#a89d88", "#bbb19b", "#cbc2ad", "#d9d1be")
TILE = R("#341710", "#4d2219", "#662e21", "#7e3b2a", "#964a35", "#ac5c42", "#bf7253", "#cd8a6a")
SLATE = R("#121519", "#1a1e23", "#23282f", "#2d343b", "#394149", "#465059", "#56616b", "#6a7680")
COPPER = R("#163029", "#1f4034", "#295142", "#346250", "#43755f", "#568a70", "#6fa084", "#8db89c")
GLASS = R("#1f3036", "#2a414a", "#38555e", "#4a6b73", "#5f838a", "#7a9ea2", "#9cbcbc", "#c4dad6")
PANE = R("#10161e", "#18212c", "#22303e", "#2f4153", "#40576c", "#587288", "#7a92a6", "#a9bccb")
WOOD = R("#1e120c", "#2c1a11", "#3c2418", "#4e3020", "#613d29", "#764c33", "#8b5d3f")
GREEN = R("#12211b", "#1a3027", "#244034", "#2f5142", "#3c6351", "#4c7662")
IRON = R("#121418", "#1c1f25", "#282c34", "#363b44", "#474d57", "#5d646f")
PAPER = R("#9d978a", "#bdb7a8", "#d8d2c2", "#ebe6d8")
LEAD = R("#2a2d31", "#353a3f", "#41474d", "#4e555c", "#5c646b", "#6b747b")

WALLS = {"white": WHITE, "basalt": BASALT, "trachyte": TRACHYTE}
ROOFS = {"tile": TILE, "slate": SLATE, "copper": COPPER, "glass": GLASS, "lead": LEAD, "white": WHITE}


def arr(ramp):
    return np.array([c[:3] for c in ramp], np.int16)


def pick(ramp, idx):
    a = arr(ramp)
    return a[np.clip(np.round(idx), 0, len(a) - 1).astype(int)]


def lit(face, gain=3.0, mid=0.6):
    return gain * (face.light - mid)


# ------------------------------------------------------------------ walls ----

def _ashlar(s, t, course=0.42, block=0.95, seed=1):
    row = np.floor(t / course).astype(int)
    off = (row % 2) * block * 0.5
    col = np.floor((s + off) / block).astype(int)
    fs, ft = (s + off) / block - col, t / course - row
    joint = (ft < 1.2 / (course * K)) | (fs < 1.2 / (block * K))
    return row, col, joint, hash2(row, col, seed)


def _rubble(s, t, seed=3):
    row = np.floor(t / 0.28).astype(int)
    jitter = hash2(row, 7, seed) * 0.4
    col = np.floor((s + jitter) / (0.36 + 0.14 * hash2(row, 3, seed))).astype(int)
    ft = t / 0.28 - row
    fs = (s + jitter) / (0.36 + 0.14 * hash2(row, 3, seed)) - col
    joint = (ft < 0.16) | (fs < 0.1)
    return joint, hash2(row, col, seed)


def wall_surface(style, s, t, face, H=None, seed=1):
    """Base wall colour (rgb int16) for a wall in the given style."""
    L = lit(face)
    x, y = np.floor(s * K), np.floor(t * K)
    if style == "white":
        idx = 5.0 + L + 0.9 * bayer(x, y) - 0.6 * (t < 0.9) * (hash2(np.floor(s * 3), 1, seed) > 0.6) * (0.9 - t)
        col = pick(WHITE, idx)
    elif style == "trachyte":
        row, c_, joint, h = _ashlar(s, t, 0.46, 1.1, seed)
        idx = 4.4 + L + (h - 0.5) * 1.2 - joint * 1.6 + 0.5 * bayer(x, y)
        col = pick(TRACHYTE, idx)
    else:                                          # dressed basalt
        row, c_, joint, h = _ashlar(s, t, 0.4, 0.8, seed)
        idx = 4.3 + L + (h - 0.5) * 1.4 + joint * 1.4 + 0.5 * bayer(x, y)
        col = pick(BASALT, idx)
    if H is not None:
        col = np.where(((t > H - 0.32) & (t <= H))[:, None], (col * 0.62).astype(np.int16), col)
    return col


def quoins_and_plinth(style, s, t, L_, face, col, plinth=0.5, seed=1):
    Lt = lit(face)
    if style == "white":
        row = np.floor(t / 0.36).astype(int)
        q = ((s < 0.32) | (s > L_ - 0.32)) | (((s < 0.62) | (s > L_ - 0.62)) & (row % 2 == 0))
        joint, h = _rubble(s, t, seed)
        qcol = pick(BASALT, 3.6 + Lt + (h - 0.5) - (np.floor(t * K) % 9 == 0) * 1.2)
        col = np.where(q[:, None], qcol, col)
        pm = t < plinth
        pcol = pick(BASALT, 3.4 + Lt + (h - 0.5) * 1.4 - joint * 1.5)
        col = np.where(pm[:, None], pcol, col)
        top = (t >= plinth) & (t < plinth + 0.08)
        col = np.where(top[:, None], pick(BASALT, 6 + Lt + 0 * s), col)
    elif style == "basalt":
        q = ((s < 0.4) | (s > L_ - 0.4))
        col = np.where(q[:, None], pick(TRACHYTE, 4.6 + Lt + 0 * s - (np.floor(t / 0.4) % 2) * 0.6), col)
        pm = t < plinth
        col = np.where(pm[:, None], pick(BASALT, 2.6 + Lt + 0 * s + 0.6 * bayer(np.floor(s * K), np.floor(t * K))), col)
    else:
        pm = t < plinth
        col = np.where(pm[:, None], pick(TRACHYTE, 3.0 + Lt + 0 * s + 0.6 * bayer(np.floor(s * K), np.floor(t * K))), col)
        cap = (t >= plinth) & (t < plinth + 0.1)
        col = np.where(cap[:, None], pick(TRACHYTE, 6 + Lt + 0 * s), col)
    return col


def trim_ramp(style):
    return BASALT if style == "white" else TRACHYTE


def apply_features(style, feats, s, t, face, col, keep):
    """Windows, doors, arches and notices on a wall face."""
    Lt = lit(face)
    tr = trim_ramp(style)
    for f in feats:
        k = f["kind"]
        s0, s1, t0, t1 = f["s0"], f["s1"], f["t0"], f["t1"]
        mid, half = (s0 + s1) / 2, (s1 - s0) / 2
        arch = f.get("arch", False)
        def inside(pad=0.0):
            m = (s >= s0 - pad) & (s <= s1 + pad) & (t >= t0 - pad) & (t <= t1 + pad)
            if arch:
                r = half + pad
                tc = t1 - half
                m &= (t <= tc) | ((s - mid) ** 2 + (t - tc) ** 2 <= r * r)
            return m
        if k == "hole":                                  # gate arch: see through to the ground behind
            m = inside()
            sur = inside(0.22) & ~m
            col = np.where(sur[:, None], pick(tr, 5.4 + Lt + 0 * s - (np.floor(np.arctan2(t - (t1 - half), s - mid) * 6) % 2) * 0.8), col)
            keep = keep & ~m
            continue
        if k == "notice":
            m = inside()
            col = np.where(m[:, None], pick(PAPER, 2.6 + 0 * s - ((np.floor(t * K) % 3 == 0) & (np.abs(s - mid) < half * 0.7)) * 1.6), col)
            continue
        sur = inside(0.16) & ~inside()
        col = np.where(sur[:, None], pick(tr, 5.0 + Lt + 0 * s - 0.6 * (t < t0)), col)
        m = inside()
        if k == "window":
            if f.get("closed"):
                plank = np.floor((s - s0) / 0.16).astype(int)
                c = pick(GREEN, 3.0 + Lt + 0 * s - (np.floor((s - s0) * K) % 4 == 0) * 1.2 - (np.abs(s - mid) < 0.03) * 2)
                c = np.where(((np.abs(t - (t0 + (t1 - t0) * 0.3)) < 0.05) | (np.abs(t - (t0 + (t1 - t0) * 0.75)) < 0.05))[:, None], pick(GREEN, 1 + 0 * s), c)
            else:
                rel = (t - t0) / max(t1 - t0, 1e-6)
                streak = (((s - s0) + (t - t0) * 0.8) % 0.9) < 0.14
                c = pick(PANE, 1.6 + 2.6 * rel + streak * 2.4 + 0.5 * bayer(np.floor(s * K), np.floor(t * K)))
                bar = (np.abs(s - mid) < 0.035) | (np.abs(t - (t0 + (t1 - t0) * 0.5)) < 0.03) | \
                      (np.abs(t - (t0 + (t1 - t0) * 0.25)) < 0.025) | (np.abs(t - (t0 + (t1 - t0) * 0.75)) < 0.025)
                c = np.where(bar[:, None], pick(WHITE, 6 + 0 * s), c)
            col = np.where(m[:, None], c, col)
            sill = (t >= t0 - 0.24) & (t < t0 - 0.12) & (s >= s0 - 0.26) & (s <= s1 + 0.26)
            col = np.where(sill[:, None], pick(tr, 6.2 + Lt + 0 * s), col)
        elif k == "door":
            leaf = (s < mid)
            rel = np.where(leaf, (s - s0) / half, (s - mid) / half)
            transom = t > t1 - 0.5
            panel = ((rel > 0.16) & (rel < 0.84)) & (((t > t0 + 0.25) & (t < t0 + 1.0)) | ((t > t0 + 1.2) & (t < t1 - 0.65)))
            c = pick(WOOD, 3.6 + Lt + 0 * s + panel * 0.9 - (np.abs(s - mid) < 0.04) * 2.4 - (np.abs(rel - 0.16) < 0.03) * 1.2)
            if f.get("closed"):
                bar = (np.abs(t - (t0 + 1.25)) < 0.09)
                c = np.where(bar[:, None], pick(IRON, 3 + (np.abs(t - (t0 + 1.25)) < 0.03) * 2), c)
                lock = (np.abs(s - mid) < 0.12) & (np.abs(t - (t0 + 1.12)) < 0.14)
                c = np.where(lock[:, None], pick(IRON, 4 + 0 * s), c)
                paper = (s > mid + 0.18) & (s < mid + 0.62) & (t > t0 + 1.45) & (t < t0 + 2.0)
                c = np.where(paper[:, None], pick(PAPER, 2.6 + 0 * s - ((np.floor(t * K) % 3 == 0) & (s > mid + 0.24) & (s < mid + 0.56)) * 1.6), c)
            else:
                c = np.where(transom[:, None], pick(PANE, 2.4 + ((s * 3 + t * 2) % 1 < 0.2) * 2), c)
                knob = (np.abs(s - (mid - 0.14)) < 0.04) & (np.abs(t - (t0 + 1.05)) < 0.04) | \
                       (np.abs(s - (mid + 0.14)) < 0.04) & (np.abs(t - (t0 + 1.05)) < 0.04)
                c = np.where(knob[:, None], np.array([200, 160, 80], np.int16), c)
            col = np.where(m[:, None], c, col)
        elif k == "dark":                               # open doorway or porch shadow
            col = np.where(m[:, None], pick(BASALT, 1 + 0.6 * bayer(np.floor(s * K), np.floor(t * K))), col)
        elif k == "vent":
            c = pick(IRON, 2 + (np.floor(t * K) % 3 == 0) * 2 + 0 * s)
            col = np.where(m[:, None], c, col)
    return col, keep


def wall_mat(style, L_, H, feats=(), plinth=0.5, seed=1, quoins=True):
    def mat(face, P, s, t):
        col = wall_surface(style, s, t, face, H, seed)
        if quoins:
            col = quoins_and_plinth(style, s, t, L_, face, col, plinth, seed)
        keep = np.ones(len(s), bool)
        col, keep = apply_features(style, feats, s, t, face, col, keep)
        return np.clip(col, 0, 255).astype(np.uint8), keep
    return mat


def gable_mat(style, seed=1):
    def mat(face, P, s, t):
        col = wall_surface(style, s, t, face, None, seed)
        keep = np.ones(len(s), bool)
        if "vent" in face.data:                       # small round vent in the gable
            cs, ct, r = face.data["vent"]
            m = (s - cs) ** 2 + (t - ct) ** 2 < r * r
            col = np.where(m[:, None], pick(PANE, 2 + 0 * s), col)
            ring = ((s - cs) ** 2 + (t - ct) ** 2 < (r + 0.12) ** 2) & ~m
            col = np.where(ring[:, None], pick(trim_ramp(style), 5 + 0 * s), col)
        return np.clip(col, 0, 255).astype(np.uint8), keep
    return mat


# ------------------------------------------------------------------ roofs ----

def _edges_st(face):
    o = face.verts[0]
    st = np.stack([(face.verts - o) @ face.a, (face.verts - o) @ face.b], 1)
    return st


def _dist_to_edges(face, s, t, which):
    st = _edges_st(face)
    d = np.full(len(s), 1e9)
    n = len(st)
    for i in range(n):
        p, q = st[i], st[(i + 1) % n]
        if not which(p, q):
            continue
        e = q - p
        L2 = max(e @ e, 1e-9)
        u = np.clip(((s - p[0]) * e[0] + (t - p[1]) * e[1]) / L2, 0, 1)
        d = np.minimum(d, np.hypot(s - (p[0] + u * e[0]), t - (p[1] + u * e[1])))
    return d


def roof_mat(kind, seed=1):
    ramp = ROOFS[kind]
    def mat(face, P, s, t):
        Lt = lit(face, 3.4, 0.62)
        x, y = np.floor(s * K), np.floor(t * K)
        if kind == "tile":
            row = np.floor(t / 0.30).astype(int)
            ft = t / 0.30 - row
            bar = ((s / 0.27) + (row % 2) * 0.5) % 1
            idx = 4.0 + Lt + (bar < 0.42) * 0.9 - (bar > 0.86) * 1.4 - (ft > 0.82) * 1.5 + (hash2(row, np.floor(s / 0.27), seed) - 0.5) * 0.9
        elif kind == "slate":
            row = np.floor(t / 0.24).astype(int)
            ft = t / 0.24 - row
            cs = np.floor((s + (row % 2) * 0.22) / 0.44)
            fs = (s + (row % 2) * 0.22) / 0.44 - cs
            idx = 4.0 + Lt - (ft > 0.8) * 1.4 - (fs < 0.08) * 0.9 + (hash2(row, cs, seed) - 0.5) * 1.3
        elif kind == "lead":
            idx = 3.0 + Lt + 0.7 * bayer(x, y) - ((s % 1.2) < 0.06) * 1.2
        elif kind == "glass":
            idx = 4.2 + Lt + (((s + t) % 1.1) < 0.18) * 1.6
            frame = ((s % 0.6) < 0.06) | ((t % 1.0) < 0.06)
            idx = np.where(frame, 7, idx)
            plant = (hash2(np.floor(s / 0.7), np.floor(t / 0.7), seed + 9) > 0.45) & ~frame
            rgb = pick(ramp, idx)
            rgb = np.where(plant[:, None], (rgb * np.array([0.62, 0.86, 0.6])).astype(np.int16), rgb)
            d = _dist_to_edges(face, s, t, lambda p, q: True)
            rgb = np.where((d < 0.1)[:, None], pick(WHITE, 6 + 0 * s), rgb)
            return np.clip(rgb, 0, 255).astype(np.uint8), np.ones(len(s), bool)
        else:
            idx = 4.0 + Lt + 0.6 * bayer(x, y)
        idx = idx + 0.5 * bayer(x, y)
        rgb = pick(ramp, idx)
        ridge = _dist_to_edges(face, s, t, lambda p, q: not (abs(p[1]) < 1e-6 and abs(q[1]) < 1e-6))
        eave = t < 0.12
        rgb = np.where((ridge < 0.16)[:, None], pick(ramp, 6.2 + 0.6 * Lt + 0 * s - (ridge < 0.05) * 2.5), rgb)
        rgb = np.where(eave[:, None], pick(ramp, 1 + 0 * s), rgb)
        return np.clip(rgb, 0, 255).astype(np.uint8), np.ones(len(s), bool)
    return mat


# ------------------------------------------------------------------ geometry ----

class Building:
    """A model: visible faces plus convex parts for shadows, the footprint and the sort line."""

    def __init__(self, name):
        self.name = name
        self.faces: list[Face] = []
        self.parts: list[np.ndarray] = []
        self.solids: list[tuple[float, float, float, float]] = []   # (u0, v0, u1, v1) metres
        self.anchor_v: float | None = None

    def add_part(self, verts):
        self.parts.append(np.asarray(verts, float))

    def solid(self, u0, v0, u1, v1):
        self.solids.append((u0, v0, u1, v1))
        self.anchor_v = v0 if self.anchor_v is None else min(self.anchor_v, v0)


def box(b: Building, u0, u1, v0, v1, z0, H, style, feats=(), plinth=0.5, seed=1, solid=True, quoins=True):
    L_ = u1 - u0
    b.faces.append(quad((u0, v0, z0), (u1, v0, z0), (u1, v0, z0 + H), (u0, v0, z0 + H),
                        wall_mat(style, L_, H, feats, plinth, seed, quoins)))
    b.add_part([(u, v, z) for u in (u0, u1) for v in (v0, v1) for z in (z0, z0 + H)])
    if solid:
        b.solid(u0, v0, u1, v1)


def roof(b: Building, u0, u1, v0, v1, zh, kind, form, ridge="auto", pitch=32, over=0.4, seed=1, gable_style=None, vent=False):
    u0, u1, v0, v1 = u0 - over, u1 + over, v0 - over, v1 + over
    zh = zh - over * math.tan(math.radians(pitch)) * 0.5
    W_, D_ = u1 - u0, v1 - v0
    tp = math.tan(math.radians(pitch))
    if ridge == "auto":
        ridge = "u" if W_ >= D_ else "v"
    m = roof_mat(kind, seed)
    pts = []
    if form == "flat":
        b.faces.append(quad((u0, v0, zh), (u1, v0, zh), (u1, v1, zh), (u0, v1, zh), m))
        pts = [(u, v, zh) for u in (u0, u1) for v in (v0, v1)]
    elif form == "gable" and ridge == "u":
        vm, zr = (v0 + v1) / 2, zh + D_ / 2 * tp
        b.faces.append(quad((u0, v0, zh), (u1, v0, zh), (u1, vm, zr), (u0, vm, zr), m))
        b.faces.append(quad((u0, v1, zh), (u1, v1, zh), (u1, vm, zr), (u0, vm, zr), m))
        pts = [(u0, v0, zh), (u1, v0, zh), (u0, v1, zh), (u1, v1, zh), (u0, vm, zr), (u1, vm, zr)]
    elif form == "gable":
        um, zr = (u0 + u1) / 2, zh + W_ / 2 * tp
        b.faces.append(quad((u0, v0, zh), (u0, v1, zh), (um, v1, zr), (um, v0, zr), m))
        b.faces.append(quad((u1, v0, zh), (u1, v1, zh), (um, v1, zr), (um, v0, zr), m))
        g = Face(np.array([(u0 + over, v0 + over, zh), (u1 - over, v0 + over, zh), (um, v0 + over, zr - over * tp)]),
                 gable_mat(gable_style or "white", seed))
        if vent:
            g.data["vent"] = ((u1 - u0) / 2 - over, (zr - zh) * 0.42, 0.42)
        b.faces.append(g)
        pts = [(u0, v0, zh), (u1, v0, zh), (u0, v1, zh), (u1, v1, zh), (um, v0, zr), (um, v1, zr)]
    elif form == "hip":
        if W_ >= D_:
            hm = D_ / 2; zr = zh + hm * tp; vm = (v0 + v1) / 2
            a_, b_ = (u0 + hm, vm, zr), (u1 - hm, vm, zr)
            b.faces.append(quad((u0, v0, zh), (u1, v0, zh), b_, a_, m))
            b.faces.append(quad((u0, v1, zh), (u1, v1, zh), b_, a_, m))
            b.faces.append(Face(np.array([(u0, v0, zh), (u0, v1, zh), a_]), m))
            b.faces.append(Face(np.array([(u1, v0, zh), (u1, v1, zh), b_]), m))
        else:
            hm = W_ / 2; zr = zh + hm * tp; um = (u0 + u1) / 2
            a_, b_ = (um, v0 + hm, zr), (um, v1 - hm, zr)
            b.faces.append(Face(np.array([(u0, v0, zh), (u1, v0, zh), a_]), m))
            b.faces.append(Face(np.array([(u0, v1, zh), (u1, v1, zh), b_]), m))
            b.faces.append(quad((u0, v0, zh), (u0, v1, zh), b_, a_, m))
            b.faces.append(quad((u1, v0, zh), (u1, v1, zh), b_, a_, m))
        pts = [(u0, v0, zh), (u1, v0, zh), (u0, v1, zh), (u1, v1, zh), a_, b_]
    b.add_part(pts)


def chimney(b: Building, u, v, z0, h, style="white"):
    w = 0.9
    box(b, u - w / 2, u + w / 2, v - w / 2, v + w / 2, z0 - 1.5, h + 1.5, style, plinth=0, quoins=False, solid=False)
    b.faces.append(quad((u - w / 2 - 0.1, v - w / 2 - 0.1, z0 + h), (u + w / 2 + 0.1, v - w / 2 - 0.1, z0 + h),
                        (u + w / 2 + 0.1, v + w / 2 + 0.1, z0 + h), (u - w / 2 - 0.1, v + w / 2 + 0.1, z0 + h), _flue))


def _flue(face, P, s, t):
    inner = (s > 0.25) & (s < face.data.get("w", 0.85)) & (t > 0.25) & (t < 0.85)
    col = np.where(inner[:, None], pick(BASALT, 0 + 0 * s), pick(TRACHYTE, 5 + 0 * s))
    return np.clip(col, 0, 255).astype(np.uint8), np.ones(len(s), bool)


# ------------------------------------------------------------------ curved ----

def _cyl_mat(style, c, r, z0, H, feats, plinth):
    cu, cv = c
    def mat(face, P, s, t):
        du, dv = P[:, 0] - cu, P[:, 1] - cv
        phi = np.arctan2(du, -dv)
        sa, ta = r * phi + r * math.pi, P[:, 2] - z0
        n = np.stack([du / r, dv / r, np.zeros_like(du)], 1)
        from .oblique import SUN
        light = n @ SUN
        class F: pass
        f = F(); f.light = 0.6
        col = wall_surface(style, sa, ta, f, H)
        col = (col * (0.72 + 0.5 * np.clip(light, -0.3, 1))[:, None]).astype(np.int16)
        if plinth:
            pm = ta < plinth
            col = np.where(pm[:, None], (pick(trim_ramp(style) if style != "trachyte" else TRACHYTE, 3.2 + 0 * sa) * (0.75 + 0.4 * np.clip(light, 0, 1))[:, None]).astype(np.int16), col)
        keep = np.ones(len(s), bool)
        f.light = 0.55
        fe = [dict(x, s0=x["s0"] + r * math.pi, s1=x["s1"] + r * math.pi) for x in feats]
        col, keep = apply_features(style, fe, sa, ta, f, col, keep)
        return np.clip(col, 0, 255).astype(np.uint8), keep
    return mat


def cylinder(b: Building, c, r, z0, H, style, feats=(), plinth=0.6, n=56, solid=True):
    cu, cv = c
    m = _cyl_mat(style, c, r, z0, H, feats, plinth)
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        p0 = (cu + r * math.sin(a0), cv - r * math.cos(a0)); p1 = (cu + r * math.sin(a1), cv - r * math.cos(a1))
        if (p0[1] + p1[1]) / 2 > cv + 0.2 * r:              # back half hidden
            continue
        b.faces.append(quad((p0[0], p0[1], z0), (p1[0], p1[1], z0), (p1[0], p1[1], z0 + H), (p0[0], p0[1], z0 + H), m))
    ring = [(cu + r * math.sin(a), cv - r * math.cos(a)) for a in np.linspace(0, 2 * math.pi, 16, endpoint=False)]
    b.add_part([(x, y, z) for x, y in ring for z in (z0, z0 + H)])
    if solid:
        for k in range(6):                                  # stack of strips approximating the circle
            y0 = cv - r + 2 * r * k / 6; y1 = cv - r + 2 * r * (k + 1) / 6
            yy = (y0 + y1) / 2
            hw = math.sqrt(max(0.0, r * r - (yy - cv) ** 2))
            b.solid(cu - hw, y0, cu + hw, y1)


def _dome_mat(c, rx, z0, rz, kind, seed=1):
    cu, cv = c
    ramp = ROOFS[kind]
    def mat(face, P, s, t):
        from .oblique import SUN
        du, dv, dz = P[:, 0] - cu, P[:, 1] - cv, np.maximum(P[:, 2] - z0, 0)
        n = np.stack([du / rx ** 2, dv / rx ** 2, dz / rz ** 2], 1)
        n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-9
        light = n @ SUN
        phi = np.arctan2(du, -dv)
        seam = ((phi / (2 * math.pi) * 24) % 1) < 0.07
        band = ((dz / rz * 6) % 1) < 0.06
        patina = hash2(np.floor((phi + 4) * rx / 0.8), np.floor(dz / 0.8), seed) - 0.5
        idx = 3.6 + 4.2 * (light - 0.55) + patina * 1.2 - seam * 1.6 - band * 0.8 + 0.5 * bayer(np.floor(P[:, 0] * K), np.floor((P[:, 1] + P[:, 2]) * K))
        if kind == "white":
            idx = idx + 1.2
        rgb = pick(ramp, idx)
        return np.clip(rgb, 0, 255).astype(np.uint8), np.ones(len(s), bool)
    return mat


def dome(b: Building, c, rx, z0, rz, kind="copper", n=48, rings=12, seed=1):
    cu, cv = c
    m = _dome_mat(c, rx, z0, rz, kind, seed)
    def pt(a, e):
        return (cu + rx * math.cos(e) * math.sin(a), cv - rx * math.cos(e) * math.cos(a), z0 + rz * math.sin(e))
    for j in range(rings):
        e0, e1 = (math.pi / 2) * j / rings, (math.pi / 2) * (j + 1) / rings
        for i in range(n):
            a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
            p00, p10, p11, p01 = pt(a0, e0), pt(a1, e0), pt(a1, e1), pt(a0, e1)
            if j == rings - 1:
                b.faces.append(Face(np.array([p00, p10, p11]), m))
            else:
                b.faces.append(Face(np.array([p00, p10, p11]), m))
                b.faces.append(Face(np.array([p00, p11, p01]), m))
    b.add_part([pt(a, e) for a in np.linspace(0, 2 * math.pi, 12, endpoint=False) for e in (0, 0.6, 1.1, math.pi / 2)])


def cone(b: Building, c, r, z0, h, kind="slate", n=40, seed=1):
    cu, cv = c
    ramp = ROOFS[kind]
    def mat(face, P, s, t):
        from .oblique import SUN
        du, dv = P[:, 0] - cu, P[:, 1] - cv
        rho = np.hypot(du, dv) + 1e-9
        nn = np.stack([du / rho * h, dv / rho * h, np.full_like(du, r)], 1)
        nn /= np.linalg.norm(nn, axis=1, keepdims=True)
        light = nn @ SUN
        phi = np.arctan2(du, -dv)
        course = ((P[:, 2] - z0) / 0.3) % 1
        seam = ((phi / (2 * math.pi) * 40 + np.floor((P[:, 2] - z0) / 0.3) * 0.5) % 1) < 0.1
        idx = 3.8 + 4.0 * (light - 0.55) - (course > 0.8) * 1.3 - seam * 0.8 + 0.5 * bayer(np.floor(P[:, 0] * K), np.floor((P[:, 1] + P[:, 2]) * K))
        return np.clip(pick(ramp, idx), 0, 255).astype(np.uint8), np.ones(len(s), bool)
    apex = (cu, cv, z0 + h)
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        p0 = (cu + r * math.sin(a0), cv - r * math.cos(a0), z0); p1 = (cu + r * math.sin(a1), cv - r * math.cos(a1), z0)
        b.faces.append(Face(np.array([p0, p1, apex]), mat))
    b.add_part([(cu + r * math.sin(a), cv - r * math.cos(a), z0) for a in np.linspace(0, 2 * math.pi, 12, endpoint=False)] + [apex])


def columns_face(b: Building, u0, u1, v, z0, H, ncol, style="trachyte", door=None):
    """A portico front: columns over a shaded interior, entablature on top, optional door behind."""
    L_ = u1 - u0
    cw = 0.62
    xs = [0.25 + i * (L_ - 0.5 - cw) / (ncol - 1) for i in range(ncol)]
    def mat(face, P, s, t):
        Lt = lit(face)
        col = pick(BASALT, 1.2 + 0 * s + 0.6 * bayer(np.floor(s * K), np.floor(t * K)) + (t / H) * 0.8)
        if door:
            d0, d1, dh = door
            m = (s >= d0) & (s <= d1) & (t < dh)
            mid = (d0 + d1) / 2
            c = pick(WOOD, 2.6 + 0 * s - (np.abs(s - mid) < 0.04) * 2 + (((s - d0) % 0.45) < 0.05) * -1)
            col = np.where(m[:, None], c, col)
        for x in xs:
            m = (s >= x) & (s <= x + cw) & (t < H - 0.7)
            rel = (s - x) / cw
            c = pick(TRACHYTE, 3.2 + 3.0 * np.sin(np.clip(rel, 0, 1) * math.pi) * (1 - rel * 0.5) + 0.4 * bayer(np.floor(s * K), np.floor(t * K)))
            base = (t < 0.35) | ((t > H - 1.0) & (t < H - 0.7))
            c = np.where(base[:, None], pick(TRACHYTE, 5.5 + Lt + 0 * s), c)
            col = np.where(m[:, None], c, col)
        ent = t >= H - 0.7
        col = np.where(ent[:, None], pick(TRACHYTE, 4.8 + Lt + 0 * s - ((t > H - 0.36) & (t < H - 0.3)) * 1.5 - (np.floor(s / 0.5) % 2) * (t < H - 0.45) * 0.6), col)
        step = t < 0.18
        col = np.where(step[:, None], pick(TRACHYTE, 6 + 0 * s), col)
        return np.clip(col, 0, 255).astype(np.uint8), np.ones(len(s), bool)
    b.faces.append(quad((u0, v, z0), (u1, v, z0), (u1, v, z0 + H), (u0, v, z0 + H), mat))


def posts(b: Building, u0, u1, v0, v1, H, style="trachyte", w=0.35):
    for u in (u0, u1 - w):
        for v in (v0, v1 - w):
            box(b, u, u + w, v, v + w, 0, H, style, plinth=0, quoins=False, solid=True)


def windows_along(L_, sill, height, width=1.1, spacing=3.2, margin=1.6, doors=(), arch=False, closed=False):
    feats = []
    n = max(1, int((L_ - 2 * margin) // spacing) + 1)
    span = (n - 1) * spacing
    start = (L_ - span) / 2
    for i in range(n):
        x = start + i * spacing
        if any(abs(x - d) < 1.9 for d in doors):
            continue
        feats.append(dict(kind="window", s0=x - width / 2, s1=x + width / 2, t0=sill, t1=sill + height, arch=arch, closed=closed))
    return feats


def door_feat(x, closed=False, width=1.8, height=2.7, arch=False):
    return dict(kind="door", s0=x - width / 2, s1=x + width / 2, t0=0.0, t1=height, closed=closed, arch=arch)
