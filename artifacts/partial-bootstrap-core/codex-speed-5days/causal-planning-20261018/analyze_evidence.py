"""Read this round's immutable evidence; never execute a session."""
import hashlib
import json
from collections import Counter
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / 'language_core/tools'))
from run_debug import record

HERE = Path(__file__).resolve().parent
RUN = HERE / 'run-180s'
packets = []
bad_lines = []
for index, line in enumerate((RUN / 'stdout.jsonl').read_text(encoding='utf-8').splitlines(), 1):
    try:
        packets.append(record(json.loads(line)))
    except json.JSONDecodeError:
        bad_lines.append(index)
events = [event for packet in packets if packet.get('type') == 'debug_events' for event in packet['batch']['events']]
executions = [event for event in events if event['type'] == 'action_execution']
detective = [event for event in events if event['actor'] == 'detective']
frozen = json.loads((HERE / 'frozen-source.json').read_text(encoding='utf-8-sig'))
changed = [path for path, expected in frozen.items() if hashlib.sha256((ROOT / path).read_bytes()).hexdigest() != expected]
summary = {
    'result': json.loads((RUN / 'result.json').read_text(encoding='utf-8')),
    'frozen_source_changed': changed,
    'bad_stdout_lines': bad_lines,
    'last_event': events[-1] if events else None,
    'last_execution': executions[-1] if executions else None,
    'target_game_time': 7200,
    'max_recorded_game_time': max((event['game_time'] for event in events), default=None),
    'event_types': dict(Counter(event['type'] for event in events)),
    'detective_executions': [event for event in detective if event['type'] == 'action_execution'],
    'detective_choices': [event for event in detective if event['type'] == 'planning_choice'],
    'detective_answer_events': [event for event in detective if 'answer' in event['type']],
    'chapter_events': [event for event in events if 'chapter' in event['type']],
    'detective_listener_parse': [event for event in detective if event['type'] == 'listener_parse'],
    'detective_observations': [event for event in detective if event['type'] == 'observation'],
}
(HERE / 'MEASURED-EVIDENCE.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({key: value for key, value in summary.items() if key not in ('detective_executions', 'detective_choices', 'detective_listener_parse', 'detective_observations')}, ensure_ascii=False, indent=2))
print('DETECTIVE EXECUTIONS')
for event in summary['detective_executions']:
    print(event['event_id'], event['game_time'], event['action_id'], event['detail'].get('status'))
