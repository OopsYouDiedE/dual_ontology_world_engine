# 《安眠者有福》岛屿与学园三维结构示意（依据 docs/design-standard.md 第一部分 §6）
# 运行：blender -b -P build_island.py -- <输出目录> [--no-render]
# 单位：1 Blender 单位 = 1 米；学院按局部标高沿自然缓坡建设。
import bpy, bmesh, math, random, os, sys, json
from math import sin, cos, pi, atan2, hypot, exp, radians
from mathutils import Vector, noise

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv and not argv[0].startswith('--') else os.path.dirname(os.path.abspath(__file__))
RENDER = '--no-render' not in argv
QUICK = '--quick' in argv
os.makedirs(OUT, exist_ok=True)
random.seed(7)

bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
CAMPUS_Z = 24.0  # 体块装配初始基准；成品地坪由各足迹独立确定
scn.unit_settings.system = 'METRIC'
scn.unit_settings.scale_length = 1.0
bpy.context.preferences.filepaths.save_version = 0
scn['原型'] = 'Faial 地貌关系的虚构压缩重组；非测绘复刻'
scn['阶段'] = '原始地形冻结的建筑选址概念；非 Godot 可玩地图'
scn['地形约束'] = '地形顶点仅来自冻结 natural_height；路、楼、水池不得改写' 

# ---------------------------------------------------------------- 材质
def mat(name, rgb, rough=0.75, metal=0.0, emit=None, es=0.0, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if emit:
        b.inputs['Emission Color'].default_value = (*emit, 1)
        b.inputs['Emission Strength'].default_value = es
    if alpha < 1:
        b.inputs['Alpha'].default_value = alpha
        m.surface_render_method = 'BLENDED'
        m.use_backface_culling = False
    m.diffuse_color = (*rgb, 1)
    return m

M = dict(
    stone=mat('石材', (0.66, 0.63, 0.57)),
    stone_dark=mat('深石', (0.30, 0.29, 0.28)),
    slate=mat('石板瓦', (0.20, 0.22, 0.26), 0.6),
    copper=mat('铜绿', (0.30, 0.55, 0.47), 0.45, 0.6),
    brass=mat('黄铜', (0.78, 0.60, 0.25), 0.3, 1.0),
    red_roof=mat('红瓦', (0.50, 0.20, 0.14)),
    blue_roof=mat('靛瓦', (0.18, 0.21, 0.40), 0.6),
    brick=mat('砖墙', (0.52, 0.27, 0.19)),
    iron=mat('铸铁', (0.22, 0.22, 0.23), 0.5, 0.8),
    white=mat('白墙', (0.90, 0.89, 0.86)),
    plaster=mat('灰泥', (0.80, 0.74, 0.62)),
    wood=mat('木', (0.40, 0.27, 0.16)),
    paving=mat('铺石', (0.58, 0.55, 0.50), 0.9),
    path=mat('步道', (0.72, 0.64, 0.50), 0.95),
    glass=mat('玻璃', (0.70, 0.88, 0.92), 0.05, alpha=0.35),
    water=mat('水面', (0.09, 0.28, 0.40), 0.12),
    reservoir=mat('库水', (0.12, 0.36, 0.44), 0.1),
    track=mat('跑道', (0.62, 0.33, 0.24), 0.9),
    lawn=mat('草坪', (0.30, 0.50, 0.22), 0.9),
    trunk=mat('树干', (0.30, 0.20, 0.12)),
    conifer=mat('山地灌丛', (0.12, 0.24, 0.13)),
    broadleaf=mat('背风阔叶', (0.16, 0.32, 0.16)),
    basalt=mat('玄武岩', (0.12, 0.15, 0.16)),
    soil=mat('火山土', (0.26, 0.23, 0.18)),
    tunnel=mat('勤务环廊', (0.55, 0.50, 0.40), 0.6),
    seal=mat('封印廊', (0.35, 0.25, 0.55), 0.4, emit=(0.5, 0.3, 1.0), es=1.5),
    reactor=mat('反应炉', (0.18, 0.18, 0.20), 0.35, 0.9),
    core=mat('源质', (1.0, 0.45, 0.1), 0.3, emit=(1.0, 0.45, 0.1), es=12.0),
    lead=mat('铅灰隔离', (0.28, 0.30, 0.32), 0.5, 0.7),
    ghost=mat('地表基准', (0.85, 0.80, 0.60), 0.9, alpha=0.10),
    anchor_n=mat('北锚', (0.6, 0.1, 0.1), emit=(0.9, 0.1, 0.1), es=3),
    anchor_w=mat('西锚', (0.7, 0.6, 0.2), emit=(1.0, 0.8, 0.3), es=3),
    anchor_s=mat('南锚', (0.5, 0.55, 0.6), emit=(0.6, 0.75, 0.9), es=3),
    anchor_e=mat('东锚', (0.9, 0.9, 0.9), emit=(1.0, 1.0, 1.0), es=3),
    text=mat('标注字', (1, 1, 1), emit=(1, 1, 1), es=1.2),
    plate=mat('标注底', (0.05, 0.05, 0.07), alpha=0.6),
)

# ---------------------------------------------------------------- 集合
def coll(name):
    c = bpy.data.collections.new(name)
    scn.collection.children.link(c)
    return c

C = {n: coll(n) for n in ['地形', '海面', '植被', '岛屿生存环', '道路步道', '公共学园层',
                          '中央厅与四院', '地下勤务层', '地下封印层', '地表基准', '标注']}

# ---------------------------------------------------------------- 几何工具
def mk(name, bm, m, col, loc=(0, 0, 0), rot=0.0):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for mm in (m if isinstance(m, (list, tuple)) else [m]):
        me.materials.append(mm)
    ob = bpy.data.objects.new(name, me)
    ob.location = loc
    ob.rotation_euler = (0, 0, rot)
    col.objects.link(ob)
    return ob

def box(name, x, y, z, sx, sy, sz, m, col, rot=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= sx; v.co.y *= sy; v.co.z = v.co.z * sz + sz / 2
    return mk(name, bm, m, col, (x, y, z), rot)

def prism(name, x, y, z, sx, sy, h, m, col, rot=0.0):
    """双坡屋顶，屋脊沿局部 X。"""
    bm = bmesh.new()
    a, b = sx / 2, sy / 2
    vs = [bm.verts.new(p) for p in [(-a, -b, 0), (a, -b, 0), (a, b, 0), (-a, b, 0), (-a, 0, h), (a, 0, h)]]
    for f in [(0, 1, 5, 4), (2, 3, 4, 5), (0, 4, 3), (1, 2, 5), (0, 3, 2, 1)]:
        bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mk(name, bm, m, col, (x, y, z), rot)

def cyl(name, x, y, z, r, h, m, col, seg=32, r2=None, rot=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)
    for v in bm.verts:
        v.co.z += h / 2
    return mk(name, bm, m, col, (x, y, z), rot)

def pyramid(name, x, y, z, s, h, m, col, rot=0.0):
    return cyl(name, x, y, z, s * 0.72, h, m, col, seg=4, r2=0.01, rot=rot + pi / 4)

def dome(name, x, y, z, r, m, col, zs=1.0):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=r)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -1e-4], context='VERTS')
    for v in bm.verts:
        v.co.z *= zs
    return mk(name, bm, m, col, (x, y, z))

def beam(name, p0, p1, r, m, col, seg=12, r1=None):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r1 is None else r1, depth=d.length)
    ob = mk(name, bm, m, col, (p0 + p1) / 2)
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
    return ob

def torus(name, R, r, z, m, col, seg=96, mseg=12):
    bm = bmesh.new()
    rows = []
    for i in range(seg):
        a = 2 * pi * i / seg
        rows.append([bm.verts.new(((R + r * cos(b)) * cos(a), (R + r * cos(b)) * sin(a), z + r * sin(b)))
                     for b in (2 * pi * j / mseg for j in range(mseg))])
    for i in range(seg):
        A, B = rows[i], rows[(i + 1) % seg]
        for j in range(mseg):
            bm.faces.new([A[j], B[j], B[(j + 1) % mseg], A[(j + 1) % mseg]])
    return mk(name, bm, m, col)

# ---------------------------------------------------------------- 地形高度场
def smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)

def angdiff(a, b):
    return (a - b + pi) % (2 * pi) - pi

def gauss(d, s):
    return exp(-d * d / (2 * s * s))

def g2(dx, dy, s):
    return exp(-(dx * dx + dy * dy) / (2 * s * s))

def pnoise(t, k, seed):
    """沿海岸角度的周期噪声。"""
    return noise.noise(Vector((cos(t) * k, sin(t) * k, seed)))

def shore_R(t):
    # Faial 的主火山、侧向旧火山脊与分段海岸关系；不复制真实岛坐标。
    R = 660 + 75 * sin(t) + 28 * sin(3 * t + 0.7) + 16 * sin(5 * t + 2.1)
    R += 22 * pnoise(t, 1.6, 0.3) + 9 * pnoise(t, 4.5, 1.9) + 4 * pnoise(t, 12, 4.4)
    R -= 205 * gauss(angdiff(t, -pi / 2), 0.19)      # 双岬之间的南岸内湾
    R -= 55 * gauss(angdiff(t, pi + 0.12), 0.30)     # 西湾缓坡火山砂滩
    R += 70 * gauss(angdiff(t, 0.66), 0.21)          # 东北信号塔岬角
    R += 85 * gauss(angdiff(t, 2.77), 0.25)          # 西侧旧火山脊延伸
    return R

def beachness(t):
    """1 = 缓坡沙滩，0 = 海崖。"""
    return max(gauss(angdiff(t, -pi / 2), 0.22), gauss(angdiff(t, pi + 0.12), 0.28))

CAMPUS_C, CAMPUS_R = (0.0, -40.0), (240.0, 215.0)
VOLC_X, VOLC_Y, CRATER_R = -95.0, 380.0, 158.0
RES_X, RES_Y, RES_FLOOR = 450.0, 275.0, 31.0
RES_Z = 36.0
RES_RADIUS, RES_BANK_Z = 25.0, 37.0
HARBOR_Y = -shore_R(-pi / 2) + 12
SIGNAL_X, SIGNAL_Y, SIGNAL_Z = 600.0, 390.0, 62.0
TERRACE=(-340,-100)
QUARRY=(425,-65,42)
WEST_T=pi+.12
WEST_R=shore_R(WEST_T)-42
ISLETS = [(665, 505, 23, 22), (-785, 135, 18, 13), (600, -435, 21, 14), (-135, -590, 14, 9)]

