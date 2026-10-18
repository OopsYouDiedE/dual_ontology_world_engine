"""Read recorded packets once; export actual actions without private planning."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path


def record(value):
    if isinstance(value, list):
        if value and all(isinstance(p, list) and len(p) == 2 and isinstance(p[0], str) for p in value):
            return {p[0]: record(p[1]) for p in value}
        return [record(p) for p in value]
    return value


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('case', type=Path)
    args = parser.parse_args()
    case = args.case.resolve()
    source = case / 'stdout.jsonl'
    digest = hashlib.sha256()
    lineage = {}
    first = {}
    accepted, rejected, models, arguments = [], [], [], []
    last = None
    last_actor = {}
    repetitions = Counter()
    issues = []
    with source.open('rb') as stream, (case / 'public-actions.jsonl').open('x', encoding='utf-8') as public:
        for line, raw in enumerate(stream, 1):
            digest.update(raw)
            try:
                packet = record(json.loads(raw))
            except (ValueError, UnicodeDecodeError) as error:
                issues.append({'line': line, 'error': str(error)})
                continue
            if packet.get('type') != 'debug_events':
                continue
            for index, event in enumerate(packet.get('batch', {}).get('events', [])):
                detail = event.get('detail') or {}
                kind = event['type']
                action = event.get('action_id') or ''
                if not action:
                    for parent in event.get('parents', []):
                        if lineage.get(parent):
                            action = lineage[parent]
                            break
                lineage[event['event_id']] = action
                item = {'event_id': event['event_id'], 'type': kind, 'actor': event.get('actor'),
                        'game_time': event.get('game_time'), 'action_id': action,
                        'label': event.get('label'), 'source_line': line, 'batch_event_index': index,
                        'raw_event_ref': event.get('raw_event_ref')}
                if kind == 'action_execution':
                    item['status'] = detail.get('status')
                    last = item
                    last_actor[event.get('actor')] = item
                    if item['status'] == 'applied':
                        first.setdefault(action, item)
                        repetitions[(event.get('actor'), action)] += 1
                if kind == 'answer_accepted':
                    accepted.append(item)
                if kind == 'answer_rejected':
                    rejected.append(item)
                if kind in ('mind_simulation', 'model_recheck'):
                    result = detail.get('result') or {}
                    models.append({**item, 'hypothesis_id': detail.get('hypothesis_id'),
                                   'calculation_scope': detail.get('calculation_scope'),
                                   'status': result.get('status'), 'plan_complete': result.get('plan_complete'),
                                   'stop_reason': result.get('stop_reason'), 'fit': result.get('fit'),
                                   'comparisons_present': 'comparisons' in result,
                                   'predictions_present': 'predictions' in result,
                                   'result_source': {'source_line': line, 'batch_event_index': index, 'path': 'detail.result'}})
                if kind in ('model_argument_accepted', 'model_argument_unavailable'):
                    result = detail.get('result') or {}
                    arguments.append({**item, 'reason': result.get('reason'), 'argument': result.get('argument')})
                if kind in ('action_execution', 'answer_submitted', 'answer_accepted', 'answer_rejected',
                            'speech_literal', 'speech_unheard', 'speech_received_pending', 'chapter_content_available',
                            'advice_decision', 'advice_decision_unavailable'):
                    item['actual_detail'] = {key: detail[key] for key in ('status', 'accepted', 'reason', 'literal_truth', 'chapter', 'movement', 'source_event') if key in detail}
                    public.write(json.dumps(item, ensure_ascii=False) + '\n')
    summary = {'scope': 'recorded packet projection; no inferred missing events or narrative completion',
               'source': str(source), 'source_sha256': digest.hexdigest(), 'invalid_lines': issues,
               'last_execution': last, 'last_execution_by_actor': last_actor,
               'accepted_answers': accepted, 'rejected_answers': rejected,
               'first_applied_action': first, 'model_calculations': models,
               'model_arguments': arguments,
               'repeated_actual_actions': [{'actor': actor, 'action_id': action, 'occurrences': count}
                                           for (actor, action), count in repetitions.most_common() if count > 1]}
    with (case / 'case-summary.json').open('x', encoding='utf-8') as output:
        json.dump(summary, output, ensure_ascii=False, indent=2)
    print(json.dumps({'case': case.name, 'last_execution': last, 'accepted_answers': accepted,
                      'result': json.loads((case / 'result.json').read_text(encoding='utf-8'))}, ensure_ascii=False))


if __name__ == '__main__':
    main()
