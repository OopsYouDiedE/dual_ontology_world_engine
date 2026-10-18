"""Small read-only evidence summaries; no execution, state reconstruction or oracle."""
import hashlib
import json
from pathlib import Path

TASK = Path(__file__).resolve().parent


def scalar(value):
    return value if isinstance(value, (str, int, float, bool, type(None))) else {'kind': type(value).__name__}


def main():
    matrix = TASK / 'counterfactual-final'
    summaries = []
    for case in sorted(p for p in matrix.iterdir() if p.is_dir()):
        result = json.loads((case / 'result.json').read_text(encoding='utf-8'))
        summary = {'configuration': case.name, 'result': result, 'last_event': None,
                   'detective_actions': [], 'answers': [], 'chapter_events': [],
                   'model_stops': {}, 'model_outcomes': {}, 'model_examples': {},
                   'source_files': {}}
        for name in ('result.json', 'evidence-summary.json', '02-引擎输出的玩家可见动作.txt'):
            summary['source_files'][name] = hashlib.sha256((case / name).read_bytes()).hexdigest()
        with (case / 'actual-event-index.jsonl').open(encoding='utf-8') as source:
            for line in source:
                e = json.loads(line)
                pointer = {k: e.get(k) for k in ('source_line', 'batch_event_index', 'event_id', 'game_time')}
                summary['last_event'] = pointer
                if e['actor'] == 'detective' and e['type'] == 'action_execution':
                    summary['detective_actions'].append({**pointer, 'action_id': e['action_id'], 'kind': e['action_kind'], 'status': e['execution_status']})
                if e['type'] in ('answer_submitted', 'answer_accepted', 'answer_rejected', 'question_transition'):
                    summary['answers'].append({**pointer, 'actor': e['actor'], 'type': e['type'], 'action_id': e['action_id'], 'parents': e['parents'], 'detail': e['detail']})
                if e['type'] == 'chapter_content_available':
                    summary['chapter_events'].append({**pointer, 'detail': e['detail']})
        with (case / 'reasoning-evidence.jsonl').open(encoding='utf-8') as source, \
                (case / 'model-evidence-compact.jsonl').open('x', encoding='utf-8') as output:
            for line in source:
                e = json.loads(line)
                if e.get('actor') != 'detective':
                    continue
                detail = e['projection']['detail']
                hypothesis = detail.get('hypothesis')
                result_model = detail.get('result', {})
                if not isinstance(result_model, dict):
                    result_model = {}
                hypothesis_id = hypothesis.get('id') if isinstance(hypothesis, dict) else hypothesis
                compact = {k: e.get(k) for k in ('source_line', 'batch_event_index', 'event_id', 'game_time', 'type')}
                compact.update({'hypothesis_id': hypothesis_id,
                    'result': {key: result_model.get(key) for key in ('status', 'plan_complete', 'fit', 'stop_reason', 'reason', 'expanded', 'remaining', 'replay_depth_limit', 'replay_stages_expanded', 'replay_depth_exhausted')},
                    'predictions': [{'prediction': p.get('prediction'), 'covered': p.get('covered'), 'confidence': p.get('confidence'), 'proposition': p.get('proposition')} for p in result_model.get('predictions', [])],
                    'comparisons': [{'result': p.get('result'), 'prediction': scalar(p.get('prediction')), 'observation': scalar(p.get('observation')), 'confidence': p.get('confidence')} for p in result_model.get('comparisons', [])],
                    'trace': [{key: node.get(key) for key in ('step', 'at', 'stage', 'law', 'status', 'reason')} for node in result_model.get('trace', [])],
                    'source_adoptions_present': bool(detail.get('source_adoptions')),
                    'competitors_present': bool(detail.get('competitors')),
                    'argument_keys': list(result_model.get('argument', {})) if isinstance(result_model.get('argument'), dict) else [],
                    'scope': 'subjective recorded computation; raw event is authoritative for omitted fields'})
                output.write(json.dumps(compact, ensure_ascii=False) + '\n')
                stop = str(result_model.get('stop_reason', result_model.get('reason', e['type'])))
                summary['model_stops'][stop] = summary['model_stops'].get(stop, 0) + 1
                identity = str((e['type'], result_model.get('status'), result_model.get('plan_complete')))
                summary['model_outcomes'][identity] = summary['model_outcomes'].get(identity, 0) + 1
                example_key = str((stop, result_model.get('plan_complete'), bool(result_model.get('predictions'))))
                summary['model_examples'].setdefault(example_key, compact)
        summaries.append(summary)
    with (TASK / 'final-recorded-evidence.json').open('x', encoding='utf-8') as output:
        json.dump({'scope': 'recorded events only; no inferred narrative completion', 'cases': summaries}, output, ensure_ascii=False, indent=2)


if __name__ == '__main__':
    main()