def campus_mask(x, y):
    e = hypot((x - CAMPUS_C[0]) / CAMPUS_R[0], (y - CAMPUS_C[1]) / CAMPUS_R[1])
    return 1 - smooth(0.92, 1.05, e)

def natural_height(x, y):
    r, t = hypot(x, y), atan2(y, x)
    d = r / shore_R(t)
    bch = beachness(t)
    if d > 1:  # 海床：沙滩外侧浅滩宽，海崖外侧陡落
        shelf = 0.02 + 0.10 * bch
        h = -1 - 5 * smooth(1, 1 + shelf, d) - 45 * smooth(1 + shelf, 1.55, d)
        for ix, iy, ir, ih in ISLETS:
            q = hypot(x - ix, y - iy) / ir
            if q < 1.4:
                rock = -3 + (ih + 3) * (1 - smooth(0.0, 1.0, q)) ** 0.6
                rock += 2.5 * noise.noise(Vector((x / 8, y / 8, 7.7)))
                h = max(h, rock)
        return h
    edge_w = 0.025 + 0.12 * bch
    s = smooth(0, edge_w, 1 - d)
    vx, vy = x - VOLC_X, y - VOLC_Y
    a = atan2(vy, vx)
    vr = hypot(vx, vy / 0.92)
    warped_r = vr * (1 + 0.045 * sin(3 * a) + 0.018 * sin(7 * a + 1))
    base = 24 + 3 * noise.noise(Vector((x / 190, y / 190, 2.2)))
    # 南侧旧熔岩裙缘自然放缓，学园立于低缓台地，而非切除百米火山坡。
    cone = 192 * gauss(vr, 225) * smooth(145, 320, y)
    rim = 64 * gauss(warped_r - CRATER_R, 25) * (0.94 + 0.10 * sin(3 * a + 0.4))
    basin = 112 * (1 - smooth(68, 139, warped_r))
    # 侵蚀沟止于外坡，不刻穿火口内壁或学园。
    gullies = sum(gauss(angdiff(a, direction), 0.045) for direction in (-2.65, -2.05, -1.12, -0.28, 0.65, 1.52, 2.35))
    erode = 16 * gullies * smooth(172, 220, vr) * (1 - smooth(370, 470, vr)) * smooth(145, 215, y)
    old_ridge = 45 * exp(-((x + 435) / 190) ** 2 - ((y - 160) / 100) ** 2)
    n = noise.noise(Vector((x / 70, y / 70, 0.5))) * 2.5 + noise.noise(Vector((x / 22, y / 22, 1.7))) * 0.9
    land = base + cone + rim - basin - erode + old_ridge + n
    coastal_bch = bch * smooth(0.55, 0.90, d)
    land = land * (1 - 0.86 * coastal_bch) + (4 + 0.12 * land) * 0.86 * coastal_bch
    h = -1 + s * (land + 1)
    return h


# 道路只登记平面意向。高度一律由冻结地表取得，不能登记工程整平面。
with open(os.path.join(os.path.dirname(__file__),'living_ring_route.json'),encoding='utf-8') as f:
    LIVING_RING=json.load(f)
ring_pts=[tuple(p[:2]) for p in LIVING_RING['points'][:-1]]
academy_loop=[(76*cos(2*pi*i/96),76*sin(2*pi*i/96)) for i in range(97)]
ROAD_SPECS={
    '生活区山麓环线':(ring_pts+[ring_pts[0]],1.2,.15),
    '学园分坡环廊':(academy_loop,4,.15),
    '石场路':([(205,-40),(285,-65),(425,-65)],3,.15),
    '运动场路':([(120,-64),(135,-120),(145,-175),(170,-190),(215,-190)],3,.15),
    '西湾路':([(-188,-54),(-245,-54),(-335,-55),(-460,-65),
        (WEST_R*cos(WEST_T),WEST_R*sin(WEST_T))],3,.15),
    '西院生活接路':([(-63,-43),(-110,-56),(-130,-95),(-175,-104),(-188,-155),
        (-115,-168),(-115,-180),(-30,-190),(0,-190)],3,.15),
    '北院公共接路':([(-54,54),(-76,82),(-80,100),(-80,140),(-80,155)],3,.15),
    '东院公共接路':([(63,43),(165,48),(170,72),(220,72),(220,-40),(205,-40),
        (165,-60),(120,-64),(63,-43)],3,.15),
}
# 仅用于先组装模型的局部相对坐标。全部建筑随后按实际足迹最高点重新定位。
LEVELS = dict(central=24.0, north=24.0, north_w=24.5, north_e=23.0,
    west=26.5, observatory=24.5, east=25.0, chemistry=23.5, anatomy=22.5,
    workshop_w=27.0, workshop_e=24.5, power=24.5, electric=25.5,
    living=24.0, dorm_b=25.0, kitchen=24.0, granary=26.0, ration=23.5,
    gate=24.0, oath=25.0, library=25.5, training=22.5, art=24.5,
    winter=24.0, bath=24.5, clinic=24.5, sport=24.0, stand=24.5, water=24.0)

FOUNDATIONS = []
FOUNDATION_BUCKETS = None
def height(x,y):
    return natural_height(x,y)


def ground(x, y):
    return max(height(x, y), 0.0)

def slope(x, y, e=2.0):
    return hypot(height(x + e, y) - height(x - e, y), height(x, y + e) - height(x, y - e)) / (2 * e)

def forest_zone(x, y, h):
    return (g2(x - 360, y - 15, 145) > 0.3 or g2(x + 430, y - 90, 135) > 0.35
            or (50 < h < 140 and 175 < y < 380 and x > 50))

# ---------------------------------------------------------------- 地形网格
def lerp3(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))

def grid_mesh(name, half, step, zfn, colfn, col, matl):
    n = half * 2 // step + 1
    xs = [-half + i * step for i in range(n)]
    verts, cols = [], []
    for y in xs:
        for x in xs:
            z = zfn(x, y)
            verts.append((x, y, z))
            cols.extend((*colfn(x, y, z), 1.0))
    faces = [(j * n + i, j * n + i + 1, (j + 1) * n + i + 1, (j + 1) * n + i)
             for j in range(n - 1) for i in range(n - 1)]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.polygons.foreach_set('use_smooth', [True] * len(faces))
    me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT').data.foreach_set('color', cols)
    me.materials.append(matl)
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    return ob

def vcol_mat(name, rough, bump=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    at = nt.nodes.new('ShaderNodeAttribute')
    at.attribute_name = 'Col'
    bsdf = nt.nodes['Principled BSDF']
    bsdf.inputs['Roughness'].default_value = rough
    nt.links.new(at.outputs['Color'], bsdf.inputs['Base Color'])
    if bump:
        tc = nt.nodes.new('ShaderNodeTexCoord')
        nz = nt.nodes.new('ShaderNodeTexNoise')
        nz.inputs['Scale'].default_value = 0.08
        nz.inputs['Detail'].default_value = 6
        bp = nt.nodes.new('ShaderNodeBump')
        bp.inputs['Strength'].default_value = bump
        nt.links.new(tc.outputs['Object'], nz.inputs['Vector'])
        nt.links.new(nz.outputs['Fac'], bp.inputs['Height'])
        nt.links.new(bp.outputs['Normal'], bsdf.inputs['Normal'])
    return m

SAND, WET = (0.33, 0.32, 0.29), (0.15, 0.17, 0.17)
ROCK, ROCK_D = (0.23, 0.26, 0.26), (0.10, 0.13, 0.14)
GRASS, GRASS_HI, HEATH = (0.24, 0.37, 0.17), (0.26, 0.35, 0.17), (0.32, 0.31, 0.20)
FOREST, LAWN = (0.10, 0.22, 0.12), (0.30, 0.43, 0.22)

def land_color(x, y, h):
    v = noise.noise(Vector((x / 40, y / 40, 3.3))) * 0.05
    if h < 0.3:
        return WET
    sl = slope(x, y)
    if sl > 0.75:
        # 薄层玄武岩色带，坡壁由几何坡度决定，不用白色覆盖高坡。
        bands = 0.12 * sin(h * 0.8) + 0.05 * sin(h * 2.1)
        return lerp3(ROCK, ROCK_D, min(1.0, max(0, smooth(0.75, 2.0, sl) * 0.7 + bands + v)))
    if hypot(x - QUARRY[0], y - QUARRY[1]) < QUARRY[2]:
        return ROCK
    if h < 4.5 and campus_mask(x, y) < 0.1 and sl < 0.4:
        return lerp3(WET, SAND, smooth(0.3, 1.8, h))
    if campus_mask(x, y) > 0.9:
        return (LAWN[0] + v, LAWN[1] + v, LAWN[2])
    if forest_zone(x, y, h):
        return (FOREST[0] + v, FOREST[1] + v, FOREST[2])
    c = lerp3(GRASS, GRASS_HI, smooth(25, 70, h))
    c = lerp3(c, HEATH, smooth(135, 205, h))
    return (c[0] + v, c[1] + v, c[2] + v * 0.5)

# 建筑轮廓登记后才生成地形，保证全幅地基与渲染网格使用同一高度函数。

FOAM, SHALLOW, MID, DEEP = (0.70, 0.78, 0.77), (0.12, 0.38, 0.37), (0.055, 0.22, 0.29), (0.025, 0.085, 0.14)

def sea_color(x, y, _z):
    D = -height(x, y)
    if D < 0:
        return SHALLOW
    fz = 0.35 + 0.5 * noise.noise(Vector((x / 12, y / 12, 9.1)))
    if D < 1.6 + fz:
        return lerp3(FOAM, SHALLOW, smooth(0.6, 1.6 + fz, D))
    if D < 8:
        return lerp3(SHALLOW, MID, smooth(1.6, 8, D))
    return lerp3(MID, DEEP, smooth(8, 42, D))

bm = bmesh.new()
bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=600000)
for v in bm.verts:
    v.co.z = -0.08
ocean=mk('外海',bm,vcol_mat('外海',.12,bump=.08),C['海面'])
ocean.data.color_attributes.new('Col','FLOAT_COLOR','POINT').data.foreach_set('color',list((*DEEP,1))*len(ocean.data.vertices))

