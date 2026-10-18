"""Freeze and run the single authorized 240-second fresh formal session."""
from pathlib import Path
import subprocess,sys,json,hashlib,shutil,os,time
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
TASK=HERE.parent
BATCH=HERE/'frozen'
BATCH.mkdir(exist_ok=False)
paths={ROOT/rel for rel in json.loads((TASK/'frozen-07/source-hashes.json').read_text(encoding='utf-8'))}
paths.update((ROOT/'language_core/tools').glob('*.py'))
paths.update((ROOT/'language_core/tools').glob('*.cs'))
paths.update([ROOT/'language_core/compiler/README.md',Path(__file__)])
expected={}
for source in sorted(paths):
 rel=source.relative_to(ROOT).as_posix()
 expected[rel]=hashlib.sha256(source.read_bytes()).hexdigest()
 target=BATCH/'source'/rel
 target.parent.mkdir(parents=True,exist_ok=True)
 shutil.copyfile(source,target)
(BATCH/'source-hashes.json').write_text(json.dumps(expected,indent=2),encoding='utf-8')
(BATCH/'git-origin.json').write_text(json.dumps({'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'tracked_dirty':subprocess.check_output(['git','diff','--name-only'],cwd=ROOT,text=True).splitlines(),'authority':'frozen source bytes and SHA-256, not HEAD alone'},indent=2),encoding='utf-8')
out=BATCH/'standard-yi'
call=[sys.executable,str(ROOT/'language_core/tools/run_debug.py'),'--out',str(out),'--timeout','240','--raw-format','dag','--projection-every','32']
(BATCH/'planned-inputs.json').write_text(json.dumps({'command':call,'fresh_session':True,'one_window_only':True,'runtime':'default server GC four heaps','projection_every':32},indent=2),encoding='utf-8')
changed=[rel for rel,digest in expected.items() if hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()!=digest]
if changed: raise RuntimeError('Source changed before run: '+repr(changed))
print(json.dumps({'starting':'standard-yi','timeout_seconds':240,'time':time.time()}),flush=True)
proc=subprocess.run(call,cwd=ROOT,env=os.environ.copy())
result=json.loads((out/'result.json').read_text(encoding='utf-8')) if (out/'result.json').exists() else {}
changed=[rel for rel,digest in expected.items() if not (ROOT/rel).is_file() or hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()!=digest]
(BATCH/'freeze-result.json').write_text(json.dumps({'runner_exit':proc.returncode,'changed_sources':changed,'single_window_executed':True,'result_path':str(out/'result.json')},indent=2),encoding='utf-8')
print(json.dumps({'runner_exit':proc.returncode,'changed_sources':changed,'result':{key:result.get(key) for key in ['exit_code','timeout_stage','source_unchanged','evidence_complete','world_horizon_observed','raw_committed_events']}},indent=2),flush=True)
if changed: raise SystemExit(2)
raise SystemExit(proc.returncode)
