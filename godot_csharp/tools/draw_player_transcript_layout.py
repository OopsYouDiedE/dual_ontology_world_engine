from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
folder = ROOT / 'artifacts/basic-ui-system/full-ui'
folder.mkdir(parents=True, exist_ok=True)
font_path = ROOT / 'godot_csharp/ShareAsset/External/ZhaohuaTypeWriter/ZhaohuaTypeWriter.ttf'

def font(size):
    return ImageFont.truetype(str(font_path), size)

samples = [
    ('wake', '生活翼 · R-05', '侦探（心声）', ['床板很硬。灯油的味道。窗外是灰的。'], '继续  ▸'),
    ('investigation', '生活翼 · 门厅', '现场调查', ['来源：眼前的物件', '现象与线索', '在纸页中逐条核对，再返回现场。'], '返回现场'),
    ('campus', '学园 · 中央厅', '学园生活', ['日程、眼前的人与可调查物品', '交谈与选择在共用界面中呈现。'], '继续  ▸'),
    ('confrontation', '学园 · 对账', '依据与陈述', ['先核对眼前事实', '选择有依据的陈述', '尚未确认的内容暂且不答'], '确认陈述'),
    ('council', '第五日 · 清晨', '名录库之议', ['听取各人的主张', '选择提案与支持', '结果以实际会话为准'], '支持这一条'),
    ('ending', '第五日 · 终局', '安眠者有福 · 完', ['名录库与共同规则的结果', '每个人的处境', '留下与没有交出的东西'], '重新开始'),
]
for key, location, title, lines, action in samples:
    image = Image.new('RGB', (1280, 992), '#1d1a1f')
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, 1279, 59), fill='#282126')
    draw.text((32, 18), location, font=font(23), fill='#ded7c1')
    # 局部地图只作布局预想，物件与人物继续使用游戏真实资源。
    for y in range(84, 920, 32):
        for x in range(128, 1152, 32):
            shade = '#40372f' if (x//32+y//32)%2 else '#463c33'
            draw.rectangle((x, y, x+31, y+31), fill=shade)
    if key in ('wake', 'campus'):
        bounds = (112, 688, 1168, 944)
        fill = '#241f23'
        text_color = '#e0d6bc'
    else:
        bounds = (256, 152, 1024, 864)
        fill = '#ddd0ad'
        text_color = '#403627'
    draw.rounded_rectangle(bounds, radius=7, fill=fill, outline='#8d7955', width=2)
    left, top, right, bottom = bounds
    draw.text((left+32, top+26), title, font=font(30), fill=text_color)
    draw.line((left+32, top+72, right-32, top+72), fill='#8d7955')
    for index, line in enumerate(lines):
        draw.text((left+32, top+96+index*43), line, font=font(23), fill=text_color)
    draw.rounded_rectangle((right-232, bottom-65, right-32, bottom-23), radius=4, fill='#394439', outline='#8d7955')
    draw.text((right-215, bottom-57), action, font=font(22), fill='#e0d6bc')
    image.save(folder / (key+'-expected.png'))