# ---------------------------------------------------------------- 道路与步道
def mesh_height(x, y):
    """采样实际4m渲染三角面，而非仅采样数学高度场。"""
    u, v = (x + 940) / 4, (y + 940) / 4
    i, j = math.floor(u), math.floor(v)
    fu, fv = u - i, v - j
    n = 471
    vs = terrain.data.vertices
    a, b = vs[j * n + i].co.z, vs[j * n + i + 1].co.z
    c, d = vs[(j + 1) * n + i + 1].co.z, vs[(j + 1) * n + i].co.z
    return a + fu * (b - a) + fv * (c - b) if fu >= fv else a + fu * (c - d) + fv * (d - a)


# 道路也延后生成，以纳入建筑承载面。

# ---------------------------------------------------------------- 建筑工具
def house(name, x, y, sx, sy, sz, wall, roof, col, rot=0.0, rh=None, z=None, ridge='x'):
    z = ground(x, y) if z is None else z
    foundation(name, x, y, sx + 1, sy + 1, z, rot)
    ob = box(name, x, y, z, sx, sy, sz, wall, col, rot)
    ob['ground_z'] = z
    rh = rh or min(sx, sy) * 0.38
    if ridge == 'x':
        prism(name + '_屋顶', x, y, z + sz, sx + 1.0, sy + 1.4, rh, roof, col, rot)
    else:
        prism(name + '_屋顶', x, y, z + sz, sy + 1.0, sx + 1.4, rh, roof, col, rot + pi / 2)
    return z

BUILDINGS = {}

def foundation(name, x, y, sx, sy, z, rot=0.0):
    FOUNDATIONS.append((x,y,sx,sy,rot,z))
    BUILDINGS[name] = dict(center=[x,y], size=[sx,sy], rotation=rot, ground_z=z)

Z = CAMPUS_Z
core, pub, isl = C['中央厅与四院'], C['公共学园层'], C['岛屿生存环']

foundation('中央厅',0,0,42,42,24)
# ---- 中央厅（交通中枢与机械广播）
cyl('中央厅_鼓座', 0, 0, Z, 20, 14, M['stone'], core, 48)
cyl('中央厅_高鼓', 0, 0, Z + 14, 16, 4, M['stone'], core, 48)
dome('中央厅_穹顶', 0, 0, Z + 18, 16, M['copper'], core, 0.9)
cyl('中央厅_灯亭', 0, 0, Z + 32, 2.6, 6, M['stone'], core, 16)
cyl('中央厅_尖顶', 0, 0, Z + 38, 3.0, 5, M['copper'], core, 16, r2=0.05)
for k in range(4):  # 机械广播铜喇叭
    a = pi / 4 + k * pi / 2
    beam(f'广播铜口_{k}', (2.4 * cos(a), 2.4 * sin(a), Z + 35), (7 * cos(a), 7 * sin(a), Z + 33.5),
         0.35, M['brass'], core, 16, r1=1.6)
for k in range(4):  # 四向门廊
    a = k * pi / 2
    house(f'中央厅_门廊_{k}', 23 * cos(a), 23 * sin(a), 10, 12, 9, M['stone'], M['slate'], core,
          rot=a, z=Z, ridge='x')

# ---- 连续分坡回廊；地面在地形生成后用登记路网建立。
def graded_roof(name, nodes, width, z_offset, thickness, material, col, closed=False):
    bm = bmesh.new()
    rows=[]
    for i, (x,y,z) in enumerate(nodes):
        a,b=nodes[max(0,i-1)],nodes[min(len(nodes)-1,i+1)]
        if closed:
            a,b=nodes[(i-1)%len(nodes)],nodes[(i+1)%len(nodes)]
        dx,dy=b[0]-a[0],b[1]-a[1]
        ll=hypot(dx,dy) or 1
        nx,ny=-dy/ll*width/2,dx/ll*width/2
        rows.append([bm.verts.new(v) for v in [(x+nx,y+ny,z+z_offset),(x-nx,y-ny,z+z_offset),
            (x-nx,y-ny,z+z_offset+thickness),(x+nx,y+ny,z+z_offset+thickness)]])
    for i in range(len(rows) if closed else len(rows)-1):
        a,b=rows[i],rows[(i+1)%len(rows)]
        for j in range(4):
            bm.faces.new([a[j],b[j],b[(j+1)%4],a[(j+1)%4]])
    if not closed:
        bm.faces.new(rows[0][::-1]); bm.faces.new(rows[-1])
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    return mk(name,bm,material,col)

# ---- 北侧文契院
house('文契院_文献工房', 0, 120, 64, 22, 14, M['stone'], M['slate'], core, rot=radians(-15), z=LEVELS['north'])
house('文契院_声纹记录室', -49, 130, 14, 40, 12, M['stone'], M['slate'], core, z=LEVELS['north_w'], ridge='y')
house('文契院_契约审议室', 49, 130, 14, 40, 12, M['stone'], M['slate'], core, z=LEVELS['north_e'], ridge='y')
foundation('文契院_名录库塔',0,146,14,14,24)
box('文契院_名录库塔', 0, 146, LEVELS['north'], 13, 13, 26, M['stone'], core)
pyramid('文契院_名录库塔顶', 0, 146, LEVELS['north'] + 26, 14, 9, M['red_roof'], core)

foundation('星仪院_共鸣厅',-157,25,29,29,26.5)
foundation('星仪院_观星塔',-158,-28,15,15,24.5)
foundation('星仪院_阵法基座室',-104,16,13,13,26.5)
# ---- 西侧星仪院
house('星仪院_星盘讲堂', -115, -10, 22, 60, 13, M['stone'], M['blue_roof'], core, rot=radians(30), z=LEVELS['west'], ridge='y')
cyl('星仪院_共鸣厅', -157, 25, LEVELS['west'], 14, 12, M['stone'], core, 40)
cyl('星仪院_共鸣厅顶', -157, 25, LEVELS['west'] + 12, 15, 8, M['blue_roof'], core, 40, r2=0.5)
cyl('星仪院_观星塔', -158, -28, LEVELS['observatory'], 7, 22, M['stone'], core, 24)
dome('星仪院_观星穹', -158, -28, LEVELS['observatory'] + 22, 7.2, M['brass'], core)
cyl('星仪院_阵法基座室', -104, 16, LEVELS['west'], 6, 7, M['stone_dark'], core, 8)
cyl('星仪院_阵法基座顶', -104, 16, LEVELS['west'] + 7, 6.5, 4, M['blue_roof'], core, 8, r2=0.3)

# ---- 南侧铁工院（夹南大道两侧）
for side in (-1, 1):
    x0 = 54 * side
    ZW = LEVELS['workshop_w' if side < 0 else 'workshop_e']
    rot_w=radians(-15) if side>0 else 0
    foundation(f'铁工院_工房_{side}',x0,-128,41,27,ZW,rot_w)
    box(f'铁工院_工房_{side}', x0, -128, ZW, 40, 26, 11, M['brick'], core, rot=rot_w)
    for i in range(4):  # 锯齿天窗屋面
        dy=-9.75+i*6.5
        prism(f'铁工院_锯齿_{side}_{i}',x0-dy*sin(rot_w),-128+dy*cos(rot_w),ZW+11,41,6.5,3.2,M['slate'],core,rot=rot_w)
house('铁工院_动力总站', 80, -162, 24, 18, 16, M['brick'], M['slate'], core, z=LEVELS['power'])
for i, yy in enumerate((-168, -156)):
    foundation(f'铁工院_烟囱_{i}',95,yy,6,6,24.5)
    cyl(f'铁工院_烟囱_{i}', 95, yy, LEVELS['power'], 2.2, 40, M['brick'], core, 16, r2=1.6)
house('铁工院_电气实验室', -80, -162, 18, 14, 9, M['brick'], M['slate'], core, z=LEVELS['electric'])
beam('铁工院_天线杆', (-80, -162, LEVELS['electric'] + 12), (-80, -162, LEVELS['electric'] + 34), 0.3, M['iron'], core)
for i in range(3):
    foundation(f'铁工院_线圈_{i}',-66,-170+i*5,4,4,25.5)
    cyl(f'铁工院_线圈_{i}', -66, -170 + i * 5, LEVELS['electric'], 1.6, 4, M['copper'], core, 16)

foundation('白塔',138,0,19,19,25)
# ---- 东侧白塔异象院
cyl('白塔', 138, 0, LEVELS['east'], 9, 58, M['white'], core, 32)
cyl('白塔_尖顶', 138, 0, LEVELS['east'] + 58, 10.5, 12, M['slate'], core, 32, r2=0.1)
house('异象院_化学博物楼', 106, 28, 30, 16, 10, M['white'], M['slate'], core, rot=pi/2, z=LEVELS['chemistry'])
house('异象院_解剖X光楼', 101, -28, 30, 16, 10, M['white'], M['slate'], core, z=LEVELS['anatomy'])
house('异象院_学园长隔离室', 158, 0, 12, 12, 8, M['white'], M['stone_dark'], core, z=LEVELS['east'])

# ---- 公共学园层
foundation('门楼_东塔',13,-232,11,11,24)
foundation('门楼_西塔',-13,-232,11,11,24)
box('门楼_东塔', 13, -232, LEVELS['gate'], 10, 10, 20, M['stone'], pub)
box('门楼_西塔', -13, -232, LEVELS['gate'], 10, 10, 20, M['stone'], pub)
pyramid('门楼_东塔顶', 13, -232, LEVELS['gate'] + 20, 11, 7, M['slate'], pub)
pyramid('门楼_西塔顶', -13, -232, LEVELS['gate'] + 20, 11, 7, M['slate'], pub)
box('门楼_门楣', 0, -232, LEVELS['gate'] + 11, 16, 8, 6, M['stone'], pub)
box('誓约庭_外墙石碑', 0, -208, natural_height(0,-208), 9, 1.8, 7, M['stone_dark'], pub)

house('教学图书馆', -108, 112, 40, 20, 15, M['stone'], M['slate'], pub, rot=radians(15), z=LEVELS['library'])
dome('教学图书馆_穹顶', -108, 112, LEVELS['library'] + 15 + 5, 6, M['copper'], pub)

