"""Report observed game clocks at real milestones, without speed-based estimates."""
import csv
import hashlib
import json
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties
from matplotlib.ticker import FuncFormatter

ROOT = Path(__file__).resolve().parents[5]
OUT = Path(__file__).resolve().parent
RUNS = {
    '修改前 r21': ROOT / 'artifacts/detective-agent/chapter2-20261011/runs/unified-r21/trajectory.jsonl',
    '修改后 r24': OUT.parent / 'raw/trajectory.jsonl',
}


def entered(region):
    return lambda r: r['kind'] == 'decision_frame' and r['value'].get('entered') == region and r['value'].get('interior') is True


def card(title):
    return lambda r: r['kind'] == 'shown' and r['value'].get('kind') == 'card' and r['value'].get('title') == title


NODES = [
    ('初始时钟', lambda r: r['kind'] == 'decision' and bool(r['value'].get('GameTime'))),
    ('实际进入走廊', entered('corridor')),
    ('实际进入空房', entered('empty')),
    ('活动厅见面对白开始', lambda r: r['kind'] == 'shown' and any('哟，又醒一个' in line[1] for line in r['value'].get('lines', []))),
    ('开场结束且已在活动厅内', entered('hall')),
    ('实际进入门厅', entered('foyer')),
    ('首次打开石板卡', card('霍铎的石板')),
    ('首次打开靴印卡', card('灰上的靴印')),
    ('实际进入 R06', entered('R06')),
    ('第一章终局', lambda r: r['kind'] == 'shown' and r['value'].get('kind') == 'ending'),
    ('本轮结束', lambda r: r['kind'] == 'stop'),
]


def minutes(clock):
    return (int(clock['Day']) - 1) * 1440 + int(clock['Minutes'])


def clock_label(value):
    if value is None:
        return '未记录'
    return f'{int(value) // 60:02}:{int(value) % 60:02}'


font = FontProperties(fname='C:/Windows/Fonts/msyh.ttc')
plt.rcParams['font.family'] = font.get_name()
plt.rcParams['axes.unicode_minus'] = False
all_nodes, traces, metadata = [], {}, {}
for name, path in RUNS.items():
    raw = path.read_bytes()
    events = [json.loads(line) for line in raw.splitlines() if line]
    samples = []
    for r in events:
        c = r['value'].get('GameTime') if r['kind'] == 'decision' else r['value'].get('time') if r['kind'] == 'stop' else None
        if c:
            samples.append({'wall_seconds': r['elapsed_ms'] / 1000, 'sequence': r['sequence'], 'game_minutes': minutes(c), 'clock': c, 'kind': r['kind']})
    assert all(b['game_minutes'] >= a['game_minutes'] for a, b in zip(samples, samples[1:])), name
    start = samples[0]['game_minutes']
    traces[name] = samples
    metadata[name] = {'input': str(path), 'sha256': hashlib.sha256(raw).hexdigest(), 'initial_game_minutes': start, 'time_scale': events[0]['value']['time_scale']}
    for label, predicate in NODES:
        event = next((r for r in events if predicate(r)), None)
        item = {'run': name, 'node': label, 'reached': bool(event)}
        if event:
            wall = event['elapsed_ms'] / 1000
            before = next((c for c in reversed(samples) if c['wall_seconds'] <= wall), None)
            after = next((c for c in samples if c['wall_seconds'] >= wall), None)
            item.update(sequence=event['sequence'], wall_seconds=wall, clock_before=before, clock_after=after)
            if event['kind'] == 'stop':
                lo = hi = minutes(event['value']['time'])
                item['stop_reason'] = event['value']['reason']
                item['reported_duration_seconds'] = event['value']['elapsed_seconds']
            elif event['kind'] == 'decision' and event['value'].get('GameTime'):
                lo = hi = minutes(event['value']['GameTime'])
            else:
                lo = before['game_minutes'] if before else None
                hi = after['game_minutes'] if after else None
            item.update(game_minutes_low=lo, game_minutes_high=hi,
                        game_elapsed_low=None if lo is None else lo-start,
                        game_elapsed_high=None if hi is None else hi-start,
                        game_clock=clock_label(lo) if lo == hi else f'{clock_label(lo)}–{clock_label(hi)}',
                        clock_precision='recorded_or_equal_brackets' if lo == hi else 'bracket_only_no_interpolation')
        all_nodes.append(item)

