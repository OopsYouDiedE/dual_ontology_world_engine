# 学园总体布局：在科尔沃岛南端熔岩台地上，以真实地形为据选址、定轴，并验算每栋楼的地坪与基础高差、每条路的坡度。
# 运行：python campus.py → site.json（布局与验算结果）
# 坐标：世界坐标见 dem.py（米，x 东 y 北）。学园局部坐标 (u, v)：v 沿主轴指向坡上，u 垂直于主轴指向右手。
import json, os
import numpy as np
from dem import island, sample, blur, HERE

SCALE = float(os.environ.get('ISLAND_SCALE', json.load(open(os.path.join(HERE, 'scale.json'), encoding='utf-8')).get('scale', 1.0) if os.path.exists(os.path.join(HERE, 'scale.json')) else 1.0))
I = island('corvo', SCALE)
_land = blur(I['land'].astype(np.float32), 2)
_Z = blur(np.where(I['land'], I['Z'], -3.0), 2)                  # 30 m 格网平滑后双线性插值（制图与估算用）
TERR = dict(I, Z=_Z); LANDF = dict(I, Z=_land)
def ground(x, y): return sample(TERR, x, y)
def is_land(x, y): return sample(LANDF, x, y) > 0.5

# ---------------------------------------------------------------- 建筑（局部坐标，尺寸 = (沿 u, 沿 v)）
# kind: open 第二章开放 / closed 封闭 / outdoor 室外场地（不计基础）
B = []
def rect(name, c, s, cat, kind='open', plinth=0.45, note=''):
    B.append(dict(name=name, shape='rect', c=c, s=s, cat=cat, kind=kind, plinth=plinth, note=note))
def circ(name, c, r, cat, kind='open', plinth=0.6, note=''):
    B.append(dict(name=name, shape='circ', c=c, r=r, cat=cat, kind=kind, plinth=plinth, note=note))

circ('中央厅', (0, 0), 12, 'core', note='穹顶约 16 m，四面门廊')
for nm, c, s in (('中央厅北门廊', (0, 14.5), (7, 5)), ('中央厅南门廊', (0, -14.5), (7, 5)),
                 ('中央厅东门廊', (14.5, 0), (5, 7)), ('中央厅西门廊', (-14.5, 0), (5, 7))):
    rect(nm, c, s, 'core', plinth=0.6)
rect('讲堂', (-30, 0), (20, 14), 'core', note='阶梯座席顺坡，讲台在低端')
rect('风雨廊', (-20.5, 0), (6, 3), 'core', plinth=0.3)
rect('名录库', (0, 46), (16, 20), 'academy', note='后半埋入坡中；前厅阶梯下到配重室')
rect('文献工房', (16, 42), (12, 12), 'academy')
rect('文契院（封闭部分）', (-18, 46), (16, 22), 'academy', 'closed')
rect('共鸣厅', (-72, 12), (18, 12), 'academy', note='净高约 9 m')
rect('沉静室', (-59.5, 8.5), (5, 5), 'academy')
rect('星盘讲堂（封闭）', (-74, 30), (20, 12), 'academy', 'closed')
circ('观星塔（封闭）', (-90, 18), 5, 'academy', 'closed')
rect('诊疗所与安全端', (56, -34), (22, 10), 'academy')
rect('化学实验室（封闭）', (58, -16), (18, 12), 'academy', 'closed')
circ('白塔（封闭）', (74, -10), 5, 'academy', 'closed')
rect('后勤院', (34, -62), (28, 20), 'service', note='厨房、食堂、配给台')
rect('粮仓', (56, -60), (8, 8), 'service', plinth=0.9, note='石墙，架高防潮')
rect('誓约庭', (0, -100), (24, 30), 'outdoor', 'outdoor', plinth=0, note='铺石顺坡')
rect('门楼', (0, -118.5), (14, 7), 'core', plinth=0.3)
rect('铁工院（封闭）', (110, -90), (40, 25), 'service', 'closed')
rect('温室', (100, 5), (10, 30), 'service', note='顺坡分三级')
rect('汲水泉', (96, -28), (4, 4), 'outdoor', plinth=0.2)
rect('晾晒场', (72, -66), (16, 12), 'outdoor', 'outdoor', plinth=0)
# 生活翼（竖厅加夹道，见 living-wing-estimate/plan.py）：北缘 v=-42，西缘 u=-75.2
LW = dict(u0=-75.2, v0=-42.0, ZL=22.4, ZD=13.2, LANE=3.0, HALL=10.0, BATH=6.0)
def lw_blocks():
    u0, v0, ZL, ZD, LA, HA, BA = (LW[k] for k in ('u0', 'v0', 'ZL', 'ZD', 'LANE', 'HALL', 'BATH'))
    uw, uh, ue = u0 + ZL / 2, u0 + ZL + HA / 2, u0 + ZL + HA + ZL / 2
    vn, vs = v0 - ZD / 2, v0 - ZD - LA - ZD / 2
    return [('甲区', (uw, vn), (ZL, ZD), 'N'), ('丁区', (ue, vn), (ZL, ZD), 'N'),
            ('丙区', (uw, vs), (ZL, ZD), 'S'), ('乙区', (ue, vs), (ZL, ZD), 'S'),
            ('起居厅北台', (uh, v0 - ZD / 2), (HA, ZD), 'N'),
            ('起居厅南台', (uh, v0 - ZD - (LA + ZD) / 2), (HA, LA + ZD), 'S'),
            ('浴室盥洗', (uh, v0 - 2 * ZD - LA - BA / 2), (HA, BA), 'B')]
