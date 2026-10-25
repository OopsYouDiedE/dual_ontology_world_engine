"""Extract E art layouts from current geometry and room recipes; no world execution."""
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'mcp/asset_pipeline'))
from chapter2_art.rooms import all_rooms

def main():
    source = ROOT / 'godot_csharp/Contracts/geometry_contract.json'
    contract = json.loads(source.read_text(encoding='utf-8'))
    recipes = {r['key']: r for r in all_rooms()}
    entries = []
    for map_id, recipe in recipes.items():
        for room in contract['maps'][map_id]['rooms']:
            x, y, w, h = room['rect']
            furniture = []
            for prop in recipe['furniture']:
                px, py = prop['position']
                if x <= px < x+w and y <= py < y+h:
                    furniture.append({'name': prop['name'], 'kind': prop['sprite'],
                                      'at': [(px-x-w/2)/32, (py-y-h/2)/32]})
            region=room['region']
            if region.startswith(('north_','south_')): region='room'
            rugs=[]
            for work in recipe.get('floor_work',[]):
                if work[0] not in ['rug','runner']: continue
                _,rx,ry,rw,rh,*rest=work
                if x<=rx+rw/2<x+w and y<=ry+rh/2<y+h:
                    rugs.append({'at':[(rx+rw/2-x-w/2)/32,(ry+rh/2-y-h/2)/32],'size':[rw/32,rh/32]})
            entries.append({'id':room['id'], 'map':map_id, 'size':[w/32,h/32],
                            'floor':recipe.get('floor',{}).get(region,recipe.get('floor',{}).get('default','oak')),
                            'rugs':rugs,
                            'rects':room['rects'], 'origin':[x/32,y/32],
                            'furniture':furniture, 'source':'geometry_contract + chapter2_art.rooms'})
    result = {'schema':1,'geometry_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
              'rooms':entries, 'outdoors':{k:v for k,v in contract['maps'].items()
              if k in ['campus_n','campus_ne','campus_nw','campus_s','campus_sw','harbor']}}
    target = ROOT / 'godot_csharp/scenes/ArtStyleTrials/E/library-layouts.json'
    target.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'{len(entries)} indoor rooms; {len(result["outdoors"])} chapter-two outdoor maps')

if __name__ == '__main__':
    main()
