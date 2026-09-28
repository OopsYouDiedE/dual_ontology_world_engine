"""MCP access to the existing First Awakening recipes, never a parallel renderer."""
from __future__ import annotations

import hashlib
import inspect
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw
import workflow as w
import pixel_handoff

sys.path.insert(0, str(w.GAME / 'tools'))
import build_first_awakening_interior as builder
from first_awakening_art import palette, props, architecture, layout, placement

SOURCE = w.GAME / 'AssetsSource/Internal/Props/FirstAwakeningInterior'
OUTPUT = builder.TARGET
RECIPE_DIR = w.GAME / 'tools/first_awakening_art'

# Explicit public drawing entry points. Helpers such as slab/leg need an existing canvas.
PROP_RECIPES = {name: getattr(props, name) for name in (
    'bed', 'nightstand', 'desk', 'chair_back', 'chair_front', 'bookcase', 'wardrobe',
    'linen_chest', 'fern', 'paper_basket', 'wall_clock', 'floor_lamp', 'study_table',
    'sideboard', 'worktable', 'washstand', 'side_table', 'range_stove', 'armchair',
    'transmitter', 'instructions', 'mechanism_sketch', 'stop_base', 'dresser',
    'coat_stand', 'globe', 'sofa', 'console_table', 'barrel', 'crate', 'sack',
    'bench', 'plant_stand', 'side_table_plain', 'room_door_leaf', 'isolation_gate', 'inner_bolt')}


def _hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _sources() -> dict:
    current = {str(p.relative_to(w.REPO)).replace('\\', '/'): _hash(p)
            for p in [Path(builder.__file__), *sorted(RECIPE_DIR.glob('*.py'))]}
    if '_loaded_source_hashes' in globals() and current != _loaded_source_hashes:
        raise ValueError('Recipe source changed since this MCP process loaded; restart before rendering or reporting provenance')
    return current


_loaded_source_hashes = _sources()


def _protect_original(source: Path, record: Path, kind: str) -> None:
    """Only our own recorded PNG derivatives can be replaced; historical originals are immutable here."""
    if source.exists():
        if not record.is_file():
            raise ValueError('Existing unowned source artwork must be preserved; choose a new asset name')
        info = json.loads(record.read_text(encoding='utf-8'))
        if info.get('producer') != kind or info.get('source_png_sha256') != _hash(source):
            raise ValueError('Source provenance changed or artist edits exist; choose a new asset name')


def native_art_catalog() -> dict:
    """Read Claude's user-identified life-wing recipe catalog and actual material ramps."""
    ramps = {key: ['#%02x%02x%02x' % tuple(c[:3]) for c in value]
             for key, value in vars(palette).items()
             if key.isupper() and isinstance(value, list) and value
             and isinstance(value[0], tuple)}
    return dict(grid=layout.CELL, map_size=[layout.W, layout.H],
        actor_design=[32, 48], actor_runtime_carrier=[64, 64],
        ramps=ramps,
        floor_materials=['oak','corridor','study','prep','hall'],
        prop_recipes={name: str(inspect.signature(fn)) for name, fn in PROP_RECIPES.items()},
        source_hashes=_sources(),
        design=dict(value='Pale floors, middle-value walnut, coloured wall faces, dark caps/void.',
            lighting='Upper-left material highlights; cool fog glass against warm lamps.',
            shading='Hue-shifted ramps, selective outlines, sparse directional texture, seeded variation.',
            identity='TEAL/SAGE/ROSE/PLUM wallpaper, quilt and rug motifs; not a universal 16-colour palette.',
            layers='Runtime architecture is a flattened painted background; furniture, doors and bolt are separate sprites.',
            caveats='mix/shift/alpha blending add colours beyond base ramps. Pixel count alone cannot assess quality. Baked contact shadows require rebuild after furniture moves.'),
        source_reference='docs/tilemap-workflow.md',
        scope='All current architecture and prop recipes; no invented character animation frames.')


