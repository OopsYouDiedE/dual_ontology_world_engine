"""Post-session measurement only; never used by the detective or game host."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('run', type=Path)
    args = parser.parse_args()
    run = args.run.resolve()
    raw = run / 'raw'
    diagnostic = raw / 'execution-diagnostics.jsonl'
    trajectory = raw / 'trajectory.jsonl'
    records = [json.loads(line) for line in diagnostic.read_bytes().splitlines() if line]
    events = [json.loads(line) for line in trajectory.read_bytes().splitlines() if line]
    nodes, seen, previous = [], set(), None

    def add(identity, label, kind, row, old, evidence):
        if identity in seen:
            return
        seen.add(identity)
        progress = row['value'].get('milestone_progress', {})
        minutes = progress.get('game_minutes', row['value'].get('game_minutes'))
        prior = old['value'].get('milestone_progress', {}) if old else {}
        nodes.append({'identity': identity, 'label': label, 'kind': kind,
                      'wall_lower': old['elapsed_ms'] / 1000 if old else None,
                      'wall_upper': row['elapsed_ms'] / 1000,
                      'game_lower': prior.get('game_minutes'), 'game_upper': minutes,
                      'evidence': evidence, 'status': 'observed_transition'})

    content = json.loads((ROOT / 'godot_csharp/scenes/Awakening/Script/generated_awakening_content.json').read_text(encoding='utf-8'))
    evidence_names = {key: value['name'] for key, value in content['EVIDENCE'].items()}
    for row in records:
        progress = row['value'].get('milestone_progress', {})
        if not progress:
            continue
        for key in progress.get('examined', {}):
            add('inspection:' + key, evidence_names.get(key, key), 'inspection', row, previous, 'examined:' + key)
        for key in progress.get('experiments', {}):
            add('verification:' + key, '验证 ' + evidence_names.get(key, key), 'verification', row, previous, 'experiments:' + key)
        step = progress.get('answer', {}).get('step', 1)
        old_step = previous['value'].get('milestone_progress', {}).get('answer', {}).get('step', 1) if previous else 1
        for index in range(old_step, min(int(step), 5)):
            add('answer:' + str(index), '铜口第' + str(index) + '答被接受', 'answer', row, previous, 'answer.step:' + str(step))
        replay = progress.get('replay_result', {})
        findings = replay.get('findings', [])
        if replay.get('full_match') is True and findings and all(f.get('status') == 'match' for f in findings):
            add('replay:full_match', '正式推演全部吻合', 'replay', row, previous, 'replay_result + examined findings')
        previous = row

    # Retain actual proofs for separate correctness/contradiction review. Do not
    # manufacture a belief by evaluating a query in this measurement process.
    proofs = [{'sequence': r['sequence'], 'wall_seconds': r['elapsed_ms'] / 1000,
               'game_time': r['value'].get('GameTime'), 'arguments': r['value'].get('Arguments', [])}
              for r in events if r['kind'] == 'decision' and r['value'].get('Arguments')]
    output = {'raw_sha256': {'trajectory': hashlib.sha256(trajectory.read_bytes()).hexdigest(),
                            'diagnostics': hashlib.sha256(diagnostic.read_bytes()).hexdigest()},
              'nodes': nodes, 'proofs': proofs,
              'missing': [k for k in ['answer:1', 'answer:2', 'answer:3', 'answer:4', 'replay:full_match'] if k not in seen],
              'note': 'Times are observed sampling bounds, not speed estimates. Inspect screenshots and receipts; proofs require separate review.'}
    (run / 'milestones.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
    with (run / 'milestones.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(nodes[0]) if nodes else ['identity', 'status'])
        writer.writeheader()
        writer.writerows(nodes)
    plt.rcParams['font.family'] = FontProperties(fname='C:/Windows/Fonts/msyh.ttc').get_name()
    fig, axes = plt.subplots(2, 1, figsize=(12, 8), sharex=True)
    start = next((r['value']['milestone_progress']['game_minutes'] for r in records
                  if 'game_minutes' in r['value'].get('milestone_progress', {})), None)
    for ax, key, label in zip(axes, ['game_upper', 'wall_upper'], ['游戏内累计分钟（上界）', '现实累计秒（上界）']):
        if nodes:
            values = [n[key] - start if key == 'game_upper' else n[key] for n in nodes]
            ax.plot(range(len(nodes)), values, marker='o', label=run.name)
        else:
            ax.text(.5, .5, '尚无可核验节点', transform=ax.transAxes, ha='center')
        ax.set_ylabel(label)
        ax.grid(alpha=.25)
    axes[-1].set_xticks(range(len(nodes)), [n['label'] for n in nodes], rotation=30, ha='right')
    fig.suptitle('真实关键进展耗时；未达成节点不绘制为零')
    fig.tight_layout()
    fig.savefig(run / 'milestone-times.png', dpi=160)
    print(json.dumps({'nodes': nodes, 'not_observed': output['missing']}, ensure_ascii=False))


if __name__ == '__main__':
    main()
