"""Cut Astra's existing border artwork into the adapter's floor topology.

Only crop, quarter-turn and paste operations are used. The transparent atlas
is an ornament overlay; the continuous authored floor remains underneath it.
"""
import hashlib
import json
import re
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'mcp/asset_pipeline/sources/Internal/Props/AuthoredRooms/shura_gallery.png'
TABLE = ROOT / 'mcp/rpg_adapter/data/autotile_tables.gd'
OUTPUT = ROOT / 'godot_csharp/ShareAsset/Internal/Props/AstraTerrain'
CELL = 120
HALF = CELL // 2
# Explicit source pixels, not inferred segmentation or generated transitions.
CUTS = {'outer': [269, 399, 329, 459], 'inner': [269, 399, 293, 423],
        'horizontal': [370, 960, 430, 980], 'vertical': [269, 450, 289, 510]}
BITS = {'N': 12, 'E': 0, 'S': 4, 'W': 8, 'NE': 15, 'SE': 3, 'SW': 7, 'NW': 11}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def publish():
    source = Image.open(SOURCE).convert('RGBA')
    for name, (x0, y0, x1, y1) in CUTS.items():
        if not (0 <= x0 < x1 <= source.width and 0 <= y0 < y1 <= source.height):
            raise ValueError(f'{name}: source crop outside artwork')
    # Consume the existing adapter table; no second topology implementation.
    table = TABLE.read_text(encoding='utf-8').split('const FLOOR_PEERING_BITS: Array = [', 1)[1].split('\n]', 1)[0]
    names = {'RIGHT_SIDE': 'E', 'BOTTOM_RIGHT_CORNER': 'SE', 'BOTTOM_SIDE': 'S',
             'BOTTOM_LEFT_CORNER': 'SW', 'LEFT_SIDE': 'W', 'TOP_LEFT_CORNER': 'NW',
             'TOP_SIDE': 'N', 'TOP_RIGHT_CORNER': 'NE'}
    masks = []
    for line in table.splitlines():
        if '# Shape' in line:
            masks.append([names[name] for name in re.findall(r'TileSet.CELL_NEIGHBOR_(\w+)', line)])
    if len(masks) != 48:
        raise ValueError('Adapter table must contain 48 RPG source shapes')
    # Shape 47 duplicates the isolated peering pattern of shape 46.
    masks = masks[:47]
    pieces = {key: source.crop(rect) for key, rect in CUTS.items()}
    atlas = Image.new('RGBA', (8 * CELL, 6 * CELL))
    entries = []
    # Rotation is clockwise from the source's north-west corner.
    quadrants = [(0, 0, 'N', 'W', 'NW', 0), (1, 0, 'N', 'E', 'NE', 1),
                 (0, 1, 'S', 'W', 'SW', 3), (1, 1, 'S', 'E', 'SE', 2)]
    for shape, neighbors in enumerate(masks):
        tile = Image.new('RGBA', (CELL, CELL))
        for qx, qy, horizontal, vertical, diagonal, turn in quadrants:
            quarter = Image.new('RGBA', (HALF, HALF))
            if horizontal not in neighbors and vertical not in neighbors:
                quarter = pieces['outer'].rotate(-90 * turn)
            elif horizontal in neighbors and vertical in neighbors:
                if diagonal not in neighbors:
                    # Opposite elbow from the same clean source corner. Keep only
                    # its 24px junction, without carrying a bright floor square.
                    quarter.paste(pieces['inner'].rotate(180), (0, 0))
                    quarter = quarter.rotate(-90 * turn)
            else:
                if horizontal not in neighbors:
                    strip = pieces['horizontal']
                    if horizontal == 'N': strip = strip.rotate(180)
                    quarter.paste(strip, (0, 0 if horizontal == 'N' else HALF - strip.height))
                if vertical not in neighbors:
                    strip = pieces['vertical']
                    if vertical == 'E': strip = strip.rotate(180)
                    quarter.paste(strip, (0 if vertical == 'W' else HALF - strip.width, 0))
            tile.paste(quarter, (qx * HALF, qy * HALF))
        atlas.paste(tile, ((shape % 8) * CELL, (shape // 8) * CELL))
        entries.append({'shape': shape, 'neighbors': neighbors, 'atlas': [shape % 8, shape // 8]})
    OUTPUT.mkdir(parents=True, exist_ok=True)
    atlas.save(OUTPUT / 'gallery_border.png')
    lines = ['[gd_resource type="TileSet" load_steps=3 format=3]',
             '[ext_resource type="Texture2D" path="res://ShareAsset/Internal/Props/AstraTerrain/gallery_border.png" id="atlas"]',
             '[sub_resource type="TileSetAtlasSource" id="source"]',
             'texture = ExtResource("atlas")', f'texture_region_size = Vector2i({CELL}, {CELL})']
    for entry in entries:
        x, y = entry['atlas']
        prefix = f'{x}:{y}/0'
        lines += [f'{x}:{y}/size_in_atlas = Vector2i(1, 1)', f'{prefix}/terrain_set = 0', f'{prefix}/terrain = 0']
        for name in entry['neighbors']:
            enum = next(key.lower() for key, short in names.items() if short == name)
            lines.append(f'{prefix}/terrains_peering_bit/{enum} = 0')
    lines += ['[resource]', f'tile_size = Vector2i({CELL}, {CELL})', 'terrain_set_0/mode = 0',
              'terrain_set_0/terrain_0/name = "Astra gallery border"',
              'terrain_set_0/terrain_0/color = Color(0.2, 0.5, 0.45, 1)', 'sources/0 = SubResource("source")']
    (OUTPUT / 'GalleryBorder.tres').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    manifest = {'schema': 1, 'source': SOURCE.relative_to(ROOT).as_posix(), 'source_sha256': digest(SOURCE),
                'source_size': list(source.size), 'cuts': CUTS, 'topology': TABLE.relative_to(ROOT).as_posix(),
                'topology_sha256': digest(TABLE), 'tile_pixels': CELL, 'world_cell_pixels': 32,
                'operations': 'exact crop, explicit quarter turns, paste onto transparent canvas; no resampling or color changes',
                'scope': 'border overlay only; floor phase, center ornament and collision remain owned by the stage',
                'limitations': 'Source illustration contains baked light and imperfect line spacing. Corner crops retain original floor pixels; visual review required.',
                'tiles': entries, 'atlas_sha256': digest(OUTPUT / 'gallery_border.png')}
    (OUTPUT / 'publication.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Published Astra gallery border from existing artwork and adapter topology')


if __name__ == '__main__':
    publish()
