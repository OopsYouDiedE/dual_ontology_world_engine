"""Deterministic 32px asset production. No model/network calls, no scene guessing."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
from collections import deque

from PIL import Image, ImageColor, ImageDraw

HERE = Path(__file__).resolve().parent
GAME = HERE.parents[2]
REPO = GAME.parent
SOURCE = GAME / 'AssetsSource/Internal/Props/TileMapWorkflow'
OUTPUT = GAME / 'ShareAsset/Internal/Props/TileMapWorkflow'
REPORT = REPO / 'artifacts/tilemap-workflow'
DIRECTIONS = [(0,-1,1),(1,0,2),(0,1,4),(-1,0,8),(1,-1,16),(1,1,32),(-1,1,64),(-1,-1,128)]


def name_ok(name):
    if not re.fullmatch(r'[a-z][a-z0-9_-]{0,63}', name):
        raise ValueError('name must be lowercase ASCII letters, digits, underscore or hyphen')
    return name


def within(path, root):
    path = Path(path).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError(f'Path must stay inside {root}')
    return path


def read_path(path):
    if path.startswith('res://'):
        return within(GAME / path[6:], GAME)
    return Path(path).resolve()


def res(path):
    return 'res://' + Path(path).relative_to(GAME).as_posix()


def save_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')


def rgba(value):
    return ImageColor.getcolor(value, 'RGBA')


def inspect_image(path: str, native_cell: int = 32) -> dict:
    """Read dimensions/alpha/grid fit only; never infer a tileset layout from a screenshot."""
    if not 1 <= native_cell <= 4096:
        raise ValueError('native_cell must be 1..4096')
    p = read_path(path)
    with Image.open(p) as src:
        im = src.convert('RGBA')
    alpha = im.getchannel('A')
    hist = alpha.histogram()
    return dict(path=str(p), size=list(im.size), native_cell=native_cell,
                exact_grid=all(n % native_cell == 0 for n in im.size),
                alpha_bounds=im.getbbox(), transparent_pixels=hist[0],
                partial_alpha_pixels=sum(hist[1:255]), sha256=hashlib.sha256(p.read_bytes()).hexdigest(),
                note='Dimensions do not establish native pixel authorship, source grid, perspective or license.')


def draw_asset(name: str, width: int, height: int, palette: dict[str, str],
               commands: list[dict], replace: bool = False) -> dict:
    """Author native pixels: commands {op:rect|line|point,xy:[...],color:palette_key}.
    Coordinates inclusive, no antialiasing; source PNG and drawing recipe are retained.
    """
    name_ok(name)
    if not 1 <= width <= 2048 or not 1 <= height <= 2048 or len(commands) > 20000:
        raise ValueError('Canvas 1..2048; at most 20000 commands')
    path = within(SOURCE / f'{name}.png', SOURCE)
    recipe = path.with_suffix('.drawing.json')
    if not replace and (path.exists() or recipe.exists()):
        raise ValueError('Source exists; use replace=true only for an intentional source edit')
    im = Image.new('RGBA', (width, height))
    draw = ImageDraw.Draw(im)
    for command in commands:
        op, xy = command['op'], command['xy']
        if op not in ('rect','line','point') or len(xy) != (2 if op == 'point' else 4):
            raise ValueError('Expected rect/line [x0,y0,x1,y1] or point [x,y]')
        if any(type(v) is not int or v < 0 or v >= (width if i % 2 == 0 else height) for i,v in enumerate(xy)):
            raise ValueError('Drawing coordinates must be integer pixels inside canvas')
        color = rgba(palette[command['color']])
        if op == 'rect': draw.rectangle(xy, fill=color)
        elif op == 'line': draw.line(xy, fill=color, width=1)
        else: draw.point(xy, fill=color)
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path)
    save_json(recipe, dict(width=width,height=height,palette=palette,commands=commands))
    return dict(source=str(path), recipe=str(recipe), authorship='native pixel commands')


def adapt_asset(name: str, source: str, crop: list[int], target_size: list[int],
                native_cell: int, mode: str = 'fit', color_key: str | None = None) -> dict:
    """Crop at SOURCE pixel coordinates, then nearest-neighbor fit with bottom-center padding.
    mode=native forbids scaling; mode=tile requires a native-cell-aligned grid crop and proportional scaling.
    color_key is explicit exact RGB removal, never guessed. Output is always a derivative.
    """
    name_ok(name)
    if len(crop) != 4 or len(target_size) != 2 or native_cell <= 0:
        raise ValueError('Expected crop [x,y,w,h], target_size [w,h], positive native_cell')
    if any(type(v) is not int for v in crop + target_size):
        raise ValueError('Sizes must be integer pixels')
    x,y,w,h = crop
    tw,th = target_size
    if min(w,h,tw,th) <= 0 or max(tw,th) > 2048:
        raise ValueError('Positive dimensions required; target at most 2048')
    src_path = read_path(source)
    with Image.open(src_path) as opened: src = opened.convert('RGBA')
    if x < 0 or y < 0 or x+w > src.width or y+h > src.height:
        raise ValueError('Crop extends outside source')
    cut = src.crop((x,y,x+w,y+h))
    if color_key:
        key = rgba(color_key)[:3]
        cut.putdata([p[:3]+(0,) if p[:3] == key else p for p in cut.getdata()])
    if mode == 'native':
        if w > tw or h > th: raise ValueError('Native asset does not fit canvas')
        size = (w,h)
    elif mode == 'tile':
        if any(v % native_cell for v in crop) or tw % 32 or th % 32 or w*th != h*tw:
            raise ValueError('Tile crop must align to source grid; target to 32px, same aspect ratio')
        size = (tw,th)
    elif mode == 'fit':
        scale = min(tw/w, th/h)
        size = (max(1,round(w*scale)), max(1,round(h*scale)))
    else: raise ValueError('mode must be native, tile or fit')
    cut = cut.resize(size, Image.Resampling.NEAREST)
    result = Image.new('RGBA', (tw,th))
    offset = ((tw-size[0])//2, th-size[1])
    result.alpha_composite(cut, offset)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    out = within(OUTPUT / f'{name}.png', OUTPUT)
    # Preserve newly supplied production originals in the canonical source family.
    SOURCE.mkdir(parents=True, exist_ok=True)
    archived = within(SOURCE / f'{name}.original{src_path.suffix.lower()}', SOURCE)
    if not src_path.is_relative_to((GAME/'AssetsSource').resolve()):
        if archived.exists() and archived.read_bytes() != src_path.read_bytes():
            raise ValueError('A different original already exists for this name; original not overwritten')
        if not archived.exists(): shutil.copyfile(src_path, archived)
        src_path = archived
    result.save(out)
    info = dict(source=str(src_path),source_sha256=hashlib.sha256(src_path.read_bytes()).hexdigest(),
                output=str(out),crop=crop,native_cell=native_cell,target_size=target_size,
                mode=mode,color_key=color_key,filter='nearest',content_size=list(size),offset=list(offset),
                scaled=size != (w,h),status='derived; visual review required',
                review=['silhouette','tile seams','32x48 actor scale','alpha edges','footprint and occlusion'])
    save_json(SOURCE / f'{name}.adapt.json', info)
    return info


def normalize(mask):
    for corner,a,b in [(16,1,2),(32,2,4),(64,4,8),(128,8,1)]:
        if not (mask & a and mask & b): mask &= ~corner
    return mask


MASKS = sorted({normalize(i) for i in range(256)})


def build_tiles(wall: str, floor: str, face: str, trim: list[str], face_lower: str | None = None) -> dict:
    """Build 47 wall-cap variants from a seamless 32px material plus 3..8 outside-to-inside trim colors.
    Material is not rotated: directional lighting stays fixed. Includes concave corners.
    Writes the workflow family's single atlas/manifest; rebuilding intentionally replaces derivatives.
    """
    if not 3 <= len(trim) <= 8: raise ValueError('trim needs 3..8 outside-to-inside colors')
    colors = [rgba(c) for c in trim]
    inputs = [read_path(p) for p in (wall,floor,face)]
    images = []
    for p in inputs:
        with Image.open(p) as opened: im = opened.convert('RGBA')
        if im.size != (32,32): raise ValueError(f'{p}: explicitly adapt to 32x32 first')
        if im.getchannel('A').getextrema() != (255,255): raise ValueError('Structural tiles must be opaque')
        images.append(im)
    atlas = Image.new('RGBA',(8*32,7*32))
    tiles = []
    for i,mask in enumerate(MASKS):
        im = images[0].copy()
        for y in range(32):
            for x in range(32):
                right,bottom = x >= 16,y >= 16
                dx,dy = (31-x if right else x),(31-y if bottom else y)
                sx,sy = (2 if right else 8),(4 if bottom else 1)
                corner = (32 if right else 64) if bottom else (16 if right else 128)
                distances = []
                if not mask & sx: distances.append(dx)
                if not mask & sy: distances.append(dy)
                if mask & sx and mask & sy and not mask & corner: distances.append(max(dx,dy))
                distance = min(distances,default=99)
                if distance < len(colors): im.putpixel((x,y),colors[distance])
        at = [i%8,i//8]
        atlas.paste(im,(at[0]*32,at[1]*32))
        tiles.append(dict(id=i,atlas=at,solid=True,terrain=0,mask=mask))
    for tile_id,im,solid in [(47,images[1],False),(48,images[2],True)]:
        at = [tile_id%8,tile_id//8]
        atlas.paste(im,(at[0]*32,at[1]*32))
        tiles.append(dict(id=tile_id,atlas=at,solid=solid,terrain=-1,mask=0))
    if face_lower:
        lower_path=read_path(face_lower)
        with Image.open(lower_path) as opened: lower=opened.convert('RGBA')
        if lower.size != (32,32) or lower.getchannel('A').getextrema() != (255,255):
            raise ValueError('face_lower must be opaque 32x32')
        atlas.paste(lower,(32,192))
        tiles.append(dict(id=49,atlas=[1,6],solid=True,terrain=-1,mask=0))
        inputs.append(lower_path)
    OUTPUT.mkdir(parents=True,exist_ok=True)
    atlas.save(OUTPUT/'atlas.png')
    data = dict(atlas=res(OUTPUT/'atlas.png'),tiles=tiles,sources=[str(p) for p in inputs],trim=trim,
                source_hashes=[hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs])
    save_json(OUTPUT/'tiles.json',data)
    save_json(SOURCE/'tiles.recipe.json',dict(wall=wall,floor=floor,face=face,trim=trim,face_lower=face_lower))
    return dict(atlas=str(OUTPUT/'atlas.png'),manifest=str(OUTPUT/'tiles.json'),variants=len(MASKS))


def audit_layout(rows: list[str], spawn: list[int], footprints: list[dict] | None = None,
                 doors: list[dict] | None = None, interaction_points: list[dict] | None = None) -> dict:
    """Read-only RPG spatial review. Coordinates are pixels except spawn and door rect (cells).
    footprints: {name,position:[x,y],collision:[dx,dy,w,h]}; images never define collision.
    doors: {name,rect:[x,y,w,h],facing:north|south|east|west}; rect is the full D passage.
    interaction_points: {name,position:[x,y],radius:32}; reachable standing points, not artwork centers.
    Uses the project's 16x12 foot collider on a 16px navigation lattice, with swept edges.
    """
    if not rows or any(len(row) != len(rows[0]) for row in rows):
        raise ValueError('Rectangular rows required')
    width, height = len(rows[0]), len(rows)
    if max(width,height)>128 or any(c not in '#Ww.D ' for row in rows for c in row):
        raise ValueError('Map must be at most 128 cells with #Ww.D or space')
    if len(spawn)!=2 or any(type(n) is not int for n in spawn):
        raise ValueError('spawn is [integer cell x,integer cell y]')
    errors, warnings, boxes = [], [], []
    def integer_pixels(values):
        # Godot JSON serializes Vector2/Rect2 coordinates as 174.0. Accept exact
        # integer-valued numbers without silently truncating genuine subpixels.
        if any(type(n) not in (int,float) or not float(n).is_integer() for n in values):
            raise ValueError('Footprints require integer-valued pixel coordinates')
        return [int(n) for n in values]
    def overlaps(a,b):
        return a[0]<b[0]+b[2] and b[0]<a[0]+a[2] and a[1]<b[1]+b[3] and b[1]<a[1]+a[3]
    def on_floor(rect):
        x,y,w,h=rect
        return all(0<=tx<width and 0<=ty<height and rows[ty][tx] in '.D'
                   for ty in range(y//32,(y+h-1)//32+1) for tx in range(x//32,(x+w-1)//32+1))
    for i,prop in enumerate(footprints or []):
        collision=prop.get('collision',[])
        if not collision: continue
        position=prop['position']
        if len(collision)!=4 or len(position)!=2 or min(collision[2:])<=0:
            raise ValueError('Footprints require integer position and positive collision rectangle')
        collision=integer_pixels(collision)
        position=integer_pixels(position)
        box=[position[0]+collision[0],position[1]+collision[1],*collision[2:]]
        name=prop.get('name',f'prop {i}')
        if not on_floor(box): errors.append(f'{name}: footprint enters wall or void')
        for other,other_box in boxes:
            if overlaps(box,other_box): errors.append(f'{name}: footprint overlaps {other}')
        boxes.append((name,box))
    declared=set()
    for door in doors or []:
        x,y,w,h=door['rect']; facing=door['facing']; name=door['name']
        if any(type(n) is not int for n in [x,y,w,h]) or min(w,h)<1 or facing not in ['north','south','east','west']:
            raise ValueError('Door needs positive cell rect and cardinal facing')
        passage={(px,py) for py in range(y,y+h) for px in range(x,x+w)}
        if not all(0<=px<width and 0<=py<height and rows[py][px]=='D' for px,py in passage):
            errors.append(f'{name}: declared passage must consist entirely of D cells')
        declared.update(passage)
        approaches=([(px,y-1) for px in range(x,x+w)]+[(px,y+h) for px in range(x,x+w)]
                    if facing in ['north','south'] else [(x-1,py) for py in range(y,y+h)]+[(x+w,py) for py in range(y,y+h)])
        if not all(0<=px<width and 0<=py<height and rows[py][px] in '.D' for px,py in approaches):
            errors.append(f'{name}: both approach sides need clear floor')
        for other,box in boxes:
            if overlaps([x*32,y*32,w*32,h*32],box): errors.append(f'{name}: passage obstructed by {other}')
    undeclared=sum(c=='D' and (x,y) not in declared for y,row in enumerate(rows) for x,c in enumerate(row))
    if undeclared: warnings.append(f'{undeclared} doorway cells have no orientation contract')
    def clear(x,y,dx=0,dy=0):
        rect=[min(x,x+dx)-8,min(y,y+dy)-12,16+abs(dx),12+abs(dy)]
        return on_floor(rect) and not any(overlaps(rect,box) for _,box in boxes)
    start=(spawn[0]*32+16,spawn[1]*32+28)
    reached=set(); queue=deque()
    if clear(*start): reached.add(start); queue.append(start)
    else: errors.append('Spawn foot collider intersects solid geometry')
    while queue:
        x,y=queue.popleft()
        for dx,dy in [(0,-16),(16,0),(0,16),(-16,0)]:
            p=(x+dx,y+dy)
            if p not in reached and clear(x,y,dx,dy): reached.add(p); queue.append(p)
    for point in interaction_points or []:
        x,y=point['position']; radius=point.get('radius',32)
        if not any((px-x)**2+(py-y)**2<=radius**2 for px,py in reached):
            errors.append(f"{point['name']}: no reachable standing point within interaction radius")
    return dict(errors=errors,warnings=warnings,footprints=len(boxes),doors=len(doors or []),
                interaction_points=len(interaction_points or []),
                reachable_standing_points=len(reached),actor_collision=[16,12],navigation_sample_px=16,
                status='spatial checks only; image perspective, occlusion and aesthetics need captured runtime review')


def compose_map(rows: list[str], spawn: list[int], props: list[dict] | None = None,
                doors: list[dict] | None = None, interaction_points: list[dict] | None = None) -> dict:
    """Compile explicit room blueprint. #=wall cap, W=upper wall, w=lower wall, .=floor, D=open doorway, space=void.
    Wall height is authored with W rows, never extruded across walkable cells. spawn is a floor CELL [x,y].
    Writes 1x/3x previews, connectivity report and build.json for export_godot.
    """
    if not rows or len(rows)>128 or not 1<=len(rows[0])<=128 or any(len(r)!=len(rows[0]) for r in rows):
        raise ValueError('Rectangular map required, 1..128 cells per side')
    if any(c not in '#Ww.D ' for row in rows for c in row): raise ValueError('Unknown map character')
    if len(spawn)!=2 or any(type(v) is not int for v in spawn): raise ValueError('spawn needs two integer cell coordinates')
    spatial=audit_layout(rows,spawn,props,doors,interaction_points)
    if spatial['errors']: raise ValueError('Spatial layout rejected: '+'; '.join(spatial['errors']))
    width,height = len(rows[0]),len(rows)
    floors = {(x,y) for y,row in enumerate(rows) for x,c in enumerate(row) if c in '.D'}
    if tuple(spawn) not in floors: raise ValueError('Spawn must be on floor')
    data=json.loads((OUTPUT/'tiles.json').read_text(encoding='utf-8'))
    atlas=Image.open(OUTPUT/'atlas.png').convert('RGBA')
    preview=Image.new('RGBA',(width*32,height*32),(10,13,18,255))
    cells=[]
    for y,row in enumerate(rows):
        for x,c in enumerate(row):
            if c==' ': continue
            if c=='#':
                mask=normalize(sum(bit for dx,dy,bit in DIRECTIONS if 0<=x+dx<width and 0<=y+dy<height and rows[y+dy][x+dx]=='#'))
                tile=MASKS.index(mask); layer='WallCaps'
            elif c in 'Ww': tile,layer=(48 if c=='W' else 49),'WallFaces'
            else: tile,layer=47,'Floor'
            if tile >= len(data['tiles']): raise ValueError('w needs face_lower in build_tiles')
            cells.append(dict(layer=layer,x=x,y=y,tile=tile))
            ax,ay=data['tiles'][tile]['atlas']
            preview.paste(atlas.crop((ax*32,ay*32,ax*32+32,ay*32+32)),(x*32,y*32))
    reached={tuple(spawn)}; queue=deque(reached)
    while queue:
        x,y=queue.popleft()
        for dx,dy,_ in DIRECTIONS[:4]:
            p=(x+dx,y+dy)
            if p in floors and p not in reached: reached.add(p); queue.append(p)
    # Void is blocked in Godot too, so reachable edges are reported as layout leaks.
    leaks=sorted([list(p) for p in floors if p[0] in (0,width-1) or p[1] in (0,height-1)])
    report=dict(grid=32,size=[width,height],walkable_cells=len(floors),reachable_cells=len(reached),
                disconnected=sorted([list(p) for p in floors-reached]),open_map_edges=leaks,
                door_cells=[[x,y] for y,row in enumerate(rows) for x,c in enumerate(row) if c=='D'],
                status='layout checks only; movement, seams, occlusion and art require runtime review')
    props = props or []
    if len(props) > 256: raise ValueError('At most 256 props')
    for prop in sorted(props,key=lambda p:(-1 if p.get('ground') else 10 if p.get('overhead') else 0,p['position'][1])):
        texture = within(read_path(prop['texture']), GAME/'ShareAsset')
        prop['texture'] = res(texture)
        for field in ('position','anchor'):
            if len(prop[field]) != 2 or any(type(v) is not int for v in prop[field]):
                raise ValueError(f'prop {field} must be [integer x,integer y]')
        collision = prop.get('collision',[])
        if collision and (len(collision)!=4 or any(type(v) is not int for v in collision) or min(collision[2:])<=0):
            raise ValueError('prop collision must be [] or [x,y,positive width,positive height]')
        prop.setdefault('collision',[])
        prop.setdefault('overhead',False)
        with Image.open(texture) as opened: image = opened.convert('RGBA')
        preview.alpha_composite(image,(prop['position'][0]-prop['anchor'][0],prop['position'][1]-prop['anchor'][1]))
    report['prop_count']=len(props)
    report['spatial']=spatial
    report['connectivity_excludes_prop_footprints']=True  # Cell flood above is the bare architecture only.
    report['spatial_includes_prop_footprints']=True
    data.update(cells=cells,rows=rows,props=props,doors=doors or [],spawn=[spawn[0]*32+16,spawn[1]*32+28],size=[width,height],
                output=res(OUTPUT)+'/',scene='res://scenes/TileMapWorkflow/Preview.tscn')
    save_json(OUTPUT/'build.json',data)
    save_json(SOURCE/'map.json',dict(rows=rows,spawn=spawn,props=props,doors=doors or [],interaction_points=interaction_points or []))
    save_json(REPORT/'validation.json',report)
    REPORT.mkdir(parents=True,exist_ok=True)
    preview.save(REPORT/'map-1x.png')
    preview.resize((width*96,height*96),Image.Resampling.NEAREST).save(REPORT/'map-3x.png')
    return dict(build=str(OUTPUT/'build.json'),preview=str(REPORT/'map-1x.png'),report=report)


def export_godot() -> dict:
    """Run fixed Godot exporter; GODOT_BIN or PATH selects executable, not tool input. No arbitrary scripts."""
    godot=os.environ.get('GODOT_BIN') or shutil.which('godot')
    if not godot: raise ValueError('Set GODOT_BIN to Godot 4.6 .NET executable')
    build=OUTPUT/'build.json'
    if not build.exists(): raise ValueError('compose_map must run first')
    REPORT.mkdir(parents=True,exist_ok=True)
    result=subprocess.run([godot,'--headless','--log-file',str(REPORT/'godot-engine.log'),'--path',str(GAME),'--script',
        'res://addons/rpg_asset_adapter/mcp/export_map.gd','--',str(build)],
        capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=120)
    log=result.stdout+'\n'+result.stderr
    REPORT.mkdir(parents=True,exist_ok=True)
    (REPORT/'godot-export.log').write_text(log,encoding='utf-8')
    environment_warning='ERROR: Failed to read the root certificate store.'
    inspected_log=log.replace(environment_warning,'WARNING: root certificate store unavailable (offline export)')
    if result.returncode or 'SCRIPT ERROR:' in inspected_log or 'ERROR:' in inspected_log:
        raise ValueError('Godot export failed: '+log[-6000:])
    return dict(scene='res://scenes/TileMapWorkflow/Preview.tscn',log=log[-4000:],
                warnings=['OS certificate store inaccessible; export uses no network'] if environment_warning in log else [],
                status='exported; art review pending')
