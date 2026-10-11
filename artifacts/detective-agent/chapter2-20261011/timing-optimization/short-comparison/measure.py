"""Remeasure archived/fresh public trajectories in a 120-second window."""
from pathlib import Path
import hashlib,json,re,statistics,sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties
ROOT=Path(__file__).resolve().parents[5]
OUT=Path(__file__).resolve().parent
TASK=ROOT/'artifacts/detective-agent/chapter2-20261011'
paths={
 'r18 历史':TASK/'runs/performance-r18/trajectory.jsonl',
 'r19 历史':TASK/'runs/performance-r19/trajectory.jsonl',
 'r20 历史':TASK/'runs/performance-r20/trajectory.jsonl',
 'r21 历史':TASK/'runs/unified-r21/trajectory.jsonl',
 'r22 历史（1倍，附录）':TASK/'level3-gemini-20261011-r22/raw/trajectory.jsonl',
 'r23 历史（1倍，附录）':TASK/'level3-gemini-20261011-r23/raw/trajectory.jsonl',
 'r24 历史':TASK/'level3-gemini-20261011-r24/raw/trajectory.jsonl',
 'r25 历史':TASK/'timing-optimization/runs/timing-r25/raw/trajectory.jsonl',
 '当前 新测':OUT/'fresh-current/raw/trajectory.jsonl'}
content=json.loads((ROOT/'godot_csharp/scenes/Awakening/Script/generated_awakening_content.json').read_text(encoding='utf-8'))
titles={v['name']:k for k,v in content['EVIDENCE'].items()}
scene=(ROOT/'godot_csharp/scenes/Awakening/Script/story/第一章/场面/07_答辩.story').read_text(encoding='utf-8')
accepted={}
for index in range(1,5):
    body=re.search(r'场面 answer_ok'+str(index)+r'\n(.*?)(?=\n场面|\Z)',scene,re.S).group(1)
    accepted[index]=[re.sub(r'\s*\[#id=.*?\]','',line.split('：',1)[1]).strip()
                     for line in body.splitlines() if '：' in line and '[#id=' in line]

def clock(row):
    value=row['value']
    t=value.get('GameTime') if row['kind']=='decision' else value.get('time') if row['kind']=='stop' else None
    return t.get('Minutes') if t else None

def measured(event,clocks):
    before=next((p for p in reversed(clocks) if p[0]<=event['elapsed_ms']),None)
    after=next((p for p in clocks if p[0]>=event['elapsed_ms']),None)
    return {'sequence':event['sequence'],'wall_seconds':event['elapsed_ms']/1000,
            'game_lower':before[1] if before else None,'game_upper':after[1] if after else None}

result={}
for name,path in paths.items():
    if not path.exists(): continue
    payload=path.read_bytes()
    allrows=[json.loads(x) for x in payload.splitlines() if x]
    rows=[r for r in allrows if r['elapsed_ms']<=120000]
    clocks=[(r['elapsed_ms'],clock(r)) for r in rows if clock(r) is not None]
    cards,answers,excluded={}, {}, []
    frames=[r for r in rows if r['kind']=='decision_frame']
    for r in rows:
        v=r['value']
        if r['kind']!='shown': continue
        if v.get('kind')=='card' and v.get('title') in titles:
            key=titles[v['title']]
            frame=next((f for f in reversed(frames) if f['elapsed_ms']<=r['elapsed_ms']),None)
            # Personal shelf/coat sources require the declared personal room;
            # distributed pipe evidence is not restricted to a single location.
            if key in ('E11','E12') and (not frame or frame['value'].get('feet_region')!='R-05'):
                excluded.append({'id':key,**measured(r,clocks),'reason':'现场不在声明的R-05，未计入有效检视'})
                continue
            if key not in cards: cards[key]={'title':v['title'],**measured(r,clocks)}
        if v.get('kind')=='lines':
            texts=[line[1] for line in v.get('lines',[])]
            for index,lines in accepted.items():
                if index not in answers and any(line in texts for line in lines):
                    answers[index]=measured(r,clocks)
    perf=[r for r in rows if r['kind']=='performance' and
          (name!='r21 历史' or r['elapsed_ms']<43486)]
    p95=[r['value']['frame_p95_ms'] for r in perf if 'frame_p95_ms' in r['value']]
    # Some old runs stop before 120s. Preserve the actual endpoint; never pad.
    stopping=next((r for r in rows if r['kind']=='stop'),None)
    result[name]={'path':str(path),'sha256':hashlib.sha256(payload).hexdigest(),
                  'window_seconds':120,'observed_until_seconds':rows[-1]['elapsed_ms']/1000,
                  'initial_game_minutes':clocks[0][1] if clocks else None,
                  'last_game_minutes':clocks[-1][1] if clocks else None,
                  'cards':cards,'answers':answers,'excluded':excluded,
                  'formal_replay_full_match':'没有保存足以确认完整正确的结果，不能按打开手稿推断',
                  'correct_brain_conclusion':'未取得完整形成记录',
                  'chapter2':'本窗口没有可确认的章节交接记录',
                  'ending_presented':any(r['kind']=='shown' and r['value'].get('kind')=='ending' for r in rows),
                  'performance_note':'排除43.486秒后候选为空的空转' if name=='r21 历史' else '前120秒的原始采样',
                  'stop_reason':stopping['value'].get('reason') if stopping else '历史记录截取前120秒',
                  'window_p95_median_ms':statistics.median(p95) if p95 else None}
    result[name]['plot_samples']=[{'wall_seconds':r['elapsed_ms']/1000,
        'game_minutes':next((t for wall,t in reversed(clocks) if wall<=r['elapsed_ms']),None),
        'frame_p95_ms':r['value']['frame_p95_ms']} for r in perf if 'frame_p95_ms' in r['value']]

