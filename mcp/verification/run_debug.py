"""Build and record one production formal-model session; no decisions in Python."""
import argparse
from collections import defaultdict
from fractions import Fraction
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "build"))
from dv_toolchain import LANGUAGE, ROOT, PROJECT, BUILD, VERIFICATION, environment, sources
from compiled_program import copy_verified, capture as capture_program, compiler_inputs, compiler_fingerprint
from matrix_lifecycle import case_guard, FLAGS
from raw_dag import inspect_journal
from binary_evidence import RecordWriter, iter_packets, records
from debug_checkpoints import validate_bundle, export_available, sha_file


def work_type(name):
    if name in ('dvs_capture_heard_utterances', 'dvs_capture_public_topics'):
        return 'heard-event-projection'
    if name in ('dvs_support_parents', 'dvs_selection_parents', 'dvs_topic_origin_speakers'):
        return 'evidence-provenance'
    if name in ('dv_get', 'dv_find', 'dv_has', 'dv_has_key', 'dv_same', 'dv_and', 'dv_or', 'dv_record_like'):
        return 'record-lookup-and-comparison'
    if name in ('dv_with', 'dv_with_many', 'dv_with_path', 'dv_clone', 'dv_append', 'dv_pair', 'dv_single'):
        return 'value-copy-and-update'
    if name.startswith('dvp_'):
        return 'planning'
    if name.startswith(('dvs_sim_', 'dvs_model_', 'dvs_cached_model')):
        return 'modeling'
    if name.startswith(('dvs_reason_', 'dvs_infer_')):
        return 'reasoning'
    if name.startswith(('dvs_speech_', 'dvs_prepare_speech_', 'dvs_listener_')):
        return 'speech'
    if name.startswith(('dvg_', 'dvs_session_', 'dvs_environment', 'dvs_owned_state_')):
        return 'orchestration'
    if name.startswith(('dv_predict', 'dv_infer', 'dv_enforce', 'dv_stage', 'dv_truth')):
        return 'laws'
    if name.startswith(('dv_', 'dvs_')):
        return 'core-or-session-other'
    return 'content-or-other'


def summarize_profile(raw_path, modules, destination):
    raw = json.loads(Path(raw_path).read_text(encoding='utf-8'))
    frequency = raw['frequency']
    locations = {}
    for path, source in modules:
        for match in re.finditer(r'^fn\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(', source, re.MULTILINE):
            locations[match.group(1)] = str(path)
    functions = []
    grouped = defaultdict(lambda: {'calls': 0, 'exclusive_ticks': 0})
    for name, data in raw['functions'].items():
        calls = data['Calls']
        ticks = data['ExclusiveTicks']
        kind = work_type(name)
        grouped[kind]['calls'] += calls
        grouped[kind]['exclusive_ticks'] += ticks
        functions.append({'name': name, 'source': locations.get(name), 'work_type': kind,
                          'calls': calls, 'exclusive_ms': ticks * 1000 / frequency,
                          'mean_exclusive_ms': ticks * 1000 / frequency / calls,
                          'inclusive_ms': data['InclusiveTicks'] * 1000 / frequency,
                          'mean_inclusive_ms': data['InclusiveTicks'] * 1000 / frequency / calls})
    total = sum(item['exclusive_ticks'] for item in grouped.values())
    types = [{'work_type': kind, 'calls': item['calls'],
              'exclusive_ms': item['exclusive_ticks'] * 1000 / frequency,
              'mean_exclusive_ms_per_call': item['exclusive_ticks'] * 1000 / frequency / item['calls'],
              'share_of_profiled_dv_percent': item['exclusive_ticks'] * 100 / total if total else 0}
             for kind, item in grouped.items()]
    result = {'basis': 'Generated DV calls and selected native runtime scopes; exclusive time includes uninstrumented ABI calls and profiler overhead. Work types are function-name groups; nested calls are charged to their own group. This is an instrumented formal-model run, not gameplay acceptance.',
              'process_elapsed_ms': raw['process_ticks'] * 1000 / frequency,
              'profiled_dv_exclusive_ms': total * 1000 / frequency,
              'work_types': sorted(types, key=lambda item: -item['exclusive_ms']),
              'functions': sorted(functions, key=lambda item: -item['exclusive_ms'])}
    Path(destination).write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return result


def write_result(directory, value):
    with RecordWriter(directory / 'result.dvlog') as output:
        output.append(value)


def record(value):
    if isinstance(value, list):
        if value and all(isinstance(p, list) and len(p) == 2 and isinstance(p[0], str) for p in value):
            return {p[0]: record(p[1]) for p in value}
        return [record(p) for p in value]
    return value


def dv_record(value):
    if isinstance(value, dict):
        return [[key, dv_record(item)] for key, item in value.items()]
    if isinstance(value, list):
        return [dv_record(item) for item in value]
    return value