def read_native_recipe(module: str) -> dict:
    """Read the actual recipe source for understanding and deliberate edits; fixed module allowlist."""
    if module not in {'pixel', 'palette', 'props', 'architecture', 'layout', 'placement', 'mind_ui'}:
        raise ValueError('module must be pixel, palette, props, architecture, layout, placement or mind_ui')
    path = RECIPE_DIR / (module + '.py')
    return dict(path=str(path), sha256=_hash(path), source=path.read_text(encoding='utf-8'),
                note='Restart the MCP process after editing Python recipes so imported modules are refreshed.')


def render_native_prop(name: str, recipe: str, parameters: dict | None = None,
                       replace: bool = False) -> dict:
    """Run a named existing furniture recipe at 1x; archive parameters, source hashes and ground anchor.
    parameters: room=0..3, seed=0..2^32-1, side=-1|1, coat/vase/open_=boolean as applicable.
    No resizing, arbitrary code, new palettes, or inferred collision rectangles.
    """
    w.name_ok(name)
    _sources()
    if recipe not in PROP_RECIPES:
        raise ValueError('Unknown recipe; call native_art_catalog')
    params = parameters or {}
    fn = PROP_RECIPES[recipe]
    signature = inspect.signature(fn)
    try:
        bound = signature.bind(**params)
    except TypeError as exc:
        raise ValueError(str(exc)) from exc
    bound.apply_defaults()
    for key, value in bound.arguments.items():
        if key in ('coat', 'vase', 'open_'):
            if type(value) is not bool: raise ValueError(key + ' must be boolean')
        elif type(value) is not int:
            raise ValueError(key + ' must be an integer')
        elif key == 'room' and not 0 <= value <= 3:
            raise ValueError('room must be 0..3')
        elif key == 'side' and value not in (-1, 1):
            raise ValueError('side must be -1 or 1')
        elif key == 'seed' and not 0 <= value < 2**32:
            raise ValueError('seed must be 0..2^32-1')
    source = w.within(SOURCE / (name + '.png'), SOURCE)
    output = w.within(OUTPUT / (name + '.png'), OUTPUT)
    record = w.within(SOURCE / (name + '.recipe.json'), SOURCE)
    _protect_original(source,record,'native_prop')
    if not replace and any(p.exists() for p in (source, output, record)):
        raise ValueError('Asset exists; replace=true is required for an intentional rebuild')
    canvas = fn(**bound.arguments)
    data = dict(recipe=recipe, parameters=bound.arguments, source_hashes=_sources(),
        size=[canvas.w, canvas.h], anchor=[canvas.w / 2, canvas.h],
        collision=None, collision_note='Read the actual scene placement; canvas bounds are not footprint.',
        authorship='native 1x recipe, no resampling', art_review='pending')
    canvas.save(source)
    canvas.save(output)
    data.update(producer='native_prop',source_png_sha256=_hash(source))
    w.save_json(record, data)
    return dict(source=str(source), output=str(output), recipe_record=str(record), **data)


def render_native_material(name: str, material: str, crop: list[int], replace: bool = False) -> dict:
    """Export an explicit 1x crop [x,y,w,h] of an existing floor recipe, without resizing.
    These are continuous seeded material fields, not guaranteed periodic 32px tiles.
    """
    w.name_ok(name)
    _sources()
    if material not in ('oak','corridor','study','prep','hall'):
        raise ValueError('material must be oak, corridor, study, prep or hall')
    if len(crop) != 4 or any(type(v) is not int for v in crop):
        raise ValueError('crop must be integer [x,y,w,h]')
    x,y,width,height=crop
    if min(x,y)<0 or min(width,height)<1 or x+width>architecture.MAP_W or y+height>architecture.MAP_H:
        raise ValueError('crop must be inside the original material field')
    source= w.within(SOURCE/(name+'.png'), SOURCE)
    output= w.within(OUTPUT/(name+'.png'), OUTPUT)
    record= w.within(SOURCE/(name+'.material.json'), SOURCE)
    _protect_original(source,record,'native_material')
    if not replace and any(p.exists() for p in (source,output,record)):
        raise ValueError('Asset exists; pass replace=true only for deliberate replacement')
    im=architecture.floor_materials()[material].image().crop((x,y,x+width,y+height))
    SOURCE.mkdir(parents=True,exist_ok=True)
    OUTPUT.mkdir(parents=True,exist_ok=True)
    im.save(source)
    im.save(output)
    info=dict(material=material,crop=crop,source_hashes=_sources(),resampling=False,
        tiling='Not proven periodic; inspect an actual repeated layout before using as a tile.',
        source=str(source),output=str(output))
    info.update(producer='native_material',source_png_sha256=_hash(source))
    w.save_json(record,info)
    return info