house('生活院_宿舍甲', -160, -72, 52, 13, 13, M['plaster'], M['red_roof'], pub, rot=radians(-15), z=LEVELS['living'])
house('生活院_宿舍乙', -210, -127, 13, 52, 13, M['plaster'], M['red_roof'], pub, z=LEVELS['dorm_b'], ridge='y')
house('生活院_公共起居室', -150, -118, 24, 16, 8, M['plaster'], M['red_roof'], pub, z=LEVELS['living'])
house('公共娱乐室', -205, -40, 18, 14, 8, M['plaster'], M['red_roof'], pub, z=LEVELS['living'])

house('后勤院_厨房', -95, -195, 30, 16, 9, M['plaster'], M['red_roof'], pub, z=LEVELS['kitchen'])
cyl('后勤院_灶烟囱', -84, -191, LEVELS['kitchen'], 1.2, 18, M['brick'], pub, 12)
for i in range(3):
    foundation(f'后勤院_粮仓_{i}',-132,-212+i*12,11,11,26)
    cyl(f'后勤院_粮仓_{i}', -132, -212 + i * 12, LEVELS['granary'], 5, 14, M['wood'], pub, 24)
    cyl(f'后勤院_粮仓顶_{i}', -132, -212 + i * 12, LEVELS['granary'] + 14, 5.5, 3.5, M['red_roof'], pub, 24, r2=0.2)
house('后勤院_配给台', -95, -212, 16, 8, 5, M['wood'], M['red_roof'], pub, z=LEVELS['ration'])

house('训练院', 165, -95, 40, 22, 12, M['stone'], M['slate'], pub, z=LEVELS['training'])

house('美术工房', 103, 106, 22, 14, 9, M['plaster'], M['red_roof'], pub, rot=-pi / 4, z=LEVELS['art'])
foundation('冬园_玻璃暖房',122,138,29,15,24,-pi/4)
box('冬园_玻璃暖房', 122, 138, LEVELS['winter'], 28, 14, 8, M['glass'], pub, rot=-pi / 4)
prism('冬园_玻璃顶', 122, 138, LEVELS['winter'] + 8, 28, 14, 5, M['glass'], pub, rot=-pi / 4)

house('公共浴室', 190, 30, 24, 14, 8, M['plaster'], M['slate'], pub, z=LEVELS['bath'])
cyl('公共浴室_锅炉烟囱', 200, 34, LEVELS['bath'], 1.2, 16, M['brick'], pub, 12)
house('公共诊疗所', 191, 58, 22, 14, 9, M['white'], M['slate'], pub, z=LEVELS['bath'])

# ---- 岛屿生存环
ys = HARBOR_Y - 48
box('潮湾_石砌卸货岸', 0, ys + 12, 2.9, 50, 12, .4, M['basalt'], isl)
box('潮湾_主栈桥', 0, ys - 25, 2.8, 8, 60, 0.6, M['wood'], isl)
box('潮湾_侧栈桥', -32, ys - 18, 2.8, 6, 40, 0.6, M['wood'], isl, rot=0.35)
for yy in range(int(ys - 50), int(ys + 6), 12):
    for xx in (-3, 3):
        beam('潮湾_栈桥桩', (xx, yy, height(xx, yy) - 1), (xx, yy, 3.0), 0.4, M['wood'], isl)
house('潮湾_仓库', 100, HARBOR_Y + 60, 26, 14, 9, M['wood'], M['red_roof'], isl, z=3)
house('潮湾_船屋', -95, HARBOR_Y + 75, 16, 20, 7, M['wood'], M['slate'], isl, ridge='y', z=3)
beam('潮湾_吊杆柱', (14, ys - 4, 3), (14, ys - 4, 20), 0.5, M['iron'], isl)
beam('潮湾_吊臂', (14, ys - 4, 19), (14, ys - 22, 16), 0.35, M['iron'], isl)
for i, (bx, by, br) in enumerate([(-14, ys - 40, 0.1), (14, ys - 50, -0.2), (-50, ys - 45, 0.5)]):
    box(f'潮湾_船_{i}', bx, by, 0, 4, 12, 1.6, M['wood'], isl, rot=br)

t_w = WEST_T
rw = shore_R(t_w)
for i, off in enumerate((-40, 0, 45)):
    hx,hy,angle=[(-533.665768,-99.638803,105),(-533.454256,-74.926457,105),
        (-533.841305,-20.250069,135)][i]
    house(f'西湾_沙滩小屋_{i}',hx,hy,6,5,3.5,M['wood'],M['red_roof'],isl,rot=radians(angle))

for j in range(3):
    house(f'运动场_看台{j}',195+j*20,-265,19,8,4,M['wood'],M['slate'],isl,z=24)

sx_, sy_ = SIGNAL_X, SIGNAL_Y
foundation('海崖信号塔',sx_,sy_,12,12,SIGNAL_Z)
zt = SIGNAL_Z
tw = cyl('海崖信号塔_铁架', sx_, sy_, zt, 5, 34, M['iron'], isl, seg=4, r2=1.2)
tw.modifiers.new('铁架', 'WIREFRAME').thickness = 0.45
beam('海崖信号塔_天线', (sx_, sy_, zt + 34), (sx_, sy_, zt + 50), 0.2, M['iron'], isl)
house('海崖信号塔_电报房', sx_ + 14, sy_ - 12, 8, 6, 4, M['stone'], M['slate'], isl, z=SIGNAL_Z)

for i in range(3):
    for j in range(3):
        gx,gy=TERRACE[0]-30+i*30,TERRACE[1]-28+j*28
        fsx,fsy=(12,7) if i<2 else (20,11)
        grot=radians(([75,90,105] if i==0 else [75,90,135])[j]) if i<2 else 0
        gz=24
        foundation(f'坡地温室_{i}{j}',gx,gy,fsx,fsy,gz,grot)
        box(f'坡地温室_{i}{j}',gx,gy,gz,fsx-1,fsy-1,3.5,M['glass'],isl,grot)
        prism(f'坡地温室顶_{i}{j}',gx,gy,gz+3.5,fsx-1,fsy-1,2.5,M['glass'],isl,grot)

# 池底、池壁是独立构件：地形原样保留，天然起伏的底面不挖平。
house('水务站_阀室滤水房',460,240,16,10,7,M['brick'],M['slate'],isl,z=30)
foundation('学园_配水池',160,105,14,14,24)
PIPE_POINTS=[]
SPILL_POINTS=[]
cyl('学园_配水池',160,105,24,6,4,M['stone'],isl,32)

# 火口内小锥与间歇湿地；饮用上水库位于独立外坡。
cz = ground(VOLC_X, VOLC_Y)
cyl('古火山口_间歇湿地', VOLC_X - 12, VOLC_Y - 6, cz + 0.15, 27, 0.1, M['soil'], C['地形'], 48)
cyl('古火山口_次级渣锥', VOLC_X + 30, VOLC_Y + 22, ground(VOLC_X + 30, VOLC_Y + 22) - 1,
    17, 16, M['basalt'], C['地形'], 36, r2=4)


# 先生成冻结自然地表，选址采样与基座都对照实际渲染三角面。
terrain=grid_mesh('岛屿地形',940,4,height,land_color,C['地形'],vcol_mat('地表',.95,.16))
# 从冻结自然地表选各栋地坪；独立基座底面随地面，顶面承托建筑。
ATTACHMENTS={'广播铜口_':'中央厅','星仪院_观星穹':'星仪院_观星塔',
    '星仪院_阵法基座顶':'星仪院_阵法基座室','铁工院_天线杆':'铁工院_电气实验室',
    '后勤院_灶烟囱':'后勤院_厨房','公共浴室_锅炉烟囱':'公共浴室',
    '门楼_门楣':'门楼_东塔','冬园_玻璃顶':'冬园_玻璃暖房'}
for i in range(3): ATTACHMENTS[f'后勤院_粮仓顶_{i}']=f'后勤院_粮仓_{i}'
for side in (-1,1): ATTACHMENTS[f'铁工院_锯齿_{side}_']=f'铁工院_工房_{side}'
for i in range(3):
    for j in range(3): ATTACHMENTS[f'坡地温室顶_{i}{j}']=f'坡地温室_{i}{j}'
for name,data in BUILDINGS.items():
    x,y=data['center'];sx,sy=data['size'];rot=data['rotation']
    values=[]
    circular=name in ('中央厅','星仪院_共鸣厅','星仪院_观星塔','白塔','学园_配水池')
    for j in range(math.ceil(sy)+1):
        for i in range(math.ceil(sx)+1):
            lx=-sx/2+sx*i/math.ceil(sx);ly=-sy/2+sy*j/math.ceil(sy)
            if circular and (lx/(sx/2))**2+(ly/(sy/2))**2>1: continue
            wx,wy=x+lx*cos(rot)-ly*sin(rot),y+lx*sin(rot)+ly*cos(rot)
            values.append(mesh_height(wx,wy))
    data['original_z']=data['ground_z']
    data['floor_z']=max(values)+.1
    data['ground_z']=data['floor_z']
    data['natural_min_z']=min(values);data['natural_max_z']=max(values)
    data['max_base_exposure_m']=data['floor_z']-min(values)
    data['shape']='circle' if circular else 'rectangle'
central_group=[n for n in BUILDINGS if n.startswith('中央厅')]
central_floor=max(BUILDINGS[n]['floor_z'] for n in central_group)
for n in central_group:
    BUILDINGS[n]['floor_z']=central_floor;BUILDINGS[n]['ground_z']=central_floor
    BUILDINGS[n]['max_base_exposure_m']=central_floor-BUILDINGS[n]['natural_min_z']
for ob in list(core.objects)+list(pub.objects)+list(isl.objects):
    owner=None
    for prefix,name in ATTACHMENTS.items():
        if ob.name.startswith(prefix): owner=name;break
    if owner is None:
        matches=[name for name in BUILDINGS if ob.name.startswith(name)]
        if matches: owner=max(matches,key=len)
    if owner:
        data=BUILDINGS[owner]
        ob.location.z+=data['floor_z']-data['original_z']
        ob['site_owner']=owner;ob['floor_z']=data['floor_z']
FOUNDATIONS=[(*d['center'],*d['size'],d['rotation'],d['floor_z']) for d in BUILDINGS.values()]
beam('海崖信号塔_天线索',(SIGNAL_X,SIGNAL_Y,BUILDINGS['海崖信号塔']['floor_z']+48),
    (SIGNAL_X+14,SIGNAL_Y-12,BUILDINGS['海崖信号塔_电报房']['floor_z']+6.3),.08,M['iron'],isl,6)


