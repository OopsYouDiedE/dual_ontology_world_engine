import json, hashlib, pathlib, datetime, subprocess
r=pathlib.Path.cwd(); out=r/'artifacts/partial-bootstrap-core/blind-level3-20261014-01'
m=json.loads((r/'language_core/Session/obj/dv/source-manifest.json').read_text(encoding='utf-8'))
checks=[]
for entry in m['modules']:
 p=r/'language_core'/entry['path']; source=p.read_text(encoding='utf-8-sig').replace('\r\n','\n')+'\n'
 actual=hashlib.sha256(source.encode()).hexdigest(); checks.append(dict(path=entry['path'],expected=entry['sha256'],actual=actual,match=actual==entry['sha256']))
files=['godot_csharp/DualOntologyWorldEngine.csproj','godot_csharp/project.godot','godot_csharp/scenes/Host/Main.gd','godot_csharp/scenes/Host/GameEngine.cs','language_core/Session/Session.cs','language_core/Session/DuelView.Session.csproj','language_core/Session/obj/dv/DVProgram.cs','language_core/Session/bin/Debug/net8.0/DuelView.Session.dll','godot_csharp/.godot/mono/temp/bin/Debug/DuelView.Session.dll','godot_csharp/.godot/mono/temp/bin/Debug/DualOntologyWorldEngine.dll','godot_csharp/.godot/mono/temp/bin/Debug/DualOntologyWorldEngine.deps.json']
data={'recorded_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'commit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'sources':checks,'files':[dict(path=p,sha256=hashlib.sha256((r/p).read_bytes()).hexdigest()) for p in files],'purpose':'Read-only wiring and build provenance; not a gameplay verdict or timing protection baseline.'}
(out/'core-preflight.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(out/'.gitattributes').write_text('*.log -text -diff\n*.jsonl -text -diff\n*.png -text\n',encoding='utf-8')
print('Manifest differences:',[v['path'] for v in checks if not v['match']])
