"""Show audited live evidence; does not control the game or modify raw records."""
import json
import sys
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

out = Path(sys.argv[1])
rows = [json.loads(line) for line in (out / 'raw/execution-diagnostics.jsonl').read_bytes().splitlines() if line]
events = [json.loads(line) for line in (out / 'raw/trajectory.jsonl').read_bytes().splitlines() if line]
start = next(r['value']['milestone_progress']['game_minutes'] for r in rows if r['value'].get('milestone_progress'))
# Review selection is recorded separately from observed progress: card/receipt
# and scene were checked by the root and independent reviewer in this run.
selection = {'E7': '石板：首次有效检视', 'E6': '乐谱：首次有效检视'}
found, prior = {}, None
for r in rows:
    progress = r['value'].get('milestone_progress', {})
    for key, label in selection.items():
        if key in progress.get('examined', {}) and key not in found:
            old = prior['value'].get('milestone_progress', {}) if prior else {}
            found[key] = {'id': key, 'label': label, 'wall_lower': prior['elapsed_ms']/1000 if prior else None,
                          'wall_upper': r['elapsed_ms']/1000, 'game_lower': old.get('game_minutes'),
                          'game_upper': progress['game_minutes'], 'audit_row_elapsed_ms': r['elapsed_ms']}
    if progress:
        prior = r
nodes = sorted(found.values(), key=lambda n: n['wall_upper'])
report = {'initial_game_minutes': start, 'nodes': nodes,
          'excluded': {'E11': 'R-08 现场打开 R-05 书架信息，来源冲突', 'E12': '空房打开 R-05 外衣信息，待核验来源'},
          'pending': ['正确脑内结论尚缺完整形成记录', '正式推演全部吻合未出现', '铜口第1—4答未被接受', '章节交接未发生'],
          'note': '采样上下界；同期现场/卡片交叉核对。后续记录不覆盖此快照。'}
(out / 'reviewed-key-nodes.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
plt.rcParams['font.family'] = FontProperties(fname='C:/Windows/Fonts/msyh.ttc').get_name()
fig, axes = plt.subplots(2, 1, figsize=(10, 7.5), sharex=True)
labels = ['开始'] + [n['label'] for n in nodes]
for ax, kind, ylabel in zip(axes, ['game', 'wall'], ['游戏内累计分钟', '现实累计秒']):
    upper = [0] + [n[kind+'_upper'] - (start if kind=='game' else 0) for n in nodes]
    lower = [0] + [(n[kind+'_lower'] if n[kind+'_lower'] is not None else n[kind+'_upper']) - (start if kind=='game' else 0) for n in nodes]
    ax.plot(range(len(labels)), upper, marker='o', color='#176b91')
    ax.fill_between(range(len(labels)), lower, upper, color='#176b91', alpha=.2, label='相邻采样时间范围')
    for i, (lo, hi) in enumerate(zip(lower, upper)):
        text = f'{hi:.1f}' if lo==hi else f'{lo:.1f}–{hi:.1f}'
        ax.annotate(text, (i,hi), xytext=(0,8), textcoords='offset points', ha='center')
    ax.set_ylabel(ylabel)
    ax.grid(alpha=.25)
axes[-1].set_xticks(range(len(labels)), labels)
fig.suptitle('四倍速引擎侦探：本轮已核验关键节点耗时')
fig.text(.5,.02,'未达成节点不计为0；书架/外衣来源冲突记录未计入有效关键检视。',ha='center',fontsize=10)
fig.tight_layout(rect=(0,.05,1,.95))
fig.savefig(out / 'key-node-times.png',dpi=150)
print(json.dumps(report,ensure_ascii=False))