# 生成冻结原始地表；楼基和道路只作为独立构件覆于地表。
FOUNDATION_BUCKETS = {}
for foundation_data in FOUNDATIONS:
    fx, fy, sx, sy, rotation, fz = foundation_data
    extent = hypot(sx, sy) / 2 + 8
    for by in range(math.floor((fy - extent) / 40), math.floor((fy + extent) / 40) + 1):
        for bx in range(math.floor((fx - extent) / 40), math.floor((fx + extent) / 40) + 1):
            FOUNDATION_BUCKETS.setdefault((bx, by), []).append(foundation_data)
grid_mesh('近岸海面', 1600, 6, lambda x, y: 0.0, sea_color, C['海面'], vcol_mat('海水', 0.12, bump=0.08))
# 独立构件：只取mesh_height，不写任何terrain顶点。
def fitted_base(name,x,y,sx,sy,rot,top,material,col,circular=False):
    verts=[];faces=[]
    if circular:
        seg=128;nr=math.ceil(max(sx,sy)/2)
        coords=[(0,0)]
        for k in range(1,nr+1):
            coords.extend((sx/2*k/nr*cos(2*pi*i/seg),sy/2*k/nr*sin(2*pi*i/seg)) for i in range(seg))
        for lx,ly in coords:
            wx,wy=x+lx*cos(rot)-ly*sin(rot),y+lx*sin(rot)+ly*cos(rot)
            verts.append((wx,wy,mesh_height(wx,wy)-.05))
        n=len(verts);verts.extend((wx,wy,top) for wx,wy,_ in verts[:n])
        for i in range(seg):
            j=(i+1)%seg
            faces.extend(((0,1+j,1+i),(n,n+1+i,n+1+j)))
        for k in range(1,nr):
            lo=1+(k-1)*seg;hi=1+k*seg
            for i in range(seg):
                j=(i+1)%seg
                faces.extend(((lo+i,lo+j,hi+j,hi+i),(lo+i+n,hi+i+n,hi+j+n,lo+j+n)))
        lo=1+(nr-1)*seg
        faces.extend((lo+i,lo+(i+1)%seg,lo+(i+1)%seg+n,lo+i+n) for i in range(seg))
    else:
        nx,ny=max(1,math.ceil(sx)),max(1,math.ceil(sy))
        for j in range(ny+1):
            for i in range(nx+1):
                lx,ly=(i/nx-.5)*sx,(j/ny-.5)*sy
                wx,wy=x+lx*cos(rot)-ly*sin(rot),y+lx*sin(rot)+ly*cos(rot)
                verts.append((wx,wy,mesh_height(wx,wy)-.05))
        n=len(verts);verts.extend((wx,wy,top) for wx,wy,_ in verts[:n])
        for j in range(ny):
            for i in range(nx):
                a=j*(nx+1)+i;b=a+1;c=b+nx+1;d=a+nx+1
                faces.extend(((a,d,c,b),(a+n,b+n,c+n,d+n)))
        edges=[(i,i+1) for i in range(nx)]+[(j*(nx+1)+nx,(j+1)*(nx+1)+nx) for j in range(ny)]
        edges += [(ny*(nx+1)+i+1,ny*(nx+1)+i) for i in range(nx)]
        edges += [((j+1)*(nx+1),j*(nx+1)) for j in range(ny)]
        faces.extend((a,b,b+n,a+n) for a,b in edges)
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(material)
    ob=bpy.data.objects.new(name,me);col.objects.link(ob)
    ob['terrain_edited']=False;ob['support_bottom_vertex_count']=n;ob['support_top_z']=top
    return ob

def inside_building(x,y,data,margin=0):
    cx,cy=data['center'];sx,sy=data['size'];rot=data['rotation']
    lx=(x-cx)*cos(rot)+(y-cy)*sin(rot);ly=-(x-cx)*sin(rot)+(y-cy)*cos(rot)
    if data['shape']=='circle': return (lx/(sx/2+margin))**2+(ly/(sy/2+margin))**2<1
    return abs(lx)<sx/2+margin and abs(ly)<sy/2+margin

ENTRIES={}
for name,d in BUILDINGS.items():
    x,y=d['center'];sx,sy=d['size'];rot=d['rotation'];floor=d['floor_z']
    support=fitted_base(name+'_随地基座',x,y,sx,sy,rot,floor,M['basalt'],pub,d['shape']=='circle')
    d['support_objects']=[support.name]
    if name=='中央厅' or '烟囱_' in name or '线圈_' in name: continue
    preferred=atan2(y,x) if name.startswith('中央厅_门廊') else atan2(-y,-x)
    angles=[rot+k*pi/2 for k in range(4)] if d['shape']!='circle' else [k*pi/8 for k in range(16)]
    angles.sort(key=lambda a:abs(angdiff(a,preferred)))
    chosen=None
    for angle in angles:
        dx,dy=cos(angle),sin(angle)
        local=(dx*cos(rot)+dy*sin(rot),-dx*sin(rot)+dy*cos(rot))
        extent=min(sx/2/abs(local[0]) if abs(local[0])>.001 else 1e9,
            sy/2/abs(local[1]) if abs(local[1])>.001 else 1e9)-.35
        if d['shape']=='circle': extent=min(sx,sy)/2-.35
        door=(x+extent*dx,y+extent*dy)
        for run in (4,5,6,8,10,12):
            start=(door[0]+run*dx,door[1]+run*dy);z0=mesh_height(*start)+.12
            if any(inside_building(*start,other,1.5) for nm,other in BUILDINGS.items() if nm!=name): continue
            rise=floor-z0
            if abs(rise)<.02: continue
            down=rise<0
            n_risers=max(1,math.ceil(abs(rise)/.175));count=n_risers+1 if down else n_risers
            tread=run/count
            if tread<.32: continue
            riser=rise/n_risers;okay=True
            for i in range(count):
                top=z0+(i if down else i+1)*riser
                for u in range(max(2,math.ceil(tread/.5))+1):
                    along=(i+u/max(2,math.ceil(tread/.5)))*tread
                    for side in (-1,-.5,0,.5,1):
                        wx,wy=start[0]-along*dx-side*dy,start[1]-along*dy+side*dx
                        if mesh_height(wx,wy)+.025>top: okay=False;break
                        if any(inside_building(wx,wy,other,.15) for nm,other in BUILDINGS.items()
                                if nm!=name and not (name.startswith('中央厅_门廊') and nm.startswith('中央厅'))):
                            okay=False;break
                    if not okay: break
                if not okay: break
            # The first riser meets the entire natural-width approach, not only its centre.
            entry_ground=[mesh_height(start[0]-side*dy,start[1]+side*dx)+.12 for side in (-1,-.5,0,.5,1)]
            first=z0 if down else z0+riser
            if max(abs(first-z) for z in entry_ground)>.20: okay=False
            if okay: chosen=(dx,dy,door,start,z0,count,tread,riser,down);break
        if chosen: break
    if not chosen: raise RuntimeError('No ungraded entrance stair fits '+name)
    dx,dy,door,start,z0,count,tread,riser,down=chosen
    step_objects=[]
    for i in range(count):
        cx=start[0]-dx*tread*(i+.5);cy=start[1]-dy*tread*(i+.5);z=z0+(i if down else i+1)*riser
        ob=fitted_base(name+f'_入口阶{i}',cx,cy,tread,2,atan2(-dy,-dx),z,M['paving'],pub)
        ob['stair_tread_m']=tread;ob['stair_riser_m']=abs(riser);step_objects.append(ob.name)
    ENTRIES[name]=dict(door_xy=list(door),start_xy=list(start),floor_z=floor,start_z=z0,
        step_objects=step_objects,riser_m=abs(riser),tread_m=tread,direction_xy=[dx,dy],descending=down)

# 门楼两侧原有短外墙沿原地面落脚，墙底和墙顶分别逐米跟随坡面。
for side in (-1,1):
    for j in range(21):
        x0=side*(18+j*4);x1=side*(22+j*4);verts=[];faces=[]
        for i in range(5):
            x=x0+(x1-x0)*i/4
            for y in (-235,-234,-233): verts.append((x,y,mesh_height(x,y)-.05))
        n=len(verts);verts.extend((x,y,z+5.05) for x,y,z in verts[:n])
        for i in range(4):
            for k in range(2):
                a=i*3+k;b=a+3;c=b+1;d=a+1
                faces.extend(((a,d,c,b),(a+n,b+n,c+n,d+n)))
        edges=[(i*3,(i+1)*3) for i in range(4)]+[((i+1)*3+2,i*3+2) for i in range(4)]
        edges.extend([(1,0),(2,1),(12,13),(13,14)])
        faces.extend((a,b,b+n,a+n) for a,b in edges)
        me=bpy.data.meshes.new('随地外墙');me.from_pydata(verts,[],faces);me.materials.append(M['stone'])
        ob=bpy.data.objects.new(f'学园外墙_{side}_{j}',me);pub.objects.link(ob)
        ob['terrain_edited']=False;ob['wall_bottom_vertex_count']=n

# 自然庭院与运动草地采用密铺面，而非水平平台。
def draped_rectangle(name,x,y,sx,sy,material,col):
    verts=[];faces=[];nx,ny=math.ceil(sx),math.ceil(sy)
    for j in range(ny+1):
        for i in range(nx+1):
            wx,wy=x-sx/2+sx*i/nx,y-sy/2+sy*j/ny
            verts.append((wx,wy,mesh_height(wx,wy)+.08))
    for j in range(ny):
        for i in range(nx):
            a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(material)
    ob=bpy.data.objects.new(name,me);col.objects.link(ob);ob['terrain_edited']=False
    return ob
draped_rectangle('誓约庭_随地庭院',0,-186,56,44,M['paving'],pub)
draped_rectangle('训练院_自然操练场',165,-132,44,30,M['path'],pub)
draped_rectangle('运动场_自然草场',215,-215,96,48,M['lawn'],isl)

# 围堰与防渗壳体覆盖天然坡面，池底保留原始坡度。
bm=bmesh.new();rows=[]
for i in range(128):
    a=2*pi*i/128
    inner=(RES_X+RES_RADIUS*cos(a),RES_Y+RES_RADIUS*sin(a))
    outer=(RES_X+(RES_RADIUS+1)*cos(a),RES_Y+(RES_RADIUS+1)*sin(a))
    rows.append([bm.verts.new((*inner,mesh_height(*inner)-.05)),bm.verts.new((*outer,mesh_height(*outer)-.05)),
        bm.verts.new((*outer,RES_BANK_Z)),bm.verts.new((*inner,RES_BANK_Z))])