def causal_check(out, result, source_hashes, normalized, env, timeout, causal_index='on',
                 causalbuildcache='on', reuse_program=None):
    """Read the sealed binary journal with the isolated 70-checks entry."""
    work = out / 'causal-program'
    work.mkdir()
    runtime = LANGUAGE / 'compiler/Runtime.cs'
    bridge = VERIFICATION / 'CausalCheckBridge.cs'
    project = PROJECT.replace('<OutputType>Exe</OutputType>',
                              '<OutputType>Exe</OutputType><StartupObject>CausalCheckBridge</StartupObject>')
    commands = [
        [sys.executable, str(BUILD / 'build_session.py'), '--entry', str(LANGUAGE / 'checks/70-checks.dv'),
         '--out', str(work), '--main', 'false'],
        ['dotnet', 'build', str(work / 'Program.csproj'), '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false']]
    build_started = time.monotonic()
    timing = result['causal_stages'] = {'build_mode': 'build', 'stage': 'build', 'build_complete': False,
                                       'opt_causalbuildcache': causalbuildcache == 'on'}
    compiler_id = compiler_fingerprint()
    candidate = Path(reuse_program).resolve().parent / 'causal-program' if reuse_program else None
    reusable = (causalbuildcache == 'on' and candidate is not None and
                (candidate.parent / 'compiled-causal-program.json').is_file())
    try:
        if reusable:
            timing['build_mode'] = 'reuse-validation'
            timing['reuse_source'] = str(candidate)
            copy_verified(candidate, work, source_hashes, False, normalized, compiler_id,
                          entry_kind='causal-check-70', manifest_name='compiled-causal-program.json')
        else:
            timing['cache_miss'] = ('disabled' if causalbuildcache != 'on' else
                                    'no-source' if candidate is None else 'no-verified-manifest')
            shutil.copyfile(runtime, work / 'Runtime.cs')
            shutil.copyfile(bridge, work / bridge.name)
            (work / 'Program.csproj').write_text(project, encoding='utf-8')
            for index, command in enumerate(commands):
                timing['stage'] = 'dv-generation' if index == 0 else 'dotnet-build'
                stage_started = time.monotonic()
                try:
                    with (out / f'causal-build-{index}.txt').open('xb') as log:
                        subprocess.run(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT,
                                       check=True, timeout=240)
                finally:
                    timing[timing['stage'] + '_seconds'] = time.monotonic() - stage_started
        timing['stage'] = 'source-verification'
        generated = json.loads((work / 'source-manifest.json').read_text(encoding='utf-8'))
        if ({entry['path']: entry['sha256'] for entry in generated['modules']} != normalized or
            generated.get('compiler') != compiler_id or generated.get('profile_instrumented') is not False or
            compiler_fingerprint() != compiler_id or
            (work / 'Program.csproj').read_text(encoding='utf-8') != project or
            sha_file(work / 'Runtime.cs') != source_hashes[str(runtime)] or
            sha_file(work / bridge.name) != source_hashes[str(bridge)] or
            any(not Path(path).is_file() or sha_file(path) != digest for path, digest in source_hashes.items())):
            raise ValueError('Causal checker inputs differ from the source frozen before the session')
        compiled = capture_program(work, source_hashes, False, normalized, compiler_id,
                                   entry_kind='causal-check-70',
                                   runtime_config={'opt_runtimespeed': env.get('DV_OPT_RUNTIMESPEED', '1') != '0'})
        manifest_target = out / 'compiled-causal-program.json'
        pending = manifest_target.with_suffix('.json.partial')
        pending.write_text(json.dumps(compiled, indent=2), encoding='utf-8')
        pending.replace(manifest_target)
        if reusable:
            timing['build_mode'] = 'verified-reuse'
        timing['build_complete'] = True
    except subprocess.TimeoutExpired:
        timing['timeout_stage'] = timing['stage']
        raise
    finally:
        timing['build_elapsed_seconds'] = time.monotonic() - build_started
    build_seconds = timing['build_elapsed_seconds']
    manifest = {'format': 'dv-causal-intake-v1', 'integrity': 'verified',
                'executor': 'formal-model', 'geometry': False,
                'raw_sha256': result['raw_sha256'], 'run_config': result['run_config'],
                'source_hashes': source_hashes,
                'source_manifest_sha256': sha_file(out / 'source-hashes.json')}
    manifest_path = out / 'causal-manifest.dv.json'
    manifest_path.write_text(json.dumps(dv_record(manifest), ensure_ascii=True), encoding='utf-8')
    report_path = out / 'causal-report.dvdag'
    started = time.monotonic()
    timing['stage'] = 'execution'
    try:
        with (out / 'causal-check.dvlog').open('xb') as stdout, (out / 'causal-check-stderr.txt').open('xb') as stderr:
            process = subprocess.run(['dotnet', str(work / 'bin/Release/net8.0/Program.dll'),
                                      str(manifest_path), str(out / result['raw_path']), str(report_path), '--opt-causal-index=' + causal_index],
                                     cwd=ROOT, env=env, stdout=stdout, stderr=stderr, timeout=timeout)
    except subprocess.TimeoutExpired:
        timing['timeout_stage'] = 'execution'
        raise
    finally:
        timing['execution_elapsed_seconds'] = time.monotonic() - started
    if process.returncode:
        raise RuntimeError(f'Causal checker exited {process.returncode}; see causal-check-stderr.txt')
    packets = [record(item) for item in records(out / 'causal-check.dvlog')]
    if len(packets) != 1 or packets[0].get('type') != 'causal_check' or not report_path.is_file():
        raise ValueError('Causal checker did not publish its binary report and summary')
    timing['stage'] = 'completed'
    return {**packets[0], 'opt_causal_index': causal_index == 'on', 'build_elapsed_seconds': build_seconds,
            'execution_elapsed_seconds': time.monotonic() - started,
            'report': report_path.name, 'summary': 'causal-check.dvlog'}


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
    """A reference preserves the original field without repeating its contents."""
    for key in path.split('.'):
        if not isinstance(value, dict) or key not in value:
            return 'not-exported'
        value = value[key]
        if isinstance(value, dict) and 'raw_ref' in value:
            return 'exported-reference'
    return 'exported-empty' if value == [] or value == {} or value == '' else 'exported'


