"""Build and record one production formal-model session; no decisions in Python."""
import argparse
from fractions import Fraction
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
from dv_toolchain import LANGUAGE, ROOT, PROJECT, environment
from dv_sources import sources
from core_profile import summarize as summarize_profile


def record(value):
    if isinstance(value, list):
        if value and all(isinstance(p, list) and len(p) == 2 and isinstance(p[0], str) for p in value):
            return {p[0]: record(p[1]) for p in value}
        return [record(p) for p in value]
    return value


def clock(value):
    minute = Fraction(str(value))
    whole = int(minute)
    return f'第 {whole // 1440 + 1} 日 {whole % 1440 // 60:02}:{whole % 60:02}'


REASONING_FIELDS = {
    'mind_simulation': ('hypothesis', 'result.assumptions', 'result.predictions',
                        'result.trace', 'result.comparisons', 'result.expanded',
                        'result.remaining', 'result.stop_reason', 'source_adoptions'),
    'model_recheck': ('hypothesis', 'model_before', 'result.predictions',
                      'result.comparisons', 'result.expanded', 'result.remaining'),
    'factor_prune': ('certificates', 'rejections', 'expanded', 'remaining',
                     'work_remaining', 'source_adoptions'),
    'model_argument_unavailable': ('hypothesis', 'competitors', 'result.reason',
                                  'result.argument', 'result.expanded', 'result.remaining'),
    'model_argument_accepted': ('hypothesis', 'competitors', 'result.reason',
                               'result.argument', 'result.expanded', 'result.remaining'),
}


def field_availability(value, path):
    """Presence is a structural observation, never validation of a model or source."""
    for key in path.split('.'):
        if not isinstance(value, dict) or key not in value:
            return 'not-exported'
        value = value[key]
    return 'exported-empty' if value == [] or value == {} or value == '' else 'exported'


def project_evidence(source_path, destination):
    """Stream already recorded packets; never open the potentially huge trace file."""
    summary = {
        'schema': 'dv-evidence-index-v1', 'source': str(source_path.resolve()),
        'projector_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'scope': 'recorded debug projection only; no state reconstruction or content judgment',
        'event_inventory': {}, 'reasoning_field_inventory': {},
        'projection_configuration': None,
        'debug_end_observed': False, 'debug_end': None,
        'narrative_completion': 'not-assessed; review declared criteria against actual event receipts',
        'source_validation': 'not-performed; parent IDs and source fields are references, not validated proof',
        'invalid_source_lines': [],
    }
    digest = hashlib.sha256()
    with source_path.open('rb') as source, \
            (destination / 'actual-event-index.jsonl').open('x', encoding='utf-8') as actual, \
            (destination / 'reasoning-evidence.jsonl').open('x', encoding='utf-8') as reasoning:
        for number, raw_line in enumerate(source, 1):
            digest.update(raw_line)
            if not raw_line.strip():
                continue
            try:
                packet = record(json.loads(raw_line))
            except (json.JSONDecodeError, UnicodeDecodeError) as error:
                summary['invalid_source_lines'].append({'source_line': number, 'error': str(error)})
                continue
            if not isinstance(packet, dict):
                summary['invalid_source_lines'].append({'source_line': number, 'error': 'packet is not a record'})
                continue
            if packet.get('type') == 'debug_start':
                summary['projection_configuration'] = packet.get('configuration')
                continue
            if packet.get('type') == 'debug_end':
                summary['debug_end_observed'] = True
                summary['debug_end'] = packet
                summary['debug_end_source_line'] = number
                continue
            if packet.get('type') != 'debug_events':
                continue
            for index, event in enumerate(packet.get('batch', {}).get('events', [])):
                kind = event['type']
                inventory = summary['event_inventory'].setdefault(kind, {'occurrences': 0, 'first': None, 'last': None})
                pointer = {'source_line': number, 'batch_event_index': index,
                           'event_id': event['event_id'], 'actor': event.get('actor'),
                           'game_time': event.get('game_time')}
                inventory['occurrences'] += 1
                inventory['first'] = inventory['first'] or pointer
                inventory['last'] = pointer
                detail = event.get('detail', {})
                if kind in REASONING_FIELDS:
                    availability = {path: field_availability(detail, path) for path in REASONING_FIELDS[kind]}
                    for path, status in availability.items():
                        field_inventory = summary['reasoning_field_inventory'].setdefault(kind, {}).setdefault(path, {})
                        field_inventory[status] = field_inventory.get(status, 0) + 1
                    item = {**pointer, 'type': kind, 'meaning': 'record of subjective calculation; predictions are not actual effects',
                            'field_availability': availability, 'projection': event}
                    reasoning.write(json.dumps(item, ensure_ascii=False) + '\n')
                else:
                    # All other events remain available for criteria chosen by the reviewer.
                    # Selection/quotation/scheduling are explicitly not completed execution.
                    role = {
                        'action_execution': 'execution-receipt',
                        'host_completion': 'host-receipt-awaiting-DV-settlement',
                        'action_scheduled': 'scheduled-intent',
                        'planning_choice': 'subjective-choice',
                        'planning_stop': 'subjective-search-stop',
                        'chapter_content_available': 'content-availability',
                        'question_transition': 'protocol-transition',
                    }.get(kind, 'recorded-mechanism-event')
                    item = {**pointer, 'type': kind, 'evidence_role': role, 'parents': event.get('parents', []),
                            'action_id': event.get('action_id'), 'action_kind': event.get('action_kind'),
                            'purpose_sources': event.get('purpose_sources', []), 'detail': detail,
                            'execution_status': detail.get('status') if kind == 'action_execution' else None,
                            'meaning': 'recorded event; no inferred achievement or narrative completion'}
                    actual.write(json.dumps(item, ensure_ascii=False) + '\n')
    summary['source_sha256'] = digest.hexdigest()
    end = summary['debug_end'] or {}
    stop = end.get('stop_evidence', {})
    summary['stop_reasons'] = stop.get('reasons', [])
    summary['world_horizon_observed'] = 'world-horizon' in summary['stop_reasons']
    summary['chapter_summary_reported'] = stop.get('chapter_summary')
    summary['packet_stream_well_formed'] = not summary['invalid_source_lines']
    with (destination / 'evidence-summary.json').open('x', encoding='utf-8') as output:
        json.dump(summary, output, ensure_ascii=False, indent=2)
    return summary


