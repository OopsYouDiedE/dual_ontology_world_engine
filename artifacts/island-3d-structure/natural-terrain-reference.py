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
RES_X, RES_Y, RES_FLOOR = 330.0, 270.0, 60.0
RES_Z = RES_FLOOR + 4
RES_RADIUS, RES_BANK_Z = 49.0, 67.0
HARBOR_Y = -shore_R(-pi / 2) + 12
SIGNAL_X, SIGNAL_Y, SIGNAL_Z = 600.0, 390.0, 62.0
PADS = [  # x, y, 平台半径, 过渡宽度, 高度
    (0, HARBOR_Y + 22, 68, 25, 3.0),
    (SIGNAL_X, SIGNAL_Y, 30, 20, SIGNAL_Z),
    (RES_X, RES_Y - 84, 20, 16, 60.0),
]
TERRACE = (-350, 260, 80)
for row in range(3):
    for column in range(3):
        PADS.append((TERRACE[0] - 30 + column * 30, TERRACE[1] - 28 + row * 28,
                     14, 5, 42.0 + row * 6))
QUARRY = (425, -65, 42)
PADS.append((QUARRY[0], QUARRY[1], 48, 20, 24.0))
WEST_T = pi + 0.12
WEST_R = shore_R(WEST_T) - 42
PADS.append((WEST_R * cos(WEST_T), WEST_R * sin(WEST_T), 58, 22, 3.5))
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


