"""Conservative helpers for inspecting and revising this project's Dialogue Manager files."""
from __future__ import annotations

import hashlib
import json
import os
import re
import tempfile
from pathlib import Path
from typing import Any

import workflow
import production

DEFAULT_STORY = 'godot_csharp/scenes/FirstAwakening/first_awakening.dialogue'
_LABEL = re.compile(r'^(\s*)~\s+([A-Za-z_][A-Za-z0-9_]*)\s*$')
_JUMP = re.compile(r'^(\s*)=>\s*([A-Za-z_][A-Za-z0-9_]*)\s*$')
_CHOICE = re.compile(r'^(\s*)-\s+(.*)$')
_BRANCH = re.compile(r'^(\s*)(if|elif)\s+(.+):\s*$|^(\s*)(else)\s*:\s*$')
_SPEAKER = re.compile(r'^(\s*)([^\s:][^:]*): (.*)$')
_TAGS = re.compile(r'(?<!\\)\[[^\]\r\n]+\]')
_ESCAPES = re.compile(r'\\.')
_INTERPOLATIONS = re.compile(r'\{\{.*?\}\}|\$[\w.]+')
_INTERPOLATION_MARKERS = re.compile(r'\{\{|\}\}|\$')
_EARLY_CONTEXT_TERMS = ('入学前', '学园', '学校', '封存')


def _story_path(path: str) -> Path:
    """Resolve only .dialogue files under the Godot scenes tree."""
    candidate = Path(path)
    if not candidate.is_absolute():
        candidate = workflow.REPO / candidate
    resolved = candidate.resolve()
    scenes = (workflow.REPO / 'godot_csharp' / 'scenes').resolve()
    try:
        resolved.relative_to(scenes)
    except ValueError as exc:
        raise ValueError('story path must stay under godot_csharp/scenes') from exc
    if resolved.suffix.lower() != '.dialogue':
        raise ValueError('story path must be a .dialogue file')
    if not resolved.is_file():
        raise ValueError('story file does not exist')
    return resolved


def _read(path: Path) -> tuple[bytes, str, list[str]]:
    raw = path.read_bytes()
    try:
        text = raw.decode('utf-8')
    except UnicodeDecodeError as exc:
        raise ValueError('story file must be UTF-8') from exc
    lines = text.splitlines(keepends=True)
    return raw, text, lines


def _line_body(line: str) -> tuple[str, str]:
    if line.endswith('\r\n'):
        return line[:-2], '\r\n'
    if line.endswith('\n') or line.endswith('\r'):
        return line[:-1], line[-1:]
    return line, ''


def _tokens(line: str) -> dict:
    return dict(tags=_TAGS.findall(line), escapes=_ESCAPES.findall(line),
                interpolations=_INTERPOLATIONS.findall(line),
                interpolation_markers=_INTERPOLATION_MARKERS.findall(line),
                inline_goto=re.findall(r'=>.*$', line))


