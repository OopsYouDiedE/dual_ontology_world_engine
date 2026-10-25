"""Render formal map resources in a minimal, disposable asset-only project."""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / 'godot_csharp'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='furnished-rooms-', dir=ROOT / '.scratch') as temporary:
        work = Path(temporary)
        pending = ['Contracts/geometry_contract.json', 'Contracts/room_art_finish.json',
                   'scenes/Story/StoryActor.gd', 'scenes/Shared/Field/RoomArtFinish.gd',
                   'ShareAsset/Internal/Characters/PixelCast/detective.tres']
        seen = set()
        while pending:
            relative = pending.pop()
            if relative in seen: continue
            seen.add(relative)
            source = GAME / relative
            if source.is_dir():
                pending.extend(p.relative_to(GAME).as_posix() for p in source.rglob('*') if p.is_file() and p.suffix != '.import')
                continue
            if not source.is_file(): raise FileNotFoundError(source)
            target = work / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            if source.suffix in ('.tscn','.tres','.gd','.gdshader','.json'):
                pending.extend(re.findall(r'res://([^"\s\)]+)', source.read_text(encoding='utf-8')))
        shutil.copyfile(ROOT/'mcp/asset_pipeline/capture_maps.gd',work/'capture.gd')
        shutil.copyfile(Path(__file__).with_name('check_furnishing_motion.gd'),work/'motion.gd')
        (work/'project.godot').write_text('config_version=5\n[application]\nconfig/name="Furnished room render fixture"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n[duelview_tools]\nrepository="'+ROOT.as_posix()+'"\n',encoding='utf-8')
        with (out/'import.log').open('w',encoding='utf-8') as log:
            subprocess.run([args.godot,'--headless','--editor','--path',str(work),'--import','--quit'],stdout=log,stderr=subprocess.STDOUT,check=True,timeout=90)
        results = {}
        for script, target, headless in [('capture.gd','runtime',False),('motion.gd','physics',True)]:
            with (out/(target+'.log')).open('w',encoding='utf-8') as log:
                command=[args.godot,'--path',str(work),'--script','res://'+script]
                if headless: command.append('--headless')
                results[target]=subprocess.run(command+['--','--out='+str(out/target)],stdout=log,stderr=subprocess.STDOUT,timeout=90).returncode
        (out/'capture-summary.json').write_text(json.dumps({'exit_codes':results,'scope':'asset render and independent collision fixture; no DV session','resources':len(seen)},indent=2),encoding='utf-8')
        return int(any(results.values()))


if __name__ == '__main__':
    raise SystemExit(main())
