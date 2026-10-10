"""生成标题页的桌面、灯光与册子封皮（设计规范第六部分 §4.7 标题页）。

整张桌面是一次渲染，而不是逐件手绘：桌面、油灯、墨水瓶、蘸水笔、放大镜与册子都按毫米建模，
用同一台正交相机、同一个点光源算出来，所以投影、明暗与投影方向全图一致。

相机：近俯视，往下 60°，正交。桌面坐标 (X, Y, Z) 以原生像素计（未缩短），画面坐标
      x = X、y = Y·ELEV_Q − Z·ELEV_K。册子在 Godot 里按同一缩短（Plane 纵向 0.87）显示。
比例：册子 A5（148 mm 宽）在原生画面上 290 px，约 1.96 px/mm。
光源：火焰为点光源，照度 = cos θ / d²（以灯脚正下方为 1）；每个可见点向火焰发一条光线判断遮挡，
      半影宽度按火焰大小与遮挡体、受光面的距离比算。灯光取低饱和暖白，不用琥珀（恶魔专属）。
着色：每种材质几级色阶；明暗、玻璃的透光与反光都按 4×4 有序抖色量化，不出现平滑渐变。

参照的历史实物（尺寸取自馆藏记录）：
- 油灯：约 1900 年德国带 Kosmos 灯头的煤油灯（玻璃油壶、Kosmos 灯头、玻璃灯罩），高 33.5 cm、直径 12.5 cm
  （westfalen.museum-digital.de）。灯罩取 Kosmos 式：下部鼓、火焰上方收腰、上接直筒。
- 墨水瓶：美国 1820–1845 年模吹铅玻璃墨水台，高 4.9 cm、直径 5.575 cm（Winterthur Museum）。
- 蘸水笔：19 世纪木杆蘸水笔，连笔尖全长 19 cm（馆藏木杆 18.4–19 cm）。
- 放大镜：侦探的黄铜圆框、短柄放大镜，镜片约 5 cm（与证物卡的描述一致）。

原生 640×496，按最近邻放大 2 倍到 1280×992；随机种子固定，可重建。

    desk.png        整块木桌面与桌上物件（册子不在图里，由 Godot 画；册子的投影已烘在桌面上）
    glow.png        灯焰光晕（RGBA，叠加混合；标题页让它轻微闪动）
    book_light.png  册子表面的乘色光照（屏幕坐标，标题页的册子着色器按屏幕 UV 取样）
    cloth.png       册子布面封皮（128 原生像素可平铺，放大 2 倍）

册子的位置、尺寸、转角、厚度与纵深缩短由 tools/build_ui_theme.py 从本文件读出写进 TitleTheme，
标题页脚本按这些常量摆放册子，与烘焙的投影对齐。

    python godot_csharp/tools/build_ui_desk.py
"""
from __future__ import annotations

import math
import random
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[1] / "ShareAsset" / "Internal" / "Props" / "UIDesk"
W, H, S = 640, 496, 2
ELEV_K = 0.5                # 竖直高度在画面上的缩短（cos 60°）
ELEV_Q = 0.87               # 桌面纵深在画面上的缩短（sin 60°）
MM = 1.96                   # 原生像素 / 毫米
VIEW = np.array([0.0, ELEV_K, ELEV_Q]) / math.hypot(ELEV_K, ELEV_Q)
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=float)
THRESH = (BAYER + 0.5) / 16.0

# ---- 册子（屏幕像素 1280×992；与 Godot 的摆放同一套数，经 TitleTheme 传过去） ----
BOOK_CENTER = (842, 468)    # 封面中心
BOOK_SIZE = (580, 820)      # 封面尺寸 = 148 × 210 mm
BOOK_ANGLE = 4.0            # 桌面上的转角（度，顺时针）
BOOK_THICK_MM = 4.0         # 册子厚度

# ---- 物件位置（原生画面像素，取桌面高度处） ----
LAMP_FOOT = (20, 272)
INK_FOOT = (104, 440)
PEN_START = (150, 494)
PEN_ANGLE = -30.0
MAG_CENTER = (200, 330)
MAG_HANDLE_ANGLE = -35.0

FLAME_HEIGHT_MM = 209.0     # Kosmos 圆芯火焰中心离桌面高度
FLAME_RADIUS_MM = 6.0       # 半影用的火焰大小

