"""Small, file-backed production helpers for dialogue review and task tracking."""
from __future__ import annotations

import hashlib
import json
import os
import re
from pathlib import Path
from typing import Any

import workflow

PLAN_PATH = workflow.REPORT / 'production-plan.json'
_CLICHES = ('突然意识到', '不由得', '心中一紧', '嘴角微微上扬', '空气仿佛凝固', '沉默了片刻', '在这个充满', '这才是真正的命运')


def review_dialogue(characters: list[dict[str, Any]], turns: list[dict[str, Any]],
                    max_lines: int = 3, max_chars_per_line: int = 24, max_turns: int = 200,
                    pixel_width: int | None = None, font_size: int | None = None) -> dict:
    """Check dialogue structure and declared facts; does not infer subtext or measure fonts."""
    if not 1 <= max_lines <= 200 or not 1 <= max_chars_per_line <= 1000 or not 1 <= max_turns <= 200:
        raise ValueError('line limits must be positive')
    if (pixel_width is None) != (font_size is None):
        raise ValueError('pixel_width and font_size must be provided together')
    if pixel_width is not None and (pixel_width < 1 or font_size < 1):
        raise ValueError('pixel_width and font_size must be positive')
    cards = {}
    for card in characters:
        cid = card.get('id')
        if not cid or cid in cards or not all(k in card for k in ('want', 'secret', 'voice', 'known_facts')):
            raise ValueError('each character needs a unique id and want, secret, voice, known_facts')
        cards[cid] = card
    if len(turns) > max_turns:
        raise ValueError(f'turn count exceeds max_turns ({max_turns})')
    issues = []
    for i, turn in enumerate(turns, 1):
        speaker, text = turn.get('speaker'), turn.get('text')
        if speaker not in cards:
            raise ValueError(f'turn {i} has unknown speaker')
        if not isinstance(text, str) or not text.strip():
            raise ValueError(f'turn {i} needs non-empty text')
        lines = text.splitlines() or [text]
        if len(lines) > max_lines:
            issues.append(dict(turn=i, kind='line_count', message=f'{len(lines)} lines exceeds {max_lines}'))
        for line_no, line in enumerate(lines, 1):
            if len(line) > max_chars_per_line:
                issues.append(dict(turn=i, line=line_no, kind='line_length', message=f'{len(line)} characters exceeds {max_chars_per_line}'))
        claims = turn.get('claims', [])
        if not isinstance(claims, list) or any(not isinstance(c, str) for c in claims):
            raise ValueError(f'turn {i} claims must be a list of explicit factual claims')
        facts = cards[speaker]['known_facts']
        if not isinstance(facts, list):
            raise ValueError(f'{speaker}.known_facts must be a list')
        for claim in claims:
            if claim not in facts:
                issues.append(dict(turn=i, kind='knowledge', message=f'{speaker} claim is not in declared known_facts: {claim}'))
        for cliche in _CLICHES:
            if cliche in text:
                issues.append(dict(turn=i, kind='cliche', message=f'common stock phrase; review voice fit: {cliche}'))
    estimate = None
    if pixel_width is not None:
        estimate = dict(max_characters_per_line=max(1, pixel_width // font_size),
                        note='Rough character budget only; proportional glyph widths, shaping and real font metrics are not measured.')
    return dict(ok=not any(x['kind'] in ('line_count', 'line_length', 'knowledge') for x in issues),
                issues=issues, turns=len(turns), lines_per_turn_limit=max_lines, turn_limit=max_turns,
                character_limit=max_chars_per_line, pixel_budget_estimate=estimate,
                checked=['speaker exists', 'explicit claims compared with declared known_facts', 'line and turn limits'],
                not_checked=['subtext, intent, voice quality, truth beyond declared facts, actual font rendering'])


def _load_plan() -> dict:
    if not PLAN_PATH.is_file():
        raise ValueError('production plan does not exist; call create_plan first')
    try:
        plan = json.loads(PLAN_PATH.read_text(encoding='utf-8'))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f'cannot read production plan: {exc}') from exc
    return plan


def _save_plan(plan: dict) -> None:
    PLAN_PATH.parent.mkdir(parents=True, exist_ok=True)
    temporary = PLAN_PATH.with_name(PLAN_PATH.name + '.tmp')
    temporary.write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding='utf-8')
    os.replace(temporary, PLAN_PATH)


