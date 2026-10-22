"""Summarize finished CLI runs without opening raw DAGs or starting sessions."""
import argparse
import json
from pathlib import Path

from binary_evidence import records


def record(value):
    if isinstance(value, list):
        if value and all(isinstance(p, list) and len(p) == 2 and isinstance(p[0], str) for p in value):
            return {p[0]: record(p[1]) for p in value}
        return [record(p) for p in value]
    if isinstance(value, dict):
        return {k: record(v) for k, v in value.items()}
    return value


def read_records(path, errors):
    try:
        for value in records(path):
            yield record(value)
    except Exception as error:
        errors.append(f'{path.name}: {type(error).__name__}: {error}')


def event_point(event):
    return {key: event.get(key) for key in ('event_id', 'type', 'actor', 'game_time', 'process_elapsed_ms')}


def scalar(value):
    return value if value is None or isinstance(value, (bool, int, float, str)) else None


def cell_summary(cell):
    if not isinstance(cell, dict):
        return None
    return {key: scalar(cell.get(key)) for key in ('known', 'conflicted', 'value')}


def closing_summary(value):
    if not isinstance(value, dict) or not value:
        return None
    return {key: cell_summary(value.get(key)) for key in ('council_closed', 'council_ended')}


def injury_summary(value):
    if not isinstance(value, list):
        return None
    return [{'actor': scalar(person.get('actor')),
             **{key: cell_summary(person.get(key)) for key in ('alive', 'dead', 'in_coma', 'stone_stupor')}}
            for person in value if isinstance(person, dict)]


def summarize(directory, root):
    item = {'scenario': directory.relative_to(root).as_posix(), 'errors': []}
    errors = item['errors']
    result_path = directory / 'result.dvlog'
    # run_debug publishes result only after session and postprocessing finish.
    # Do not inspect even stdout/index while the run has no completion report.
    if not result_path.is_file():
        item.update(status='unfinished-or-result-missing', skipped_live_outputs=True)
        return item
    results = list(read_records(result_path, errors))
    if len(results) != 1 or not isinstance(results[0], dict):
        item.update(status='invalid-result', skipped_live_outputs=True)
        return item
    result = results[0]
    item.update(status='reported', execution_seconds=result.get('execution_elapsed_seconds', result.get('elapsed_seconds')),
                timeout=('timeout_seconds' in result), timeout_seconds=result.get('timeout_seconds'),
                timeout_stage=result.get('timeout_stage'), exit_code=result.get('exit_code'),
                source_consistent=result.get('build_source_consistent'), source_unchanged=result.get('source_unchanged'),
                configuration_matches=result.get('run_config_matches'), sealed=result.get('raw_sealing_complete'),
                evidence_complete=result.get('evidence_complete'), run_config=result.get('run_config'),
                text_entries=0, debug_end_observed=False, last_event=None,
                chapter_content_available=[], closing_event=None, closing_event_status='missing-explicit-closing-event',
                chapter_summary=None, injury_facts=None)
    for key in ('error', 'export_error', 'raw_validation_error'):
        if result.get(key):
            errors.append(f'result.{key}: {result[key]}')
    for packet in read_records(directory / 'stdout.dvlog', errors):
        if not isinstance(packet, dict):
            errors.append('stdout.dvlog: non-record packet')
            continue
        if packet.get('type') == 'player_presentation':
            item['text_entries'] += 1
        if packet.get('type') == 'debug_end':
            stop = packet.get('stop_evidence', {})
            if not isinstance(stop, dict):
                stop = {}
            item.update(debug_end_observed=True, end_game_time=packet.get('game_time'),
                        chapter_summary=closing_summary(stop.get('chapter_summary')),
                        injury_facts=injury_summary(stop.get('injury_facts')),
                        stop_reasons=stop.get('reasons'))
    for event in read_records(directory / 'raw-event-index.dvlog', errors):
        if not isinstance(event, dict):
            errors.append('raw-event-index.dvlog: non-record packet')
            continue
        point = event_point(event)
        item['last_event'] = point
        if event.get('type') == 'chapter_content_available':
            item['chapter_content_available'].append(point)
    # The generic index has no committed effect payload. A chapter summary can
    # confirm the terminal state, but cannot identify the action that caused it.
    item['closing_state_reported'] = item['chapter_summary'] not in (None, False, [], {})
    item['text_scope'] = 'recorded player_presentation packets; count is not a completeness claim'
    return item