DESK_TINTS = np.array([(34, 28, 34), (56, 46, 48), (88, 72, 64), (126, 104, 86), (168, 142, 110), (208, 180, 138), (240, 220, 180)], dtype=float)
BOOK_TINTS = [(84, 72, 66), (118, 100, 86), (152, 130, 108), (186, 160, 128), (216, 192, 152), (240, 220, 182), (255, 242, 212)]
WOOD_TONES = np.array([(96, 60, 38), (112, 72, 46), (126, 84, 54)], dtype=float)
RAMPS = {
    "brass": [(40, 28, 16), (74, 52, 28), (116, 86, 44), (162, 124, 64), (204, 168, 96), (236, 212, 150), (255, 244, 212)],
    "ink": [(8, 9, 14), (16, 19, 30), (28, 33, 50), (44, 52, 78), (70, 82, 112), (120, 132, 160)],
    "wood_dark": [(22, 13, 8), (40, 24, 15), (62, 40, 25), (90, 60, 37), (122, 86, 55), (164, 124, 82)],
    "horn": [(18, 14, 12), (34, 27, 22), (56, 45, 36), (84, 68, 54), (118, 98, 78)],
    "iron": [(14, 13, 14), (28, 26, 27), (46, 43, 42), (70, 66, 62), (100, 95, 88), (140, 134, 124)],
    "soot": [(18, 15, 12), (34, 29, 23), (56, 48, 38), (84, 72, 56), (118, 102, 80)],
    "steel": [(30, 30, 34), (56, 56, 62), (92, 92, 100), (140, 140, 150), (200, 200, 210)],
}


def mm(v: float) -> float:
    return v * MM


def to_desk(x: float, y: float) -> tuple[float, float]:
    return x, y / ELEV_Q


def to_screen(X: float, Y: float, Z: float = 0.0) -> tuple[float, float]:
    return X, Y * ELEV_Q - Z * ELEV_K


LAMP = to_desk(*LAMP_FOOT)
FLAME3 = np.array([LAMP[0], LAMP[1], mm(FLAME_HEIGHT_MM)])
FLAME = tuple(round(v) for v in to_screen(*FLAME3))
BOOK_H = mm(BOOK_THICK_MM)
# 封面中心（屏幕，原生像素）在册子顶面高度处 → 桌面坐标
BOOK_DESK = (BOOK_CENTER[0] / S, (BOOK_CENTER[1] / S + BOOK_H * ELEV_K) / ELEV_Q)
BOOK_DESK_SIZE = (BOOK_SIZE[0] / S, BOOK_SIZE[1] / S)

YS, XS = np.mgrid[0:H, 0:W]
XC = XS + 0.5
Y0 = (YS + 0.5) / ELEV_Q
TH = THRESH[YS % 4, XS % 4]


def quantize(v: np.ndarray, n: int) -> np.ndarray:
    """0..1 → 0..n-1 级，级间按有序抖色取整。"""
    f = np.clip(v, 0.0, 1.0) * (n - 1)
    lo = np.floor(f)
    return np.minimum(n - 1, lo + (f - lo > TH)).astype(int)


def brightness(e: np.ndarray) -> np.ndarray:
    """照度到感知亮度：压缩动态范围，离灯远处不至于全黑。"""
    return np.clip(np.maximum(e, 0.0) ** 0.36, 0.0, 1.0)


# ================================================================ 桌面 ===

def value_noise(seed: int, cell: float):
    rng = np.random.default_rng(seed)
    table = rng.random((256, 256))

    def sample(x: np.ndarray, y: np.ndarray) -> np.ndarray:
        cx, cy = x / cell, y / cell
        ix, iy = np.floor(cx).astype(int), np.floor(cy).astype(int)
        tx, ty = cx - ix, cy - iy
        sx, sy = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        t = lambda a, b: table[a % 256, b % 256]
        a = t(ix, iy) * (1 - sx) + t(ix + 1, iy) * sx
        b = t(ix, iy + 1) * (1 - sx) + t(ix + 1, iy + 1) * sx
        return a * (1 - sy) + b * sy
    return sample


def wood_albedo() -> np.ndarray:
    """整块木板的纹理，在桌面坐标里生成（所以随桌面一起纵向缩短）：纹线近乎平行、贯穿全宽、间距不匀，
    右下一个节疤。三档颜色，档间有序抖色。"""
    X, Y = XC, Y0
    drift, spacing, fibre = value_noise(7, 260.0), value_noise(11, 60.0), value_noise(19, 3.0)
    kx, ky = (X - 520) / 2.2, Y - 470
    kd = np.hypot(kx, ky)
    bend = 18.0 * np.exp(-kd / 20.0) * np.where(ky >= 0, 1, -1)
    yy = Y + 30.0 * drift(X, Y * 0.6) + bend
    phase = yy * 0.22 + 6.0 * spacing(X * 0.4, yy)
    line = np.abs(np.sin(phase)) ** 6
    band = spacing(X * 0.25, yy * 1.5)
    g = 0.55 * line + 0.3 * band + 0.25 * fibre(X * 0.12, Y)
    g = np.where(kd < 9, 0.25 + 0.6 * (np.sin(kd * 1.1) * 0.5 + 0.5), g)
    return WOOD_TONES[quantize(1.0 - g, 3)]


