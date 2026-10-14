"""Private final-state diagnostic; never supplied to a blind player."""
import json, pathlib, sys
from stream_events import rec
source = pathlib.Path(__file__).with_name('inspect_tail.py').read_text(encoding='utf-8')
exec(source[:source.index('run = pathlib.Path')])
run = pathlib.Path(sys.argv[1])
with (run/'raw/session.json').open(encoding='utf-8') as stream:
    reader=Reader(stream)
    reader.char('['); reader.char('[')
    assert reader.value() == 'events'
    reader.char(','); reader.char('[')
    while reader.peek() != ']':
        reader.value()
        if reader.peek() == ',': reader.char(',')
    reader.char(']'); reader.char(']')
    final=None
    while reader.peek() == ',':
        reader.char(','); pair=reader.value()
        if pair[0]=='final': final=rec(pair[1])
    reader.char(']')
    assert not (reader.buf+stream.read()).strip(), 'Trailing bytes'
actor=next(rec(a) for a in final['actors'] if rec(a)['id']=='demon')
mind=rec(actor['mind'])
def state(world, entity):
    return rec(rec(rec(rec(world['objects'])[entity])['facets'])['state'])
def cells(world):
    return {k:rec(v) for k,v in state(world,'question').items() if k.startswith('solved_')}
latest=mind['time']
for item in mind.get('history',[]):
    entry=rec(item)
    if entry.get('kind')=='observation-batch':
        for item in entry['observations']: latest=max(latest, rec(item)['time'])
result={'scope':'R18 own final raw, private diagnostic; complete JSON parsed to EOF, save/closure completeness separate','owner_time':mind['time'],'latest_received_time':latest,'owner_question':cells(mind),'physical_question':cells(rec(final['physical'])),'departure_goals':[rec(g) for g in rec(actor['policy']).get('departure_goals',[])]}
with (run/'judge-state.json').open('x',encoding='utf-8') as out: json.dump(result,out,ensure_ascii=False,indent=2)
print('Wrote own-run private judge state; trace JSON parsed to EOF')
