"""Strip private reasoning and progress from already recorded real sessions."""
import argparse,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('run');args=p.parse_args()
run=Path(args.run).resolve()
records=[]
for line in (run/'raw/trajectory.jsonl').read_bytes().splitlines():
    r=json.loads(line)
    if r['kind'] in ('shown','act','keyboard_move','interaction_feedback','decision_frame','screenshot'):
        records.append(r)
    elif r['kind']=='observation':
        v=r['value'];records.append({**r,'value':{k:v[k] for k in ('Text','Lines','Candidates','Position','Field','Complete') if k in v}})
    elif r['kind']=='stop':
        records.append({**r,'value':{k:v for k,v in r['value'].items() if k!='last_reasoning'}})
target=run/'public-events.jsonl'
if target.exists():raise RuntimeError('Do not overwrite public review snapshot')
target.write_text('\n'.join(json.dumps(r,ensure_ascii=False) for r in records)+'\n',encoding='utf-8')
print(target)
