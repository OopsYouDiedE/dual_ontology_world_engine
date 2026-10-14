import pathlib,json,gzip,hashlib,shutil,gc
r=pathlib.Path(__file__).parent
summaries={}
def record(x):
 return dict(x) if isinstance(x,list) and all(isinstance(i,list) and len(i)==2 and isinstance(i[0],str) for i in x) else x
for group in ('detective-x4','blind-x4','detective-x4-readable'):
 d=r/group/'raw'
 for name in ('session.json','save.json'):
  src=d/name; dst=d/(name+'.gz')
  if not dst.exists():
   with src.open('rb') as a,gzip.GzipFile(filename=str(dst),mode='wb',mtime=0) as b: shutil.copyfileobj(a,b)
  with gzip.open(dst,'rb') as f: restored=hashlib.file_digest(f,'sha256').hexdigest()
  with src.open('rb') as f: original=hashlib.file_digest(f,'sha256').hexdigest()
  if restored!=original: raise RuntimeError('Archive changed bytes: '+str(src))
 root=record(json.loads((d/'session.json').read_bytes())); events=[record(e) for e in root['events']]
 selected=[]
 for e in events:
  if e.get('type')=='action_execution' or (e.get('actor')=='detective' and e.get('type') in ('action_selected','planning_stop')) or e.get('type')=='session_stopped':
   data=record(e['data']); action=record(data.get('action',[]))
   selected.append({'id':e['id'],'type':e['type'],'actor':e.get('actor'),'game_time':e['time'],'action':action.get('id') if isinstance(action,dict) else None,'status':data.get('status'),'outcome':data.get('outcome')})
 summaries[group]={'status':root['status'],'time':root['time'],'event_types':sorted(set(e['type'] for e in events)),'selected_public_execution_events':selected,'trace_sha256':hashlib.sha256((d/'session.json').read_bytes()).hexdigest()}
 del root,events; gc.collect()
(r/'session-summaries.json').write_text(json.dumps(summaries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
review=r/'review'; review.mkdir(exist_ok=True)
for group,names in {'detective-x4-readable':['frame-000040317.png','frame-000089652.png'],'blind-x4':['frame-000038228.png','frame-000106378.png','frame-000217808.png','frame-000239923.png']}.items():
 for name in names:
  shutil.copyfile(r/group/name,review/(group+'-'+name))
files=[]
for p in sorted(r.rglob('*')):
 if p.is_file() and p.name!='evidence-index.json':
  with p.open('rb') as f: h=hashlib.file_digest(f,'sha256').hexdigest()
  files.append({'path':p.relative_to(r).as_posix(),'bytes':p.stat().st_size,'sha256':h})
(r/'evidence-index.json').write_text(json.dumps(files,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Raw archives preserve original bytes. Evidence index saved; no gameplay verdict inferred.')
