"""MCP access to shared furniture and material recipes used by current maps."""
from __future__ import annotations

import hashlib
import inspect
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw
import workflow as w

sys.path.insert(0, str(w.GAME / 'tools'))
from first_awakening_art import palette, props, architecture

SOURCE = w.GAME / 'AssetsSource/Internal/Props/FirstAwakeningInterior'
OUTPUT = w.GAME / 'ShareAsset/Internal/Props/FirstAwakeningInterior'
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
            for p in sorted(RECIPE_DIR.glob('*.py'))}
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
    """Read the shared recipe catalog and actual material ramps."""
    ramps = {key: ['#%02x%02x%02x' % tuple(c[:3]) for c in value]
             for key, value in vars(palette).items()
             if key.isupper() and isinstance(value, list) and value
             and isinstance(value[0], tuple)}
    return dict(grid=32,
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
        source_reference='docs/design-standard.md',
        scope='Shared furniture and material recipes restored from repository history. No complete scene reconstruction or character animation generation.')


def read_native_recipe(module: str) -> dict:
    """Read the actual recipe source for understanding and deliberate edits; fixed module allowlist."""
    if module not in {'pixel', 'palette', 'props', 'architecture', 'layout'}:
        raise ValueError('module must be pixel, palette, props, architecture or layout')
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