(OUT/'measurements.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
def stamp(node):
    if not node: return '本窗口未发生'
    a,b=node['game_lower'],node['game_upper']
    fmt=lambda m: '?' if m is None else f'{int(m)//60:02}:{int(m)%60:02}'
    return f"{node['wall_seconds']:.3f}秒 / {fmt(a)}"+(f'–{fmt(b)}' if a!=b else '')
lines=['# 各版本前120秒测量','', '历史是原始实测记录重新统计；当前新测为新正式会话。时间为现实秒 / 相邻采样游戏时钟；未按倍速推算。','',
       '| 版本 | 首次有效关键检视 | 第一答被接受 | 第二答被接受 | 第三答被接受 | 第四答被接受 |',
       '| --- | --- | --- | --- | --- | --- |']
for name,d in result.items():
    first=min(d['cards'].values(),key=lambda n:n['wall_seconds']) if d['cards'] else None
    lines.append('| '+name+' | '+((first['title']+'：'+stamp(first)) if first else '本窗口未发生')+' | '+' | '.join(stamp(d['answers'].get(i)) for i in range(1,5))+' |')
lines += ['','## 逐件关键检视','', '| 版本 | 隔离门 | 靴印 | 石板 | 乐谱 | 杯子 | 酵种罐 |','| --- | --- | --- | --- | --- | --- | --- |']
for name,d in result.items():
    lines.append('| '+name+' | '+' | '.join(stamp(d['cards'].get(k)) for k in ['E1','E3','E7','E6','E9','E10'])+' |')
(OUT/'results.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
plt.rcParams['font.family']=FontProperties(fname='C:/Windows/Fonts/msyh.ttc').get_name()
fig,axes=plt.subplots(2,1,figsize=(14,9),sharey=True)
for index,(name,d) in enumerate(result.items()):
    if '附录' in name: continue
    samples=d['plot_samples'];color=f'C{index%10}'
    for panel,ax in enumerate(axes):
        points=[(s['game_minutes']-d['initial_game_minutes'] if panel==0 else s['wall_seconds'],s['frame_p95_ms'])
                for s in samples if s['game_minutes'] is not None]
        ax.plot([x for x,y in points],[y for x,y in points],label=name,color=color,lw=1,alpha=.85)
        for question,node in d['answers'].items():
            minute=node['game_upper'] if node['game_upper'] is not None else node['game_lower']
            x=minute-d['initial_game_minutes'] if panel==0 else node['wall_seconds']
            ax.axvline(x,color=color,ls=':',lw=.7,alpha=.6)
            ax.annotate(f'{name} 第{question}答',xy=(x,1),xycoords=('data','axes fraction'),
                        xytext=(.18+index*.10,1.04+(index%3)*.10),textcoords='axes fraction',
                        fontsize=8,color=color,arrowprops={'arrowstyle':'-','color':color,'lw':.6})
for ax in axes:
    ax.grid(alpha=.2);ax.set_ylabel('帧耗时 P95（毫秒）')
axes[0].set_xlabel('游戏经过分钟（各版独立实测起点；线性）')
axes[1].set_xlabel('实际测试经过秒数（线性；统一前120秒）');axes[1].set_xlim(0,120)
axes[0].legend(ncol=4,fontsize=9,loc='upper right')
fig.suptitle('历次版本短窗口：关键答案与帧耗时',fontsize=16)
fig.subplots_adjust(top=.82,bottom=.10,left=.07,right=.98,hspace=.6)
fig.savefig(OUT/'comparison-linear.png',dpi=150)
print('\n'.join(lines[:14]))
