# 生活翼平面图：竖向起居厅 + 夹道（售楼处户型图式，按比例）。单位：米，北在上。
# 运行：python plan.py → plan.svg（主题色变量版，供对话内嵌）与 levels.json（地坪标高）
import json, os

# ---- 尺寸
ZD, ZL = 13.2, 22.4        # 寝区南北进深（中廊双面排房）、东西长
LANE = 3.0                 # 夹道宽
HALL = 10.0                # 起居厅东西宽
BATH = 6.0                 # 浴室附楼南北深
W = 2 * ZL + HALL          # 东西总宽
NS = 2 * ZD + LANE         # 寝区段南北总长
STEP_Y = ZD                # 起居厅南北两台的分界（与夹道北沿对齐）
GRADE = 0.025              # 北高南低（旧岛屿模型南坡量级，待地形重做后核对）
TOTAL_NS = NS + BATH

def ground(y): return GRADE * (TOTAL_NS - y)
def floor(y0, y1): return round(max(ground(y0), ground(y1)) + 0.3, 2)
LEVELS = {
    '甲区 / 丁区': floor(0, ZD), '起居厅北台': floor(0, STEP_Y),
    '丙区 / 乙区': floor(ZD + LANE, NS), '起居厅南台': floor(STEP_Y, NS),
    '浴室盥洗': floor(NS, TOTAL_NS), '夹道（顺坡）': [round(ground(ZD), 2), round(ground(ZD + LANE), 2)],
}
LEVELS['起居厅北台'] = LEVELS['甲区 / 丁区']           # 北台与北区同高，寝区门不设台阶
LEVELS['起居厅南台'] = LEVELS['丙区 / 乙区']

# ---- 画布
K, OX, OY = 10.0, 66, 70
out = []
def px(x): return OX + x * K
def py(y): return OY + y * K
def rect(x0, y0, x1, y1, cls=None, fill=None, extra=''):
    x0, x1 = sorted((x0, x1)); y0, y1 = sorted((y0, y1))
    a = f' class="{cls}"' if cls else ''
    f = f' fill="{fill}"' if fill else ''
    out.append(f'<rect{a}{f} x="{px(x0):.1f}" y="{py(y0):.1f}" width="{(x1-x0)*K:.1f}" height="{(y1-y0)*K:.1f}" {extra}/>')
def text(x, y, s, cls='ts', anchor='middle'):
    out.append(f'<text class="{cls}" x="{px(x):.1f}" y="{py(y):.1f}" text-anchor="{anchor}" dominant-baseline="central">{s}</text>')
def outline(x0, y0, x1, y1, rx=0):
    x0, x1 = sorted((x0, x1)); y0, y1 = sorted((y0, y1))
    out.append(f'<rect x="{px(x0):.1f}" y="{py(y0):.1f}" width="{(x1-x0)*K:.1f}" height="{(y1-y0)*K:.1f}" rx="{rx}" fill="none" stroke="var(--t)" stroke-width="0.8"/>')

def mapper(g):
    def m(a, b):
        (x0, y0), (x1, y1) = g(*a), g(*b)
        return min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)
    return m
def space(m, a, b, cls, *lines):
    x0, y0, x1, y1 = m(a, b)
    out.append(f'<g class="{cls}">'); rect(x0, y0, x1, y1)
    n = len(lines)
    for i, s in enumerate(lines):
        text((x0 + x1) / 2, (y0 + y1) / 2 + (i - (n - 1) / 2) * 1.8, s, 'th' if (i == 0 and n > 1) else 'ts')
    out.append('</g>')
def gap(m, a, b, fill=None):
    x0, y0, x1, y1 = m(a, b)
    rect(x0, y0, x1, y1, cls=None if fill else 'c-gray', fill=fill)

# 寝区模板 (u 自外端向起居厅, v 自寝区北沿向南)
ZONES = {
    '甲': (lambda u, v: (u, v), 1),
    '丙': (lambda u, v: (u, ZD + LANE + v), 9),
    '丁': (lambda u, v: (W - u, v), 13),
    '乙': (lambda u, v: (W - u, ZD + LANE + v), 5),
}

