from pathlib import Path
import json,re,hashlib,statistics
ROOT=Path.cwd();OUT=ROOT/'artifacts/detective-agent/chapter2-20261011/timing-optimization/branch-trials'
paths={'历史r20':ROOT/'artifacts/detective-agent/chapter2-20261011/runs/performance-r20',
       '回退前当前版':ROOT/'artifacts/detective-agent/chapter2-20261011/timing-optimization/short-comparison/fresh-current/raw',
       '恢复基线':OUT/'r20-baseline-90/raw',
       '目标连续性':OUT/'goal-continuity-90/raw',
       '导航反馈':OUT/'route-feedback-90/raw',
       '读图性能':OUT/'read-performance-90/raw'}
scene=(ROOT/'godot_csharp/scenes/Awakening/Script/story/第一章/场面/07_答辩.story').read_text(encoding='utf-8')
accepted={i:[re.sub(r'\s*\[#id=.*?\]','',l.split('：',1)[1]).strip() for l in re.search(r'场面 answer_ok'+str(i)+r'\n(.*?)(?=\n场面|\Z)',scene,re.S).group(1).splitlines() if '：' in l and '[#id=' in l] for i in range(1,5)}
content=json.loads((ROOT/'godot_csharp/scenes/Awakening/Script/generated_awakening_content.json').read_text(encoding='utf-8'))
titles={v['name']:k for k,v in content['EVIDENCE'].items()}
result={}
for name,p in paths.items():
    if not (p/'trajectory.jsonl').exists():continue
    payload=(p/'trajectory.jsonl').read_bytes();allrows=[json.loads(l) for l in payload.splitlines()]
    rows=[r for r in allrows if r['elapsed_ms']<=90000]
    clocks=[(r['elapsed_ms'],r['value']['GameTime']['Minutes']) for r in allrows if r['kind']=='decision']
    frames=[r for r in rows if r['kind']=='decision_frame'];cards={};answers={}
    def time(r):
        before=next((t for w,t in reversed(clocks) if w<=r['elapsed_ms']),None)
        after=next((t for w,t in clocks if w>=r['elapsed_ms']),None)
        return {'real_seconds':r['elapsed_ms']/1000,'game_before':before,'game_after':after,'sequence':r['sequence']}
    for r in rows:
        v=r['value']
        if r['kind']!='shown':continue
        if v.get('kind')=='card' and v.get('title') in titles:
            key=titles[v['title']]
            frame=next((f for f in reversed(frames) if f['elapsed_ms']<=r['elapsed_ms']),None)
            if key in ('E11','E12') and (not frame or frame['value'].get('feet_region')!='R-05'):continue
            if key not in cards:cards[key]={'title':v['title'],**time(r)}
        if v.get('kind')=='lines':
            texts=[l[1] for l in v.get('lines',[])]
            for i,lines in accepted.items():
                if str(i) not in answers and any(l in texts for l in lines):answers[str(i)]=time(r)
    samples=[{'real_seconds':r['elapsed_ms']/1000,'game_minutes':next((t for w,t in reversed(clocks) if w<=r['elapsed_ms']),None),'p95_ms':r['value']['frame_p95_ms']} for r in rows if r['kind']=='performance' and 'frame_p95_ms' in r['value']]
    replay='未取得完整正确推演的直接证据'
    if (p/'milestone-progress.jsonl').exists():
        progress=[json.loads(l) for l in (p/'milestone-progress.jsonl').read_bytes().splitlines()]
        progress=[r for r in progress if r['elapsed_ms']<=90000]
        if any(r['value'].get('replay_result',{}).get('full_match') is True for r in progress):replay='有完整正确结果，须另核玩家可见反馈'
        elif any(r['value'].get('replay_count',0)>0 for r in progress):replay='已执行重演，未完整正确'
        else:replay='窗口内未执行重演'
    result[name]={'raw_sha256':hashlib.sha256(payload).hexdigest(),'budget_real_seconds':90,'cards':cards,'accepted_answers':answers,'correct_replay':replay,'correct_brain_inference':'未取得完整形成记录','initial_game_minutes':clocks[0][1] if clocks else None,'samples':samples,'frame_window_p95_median_ms':statistics.median(s['p95_ms'] for s in samples) if samples else None}
(OUT/'measurements.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
def stamp(t):
    if not t:return '窗口内未发生'
    fmt=lambda m:'?' if m is None else f'{int(m)//60:02}:{int(m)%60:02}'
    a,b=t['game_before'],t['game_after']
    return f"{t['real_seconds']:.2f}秒 / {fmt(a)}"+(f'–{fmt(b)}' if a!=b else '')
lines=['# 90秒同窗口比较','', '时间为现实秒 / 实测游戏时钟区间，正确答案指首次公开接受反馈。','', '| 版本 | 首次关键检视 | 第一答 | 第二答 | 第三答 | 第四答 |','| --- | --- | --- | --- | --- | --- |']
for name,d in result.items():
    first=min(d['cards'].values(),key=lambda t:t['real_seconds']) if d['cards'] else None
    lines.append('| '+name+' | '+(first['title']+'：'+stamp(first) if first else '窗口内未发生')+' | '+' | '.join(stamp(d['accepted_answers'].get(str(i))) for i in range(1,5))+' |')
lines+=['','| 版本 | 隔离门 | 靴印 | 石板 | 乐谱 | 杯子 | 酵种罐 |','| --- | --- | --- | --- | --- | --- | --- |']
for name,d in result.items():lines.append('| '+name+' | '+' | '.join(stamp(d['cards'].get(k)) for k in ('E1','E3','E7','E6','E9','E10'))+' |')
(OUT/'results.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('\n'.join(lines[:13]))
