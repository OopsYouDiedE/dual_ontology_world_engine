from pathlib import Path
from io import StringIO
import sys,json,hashlib
repo=Path(__file__).resolve().parents[4]
sys.path.insert(0,str(repo/'mcp/verification'))
from raw_dag import RawDagIndex
folder=Path(__file__).resolve().parent

def expand(index,selected):
    stream=StringIO();index.write_value(selected,stream,'dv-tagged')
    return json.loads(stream.getvalue())

def read(name):
    with RawDagIndex(folder/name) as index:
        s=index.summary
        assert s['complete_sealing'] and not s['invalid_tail']
        assert s['residual_tail_bytes']==0 and s['uncommitted_suffix_bytes']==0
        checkpoints=[(name,expand(index,{'node':root})) for name,root in index.db.execute('SELECT name,root FROM checkpoints ORDER BY seq')]
        events=[(id,ordinal,expand(index,{'node':root})) for id,root,ordinal in index.db.execute('SELECT id,root,ordinal FROM events ORDER BY ordinal')]
        return checkpoints,events,s

a,b=read('0.dvdag'),read('1.dvdag')
assert a[:2]==b[:2]
assert [item[0] for item in a[1]]==['e1','e2']
for mode in ['0','1']:
    value=read(mode+'.dvsave')
    roundtrip=read(mode+'-roundtrip.dvsave')
    assert value[:2]==roundtrip[:2]
    assert value[0][-1][1]==a[0][-1][1]
text=json.dumps(a[:2],ensure_ascii=False,separators=(',',':'))
assert '1/3' in text and '2/7' in text and '37/41' in text
print('All six binary DAG files: full checksum chains and final seals valid; no residual or uncommitted tail.')
print('Both journal checkpoints, event order and complete tagged values identical; both C# value-save/restored-owner roundtrips identical.')
print('Monotonic envelope timestamps intentionally excluded; every event value, field, item, order and rational tag compared.')
print('decoded-checkpoints-events-sha256='+hashlib.sha256(text.encode()).hexdigest())
for name in ['0.dvdag','1.dvdag','0.dvsave','1.dvsave','0-roundtrip.dvsave','1-roundtrip.dvsave']:
    print(name+': '+str(read(name)[2]['source_bytes'])+' bytes; complete_sealing=true')

