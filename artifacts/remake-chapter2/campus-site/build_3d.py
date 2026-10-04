# 学园三维模型与渲染（Blender 4.5，在 Blender 内运行）。
# 运行：blender -b -P build_3d.py -- [相机名 ...] [--samples N] [--res 1600x900] [--save]
# 读取 export_scene.py 生成的 terrain.npz 与 scene.json；输出 renders/<相机>.png 与 renders/<相机>_labels.json（供 label_renders.py 叠加注记）。
import bpy, bmesh, json, math, os, sys
import numpy as np
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'renders'); os.makedirs(OUT, exist_ok=True)
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def opt(name, default):
    return argv[argv.index(name) + 1] if name in argv else default
SAMPLES = int(opt('--samples', 32))
RES = tuple(int(v) for v in opt('--res', '1600x900').split('x'))
CAMS = [a for a in argv if not a.startswith('--') and not a.replace('x', '').isdigit()] or ['aerial', 'avenue', 'living', 'top', 'island']

SC = json.load(open(os.path.join(HERE, 'scene.json'), encoding='utf-8'))
T = np.load(os.path.join(HERE, 'terrain.npz'))
TX, TY, TZ = T['xs'], T['ys'], T['z']
EU, EV = Vector((*SC['eu'], 0)), Vector((*SC['ev'], 0))
O = Vector((*SC['origin'], 0))

def ground(x, y):
    fx = np.interp(x, TX, np.arange(len(TX))); fy = np.interp(y, TY, np.arange(len(TY)))
    i0, j0 = int(min(np.floor(fy), len(TY) - 2)), int(min(np.floor(fx), len(TX) - 2))
    ty, tx = fy - i0, fx - j0
    z = TZ[i0:i0 + 2, j0:j0 + 2]
    return float((z[0, 0] * (1 - tx) + z[0, 1] * tx) * (1 - ty) + (z[1, 0] * (1 - tx) + z[1, 1] * tx) * ty)

# ------------------------------------------------------------------ 场景基础
bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
scn.unit_settings.system = 'METRIC'
scn.render.engine = 'CYCLES'
scn.cycles.device = 'CPU'
scn.cycles.samples = SAMPLES
scn.cycles.use_adaptive_sampling = True
scn.cycles.use_denoising = True
scn.cycles.max_bounces = 6
scn.render.resolution_x, scn.render.resolution_y = RES
scn.render.film_transparent = False
scn.view_settings.exposure = -0.2
scn.view_settings.view_transform = 'AgX'
try:
    scn.view_settings.look = 'AgX - Medium High Contrast'
except Exception:
    pass

world = bpy.data.worlds.new('天空'); scn.world = world; world.use_nodes = True
wn = world.node_tree.nodes; wl = world.node_tree.links
bg = wn['Background']
SUN_ELEV, SUN_AZ = math.radians(32), math.radians(215)          # 午后偏西南的太阳
try:
    sky = wn.new('ShaderNodeTexSky'); sky.sky_type = 'NISHITA'
    sky.sun_elevation, sky.sun_rotation = SUN_ELEV, SUN_AZ
    sky.air_density, sky.dust_density = 1.0, 1.5
    sky.sun_disc = False
    wl.new(sky.outputs['Color'], bg.inputs['Color']); bg.inputs['Strength'].default_value = 0.22
except Exception:
    bg.inputs['Color'].default_value = (0.62, 0.72, 0.82, 1); bg.inputs['Strength'].default_value = 0.8
sun = bpy.data.objects.new('太阳', bpy.data.lights.new('太阳', 'SUN'))
sun.data.energy = 2.4; sun.data.angle = math.radians(1.2); sun.data.color = (1.0, 0.95, 0.88)
sun.rotation_euler = (math.pi / 2 - SUN_ELEV, 0, -SUN_AZ + math.pi)
scn.collection.objects.link(sun)

