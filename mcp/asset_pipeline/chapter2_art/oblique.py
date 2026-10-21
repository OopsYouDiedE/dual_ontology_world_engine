"""Three-quarter (oblique) renderer for outdoor buildings.

Projection: ground plan straight from above, heights drawn straight up the screen at the same
scale, x = (u - U0) * K, y = (V1 - v - z) * K. East and west walls are edge-on, south walls face
the viewer, roofs show both slopes. Every face is a convex planar polygon in metres; its material
gets the face-local coordinates (s along the face, t up the face) and the 3-D point, and picks a
colour from a palette ramp. Light comes from the south-west and above, so south facades and south
roof slopes are lit and cast shadows fall to the north-east.
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field
from typing import Callable

import numpy as np

from first_awakening_art.pixel import Canvas

SUN = np.array([-0.40, -0.50, 0.77])
SUN = SUN / np.linalg.norm(SUN)
SHADOW = (-SUN[0] / SUN[2], -SUN[1] / SUN[2])      # ground offset per metre of height (to the NE)


@dataclass
class Face:
    verts: np.ndarray                                 # (n, 3) metres (u, v, z), convex, planar
    mat: Callable                                     # mat(face, P, s, t) -> (rgb (n,3) uint8, keep (n,) bool)
    a: np.ndarray = None                              # in-plane axes (unit); s along a, t along b
    b: np.ndarray = None
    normal: np.ndarray = None
    tag: str = ""
    data: dict = field(default_factory=dict)
    cast: bool = True

    def __post_init__(self):
        self.verts = np.asarray(self.verts, float)
        o = self.verts[0]
        if self.a is None:
            e = self.verts[1] - o
            self.a = e / np.linalg.norm(e)
        for i in range(2, len(self.verts)):
            n = np.cross(self.verts[1] - o, self.verts[i] - o)
            if np.linalg.norm(n) > 1e-9:
                break
        n = n / np.linalg.norm(n)
        if n[2] - n[1] < -1e-9 or (abs(n[2] - n[1]) <= 1e-9 and n[0] < 0):   # towards the viewer (south, up)
            n = -n
        if self.normal is None:
            self.normal = n
        if self.b is None:
            b = np.cross(self.normal, self.a)
            if b[2] < 0 or (abs(b[2]) < 1e-9 and b[1] < 0):
                b = -b
            self.b = b / np.linalg.norm(b)

    @property
    def light(self) -> float:
        return float(np.dot(self.normal, SUN))


def quad(p0, p1, p2, p3, mat, **kw) -> Face:
    return Face(np.array([p0, p1, p2, p3], float), mat, **kw)


class Frame:
    """Global screen frame shared by the ground and every sprite."""

    def __init__(self, U0: float, V1: float, K: int):
        self.U0, self.V1, self.K = U0, V1, K

    def screen(self, P: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
        P = np.asarray(P, float)
        return (P[..., 0] - self.U0) * self.K, (self.V1 - P[..., 1] - P[..., 2]) * self.K

    def ground(self, u, v):
        return (np.asarray(u, float) - self.U0) * self.K, (self.V1 - np.asarray(v, float)) * self.K


def render(frame: Frame, faces: list[Face], anchor_v: float) -> tuple[Canvas, int, int]:
    """Rasterise faces into a sprite whose bottom edge is the ground line v = anchor_v.

    Returns (canvas, left, bottom) in global pixels; the scene places the sprite at
    (left + w // 2, bottom) with offset (-w // 2, -h).
    """
    K = frame.K
    xs, ys = [], []
    for f in faces:
        x, y = frame.screen(f.verts)
        xs.append(x); ys.append(y)
    xs, ys = np.concatenate(xs), np.concatenate(ys)
    left, top = int(math.floor(xs.min())) - 1, int(math.floor(ys.min())) - 1
    right = int(math.ceil(xs.max())) + 1
    bottom = int(round((frame.V1 - anchor_v) * K))
    w, h = right - left, max(1, bottom - top)
    rgba = np.zeros((h, w, 4), np.uint8)
    zbuf = np.full((h, w), -1e9)
    eps = 0.6 / K
    for f in faces:
        fx, fy = frame.screen(f.verts)
        o = f.verts[0]
        M = K * np.array([[f.a[0], f.b[0]], [-(f.a[1] + f.a[2]), -(f.b[1] + f.b[2])]])
        det = np.linalg.det(M)
        if abs(det) < 1e-6:
            continue
        Minv = np.linalg.inv(M)
        ox, oy = frame.screen(o)
        bx0, bx1 = max(left, int(math.floor(fx.min()))), min(right, int(math.ceil(fx.max())) + 1)
        by0, by1 = max(top, int(math.floor(fy.min()))), min(bottom, int(math.ceil(fy.max())) + 1)
        if bx1 <= bx0 or by1 <= by0:
            continue
        gx, gy = np.meshgrid(np.arange(bx0, bx1) + 0.5, np.arange(by0, by1) + 0.5)
        dx, dy = gx - ox, gy - oy
        s = Minv[0, 0] * dx + Minv[0, 1] * dy
        t = Minv[1, 0] * dx + Minv[1, 1] * dy
        st = np.stack([(f.verts - o) @ f.a, (f.verts - o) @ f.b], 1)
        area = np.sum(st[:, 0] * np.roll(st[:, 1], -1) - np.roll(st[:, 0], -1) * st[:, 1])
        sign = 1.0 if area > 0 else -1.0
        inside = np.ones(s.shape, bool)
        for i in range(len(st)):
            p, q = st[i], st[(i + 1) % len(st)]
            e = q - p
            L = math.hypot(*e) or 1.0
            cross = (e[0] * (t - p[1]) - e[1] * (s - p[0])) * sign / L
            inside &= cross >= -eps
        P = o + s[..., None] * f.a + t[..., None] * f.b
        depth = P[..., 2] - P[..., 1]
        sub = zbuf[by0 - top:by1 - top, bx0 - left:bx1 - left]
        m = inside & (depth > sub)
        if not m.any():
            continue
        rgb, keep = f.mat(f, P[m], s[m], t[m])
        idx = np.nonzero(m)
        iy, ix = idx[0][keep], idx[1][keep]
        dst = rgba[by0 - top:by1 - top, bx0 - left:bx1 - left]
        dst[iy, ix, :3] = rgb[keep]
        dst[iy, ix, 3] = 255
        sub[iy, ix] = depth[m][keep]
    c = Canvas(w, h)
    c.a[:] = rgba
    return c, left, bottom


def hull(points) -> list[tuple[float, float]]:
    pts = sorted(set((round(x, 3), round(y, 3)) for x, y in points))
    if len(pts) < 3:
        return pts
    def half(seq):
        out = []
        for p in seq:
            while len(out) >= 2 and (out[-1][0] - out[-2][0]) * (p[1] - out[-2][1]) - (out[-1][1] - out[-2][1]) * (p[0] - out[-2][0]) <= 0:
                out.pop()
            out.append(p)
        return out
    lo, hi = half(pts), half(pts[::-1])
    return lo[:-1] + hi[:-1]


def part_shadow(frame: Frame, verts: np.ndarray) -> list[tuple[float, float]]:
    """Ground shadow of a convex volume: hull of its footprint and its vertices cast along the sun."""
    P = np.asarray(verts, float)
    u = np.concatenate([P[:, 0], P[:, 0] + P[:, 2] * SHADOW[0]])
    v = np.concatenate([P[:, 1], P[:, 1] + P[:, 2] * SHADOW[1]])
    x, y = frame.ground(u, v)
    return hull(zip(x.tolist(), y.tolist()))


# ------------------------------------------------------------------ shading helpers ----

def tone(ramp: list, base: np.ndarray | float, face: Face, gain: float = 3.2, mid: float = 0.62) -> np.ndarray:
    """Pick ramp colours: base index plus a lighting step from the face normal."""
    arr = np.array([c[:3] for c in ramp], np.uint8)
    idx = np.asarray(base, float) + gain * (face.light - mid)
    idx = np.clip(np.round(idx), 0, len(ramp) - 1).astype(int)
    return arr[idx]


def bayer(x: np.ndarray, y: np.ndarray) -> np.ndarray:
    B = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0 - 0.47
    return B[np.asarray(y, int) % 4, np.asarray(x, int) % 4]


def hash2(a: np.ndarray, b: np.ndarray, seed: int = 0) -> np.ndarray:
    """Deterministic per-cell noise in [0, 1)."""
    a = np.asarray(a, np.int64); b = np.asarray(b, np.int64)
    h = (a * 374761393 + b * 668265263 + seed * 2147483647) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65536.0
