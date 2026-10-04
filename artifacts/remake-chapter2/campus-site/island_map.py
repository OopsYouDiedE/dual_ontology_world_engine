# 岛屿地形图：分层设色 + 山体阴影 + 等高线 + 低缓地标示 + 地名。
# 运行：python island_map.py corvo|faial → island_<key>.png
import sys, os
import numpy as np
from PIL import Image, ImageDraw
from dem import island, sample, blur, to_xy, ATTRIBUTION, HERE
from cartography import contour_segments, hillshade, font, halo_text

CFG = {
    'corvo': dict(px_per_m=0.4, interval=10, index=50, name='学园之岛（科尔沃岛真实地形，三向等比缩为 0.5 倍）',
                  labels=[('crater', '火口 Caldeirão'), ('南端熔岩台地', 39.6700, -31.1030),
                          ('north', '北岸海崖'), ('西岸海崖', 39.7000, -31.1255)], ray=-55),
    'faial': dict(px_per_m=0.07, interval=50, index=250, name='法亚尔岛（Faial，亚速尔群岛）',
                  ray=225, labels=[('crater', '破火山口 Caldeira'), ('卡佩卢半岛 Capelo', 38.5900, -28.8050),
                          ('佩德罗·米格尔地堑', 38.5640, -28.6500), ('奥尔塔台地 Horta', 38.5420, -28.6420),
                          ('吉亚山 Monte da Guia', 38.5170, -28.6255)]),
}
TINT = [(0, (196, 204, 168)), (50, (176, 190, 146)), (150, (160, 172, 126)), (300, (170, 164, 120)),
        (500, (172, 150, 112)), (700, (160, 138, 112)), (1050, (196, 186, 170))]
SEA = (168, 190, 196)

def depressions(I, depth=20.0):
    """优先队列填洼（只用于找火口）：返回积水深度超过 depth 的格点。"""
    import heapq
    Z = np.where(I['land'], I['Z'], 0.0); H, W = Z.shape
    F = np.full(Z.shape, np.inf); seen = np.zeros(Z.shape, bool); q = []
    for i in range(H):
        for j in (0, W - 1):
            F[i, j] = Z[i, j]; seen[i, j] = True; heapq.heappush(q, (Z[i, j], i, j))
    for j in range(W):
        for i in (0, H - 1):
            if not seen[i, j]:
                F[i, j] = Z[i, j]; seen[i, j] = True; heapq.heappush(q, (Z[i, j], i, j))
    while q:
        h, i, j = heapq.heappop(q)
        for a, b in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
            if 0 <= a < H and 0 <= b < W and not seen[a, b]:
                seen[a, b] = True; F[a, b] = max(Z[a, b], h); heapq.heappush(q, (F[a, b], a, b))
    return (F - Z) > depth