for i in range(128):
    aa,bb=rows[i],rows[(i+1)%128]
    for j in range(4): bm.faces.new([aa[j],bb[j],bb[(j+1)%4],aa[(j+1)%4]])
mk('上水库_独立围堰',bm,M['stone'],isl)
bm=bmesh.new();center=bm.verts.new((RES_X,RES_Y,mesh_height(RES_X,RES_Y)+.04));last=None
for k in range(1,26):
    row=[bm.verts.new((RES_X+k*cos(2*pi*i/128),RES_Y+k*sin(2*pi*i/128),
        mesh_height(RES_X+k*cos(2*pi*i/128),RES_Y+k*sin(2*pi*i/128))+.04)) for i in range(128)]
    for i in range(128):
        if last is None: bm.faces.new([center,row[i],row[(i+1)%128]])
        else: bm.faces.new([last[i],row[i],row[(i+1)%128],last[(i+1)%128]])
    last=row
mk('上水库_随地防渗壳',bm,M['basalt'],isl)
cyl('上水库_库水',RES_X,RES_Y,RES_Z,24.8,.06,M['reservoir'],isl,128)
pipe_xy=[(460,240),(410,210),(340,175),(260,130),(160,105)]
for a,b in zip(pipe_xy,pipe_xy[1:]):
    n=max(1,math.ceil(hypot(b[0]-a[0],b[1]-a[1])/8))
    for i in range(n):
        x,y=a[0]+(b[0]-a[0])*i/n,a[1]+(b[1]-a[1])*i/n
        PIPE_POINTS.append((x,y,mesh_height(x,y)+.6))
PIPE_POINTS.append((160,105,BUILDINGS['学园_配水池']['floor_z']+.6))
for a,b in zip(PIPE_POINTS,PIPE_POINTS[1:]): beam('水务站_随坡输水管',a,b,.4,M['iron'],isl)
out_r=shore_R(.2)+15
SPILL_POINTS=[(475,275,36),(520,250,28),(570,210,18),(out_r*cos(.2),out_r*sin(.2),1)]
for a,b in zip(SPILL_POINTS,SPILL_POINTS[1:]): beam('上水库_溢流支管',a,b,.6,M['stone_dark'],isl)

# 桩基码头连接自然陆岸，桩脚到海床，陆岸仓库不抬地迁海边。
for x in (-18,0,18):
    for y in (ys+8,ys+16): beam('潮湾_卸货平台桩',(x,y,mesh_height(x,y)-.5),(x,y,3.3),.6,M['basalt'],isl)
dock_nodes=[]
for i in range(17):
    y=ys+12+(HARBOR_Y+22-ys-12)*i/16
    # 板面最低高程取全宽原地面；坡道只抬独立板面，不削填原地面。
    dock_ground=max(mesh_height(x,y) for x in (-2,-1.5,-1,-.5,0,.5,1,1.5,2))
    dock_nodes.append((0,y,max(3.4,dock_ground+.15)))
for indices,direction in ((range(1,len(dock_nodes)),1),(range(len(dock_nodes)-2,-1,-1),-1)):
    for i in indices:
        j=i-direction
        x,y,z=dock_nodes[i];other=dock_nodes[j]
        dock_nodes[i]=(x,y,max(z,other[2]-.12*abs(y-other[1])))
dock=graded_roof('潮湾_独立登岸栈道',dock_nodes,4,-.25,.25,M['wood'],isl)
dock['terrain_edited']=False;dock['requested_max_grade_percent']=12.0
for index in range(0,len(dock_nodes),2):
    x,y,z=dock_nodes[index]
    # 首末桩帽完全位于板内；嵌入板底8cm覆盖坡面下的整个承托截面。
    if index==0:
        other=dock_nodes[1];y+=.3;z+=.3*(other[2]-z)/(other[1]-dock_nodes[0][1])
    elif index==len(dock_nodes)-1:
        other=dock_nodes[-2];y-=.3;z-=.3*(z-other[2])/(dock_nodes[-1][1]-other[1])
    for side in (-1,1): beam('潮湾_登岸栈道桩',(x+side*1.5,y,mesh_height(x+side*1.5,y)-.5),(x+side*1.5,y,z-.25+.08),.25,M['wood'],isl)

sys.path.insert(0,OUT)
from natural_paths import route_on_natural
from build_natural_paths import build_natural_surface
NATURAL_ROADS={};NATURAL_PATH_POINTS={}
plans={name:([tuple(p[:2]) for p in nodes],width,limit) for name,(nodes,width,limit) in ROAD_SPECS.items()}
plans['南大道']=([(0,HARBOR_Y+22),(0,-340),(0,-310),(0,-280),(0,-250),(0,-76)],2,.15)
plans['水库路']=([(160,130),(220,130),(280,145),(340,175),(405,210),ENTRIES['水务站_阀室滤水房']['start_xy']],3,.15)
plans['信号塔路']=([(405,210),(500,290),ENTRIES['海崖信号塔']['start_xy']],2.5,.15)
plans.pop('信号塔支路',None)
plans['温室路']=([(-150,-90),(-260,-90),(-355,-100)],3,.15)
plans['温室生活环线接路']=([(-355,-100),(-275,-100)],2.5,.15)
plans['港口仓库接路']=([(0,-310),ENTRIES['潮湾_仓库']['start_xy']],3,.15)
plans['港口船屋接路']=([(0,-310),(-45,-300),ENTRIES['潮湾_船屋']['start_xy']],3,.15)
main_pairs={'中央北廊':(1,'文契院_文献工房'),'中央西廊':(2,'星仪院_星盘讲堂'),
    '中央东廊':(0,'白塔'),'中央南廊':(3,None)}
for nm,(k,target) in main_pairs.items():
    origin=ENTRIES[f'中央厅_门廊_{k}'];start=origin['start_xy'];od=origin['direction_xy']
    a=pi/2 if k==1 else pi if k==2 else 0 if k==0 else -pi/2
    pts=[start,(start[0]+4*od[0],start[1]+4*od[1]),(76*cos(a),76*sin(a))]
    if target:
        e=ENTRIES[target];p=e['start_xy'];dx,dy=e['direction_xy']
        pts.extend([(p[0]+6*dx,p[1]+6*dy),p])
    plans[nm]=(pts,5,.15)
for nm,owner in [('水库路','水务站_阀室滤水房'),('信号塔路','海崖信号塔'),
        ('港口仓库接路','潮湾_仓库'),('港口船屋接路','潮湾_船屋')]:
    pts,w,lim=plans[nm];e=ENTRIES[owner];p=e['start_xy'];dx,dy=e['direction_xy']
    plans[nm]=(pts[:-1]+[(p[0]+3*dx,p[1]+3*dy),p],w,lim)
def building_obstacle(x,y):
    if abs(x)<4.7 and abs(y+208)<1.1: return True  # 誓约石碑
    if 17.8<abs(x)<102.2 and abs(y+234)<1.2: return True
    for fx,fy,sx,sy,rot,fz in FOUNDATION_BUCKETS.get((math.floor(x/40),math.floor(y/40)),[]):
        lx=(x-fx)*cos(rot)+(y-fy)*sin(rot);ly=-(x-fx)*sin(rot)+(y-fy)*cos(rot)
        if abs(lx)<sx/2+.2 and abs(ly)<sy/2+.2: return True
    return False
WALK_BRANCHES=[]
DIRECT_STAIR_LEGS={'南大道':(0,1)}
DIRECT_STAIR_LEGS['水库路']=(len(plans['水库路'][0])-2,)
def construct_route(name,pts,width,limit,trial=False):
    result=LIVING_RING if name=='生活区山麓环线' else route_on_natural(mesh_height,pts,width,max_grade=limit,corridor=60,grid=6,
        sample_step=.25,obstacle_fn=building_obstacle,
        direct_stair_legs=(len(pts)-2,) if trial else DIRECT_STAIR_LEGS.get(name,()))
    built=build_natural_surface(name,result,width,mesh_height,[M['path'],M['basalt']],C['道路步道'])
    blocked=result['kind']=='blocked' or bool(built.get('blocked'))
    if blocked and trial:
        for ob in built['objects']: bpy.data.objects.remove(ob,do_unlink=True)
        return None
    geometry={k:v for k,v in built.items() if k!='objects'}
    geometry['objects']=[ob.name for ob in built['objects']]
    NATURAL_ROADS[name]=dict(statistics=result['statistics'],geometry=geometry,terrain_edited=False,blocked=blocked)
    NATURAL_PATH_POINTS[name]=result['points']
    if not trial:
        for segment in result['segments']:
            if segment['kind']=='walk': WALK_BRANCHES.extend(zip(segment['points'],segment['points'][1:]))
    print('natural route',name,flush=True)
    return result
for name,(pts,width,limit) in plans.items(): construct_route(name,pts,width,limit)

# 从已完成道路的实际走线选接点，避开楼基及其他楼门，不取意向线的投影。
for name,entry in ENTRIES.items():
    if name.startswith('中央厅') or name in ('文契院_文献工房','星仪院_星盘讲堂','白塔',
        '潮湾_仓库','潮湾_船屋','水务站_阀室滤水房','海崖信号塔'): continue
    p=entry['start_xy'];candidates=[];seen=set()
    for a,b in WALK_BRANCHES:
        dx,dy=b[0]-a[0],b[1]-a[1];ll=dx*dx+dy*dy
        if ll<.001: continue
        t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/ll));length=math.sqrt(ll)
        for offset in (0,-5,5,-10,10,-20,20):
            tt=max(0,min(1,t+offset/length));q=(a[0]+tt*dx,a[1]+tt*dy)
            key=(round(q[0],2),round(q[1],2))
            if key in seen: continue
            seen.add(key);dist=hypot(q[0]-p[0],q[1]-p[1])
            if dist<.1 or any(building_obstacle(q[0]+1.25*cos(k*pi/4),q[1]+1.25*sin(k*pi/4)) for k in range(8)): continue
            candidates.append((dist,q))
    candidates.sort(key=lambda v:v[0]);success=False
    for dist,q in candidates[:20]:
        dx,dy=entry['direction_xy'];approach=(p[0]+2*dx,p[1]+2*dy)
        result=construct_route('入口_'+name,[q,approach,p],2,.15,trial=True)
        if result: success=True;break
    if not success: raise RuntimeError('No natural path reaches entrance '+name)

