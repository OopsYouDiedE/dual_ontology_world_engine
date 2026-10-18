import hashlib,json,os,shutil,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'language_core/tools'))
from dv_sources import sources
out=ROOT/'artifacts/rejection-probes-01'
out.mkdir(exist_ok=False)
probe=Path(__file__).with_name('rejection-probe.dv')
chunks=[];ranges=[];manifest=[];offset=0
for path,text in [*sources([ROOT/'language_core/game/entry.dv']),(probe,probe.read_text(encoding='utf8'))]:
 length=len(text.encode('utf-16-le'))//2
 chunks.append(text);ranges.append([offset,length]);offset+=length
 manifest.append({'path':str(path),'sha256':hashlib.sha256(text.encode()).hexdigest()})
(out/'probe-bundle.dv').write_text(''.join(chunks),encoding='utf8',newline='\n')
(out/'ranges.json').write_text(json.dumps(ranges),encoding='utf8')
(out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
compiler=json.loads((ROOT/'artifacts/partial-bootstrap-core/parallel-repairs-20261018/syntax-probes-01/commands.json').read_text())[0]['argv'][1]
env=os.environ.copy();env['DOTNET_ROLL_FORWARD']='Major';env['DOTNET_CLI_UI_LANGUAGE']='en'
commands=[]
def run(label,args):
 p=subprocess.run(list(map(str,args)),cwd=ROOT,env=env,capture_output=True,timeout=180)
 (out/(label+'.stdout.txt')).write_bytes(p.stdout);(out/(label+'.stderr.txt')).write_bytes(p.stderr)
 commands.append({'label':label,'args':list(map(str,args)),'exit':p.returncode})
 (out/'commands.json').write_text(json.dumps(commands,indent=2),encoding='utf8')
 if p.returncode:raise RuntimeError(label+' failed; inspect preserved output')
 return p
run('generate',['dotnet',compiler,out/'probe-bundle.dv',out/'Program.cs','--emit',out/'ranges.json'])
shutil.copyfile(ROOT/'language_core/compiler/Runtime.cs',out/'Runtime.cs')
(out/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>')
run('build',['dotnet','build',out/'Probe.csproj','-c','Release','--disable-build-servers','-p:NuGetAudit=false'])
p=run('execute',['dotnet',out/'bin/Release/net8.0/Probe.dll'])
print(p.stdout.decode('utf8'))
