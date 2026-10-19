from pathlib import Path
import subprocess,sys,os,json,hashlib
BASE=Path(__file__).resolve().parent
ROOT=BASE.parents[4]
HERE=BASE/"engineering-04"
HERE.mkdir(exist_ok=False)
env=os.environ.copy();env['DOTNET_ROLL_FORWARD']='Major'
project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>'
for name,source in [('before',BASE/'Runtime.stage01.cs.txt'),('after',ROOT/'language_core/compiler/Runtime.cs')]:
 p=HERE/name;p.mkdir(exist_ok=False)
 (p/'Runtime.cs').write_bytes(source.read_bytes());(p/'Program.cs').write_bytes((BASE/'Probe.stage02.cs.txt').read_bytes());(p/'Probe.csproj').write_text(project)
 with (p/'build.log').open('wb') as f:subprocess.run(['dotnet','build',str(p/'Probe.csproj'),'-c','Release','--disable-build-servers','-p:NuGetAudit=false'],stdout=f,stderr=subprocess.STDOUT,check=True,env=env,timeout=120)
 with (p/'stdout.txt').open('wb') as stdout,(p/'stderr.txt').open('wb') as stderr:subprocess.run(['dotnet',str(p/'bin/Release/net8.0/Probe.dll'),str(p)],stdout=stdout,stderr=stderr,check=True,env=env,timeout=120)
comparison={}
for name in ['semantics.json','neutral.dag.jsonl','failure.dag.jsonl.partial']:
 a=(HERE/'before'/name).read_bytes();b=(HERE/'after'/name).read_bytes();comparison[name]={'byte_identical':a==b,'before_sha256':hashlib.sha256(a).hexdigest(),'after_sha256':hashlib.sha256(b).hexdigest()}
comparison['metrics']={name:json.loads((HERE/name/'metrics.json').read_text()) for name in ['before','after']}
comparison['json_integrity']={name:json.loads((HERE/name/'json-integrity.json').read_text()) for name in ['before','after']}
(HERE/'comparison.json').write_text(json.dumps(comparison,indent=2))
print(json.dumps(comparison,indent=2))
if not all(comparison[n]['byte_identical'] for n in ['semantics.json','neutral.dag.jsonl','failure.dag.jsonl.partial']):raise SystemExit('Unexpected semantic or DAG difference')
if not comparison['json_integrity']['after']['complete']:raise SystemExit('Candidate omitted JSON data')