def _parse(text: str, lines: list[str]) -> dict:
    entries, controls, unknown = [], [], []
    labels = []
    parsed_rows = []
    for line_no, full_line in enumerate(lines, 1):
        body, ending = _line_body(full_line)
        if not body.strip():
            kind, data = 'blank', {}
        elif body.lstrip().startswith('#'):
            kind, data = 'comment', {}
        elif match := _LABEL.fullmatch(body):
            kind, data = 'label', dict(indent=match.group(1), label=match.group(2))
            labels.append(match.group(2))
        elif match := _JUMP.fullmatch(body):
            kind, data = 'jump', dict(indent=match.group(1), target=match.group(2))
        elif match := _CHOICE.fullmatch(body):
            kind, data = 'choice', dict(indent=match.group(1), text=match.group(2))
        elif re.match(r'^\s*do\b', body):
            indent = re.match(r'^\s*', body).group(0)
            kind, data = 'action', dict(indent=indent, action=body[len(indent):])
        elif _BRANCH.fullmatch(body):
            match = _BRANCH.fullmatch(body)
            indent = match.group(1) if match.group(1) is not None else match.group(4)
            branch = match.group(2) if match.group(2) is not None else match.group(5)
            condition = match.group(3).strip() if match.group(3) is not None else ''
            kind, data = 'branch', dict(indent=indent, branch=branch, condition=condition)
        elif (match := _SPEAKER.fullmatch(body)) and not re.match(r'^\s*(?:if|elif|else|while|match|when|set|using|import)\b', body):
            kind, data = 'dialogue', dict(indent=match.group(1), speaker=match.group(2), text=match.group(3))
        else:
            kind, data = 'unknown', dict(text=body)
            unknown.append(dict(line=line_no, text=body, note='Unrecognized by this limited parser; preserved verbatim.'))
        row = dict(line=line_no, raw=body, kind=kind, **data)
        entries.append(row)
        parsed_rows.append((row, ending))
        tokens = _tokens(body)
        if kind == 'dialogue':
            controls.append(dict(line=line_no, kind=kind, indent=data['indent'], speaker=data['speaker'], **tokens))
        elif kind not in ('blank', 'comment'):
            controls.append(dict(line=line_no, kind=kind, **{k: v for k, v in data.items() if k != 'text'}, **tokens))
        elif tokens['tags'] or tokens['escapes']:
            controls.append(dict(line=line_no, kind='protected_tokens', **tokens))

    targets = {row['target'] for row, _ in parsed_rows if row['kind'] == 'jump'}
    unresolved = sorted(target for target in targets if target not in labels and target != 'END')
    fingerprint_payload = dict(
        controls=controls,
        line_count=len(lines),
        endings=[_line_body(line)[1] for line in lines],
        terminal_newline=bool(lines and _line_body(lines[-1])[1]),
    )
    fingerprint = hashlib.sha256(json.dumps(fingerprint_payload, ensure_ascii=False, sort_keys=True,
                                            separators=(',', ':')).encode('utf-8')).hexdigest()
    duplicates=sorted({label for label in labels if labels.count(label)>1})
    return dict(entries=entries, labels=labels, controls=controls, control_fingerprint=fingerprint,
                unknown_syntax=unknown, unresolved_jumps=unresolved, duplicate_labels=duplicates,
                safe_to_apply=not unknown and not unresolved and not duplicates)


def inspect_story(path: str = DEFAULT_STORY) -> dict:
    """Read a scene .dialogue file with a limited parser and preserve every source line."""
    resolved = _story_path(path)
    raw, text, lines = _read(resolved)
    parsed = _parse(text, lines)
    return dict(path=resolved.relative_to(workflow.REPO).as_posix(), sha256=hashlib.sha256(raw).hexdigest(),
                source_lines=[dict(line=i, text=_line_body(line)[0], ending=_line_body(line)[1])
                              for i, line in enumerate(lines, 1)],
                entries=parsed['entries'], labels=parsed['labels'], controls=parsed['controls'],
                control_fingerprint=parsed['control_fingerprint'], unknown_syntax=parsed['unknown_syntax'],
                unresolved_jumps=parsed['unresolved_jumps'], safe_to_apply=parsed['safe_to_apply'],
                duplicate_labels=parsed['duplicate_labels'],
                parser_scope='Recognizes labels, speaker lines, choices, jumps, do actions, if/elif/else branches and preserves unknown lines; not a full DialogueManager parser.')


