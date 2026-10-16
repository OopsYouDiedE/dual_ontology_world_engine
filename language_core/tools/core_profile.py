"""Summarize opt-in generated-DV call timing; no session logic or decisions."""
import argparse
from collections import defaultdict
import json
from pathlib import Path
import re


def work_type(name):
    if name in ('dvs_capture_heard_utterances', 'dvs_capture_public_topics'):
        return 'heard-event-projection'
    if name in ('dvs_support_parents', 'dvs_selection_parents', 'dvs_topic_origin_speakers'):
        return 'evidence-provenance'
    if name in ('dv_get', 'dv_find', 'dv_has', 'dv_has_key', 'dv_same', 'dv_and', 'dv_or', 'dv_record_like'):
        return 'record-lookup-and-comparison'
    if name in ('dv_with', 'dv_with_many', 'dv_with_path', 'dv_clone', 'dv_append', 'dv_pair', 'dv_single'):
        return 'value-copy-and-update'
    if name.startswith('dvp_'):
        return 'planning'
    if name.startswith(('dvs_sim_', 'dvs_model_', 'dvs_cached_model')):
        return 'modeling'
    if name.startswith(('dvs_reason_', 'dvs_infer_')):
        return 'reasoning'
    if name.startswith(('dvs_speech_', 'dvs_prepare_speech_', 'dvs_listener_')):
        return 'speech'
    if name.startswith(('dvg_', 'dvs_session_', 'dvs_environment', 'dvs_owned_state_')):
        return 'orchestration'
    if name.startswith(('dv_predict', 'dv_infer', 'dv_enforce', 'dv_stage', 'dv_truth')):
        return 'laws'
    if name.startswith(('dv_', 'dvs_')):
        return 'core-or-session-other'
    return 'content-or-other'


def summarize(raw_path, modules, destination):
    raw = json.loads(Path(raw_path).read_text(encoding='utf-8'))
    frequency = raw['frequency']
    locations = {}
    for path, source in modules:
        for match in re.finditer(r'^fn\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(', source, re.MULTILINE):
            locations[match.group(1)] = str(path)
    functions = []
    grouped = defaultdict(lambda: {'calls': 0, 'exclusive_ticks': 0})
    for name, data in raw['functions'].items():
        calls = data['Calls']
        ticks = data['ExclusiveTicks']
        kind = work_type(name)
        grouped[kind]['calls'] += calls
        grouped[kind]['exclusive_ticks'] += ticks
        functions.append({'name': name, 'source': locations.get(name), 'work_type': kind,
                          'calls': calls, 'exclusive_ms': ticks * 1000 / frequency,
                          'mean_exclusive_ms': ticks * 1000 / frequency / calls,
                          'inclusive_ms': data['InclusiveTicks'] * 1000 / frequency,
                          'mean_inclusive_ms': data['InclusiveTicks'] * 1000 / frequency / calls})
    total = sum(item['exclusive_ticks'] for item in grouped.values())
    types = [{'work_type': kind, 'calls': item['calls'],
              'exclusive_ms': item['exclusive_ticks'] * 1000 / frequency,
              'mean_exclusive_ms_per_call': item['exclusive_ticks'] * 1000 / frequency / item['calls'],
              'share_of_profiled_dv_percent': item['exclusive_ticks'] * 100 / total if total else 0}
             for kind, item in grouped.items()]
    result = {'basis': 'Generated DV function calls; exclusive time includes direct platform ABI calls and profiler overhead. Work types are function-name groups; nested calls are charged to their own group. This is an instrumented formal-model run, not gameplay acceptance.',
              'process_elapsed_ms': raw['process_ticks'] * 1000 / frequency,
              'profiled_dv_exclusive_ms': total * 1000 / frequency,
              'work_types': sorted(types, key=lambda item: -item['exclusive_ms']),
              'functions': sorted(functions, key=lambda item: -item['exclusive_ms'])}
    Path(destination).write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('raw', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    summarize(args.raw, [], args.out)