# ================================================================ 建模 ===

class Revolution:
    """绕竖直轴的回转体：profile(z_mm) → 半径 mm（0 为空）。kind：opaque / glass / flame。"""
    def __init__(self, name, axis, profile, z0_mm, z1_mm, material, kind="opaque", gloss=0.5):
        self.name, self.axis, self.profile = name, axis, profile
        self.z0, self.z1 = mm(z0_mm), mm(z1_mm)
        self.material, self.kind, self.gloss = material, kind, gloss

    def r(self, z: float) -> float:
        return mm(self.profile(z / MM))

    def trace(self):
        """自上而下逐层找每条视线最先进入回转体的高度；返回（切片、掩码、z、法线 x、y、z），只在包围盒里算。"""
        cx, cy = self.axis
        rmax = max(self.r(z) for z in np.arange(self.z0, self.z1 + 0.5, 0.5))
        x0, x1 = max(0, int(cx - rmax) - 1), min(W, int(cx + rmax) + 2)
        y0 = max(0, int((cy - rmax) * ELEV_Q - self.z1 * ELEV_K) - 1)
        y1 = min(H, int((cy + rmax) * ELEV_Q - self.z0 * ELEV_K) + 2)
        if x1 <= x0 or y1 <= y0:
            return None
        sl = (slice(y0, y1), slice(x0, x1))
        dx = XC[sl] - cx
        yb = Y0[sl]
        hit = np.zeros(dx.shape, bool)
        zhit = np.zeros(dx.shape)
        nx, ny, nz = np.zeros(dx.shape), np.zeros(dx.shape), np.zeros(dx.shape)
        dz = 0.5
        prev_r = 0.0
        z = self.z1
        while z >= self.z0:
            r = self.r(z)
            if r > 0:
                dy = yb + z * ELEV_K / ELEV_Q - cy
                rho = np.hypot(dx, dy)
                new = (rho <= r) & ~hit
                if new.any():
                    zhit[new] = z
                    # 只有真正的台阶（相邻两层半径突增）或最顶一层才是朝上的平面；其余按轮廓斜率求法线
                    top = new & ((r - prev_r > 2.0) | (z >= self.z1 - 1e-6))
                    side = new & ~top
                    nz[top] = 1.0
                    if side.any():
                        slope = (self.r(min(self.z1, z + dz)) - self.r(max(self.z0, z - dz))) / (2 * dz)
                        rr = np.maximum(rho[side], 1e-6)
                        vx, vy, vz = dx[side] / rr, dy[side] / rr, np.full(rr.shape, -slope)
                        nn = np.sqrt(vx * vx + vy * vy + vz * vz)
                        nx[side], ny[side], nz[side] = vx / nn, vy / nn, vz / nn
                    hit |= new
            prev_r = r
            z -= dz
        return sl, hit, zhit, nx, ny, nz


class Capsule:
    """平放在桌上的圆杆：桌面线段 a—b，离桌高 = 半径。"""
    def __init__(self, name, a, b, radius_mm, material, gloss=0.5):
        self.name, self.material, self.gloss = name, material, gloss
        self.r = mm(radius_mm)
        self.a = np.array([a[0], a[1], self.r])
        self.b = np.array([b[0], b[1], self.r])

    def trace(self):
        d = self.b - self.a
        length = np.linalg.norm(d)
        d = d / length
        pts = [to_screen(*self.a), to_screen(*self.b)]
        x0 = max(0, int(min(p[0] for p in pts) - self.r) - 2)
        x1 = min(W, int(max(p[0] for p in pts) + self.r) + 3)
        y0 = max(0, int(min(p[1] for p in pts) - self.r * 2) - 2)
        y1 = min(H, int(max(p[1] for p in pts) + self.r * 2) + 3)
        if x1 <= x0 or y1 <= y0:
            return None
        sl = (slice(y0, y1), slice(x0, x1))
        P0 = np.stack([XC[sl], Y0[sl], np.zeros_like(XC[sl])], -1)
        V = np.array([0.0, ELEV_K, ELEV_Q])
        Wv = P0 - self.a
        Vp = V - V.dot(d) * d
        Wp = Wv - (Wv @ d)[..., None] * d
        A = Vp.dot(Vp)
        B = 2 * (Wp @ Vp)
        C = (Wp * Wp).sum(-1) - self.r ** 2
        disc = B * B - 4 * A * C
        ok = disc >= 0
        s = np.where(ok, (-B + np.sqrt(np.maximum(disc, 0))) / (2 * A), 0.0)
        Pt = P0 + s[..., None] * V
        u = (Pt - self.a) @ d
        hit = ok & (u >= 0) & (u <= length) & (s > 0)
        axis_pt = self.a + u[..., None] * d
        n = (Pt - axis_pt) / self.r
        return sl, hit, Pt[..., 2], n[..., 0], n[..., 1], n[..., 2]


