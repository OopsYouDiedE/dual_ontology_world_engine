"""Evidence-only summarizer for one authorized formal-model window."""
import json
import hashlib
import sys
from pathlib import Path
from collections import Counter

here = Path(__file__).resolve().parent
root = here.parents[3]
sys.path.insert(0, str(root / 'language_core/tools'))
from run_debug import record

folder = here / sys.argv[1]
run = folder / 'run'
packets = []
malformed = []
for number, line in enumerate((run / 'stdout.jsonl').read_text(encoding='utf-8').splitlines(), 1):
    try:
        packets.append(record(json.loads(line)))
    except json.JSONDecodeError:
        malformed.append(number)
events = [event for p in packets if p.get('type') == 'debug_events' for event in p['batch']['events']]
execution = [e for e in events if e['type'] == 'action_execution']
choices = [e for e in events if e['type'] == 'planning_choice']
mine = [e for e in execution if e['actor'] == 'detective']
frozen = json.loads((folder / 'frozen-source.json').read_text(encoding='utf-8'))
result = {
    'run_result': json.loads((run / 'result.json').read_text(encoding='utf-8')),
    'changed_files': [path for path, sha in frozen.items() if hashlib.sha256((root / path).read_bytes()).hexdigest() != sha],
    'malformed_stdout_lines': malformed,
    'last_event': events[-1] if events else None,
    'last_execution': execution[-1] if execution else None,
    'last_detective_execution': mine[-1] if mine else None,
    'max_game_time': max((e['game_time'] for e in events), default=None),
    'debug_end': [p for p in packets if p.get('type') == 'debug_end'],
    'detective_executions': mine,
    'detective_answers': [e for e in events if e['actor'] == 'detective' and e['type'] in ('answer_accepted', 'answer_rejected')],
    'choice_stops': dict(Counter(e['detail'].get('stop') for e in choices)),
    'reason_reuse': dict(Counter(str(e['detail'].get('reason_reused')) for e in choices)),
    'events_by_type': dict(Counter(e['type'] for e in events)),
}
profile = run / 'profile-raw.json'
if profile.exists():
    data = json.loads(profile.read_text())
    result['profile_sample_seconds'] = data['process_ticks'] / data['frequency']
    result['profile_top'] = [dict(name=name, calls=values['Calls'], exclusive_seconds=values['ExclusiveTicks']/data['frequency'], inclusive_seconds=values['InclusiveTicks']/data['frequency']) for name, values in sorted(data['functions'].items(), key=lambda pair: -pair[1]['ExclusiveTicks'])[:25]]
(folder / 'MEASURED.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k not in ('detective_executions','debug_end','last_event')}, ensure_ascii=False, indent=2))
