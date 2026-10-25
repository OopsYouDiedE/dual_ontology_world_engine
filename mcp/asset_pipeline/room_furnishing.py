"""Explicit furniture families and matching physical footprints for authored rooms."""
from copy import deepcopy
from math import ceil, floor

# World cells stay 32px. These are object ratios, not a new world metre scale.
SCALES = {
    'bed_0': 1.45, 'desk_0': 1.40, 'wardrobe': 1.35, 'nightstand': 1.25,
    'study_table': 1.45, 'worktable': 1.40, 'long_table': 1.50,
    'long_bench': 1.40, 'chair_front': 1.20, 'chair_back': 1.20,
    'armchair': 1.60, 'side_table_plain': 1.45, 'bookcase_0': 1.30,
    'bookcase_study': 1.30, 'bookcase_study_b': 1.30, 'bookcase_empty': 1.30,
    'sideboard': 1.35, 'range_stove': 1.25, 'fireplace': 1.30,
    'upright_piano': 1.40, 'piano_stool': 1.20, 'chess_table': 1.35,
    'granary_bin': 1.70, 'basin_row': 1.20, 'washstand': 1.25,
    'bathtub': 1.30, 'lecture_desk_row': 1.20,
}


def tune_room(room):
    """Run once after footprint resolution, before scene/marker publication."""
    result = deepcopy(room)
    if result.get('furnishing_revision') == 1:
        return result
    result['furnishing_revision'] = 1
    for item in result['furniture']:
        scale = SCALES.get(item['sprite'], 1.0)
        item['visual_scale'] = scale
        x, y = item['position']
        # Matching table/chair groups have explicit source positions.
        dy = 0
        if item['name'] in ('StudyChairNW', 'StudyChairNE'):
            dy = -34
        elif item['name'] in ('StudyChairSW', 'StudyChairSE'):
            dy = 8
        elif result['key'] == 'living_hall' and item['name'] == 'BenchN':
            dy = -8
        elif result['key'] == 'living_hall' and item['name'] == 'BenchS':
            dy = 10
        elif result['key'] == 'logistics':
            dy = {'BenchA1': -38, 'BenchA2': -4, 'TableB': 22, 'BenchB1': -16, 'BenchB2': 18}.get(item['name'], 0)
        item['position'] = [x, y + dy]
        footprint = item.get('footprint')
        if footprint:
            left, top, width, height = footprint
            item['footprint'] = [floor(x + (left-x)*scale), floor(y + (top-y)*scale + dy),
                                 ceil(width*scale), ceil(height*scale)]
    # Decorative evidence remains on its actual supporting tabletop.
    for item in result['furniture']:
        if item['name'] == 'Slate':
            item['position'][1] -= 5
    if result['key'] == 'logistics':
        from PIL import Image
        from pathlib import Path
        game = Path(__file__).resolve().parents[2] / 'godot_csharp'
        import json
        publication = json.loads((game/'ShareAsset/Internal/Props/ReferenceDining/approved-publication.json').read_text(encoding='utf-8'))
        feet = {asset['output']: asset.get('foot') for asset in publication['assets']}
        replacements = {
            'TableA': ('table-a',240), 'TableB': ('table-b',240),
            'BenchA1': ('bench',179), 'BenchA2': ('bench',179),
            'BenchB1': ('bench',179), 'BenchB2': ('bench',179),
            'Worktable': ('prep',134),
            'Range': ('range',100), 'Hood': ('hood',112),
            'DishRack': ('dish-rack',60), 'RationCounter': ('counter',100),
        }
        for item in result['furniture']:
            if item['name'] not in replacements: continue
            asset,width = replacements[item['name']]
            texture = f'res://ShareAsset/Internal/Props/ReferenceDining/{asset}.png'
            source = game / texture.removeprefix('res://')
            if not source.is_file(): raise FileNotFoundError(source)
            with Image.open(source) as image:
                size = image.size
            item['texture'] = texture
            item['sprite'] = 'reference_dining_' + asset.replace('-','_')
            item['visual_scale'] = width/size[0]
            anchor = feet[asset+'.png']
            item['draw_offset'] = [-anchor[0],-anchor[1]]
            if item['name'] == 'Hood':
                item['position'][1] = 86
                item['z_index'] = -1
        result['art_lights'] = [{'kind':'table_lamp','point':[x,y]} for x,y in
                                [(642,128),(766,128),(642,278),(766,278)]]
    for group in ('sites', 'geometry_sites'):
        for site in result.get(group, []):
            site['point'] = nearest_approach(result, site['point'])
    return result


def nearest_approach(room, point):
    """Retain the station's semantic room; move only if the body cannot fit."""
    from chapter2_art.painter import Grid
    grid = Grid(room)
    x, y = point
    region = grid.region(int(x//32), int(y//32))
    def legal(px, py):
        for dx, dy in ((-14,-14), (14,-14), (-14,14), (14,14)):
            if not grid.walkable(int((px+dx)//32), int((py+dy)//32)):
                return False
        if grid.region(int(px//32), int(py//32)) != region:
            return False
        for item in room['furniture']:
            rect = item.get('footprint')
            if rect and rect[0]-14 < px < rect[0]+rect[2]+14 and rect[1]-14 < py < rect[1]+rect[3]+14:
                return False
        return True
    if legal(x,y):
        return [x,y]
    options = [(x+dx,y+dy) for dy in range(-96,97,8) for dx in range(-96,97,8)]
    options.sort(key=lambda p: ((p[0]-x)**2+(p[1]-y)**2, p[1],p[0]))
    for px, py in options:
        if legal(px,py): return [px,py]
    raise ValueError(f"No body-sized approach near {room['key']}:{point}")


def ornaments(room):
    """Existing green/gold rug motifs, enlarged independently of floor tiles."""
    result=[]
    for entry in room.get('floor_work', []):
        if entry[0] not in ('rug','runner'): continue
        kind,x,y,w,h,*_ = entry
        if kind == 'rug':
            nw,nh=round(w*1.15),round(h*1.15)
            result.append([x-(nw-w)/2,y-(nh-h)/2,nw,nh])
        else:
            result.append([x-16,y,w+32,h])
    return result
