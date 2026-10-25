import json, hashlib
from pathlib import Path

root = Path('.')
source_path = Path('godot_csharp/Contracts/geometry_contract.json')
data = json.loads(source_path.read_text(encoding='utf-8'))
rooms, edges, rejected = [], [], []
room_ids = {map_name: {r['id']: r for r in m.get('rooms', [])} for map_name, m in data['maps'].items()}
room_records = {}
edge_index = {}
for map_name, m in data['maps'].items():
    scene = m['scene']
    for i, room in enumerate(m.get('rooms', [])):
        room_id = room['id']
        rects = room.get('rects') or [room['rect']]
        areas = [rect[2] * rect[3] / (data['cell_size'] ** 2) for rect in rects]
        total_area = sum(rect[2] * rect[3] for rect in rects)
        center_x = sum((rect[0] + rect[2] / 2) * rect[2] * rect[3] for rect in rects) / total_area
        center_y = sum((rect[1] + rect[3] / 2) * rect[2] * rect[3] for rect in rects) / total_area
        record = {
            'id': room_id, 'map': map_name, 'map_id': map_name, 'scene': scene,
            'rect': room['rect'], 'rects': rects, 'rect_areas_32_cells': areas,
            'area_32_cells': sum(areas), 'center_x': center_x, 'center_y': center_y,
            'door_ids': [], 'exit_ids': [],
            'source': {'path': source_path.as_posix(), 'pointer': f'/maps/{map_name}/rooms/{i}'}
        }
        rooms.append(record)
        room_records[(map_name, room_id)] = record
    for i, door in enumerate(m.get('doors', [])):
        door_id = door['id']
        src = door.get('from')
        dst = door.get('to')
        source_ptr = f'/maps/{map_name}/doors/{i}'
        if src in room_ids.get(map_name, {}) and dst in room_ids.get(map_name, {}):
            room_records[(map_name, src)]['door_ids'].append(door_id)
            room_records[(map_name, dst)]['door_ids'].append(door_id)
            edge_id = f'{map_name}:door:{door_id}'
            edges.append({'id': edge_id, 'from': src, 'to': dst, 'map': map_name,
                          'kind': 'door', 'door_id': door_id, 'point': door.get('point'),
                          'source': {'path': source_path.as_posix(), 'pointer': source_ptr}})
            edge_index[(map_name, door_id)] = edge_id
        elif door.get('target_map'):
            target_map = door['target_map']
            exit_match = next(((j, ex) for j, ex in enumerate(m.get('exits', [])) if ex.get('id') == door_id), None)
            if exit_match is None:
                rejected.append({'map': map_name, 'door_id': door_id, 'source_pointer': source_ptr,
                                 'reason': 'cross-map door has no same-map exit with matching id'})
                continue
            exit_i, ex = exit_match
            spawn_id = ex.get('spawn')
            target = data['maps'].get(target_map)
            spawn = target.get('spawns', {}).get(spawn_id) if target else None
            target_room = spawn.get('room') if spawn else None
            if src not in room_ids.get(map_name, {}):
                reason = f'cross-map door source {src!r} does not resolve to a room in source map'
            elif target is None:
                reason = f'target map {target_map!r} is absent from contract'
            elif spawn is None:
                reason = f'target spawn {spawn_id!r} is absent from target map {target_map!r}'
            elif target_room not in room_ids.get(target_map, {}):
                reason = f'target spawn room {target_room!r} does not resolve in target map {target_map!r}'
            else:
                reason = None
            if reason:
                rejected.append({'map': map_name, 'door_id': door_id, 'exit_id': ex.get('id'),
                                 'source_pointer': source_ptr, 'exit_pointer': f'/maps/{map_name}/exits/{exit_i}',
                                 'reason': reason})
                continue
            room_records[(map_name, src)]['door_ids'].append(door_id)
            room_records[(map_name, src)]['exit_ids'].append(ex['id'])
            edge_id = f'{map_name}:exit:{ex["id"]}'
            edges.append({'id': edge_id, 'from': src, 'to': target_room, 'map': map_name,
                          'kind': 'exit', 'door_id': door_id, 'exit_id': ex['id'],
                          'point': door.get('point'), 'exit_point': ex.get('point'), 'exit_rect': ex.get('rect'),
                          'internal_gate': ex.get('internal_gate', False), 'target_map': target_map, 'spawn': spawn_id,
                          'source': {'path': source_path.as_posix(), 'pointer': source_ptr,
                                     'exit_pointer': f'/maps/{map_name}/exits/{exit_i}',
                                     'spawn_pointer': f'/maps/{target_map}/spawns/{spawn_id}'}})
            edge_index[(map_name, door_id)] = edge_id
        else:
            rejected.append({'map': map_name, 'door_id': door_id, 'source_pointer': source_ptr,
                             'reason': 'door endpoints do not both resolve within map and no target_map is declared'})
    # Preserve orphan exit facts with a reason; do not infer edge endpoints.
    for i, ex in enumerate(m.get('exits', [])):
        if (map_name, ex['id']) not in edge_index:
            rejected.append({'map': map_name, 'exit_id': ex['id'], 'source_pointer': f'/maps/{map_name}/exits/{i}',
                             'reason': 'exit has no accepted same-id door edge with a resolvable target spawn room'})

def dv(value):
    if isinstance(value, bool): return 'true' if value else 'false'
    if isinstance(value, (int, float)): return json.dumps(value, ensure_ascii=False, separators=(',', ':'))
    if isinstance(value, str): return json.dumps(value, ensure_ascii=False)
    if isinstance(value, list): return '[' + ', '.join(dv(x) for x in value) + ']'
    if isinstance(value, dict): return '{' + ', '.join(dv(k) + ': ' + dv(v) for k, v in value.items()) + '}'
    raise TypeError(type(value))

def fn(name, entries):
    return f'fn {name}():\n    return [\n' + ''.join('        ' + dv(e) + ',\n' for e in entries) + '    ]\n'
module = '#include 35-campaign-schema.dv\n\n' + fn('island_topology_rooms', rooms) + '\n' + fn('island_topology_edges', edges)
Path('dv_core/modules/36-island-topology.dv').write_text(module, encoding='utf-8', newline='\n')
contract_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()
generator_path = Path(__file__).resolve().relative_to(root.resolve()).as_posix()
generator_hash = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
prov = {'schema':'island-topology-source-v1', 'source':source_path.as_posix(), 'source_sha256':contract_hash,
        'generator':generator_path, 'generator_sha256':generator_hash,
        'cell_size_px':data['cell_size'], 'interpretation':'area_32_cells is exact rect area divided by 32px squared; geometric cell units only, not meters.',
        'room_count':len(rooms), 'accepted_edge_count':len(edges), 'edge_counts':{
            'door':sum(e['kind']=='door' for e in edges), 'exit':sum(e['kind']=='exit' for e in edges)},
        'rejected_count':len(rejected), 'rejected':rejected,
        'room_sources':[{'id':r['id'],'map':r['map'],'pointer':r['source']['pointer']} for r in rooms],
        'edge_sources':[{'id':e['id'],'from':e['from'],'to':e['to'],'kind':e['kind'],'source':e['source']} for e in edges]}
outdir=Path('artifacts/dv-approx-highlights')
outdir.mkdir(parents=True, exist_ok=True)
(outdir/'island-topology-source.json').write_text(json.dumps(prov,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
print(f'rooms={len(rooms)} edges={len(edges)} doors={prov["edge_counts"]["door"]} exits={prov["edge_counts"]["exit"]} rejected={len(rejected)} sha256={contract_hash}')