class Torus:
    """平放的圆框（放大镜）：中心 c、环半径 R、管半径 rt。"""
    def __init__(self, name, c, R_mm, rt_mm, material, gloss=0.9):
        self.name, self.c, self.R, self.rt = name, c, mm(R_mm), mm(rt_mm)
        self.material, self.gloss = material, gloss

    def trace(self):
        cx, cy = self.c
        ext = self.R + self.rt
        zc = self.rt
        x0, x1 = max(0, int(cx - ext) - 1), min(W, int(cx + ext) + 2)
        y0, y1 = max(0, int((cy - ext) * ELEV_Q - 2 * zc * ELEV_K) - 1), min(H, int((cy + ext) * ELEV_Q) + 2)
        sl = (slice(y0, y1), slice(x0, x1))
        dx = XC[sl] - cx
        yb = Y0[sl]
        hit = np.zeros(dx.shape, bool)
        zhit, nx, ny, nz = np.zeros(dx.shape), np.zeros(dx.shape), np.zeros(dx.shape), np.zeros(dx.shape)
        z = 2 * zc
        while z >= 0:
            dy = yb + z * ELEV_K / ELEV_Q - cy
            rho = np.hypot(dx, dy)
            q = (rho - self.R) ** 2 + (z - zc) ** 2
            new = (q <= self.rt ** 2) & ~hit
            if new.any():
                rr = np.maximum(rho[new], 1e-6)
                k = (rho[new] - self.R) / self.rt
                nx[new], ny[new], nz[new] = k * dx[new] / rr, k * dy[new] / rr, (z - zc) / self.rt
                zhit[new] = z
                hit |= new
            z -= 0.25
        return sl, hit, zhit, nx, ny, nz


# ---- 遮挡体（投影用，向量化） ----

def soft_edge(edge: np.ndarray, t: np.ndarray) -> np.ndarray:
    """edge > 0 在遮挡体内；半影宽度按火焰大小 × 遮挡体到受光面 / 火焰到遮挡体。"""
    width = np.maximum(1.0, mm(FLAME_RADIUS_MM) * t / np.maximum(1e-3, 1 - t))
    return np.clip(0.5 + edge / (2 * width), 0.0, 1.0)


class OcCylinder:
    def __init__(self, part, c, r_mm, z0_mm, z1_mm, opacity=1.0):
        self.part, self.c, self.r, self.z0, self.z1, self.opacity = part, c, mm(r_mm), mm(z0_mm), mm(z1_mm), opacity

    def block(self, P):
        dz = FLAME3[2] - P[2]
        safe = np.maximum(dz, 1e-6)
        t0 = np.clip((self.z0 - P[2]) / safe, 0, 1)
        t1 = np.clip((self.z1 - P[2]) / safe, 0, 1)
        dx, dy = FLAME3[0] - P[0], FLAME3[1] - P[1]
        ox, oy = P[0] - self.c[0], P[1] - self.c[1]
        dd = np.maximum(dx * dx + dy * dy, 1e-6)
        t = np.clip(-(ox * dx + oy * dy) / dd, t0, t1)
        dist = np.hypot(ox + t * dx, oy + t * dy)
        return soft_edge(self.r - dist, t) * (t1 > t0) * (dz > 0) * self.opacity


class OcEllipsoid:
    def __init__(self, part, c, rx_mm, rz_mm, opacity=1.0):
        self.part, self.c, self.rx, self.rz, self.opacity = part, np.array(c), mm(rx_mm), mm(rz_mm), opacity

    def block(self, P):
        sc = (1.0, 1.0, self.rx / self.rz)
        a = [(P[i] - self.c[i]) * sc[i] for i in range(3)]
        b = [(FLAME3[i] - self.c[i]) * sc[i] for i in range(3)]
        ab = [b[i] - a[i] for i in range(3)]
        denom = np.maximum(ab[0] ** 2 + ab[1] ** 2 + ab[2] ** 2, 1e-6)
        t = np.clip(-(a[0] * ab[0] + a[1] * ab[1] + a[2] * ab[2]) / denom, 0, 1)
        dist = np.sqrt(sum((a[i] + t * ab[i]) ** 2 for i in range(3)))
        return soft_edge(self.rx - dist, t) * self.opacity


