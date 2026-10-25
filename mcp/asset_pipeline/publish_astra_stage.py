"""Cut Astra's approved boards. No painting, recolouring or generative art.

Paper is flood-separated. Pose bands are fitted to the same anatomical head
unit, with every crop/scale/mirror recorded beside the published assets.
"""
from pathlib import Path
from collections import deque
import hashlib
import json
import math
import struct
import wave
from PIL import Image, ImageOps, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'mcp/asset_pipeline/sources/Internal/ModernStage/Astra'
DEST = ROOT / 'godot_csharp/ShareAsset/Internal/Props/ModernStage'


def isolate(image):
    image = image.convert('RGBA')
    w, h = image.size
    pixels = image.load()
    seen = bytearray(w * h)
    queue = deque()
    def offer(x, y):
        at = y * w + x
        if seen[at]: return
        r, g, b, a = pixels[x, y]
        if a < 20 or (r > 165 and g > 155 and b > 135 and max(r, g, b) - min(r, g, b) < 75):
            seen[at] = 1; queue.append((x, y))
    for x in range(w): offer(x, 0); offer(x, h - 1)
    for y in range(h): offer(0, y); offer(w - 1, y)
    while queue:
        x, y = queue.popleft()
        if x: offer(x - 1, y)
        if x + 1 < w: offer(x + 1, y)
        if y: offer(x, y - 1)
        if y + 1 < h: offer(x, y + 1)
    mask = bytearray(0 if seen[i] else 255 for i in range(w * h))
    visited = bytearray(w * h)
    largest = []
    for start in range(w * h):
        if visited[start] or not mask[start]: continue
        component = []
        queue = deque([start]); visited[start] = 1
        while queue:
            at = queue.popleft(); component.append(at)
            x, y = at % w, at // w
            for nxt in ((at - 1 if x else -1), (at + 1 if x + 1 < w else -1),
                        (at - w if y else -1), (at + w if y + 1 < h else -1)):
                if nxt >= 0 and not visited[nxt] and mask[nxt]:
                    visited[nxt] = 1; queue.append(nxt)
        if len(component) > len(largest): largest = component
    alpha = bytearray(w * h)
    for at in largest: alpha[at] = 255
    image.putalpha(Image.frombytes('L', (w, h), bytes(alpha)))
    return image


def publish():
    DEST.mkdir(parents=True, exist_ok=True)
    records = {}
    board = Image.open(SOURCE / 'environment.png').convert('RGBA')
    cuts = {'wood': (1118, 56, 1498, 290), 'stone': (1118, 362, 1498, 594),
            'stone-wall': (1118, 362, 1498, 433),
            'stone-trim': (1115, 358, 1501, 361),
            'facade': (30, 38, 1079, 334), 'table': (296, 464, 816, 766),
            'chair': (108, 483, 242, 746)}
    for name, region in cuts.items():
        cut = board.crop(region)
        if name in ('table', 'chair'):
            outline = ([(5,5),(515,5),(515,181),(498,181),(498,299),(475,299),
                        (475,210),(34,210),(34,299),(10,299),(10,181),(5,181)] if name == 'table' else
                       [(10,5),(25,5),(25,20),(107,20),(107,5),(122,5),(122,98),
                        (126,98),(126,185),(122,185),(122,256),(108,256),(108,193),
                        (24,193),(24,256),(10,256),(10,185),(6,185),(6,98),(10,98)])
            alpha = Image.new('L', cut.size)
            ImageDraw.Draw(alpha).polygon(outline, fill=255)
            cut.putalpha(alpha)
        cut.save(DEST / f'{name}.png')
        records[name] = {'source': 'environment.png', 'crop': list(region), 'alpha_separated': name in ('table', 'chair')}
        if name in ('table', 'chair'): records[name]['alpha_polygon'] = outline
    originals = Image.open(SOURCE / 'scholar-original.png')
    short = Image.open(SOURCE / 'scholar-short.png')
    poses = [('draw', short, (105, 246, 423, 741), 294, 431, 737, True, 267),
             ('raise', originals, (525, 45, 915, 755), 185, 316, 750, False, 760),
             ('strike', originals, (939, 140, 1300, 755), 185, 316, 750, False, 1131),
             ('block', short, (1330, 246, 1730, 741), 294, 431, 737, True, 1560)]
    head_unit = 140
    for name, source, box, crown, chin, sole, mirror, foot_x in poses:
        cut = isolate(source.crop(box))
        split = chin - box[1]
        upper = cut.crop((0, 0, cut.width, split))
        lower = cut.crop((0, split, cut.width, sole - box[1]))
        scale = head_unit / (chin - crown)
        width = round(cut.width * scale)
        upper = upper.resize((width, round(upper.height * scale)), Image.Resampling.LANCZOS)
        lower = lower.resize((width, head_unit * 2), Image.Resampling.LANCZOS)
        result = Image.new('RGBA', (width, upper.height + lower.height))
        result.alpha_composite(upper)
        result.alpha_composite(lower, (0, upper.height))
        if mirror: result = ImageOps.mirror(result)
        pivot_x = (foot_x - box[0]) * scale
        if mirror: pivot_x = result.width - pivot_x
        aligned = Image.new('RGBA', (512, result.height))
        aligned.alpha_composite(result, (round(256 - pivot_x), 0))
        aligned.save(DEST / f'scholar-{name}.png')
        records['scholar-' + name] = {'source': 'scholar-short.png' if source is short else 'scholar-original.png',
             'crop': list(box), 'skull_crown_y': crown, 'chin_y': chin, 'sole_y': sole,
             'head_unit': head_unit, 'below_chin_height': head_unit * 2, 'mirror': mirror, 'source_pivot_x': foot_x,
             'transforms': 'alpha-separation; head-band uniform scale; below-chin vertical fit; optional mirror; no repaint'}
    lines = ['[gd_resource type="SpriteFrames" format=3]']
    for name, *_ in poses:
        lines.append(f'[ext_resource type="Texture2D" path="res://ShareAsset/Internal/Props/ModernStage/scholar-{name}.png" id="{name}"]')
    animations = []
    for animation, frame in [('idle_front', 'draw'), ('idle_back', 'draw'), ('idle_left', 'draw'),
                             ('draw_front', 'draw'), ('raise_front', 'raise'), ('strike_front', 'strike'), ('block_front', 'block')]:
        animations.append('{"frames": [{"duration": 1.0, "texture": ExtResource("%s")}], "loop": true, "name": &"%s", "speed": 1.0}' % (frame, animation))
    lines += ['[resource]', 'animations = [' + ',\n'.join(animations) + ']', 'metadata/world_scale = 0.1']
    (DEST / 'scholar.tres').write_text('\n\n'.join(lines) + '\n', encoding='utf-8')
    with wave.open(str(DEST / 'cane-impact.wav'), 'wb') as audio:
        audio.setparams((1, 2, 24000, 0, 'NONE', 'not compressed'))
        audio.writeframes(b''.join(struct.pack('<h', round(22000 * math.exp(-i / 24000 * 38) *
          (.45 * math.sin(2 * math.pi * 177 * i / 24000) + .22 * math.sin(2 * math.pi * 877 * i / 24000)))) for i in range(7200)))
    sources = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in SOURCE.glob('*.png')}
    (DEST / 'publication.json').write_text(json.dumps({'schema': 2, 'artist': 'Astra', 'sources': sources,
       'world_head_unit': 14, 'world_below_chin': 28, 'logical_cell': 32, 'cuts': records}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Published Astra source cuts: {DEST}')


if __name__ == '__main__': publish()
