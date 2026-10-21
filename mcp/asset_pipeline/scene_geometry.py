"""Read authored Godot geometry for publication and the matching tool inventory."""
from __future__ import annotations

import json
from pathlib import Path
import re

OUTDOOR_FIELDS = ('outdoor_origin', 'outdoor_core', 'outdoor_cores', 'outdoor_origins')


def scene_source(path: Path):
    nodes, shapes = {}, {}
    text = path.read_text(encoding='utf-8')
    for block in re.findall(r'(?ms)^\[(?:node|sub_resource)\b.*?(?=^\[|\Z)', text):
        header = dict(re.findall(r'(\w+)="([^"]*)"', block.splitlines()[0]))
        fields = dict(re.findall(r'(?m)^([^\n=]+) = (.+)$', block))
        if block.startswith('[sub_resource'):
            shapes[header['id']] = {'type': header['type'], 'fields': fields}
        else:
            parent = header.get('parent')
            key = '' if parent is None else header['name'] if parent == '.' else parent + '/' + header['name']
            nodes[key] = {'type': header.get('type', ''), 'fields': fields}
    return nodes, shapes


def vector(value: str):
    match = re.fullmatch(r'Vector2\(([^,]+),\s*([^)]+)\)', value)
    if match is None:
        raise ValueError('Expected Vector2, got ' + value)
    return [float(match[1]), float(match[2])]


def scene_point(nodes, path):
    result = [0.0, 0.0]
    while True:
        fields = nodes[path]['fields']
        if float(fields.get('rotation', '0')) != 0 or vector(fields.get('scale', 'Vector2(1, 1)')) != [1, 1]:
            raise ValueError('Geometry publication requires unrotated, unscaled marker parents: ' + path)
        point = vector(fields.get('position', 'Vector2(0, 0)'))
        result = [result[0] + point[0], result[1] + point[1]]
        if path == '':
            return result
        path = path.rsplit('/', 1)[0] if '/' in path else ''


def outdoor_metadata(nodes):
    fields = nodes['']['fields']
    return {name: json.loads(re.sub(r'\b(?:Vector2|Rect2)\(([^()]*)\)', r'[\1]', fields['metadata/' + name]))
            for name in OUTDOOR_FIELDS if 'metadata/' + name in fields}


def scene_exits(nodes, shapes):
    result = []
    for path, node in nodes.items():
        if not path.startswith('Exits/') or '/' in path[len('Exits/'):] or node['type'] != 'Area2D':
            continue
        fields = node['fields']
        shape_path = path + '/Shape'
        shape = nodes[shape_path]
        reference = re.fullmatch(r'SubResource\("([^"]+)"\)', shape['fields']['shape'])
        if shape['type'] != 'CollisionShape2D' or reference is None:
            raise ValueError(path + ' requires a declared CollisionShape2D')
        resource = shapes[reference[1]]
        if resource['type'] != 'RectangleShape2D':
            raise ValueError(path + ' requires RectangleShape2D geometry')
        size = vector(resource['fields']['size'])
        center = scene_point(nodes, shape_path)
        target = json.loads(fields.get('metadata/target', '""'))
        spawn = json.loads(fields.get('metadata/spawn', '""'))
        if target == 'living_hall':
            target, spawn = 'living_floor', 'living_hall_' + spawn
        entry = {'id': path.split('/', 1)[1], 'path': path,
                 'point': scene_point(nodes, path),
                 'rect': [center[0] - size[0] / 2, center[1] - size[1] / 2, *size],
                 'target': target, 'spawn': spawn,
                 'internal_gate': json.loads(fields.get('metadata/internal_gate', 'false'))}
        if 'metadata/gate' in fields:
            entry['gate'] = json.loads(fields['metadata/gate'])
        result.append(entry)
    return result