class EvidenceProjector:
    """One-pass indices point into the untouched packet file, never copy models."""
    def __init__(self, source_path, destination, optimize=True):
        self.source_path = source_path
        self.optimize = optimize
        self.resolved_source = str(source_path.resolve())
        self.digest = hashlib.sha256()
        self.summary = {
            'schema': 'dv-evidence-index-v2', 'source': self.resolved_source,
            'projector_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            'scope': 'recorded packet projection only; original values remain in source packets/raw DAG',
            'event_inventory': {}, 'reasoning_field_inventory': {},
            'projection_configuration': None, 'raw_format': None,
            'debug_end_observed': False, 'debug_end': None,
            'narrative_completion': 'not-assessed; review actual receipts against declared criteria',
            'source_validation': 'not-performed; parent IDs are references, not validated proof',
            'invalid_source_records': [],
        }
        self.actual = RecordWriter(destination / 'actual-event-index.dvlog')
        self.reasoning = RecordWriter(destination / 'reasoning-evidence.dvlog')

    def consume(self, packet, number):
        if not isinstance(packet, dict):
            self.summary['invalid_source_records'].append({'source_record': number, 'error': 'packet is not a record'})
            return
        coverage = self.summary.setdefault('projection_coverage', {'last_event_cursor': 0, 'last_flushed_step': 0, 'inherited_event_count': 0})
        if packet.get('type') == 'debug_start':
            coverage['inherited_event_count'] = packet.get('inherited_event_count', 0)
            coverage['initial_steps'] = packet.get('initial_steps', 0)
            coverage['projection_every'] = packet.get('projection_every', 1)
            self.summary['projection_configuration'] = packet.get('configuration')
            self.summary['run_config'] = packet.get('run_config')
            self.summary['raw_format'] = packet.get('raw_format', 'json')
        elif packet.get('type') == 'debug_end':
            self.summary['debug_end_observed'] = True
            self.summary['debug_end'] = packet
            self.summary['debug_end_source_record'] = number
        elif packet.get('type') == 'debug_events':
            coverage['last_event_cursor'] = packet.get('batch', {}).get('next', coverage['last_event_cursor'])
            coverage['last_flushed_step'] = packet.get('through_step', coverage['last_flushed_step'])
            for index, event in enumerate(packet.get('batch', {}).get('events', [])):
                kind = event['type']
                pointer = {'source_record': number, 'batch_event_index': index,
                           'event_id': event['event_id'], 'actor': event.get('actor'),
                           'game_time': event.get('game_time')}
                inventory = self.summary['event_inventory'].setdefault(kind, {'occurrences': 0, 'first': None, 'last': None})
                inventory['occurrences'] += 1
                inventory['first'] = inventory['first'] or pointer
                inventory['last'] = pointer
                detail = event.get('detail') or {}
                if not isinstance(detail, dict):
                    detail = {}
                reference = {'packet_source': self.resolved_source if self.optimize else str(self.source_path.resolve()), **pointer,
                             'raw_event_ref': event.get('raw_event_ref', False)}
                if kind in REASONING_FIELDS:
                    availability = {path: field_availability(detail, path) for path in REASONING_FIELDS[kind]}
                    for path, status in availability.items():
                        statuses = self.summary['reasoning_field_inventory'].setdefault(kind, {}).setdefault(path, {})
                        statuses[status] = statuses.get(status, 0) + 1
                    result = detail.get('result') or {}
                    if not isinstance(result, dict):
                        result = {}
                    item = {**pointer, 'type': kind, 'field_availability': availability,
                            'projection_source': reference,
                            'calculation_scope': detail.get('calculation_scope', result.get('evaluation_scope', detail.get('scope'))),
                            'status': result.get('status'), 'stop_reason': result.get('stop_reason'),
                            'meaning': 'subjective calculation; full assumptions/predictions/comparisons remain at source'}
                    if self.optimize:
                        self.reasoning.append(item)
                    else:
                        self.reasoning.write(json.dumps(item, ensure_ascii=False) + '\n')
                else:
                    role = {'action_execution': 'execution-receipt',
                            'host_completion': 'host-receipt-awaiting-DV-settlement',
                            'action_scheduled': 'scheduled-intent', 'planning_choice': 'subjective-choice',
                            'chapter_content_available': 'content-availability'}.get(kind, 'recorded-mechanism-event')
                    item = {**pointer, 'type': kind, 'evidence_role': role,
                            'parents': event.get('parents', []), 'action_id': event.get('action_id'),
                            'action_kind': event.get('action_kind'), 'detail_source': reference,
                            'execution_status': detail.get('status') if kind == 'action_execution' else None,
                            'meaning': 'recorded event; no inferred narrative completion'}
                    if self.optimize:
                        self.actual.append(item)
                    else:
                        self.actual.write(json.dumps(item, ensure_ascii=False) + '\n')

    def finish(self, destination):
        self.actual.close()
        self.reasoning.close()
        self.summary['source_sha256'] = self.digest.hexdigest()
        stop = (self.summary['debug_end'] or {}).get('stop_evidence', {})
        self.summary['stop_reasons'] = stop.get('reasons', [])
        self.summary['world_horizon_observed'] = 'world-horizon' in self.summary['stop_reasons']
        self.summary['chapter_summary_reported'] = stop.get('chapter_summary')
        self.summary['packet_stream_well_formed'] = not self.summary['invalid_source_records']
        with RecordWriter(destination / 'evidence-summary.dvlog') as output:
            output.append(self.summary)
        return self.summary


def project_evidence(source_path, destination):
    projector = EvidenceProjector(source_path, destination)
    try:
        with source_path.open('rb') as source:
            for number, (raw, decoded) in enumerate(iter_packets(source), 1):
                projector.digest.update(raw)
                if not raw.strip():
                    continue
                try:
                    projector.consume(record(decoded if decoded is not None else json.loads(raw)), number)
                except (json.JSONDecodeError, UnicodeDecodeError) as error:
                    projector.summary['invalid_source_records'].append({'source_record': number, 'error': str(error)})
    finally:
        summary = projector.finish(destination)
    return summary


