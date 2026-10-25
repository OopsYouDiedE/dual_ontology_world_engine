"""Recover retained outdoor placement without executing the retired raster generator.

The original CampusSite author directory is absent. Building footprints are only
accepted when a retained collider matches the sprite's ground-line anchor exactly.
"""
import hashlib
import json
import re
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
GAME = ROOT / 'godot_csharp'
TARGET = GAME / 'scenes/ArtStyleTrials/E/areas'
IDS = ['campus_nw','campus_n','campus_ne','campus_sw','campus_s','harbor']
HEIGHTS = {'讲堂':6.0,'名录库':6.5,'文献工房':4.6,'文契院（封闭部分）':5.6,
           '共鸣厅':10.0,'沉静室':3.4,'星盘讲堂（封闭）':6.0,'诊疗所与安全端':4.4,
           '化学实验室（封闭）':5.0,'后勤院':5.0,'铁工院（封闭）':7.0,'粮仓':4.5,
           '温室':3.0,'生活翼·甲区':3.8,'生活翼·乙区':3.8,'生活翼·丙区':3.8,
           '生活翼·丁区':3.8,'生活翼·浴室盥洗':3.4,'生活翼·起居厅':5.6}

def vector(body, key):
    found = re.search(r'^'+re.escape(key)+r' = Vector2\(([^)]+)\)',body,re.M)
    return [float(x) for x in found[1].split(',')] if found else None

def main():
    maps = {}
    dependencies = set()
    for id in IDS:
        path=GAME/f'scenes/Chapter2/Maps/{id}.tscn'
        text=path.read_text(encoding='utf-8-sig')
        refs={rid:resource for resource,rid in re.findall(r'\[ext_resource type="Texture2D" path="([^"]+)" id="([^"]+)"\]',text)}
        shapes={name:vector(body,'size') for name,body in re.findall(r'\[sub_resource type="RectangleShape2D" id="([^"]+)"\]([^\[]+)',text)}
        nodes=re.findall(r'\[node name="([^"]+)" type="([^"]+)" parent="([^"]+)"\]([^\[]*)',text)
        solids=[]
        for name,typ,parent,body in nodes:
            if typ!='CollisionShape2D' or parent!='Solids': continue
            ref=re.search(r'shape = SubResource\("([^"]+)"\)',body)
            if ref and shapes.get(ref[1]): solids.append({'at':vector(body,'position'),'size':shapes[ref[1]],'node':name})
        ground=refs['arch']
        size=list(Image.open(GAME/ground.removeprefix('res://')).size)
        dependencies.add(ground)
        objects=[]
        for name,typ,parent,body in nodes:
            if typ!='Sprite2D' or parent!='Objects': continue
            match=re.search(r'texture = ExtResource\("([^"]+)"\)',body)
            if not match: continue
            texture=refs[match[1]]
            at=vector(body,'position')
            offset=vector(body,'offset') or [0,0]
            item={'name':name,'at':at,'texture':texture,'offset':offset,'representation':'retained-raster'}
            candidates=[s for s in solids if abs(s['at'][0]-at[0])<=1 and abs(s['at'][1]+s['size'][1]/2-at[1])<=1]
            if name in HEIGHTS and candidates:
                # Large off-map blockers are excluded; footprint must fit source sprite width.
                pixels=Image.open(GAME/texture.removeprefix('res://')).size
                candidates=[s for s in candidates if s['size'][0]<=pixels[0]+2 and s['size'][0]>=48]
                if candidates:
                    footprint=max(candidates,key=lambda s:s['size'][0]*s['size'][1])
                    item.update(representation='building-3d',footprint=footprint,height=HEIGHTS[name])
            if any(k in texture for k in ['outdoor_round','outdoor_cypress','outdoor_pine','outdoor_hydrangea']):
                item['representation']='vegetation-3d'
                item['image_size']=list(Image.open(GAME/texture.removeprefix('res://')).size)
            if item['representation']=='retained-raster': dependencies.add(texture)
            objects.append(item)
        maps[id]={'size_px':size,'ground':ground,'objects':objects,'source':path.relative_to(ROOT).as_posix(),
                  'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    data={'schema':1,'pixels_per_scene_unit':24,'height_source':'mcp/asset_pipeline/chapter2_art/outdoor.py models()',
          'height_source_sha256':hashlib.sha256((ROOT/'mcp/asset_pipeline/chapter2_art/outdoor.py').read_bytes()).hexdigest(),
          'limits':['CampusSite source absent; retained ground PNG preserves roads/coast and baked shadows.',
                    'Unmatched special buildings/walls retain source raster; they are not new 3D models.',
                    'No doors are generated. No gameplay collision or traversal is changed.'], 'maps':maps}
    TARGET.mkdir(parents=True,exist_ok=True)
    (TARGET/'outdoors-data.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    # Explicit static reference text consumed by the existing resource-closure copier.
    (TARGET/'outdoors-resources.gd').write_text('extends RefCounted\n# Explicit retained resource closure.\nconst SOURCES = [\n'+''.join('    "'+p+'",\n' for p in sorted(dependencies))+']\n',encoding='utf-8')
    for id,m in maps.items():
        print(id,{k:sum(o['representation']==k for o in m['objects']) for k in ['building-3d','vegetation-3d','retained-raster']})

if __name__=='__main__': main()
