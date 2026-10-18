import argparse,hashlib,json,os,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
parser=argparse.ArgumentParser();parser.add_argument('--compiler',required=True);parser.add_argument('--out',required=True);args=parser.parse_args()
out=ROOT/args.out;out.mkdir(parents=True,exist_ok=False)
env=os.environ.copy();env.setdefault('DOTNET_ROLL_FORWARD','Major');env['DOTNET_CLI_UI_LANGUAGE']='en'
records=[]
def run(label,argv):
 p=subprocess.run(list(map(str,argv)),cwd=ROOT,env=env,capture_output=True,timeout=180)
 (out/(label+'.stdout.txt')).write_bytes(p.stdout);(out/(label+'.stderr.txt')).write_bytes(p.stderr)
 records.append({'label':label,'argv':list(map(str,argv)),'exit':p.returncode});(out/'commands.json').write_text(json.dumps(records,indent=2),encoding='utf8')
 return p
samples=ROOT/'language_core/compiler/examples/syntax-probes'
checks={
 'comprehension':[1,10,[12,13],[[2,4],[4,6]],[11,12],[],[],'negative','zero','one','many'],
 'branch-boundaries':['nested','outer','independent','fallback',2],
 'unbound-binding':'unbound local','bad-projection-type':'numeric literal has incompatible type',
 'immutable-rebinding':'assignment requires var','malformed-elif':'expected :',
 'orphan-elif':'unexpected token after statement','while-elif':'unexpected token after statement','intervening-elif':'unexpected token after statement',
 'bad-source-runtime':'Expected List','bad-guard-runtime':'Expected Bool'}
results=[]
for name,expected in checks.items():
 work=out/name;work.mkdir();source=samples/(name+'.dv')
 p=run(name+'-generate',['dotnet',ROOT/args.compiler,source,work/'Program.cs'])
 text=(p.stdout+p.stderr).decode('utf8','replace')
 diagnostic=name not in ('comprehension','branch-boundaries','bad-source-runtime','bad-guard-runtime')
 if diagnostic:
  results.append({'input':name,'phase':'compile','exit':p.returncode,'expected':expected,'observed':text,'matches':p.returncode!=0 and expected in text and str(source) in text});continue
 if p.returncode:
  results.append({'input':name,'phase':'generate','exit':p.returncode,'observed':text,'matches':False});continue
 shutil.copyfile(ROOT/'language_core/compiler/Runtime.cs',work/'Runtime.cs')
 (work/'Program.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>',encoding='utf8')
 p=run(name+'-build',['dotnet','build',work/'Program.csproj','-c','Release','--disable-build-servers','-p:NuGetAudit=false'])
 if p.returncode:
  results.append({'input':name,'phase':'build','exit':p.returncode,'matches':False});continue
 p=run(name+'-execute',['dotnet',work/'bin/Release/net8.0/Program.dll']);text=(p.stdout+p.stderr).decode('utf8','replace')
 try: matches=(p.returncode==0 and json.loads(p.stdout)==expected) if isinstance(expected,list) else p.returncode!=0 and expected in text
 except Exception:matches=False
 results.append({'input':name,'phase':'execute','exit':p.returncode,'expected':expected,'observed':text,'matches':matches})
(out/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
(out/'fingerprints.json').write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [ROOT/args.compiler,ROOT/'language_core/compiler/compiler.dv',ROOT/'language_core/compiler/Runtime.cs',*samples.glob('*.dv')]},indent=2),encoding='utf8')
for r in results:print(r['input'],r['phase'],r['exit'],r['matches'])
raise SystemExit(0 if all(r['matches'] for r in results) else 1)

