"""Read frozen binary evidence; write new binary projections without rerunning games."""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / 'language_core/tools'))
from binary_evidence import records, RecordWriter


def details(row):
    d = row.get('details', {})
    if isinstance(d, str):
        try: d = json.loads(d)
        except ValueError: return {}
    return d.get('details', d) if isinstance(d, dict) else {}


def summarize(run):
    rows = list(records(run / 'run/live-timing.dvlog'))
    locks, current = [], None
    for row in rows:
        if row['stage'] != 'input_gate_state': continue
        d = details(row)
        if not d['input_enabled'] and current is None:
            current = {'begin_us': row['real_us'], 'initial_state': d}
        elif d['input_enabled'] and current is not None:
            current['end_us'] = row['real_us']
            current['duration_ms'] = (current['end_us'] - current['begin_us']) / 1000
            locks.append(current)
            current = None
    for lock in locks:
        subset = [r for r in rows if lock['begin_us'] <= r['real_us'] <= lock['end_us']]
        starts = [r for r in subset if r['stage'] == 'execution_begin']
        own = [r for r in starts if details(r).get('actor') == 'detective']
        lock['executions'] = [{'at_us': r['real_us'], **details(r)} for r in starts]
        lock['other_executions'] = len(starts) - len(own)
        lock['own_actions'] = [details(r).get('intent_action') for r in subset if r['stage'] == 'input_gate_state' and details(r).get('intent_actor') == 'detective']
        lock['own_actions'] = list(dict.fromkeys(lock['own_actions']))
        lock['physical_arrivals'] = [{'at_us': r['real_us'], **details(r)} for r in subset if r['stage'] == 'physical_location' and details(r).get('actor') == 'detective']
        lock['map_switching'] = any(details(r).get('map_switching', False) for r in subset if r['stage'] == 'input_gate_state')
        lock['own_execution_spans'] = []
        for start in own:
            event = details(start).get('action_event')
            end = next((r for r in subset if r['stage'] == 'execution_end' and details(r).get('action_event') == event), None)
            if end:
                lock['own_execution_spans'].append({'begin_us': start['real_us'], 'end_us': end['real_us'], 'duration_ms': (end['real_us']-start['real_us'])/1000})
        lock['frame_gaps'] = [{'at_us': r['real_us'], **details(r)} for r in subset if r['stage'] == 'frame_gap']
    result = {'scope': 'Frozen instrumented window; locks are observed drawing boundaries, not input-to-photon latency or CPU attribution.',
              'locks': locks, 'unfinished_lock': current}
    out = run / 'input-gates-summary.dvlog'
    if out.exists(): raise FileExistsError(out)
    with RecordWriter(out) as writer: writer.append(result)
    for lock in locks:
        print(run.name, round(lock['begin_us']/1e6, 3), round(lock['duration_ms'], 3), lock['own_actions'], 'other executions', lock['other_executions'], 'own execution ms', [round(x['duration_ms'],3) for x in lock['own_execution_spans']], 'map switch', lock['map_switching'])


if __name__ == '__main__':
    summarize(Path(sys.argv[1]))
