# 给 Blender 渲染图叠加中文注记（系统 Python）。读取 renders/<视角>.png 与 <视角>_labels.json，输出 <视角>_labeled.png。
# 运行：python label_renders.py [视角 ...]
import json, os, sys
from PIL import Image, ImageDraw
from cartography import font, halo_text
from dem import HERE

R = os.path.join(HERE, 'renders')
SKIP = ('门廊', '风雨廊', '台基')
NAME = {'诊疗所与安全端': '诊疗所·安全端', '文契院（封闭部分）': '文契院（封闭）', '全岛最高点': '全岛最高点 351 m'}
TITLE = {'aerial': '学园鸟瞰（自南面海上）', 'avenue': '自门楼内侧沿大道北望中央厅', 'living': '生活翼（起居厅加高，两侧高侧窗；夹道露天）',
         'top': '学园俯视（正投影，主轴竖直向上）', 'island': '学园之岛南端：学园所在的熔岩台地'}
ONLY = {'island': ('中央厅', '全岛最高点', '港'), 'avenue': ('中央厅', '讲堂', '文契院（封闭部分）', '名录库', '生活翼', '后勤院')}

def run(view):
    img = Image.open(os.path.join(R, f'{view}.png')).convert('RGB')
    labels = json.load(open(os.path.join(R, f'{view}_labels.json'), encoding='utf-8'))
    d = ImageDraw.Draw(img); f = font(22, True); boxes = []
    for nm, (x, y, depth) in sorted(labels.items(), key=lambda kv: kv[1][2]):     # 近处优先
        if any(k in nm for k in SKIP) or (view in ONLY and nm not in ONLY[view]):
            continue
        text = NAME.get(nm, nm)
        if view == 'island' and nm == '中央厅':
            text = '学园'
        w = 22 * len(text) + 10
        box = (x - w / 2, y - 16, x + w / 2, y + 16)
        if any(not (box[2] < b[0] or box[0] > b[2] or box[3] < b[1] or box[1] > b[3]) for b in boxes):
            continue
        boxes.append(box)
        halo_text(d, (x, y), text, f, fill=(30, 24, 18), halo=(250, 246, 236), w=4)
    halo_text(d, (24, 24), TITLE[view], font(28, True), anchor='la', fill=(30, 24, 18), halo=(250, 246, 236), w=5)
    halo_text(d, (24, img.height - 24), '地形：Copernicus DEM GLO-30（科尔沃岛，三向等比缩为 0.5 倍）；建筑体量按学园总平面，屋面、材质为示意', font(16), anchor='lb',
              fill=(30, 24, 18), halo=(250, 246, 236), w=3)
    out = os.path.join(R, f'{view}_labeled.png'); img.save(out); return out

if __name__ == '__main__':
    for v in (sys.argv[1:] or ['aerial', 'avenue', 'living', 'top', 'island']):
        print(run(v))