def show(value):
    if value is None:
        return '未知/缺失'
    if value is True:
        return '是'
    if value is False:
        return '否'
    return str(value).replace('|', '\\|').replace('\n', ' ')


def point_text(point):
    if not point:
        return '缺失'
    return f"{show(point.get('event_id'))} ({show(point.get('type'))}); 游戏时间 {show(point.get('game_time'))}; 实测进程毫秒 {show(point.get('process_elapsed_ms'))}"


def markdown(summary):
    lines = ['# CLI 性能证据汇总', '',
             '仅读取已发布 result.dvlog 的场景及其二进制投影/事件索引；不打开原始 DAG，不推算时间。游戏时间保留源值（分钟），进程毫秒保留实测值。章节内容开放不等于玩家交接；结束状态不等于已定位章末事件。', '',
             '| 场景 | 执行秒 | 超时 | 构建来源一致 | 运行源码未变 | 完整封存 | 正文条目 | debug_end |',
             '| --- | ---: | --- | --- | --- | --- | ---: | --- |']
    for item in summary['scenarios']:
        lines.append('| ' + ' | '.join(show(item.get(k)) for k in ('scenario', 'execution_seconds', 'timeout', 'source_consistent', 'source_unchanged', 'sealed', 'text_entries', 'debug_end_observed')) + ' |')
    for item in summary['scenarios']:
        lines += ['', f"## {item['scenario']}", '', f"状态：{item['status']}。", '',
                  f"末事件：{point_text(item.get('last_event'))}。",
                  f"章末状态摘要已记录：{show(item.get('closing_state_reported'))}；章末事件编号、事件游戏时间及实测进程毫秒：缺失（现有索引未携带实际效果，不用末事件代替）。"]
        for point in item.get('chapter_content_available', []):
            lines.append(f"章节内容开放：{point_text(point)}。")
        if not item.get('chapter_content_available'):
            lines.append('章节内容开放事件：缺失。')
        if item.get('chapter_summary'):
            lines += ['', '| 章末字段 | known | conflicted | value |', '| --- | --- | --- | --- |']
            for key, cell in item['chapter_summary'].items():
                cell = cell or {}
                lines.append('| ' + key + ' | ' + ' | '.join(show(cell.get(k)) for k in ('known', 'conflicted', 'value')) + ' |')
        if item.get('injury_facts'):
            lines += ['', '伤亡核验摘要（仅 known/conflicted/value；完整原始字段见 stdout.dvlog）：', '',
                      '| 人物 | 字段 | known | conflicted | value |', '| --- | --- | --- | --- | --- |']
            for person in item['injury_facts']:
                for key in ('alive', 'dead', 'in_coma', 'stone_stupor'):
                    cell = person.get(key) or {}
                    lines.append('| ' + show(person.get('actor')) + ' | ' + key + ' | ' + ' | '.join(show(cell.get(k)) for k in ('known', 'conflicted', 'value')) + ' |')
        else:
            lines.append('结束时伤亡字段：缺失。')
        for error in item['errors']:
            lines.append(f"读取/运行错误：{show(error)}")
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path('artifacts/perf-opt/aggressive'))
    parser.add_argument('--scenario', help='Only summarize this relative scenario directory under --root')
    args = parser.parse_args()
    root = args.root.resolve()
    if not root.is_dir():
        parser.error(f'Root does not exist: {root}')
    directories = set()
    if args.scenario is not None:
        selected = (root / args.scenario).resolve()
        if selected == root or not selected.is_relative_to(root) or not selected.is_dir():
            parser.error('--scenario must identify an existing subdirectory within --root')
        directories.add(selected)
    else:
        for name in ('result.dvlog', 'run-provenance.dv.json'):
            directories.update(path.parent for path in root.rglob(name))
    summary = {'schema': 'aggressive-engineering-summary-v2',
               'scope': 'engineering summary only; no raw event payloads or inferred milestone timing',
               'root': str(root), 'scenarios': [summarize(path, root) for path in sorted(directories)]}
    # These small derived engineering reports may be replaced on each invocation.
    for name, text in (('summary.json', json.dumps(summary, ensure_ascii=False, indent=2, default=str) + '\n'),
                       ('summary.md', markdown(summary))):
        temporary = root / (name + '.partial')
        temporary.write_text(text, encoding='utf-8')
        temporary.replace(root / name)
    print(f"Wrote {root / 'summary.md'} ({len(summary['scenarios'])} scenarios)")


if __name__ == '__main__':
    main()
