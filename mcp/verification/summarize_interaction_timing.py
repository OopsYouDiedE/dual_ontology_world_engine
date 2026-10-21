"""Pair monotonic host phases within their command and materialization identity."""
from __future__ import annotations

import argparse
from collections import defaultdict
import json
from pathlib import Path
from binary_evidence import records, RecordWriter

INTERVALS = {
    'queue_ms': ('command_received', 'command_started'),
    'prepare_ms': ('prepare_begin', 'prepare_end'),
    'snapshot_ms': ('snapshot_begin', 'snapshot_end'),
    'execution_ms': ('execution_begin', 'execution_end'),
    'complete_ms': ('complete_begin', 'complete_end'),
    'input_to_first_frame_ms': ('input_request', 'first_frame_drawn'),
    'input_to_local_choices_ms': ('input_request', 'local_choices_frame'),
    'local_motion_ms': ('local_motion_begin', 'local_motion_end'),
    'intent_projection_ms': ('dv_intent_projection_begin', 'dv_intent_projection_end'),
    'snapshot_projection_ms': ('dv_snapshot_projection_begin', 'dv_snapshot_projection_end'),
    'materialization_ms': ('materialization_begin', 'materialization_end'),
    'dv_materialization_ms': ('dv_materialization_begin', 'dv_materialization_end'),
}


def details(row):
    value = row.get('details', {})
    if isinstance(value, str):
        try:
            value = json.loads(value)
        except (ValueError, TypeError):
            return {'label': value}
    if not isinstance(value, dict):
        return {}
    inner = value.get('details')
    if isinstance(inner, dict):
        return {**value, **inner}
    return value


def materialization(row):
    data = details(row)
    for key in ('materialization_kind', 'materialization', 'projection_kind',
                'projection', 'snapshot_kind', 'part'):
        value = row.get(key, data.get(key))
        if value is not None:
            return str(value)
    stage = row.get('stage', '')
    if 'projection' in stage or 'materialization' in stage:
        return str(data.get('label', ''))
    return ''


def summarize(rows):
    grouped = defaultdict(list)
    for row in rows:
        identifier = row.get('interaction_id', '')
        if identifier:
            grouped[identifier].append(row)
    result = []
    for identifier, interaction_rows in grouped.items():
        spans = []
        unmatched = []
        for name, (begin, end) in INTERVALS.items():
            pending = defaultdict(list)
            for row in interaction_rows:
                stage = row.get('stage')
                if stage not in (begin, end):
                    continue
                # Host commands and repeated viewer/executor/intent conversions
                # are separate clocks even when they share one interaction id.
                scope = (row.get('command_id', 0), materialization(row))
                if stage == begin:
                    pending[scope].append(row)
                elif pending[scope]:
                    start = pending[scope].pop()
                    duration = row['real_us'] - start['real_us']
                    if duration < 0:
                        unmatched.append({'interval': name, 'reason': 'nonmonotonic_pair',
                                          'begin': start, 'end': row})
                        continue
                    spans.append({'interval': name, 'command_id': scope[0],
                                  'materialization': scope[1],
                                  'presentation_id': details(row).get('presentation_id', ''),
                                  'begin_us': start['real_us'], 'end_us': row['real_us'],
                                  'milliseconds': duration / 1000})
                else:
                    unmatched.append({'interval': name, 'reason': 'end_without_matching_begin', 'stage': row})
            for pending_rows in pending.values():
                unmatched.extend({'interval': name, 'reason': 'begin_without_matching_end', 'stage': row}
                                 for row in pending_rows)
        values = {}
        for name in INTERVALS:
            values_for_name = [span['milliseconds'] for span in spans if span['interval'] == name]
            values[name] = values_for_name[0] if len(values_for_name) == 1 else None
        result.append({'interaction_id': identifier, 'milliseconds': values,
                       'intervals': spans, 'unmatched': unmatched, 'stages': interaction_rows})
    return {'schema': 'command-materialization-timing-v2',
            'clock': 'GameEngine Stopwatch, real microseconds; independent of playback speed',
            'pairing': 'interaction_id + command_id + materialization; repeated occurrences retained',
            'missing': 'null means no unique matched interval; cross-command latency without explicit correlation remains unknown',
            'interactions': result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('--out', required=True, type=Path)
    args = parser.parse_args()
    if args.out.exists():
        parser.error('Use a new timing summary path')
    with RecordWriter(args.out) as output:
        output.append(summarize(records(args.source)))


if __name__ == '__main__':
    main()
