import os,sys,subprocess,json,shutil
from pathlib import Path
root=Path(__file__).resolve().parents[5]
sys.path.insert(0,str(root/'language_core/tools'))
from dv_toolchain import environment,PROJECT
h=Path(__file__).resolve().parent
build=h/'binding-program';build.mkdir(exist_ok=True)
env=environment()
with (h/'binding-build.txt').open('wb') as log:
 subprocess.run([sys.executable,str(root/'language_core/tools/build_session.py'),'--entry',str(h/'binding-check.dv'),'--out',str(build),'--main','dvg_debug_run'],env=env,stdout=log,stderr=subprocess.STDOUT,check=True)
 shutil.copyfile(root/'language_core/compiler/Runtime.cs',build/'Runtime.cs')
 (build/'Program.csproj').write_text(PROJECT)
 subprocess.run(['dotnet','build',str(build/'Program.csproj'),'-c','Release','--disable-build-servers','-p:NuGetAudit=false'],env=env,stdout=log,stderr=subprocess.STDOUT,check=True)
results=[]
for arg in ['normal','duplicate','validate']:
 result=subprocess.run(['dotnet',str(build/'bin/Release/net8.0/Program.dll'),arg],env=env,capture_output=True)
 (h/('binding-'+arg+'.txt')).write_bytes(result.stdout+result.stderr)
 results.append({'case':arg,'exit_code':result.returncode})
 if arg=='normal' and result.returncode!=0:raise RuntimeError('neutral lookup failed')
 if arg!='normal' and (result.returncode==0 or b'Ambiguous' not in result.stderr):raise RuntimeError('duplicate guard missing')
(h/'binding-result.json').write_text(json.dumps(results,indent=2))
