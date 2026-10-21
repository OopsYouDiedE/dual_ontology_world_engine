"""Publish scene markers into the shared geometry contract; retain authored room regions."""
import argparse
import json
from pathlib import Path

from scene_geometry import OUTDOOR_FIELDS, outdoor_metadata, scene_exits, scene_point, scene_source, vector

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / 'godot_csharp'
CONTRACT = GAME / 'Contracts/geometry_contract.json'


def scene_markers(path):
    nodes, shapes = scene_source(path)
    result = {'scene': 'res://' + path.relative_to(GAME).as_posix(),
              'size': vector(nodes['']['fields']['metadata/map_size']), 'sites': {}, 'stations': {},
              'stands': {}, 'spawns': {}, 'exits': scene_exits(nodes, shapes), **outdoor_metadata(nodes)}
    groups = {'Sites': 'sites', 'Stations': 'stations', 'Stands': 'stands', 'Spawns': 'spawns'}
    for node_path, node in nodes.items():
        parent, _, name = node_path.rpartition('/')
        if parent in groups and node['type'] == 'Marker2D':
            value = {'point': scene_point(nodes, node_path), 'path': node_path}
            if 'metadata/label' in node['fields']:
                value['label'] = json.loads(node['fields']['metadata/label'])
            result[groups[parent]][name] = value
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
        for field in OUTDOOR_FIELDS:
            item.pop(field, None)
        item.update(update)
        for door in item['doors']:
            if door.get('target_map') == 'living_hall':
                door['target_map'] = 'living_floor'
                if door['to'] == 'living_hall':
                    door['to'] = 'living_floor'
    args.out.write_text(json.dumps(contract, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