class OcCapsule:
    def __init__(self, part, a, b, r_mm):
        self.part, self.a, self.b, self.r = part, a, b, mm(r_mm)

    def block(self, P):
        dz = FLAME3[2] - P[2]
        t = (self.r - P[2]) / np.maximum(dz, 1e-6)
        qx, qy = P[0] + t * (FLAME3[0] - P[0]), P[1] + t * (FLAME3[1] - P[1])
        ax, ay = self.a
        bx, by = self.b
        vx, vy = bx - ax, by - ay
        s = np.clip(((qx - ax) * vx + (qy - ay) * vy) / (vx * vx + vy * vy), 0, 1)
        dist = np.hypot(qx - ax - s * vx, qy - ay - s * vy)
        return soft_edge(self.r - dist, t) * ((t > 0) & (t < 1))


class OcRing:
    def __init__(self, part, c, r_in_mm, r_out_mm, z_mm):
        self.part, self.c, self.r_in, self.r_out, self.z = part, c, mm(r_in_mm), mm(r_out_mm), mm(z_mm)

    def block(self, P):
        dz = FLAME3[2] - P[2]
        t = (self.z - P[2]) / np.maximum(dz, 1e-6)
        d = np.hypot(P[0] + t * (FLAME3[0] - P[0]) - self.c[0], P[1] + t * (FLAME3[1] - P[1]) - self.c[1])
        return np.minimum(soft_edge(self.r_out - d, t), soft_edge(d - self.r_in, t)) * ((t > 0) & (t < 1))


class OcBox:
    """册子：桌面上转过 angle 的长方体，高 h。"""
    def __init__(self, part, c, size, angle_deg, h):
        self.part, self.c, self.hw, self.hh, self.h = part, c, size[0] / 2, size[1] / 2, h
        a = math.radians(angle_deg)
        self.ca, self.sa = math.cos(a), math.sin(a)

    def block(self, P):
        dz = FLAME3[2] - P[2]
        t = (self.h - P[2]) / np.maximum(dz, 1e-6)
        qx, qy = P[0] + t * (FLAME3[0] - P[0]) - self.c[0], P[1] + t * (FLAME3[1] - P[1]) - self.c[1]
        lx, ly = qx * self.ca + qy * self.sa, -qx * self.sa + qy * self.ca
        edge = -np.maximum(np.abs(lx) - self.hw, np.abs(ly) - self.hh)
        return soft_edge(edge, t) * ((t > 0) & (t < 1))


# ================================================================ 场景 ===

def kosmos_foot(z: float) -> float:
    """Kosmos 煤油灯的铸铁座与灯柱（毫米）：圆座、锥台、灯柱与中间一道小节。"""
    if z < 5: return 62.5
    if z < 18: return 62.5 - (z - 5) * 1.6
    if z < 80: return 15.0 if 46 <= z <= 54 else 10.0
    return 0.0


def kosmos_collar(z: float) -> float:
    """油壶下的黄铜托与油壶上的黄铜灯头座（毫米）。"""
    if 80 <= z < 88: return 22.0
    if 165 <= z < 172: return 22.0
    return 0.0


def kosmos_gallery(z: float) -> float:
    """Kosmos 灯头的托架（夹灯罩的一圈），顶面被烟熏黑。"""
    return 24.0 if 172 <= z <= 188 else 0.0


def kosmos_font(z: float) -> float:
    """玻璃油壶：88–165 mm，最宽处半径 55 mm。"""
    if 88 <= z <= 165:
        return 22.0 + 33.0 * math.sin(math.pi * (z - 88) / 77) ** 0.8
    return 0.0


def kosmos_chimney(z: float) -> float:
    """Kosmos 灯罩：188–200 下部鼓起，火焰上方 214 收腰，226 以上直筒。"""
    if z < 188 or z > 335: return 0.0
    if z < 200: return 21.0
    if z < 214: return 21.0 - 6.5 * (1 - math.cos(math.pi * (z - 200) / 14)) / 2
    if z < 226: return 14.5 + 2.5 * (1 - math.cos(math.pi * (z - 214) / 12)) / 2
    return 17.0


def flame_profile(z: float) -> float:
    if 196 <= z <= 222:
        t = (z - 196) / 26
        return 9.0 * math.sin(math.pi * t ** 0.7)
    return 0.0


