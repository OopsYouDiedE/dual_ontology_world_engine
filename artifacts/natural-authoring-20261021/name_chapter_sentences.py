#!/usr/bin/env python3
"""Give migrated declarations readable local names without changing stable IDs."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
NAMES = {
    'detective': '侦探', 'huo': '霍铎', 'shen': '沈鼎', 'su': '苏泠', 'demon': '恶魔',
    'quer': '雀儿', 'ward': '沃德', 'gu': '顾平章', 'lucian': '卢西安',
    'hall': '乙区大厅', 'foyer': '乙区门厅', 'corridor': '乙区走廊', 'pantry': '备餐间',
    'living_hall': '起居厅', 'central_hall': '中央厅', 'logistics': '后勤院',
    'clinic': '诊疗室', 'greenhouse': '温室', 'document_workshop': '文献工房',
    'resonance_hall': '共鸣厅', 'quiet_room': '沉静室', 'white_tower_safe': '白塔安全端',
    'oath_court': '誓约庭', 'outdoor_meter': '户外仪器阵',
    'c2.counter': '配给柜', 'c2.ledger': '配给总账', 'c2.granary': '粮仓',
    'location': '所在地', 'remaining': '余量', 'counted': '已点数', 'open': '开放状态',
    'key_owner': '持钥人', 'locked': '锁定状态', 'double_bolts': '双闩状态',
    'total_store_locked': '总粮仓锁定', 'reserve_added': '储备已入账',
    'reserve_received': '已收储备', 'removed': '已移出份数', 'day_recorded': '当日登记份数',
    'rations': '持有口粮', 'consumed': '已食用份数', 'hungry': '饥饿状态',
    'busy_care': '正在照护', 'alive': '存活状态', 'closed': '关闭状态',
}
PARAMETERS = {
    'actor': '行动者', 'target': '目标', 'origin': '起点', 'group': '分区',
    'id': '标识', 'sender': '发起者', 'patient': '病人', 'row': '条目',
    'gate': '廊门', 'day': '日次', 'meal': '餐次', 'start': '开始时刻',
    'end': '结束时刻', 'accepted': '是否接受', 'value': '取值', 'light': '亮度',
    'norm': '规约', 'change': '变化',
}
TOKEN = re.compile(r'"(?:\\.|[^"\\])*"|\b\w+\b')
HEADER = re.compile(r'^(法则|行动) (\w+)(?:，给定 (.*))?：$')


def transform(text):
    lines, used, changes = text.split('\n'), {}, []
    i = 0
    while i < len(lines):
        header = HEADER.fullmatch(lines[i])
        if not header:
            i += 1
            continue
        params = (header[3] or '').split('、')
        rename = {p: PARAMETERS[p] for p in params if p in PARAMETERS}
        j = i + 1
        while j < len(lines) and (not lines[j].strip() or lines[j].startswith(' ')):
            j += 1
        before = '\n'.join(lines[i:j])

        def replace(match):
            token = match.group()
            if token.startswith('"'):
                value = json.loads(token)
                if value in NAMES:
                    used[value] = NAMES[value]
                    return NAMES[value]
                return token
            return rename.get(token, token)

        for n in range(i, j):
            lines[n] = TOKEN.sub(replace, lines[n])
        after = '\n'.join(lines[i:j])
        if after != before:
            changes.append(dict(name=header[2], parameters=rename, before=before, after=after))
        i = j
    position = next(n for n, line in enumerate(lines) if line.startswith(('fn ', 'axiom ', '法则 ', '行动 ', '叙述 ')))
    aliases = ['// 文件内称谓只展开为原有标识，不改变对象身份或授予认识。']
    aliases += [f'称谓 {name} 为「{value}」。' for value, name in used.items()]
    aliases.append('')
    lines[position:position] = aliases
    return '\n'.join(lines), used, changes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    pending, entries = [], []
    for relative in ['language_core/40-chapter1.dv', 'language_core/50-chapter2.dv']:
        path = args.root / relative
        source = path.read_text(encoding='utf-8')
        if '// 文件内称谓只展开为原有标识' in source:
            raise ValueError('Naming migration has already been applied: ' + relative)
        result, aliases, changes = transform(source)
        pending.append((path, source, result))
        entries.append(dict(path=relative, aliases=aliases, changes=changes,
                            before_sha256=hashlib.sha256(source.encode()).hexdigest(),
                            after_sha256=hashlib.sha256(result.encode()).hexdigest()))
    with args.report.open('x', encoding='utf-8', newline='\n') as handle:
        json.dump(dict(kind='engineering-lexical-migration', files=entries), handle, ensure_ascii=False, indent=2)
        handle.write('\n')
    if args.write:
        for path, source, _ in pending:
            if path.read_text(encoding='utf-8') != source:
                raise RuntimeError('Changed source: ' + str(path))
        for path, _, result in pending:
            path.write_text(result, encoding='utf-8', newline='\n')
    print(json.dumps([dict(path=e['path'], aliases=len(e['aliases']), declarations=len(e['changes'])) for e in entries], ensure_ascii=False))


if __name__ == '__main__':
    main()