# ------------------------------------------------------------------ 材质
def mat(name, rgb, rough=0.8, metal=0.0, alpha=1.0, transmission=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes['Principled BSDF']
    p.inputs['Base Color'].default_value = (*rgb, 1); p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    if transmission:
        p.inputs['Transmission Weight'].default_value = transmission
    if alpha < 1:
        p.inputs['Alpha'].default_value = alpha
    return m
M = dict(
    basalt=mat('玄武岩', (0.22, 0.21, 0.20), 0.85), plaster=mat('白灰墙', (0.78, 0.75, 0.68), 0.9),
    white=mat('白塔', (0.86, 0.85, 0.82), 0.7), brick=mat('砖', (0.42, 0.24, 0.17), 0.85),
    stone=mat('台基石', (0.40, 0.38, 0.35), 0.9), tile=mat('红瓦', (0.46, 0.20, 0.13), 0.7),
    slate=mat('石板瓦', (0.17, 0.18, 0.20), 0.6), copper=mat('铜绿', (0.24, 0.44, 0.38), 0.45, 0.5),
    glass=mat('玻璃', (0.75, 0.85, 0.88), 0.05, transmission=0.9), window=mat('窗', (0.06, 0.07, 0.08), 0.2),
    paving=mat('铺石', (0.52, 0.50, 0.46), 0.95), gravel=mat('碎石路', (0.44, 0.41, 0.35), 0.95),
    quay=mat('码头', (0.35, 0.34, 0.33), 0.9))

def terrain_material():
    m = bpy.data.materials.new('地面'); m.use_nodes = True
    nt = m.node_tree; n = nt.nodes; l = nt.links
    p = n['Principled BSDF']; p.inputs['Roughness'].default_value = 0.95
    geo = n.new('ShaderNodeNewGeometry'); sep = n.new('ShaderNodeSeparateXYZ'); l.new(geo.outputs['Normal'], sep.inputs[0])
    rmp = n.new('ShaderNodeValToRGB')                         # 坡度：缓处草地，陡处裸岩
    rmp.color_ramp.elements[0].position, rmp.color_ramp.elements[0].color = 0.80, (0.13, 0.12, 0.10, 1)
    rmp.color_ramp.elements[1].position, rmp.color_ramp.elements[1].color = 0.93, (0.14, 0.20, 0.08, 1)
    l.new(sep.outputs['Z'], rmp.inputs['Fac'])
    pos = n.new('ShaderNodeSeparateXYZ'); l.new(geo.outputs['Position'], pos.inputs[0])
    heath = n.new('ShaderNodeMapRange'); heath.inputs['From Min'].default_value = 60; heath.inputs['From Max'].default_value = 210
    l.new(pos.outputs['Z'], heath.inputs['Value'])
    mixh = n.new('ShaderNodeMix'); mixh.data_type = 'RGBA'; mixh.inputs[7].default_value = (0.19, 0.16, 0.09, 1)   # 高处荒草
    l.new(heath.outputs['Result'], mixh.inputs['Factor']); l.new(rmp.outputs['Color'], mixh.inputs[6])
    noise = n.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = 0.02; noise.inputs['Detail'].default_value = 6
    nr = n.new('ShaderNodeValToRGB')
    nr.color_ramp.elements[0].position, nr.color_ramp.elements[0].color = 0.35, (0.10, 0.15, 0.06, 1)
    nr.color_ramp.elements[1].position, nr.color_ramp.elements[1].color = 0.65, (0.20, 0.24, 0.11, 1)
    l.new(noise.outputs['Fac'], nr.inputs['Fac'])
    mix = n.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'OVERLAY'; mix.inputs['Factor'].default_value = 0.35
    l.new(mixh.outputs[2], mix.inputs[6]); l.new(nr.outputs['Color'], mix.inputs[7])
    shore = n.new('ShaderNodeMapRange'); shore.inputs['From Min'].default_value = 0.0; shore.inputs['From Max'].default_value = 2.0
    l.new(pos.outputs['Z'], shore.inputs['Value'])
    mix2 = n.new('ShaderNodeMix'); mix2.data_type = 'RGBA'
    mix2.inputs[6].default_value = (0.13, 0.12, 0.11, 1)       # 海岸黑色玄武岩
    l.new(shore.outputs['Result'], mix2.inputs['Factor']); l.new(mix.outputs[2], mix2.inputs[7])
    l.new(mix2.outputs[2], p.inputs['Base Color'])
    return m
M['ground'] = terrain_material()
M['sea'] = mat('海', (0.03, 0.09, 0.12), 0.06)

COLL = {}
def coll(name):
    if name not in COLL:
        c = bpy.data.collections.new(name); scn.collection.children.link(c); COLL[name] = c
    return COLL[name]

def new_obj(name, bm, material, collection='建筑'):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); coll(collection).objects.link(ob)
    if isinstance(material, list):
        for mm in material: me.materials.append(mm)
    else:
        me.materials.append(material)
    return ob