def _check_change(line_no: int, expected: str, replacement: str, parsed: dict, lines: list[str]) -> dict:
    if type(line_no) is not int or not 1 <= line_no <= len(lines):
        raise ValueError('change line is outside the source file')
    if not isinstance(expected, str) or not isinstance(replacement, str):
        raise ValueError('expected and replacement must be strings')
    if '\n' in expected or '\r' in expected or '\n' in replacement or '\r' in replacement or len(replacement.splitlines())>1:
        raise ValueError('single-line changes cannot add, remove or alter line endings')
    current, ending = _line_body(lines[line_no - 1])
    if current != expected:
        raise ValueError(f'line {line_no} compare-and-swap mismatch')
    row = parsed['entries'][line_no - 1]
    if row['kind'] == 'dialogue':
        prefix = f"{row['indent']}{row['speaker']}: "
        if not current.startswith(prefix) or not replacement.startswith(prefix):
            raise ValueError('dialogue revision must preserve indentation and speaker prefix')
    else:
        raise ValueError('only existing speaker dialogue text may be revised; choices and controls are read-only')
    if _tokens(current) != _tokens(replacement):
        raise ValueError('revision must preserve protected tags and escape tokens')
    return dict(line=line_no, old=current, new=replacement, ending=ending, kind=row['kind'])


def _prepare_revision(source_path: str, changes: list[dict[str, Any]], source_sha256: str) -> tuple[Path, bytes, str, list[str], dict, list[dict]]:
    path = _story_path(source_path)
    raw, text, lines = _read(path)
    digest = hashlib.sha256(raw).hexdigest()
    if digest != source_sha256:
        raise ValueError('source SHA-256 is stale; inspect the file and retry')
    parsed = _parse(text, lines)
    if not changes or len(changes) > 200:
        raise ValueError('provide 1..200 line changes')
    prepared, seen = [], set()
    for change in changes:
        line_no = change.get('line')
        if line_no in seen:
            raise ValueError(f'duplicate line change: {line_no}')
        seen.add(line_no)
        prepared.append(_check_change(line_no, change.get('expected'), change.get('replacement'), parsed, lines))
    revised = list(lines)
    for item in prepared:
        revised[item['line'] - 1] = item['new'] + item['ending']
    revised_text = ''.join(revised)
    revised_parsed = _parse(revised_text, revised)
    if revised_parsed['control_fingerprint'] != parsed['control_fingerprint']:
        raise ValueError('revision changed the control-structure fingerprint')
    return path, raw, text, lines, parsed, prepared


def review_story_revision(source_path: str, changes: list[dict[str, Any]], source_sha256: str,
                          cards: list[dict[str, Any]] | None = None,
                          known_facts: dict[str, list[str]] | None = None) -> dict:
    """Return a compare-and-swap patch preview; no file is written."""
    path, raw, text, lines, parsed, prepared = _prepare_revision(source_path, changes, source_sha256)
    revised = list(lines)
    for item in prepared:
        revised[item['line'] - 1] = item['new'] + item['ending']
    revised_text = ''.join(revised)
    turns = []
    for row in _parse(revised_text, revised)['entries']:
        if row['kind'] == 'dialogue':
            turns.append(dict(speaker=row['speaker'], text=row['text'], claims=[]))
    dialogue_review = None
    dialogue_review_note = None
    if cards is not None:
        cards_by_id = {card.get('id'): dict(card) for card in cards}
        if known_facts:
            for cid, facts in known_facts.items():
                if cid in cards_by_id:
                    cards_by_id[cid]['known_facts'] = facts
        if all(all(key in card for key in ('want', 'secret', 'voice', 'known_facts')) for card in cards_by_id.values()):
            dialogue_review = production.review_dialogue(list(cards_by_id.values()), turns)
        else:
            dialogue_review_note = 'Card review requires want, secret, voice, and known_facts for every supplied card.'
    elif known_facts is not None:
        dialogue_review_note = 'Known facts alone do not provide the complete character-card schema; semantic dialogue review was skipped.'
    phase_facts = {card.get('id'): card.get('known_facts', []) for card in (cards or [])}
    phase_facts.update(known_facts or {})
    context_warnings = []
    current_label = ''
    for row in _parse(revised_text, revised)['entries']:
        if row['kind'] == 'label':
            current_label = row['label']
        early_phase = current_label == 'opening' or current_label.endswith('_0')
        if row['kind'] == 'dialogue' and early_phase:
            terms = [term for term in _EARLY_CONTEXT_TERMS if term in row['text']]
            if terms:
                facts = phase_facts.get(row['speaker'], [])
                if not all(any(term in fact for fact in facts) for term in terms):
                    context_warnings.append(dict(line=row['line'], label=current_label, speaker=row['speaker'],
                                                 terms=terms, kind='knowledge_context',
                                                 message='Opening/intro knowledge needs author context review; phase-label heuristic only.'))
    revised_bytes = revised_text.encode('utf-8')
    return dict(path=path.relative_to(workflow.REPO).as_posix(), source_sha256=hashlib.sha256(raw).hexdigest(),
                revised_sha256=hashlib.sha256(revised_bytes).hexdigest(), patch=prepared,
                control_fingerprint=parsed['control_fingerprint'], control_preserved=True,
                safe_to_apply=parsed['safe_to_apply'], dialogue_review=dialogue_review,
                dialogue_review_note=dialogue_review_note,
                context_warnings=context_warnings,
                limitations=['Only existing speaker dialogue text can change; choices and controls are read-only.', 'No subtext or factual truth is inferred.',
                              'Source must be understood and all jumps resolve before apply.',
                              'Opening/intro checks use opening and *_0 labels, not source ordering.',
                              'Pixel-width budgets are heuristic and do not measure actual font glyphs.'])


