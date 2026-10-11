import json, sys
from collections import Counter, defaultdict
from pathlib import Path

path = Path(sys.argv[1])
events = [json.loads(s) for s in path.read_text(encoding='utf-8').splitlines() if s]
ending = next((e for e in events if e['kind'] == 'shown' and e['value'].get('kind') == 'ending'), None)
limit = ending['elapsed_ms'] if ending else float('inf')
offers, decisions, actions = {}, {}, []
forms = []
room_entries = []
last_room = None
for e in events:
    if e['elapsed_ms'] > limit: break
    v, k = e['value'], e['kind']
    if k == 'observation': offers = {c['Token']: c for c in v.get('Candidates', [])}
    if k == 'decision':
        assert v['Token'] in offers
        decisions[v['Token']] = {'seconds': e['elapsed_ms']/1000, 'candidate': offers[v['Token']], 'decision': v}
    if k == 'act': actions.append(decisions[v['token']])
    if k == 'shown' and v.get('kind') == 'form':
        forms.append({'seconds': e['elapsed_ms']/1000, 'question': v.get('header', {}).get('question', ''), 'fields': v.get('fields', [])})
    if k == 'decision_frame' and v.get('entered') and v['entered'] != last_room:
        last_room = v['entered']
        room_entries.append({'seconds': e['elapsed_ms']/1000, 'room':last_room})
def stats(records):
    counts = Counter((r['candidate']['Kind'], r['candidate']['Label']) for r in records)
    return {'actions': len(records), 'repeats': sum(n-1 for n in counts.values()), 'groups': [{'kind': k[0], 'label': k[1], 'actions': n, 'repeats': n-1} for k,n in counts.most_common()]}
receipts = {e['value']['token']:e['value'] for e in events if e['kind']=='interaction_feedback' and e['elapsed_ms']<=limit}
contacts = [r for r in actions if receipts.get(r['decision']['Token'],{}).get('reached') is True]
result = {'ending_seconds': ending['elapsed_ms']/1000 if ending else None, 'game_time': actions[-1]['decision'].get('GameTime') if actions else None,
          'total_actions':len(actions), 'relevant': stats([r for r in actions if r['candidate']['Kind'] not in ('continue','move','wait')]),
          'by_kind': {kind:stats([r for r in actions if r['candidate']['Kind']==kind]) for kind in sorted({r['candidate']['Kind'] for r in actions})},
          'successful_contacts':stats(contacts), 'forms':forms,
          'recorded_room_entries':room_entries, 'recorded_room_returns': len(room_entries)-len({r['room'] for r in room_entries}),
          'form_actions':[{'seconds':r['seconds'], 'label':r['candidate']['Label'], 'purpose':r['candidate']['Purpose'], 'score':r['decision']['Score']}
                          for r in actions if r['candidate']['Purpose'] in ('statement','evidence','tab','commit','cancel')]}
print(json.dumps(result, ensure_ascii=False, indent=2))