for nm, c, s, grp in lw_blocks():
    rect('生活翼·' + nm, c, s, 'living', plinth=0.3, note=grp)

# ---------------------------------------------------------------- 路（局部坐标折线）
PATHS = [
    ('主轴大道', [(0, -115), (0, -85), (0, -60), (0, -17)], 8),
    ('名录库前路', [(0, 17), (0, 36)], 4),
    ('生活翼北门—中央厅', [(-47.8, -42), (-47.8, -30), (-10, -10)], 3),
    ('生活翼东门—大道', [(-20.4, -56.7), (-4, -56.7)], 3),
    ('大道—后勤院', [(4, -62), (20, -62)], 3),
    ('后勤院—诊疗所', [(42, -52), (50, -39)], 2.5),
    ('中央厅—白塔院', [(17, 0), (40, -4), (49, -10)], 3),
    ('讲堂—星仪院', [(-40, 0), (-56, 6)], 3),
    ('生活翼西门—星仪院', [(-75.2, -56.7), (-86, -50), (-86, 0), (-80, 6)], 2.5),
    ('后勤院后门—晾晒场—汲水泉—温室', [(48, -62), (64, -64), (88, -46), (96, -30), (100, -12)], 2),
    ('后勤院—铁工院', [(48, -70), (80, -84), (90, -88)], 4),
]

# ---------------------------------------------------------------- 变换
def basis(bearing_deg):
    b = np.radians(bearing_deg)
    ev = np.array([np.sin(b), np.cos(b)]); eu = np.array([np.cos(b), -np.sin(b)])
    return eu, ev
def to_world(O, bearing, u, v):
    eu, ev = basis(bearing)
    u = np.asarray(u, float); v = np.asarray(v, float)
    return O[0] + u * eu[0] + v * ev[0], O[1] + u * eu[1] + v * ev[1]

def footprint_points(b, step):
    if b['shape'] == 'rect':
        (cu, cv), (su, sv) = b['c'], b['s']
        us = np.linspace(cu - su / 2, cu + su / 2, max(2, int(su / step) + 1))
        vs = np.linspace(cv - sv / 2, cv + sv / 2, max(2, int(sv / step) + 1))
        U, V = np.meshgrid(us, vs); return U.ravel(), V.ravel()
    (cu, cv), r = b['c'], b['r']
    us = np.arange(cu - r, cu + r + 0.01, step); U, V = np.meshgrid(us, us - cu + cv)
    m = np.hypot(U - cu, V - cv) <= r
    return U[m], V[m]

PTS = {b['name']: footprint_points(b, 4.0) for b in B}
SAFE = [(du, dv) for du in (-25, 0, 25) for dv in (-25, 0, 25)]

_i = np.unravel_index(np.argmax(np.where(I['land'], I['Z'], -1)), I['Z'].shape)
SUMMIT = (float(I['xs'][_i[1]]), float(I['ys'][_i[0]]), float(I['Z'][_i]))     # 全岛最高点（火口西南缘）
def summit_bearing(O):
    return np.degrees(np.arctan2(SUMMIT[0] - O[0], SUMMIT[1] - O[1]))

