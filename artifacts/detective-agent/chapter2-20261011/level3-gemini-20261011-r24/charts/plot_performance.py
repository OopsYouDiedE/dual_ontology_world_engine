"""Plot real wall-clock measurements; never infer FPS from decision timestamps."""
import argparse
import csv
import json
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

parser = argparse.ArgumentParser()
parser.add_argument('traces', nargs='+', type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
font = FontProperties(fname='C:/Windows/Fonts/msyh.ttc')
plt.rcParams['font.family'] = font.get_name()
plt.rcParams['axes.unicode_minus'] = False
fig, axes = plt.subplots(4, len(args.traces), figsize=(8 * len(args.traces), 13), squeeze=False)
summaries, table, waits, managed_rows = [], [], [], []
for column, path in enumerate(args.traces):
    events = [json.loads(line) for line in path.read_text(encoding='utf-8').splitlines() if line]
    name = '修改前 r21' if path.parent.name == 'unified-r21' else '修改后 r24'
    ending = next((e['elapsed_ms'] for e in events if e['kind'] == 'shown' and e['value'].get('kind') == 'ending'), None)
    if ending is not None: events = [e for e in events if e['elapsed_ms'] <= ending]
    stopped = next((e['value'] for e in reversed(events) if e['kind'] == 'stop'), {})
    stalled_from = max((e['elapsed_ms']/1000 for e in events if e['kind'] == 'act'), default=0) if stopped.get('reason') == 'no_public_progress' else None
    perf = [e for e in events if e['kind'] == 'performance']
    t = np.array([e['elapsed_ms'] / 1000 for e in perf])
    values = [e['value'] for e in perf]
    starts = {}
    render = []
    offers = {}
    huo = None
    for e in events:
        k, v = e['kind'], e['value']
        if k == 'observation': offers = {c['Token']: c for c in v.get('Candidates', [])}
        if k == 'decision':
            c = offers.get(v.get('Token'), {})
            if huo is None and c.get('Kind') == 'interact' and '霍' in c.get('Label', ''):
                huo = e['elapsed_ms'] / 1000
        if k == 'render_wait_start': starts[v['token']] = e['elapsed_ms']
        if k == 'render_wait_complete' and v['token'] in starts:
            render.append((e['elapsed_ms']/1000, e['elapsed_ms'] - starts.pop(v['token'])))
        if k == 'timing': waits.append(dict(run=name, seconds=e['elapsed_ms']/1000, **v))
    for e in perf:
        v=e['value']; row={'run':name, 'seconds':e['elapsed_ms']/1000,
            **{k:v[k] for k in ['window_seconds','fps','frame_p95_ms','frame_max_ms']}}
        for stage, measurements in v.get('managed', {}).items():
            managed_rows.append({'run':name, 'seconds':e['elapsed_ms']/1000,
                'stage':stage, 'window_seconds':v['window_seconds'], **measurements})
        for key in ['dispatch:tick','decision','cognition_snapshot']:
            m=v.get('managed',{}).get(key,{})
            for metric in ['calls','total_ms','max_ms']: row[key+'_'+metric]=m.get(metric,0)
        table.append(row)
    ax=axes[0,column]
    ax.plot(t,[v['fps'] for v in values],color='#147d92',label='实测 FPS（窗口帧数 / 墙钟）')
    ax.axhline(60,color='gray',ls=':',lw=1)
    ax.set_ylabel('帧 / 秒'); ax.set_title(name + ' · 4 倍速')
    ax=axes[1,column]
    ax.plot(t,[v['frame_max_ms'] for v in values],label='窗口最长一帧',color='#d35f45')
    ax.plot(t,[v['frame_p95_ms'] for v in values],label='窗口 P95 帧间隔',color='#dc9a21')
    ax.set_ylabel('帧间隔（毫秒）')
    ax=axes[2,column]
    for key,label,color in [('dispatch:tick','世界 tick 总耗时','#9b59b6'),('decision','玩家决策总耗时','#2479ba')]:
        ax.plot(t,[v.get('managed',{}).get(key,{}).get('total_ms',0)/v['window_seconds'] for v in values],label=label,color=color)
    ax.set_ylabel('毫秒 / 墙钟秒')
    ax=axes[3,column]
    if render: ax.plot(*zip(*render),label='等待下一次实际绘制',color='#7165ab',alpha=.8)
    for stage,label,color in [('reading','主动阅读等待实耗','#289668'),('observe','观察刷新计算','#ca9048'),('screenshot','同步截图采集','#b94a62')]:
        records=[w for w in waits if w['run']==name and w['stage']==stage]
        if records: ax.plot([w['seconds'] for w in records],[w['ms'] for w in records],label=label,color=color,alpha=.8)
    ax.set_ylabel('单次实耗（毫秒）'); ax.set_xlabel('本轮开始后的墙钟时间（秒）')
    for ax in axes[:,column]:
        if stalled_from is not None:
            ax.axvspan(stalled_from, t[-1], color='#9b9b9b', alpha=.18)
        ax.grid(alpha=.2); ax.legend(fontsize=8,loc='upper left')
        if huo is not None: ax.axvline(huo,color='#444444',ls='--',alpha=.5)
    if huo is not None: axes[0,column].text(huo+.6,axes[0,column].get_ylim()[1]*.82,'首次选择与霍交互',fontsize=9)
    if stalled_from is not None: axes[0,column].text(stalled_from+.6, axes[0,column].get_ylim()[1]*.55, '最后一次操作后停滞\n此段高帧率不代表性能改善', fontsize=9)
    segments=[]
    active_end = min(t[-1], stalled_from) if stalled_from is not None else t[-1]
    windows = [('early_active',5,min(huo or 25,active_end)),
               ('late_active',max(0,active_end-60),active_end)]
    if stalled_from is not None: windows.append(('stalled_idle',stalled_from,t[-1]+1))
    for label,lo,hi in windows:
        selected=[v for x,v in zip(t,values) if lo<=x<hi]
        seconds=sum(v['window_seconds'] for v in selected)
        segments.append({'segment':label,'from_s':lo,'to_s':hi,'mean_fps':sum(v['fps']*v['window_seconds'] for v in selected)/seconds if seconds else None,
            'longest_frame_ms':max((v['frame_max_ms'] for v in selected),default=0),
            'tick_ms_per_s':sum(v.get('managed',{}).get('dispatch:tick',{}).get('total_ms',0) for v in selected)/seconds if seconds else None})
    summaries.append({'run':name,'stalled_from_s':stalled_from,'first_huo_choice_seconds':huo,'first_chapter_ending_seconds':None if ending is None else ending/1000,'segments':segments,
        'render_wait_max_ms':max((v[1] for v in render),default=0),
        'render_wait_p95_ms':float(np.percentile([v[1] for v in render],95)) if render else None,
        'end':next((e['value'] for e in reversed(events) if e['kind']=='stop'),{})})
for row in range(4):
    top=max(ax.get_ylim()[1] for ax in axes[row,:])
    for ax in axes[row,:]: ax.set_ylim(0,top)
fig.suptitle('第一章 4 倍速：实际帧率、计算与等待\n相同随机种子与启动参数；自主轨迹会分岔，同一行采用相同纵轴范围',fontsize=15)
fig.tight_layout(rect=(0,.035,1,.95))
fig.text(.02,.01,'仅第一章，出现终局后截断。等待不是 CPU 时间；灰区表示最后一次操作后无进展，不能作正常负载性能证据。',fontsize=10)
fig.savefig(args.output/'performance.png',dpi=160)
for name,rows in [('frames.csv',table),('timings.csv',waits),('managed-stages.csv',managed_rows)]:
    keys=list(dict.fromkeys(k for row in rows for k in row))
    with (args.output/name).open('w',newline='',encoding='utf-8-sig') as stream:
        writer=csv.DictWriter(stream,fieldnames=keys); writer.writeheader(); writer.writerows(rows)
(args.output/'summary.json').write_text(json.dumps(summaries,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps([{k:v for k,v in s.items() if k!='end'} for s in summaries],ensure_ascii=False,indent=2))