def render(key):
    import campus as _C
    c = CFG[key]; I = island(key, _C.SCALE if key == 'corvo' else 1.0)
    k = c['px_per_m']; SS = 2                                  # 超采样
    x0, x1, y0, y1 = I['xs'][0], I['xs'][-1], I['ys'][-1], I['ys'][0]
    W, H = int((x1 - x0) * k), int((y1 - y0) * k)
    gx = x0 + (np.arange(W) + 0.5) / k
    gy = y1 - (np.arange(H) + 0.5) / k
    GX, GY = np.meshgrid(gx, gy)
    Z = sample(I, GX, GY)
    land = sample(dict(I, Z=I['land'].astype(np.float32)), GX, GY) > 0.5
    Zs = blur(np.where(land, Z, -5), 2)
    step = 1 / k
    # 设色与阴影
    hs = np.array([t[0] for t in TINT]); cs = np.array([t[1] for t in TINT], float)
    rgb = np.stack([np.interp(Zs, hs, cs[:, i]) for i in range(3)], -1)
    shade = hillshade(Zs, step, zscale=1.0)
    rgb = rgb * (0.50 + 0.62 * shade[..., None])
    rgb[~land] = SEA
    # 低缓地：坡度 ≤ 6%、海拔 3—120 m
    gyy, gxx = np.gradient(Zs, step)
    slope = np.hypot(gxx, gyy)
    sc = I.get('scale', 1.0)
    gentle = land & (slope <= 0.06) & (Zs >= 3 * sc) & (Zs <= 120 * sc)
    rgb[gentle] = rgb[gentle] * 0.55 + np.array([236, 196, 120]) * 0.45
    img = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).resize((W * SS, H * SS), Image.BICUBIC)
    d = ImageDraw.Draw(img)
    def P(x, y): return ((x - x0) * k * SS, (y1 - y) * k * SS)
    # 等高线
    lv = np.arange(c['interval'], Zs.max(), c['interval'])
    for L in lv:
        segs = contour_segments(gx, gy, np.where(land, Zs, -5), L)
        idx = L % c['index'] == 0
        col, wd = ((96, 74, 52), 3) if idx else ((120, 100, 78), 1)
        for s in segs:
            d.line([P(s[0], s[1]), P(s[2], s[3])], fill=col, width=wd)
    for s in contour_segments(gx, gy, blur(land.astype(float), 2), 0.5):    # 海岸线
        d.line([P(s[0], s[1]), P(s[2], s[3])], fill=(52, 64, 72), width=4)
    # 指数等高线注记：沿一条由海岸到最高点的剖线放置
    iy, ix = np.unravel_index(np.argmax(np.where(land, Zs, -1)), Zs.shape)
    top = (gx[ix], gy[iy])
    f_s, f_m, f_l = font(13 * SS), font(16 * SS, True), font(22 * SS, True)
    halo_text(d, P(*top), f'▲ {Zs[iy, ix]:.0f} m', f_s, anchor='lb')
    for spec in c['labels']:
        if spec[0] == 'crater':                      # 火口：原始格网上填洼后积水深 > 20 m 的洼地形心
            dep = depressions(I, 20.0 * I.get('scale', 1.0))
            x, y = I['xs'][np.where(dep.any(0))[0]].mean(), I['ys'][np.where(dep.any(1))[0]].mean()
            lab = spec[1]
        elif spec[0] == 'north':
            rows = np.where(land.any(1))[0]; cols = np.where(land[rows[0]])[0]
            x, y = gx[cols].mean(), gy[rows[0]] - 250 * I.get('scale', 1.0); lab = spec[1]
        else:
            lab, (x, y) = spec[0], to_xy(I, spec[1], spec[2])
        halo_text(d, P(x, y), lab, f_m)
    # 指数等高线数字：沿最高点出发的一条剖线，每条只注一次
    for ang in np.radians([c['ray']]):
        for L in lv[lv % c['index'] == 0]:
            rr = np.linspace(0, max(W, H) / k, 3000)
            px, py = top[0] + rr * np.cos(ang), top[1] + rr * np.sin(ang)
            ok = (px > gx[0]) & (px < gx[-1]) & (py > gy[-1]) & (py < gy[0])
            if not ok.any():
                continue
            zz = sample(dict(I, Z=np.where(I['land'], I['Z'], -5)), px[ok], py[ok])
            hit = np.where(np.diff(np.sign(zz - L)) != 0)[0]
            if len(hit) and rr[ok][hit[0]] > 60 / k:
                j = hit[0]
                halo_text(d, P(px[ok][j], py[ok][j]), f'{L:.0f}', f_s)
    if key == 'corvo' and os.path.exists(os.path.join(HERE, 'site.json')):      # 学园位置
        import json, campus as CS
        R = json.load(open(os.path.join(HERE, 'site.json'), encoding='utf-8'))
        Oc, br = tuple(R['origin_world']), R['bearing_deg']
        for b in CS.B:
            if b['shape'] != 'rect':
                continue
            (cu, cv), (su, sv) = b['c'], b['s']
            X, Y = CS.to_world(Oc, br, [cu - su / 2, cu + su / 2, cu + su / 2, cu - su / 2], [cv - sv / 2, cv - sv / 2, cv + sv / 2, cv + sv / 2])
            d.polygon([P(float(a), float(b_)) for a, b_ in zip(X, Y)], fill=(120, 60, 40))
        if 'harbor_road' in R:
            import render_site as RS
            d.line([P(*q) for q in RS.HARBOR_ROAD], fill=(120, 60, 40), width=2 * SS)
        halo_text(d, P(Oc[0] - 170, Oc[1] + 20), '学园', font(15 * SS, True), fill=(120, 60, 40))
    # 标题、比例尺、指北、图例、署名
    halo_text(d, (24 * SS, 24 * SS), c['name'], f_l, anchor='la')
    halo_text(d, (24 * SS, 58 * SS), f'等高距 {c["interval"]} m，加粗 {c["index"]} m；黄色：坡度 ≤ 6% 的低缓地', font(13 * SS), anchor='la')
    bar = 500 if key == 'corvo' else 5000
    bx, by = 24 * SS, (H - 60) * SS
    d.rectangle([bx, by, bx + bar * k * SS / 2, by + 6 * SS], fill=(40, 32, 22))
    d.rectangle([bx + bar * k * SS / 2, by, bx + bar * k * SS, by + 6 * SS], fill=(150, 140, 120))
    halo_text(d, (bx, by + 18 * SS), '0', font(12 * SS), anchor='mm')
    halo_text(d, (bx + bar * k * SS, by + 18 * SS), (f'{bar // 1000} km' if bar >= 1000 else f'{bar} m'), font(12 * SS), anchor='mm')
    nx, ny = (W - 40) * SS, 40 * SS
    d.polygon([(nx, ny - 22 * SS), (nx + 9 * SS, ny + 10 * SS), (nx, ny + 4 * SS), (nx - 9 * SS, ny + 10 * SS)], fill=(40, 32, 22))
    halo_text(d, (nx, ny + 24 * SS), '北', font(13 * SS, True))
    halo_text(d, (24 * SS, (H - 22) * SS), ATTRIBUTION, font(10 * SS), anchor='la', w=2)
    img = img.resize((W, H), Image.LANCZOS)
    out = os.path.join(HERE, f'island_{key}.png'); img.save(out)
    # 统计
    area = land.sum() * step * step / 1e6
    return dict(key=key, size_km=((x1 - x0) / 1000, (y1 - y0) / 1000), land_km2=round(area, 2),
                top_m=round(float(Zs[iy, ix]), 1), gentle_ha=round(gentle.sum() * step * step / 1e4, 1), out=out)

if __name__ == '__main__':
    for key in (sys.argv[1:] or ['corvo', 'faial']):
        print(render(key))
