"""Summarize finished CLI runs without opening raw DAGs or starting sessions."""
import argparse
import hashlib
import json
import math
from pathlib import Path

from binary_evidence import records
from matrix_lifecycle import case_guard


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
             **{key: cell_summary(person.get(key)) for key in ('alive', 'dead', 'in_coma', 'stone_stupor', 'erosion_exposure', 'serious_injury', 'motor_impairment', 'respiratory_failure', 'anti_erosion_treated')}}
            for person in value if isinstance(person, dict)]


def _summarize(directory, root):
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
                chapter_summary=None, injury_facts=None, first_effective_inspection=None,
                chapter_one_handoff=None, closing_observation=None, answer_receipts=[],
                causal_check_complete=result.get('causal_check_complete'),
                causal_check_status=result.get('causal_check_status'),
                postprocess_seconds=result.get('postprocess_elapsed_seconds'),
                build_seconds=result.get('build_elapsed_seconds'), runtime_config=result.get('runtime_config'),
                runtime_environment=result.get('runtime_environment'))
    fingerprint_path = directory / 'source-hashes.json'
    if fingerprint_path.is_file():
        hashes = json.loads(fingerprint_path.read_text(encoding='utf-8'))
        modules = {path.replace(chr(92), '/').rsplit('/', 1)[-1]: digest for path, digest in hashes.items()
                   if path.replace(chr(92), '/').rsplit('/', 1)[-1].startswith(('20-', '30-', '35-', '40-', '50-', '60-'))}
        item['module_hashes_20_60'] = modules
        item['compilation_input_fingerprint'] = hashlib.sha256(json.dumps(hashes, sort_keys=True).encode()).hexdigest()
        item['model_fingerprint'] = hashlib.sha256(json.dumps(modules, sort_keys=True).encode()).hexdigest()
    measurement = root / (directory.name + '-measurement.json')
    if directory.parent == root and measurement.is_file():
        timing = json.loads(measurement.read_text(encoding='utf-8'))
        if timing.get('result_sha256') == hashlib.sha256(result_path.read_bytes()).hexdigest():
            for key in ('total_wall_seconds', 'command_wall_seconds', 'cleanup_seconds', 'session_limit_seconds'):
                item[key] = timing.get(key)
    inspections = {}
    projected = {}
    closing_candidate = None
    viewers = None if (result.get('run_config') or {}).get('detective_zone') == 'none' else {'detective'}
    for key in ('error', 'export_error', 'raw_validation_error'):
        if result.get(key):
            errors.append(f'result.{key}: {result[key]}')
    for packet in read_records(directory / 'stdout.dvlog', errors):
        if not isinstance(packet, dict):
            errors.append('stdout.dvlog: non-record packet')
            continue
        if packet.get('type') == 'player_presentation':
            item['text_entries'] += 1
        if packet.get('type') == 'debug_events':
            for event in packet.get('batch', {}).get('events', []):
                if not isinstance(event, dict):
                    errors.append('debug_events: non-record event')
                    continue
                kind = event.get('type')
                actor = event.get('actor')
                detail = event.get('detail') if isinstance(event.get('detail'), dict) else {}
                eligible_viewer = viewers is None or actor in viewers
                if kind in ('host_completion', 'action_execution') and isinstance(event.get('event_id'), str):
                    projected[event['event_id']] = event
                if kind in ('answer_accepted', 'answer_rejected'):
                    item['answer_receipts'].append({**event_point(event), 'parents': event.get('parents', []),
                        'accepted': detail.get('accepted'), 'literal_truth': detail.get('literal_truth'),
                        'epistemic_support': detail.get('epistemic_support'), 'raw_event_ref': event.get('raw_event_ref')})
                if kind == 'action_execution' and detail.get('status') == 'applied' and eligible_viewer:
                    action = event.get('action_id') or ''
                    point = {**event_point(event), 'action_id': action, 'action_kind': event.get('action_kind'),
                             'parents': event.get('parents', []), 'status': 'applied'}
                    if event.get('action_kind') == 'inspect':
                        inspections[event['event_id']] = point
                    handoff = (action == actor + '.departure.cross-out'
                               or (action.startswith(actor + '.other.exit.') and '.threshold.' in action)
                               or (viewers is None and action == actor + '.departure.join-living'))
                    if handoff and item['chapter_one_handoff'] is None:
                        item['chapter_one_handoff'] = point
                if kind == 'observation' and eligible_viewer and detail.get('observations') and item['first_effective_inspection'] is None:
                    receipt = next((inspections[parent] for parent in event.get('parents', []) if parent in inspections), None)
                    if receipt:
                        item['first_effective_inspection'] = {**receipt, 'observation_event': event['event_id']}
        if packet.get('type') == 'debug_end':
            stop = packet.get('stop_evidence', {})
            if not isinstance(stop, dict):
                stop = {}
            item.update(debug_end_observed=True, end_game_time=packet.get('game_time'),
                        chapter_summary=closing_summary(stop.get('chapter_summary')),
                        injury_facts=injury_summary(stop.get('injury_facts')),
                        stop_reasons=stop.get('reasons'))
            closing_candidate = stop.get('closing_observation')
    milestones = [item.get(name) for name in ('first_effective_inspection', 'chapter_one_handoff', 'closing_observation')]
    milestones += item['answer_receipts']
    indexed = {point['event_id']: point for point in milestones if point and point.get('event_id')}
    completion = closing_candidate.get('completion') if isinstance(closing_candidate, dict) else None
    closing_ids = {completion.get(k) for k in ('host_event', 'execution_event') if isinstance(completion.get(k), str)} if isinstance(completion, dict) else set()
    raw_points = {}
    for event in read_records(directory / 'raw-event-index.dvlog', errors):
        if not isinstance(event, dict):
            errors.append('raw-event-index.dvlog: non-record packet')
            continue
        point = event_point(event)
        item['last_event'] = point
        if event.get('event_id') in closing_ids:
            raw_points[event['event_id']] = event
        if event.get('event_id') in indexed:
            original = indexed[event['event_id']]
            if (original.get('actor'), original.get('type')) == (point.get('actor'), point.get('type')):
                original.update(point)
            else:
                errors.append('Event index metadata differs from projected milestone: ' + str(event.get('event_id')))
        if event.get('type') == 'chapter_content_available':
            item['chapter_content_available'].append(point)
    # The generic index has no committed effect payload. A chapter summary can
    # confirm the terminal state, but cannot identify the action that caused it.
    item['closing_state_reported'] = item['chapter_summary'] not in (None, False, [], {})
    closed = (item['chapter_summary'] or {}).get('council_closed') or {}
    item['closing_state_confirmed'] = (closed.get('known') is True and closed.get('conflicted') is False
                                       and closed.get('value') is True)
    closing, reason = validate_closing(closing_candidate, projected, raw_points, viewers)
    item['closing_observation'] = closing
    item['closing_event'] = closing
    item['closing_event_status'] = 'actual-viewer-inspection' if closing else reason
    elapsed = item.get('execution_seconds')
    item['speed_target_seconds'] = 180
    item['speed_target_scope'] = 'session process only; excludes compilation, postprocessing and cleanup'
    item['speed_target_met'] = bool(item.get('evidence_complete') is True and
        item.get('source_consistent') is True and item.get('source_unchanged') is True and
        item.get('configuration_matches') is True and item.get('sealed') is True and not errors and
        type(elapsed) in (int, float) and math.isfinite(elapsed) and 0 <= elapsed <= 180 and
        closing and item['closing_state_confirmed'])
    item['text_scope'] = 'recorded player_presentation packets; count is not a completeness claim'
    return item


