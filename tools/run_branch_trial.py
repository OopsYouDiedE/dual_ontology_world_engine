"""90-second real detective session against an explicitly committed branch seal."""
from pathlib import Path
import argparse,hashlib,json,os,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--name',required=True)
    parser.add_argument('--seal',required=True)
    args=parser.parse_args()
    if Path(args.name).name!=args.name:raise ValueError('Plain run name required')
    anchor=subprocess.check_output(['git','show','HEAD:'+args.seal],cwd=ROOT)
    manifest=json.loads(anchor)
    def check():
        if json.loads((ROOT/args.seal).read_bytes())!=manifest:raise RuntimeError('Committed seal altered')
        changed=[n for n,h in manifest['files'].items() if not (ROOT/n).is_file() or sha(ROOT/n)!=h]
        if changed:raise RuntimeError('Protected sources/runtime changed: '+str(changed))
    check()
    out=ROOT/'artifacts/detective-agent/chapter2-20261011/timing-optimization/branch-trials'/args.name
    out.mkdir(parents=True,exist_ok=False)
    command=[sys.executable,str(ROOT/'godot_csharp/tools/full_test.py'),'--name',args.name,'--seed','0','--time-scale','4','--max-seconds','90','--no-progress-seconds','60','--shot-seconds','10','--line-seconds','0.6']
    (out/'command.json').write_text(json.dumps(command,indent=2),encoding='utf-8')
    (out/'guard-start.json').write_text(json.dumps({'seal_sha256':hashlib.sha256(anchor).hexdigest(),'branch':subprocess.check_output(['git','branch','--show-current'],cwd=ROOT).decode().strip()}),encoding='utf-8')
    env={**os.environ,'GODOT':os.environ.get('GODOT','C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe')}
    with (out/'runner.log').open('wb') as stream:run=subprocess.run(command,cwd=ROOT,env=env,stdout=stream,stderr=subprocess.STDOUT)
    raw=ROOT/'tmp/full-test'/args.name
    if raw.exists():shutil.copytree(raw,out/'raw')
    if raw.with_suffix('.log').exists():shutil.copyfile(raw.with_suffix('.log'),out/'godot.log')
    check()
    (out/'guard-end.json').write_text(json.dumps({'sources_and_runtime_unchanged':True,'exit':run.returncode}),encoding='utf-8')
    (out/'evidence-sha256.json').write_text(json.dumps({p.relative_to(out).as_posix():sha(p) for p in out.rglob('*') if p.is_file()},indent=2),encoding='utf-8')
    print(out)
if __name__=='__main__':main()
