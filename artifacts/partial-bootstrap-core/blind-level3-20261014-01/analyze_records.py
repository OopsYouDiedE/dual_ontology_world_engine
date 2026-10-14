import pathlib,json,gzip,hashlib,collections
r=pathlib.Path(__file__).parent
p=r/'detective-x4/raw/session.json'
def record(x): return dict(x) if isinstance(x,list) and all(isinstance(i,list) and len(i)==2 and isinstance(i[0],str) for i in x) else x
s=record(json.loads(p.read_bytes())); print('Root:',list(s))
events=[record(e) for e in s['events']]
print('Last:',[{k:e.get(k) for k in ('id','type','actor','time')} for e in events[-5:]])
selected=[]
for e in events:
 if e.get('actor')=='detective' and e.get('type') in ('action_selected','execution_receipt','action_completed','planning_stop'):
  d=record(e['data']); a=record(d.get('action',[])); selected.append(dict(id=e['id'],type=e['type'],time=e['time'],action=a.get('id') if isinstance(a,dict) else None,action_kind=a.get('kind') if isinstance(a,dict) else None,reason=d.get('reason'),status=d.get('status')))
print('Detective:',json.dumps(selected,ensure_ascii=False))
print('Event types:',sorted(set(e['type'] for e in events)))
(r/'detective-x4/event-summary.json').write_text(json.dumps(dict(detective=selected,last=[{k:e.get(k) for k in ('id','type','actor','time')} for e in events[-5:]],events_with_progress=[{k:e.get(k) for k in ('id','type','actor','time')} for e in events if any(word in e['type'] for word in ('chapter','accept','milestone'))]),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for name in ('session.json','save.json'):
 data=(p.parent/name).read_bytes()
 with gzip.GzipFile(filename=str(p.parent/(name+'.gz')),mode='wb',mtime=0) as f: f.write(data)
 print(name,len(data),hashlib.sha256(data).hexdigest())
