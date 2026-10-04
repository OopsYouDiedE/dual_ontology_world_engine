# 学园总平面图（真实地形，1 m 等高线）与主轴剖面。读取 campus.py 的布局与 site.json 的选址结果。
# 运行：python render_site.py → campus_plan.png、campus_section.png、site.json（补充路与港口验算）
import json, os
import numpy as np
from PIL import Image, ImageDraw
import campus as S
from dem import HERE, ATTRIBUTION, blur
from cartography import contour_segments, hillshade, font, halo_text

R = json.load(open(os.path.join(HERE, 'site.json'), encoding='utf-8'))
O, BR = tuple(R['origin_world']), R['bearing_deg']
FLOOR = {b['name']: b['floor'] for b in R['buildings']}

# 港口与港路（世界坐标，经实测选线）
QUAY = [(-460, -1500), (-436, -1503)]                     # 0.5 倍岛（scale.json）
HARBOR_ROAD = [None, (-605, -1530), (-560, -1530), (-520, -1528), (-490, -1518), (-491, -1548), (-469, -1530), (-460, -1500)]
GATE = S.to_world(O, BR, 0, -122)
HARBOR_ROAD[0] = (float(GATE[0]), float(GATE[1]))
# 生活翼西门—星仪院改线
S.PATHS = [p for p in S.PATHS if p[0] != '生活翼西门—星仪院'] + [('生活翼西门—星仪院', [(-75.2, -56.7), (-82, -48), (-74, -22), (-72, 6)], 2.5)]

K, SS = 3.2, 2                                   # 每米像素、超采样
_xs, _ys = [], []
for _b in S.B:
    _u = [_b['c'][0] - 15, _b['c'][0] + 15]; _v = [_b['c'][1] - 15, _b['c'][1] + 15]
    _X, _Y = S.to_world(O, BR, [_u[0], _u[1], _u[1], _u[0]], [_v[0], _v[0], _v[1], _v[1]]); _xs += list(_X); _ys += list(_Y)
_xs += [p[0] for p in QUAY]; _ys += [p[1] for p in QUAY]
X0, X1, Y0, Y1 = min(_xs) - 35, max(_xs) + 45, min(_ys) - 40, max(_ys) + 45
TW = 600                                         # 右侧一览表宽（像素）
W, H = int((X1 - X0) * K), int((Y1 - Y0) * K)
MT, MB = 90, 150                                 # 上下留白（像素，未超采样）
def P(x, y): return ((x - X0) * K * SS, ((Y1 - y) * K + MT) * SS)

def grade_line(pts, step=2.0):
    out = []
    for (xa, ya), (xb, yb) in zip(pts[:-1], pts[1:]):
        L = float(np.hypot(xb - xa, yb - ya)); n = max(2, int(L / step))
        xs, ys = np.linspace(xa, xb, n), np.linspace(ya, yb, n)
        z = S.ground(xs, ys)
        out.append(dict(length=round(L, 1), rise=round(float(z[-1] - z[0]), 2), grade=round(float(abs(z[-1] - z[0]) / L), 3),
                        max_local=round(float(np.max(np.abs(np.diff(z))) / (L / (n - 1))), 3), land=bool(S.is_land(xs, ys).all())))
    return out

def axis_note():
    dd = ((np.degrees(np.arctan2(S.SUMMIT[0] - O[0], S.SUMMIT[1] - O[1])) - BR) + 180) % 360 - 180
    side = '基本正对' if abs(dd) < 5 else ('右前方约 %.0f°' % dd if dd > 0 else '左前方约 %.0f°' % -dd)
    return f"主轴 方位 {BR:.0f}°；全岛最高点 {S.SUMMIT[2]:.0f} m {side}（距 {np.hypot(S.SUMMIT[0] - O[0], S.SUMMIT[1] - O[1]) / 1000:.1f} km）"