def apply_story_revision(source_path: str, changes: list[dict[str, Any]], source_sha256: str) -> dict:
    """Apply a verified single-line story patch atomically to its existing scene source."""
    path, raw, text, lines, parsed, prepared = _prepare_revision(source_path, changes, source_sha256)
    if not parsed['safe_to_apply']:
        raise ValueError('source contains unknown syntax, duplicate labels or unresolved jumps; apply is blocked until understood')
    revised = list(lines)
    for item in prepared:
        revised[item['line'] - 1] = item['new'] + item['ending']
    revised_text = ''.join(revised)
    if _parse(revised_text, revised)['control_fingerprint'] != parsed['control_fingerprint']:
        raise ValueError('control structure changed during final validation')
    current = path.read_bytes()
    if hashlib.sha256(current).digest() != hashlib.sha256(raw).digest():
        raise ValueError('source changed before apply; inspect again and retry')
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode='wb', dir=path.parent, prefix=path.name + '.', suffix='.tmp', delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(revised_text.encode('utf-8'))
            stream.flush()
            os.fsync(stream.fileno())
        if hashlib.sha256(path.read_bytes()).digest() != hashlib.sha256(raw).digest():
            raise ValueError('source changed during apply preparation; inspect again and retry')
        os.replace(temporary, path)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()
    return dict(path=path.relative_to(workflow.REPO).as_posix(),
                source_sha256=hashlib.sha256(raw).hexdigest(),
                sha256=hashlib.sha256(revised_text.encode('utf-8')).hexdigest(),
                applied_lines=[item['line'] for item in prepared], control_fingerprint=parsed['control_fingerprint'])


