# 制图工具：等高线（向量化行进方格）、山体阴影、分层设色、中文字体。只依赖 numpy 与 Pillow。
import numpy as np
from PIL import Image, ImageDraw, ImageFont

FONT = r'C:\Windows\Fonts\msyh.ttc'
FONT_B = r'C:\Windows\Fonts\msyhbd.ttc'

def font(size, bold=False):
    if not bold and size == 22:      # 微软雅黑常规体 22 px 时“誓”等字的字形提示有缺陷
        size = 23
    return ImageFont.truetype(FONT_B if bold else FONT, size)

# 边序号：0 上（tl-tr），1 右（tr-br），2 下（bl-br），3 左（tl-bl）
_CASES = {1: [(3, 2)], 2: [(2, 1)], 3: [(3, 1)], 4: [(0, 1)], 5: [(0, 3), (2, 1)], 6: [(0, 2)],
          7: [(0, 3)], 8: [(0, 3)], 9: [(0, 2)], 10: [(0, 1), (3, 2)], 11: [(0, 1)], 12: [(3, 1)],
          13: [(2, 1)], 14: [(3, 2)]}

def contour_segments(xs, ys, Z, level):
    """返回 (N, 4) 的线段数组 [x0, y0, x1, y1]，坐标与 xs、ys 同单位。ys 自上而下。"""
    tl, tr, bl, br = Z[:-1, :-1], Z[:-1, 1:], Z[1:, :-1], Z[1:, 1:]
    case = (tl > level) * 8 + (tr > level) * 4 + (br > level) * 2 + (bl > level) * 1
    X0, X1 = xs[:-1][None, :], xs[1:][None, :]
    Y0, Y1 = ys[:-1][:, None], ys[1:][:, None]
    def lerp(za, zb):
        with np.errstate(divide='ignore', invalid='ignore'):
            t = (level - za) / (zb - za)
        return np.clip(np.nan_to_num(t, nan=0.5), 0, 1)
    shape = case.shape
    ex = [X0 + lerp(tl, tr) * (X1 - X0), np.broadcast_to(X1, shape), X0 + lerp(bl, br) * (X1 - X0), np.broadcast_to(X0, shape)]
    ey = [np.broadcast_to(Y0, shape), Y0 + lerp(tr, br) * (Y1 - Y0), np.broadcast_to(Y1, shape), Y0 + lerp(tl, bl) * (Y1 - Y0)]
    segs = []
    for c, pairs in _CASES.items():
        m = case == c
        if not m.any():
            continue
        for e0, e1 in pairs:
            segs.append(np.stack([ex[e0][m], ey[e0][m], ex[e1][m], ey[e1][m]], axis=1))
    return np.concatenate(segs) if segs else np.zeros((0, 4))

def hillshade(Z, step, azimuth=315, altitude=40, zscale=1.0):
    gy, gx = np.gradient(Z * zscale, step)
    gy = -gy                                    # 行向下为南
    slope = np.arctan(np.hypot(gx, gy))
    aspect = np.arctan2(-gx, gy)
    az, alt = np.radians(azimuth), np.radians(altitude)
    shade = np.sin(alt) * np.cos(slope) + np.cos(alt) * np.sin(slope) * np.cos(az - aspect)
    return np.clip(shade, 0, 1)

LAND_STOPS = [(-0.5, (214, 207, 178)), (3, (205, 199, 168)), (20, (176, 186, 150)), (50, (150, 168, 128)),
              (100, (138, 150, 112)), (150, (150, 140, 108)), (200, (160, 140, 116)), (260, (176, 162, 146))]
SEA_STOPS = [(-60, (96, 122, 138)), (-20, (128, 156, 168)), (-5, (160, 186, 192)), (-0.5, (186, 206, 206))]

def tint(Z):
    out = np.zeros(Z.shape + (3,))
    for stops, mask in ((LAND_STOPS, Z >= -0.5), (SEA_STOPS, Z < -0.5)):
        hs = np.array([s[0] for s in stops]); cs = np.array([s[1] for s in stops], float)
        for k in range(3):
            out[..., k] = np.where(mask, np.interp(Z, hs, cs[:, k]), out[..., k])
    return out

def shaded_relief(Z, step, zscale=1.0, strength=0.55):
    rgb = tint(Z)
    hs = hillshade(Z, step, zscale=zscale)
    land = Z >= -0.5
    k = np.where(land, (1 - strength) + strength * hs * 1.25, 1.0)[..., None]
    return np.clip(rgb * k, 0, 255).astype(np.uint8)

def draw_segments(draw, segs, to_px, fill, width):
    for x0, y0, x1, y1 in segs:
        draw.line([to_px(x0, y0), to_px(x1, y1)], fill=fill, width=width)

def halo_text(draw, xy, text, fnt, fill=(40, 32, 22), halo=(245, 240, 228), anchor='mm', w=3):
    draw.text(xy, text, font=fnt, fill=fill, anchor=anchor, stroke_width=w, stroke_fill=halo)