# ------------------------------------------------------------------ 地形与海
def build_terrain():
    ny, nx = TZ.shape
    X, Y = np.meshgrid(TX, TY)
    verts = np.stack([X.ravel(), Y.ravel(), TZ.ravel()], 1)
    idx = np.arange(ny * nx).reshape(ny, nx)
    faces = np.stack([idx[:-1, :-1].ravel(), idx[:-1, 1:].ravel(), idx[1:, 1:].ravel(), idx[1:, :-1].ravel()], 1)
    me = bpy.data.meshes.new('地形')
    me.from_pydata(verts.tolist(), [], faces.tolist())
    me.shade_smooth()
    ob = bpy.data.objects.new('地形', me); coll('地形').objects.link(ob); me.materials.append(M['ground'])
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20000)
    for v in bm.verts:
        v.co.z = 0.0
    sea = new_obj('海面', bm, M['sea'], '地形'); sea.location = (float(TX.mean()), float(TY.mean()), 0)
build_terrain()

# ------------------------------------------------------------------ 几何工具
def prism(name, poly, z0, z1, material, collection='建筑'):
    bm = bmesh.new()
    bot = [bm.verts.new((x, y, z0)) for x, y in poly]
    top = [bm.verts.new((x, y, z1)) for x, y in poly]
    bm.faces.new(bot[::-1]); bm.faces.new(top)
    n = len(poly)
    for i in range(n):
        bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i]))
    return new_obj(name, bm, material, collection)

def expand(c, d):
    c = [Vector((x, y, 0)) for x, y in c]
    cen = sum(c, Vector()) / 4
    a, b = (c[1] - c[0]).normalized(), (c[3] - c[0]).normalized()
    sign = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    return [tuple((p + a * s[0] * d + b * s[1] * d)[:2]) for p, s in zip(c, sign)]

def rect_axes(c):
    A, B, C, D = [Vector((x, y, 0)) for x, y in c]
    return A, B, C, D

def gable(name, corners, z, ridge, material, pitch=35, hip=False, collection='建筑'):
    """corners 依次为 (-u,-v),(+u,-v),(+u,+v),(-u,+v)；ridge='u' 表示屋脊平行于 u。"""
    A, B, C, D = rect_axes(expand(corners, 0.45))
    if ridge == 'v':                                           # 统一成屋脊平行于 A→B 的情形
        A, B, C, D = D, A, B, C
    span = (D - A).length
    rise = span / 2 * math.tan(math.radians(pitch))
    m1, m2 = (A + D) / 2, (B + C) / 2
    if hip:
        inset = (B - A).normalized() * min(span / 2, (B - A).length / 2 - 0.01)
        m1, m2 = m1 + inset, m2 - inset
    zz = Vector((0, 0, z)); r = Vector((0, 0, z + rise))
    bm = bmesh.new()
    a, b, c_, d = (bm.verts.new(p + zz) for p in (A, B, C, D))
    r1, r2 = bm.verts.new(m1 + r), bm.verts.new(m2 + r)
    bm.faces.new((a, b, r2, r1)); bm.faces.new((c_, d, r1, r2))
    bm.faces.new((d, a, r1)); bm.faces.new((b, c_, r2)); bm.faces.new((a, d, c_, b))
    return new_obj(name, bm, material, collection)

def cylinder(name, center, r, z0, z1, material, r_top=None, seg=48, collection='建筑'):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r_top is None else r_top, depth=z1 - z0)
    ob = new_obj(name, bm, material, collection); ob.location = (center[0], center[1], (z0 + z1) / 2)
    return ob

def dome(name, center, r, z, material, squash=0.7, collection='建筑'):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=r)
    for v in list(bm.verts):
        if v.co.z < -0.01:
            bm.verts.remove(v)
    for v in bm.verts:
        v.co.z *= squash
    ob = new_obj(name, bm, material, collection); ob.location = (center[0], center[1], z)
    return ob

def draped(name, pts, width, material, lift=0.06, step=2.0, collection='道路'):
    pts = [Vector((x, y, 0)) for x, y in pts]
    dense = []
    for a, b in zip(pts[:-1], pts[1:]):
        n = max(1, int((b - a).length / step))
        for i in range(n):
            dense.append(a + (b - a) * (i / n))
    dense.append(pts[-1])
    bm = bmesh.new(); left, right = [], []
    for i, p in enumerate(dense):
        t = (dense[min(i + 1, len(dense) - 1)] - dense[max(i - 1, 0)]).normalized()
        nrm = Vector((-t.y, t.x, 0)) * width / 2
        for side, lst in ((p + nrm, left), (p - nrm, right)):
            lst.append(bm.verts.new((side.x, side.y, ground(side.x, side.y) + lift)))
    for i in range(len(dense) - 1):
        bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
    return new_obj(name, bm, material, collection)