def inkwell_glass(z: float) -> float:
    """Winterthur 墨水台：Ø55.75 × 49 mm；直身、圆肩、短颈、唇口。"""
    if z < 0 or z > 49: return 0.0
    if z < 32: return 27.9 - z * 0.03
    if z < 42: return 9.0 + 18.0 * math.cos((z - 32) / 10 * math.pi / 2)
    if z < 47: return 8.5
    return 10.0


def inkwell_ink(z: float) -> float:
    return max(0.0, inkwell_glass(z) - 2.6) if z < 30 else 0.0


def build_scene():
    lamp_axis = to_desk(*LAMP_FOOT)
    ink_axis = to_desk(*INK_FOOT)
    pen_a = np.array(to_desk(*PEN_START))
    pen_dir = np.array([math.cos(math.radians(PEN_ANGLE)), math.sin(math.radians(PEN_ANGLE))])
    holder_b = pen_a + pen_dir * mm(150)
    ferrule_b = pen_a + pen_dir * mm(168)
    nib_b = pen_a + pen_dir * mm(190)
    mag_c = to_desk(*MAG_CENTER)
    hd = np.array([math.cos(math.radians(MAG_HANDLE_ANGLE)), math.sin(math.radians(MAG_HANDLE_ANGLE))])
    h_a = np.array(mag_c) + hd * mm(28)
    h_f = h_a + hd * mm(10)
    h_b = h_f + hd * mm(80)

    opaque = [
        Revolution("lamp", lamp_axis, kosmos_foot, 0, 80, "iron", gloss=0.35),
        Revolution("lamp", lamp_axis, kosmos_collar, 80, 172, "brass", gloss=0.6),
        Revolution("lamp", lamp_axis, kosmos_gallery, 172, 188, "soot", gloss=0.2),
        Revolution("ink", ink_axis, inkwell_ink, 0, 30, "ink", gloss=0.8),
        Capsule("pen", pen_a, holder_b, 4.0, "wood_dark"),
        Capsule("pen", holder_b, ferrule_b, 3.6, "brass", gloss=0.8),
        Capsule("pen", ferrule_b, nib_b, 1.6, "steel", gloss=0.9),
        Torus("mag", mag_c, 26.0, 2.6, "brass"),
        Capsule("mag", h_a, h_f, 4.0, "brass", gloss=0.8),
        Capsule("mag", h_f, h_b, 5.0, "horn", gloss=0.6),
    ]
    flame = Revolution("flame", lamp_axis, flame_profile, 196, 222, "flame", kind="flame")
    glass = [
        ("ink_glass", Revolution("ink_glass", ink_axis, inkwell_glass, 0, 49, "glass", kind="glass"), (0.96, 0.98, 1.0)),
        ("font", Revolution("font", lamp_axis, kosmos_font, 88, 165, "glass", kind="glass"), (1.0, 0.97, 0.9)),
        ("chimney", Revolution("chimney", lamp_axis, kosmos_chimney, 188, 335, "glass", kind="glass"), (1.0, 1.0, 1.0)),
    ]
    occluders = [
        OcCylinder("lamp", lamp_axis, 62.5, 0, 18),
        OcCylinder("lamp", lamp_axis, 10.0, 18, 88),
        OcEllipsoid("font", (lamp_axis[0], lamp_axis[1], mm(126.5)), 55.0, 38.5, opacity=0.4),
        OcCylinder("lamp", lamp_axis, 24.0, 165, 188),
        OcCylinder("ink", ink_axis, 27.9, 0, 32),
        OcCylinder("ink", ink_axis, 10.0, 32, 49),
        OcCapsule("pen", pen_a, nib_b, 4.0),
        OcRing("mag", mag_c, 23.4, 28.6, 2.6),
        OcCapsule("mag", h_a, h_b, 5.0),
        OcBox("book", BOOK_DESK, BOOK_DESK_SIZE, BOOK_ANGLE, BOOK_H),
    ]
    lens = (mag_c, mm(23.4), mm(2.6))
    return opaque, flame, glass, occluders, lens


# ================================================================ 渲染 ===

OCCLUDERS: list = []


def visibility(P, part):
    vis = np.ones(P[0].shape)
    for o in OCCLUDERS:
        mask = part != o.part
        if o.part == "font":
            mask &= part != "lamp"
        if not mask.any():
            continue
        b = o.block(P)
        vis = np.where(mask, vis * (1 - 0.88 * b), vis)
    return vis


