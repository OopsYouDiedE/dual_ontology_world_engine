from pathlib import Path
import json
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties
from matplotlib.ticker import ScalarFormatter, FuncFormatter, MultipleLocator
OUT=Path.cwd()/'artifacts/detective-agent/chapter2-20261011/timing-optimization/branch-trials'
data=json.loads((OUT/'measurements.json').read_text(encoding='utf-8'))
plt.rcParams['font.family']=FontProperties(fname='C:/Windows/Fonts/msyh.ttc').get_name()
fig,axes=plt.subplots(2,1,figsize=(15,13),sharey=True)
markers=[[],[]]
for index,(name,d) in enumerate(data.items()):
    color=f'C{index}';samples=d['samples'];tag=chr(65+index)
    for panel,ax in enumerate(axes):
        points=[(s['game_minutes'] if panel==0 else s['real_seconds'],s['p95_ms']) for s in samples if s['game_minutes'] is not None]
        ax.plot([x for x,y in points],[y for x,y in points],color=color,lw=1,alpha=.8,label=f'{tag} {name}')
        nodes=[]
        if '1' in d['accepted_answers']:nodes.append(('第一答',d['accepted_answers']['1']))
        for key,label in [('E3','靴印'),('E9','杯子'),('E6','乐谱')]:
            if key in d['cards']:nodes.append((label,d['cards'][key]))
        for n,(label,node) in enumerate(nodes):
            minute=node['game_after'] if node['game_after'] is not None else node['game_before']
            x=minute if panel==0 else node['real_seconds']
            if panel==0 and node['game_before'] is not None and node['game_after'] is not None:
                ax.axvspan(node['game_before'],node['game_after'],color=color,alpha=.035)
            ax.axvline(x,color=color,ls=':',lw=.6,alpha=.35)
            markers[panel].append((x,f'{tag}·{label}',color))
for ax in axes:
    ax.set_yscale('log', nonpositive='mask')
    ax.set_yticks([10,30,100,300,1000,3000])
    ax.yaxis.set_major_formatter(ScalarFormatter())
    ax.grid(which='major',alpha=.25)
    ax.grid(which='minor',axis='y',alpha=.08)
    ax.set_ylabel('采样窗口帧耗时 P95（毫秒，对数刻度）')
axes[0].set_xlim(480,900)
axes[0].xaxis.set_major_locator(MultipleLocator(60))
axes[0].xaxis.set_major_formatter(FuncFormatter(lambda x,pos:f'{int(round(x))//60}:{int(round(x))%60:02}'))
axes[0].set_xlabel('游戏时钟（时:分，线性）')
axes[1].set_xlim(0,90)
axes[1].xaxis.set_major_locator(MultipleLocator(15))
axes[1].xaxis.set_major_formatter(FuncFormatter(lambda x,pos:f'{int(round(x))//60}:{int(round(x))%60:02}'))
axes[1].set_xlabel('实际测试耗时（分:秒，90秒窗口，线性）')
# Labels stay exactly above their event x. Only height changes; leaders are vertical.
for panel,ax in enumerate(axes):
    lane_ends=[];spacing=(ax.get_xlim()[1]-ax.get_xlim()[0])*.012
    for x,label,color in sorted(markers[panel]):
        lane=next((i for i,end in enumerate(lane_ends) if x-end>=spacing),len(lane_ends))
        if lane==len(lane_ends):lane_ends.append(x)
        else:lane_ends[lane]=x
        y=1.04+lane*.22
        ax.plot([x,x],[1,y],transform=ax.get_xaxis_transform(),color=color,lw=.7,clip_on=False)
        ax.text(x,y,label,transform=ax.get_xaxis_transform(),rotation=90,ha='center',va='bottom',fontsize=8,color=color,clip_on=False)
axes[0].legend(ncol=3,fontsize=9,loc='upper right')
fig.suptitle('r20基线与三个隔离优化分支：90秒关键节点',fontsize=16,y=.985)
axes[0].set_position([.07,.53,.91,.20])
axes[1].set_position([.07,.10,.91,.20])
fig.text(.07,.035,'仅供解释节点耗时：单次路线不同；恢复基线存在同期编译资源竞争，不能据此证明性能因果改善。',fontsize=10)
fig.savefig(OUT/'comparison-90-log-vertical.png',dpi=150)
