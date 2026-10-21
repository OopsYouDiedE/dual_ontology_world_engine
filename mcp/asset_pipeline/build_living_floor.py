"""Compose published living-wing scenes into one physical floor; keep source assets."""
import json
import re
from pathlib import Path

ROOT = (Path(__file__).resolve().parents[2] / "godot_csharp")
MAPS = ROOT / 'scenes/Chapter2/Maps'
PARTS = {'zone_jia': (0, 0), 'zone_ding': (1600, 0),
         'zone_bing': (0, 704), 'zone_yi': (1600, 704), 'living_hall': (1120, 128)}


def _publish_stands_only(path, generated):
    """Add newly authored markers while retaining the published floor's bytes."""
    source = path.read_bytes().decode('utf-8')
    def markers(text):
        result = {}
        for match in re.finditer(r'(?ms)^\[node\b.*?(?=^\[|\Z)', text):
            header = match[0].splitlines()[0]
            if 'parent="Stands"' not in header:
                continue
            name = re.search(r'name="([^"]+)"', header)[1]
            if name in result or 'type="Marker2D"' not in header:
                raise ValueError(f'Invalid Stand marker: {name}')
            result[name] = match
        return result
    previous, current = markers(source), markers(generated)
    if not previous:
        raise ValueError('Published floor has no standing markers; retain it for review')
    for name, match in previous.items():
        if name not in current or match[0].replace('\r\n', '\n').strip() != current[name][0].replace('\r\n', '\n').strip():
            raise ValueError(f'Published floor Stand differs from source: {name}')
    added = [match[0].strip() for name, match in current.items() if name not in previous]
    if not added:
        return
    newline = '\r\n' if '\r\n' in source else '\n'
    text = (newline * 2).join(block.replace('\r\n', '\n').replace('\n', newline) for block in added) + newline * 2
    end = max(match.end() for match in previous.values())
    path.write_bytes((source[:end] + text + source[end:]).encode('utf-8'))