# 环廊及径向廊的屋面、柱脚从实际贴地路面重新建，独立落地。
from mathutils.bvhtree import BVHTree
for name in ['学园分坡环廊']+list(main_pairs):
    pts=NATURAL_PATH_POINTS[name];nodes=[(x,y,z+.12) for x,y,z in pts]
    loop=name=='学园分坡环廊'
    roof=graded_roof('分坡环廊_屋面' if loop else name+'_屋面',nodes[:-1] if loop else nodes,
        10 if loop else 8,5.5,.6 if loop else .5,M['slate'],core,loop)
    roof.data.calc_loop_triangles()
    roof_tree=BVHTree.FromPolygons([v.co for v in roof.data.vertices],
        [t.vertices for t in roof.data.loop_triangles],all_triangles=True)
    seq=nodes
    for i,(a,b) in enumerate(zip(seq,seq[1:])):
        dx,dy=b[0]-a[0],b[1]-a[1];length=hypot(dx,dy)
        if length<.2: continue
        count=max(1,math.ceil(length/8))
        for j in range(count):
            t=(j+.5)/count;x,y=a[0]+t*dx,a[1]+t*dy
            for side in (-1,1):
                hit=None
                for offset in (3,2.75,2.5):
                    xx,yy=x-dy/length*offset*side,y+dx/length*offset*side;base=mesh_height(xx,yy)-.05
                    hit=roof_tree.ray_cast(Vector((xx,yy,base)),Vector((0,0,1)),100)[0]
                    if hit is not None: break
                if hit is None: raise RuntimeError('Roof has no support above '+name)
                column=cyl(name+f'_随地柱{i}_{j}_{side}',xx,yy,base,.3,hit.z-base,M['stone'],core,8)
                column['roof_owner']=roof.name;column['ground_contact_z']=base;column['roof_contact_z']=float(hit.z)

PATH_BUCKETS={}
for pts in NATURAL_PATH_POINTS.values():
    for a,b in zip(pts,pts[1:]):
        for by in range(math.floor((min(a[1],b[1])-6)/40),math.floor((max(a[1],b[1])+6)/40)+1):
            for bx in range(math.floor((min(a[0],b[0])-6)/40),math.floor((max(a[0],b[0])+6)/40)+1):
                PATH_BUCKETS.setdefault((bx,by),[]).append((a,b))
def near_walkway(x,y):
    for a,b in PATH_BUCKETS.get((math.floor(x/40),math.floor(y/40)),[]):
            dx,dy=b[0]-a[0],b[1]-a[1];ll=dx*dx+dy*dy
            if ll<.001: continue
            t=max(0,min(1,((x-a[0])*dx+(y-a[1])*dy)/ll))
            if hypot(x-a[0]-t*dx,y-a[1]-t*dy)<6: return True
    return False

qx, qy, _ = QUARRY
for i in range(8):
    a = random.uniform(0, 2 * pi)
    rr = random.uniform(4, 22)
    bx, by = qx + rr * cos(a), qy + rr * sin(a)
    box(f'石场_石料_{i}', bx, by, ground(bx, by), 3, 2, 1.6, M['stone'], isl, rot=a)
dz = ground(qx - 30, qy)
beam('石场_起重架', (qx - 30, qy, dz), (qx - 18, qy, dz + 16), 0.4, M['wood'], isl)

# ---------------------------------------------------------------- 植被
def tree_mesh(kind):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.5, radius2=0.35, depth=3)
    for v in bm.verts:
        v.co.z += 1.5
    old = set(bm.faces)
    if kind == 'con':
        res = bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=3.2, radius2=0.1, depth=9)
        for v in res['verts']:
            v.co.z += 6.5
    else:
        res = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=3.6)
        for v in res['verts']:
            v.co.z += 5.5
    for f in bm.faces:
        f.material_index = 0 if f in old else 1
    me = bpy.data.meshes.new('树_' + kind)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(M['trunk'])
    me.materials.append(M['conifer'] if kind == 'con' else M['broadleaf'])
    return me

TREES = {'con': tree_mesh('con'), 'broad': tree_mesh('broad')}
avoid = [(QUARRY[0], QUARRY[1], 60), (TERRACE[0], TERRACE[1], 90), (RES_X, RES_Y, 82), (215, -215, 90),
         (sx_, sy_, 35), (0, ys + 20, 80), (VOLC_X, VOLC_Y, 145)]
placed = 0
for _ in range(40000):
    if placed >= 1900:
        break
    x, y = random.uniform(-800, 800), random.uniform(-700, 800)
    h = height(x, y)
    if h < 5 or slope(x, y) > 0.6 or h > 185:
        continue
    if any(max(abs((x-fx)*cos(rot)+(y-fy)*sin(rot))-sx/2, abs(-(x-fx)*sin(rot)+(y-fy)*cos(rot))-sy/2) < 10 for fx,fy,sx,sy,rot,fz in FOUNDATION_BUCKETS.get((math.floor(x/40),math.floor(y/40)),[])):
        continue
    if any(hypot(x - ax, y - ay) < ar for ax, ay, ar in avoid):
        continue
    if near_walkway(x, y):
        continue
    dense = forest_zone(x, y, h)
    if not dense and random.random() > 0.10:
        continue
    kind = 'con' if h > 130 and random.random() < 0.70 else 'broad'
    ob = bpy.data.objects.new('树', TREES[kind])
    ob.location = (x, y, h - 0.3)
    s = random.uniform(0.65, 1.3) * (0.4 if h > 130 else 1.0)
    ob.scale = (s, s, s * random.uniform(0.9, 1.2))
    ob.rotation_euler = (0, 0, random.uniform(0, 2 * pi))
    C['植被'].objects.link(ob)
    placed += 1

# ---------------------------------------------------------------- 地下勤务层
ug, sl = C['地下勤务层'], C['地下封印层']
ZS, ZSEAL, ZISO = -4.0, -35.0, -75.0
torus('地下勤务环廊', 110, 3, ZS, M['tunnel'], ug)
service_owners={'北':'文契院_文献工房','西':'星仪院_星盘讲堂','南':'铁工院_工房_-1',
    '东':'白塔','生活院':'生活院_公共起居室','后勤院':'后勤院_厨房','训练院':'训练院',
    '浴室诊疗所':'公共浴室','图书馆':'教学图书馆','美术冬园':'美术工房','门楼':'门楼_东塔','中央厅':'中央厅'}
service_links={nm:(*BUILDINGS[owner]['center'],BUILDINGS[owner]['floor_z']) for nm,owner in service_owners.items()}
for nm,(x,y,entry_z) in service_links.items():
    a=atan2(y,x)
    p_ring=(110*cos(a),110*sin(a),ZS)
    if nm!='中央厅':
        beam(f'勤务支廊_{nm}',p_ring,(x,y,ZS),2.2,M['tunnel'],ug,10)
    shaft=beam(f'勤务竖井_{nm}',(x,y,ZS),(x,y,entry_z),1.8 if nm!='中央厅' else 2.4,M['tunnel'],ug,12)
    shaft['entry_z']=entry_z
    shaft['entry_xy']=[x,y]
for k, a in enumerate((pi / 2, pi, -pi / 2, 0)):
    beam(f'勤务径廊_{k}', (0, 0, ZS), (107 * cos(a), 107 * sin(a), ZS), 2.0, M['tunnel'], ug, 10)

# ---------------------------------------------------------------- 地下封印层
torus('四锚封印廊', 45, 3.2, ZSEAL, M['seal'], sl)
for key, a in [('n', pi / 2), ('w', pi), ('s', -pi / 2), ('e', 0)]:
    ax, ay = 45 * cos(a), 45 * sin(a)
    box(f'封印锚室_{key}', ax, ay, ZSEAL - 5, 12, 12, 10, M['stone_dark'], sl, rot=a)
    cyl(f'封印锚核_{key}', ax, ay, ZSEAL + 5, 2.2, 1.2, M['anchor_' + key], sl, 16)
    # 示意：勤务环廊经斜井下至锚室（设计未规定具体入口，按四院对位推定）
    beam(f'封印斜井_{key}', (106 * cos(a), 106 * sin(a), ZS), (51 * cos(a), 51 * sin(a), ZSEAL + 1),
         1.6, M['stone_dark'], sl, 10)
cyl('源质反应炉_炉体', 0, 0, ZSEAL - 12, 15, 24, M['reactor'], sl, 48)
dome('源质反应炉_顶盖', 0, 0, ZSEAL + 12, 15, M['reactor'], sl, 0.5)
torus('源质反应炉_源质环', 15.6, 1.1, ZSEAL, M['core'], sl, 64, 10)
beam('反应炉_唯一连接井', (0, 0, ZSEAL - 12), (0, 0, ZISO + 6), 1.3, M['lead'], sl, 12)
box('核心隔离舱', 0, 0, ZISO - 5, 14, 14, 11, M['lead'], sl)
cyl('核心隔离舱_源质', 0, 0, ZISO + 6, 2.0, 0.8, M['core'], sl, 16)

# 剖视图保留实际分层地表的半透明裁片，移除共面圆盘。
grid_mesh('学院分层地表裁片',260,4,height,lambda x,y,z:(.7,.65,.5),C['地表基准'],M['ghost'])

# ---------------------------------------------------------------- 标注
FONT = bpy.data.fonts.load(r'C:\Windows\Fonts\simhei.ttf')
LABELS = []

def label(text, x, y, cats, lift=30.0, z=None):
    zg = ground(x, y) if z is None else z
    cu = bpy.data.curves.new('标注', 'FONT')
    cu.body = text
    cu.font = FONT
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.materials.append(M['text'])
    ob = bpy.data.objects.new('标注_' + text, cu)
    ob.location = (x, y, zg + lift)
    C['标注'].objects.link(ob)
    lead = beam('引线_' + text, (x, y, zg), (x, y, zg + lift - 0.6), 0.35, M['text'], C['标注'], 6)
    for o in (ob, lead):
        o.visible_shadow = False
    LABELS.append(dict(ob=ob, cats=set(cats), lead=lead))

