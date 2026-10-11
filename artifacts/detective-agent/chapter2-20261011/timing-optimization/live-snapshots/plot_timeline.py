"""Render real seconds/game clocks on the bottom, with staggered milestones."""
import json
import sys
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
fig, axes = plt.subplots(2, 1, figsize=(13.5, 7.8), sharex=True,
                         gridspec_kw={'height_ratios':[1,1]})
for ax, key, ylabel, color in zip(axes, ['fps','frame_p95_ms'], ['实际帧率（FPS）','帧耗时 P95（毫秒）'], ['#176b91','#b85e22']):
    data = [(r['elapsed_ms']/1000, r['value'][key]) for r in performance if key in r['value']]
    ax.plot([x for x,y in data], [y for x,y in data], color=color, linewidth=1.25)
    ax.set_ylabel(ylabel)
    ax.grid(alpha=.2)
axes[0].set_yscale('log')
axes[0].set_title('四倍速引擎侦探 · 实测性能与关键节点', loc='left', pad=105, fontsize=16)
for i, node in enumerate(reviewed['nodes']):
    x = node['wall_upper']
    label_x = .12 + i*.34
    label_y = 1.10 + (i%2)*.23
    game_lo, game_hi = int(node['game_lower']), int(node['game_upper'])
    clock = lambda m: f'{m//60:02}:{m%60:02}'
    text = f"{node['label']}\n测试 {node['wall_lower']:.1f}–{x:.1f} 秒｜游戏 {clock(game_lo)}–{clock(game_hi)}"
    axes[0].annotate(text, xy=(x,1), xycoords=('data','axes fraction'),
                     xytext=(label_x,label_y), textcoords='axes fraction',
                     ha='center', va='bottom', fontsize=10,
                     bbox={'boxstyle':'round,pad=.5','fc':'#eef6fa','ec':'#176b91'},
                     arrowprops={'arrowstyle':'-','color':'#176b91','lw':1}, annotation_clip=False)
    for ax in axes:
        ax.axvspan(node['wall_lower'], node['wall_upper'], color='#176b91', alpha=.13)
        ax.axvline(x,color='#176b91',ls='--',lw=.8,alpha=.65)
last = clocks[-1][0]
targets = [0] + list(range(60, int(last),60)) + [last]
chosen = [min(clocks,key=lambda p:abs(p[0]-t)) for t in targets]
ticks = [p[0] for p in chosen]
labels = [f'{p[0]:.1f} 秒\n{int(p[1])//60:02}:{int(p[1])%60:02}' for p in chosen]
axes[-1].set_xticks(ticks,labels,fontsize=10)
axes[-1].set_xlabel('测试经过时间（上行：现实秒） / 同时记录的游戏时钟（下行）',labelpad=12)
axes[-1].set_xlim(0,last+3)
fig.text(.08,.027,'时钟来自实际采样，未按倍速推算。未达成的推演、答案与交接不标成已完成；来源冲突检视未计入。',fontsize=10,color='#555555')
fig.subplots_adjust(top=.72,bottom=.19,left=.08,right=.98,hspace=.16)
fig.savefig(out/'milestone-timeline.png',dpi=160)
print(out/'milestone-timeline.png')