def rebuild_native_interior(mode: str = 'verify') -> dict:
    """Render every life-wing recipe through the existing builder.
    verify compares pixels/layout without changing runtime files; rebuild deliberately replaces
    only files owned by this builder. Both save a report in the existing tilemap-workflow archive.
    """
    if mode not in ('verify', 'rebuild'):
        raise ValueError('mode must be verify or rebuild')
    _sources()
    canvases, data, manifest = builder.render()
    comparisons = []
    for name, canvas in canvases.items():
        path = w.within(OUTPUT / (name + '.png'), OUTPUT)
        expected = canvas.image()
        equal = False
        if path.exists():
            with Image.open(path) as im:
                current = im.convert('RGBA')
                equal = current.size == expected.size and current.tobytes() == expected.tobytes()
        comparisons.append(dict(name=name, size=list(expected.size), runtime_matches=equal,
            pixel_sha256=hashlib.sha256(expected.tobytes()).hexdigest()))
    layout_path = OUTPUT / 'layout.json'
    layout_matches = layout_path.exists() and json.loads(layout_path.read_text(encoding='utf-8')) == data
    manifest['source_hashes'] = _sources()
    if mode == 'rebuild':
        for name, canvas in canvases.items():
            canvas.save(w.within(OUTPUT / (name + '.png'), OUTPUT))
        w.save_json(layout_path, data)
        w.save_json(OUTPUT / 'manifest.json', manifest)
    report = dict(mode=mode, sprites=len(canvases)-1, images=comparisons,
        layout_matches=layout_matches, matches_before_write=layout_matches and all(x['runtime_matches'] for x in comparisons),
        source_hashes=_sources(),
        runtime_written=mode == 'rebuild', art_review='not performed by this tool',
        next_action='Inspect 1x pixels and runtime doors/occlusion. This is not a playthrough result.')
    w.save_json(w.REPORT / 'native-art-check.json', report)
    return report