def _validate_acyclic(tasks: list[dict]) -> None:
    by_id = {t['id']: t for t in tasks}
    if len(by_id) != len(tasks):
        raise ValueError('task ids must be unique')
    for t in tasks:
        if any(dep not in by_id for dep in t['depends_on']):
            raise ValueError(f"{t['id']} has an unknown dependency")
    visiting, visited = set(), set()
    def visit(tid):
        if tid in visiting: raise ValueError('dependency cycle detected')
        if tid in visited: return
        visiting.add(tid)
        for dep in by_id[tid]['depends_on']: visit(dep)
        visiting.remove(tid); visited.add(tid)
    for tid in by_id: visit(tid)


def create_plan(goal: str, tasks: list[dict[str, Any]], replace_completed: bool = False) -> dict:
    """Create the one project production plan; task goal is the explicit scope boundary."""
    if not isinstance(goal, str) or not goal.strip():
        raise ValueError('plan needs an explicit overall goal')
    if PLAN_PATH.exists():
        previous = _load_plan()
        if not replace_completed or not previous.get('tasks') or any(t.get('status') != 'done' for t in previous['tasks']):
            raise ValueError('existing plan can be replaced only with replace_completed=true after every task is done')
        previous_by_id = {x['id']: x for x in previous['tasks']}
        if any(not _completion_valid(t, previous_by_id, previous) for t in previous['tasks']):
            raise ValueError('completed plan has missing, changed or undeclared acceptance/evidence; cannot replace')
    if not tasks or len(tasks) > 200:
        raise ValueError('plan needs 1..200 tasks')
    normalized = []
    for task in tasks:
        tid, task_goal, acceptance = task.get('id'), task.get('goal'), task.get('acceptance')
        deps = task.get('depends_on', [])
        preparation = task.get('preparation')
        if not isinstance(tid, str) or not re.fullmatch(r'[a-z][a-z0-9_-]{0,63}', tid):
            raise ValueError('task id must be lowercase ASCII and 1..64 characters')
        if not isinstance(task_goal, str) or not task_goal.strip() or not isinstance(acceptance, list) or not acceptance or any(not isinstance(x, str) or not x.strip() for x in acceptance):
            raise ValueError(f'{tid} needs a bounded goal and non-empty acceptance requirements')
        if not isinstance(deps, list) or any(not isinstance(d, str) for d in deps):
            raise ValueError(f'{tid}.depends_on must be a list of task ids')
        if preparation is not None and (not isinstance(preparation, str) or not preparation.strip()):
            raise ValueError(f'{tid}.preparation must be a non-empty string when provided')
        normalized.append(dict(id=tid, goal=task_goal.strip(), depends_on=deps, acceptance=acceptance,
                               preparation=preparation or f"Work only within this goal: {task_goal.strip()}. Gather evidence for the listed acceptance requirements.",
                               status='todo', acceptance_confirmed=False, evidence=[]))
    _validate_acyclic(normalized)
    plan = dict(goal=goal.strip(), tasks=normalized, status_semantics='Task statuses are workflow bookkeeping, separate from Core world state.',
                evidence_semantics='Acceptance confirmation is an author declaration. Evidence is independently checked for repo presence and SHA-256 integrity; neither proves artistic quality or runtime behavior.')
    _save_plan(plan)
    return plan


def read_plan() -> dict:
    """Read plan; missing/changed evidence appears as a failed check rather than hiding the plan."""
    plan = _load_plan()
    for task in plan['tasks']:
        task['evidence_check'] = [_check_evidence(e) for e in task['evidence']]
        task['completion_valid'] = _completion_valid(task, {x['id']: x for x in plan['tasks']}, plan) if task['status'] == 'done' else None
    return plan


def _repo_file(relative_path: str) -> Path:
    if not isinstance(relative_path, str) or not relative_path or Path(relative_path).is_absolute():
        raise ValueError('evidence path must be repo-relative')
    path = (workflow.REPO / relative_path).resolve()
    try:
        path.relative_to(workflow.REPO.resolve())
    except ValueError as exc:
        raise ValueError('evidence path must remain inside the repository') from exc
    if not path.is_file():
        raise ValueError(f'evidence file does not exist: {relative_path}')
    return path


def _check_evidence(evidence: dict) -> dict:
    try:
        path = _repo_file(evidence['path'])
    except (ValueError, OSError) as exc:
        return dict(path=evidence.get('path'), expected_sha256=evidence.get('sha256'), actual_sha256=None,
                    exists=False, hash_matches=False, error=str(exc))
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    return dict(path=evidence['path'], expected_sha256=evidence['sha256'], actual_sha256=digest,
                exists=True, hash_matches=digest == evidence['sha256'])