def story_design_contract() -> dict:
    """Return source-grounded revision principles and empty character-card fields."""
    source_paths = {
        'current_dialogue': DEFAULT_STORY,
        'observation_cards': 'artifacts/first-awakening/observation-cards.md',
        'questioning_review': 'artifacts/first-awakening/questioning-review.md',
        'reasoning_design_review': 'artifacts/first-awakening/reasoning-design-review.md',
    }
    patterns = {
        'observation_cards': ('只描述', '不能', '未知', '假设', '可见', '未实现', '判断'),
        'questioning_review': ('不能', '未知', '已完成修订', '问答', '未', '原型'),
        'reasoning_design_review': ('假设', '已知', '验证', '失败', '等价', '现实', '证据'),
    }
    sources = {}
    for name, relative in source_paths.items():
        path = (workflow.REPO / relative).resolve()
        try:
            path.relative_to(workflow.REPO.resolve())
        except ValueError as exc:
            raise ValueError(f'contract source escaped repository: {relative}') from exc
        if not path.is_file():
            raise ValueError(f'contract source is missing: {relative}')
        raw = path.read_bytes()
        try:
            text = raw.decode('utf-8')
        except UnicodeDecodeError as exc:
            raise ValueError(f'contract source is not UTF-8: {relative}') from exc
        if name == 'current_dialogue':
            excerpt = text[:700].strip()
        else:
            selected = []
            for paragraph in re.split(r'\n\s*\n', text):
                if any(term in paragraph for term in patterns[name]):
                    selected.append(paragraph.strip())
                if sum(len(item) for item in selected) >= 700:
                    break
            excerpt = '\n\n'.join(selected)[:900]
        sources[name] = dict(path=relative, sha256=hashlib.sha256(raw).hexdigest(), excerpt=excerpt)
    return dict(sources=sources,
                revision_principles=[
                    'Follow docs/design-standard.md#语言风格 for all on-screen writing: omit text for information the visuals can convey, and use contrastive clauses only when necessary.',
                    'Keep observable facts, character hypotheses, and rule-produced results explicitly separate.',
                    'A character gains knowledge only through a concrete observation or an explicitly recorded acquisition.',
                    'Weak observations do not certify a unique historical sequence; retain feasible equivalent explanations as unknown.',
                    'An NPC may state a current interpretation, but that statement does not authenticate the past.',
                    'Failure feedback may cite only observations already submitted by the player.',
                    'Allow repeated questions and an explicit exit choice; do not report an unimplemented experiment as a failed experiment.'
                ],
                character_card_template=dict(id='', want='', secret='', voice='', known_facts=[]),
                character_card_note='These fields are blank prompts for the author. No want, secret, voice, or knowledge is inferred or invented.',
                excerpt_limit_chars=900)


def review_story_beats(beats: list[dict[str, Any]], characters: list[dict[str, Any]]) -> dict:
    """Check declared knowledge bookkeeping without deciding whether story claims are true."""
    if len(beats) > 500 or len(characters) > 200:
        raise ValueError('review accepts at most 500 beats and 200 characters')
    knowledge = {}
    for character in characters:
        cid = character.get('id')
        facts = character.get('known_facts', [])
        if not cid or cid in knowledge or not isinstance(facts, list) or any(not isinstance(fact, str) for fact in facts):
            raise ValueError('characters need unique ids and known_facts lists')
        knowledge[cid] = set(facts)
    issues = []
    allowed = {'observation', 'hypothesis', 'rule_result'}
    for index, beat in enumerate(beats, 1):
        kind, cid = beat.get('kind'), beat.get('character')
        if kind not in allowed:
            raise ValueError(f'beat {index} kind must be observation, hypothesis, or rule_result')
        if cid not in knowledge:
            raise ValueError(f'beat {index} references unknown character')
        for field in ('goal', 'action', 'information'):
            if not isinstance(beat.get(field), str) or not beat[field].strip():
                issues.append(dict(beat=index, kind='missing_field', field=field))
        claims = beat.get('claims', [])
        acquires = beat.get('acquires', [])
        if any(not isinstance(values, list) or any(not isinstance(v, str) for v in values) for values in (claims, acquires)):
            raise ValueError(f'beat {index} claims and acquires must be string lists')
        if kind == 'rule_result' and (not isinstance(beat.get('source_ref'), str) or not beat.get('source_ref', '').strip()):
            issues.append(dict(beat=index, kind='missing_mechanism_source', message='Rule result needs a mechanism/source reference.'))
        for claim in claims:
            if claim not in knowledge[cid]:
                issues.append(dict(beat=index, kind='unknown_to_character', character=cid, claim=claim,
                                   message='Claim is not in this character’s known facts; add a prior acquisition or mark it as a hypothesis.'))
        if kind == 'hypothesis' and beat.get('presented_as_fact') is True:
            issues.append(dict(beat=index, kind='hypothesis_presented_as_fact', character=cid,
                               message='Hypothesis is explicitly marked as presented like a fact.'))
        if kind == 'hypothesis' and acquires:
            issues.append(dict(beat=index, kind='hypothesis_cannot_acquire_fact', character=cid,
                               message='A hypothesis does not update known facts; record a later observation or rule result.'))
        elif kind != 'hypothesis':
            knowledge[cid].update(acquires)
    return dict(ok=not issues, issues=issues,
                final_known_facts={cid: sorted(facts) for cid, facts in knowledge.items()},
                checked=['declared character knowledge order', 'explicit hypothesis/fact labeling', 'rule-result source reference'],
                not_checked=['truth of observations or hypotheses', 'unstated context, motives, or causal correctness'])