def export(out, destination=None, scope='正式逻辑会话的实际可见正文；不含 Godot 画面。仅覆盖本轮已执行范围。'):
    destination = destination or out
    complete = True
    with (out / 'stdout.jsonl').open(encoding='utf-8') as source, \
            (destination / 'events.jsonl').open('x', encoding='utf-8') as events, \
            (destination / '02-引擎输出的玩家可见动作.txt').open('x', encoding='utf-8') as visible, \
            (destination / '调试轨迹.txt').open('x', encoding='utf-8') as debug, \
            (destination / 'format-notices.jsonl').open('x', encoding='utf-8') as notices:
        visible.write(scope + '\n\n')
        for number, line in enumerate(source, 1):
            if not line.strip():
                continue
            try:
                packet = record(json.loads(line))
            except json.JSONDecodeError as error:
                complete = False
                notices.write(json.dumps({'source_line': number, 'issue': 'invalid-or-truncated-json', 'error': str(error)}) + '\n')
                continue
            events.write(json.dumps(packet, ensure_ascii=False) + '\n')
            if packet.get('type') == 'player_presentation':
                p = packet['presentation']
                if not p.get('visible'):
                    continue
                judgment = p.get('judgment')
                blocks = [(p.get('surface_voice'), [p.get('surface', '')]), (p.get('voice'), p.get('lines', []))]
                emitted = False
                for kind, texts in blocks:
                    paragraphs = [part.strip() for text in texts for part in text.splitlines() if part.strip()]
                    if not paragraphs:
                        continue
                    emitted = True
                    voice = {'narration': '叙述', 'private-monologue': '独白', 'bodily-flash': '触觉闪回', 'visible-inspection': '观察', 'visible-action': '动作叙述', 'dialogue': '对白'}.get(kind, '正文（源记录未分型）')
                    name = p.get('speaker_name') or ('未具名说话者' if kind == 'dialogue' else '未具名主体')
                    visible.write(f"[{clock(p['game_time'])}] {name} · {voice}\n")
                    for paragraph in paragraphs:
                        visible.write(paragraph + '\n\n')
                if not emitted:
                    notices.write(json.dumps({'source_line': number, 'issue': 'visible-action-without-text', 'execution_event': p.get('execution_event'), 'action_id': p.get('action_id'), 'action_kind': p.get('action_kind'), 'has_judgment': isinstance(judgment, dict), 'note': '正文保持空白；结合行动类型区分移动与缺失文案'}, ensure_ascii=False) + '\n')
                if isinstance(judgment, dict):
                    outcome = {'answer_accepted': '本次答辩已获接受。', 'answer_rejected': '本次答辩未获接受。'}.get(judgment['status'], judgment['status'])
                    visible.write(f"[{clock(judgment.get('game_time', p['game_time']))}] 答辩回执\n{outcome}\n\n")
            elif packet.get('type') == 'debug_events':
                for event in packet['batch']['events']:
                    debug.write(f"[{clock(event['game_time'])}] {event['event_id']} {event['actor'] or '世界'} · {event['type']}\n")
                    if event['action_id'] or event['label']:
                        debug.write(f"行动：{event['action_id']} {event['label']}".rstrip() + '\n')
                    if event['parents']:
                        debug.write(f"来源事件：{', '.join(event['parents'])}\n")
                    if event['detail']:
                        for key, value in event['detail'].items():
                            if key == 'observations' and isinstance(value, list):
                                for observation in value:
                                    ref = observation.get('ref', {})
                                    field = '.'.join(str(ref.get(k, '')) for k in ('object', 'facet', 'field'))
                                    debug.write(f"观察：{field} = {json.dumps(observation.get('value'), ensure_ascii=False)}；来源 {observation.get('source', '')}/{observation.get('origin', '')}\n")
                            else:
                                debug.write(f"{key}：{json.dumps(value, ensure_ascii=False)}\n")
                    debug.write('\n')
    for name in ('02-引擎输出的玩家可见动作.txt', '调试轨迹.txt'):
        path = destination / name
        content = path.read_text(encoding='utf-8').rstrip()
        path.write_text(content + '\n' if content else '', encoding='utf-8')
    return complete


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--entry', type=Path, default=LANGUAGE / 'game/debug-run.dv')
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--timeout', type=int, default=600)
    parser.add_argument('--gc-mode', choices=('server', 'workstation'), default='server')
    parser.add_argument('--gc-heaps', type=int, default=min(4, os.cpu_count() or 1))
    parser.add_argument('--profile', action='store_true', help='Build an instrumented DV session and report per-function and work-type time')
    parser.add_argument('--evidence-from', type=Path, help='Only project an existing events.jsonl packet stream into a new --out directory; never build or execute a session')
    parser.add_argument('--detective-profile', default='Standard_Detective', choices=('Standard_Detective', 'Foolish_Detective'))
    parser.add_argument('--detective-zone', default='zone_yi', choices=('zone_yi', 'zone_jia', 'zone_bing', 'zone_ding'))
    parser.add_argument('--trace-compression', choices=('none', 'gzip'), default='none', help='Lossless storage for the full raw JSON trace; no field projection')
    args = parser.parse_args()
    if args.gc_heaps < 1:
        parser.error('--gc-heaps must be positive')
    if args.timeout < 1:
        parser.error('--timeout must be positive')
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    if args.evidence_from:
        evidence = project_evidence(args.evidence_from.resolve(), out)
        return 0 if evidence['packet_stream_well_formed'] else 1
    build = out / 'program'
    build.mkdir()
    env = environment()
    modules = sources([args.entry.resolve()])
    inputs = [path for path, _ in modules]
    inputs += [LANGUAGE / p for p in ('compiler/Runtime.cs', 'compiler/compiler.dv', 'tools/build_session.py', 'tools/dv_sources.py', 'tools/run_debug.py', 'tools/dv_toolchain.py')]
    if args.profile:
        inputs += [LANGUAGE / p for p in ('tools/DvProfiler.cs', 'tools/core_profile.py')]
    hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}
    normalized = {str(path): hashlib.sha256(text.encode('utf-8')).hexdigest() for path, text in modules}
    (out / 'source-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
    commands = [
        [sys.executable, str(LANGUAGE / 'tools/build_session.py'), '--entry', str(args.entry.resolve()), '--out', str(build), '--main', 'dvg_debug_run'] + (['--profile'] if args.profile else []),
        ['dotnet', 'build', str(build / 'Program.csproj'), '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false']]
    shutil.copyfile(LANGUAGE / 'compiler/Runtime.cs', build / 'Runtime.cs')
    (build / 'Program.csproj').write_text(PROJECT, encoding='utf-8')
    for index, cmd in enumerate(commands):
        with (out / f'build-{index}.txt').open('wb') as log:
            subprocess.run(cmd, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=240)
    manifest = json.loads((build / 'source-manifest.json').read_text())
    build_consistent = (
        all(Path(p).is_file() and hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p, h in hashes.items())
        and {entry['path']: entry['sha256'] for entry in manifest['modules']} == normalized
        and hashlib.sha256((build / 'Runtime.cs').read_bytes()).hexdigest() == hashes[str(LANGUAGE / 'compiler/Runtime.cs')])
    if not build_consistent:
        (out / 'result.json').write_text(json.dumps({'executor': 'formal-model', 'geometry': False, 'build_source_consistent': False, 'source_unchanged': False, 'trace_written': False, 'error': 'Sources changed during build or generated inputs differ; session not started.'}, indent=2), encoding='utf-8')
        return 1
    execution_env = env.copy()
    execution_env['DOTNET_gcServer'] = '1' if args.gc_mode == 'server' else '0'
    execution_env['DOTNET_GCHeapCount'] = format(args.gc_heaps, 'x')
    if args.profile:
        execution_env['DV_PROFILE_PATH'] = str(out / 'profile-raw.json')
    start = time.monotonic()
    trace_name = 'trace.json.gz' if args.trace_compression == 'gzip' else 'trace.json'
    result = {'executor': 'formal-model', 'geometry': False, 'build_source_consistent': True, 'entry': str(args.entry.resolve()), 'detective_profile': args.detective_profile, 'detective_zone': args.detective_zone, 'gc_mode_requested': args.gc_mode, 'gc_heap_count_requested': args.gc_heaps, 'runtime_environment': {key: value for key, value in execution_env.items() if key.startswith(('DOTNET_GC', 'DOTNET_gc'))}}
    try:
        with (out / 'stdout.jsonl').open('wb') as stdout, (out / 'stderr.txt').open('wb') as stderr:
            process = subprocess.run(['dotnet', str(build / 'bin/Release/net8.0/Program.dll'), str(out / trace_name), args.detective_profile, args.detective_zone], cwd=ROOT, env=execution_env, stdout=stdout, stderr=stderr, timeout=args.timeout)
            result['exit_code'] = process.returncode
    except subprocess.TimeoutExpired:
        result['timeout_seconds'] = args.timeout
    finally:
        result['elapsed_seconds'] = time.monotonic() - start
        result['source_unchanged'] = all(Path(p).is_file() and hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p, h in hashes.items())
        result['trace_path'] = trace_name
        result['trace_storage_encoding'] = args.trace_compression
        result['trace_written'] = (out / trace_name).is_file()
        result['trace_partial_written'] = (out / (trace_name + '.partial')).is_file()
        if args.profile and (out / 'profile-raw.json').is_file():
            try:
                summarize_profile(out / 'profile-raw.json', modules, out / 'profile-summary.json')
                result['profile_written'] = True
            except Exception as error:
                result['profile_error'] = str(error)
        try:
            result['output_well_formed'] = export(out)
            result['text_exported'] = True
            evidence = project_evidence(out / 'events.jsonl', out)
            result['evidence_projection'] = 'evidence-summary.json'
            result['logical_loop_finished'] = evidence['debug_end_observed']
            result['stop_reasons'] = evidence['stop_reasons']
            result['world_horizon_observed'] = evidence['world_horizon_observed']
            result['chapter_summary_reported'] = evidence['chapter_summary_reported']
            result['narrative_completion'] = evidence['narrative_completion']
            if 'timeout_seconds' in result:
                result['timeout_stage'] = 'trace-close-or-serialization' if result['logical_loop_finished'] else 'logical-session'
        except Exception as error:
            result['export_error'] = str(error)
        result['evidence_complete'] = bool(result.get('exit_code') == 0 and result.get('output_well_formed') and result.get('logical_loop_finished') and result['trace_written'] and not result['trace_partial_written'] and result['source_unchanged'])
        (out / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    return 0 if result['evidence_complete'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
