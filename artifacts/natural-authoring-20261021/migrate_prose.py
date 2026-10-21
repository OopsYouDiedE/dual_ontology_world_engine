#!/usr/bin/env python3
"""One-time lossless chapter prose/action migration; never a production parser."""
from __future__ import annotations
import argparse
import ast
import hashlib
import json
from pathlib import Path
import re
from migrate_rules import FILES, ROOT, Renderer, call, declarations


def digest(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def prose_value(expression):
    try:
        value = json.loads(expression)
    except (ValueError, TypeError):
        return None
    if not isinstance(value, str) or len(value) <= 5:
        return None
    if not any('\u4e00' <= c <= '\u9fff' for c in value):
        return None
    # These edges need explicit author choice; never normalize authored bytes.
    if '\r' in value or '\t' in value or value.endswith('\n'):
        return None
    if any(line.startswith(' ') for line in value.split('\n')):
        return None
    return value


def raw_block(heading, text, indent):
    return heading + '\n' + '\n'.join(indent + line for line in text.split('\n'))


def plan(text):
    replacements, covered = [], set()
    for head, row, start, end, expression, reason in declarations(text):
        if reason or head['kind'] != 'fn':
            continue
        value = prose_value(expression)
        if value is not None and not head['params'].strip():
            after = raw_block('叙述 ' + head['name'] + '：', value, '    ')
            replacements.append(dict(name=head['name'], kind='narration', line=row,
                                     start=start, end=end, before=text[start:end], after=after))
            covered.add(row + 1)
            continue
        try:
            node = ast.parse(expression, mode='eval').body
        except SyntaxError:
            continue
        once = call(node, 'c1_once', 1)
        action = node.args[0] if once else node
        if not call(action, 'c1_action', 7):
            continue
        params = [p.strip() for p in head['params'].split(',') if p.strip()]
        if any(not p.isidentifier() for p in params):
            continue
        render = Renderer(expression)
        id_, kind, known, physical, law, grants, source = action.args
        lines = ['行动 ' + head['name'] + ('，给定 ' + '、'.join(params) if params else '') + '：',
                 '    编号 ' + render.raw(id_) + '。',
                 '    类别 ' + render.raw(kind) + '。',
                 '    本人须知 ' + render.proposition(known) + '。',
                 '    现场要求 ' + render.proposition(physical) + '。',
                 '    遵循 ' + render.raw(law) + '。',
                 '    可获知 ' + render.raw(grants) + '。',
                 '    依据 [' + render.raw(source) + ']。']
        if once:
            lines.append('    仅一次 true。')
        replacements.append(dict(name=head['name'], kind='action', line=row,
                                 start=start, end=end, before=text[start:end], after='\n'.join(lines)))
        covered.add(row + 1)
    current, offset = '', 0
    for row, line in enumerate(text.splitlines(keepends=True), 1):
        head = re.match(r'^(?:fn|axiom) (\w+)', line)
        if head:
            current = head[1]
        m = re.fullmatch(r'( +)return (.*)\n?', line)
        if m and row not in covered:
            indent, expression = m.groups()
            value = prose_value(expression)
            kind, content = 'narration-return', value
            if current == 'c1_op_wake_lines':
                try:
                    values = json.loads(expression)
                except ValueError:
                    values = None
                if isinstance(values, list) and values and all(isinstance(x, str) and x and '\n' not in x for x in values):
                    kind, content = 'dialogue-return', '\n'.join(values)
            if content is not None:
                heading = indent + ('台词：' if kind == 'dialogue-return' else '叙述：')
                after = raw_block(heading, content, indent + '    ')
                before = line.removesuffix('\n')
                replacements.append(dict(name=current, kind=kind, line=row, start=offset,
                                         end=offset + len(before), before=before, after=after))
        offset += len(line)
    replacements.sort(key=lambda change: change['start'])
    for previous, current_change in zip(replacements, replacements[1:]):
        if previous['end'] > current_change['start']:
            raise ValueError('Overlapping migration edits')
    result = text
    for change in reversed(replacements):
        if result[change['start']:change['end']] != change['before']:
            raise ValueError('Changed migration input')
        result = result[:change['start']] + change['after'] + result[change['end']:]
    return result, replacements


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    pending, entries = [], []
    for relative in FILES:
        path = args.root / relative
        original = path.read_text(encoding='utf-8-sig')
        candidate, changes = plan(original)
        pending.append((path, original, candidate))
        entries.append(dict(path=relative, before_sha256=digest(original),
                            after_sha256=digest(candidate), changes=changes))
    args.report.parent.mkdir(parents=True, exist_ok=True)
    with args.report.open('x', encoding='utf-8', newline='\n') as handle:
        json.dump(dict(kind='engineering-source-migration', writes_requested=args.write,
                       scope='Exact authored prose and pure action constructors; no execution',
                       files=entries), handle, ensure_ascii=False, indent=2)
        handle.write('\n')
    if args.write:
        for path, original, _ in pending:
            if path.read_text(encoding='utf-8-sig') != original:
                raise RuntimeError('Source changed during migration: ' + str(path))
        for path, _, candidate in pending:
            path.write_text(candidate, encoding='utf-8', newline='\n')
    for entry in entries:
        counts = {}
        for change in entry['changes']:
            counts[change['kind']] = counts.get(change['kind'], 0) + 1
        print(json.dumps(dict(path=entry['path'], changes=counts), ensure_ascii=False))


if __name__ == '__main__':
    main()
