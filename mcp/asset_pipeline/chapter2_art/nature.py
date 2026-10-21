"""Vegetation and small outdoor pieces for the three-quarter view, at 24 px per metre.

Island planting follows the Azores: pittosporum ('incenso') as the round shade tree,
cryptomeria in windbreak rows, hydrangea hedges along walls, faya bushes on the slopes.
Light from the south-west: the left and lower side of every crown is lit.
"""
from __future__ import annotations

import math

import numpy as np

from first_awakening_art.pixel import Canvas, hexc
from .oblique import bayer, hash2

K = 24


def R(*h):
    return np.array([hexc(x)[:3] for x in h], np.int16)


LEAF = R("#0f1f16", "#16301f", "#1f4029", "#2a5233", "#37653d", "#467a47", "#5a8f52", "#74a660")
CONIFER = R("#0c1a16", "#11241d", "#183126", "#203f30", "#2b4f3a", "#386045", "#477251")
BARK = R("#1f1712", "#2d2119", "#3d2d22", "#4e3a2c", "#614a39")
HYDRANGEA = [R("#2c3566", "#3c4a8a", "#5168ad", "#7790cf", "#a5b9e6"),
             R("#3d2a63", "#55398a", "#7350ae", "#9473cb", "#baa1e2"),
             R("#5a3048", "#7d4466", "#a25d86", "#c482a6", "#e0afc7")]
ROCK = R("#121315", "#1b1c20", "#25272c", "#30333a", "#3e424a", "#50555e", "#666c75")


