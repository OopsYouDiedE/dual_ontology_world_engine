# 导出三维场景数据（系统 Python）：地形高程网格 terrain.npz 与建筑、道路几何 scene.json，供 Blender 脚本 build_3d.py 读取。
# 运行：python export_scene.py
import json, os
import numpy as np
import campus as S
import render_site as RS
from dem import HERE

O = tuple(json.load(open(os.path.join(HERE, 'site.json'), encoding='utf-8'))['origin_world'])
BR = json.load(open(os.path.join(HERE, 'site.json'), encoding='utf-8'))['bearing_deg']
SITE = json.load(open(os.path.join(HERE, 'site.json'), encoding='utf-8'))
FLOOR = {b['name']: b for b in SITE['buildings']}

# ---- 地形：全岛 10 m 网格（海面以下取平滑后的负值，供海床着色）
xs = np.arange(S.I['xs'][0] + 60, S.I['xs'][-1] - 60, 10.0)
ys = np.arange(S.I['ys'][-1] + 60, S.I['ys'][0] - 60, 10.0)
GX, GY = np.meshgrid(xs, ys)
Z = S.ground(GX, GY).astype(np.float32)
LAND = (S.sample(S.LANDF, GX, GY) > 0.5)
Z = np.where(LAND, Z, np.minimum(Z, -0.5)).astype(np.float32)
np.savez_compressed(os.path.join(HERE, 'terrain.npz'), xs=xs, ys=ys, z=Z)

# ---- 建筑：高度与屋顶形式
SPEC = {   # 名称: (檐高 m, 屋顶, 材质)
    '中央厅': (9.0, 'dome', 'basalt'), '讲堂': (6.0, 'gable', 'basalt'), '风雨廊': (3.0, 'flat', 'basalt'),
    '名录库': (5.5, 'hip', 'basalt'), '文献工房': (4.5, 'gable', 'basalt'), '文契院（封闭部分）': (4.5, 'gable', 'basalt'),
    '共鸣厅': (9.0, 'gable', 'basalt'), '沉静室': (3.5, 'flat', 'basalt'), '星盘讲堂（封闭）': (5.0, 'gable', 'basalt'),
    '观星塔（封闭）': (12.0, 'dome', 'basalt'), '诊疗所与安全端': (4.2, 'gable', 'plaster'), '化学实验室（封闭）': (4.5, 'gable', 'plaster'),
    '白塔（封闭）': (22.0, 'cone', 'white'), '后勤院': (4.5, 'gable', 'plaster'), '粮仓': (4.0, 'hip', 'basalt'),
    '门楼': (7.0, 'gate', 'basalt'), '铁工院（封闭）': (7.0, 'gable', 'brick'), '温室': (3.5, 'glass', 'glass'),
    '汲水泉': (2.8, 'hip', 'basalt'),
}
for nm in ('中央厅北门廊', '中央厅南门廊', '中央厅东门廊', '中央厅西门廊'):
    SPEC[nm] = (6.0, 'gable', 'basalt')
LW_SPEC = {'甲区': (4.2, 'gable', 'plaster'), '丁区': (4.2, 'gable', 'plaster'), '丙区': (4.2, 'gable', 'plaster'),
           '乙区': (4.2, 'gable', 'plaster'), '起居厅北台': (7.0, 'hall', 'plaster'), '起居厅南台': (7.0, 'hall', 'plaster'),
           '浴室盥洗': (3.6, 'hip', 'plaster')}

eu, ev = S.basis(BR)
def world_rect(c, s):
    (cu, cv), (su, sv) = c, s
    corners = [(cu - su / 2, cv - sv / 2), (cu + su / 2, cv - sv / 2), (cu + su / 2, cv + sv / 2), (cu - su / 2, cv + sv / 2)]
    X, Y = S.to_world(O, BR, [p[0] for p in corners], [p[1] for p in corners])
    return [[float(x), float(y)] for x, y in zip(X, Y)]

buildings = []
for b in S.B:
    nm = b['name']
    f = FLOOR[nm]
    entry = dict(name=nm, kind=b['kind'], cat=b['cat'], shape=b['shape'], floor=f['floor'], ground_min=f['ground_min'],
                 ground_max=f['ground_max'])
    cx, cy = S.to_world(O, BR, b['c'][0], b['c'][1])
    entry['center'] = [float(cx), float(cy)]
    if b['shape'] == 'rect':
        entry['corners'] = world_rect(b['c'], b['s']); entry['size'] = list(b['s'])
    else:
        entry['radius'] = b['r']
    if nm.startswith('生活翼·'):
        key = nm.split('·')[1]; entry['height'], entry['roof'], entry['mat'] = LW_SPEC[key]
        # 寝区屋脊沿 u（顺等高线），起居厅屋脊沿 v（沿主轴）
        entry['ridge'] = 'v' if key.startswith('起居厅') else 'u'
    elif b['kind'] == 'outdoor':
        entry['height'], entry['roof'], entry['mat'] = 0, 'none', 'paving'
    else:
        entry['height'], entry['roof'], entry['mat'] = SPEC[nm]
        entry['ridge'] = 'u' if b['shape'] == 'rect' and b['s'][0] >= b['s'][1] else 'v'
    buildings.append(entry)
# 起居厅南北两台连成一体屋顶：取两台中较高地坪
paths = []
for name, pts, width in RS.S.PATHS:
    X, Y = S.to_world(O, BR, [p[0] for p in pts], [p[1] for p in pts])
    paths.append(dict(name=name, width=width, pts=[[float(x), float(y)] for x, y in zip(X, Y)]))
paths.append(dict(name='港路', width=5, pts=[list(map(float, p)) for p in RS.HARBOR_ROAD]))
scene = dict(origin=O, bearing=BR, eu=eu.tolist(), ev=ev.tolist(), buildings=buildings, paths=paths,
             quay=[list(map(float, p)) for p in RS.QUAY], summit=list(S.SUMMIT))
json.dump(scene, open(os.path.join(HERE, 'scene.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('terrain', Z.shape, 'buildings', len(buildings), 'paths', len(paths))
