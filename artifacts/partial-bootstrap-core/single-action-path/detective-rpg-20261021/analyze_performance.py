"""Offline derived timing summary; never launches a session or changes sources."""
import collections
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / 'language_core/tools'))
from binary_evidence import RecordWriter, records

run = Path(sys.argv[1]).resolve()
rows = list(records(run / 'host-session/interaction-timing.dvlog'))
pairs = {
    'command_received': 'command_started', 'open_begin': 'open_end',
    'prepare_begin': 'prepare_end', 'complete_begin': 'complete_end',
    'snapshot_begin': 'snapshot_end', 'dv_cognition-start': 'dv_cognition-end',
    'dv_choice-start': 'dv_choice-end', 'dv_schedule-start': 'dv_schedule-end',
    'dv_goals-start': 'dv_goals-end',
    'dv_snapshot_projection_begin': 'dv_snapshot_projection_end',
    'dv_host_json_begin': 'dv_host_json_end', 'materialize_begin': 'materialize_end',
    'handoff_begin': 'handoff_end', 'view_build_begin': 'view_build_end',
    'ui_apply_begin': 'ui_apply_end', 'visible_trace_begin': 'visible_trace_end',
    'capture_begin': 'capture_readback_end', 'capture_readback_end': 'capture_png_end',
    'execution_begin': 'execution_end', 'presentation_begin': 'reading_finished',
}
global_stages = {'execution_begin', 'presentation_begin', 'visible_trace_begin',
                 'ui_apply_begin', 'capture_begin', 'capture_readback_end', 'view_build_begin'}
stacks = collections.defaultdict(list)
values = collections.defaultdict(list)
by_details = collections.defaultdict(list)
ends = {end: begin for begin, end in pairs.items()}
for row in rows:
    stage = row['stage']
    command = row.get('command_id', 0)
    if stage in ends:
        begin = ends[stage]
        key = (begin, 0 if begin in global_stages else command)
        if stacks[key]:
            start = stacks[key].pop()
            ms = (row['real_us'] - start['real_us']) / 1000
            values[begin].append(ms)
            by_details[(begin, str(start.get('details', '')))].append(ms)
    if stage in pairs:
        stacks[(stage, 0 if stage in global_stages else command)].append(row)

def summary(numbers):
    ordered = sorted(numbers)
    return {'count': len(ordered), 'total_ms': sum(ordered),
            'p50_ms': statistics.median(ordered),
            'p95_nearest_rank_ms': ordered[max(0, (95 * len(ordered) + 99) // 100 - 1)],
            'max_ms': max(ordered)}

archive = list(records(run / 'run/archive-timing.dvlog'))
archive_summaries = {}
for name, begin, end in [('queue', 'queued_us', 'begin_us'),
                         ('png_encode', 'begin_us', 'encoded_us'),
                         ('write', 'encoded_us', 'written_us'),
                         ('hash_notify', 'written_us', 'end_us')]:
    numbers = [(r[end] - r[begin]) / 1000 for r in archive if begin in r and end in r]
    if numbers:
        archive_summaries[name] = summary(numbers)
result = {
    'scope': 'Derived exclusive-category totals are not computed. Nested/concurrent spans must not be summed. All durations are monotonic wall time, not CPU attribution.',
    'pairing': 'LIFO by command_id for worker phases; global LIFO for serialized main-thread spans; details retained as separate aggregate groups.',
    'stages': {k: summary(v) for k, v in values.items()},
    'by_details': [{'stage': k[0], 'details': k[1], **summary(v)} for k, v in by_details.items()],
    'unfinished': [r for stack in stacks.values() for r in stack],
    'archive': archive_summaries,
}
out = run / 'phase-summary.dvlog'
if out.exists():
    raise SystemExit('Refusing to overwrite derived evidence')
with RecordWriter(out) as writer:
    writer.append(result)
print(out)
