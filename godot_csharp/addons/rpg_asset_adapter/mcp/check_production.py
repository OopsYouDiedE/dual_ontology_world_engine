"""Focused boundary and persistence checks; all plan/evidence files live in tempfile."""
import json
import tempfile
from pathlib import Path

import production


def main():
    result = production.review_dialogue(
        [{'id': 'ana', 'want': 'leave', 'secret': 'none', 'voice': 'plain', 'known_facts': ['door is locked']}],
        [{'speaker': 'ana', 'text': 'The door is locked.', 'action': 'points at door', 'claims': ['door is locked']}],
        pixel_width=320, font_size=16)
    assert result['ok'] and result['pixel_budget_estimate']['max_characters_per_line'] == 20
    assert 'subtext' in result['not_checked'][0]
    flagged = production.review_dialogue(
        [{'id': 'ana', 'want': 'leave', 'secret': '', 'voice': '', 'known_facts': []}],
        [{'speaker': 'ana', 'text': 'I know the secret. 心中一紧', 'claims': ['the secret']}])
    assert {x['kind'] for x in flagged['issues']} == {'knowledge', 'cliche'}
    multiline = production.review_dialogue(
        [{'id': 'ana', 'want': 'leave', 'secret': '', 'voice': '', 'known_facts': []}],
        [{'speaker': 'ana', 'text': 'one\ntwo\nthree\nfour'}])
    assert any(x['kind'] == 'line_count' for x in multiline['issues'])
    long_line = production.review_dialogue(
        [{'id': 'ana', 'want': 'leave', 'secret': '', 'voice': '', 'known_facts': []}],
        [{'speaker': 'ana', 'text': 'x' * 25}])
    assert any(x['kind'] == 'line_length' for x in long_line['issues'])
    try:
        production.review_dialogue([], [], pixel_width=20)
        raise AssertionError('incomplete pixel budget accepted')
    except ValueError:
        pass

    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        production.PLAN_PATH = root / 'artifacts' / 'tilemap-workflow' / 'production-plan.json'
        production.workflow.REPO = root
        source = root / 'deliverable.txt'
        source.write_text('reviewable output', encoding='utf-8')
        production.create_plan('Create a single playable scene', [
            {'id': 'draft', 'goal': 'Draft one scene', 'depends_on': [], 'acceptance': ['scene entry defined'], 'preparation': 'Inspect the scene entry and draft only that scene.'},
            {'id': 'review', 'goal': 'Review the scene', 'depends_on': ['draft'], 'acceptance': ['review recorded']}])
        assert production.read_plan()['goal'] == 'Create a single playable scene'
        assert production.next_task()['tasks'][0]['preparation'].startswith('Inspect')
        assert len(production.next_task()['tasks']) == 1
        try:
            production.update_task('review', status='done')
            raise AssertionError('dependency bypass accepted')
        except ValueError:
            pass
        production.update_task('draft', status='done', acceptance_confirmed=True,
                               evidence_paths=['deliverable.txt'])
        assert production.read_plan()['tasks'][0]['evidence_check'][0]['hash_matches']
        assert production.next_task()['tasks'][0]['id'] == 'review'
        production.update_task('review', status='done', acceptance_confirmed=True,
                               evidence_paths=['deliverable.txt'])
        try:
            production.update_task('draft', status='todo')
            raise AssertionError('completed dependency with completed dependent reopened')
        except ValueError:
            pass
        production.create_plan('Replacement plan', [
            {'id': 'fresh', 'goal': 'One new scene', 'acceptance': ['entry documented']}], replace_completed=True)
        assert production.read_plan()['goal'] == 'Replacement plan'
        production.update_task('fresh', status='done', acceptance_confirmed=True, evidence_paths=['deliverable.txt'])
        try:
            production.update_task('fresh', acceptance_confirmed=False)
            raise AssertionError('done task weakened without revalidation')
        except ValueError:
            pass
        production.PLAN_PATH.unlink()
        production.create_plan('Create a single playable scene', [
            {'id': 'draft', 'goal': 'Draft one scene', 'acceptance': ['scene entry defined']},
            {'id': 'other', 'goal': 'Other completed prerequisite', 'acceptance': ['other recorded']},
            {'id': 'review', 'goal': 'Review the scene', 'depends_on': ['draft'], 'acceptance': ['review recorded']}])
        production.update_task('draft', status='done', acceptance_confirmed=True, evidence_paths=['deliverable.txt'])
        production.update_task('other', status='done', acceptance_confirmed=True, evidence_paths=['deliverable.txt'])
        def mutate_definition(change):
            data = json.loads(production.PLAN_PATH.read_text(encoding='utf-8'))
            change(data)
            production.PLAN_PATH.write_text(json.dumps(data, ensure_ascii=False), encoding='utf-8')
            assert production.read_plan()['tasks'][0]['completion_valid'] is False
            assert 'draft' in production.next_task()['invalid_done_tasks']
            production.update_task('other', acceptance_confirmed=True)
            production.update_task('draft', acceptance_confirmed=True)
            assert production.read_plan()['tasks'][0]['completion_valid'] is True
        mutate_definition(lambda p: p.update(goal='Changed plan goal'))
        mutate_definition(lambda p: p['tasks'][0].update(goal='Changed task goal'))
        mutate_definition(lambda p: p['tasks'][0].update(acceptance=['Changed acceptance']))
        mutate_definition(lambda p: p['tasks'][0].update(depends_on=['other']))
        source.write_text('tampered', encoding='utf-8')
        assert not production.read_plan()['tasks'][0]['evidence_check'][0]['hash_matches']
        assert production.read_plan()['tasks'][0]['completion_valid'] is False
        next_state = production.next_task()
        assert set(next_state['invalid_done_tasks']) == {'draft', 'other'} and not next_state['tasks']
        try:
            production.update_task('review', status='done', acceptance_confirmed=True,
                                   evidence_paths=['deliverable.txt'])
            raise AssertionError('stale completed dependency accepted')
        except ValueError:
            pass
        source.unlink()
        missing_check = production.read_plan()['tasks'][0]['evidence_check'][0]
        assert not missing_check['exists'] and not missing_check['hash_matches']
        assert set(production.next_task()['invalid_done_tasks']) == {'draft', 'other'}
        try:
            production.update_task('draft', evidence_paths=[])
            raise AssertionError('done task cleared evidence')
        except ValueError:
            pass
        try:
            production.update_task('review', status='done', acceptance_confirmed=True,
                                   evidence_paths=['missing.txt'])
            raise AssertionError('missing evidence accepted')
        except ValueError:
            pass
        saved_hash = json.loads(production.PLAN_PATH.read_text(encoding='utf-8'))['tasks'][0]['evidence'][0]['sha256']
        assert len(saved_hash) == 64 and not source.exists()
        cycle_path = production.PLAN_PATH
        cycle_path.unlink()
        try:
            production.create_plan('Cycle test', [
                {'id': 'a', 'goal': 'A', 'depends_on': ['b'], 'acceptance': ['A']},
                {'id': 'b', 'goal': 'B', 'depends_on': ['a'], 'acceptance': ['B']}])
            raise AssertionError('dependency cycle accepted')
        except ValueError as exc:
            assert 'cycle' in str(exc)
    print('production checks passed')


if __name__ == '__main__':
    main()
