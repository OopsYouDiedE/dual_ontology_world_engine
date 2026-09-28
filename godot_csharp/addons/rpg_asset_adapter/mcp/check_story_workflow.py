"""Checks parser boundaries, safe text-only patching, and declared-knowledge sequencing."""
import hashlib
import json
import tempfile
from pathlib import Path

import story_workflow as story


def raises(call, fragment):
    try:
        call()
        raise AssertionError(f'expected ValueError containing {fragment!r}')
    except ValueError as exc:
        assert fragment in str(exc), str(exc)


def main():
    actual = story.inspect_story()
    assert actual['labels'][0] == 'opening'
    assert actual['safe_to_apply'] and not actual['unknown_syntax'] and not actual['unresolved_jumps']
    assert all('text' in entry for entry in actual['source_lines'])
    assert any(row['kind'] == 'choice' for row in actual['entries'])
    assert any(row['kind'] == 'action' for row in actual['entries'])
    contract = story.story_design_contract()
    assert set(contract['sources']) == {'current_dialogue', 'observation_cards', 'questioning_review', 'reasoning_design_review'}
    assert contract['sources']['current_dialogue']['sha256'] == actual['sha256']
    assert all(len(source['sha256']) == 64 for source in contract['sources'].values())
    assert contract['character_card_template'] == dict(id='', want='', secret='', voice='', known_facts=[])

    original_repo = story.workflow.REPO
    with tempfile.TemporaryDirectory() as temp:
        root = Path(temp)
        story.workflow.REPO = root
        scene_dir = root / 'godot_csharp' / 'scenes' / 'Sample'
        scene_dir.mkdir(parents=True)
        safe = scene_dir / 'sample.dialogue'
        safe_text = '~ start\n侦探: 原文[wait]\\! {{name}} $player\n- 原选项\n\t=> start\n'
        safe.write_text(safe_text, encoding='utf-8', newline='')
        digest = hashlib.sha256(safe.read_bytes()).hexdigest()
        inspection = story.inspect_story('godot_csharp/scenes/Sample/sample.dialogue')
        assert inspection['safe_to_apply']
        fingerprint = inspection['control_fingerprint']
        expected = '侦探: 原文[wait]\\! {{name}} $player'
        replacement = '侦探: 新文[wait]\\! {{name}} $player'
        change = [dict(line=2, expected=expected, replacement=replacement)]
        preview = story.review_story_revision('godot_csharp/scenes/Sample/sample.dialogue', change, digest)
        assert preview['control_preserved'] and preview['patch'][0]['new'] == replacement
        applied = story.apply_story_revision('godot_csharp/scenes/Sample/sample.dialogue', change, digest)
        assert applied['control_fingerprint'] == fingerprint
        assert '侦探: 新文[wait]\\!' in safe.read_text(encoding='utf-8')
        raises(lambda: story.apply_story_revision('godot_csharp/scenes/Sample/sample.dialogue', change, digest), 'stale')

        fresh_hash = hashlib.sha256(safe.read_bytes()).hexdigest()
        current_line = '侦探: 新文[wait]\\! {{name}} $player'
        raises(lambda: story.review_story_revision('godot_csharp/scenes/Sample/sample.dialogue',
              [dict(line=2, expected=current_line, replacement='侦探: injected\n~ injected')], fresh_hash), 'single-line')
        raises(lambda: story.review_story_revision('godot_csharp/scenes/Sample/sample.dialogue',
              [dict(line=2, expected='侦探: stale[wait]\\!', replacement=current_line)], fresh_hash), 'mismatch')
        raises(lambda: story.review_story_revision('godot_csharp/scenes/Sample/sample.dialogue',
              [dict(line=2, expected=current_line, replacement='侦探: 新文[if]\\! {{name}} $player')], fresh_hash), 'protected tags')
        raises(lambda: story.review_story_revision('godot_csharp/scenes/Sample/sample.dialogue',
              [dict(line=2, expected=current_line, replacement='侦探: 新文[wait]\\! {{other}} $player')], fresh_hash), 'protected tags')
        raises(lambda: story.review_story_revision('godot_csharp/scenes/Sample/sample.dialogue',
              [dict(line=3, expected='- 原选项', replacement='- 新选项')], fresh_hash), 'read-only')
        raises(lambda: story.review_story_revision('godot_csharp/scenes/Sample/sample.dialogue',
              [dict(line=2, expected=current_line, replacement=current_line+' => END')], fresh_hash), 'protected tags')
        raises(lambda: story.inspect_story('README.md'), 'under godot_csharp/scenes')

        unknown = scene_dir / 'unknown.dialogue'
        unknown.write_text('~ start\n侦探: 可审阅\nset something = true\n=> absent_target\n', encoding='utf-8')
        unknown_path = 'godot_csharp/scenes/Sample/unknown.dialogue'
        unknown_inspect = story.inspect_story(unknown_path)
        assert unknown_inspect['unknown_syntax'][0]['line'] == 3
        assert unknown_inspect['unresolved_jumps'] == ['absent_target']
        unknown_hash = hashlib.sha256(unknown.read_bytes()).hexdigest()
        unknown_preview = story.review_story_revision(unknown_path,
            [dict(line=2, expected='侦探: 可审阅', replacement='侦探: 可预览')], unknown_hash)
        assert not unknown_preview['safe_to_apply']
        raises(lambda: story.apply_story_revision(unknown_path,
              [dict(line=2, expected='侦探: 可审阅', replacement='侦探: 可预览')], unknown_hash), 'apply is blocked')
        branches = scene_dir / 'branches.dialogue'
        branches.write_text('~ branch\nif ready:\n  侦探: 条件成立\nelif waiting:\n  do run_wait()\nelse:\n  => branch\n', encoding='utf-8')
        branch_parse = story.inspect_story('godot_csharp/scenes/Sample/branches.dialogue')
        assert [row['branch'] for row in branch_parse['controls'] if row['kind'] == 'branch'] == ['if', 'elif', 'else']
        assert any(row['kind'] == 'action' for row in branch_parse['controls'])
        stages = scene_dir / 'stages.dialogue'
        stages.write_text('~ engineer_0\n侦探: 学园\n=> ending_history\n~ ending_history\n侦探: 学园\n', encoding='utf-8')
        stages_hash = hashlib.sha256(stages.read_bytes()).hexdigest()
        stage_preview = story.review_story_revision('godot_csharp/scenes/Sample/stages.dialogue',
            [dict(line=5, expected='侦探: 学园', replacement='侦探: 旧校舍')], stages_hash)
        assert {warning['label'] for warning in stage_preview['context_warnings']} == {'engineer_0'}

    story.workflow.REPO = original_repo
    initial = story.review_story_beats([
        dict(kind='hypothesis', character='detective', goal='Explain a mark', action='imagines its source',
             information='The mark resembles a latch trace.', claims=['outside opened the door'],
             acquires=['outside opened the door'], presented_as_fact=True),
        dict(kind='observation', character='detective', goal='Inspect latch', action='touches the latch',
             information='The latch is brass.', acquires=['latch is brass']),
        dict(kind='rule_result', character='detective', goal='Test latch', action='moves the bar',
             information='The door opens inward.', claims=['latch is brass'])],
        [dict(id='detective', known_facts=['door is locked'])])
    assert any(issue['kind'] == 'unknown_to_character' for issue in initial['issues'])
    assert any(issue['kind'] == 'hypothesis_presented_as_fact' for issue in initial['issues'])
    assert any(issue['kind'] == 'hypothesis_cannot_acquire_fact' for issue in initial['issues'])
    assert any(issue['kind'] == 'missing_mechanism_source' for issue in initial['issues'])
    assert 'outside opened the door' not in initial['final_known_facts']['detective']
    assert 'latch is brass' in initial['final_known_facts']['detective']
    clean = story.review_story_beats([
        dict(kind='observation', character='detective', goal='Inspect latch', action='touches it',
             information='The latch is brass.', acquires=['latch is brass']),
        dict(kind='rule_result', character='detective', goal='Test latch', action='moves it',
             information='The door opens inward.', claims=['latch is brass'], source_ref='Mechanism/door-lock')],
        [dict(id='detective', known_facts=[])])
    assert clean['ok']
    print('story workflow checks passed')


if __name__ == '__main__':
    main()
