"""生成标题页的桌面、灯光与册子封皮（设计规范第六部分 §4.7 标题页）。

近俯视（约 60° 往下看，与地图同为俯视为主）：竖直高度按 ELEV_K 缩短，水平的圆按 ELEV_Q 压成接近正圆的椭圆。
木桌与黄铜油灯；光照分 7 级，级间 4×4 有序抖色，光池以灯脚为中心、纵向略压。
灯光取低饱和暖白，不用琥珀（恶魔专属）。原生 640×496，按最近邻放大 2 倍到 1280×992；随机种子固定，可重建。

    desk.png        桌面×灯光，含油灯与桌上物件（墨水瓶与蘸水笔、手记与剪报、放大镜、石墨笔；不含光晕）
                    物件按法线逐像素着色，量化到材质色阶，级间同样用有序抖色
    glow.png        灯焰光晕（RGBA，叠加混合；标题页让它轻微闪动）
    book_light.png  册子表面的乘色光照（屏幕坐标，标题页的册子着色器按屏幕 UV 取样）
    cloth.png       册子布面封皮（128 原生像素可平铺，放大 2 倍）

    python godot_csharp/tools/build_ui_desk.py
"""
from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image

OUT = Path(__file__).resolve().parents[1] / "ShareAsset" / "Internal" / "Props" / "UIDesk"
W, H, S = 640, 496, 2
ELEV_K = 0.5               # 竖直高度在画面上的缩短（cos 60°）
ELEV_Q = 0.87              # 水平圆的纵横比（sin 60°）
LAMP_SCALE = 1.4           # 油灯尺寸倍率
INK_SCALE = 1.5            # 墨水瓶尺寸倍率
FOOT = (92, 150)           # 灯脚（光池中心）
FLAME_HEIGHT = round(38 * LAMP_SCALE)   # 火焰离桌面的高度（原生像素）
FLAME = (FOOT[0], FOOT[1] - round(FLAME_HEIGHT * ELEV_K))
SQUASH = 1 / ELEV_Q
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
LEVELS = 7
DESK_TINTS = [(40, 34, 40), (62, 52, 52), (94, 78, 70), (132, 110, 90), (172, 146, 114), (210, 182, 140), (240, 220, 180)]
BOOK_TINTS = [(92, 78, 72), (124, 104, 90), (156, 132, 110), (188, 162, 130), (218, 194, 154), (240, 220, 182), (255, 242, 212)]


def dither_level(v: float, x: int, y: int) -> int:
    v = max(0.0, min(1.0, v)) * (LEVELS - 1)
    lo = int(v)
    return min(LEVELS - 1, lo + (1 if v - lo > (BAYER[y % 4][x % 4] + 0.5) / 16 else 0))


def dist(x: int, y: int) -> float:
    return math.hypot(x - FOOT[0], (y - FOOT[1]) * SQUASH)


def ell(x: float, y: float, rx: float, ry: float) -> bool:
    return (x / rx) ** 2 + (y / ry) ** 2 <= 1