def wall(name, p0, p1, thick, height, material, step=2.0):
    """沿地面起伏的矮墙：每 2 m 一段，墙底埋入地下 0.3 m，墙顶高出地面 height。"""
    bm = bmesh.new(); L = (p1 - p0).length; dirv = (p1 - p0).normalized(); nrm = Vector((-dirv.y, dirv.x, 0)) * thick / 2
    n = max(1, int(L / step))
    for i in range(n):
        a, b_ = p0 + dirv * (L * i / n), p0 + dirv * (L * (i + 1) / n)
        za, zb = ground(a.x, a.y), ground(b_.x, b_.y)
        lo, hi = min(za, zb) - 0.3, max(za, zb) + height
        q = [a + nrm, b_ + nrm, b_ - nrm, a - nrm]
        bot = [bm.verts.new((v.x, v.y, lo)) for v in q]; top = [bm.verts.new((v.x, v.y, hi)) for v in q]
        bm.faces.new(bot[::-1]); bm.faces.new(top)
        for k in range(4):
            bm.faces.new((bot[k], bot[(k + 1) % 4], top[(k + 1) % 4], top[k]))
    return new_obj(name, bm, material, '建筑')

def draped_rect(name, corners, material, lift=0.08, step=2.0, collection='道路'):
    A, B, C, D = rect_axes(corners)
    nu, nv = max(1, int((B - A).length / step)), max(1, int((D - A).length / step))
    bm = bmesh.new(); grid = []
    for j in range(nv + 1):
        row = []
        for i in range(nu + 1):
            p = A + (B - A) * (i / nu) + (D - A) * (j / nv)
            row.append(bm.verts.new((p.x, p.y, ground(p.x, p.y) + lift)))
        grid.append(row)
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    return new_obj(name, bm, material, collection)

def local_rect(center, eu, ev, su, sv):
    c = Vector((center[0], center[1], 0))
    return [tuple((c + eu * su * a / 2 + ev * sv * b / 2)[:2]) for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1))]

# ------------------------------------------------------------------ 建筑
B = {b['name']: b for b in SC['buildings']}
ROOF_MAT = {'basalt': 'slate', 'plaster': 'tile', 'brick': 'tile', 'white': 'slate', 'glass': 'glass'}
LABEL_POINTS = {}

def plinth(b, poly):
    prism(b['name'] + '·台基', poly, b['ground_min'] - 0.6, b['floor'], M['stone'])