def story_scene_design_contract() -> dict:
    """Give the author a scene-first dramatic design and review contract."""
    source_paths = ('docs/design-standard.md',)
    sources = []
    for relative in source_paths:
        source = workflow.REPO / relative
        sources.append(dict(path=relative, sha256=hashlib.sha256(source.read_bytes()).hexdigest()))
    return dict(
        sources=sources,
        hierarchy=['arc: dramatic question, changing relationships, withheld/revealed facts, ending state',
                   'scene: entrance state, immediate want, resistance, player verbs, visible result, exit state',
                   'beat: actor action, another actor or object response, new observable information'],
        scene_template=dict(scene='', entrance_state='', dramatic_question='',
                            characters=[dict(id='', immediate_want='', leverage='', voice='', known_facts=[])],
                            beats=[dict(trigger='', player_action='', resistance='', observable_result='',
                                        information_gained='', state_change='', source_ref='')],
                            exit_state='', next_hook=''),
        review_questions=[
            'Before scripting, was the complete player experience written as a comprehensive continuous novel-like narrative, including attention, interpretation, decisions, exact inputs, feedback and branch recovery, using only player-available information, as required by docs/design-standard.md#片段设计顺序? A checklist or synopsis alone is insufficient.',
            'Has actual player-input acceptance compared expected and observed steps, measured deviations and distinguished faulty assumptions from game design or implementation faults, as required by docs/design-standard.md#完成条件?',
            'Does every on-screen line follow docs/design-standard.md#语言风格: no text for visually conveyable information, and contrastive clauses only when necessary?',
            'What can the player do before anyone explains the situation?',
            'What does each speaker want from the listener in this moment? What might the listener refuse?',
            'Which fact is discovered by touching, seeing, comparing, choosing or failing, and where is that action implemented?',
            'Which line interprets evidence the player has not yet acquired or answers the dramatic question early?',
            'If a monologue is removed, can the scene still communicate through staging, objects and reactions?',
            'Does a choice alter state, access, relationship or information, and can the player notice the result?',
            'Are observations, character guesses and Core rule results distinguished?',
            'Does the exit state differ from the entrance state in a way the next scene uses?'],
        principle='Narration and inner voice may convey sensory experience or a personal decision, but should not replace a playable discovery or explain a conclusion for the player.',
        workflow=['inventory_story_project', 'audit_story_presentation',
                  'write the full player experience as a comprehensive continuous novel-like narrative before scripting: attention, interpretation, decisions, exact operations, feedback, branches, failure and recovery',
                  'draft arc/scene/beat cards with the returned template', 'review_story_beats',
                  'inspect_story', 'review_story_revision', 'apply_story_revision',
                  'compile Dialogue Manager and play the scene; compare actual steps with the original operation hypothesis, diagnose deviations, revise and retest; human literary review'],
        limit='The MCP supplies evidence and review structure. Its static signals are candidates, not literary verdicts or proof of runtime interaction.')


