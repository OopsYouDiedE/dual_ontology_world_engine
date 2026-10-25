"""Slice authored RPG plates and pose art. Does not launch Godot or edit scenes.

Characters are new ImageGen drawings, reduced once into pixel canvases; connected
alpha components keep neighbouring wide swords out of adjacent pose crops.
"""
from pathlib import Path
from collections import deque
import hashlib
import json
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'mcp/asset_pipeline/sources/Internal'
OUTPUT = ROOT / 'godot_csharp/ShareAsset/Internal'
REPORT = ROOT / 'artifacts/dv-approx-highlights/rpg-scenes'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rel(path):
    return path.relative_to(ROOT).as_posix()


def resource(path):
    return 'res://' + path.relative_to(ROOT / 'godot_csharp').as_posix()


def write_image(image, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def components(image, minimum=2000):
    """Separate source silhouettes by actual opaque connected pixels."""
    w, h = image.size
    alpha = image.getchannel('A').point(lambda a: 255 if a >= 128 else 0)
    data = alpha.tobytes()
    seen = bytearray(w * h)
    found = []
    for start, value in enumerate(data):
        if not value or seen[start]:
            continue
        queue = deque([start])
        seen[start] = 1
        pixels = []
        while queue:
            at = queue.popleft()
            pixels.append(at)
            x = at % w
            for other in ((at-1 if x else -1), (at+1 if x+1<w else -1), at-w, at+w):
                if 0 <= other < len(data) and data[other] and not seen[other]:
                    seen[other] = 1
                    queue.append(other)
        if len(pixels) < minimum:
            continue
        xs = [p % w for p in pixels]
        ys = [p // w for p in pixels]
        box = (min(xs), min(ys), max(xs)+1, max(ys)+1)
        mask_data = bytearray(w*h)
        for at in pixels:
            mask_data[at] = 255
        mask = Image.frombytes('L', (w,h), bytes(mask_data)).crop(box)
        crop = image.crop(box)
        crop.putalpha(mask)
        found.append((box, crop))
    return found


def publish_actors(manifest):
    sheets = [
        ('01-poet-poses', 1, [['poet_hesitate','poet_step','poet_guard','poet_guard_unsteady']]),
        ('01-escape-pursuit', 2, [['thief_hurt','thief_limp_a','thief_limp_b'], ['detective_chase_a','detective_chase_b','detective_recoil']]),
        ('06-discovery-actors', 2, [['detective_idle','detective_point_down','detective_warn_back'], ['chef_idle','chef_warn','chef_reach']]),
        ('06-carry-actors', 2, [['detective_pickup','detective_carry_a','detective_carry_b'], ['chef_pickup','chef_carry_a','chef_carry_b']]),
    ]
    for sheet, rows, names in sheets:
        source = SOURCE / 'Characters/HighlightsRpg' / (sheet+'.png')
        image = Image.open(source).convert('RGBA')
        groups = components(image)
        ordered = []
        for row in range(rows):
            line = [g for g in groups if row*image.height/rows <= (g[0][1]+g[0][3])/2 < (row+1)*image.height/rows]
            line.sort(key=lambda g: g[0][0])
            if len(line) != len(names[row]):
                raise ValueError(f'{sheet} row {row}: source has {len(line)} connected bodies, expected {len(names[row])}')
            ordered.extend(zip(names[row], line))
        for name, (box, crop) in ordered:
            scale = 94 / crop.height
            size = (max(1, round(crop.width*scale)), 94)
            small = crop.resize(size, Image.Resampling.NEAREST)
            # Stable hard palette: source contour/texture preserved; no dithering.
            alpha = small.getchannel('A')
            rgb = small.convert('RGB').quantize(colors=32, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert('RGBA')
            rgb.putalpha(alpha)
            foot_pixels = [(x,y) for y in range(max(0,small.height-8),small.height) for x in range(small.width) if alpha.getpixel((x,y))]
            foot_x = round(sum(x for x,y in foot_pixels)/len(foot_pixels))
            canvas_width = 160 if name.startswith('poet') else 128
            anchor_x = 56 if name.startswith('poet') else 64
            x = anchor_x-foot_x
            if x < 0 or x+small.width > canvas_width:
                canvas_width = max(canvas_width, x+small.width)
                if x < 0:
                    anchor_x -= x
                    canvas_width -= x
                    x = 0
            canvas = Image.new('RGBA', (canvas_width,96))
            canvas.alpha_composite(rgb, (x,0))
            output = OUTPUT / 'Characters/HighlightsRpg' / (name+'.png')
            write_image(canvas, output)
            manifest['poses'][name] = {'texture':resource(output),'source':rel(source),'source_sha256':digest(source),'source_rect':[box[0],box[1],box[2]-box[0],box[3]-box[1]],'pixel_size':list(canvas.size),'body_base_px':[64,96],'foot_px':[anchor_x,94],'world_scale':0.5,'world_body':[32,48],'source_scale':scale,'palette_colors':32,'alpha_cutoff':128,'kind':'authored_pose_not_automatic_full_walk_cycle'}


def publish_regions(manifest):
    # Pixel bounds are declared against the exact clean plates, never a guessed tile size.
    regions = {
        '01-shura-poet': [
            ('north_wall',[0,0,1448,400],-20), ('west_door_and_furnishings',[0,400,320,530],-19),
            ('floor_projection',[320,400,928,530],-30), ('east_furnishings',[1248,400,200,530],-19),
            ('foreground_railing',[0,930,1448,156],20),
        ],
        '06-last-transfer': [
            ('north_wall_and_upstairs',[240,0,840,180],-20), ('interior_floor',[240,180,840,340],-30),
            ('west_wall_foundation',[0,0,240,980],-19), ('east_wall_foundation',[1080,0,368,980],-19),
            ('threshold_platform',[240,520,840,70],-25), ('descending_steps',[240,590,840,390],-25),
            ('lower_ground',[0,980,1448,106],-30),
        ],
    }
    factor = 1024/1448
    for stage, definitions in regions.items():
        source = SOURCE/'Scenes/HighlightsRpg'/(stage+'-clean.png')
        clean = Image.open(source).convert('RGBA')
        rendered = clean.resize((1024,768),Image.Resampling.NEAREST)
        manifest['layers'][stage] = []
        for name, rect, z in definitions:
            current_source = source
            current_image = rendered
            if stage=='01-shura-poet' and name=='north_wall':
                # Corrected target's north-wall crop contains no character pixels.
                current_source = SOURCE/'Scenes/HighlightsRpg'/(stage+'-target.png')
                current_image=Image.open(current_source).convert('RGBA').resize((1024,768),Image.Resampling.NEAREST)
            x,y,w,h=rect
            bounds=[round(x*factor),round(y*factor),round((x+w)*factor),round((y+h)*factor)]
            piece=current_image.crop(bounds)
            output=OUTPUT/'Props/HighlightsRpg'/stage/(name+'.png')
            write_image(piece,output)
            manifest['layers'][stage].append({'id':name,'texture':resource(output),'source':rel(current_source),'source_sha256':digest(current_source),'source_rect':rect,'source_to_runtime':factor,'world_scale':0.5,'world_origin':[bounds[0]/2,bounds[1]/2],'world_size':[piece.width/2,piece.height/2],'world_phase':[bounds[0]/2,bounds[1]/2],'z_index':z,'repeat':False,'contains_people':False,'note':'Fixed projection region, not a universal seamless tile.'})


def publish_props(manifest):
    source=SOURCE/'Props/HighlightsRpg/06-cargo.png'
    image=Image.open(source).convert('RGBA')
    groups=sorted(components(image),key=lambda g:g[0][0])
    if len(groups)!=3:
        raise ValueError('cargo source must contain three separate objects')
    for name,(box,crop) in zip(['grain_sack','seedling_tray','medicine_crate'],groups):
        factor=64/max(crop.size)
        size=tuple(max(1,round(v*factor)) for v in crop.size)
        output=OUTPUT/'Props/HighlightsRpg/Cargo'/(name+'.png')
        write_image(crop.resize(size,Image.Resampling.NEAREST),output)
        manifest['props'][name]={'texture':resource(output),'source':rel(source),'source_rect':[box[0],box[1],box[2]-box[0],box[3]-box[1]],'world_scale':0.5,'foot_px':[size[0]/2,size[1]],'world_size':[v/2 for v in size]}
    source=SOURCE/'Props/HighlightsRpg/06-source-matter.png'
    image=Image.open(source).convert('RGBA')
    for index,vertical in enumerate([(0,296),(296,592),(592,887)]):
        crop=image.crop((0,vertical[0],image.width,vertical[1]))
        alpha=crop.getchannel('A').point(lambda a:255 if a>=128 else 0)
        crop.putalpha(alpha)
        output=OUTPUT/'Props/HighlightsRpg/Effects'/('source_matter_%d.png'%index)
        runtime_height=round(crop.height*1024/image.width)
        write_image(crop.resize((1024,runtime_height),Image.Resampling.NEAREST),output)
        manifest['effects'].append({'texture':resource(output),'source':rel(source),'source_rect':[0,vertical[0],image.width,vertical[1]-vertical[0]],'world_scale':0.5,'pixel_size':[1024,runtime_height],'purpose':'Independent advancing edge; repeat downward only in exterior clip region, never overlay interior.'})


def publish_door_occluders(manifest):
    # Exact pixel masks isolate open leaves for foot-based occlusion. No repaint.
    polygons = {
        '01-shura-poet': [('open_door_leaf',[(213,215),(302,198),(303,515),(214,483)],181)],
        '06-last-transfer': [('open_door_left',[(365,352),(438,399),(438,561),(365,518)],198),('open_door_right',[(959,256),(1078,312),(1078,536),(959,482)],190)],
    }
    manifest['door_occluders']={}
    for stage, entries in polygons.items():
        source=SOURCE/'Scenes/HighlightsRpg'/(stage+'-clean.png')
        original=Image.open(source).convert('RGBA')
        manifest['door_occluders'][stage]=[]
        for name,polygon,foot in entries:
            mask=Image.new('L',original.size)
            ImageDraw.Draw(mask).polygon(polygon,fill=255)
            layer=original.copy()
            layer.putalpha(mask)
            layer=layer.resize((1024,768),Image.Resampling.NEAREST)
            bounds=layer.getbbox()
            output=OUTPUT/'Props/HighlightsRpg'/stage/(name+'.png')
            write_image(layer.crop(bounds),output)
            manifest['door_occluders'][stage].append({'texture':resource(output),'world_origin':[bounds[0]/2,bounds[1]/2],'world_scale':0.5,'sort_foot_y':foot,'source':rel(source),'source_polygon':polygon,'note':'Occlusion-only duplicate of static open leaf. Never animate the leaf independently without clearing its base pixels.'})


def publish():
    manifest={'schema':1,'scope':['01-shura-poet','06-last-transfer'],'tool':'publish_highlights_rpg.py','logical_canvas':[512,384],'viewport':[1024,768],'camera_zoom':[2,2],'source_canvas':[1448,1086],'world_grid':32,'body_world':[32,48],'poses':{},'layers':{},'props':{},'effects':[],'limits':['Environment is separated into fixed architectural regions, not universal autotile terrain.','Body canvases have transparent overdraw for swords/arms; feet and collision remain independent.','Authored poses are not a complete locomotion library; do not alias static frames as a full gait.','No Godot render, collision or action acceptance is claimed by this publisher.']}
    publish_actors(manifest)
    publish_regions(manifest)
    publish_props(manifest)
    publish_door_occluders(manifest)
    path=OUTPUT/'Props/HighlightsRpg/publication.json'
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    REPORT.mkdir(parents=True,exist_ok=True)
    (REPORT/'publication.json').write_text(path.read_text(encoding='utf-8'),encoding='utf-8')
    layout=SOURCE/'Scenes/HighlightsRpg/layout.json'
    if layout.exists():
        (path.parent/'layout.json').write_text(layout.read_text(encoding='utf-8'),encoding='utf-8')
        (REPORT/'layout.json').write_text(layout.read_text(encoding='utf-8'),encoding='utf-8')
    print(rel(path))


if __name__=='__main__':
    publish()