def building(b):
    nm, kind = b['name'], b['kind']
    if nm.startswith('生活翼·'):
        return
    if kind == 'outdoor':
        if nm == '誓约庭':
            draped_rect('誓约庭·铺石', b['corners'], M['paving'])
            A, Bv, C, D = rect_axes(b['corners'])
            for p0, p1 in ((A, Bv), (Bv, C), (C, D), (D, A)):        # 矮墙，南北两边留口
                L = (p1 - p0).length; dirv = (p1 - p0).normalized()
                segs = [(0, L)] if abs(dirv.dot(EV)) > 0.5 else [(0, L / 2 - 2.5), (L / 2 + 2.5, L)]
                for s0, s1 in segs:
                    q0, q1 = p0 + dirv * s0, p0 + dirv * s1
                    wall('誓约庭·矮墙', q0, q1, 0.5, 0.9, M['basalt'])
        else:
            draped_rect(nm, b['corners'], M['gravel'])
        LABEL_POINTS[nm] = (*b['center'], ground(*b['center']) + 1)
        return
    h, roof, mtype = b['height'], b['roof'], b['mat']
    f = b['floor']
    if b['shape'] == 'circ':
        c, r = b['center'], b['radius']
        cylinder(nm + '·台基', c, r + 0.4, b['ground_min'] - 0.6, f, M['stone'])
        cylinder(nm, c, r, f, f + h, M[mtype])
        if roof == 'dome':
            cylinder(nm + '·鼓座', c, r * 0.86, f + h, f + h + 1.6, M[mtype])
            dome(nm + '·穹顶', c, r * 0.86, f + h + 1.6, M['copper'], squash=0.75)
            cylinder(nm + '·灯亭', c, r * 0.12, f + h + 1.6 + r * 0.86 * 0.75 - 0.3, f + h + 1.6 + r * 0.86 * 0.75 + 2.0, M[mtype])
            top = f + h + 1.6 + r * 0.86 * 0.75 + 2
        else:
            cylinder(nm + '·尖顶', c, r + 0.5, f + h, f + h + r * 1.6, M['slate'], r_top=0.0)
            top = f + h + r * 1.6
        LABEL_POINTS[nm] = (c[0], c[1], top + 1)
        return
    corners = b['corners']
    plinth(b, expand(corners, 0.3))
    if roof == 'gate':                                           # 门楼：两座塔夹一道门
        su, sv = b['size']
        for side in (-1, 1):
            cen = Vector((*b['center'], 0)) + EU * side * (su / 2 - 2)
            tc = local_rect(cen, EU, EV, 4, sv)
            prism(nm + '·塔', tc, f, f + 10, M[mtype]); gable(nm + '·塔顶', tc, f + 10, 'u', M['slate'], pitch=50, hip=True)
        for side in (-1, 1):
            cen = Vector((*b['center'], 0)) + EU * side * 2.4
            prism(nm + '·门墩', local_rect(cen, EU, EV, 1.2, sv * 0.8), f, f + 7, M[mtype])
        prism(nm + '·门楣', local_rect(Vector((*b['center'], 0)), EU, EV, 6, sv * 0.8), f + 4.8, f + 7, M[mtype])
        LABEL_POINTS[nm] = (*b['center'], f + 14)
        return
    if roof == 'glass':                                          # 温室：顺坡三级
        su, sv = b['size']
        for k in range(3):
            cen = Vector((*b['center'], 0)) + EV * (-sv / 2 + sv / 6 * (2 * k + 1))
            seg = local_rect(cen, EU, EV, su, sv / 3)
            gz = max(ground(x, y) for x, y in seg) + 0.3
            prism(nm + f'·{k + 1}级台基', expand(seg, 0.2), gz - 1.5, gz, M['stone'])
            prism(nm + f'·{k + 1}级', seg, gz, gz + h, M['glass']); gable(nm + f'·{k + 1}级屋面', seg, gz + h, 'v', M['glass'], pitch=30)
        LABEL_POINTS[nm] = (*b['center'], f + h + 4)
        return
    prism(nm, corners, f, f + h, M[mtype])
    rm = M[ROOF_MAT[mtype]]
    if roof in ('gable', 'hip'):
        gable(nm + '·屋顶', corners, f + h, b.get('ridge', 'u'), rm, pitch=32 if roof == 'gable' else 30, hip=(roof == 'hip'))
    elif roof == 'flat':
        prism(nm + '·檐', expand(corners, 0.3), f + h, f + h + 0.4, rm)
    if nm == '铁工院（封闭）':
        A, Bv, C, D = rect_axes(corners)
        cc = A + (Bv - A) * 0.85 + (D - A) * 0.5
        cylinder(nm + '·烟囱', (cc.x, cc.y), 1.3, f, f + 24, M['brick'], r_top=1.0)
    LABEL_POINTS[nm] = (*b['center'], f + h + 5)

for b in SC['buildings']:
    building(b)

# 生活翼：四个寝区、起居厅（加高、高侧窗）、浴室；夹道为露天
LW = {b['name'].split('·')[1]: b for b in SC['buildings'] if b['name'].startswith('生活翼·')}
for key in ('甲区', '丁区', '丙区', '乙区', '浴室盥洗'):
    b = LW[key]; f = b['floor']
    plinth(b, expand(b['corners'], 0.3))
    prism('生活翼·' + key, b['corners'], f, f + b['height'], M['plaster'])
    gable('生活翼·' + key + '·屋顶', b['corners'], f + b['height'], 'u' if key != '浴室盥洗' else 'v', M['tile'], pitch=30,
          hip=(key == '浴室盥洗'))
hn, hs = LW['起居厅北台'], LW['起居厅南台']
for b in (hn, hs):
    plinth(b, expand(b['corners'], 0.3))
eave = max(hn['floor'], hs['floor']) + 7.0
hall = [hs['corners'][0], hs['corners'][1], hn['corners'][2], hn['corners'][3]]
for b in (hn, hs):
    prism('生活翼·起居厅·' + b['name'][-2:], b['corners'], b['floor'], eave, M['plaster'])