def summarize(directory, root):
    with case_guard(directory):
        try:
            return _summarize(directory, root)
        except (ValueError, TypeError, KeyError, AttributeError, OSError) as error:
            return {'scenario': directory.relative_to(root).as_posix(), 'status': 'invalid-report',
                    'speed_target_met': False, 'errors': [type(error).__name__ + ': ' + str(error)]}


def validate_closing(candidate, projected, raw, viewers):
    if not isinstance(candidate, dict) or not isinstance(candidate.get('completion'), dict):
        return None, 'missing-actual-completion'
    completion = candidate['completion']
    actor, action, execution = candidate.get('viewer'), candidate.get('action'), candidate.get('execution_event')
    host, prepared = completion.get('host_event'), completion.get('action_event')
    if (not all(isinstance(x, str) and x for x in (actor, action, execution, host, prepared)) or
            completion.get('actor') != actor or completion.get('execution_event') != execution or
            completion.get('status') != 'applied' or (viewers is not None and actor not in viewers)):
        return None, 'invalid-closing-completion'
    if action not in (actor + '.c2.daily.closing.central_hall', actor + '.c2.daily.closing.oath_court'):
        return None, 'not-a-closing-inspection'
    actual, host_event = projected.get(execution, {}), projected.get(host, {})
    if (actual.get('type') != 'action_execution' or actual.get('actor') != actor or
            actual.get('action_id') != action or actual.get('action_kind') != 'inspect' or
            (actual.get('detail') or {}).get('status') != 'applied' or actual.get('parents') != [host] or
            host_event.get('type') != 'host_completion' or host_event.get('actor') != actor):
        return None, 'closing-receipt-not-corroborated-by-projected-events'
    for identity, kind in ((execution, 'action_execution'), (host, 'host_completion')):
        if raw.get(identity, {}).get('type') != kind or raw[identity].get('actor') != actor:
            return None, 'closing-receipt-not-in-committed-index'
    # action_event is the host action sequence (e.g. action0), not an eN event ID.
    if (raw[host].get('host_action_event') != prepared or
            raw[execution].get('parent_ids') != [host] or
            raw[host].get('parent_ids') != host_event.get('parents')):
        return None, 'closing-host-action-or-parent-chain-mismatch'
    ordinals = [raw[identity].get('ordinal') for identity in (host, execution)]
    if not all(type(value) is int for value in ordinals) or not ordinals[0] < ordinals[1]:
        return None, 'closing-receipt-order-invalid'
    return {**event_point(raw[execution]), 'action_id': action, 'completion': completion}, None


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
             '仅读取已发布结果、二进制投影和事件索引；不打开原始 DAG，不推算缺失时间。正式运行窗口与 180 秒会话执行目标分别报告；执行耗时不冒充包含构建/后处理的命令总墙钟。', '',
             '| 场景 | 执行秒 | 超时 | 源码未变 | 证据完整 | 正文条目 | 180 秒目标 |',
             '| --- | ---: | --- | --- | --- | ---: | --- |']
    for item in summary['scenarios']:
        lines.append('| ' + ' | '.join(show(item.get(k)) for k in
                     ('scenario', 'execution_seconds', 'timeout', 'source_unchanged', 'evidence_complete',
                      'text_entries', 'speed_target_met')) + ' |')
    lines += ['', '| 场景 | 首次有效检视 | 第一章实际交接 | 章末真实检视 |', '| --- | --- | --- | --- |']
    for item in summary['scenarios']:
        lines.append('| ' + show(item['scenario']) + ' | ' + ' | '.join(
            point_text(item.get(key)) for key in
            ('first_effective_inspection', 'chapter_one_handoff', 'closing_observation')) + ' |')
    by_name = {item['scenario']: item for item in summary['scenarios']}
    if 'S1' in by_name and 'S4-foolish' in by_name:
        standard, foolish = by_name['S1'], by_name['S4-foolish']
        left, right = standard.get('run_config') or {}, foolish.get('run_config') or {}
        changed = sorted(key for key in left.keys() | right.keys() if left.get(key) != right.get(key))
        identical = bool(standard.get('module_hashes_20_60') and standard.get('module_hashes_20_60') == foolish.get('module_hashes_20_60'))
        runtime_equal = (standard.get('runtime_config') == foolish.get('runtime_config') and
                         standard.get('runtime_environment') == foolish.get('runtime_environment'))
        compiler_equal = (standard.get('compilation_input_fingerprint') is not None and
                          standard.get('compilation_input_fingerprint') == foolish.get('compilation_input_fingerprint'))
        lines += ['', '## S5 模型对照', '',
                  f"20–60 模块（含公共 35）指纹一致：{show(identical)}；变化配置：{', '.join(changed) or '无'}。",
                  f"完整编译输入一致：{show(compiler_equal)}；运行时配置/环境一致：{show(runtime_equal)}；仅 profile 配置变化：{show(changed == ['profile'] and runtime_equal)}。"]
    for item in summary['scenarios']:
        lines += ['', f"## {item['scenario']}", '', f"末事件：{point_text(item.get('last_event'))}。",
                  f"章末状态已确认：{show(item.get('closing_state_confirmed'))}；章末检视：{point_text(item.get('closing_observation'))}。",
                  f"检视核验状态：{show(item.get('closing_event_status'))}。",
                  f"领域核验状态：{show(item.get('causal_check_status'))}；核验完成：{show(item.get('causal_check_complete'))}。",
                  f"20–60 模块指纹：{show(item.get('model_fingerprint'))}。",
                  f"构建秒：{show(item.get('build_seconds'))}；会话执行秒：{show(item.get('execution_seconds'))}；后处理秒：{show(item.get('postprocess_seconds'))}；命令总墙钟秒：{show(item.get('command_wall_seconds'))}；含清理的场景总墙钟秒：{show(item.get('total_wall_seconds'))}。"]
        for point in item.get('chapter_content_available', []):
            lines.append(f"章节内容开放：{point_text(point)}。内容开放不代替实际交接。")
        if item.get('answer_receipts'):
            lines += ['', '| 事件 | 主体 | 游戏时间 | 实测进程毫秒 | 被接受 | 字面真值 | 本人依据 |',
                      '| --- | --- | ---: | ---: | --- | --- | --- |']
            for receipt in item['answer_receipts']:
                lines.append('| ' + ' | '.join(show(receipt.get(key)) for key in
                             ('event_id', 'actor', 'game_time', 'process_elapsed_ms', 'accepted', 'literal_truth', 'epistemic_support')) + ' |')
        if item.get('chapter_summary'):
            lines += ['', '| 章末字段 | known | conflicted | value |', '| --- | --- | --- | --- |']
            for key, cell in item['chapter_summary'].items():
                lines.append('| ' + key + ' | ' + ' | '.join(show((cell or {}).get(k)) for k in ('known', 'conflicted', 'value')) + ' |')
        if item.get('injury_facts'):
            lines += ['', '| 主体 | 存活 | 死亡 | 昏迷 | 石化沉睡 | 侵蚀暴露 | 本章重伤经历 | 当前运动障碍 | 当前呼吸衰竭 | 实际有效给药 |', '| --- | --- | --- | --- | --- | ---: | --- | --- | --- | --- |']
            for person in item['injury_facts']:
                cells = []
                for key in ('alive', 'dead', 'in_coma', 'stone_stupor', 'erosion_exposure', 'serious_injury', 'motor_impairment', 'respiratory_failure', 'anti_erosion_treated'):
                    cell = person.get(key) or {}
                    cells.append(show(cell.get('value')) if cell.get('known') is True and not cell.get('conflicted') else '未知/冲突')
                lines.append('| ' + show(person.get('actor')) + ' | ' + ' | '.join(cells) + ' |')
        for error in item.get('errors', []):
            lines.append(f"读取或运行错误：{show(error)}")
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path('artifacts/perf-opt/aggressive'))
    parser.add_argument('--scenario', action='append', help='Summarize this relative scenario directory; may be repeated')
    args = parser.parse_args()
    root = args.root.resolve()
    if not root.is_dir():
        parser.error(f'Root does not exist: {root}')
    directories = set()
    if args.scenario is not None:
        for scenario in args.scenario:
            selected = (root / scenario).resolve()
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
