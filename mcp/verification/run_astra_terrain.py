"""Capture the actual gallery and source-cut terrain topology without a DV session."""
import argparse
import hashlib
import subprocess
import sys
import re
import shutil
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'mcp'))
from godot_workspace import resolve_godot, GAME


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    if any(out.iterdir()):
        raise ValueError('Capture requires an empty fixed output directory; stop old work and clean it explicitly')
    files = [ROOT / 'mcp/asset_pipeline/publish_astra_terrain.py', Path(__file__),
             Path(__file__).with_name('capture_astra_terrain.gd'),
             GAME / 'scenes/Shared/Field/AuthoredTerrainBorder.gd',
             GAME / 'scenes/Highlights/GalleryFloorOrnament.gd',
             GAME / 'scenes/Highlights/Highlights.gd',
             ROOT / 'mcp/rpg_adapter/data/autotile_tables.gd',
             ROOT / 'mcp/asset_pipeline/sources/Internal/Props/AuthoredRooms/shura_gallery.png',
             *GAME.joinpath('ShareAsset/Internal/Props/AstraTerrain').glob('*')]
    (out / 'sources.sha256').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.relative_to(ROOT).as_posix() + '\n' for p in files if p.is_file() and p.suffix != '.import'), encoding='utf-8')
    godot = resolve_godot(args.godot)
    with tempfile.TemporaryDirectory(prefix='astra-terrain-', dir=ROOT / '.scratch') as temporary:
        work = Path(temporary)
        pending = ['scenes/Highlights/Highlights.tscn', 'scenes/Shared/Field/AuthoredTerrainBorder.gd',
                   'ShareAsset/Internal/Props/AstraTerrain', 'ShareAsset/Internal/Props/Highlights',
                   'ShareAsset/Internal/Props/AuthoredRooms', 'ShareAsset/Internal/Props/ModernStage',
                   'ShareAsset/Internal/Characters/Highlights', 'ShareAsset/Internal/Characters/PixelCast',
                   'Contracts/highlight_stages.json']
        seen = set()
        while pending:
            relative = pending.pop()
            if relative in seen: continue
            seen.add(relative)
            source = GAME / relative
            if source.is_dir():
                pending.extend(p.relative_to(GAME).as_posix() for p in source.rglob('*') if p.is_file() and p.suffix != '.import')
                continue
            if not source.is_file():
                # Dynamic format strings and directory prefixes are supplied by the explicit roots above.
                if '%' in relative or relative.endswith(('/', '_')): continue
                raise FileNotFoundError(source)
            target = work / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            if source.suffix in ('.gd', '.tres', '.tscn', '.gdshader'):
                pending.extend(re.findall(r'res://([^"\s\)]+)', source.read_text(encoding='utf-8')))
        shutil.copyfile(Path(__file__).with_name('capture_astra_terrain.gd'), work / 'capture.gd')
        (work / 'project.godot').write_text('config_version=5\n[application]\nconfig/name="Astra terrain finite render"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n', encoding='utf-8')
        with (out / 'import.log').open('w', encoding='utf-8') as log:
            subprocess.run([godot, '--headless', '--editor', '--path', str(work), '--import', '--quit'], stdout=log, stderr=subprocess.STDOUT, timeout=90, check=True)
        with (out / 'console.log').open('w', encoding='utf-8') as log:
            result = subprocess.run([godot, '--path', str(work), '--rendering-method', 'gl_compatibility',
                                     '--script', 'res://capture.gd', '--', '--out=' + str(out)],
                                    stdout=log, stderr=subprocess.STDOUT, timeout=60)
    console = (out / 'console.log').read_text(encoding='utf-8')
    return result.returncode or int('ERROR:' in console or 'SCRIPT ERROR:' in console or not (out / 'observations.dvlog').is_file())


if __name__ == '__main__':
    raise SystemExit(main())