# ---- 底板（墙）
for _, (g, _) in ZONES.items():
    m = mapper(g); rect(*m((0, 0), (ZL, ZD)), fill='var(--p)')
rect(ZL, 0, ZL + HALL, NS, fill='var(--p)')
rect(ZL, NS, ZL + HALL, TOTAL_NS, fill='var(--p)')
# 夹道
for x0 in (0, ZL + HALL):
    out.append('<g class="c-green">'); rect(x0, ZD, x0 + ZL, ZD + LANE)
    text(x0 + ZL / 2, ZD + LANE / 2, '夹道 3 m · 碎石', 'ts'); out.append('</g>')

# ---- 寝区
for name, (g, n0) in ZONES.items():
    m = mapper(g)
    num = n0
    for row, (v0, v1, dv) in enumerate([(0.6, 5.4, (5.4, 5.7)), (7.8, 12.6, (7.5, 7.8))]):
        for k in range(3):
            u0 = 0.6 + k * 3.85
            occ = k >= 1
            label = f'{num:02d}' if occ else '空'
            if occ: num += 1
            space(m, (u0, v0), (u0 + 3.6, v1), 'c-amber' if occ else 'c-gray', label)
            gap(m, (u0 + 1.35, dv[0]), (u0 + 2.25, dv[1]))
            bv = (0.9, 2.9) if row == 0 else (10.3, 12.3)            # 床 0.9×2.0
            x0, y0, x1, y1 = m((u0 + 2.55, bv[0]), (u0 + 3.45, bv[1])); outline(x0, y0, x1, y1, 1)
    space(m, (0.6, 5.7), (11.9, 7.5), 'c-gray')                       # 中廊
    space(m, (12.2, 0.6), (18.2, 8.1), 'c-teal', f'{name}区', '活动厅')
    space(m, (12.2, 8.4), (18.2, 12.6), 'c-teal', '备餐间')
    space(m, (18.5, 0.6), (21.8, 4.05), 'c-blue', '厕')
    space(m, (18.5, 4.35), (21.8, 8.85), 'c-gray', '门厅')
    space(m, (18.5, 9.15), (21.8, 12.6), 'c-gray', '储物')
    for a, b in [((11.9, 5.8), (12.2, 7.4)), ((14.5, 8.1), (15.5, 8.4)), ((18.2, 5.0), (18.5, 6.0)),
                 ((19.6, 4.05), (20.6, 4.35)), ((19.6, 8.85), (20.6, 9.15))]:
        gap(m, a, b)
    gap(m, (21.8, 5.85), (ZL, 7.35), '#EF9F27')                      # 隔离门

# ---- 起居厅（两台）
out.append('<g class="c-teal">'); rect(ZL, 0.6, ZL + HALL, STEP_Y)
text(ZL + HALL / 2, 4.4, '起居厅', 'th'); text(ZL + HALL / 2, 6.2, f'北台 +{LEVELS["起居厅北台"]:.2f}', 'ts')
text(ZL + HALL / 2, 8.0, '净高约 7 m', 'ts'); text(ZL + HALL / 2, 9.6, '高侧窗采光', 'ts')
out.append('</g>')
out.append('<g class="c-teal">'); rect(ZL, STEP_Y + 0.9, ZL + HALL, NS - 0.6)
text(ZL + HALL / 2, 17.0, f'南台 +{LEVELS["起居厅南台"]:.2f}', 'ts'); out.append('</g>')
out.append('<g class="c-gray">'); rect(ZL, STEP_Y, ZL + HALL, STEP_Y + 0.9); out.append('</g>')
for i in range(1, 3):                                                # 三级台阶
    yy = py(STEP_Y + 0.3 * i)
    out.append(f'<line x1="{px(ZL):.1f}" y1="{yy:.1f}" x2="{px(ZL+HALL):.1f}" y2="{yy:.1f}" stroke="var(--t)" stroke-width="0.6"/>')