def render() -> np.ndarray:
    global OCCLUDERS
    opaque, flame, glass, OCCLUDERS, lens = build_scene()
    depth = np.zeros((H, W))                 # 视线参数 s = z / ELEV_Q；桌面为 0
    zbuf = np.zeros((H, W))
    part = np.full((H, W), "desk", dtype=object)
    mat = np.full((H, W), "desk", dtype=object)
    gloss = np.zeros((H, W))
    N = np.zeros((H, W, 3))
    N[..., 2] = 1.0
    for obj in opaque:
        res = obj.trace()
        if res is None:
            continue
        sl, hit, zh, nx, ny, nz = res
        s = zh / ELEV_Q
        front = hit & (s > depth[sl])
        depth[sl][front] = s[front]
        zbuf[sl][front] = zh[front]
        part[sl][front] = obj.name
        mat[sl][front] = obj.material
        gloss[sl][front] = obj.gloss
        N[sl][front] = np.stack([nx, ny, nz], -1)[front]
    # 每个可见点的桌面坐标、受光与遮挡
    P = (XC, Y0 + zbuf * ELEV_K / ELEV_Q, zbuf)
    vis = visibility(P, part)
    L = np.stack([FLAME3[0] - P[0], FLAME3[1] - P[1], FLAME3[2] - P[2]], -1)
    dist2 = (L * L).sum(-1)
    Lh = L / np.sqrt(dist2)[..., None]
    fz2 = FLAME3[2] ** 2
    cos = np.clip((N * Lh).sum(-1), 0, 1)
    light = brightness(cos * fz2 / dist2) * vis
    img = np.zeros((H, W, 3))
    desk_mask = mat == "desk"
    tint = DESK_TINTS[quantize(light, len(DESK_TINTS))]
    img[desk_mask] = (wood_albedo() * tint / 255.0)[desk_mask]
    Hh = Lh + VIEW
    Hh /= np.linalg.norm(Hh, axis=-1, keepdims=True)
    spec = np.clip((N * Hh).sum(-1), 0, 1) ** 36 * vis * brightness(fz2 / dist2)
    for name, colors in RAMPS.items():
        m = mat == name
        if not m.any():
            continue
        # 曝光：受光最强处落在倒数第二级，最亮一级留给高光
        v = 0.08 + 0.7 * light + 0.8 * gloss * spec
        img[m] = np.array(colors, dtype=float)[quantize(v, len(colors))][m]
    # 火焰（自发光）
    res = flame.trace()
    if res is not None:
        sl, hit, zh, nx, ny, nz = res
        s = zh / ELEV_Q
        front = hit & (s > depth[sl])
        cx, cy = flame.axis
        rho = np.hypot(XC[sl] - cx, Y0[sl] + zh * ELEV_K / ELEV_Q - cy)
        zmm = zh / MM
        rr = np.vectorize(lambda z: max(1e-3, flame_profile(z)))(zmm) * MM
        f = rho / rr
        t = (zmm - 196) / 26
        col = np.where((t < 0.14)[..., None], np.array([92, 104, 140.0]),
              np.where(((f < 0.5) & (t > 0.25))[..., None], np.array([255, 253, 244.0]),
              np.where((f < 0.82)[..., None], np.array([255, 240, 202.0]), np.array([244, 218, 160.0]))))
        img[sl][front] = col[front]
        depth[sl][front] = s[front]
    # 镜片：把框里看到的东西放大 1.6 倍（取已渲好的画面）；朝灯对侧的内缘压一道框影，朝灯一侧一道高光
    (lcx, lcy), lr, lz = lens
    scx, scy = to_screen(lcx, lcy, lz)
    ux, wy = (XC - scx) / lr, (YS + 0.5 - scy) / (lr * ELEV_Q)
    rim = np.hypot(ux, wy)
    inside = (rim <= 1) & (depth <= lz / ELEV_Q + 0.5)
    src = img.copy()
    sx = np.clip((scx + (XC - scx) / 1.6).astype(int), 0, W - 1)
    sy = np.clip((scy + (YS + 0.5 - scy) / 1.6 + lz * ELEV_K).astype(int), 0, H - 1)
    mag = src[sy, sx] * 1.06
    fl = np.hypot(scx - FLAME[0], scy - FLAME[1])
    away = (ux * (scx - FLAME[0]) + wy * (scy - FLAME[1])) / (fl * np.maximum(rim, 1e-6))
    mag = np.where(((rim > 0.84) & (away > 0.25))[..., None], mag * 0.62, mag)
    mag = np.where(((rim > 0.86) & (rim < 0.95) & (away < -0.75))[..., None], np.array([252, 246, 228.0]), mag)
    img[inside] = mag[inside]
    # 玻璃：不填色，透出后面的东西；掠射处反光、朝灯处一点高光、灯罩里被火焰照亮
    for name, g, tint_rgb in glass:
        res = g.trace()
        if res is None:
            continue
        sl, hit, zh, nx, ny, nz = res
        s = zh / ELEV_Q
        front = hit & (s > depth[sl])
        n = np.stack([nx, ny, nz], -1)
        fres = (1 - np.abs(n @ VIEW)) ** 3
        Pg = (XC[sl], Y0[sl] + zh * ELEV_K / ELEV_Q, zh)
        Lg = np.stack([FLAME3[0] - Pg[0], FLAME3[1] - Pg[1], FLAME3[2] - Pg[2]], -1)
        Lg /= np.linalg.norm(Lg, axis=-1, keepdims=True)
        hg = Lg + VIEW
        hg /= np.linalg.norm(hg, axis=-1, keepdims=True)
        sp = np.clip((n * hg).sum(-1), 0, 1) ** 60
        glow = np.zeros(zh.shape)
        if name == "chimney":
            glow = np.clip(1 - np.hypot(XC[sl] - FLAME[0], YS[sl] + 0.5 - FLAME[1]) / mm(34), 0, 1)
        # 透过玻璃看到的颜色保持原样（已是量化过的色阶）；只叠加分级抖色的反光、高光与灯罩里的火光
        behind = img[sl] * np.array(tint_rgb)
        # 只有接近掠射的轮廓处才反光，玻璃因此显出一道道细边，不会在缓曲面上分出色带
        reflect = np.clip((fres - 0.3) * 3.0, 0, 1) + np.clip(sp * 1.4, 0, 1)
        levels = np.minimum(3, np.floor(reflect * 3 + TH[sl]))[..., None] / 3.0
        col = behind + levels * (np.array([236, 228, 206.0]) - behind)
        if name == "chimney":
            g = np.minimum(3, np.floor(glow * 3 + TH[sl]))[..., None] / 3.0
            col = col + g * (np.array([255, 236, 196.0]) - col) * 0.55
        img[sl][front] = np.clip(col, 0, 255)[front]
    return np.clip(img, 0, 255).astype(np.uint8)


