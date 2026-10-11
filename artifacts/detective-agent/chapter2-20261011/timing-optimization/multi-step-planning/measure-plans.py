"""Offline measurement of sealed 90-second sessions; no game inputs."""
import argparse, hashlib, json, re, statistics
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--runs',required=True,help='JSON mapping version labels to raw directories')
p.add_argument('--out',required=True)
args=p.parse_args()
root=Path(__file__).resolve().parents[5]
out=Path(args.out).resolve();out.mkdir(parents=True,exist_ok=True)
paths=json.loads(Path(args.runs).read_text(encoding='utf-8'))
scene=(root/'godot_csharp/scenes/Awakening/Script/story/第一章/场面/07_答辩.story').read_text(encoding='utf-8')
accepted={str(i):[re.sub(r'\s*\[#id=.*?\]','',l.split('：',1)[1]).strip() for l in re.search(r'场面 answer_ok'+str(i)+r'\n(.*?)(?=\n场面|\Z)',scene,re.S).group(1).splitlines() if '：' in l and '[#id=' in l] for i in range(1,5)}
content=json.loads((root/'godot_csharp/scenes/Awakening/Script/generated_awakening_content.json').read_text(encoding='utf-8'))
titles={v['name']:k for k,v in content['EVIDENCE'].items()}
result={};plans=[]
for name,raw in paths.items():
    raw=Path(raw);payload=(raw/'trajectory.jsonl').read_bytes()
    allrows=[json.loads(l) for l in payload.splitlines()];rows=[r for r in allrows if r['elapsed_ms']<=90000]
    clocks=[(r['elapsed_ms'],r['value']['GameTime']['Minutes']) for r in allrows if r['kind']=='decision' and 'GameTime' in r['value']]
    frames=[r for r in rows if r['kind']=='decision_frame'];cards={};answers={};proofs={}
    def time(r):
        return {'real_seconds':r['elapsed_ms']/1000,'game_before':next((t for w,t in reversed(clocks) if w<=r['elapsed_ms']),None),'game_after':next((t for w,t in clocks if w>=r['elapsed_ms']),None),'sequence':r['sequence']}
    for r in rows:
        v=r['value']
        if r['kind']=='decision':
            for a in v.get('Arguments',[]):
                if a.get('Value')=='True' and a.get('Complete') and a.get('Derivations') and a['Proposition'] not in proofs:
                    proofs[a['Proposition']]={**time(r),'derivations':a['Derivations']}
            planning={k:v[k] for k in v if any(w in k.lower() for w in ('plan','backup','immediate','rollout','future'))}
            if planning:plans.append({'version':name,**time(r),'chosen':next((a for a in v.get('Alternatives',[]) if a['Token']==v.get('Token')),None),'planning':planning})
        if r['kind']!='shown':continue
        if v.get('kind')=='card' and v.get('title') in titles:
            key=titles[v['title']];frame=next((f for f in reversed(frames) if f['elapsed_ms']<=r['elapsed_ms']),None)
            if key in ('E11','E12') and (not frame or frame['value'].get('feet_region')!='R-05'):continue
            if key not in cards:cards[key]={'title':v['title'],**time(r)}
        if v.get('kind')=='lines':
            texts=[l[1] for l in v.get('lines',[])]
            for key,lines in accepted.items():
                if key not in answers and any(l in texts for l in lines):answers[key]=time(r)
    samples=[{'real_seconds':r['elapsed_ms']/1000,'game_minutes':next((t for w,t in reversed(clocks) if w<=r['elapsed_ms']),None),'p95_ms':r['value']['frame_p95_ms']} for r in rows if r['kind']=='performance' and 'frame_p95_ms' in r['value']]
    progress=[json.loads(l) for l in (raw/'milestone-progress.jsonl').read_bytes().splitlines()] if (raw/'milestone-progress.jsonl').exists() else []
    progress=[r for r in progress if r['elapsed_ms']<=90000]
    last=progress[-1]['value'] if progress else {}
    replay=next((r for r in progress if r['value'].get('replay_result',{}).get('full_match') is True),None)
    result[name]={'raw_sha256':hashlib.sha256(payload).hexdigest(),'cards':cards,'accepted_answers':answers,'proved_claims':proofs,'samples':samples,'correct_replay':{'real_seconds':replay['elapsed_ms']/1000,'game_minutes':replay['value'].get('game_minutes')} if replay else None,'stop':next((r['value'] for r in reversed(allrows) if r['kind']=='stop'),None),'last_progress':{k:last.get(k) for k in ('chapter','stage','answer','examined','experiments','replay_count')},'frame_window_p95_median_ms':statistics.median(s['p95_ms'] for s in samples) if samples else None}
(out/'measurements.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'action-plans.jsonl').write_text('\n'.join(json.dumps(x,ensure_ascii=False) for x in plans)+'\n',encoding='utf-8')
def stamp(t):
    if not t:return '窗口内未发生'
    f=lambda m:'?' if m is None else f'{int(m)//60}:{int(m)%60:02}'
    a,b=t['game_before'],t['game_after']
    return f"{t['real_seconds']:.2f}秒 / {f(a)}"+(f'–{f(b)}' if a!=b else '')
lines=['# 90秒行动规划比较','','现实秒 / 实测游戏时钟，正确答案时刻为公开接受反馈。','','| 版本 | 第一答 | 第二答 | 第三答 | 第四答 |','| --- | --- | --- | --- | --- | --- |']
for name,d in result.items():lines.append('| '+name+' | '+' | '.join(stamp(d['accepted_answers'].get(str(i))) for i in range(1,5))+' |')
lines+=['','| 版本 | 靴印 | 音叉 | 乐谱 | 铜管 | 杯子 | 酵种罐 |','| --- | --- | --- | --- | --- | --- | --- |']
for name,d in result.items():lines.append('| '+name+' | '+' | '.join(stamp(d['cards'].get(k)) for k in ('E3','E5','E6','E8','E9','E10'))+' |')
(out/'results.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('\n'.join(lines))