def build(stands_only=False):
    resources, nodes, terrain = [], [], [['#'] * 86 for _ in range(50)]
    lights = []
    for key, (dx, dy) in PARTS.items():
        source = (MAPS / f'{key}.tscn').read_text(encoding='utf-8')
        blocks = re.findall(r'(?ms)^\[(?:ext_resource|sub_resource|node)\b.*?(?=^\[|\Z)', source)
        rows = json.loads(re.search(r'^metadata/terrain_rows = (.*)$', source, re.M)[1])
        if key == 'living_hall':
            rows[17:17] = ['#..............#'] * 6
        for y, row in enumerate(rows):
            for x, mark in enumerate(row):
                xx, yy = x + dx // 32, y + dy // 32
                # Shared wall columns include the same aligned, two-cell door.
                if mark != '#' or terrain[yy][xx] == '#': terrain[yy][xx] = mark
        source_lights = json.loads(re.search(r'^metadata/stage_lights = (.*)$', source, re.M)[1])
        def position(x, y):
            return x + dx, y + dy + (192 if key == 'living_hall' and y >= 544 else 0)
        for light in source_lights:
            lights.append({**light, 'point': list(position(*light['point']))})
        for block in blocks:
            block = re.sub(r'((?:id=|(?:Ext|Sub)Resource\()")([^"]+)', rf'\g<1>{key}_\2', block)
            if not block.startswith('[node'):
                if block.startswith('[ext_resource') and f'id="{key}_arch"' in block:
                    png = f'res://ShareAsset/Internal/Props/Chapter2/rooms/{key}.png'
                    relief = png.removesuffix('.png') + '.relief.tres'
                    if (ROOT / relief.removeprefix('res://')).is_file():
                        block = block.replace(png, relief)
                resources.append(block.strip())
                continue
            header, _, body = block.partition('\n')
            parent_match = re.search(r'parent="([^"]+)"', header)
            if not parent_match: continue
            name = re.search(r'name="([^"]+)"', header)[1]
            parent = parent_match[1]
            if parent == '.' and name in ('Objects', 'Solids', 'Exits', 'Spawns', 'Stands', 'Sites'): continue
            if parent.startswith('Exits') and key != 'living_hall': continue
            if parent == 'Exits' and 'metadata/target = "zone_' in body:
                # Internal thresholds remain physical gate areas, never map exits.
                body = body.replace('metadata/target = "zone_', 'metadata/gate = "zone_')
                body += '\nmetadata/internal_gate = true\n'
            prefix = '' if '/' in parent or (key == 'zone_yi' and parent in ('Objects', 'Sites')) else key + '_'
            header = header.replace(f'name="{name}"', f'name="{prefix}{name}"')
            if '/' in parent:
                group, rest = parent.split('/', 1)
                header = header.replace(f'parent="{parent}"', f'parent="{group}/{key}_{rest}"')
            if parent in ('Objects', 'Solids', 'Exits', 'Spawns', 'Stands', 'Sites') or parent == '.':
                match = re.search(r'^position = Vector2\(([^,]+), ([^)]+)\)', body, re.M)
                x, y = (float(match[1]), float(match[2])) if match else (0, 0)
                px, py = position(x, y)
                # Stretch only walls spanning the inserted central hall strip.
                if key == 'living_hall' and parent == 'Solids':
                    shape_id = re.search(r'SubResource\("([^"]+)"\)', body)
                    if shape_id:
                        for i, resource in enumerate(resources):
                            if f'id="{shape_id[1]}"' not in resource: continue
                            size = re.search(r'size = Vector2\(([^,]+), ([^)]+)\)', resource)
                            if size and y - float(size[2])/2 < 544 < y + float(size[2])/2:
                                resources[i] = resource.replace(size[0], f'size = Vector2({size[1]}, {float(size[2])+192:g})')
                                py = y + dy + 96
                prop = f'position = Vector2({px:g}, {py:g})'
                body = body.replace(match[0], prop) if match else prop + '\n' + body
            if key == 'living_hall' and parent == '.' and (name == 'Architecture' or 'metadata/surface_layer = true' in body):
                # Each image-generated surface layer has its own 2x texture.
                # Split texture coordinates at the same physical corridor join.
                texture_id = re.search(r'texture = ExtResource\("([^"]+)"\)', body)[1]
                scale_match = re.search(r'scale = Vector2\(([^,]+), ([^)]+)\)', body)
                sx = float(scale_match[1]) if scale_match else 1.0
                sy_scale = float(scale_match[2]) if scale_match else 1.0
                z_match = re.search(r'z_index = (-?\d+)', body)
                z = int(z_match[1]) if z_match else -10
                for suffix, sy, height, dest_y, stretch in [('top', 0, 544, 128, 1), ('join', 544, 32, 672, 6), ('bottom', 544, 736, 864, 1)]:
                    nodes.append(f'[node name="hall_{name}_{suffix}" type="Sprite2D" parent="."]\nz_index = {z}\nposition = Vector2(1120, {dest_y})\nscale = Vector2({sx:g}, {sy_scale*stretch:g})\ntexture = ExtResource("{texture_id}")\ncentered = false\nregion_enabled = true\nregion_rect = Rect2(0, {sy/sy_scale:g}, {512/sx:g}, {height/sy_scale:g})\nmetadata/surface_layer = true')
                continue
            nodes.append(header + '\n' + body.strip())
    for i, node in enumerate(nodes):
        if re.match(r'\[node name="hall_(top|join|bottom)"', node):
            nodes[i] = node.replace('\nposition =', '\nz_index = -10\nposition =', 1)
    root = '[node name="LivingFloor" type="Node2D"]\nmetadata/title = "生活翼"\nmetadata/map_size = Vector2(2752, 1600)\n'
    root += 'metadata/terrain_rows = ' + json.dumps([''.join(r) for r in terrain]) + '\n'
    root += 'metadata/stage_lights = ' + json.dumps(lights) + '\n'
    root += 'metadata/floor_parts = ' + json.dumps({k: list(v) for k,v in PARTS.items()}) + '\n'
    groups = [f'[node name="{g}" type="{t}" parent="."]' for g,t in [('Objects','Node2D'),('Solids','StaticBody2D'),('Exits','Node2D'),('Spawns','Node2D'),('Stands','Node2D'),('Sites','Node2D')]]
    root += 'texture_filter = 1\n'
    groups[0] += '\ny_sort_enabled = true'
    groups[1] += '\ncollision_mask = 0'
    resources.sort(key=lambda block: 0 if block.startswith('[ext_resource') else 1)
    output = f'[gd_scene load_steps={len(resources)+1} format=3]\n\n' + '\n\n'.join(resources + [root] + groups + nodes) + '\n'
    target = MAPS / 'living_floor.tscn'
    if stands_only:
        _publish_stands_only(target, output)
    else:
        target.write_text(output, encoding='utf-8', newline='\n')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--stands-only', action='store_true', help='Append source Stand markers without changing other published floor content.')
    build(stands_only=parser.parse_args().stands_only)
