import json
from pathlib import Path

root = Path(__file__).resolve().parent
measurements = json.loads((root / 'measurements.json').read_text(encoding='utf-8'))
names = dict(zip(('r20-baseline-90', 'goal-continuity-90', 'route-feedback-90', 'read-performance-90'), ('恢复基线', '目标连续性', '导航反馈', '读图性能')))
out = []
for run in ('r20-baseline-90', 'goal-continuity-90', 'route-feedback-90', 'read-performance-90'):
    rows = [json.loads(line) for line in (root / run / 'raw/trajectory.jsonl').open(encoding='utf-8')]
    out.append('\n## ' + run)
    previous = None
    for row in rows:
        if row['kind'] != 'decision' or row['elapsed_ms'] < measurements[names[run]]['accepted_answers']['1']['real_seconds'] * 1000:
            continue
        v = row['value']
        alternatives = v.get('Alternatives', [])
        chosen = next((a for a in alternatives if a['Token'] == v.get('Token')), None)
        if chosen is None:
            continue
        label = chosen['Label']
        identity = (chosen['Kind'], label if chosen['Kind'] != 'move' else label.split(' ')[0])
        if identity == previous:
            continue
        previous = identity
        out.append(f"{row['elapsed_ms']/1000:.3f}s seq={row['sequence']} game={v.get('GameTime')} CHOSEN {chosen['Kind']} {label} score={chosen['Score']} basis={chosen['Basis']}")
        for argument in v.get('Arguments', []):
            if argument.get('Missing'):
                out.append(f"  MISSING {argument['Proposition']} value={argument['Value']} complete={argument['Complete']} missing={argument['Missing']}")
        for a in sorted(alternatives, key=lambda a: -a['Score'])[:3]:
            out.append(f"  TOP {a['Kind']} {a['Label']} score={a['Score']} basis={a['Basis']}")
        for a in alternatives:
            if any(word in a['Label'] for word in ('铜口', '作答', '重演', '推演', '霍铎', '管接头', '音叉', '回答始业')) and a not in sorted(alternatives, key=lambda a: -a['Score'])[:3]:
                out.append(f"  TARGET {a['Kind']} {a['Label']} score={a['Score']} basis={a['Basis']}")
    out.append('PUBLIC QUESTION/ANSWER TEXT')
    for r in rows:
        if r['kind'] == 'shown' and r['elapsed_ms'] > 35000:
            if any(w in str(r['value']) for w in ('铜口', '声路', '音叉', '墙里的声音', '门外')):
                out.append(f"{r['elapsed_ms']/1000:.3f}s seq={r['sequence']} {r['value']}")
(root / 'preference-trace.txt').write_text('\n'.join(out), encoding='utf-8')
print('\n'.join(out[:100]))
