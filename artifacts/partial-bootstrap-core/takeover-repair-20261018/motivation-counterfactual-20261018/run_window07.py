from pathlib import Path
import subprocess,sys,json,hashlib,time,shutil,os
ROOT=Path(__file__).resolve().parents[4]
TASK=Path(__file__).resolve().parent
BATCH=TASK/'frozen-07'
BATCH.mkdir(exist_ok=False)
previous=json.loads((TASK/'frozen-06/source-hashes.json').read_text(encoding='utf-8'))
expected={}
for rel in previous:
 source=ROOT/rel
 expected[rel]=hashlib.sha256(source.read_bytes()).hexdigest()
 target=BATCH/'source'/rel
 target.parent.mkdir(parents=True,exist_ok=True)
 shutil.copyfile(source,target)
(BATCH/'source-hashes.json').write_text(json.dumps(expected,indent=2),encoding='utf-8')
cases=[('standard-yi',[]),('standard-yi-tiered-off',[])]
(BATCH/'planned-inputs.json').write_text(json.dumps(cases,indent=2),encoding='utf-8')
for name,extra in cases:
 changed=[rel for rel,digest in expected.items() if not (ROOT/rel).is_file() or hashlib.sha256((ROOT/rel).read_bytes()).hexdigest()!=digest]
 if changed or (TASK/'STOP-MATRIX07').exists():
  print(json.dumps({'stopped':True,'changed':changed}),flush=True);sys.exit(2)
 print(json.dumps({'starting':name,'time':time.time()}),flush=True)
 out=BATCH/name
 call=[sys.executable,str(ROOT/'language_core/tools/run_debug.py'),'--out',str(out),'--timeout','180','--raw-format','dag',*extra]
 env=os.environ.copy()
 if name.endswith('tiered-off'):
  env['DOTNET_TieredCompilation']='0'
 proc=subprocess.run(call,cwd=ROOT,env=env)
 result=json.loads((out/'result.json').read_text(encoding='utf-8')) if (out/'result.json').exists() else {}
 compact={key:result.get(key) for key in ['exit_code','timeout_stage','source_unchanged','run_config_matches','evidence_complete','world_horizon_observed','raw_committed_events']}
 print(json.dumps({'finished':name,'result':compact}),flush=True)
 if result.get('source_unchanged') is False or result.get('exit_code') not in (None,0) or not result:
  print('Stopped after engineering failure; preserve evidence.',flush=True);sys.exit(1)
print('Frozen07 bounded window finished; review completion and evidence explicitly.',flush=True)