I, P, U, S = 'island', 'campus', 'under', 'core'
label('南岸潮湾（抵达·后勤）', 0, ys - 20, [I], 30, z=2)
label('西湾沙滩', (rw - 20) * cos(t_w), (rw - 20) * sin(t_w), [I], 25)
label('中央草场·露天运动场', 215, -215, [I, P], 26)
label('山麓生活环线',-270,-215,[I],22)
label('海崖信号塔', sx_, sy_, [I], 62)
label('坡地温室', *TERRACE[:2], [I], 30)
label('上水库·水务站', RES_X, RES_Y, [I], 30)
label('林地', 290, 40, [I], 30)
label('石场', *QUARRY[:2], [I], 30)
label('古火山口·间歇湿地', VOLC_X, VOLC_Y, [I], 42)
label('学园（环形研究院）', 0, -20, [I + '_plan'], 90)
label(f"中央厅 · {BUILDINGS['中央厅']['floor_z']:.1f}m", 0, 0, [P, S], 62)
label('北·文契院（顺坡选址）', 0, 130, [P, S], 48)
label('西·星仪院（顺坡选址）', -130, 0, [P, S], 44)
label('南·铁工院（独立地坪）', 0, -135, [P, S], 44)
label('东·白塔院（独立地坪）', 138, 0, [P, S], 86)
label('环廊随自然地面起伏', -57, -57, [P], 16)
label('门楼·誓约庭', 0, -225, [P], 36)
label('教学图书馆', -108, 112, [P], 38)
label('生活院（宿舍·起居室）', -178, -95, [P], 32)
label('公共娱乐室', -205, -40, [P], 24)
label('后勤院（厨房·粮仓·配给台）', -105, -200, [P], 30)
label('训练院', 165, -105, [P], 32)
label('美术工房·冬园', 110, 118, [P], 28)
label('公共浴室·诊疗所', 188, 44, [P], 28)
label('地下勤务环廊（动力·水务·通风·仓储·废物）', 20, -118, [U], 7, z=ZS)
label('四锚封印廊', -45, 0, [U], 12, z=ZSEAL)
label('源质反应炉', -22, -30, [U], 13, z=ZSEAL)
label('核心隔离舱（仅连反应炉）', -26, -26, [U], 2, z=ZISO)
label('冻结原始地表',180,-150,[U],3,z=24)

bpy.context.view_layer.update()
for L in LABELS:  # 半透明底板
    ob = L['ob']
    bb = [Vector(c) for c in ob.bound_box]
    x0, x1 = min(v.x for v in bb) - 0.3, max(v.x for v in bb) + 0.3
    y0, y1 = min(v.y for v in bb) - 0.22, max(v.y for v in bb) + 0.22
    bmp = bmesh.new()
    vs = [bmp.verts.new((a, b, -0.02)) for a, b in [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]]
    bmp.faces.new(vs)
    plate = mk('底板', bmp, M['plate'], C['标注'])
    plate.parent = ob
    plate.visible_shadow = False
    L['plate'] = plate
    ob.constraints.new('COPY_ROTATION')

# ---------------------------------------------------------------- 灯光、相机、渲染
world = bpy.data.worlds.new('天空')
world.use_nodes = True
wnt = world.node_tree
BG = wnt.nodes['Background']
SKY = wnt.nodes.new('ShaderNodeTexSky')
SKY.sky_type = 'NISHITA'
SUN_EL, SUN_AZ = radians(38), radians(-35)   # 东南侧光，勾出海崖与山体
SKY.sun_elevation = SUN_EL
SKY.sun_rotation = pi / 2 - SUN_AZ
SKY.sun_disc = False
SKY.dust_density = 0.6
SKY.air_density = 1.4
scn.world = world

def set_sky(dark):
    for l in list(wnt.links):
        if l.to_node == BG and l.to_socket.name == 'Color':
            wnt.links.remove(l)
    if dark:
        BG.inputs['Color'].default_value = (0.05, 0.06, 0.09, 1)
        BG.inputs['Strength'].default_value = 1.0
    else:
        wnt.links.new(SKY.outputs['Color'], BG.inputs['Color'])
        BG.inputs['Strength'].default_value = 0.13

sun_d = bpy.data.lights.new('太阳', 'SUN')
sun_d.energy = 2.5
sun_d.angle = radians(1.5)
sun = bpy.data.objects.new('太阳', sun_d)
sun_vec = Vector((cos(SUN_EL) * cos(SUN_AZ), cos(SUN_EL) * sin(SUN_AZ), sin(SUN_EL)))
sun.rotation_euler = (-sun_vec).to_track_quat('-Z', 'Y').to_euler()
scn.collection.objects.link(sun)
try:
    scn.view_settings.view_transform = 'AgX'
    scn.view_settings.look = 'AgX - Medium High Contrast'
except TypeError:
    pass

def camera(name, loc, target=None, lens=35, ortho=None):
    cd = bpy.data.cameras.new(name)
    cd.clip_start, cd.clip_end = 1, 300000
    if ortho:
        cd.type = 'ORTHO'
        cd.ortho_scale = ortho
    else:
        cd.lens = lens
    ob = bpy.data.objects.new(name, cd)
    ob.location = loc
    if target:
        ob.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    scn.collection.objects.link(ob)
    return ob

CAMS = dict(
    hero=camera('相机_全岛外观', (1300, -2100, 1250), (-30, 60, 45), 48),
    overview=camera('相机_全岛鸟瞰', (-1400, -1600, 1850), (0, 60, 65), 46),
    plan=camera('相机_全岛平面', (0, 80, 1600), ortho=1750),
    campus_plan=camera('相机_学园平面', (15, -45, 900), ortho=540),
    campus=camera('相机_学园透视', (210, -360, 200), (-10, -35, 24), 26),
    under=camera('相机_地下剖视', (-200, -300, 20), (0, 0, -28), 28),
    volcano=camera('相机_火口与水源', (360, -120, 920), (60, 330, 80), 32),
    terrace=camera('相机_学院分层',(-315,-400,125),(-10,-30,24),38),
)

scn.render.engine = 'BLENDER_EEVEE_NEXT'
scn.eevee.taa_render_samples = 48
scn.render.image_settings.file_format = 'PNG'
SURFACE = ['地形', '海面', '植被', '岛屿生存环', '道路步道', '公共学园层', '中央厅与四院']

def shot(fn, cam, cats, scale, leaders, visible, res=(1920, 1080), dark=False, render=True):
    scn.camera = cam
    set_sky(dark)
    scn.render.resolution_x, scn.render.resolution_y = res
    for L in LABELS:
        vis = bool(L['cats'] & cats)
        for o in (L['ob'], L['plate']):
            o.hide_render = not vis
            o.hide_set(not vis)
        L['lead'].hide_render = not (vis and leaders)
        L['lead'].hide_set(not (vis and leaders))
        L['ob'].constraints[0].target = cam
        L['ob'].scale = (scale, scale, scale)
    for n, c in C.items():
        c.hide_render = n != '标注' and n not in visible
        c.hide_viewport = n != '标注' and n not in visible
    scn.render.filepath = os.path.join(OUT, fn)
    if render:
        if QUICK:
            scn.render.resolution_percentage = 60
            scn.eevee.taa_render_samples = 24
        bpy.ops.render.render(write_still=True)
        print('rendered', fn, flush=True)


def validate():
    error=max(abs(v.co.z-natural_height(v.co.x,v.co.y)) for v in terrain.data.vertices)
    report=dict(stage='Frozen natural terrain concept; not Godot runtime',prototype='Faial relationship; fictional terrain',
        units='metres',terrain_vertices=len(terrain.data.vertices),terrain_faces=len(terrain.data.polygons),
        natural_terrain_max_vertex_error_m=error,terrain_earthworks_m3=0.0,buildings=BUILDINGS,
        routes=NATURAL_ROADS,entries=ENTRIES,service_entries=service_links,
        reservoir_water_z=RES_Z,reservoir_bank_z=RES_BANK_Z,failures=[])
    if error>.001: report['failures'].append('Natural terrain modified')
    blocked=[n for n,d in NATURAL_ROADS.items() if d.get('blocked')]
    if blocked: report['failures'].append('Blocked routes: '+', '.join(blocked))
    base_intrusion=0
    for n,d in BUILDINGS.items():
        x,y=d['center'];sx,sy=d['size'];rot=d['rotation']
        for i in range(25):
            for j in range(25):
                lx,ly=(i/24-.5)*sx,(j/24-.5)*sy
                if d['shape']=='circle' and (2*lx/sx)**2+(2*ly/sy)**2>1: continue
                wx,wy=x+lx*cos(rot)-ly*sin(rot),y+lx*sin(rot)+ly*cos(rot)
                base_intrusion=max(base_intrusion,mesh_height(wx,wy)-d['floor_z'])
    report['building_floor_max_intrusion_m']=base_intrusion
    if base_intrusion>.02: report['failures'].append('Building intersects natural ground')
    report['passed']=not report['failures']
    with open(os.path.join(OUT,'validation.json'),'w',encoding='utf-8') as f: json.dump(report,f,ensure_ascii=False,indent=2)
    print('VALIDATION',json.dumps({k:v for k,v in report.items() if k not in ('buildings','routes','entries','service_entries')},ensure_ascii=False),flush=True)
    return report['passed']

valid = validate()
shot('00_全岛外观.png', CAMS['hero'], set(), 1, False, SURFACE, render=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'island_structure.blend'))
if RENDER and valid:
    shot('00_全岛外观.png', CAMS['hero'], set(), 1, False, SURFACE)
    shot('01_全岛鸟瞰.png', CAMS['overview'], {I, I+'_plan'}, 22, True, SURFACE)
    shot('02_全岛平面.png', CAMS['plan'], {I, I + '_plan'}, 22, False, SURFACE, (2200, 2200))
    shot('03_学园平面.png', CAMS['campus_plan'], {P}, 7.5, False, SURFACE, (2000, 2000))
    shot('04_学园透视.png', CAMS['campus'], {P}, 6, True, SURFACE)
    shot('07_学院分层.png', CAMS['terrace'], set(),1,False,SURFACE)
    shot('05_地下剖视.png', CAMS['under'], {U}, 5.5, True,
         ['中央厅与四院', '地下勤务层', '地下封印层', '地表基准'], dark=True)
    shot('06_火口与水源.png', CAMS['volcano'], set(), 1, False, SURFACE)
    shot('00_全岛外观.png', CAMS['hero'], set(), 1, False, SURFACE, render=False)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'island_structure.blend'))
if not valid:
    raise RuntimeError('Model validation failed; see validation.json')