def wood() -> Image.Image:
    rng = random.Random(7)
    im = Image.new("RGB", (W, H))
    px = im.load()
    base = [(120, 78, 50), (112, 72, 46), (126, 84, 54)]
    plank_h = 40
    seams = {row: rng.randrange(0, W) for row in range(H // plank_h + 2)}
    phase = [rng.random() * 6.28 for _ in range(32)]
    for y in range(H):
        row, yy = y // plank_h, y % plank_h
        c0 = base[row % 3]
        for x in range(W):
            g = math.sin(x * 0.045 + phase[row % 32] + math.sin(x * 0.011 + row) * 2.2 + yy * 0.42)
            k = 0.88 + (0.12 if g > 0.55 else 0) - (0.08 if g < -0.75 else 0)
            c = tuple(int(v * k) for v in c0)
            if yy == 0:
                c = (46, 28, 20)
            elif yy == 1:
                c = tuple(int(v * 0.8) for v in c0)
            if x == seams[row]:
                c = (52, 32, 22)
            px[x, y] = c
    return im


# ------------------------------------------------------------- 立体着色 ---
# 物件按法线逐像素算明暗（火焰为光源），再量化到各自材质的几级色阶，级间 4×4 有序抖色——
# 与桌面光照同一手法，所以物件有体积、又不出现平滑渐变。坐标：x 向右、y 向下（屏幕），z 朝向观者。

BRASS = [(44, 30, 18), (78, 56, 30), (120, 90, 46), (166, 128, 66), (206, 170, 98), (236, 212, 150), (255, 244, 212)]
INK_GLASS = [(10, 12, 18), (20, 24, 36), (34, 40, 60), (52, 62, 90), (84, 98, 128), (150, 162, 186), (226, 230, 236)]
WOOD_DARK = [(26, 16, 10), (44, 28, 18), (68, 44, 28), (96, 64, 40), (128, 90, 58), (170, 128, 86)]
CEDAR = [(54, 36, 22), (92, 62, 38), (132, 94, 58), (172, 128, 82), (206, 162, 108), (232, 196, 140)]
PAPER = [(60, 52, 46), (96, 84, 72), (136, 120, 100), (176, 158, 130), (210, 194, 160), (232, 220, 188), (246, 238, 214)]
GRAPHITE = [(22, 22, 26), (40, 40, 46), (62, 62, 70), (96, 96, 106), (150, 150, 160)]
GLASS = [(96, 84, 70), (150, 136, 112), (200, 188, 160), (232, 222, 196), (248, 242, 224), (255, 252, 240)]
HAND_INK = (47, 59, 87)
PRINT_INK = (40, 34, 30)


def ramp(colors, v: float, x: int, y: int):
    v = max(0.0, min(1.0, v)) * (len(colors) - 1)
    lo = int(v)
    return colors[min(len(colors) - 1, lo + (1 if v - lo > (BAYER[y % 4][x % 4] + 0.5) / 16 else 0))]


def norm(v):
    n = math.sqrt(sum(c * c for c in v)) or 1.0
    return tuple(c / n for c in v)


def dot(a, b):
    return sum(p * q for p, q in zip(a, b))


def desk_light(x: int, y: int) -> float:
    return max(0.0, min(1.0, 1.0 - (dist(x, y) / 560.0) ** 0.62))


def light_at(x: float, y: float, lift: float = 24.0):
    """从 (x, y) 指向火焰的光线方向；lift 为火焰朝观者一侧的高度。"""
    return norm((FLAME[0] - x, FLAME[1] - y, lift))


def lit(n, x: int, y: int, ambient=0.18, gloss=0.0, power=24, lift=40.0) -> float:
    """漫反射 + 高光，乘上此处的灯光强度。"""
    L = light_at(x, y, lift)
    diffuse = max(0.0, dot(n, L))
    spec = 0.0
    if gloss:
        h = norm((L[0], L[1], L[2] + 1.0))
        spec = gloss * max(0.0, dot(n, h)) ** power
    return (ambient + (1 - ambient) * diffuse) * max(0.35, desk_light(x, y)) + spec


class Canvas:
    def __init__(self, im: Image.Image):
        self.im = im
        self.px = im.load()

    def put(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.px[x, y] = c

    def get(self, x, y):
        return self.px[min(W - 1, max(0, x)), min(H - 1, max(0, y))]

    def darken(self, x, y, k):
        if 0 <= x < W and 0 <= y < H:
            r, g, b = self.px[x, y]
            self.px[x, y] = (int(r * k), int(g * k), int(b * k))


def shadow_ellipse(cv: Canvas, cx, cy, rx, ry, k=0.5):
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            if ell(x - cx, y - cy, rx, ry):
                # 影子边缘用抖色收一圈，不用平滑渐变
                edge = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
                if edge < 0.7 or (BAYER[y % 4][x % 4] + 0.5) / 16 > (edge - 0.7) / 0.3:
                    cv.darken(x, y, k)


def cylinder(cv: Canvas, cx, top, bottom, rx, ry, colors, gloss=0.6, top_colors=None, top_v=None):
    """竖直圆柱：侧面按法线着色，顶面是椭圆（3/4 俯视）。top 为顶面椭圆中心 y，bottom 为底面椭圆中心 y。"""
    for y in range(int(top), int(bottom + ry) + 1):
        for x in range(int(cx - rx), int(cx + rx) + 1):
            t = (x - cx) / rx
            if abs(t) > 1:
                continue
            below_bottom = y > bottom and not ell(x - cx, y - bottom, rx, ry)
            if below_bottom:
                continue
            side = math.sqrt(max(0.0, 1 - t * t))
            n = (t, side * ELEV_Q, side * ELEV_K)
            cv.put(x, y, ramp(colors, lit(n, x, y, gloss=gloss), x, y))
    for y in range(int(top - ry) - 1, int(top + ry) + 2):
        for x in range(int(cx - rx), int(cx + rx) + 1):
            if ell(x - cx, y - top, rx, ry):
                n = (0.0, -ELEV_K, ELEV_Q)
                v = lit(n, x, y, gloss=gloss * 0.6) if top_v is None else top_v
                cv.put(x, y, ramp(top_colors or colors, v, x, y))


def sphere(cv: Canvas, cx, cy, rx, ry, colors, gloss=0.9):
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            u, w = (x - cx) / rx, (y - cy) / ry
            d = u * u + w * w
            if d > 1:
                continue
            n = (u, w, math.sqrt(1 - d))
            cv.put(x, y, ramp(colors, lit(n, x, y, gloss=gloss, power=48), x, y))


def stroke(cv: Canvas, a, b, width, colors, shade_side=True, shadow=(1, 2)):
    """一根细长物件（笔杆、铅笔、手柄）：沿长轴画，横截面按圆柱着色；先在下方偏移处压一道影子。"""
    ax, ay = a
    bx, by = b
    length = math.hypot(bx - ax, by - ay)
    ux, uy = (bx - ax) / length, (by - ay) / length
    px_, py_ = -uy, ux
    steps = int(length * 2) + 1
    if shadow:
        for i in range(steps):
            s = i / (steps - 1)
            for j in range(-int(width / 2), int(width / 2) + 1):
                cv.darken(int(round(ax + ux * length * s + px_ * j + shadow[0])), int(round(ay + uy * length * s + py_ * j + shadow[1])), 0.62)
    for i in range(steps):
        s = i / (steps - 1)
        for j10 in range(-int(width * 5), int(width * 5) + 1):
            j = j10 / 10
            x = int(round(ax + ux * length * s + px_ * j))
            y = int(round(ay + uy * length * s + py_ * j))
            t = j / (width / 2) if width else 0
            n = norm((px_ * t, py_ * t - 0.3, math.sqrt(max(0.05, 1 - t * t))))
            cv.put(x, y, ramp(colors(s) if callable(colors) else colors, lit(n, x, y, gloss=0.5), x, y))


def lamp3d(cv: Canvas) -> None:
    """黄铜油灯（近俯视）：圆形底盘、球形油壶、灯颈、灯头、短短一截玻璃灯罩，火焰从灯罩里透出。光从火焰来。"""
    cx, fy = FOOT
    k = LAMP_SCALE
    y_at = lambda h: fy - h * k * ELEV_K
    shadow_ellipse(cv, cx + 3 * k, fy + 2 * k, 30 * k, 30 * k * ELEV_Q, 0.45)
    cylinder(cv, cx, y_at(6), y_at(0), 24 * k, 24 * k * ELEV_Q, BRASS, gloss=0.35, top_v=0.62)
    sphere(cv, cx, y_at(22), 17 * k, 17 * k, BRASS, gloss=0.55)
    cylinder(cv, cx, y_at(44), y_at(36), 8 * k, 8 * k * ELEV_Q, BRASS, gloss=0.6)
    cylinder(cv, cx, y_at(48), y_at(44), 11 * k, 11 * k * ELEV_Q, BRASS, gloss=0.6, top_v=0.7)
    # 玻璃灯罩：自下而上一圈圈画，越高越近观者、后画；内里被火焰照亮，左侧一道反光，右缘偏暗
    h = 48.0
    while h <= 100.0:
        q = (h - 64) / 12
        r = (5.5 + 8 * math.exp(-q * q) if h < 86 else 5.5) * k
        cy = y_at(h)
        for y in range(int(cy - r * ELEV_Q) - 1, int(cy + r * ELEV_Q) + 2):
            for x in range(int(cx - r), int(cx + r) + 1):
                if not ell(x - cx, y - cy, r, r * ELEV_Q):
                    continue
                t = (x - cx) / r
                glow = max(0.0, 1 - math.hypot(x - FLAME[0], y - FLAME[1]) / (22 * k))
                v = 0.3 + 0.55 * glow + 0.35 * abs(t) ** 6
                if -0.62 < t < -0.42:
                    v += 0.28
                if t > 0.86:
                    v -= 0.18
                cv.put(x, y, ramp(GLASS, v, x, y))
        h += 0.5
    # 火焰：从上往下看得到焰心与一圈内焰，透过玻璃
    fx, f0 = FLAME
    for y in range(f0 - round(7 * k), f0 + round(4 * k)):
        t = (y - f0 + 7 * k) / (11 * k)
        hw = 1 + 3.2 * k * math.sin(math.pi * max(0.0, t) ** 0.8)
        for x in range(int(fx - hw), int(fx + hw) + 1):
            d = abs(x - fx) / max(hw, 1)
            if y >= f0 + 2 * k:
                c = (92, 104, 140) if d < 0.6 else (150, 120, 92)
            elif d < 0.45 and y > f0 - 4 * k:
                c = (255, 253, 244)
            elif d < 0.8:
                c = (255, 240, 202)
            else:
                c = (244, 218, 160)
            cv.put(x, y, c)
    # 灯罩顶口：一圈亮边，口里看得见下面的光
    top = y_at(100)
    for y in range(int(top - 6 * k), int(top + 6 * k) + 1):
        for x in range(int(cx - 7 * k), int(cx + 7 * k) + 1):
            outer = ell(x - cx, y - top, 6.5 * k, 6.5 * k * ELEV_Q)
            inner = ell(x - cx, y - top, 4.6 * k, 4.6 * k * ELEV_Q)
            if outer and not inner:
                cv.put(x, y, ramp(GLASS, 0.9 if x < cx else 0.6, x, y))
            elif inner:
                cv.put(x, y, ramp(GLASS, 0.42 + 0.3 * max(0.0, 1 - math.hypot(x - FLAME[0], y - FLAME[1]) / (30 * k)), x, y))


def inkwell(cv: Canvas, cx: int, fy: int) -> None:
    """玻璃墨水瓶（圆柱），瓶里是笔迹色的墨；影子投向远离灯的一侧。"""
    away = norm((cx - FOOT[0], (fy - FOOT[1]) * 1.4))
    k = INK_SCALE
    shadow_ellipse(cv, cx + away[0] * 6 * k, fy + 1 + away[1] * 4 * k, 15 * k, 15 * k * ELEV_Q, 0.5)
    cylinder(cv, cx, fy - 18 * k * ELEV_K, fy, 13 * k, 13 * k * ELEV_Q, INK_GLASS, gloss=0.45, top_v=0.3)
    cylinder(cv, cx, fy - 24 * k * ELEV_K, fy - 18 * k * ELEV_K, 5 * k, 5 * k * ELEV_Q, INK_GLASS, gloss=0.45, top_v=0.38)
    mouth = fy - 24 * k * ELEV_K
    for y in range(int(mouth) - 6, int(mouth) + 7):
        for x in range(cx - 6, cx + 7):
            if ell(x - cx, y - mouth, 3 * k, 3 * k * ELEV_Q):
                cv.put(x, y, (8, 9, 14))
    # 瓶肩一道弧形反光（朝灯一侧）
    top = fy - 18 * k * ELEV_K
    for i in range(10):
        theta = math.radians(200 + i * 5)
        cv.put(int(round(cx + 11 * k * math.cos(theta))), int(round(top + 11 * k * ELEV_Q * math.sin(theta))), INK_GLASS[5])


def sheet(cv: Canvas, cx, cy, w, h, angle_deg, kind: str, seed: int) -> list:
    """一页散纸：kind = hand（本人手记，笔迹色短划）或 print（剪报，两栏印刷灰块与一道标题）。返回四角。"""
    rng = random.Random(seed)
    a = math.radians(angle_deg)
    ca, sa = math.cos(a), math.sin(a)
    def to_local(x, y):
        dx, dy = x - cx, y - cy
        return dx * ca + dy * sa + w / 2, -dx * sa + dy * ca + h / 2
    corners = [(cx + (u - w / 2) * ca - (v - h / 2) * sa, cy + (u - w / 2) * sa + (v - h / 2) * ca) for u, v in ((0, 0), (w, 0), (w, h), (0, h))]
    xs = [p[0] for p in corners]
    ys = [p[1] for p in corners]
    # 纸很薄：影子只偏 2、3 像素
    for y in range(int(min(ys)), int(max(ys)) + 4):
        for x in range(int(min(xs)), int(max(xs)) + 4):
            u, v = to_local(x - 2, y - 3)
            if 0 <= u < w and 0 <= v < h:
                cv.darken(x, y, 0.6)
    marks = {}
    if kind == "hand":
        for row in range(12, h - 8, 7):
            u = 10 + rng.randint(0, 4)
            while u < w - 10:
                length = rng.randint(4, 14)
                if rng.random() > 0.12:
                    for k in range(length):
                        marks[(u + k, row + (1 if rng.random() < 0.15 else 0))] = HAND_INK
                u += length + rng.randint(2, 4)
            if rng.random() < 0.25:
                row += 3
    else:
        for u in range(8, w - 8):
            for v in (9, 10, 11):
                if rng.random() < 0.85:
                    marks[(u, v)] = PRINT_INK
        col_w = (w - 22) // 2
        for col in range(2):
            u0 = 8 + col * (col_w + 6)
            for row in range(18, h - 6, 3):
                for u in range(u0, u0 + col_w):
                    if rng.random() < 0.55:
                        marks[(u, row)] = PRINT_INK
    for y in range(int(min(ys)) - 1, int(max(ys)) + 2):
        for x in range(int(min(xs)) - 1, int(max(xs)) + 2):
            u, v = to_local(x, y)
            if not (0 <= u < w and 0 <= v < h):
                continue
            light = desk_light(x, y) * 1.08
            ink = marks.get((int(u), int(v)))
            if ink is not None:
                k = 0.35 + 0.65 * light
                cv.put(x, y, tuple(int(c * k) for c in ink))
            else:
                cv.put(x, y, ramp(PAPER, light, x, y))
    return corners


def magnifier(cv: Canvas, cx: int, cy: int) -> None:
    """侦探的放大镜：黄铜圆框（3/4 俯视成椭圆）、短柄；镜片下的纸被放大得更亮，内缘一道高光。"""
    rx, ring = 17.0, 3.2
    ry = rx * ELEV_Q
    handle_a = (cx + rx * 0.72, cy + ry * 0.72)
    handle_b = (cx + rx * 0.72 + 15, cy + ry * 0.72 + 11)
    stroke(cv, (handle_a[0] - 2, handle_a[1] - 1.5), (handle_a[0] + 3, handle_a[1] + 2), 4, BRASS)
    stroke(cv, (handle_a[0] + 3, handle_a[1] + 2), handle_b, 4, WOOD_DARK)
    shadow_ellipse(cv, cx + 3, cy + 4, rx + 1, ry + 1, 0.7)
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            outer = ell(x - cx, y - cy, rx, ry)
            inner = ell(x - cx, y - cy, rx - ring, ry - ring * ELEV_Q)
            if not outer:
                continue
            if inner:
                r, g, b = cv.get(x, y)
                cv.put(x, y, (min(255, int(r * 1.12 + 6)), min(255, int(g * 1.12 + 6)), min(255, int(b * 1.1 + 4))))
                continue
            theta = math.atan2((y - cy) / ry, (x - cx) / rx)
            n = norm((math.cos(theta), math.sin(theta) - 0.3, 0.8))
            cv.put(x, y, ramp(BRASS, lit(n, x, y, gloss=0.9), x, y))
    for i in range(14):
        theta = math.radians(200 + i * 4)
        x = int(round(cx + (rx - ring - 1.5) * math.cos(theta)))
        y = int(round(cy + (ry - ring * ELEV_Q - 1.2) * math.sin(theta)))
        cv.put(x, y, (252, 246, 228))


def pencil(cv: Canvas, a, b) -> None:
    """石墨笔：木杆，削尖的一端露出木色锥面与笔芯。"""
    def colors(s):
        if s > 0.94:
            return GRAPHITE
        if s > 0.84:
            return CEDAR[2:]
        return CEDAR
    stroke(cv, a, b, 3, colors)


def dip_pen(cv: Canvas, a, b) -> None:
    """蘸水笔：深色木杆，前端一截黄铜笔舌。"""
    stroke(cv, a, b, 3, lambda s: BRASS if s > 0.86 else WOOD_DARK)


def desk_objects(cv: Canvas) -> None:
    lamp3d(cv)
    sheet(cv, 178, 302, 96, 124, 7, "print", 3)
    sheet(cv, 146, 286, 92, 120, -9, "hand", 5)
    pencil(cv, (100, 240), (166, 222))
    magnifier(cv, 170, 350)
    inkwell(cv, 74, 446)
    dip_pen(cv, (104, 468), (186, 438))


def glow() -> Image.Image:
    """灯焰周围三圈同心抖色光晕（RGBA）。"""
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = im.load()
    for y in range(H):
        for x in range(W):
            d = math.hypot(x - FLAME[0], (y - FLAME[1]) * 1.05)
            v = max(0.0, 1 - d / 120.0) ** 1.4 * 3
            lo = int(v)
            lv = min(3, lo + (1 if v - lo > (BAYER[y % 4][x % 4] + 0.5) / 16 else 0))
            if lv:
                px[x, y] = (255, 238, 200, (0, 34, 70, 120)[lv])
    return im


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
    desk = wood()
    px = desk.load()
    for y in range(H):
        for x in range(W):
            t = DESK_TINTS[dither_level(1.0 - (dist(x, y) / 560.0) ** 0.62, x, y)]
            r, g, b = px[x, y]
            px[x, y] = (r * t[0] // 255, g * t[1] // 255, b * t[2] // 255)
    desk_objects(Canvas(desk))
    book = Image.new("RGB", (W, H))
    bp = book.load()
    for y in range(H):
        for x in range(W):
            bp[x, y] = BOOK_TINTS[dither_level(1.0 - (dist(x, y) - 140) / 440.0, x, y)]
    outputs = {"desk.png": desk, "glow.png": glow(), "book_light.png": book}
    for name, im in outputs.items():
        im.resize((W * S, H * S), Image.NEAREST).save(OUT / name)
        print(f"wrote {OUT / name}")
    cloth().save(OUT / "cloth.png")
    print(f"wrote {OUT / 'cloth.png'}")


if __name__ == "__main__":
    main()
