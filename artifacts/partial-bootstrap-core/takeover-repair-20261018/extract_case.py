"""Read-only extraction of recorded events, never a session driver or oracle."""
import argparse
import hashlib
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('case', type=Path)
    args = parser.parse_args()
    case = args.case
    observed = {'source': str(case.resolve()), 'scope': 'actual recorded packets only; not inferred completion',
                'detective_executions': [], 'answer_receipts': [], 'chapter_events': [],
                'detective_models': [], 'last_recorded_event': None}
    wanted = {'hypothesis', 'status', 'fit', 'assumptions', 'predictions', 'comparisons',
              'trace', 'coverage', 'expanded', 'remaining', 'stop_reason', 'reason',
              'plan_complete', 'replay_depth_exhausted', 'replay_stages_expanded', 'argument'}
    with (case / 'events.jsonl').open(encoding='utf-8') as source, \
            (case / 'review-public.jsonl').open('x', encoding='utf-8') as public:
        for number, line in enumerate(source, 1):
            packet = json.loads(line)
            if packet.get('type') == 'player_presentation':
                if packet['presentation'].get('visible'):
                    public.write(json.dumps({'source_line': number, **packet}, ensure_ascii=False) + '\n')
            if packet.get('type') != 'debug_events':
                continue
            for index, event in enumerate(packet.get('batch', {}).get('events', [])):
                pointer = {'source_line': number, 'batch_event_index': index,
                           'event_id': event['event_id'], 'game_time': event['game_time']}
                observed['last_recorded_event'] = {**pointer, 'type': event['type']}
                if event['actor'] == 'detective' and event['type'] == 'action_execution':
                    observed['detective_executions'].append({**pointer, 'action_id': event['action_id'],
                        'action_kind': event['action_kind'], 'detail': event['detail'], 'parents': event['parents']})
                if event['type'] in ('answer_submitted', 'answer_accepted', 'answer_rejected', 'question_transition'):
                    observed['answer_receipts'].append({**pointer, 'type': event['type'],
                        'actor': event['actor'], 'action_id': event['action_id'], 'detail': event['detail'], 'parents': event['parents']})
                if event['type'] == 'chapter_content_available':
                    observed['chapter_events'].append({**pointer, 'detail': event['detail']})
                if event['actor'] == 'detective' and event['type'] in ('mind_simulation', 'model_recheck', 'model_argument_accepted', 'model_argument_unavailable', 'factor_prune'):
                    detail = event['detail']
                    result = detail.get('result', {})
                    observed['detective_models'].append({**pointer, 'type': event['type'],
                        'hypothesis': detail.get('hypothesis'),
                        'result': {key: value for key, value in result.items() if key in wanted} if isinstance(result, dict) else result,
                        'source_adoptions_present': bool(detail.get('source_adoptions')),
                        'competitors_present': bool(detail.get('competitors')),
                        'certificates': detail.get('certificates'), 'rejections': detail.get('rejections'),
                        'scope': 'subjective calculation; no actual effect inferred'})
    observed['public_projection_sha256'] = hashlib.sha256((case / 'review-public.jsonl').read_bytes()).hexdigest()
    with (case / 'observed-milestones.json').open('x', encoding='utf-8') as output:
        json.dump(observed, output, ensure_ascii=False, indent=2)
    print(json.dumps({'case': str(case), 'last_recorded_event': observed['last_recorded_event']}))


if __name__ == '__main__':
    main()