(OUT/'key-nodes.json').write_text(json.dumps({'method': 'Only public decision.GameTime and stop.time; bracket other events with surrounding observed clocks; no wall-time multiplication or interpolation.', 'inputs': metadata, 'nodes': all_nodes}, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
with (OUT/'key-nodes.csv').open('w', encoding='utf-8-sig', newline='') as f:
    fields = ['run', 'node', 'reached', 'sequence', 'wall_seconds', 'game_clock', 'game_elapsed_low', 'game_elapsed_high', 'clock_precision']
    w = csv.DictWriter(f, fieldnames=fields, extrasaction='ignore'); w.writeheader(); w.writerows(all_nodes)
with (OUT/'game-clock-samples.csv').open('w', encoding='utf-8-sig', newline='') as f:
    w = csv.writer(f); w.writerow(['run', 'sequence', 'wall_seconds', 'game_minutes', 'game_clock', 'phase'])
    for name, samples in traces.items():
        for s in samples: w.writerow([name, s['sequence'], s['wall_seconds'], s['game_minutes'], clock_label(s['game_minutes']), s['clock']['Phase']])

colors = {'修改前 r21': '#2479ba', '修改后 r24': '#d35f45'}
fig, (ax, bx) = plt.subplots(2, 1, figsize=(12, 8.4), gridspec_kw={'height_ratios':[1.3, 1]})
for name, samples in traces.items():
    ax.plot([s['wall_seconds'] for s in samples], [s['game_minutes'] for s in samples], color=colors[name], label=name, linewidth=1.7)
    for node in all_nodes:
        if node['run'] != name or not node['reached'] or node['node'] not in ['开场结束且已在活动厅内', '首次打开靴印卡', '实际进入 R06', '本轮结束']:
            continue
        lo, hi = node['game_minutes_low'], node['game_minutes_high']
        y=(lo+hi)/2
        ax.errorbar(node['wall_seconds'], y, yerr=(hi-lo)/2, marker='o', color=colors[name], capsize=3)
        text={'开场结束且已在活动厅内':'活动厅开场结束','首次打开靴印卡':'靴印卡（时钟范围）','实际进入 R06':'进入 R06（时钟范围）','本轮结束':'无进展停止' if name=='修改前 r21' else '预算结束'}[node['node']]
        offset = (-72,-22) if node['node']=='本轮结束' else (5,9)
        align = 'left'
        if node['node']=='本轮结束' and name=='修改后 r24': offset=(-5,12); align='right'
        ax.annotate(text, (node['wall_seconds'], y), xytext=offset, ha=align, textcoords='offset points', fontsize=9, color=colors[name])
ax.yaxis.set_major_formatter(FuncFormatter(lambda v, p: clock_label(v)))
ax.set_yticks(range(490, 751, 30))
ax.set_ylabel('第 1 日游戏内时钟'); ax.set_xlabel('从启动算起的现实时间（秒）')
ax.set_title('游戏内时钟与现实耗时：相同种子、四倍速，真实路线不同')
ax.grid(alpha=.2); ax.legend(loc='upper left')
ax.axvspan(43.486, 102.793, color='#999999', alpha=.12)
ax.text(48, 500, '灰区：旧轨迹靴印卡停滞\n高 FPS 或现实时间增长不代表游戏推进', fontsize=9, color='#555555')

common=['实际进入走廊', '实际进入空房', '开场结束且已在活动厅内']
for name in RUNS:
    ns=[next(n for n in all_nodes if n['run']==name and n['node']==label) for label in common]
    ys=[(n['game_elapsed_low']+n['game_elapsed_high'])/2 for n in ns]
    errors=[(n['game_elapsed_high']-n['game_elapsed_low'])/2 for n in ns]
    bx.errorbar(range(len(common)),ys,yerr=errors,marker='o',capsize=4,color=colors[name],label=name)
    for x,n,y in zip(range(len(common)),ns,ys): bx.annotate(n['game_clock'],(x,y),xytext=(7,8 if name=='修改前 r21' else -17),textcoords='offset points',fontsize=9,color=colors[name])
bx.set_xticks(range(len(common)),['进入走廊','进入空房','活动厅开场结束'])
bx.set_ylabel('自初始 08:10 累积的游戏分钟')
bx.set_ylim(-10, 140)
bx.set_yticks(range(0,141,20))
bx.set_title('共同节点的游戏时间：误差线保留相邻时钟范围，不虚构精确值')
bx.grid(alpha=.2); bx.legend(loc='upper left')
fig.text(.02,.012,'折线只连接已记录的时钟点。游戏时间含剧情跳时；不能用现实秒数 × 4 推算。未到达的节点不绘制为零。',fontsize=10)
fig.tight_layout(rect=(0,.04,1,1))
fig.savefig(OUT/'game-clock-comparison.png',dpi=160)
plt.close(fig)
print(json.dumps([{k:v for k,v in n.items() if k not in ['clock_before','clock_after']} for n in all_nodes],ensure_ascii=False,indent=2))
