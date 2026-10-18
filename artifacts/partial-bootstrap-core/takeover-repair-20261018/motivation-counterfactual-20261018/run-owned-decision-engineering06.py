from pathlib import Path
import subprocess,sys,json,shutil,hashlib
ROOT=Path(__file__).resolve().parents[4]; TASK=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'language_core/tools'))
from dv_toolchain import PROJECT,environment
from dv_sources import sources
out=TASK/'owned-decision-engineering-06';out.mkdir(exist_ok=False)
entry=TASK/'owned-decision-predecessor-probe.dv'
hashes={str(q):hashlib.sha256(q.read_bytes()).hexdigest() for q,_ in sources([entry])}
(out/'source-hashes.json').write_text(json.dumps(hashes,indent=2))
def run(command,label):
 with (out/(label+'.stdout')).open('wb') as so,(out/(label+'.stderr')).open('wb') as se:
  return subprocess.run([str(x) for x in command],cwd=ROOT,env=environment(),stdout=so,stderr=se,timeout=180).returncode
program=out/'program'
assert run([sys.executable,ROOT/'language_core/tools/build_session.py','--entry',entry,'--out',program,'--main','dvg_debug_run'],'generation')==0
shutil.copyfile(ROOT/'language_core/compiler/Runtime.cs',program/'Runtime.cs')
(program/'Program.csproj').write_text(PROJECT)
assert run(['dotnet','build',program/'Program.csproj','-c','Release','--disable-build-servers','-p:NuGetAudit=false'],'build')==0
exe=program/'bin/Release/net8.0/Program.dll'
legacy=run(['dotnet',exe,'legacy-first'],'legacy')
current=run(['dotnet',exe],'current')
records=[dict(json.loads(line)) for line in (out/'current.stdout').read_text(encoding='utf-8-sig').splitlines()]
result={'scope':'neutral-helper-only; not a formal session','legacy_exit':legacy,'legacy_expected_list': 'Expected List' in (out/'legacy.stderr').read_text(),'current_exit':current,'comparisons':[r for r in records if 'same' in r],'completion_marker': records[-1].get('formal_session') if records else None,'source_unchanged':all(hashlib.sha256(Path(q).read_bytes()).hexdigest()==h for q,h in hashes.items())}
(out/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
if legacy==0 or not result['legacy_expected_list'] or current or any(r['same'] is not True for r in result['comparisons']) or result['completion_marker']!='not-started' or not result['source_unchanged']:raise SystemExit(1)
print(out/'result.json')