def export(out, destination=None, scope='正式逻辑会话已输出到stdout的实际可见正文；不含Godot画面。中断时未flush的原始DAG尾不在本文中，不代表完整会话正文。', raw_format='dag', with_summary=False, optimize=True):
    destination = destination or out
    source_path = out / 'stdout.dvlog'
    if not source_path.exists(): source_path = out / 'stdout.jsonl'  # old evidence, read-only
    projector = EvidenceProjector(source_path, destination, optimize=optimize)
    complete = True
    # The legacy normalized packet file remains available in default JSON mode.
    # DAG mode keeps untouched stdout as its sole packet source, avoiding a duplicate.
    events = None  # original binary stdout is the sole packet source
    try:
        with source_path.open('rb') as source, \
                (destination / '02-引擎输出的玩家可见动作.txt').open('x', encoding='utf-8') as visible, \
                (destination / '调试轨迹.txt').open('x', encoding='utf-8') as debug, \
                RecordWriter(destination / 'format-notices.dvlog') as notices:
            visible.write(scope + '\n\n')
            for number, (raw, decoded) in enumerate(iter_packets(source), 1):
                projector.digest.update(raw)
                if not raw.strip():
                    continue
                try:
                    packet = record(decoded if decoded is not None else json.loads(raw))
                except (json.JSONDecodeError, UnicodeDecodeError) as error:
                    complete = False
                    issue = {'source_record': number, 'issue': 'invalid-or-truncated-record', 'error': str(error)}
                    notices.write(json.dumps(issue) + '\n')
                    projector.summary['invalid_source_records'].append(issue)
                    continue
                projector.consume(packet, number)
                if not isinstance(packet, dict):
                    complete = False
                    continue
                if events:
                    events.write(json.dumps(packet, ensure_ascii=False) + '\n')
                if packet.get('type') == 'player_presentation':
                    p = packet['presentation']
                    if not p.get('visible'):
                        continue
                    judgment = p.get('judgment')
                    blocks = [(p.get('surface_voice'), [p.get('surface', '')]), (p.get('voice'), p.get('lines', []))]
                    emitted = False
                    for kind, texts in blocks:
                        paragraphs = [part.strip() for text in texts
                                      for part in (text.get('text', '') if isinstance(text, dict) else text).splitlines()
                                      if part.strip()]
                        if not paragraphs:
                            continue
                        emitted = True
                        voice = {'narration': '叙述', 'private-monologue': '独白', 'bodily-flash': '触觉闪回', 'visible-inspection': '观察', 'visible-action': '动作叙述', 'dialogue': '对白'}.get(kind, '正文（源记录未分型）')
                        name = p.get('speaker_name') or ('未具名说话者' if kind == 'dialogue' else '未具名主体')
                        visible.write(f"[{clock(p['game_time'])}] {name} · {voice}\n")
                        for paragraph in paragraphs:
                            visible.write(paragraph + '\n\n')
                    if not emitted:
                        notices.write(json.dumps({'source_record': number, 'issue': 'visible-action-without-text', 'execution_event': p.get('execution_event'), 'action_id': p.get('action_id'), 'action_kind': p.get('action_kind')}, ensure_ascii=False) + '\n')
                    if isinstance(judgment, dict):
                        outcome = {'answer_accepted': '本次答辩已获接受。', 'answer_rejected': '本次答辩未获接受。'}.get(judgment['status'], judgment['status'])
                        visible.write(f"[{clock(judgment.get('game_time', p['game_time']))}] 答辩回执\n{outcome}\n\n")
                elif packet.get('type') == 'debug_events':
                    for index, event in enumerate(packet['batch']['events']):
                        debug.write(f"[{clock(event['game_time'])}] {event['event_id']} {event['actor'] or '世界'} · {event['type']}\n")
                        if event['action_id'] or event['label']:
                            debug.write(f"行动：{event['action_id']} {event['label']}".rstrip() + '\n')
                        if event['parents']:
                            debug.write(f"来源事件：{', '.join(event['parents'])}\n")
                        detail = event.get('detail') or {}
                        if isinstance(detail, dict):
                            for key in ('status', 'reason', 'stop', 'score', 'plan_expansions', 'remaining'):
                                value = detail.get(key)
                                if isinstance(value, (str, int, float, bool)):
                                    debug.write(f'{key}：{value}\n')
                        debug.write(f"完整投影：stdout.dvlog 第{number}条记录 batch.events[{index}]；原始事件以raw_event_ref定位。\n\n")
    finally:
        if events:
            events.close()
        summary = projector.finish(destination)
    # Output was streamed with final newlines; never reread a large text artifact.
    return (complete, summary) if with_summary else complete


