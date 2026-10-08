import json, subprocess, pathlib, time, sys
ROOT=pathlib.Path(r'C:\Users\iii\Desktop\dual_ontology_world_engine')
OUT=ROOT/'artifacts/basic-ui-system/level3-6_1-sol/level3-sol61-20261008-01'
CMD=[sys.executable,str(ROOT/'godot_csharp/tools/blind_playtest.py'),'--session=level3-sol61-20261008-01']

def call(args):
    p=subprocess.run(CMD+args,cwd=ROOT,capture_output=True,text=True,encoding='utf-8',timeout=25)
    if p.returncode or not p.stdout.strip(): raise RuntimeError(p.stderr or p.stdout or 'No response')
    j=json.loads(p.stdout)
    if not j.get('accepted') or j.get('error'): raise RuntimeError(j)
    return j

def dialogue(j):
    c=j.get('controls',[])
    return bool(c) and all(x['kind']=='Button' and x['text'] in ('继续  ▸','回看') for x in c) and any('Space 继续' in x['text'] for x in j.get('visible_text',[]))
def ready(j):
    return next((c for c in j.get('controls',[]) if c['text']=='继续  ▸' and not c['disabled']),None)
records=[]
try:
    j=call(['snapshot'])
    records.append(j)
    for n in range(12):
        if not dialogue(j) or not ready(j):
            records.append(j); break
        state=json.loads((OUT/'recording.json').read_text(encoding='utf-8-sig'))
        if state['state']!='recording' or not state.get('recording_ready'): raise RuntimeError('Recording is not active')
        (OUT/'batch-input.json').write_text(json.dumps({'type':'click_control','control_id':ready(j)['control_id']}),encoding='utf-8')
        j=call(['action','--file='+str(OUT/'batch-input.json'),'--name=continuous_dialogue'])
        deadline=time.monotonic()+12
        while dialogue(j) and not ready(j) and time.monotonic()<deadline:
            time.sleep(.7)
            j=call(['snapshot'])
        records.append(j)
        if not dialogue(j) or not ready(j): break
except Exception as e:
    print(json.dumps({'error':str(e)},ensure_ascii=False))
finally:
    dest=OUT/('batch-'+str(int(time.time()))+'.json')
    dest.write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'saved':str(dest),'pages':[{'seq':j.get('ack_seq'),'screenshot':j.get('screenshot'),'text':j.get('visible_text'),'controls':j.get('controls')} for j in records]},ensure_ascii=False))