gable('生活翼·起居厅·屋顶', hall, eave, 'v', M['slate'], pitch=28)
A, Bv, C, D = rect_axes(hall)
for p0, p1 in ((A, D), (Bv, C)):                                   # 高侧窗带：高出寝区屋顶的两侧长墙
    out = (p0 - (A + Bv) / 2 if p0 == A else p0 - (A + Bv) / 2).normalized() * 0.05
    nrm = (Bv - A).normalized() * (-0.06 if p0 == A else 0.06)
    for k in range(6):
        t0, t1 = 0.06 + k * 0.155, 0.06 + k * 0.155 + 0.11
        q0, q1 = p0 + (p1 - p0) * t0, p0 + (p1 - p0) * t1
        poly = [tuple((q0 + nrm * 0)[:2]), tuple((q1 + nrm * 0)[:2]), tuple((q1 + nrm)[:2]), tuple((q0 + nrm)[:2])]
        prism('生活翼·高侧窗', poly, eave - 2.0, eave - 0.6, M['window'])
cen = (Vector((*hn['center'], 0)) + Vector((*hs['center'], 0))) / 2
LABEL_POINTS['生活翼'] = (cen.x, cen.y, eave + 6)

# 道路、码头
for p in SC['paths']:
    draped('路·' + p['name'], p['pts'], p['width'], M['gravel'])
(qa, qb) = [Vector((x, y, 0)) for x, y in SC['quay']]
d = (qb - qa).normalized(); n = Vector((-d.y, d.x, 0)) * 3
prism('码头', [tuple((qa + n)[:2]), tuple((qb + n)[:2]), tuple((qb - n)[:2]), tuple((qa - n)[:2])], -3, 1.8, M['quay'])
LABEL_POINTS['港'] = (qb.x, qb.y, 6)
LABEL_POINTS['全岛最高点'] = (SC['summit'][0], SC['summit'][1], SC['summit'][2] + 20)

# ------------------------------------------------------------------ 相机
def camera(name, loc, target, lens=35, ortho=None, up_axis=None):
    cd = bpy.data.cameras.new(name)
    if ortho:
        cd.type = 'ORTHO'; cd.ortho_scale = ortho
    else:
        cd.lens = lens
    cd.clip_start, cd.clip_end = 0.5, 20000
    ob = bpy.data.objects.new(name, cd); scn.collection.objects.link(ob)
    ob.location = loc
    direction = Vector(target) - Vector(loc)
    ob.rotation_euler = direction.to_track_quat('-Z', up_axis or 'Y').to_euler()
    return ob

g0 = ground(*SC['origin'])
O3 = Vector((O.x, O.y, g0))
LWc = Vector((cen.x, cen.y, hn['floor']))
CAM = {
    'aerial': lambda: camera('aerial', O3 - EV * 330 + EU * 70 + Vector((0, 0, 165)), O3 - EV * 25 + EU * 15, 34),
    'avenue': lambda: camera('avenue', O3 - EV * 112 + Vector((0, 0, ground(*(O - EV * 112)[:2]) - g0 + 1.7)),
                             O3 + EV * 400 + Vector((0, 0, 62)), 28),
    'living': lambda: camera('living', LWc - EV * 70 - EU * 75 + Vector((0, 0, 48)), LWc + Vector((0, 0, 2)), 40),
    'island': lambda: camera('island', O3 - EV * 1250 + EU * 520 + Vector((0, 0, 280)), Vector((SC['summit'][0], SC['summit'][1], 120)), 40),
}
def top_cam():
    ob = camera('top', O3 - EV * 35 + EU * 18 + Vector((0, 0, 600)), O3 - EV * 35 + EU * 18, ortho=340)
    ob.rotation_euler = (0, 0, -math.radians(-SC['bearing']))
    ob.rotation_euler = (0, 0, math.radians(-SC['bearing']))
    return ob
CAM['top'] = top_cam

def render(name):
    cam = CAM[name]()
    scn.camera = cam
    scn.render.filepath = os.path.join(OUT, f'{name}.png')
    bpy.ops.render.render(write_still=True)
    labels = {}
    for k, (x, y, z) in LABEL_POINTS.items():
        co = world_to_camera_view(scn, cam, Vector((x, y, z)))
        if 0 <= co.x <= 1 and 0 <= co.y <= 1 and co.z > 0:
            labels[k] = [co.x * RES[0], (1 - co.y) * RES[1], co.z]
    json.dump(labels, open(os.path.join(OUT, f'{name}_labels.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

if '--save' in argv:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, 'campus_3d.blend'))
for c in CAMS:
    render(c)
print('done', CAMS)