def _main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--entry', type=Path, default=LANGUAGE / 'checks/65-debug-runner.dv')
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--timeout', type=int, default=600)
    parser.add_argument('--projection-every', type=int, default=1, help='Flush diagnostic stdout every N actual completed steps; N>1 requires online raw DAG')
    parser.add_argument('--resume-checkpoint', type=Path, help='Explicit validated stage checkpoint bundle; never initializes a fresh session')
    parser.add_argument('--gc-mode', choices=('server', 'workstation'), default=None)
    parser.add_argument('--gc-heaps', type=int, default=None)
    parser.add_argument('--profile', action='store_true', help='Build an instrumented DV session and report per-function and work-type time')
    parser.add_argument('--reuse-program', type=Path, help='Reuse a previously fingerprinted Release program with identical complete source inputs')
    parser.add_argument('--opt-causalbuildcache', choices=('on', 'off'), default='on',
                        help='Reuse the verified sibling causal-program of --reuse-program; still execute every causal check')
    parser.add_argument('--evidence-from', type=Path, help='Only project an existing binary packet stream into a new --out directory; never build or execute a session')
    parser.add_argument('--causal-timeout', type=int, default=300,
                        help='Separate time limit for read-only 70-checks after a complete sealed session')
    parser.add_argument('--opt-causal-index', choices=('on', 'off'), default='on', help='Read-only checker event/ancestor index; does not affect session decisions')
    parser.add_argument('--opt-evidence-index', choices=('on', 'off'), default='on', help='Reuse decoded immutable journal fields during read-only evidence validation')
    parser.add_argument('--detective-profile', default='Standard_Detective', choices=('Standard', 'Foolish', 'Standard_Detective', 'Foolish_Detective', 'Shura_Detective'))
    parser.add_argument('--opening-seed', type=int, default=1)
    parser.add_argument('--homecoming-negotiation', action='store_true', help='Enable declared homecoming negotiation candidates; does not seed votes or outcomes')
    parser.add_argument('--su-initial-room', choices=('hall', 'R08', 'R05'), default='hall')
    parser.add_argument('--private-execution-benefit-multiplier', type=int, choices=(1, 2), default=1)
    parser.add_argument('--detective-zone', default='zone_yi', choices=('zone_yi', 'zone_jia', 'zone_bing', 'zone_ding', 'none'))
    parser.add_argument('--trace-compression', choices=('none',), default='none', help='Deprecated compatibility argument; JSON recordings are prohibited')
    parser.add_argument('--raw-format', choices=('dag',), default='dag', help='Lossless typed binary DAG; JSON recording is forbidden')
    optimization_names = FLAGS
    for name in optimization_names:
        parser.add_argument('--opt-' + name, choices=('on', 'off'), default='on')
    parser.add_argument('--prune-top-k', type=int, default=3)
    parser.add_argument('--plan-depth', type=int, default=3)
    parser.add_argument('--plan-time-bucket', type=int, default=60)
    parser.add_argument('--opt-dirty-verify', type=int, nargs='?', const=100, default=0, help='Recheck dependency cache every N accesses; no argument means 100')
    parser.add_argument('--opt-mask-verify', action='store_true')
    parser.add_argument('--selective-planning', choices=('on', 'off'), default='on')
    parser.add_argument('--stop-at-chapter-end', choices=('on', 'off'), default='on', help='Stop after an objective chapter-two closing record and a successful closing inspection by a recorded viewer')
    args = parser.parse_args()
    # Single-thread CLI defaults; explicit GC arguments retain precedence.
    if args.gc_mode is None:
        args.gc_mode = 'server'
    if args.gc_heaps is None:
        args.gc_heaps = min(8 if args.opt_gc == 'on' else 4, os.cpu_count() or 1)
    args.detective_profile = {'Standard': 'Standard_Detective', 'Foolish': 'Foolish_Detective'}.get(args.detective_profile, args.detective_profile)
    if args.prune_top_k < 1:
        parser.error('--prune-top-k must be positive')
    if args.plan_depth < 1 or args.plan_time_bucket < 1 or args.opt_dirty_verify < 0:
        parser.error('Planning depth/bucket must be positive; dirty verification interval must be nonnegative')
    if args.opening_seed < 0:
        parser.error('--opening-seed must be nonnegative')
    run_config = {'seed': args.opening_seed, 'profile': args.detective_profile, 'detective_zone': args.detective_zone, 'su_initial_room': args.su_initial_room, 'private_execution_benefit_multiplier': args.private_execution_benefit_multiplier, 'homecoming_negotiation': args.homecoming_negotiation}
    run_config.update({'opt_' + name: getattr(args, 'opt_' + name) == 'on' for name in optimization_names})
    run_config.update(prune_top_k=args.prune_top_k, selective_planning=args.selective_planning == 'on', stop_at_chapter_end=args.stop_at_chapter_end == 'on')
    run_config.update(prune_depth=args.plan_depth, plan_time_bucket=args.plan_time_bucket, opt_dirty_verify=args.opt_dirty_verify, opt_mask_verify=args.opt_mask_verify)
    if args.projection_every < 1:
        parser.error('--projection-every must be positive')
    if args.projection_every > 1 and args.raw_format != 'dag':
        parser.error('Batched projection requires --raw-format dag; original raw events cannot be disabled')
    if args.resume_checkpoint and (args.raw_format != 'dag' or args.entry.resolve() != (LANGUAGE / 'checks/65-debug-runner.dv').resolve()):
        parser.error('Restore requires the production debug-run entry and --raw-format dag')
    if args.gc_heaps < 1:
        parser.error('--gc-heaps must be positive')
    if args.timeout < 1:
        parser.error('--timeout must be positive')
    if args.causal_timeout < 1:
        parser.error('--causal-timeout must be positive')
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    if args.evidence_from:
        evidence = project_evidence(args.evidence_from.resolve(), out)
        return 0 if evidence['packet_stream_well_formed'] else 1
    resume_bundle = None
    resume_save = None
    if args.resume_checkpoint:
        try:
            resume_bundle, resume_save = validate_bundle(args.resume_checkpoint)
            saved_config = resume_bundle['metadata']['run_config']
            config_options = {'--opening-seed': 'seed', '--detective-profile': 'profile', '--detective-zone': 'detective_zone', '--su-initial-room': 'su_initial_room', '--private-execution-benefit-multiplier': 'private_execution_benefit_multiplier', '--homecoming-negotiation': 'homecoming_negotiation'}
            config_options.update({'--opt-' + name: 'opt_' + name for name in optimization_names})
            config_options.update({'--prune-top-k': 'prune_top_k', '--selective-planning': 'selective_planning', '--stop-at-chapter-end': 'stop_at_chapter_end'})
            config_options.update({'--plan-depth': 'prune_depth', '--plan-time-bucket': 'plan_time_bucket', '--opt-dirty-verify': 'opt_dirty_verify', '--opt-mask-verify': 'opt_mask_verify'})
            explicit_options = {item.split('=', 1)[0] for item in sys.argv[1:]}
            for option, key in config_options.items():
                if option in explicit_options and run_config[key] != saved_config[key]:
                    raise ValueError(f'Restore configuration conflicts with checkpoint: {option}')
            run_config = saved_config
            if "--gc-mode" not in explicit_options:
                args.gc_mode = "server"
            if "--gc-heaps" not in explicit_options:
                args.gc_heaps = min(8 if run_config.get("opt_gc", False) else 4, os.cpu_count() or 1)
            args.profile = resume_bundle['metadata']['provenance'].get('instrumented', False)
            args.opening_seed = run_config['seed']
            args.detective_profile = run_config['profile']
            args.detective_zone = run_config['detective_zone']
            args.su_initial_room = run_config['su_initial_room']
            args.private_execution_benefit_multiplier = run_config['private_execution_benefit_multiplier']
            args.homecoming_negotiation = run_config['homecoming_negotiation']
        except Exception as error:
            write_result(out, {'session_started': False, 'restore_rejected': True, 'error': str(error)})
            return 1
    build = out / 'program'
    build.mkdir()
    env = environment()
    modules = sources([args.entry.resolve()])
    checker_modules = sources([LANGUAGE / 'checks/70-checks.dv'])
    inputs = [path for path, _ in modules]
    inputs += [path for path, _ in checker_modules]
    inputs += compiler_inputs()
    inputs += [BUILD / 'build_session.py', BUILD / 'dv_toolchain.py']
    inputs += [VERIFICATION / p for p in ('run_debug.py', 'raw_dag.py', 'binary_evidence.py', 'debug_checkpoints.py', 'DebugCheckpointResume.cs', 'CausalCheckBridge.cs', 'compiled_program.py', 'matrix_lifecycle.py')]
    if args.profile:
        inputs += [VERIFICATION / 'DvProfiler.cs']
    hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}
    normalized = {str(path): hashlib.sha256(text.encode('utf-8')).hexdigest() for path, text in modules}
    checker_normalized = {str(path): hashlib.sha256(text.encode('utf-8')).hexdigest() for path, text in checker_modules}
    (out / 'source-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
    verification_config = {'opt_causal_index': args.opt_causal_index == 'on',
                           'opt_causalbuildcache': args.opt_causalbuildcache == 'on',
                           'timeout_seconds': args.causal_timeout,
                           'opt_evidence_index': args.opt_evidence_index == 'on'}
    provenance = {'format': 'dv-debug-provenance-v1', 'source_hashes': hashes, 'source_manifest_sha256': sha_file(out / 'source-hashes.json'), 'run_config': run_config, 'instrumented': args.profile, 'runtime_config': {'opt_runtimespeed': args.opt_runtimespeed == 'on'}, 'verification_config': verification_config,
                  'verification_config_sha256': hashlib.sha256(json.dumps(verification_config, sort_keys=True).encode('utf-8')).hexdigest()}
    provenance_path = out / 'run-provenance.dv.json'
    provenance_path.write_text(json.dumps(dv_record(provenance), ensure_ascii=True), encoding='utf-8')
    restore_input_hashes = {}
    if resume_save:
        restore_input_hashes = {str(args.resume_checkpoint.resolve()): sha_file(args.resume_checkpoint), str(resume_save): sha_file(resume_save), resume_bundle['raw_source']: resume_bundle['raw_sha256']}
    (out / '.gitattributes').write_text('*.dvlog* -text -diff\n*.dvdag* -text -diff\n*.dvsave* -text -diff\nstderr.txt -text -diff\n', encoding='utf-8')
    commands = [
        [sys.executable, str(BUILD / 'build_session.py'), '--entry', str(args.entry.resolve()), '--out', str(build), '--main', 'dvg_debug_run'] + (['--profile'] if args.profile else []),
        ['dotnet', 'build', str(build / 'Program.csproj'), '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false']]
    shutil.copyfile(LANGUAGE / 'compiler/Runtime.cs', build / 'Runtime.cs')
    project = PROJECT
    if args.profile:
        project = project.replace("<OutputType>Exe</OutputType>", "<OutputType>Exe</OutputType><DefineConstants>$(DefineConstants);DV_PROFILE_NATIVE</DefineConstants>")
    if resume_bundle:
        shutil.copyfile(VERIFICATION / 'DebugCheckpointResume.cs', build / 'DebugCheckpointResume.cs')
        project = project.replace('<OutputType>Exe</OutputType>', '<OutputType>Exe</OutputType><StartupObject>DebugCheckpointResume</StartupObject>')
    (build / 'Program.csproj').write_text(project, encoding='utf-8')
    build_start = time.monotonic()
    build_stages = []
    if args.reuse_program:
        if resume_bundle:
            raise ValueError('A restored session requires its own generated entry point')
        reuse = args.reuse_program.resolve()
        copy_verified(reuse, build, hashes, args.profile, normalized, compiler_fingerprint())
        build_stages.append({'stage': 'verified-program-reuse', 'completed': True,
                             'elapsed_seconds': time.monotonic() - build_start, 'source': str(reuse)})
        commands = []
        (out / 'build-timing.json').write_text(json.dumps({
            'elapsed_seconds': time.monotonic() - build_start, 'stages': build_stages,
            'scope': 'engineering program reuse only; excludes session execution'}, indent=2), encoding='utf-8')
    for index, cmd in enumerate(commands):
        stage_start = time.monotonic()
        stage_completed = False
        try:
            with (out / f'build-{index}.txt').open('wb') as log:
                subprocess.run(cmd, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=240)
            stage_completed = True
        finally:
            build_stages.append({'stage': 'dv-generation' if index == 0 else 'dotnet-build', 'completed': stage_completed, 'elapsed_seconds': time.monotonic() - stage_start})
            with (out / 'build-timing.json').open('w', encoding='utf-8') as output:
                json.dump({'elapsed_seconds': time.monotonic() - build_start, 'stages': build_stages, 'scope': 'engineering compilation only; excludes session execution'}, output, indent=2)
    build_elapsed = time.monotonic() - build_start
    manifest = json.loads((build / 'source-manifest.json').read_text())
    build_consistent = (
        all(Path(p).is_file() and hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p, h in hashes.items())
        and {entry['path']: entry['sha256'] for entry in manifest['modules']} == normalized
        and manifest.get('profile_instrumented') is args.profile
        and manifest.get('compiler') == compiler_fingerprint()
        and (build / 'Program.csproj').read_text(encoding='utf-8') == project
        and hashlib.sha256((build / 'Runtime.cs').read_bytes()).hexdigest() == hashes[str(LANGUAGE / 'compiler/Runtime.cs')]
        and all(Path(p).is_file() and sha_file(p) == h for p, h in restore_input_hashes.items()))
    if not build_consistent:
        write_result(out, {'executor': 'formal-model', 'geometry': False, 'run_config': run_config, 'build_source_consistent': False, 'source_unchanged': False, 'trace_written': False, 'error': 'Sources changed during build or generated inputs differ; session not started.'})
        return 1
    compiled = capture_program(build, hashes, args.profile, normalized, compiler_fingerprint(),
                               entry_kind='checkpoint-resume' if resume_bundle else 'fresh-debug',
                               runtime_config={'opt_runtimespeed': args.opt_runtimespeed == 'on'})
    (out / 'compiled-program.json').write_text(json.dumps(compiled, indent=2), encoding='utf-8')
    execution_env = env.copy()
    execution_env['DV_OPT_RUNTIMESPEED'] = '1' if args.opt_runtimespeed == 'on' else '0'
    for name in ('cow', 'mask', 'lazyinternal', 'mask_verify'):
        execution_env['DV_OPT_' + name.upper()] = '1' if run_config.get('opt_' + name, False) else '0'
    execution_env['DV_OPT_APPENDSHARE'] = '1' if run_config.get('opt_appendshare', False) else '0'
    execution_env['DV_OPT_PLANCACHE'] = '1' if run_config.get('opt_plancache', False) else '0'
    execution_env['DV_OPT_DECLCACHE'] = '1' if run_config.get('opt_declcache', False) else '0'
    execution_env['DV_OPT_READVIEW'] = '1' if run_config.get('opt_readview', False) else '0'
    execution_env['DV_OPT_RECORDCACHE'] = '1' if run_config.get('opt_recordcache', False) else '0'
    execution_env['DV_OPT_PREDICATE'] = '1' if run_config.get('opt_predicate', False) else '0'
    execution_env['DV_OPT_PREDICATECACHE'] = '1' if run_config.get('opt_predicatecache', False) else '0'
    execution_env['DOTNET_gcServer'] = '1' if args.gc_mode == 'server' else '0'
    execution_env['DOTNET_GCHeapCount'] = format(args.gc_heaps, 'x')
    if args.profile:
        execution_env['DV_PROFILE_PATH'] = str(out / 'profile-raw.json')
    execution_env['DV_BINARY_STDOUT'] = '1'
    if (any(not Path(path).is_file() or sha_file(path) != digest for path, digest in hashes.items())
            or compiler_fingerprint() != manifest['compiler']):
        write_result(out, {'session_started': False, 'build_source_consistent': False,
                           'source_unchanged': False, 'error': 'Sources changed after program fingerprinting; session not started.'})
        return 1
    start = time.monotonic()
    trace_name = 'raw-events.dvdag'
    result = {'executor': 'formal-model', 'geometry': False, 'run_config': run_config, 'build_source_consistent': True, 'entry': str(args.entry.resolve()), 'detective_profile': args.detective_profile, 'detective_zone': args.detective_zone, 'raw_format': args.raw_format, 'build_elapsed_seconds': build_elapsed, 'build_stages': build_stages, 'gc_mode_requested': args.gc_mode, 'gc_heap_count_requested': args.gc_heaps, 'runtime_environment': {key: value for key, value in execution_env.items() if key.upper().startswith(('DOTNET_GC', 'DOTNET_TIERED', 'COMPLUS_TIERED', 'DV_OPT_'))}}
    result['runtime_config'] = {'opt_runtimespeed': args.opt_runtimespeed == 'on'}
    result['runtime_config_matches'] = ((execution_env['DV_OPT_RUNTIMESPEED'] == '1') == run_config['opt_runtimespeed'])
    result['projection_every'] = args.projection_every
    result['resumed'] = resume_bundle is not None
    result['restore_input_hashes'] = restore_input_hashes
    if resume_bundle:
        result['restore_origin'] = {key: resume_bundle['metadata'][key] for key in ('checkpoint', 'commit_frame', 'game_time', 'steps', 'last_event')}
    try:
        with (out / 'stdout.dvlog').open('xb') as stdout, (out / 'stderr.txt').open('wb') as stderr:
            program_args = [str(out / trace_name), args.detective_profile, args.detective_zone, args.raw_format, str(args.opening_seed), args.su_initial_room, str(args.private_execution_benefit_multiplier), str(args.homecoming_negotiation).lower(), str(args.projection_every), str(provenance_path), str(resume_save) if resume_save else '']
            program_args.append(json.dumps(dv_record(run_config), ensure_ascii=True, separators=(',', ':')))
            process = subprocess.run(['dotnet', str(build / 'bin/Release/net8.0/Program.dll'), *program_args], cwd=ROOT, env=execution_env, stdout=stdout, stderr=stderr, timeout=args.timeout)
            result['exit_code'] = process.returncode
    except subprocess.TimeoutExpired:
        result['timeout_seconds'] = args.timeout
    finally:
        result['elapsed_seconds'] = time.monotonic() - start
        result['execution_elapsed_seconds'] = result['elapsed_seconds']
        postprocess_start = time.monotonic()
        postprocess_stages = {}
        postprocess_stage_start = postprocess_start
        result['source_unchanged'] = all(Path(p).is_file() and hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p, h in {**hashes, **restore_input_hashes}.items())
        postprocess_stages['initial_source_verification_seconds'] = time.monotonic() - postprocess_stage_start
        postprocess_stage_start = time.monotonic()
        result['raw_path'] = trace_name
        result['trace_path'] = trace_name
        result['trace_storage_encoding'] = 'dv-raw-dag-v2'
        result['raw_written'] = (out / trace_name).is_file()
        result['raw_partial_written'] = (out / (trace_name + '.partial')).is_file()
        result['trace_written'] = result['raw_written']
        result['trace_partial_written'] = result['raw_partial_written']
        if args.raw_format == 'dag':
            raw_source = out / (trace_name if result['raw_written'] else trace_name + '.partial')
            try:
                raw_summary = inspect_journal(raw_source, out / 'raw-event-index.dvlog', optimize=args.opt_evidence_index == 'on')
                with (out / 'raw-dag-summary.json').open('x', encoding='utf-8') as output:
                    json.dump(raw_summary, output, ensure_ascii=False, indent=2)
                result['raw_dag_summary'] = 'raw-dag-summary.json'
                result['raw_sha256'] = raw_summary['source_sha256']
                result['raw_event_index'] = 'raw-event-index.dvlog'
                result['raw_sealing_complete'] = bool(result['raw_written'] and raw_summary['complete_sealing'])
                result['raw_committed_events'] = raw_summary['committed_events']
                result['raw_event_prefix_available'] = raw_summary['committed_events'] > 0
                result['raw_evidence_scope'] = raw_summary['scope']
                result['stage_checkpoints'] = [{**item, 'storage': 'committed exact raw DAG; tagged Save export is explicit, never automatic'} for item in raw_summary.get('checkpoint_commits', []) if item['name'] in ('stage-day3', 'stage-day5')]
                result['checkpoint_save_exports'] = 'not automatically expanded; use debug_checkpoints.py on an actual committed stage'
            except Exception as error:
                result['raw_sealing_complete'] = False
                result['raw_validation_error'] = str(error)
        else:
            result['raw_sealing_complete'] = bool(result['trace_written'] and not result['trace_partial_written'])
        postprocess_stages['raw_validation_and_index_seconds'] = time.monotonic() - postprocess_stage_start
        postprocess_stage_start = time.monotonic()
        if args.profile and (out / 'profile-raw.json').is_file():
            try:
                summarize_profile(out / 'profile-raw.json', modules, out / 'profile-summary.json')
                result['profile_written'] = True
            except Exception as error:
                result['profile_error'] = str(error)
        postprocess_stages['profile_summary_seconds'] = time.monotonic() - postprocess_stage_start
        postprocess_stage_start = time.monotonic()
        try:
            result['output_well_formed'], evidence = export(out, raw_format=args.raw_format, with_summary=True, optimize=args.opt_evidence_index == 'on')
            result['text_exported'] = True
            result['actual_run_config'] = evidence.get('run_config')
            result['run_config_matches'] = result['actual_run_config'] == run_config and result['runtime_config_matches']
            result['event_packet_source'] = 'stdout.dvlog'
            result['stdout_projection_coverage'] = evidence.get('projection_coverage')
            coverage = evidence.get('projection_coverage', {})
            if args.raw_format == 'dag' and 'raw_committed_events' in result:
                projected = max(0, coverage.get('last_event_cursor', 0) - coverage.get('inherited_event_count', 0))
                result['raw_committed_events_not_yet_projected'] = max(0, result['raw_committed_events'] - projected)
            result['player_text_scope'] = 'flushed stdout presentations only; unflushed raw tail is not reconstructed or claimed complete'
            result['completed_steps_after_last_flush'] = 0 if evidence['debug_end_observed'] else 'unknown in interrupted computation; original committed raw events retained'
            result['evidence_projection'] = 'evidence-summary.dvlog'
            result['logical_loop_finished'] = evidence['debug_end_observed']
            result['stop_reasons'] = evidence['stop_reasons']
            result['world_horizon_observed'] = evidence['world_horizon_observed']
            result['chapter_summary_reported'] = evidence['chapter_summary_reported']
            result['narrative_completion'] = evidence['narrative_completion']
            if 'timeout_seconds' in result:
                result['timeout_stage'] = 'trace-close-or-serialization' if result['logical_loop_finished'] else 'logical-session'
        except Exception as error:
            result['export_error'] = str(error)
        postprocess_stages['stdout_export_and_index_seconds'] = time.monotonic() - postprocess_stage_start
        result['evidence_complete'] = bool(result.get('exit_code') == 0 and result.get('output_well_formed')
                                           and result.get('logical_loop_finished') and result['raw_sealing_complete']
                                           and result['source_unchanged'] and result.get('run_config_matches') is True)
        postprocess_stage_start = time.monotonic()
        result['causal_check_complete'] = False
        result['main_causal_supported'] = False
        if result['evidence_complete']:
            try:
                result['causal_check'] = causal_check(out, result, hashes, checker_normalized,
                                                     execution_env, args.causal_timeout, args.opt_causal_index,
                                                     args.opt_causalbuildcache, args.reuse_program)
                result['causal_check_reason'] = result['causal_check']['domain_evaluation_reason']
                result['causal_check_complete'] = result['causal_check'].get('domain_evaluation_complete') is True
                result['causal_check_status'] = 'completed' if result['causal_check_complete'] else 'not-completed'
                result['main_causal_supported'] = (result['causal_check_complete'] and
                                                  result['causal_check'].get('main_causal_supported') is True)
            except subprocess.TimeoutExpired:
                result['causal_check_status'] = 'timeout'
                stage = result.get('causal_stages', {}).get('timeout_stage', 'unknown')
                result['causal_check_error'] = f'Read-only causal checker timed out during {stage}'
            except Exception as error:
                result['causal_check_status'] = 'error'
                result['causal_check_error'] = str(error)
        else:
            result['causal_check_status'] = 'not-run-incomplete-evidence'
            result['causal_check_reason'] = 'incomplete_evidence'
        postprocess_stages['causal_build_and_check_seconds'] = time.monotonic() - postprocess_stage_start
        postprocess_stage_start = time.monotonic()
        result['source_unchanged'] = all(Path(path).is_file() and sha_file(path) == digest
                                         for path, digest in {**hashes, **restore_input_hashes}.items())
        result['evidence_complete'] = result['evidence_complete'] and result['source_unchanged']
        postprocess_stages['final_source_verification_seconds'] = time.monotonic() - postprocess_stage_start
        result['postprocess_stages'] = postprocess_stages
        result['postprocess_elapsed_seconds'] = time.monotonic() - postprocess_start
        write_result(out, result)
    return 0 if result['evidence_complete'] and result['causal_check_complete'] else 1


def main():
    # Same-case readers/runners hold an OS lease until all postprocessing ends.
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument('--out', type=Path)
    known, _ = parser.parse_known_args()
    with case_guard(known.out):
        return _main()


if __name__ == '__main__':
    raise SystemExit(main())
