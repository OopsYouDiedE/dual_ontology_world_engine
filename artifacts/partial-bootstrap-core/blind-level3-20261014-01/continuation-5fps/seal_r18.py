import hashlib, json, pathlib, subprocess

root = pathlib.Path.cwd()
base = root / 'artifacts/partial-bootstrap-core/blind-level3-20261014-01/continuation-5fps'
out = base / 'source-r18'
out.mkdir(exist_ok=False)
def sha(data): return hashlib.sha256(data).hexdigest().upper()
def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with open('\\\\?\\' + str(path.resolve()), 'xb') as f: f.write(data)
records = []
for entry in json.loads((base/'source-r17/files.json').read_text(encoding='utf-8-sig')):
    p = pathlib.Path(entry['Path'])
    data = p.read_bytes()
    write(out/'files'/p.relative_to(root), data)
    records.append({'Path': str(p), 'Hash': sha(data)})
manifest = json.loads((root/'language_core/Session/obj/dv/source-manifest.json').read_text(encoding='utf-8-sig'))
modules=[]
for m in manifest['modules']:
    p = root/'language_core'/m['path']
    actual = hashlib.sha256((p.read_text(encoding='utf-8-sig').replace('\r\n','\n')+'\n').encode()).hexdigest()
    assert actual == m['sha256'], m['path']
    modules.append({'path':m['path'],'expected':m['sha256'],'actual':actual,'match':True})
library=root/'language_core/Session/bin/Debug/net8.0/DuelView.Session.dll'
loaded=root/'godot_csharp/.godot/mono/temp/bin/Debug/DuelView.Session.dll'
assert sha(library.read_bytes()) == sha(loaded.read_bytes())
draft=root/'language_core/verification/scenarios/chapter1-recall.dv'
draft_sha=sha(draft.read_bytes())
assert draft_sha == '2C47AFB0546D72249ED49C55158E8236AE6C7EE7D5A622F4F882076E42E5EB78'
assert not any(m['path'].endswith('chapter1-recall.dv') for m in manifest['modules'])
provenance={'scope':'R18 distinct reviewed adoption builder, subjective evidence refresh, streaming closure and passive render diagnostic; provenance only','manifest_modules':modules,'session_library_sha256':sha(library.read_bytes()),'host_loaded_session_library_sha256':sha(loaded.read_bytes()),'excluded_draft_sha256':draft_sha,'excluded_draft_imported':False}
for name,value in [('files.json',records),('source-manifest.json',manifest),('core-provenance.json',provenance)]:
    write(out/name,(json.dumps(value,ensure_ascii=False,indent=2)+'\n').encode())
write(out/'.gitattributes',b'*.json -text\nfiles/** -text\n*.diff -text\n')
paths=['AGENTS.md','godot_csharp/AGENTS.md','godot_csharp/scenes/Host/GameEngine.cs','godot_csharp/tools/dv_level3_capture.gd','language_core/AGENTS.md','language_core/Session/Session.cs','language_core/core/10-laws.dv','language_core/verification/AGENTS.md','language_core/verification/DESIGN.md','language_core/verification/scenarios/chapter1-departure.dv','language_core/verification/scenarios/chapter2-content.dv','language_core/verification/scenarios/chapter2-dialogue.dv','language_core/verification/session/15-action-planning.dv']
diff=subprocess.check_output(['git','diff','--',*paths])
write(base/'protected-change-r18-final.diff',diff)
print(json.dumps({'snapshot':str(out),'session_sha256':provenance['session_library_sha256'],'scope':'source/runtime sealed; no gameplay conclusion'}))
