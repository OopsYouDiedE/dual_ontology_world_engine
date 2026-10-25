"""Sequential, isolated real-renderer captures for the four public-hall routes."""
import argparse
import hashlib
import json
import re
import shutil
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / 'godot_csharp'
sys.path.insert(0, str(ROOT / 'mcp'))
from godot_workspace import resolve_godot


def copy_closure(work, route):
    pending = [f'scenes/ArtStyleTrials/{route}']
    asset_root = f'ShareAsset/Internal/ArtStyleTrials/{route}'
    if (GAME / asset_root).is_dir():
        pending.append(asset_root)
    seen = set()
    copied = []
    while pending:
        relative = pending.pop()
        if relative in seen:
            continue
        seen.add(relative)
        source = GAME / relative
        if source.is_dir():
            pending.extend(p.relative_to(GAME).as_posix() for p in source.rglob('*')
                           if p.is_file() and p.suffix not in ('.import', '.uid'))
            continue
        if not source.is_file():
            if '%' in relative or relative.endswith(('/', '_')):
                continue
            raise FileNotFoundError(source)
        target = work / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        copied.append(source)
        if source.suffix in ('.gd', '.tscn', '.tres', '.gdshader'):
            pending.extend(re.findall(r'res://([^"\s\)]+)', source.read_text(encoding='utf-8-sig')))
    return copied


def capture(godot, route, out):
    out.mkdir(parents=True, exist_ok=True)
    if any(out.iterdir()):
        raise ValueError(f'Stop prior work and clean fixed route output before capture: {out}')
    scratch = ROOT / '.scratch'
    scratch.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='style-route-' + route.lower() + '-', dir=scratch) as temporary:
        work = Path(temporary)
        files = copy_closure(work, route)
        renderer = "forward_plus" if route == "E" else "gl_compatibility"
        capture_script = Path(__file__).with_name('capture_style_route.gd')
        shutil.copyfile(capture_script, work / 'capture.gd')
        (work / 'project.godot').write_text(f'''config_version=5
[application]
config/name="Public hall art route comparison"
[display]
window/size/viewport_width=1000
window/size/viewport_height=1000
[rendering]
renderer/rendering_method="{renderer}"
renderer/rendering_method.mobile="{renderer}"
lights_and_shadows/directional_shadow/size=4096
lights_and_shadows/positional_shadow/atlas_size=4096
lights_and_shadows/directional_shadow/soft_shadow_filter_quality=3
lights_and_shadows/positional_shadow/soft_shadow_filter_quality=3
environment/defaults/default_clear_color=Color(0.06,0.07,0.08,1)
''', encoding='utf-8')
        files.extend([capture_script, Path(__file__)])
        (out / 'sources.sha256').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.relative_to(ROOT).as_posix() + '\n' for p in sorted(files)), encoding='utf-8')
        with (out / 'import.log').open('w', encoding='utf-8') as log:
            imported = subprocess.run([godot, '--headless', '--editor', '--path', str(work), '--import', '--quit'],
                                      stdout=log, stderr=subprocess.STDOUT, timeout=120)
        if imported.returncode:
            return {'route': route, 'status': 'import-failed', 'exit_code': imported.returncode}
        with (out / 'console.log').open('w', encoding='utf-8') as log:
            result = subprocess.run([godot, '--path', str(work), '--rendering-method', renderer,
                                     '--script', 'res://capture.gd', '--', '--route=' + route, '--out=' + str(out)],
                                    stdout=log, stderr=subprocess.STDOUT, timeout=60)
    logs = '\n'.join((out / name).read_text(encoding='utf-8') for name in ('console.log', 'import.log'))
    path = out / 'observations.dvlog'
    sealed = False
    if path.is_file():
        b = path.read_bytes()
        sealed = len(b) >= 48 and b[:8] == b'DVSTYLE1' and len(b) == 48 + struct.unpack('<Q', b[8:16])[0] and hashlib.sha256(b[48:]).digest() == b[16:48]
    expected = ['normal', 'normal-dynamic', 'off', 'shift']
    if route == 'E':
        expected += ['effects-off', 'room-hall', 'room-jia', 'room-yi', 'room-bing', 'room-ding', 'room-bath', 'detail-reading', 'detail-gathering']
    pictures = all((out / (mode + '.png')).is_file() for mode in expected)
    return {'route': route, 'status': 'captured' if result.returncode == 0 and sealed and pictures and 'ERROR:' not in logs else 'failed',
            'exit_code': result.returncode, 'renderer': renderer, 'sealed': sealed, 'pictures': pictures,
            'errors_in_logs': 'ERROR:' in logs, 'warnings_in_logs': 'WARNING:' in logs,
            'scope': 'finite scene render and light-mode observations; no Core, AI, gameplay or performance acceptance'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', required=True)
    parser.add_argument('--routes', nargs='+', choices=list('ABCDE'), default=list('ABCD'))
    parser.add_argument('--out', type=Path, default=ROOT / 'artifacts/style-route-comparison/runtime')
    args = parser.parse_args()
    godot = resolve_godot(args.godot)
    summary = []
    for route in args.routes:
        out = args.out.resolve() / route
        try:
            result = capture(godot, route, out)
        except Exception as error:
            result = {'route': route, 'status': 'failed', 'error': str(error)}
        out.mkdir(parents=True, exist_ok=True)
        (out / 'capture-summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        summary.append(result)
        print(json.dumps(result, ensure_ascii=False), flush=True)
    return int(any(item['status'] != 'captured' for item in summary))


if __name__ == '__main__':
    raise SystemExit(main())
