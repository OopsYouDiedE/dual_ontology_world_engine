import pathlib,json,hashlib
r=pathlib.Path.cwd(); d=r/'artifacts/partial-bootstrap-core/blind-level3-20261014-01'
before=json.loads((d/'core-preflight.json').read_text(encoding='utf-8'))
checks=[]
for x in before['files']:
 actual=hashlib.sha256((r/x['path']).read_bytes()).hexdigest(); checks.append(dict(path=x['path'],before=x['sha256'],after=actual,unchanged=actual==x['sha256']))
for x in before['sources']:
 p=r/'language_core'/x['path']; actual=hashlib.sha256((p.read_text(encoding='utf-8-sig').replace('\r\n','\n')+'\n').encode()).hexdigest(); checks.append(dict(path='language_core/'+x['path'],before=x['actual'],after=actual,unchanged=actual==x['actual']))
(d/'core-postflight.json').write_text(json.dumps({'checks':checks,'capture_final_sha256':hashlib.sha256((r/'godot_csharp/tools/dv_level3_capture.gd').read_bytes()).hexdigest()},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
index=[]
for p in sorted(d.rglob('*')):
 if p.is_file() and p.name!='evidence-index.json':
  with p.open('rb') as f: h=hashlib.file_digest(f,'sha256').hexdigest()
  index.append(dict(path=p.relative_to(d).as_posix(),bytes=p.stat().st_size,sha256=h))
(d/'evidence-index.json').write_text(json.dumps(index,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Changed protected gameplay/build files:',[x['path'] for x in checks if not x['unchanged']])