def inventory_story_project() -> dict:
    """Inventory authored dialogue across the actual Godot scenes tree."""
    scenes = workflow.REPO / 'godot_csharp' / 'scenes'
    files = []
    for path in sorted(scenes.rglob('*.dialogue')):
        inspected = inspect_story(str(path))
        entries = inspected['entries']
        files.append(dict(path=inspected['path'], sha256=inspected['sha256'],
                          labels=inspected['labels'], dialogue_lines=sum(row['kind'] == 'dialogue' for row in entries),
                          choices=sum(row['kind'] == 'choice' for row in entries),
                          actions=sum(row['kind'] == 'action' for row in entries),
                          parser_warnings=len(inspected['unknown_syntax']) + len(inspected['unresolved_jumps'])))
    return dict(files=files, note='Inventory includes samples and scenes not necessarily reachable from the current main entry; inspect scene references before claiming runtime coverage.')


def audit_story_presentation(path: str = DEFAULT_STORY) -> dict:
    """Find source-cited narration and exposition candidates, without judging literary quality."""
    inspected = inspect_story(path)
    sections = []
    section = None
    interpretation = re.compile(r'不能证明|说明了|先移开|随后擦|才是|不是|并非|所以|因此|必然|意味着|你把先后关系|你们辨认出|你们说明了')
    for row in inspected['entries']:
        if row['kind'] == 'label':
            section = dict(label=row['label'], start_line=row['line'], lines=[], dialogue=0,
                           choices=0, actions=0, voices={}, candidates=[])
            sections.append(section)
        if section is None:
            continue
        section['lines'].append(row)
        if row['kind'] == 'choice':
            section['choices'] += 1
        elif row['kind'] == 'action':
            section['actions'] += 1
        elif row['kind'] == 'dialogue':
            section['dialogue'] += 1
            speaker, body = row['speaker'], row['text']
            section['voices'][speaker] = section['voices'].get(speaker, 0) + 1
            if '心声' in speaker or speaker == '旁白':
                section['candidates'].append(dict(line=row['line'], kind='inner_voice', text=body,
                    question='Is this a sensation or decision, or does it tell the player what to infer?'))
            elif interpretation.search(body):
                section['candidates'].append(dict(line=row['line'], kind='explained_inference', text=body,
                    question='Has the player acquired and compared the evidence before this line?'))
            if len(body) >= 90:
                section['candidates'].append(dict(line=row['line'], kind='long_turn', text=body,
                    question='Can a physical action, interruption or response carry part of this turn?'))
    result = []
    for section in sections:
        if not section['dialogue'] and not section['choices']:
            continue
        candidates = section['candidates']
        if section['dialogue'] >= 4 and not section['choices'] and not section['actions']:
            candidates.append(dict(line=section['start_line'], kind='unbroken_dialogue',
                text='', question='Is there a player action, inspectable object or consequential choice that can interrupt this sequence?'))
        result.append(dict(label=section['label'], start_line=section['start_line'],
                           dialogue_lines=section['dialogue'], choices=section['choices'], actions=section['actions'],
                           voices=section['voices'], candidates=candidates))
    return dict(path=inspected['path'], sha256=inspected['sha256'], sections=result,
                candidate_count=sum(len(item['candidates']) for item in result),
                limits=['Static Dialogue Manager source only; scene scripts may add interactive actions and visible outcomes.',
                        'Inner voice, long turns and inference words are review candidates, not automatic defects.',
                        'Review timing and player knowledge in the actual game before rewriting.'])


def audit_story_project() -> dict:
    """Run the same source review over every authored dialogue, with per-file evidence."""
    inventory = inventory_story_project()
    audits = [audit_story_presentation(item['path']) for item in inventory['files']]
    return dict(audits=audits, candidate_count=sum(item['candidate_count'] for item in audits),
                note='Includes inactive samples; scene reachability and artistic merit require runtime and human review.')