# 侧门（通夹道）
for x0 in (ZL, ZL + HALL - 0.6):
    rect(x0, ZD, x0 + 0.6, ZD + LANE, fill='var(--p)')
    rect(x0, ZD + 0.9, x0 + 0.6, ZD + 2.1, cls='c-gray')
# 陈设
rect(24.0, 0.6, 27.0, 1.5, fill='var(--t)'); text(25.5, 2.4, '壁炉', 'ts')
outline(*[v for v in (25.0, 20.0, 29.8, 25.4)], 2); text(27.4, 22.7, '长桌', 'ts')
outline(29.6, 26.6, 31.6, 28.2, 1); text(30.6, 27.4, '琴', 'ts')
out.append(f'<circle cx="{px(24.4):.1f}" cy="{py(27.2):.1f}" r="7" fill="none" stroke="var(--t)" stroke-width="0.8"/>'); text(24.4, 27.2, '棋', 'ts')
# 北门与门廊
out.append('<g class="c-gray">'); rect(28.0, -1.8, 31.6, 0); out.append('</g>')
rect(29.0, 0, 30.6, 0.6, fill='#D85A30')
# 浴室附楼
out.append('<g class="c-blue">'); rect(ZL + 0.6, NS + 0.0, 27.2, TOTAL_NS - 0.6); text(25.0, NS + 3.0, '盥洗', 'ts'); out.append('</g>')
out.append('<g class="c-blue">'); rect(27.5, NS + 0.0, ZL + HALL - 0.6, TOTAL_NS - 0.6); text(30.1, NS + 3.0, '浴室', 'ts'); out.append('</g>')
rect(24.5, NS - 0.6, 25.5, NS, cls='c-gray'); rect(27.2, NS + 2.5, 27.5, NS + 3.5, cls='c-gray')
# 夹道外端栅门
rect(0, ZD + 0.2, 0.4, ZD + LANE - 0.2, fill='#D85A30'); rect(W - 0.4, ZD + 0.2, W, ZD + LANE - 0.2, fill='#D85A30')
# 守夜点
out.append(f'<circle cx="{px(ZL+HALL/2):.1f}" cy="{py(19.0):.1f}" r="4" fill="#BA7517"/>')

# ---- 外部标注
text(29.8, -3.0, '北门 · 往中央厅', 'ts')
text(-0.6, ZD + 0.7, '西门', 'th', 'end'); text(-0.6, ZD + 2.4, '誓约庭', 'ts', 'end')
text(W + 0.6, ZD + 0.7, '东门', 'th', 'start'); text(W + 0.6, ZD + 2.4, '后勤院', 'ts', 'start')
for y, k in ((ZD / 2, '甲区 / 丁区'), (ZD + LANE + ZD / 2, '丙区 / 乙区')):
    text(-0.6, y, f'+{LEVELS[k]:.2f}', 'ts', 'end'); text(W + 0.6, y, f'+{LEVELS[k]:.2f}', 'ts', 'start')
text(ZL + HALL / 2, TOTAL_NS + 1.4, f'+{LEVELS["浴室盥洗"]:.2f}', 'ts')
# 尺寸线（下）
yd = TOTAL_NS + 3.2
def dim(x0, x1, y, s):
    out.append(f'<line x1="{px(x0):.1f}" y1="{py(y):.1f}" x2="{px(x1):.1f}" y2="{py(y):.1f}" stroke="var(--t)" stroke-width="0.5"/>')
    for x in (x0, x1): out.append(f'<line x1="{px(x):.1f}" y1="{py(y)-4:.1f}" x2="{px(x):.1f}" y2="{py(y)+4:.1f}" stroke="var(--t)" stroke-width="0.5"/>')
    text((x0 + x1) / 2, y + 1.2, s, 'ts')
