"""Finite E library render, isolated pure GDScript workspace. Never starts Core."""
import argparse
import hashlib
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'mcp/verification'))
from run_style_routes import copy_closure

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--godot',required=True)
    parser.add_argument('--diagnose',action='store_true')
    args=parser.parse_args()
    out=ROOT/'artifacts/style-e/library-render'
    out.mkdir(parents=True,exist_ok=True)
    if any(out.iterdir()): raise RuntimeError('Explicitly clear fixed library-render after previous capture has stopped')
    with tempfile.TemporaryDirectory(prefix='e-library-',dir=ROOT/'.scratch') as temporary:
        work=Path(temporary)
        sources=copy_closure(work,'E')
        sources += [Path(__file__), ROOT/'mcp/verification/capture_e_library.gd']
        (out/'sources.sha256').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(ROOT).as_posix()+'\n' for p in sorted(sources)),encoding='utf-8')
        shutil.copyfile(ROOT/'mcp/verification/capture_e_library.gd',work/'capture.gd')
        (work/'project.godot').write_text('''config_version=5
[application]
config/name="E asset library"
[display]
window/size/viewport_width=800
window/size/viewport_height=800
[rendering]
renderer/rendering_method="forward_plus"
lights_and_shadows/directional_shadow/size=4096
lights_and_shadows/positional_shadow/atlas_size=4096
lights_and_shadows/directional_shadow/soft_shadow_filter_quality=3
lights_and_shadows/positional_shadow/soft_shadow_filter_quality=3
''',encoding='utf-8')
        for name, command, timeout in [
            ('import.log',['--headless','--editor','--import','--quit'],120),
            ('console.log',['--script','res://capture.gd','--','--out='+str(out)]+(['--diagnose'] if args.diagnose else []),240)]:
            with (out/name).open('w',encoding='utf-8') as log:
                result=subprocess.run([args.godot,'--path',str(work),*command],stdout=log,stderr=subprocess.STDOUT,timeout=timeout)
            if result.returncode: raise RuntimeError(f'{name}: exit {result.returncode}')
    print(f'Captured {len(list(out.glob("*.png")))} views. Inspect logs and images; render only, no gameplay acceptance.')

if __name__=='__main__': main()
