"""Publish scene markers into the shared geometry contract; retain authored room regions."""
import argparse
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / 'godot_csharp'
CONTRACT = GAME / 'Contracts/geometry_contract.json'


def point(text, name='position'):
    match = re.search(r'^' + re.escape(name) + r' = Vector2\(([^,]+),\s*([^)]+)\)', text, re.MULTILINE)
    return [float(match[1]), float(match[2])] if match else [0, 0]


def scene_markers(path):
    text = path.read_text(encoding='utf-8')
    result = {'scene': 'res://' + path.relative_to(GAME).as_posix(),
              'size': point(text, 'metadata/map_size'), 'sites': {}, 'stations': {},
              'stands': {}, 'spawns': {}, 'exits': []}
    groups = {'Sites': 'sites', 'Stations': 'stations', 'Stands': 'stands', 'Spawns': 'spawns'}
    for block in re.findall(r'(?ms)^\[node\b.*?(?=^\[|\Z)', text):
        header = dict(re.findall(r'(\w+)="([^"]*)"', block.splitlines()[0]))
        parent, name = header.get('parent'), header['name']
        if parent in groups:
            value = {'point': point(block), 'path': parent + '/' + name}
            label = re.search(r'^metadata/label = (.+)$', block, re.MULTILINE)
            if label:
                value['label'] = json.loads(label[1])
            result[groups[parent]][name] = value
        if parent == 'Exits':
            target = re.search(r'^metadata/target = "([^"]+)"', block, re.MULTILINE)
            if target:
                spawn = re.search(r'^metadata/spawn = "([^"]+)"', block, re.MULTILINE)
                result['exits'].append({'id': name, 'target': target[1], 'point': point(block),
                                        'spawn': spawn[1] if spawn else ''})
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, default=CONTRACT)
    args = parser.parse_args()
    contract = json.loads(args.out.read_text(encoding='utf-8'))
    for name, item in contract['maps'].items():
        update = scene_markers(GAME / item['scene'].removeprefix('res://'))
        for group in ('sites', 'stations', 'stands', 'spawns'):
            for marker in update[group].values():
                x, y = marker['point']
                marker['room'] = next((room['id'] for room in item['rooms']
                    if len(item['rooms']) == 1 or any(
                        rx <= x < rx + width and ry <= y < ry + height
                        for rx, ry, width, height in room['rects'])), '')
        item.update(update)
    args.out.write_text(json.dumps(contract, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