def main():
    gx = X0 + (np.arange(W) + 0.5) / K; gy = Y1 - (np.arange(H) + 0.5) / K
    GX, GY = np.meshgrid(gx, gy)
    Z = S.ground(GX, GY); land = S.sample(S.LANDF, GX, GY) > 0.5
    shade = hillshade(Z, 1 / K, zscale=2.0)
    base = np.array([226, 220, 196], float)
    rgb = np.where(land[..., None], base * (0.82 + 0.25 * shade[..., None]), np.array([176, 198, 204], float))
    img = Image.new('RGB', ((W + TW) * SS, (H + MT + MB) * SS), (246, 242, 232))
    img.paste(Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).resize((W * SS, H * SS), Image.BICUBIC), (0, MT * SS))
    d = ImageDraw.Draw(img)
    Zc = np.where(land, Z, -2)
    for lv in range(1, int(Z.max()) + 1):
        bold = lv % 5 == 0
        for s in contour_segments(gx, gy, Zc, lv):
            d.line([P(s[0], s[1]), P(s[2], s[3])], fill=(150, 128, 100) if bold else (196, 180, 156), width=3 if bold else 1)
    for s in contour_segments(gx, gy, S.sample(S.LANDF, GX, GY), 0.5):
        d.line([P(s[0], s[1]), P(s[2], s[3])], fill=(60, 72, 80), width=4)
    # 等高线注记（沿主轴延长线与两条平行线）
    f10 = font(10 * SS)
    for du in (-120, 0, 150):
        vv = np.linspace(-200, 140, 1400)
        xs, ys = S.to_world(O, BR, np.full_like(vv, du), vv)
        z = S.ground(xs, ys); inside = (xs > X0 + 20) & (xs < X1 - 20) & (ys > Y0 + 20) & (ys < Y1 - 20) & S.is_land(xs, ys)
        for lv in range(5, 40, 5):
            hit = np.where(inside[:-1] & (np.diff(np.sign(z - lv)) != 0))[0]
            if len(hit):
                j = hit[0]; halo_text(d, P(xs[j], ys[j]), str(lv), f10, fill=(120, 96, 70), w=2)
    # 路
    def strip(pts_world, width, fill, outline=None):
        for (xa, ya), (xb, yb) in zip(pts_world[:-1], pts_world[1:]):
            d.line([P(xa, ya), P(xb, yb)], fill=outline or fill, width=int(width * K * SS) + (2 * SS if outline else 0))
        for (xa, ya), (xb, yb) in zip(pts_world[:-1], pts_world[1:]):
            d.line([P(xa, ya), P(xb, yb)], fill=fill, width=int(width * K * SS))
        for x, y in pts_world:
            r = width * K * SS / 2
            d.ellipse([P(x, y)[0] - r, P(x, y)[1] - r, P(x, y)[0] + r, P(x, y)[1] + r], fill=fill)
    path_out = []
    for name, pts, width in S.PATHS:
        X, Y = S.to_world(O, BR, [p[0] for p in pts], [p[1] for p in pts])
        wpts = list(zip(map(float, X), map(float, Y)))
        strip(wpts, width, (236, 228, 206), (176, 160, 130))
        segs = grade_line(wpts); path_out.append(dict(name=name, width=width, segments=segs))
        for (xa, ya), (xb, yb), sg in zip(wpts[:-1], wpts[1:], segs):
            if sg['length'] > 18:
                halo_text(d, P((xa + xb) / 2, (ya + yb) / 2), f"{sg['grade'] * 100:.0f}%", font(9 * SS), fill=(90, 80, 60), w=2)
    strip(HARBOR_ROAD, 5, (236, 228, 206), (176, 160, 130))
    road = grade_line(HARBOR_ROAD)
    for (xa, ya), (xb, yb), sg in zip(HARBOR_ROAD[:-1], HARBOR_ROAD[1:], road):
        halo_text(d, P((xa + xb) / 2, (ya + yb) / 2), f"{sg['grade'] * 100:.0f}%", font(9 * SS), fill=(90, 80, 60), w=2)
    d.line([P(*QUAY[0]), P(*QUAY[1])], fill=(110, 104, 96), width=int(6 * K * SS))
    # 塌方点：港路沿海崖一段
    cx, cy = (HARBOR_ROAD[5][0] + HARBOR_ROAD[6][0]) / 2, (HARBOR_ROAD[5][1] + HARBOR_ROAD[6][1]) / 2
    for a in (1, -1):
        d.line([P(cx - 6, cy - 6 * a), P(cx + 6, cy + 6 * a)], fill=(196, 60, 50), width=4 * SS)
    # 建筑
    COL = {'core': (196, 186, 168), 'academy': (214, 190, 150), 'living': (196, 176, 206), 'service': (190, 196, 176),
           'outdoor': (214, 222, 190)}
    def poly(b, du=0.0):
        if b['shape'] == 'rect':
            (cu, cv), (su, sv) = b['c'], b['s']
            U = [cu - su / 2, cu + su / 2, cu + su / 2, cu - su / 2]; V = [cv - sv / 2, cv - sv / 2, cv + sv / 2, cv + sv / 2]
        else:
            t = np.linspace(0, 2 * np.pi, 49)[:-1]; (cu, cv), r = b['c'], b['r']
            U, V = cu + r * np.cos(t), cv + r * np.sin(t)
        X, Y = S.to_world(O, BR, U, V)
        return [P(float(x), float(y)) for x, y in zip(X, Y)]
    for b in S.B:
        if b['name'].startswith('生活翼'):
            continue
        pg = poly(b)
        if b['kind'] == 'outdoor':
            d.polygon(pg, fill=COL['outdoor'], outline=(120, 130, 100), width=2 * SS)
        elif b['kind'] == 'closed':
            d.polygon(pg, fill=(206, 202, 194), outline=(120, 116, 110), width=2 * SS)
            xs_ = [p[0] for p in pg]; ys_ = [p[1] for p in pg]
            for t in np.linspace(0, 1, 12)[1:-1]:          # 斜线填充表示封闭
                pass
        else:
            d.polygon(pg, fill=COL[b['cat']], outline=(70, 60, 50), width=3 * SS)
    # 封闭建筑加斜线
    for b in S.B:
        if b['kind'] != 'closed':
            continue
        pg = poly(b); xs_ = [p[0] for p in pg]; ys_ = [p[1] for p in pg]
        mask = Image.new('L', img.size, 0); ImageDraw.Draw(mask).polygon(pg, fill=255)
        hatch = Image.new('RGB', img.size, (206, 202, 194)); hd = ImageDraw.Draw(hatch)
        x0_, x1_, y0_, y1_ = int(min(xs_)), int(max(xs_)), int(min(ys_)), int(max(ys_))
        for o in range(x0_ - (y1_ - y0_), x1_, 8 * SS):
            hd.line([(o, y1_), (o + (y1_ - y0_), y0_)], fill=(150, 146, 138), width=SS)
        img.paste(hatch, (0, 0), mask); d.polygon(pg, outline=(120, 116, 110), width=2 * SS)
    # 生活翼：按平面图画外墙、房间分隔、夹道与起居厅
    LW = S.LW
    def lw(x, y):                                   # 平面图坐标（米，原点西北角，x 东 y 南）→ 画布
        X, Y = S.to_world(O, BR, LW['u0'] + x, LW['v0'] - y); return P(float(X), float(Y))
    ZL, ZD, LA, HA, BA = LW['ZL'], LW['ZD'], LW['LANE'], LW['HALL'], LW['BATH']
    def lw_rect(x0, y0, x1, y1, fill, outline=(70, 60, 50), wd=3):
        d.polygon([lw(x0, y0), lw(x1, y0), lw(x1, y1), lw(x0, y1)], fill=fill, outline=outline, width=wd * SS)
    for ox in (0, ZL + HA):
        for oy in (0, ZD + LA):
            lw_rect(ox, oy, ox + ZL, oy + ZD, COL['living'])
            xa, xb = (ox, ox + ZL)
            # 中廊与房间分隔（寝区外端为房间段，长约 11.9 m）
            rooms_end = 12.2 if ox == 0 else ZL - 12.2
            for yy in (oy + 5.55, oy + 7.65):
                a, b_ = (ox + 0.6, ox + 12.2) if ox == 0 else (ox + ZL - 12.2, ox + ZL - 0.6)
                d.line([lw(a, yy), lw(b_, yy)], fill=(70, 60, 50), width=SS)
            for kx in (1, 2, 3):
                xx = ox + 0.6 + kx * 3.85 - 0.125 if ox == 0 else ox + ZL - 0.6 - kx * 3.85 + 0.125
                for y_a, y_b in ((oy + 0.6, oy + 5.55), (oy + 7.65, oy + ZD - 0.6)):
                    d.line([lw(xx, y_a), lw(xx, y_b)], fill=(70, 60, 50), width=SS)
            # 隔离门
            dx = ox + ZL if ox == 0 else ox
            d.line([lw(dx, oy + 5.85), lw(dx, oy + 7.35)], fill=(239, 159, 39), width=4 * SS)
        lw_rect(ox, ZD, ox + ZL, ZD + LA, (214, 222, 190), (120, 130, 100), 1)
    lw_rect(ZL, 0, ZL + HA, 2 * ZD + LA, (220, 214, 236))
    d.line([lw(ZL, ZD + 0.45), lw(ZL + HA, ZD + 0.45)], fill=(70, 60, 50), width=SS)
    lw_rect(ZL, 2 * ZD + LA, ZL + HA, 2 * ZD + LA + BA, (196, 214, 226))
    # 注记
    fN, fS = font(12 * SS, True), font(10 * SS)
    def label(name, u, v, sub=None, big=False):
        X, Y = S.to_world(O, BR, u, v); p = P(float(X), float(Y))
        halo_text(d, p, name, font(13 * SS, True) if big else fN, w=3)
        if sub: halo_text(d, (p[0], p[1] + 14 * SS), sub, fS, fill=(80, 70, 56), w=2)
    def fl(n): return f'地坪 {FLOOR[n]:.1f}'
    DIM = {}
    for b in S.B:
        DIM[b['name']] = (f"{b['s'][0]:g}×{b['s'][1]:g}" if b['shape'] == 'rect' else f"Ø{2 * b['r']:g}")
    REL = {r['name']: r['relief'] for r in R['buildings']}
    LW_FLOORS = f"{FLOOR['生活翼·甲区']:.1f} / {FLOOR['生活翼·丙区']:.1f}"
    LW_REL = max(REL[n] for n in REL if n.startswith('生活翼'))
    ENTRIES = [  # 编号, 名称, 依据建筑, 注记位置 (u, v), 状态, 备注
        (1, '门楼', '门楼', (0, -131), '开放', '大道起点，面海'),
        (2, '誓约庭', '誓约庭', (0, -100), '室外', '铺石顺坡；第 7 日会场'),
        (3, '生活翼', None, (-47.8, -33), '开放', '竖厅加夹道，见生活翼平面'),
        (4, '后勤院', '后勤院', (34, -61), '开放', '厨房、食堂、配给台'),
        (5, '粮仓', '粮仓', (56, -70), '开放', '架高防潮'),
        (6, '中央厅', '中央厅', (0, 2), '开放', '穹顶约 16 m，四面门廊'),
        (7, '讲堂', '讲堂', (-30, 2), '开放', '阶梯座席顺坡'),
        (8, '名录库', '名录库', (0, 47), '开放', '后半埋入坡中；下有配重室'),
        (9, '文献工房', '文献工房', (16, 43), '开放', '文契组旧怨现场'),
        (10, '共鸣厅', '共鸣厅', (-72, 13), '开放', '净高约 9 m'),
        (11, '沉静室', '沉静室', (-56, 0), '开放', '星仪组旧怨现场'),
        (12, '诊疗所·安全端', '诊疗所与安全端', (56, -33), '开放', '后勤组旧怨现场'),
        (13, '温室', '温室', (100, 5), '开放', '三级台地'),
        (14, '汲水泉', '汲水泉', (104, -30), '开放', '泉亭与石槽'),
        (15, '晾晒场', '晾晒场', (72, -66), '室外', ''),
        (16, '文契院其余', '文契院（封闭部分）', (-18, 46), '封闭', '声纹记录室、契约审议室'),
        (17, '星盘讲堂', '星盘讲堂（封闭）', (-74, 30), '封闭', ''),
        (18, '观星塔', '观星塔（封闭）', (-90, 18), '封闭', ''),
        (19, '化学实验室', '化学实验室（封闭）', (58, -16), '封闭', ''),
        (20, '白塔', '白塔（封闭）', (74, -10), '封闭', '地标'),
        (21, '铁工院', '铁工院（封闭）', (110, -90), '封闭', '动力断联，烟囱不冒烟'),
    ]
    for num, nm, key, (u, v), st, note in ENTRIES:
        X, Y = S.to_world(O, BR, u, v); p = P(float(X), float(Y))
        r = 9 * SS
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=(250, 246, 236), outline=(70, 60, 50), width=SS)
        halo_text(d, p, str(num), font(10 * SS, True), w=0)
    # 一览表
    tx, ty = (W + 16) * SS, (MT + 4) * SS
    halo_text(d, (tx, ty), '建筑一览', font(16 * SS, True), anchor='la', w=0)
    cols = [(0, '号'), (30, '名称'), (150, '外廓 m'), (240, '地坪 m'), (318, '基底高差'), (402, '状态')]
    ty += 30 * SS
    for cx_, t in cols:
        halo_text(d, (tx + cx_ * SS, ty), t, font(11 * SS, True), anchor='la', w=0)
    ty += 20 * SS
    d.line([(tx, ty - 4 * SS), (tx + (TW - 40) * SS, ty - 4 * SS)], fill=(120, 110, 90), width=SS)
    rows = []
    for num, nm, key, uv, st, note in ENTRIES:
        if key is None:
            rows.append((num, nm, '54.8×35.4', LW_FLOORS, f'{LW_REL:.1f}', st, note))
        elif st == '室外':
            rows.append((num, nm, DIM[key], '—', '顺坡', st, note))
        else:
            rows.append((num, nm, DIM[key], f'{FLOOR[key]:.1f}', f'{REL[key]:.1f}', st, note))
    for row in rows:
        vals = [str(row[0]), row[1], row[2], row[3], row[4], row[5]]
        for (cx_, _), t in zip(cols, vals):
            halo_text(d, (tx + cx_ * SS, ty), t, font(11 * SS), anchor='la', w=0, fill=(40, 32, 22) if row[5] != '封闭' else (120, 112, 100))
        ty += 20 * SS
        if row[6]:
            halo_text(d, (tx + 30 * SS, ty), row[6], font(9 * SS), anchor='la', w=0, fill=(110, 100, 84))
            ty += 17 * SS
        ty += 3 * SS
    ty += 8 * SS
    for t in ('外廓：外墙轮廓，沿主轴横向 × 纵向；圆形为直径。', '地坪：基底最高点 + 台基高（海拔）。生活翼为北半 / 南半。',
              '基底高差：外廓内自然地面最高与最低之差，即台基外露的最大高度减台基高。'):
        halo_text(d, (tx, ty), t, font(9 * SS), anchor='la', w=0, fill=(90, 80, 64)); ty += 15 * SS
    hx, hy = QUAY[1]; halo_text(d, P(hx - 6, hy - 14), '港', font(13 * SS, True))
    halo_text(d, P(cx - 30, cy - 4), '港路塌方处', fS, fill=(150, 50, 40), w=2)
    # 主轴
    ax0 = S.to_world(O, BR, 0, 62); ax1 = S.to_world(O, BR, 0, 84)
    d.line([P(*ax0), P(*ax1)], fill=(120, 60, 40), width=2 * SS)
    tip = P(*ax1); halo_text(d, (tip[0] + 8 * SS, tip[1]), axis_note(), fS, fill=(120, 60, 40), w=2, anchor='lm')
    # 标题、图例、比例尺、指北、署名
    halo_text(d, (24 * SS, 30 * SS), '学园总平面（学园之岛南端熔岩台地；科尔沃岛地形缩为 0.5 倍）', font(20 * SS, True), anchor='la')
    halo_text(d, (24 * SS, 62 * SS), '等高距 1 m（每 5 m 加粗并注记，海拔米）；圆圈编号见右侧一览表；路上百分数为平均坡度', font(11 * SS), anchor='la')
    by = (H + MT + 24) * SS
    items = [((196, 176, 206), '生活翼'), ((196, 186, 168), '公共建筑'), ((214, 190, 150), '研究院开放部分'),
             ((190, 196, 176), '后勤与生产'), ((206, 202, 194), '封闭（斜线）'), ((214, 222, 190), '室外场地'), ((236, 228, 206), '碎石路')]
    lx = 24 * SS
    for col, t in items:
        d.rectangle([lx, by, lx + 16 * SS, by + 12 * SS], fill=col, outline=(90, 80, 60))
        halo_text(d, (lx + 22 * SS, by + 6 * SS), t, font(11 * SS), anchor='lm', w=0); lx += (34 + 13 * len(t)) * SS
    sb = 50
    bx, byy = 24 * SS, by + 34 * SS
    d.rectangle([bx, byy, bx + sb / 2 * K * SS, byy + 5 * SS], fill=(40, 32, 22))
    d.rectangle([bx + sb / 2 * K * SS, byy, bx + sb * K * SS, byy + 5 * SS], fill=(150, 140, 120))
    halo_text(d, (bx, byy + 16 * SS), '0', font(10 * SS), w=0); halo_text(d, (bx + sb * K * SS, byy + 16 * SS), f'{sb} m', font(10 * SS), w=0)
    nx, ny = (W + TW - 40) * SS, 50 * SS
    d.polygon([(nx, ny - 22 * SS), (nx + 9 * SS, ny + 10 * SS), (nx, ny + 4 * SS), (nx - 9 * SS, ny + 10 * SS)], fill=(40, 32, 22))
    halo_text(d, (nx, ny + 24 * SS), '北', font(12 * SS, True))
    halo_text(d, (24 * SS, (H + MT + MB - 34) * SS), '地形：' + ATTRIBUTION, font(9 * SS), anchor='la', w=0)
    halo_text(d, (24 * SS, (H + MT + MB - 18) * SS), '说明：30 m 数字表面模型经平滑与插值得到 1 m 等高线，台地上现实村落的房屋略抬高了局部地面；标高为概念级，施工图须实测。', font(9 * SS), anchor='la', w=0)
    img = img.resize((W + TW, H + MT + MB), Image.LANCZOS)
    img.save(os.path.join(HERE, 'campus_plan.png'))
    R['paths'] = path_out; R['harbor_road'] = road; R['quay'] = QUAY
    json.dump(R, open(os.path.join(HERE, 'site.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    return path_out, road

def section():
    """主轴剖面：从海边经学园到全岛最高点（水平 1:10000 量级，竖向放大 4 倍），另附学园段放大。"""
    Wp, Hp = 1500, 760
    img = Image.new('RGB', (Wp * 2, Hp * 2), (246, 242, 232)); d = ImageDraw.Draw(img)
    def panel(v0, v1, top, height, zmax, ex, title, show_buildings, line=None):
        vv = np.linspace(v0, v1, 1600)
        if line is None:
            X, Y = S.to_world(O, BR, np.zeros_like(vv), vv)
        else:
            (ax, ay), (bx_, by_) = line; L = np.hypot(bx_ - ax, by_ - ay)
            X, Y = ax + (bx_ - ax) / L * vv, ay + (by_ - ay) / L * vv
        z = S.ground(X, Y); z = np.where(S.is_land(X, Y), z, 0)
        l, r = 90, Wp - 40
        def Q(v, h): return ((l + (v - v0) / (v1 - v0) * (r - l)) * 2, (top + height - h / zmax * height) * 2)
        d.polygon([Q(v0, 0)] + [Q(a, b) for a, b in zip(vv, z)] + [Q(v1, 0)], fill=(214, 204, 178), outline=(110, 90, 70))
        d.line([Q(v0, 0), Q(v1, 0)], fill=(90, 120, 140), width=4)
        for h in range(0, int(zmax) + 1, int(zmax / 5)):
            d.line([Q(v0, h), (Q(v0, h)[0] - 10, Q(v0, h)[1])], fill=(90, 80, 60), width=2)
            halo_text(d, (Q(v0, h)[0] - 16, Q(v0, h)[1]), f'{h}', font(20), anchor='rm', w=0)
        halo_text(d, (l * 2, (top - 22) * 2), title, font(26, True), anchor='la', w=0)
        if show_buildings:
            for b in S.B:
                if b['shape'] != 'rect' and b['name'] != '中央厅':
                    continue
                cu, cv = b['c']
                half = (b['s'][1] / 2) if b['shape'] == 'rect' else b['r']
                if abs(cu) > (b['s'][0] / 2 if b['shape'] == 'rect' else b['r']) or b['kind'] == 'outdoor':
                    continue                                    # 只画被主轴穿过的建筑
                f = FLOOR[b['name']]
                hgt = {'中央厅': 16, '门楼': 9, '名录库': 6}.get(b['name'], 5)
                vs_ = np.linspace(cv - half, cv + half, 40)
                Xb, Yb = S.to_world(O, BR, np.zeros_like(vs_), vs_); gz = S.ground(Xb, Yb)
                d.polygon([Q(a, f) for a in vs_] + [Q(a, g) for a, g in zip(vs_[::-1], gz[::-1])], fill=(150, 140, 126))   # 台基
                xa, ya = Q(cv - half, f); xb, yb = Q(cv + half, f + hgt)
                d.rectangle([xa, yb, xb, ya], outline=(60, 50, 40), width=3, fill=(232, 224, 210))
                if '门廊' not in b['name']:
                    halo_text(d, ((xa + xb) / 2, yb - 16), b['name'], font(20, True), w=0)
                    halo_text(d, ((xa + xb) / 2, (ya + yb) / 2), f'地坪 {f:.1f}', font(18), w=0)
    Lsum = float(np.hypot(S.SUMMIT[0] - O[0], S.SUMMIT[1] - O[1]))
    panel(-260, Lsum, 70, 260 * 720 / 720, 720, 1, f'剖面一：中央厅—全岛最高点连线，自海边至山顶（{Lsum / 1000:.1f} km，竖向放大约 {(260 / 720) / ((Wp - 130) / (Lsum + 260)):.1f} 倍）', False,
          line=((O[0], O[1]), (S.SUMMIT[0], S.SUMMIT[1])))
    halo_text(d, ((Wp - 40) * 2, 70 * 2 + 10), f'▲ 最高点 {S.SUMMIT[2]:.0f} m（火口西南缘）', font(22, True), anchor='ra', w=0)
    halo_text(d, ((90 + 260 / (Lsum + 260) * (Wp - 130)) * 2, (70 + 260) * 2 - 70), '学园', font(22, True), w=0)
    panel(-140, 70, 430, 280, 30, 1, '剖面二：学园段（门楼—誓约庭—大道—中央厅—名录库），竖向放大约 7 倍', True)
    halo_text(d, (180, (Hp - 20) * 2), '水平距离以中央厅为 0，向坡上为正；高程为海拔（米）', font(20), anchor='la', w=0)
    img.resize((Wp, Hp), Image.LANCZOS).save(os.path.join(HERE, 'campus_section.png'))

if __name__ == '__main__':
    paths, road = main()
    section()
    for p in paths: print(p['name'], [(s['grade'], s['max_local'], s['land']) for s in p['segments']])
    print('港路', [(s['grade'], s['max_local'], s['land']) for s in road])