def _definition_hash(plan: dict, task: dict) -> str:
    definition = dict(plan_goal=plan['goal'], task_goal=task['goal'],
                      acceptance=task['acceptance'], depends_on=task['depends_on'])
    canonical = json.dumps(definition, ensure_ascii=False, sort_keys=True, separators=(',', ':'))
    return hashlib.sha256(canonical.encode('utf-8')).hexdigest()


def _completion_valid(task: dict, by_id: dict[str, dict], plan: dict) -> bool:
    if task.get('status') != 'done' or not task.get('acceptance_confirmed') or not task.get('evidence'):
        return False
    if task.get('acceptance_definition_sha256') != _definition_hash(plan, task):
        return False
    if any(dep not in by_id or not _completion_valid(by_id[dep], by_id, plan) for dep in task.get('depends_on', [])):
        return False
    return all(check['hash_matches'] for check in (_check_evidence(e) for e in task['evidence']))


def update_task(task_id: str, status: str | None = None, acceptance_confirmed: bool | None = None,
                evidence_paths: list[str] | None = None, note: str | None = None) -> dict:
    """Update task tracking. Scope/goal/acceptance remain fixed; done requires ready deps and verified evidence."""
    if status not in (None, 'todo', 'in_progress', 'done'):
        raise ValueError('status must be todo, in_progress, or done')
    plan = _load_plan(); by_id = {t['id']: t for t in plan['tasks']}
    if task_id not in by_id: raise ValueError('unknown task id')
    task = by_id[task_id]
    if acceptance_confirmed is not None:
        task['acceptance_confirmed'] = bool(acceptance_confirmed)
        task['acceptance_definition_sha256'] = _definition_hash(plan, task) if acceptance_confirmed else None
    if evidence_paths is not None:
        task['evidence'] = []
        for path_text in evidence_paths:
            path = _repo_file(path_text)
            task['evidence'].append(dict(path=Path(path_text).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    if note is not None:
        task['note'] = note
    effective_status = status if status is not None else task['status']
    if effective_status == 'done':
        incomplete = [d for d in task['depends_on'] if not _completion_valid(by_id[d], by_id, plan)]
        if incomplete: raise ValueError('cannot mark done; dependencies incomplete: ' + ', '.join(incomplete))
        if not task['acceptance_confirmed']:
            raise ValueError('cannot mark done; acceptance requirements need author confirmation')
        if task.get('acceptance_definition_sha256') != _definition_hash(plan, task):
            raise ValueError('cannot remain done; plan/task definition changed and acceptance needs reconfirmation')
        if not task['evidence']:
            raise ValueError('cannot mark done; at least one repo evidence file is required')
        checks = [_check_evidence(e) for e in task['evidence']]
        if any(not c['hash_matches'] for c in checks): raise ValueError('cannot remain done; evidence missing or hash changed')
    if status in ('todo', 'in_progress') and task['status'] == 'done':
        descendants = {task_id}
        changed = True
        while changed:
            changed = False
            for candidate in plan['tasks']:
                if candidate['status'] == 'done' and candidate['id'] not in descendants and any(d in descendants for d in candidate['depends_on']):
                    descendants.add(candidate['id']); changed = True
        if len(descendants) > 1:
            raise ValueError('cannot reopen task while completed dependent tasks remain done')
    if status is not None: task['status'] = status
    _save_plan(plan)
    task['evidence_check'] = [_check_evidence(e) for e in task['evidence']]
    return task


def next_task() -> dict:
    """Return ready tasks and preparation derived only from their declared goals and requirements."""
    plan = _load_plan()
    by_id = {t['id']: t for t in plan['tasks']}
    valid_done = {t['id'] for t in plan['tasks'] if _completion_valid(t, by_id, plan)}
    ready = [t for t in plan['tasks'] if t['status'] != 'done' and
             all(dep in valid_done for dep in t['depends_on'])]
    return dict(tasks=[dict(id=t['id'], goal=t['goal'], acceptance=t['acceptance'],
                            preparation=t['preparation'],
                            status=t['status']) for t in ready],
                invalid_done_tasks=[t['id'] for t in plan['tasks'] if t['status'] == 'done' and t['id'] not in valid_done],
                note='No agent is started automatically. Preparation does not expand the task goal.')
