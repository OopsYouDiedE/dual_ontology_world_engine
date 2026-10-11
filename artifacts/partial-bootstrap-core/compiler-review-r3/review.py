import hashlib, json, subprocess, shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
SEALED=ROOT/'artifacts/partial-bootstrap-core/compiler-r3'
COMP=ROOT/'language_core/compiler'
C3=SEALED/'c3/bin/Release/net8.0/Program.dll'
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
inputs=[p for p in COMP.rglob('*') if p.is_file() and not {'bin','obj'}.intersection(p.parts)]
inputs += [ROOT/'language_core/tools/bootstrap_compiler.py']
inputs += [p for p in SEALED.rglob('*') if p.is_file()]
before={str(p.relative_to(ROOT)):digest(p) for p in inputs}
(OUT/'before.json').write_text(json.dumps(before,indent=2))
commands=[]
def run(label,args):
    r=subprocess.run([str(a) for a in args],cwd=ROOT,capture_output=True,timeout=180)
    (OUT/(label+'.stdout.txt')).write_bytes(r.stdout)
    (OUT/(label+'.stderr.txt')).write_bytes(r.stderr)
    commands.append(dict(label=label,args=[str(a) for a in args],exit=r.returncode))
    (OUT/'commands.json').write_text(json.dumps(commands,indent=2))
    return r
def build(folder):
    shutil.copyfile(SEALED/'c3/Runtime.cs',folder/'Runtime.cs')
    shutil.copyfile(SEALED/'c3/Program.csproj',folder/'Program.csproj')
    r=run(folder.name+'-build',['dotnet','build',folder/'Program.csproj','-c','Release','--disable-build-servers'])
    if r.returncode: raise RuntimeError(folder.name+' build failed')
    return folder/'bin/Release/net8.0/Program.dll'
facts={}
facts['fixed_point_bytes']=(SEALED/'c2/Program.cs').read_bytes()==(SEALED/'c3/Program.cs').read_bytes()
facts['c2_source_sha256']=digest(SEALED/'c2/Program.cs')
facts['c3_source_sha256']=digest(SEALED/'c3/Program.cs')
facts['deps']=json.loads(C3.with_suffix('.deps.json').read_text())
manifest=json.loads((SEALED/'sources.json').read_text())
facts['compiler_sources_match_seal']={str(p.relative_to(ROOT)):digest(p)==manifest.get(str(p.relative_to(ROOT))) for p in inputs if p.is_relative_to(COMP)}
run('toolchain',['dotnet','--version'])
unicode='left\u0085mid\u2028next\u2029right'
folder=OUT/'unicode';folder.mkdir(exist_ok=False)
source=OUT/'unicode.dv'
source.write_text('(defn identity (text) text)\n(main (do (rt_print (identity "'+unicode+'")) (rt_print "\\\\u2028") (rt_print "\\u0085\\u2028\\u2029")))',encoding='utf-8')
r=run('unicode-generate',['dotnet',C3,source,folder/'Program.cs'])
if r.returncode: raise RuntimeError('unicode generation failed')
exe=build(folder);r=run('unicode-execute',['dotnet',exe])
expected=(unicode+'\r\n\\u2028\r\n\u0085\u2028\u2029\r\n').encode('utf-8')
(OUT/'unicode.expected.bin').write_bytes(expected)
facts['unicode_exact_bytes']=r.stdout==expected and r.returncode==0
facts['unicode_actual_hex']=r.stdout.hex()
invalid={
'unbound':'(main absent)',
'scope_escape':'(main (do (let (local 3) local) local))',
'duplicate_function':'(defn f () 1) (defn f () 2) (main 0)',
'duplicate_parameter':'(defn f (x x) x) (main 0)',
'shadow_parameter':'(defn f (x) (let (x 2) x)) (main 0)',
'forward_local':'(main (let (x y y 3) x))',
'bad_identifier':'(defn bad-name () 1) (main 0)',
'user_arity':'(defn f (x y) x) (main (f 1))',
'native_arity':'(main (rt_add 1 2 3))',
'form_arity':'(main (if true 1))',
'unknown_function':'(main (missing 1))',
'reserved_prefix':'(defn rt_mine (x) x) (main 0)',
'reserved_existing':'(defn rt_add (x y) x) (main 0)',
'reserved_bare':'(defn rt_ () 1) (main 0)',
}
facts['diagnostics']={}
for name,text in invalid.items():
    src=OUT/(name+'.dv');src.write_text(text,encoding='utf-8');target=OUT/(name+'.cs')
    r=run(name,['dotnet',C3,src,target])
    facts['diagnostics'][name]={'exit':r.returncode,'target_exists':target.exists(),'stderr':r.stderr.decode('utf-8')}
mutated=OUT/'variant.dv'
original=(COMP/'compiler.dv').read_text(encoding='utf-8')
mutated.write_text(original.replace('class DVProgram','class IndependentReviewProgram'),encoding='utf-8')
folder=OUT/'variant';folder.mkdir(exist_ok=False)
r=run('variant-generate',['dotnet',C3,mutated,folder/'Program.cs'])
if r.returncode: raise RuntimeError('variant generation failed')
exe=build(folder)
r=run('variant-compile-unicode',['dotnet',exe,source,OUT/'variant-output.cs'])
facts['source_variant_changed_output']=r.returncode==0 and 'class IndependentReviewProgram' in (OUT/'variant-output.cs').read_text()
probe=OUT/'assembly-probe';probe.mkdir(exist_ok=False)
(probe/'Program.cs').write_text('using System; using System.Reflection; class Probe { static void Main(string[] args) { var a=Assembly.LoadFile(args[0]); foreach(var r in a.GetReferencedAssemblies()) Console.WriteLine("REF " + r.FullName); foreach(var t in a.GetTypes()) Console.WriteLine("TYPE " + t.FullName); } }')
exe=build(probe)
run('assembly-metadata',['dotnet',exe,C3])
after={str(p.relative_to(ROOT)):digest(p) for p in inputs}
(OUT/'after.json').write_text(json.dumps(after,indent=2))
facts['sources_and_sealed_dlls_unchanged']=before==after
(OUT/'result.json').write_text(json.dumps(facts,indent=2),encoding='utf-8')
print(json.dumps(facts,indent=2))