def glow() -> Image.Image:
    d = np.hypot(XC - FLAME[0], (YS + 0.5 - FLAME[1]) * 1.05)
    v = np.clip(1 - d / 170.0, 0, 1) ** 1.4 * 3
    lv = np.minimum(3, np.floor(v) + (v - np.floor(v) > TH)).astype(int)
    rgba = np.zeros((H, W, 4), np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = 255, 238, 200
    rgba[..., 3] = np.array([0, 24, 50, 88])[lv]
    return Image.fromarray(rgba, "RGBA")


def book_light() -> Image.Image:
    """册子顶面（离桌 BOOK_THICK_MM）的乘色光照：同一光源，册子比桌面亮，是画面主体。"""
    Z = BOOK_H
    X, Y = XC, (YS + 0.5 + Z * ELEV_K) / ELEV_Q
    dx, dy, dz = FLAME3[0] - X, FLAME3[1] - Y, FLAME3[2] - Z
    e = dz * FLAME3[2] ** 2 / (dx * dx + dy * dy + dz * dz) ** 1.5
    v = 0.12 + 0.95 * brightness(e)
    return Image.fromarray(np.array(BOOK_TINTS, np.uint8)[quantize(v, len(BOOK_TINTS))], "RGB")


def cloth() -> Image.Image:
    """布面：墨绿底上的平纹经纬（2×2 一格明暗交错）与零星粗纱。"""
    rng = random.Random(23)
    n = 128
    im = Image.new("RGB", (n, n))
    px = im.load()
    base = (42, 44, 36)
    for y in range(n):
        for x in range(n):
            weave = ((x // 2) + (y // 2)) % 2
            k = 1.06 if weave else 0.94
            if rng.random() < 0.04:
                k *= 0.9
            px[x, y] = tuple(min(255, int(v * k)) for v in base)
    for _ in range(18):
        y = rng.randrange(n)
        x0 = rng.randrange(n)
        for k in range(rng.randint(4, 10)):
            r, g, b = px[(x0 + k) % n, y]
            px[(x0 + k) % n, y] = (r + 8, g + 8, b + 6)
    return im.resize((n * S, n * S), Image.NEAREST)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    outputs = {"desk.png": Image.fromarray(render(), "RGB"), "glow.png": glow(), "book_light.png": book_light()}
    for name, im in outputs.items():
        im.resize((W * S, H * S), Image.NEAREST).save(OUT / name)
        print(f"wrote {OUT / name}")
    cloth().save(OUT / "cloth.png")
    print(f"wrote {OUT / 'cloth.png'}")


if __name__ == "__main__":
    main()