def evaluate(O, bearing, full=False):
    cost, res = 0.0, []
    for b in B:
        U, V = PTS[b['name']] if not full else footprint_points(b, 1.0)
        X, Y = to_world(O, bearing, U, V)
        z = ground(X, Y)
        land = is_land(X, Y)
        if not land.all() or z.min() < 6:
            cost += 1e6
        rel = float(z.max() - z.min())
        area = b['s'][0] * b['s'][1] if b['shape'] == 'rect' else np.pi * b['r'] ** 2
        if b['kind'] != 'outdoor':
            cost += area * rel ** 2
        if full:
            res.append(dict(name=b['name'], ground_min=round(float(z.min()), 2), ground_max=round(float(z.max()), 2),
                            relief=round(rel, 2), floor=round(float(z.max()) + b['plinth'], 2)))
    # 海岸退让：主要建筑四周 25 m 须为陆地
    for nm in ('门楼', '铁工院（封闭）', '观星塔（封闭）', '温室'):
        b = next(x for x in B if x['name'] == nm)
        cu, cv = b['c']
        X, Y = to_world(O, bearing, [cu + du for du, dv in SAFE], [cv + dv for du, dv in SAFE])
        if not is_land(X, Y).all():
            cost += 1e6
    # 主轴大道坡度：超过 8% 的部分需设台阶，计入成本
    X, Y = to_world(O, bearing, [0, 0], [-115, -17])
    g = abs(float(ground(X[1], Y[1]) - ground(X[0], Y[0]))) / 98
    cost += 4e4 * max(0, g - 0.08)
    cost += 150 * abs(((bearing - summit_bearing(O)) + 180) % 360 - 180)  # 主轴指向全岛最高点（景观）
    return (cost, res) if full else cost

def search():
    best = None
    for bearing in range(-30, 41, 5):
        for x in range(int(-1450 * SCALE) - 150, int(-850 * SCALE) + 151, 20):
            for y in range(int(-3150 * SCALE) - 100, int(-2650 * SCALE) + 151, 20):
                c = evaluate((x, y), bearing)
                if best is None or c < best[0]:
                    best = (c, (x, y), bearing)
    c, (x0, y0), b0 = best
    for bearing in np.arange(b0 - 5, b0 + 5.1, 1):
        for x in range(x0 - 20, x0 + 21, 4):
            for y in range(y0 - 20, y0 + 21, 4):
                c = evaluate((x, y), bearing)
                if c < best[0]:
                    best = (c, (x, y), float(bearing))
    return best

def path_report(O, bearing):
    out = []
    for name, pts, width in PATHS:
        U = np.array([p[0] for p in pts], float); V = np.array([p[1] for p in pts], float)
        segs = []
        for i in range(len(pts) - 1):
            n = max(2, int(np.hypot(U[i + 1] - U[i], V[i + 1] - V[i]) / 2))
            uu, vv = np.linspace(U[i], U[i + 1], n), np.linspace(V[i], V[i + 1], n)
            X, Y = to_world(O, bearing, uu, vv); z = ground(X, Y)
            L = np.hypot(U[i + 1] - U[i], V[i + 1] - V[i])
            segs.append(dict(length=round(float(L), 1), rise=round(float(z[-1] - z[0]), 2),
                             grade=round(float(abs(z[-1] - z[0]) / L), 3),
                             max_local=round(float(np.max(np.abs(np.diff(z)) / (L / (n - 1)))), 3)))
        out.append(dict(name=name, width=width, segments=segs))
    return out

if __name__ == '__main__':
    c, O, bearing = search()
    cost, res = evaluate(O, bearing, full=True)
    out = dict(origin_world=O, bearing_deg=bearing, summit=SUMMIT, summit_bearing_deg=round(summit_bearing(O), 1), cost=round(cost),
               buildings=res, paths=path_report(O, bearing))
    json.dump(out, open(os.path.join(HERE, 'site.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('origin', O, 'bearing', bearing, 'summit', SUMMIT, 'summit bearing', out['summit_bearing_deg'], 'cost', round(cost))
    for r in res: print(f"  {r['name']:<16} 地面 {r['ground_min']:6.2f}—{r['ground_max']:6.2f}  高差 {r['relief']:5.2f}  地坪 {r['floor']:6.2f}")
    for p in out['paths']: print('  ', p['name'], [(s['grade'], s['max_local']) for s in p['segments']])
