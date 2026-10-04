"""Chapter-2 outdoors at true proportions: the whole campus, the harbour road and the harbour.

Scale 24 px per metre in a three-quarter view (oblique.py). Positions, sizes and paths come
from artifacts/remake-chapter2/campus-site (Corvo terrain scaled 0.5, siting in site.json, the
building list and paths in campus.py, the harbour road in render_site.py). The map 'up' is the
campus axis (bearing about 2 degrees, nearly north).

The painted area is u -128..206 m, v -160..84 m (8016 x 5856 px), cut into six overlapping
regions; the walking script switches region where the player leaves a region's core, keeping
the world position, and the 24 m overlaps keep the camera continuous across the switch.

Walkable: inside the campus wall, the harbour road from the gate (blocked at the rockfall), and
the harbour landing and quay beyond it (reachable once the road is repaired). Everything else
is scenery baked into the ground layer.
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from first_awakening_art.pixel import Canvas, hexc
from . import buildings as BB
from . import nature as NT
from .oblique import SHADOW, Face, Frame, bayer, hash2, part_shadow, quad, render

ROOT = Path(__file__).resolve().parents[3]
SITE_DIR = ROOT / "artifacts/remake-chapter2/campus-site"
K = 24
U0, U1, V0, V1 = -128.0, 206.0, -160.0, 84.0
W, H = int((U1 - U0) * K), int((V1 - V0) * K)
FR = Frame(U0, V1, K)

REGIONS = [  # key, title, painted rect (u0, u1, v0, v1), core rect
    ("campus_nw", "学园·讲堂与星仪院", (-128, -4, -50, 84), (-128, -16, -38, 84)),
    ("campus_n", "学园·中央厅与名录库", (-28, 98, -50, 84), (-16, 86, -38, 84)),
    ("campus_ne", "学园·温室与汲水泉", (74, 206, -50, 84), (86, 206, -38, 84)),
    ("campus_sw", "学园·生活翼", (-128, -4, -160, -26), (-128, -16, -160, -38)),
    ("campus_s", "学园·誓约庭与后勤院", (-28, 98, -160, -26), (-16, 86, -160, -38)),
    ("harbor", "港路与港口", (74, 206, -160, -26), (86, 206, -160, -38)),
]

DOORS = [  # (名称, u, v, 朝外方向, 目标房间, 目标出生点, 外景出生点名)
    ("生活翼北门", -47.8, -42.0, (0, 1), "living_hall", "north_door", "living_north"),
    ("夹道西", -52.8, -56.7, (-1, 0), "living_hall", "west_lane", "lane_west"),
    ("夹道东", -42.8, -56.7, (1, 0), "living_hall", "east_lane", "lane_east"),
    ("中央厅北", 0.0, 17.0, (0, 1), "central_hall", "north", "hall_north"),
    ("中央厅南", 0.0, -17.0, (0, -1), "central_hall", "south", "hall_south"),
    ("中央厅东", 17.0, 0.0, (1, 0), "central_hall", "east", "hall_east"),
    ("名录库", 0.0, 36.0, (0, -1), "archive", "south", "archive_front"),
    ("共鸣厅", -72.0, 6.0, (0, -1), "resonance", "south", "resonance_front"),
    ("诊疗所", 56.0, -39.0, (0, -1), "clinic", "south", "clinic_front"),
    ("后勤院西", 20.0, -67.0, (-1, 0), "logistics", "west", "logistics_west"),
    ("后勤院东", 48.0, -67.0, (1, 0), "logistics", "east", "logistics_east"),
    ("粮仓", 56.0, -64.0, (0, -1), "granary", "south", "granary_front"),
    ("温室", 100.0, -10.0, (0, -1), "greenhouse", "south", "greenhouse_front"),
]
CLOSED = {"文契院（封闭部分）", "星盘讲堂（封闭）", "化学实验室（封闭）", "白塔（封闭）", "铁工院（封闭）", "观星塔（封闭）"}

# 学园围墙（裁定 2026-10-04）：玄武岩砌墙高 1.8 m，粗面岩压顶；门楼是唯一出入口
ENCLOSURE = [(-7.0, -118.5), (-104.0, -112.0), (-104.0, 66.0), (138.0, 66.0), (138.0, -108.0), (7.0, -118.5)]


def px(u, v):
    return ((np.asarray(u, float) - U0) * K, (V1 - np.asarray(v, float)) * K)


def R(*h):
    return np.array([hexc(x)[:3] for x in h], np.int16)


LAWN = R("#253f22", "#2e4b29", "#375830", "#416536", "#4c723c", "#598043", "#698f4d", "#7c9f5b")
PASTURE = R("#2a4122", "#334d27", "#3d5a2d", "#486733", "#53743a", "#608141", "#709049")
HEATH = R("#2f3524", "#3a412a", "#464d31", "#535838", "#616440", "#706f49")
GRAVEL = R("#4d473e", "#5f584c", "#716a5c", "#837b6b", "#958d7b", "#a69e8b", "#b6ae9b")
EARTH = R("#3f3328", "#4e4031", "#5e4e3b", "#6e5c46", "#7e6a51", "#8e795d")
LIME = R("#8a877d", "#a6a397", "#bdbaae", "#d1cec3", "#e2dfd5", "#eeebe3")
BASALT = R("#121316", "#1b1c20", "#25272c", "#30333a", "#3d4148", "#4b5058", "#5d636b")
TRACH = R("#4f483f", "#665d51", "#7d7363", "#938875", "#a89d88", "#bbb19b", "#cbc2ad")
ROCK = R("#1d1f22", "#282a2e", "#33363b", "#3f4248", "#4c5057", "#5b6068")
SEA = R("#0b1f2c", "#10293a", "#163448", "#1c4156", "#244f64", "#2f6072", "#3f7482", "#5a8e97", "#86b0b0")
FOAM = R("#a9c4c2", "#cfe0dc", "#eef4f1")


def _campus():
    sys.path.insert(0, str(SITE_DIR))
    import campus as S                     # noqa: E402  (site scripts, scaled terrain)
    import render_site as RS               # noqa: E402
    site = json.loads((SITE_DIR / "site.json").read_text(encoding="utf-8"))
    return S, RS, site


def _to_uv(O, BR):
    b = math.radians(BR)
    ev = (math.sin(b), math.cos(b)); eu = (math.cos(b), -math.sin(b))
    return lambda x, y: ((x - O[0]) * eu[0] + (y - O[1]) * eu[1], (x - O[0]) * ev[0] + (y - O[1]) * ev[1])


def _upsample(a, mode=Image.BILINEAR):
    return np.asarray(Image.fromarray(a.astype(np.float32), "F").resize((W, H), mode))


def _lowfreq(cell_m, seed, shape=(H, W)):
    gw, gh = int(W / (cell_m * K)) + 3, int(H / (cell_m * K)) + 3
    g = np.random.default_rng(seed).random((gh, gw)).astype(np.float32)
    big = Image.fromarray(g, "F").resize((int(gw * cell_m * K), int(gh * cell_m * K)), Image.BICUBIC)
    return np.asarray(big)[:shape[0], :shape[1]]


def _line_mask(polys, width_m, size=(W, H), jitter=0.0):
    m = Image.new("L", size, 0)
    d = ImageDraw.Draw(m)
    for pts in polys:
        xy = [tuple(map(float, px(u, v))) for u, v in pts]
        d.line(xy, fill=255, width=max(1, int(round(width_m * K))), joint="curve")
        r = width_m * K / 2
        for x, y in (xy[0], xy[-1]):
            d.ellipse((x - r, y - r, x + r, y + r), fill=255)
    return np.asarray(m) > 0


def _poly_mask(pts, size=(W, H)):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).polygon([tuple(map(float, px(u, v))) for u, v in pts], fill=255)
    return np.asarray(m) > 0


def _rect_mask(cu, cv, su, sv):
    x0, y0 = px(cu - su / 2, cv + sv / 2)
    x1, y1 = px(cu + su / 2, cv - sv / 2)
    m = np.zeros((H, W), bool)
    m[int(y0):int(y1), int(x0):int(x1)] = True
    return m


def _grow(mask, px_r):
    im = Image.fromarray((mask * 255).astype(np.uint8))
    while px_r > 0:
        k = min(px_r, 7)
        im = im.filter(ImageFilter.MaxFilter(2 * k + 1 if 2 * k + 1 <= 15 else 15))
        px_r -= k
    return np.asarray(im) > 0


# ================================================================ terrain ====

class Ground:
    def __init__(self):
        S, RS, site = _campus()
        self.S, self.RS, self.site = S, RS, site
        self.O, self.BR = tuple(site["origin_world"]), site["bearing_deg"]
        self.to_uv = _to_uv(self.O, self.BR)
        q = 4
        us = U0 + (np.arange(W // q) + 0.5) * q / K
        vs = V1 - (np.arange(H // q) + 0.5) * q / K
        UU, VV = np.meshgrid(us, vs)
        X, Y = S.to_world(self.O, self.BR, UU, VV)
        Zq = S.ground(X, Y).astype(np.float32)
        Lq = S.sample(S.LANDF, X, Y).astype(np.float32)
        gy, gx = np.gradient(Zq * 2.0, q / K)
        gy = -gy
        slope = np.arctan(np.hypot(gx, gy)); aspect = np.arctan2(-gx, gy)
        hs = np.sin(math.radians(45)) * np.cos(slope) + np.cos(math.radians(45)) * np.sin(slope) * np.cos(math.radians(225) - aspect)
        self.Z = _upsample(Zq)
        self.landf = _upsample(Lq)
        self.land = self.landf > 0.5
        self.hill = _upsample(np.clip(hs, 0, 1)) - 0.72
        self.road = [self.to_uv(*p) for p in RS.HARBOR_ROAD]
        self.quay = [self.to_uv(*p) for p in RS.QUAY]
        r = self.road
        self.collapse = ((r[5][0] + r[6][0]) / 2, (r[5][1] + r[6][1]) / 2)
        self.B = {b["name"]: b for b in S.B}
        self.LW = S.LW
        self.inside = _poly_mask(ENCLOSURE)


def paint_ground(G: Ground):
    """The ground layer: grass, pasture, paths, courts, road, coast, sea. Returns rgb int16 and the surface map."""
    xs = np.arange(W)[None, :]
    ys = np.arange(H)[:, None]
    B4 = bayer(xs, ys).astype(np.float32)
    n1 = _lowfreq(9, 11) - 0.5
    n2 = _lowfreq(2.2, 12) - 0.5
    fine = hash2(xs // 2, ys // 2, 13).astype(np.float32) - 0.5
    sea = ~G.land
    near_sea = _upsample(np.asarray(Image.fromarray((sea[::4, ::4] * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(14))).astype(np.float32)) > 6
    # --- grass everywhere on land, mown lawn inside the wall, pasture fields and heath outside
    idx = 3.4 + n1 * 1.8 + n2 * 1.0 + fine * 0.7 + G.hill * 2.4 + B4 * 0.9
    rgb = LAWN[np.clip(np.round(idx), 0, 7).astype(np.int8)]
    outside = ~G.inside
    fields = hash2(np.floor((xs / K + 7 + n1 * 6) / 26), np.floor((ys / K + n1 * 6) / 21), 31)
    pidx = 3.0 + n1 * 1.4 + n2 * 0.8 + fine * 0.8 + G.hill * 2.4 + B4 * 0.9 + (fields - 0.5) * 2.0
    prgb = PASTURE[np.clip(np.round(pidx), 0, 6).astype(np.int8)]
    rgb = np.where(outside[..., None], prgb, rgb)
    high = outside & (G.Z > 30 + n1 * 6)
    hidx = 2.6 + n2 * 1.6 + fine + G.hill * 2.4 + B4 * 0.9
    rgb = np.where(high[..., None], HEATH[np.clip(np.round(hidx), 0, 5).astype(np.int8)], rgb)
    # --- coastal rock band and shore boulders
    rocky = outside & G.land & near_sea & (G.Z < 5.5 + n1 * 3)
    ridx = 2.4 + n2 * 2.2 + fine * 1.4 + G.hill * 2 + B4
    rock_rgb = ROCK[np.clip(np.round(ridx), 0, 5).astype(np.int8)]
    patchy = (n2 + fine * 0.6) > -0.05
    rgb = np.where((rocky & patchy)[..., None], rock_rgb, rgb)
    shore = G.land & (G.landf < 0.62)
    rgb = np.where(shore[..., None], rock_rgb, rgb)
    surface = np.zeros((H, W), np.uint8)               # 0 lawn 1 pasture 2 gravel 3 calçada 4 flag 5 earth 6 road 7 rock 9 sea
    surface[outside] = 1
    surface[rocky & patchy] = 7
    surface[shore] = 7
    # --- sea: depth from the land fraction, wave dashes, shallows and foam
    depth = np.clip(1 - G.landf * 2, 0, 1)
    sidx = 1.0 + (1 - depth) * 4.5 + n1 * 1.2 + B4 * 0.8
    wave = (hash2(np.floor(xs / 11), np.floor(ys / 5), 17) > 0.86) & ((xs % 11) < 6) & ((ys % 5) == 0)
    sidx = sidx + wave * 2.2
    srgb = SEA[np.clip(np.round(sidx), 0, 8).astype(np.int8)]
    rgb = np.where(sea[..., None], srgb, rgb)
    surface[sea] = 9
    edge = sea & _grow(G.land, 3)
    rgb[edge] = FOAM[2]
    edge2 = sea & _grow(G.land, 9) & ~_grow(G.land, 6) & ((hash2(xs // 3, ys // 2, 19) > 0.45))
    rgb[edge2] = FOAM[0]
    return rgb, surface


def paint_paths(G: Ground, rgb, surface):
    xs = np.arange(W)[None, :]
    ys = np.arange(H)[:, None]
    B4 = bayer(xs, ys)
    fine = hash2(xs // 2, ys // 2, 23) - 0.5
    peb = hash2(xs // 3, ys // 3, 24)

    tex = {}

    def lay(mask, kind):
        if kind not in tex:
            if kind == "gravel":
                idx = 3.2 + fine * 1.6 + (peb > 0.9) * 1.6 - (peb < 0.06) * 1.6 + B4 * 0.8
                tex[kind] = (GRAVEL[np.clip(np.round(idx), 0, 6).astype(np.int8)], 2)
            elif kind == "road":
                idx = 2.6 + fine * 1.5 + (peb > 0.92) * 1.5 + B4 * 0.8
                tex[kind] = (EARTH[np.clip(np.round(idx), 0, 5).astype(np.int8)], 6)
            else:
                idx = 2.4 + fine * 1.4 + B4 * 0.8
                tex[kind] = (EARTH[np.clip(np.round(idx), 0, 5).astype(np.int8)], 5)
        col, code = tex[kind]
        rgb[mask] = col[mask]
        surface[mask] = code

    for name, pts, w in G.S.PATHS:
        if name == "主轴大道":
            continue
        core = _line_mask([pts], w)
        ragged = _line_mask([pts], w + 0.35) & (hash2(xs // 2, ys // 2, 25) > 0.5)
        m = core | ragged
        lay(m, "gravel")
        rim = _grow(m, 1) & ~m & G.inside
        rgb[rim] = (rgb[rim] * 0.72).astype(np.int16)
    # harbour road with wheel ruts; it runs on to the quay
    road = [G.road[0]] + G.road[1:]
    m = _line_mask([road], 4.4) | (_line_mask([road], 4.8) & (hash2(xs // 2, ys // 2, 26) > 0.5))
    lay(m, "road")
    for off in (-0.8, 0.8):
        rut = _offset_line(road, off)
        r_ = _line_mask([rut], 0.25) & m
        rgb[r_] = (rgb[r_] * 0.78).astype(np.int16)
    # avenue: calçada of limestone setts, basalt borders and a basalt wave down the middle
    av = _rect_mask(0, -66, 8, 98)
    u = U0 + (xs + 0.5) / K
    v = V1 - (ys + 0.5) / K
    sett = ((xs % 5) == 0) | ((ys + (xs // 5) % 2 * 2) % 5 == 0)
    black = (np.abs(np.abs(u) - 3.55) < 0.42) | (np.abs(u - 1.6 * np.sin(2 * np.pi * v / 6.0)) < 0.33)
    hb = hash2(xs // 5, ys // 5, 27)
    c_w = LIME[np.clip(np.round(3.2 + (hb - 0.5) * 1.6 - sett * 1.8 + B4 * 0.5), 0, 5).astype(np.int8)]
    c_b = BASALT[np.clip(np.round(3.0 + (hb - 0.5) * 1.6 - sett * 1.5 + B4 * 0.5), 0, 6).astype(np.int8)]
    col = np.where(black[..., None], c_b, c_w)
    rgb[av] = col[av]
    surface[av] = 3
    # oath court: trachyte flags falling in steps toward the gate; low walls are sprites
    oc = G.B["誓约庭"]
    om = _rect_mask(*oc["c"], *oc["s"]) | _rect_mask(0, -118.5, 6.0, 7.0)
    row = np.floor((v - (-115)) / 0.7)
    flag_u = np.floor((u + 12 + (row % 2) * 0.5) / 1.0)
    fu = (u + 12 + (row % 2) * 0.5) % 1.0
    fv = ((v - (-115)) / 0.7) % 1
    joint = (fu < 0.06) | (fv < 0.08)
    hf = hash2(flag_u, row, 28)
    idx = 3.6 + (hf - 0.5) * 1.6 - joint * 1.8 + B4 * 0.6 + G.hill * 1.2
    col = TRACH[np.clip(np.round(idx), 0, 6).astype(np.int8)]
    rgb[om] = col[om]
    surface[om] = 4
    for vs_ in (-111.0, -105.0, -99.0, -93.0):                # step risers face the gate (south)
        y0 = int(px(0, vs_)[1])
        x0, x1 = int(px(-12, 0)[0]), int(px(12, 0)[0])
        rgb[y0:y0 + 5, x0:x1] = (TRACH[1] + np.zeros((5, x1 - x0, 1), np.int16))
        rgb[y0 - 1, x0:x1] = TRACH[6]
    # drying yard: trodden earth
    dy = G.B["晾晒场"]
    lay(_rect_mask(*dy["c"], *dy["s"]), "earth")
    # harbour landing and quay: basalt flags; the quay stands 1.6 m out of the water
    land_poly = [(155, -101), (168, -101), (168, -89), (155, -89)]
    lm = _poly_mask(land_poly) | _line_mask([G.quay], 6.0)
    q_u = np.floor(u / 0.9); q_v = np.floor(v / 0.6)
    hq = hash2(q_u + (q_v % 2) * 0.5, q_v, 29)
    jq = ((u / 0.9 + (q_v % 2) * 0.5) % 1 < 0.07) | ((v / 0.6) % 1 < 0.1)
    col = BASALT[np.clip(np.round(4.0 + (hq - 0.5) * 1.6 - jq * 2 + B4 * 0.6), 0, 6).astype(np.int8)]
    rgb[lm] = col[lm]
    surface[lm] = 4
    face = np.zeros_like(lm)
    for k in range(1, int(1.6 * K)):
        face[k:] |= lm[:-k]
    face &= ~lm & (surface == 9)
    fcol = BASALT[np.clip(np.round(2.4 + (hash2(xs // 7, ys // 4, 30) - 0.5) * 1.6 - ((ys % 4) == 0) * 1.2), 0, 6).astype(np.int8)]
    rgb[face] = fcol[face]
    foot = _grow(face | lm, 2) & ~face & ~lm & (surface == 9)
    rgb[foot] = FOAM[1]
    return lm


def _offset_line(pts, d):
    out = []
    for i, (u, v) in enumerate(pts):
        a = pts[max(0, i - 1)]; b = pts[min(len(pts) - 1, i + 1)]
        du, dv = b[0] - a[0], b[1] - a[1]
        L = math.hypot(du, dv) or 1
        out.append((u - dv / L * d, v + du / L * d))
    return out


def pasture_walls(G: Ground, rgb, keep_clear):
    """Dry-stone field walls outside the campus, baked: top in plan plus a 1 m face below it."""
    g = np.random.default_rng(41)
    us_ = np.arange(-160, 240, 24.0); vs_ = np.arange(-190, 110, 20.0)
    P = {(i, j): (u + g.uniform(-5, 5), v + g.uniform(-4, 4)) for i, u in enumerate(us_) for j, v in enumerate(vs_)}
    lines = []
    for (i, j), a in P.items():
        for b in (P.get((i + 1, j)), P.get((i, j + 1))):
            if b is None or g.random() < 0.18:
                continue
            if g.random() < 0.4:                              # a field gate, 3.5 m
                L = math.hypot(b[0] - a[0], b[1] - a[1])
                t = g.uniform(0.25, 0.75); h = 1.75 / L
                lines.append([a, (a[0] + (b[0] - a[0]) * (t - h), a[1] + (b[1] - a[1]) * (t - h))])
                lines.append([(a[0] + (b[0] - a[0]) * (t + h), a[1] + (b[1] - a[1]) * (t + h)), b])
            else:
                lines.append([a, b])
    top = _line_mask(lines, 0.7)
    top &= G.land & ~keep_clear & ~(G.Z > 34) & (G.landf > 0.7)
    xs = np.arange(W)[None, :]; ys = np.arange(H)[:, None]
    st = hash2(xs // 6, ys // 4 + (xs // 6) % 2, 43)
    up = np.zeros_like(top)
    hgt = int(1.0 * K)
    up[:-hgt] = top[hgt:]                                   # the top sits 1 m up the screen
    face = np.zeros_like(top)
    for k in range(hgt):
        face[k:H - hgt + k] |= top[hgt:]
    face &= ~up
    shade = BASALT[np.clip(np.round(2.0 + (st - 0.5) * 2.2 - ((ys % 6) == 0) * 1.2), 0, 6).astype(np.int8)]
    topc = BASALT[np.clip(np.round(4.0 + (st - 0.5) * 2.2), 0, 6).astype(np.int8)]
    rgb[face] = shade[face]
    rgb[up] = topc[up]
    shadow = np.zeros_like(top)
    off = int(0.8 * K)
    shadow[:-off, off:] = (top | face)[off:, :-off]
    shadow &= ~(face | up)
    rgb[shadow] = (rgb[shadow] * 0.7).astype(np.int16)


def tufts_and_flowers(rgb, surface, seed=51):
    g = np.random.default_rng(seed)
    n = W * H // 900
    x = g.integers(2, W - 2, n); y = g.integers(3, H - 1, n)
    ok = (surface[y, x] <= 1)
    x, y = x[ok], y[ok]
    dark = (rgb[y, x] * 0.68).astype(np.int16)
    light = np.clip(rgb[y, x] * 1.25 + 10, 0, 255).astype(np.int16)
    for dx, dy in ((0, 0), (-1, -1), (1, -1), (0, -2)):
        rgb[y + dy, x + dx] = dark
    rgb[y - 2, x - 1] = light; rgb[y - 3, x] = light
    m = W * H // 5000
    x = g.integers(1, W - 1, m); y = g.integers(1, H - 1, m)
    ok = (surface[y, x] <= 1)
    x, y = x[ok], y[ok]
    cols = R("#e9e4d2", "#e6cf5a", "#c99ac9", "#f2f0ea")[g.integers(0, 4, len(x))]
    rgb[y, x] = cols
    rgb[y, x + 1] = (cols * 0.8).astype(np.int16)


# ================================================================ buildings ====

def _south_doors(cu, cv, su, sv):
    out, closed = [], []
    u0, v0 = cu - su / 2, cv - sv / 2
    for name, u, v, (du, dv), *_ in DOORS:
        if (du, dv) == (0, -1) and abs(v - v0) < 0.2 and u0 < u < u0 + su:
            out.append(u - u0)
    return out


def models(G: Ground) -> list[BB.Building]:
    out = []
    B = G.B

    def plain(name, style, roof, form, h, closed=False, arch=False, sill=1.0, wh=1.8, spacing=3.2, vent=False, ridge="auto",
              chimneys=(), extra=(), pitch=32, quoins=True):
        b = B[name]
        (cu, cv), (su, sv) = b["c"], b["s"]
        u0, u1, v0, v1 = cu - su / 2, cu + su / 2, cv - sv / 2, cv + sv / 2
        doors = _south_doors(cu, cv, su, sv)
        feats = [BB.door_feat(d, arch=arch) for d in doors]
        if closed and not doors:
            doors = [su / 2]
            feats = [BB.door_feat(su / 2, closed=True, arch=arch)]
        feats = BB.windows_along(su, sill, wh, width=1.1 if not arch else 1.2, spacing=spacing, doors=doors, arch=arch, closed=closed) + feats + list(extra)
        m = BB.Building(name)
        BB.box(m, u0, u1, v0, v1, 0, h, style, feats, quoins=quoins)
        BB.roof(m, u0, u1, v0, v1, h, roof, form, ridge=ridge, gable_style=style, vent=vent, pitch=pitch)
        for (cu_, cv_) in chimneys:
            BB.chimney(m, cu_, cv_, h + 1.0, 3.2, style)
        out.append(m)
        return m

    plain("讲堂", "trachyte", "slate", "gable", 6.0, arch=True, sill=1.3, wh=2.6)
    plain("名录库", "basalt", "slate", "gable", 6.5, arch=True, sill=1.6, wh=2.4, vent=True)
    plain("文献工房", "basalt", "slate", "hip", 4.6, sill=1.1)
    plain("文契院（封闭部分）", "basalt", "slate", "gable", 5.6, closed=True, arch=True, sill=1.3, wh=2.2)
    plain("共鸣厅", "basalt", "slate", "gable", 10.0, arch=True, sill=3.0, wh=4.2, spacing=3.6)
    plain("沉静室", "basalt", "slate", "hip", 3.4, sill=1.6, wh=0.9, spacing=6)
    plain("星盘讲堂（封闭）", "basalt", "slate", "gable", 6.0, closed=True, arch=True, sill=1.3, wh=2.4)
    plain("诊疗所与安全端", "white", "tile", "gable", 4.4)
    plain("化学实验室（封闭）", "basalt", "tile", "gable", 5.0, closed=True)
    plain("后勤院", "white", "tile", "gable", 5.0, chimneys=[(26, -57), (42, -58)])
    iron = plain("铁工院（封闭）", "basalt", "tile", "gable", 7.0, closed=True, sill=2.4, wh=2.4, spacing=4.0)
    BB.chimney(iron, 124, -84, 11.0, 11.0, "basalt")

    # granary: rubble walls lifted on staddle stones
    b = B["粮仓"]
    (cu, cv), (su, sv) = b["c"], b["s"]
    m = BB.Building("粮仓")
    for su_ in (0.6, su / 2, su - 0.6):
        BB.box(m, cu - su / 2 + su_ - 0.3, cu - su / 2 + su_ + 0.3, cv - sv / 2 + 0.2, cv - sv / 2 + 0.8, 0, 0.9, "basalt", plinth=0, quoins=False, solid=False)
    BB.box(m, cu - su / 2, cu + su / 2, cv - sv / 2, cv + sv / 2, 0.9, 3.6, "basalt", [BB.door_feat(su / 2, width=1.4, height=2.3)], plinth=0.2)
    BB.roof(m, cu - su / 2, cu + su / 2, cv - sv / 2, cv + sv / 2, 4.5, "tile", "hip")
    m.solid(cu - su / 2, cv - sv / 2, cu + su / 2, cv + sv / 2)
    out.append(m)

    # central hall: trachyte drum, copper dome, lantern; four porticoes
    m = BB.Building("中央厅")
    wins = []
    for ang in (-62, -36, 36, 62):
        s = 12 * math.radians(ang)
        wins.append(dict(kind="window", s0=s - 0.75, s1=s + 0.75, t0=2.2, t1=5.6, arch=True))
    BB.cylinder(m, (0, 0), 12, 0, 7.5, "trachyte", wins, plinth=0.7)
    BB.dome(m, (0, 0), 12.4, 7.5, 8.5, "copper")
    BB.cylinder(m, (0, 0), 1.7, 15.6, 2.0, "trachyte", [dict(kind="window", s0=-0.4, s1=0.4, t0=0.4, t1=1.6, arch=True)], plinth=0, solid=False)
    BB.dome(m, (0, 0), 1.9, 17.6, 1.3, "copper", n=20, rings=5)
    out.append(m)
    for nm, (cu, cv), (su, sv), side in (("中央厅北门廊", (0, 14.5), (7, 5), "n"), ("中央厅南门廊", (0, -14.5), (7, 5), "s"),
                                       ("中央厅东门廊", (14.5, 0), (5, 7), "e"), ("中央厅西门廊", (-14.5, 0), (5, 7), "w")):
        m = BB.Building(nm)
        u0, u1, v0, v1 = cu - su / 2, cu + su / 2, cv - sv / 2, cv + sv / 2
        if side == "s":
            BB.columns_face(m, u0, u1, v0, 0, 5.2, 4, door=(2.6, 4.4, 3.2))
        elif side in "ew":
            BB.columns_face(m, u0, u1, v0, 0, 5.2, 2)
        else:
            BB.box(m, u0, u1, v0, v1, 0, 5.2, "trachyte", quoins=False, solid=False)
        m.solid(u0, v0, u1, v1)
        m.add_part([(u, v, z) for u in (u0, u1) for v in (v0, v1) for z in (0, 5.2)])
        BB.roof(m, u0, u1, v0, v1, 5.2, "slate", "gable", ridge="v" if side in "ns" else "u", gable_style="trachyte", pitch=24, over=0.3)
        out.append(m)
    # covered walk between the lecture hall and the west portico
    b = B["风雨廊"]
    (cu, cv), (su, sv) = b["c"], b["s"]
    m = BB.Building("风雨廊")
    BB.posts(m, cu - su / 2, cu + su / 2, cv - sv / 2, cv + sv / 2, 3.0)
    BB.roof(m, cu - su / 2, cu + su / 2, cv - sv / 2, cv + sv / 2, 3.0, "slate", "gable", ridge="u", pitch=22, over=0.2)
    m.solid(cu - su / 2, cv - sv / 2, cu + su / 2, cv + sv / 2)
    out.append(m)

    # towers
    b = B["观星塔（封闭）"]
    m = BB.Building("观星塔（封闭）")
    BB.cylinder(m, b["c"], 5, 0, 11, "basalt", [dict(kind="door", s0=-0.8, s1=0.8, t0=0, t1=2.6, closed=True)] +
                [dict(kind="window", s0=x - 0.3, s1=x + 0.3, t0=t, t1=t + 1.3, closed=True) for x, t in ((-2.6, 4.2), (2.6, 4.2), (0, 7.4))], plinth=0.6)
    BB.dome(m, b["c"], 5.3, 11, 4.4, "copper", n=36, rings=9)
    out.append(m)
    b = B["白塔（封闭）"]
    m = BB.Building("白塔（封闭）")
    BB.cylinder(m, b["c"], 5, 0, 15, "white", [dict(kind="door", s0=-0.8, s1=0.8, t0=0, t1=2.6, closed=True)] +
                [dict(kind="window", s0=-0.35, s1=0.35, t0=t, t1=t + 1.5, closed=True) for t in (5.0, 9.0, 12.4)], plinth=0.7)
    BB.cone(m, b["c"], 5.6, 15, 6.8, "slate")
    out.append(m)

    # gatehouse: two towers and the arch block between them
    b = B["门楼"]
    (cu, cv), (su, sv) = b["c"], b["s"]
    m = BB.Building("门楼")
    for side in (-1, 1):
        tu = cu + side * 5
        BB.box(m, tu - 2, tu + 2, cv - 3.5, cv + 3.5, 0, 9, "trachyte",
               [dict(kind="window", s0=1.5, s1=2.5, t0=5.2, t1=7.0, arch=True), dict(kind="window", s0=1.7, s1=2.3, t0=2.0, t1=3.0)])
        BB.roof(m, tu - 2, tu + 2, cv - 3.5, cv + 3.5, 9, "slate", "hip", pitch=40, over=0.3)
    BB.box(m, cu - 3, cu + 3, cv - 3.0, cv + 3.0, 0, 6.6, "trachyte", [dict(kind="hole", s0=1.0, s1=5.0, t0=0, t1=4.8, arch=True)],
           quoins=False, solid=False)
    BB.roof(m, cu - 3, cu + 3, cv - 3.0, cv + 3.0, 6.6, "lead", "flat", over=0.15)
    m.solid(cu - 7, cv - 3.5, cu - 3, cv + 3.5)
    m.solid(cu + 3, cv - 3.5, cu + 7, cv + 3.5)
    m.anchor_v = cv - 3.5
    out.append(m)

    # greenhouse: three glass houses stepping up the slope
    b = B["温室"]
    (cu, cv), (su, sv) = b["c"], b["s"]
    m = BB.Building("温室")
    for k in range(3):
        v0 = cv - sv / 2 + k * 10
        BB.box(m, cu - su / 2, cu + su / 2, v0, v0 + 10, 0, 0.6, "basalt", plinth=0, quoins=False, solid=False)
        gf = Face(np.array([(cu - su / 2, v0, 0.6), (cu + su / 2, v0, 0.6), (cu + su / 2, v0, 3.0), (cu - su / 2, v0, 3.0)]), _glass_wall(k == 0))
        m.faces.append(gf)
        m.add_part([(u, v, z) for u in (cu - su / 2, cu + su / 2) for v in (v0, v0 + 10) for z in (0, 3)])
        BB.roof(m, cu - su / 2, cu + su / 2, v0, v0 + 10, 3.0, "glass", "gable", ridge="v", pitch=28, over=0.1, gable_style="glass")
        m.faces[-1].mat = _glass_wall(False, gable=True)
    m.solid(cu - su / 2, cv - sv / 2, cu + su / 2, cv + sv / 2)
    out.append(m)

    # spring pavilion
    b = B["汲水泉"]
    (cu, cv), (su, sv) = b["c"], b["s"]
    m = BB.Building("汲水泉亭")
    BB.posts(m, cu - su / 2, cu + su / 2, cv - sv / 2, cv + sv / 2, 2.6, w=0.3)
    BB.roof(m, cu - su / 2, cu + su / 2, cv - sv / 2, cv + sv / 2, 2.6, "tile", "hip", pitch=30, over=0.35)
    m.anchor_v = cv - sv / 2
    out.append(m)

    # living wing (vertical hall with lanes)
    LW = G.LW
    u0, v0, ZL, ZD, LA, HA, BA = (LW[k] for k in ("u0", "v0", "ZL", "ZD", "LANE", "HALL", "BATH"))
    for nm, (cu, cv), (su, sv), grp in G.S.lw_blocks():
        if nm.startswith("起居厅"):
            continue
        m = BB.Building("生活翼·" + nm)
        a0, a1, b0, b1 = cu - su / 2, cu + su / 2, cv - sv / 2, cv + sv / 2
        if nm == "浴室盥洗":
            feats = BB.windows_along(su, 1.9, 0.8, width=0.9, spacing=2.6, margin=1.4)
            BB.box(m, a0, a1, b0, b1, 0, 3.4, "white", feats)
            BB.roof(m, a0, a1, b0, b1, 3.4, "tile", "hip")
        else:
            feats = BB.windows_along(su, 1.0, 1.6, width=1.0, spacing=3.7, margin=1.9)
            BB.box(m, a0, a1, b0, b1, 0, 3.8, "white", feats)
            BB.roof(m, a0, a1, b0, b1, 3.8, "tile", "gable", ridge="u", chimneys=None) if False else \
                BB.roof(m, a0, a1, b0, b1, 3.8, "tile", "gable", ridge="u")
            BB.chimney(m, a0 + su * (0.3 if grp == "N" else 0.7), cv + 1.2, 6.4, 1.6, "white")
        out.append(m)
    hu = u0 + ZL + HA / 2
    m = BB.Building("生活翼·起居厅")
    a0, a1, b0, b1 = hu - HA / 2, hu + HA / 2, v0 - 2 * ZD - LA, v0
    clere = [dict(kind="window", s0=x - 0.5, s1=x + 0.5, t0=4.0, t1=5.1, arch=True) for x in (2.2, 5.0, 7.8)]
    BB.box(m, a0, a1, b0, b1, 0, 5.6, "white", clere)
    BB.roof(m, a0, a1, b0, b1, 5.6, "slate", "gable", ridge="v", gable_style="white", vent=True, pitch=30)
    out.append(m)
    return out


def _glass_wall(door, gable=False):
    def mat(face, P, s, t):
        Lt = BB.lit(face)
        idx = 4.0 + Lt + (((s + t * 0.7) % 1.3) < 0.2) * 1.6
        plant = (hash2(np.floor(s / 0.5), np.floor(t / 0.5), 61) > 0.42) & (t < 1.9)
        rgb = BB.pick(BB.GLASS, idx)
        rgb = np.where(plant[:, None], (rgb * np.array([0.6, 0.88, 0.58])).astype(np.int16), rgb)
        frame = ((s % 0.6) < 0.07) | ((t % 1.2) < 0.07)
        rgb = np.where(frame[:, None], BB.pick(BB.WHITE, 6 + 0 * s), rgb)
        keep = np.ones(len(s), bool)
        if door:
            mid = 5.0
            m = (np.abs(s - mid) < 0.9) & (t < 2.4)
            d = BB.pick(BB.WOOD, 3.4 + 0 * s - (np.abs(s - mid) < 0.04) * 2)
            d = np.where(((t > 0.9) & (np.abs(s - mid) > 0.12) & (np.abs(s - mid) < 0.78))[:, None], BB.pick(BB.PANE, 3 + 0 * s), d)
            rgb = np.where(m[:, None], d, rgb)
        return np.clip(rgb, 0, 255).astype(np.uint8), keep
    return mat


# ================================================================ walls ====

def _rubble_mat(h, coping):
    def mat(face, P, s, t):
        Lt = BB.lit(face)
        if face.normal[2] > 0.9:                          # top
            if coping:
                col = BB.pick(BB.TRACHYTE, 5.0 + Lt + (hash2(np.floor(s / 0.7), 0, 71) - 0.5) * 1.2 - ((s % 0.7) < 0.06) * 2 + 0 * t)
            else:
                col = BB.pick(BB.BASALT, 4.2 + Lt + (hash2(np.floor(s / 0.3), np.floor(t / 0.25), 72) - 0.5) * 2.4)
            return np.clip(col, 0, 255).astype(np.uint8), np.ones(len(s), bool)
        joint, hh = BB._rubble(s + P[:, 0] * 0.0 + face.data.get("s_off", 0), t, 73)
        col = BB.pick(BB.BASALT, 3.6 + Lt + (hh - 0.5) * 2.2 + joint * (1.6 if coping else -1.6))
        if coping:
            cap = t > h - 0.16
            col = np.where(cap[:, None], BB.pick(BB.TRACHYTE, 4.2 + Lt + 0 * s), col)
        col = np.where((t < 0.12)[:, None], (col * 0.7).astype(np.int16), col)
        return np.clip(col, 0, 255).astype(np.uint8), np.ones(len(s), bool)
    return mat


def wall_run(name, pts, h, thick, coping, seg=2.4):
    """Split a polyline wall into short boxes, one sprite each (correct depth sorting)."""
    out = []
    k = 0
    acc = 0.0
    for (ua, va), (ub, vb) in zip(pts[:-1], pts[1:]):
        L = math.hypot(ub - ua, vb - va)
        n = max(1, int(math.ceil(L / seg)))
        du, dv = (ub - ua) / L, (vb - va) / L
        nu, nv = dv, -du                                  # the side normal
        if nv > 0 or (abs(nv) < 1e-9 and nu < 0):
            nu, nv = -nu, -nv                             # pick the south-facing side
        for i in range(n):
            a = (ua + du * L * i / n, va + dv * L * i / n)
            b = (ua + du * L * (i + 1) / n, va + dv * L * (i + 1) / n)
            ht = thick / 2
            p = [(a[0] + nu * ht, a[1] + nv * ht), (b[0] + nu * ht, b[1] + nv * ht), (b[0] - nu * ht, b[1] - nv * ht), (a[0] - nu * ht, a[1] - nv * ht)]
            m = BB.Building(f"{name}{k:03d}")
            mat = _rubble_mat(h, coping)
            if abs(nv) > 0.05:
                f = quad((p[0][0], p[0][1], 0), (p[1][0], p[1][1], 0), (p[1][0], p[1][1], h), (p[0][0], p[0][1], h), mat)
                f.data["s_off"] = acc
                m.faces.append(f)
            # end caps toward the viewer
            for e0, e1 in ((p[0], p[3]), (p[1], p[2])):
                if abs(e0[1] - e1[1]) > 0.05 or True:
                    cap = quad((e0[0], e0[1], 0), (e1[0], e1[1], 0), (e1[0], e1[1], h), (e0[0], e0[1], h), mat)
                    if cap.normal[1] < -0.05:
                        m.faces.append(cap)
            m.faces.append(quad((p[0][0], p[0][1], h), (p[1][0], p[1][1], h), (p[2][0], p[2][1], h), (p[3][0], p[3][1], h), mat))
            m.add_part([(x, y, z) for x, y in p for z in (0, h)])
            m.anchor_v = min(q[1] for q in p)
            us = [q[0] for q in p]; vs = [q[1] for q in p]
            m.solid(min(us), min(vs), max(us), max(vs))
            m.anchor_v = min(vs)
            out.append(m)
            acc += L / n
            k += 1
    return out


# ================================================================ assembly ====

def _canvas_key(prefix, i):
    return f"{prefix}_{i:03d}"


def build(prop_sizes: dict, prop_fps: dict):
    """Returns (region rooms, spawn name -> region key, sprites)."""
    G = Ground()
    rgb, surface = paint_ground(G)
    landing = paint_paths(G, rgb, surface)
    corridor = _line_mask([G.road], 5.2)
    keep_clear = _grow(G.inside, int(3 * K)) | _grow(corridor, int(2.5 * K)) | _grow(landing, K)
    pasture_walls(G, rgb, keep_clear)
    tufts_and_flowers(rgb, surface)

    objects = []                                         # sprites: dict(name, key, canvas|None, x, y, footprint, occluder)
    sprites = {}
    shadow = Image.new("L", (W, H), 0)
    sd = ImageDraw.Draw(shadow)

    def add_model(m: BB.Building, prefix, occluder=True):
        if not m.faces:
            return
        can, left, bottom = render(FR, m.faces, m.anchor_v)
        key = _canvas_key(prefix, len([k for k in sprites if k.startswith(prefix)]))
        sprites[key] = can
        for part in m.parts:
            sd.polygon(part_shadow(FR, part), fill=255)
        fps = []
        for u0, v0, u1, v1 in m.solids:
            x0, y0 = px(u0, v1); x1, y1 = px(u1, v0)
            fps.append([int(round(x0)), int(round(y0)), int(round(x1 - x0)), int(round(y1 - y0))])
        objects.append(dict(name=m.name, key=key, x=left + can.w // 2, y=bottom, solids=fps, occluder=occluder))

    for m in models(G):
        add_model(m, "outdoor_bldg")
    # campus wall (mortared basalt, trachyte coping) and the gate's flanking walls
    for m in wall_run("围墙", ENCLOSURE, 1.8, 0.7, True):
        add_model(m, "outdoor_wall", occluder=False)
    # oath court low walls with openings on the axis
    oc = G.B["誓约庭"]
    (cu, cv), (su, sv) = oc["c"], oc["s"]
    for pts in ([(cu - su / 2, cv - sv / 2 + 1.5), (cu - su / 2, cv + sv / 2)], [(cu + su / 2, cv - sv / 2 + 1.5), (cu + su / 2, cv + sv / 2)],
                [(cu - su / 2, cv + sv / 2), (cu - 4.5, cv + sv / 2)], [(cu + 4.5, cv + sv / 2), (cu + su / 2, cv + sv / 2)]):
        for m in wall_run("庭墙", pts, 0.8, 0.5, True, seg=3.0):
            add_model(m, "outdoor_wall", occluder=False)
    # harbour road: dry-stone walls along both sides to the cliff top, parapet down the zig-zag
    r = G.road
    left_ = _offset_line(r[:5], 3.0)
    right_ = _offset_line(r[:5], -3.0)
    left_[0] = (r[0][0] - 3.0, r[0][1] - 3.6); right_[0] = (r[0][0] + 3.0, r[0][1] - 3.6)
    for nm, pts in (("港路墙北", left_), ("港路墙南", right_)):
        for m in wall_run(nm, pts, 1.1, 0.6, False, seg=2.2):
            add_model(m, "outdoor_wall", occluder=False)
    for i, (a, b) in enumerate(zip(r[4:7], r[5:8])):
        side = -2.9 if i != 1 else 2.9
        seg = _offset_line([a, b], side)
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        t0, t1 = 3.5 / L, 1 - 3.5 / L
        pa = (seg[0][0] + (seg[1][0] - seg[0][0]) * t0, seg[0][1] + (seg[1][1] - seg[0][1]) * t0)
        pb = (seg[0][0] + (seg[1][0] - seg[0][0]) * t1, seg[0][1] + (seg[1][1] - seg[0][1]) * t1)
        for m in wall_run(f"坡道矮墙{i}", [pa, pb], 0.8, 0.5, False, seg=2.2):
            add_model(m, "outdoor_wall", occluder=False)

    # walkable area
    walk = (G.inside & ~_grow(~G.inside, int(0.5 * K))) | corridor | landing
    gate = G.B["门楼"]
    walk |= _rect_mask(gate["c"][0], gate["c"][1] - 1.5, 6.0, 10.0)
    walk &= (G.land | landing)
    walk &= ~((surface == 9) & ~landing)

    # planting: shade trees on the lawns, windbreaks outside the wall, hydrangeas on the walls
    trees = []
    for i, (u, v) in enumerate(((-22, -93), (22, -96), (-24, -110), (25, -110), (-40, 22), (32, 24), (-58, 40), (46, 50),
                                (80, 40), (118, 30), (122, -12), (118, -45), (-96, -32), (-95, -78), (-62, -92), (-36, -100),
                                (40, -96), (70, -100), (-32, 52), (-110, 20), (-12, -40), (14, -40), (66, 8), (84, -50),
                                (-88, 48), (-70, 52), (20, 60), (60, 58), (100, 58), (126, 50), (36, -20), (48, 12),
                                (-46, -12), (-66, -24), (-88, -96), (-50, -100), (30, -112), (56, -108), (112, -60), (126, -30))):
        trees.append(("round", u, v, i))
    for i, u in enumerate(np.arange(-122, 200, 5.5)):
        trees.append(("conifer", float(u), 72.0 + (i % 2) * 2.2, 100 + i))
    for i, v in enumerate(np.arange(-150, 72, 5.5)):
        trees.append(("conifer", -114.0 - (i % 2) * 2.4, float(v), 200 + i))
    bushes = []
    for i, (u0_, v0_, u1_, v1_) in enumerate(((-100, 64, 134, 64), (-100, 62, -100, -108), (134, 62, 134, -104))):
        n = int(math.hypot(u1_ - u0_, v1_ - v0_) / 2.4)
        for k in range(n):
            if hash2(k, i, 81) < 0.45:
                continue
            bushes.append(("hydrangea", u0_ + (u1_ - u0_) * k / n, v0_ + (v1_ - v0_) * k / n, 300 + i * 500 + k))
    for name, b in G.B.items():                            # clumps at the front corners of buildings
        if b["kind"] == "outdoor" or b["shape"] != "rect" or name.startswith("生活翼·起居") or name in ("风雨廊", "门楼", "汲水泉"):
            continue
        (cu, cv), (su, sv) = b["c"], b["s"]
        for k, side in enumerate((-1, 1)):
            if hash2(int(cu), k, 83) < 0.35:
                continue
            for j in range(3):
                bushes.append(("hydrangea", cu + side * (su / 2 - 0.9 - j * 1.5), cv - sv / 2 - 0.9, 5000 + int(cu * 7) + k * 3 + j))
    for side, pts in ((1, _offset_line(r[:5], 4.4)), (-1, _offset_line(r[:5], -4.4))):
        for a, b in zip(pts[:-1], pts[1:]):
            L = math.hypot(b[0] - a[0], b[1] - a[1])
            for k in range(int(L / 2.0)):
                if hash2(k, int(a[0]), 82 + side) < 0.3:
                    continue
                bushes.append(("hydrangea", a[0] + (b[0] - a[0]) * k / int(L / 2.0), a[1] + (b[1] - a[1]) * k / int(L / 2.0), 2000 + len(bushes)))
    g = np.random.default_rng(91)
    for _ in range(140):                                 # faya scrub on the outer slopes
        u, v = g.uniform(U0, U1), g.uniform(V0, V1)
        x, y = px(u, v)
        if 0 <= int(x) < W and 0 <= int(y) < H and G.land[int(y), int(x)] and not keep_clear[int(y), int(x)]:
            bushes.append(("faya", u, v, 3000 + _))
    for _ in range(90):                                  # basalt boulders on the coast
        u, v = g.uniform(100, U1), g.uniform(V0, -20)
        x, y = px(u, v)
        if 0 <= int(x) < W and 0 <= int(y) < H and surface[int(y), int(x)] == 7 and not keep_clear[int(y), int(x)]:
            bushes.append(("boulder", u, v, 4000 + _))

    plant_cache = {}

    def plant(kind, u, v, seed):
        variant = seed % 4
        key = f"outdoor_{kind}_{variant}"
        if key not in plant_cache:
            if kind == "round":
                plant_cache[key] = NT.round_tree(variant + 1, 5.0 + variant * 0.4, 6.4 + variant * 0.5)
            elif kind == "conifer":
                plant_cache[key] = NT.conifer(variant + 5, 3.4 + variant * 0.2, 10.0 + variant * 0.8)
            elif kind == "hydrangea":
                plant_cache[key] = NT.bush(variant + 9, 1.8, 1.1, variant % 3)
            elif kind == "faya":
                plant_cache[key] = NT.bush(variant + 13, 2.6, 1.7)
            else:
                plant_cache[key] = NT.boulder(variant + 17, 1.1 + variant * 0.25)
        can = plant_cache[key]
        x, y = px(u, v)
        x, y = int(round(x)), int(round(y))
        rect_walk = walk[max(0, y - can.h):min(H, y + 4), max(0, x - can.w // 2):min(W, x + can.w // 2)]
        trunk = {"round": 0.5, "conifer": 0.6, "hydrangea": 1.4, "faya": 2.0, "boulder": 1.0}[kind]
        hgt = {"round": 6.0, "conifer": 10.0, "hydrangea": 1.0, "faya": 1.5, "boulder": 0.8}[kind]
        rad = {"round": 2.4, "conifer": 1.6, "hydrangea": 0.8, "faya": 1.2, "boulder": 0.5}[kind]
        sx, sy = px(u + SHADOW[0] * hgt * 0.6, v + SHADOW[1] * hgt * 0.6)
        NT.stamp_shadow(rgb, sx, sy, rad * K * 1.1, rad * K * 0.75)
        if not rect_walk.any():                          # never overlaps where anyone walks: bake into the ground
            _bake(rgb, can, x - can.w // 2, y - can.h)
            return
        fp = [int(x - trunk * K / 2), int(y - trunk * K * 0.4), int(trunk * K), int(max(6, trunk * K * 0.4))]
        objects.append(dict(name=f"{kind}{seed}", key=key, x=x, y=y, solids=[fp], occluder=kind in ("round", "conifer")))
        sprites[key] = can

    # shadows first (under everything), then bake/plant
    sh = np.asarray(shadow) > 0
    rgb[sh] = (rgb[sh] * np.array([0.58, 0.6, 0.68])).astype(np.int16)
    for kind, u, v, seed in sorted(trees + bushes, key=lambda t: t[2], reverse=True):
        x, y = px(u, v)
        if not (0 <= x < W and 0 <= y < H) or not G.land[int(y), int(x)]:
            continue
        if any(_near_building(G, u, v, 3.0 if kind in ("round", "conifer") else 0.8)):
            continue
        if kind in ("round", "conifer") and _on_path(surface, x, y, 2.8):
            continue
        if kind in ("hydrangea", "faya") and _on_path(surface, x, y, 1.0):
            continue
        plant(kind, u, v, seed)

    # door steps, exits and spawns
    exits, spawns = [], {}
    for name, u, v, (du, dv), target, spawn, own in DOORS:
        x, y = px(u, v)
        _step(rgb, x, y, du, dv)
        tx, ty = px(u + du * 0.4, v + dv * 0.4)
        exits.append({"name": name, "rect_px": [int(tx - 18), int(ty - 18), 36, 36], "target": target, "spawn": spawn})
        sx, sy = px(u + du * 2.6, v + dv * 2.6)
        spawns[own] = [int(sx), int(sy + (8 if dv == 0 else 0))]
    gx, gy = px(gate["c"][0], gate["c"][1] + 6)
    spawns["gate_inside"] = [int(gx), int(gy)]
    gx, gy = px(gate["c"][0], gate["c"][1] - 7)
    spawns["gate_outside"] = [int(gx), int(gy)]
    qx, qy = px(158, -96)
    spawns["harbor_landing"] = [int(qx), int(qy)]

    # small props
    def put(name, sprite, u, v):
        x, y = px(u, v)
        fp = prop_fps.get(sprite)
        objects.append(dict(name=name, key=sprite, x=int(x), y=int(y), occluder=False,
                            solids=[] if fp is None else [[int(x - fp[0] / 2), int(y - fp[1]), fp[0], fp[1]]]))

    for i, v in enumerate(range(-80, -20, 12)):
        put(f"LampW{i}", "lamp_post", -4.8, v)
        put(f"LampE{i}", "lamp_post", 4.8, v)
    for i, (u, v) in enumerate(((-8, -95), (8, -95), (-8, -107), (8, -107))):
        put(f"OathBench{i}", "stone_bench", u, v)
    put("DryingA", "drying_lines", 70, -62)
    put("DryingB", "drying_lines", 74, -67)
    put("DryingC", "drying_lines", 70, -70)
    put("Spring", "spring_trough", 96, -28.6)
    put("Buckets", "bucket_pair", 98.6, -30.6)
    LW = G.LW
    for tag, u in (("W", LW["u0"] - 0.4), ("E", LW["u0"] + 2 * LW["ZL"] + LW["HALL"] + 0.4)):
        put(f"LaneGate{tag}", "gate_leaf", u, LW["v0"] - LW["ZD"] - LW["LANE"] / 2 - 0.2)
    for i, (u, v) in enumerate(((-30, -8), (30, 8), (-60, -10), (60, -22), (-30, -44))):
        put(f"Bench{i}", "stone_bench", u, v)
    # the contract stele at the landing; bollards on the quay
    st = stele()
    sprites["outdoor_stele"] = st
    x, y = px(158.0, -90.5)
    objects.append(dict(name="契约石碑", key="outdoor_stele", x=int(x), y=int(y), occluder=False,
                        solids=[[int(x - 0.6 * K), int(y - 0.5 * K), int(1.2 * K), int(0.5 * K)]]))
    bol = bollard()
    sprites["outdoor_bollard"] = bol
    for i, t in enumerate((0.3, 0.6, 0.9)):
        a, b = G.quay
        u, v = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t + 2.4
        x, y = px(u, v)
        objects.append(dict(name=f"Bollard{i}", key="outdoor_bollard", x=int(x), y=int(y), occluder=False,
                            solids=[[int(x - 7), int(y - 7), 14, 7]]))

    # rockfall: rubble across the zig-zag; the road beyond is cut
    cu, cv = G.collapse
    x, y = px(cu, cv)
    g = np.random.default_rng(77)
    for _ in range(260):
        rx, ry = x + float(g.normal(0, 46)), y + float(g.normal(0, 30))
        rr = float(g.uniform(3, 10))
        _blob(rgb, rx, ry + 2, rr + 2, rr * 0.7 + 2, (14, 15, 17))
        _blob(rgb, rx, ry, rr, rr * 0.7, tuple(int(c) for c in ROCK[int(g.integers(1, 6))]))
    for i in range(7):
        rk = f"outdoor_boulder_{i % 4}"
        if rk not in plant_cache:
            plant_cache[rk] = NT.boulder(i % 4 + 17, 1.1 + (i % 4) * 0.25)
        sprites[rk] = plant_cache[rk]
        bx, by = x + float(g.normal(0, 40)), y + float(g.normal(0, 22))
        objects.append(dict(name=f"Rockfall{i}", key=rk, x=int(bx), y=int(by), occluder=False, solids=[]))
    barrier = [int(x - 3.8 * K), int(y - 3.8 * K), int(7.6 * K), int(7.6 * K)]

    # solids: everything not walkable (16 px cells), building and prop footprints, the rockfall
    solids = _rects(~walk, 16)
    solids.append(barrier)
    for o in objects:
        solids += o.get("solids", [])

    sites = [{"id": "lane", "point": list(map(int, px(-47.8, -56.7))), "label": "夹道"},
             {"id": "collapse", "point": [int(x), int(y)], "label": "港路塌方处"},
             {"id": "oath_court", "point": list(map(int, px(0, -100))), "label": "誓约庭"},
             {"id": "spring", "point": list(map(int, px(96, -31))), "label": "汲水泉"},
             {"id": "drying_yard", "point": list(map(int, px(72, -66))), "label": "晾晒场"},
             {"id": "gate", "point": list(map(int, px(0, -118.5))), "label": "门楼"},
             {"id": "stele", "point": list(map(int, px(158.0, -92))), "label": "契约石碑"},
             {"id": "harbor", "point": list(map(int, px(175, -97))), "label": "码头"}]
    ground = Canvas(W, H)
    ground.a[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    ground.a[..., 3] = 255
    rooms, spawn_region = _regions(ground, objects, sprites, solids, exits, spawns, sites, prop_sizes)
    return rooms, spawn_region, sprites


def _near_building(G, u, v, pad):
    for b in G.S.B:
        if b["shape"] == "circ":
            yield math.hypot(u - b["c"][0], v - b["c"][1]) < b["r"] + pad + 5
        else:
            (cu, cv), (su, sv) = b["c"], b["s"]
            yield abs(u - cu) < su / 2 + pad and (cv - sv / 2 - pad) < v < (cv + sv / 2 + pad + 4)


def _on_path(surface, x, y, r_m):
    r = int(r_m * K)
    sub = surface[max(0, int(y) - r):int(y) + r, max(0, int(x) - r):int(x) + r]
    return bool(((sub >= 2) & (sub <= 6)).any())


def _bake(rgb, can: Canvas, x0, y0):
    h, w = can.h, can.w
    xa, ya = max(0, x0), max(0, y0)
    xb, yb = min(W, x0 + w), min(H, y0 + h)
    if xb <= xa or yb <= ya:
        return
    src = can.a[ya - y0:yb - y0, xa - x0:xb - x0]
    m = src[..., 3] > 0
    rgb[ya:yb, xa:xb][m] = src[..., :3][m]


def _blob(rgb, x, y, rx, ry, col):
    x0, x1 = int(max(0, x - rx)), int(min(W, x + rx + 1))
    y0, y1 = int(max(0, y - ry)), int(min(H, y + ry + 1))
    if x1 <= x0 or y1 <= y0:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1] + 0.5
    m = ((xx - x) / rx) ** 2 + ((yy - y) / ry) ** 2 < 1
    rgb[y0:y1, x0:x1][m] = col


def _step(rgb, x, y, du, dv):
    """A trachyte door step on the ground, in front of the door."""
    if dv != 0:
        x0, y0, w, h = int(x - 26), int(y if dv < 0 else y - 14), 52, 14
    else:
        x0, y0, w, h = int(x if du > 0 else x - 14), int(y - 26), 14, 52
    rgb[y0:y0 + h, x0:x0 + w] = TRACH[5]
    rgb[y0 + h - 3:y0 + h, x0:x0 + w] = TRACH[2]
    rgb[y0, x0:x0 + w] = TRACH[6]


def stele() -> Canvas:
    m = BB.Building("stele")
    def mat(face, P, s, t):
        Lt = BB.lit(face)
        col = BB.pick(BB.TRACHYTE, 4.0 + Lt + (hash2(np.floor(s * K / 2), np.floor(t * K / 2), 5) - 0.5) * 1.2)
        if face.normal[2] < 0.5:
            line = (t > 0.6) & (t < 2.0) & (np.floor(t * K) % 4 == 0) & (s > 0.12) & (s < 1.0) & (hash2(np.floor(s * K / 3), np.floor(t * K / 4), 6) > 0.25)
            worn = (t < 1.0) & (hash2(np.floor(s * K / 5), np.floor(t * K / 5), 7) > 0.5)        # rules 9-10: weathered
            col = np.where((line & ~worn)[:, None], BB.pick(BB.TRACHYTE, 1.5 + 0 * s), col)
        return np.clip(col, 0, 255).astype(np.uint8), np.ones(len(s), bool)
    BB.box(m, -0.75, 0.75, -0.3, 0.3, 0, 0.3, "basalt", plinth=0, quoins=False)
    m.faces.append(quad((-0.75, -0.3, 0.3), (0.75, -0.3, 0.3), (0.75, 0.3, 0.3), (-0.75, 0.3, 0.3), BB.roof_mat("lead")))
    m.faces.append(quad((-0.56, -0.12, 0.3), (0.56, -0.12, 0.3), (0.56, -0.12, 2.3), (-0.56, -0.12, 2.3), mat))
    m.faces.append(quad((-0.56, -0.12, 2.3), (0.56, -0.12, 2.3), (0.56, 0.12, 2.3), (-0.56, 0.12, 2.3), mat))
    can, left, bottom = render(Frame(-2, 2, K), m.faces, -0.3)
    return can


def bollard() -> Canvas:
    m = BB.Building("bollard")
    BB.cylinder(m, (0, 0), 0.22, 0, 0.6, "basalt", plinth=0, n=16, solid=False)
    BB.dome(m, (0, 0), 0.3, 0.6, 0.12, "lead", n=12, rings=3)
    can, left, bottom = render(Frame(-2, 2, K), m.faces, -0.3)
    return can


def _rects(mask, cell):
    """Merge a pixel mask into rectangles on a coarse grid (cell px), greedy rows then columns."""
    gh, gw = H // cell + 1, W // cell + 1
    padded = np.zeros((gh * cell, gw * cell), bool)
    padded[:H, :W] = mask
    blk = padded.reshape(gh, cell, gw, cell).mean(axis=(1, 3)) > 0.5
    rects = []
    open_runs = {}
    for y in range(gh):
        row = blk[y]
        runs = []
        x = 0
        while x < gw:
            if not row[x]:
                x += 1
                continue
            s = x
            while x < gw and row[x]:
                x += 1
            runs.append((s, x))
        new_open = {}
        for r in runs:
            if r in open_runs:
                new_open[r] = open_runs[r]
            else:
                new_open[r] = y
        for r, y0 in open_runs.items():
            if r not in new_open:
                rects.append([r[0] * cell, y0 * cell, (r[1] - r[0]) * cell, (y - y0) * cell])
        open_runs = new_open
    for r, y0 in open_runs.items():
        rects.append([r[0] * cell, y0 * cell, (r[1] - r[0]) * cell, (gh - y0) * cell])
    return rects


def _regions(ground, objects, sprites, solids, exits, spawns, sites, prop_sizes):
    sizes = dict(prop_sizes)
    for k, c in sprites.items():
        sizes[k] = (c.w, c.h)
    cores = {}
    origins = {}
    for key, title, (a0, a1, b0, b1), (c0, c1, d0, d1) in REGIONS:
        x0, y0 = px(a0, b1)
        origins[key] = (int(round(x0)), int(round(y0)))
        cx0, cy0 = px(c0, d1); cx1, cy1 = px(c1, d0)
        cores[key] = (int(round(cx0)), int(round(cy0)), int(round(cx1 - cx0)), int(round(cy1 - cy0)))
    rooms = []
    spawn_region = {}
    for name, (x, y) in spawns.items():
        for key, *_ in REGIONS:
            c = cores[key]
            if c[0] <= x < c[0] + c[2] and c[1] <= y < c[1] + c[3]:
                spawn_region[name] = key
    for key, title, (a0, a1, b0, b1), core in REGIONS:
        ox, oy = origins[key]
        x1, y1 = px(a1, b0)
        rw, rh = int(round(x1)) - ox, int(round(y1)) - oy
        arch = Canvas(rw, rh)
        arch.a[:] = ground.a[oy:oy + rh, ox:ox + rw]
        furn = []
        names = set()
        for o in objects:
            w, h = sizes[o["key"]]
            l, t = o["x"] - w // 2, o["y"] - h
            if l + w < ox or l > ox + rw or t + h < oy or t > oy + rh:
                continue
            nm = o["name"]
            while nm in names:
                nm += "_"
            names.add(nm)
            furn.append({"name": nm, "sprite": o["key"], "position": [o["x"] - ox, o["y"] - oy], "footprint": None,
                         "occluder": o.get("occluder", False)})
        sol = []
        for x, y, w, h in solids:
            xa, ya, xb, yb = max(x, ox), max(y, oy), min(x + w, ox + rw), min(y + h, oy + rh)
            if xb > xa and yb > ya:
                sol.append([xa - ox, ya - oy, xb - xa, yb - ya])
        ex = []
        for e in exits:
            x, y, w, h = e["rect_px"]
            if ox <= x and x + w <= ox + rw and oy <= y and y + h <= oy + rh:
                ex.append(dict(e, rect_px=[x - ox, y - oy, w, h]))
        sp = {n: [x - ox, y - oy] for n, (x, y) in spawns.items() if ox <= x < ox + rw and oy <= y < oy + rh}
        st = [dict(s, point=[s["point"][0] - ox, s["point"][1] - oy]) for s in sites
              if ox <= s["point"][0] < ox + rw and oy <= s["point"][1] < oy + rh]
        meta = {"outdoor_origin": f"Vector2({ox}, {oy})",
                "outdoor_core": "Rect2(%d, %d, %d, %d)" % cores[key],
                "outdoor_cores": "{" + ", ".join('"%s": Rect2(%d, %d, %d, %d)' % ((k,) + cores[k]) for k in cores) + "}",
                "outdoor_origins": "{" + ", ".join('"%s": Vector2(%d, %d)' % ((k,) + origins[k]) for k in origins) + "}"}
        stand = []
        if key == "campus_s":                              # 誓约庭：众人站立处
            for v in (-94.0, -98.0, -102.0, -106.0):
                for u in (-9.0, -6.0, -3.0, 3.0, 6.0, 9.0):
                    x, y = px(u, v)
                    stand.append([int(x) - ox, int(y) - oy])
        rooms.append(dict(key=key, title=title, rows=[], size_px=[rw, rh], architecture=arch, furniture=furn,
                          solids_px=sol, exits=ex, spawns=sp, sites=st, meta=meta, stands=stand))
    return rooms, spawn_region
