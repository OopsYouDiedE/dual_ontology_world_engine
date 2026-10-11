"""Two independent linear timelines; one measured frame-time statistic."""
import json
import sys
from bisect import bisect_left
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

out = Path(sys.argv[1])
events = [json.loads(x) for x in (out/'raw/trajectory.jsonl').read_bytes().splitlines() if x]
audit = [json.loads(x) for x in (out/'raw/execution-diagnostics.jsonl').read_bytes().splitlines() if x]
reviewed = json.loads((out/'reviewed-key-nodes.json').read_text(encoding='utf-8'))
clocks = [(r['elapsed_ms']/1000, r['value']['milestone_progress']['game_minutes'])
          for r in audit if 'game_minutes' in r['value'].get('milestone_progress', {})]
performance = [r for r in events if r['kind']=='performance']
plt.rcParams['font.family'] = FontProperties(fname='C:/Windows/Fonts/msyh.ttc').get_name()
plt.rcParams['axes.unicode_minus'] = False
initial = reviewed['initial_game_minutes']
clock_seconds = [t for t,m in clocks]
samples = []
for r in performance:
    if 'frame_p95_ms' not in r['value']:
        continue
    wall = r['elapsed_ms']/1000
    index = bisect_left(clock_seconds,wall)
    adjacent = [i for i in (index-1,index) if 0 <= i < len(clocks)]
    nearest = min(adjacent,key=lambda i:abs(clocks[i][0]-wall))
    sampled_wall, minute = clocks[nearest]
    samples.append({'sequence': r['sequence'], 'wall_seconds': wall,
                    'game_minutes': minute, 'clock_sample_wall_seconds': sampled_wall,
                    'frame_p95_ms': r['value']['frame_p95_ms']})
# Game time uses observed clocks, never a four-times conversion. The two
# timelines are independent; sharing an x transform would misrepresent pauses.
fig, axes = plt.subplots(2, 1, figsize=(13.5, 8.9), sharey=True)
for panel, ax in enumerate(axes):
    game_axis = panel == 0
    xs = [s['game_minutes']-initial if game_axis else s['wall_seconds'] for s in samples]
    ax.plot(xs,[s['frame_p95_ms'] for s in samples],color='#176b91',lw=1.2)
    ax.set_ylabel('帧耗时 P95（毫秒）')
    ax.grid(alpha=.2)
    ax.set_title('按游戏经过时间排布' if game_axis else '按实际测试时间排布',
                 loc='right',fontsize=11,color='#555555')
    ax.set_xlim(0,clocks[-1][1]-initial if game_axis else clocks[-1][0])
    key = 'game' if game_axis else 'wall'
    for i, node in enumerate(reviewed['nodes']):
        lower = node[key+'_lower']-(initial if game_axis else 0)
        upper = node[key+'_upper']-(initial if game_axis else 0)
        text = node['label']+'\n'+f'{lower:.1f}–{upper:.1f} '+('游戏分钟' if game_axis else '测试秒')
        ax.annotate(text,xy=(upper,1),xycoords=('data','axes fraction'),
                    xytext=(.23+i*.32,1.07+(i%2)*.19),textcoords='axes fraction',
                    ha='center',va='bottom',fontsize=10,
                    bbox={'boxstyle':'round,pad=.4','fc':'#eef6fa','ec':'#176b91'},
                    arrowprops={'arrowstyle':'-','color':'#176b91','lw':1},annotation_clip=False)
        ax.axvspan(lower,upper,color='#176b91',alpha=.13)
        ax.axvline(upper,color='#176b91',ls='--',lw=.8,alpha=.65)
    if game_axis:
        ticks = list(range(0,int(clocks[-1][1]-initial)+1,60))
        ax.set_xticks(ticks,[f'{t} 分钟' for t in ticks])
        ax.set_xlabel('游戏经过时间（线性刻度；起点游戏时钟 08:10）')
    else:
        ticks = list(range(0,int(clocks[-1][0])+1,60))
        ax.set_xticks(ticks,[f'{t} 秒' for t in ticks])
        ax.set_xlabel('实际测试经过时间（线性刻度）')
fig.suptitle('四倍速引擎侦探 · 两种时间尺度下的帧耗时与关键节点',x=.08,ha='left',fontsize=16,y=.985)
fig.text(.08,.025,'上下图为同一份帧耗时数据，各用独立线性时间轴。只显示帧耗时，不重复显示帧率；节点阴影为采样区间。',fontsize=10,color='#555555')
fig.subplots_adjust(top=.80,bottom=.12,left=.08,right=.98,hspace=.65)
fig.savefig(out/'milestone-timeline-linear.png',dpi=150)
(out/'timeline-samples.json').write_text(json.dumps(samples,ensure_ascii=False,indent=2),encoding='utf-8')
print(out/'milestone-timeline-linear.png')
