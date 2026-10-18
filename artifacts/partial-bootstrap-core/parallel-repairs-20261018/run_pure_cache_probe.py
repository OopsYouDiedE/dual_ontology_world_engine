import hashlib,json,os,re,shutil,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3];sys.path.insert(0,str(ROOT/'language_core/tools'))
from dv_sources import sources
base=Path(__file__).parent;out=base/'pure-cache-probe-04';out.mkdir(exist_ok=False)
env=os.environ.copy();env['DOTNET_ROLL_FORWARD']='Major';env['DOTNET_CLI_UI_LANGUAGE']='en';commands=[]
def run(label,args):
 p=subprocess.run(list(map(str,args)),cwd=ROOT,env=env,capture_output=True,timeout=180)
 (out/(label+'.stdout.txt')).write_bytes(p.stdout);(out/(label+'.stderr.txt')).write_bytes(p.stderr)
 commands.append({'label':label,'args':list(map(str,args)),'exit':p.returncode});(out/'commands.json').write_text(json.dumps(commands,indent=2),encoding='utf8');return p
def extract(text,name):
 m=re.search(r'^fn '+name+r'\(',text,re.M);end=re.search(r'^(?:fn |axiom |\(main)',text[m.end():],re.M)
 return text[m.start():m.end()+end.start()] if end else text[m.start():]
core=subprocess.check_output(['git','show','HEAD:language_core/core/00-data.dv'],cwd=ROOT).decode('utf8')
plan=subprocess.check_output(['git','show','HEAD:language_core/verification/session/15-action-planning.dv'],cwd=ROOT).decode('utf8')
old=extract(core,'dv_read').replace('fn dv_read(', 'fn probe_old_read(',1)+extract(plan,'dvp_topic').replace('fn dvp_topic(','fn probe_old_topic(',1)+extract(core,'dv_truth').replace('dv_truth','probe_old_truth')+extract(core,'dv_truth_uncached').replace('dv_truth','probe_old_truth')
probe=base/'pure-cache-probe.dv';extra=out/'original-and-probe.dv';extra.write_text(old+probe.read_text(encoding='utf8'),encoding='utf8')
chunks=[];ranges=[];manifest=[];offset=0
for path,text in [*sources([ROOT/'language_core/game/entry.dv']),(extra,extra.read_text(encoding='utf8'))]:
 length=len(text.encode('utf-16-le'))//2;chunks.append(text);ranges.append([offset,length]);offset+=length
 manifest.append({'path':str(path),'sha256':hashlib.sha256(text.encode()).hexdigest()})
(out/'bundle.dv').write_text(''.join(chunks),encoding='utf8',newline='\n');(out/'ranges.json').write_text(json.dumps(ranges),encoding='utf8');(out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
compiler=json.loads((base/'syntax-probes-01/commands.json').read_text())[0]['argv'][1]
p=run('generate',['dotnet',compiler,out/'bundle.dv',out/'Program.cs','--emit',out/'ranges.json'])
if p.returncode:raise RuntimeError('generate')
shutil.copyfile(ROOT/'language_core/compiler/Runtime.cs',out/'Runtime.cs')
(out/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>')
p=run('build',['dotnet','build',out/'Probe.csproj','-c','Release','--disable-build-servers','-p:NuGetAudit=false'])
if p.returncode:raise RuntimeError('build')
dll=out/'bin/Release/net8.0/Probe.dll';p=run('normal',['dotnet',dll,'normal']);results={'normal':{'exit':p.returncode,'output':p.stdout.decode('utf8')}}
expected=[True,True,False,False,False,True,True,False,True,False,True,False,True,False,True,'unconventional','true','false','read-copy-and-invalidation','list-constant-return-contract']
results['normal']['expected']=expected;results['normal']['equal']=p.returncode==0 and json.loads(p.stdout)==expected
for mode in ['nonlist-owner','missing-op','missing-value','late-all-error','invalid-binding','question-before-policy','read-cross-subject','read-leading-order','read-invalid-objects','read-missing-field']:
 pair=[]
 for version in ['old','new']:
  p=run(mode+'-'+version,['dotnet',dll,mode,version]);line=p.stderr.decode('utf8','replace').splitlines()[0] if p.stderr else '';pair.append({'version':version,'exit':p.returncode,'first_error':line})
 results[mode]={'runs':pair,'equal':pair[0]['exit']!=0 and pair[1]['exit']!=0 and pair[0]['first_error']==pair[1]['first_error']}
(out/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(results,ensure_ascii=False,indent=2))
raise SystemExit(0 if all(v['equal'] for v in results.values()) else 1)

