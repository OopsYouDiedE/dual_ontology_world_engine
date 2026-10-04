"""Campus exterior: ground, coast, paths and top-down roofs painted from the site plan.

Source data: artifacts/remake-chapter2/campus-site (campus.py building list, site.json
siting and paths, the scaled Corvo terrain). Scale: 12 px per metre; the map uses the
campus axis as 'up' (bearing about 2 degrees, so nearly north). Roofs are drawn
straight from above with a south-east cast shadow, so doors on any side stay usable.
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import numpy as np

from first_awakening_art.palette import (BRASS, CAP, CREAM, FOG, GRASS, IRON, LEAF, LINEN, OCHRE, SAGE, SLATE, STONE,
                                         TILE, WALNUT, VOID)
from first_awakening_art.pixel import Canvas, mix, rng, shift, hexc
from . import painter

ROOT = Path(__file__).resolve().parents[3]
SITE_DIR = ROOT / "artifacts/remake-chapter2/campus-site"
K = 12                                     # px per metre
U0, U1, V0, V1 = -100.0, 176.0, -142.0, 70.0
W, H = int((U1 - U0) * K), int((V1 - V0) * K)

TILE_ROOF = [hexc(v) for v in ("#3e1c16", "#5a281e", "#743527", "#8c4431", "#a4573e", "#b9704f")]
SLATE_ROOF = [hexc(v) for v in ("#1c2026", "#2a3038", "#39414b", "#4a535e", "#5d6873", "#73808a")]
COPPER = [hexc(v) for v in ("#1f3b34", "#2c5249", "#3c6b5f", "#4f8574", "#6aa08c", "#8cbaa5")]
WHITE_ROOF = [hexc(v) for v in ("#6d6a64", "#8d8a83", "#aaa79f", "#c4c1b8", "#dbd8cf", "#ecebe4")]
SEA = [hexc(v) for v in ("#132a35", "#1b3a48", "#26505e", "#356775", "#4f8590", "#86b3b6")]


def px(u: float, v: float) -> tuple[float, float]:
    return ((u - U0) * K, (V1 - v) * K)


def _campus():
    sys.path.insert(0, str(SITE_DIR))
    import campus as S                     # noqa: E402  (site scripts, scaled terrain)
    site = json.loads((SITE_DIR / "site.json").read_text(encoding="utf-8"))
    return S, site


ROOF = {  # 名称: (屋面, 形式)；未列出的建筑按材质默认
    "中央厅": ("copper", "dome"), "观星塔（封闭）": ("copper", "dome"), "白塔（封闭）": ("white", "cone"),
    "温室": ("glass", "glass"), "门楼": ("slate", "hip"), "风雨廊": ("slate", "flat"), "粮仓": ("tile", "hip"),
    "汲水泉": ("tile", "hip"), "铁工院（封闭）": ("tile", "gable"), "后勤院": ("tile", "gable"),
    "诊疗所与安全端": ("tile", "gable"), "化学实验室（封闭）": ("tile", "gable"),
}
CLOSED_DOORS = {"文契院（封闭部分）": [(0, -1)], "星盘讲堂（封闭）": [(0, -1)], "化学实验室（封闭）": [(0, -1)],
                "白塔（封闭）": [(0, -1)], "铁工院（封闭）": [(-1, 0)], "观星塔（封闭）": [(0, -1)]}


def paint():
    S, site = _campus()
    O, BR = tuple(site["origin_world"]), site["bearing_deg"]
    us = U0 + (np.arange(W) + 0.5) / K
    vs = V1 - (np.arange(H) + 0.5) / K
    UU, VV = np.meshgrid(us, vs)
    X, Y = S.to_world(O, BR, UU, VV)
    Z = S.ground(X, Y)
    land = S.sample(S.LANDF, X, Y) > 0.5
    # ground: grass toned by hillshade, dark basalt along the shore
    gy, gx = np.gradient(Z * 3.0, 1 / K)
    gy = -gy
    slope = np.arctan(np.hypot(gx, gy)); aspect = np.arctan2(-gx, gy)
    shade = np.clip(np.sin(math.radians(40)) * np.cos(slope) + np.cos(math.radians(40)) * np.sin(slope) * np.cos(math.radians(315) - aspect), 0, 1)
    g0 = rng(5)
    tone = g0.choice([0, 1, 1, 2], size=(H, W))
    pal = np.array([mix(SAGE[3], LEAF[3], 0.35)[:3], mix(SAGE[3], SAGE[4], 0.4)[:3], mix(SAGE[4], OCHRE[3], 0.18)[:3]], float)
    rgb = pal[tone]
    patch = painter._tile_to(painter.material("grass"), W, H).astype(float)[..., :3]
    rgb = rgb * 0.86 + patch * 0.14
    rgb = rgb * (0.70 + 0.42 * shade[..., None])
    shore = land & (Z < 1.6)
    basalt = painter._tile_to(painter.material("basalt"), W, H).astype(float)[..., :3]
    rgb = np.where(shore[..., None], basalt * 0.8, rgb)
    sea = np.array(SEA[2][:3], float) * (0.85 + 0.25 * np.clip(-Z / 3, 0, 1))[..., None]
    rgb = np.where(land[..., None], rgb, sea)
    # foam fringe
    edge = np.zeros_like(land)
    for d in (1, 2, 3, 4):
        edge |= np.roll(land, d, 0) | np.roll(land, -d, 0) | np.roll(land, d, 1) | np.roll(land, -d, 1)
    foam = edge & ~land
    rgb = np.where(foam[..., None], np.array(SEA[5][:3], float), rgb)
    c = Canvas(W, H)
    c.a[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    c.a[..., 3] = 255
    # paths, avenue, oath court
    gravel = painter._tile_to(painter.material("gravel"), W, H)
    paving = painter._tile_to(painter.material("paving"), W, H)
    def stroke(points, width, src):
        for (ua, va), (ub, vb) in zip(points[:-1], points[1:]):
            ax, ay = px(ua, va); bx, by = px(ub, vb)
            x0, x1 = int(max(0, min(ax, bx) - width * K)), int(min(W, max(ax, bx) + width * K))
            y0, y1 = int(max(0, min(ay, by) - width * K)), int(min(H, max(ay, by) + width * K))
            if x1 <= x0 or y1 <= y0:
                continue
            gx_, gy_ = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
            dx, dy = bx - ax, by - ay
            t = np.clip(((gx_ - ax) * dx + (gy_ - ay) * dy) / max(dx * dx + dy * dy, 1e-6), 0, 1)
            d = np.hypot(gx_ - (ax + t * dx), gy_ - (ay + t * dy))
            m = d <= width * K / 2
            c.a[y0:y1, x0:x1][m] = src[y0:y1, x0:x1][m]
            rim = (d > width * K / 2 - 1.2) & m
            c.a[y0:y1, x0:x1][rim] = (np.array(STONE[1], np.uint8))
    ev_to_uv = lambda X_, Y_: None
    to_uv = _inverse(O, BR)
    for p in site["paths"]:
        pts = _path_uv(S, O, BR, p["name"])
        stroke(pts, p["width"] if p["name"] != "主轴大道" else 8, paving if p["name"] == "主轴大道" else gravel)
    road = [to_uv(*q) for q in site["harbor_road"]] if False else [to_uv(*q) for q in _harbor_road()]
    stroke(road, 5, gravel)
    oath = next(b for b in S.B if b["name"] == "誓约庭")
    _fill_rect(c, oath, paving)
    dry = next(b for b in S.B if b["name"] == "晾晒场")
    _fill_rect(c, dry, gravel)
    for q0, q1 in [(_harbor_quay()[0], _harbor_quay()[1])]:
        a, b = to_uv(*q0), to_uv(*q1)
        stroke([a, b], 6, painter._tile_to(painter.material("basalt"), W, H))
    # lanes of the living wing: gravel, wet in the morning
    LW = S.LW
    for side in (0, 1):
        u_a = LW["u0"] + (0 if side == 0 else LW["ZL"] + LW["HALL"])
        rect = dict(c=(u_a + LW["ZL"] / 2, LW["v0"] - LW["ZD"] - LW["LANE"] / 2), s=(LW["ZL"], LW["LANE"]))
        _fill_rect(c, rect, gravel)
    # collapse on the harbour road: rubble across the zig-zag ramp
    cu, cv = to_uv(*_collapse_point())
    g = rng(77)
    cx_, cy_ = px(cu, cv)
    for _ in range(140):
        rx, ry = cx_ + float(g.normal(0, 18)), cy_ + float(g.normal(0, 12))
        r_ = float(g.uniform(2, 6))
        c.ellipse(rx, ry + 1, r_ + 1, r_ * 0.7 + 1, CAP[1])
        c.ellipse(rx, ry, r_, r_ * 0.7, STONE[int(g.integers(1, 5))])
    return c, (S, site, O, BR, land)


def _inverse(O, BR):
    b = math.radians(BR)
    ev = (math.sin(b), math.cos(b)); eu = (math.cos(b), -math.sin(b))
    def f(x, y):
        dx, dy = x - O[0], y - O[1]
        return (dx * eu[0] + dy * eu[1], dx * ev[0] + dy * ev[1])
    return f


def _render_site():
    sys.path.insert(0, str(SITE_DIR))
    import render_site as RS                   # noqa: E402
    return RS


def _harbor_road():
    return _render_site().HARBOR_ROAD


def _harbor_quay():
    return _render_site().QUAY


def _collapse_point():
    r = _render_site().HARBOR_ROAD
    return ((r[5][0] + r[6][0]) / 2, (r[5][1] + r[6][1]) / 2)


def _path_uv(S, O, BR, name):
    for n, pts, w in _render_site().S.PATHS:
        if n == name:
            return [tuple(p) for p in pts]
    return []


def _fill_rect(c, b, src):
    (cu, cv), (su, sv) = b["c"], b["s"]
    x0, y0 = px(cu - su / 2, cv + sv / 2)
    x1, y1 = px(cu + su / 2, cv - sv / 2)
    x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
    c.a[y0:y1, x0:x1] = src[y0:y1, x0:x1]


# ------------------------------------------------------------------ roofs ----

def roof_sprite(kind: str, form: str, w: int, h: int, ridge: str, seed: int = 1, clerestory: bool = False) -> Canvas:
    """Top-down roof, light from the north-west; the canvas is the footprint plus a cast shadow."""
    pad = 10
    c = Canvas(w + pad, h + pad)
    for i in range(pad):                       # cast shadow to the south-east
        a = int(110 * (1 - i / pad) ** 1.3)
        if form in ("dome", "cone"):
            c.ellipse(w / 2 + i + 2, h / 2 + i + 2, w / 2, h / 2, (16, 22, 18, a))
        else:
            c.rect(i + 2, i + 2, w, h, (16, 22, 18, a))
    ramp = {"tile": TILE_ROOF, "slate": SLATE_ROOF, "copper": COPPER, "white": WHITE_ROOF, "glass": FOG}[kind]
    if form in ("dome", "cone"):
        r = min(w, h) / 2
        cx, cy = w / 2, h / 2
        for y in range(h):
            for x in range(w):
                d = math.hypot(x + 0.5 - cx, y + 0.5 - cy) / r
                if d > 1:
                    continue
                lit = ((cx - x) + (cy - y)) / (r * 1.6)
                tone = 2.6 + 2.2 * lit * (1 - d * 0.5) - d * 0.8
                c.px(x, y, ramp[int(max(0, min(5, round(tone))))])
        for k in range(16 if form == "dome" else 24):
            a = k * 2 * math.pi / (16 if form == "dome" else 24)
            c.line(int(cx + r * 0.18 * math.cos(a)), int(cy + r * 0.18 * math.sin(a)), int(cx + r * 0.97 * math.cos(a)), int(cy + r * 0.97 * math.sin(a)), ramp[1])
        c.ellipse(cx, cy, r * 0.16, r * 0.16, ramp[4] if form == "dome" else ramp[3])
        c.ellipse(cx - 1, cy - 1, r * 0.08, r * 0.08, BRASS[4] if form == "dome" else ramp[5])
        return c
    if form == "glass":
        c.rect(0, 0, w, h, FOG[3])
        c.dither(0, 0, w, h, FOG[4], 0.25)
        for x in range(0, w, 8):
            c.vline(x, 0, h, IRON[2])
        for y in range(0, h, h // 3):
            c.hline(0, y, w, IRON[1])
            c.hline(0, y + 1, w, IRON[3])
        c.frame(0, 0, w, h, IRON[1])
        return c
    if form == "flat":
        c.rect(0, 0, w, h, ramp[2])
        c.dither(0, 0, w, h, ramp[3], 0.2)
        c.frame(0, 0, w, h, ramp[0])
        return c
    # gable / hip: classify each pixel by its nearest eave
    along_x = (ridge == "u")                   # ridge parallel to the map's x axis
    for y in range(h):
        for x in range(w):
            dn, ds, dw, de = y, h - 1 - y, x, w - 1 - x
            if form == "hip":
                facet = min((dn, "n"), (ds, "s"), (dw, "w"), (de, "e"))[1]
            else:
                facet = ("n" if dn < ds else "s") if along_x else ("w" if dw < de else "e")
            base = {"n": 4, "w": 4, "s": 2, "e": 3}[facet]
            course = (dn if facet in "ns" else dw) if facet in "nw" else (ds if facet == "s" else de)
            tone = base - (1 if course % 4 == 3 else 0)
            col = ramp[tone]
            if kind == "tile" and course % 4 != 3 and ((x if facet in "ns" else y) + (course // 4) * 3) % 7 == 0:
                col = ramp[tone - 1]
            c.px(x, y, col)
    # ridge and hips
    if form == "hip":
        m = min(w, h) // 2
        if along_x or w >= h:
            c.hline(m, h // 2, max(1, w - 2 * m), ramp[5])
            c.line(0, 0, m, h // 2, ramp[5]); c.line(0, h - 1, m, h // 2, ramp[1])
            c.line(w - 1, 0, w - 1 - m, h // 2, ramp[5]); c.line(w - 1, h - 1, w - 1 - m, h // 2, ramp[1])
        else:
            c.vline(w // 2, m, max(1, h - 2 * m), ramp[5])
    elif along_x:
        c.hline(0, h // 2, w, ramp[5]); c.hline(0, h // 2 + 1, w, ramp[1])
    else:
        c.vline(w // 2, 0, h, ramp[5]); c.vline(w // 2 + 1, 0, h, ramp[1])
    if clerestory:                             # glazing strips on both slopes of the raised hall roof
        for x in (w // 2 - 6, w // 2 + 5):
            for y in range(6, h - 6, 10):
                c.rect(x, y, 2, 6, FOG[4])
    c.frame(0, 0, w, h, ramp[0])
    g = rng(seed)
    if kind == "tile" and w * h > 2500:        # chimneys
        for _ in range(1 + w * h // 30000):
            cx_, cy_ = int(g.integers(6, w - 12)), int(g.integers(4, max(5, h // 2 - 6)))
            c.rect(cx_, cy_, 6, 7, STONE[2]); c.rect(cx_, cy_, 6, 2, STONE[4]); c.rect(cx_ + 1, cy_ + 2, 4, 2, CAP[0])
    return c


def porch(c: Canvas, x: float, y: float, side: tuple[int, int], closed: bool) -> None:
    """Door step outside a footprint edge; side is the outward direction (du, dv)."""
    du, dv = side
    if dv != 0:
        x0, y0, w_, h_ = int(x - 18), int(y if dv < 0 else y - 10), 36, 10
    else:
        x0, y0, w_, h_ = int(x if du > 0 else x - 10), int(y - 18), 10, 36
    c.rect(x0, y0, w_, h_, STONE[4]); c.frame(x0, y0, w_, h_, STONE[1])
    c.hline(x0, y0, w_, STONE[5])
    if closed:
        if dv != 0:
            c.rect(x0 + 4, y0 + (0 if dv < 0 else h_ - 3), w_ - 8, 3, IRON[1]); c.hline(x0 + 4, y0 + (0 if dv < 0 else h_ - 3), w_ - 8, IRON[4])
        else:
            c.rect(x0 + (0 if du > 0 else w_ - 3), y0 + 4, 3, h_ - 8, IRON[1])