def _canvas_from(rgb, alpha):
    h, w = alpha.shape
    c = Canvas(w, h)
    c.a[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    c.a[..., 3] = np.where(alpha, 255, 0).astype(np.uint8)
    return c


def _outline(rgb, alpha, dark=(14, 22, 16)):
    edge = np.zeros_like(alpha)
    edge[1:] |= alpha[:-1]; edge[:-1] |= alpha[1:]; edge[:, 1:] |= alpha[:, :-1]; edge[:, :-1] |= alpha[:, 1:]
    edge &= ~alpha
    rgb[edge] = dark
    return rgb, alpha | edge


def _blobs(w, h, spheres, ramp, seed, clump=3):
    """Shade a union of spheres (cx, cy, r, depth) in pixels; light from the lower left."""
    yy, xx = np.mgrid[0:h, 0:w] + 0.5
    best = np.full((h, w), -1e9)
    nx = np.zeros((h, w)); ny = np.zeros((h, w)); nz = np.zeros((h, w))
    for cx, cy, r, dep in spheres:
        d2 = (xx - cx) ** 2 + (yy - cy) ** 2
        m = d2 < r * r
        z = np.sqrt(np.maximum(r * r - d2, 0)) + dep
        m &= z > best
        best = np.where(m, z, best)
        nx = np.where(m, (xx - cx) / r, nx); ny = np.where(m, (yy - cy) / r, ny); nz = np.where(m, np.sqrt(np.maximum(0, 1 - ((xx - cx) ** 2 + (yy - cy) ** 2) / (r * r))), nz)
    alpha = best > -1e8
    light = -0.55 * nx + 0.25 * ny + 0.8 * nz                       # screen-left and up lit
    cl = hash2(np.floor(xx / clump), np.floor(yy / clump), seed)
    cs = clump * 2.4                                                 # leaf clusters (Worley cells): lit upper-left, dark seams
    gx, gy = np.floor(xx / cs), np.floor(yy / cs)
    d1 = np.full((h, w), 9.0); d2 = np.full((h, w), 9.0); bx = np.zeros((h, w)); by = np.zeros((h, w))
    for ox in (-1, 0, 1):
        for oy in (-1, 0, 1):
            px_ = (gx + ox + 0.2 + 0.6 * hash2(gx + ox, gy + oy, seed + 3)) * cs
            py_ = (gy + oy + 0.2 + 0.6 * hash2(gx + ox, gy + oy, seed + 4)) * cs
            d = np.hypot(xx - px_, yy - py_) / cs
            closer = d < d1
            d2 = np.where(closer, d1, np.minimum(d2, d))
            bx = np.where(closer, (xx - px_) / cs, bx); by = np.where(closer, (yy - py_) / cs, by)
            d1 = np.where(closer, d, d1)
    seam = (d2 - d1) < 0.09
    idx = 1.0 + 4.2 * np.clip(light, 0, 1) + (cl - 0.5) * 0.8 - (bx + by) * 1.5 - seam * 1.3 + 0.5 * bayer(xx, yy)
    idx = np.clip(np.round(idx), 0, len(ramp) - 1).astype(int)
    return ramp[idx], alpha


def round_tree(seed: int, crown_m: float = 5.2, height_m: float = 6.8) -> Canvas:
    g = np.random.default_rng(seed)
    r_px = crown_m * K / 2
    w = int(crown_m * K + 10)
    h = int(height_m * K + 6)
    cx, cy = w / 2, h - (height_m * K - r_px)              # crown centre on screen
    spheres = [(cx, cy, r_px * 0.78, 0)]
    for _ in range(14):
        a = g.uniform(0, 2 * math.pi); d = g.uniform(0.25, 0.62) * r_px
        spheres.append((cx + d * math.cos(a), cy + d * math.sin(a) * 0.8, r_px * g.uniform(0.32, 0.46), g.uniform(-4, 6)))
    rgb, alpha = _blobs(w, h, spheres, LEAF, seed)
    # trunk below the crown
    tw = max(4, int(0.36 * K))
    ty0 = int(cy + r_px * 0.45)
    yy, xx = np.mgrid[0:h, 0:w]
    trunk = (np.abs(xx + 0.5 - cx) < tw / 2) & (yy >= ty0) & (yy < h - 1)
    shade = np.clip(((xx - cx) / tw + 0.5), 0, 1)
    trgb = BARK[np.clip(np.round(3.2 - 2.4 * shade + (yy % 5 == 0) * -1), 0, 4).astype(int)]
    rgb = np.where((trunk & ~alpha)[..., None], trgb, rgb)
    alpha = alpha | trunk
    rgb, alpha = _outline(rgb, alpha)
    return _canvas_from(rgb, alpha)


def conifer(seed: int, width_m: float = 3.6, height_m: float = 11.0) -> Canvas:
    g = np.random.default_rng(seed)
    w = int(width_m * K + 8); h = int(height_m * K + 4)
    yy, xx = np.mgrid[0:h, 0:w] + 0.5
    cx = w / 2
    base = h - 0.9 * K                                       # skirt above the trunk
    top = 4
    rel = np.clip((yy - top) / (base - top), 0, 1)            # 0 at the tip, 1 at the skirt
    tiers = 9
    tier = np.floor(rel * tiers)
    ft = rel * tiers - tier
    half = (0.12 + 0.88 * rel ** 0.85) * width_m * K / 2 * (0.78 + 0.28 * ft)
    jag = (hash2(np.floor(yy / 3), np.floor(xx / 4), seed) - 0.5) * 6
    crown = (np.abs(xx - cx) < half + jag) & (yy >= top) & (yy < base + 2)
    x_rel = (xx - cx) / np.maximum(half, 1)
    light = 0.62 - 0.5 * x_rel - 0.35 * (1 - ft)
    cl = hash2(np.floor(xx / 3), np.floor(yy / 3), seed + 1)
    idx = np.clip(np.round(1.2 + 3.6 * light + (cl - 0.5) * 1.4 + 0.5 * bayer(xx, yy)), 0, len(CONIFER) - 1).astype(int)
    rgb = CONIFER[idx]
    tw = max(4, int(0.42 * K))
    trunk = (np.abs(xx - cx) < tw / 2) & (yy >= base - 4) & (yy < h - 1) & ~crown
    rgb = np.where(trunk[..., None], BARK[np.clip(np.round(3 - 2.2 * np.clip((xx - cx) / tw + 0.5, 0, 1)), 0, 4).astype(int)], rgb)
    alpha = crown | trunk
    rgb, alpha = _outline(rgb, alpha, (10, 18, 15))
    return _canvas_from(rgb, alpha)


def bush(seed: int, width_m: float = 2.6, height_m: float = 1.6, flowers: int | None = None) -> Canvas:
    """Faya bush, or a hydrangea when flowers is a colour index."""
    g = np.random.default_rng(seed)
    w = int(width_m * K + 6); h = int(height_m * K + width_m * K * 0.35 + 4)
    spheres = []
    for _ in range(9):
        spheres.append((w / 2 + g.uniform(-0.32, 0.32) * width_m * K, h - g.uniform(0.32, 0.62) * height_m * K,
                        g.uniform(0.24, 0.34) * width_m * K, g.uniform(-3, 3)))
    rgb, alpha = _blobs(w, h, spheres, LEAF, seed, clump=2)
    if flowers is not None:
        ramp = HYDRANGEA[flowers % 3]
        for _ in range(int(width_m * 7)):
            fx, fy = g.uniform(5, w - 5), g.uniform(h * 0.15, h - 6)
            if not alpha[int(fy), int(fx)]:
                continue
            r = g.uniform(2.6, 4.2)
            yy, xx = np.mgrid[0:h, 0:w] + 0.5
            m = ((xx - fx) ** 2 + (yy - fy) ** 2 < r * r) & alpha
            lt = np.clip(1.6 + 2.2 * (-(xx - fx) - (yy - fy)) / r * 0.5 + (hash2(xx, yy, seed) - 0.5) * 1.4, 0, 4)
            rgb = np.where(m[..., None], ramp[np.round(lt).astype(int)], rgb)
    rgb, alpha = _outline(rgb, alpha)
    return _canvas_from(rgb, alpha)


def boulder(seed: int, size_m: float = 1.2) -> Canvas:
    g = np.random.default_rng(seed)
    w = int(size_m * K + 6); h = int(size_m * K * 0.8 + 6)
    spheres = [(w / 2 + g.uniform(-0.2, 0.2) * size_m * K, h - g.uniform(0.3, 0.45) * size_m * K, g.uniform(0.28, 0.42) * size_m * K, 0) for _ in range(4)]
    rgb, alpha = _blobs(w, h, spheres, ROCK, seed, clump=4)
    rgb, alpha = _outline(rgb, alpha, (8, 9, 11))
    return _canvas_from(rgb, alpha)


def stamp_shadow(rgb: np.ndarray, x: float, y: float, rx: float, ry: float, k: float = 0.62) -> None:
    """Darken an ellipse of the ground image in place (cast shadow of a crown)."""
    h, w = rgb.shape[:2]
    x0, x1 = int(max(0, x - rx)), int(min(w, x + rx + 1))
    y0, y1 = int(max(0, y - ry)), int(min(h, y + ry + 1))
    if x1 <= x0 or y1 <= y0:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1] + 0.5
    m = ((xx - x) / rx) ** 2 + ((yy - y) / ry) ** 2 < 1
    sub = rgb[y0:y1, x0:x1]
    sub[m] = (sub[m] * np.array([k, k * 1.02, k * 1.1])).clip(0, 255).astype(sub.dtype)
