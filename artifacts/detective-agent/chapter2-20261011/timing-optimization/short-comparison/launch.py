"""A short real session, with a separately committed source/runtime seal."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,sys

ROOT=Path(__file__).resolve().parents[5]
OUT=Path(__file__).resolve().parent
SEAL=(OUT/'source-seal.json').relative_to(ROOT).as_posix()
NAME='timing-short-current-r26'
RUN=OUT/'fresh-current'

def check():
    anchor=subprocess.check_output(['git','show','HEAD:'+SEAL],cwd=ROOT)
    manifest=json.loads(anchor)
    if manifest!=json.loads((ROOT/SEAL).read_bytes()): raise RuntimeError('Seal changed')
    changed=[p for p,h in manifest['files'].items() if not (ROOT/p).is_file() or hashlib.sha256((ROOT/p).read_bytes()).hexdigest()!=h]
    if changed: raise RuntimeError('Protected sources/runtime changed: '+str(changed))
    return hashlib.sha256(anchor).hexdigest()

if RUN.exists(): raise RuntimeError('Refuse to overwrite run')
seal_hash=check()
RUN.mkdir(parents=True)
(RUN/'guard-start.json').write_text(json.dumps({'seal_sha256':seal_hash}),encoding='utf-8')
cmd=[sys.executable,str(ROOT/'godot_csharp/tools/full_test.py'),'--name',NAME,'--seed','0',
     '--time-scale','4','--max-seconds','120','--no-progress-seconds','60','--shot-seconds','10','--line-seconds','0.6']
(RUN/'command.json').write_text(json.dumps(cmd,indent=2),encoding='utf-8')
with (RUN/'runner.log').open('wb') as log:
    result=subprocess.run(cmd,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
raw=ROOT/'tmp/full-test'/NAME
if raw.exists(): shutil.copytree(raw,RUN/'raw')
if raw.with_suffix('.log').exists(): shutil.copyfile(raw.with_suffix('.log'),RUN/'godot.log')
(RUN/'evidence-sha256.json').write_text(json.dumps({p.relative_to(RUN).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in RUN.rglob('*') if p.is_file()},indent=2),encoding='utf-8')
try:
    end_hash=check()
except Exception as error:
    (RUN/'INVALID.json').write_text(json.dumps({'reason':str(error)}),encoding='utf-8')
    raise
(RUN/'guard-end.json').write_text(json.dumps({'seal_sha256':end_hash,'sources_and_runtime_unchanged':True,'exit':result.returncode}),encoding='utf-8')
print(RUN)
