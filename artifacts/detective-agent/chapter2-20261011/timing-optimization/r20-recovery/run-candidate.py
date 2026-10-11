"""Isolated, read-only-source 120s real detective run of the archived commit."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[5]
OUT=Path(__file__).resolve().parent
TARGET=ROOT/'scratch/r20-recovery'
def fingerprint():
    paths=[p for directory in ('csharp','godot_csharp','tools','docs') for p in (TARGET/directory).rglob('*')
           if p.is_file() and not any(x in ('bin','obj','.godot') for x in p.relative_to(TARGET).parts)]
    paths += list((TARGET/'godot_csharp/.godot/mono/temp/bin/Debug').glob('*.dll'))
    return {p.relative_to(TARGET).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
before=fingerprint()
(OUT/'candidate-source-seal.json').write_text(json.dumps(before,ensure_ascii=False,indent=2),encoding='utf-8')
cmd=[sys.executable,'godot_csharp/tools/full_test.py','--name','r20-commit-recovery-120','--seed','0','--time-scale','4','--max-seconds','120','--no-progress-seconds','60','--shot-seconds','10','--line-seconds','0.6']
(OUT/'candidate-command.json').write_text(json.dumps(cmd,ensure_ascii=False,indent=2),encoding='utf-8')
with (OUT/'candidate-run.log').open('wb') as stream:
    run=subprocess.run(cmd,cwd=TARGET,env={**os.environ,'GODOT':'C:/Users/iii/Desktop/Godot_v4.6.1-stable_mono_win64/Godot_v4.6.1-stable_mono_win64_console.exe'},stdout=stream,stderr=subprocess.STDOUT)
if fingerprint()!=before:raise RuntimeError('Candidate source/runtime changed during execution')
raw=TARGET/'tmp/full-test/r20-commit-recovery-120'
if raw.exists():shutil.copytree(raw,OUT/'candidate-raw')
shutil.copyfile(raw.with_suffix('.log'),OUT/'candidate-godot.log')
(OUT/'candidate-guard-end.json').write_text(json.dumps({'sources_and_runtime_unchanged':True,'exit':run.returncode}),encoding='utf-8')