dim(0, ZL, yd, f'{ZL:.1f} m'); dim(ZL, ZL + HALL, yd, f'{HALL:.0f} m'); dim(ZL + HALL, W, yd, f'{ZL:.1f} m')
dim(0, W, yd + 3.0, f'{W:.1f} m')
# 比例尺与指北
by = yd + 6.2
rect(0, by, 5, by + 0.5, fill='var(--p)'); rect(5, by, 10, by + 0.5, fill='var(--t)')
text(0, by + 1.6, '0', 'ts', 'start'); text(10, by + 1.6, '10 m', 'ts', 'start')
nx = W - 1
out.append(f'<path d="M{px(nx):.1f} {py(by)-10:.1f} L{px(nx)+6:.1f} {py(by)+8:.1f} L{px(nx):.1f} {py(by)+4:.1f} L{px(nx)-6:.1f} {py(by)+8:.1f} Z" fill="var(--p)"/>')
text(nx - 1.4, by + 0.4, '北', 'ts', 'end')
# 图例
legend = [('c-amber', '住房（R-xx）'), ('c-gray', '空房 · 交通 · 储物'), ('c-teal', '活动厅 · 起居厅'),
          ('c-blue', '厕 · 盥洗 · 浴室'), ('c-green', '夹道（露天）')]
lx, ly = 14.0, by + 0.25
for cls, s in legend[:2]:
    out.append(f'<g class="{cls}">'); rect(lx, ly - 0.6, lx + 1.4, ly + 0.6); out.append('</g>')
    text(lx + 1.9, ly, s, 'ts', 'start'); lx += 13.5
ly2, lx = ly + 2.4, 0.0
for cls, s in legend[2:]:
    out.append(f'<g class="{cls}">'); rect(lx, ly2 - 0.6, lx + 1.4, ly2 + 0.6); out.append('</g>')
    text(lx + 1.9, ly2, s, 'ts', 'start'); lx += 13.5
lx += 0.5
rect(lx, ly2 - 0.25, lx + 1.4, ly2 + 0.25, fill='#EF9F27'); text(lx + 1.9, ly2, '寝区隔离门', 'ts', 'start')
ly3 = ly2 + 2.4
rect(0, ly3 - 0.25, 1.4, ly3 + 0.25, fill='#D85A30'); text(1.9, ly3, '出口', 'ts', 'start')
out.append(f'<circle cx="{px(9.7):.1f}" cy="{py(ly3):.1f}" r="4" fill="#BA7517"/>'); text(10.4, ly3, '守夜点', 'ts', 'start')
outline(16.5, ly3 - 0.6, 17.4, ly3 + 0.6, 1); text(17.9, ly3, '床 0.9 × 2 m', 'ts', 'start')
text(W, ly3, '标高单位：米（相对南端地面）', 'ts', 'end')

H = int(py(ly3 + 1.6))
desc = (f'竖向起居厅居中，东西各两个寝区，每对之间留 3 米夹道；寝区中廊两侧各三间房；北门在起居厅北端，'
        f'西门东门在夹道外端，浴室盥洗接在南端。外廓 {W:.1f} × {TOTAL_NS:.1f} 米。')
svg = (f'<svg width="100%" viewBox="0 0 680 {H}" role="img"><title>生活翼平面图（竖厅加夹道）</title><desc>{desc}</desc>'
       + ''.join(out) + '</svg>')
here = os.path.dirname(os.path.abspath(__file__))
open(os.path.join(here, 'plan.svg'), 'w', encoding='utf-8').write(svg)
roof = 4 * ZL * ZD + HALL * NS + HALL * BATH
json.dump({'外廓_m': [round(W, 1), round(TOTAL_NS, 1)], '屋面_m2': round(roof), '人均屋面_m2': round(roof / 16, 1),
           '地坪标高_m': LEVELS, '坡度假设': GRADE}, open(os.path.join(here, 'levels.json'), 'w', encoding='utf-8'),
          ensure_ascii=False, indent=2)
print(round(W, 1), round(TOTAL_NS, 1), round(roof), H, len(svg))
print(LEVELS)
