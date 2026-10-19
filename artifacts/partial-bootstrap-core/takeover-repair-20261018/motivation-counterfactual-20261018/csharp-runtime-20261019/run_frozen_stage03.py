from pathlib import Path
import subprocess,sys,hashlib,json,shutil,os,time
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
frozen=HERE/'frozen-03';frozen.mkdir(exist_ok=False)
source=frozen/'source';source.mkdir()
shutil.copytree(HERE/'frozen/source/language_core',source/'language_core',ignore=shutil.ignore_patterns('bin','obj','__pycache__','node_modules','.vs'))
for name in ['compiler/Runtime.cs','AGENTS.md','compiler/README.md','core/README.md']:
 shutil.copyfile(ROOT/'language_core'/name,source/'language_core'/name)
hashes={p.relative_to(source).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(source.rglob('*')) if p.is_file()}
(frozen/'source-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
(frozen/'origin.json').write_text(json.dumps({'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'scope':'stage03 C# candidate; exact same DV content as first frozen run; fresh formal-model; no geometry; no preset decisions','tracked_dirty':subprocess.check_output(['git','diff','--name-only'],cwd=ROOT,text=True).splitlines()},indent=2),encoding='utf-8')
command=[sys.executable,str(source/'language_core/tools/run_debug.py'),'--out',str(frozen/'standard-yi'),'--timeout','240','--raw-format','dag','--projection-every','32']
(frozen/'command.json').write_text(json.dumps(command,indent=2))
print('Stage03 frozen source ready; same DV content, updated C# runtime; starting build and one 240-second formal session.',flush=True)
start=time.monotonic()
with (frozen/'driver.stdout.txt').open('xb') as output,(frozen/'driver.stderr.txt').open('xb') as errors:
 process=subprocess.run(command,cwd=source,env=os.environ.copy(),stdout=output,stderr=errors)
changes=[name for name,digest in hashes.items() if not (source/name).is_file() or hashlib.sha256((source/name).read_bytes()).hexdigest()!=digest]
(frozen/'freeze-result.json').write_text(json.dumps({'exit_code':process.returncode,'driver_wall_seconds':time.monotonic()-start,'changed_frozen_sources':changes,'workspace_changes_since_freeze':[name for name,digest in hashes.items() if (ROOT/name).is_file() and hashlib.sha256((ROOT/name).read_bytes()).hexdigest()!=digest]},indent=2))
print((frozen/'freeze-result.json').read_text(),flush=True)
if (frozen/'standard-yi/result.json').exists():print((frozen/'standard-yi/result.json').read_text(),flush=True)
raise SystemExit(process.returncode)
