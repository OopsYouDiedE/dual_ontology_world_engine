import xml.etree.ElementTree as E,json,csv,collections,zipfile
from pathlib import Path
p=Path('artifacts/theme-film')
with zipfile.ZipFile('music/The Unfallen Duet.mscz') as z:
    root=E.fromstring(z.read('The Unfallen Duet.mscx')).find('Score')
names=['C','C#','D','Eb','E','F','F#','G','Ab','A','Bb','B']
def nn(x):return names[x%12]+str(x//12-1)
rows=[]; details=[]
for s in root.findall('Staff'):
 for i,m in enumerate(s.findall('Measure'),1):
  events=[]
  for v in m.findall('voice'):
   for el in v:
    if el.tag in ('Chord','Rest'):
     events.append({'kind':el.tag,'dur':el.findtext('durationType'),'dots':el.findtext('dots','0'),'notes':[int(n.findtext('pitch')) for n in el.findall('Note')]})
  texts=[(e.tag,''.join(e.itertext()).strip()) for e in m.iter() if e.tag in ('StaffText','RehearsalMark','Dynamic','Tempo','KeySig')]
  details.append({'staff':s.get('id'),'measure':i,'events':events,'markings':texts})
  if s.get('id')=='1': rows.append({'measure':i,'start_seconds':round((i-1)*4/3,6),'end_seconds':round(i*4/3,6),'start_with_5s_intro':round(5+(i-1)*4/3,6),'markings':str(texts)})
(p/'score-events.json').write_text(json.dumps(details,ensure_ascii=False,indent=2),encoding='utf-8')
with (p/'measure-timing.csv').open('w',newline='',encoding='utf-8-sig') as f:
 w=csv.DictWriter(f,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)
for d in details:
 if d['staff']=='1' and d['markings']:print(d['measure'],d['markings'])
for lo,hi in [(1,16),(29,36),(45,52),(61,68),(77,88),(103,110)]:
 for i in range(lo,hi+1):
  print('M',i, ' | '.join('S'+d['staff']+':'+ ' '.join('/'.join(nn(x) for x in e['notes'])+':'+e['dur']+('.' if e['dots']!='0' else '') for e in d['events']) for d in details if d['measure']==i))
for s in ['1','2','3']:
 notes=[n for d in details if d['staff']==s for e in d['events'] for n in e['notes']]
 print('range',s,nn(min(notes)),nn(max(notes)),len(notes))
