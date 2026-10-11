import hashlib, json, subprocess, shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
C3 = ROOT / 'artifacts/partial-bootstrap-core/compiler-r1/c3'
COMP = ROOT / 'language_core/compiler'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
tracked = [p for p in COMP.rglob('*') if p.is_file() and not {'bin','obj'}.intersection(p.parts)] + [C3/'Program.cs', C3/'Runtime.cs', C3/'bin/Release/net8.0/Program.dll']
before = {str(p.relative_to(ROOT)):sha(p) for p in tracked}
(OUT/'sources-before.json').write_text(json.dumps(before,indent=2))
records=[]
def run(label,args):
    p=subprocess.run(list(map(str,args)),cwd=ROOT,capture_output=True,timeout=120)
    (OUT/(label+'.stdout.txt')).write_bytes(p.stdout)
    (OUT/(label+'.stderr.txt')).write_bytes(p.stderr)
    records.append({'label':label,'command':list(map(str,args)),'exit':p.returncode})
    (OUT/'commands.json').write_text(json.dumps(records,indent=2))
    return p.returncode
cases={
'source_a':'(main (rt_print (rt_add 17 25)))',
'source_b':'(main (rt_print (rt_add 17 26)))',
'strings':r'(main (rt_print "quote:\" slash:\\ line:\n tab:\t unicode:\u4e2d"))',
'unicode_line_separator':'(main (rt_print "alpha\u2028beta"))',
'scope_siblings':'(main (do (let (x 7) (rt_print x)) (let (x 9) (rt_print x))))',
'scope_sequential':'(main (let (x 7 y (rt_add x 2)) (rt_print y)))',
'scope_leak':'(main (do (let (x 7) x) (rt_print x)))',
'scope_shadow':'(main (let (x 7) (let (x 8) (rt_print x))))',
'scope_duplicate':'(main (let (x 7 x 8) x))',
'forward_recursion':'(defn odd (n) (if (rt_eq n 0) false (even (rt_sub n 1)))) (defn even (n) (if (rt_eq n 0) true (odd (rt_sub n 1)))) (main (rt_print (odd 9)))',
'bad_arity':'(defn f (x) x) (main (f 1 2))',
'unknown_call':'(main (unknown 1))',
'bad_syntax':'(main (rt_print 1)',
'bad_escape':r'(main (rt_print "\q"))',
'bad_parameter':'(defn f (x x) x) (main (f 1 2))',
'bad_type':'(main (rt_print (rt_add "text" 2)))',
'bad_condition':'(main (if 1 (rt_print "yes") (rt_print "no")))',
'dead_bad_type':'(main (if false (rt_add "text" 2) (rt_print "ok")))',
'unsupported_lambda':'(main (lambda (x) x))',
}
for name,source in cases.items():
    folder=OUT/name; folder.mkdir(exist_ok=False)
    dv=folder/'input.dv'; dv.write_text(source,encoding='utf-8')
    code=folder/'Program.cs'
    if run(name+'-generate',['dotnet',C3/'bin/Release/net8.0/Program.dll',dv,code]): continue
    shutil.copyfile(C3/'Runtime.cs',folder/'Runtime.cs')
    shutil.copyfile(C3/'Program.csproj',folder/'Program.csproj')
    if run(name+'-build',['dotnet','build',folder/'Program.csproj','-c','Release','--disable-build-servers']):continue
    run(name+'-execute',['dotnet',folder/'bin/Release/net8.0/Program.dll'])
after={str(p.relative_to(ROOT)):sha(p) for p in tracked}
(OUT/'sources-after.json').write_text(json.dumps(after,indent=2))
(OUT/'integrity.json').write_text(json.dumps({'sources_unchanged':before==after,'c2_c3_source_equal':(C3/'Program.cs').read_bytes()==(C3.parent/'c2/Program.cs').read_bytes()},indent=2))
print(json.dumps(records,indent=2))