def native_art_contact_sheet() -> dict:
    """Create a labelled nearest-neighbour review sheet of every current prop recipe; no runtime writes."""
    _sources()
    sprites = props.build()
    columns, cell_w, cell_h = 6, 232, 190
    sheet = Image.new('RGB', (columns * cell_w, ((len(sprites)+columns-1)//columns)*cell_h), '#24212a')
    draw = ImageDraw.Draw(sheet)
    for i, (name, canvas) in enumerate(sprites.items()):
        x, y = (i % columns)*cell_w, (i//columns)*cell_h
        draw.text((x+6,y+5), name, fill='#ead9b5')
        im = canvas.image()
        factor = max(1, min(2, (cell_w-12)//canvas.w, (cell_h-32)//canvas.h))
        zoom = im.resize((canvas.w*factor,canvas.h*factor),Image.Resampling.NEAREST)
        sheet.paste(zoom,(x+6,y+25),zoom)
    w.REPORT.mkdir(parents=True,exist_ok=True)
    path = w.REPORT/'native-props-review.png'
    sheet.save(path)
    return dict(path=str(path), count=len(sprites), status='offline recipe review, not runtime capture')


def export_native_handoff(replace: bool = False) -> dict:
    """Export all current props as editable static Aseprite sources and an untrimmed PNG/JSON atlas.
    Also export R-05 with background and furniture as separate named layers. Furniture interiors
    and the architectural background stay flattened exactly as their current source recipes do.
    These editing sources are not automatically consumed by the recipe rebuilder.
    """
    _sources()
    canvases, data, _ = builder.render()
    sprites = {name: canvas.image() for name, canvas in canvases.items() if name != 'life_wing'}
    source_outputs = {SOURCE/'life_wing.aseprite': pixel_handoff._make_aseprite([
        ('architecture (painted, flattened)',canvases['life_wing'].image())])}
    for name, im in sprites.items():
        source_outputs[SOURCE/(name+'.aseprite')] = pixel_handoff._make_aseprite([(name, im)])
    room_layers = [('architecture (painted, flattened)', canvases['life_wing'].image().crop((0,0,320,320)))]
    for item in sorted(placement.dorm(0), key=lambda item: item['position'][1]):
        sprite = sprites[item['sprite']]
        layer = Image.new('RGBA',(320,320))
        x,y = item['position']
        layer.alpha_composite(sprite,(int(x-sprite.width/2),y-sprite.height))
        room_layers.append((item['name'],layer))
    source_outputs[SOURCE/'room-05-handoff.aseprite'] = pixel_handoff._make_aseprite(room_layers)
    tile_w = max(im.width for im in sprites.values()) + 2
    tile_h = max(im.height for im in sprites.values()) + 2
    columns = 6
    atlas = Image.new('RGBA',(tile_w*columns, tile_h*((len(sprites)+columns-1)//columns)))
    frames = []
    for i,(name,im) in enumerate(sprites.items()):
        x,y=(i%columns)*tile_w,(i//columns)*tile_h
        atlas.alpha_composite(im,(x,y))
        frames.append(dict(filename=name,frame=dict(x=x,y=y,w=im.width,h=im.height),
            rotated=False,trimmed=False,spriteSourceSize=dict(x=0,y=0,w=im.width,h=im.height),
            sourceSize=dict(w=im.width,h=im.height),duration=100,
            anchor=[im.width/2,im.height],collision=None))
    atlas_path = OUTPUT/'native-props-atlas.png'
    atlas_json = OUTPUT/'native-props-atlas.json'
    manifest_path = SOURCE/'native-handoff.json'
    destinations = [*source_outputs,atlas_path,atlas_json,manifest_path]
    for path in destinations:
        w.within(path,SOURCE if path in source_outputs or path == manifest_path else OUTPUT)
    if not replace and any(p.exists() for p in destinations):
        raise ValueError('Handoff exists. replace=true deliberately replaces editable sources; preserve artist edits first.')
    SOURCE.mkdir(parents=True,exist_ok=True)
    OUTPUT.mkdir(parents=True,exist_ok=True)
    for path, content in source_outputs.items(): path.write_bytes(content)
    atlas.save(atlas_path)
    w.save_json(atlas_json,dict(frames=frames,meta=dict(image=atlas_path.name,size=dict(w=atlas.width,h=atlas.height),
        scale='1',app='rpg-tilemap native recipe handoff',note='Independent static props, not animation frames.')))
    manifest = dict(source_hashes=_sources(), source_files=[str(p) for p in source_outputs],
        atlas=str(atlas_path),metadata=str(atlas_json),room_layers=[n for n,_ in room_layers],
        placements=data['furniture'],
        limitations=['Static single-frame documents; no fabricated character animation.',
            'Each furniture source has one painted layer; background room architecture is flattened.',
            'Room handoff is an offline editing reference, without characters, runtime lights or interaction.',
            'Aseprite edits require explicit export/integration; rebuilding Python recipes overwrites their runtime PNGs.'],
        artistic_acceptance='pending')
    w.save_json(manifest_path,manifest)
    return dict(sources=len(source_outputs),room_layers=len(room_layers),manifest=str(manifest_path),
        atlas=str(atlas_path),metadata=str(atlas_json),artistic_acceptance='pending')
