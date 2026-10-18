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
from raw_dag import inspect_journal
from debug_checkpoints import validate_bundle, export_available, sha_file


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
    def __init__(self, source_path, destination):
        self.source_path = source_path
        self.digest = hashlib.sha256()
        self.summary = {
            'schema': 'dv-evidence-index-v2', 'source': str(source_path.resolve()),
            'projector_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
            'scope': 'recorded packet projection only; original values remain in source packets/raw DAG',
            'event_inventory': {}, 'reasoning_field_inventory': {},
            'projection_configuration': None, 'raw_format': None,
            'debug_end_observed': False, 'debug_end': None,
            'narrative_completion': 'not-assessed; review actual receipts against declared criteria',
            'source_validation': 'not-performed; parent IDs are references, not validated proof',
            'invalid_source_lines': [],
        }
        self.actual = (destination / 'actual-event-index.jsonl').open('x', encoding='utf-8')
        self.reasoning = (destination / 'reasoning-evidence.jsonl').open('x', encoding='utf-8')

    def consume(self, packet, number):
        if not isinstance(packet, dict):
            self.summary['invalid_source_lines'].append({'source_line': number, 'error': 'packet is not a record'})
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
            self.summary['debug_end_source_line'] = number
        elif packet.get('type') == 'debug_events':
            coverage['last_event_cursor'] = packet.get('batch', {}).get('next', coverage['last_event_cursor'])
            coverage['last_flushed_step'] = packet.get('through_step', coverage['last_flushed_step'])
            for index, event in enumerate(packet.get('batch', {}).get('events', [])):
                kind = event['type']
                pointer = {'source_line': number, 'batch_event_index': index,
                           'event_id': event['event_id'], 'actor': event.get('actor'),
                           'game_time': event.get('game_time')}
                inventory = self.summary['event_inventory'].setdefault(kind, {'occurrences': 0, 'first': None, 'last': None})
                inventory['occurrences'] += 1
                inventory['first'] = inventory['first'] or pointer
                inventory['last'] = pointer
                detail = event.get('detail') or {}
                if not isinstance(detail, dict):
                    detail = {}
                reference = {'packet_source': str(self.source_path.resolve()), **pointer,
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
                    self.actual.write(json.dumps(item, ensure_ascii=False) + '\n')

    def finish(self, destination):
        self.actual.close()
        self.reasoning.close()
        self.summary['source_sha256'] = self.digest.hexdigest()
        stop = (self.summary['debug_end'] or {}).get('stop_evidence', {})
        self.summary['stop_reasons'] = stop.get('reasons', [])
        self.summary['world_horizon_observed'] = 'world-horizon' in self.summary['stop_reasons']
        self.summary['chapter_summary_reported'] = stop.get('chapter_summary')
        self.summary['packet_stream_well_formed'] = not self.summary['invalid_source_lines']
        with (destination / 'evidence-summary.json').open('x', encoding='utf-8') as output:
            json.dump(self.summary, output, ensure_ascii=False, indent=2)
        return self.summary


def project_evidence(source_path, destination):
    projector = EvidenceProjector(source_path, destination)
    try:
        with source_path.open('rb') as source:
            for number, raw in enumerate(source, 1):
                projector.digest.update(raw)
                if not raw.strip():
                    continue
                try:
                    projector.consume(record(json.loads(raw)), number)
                except (json.JSONDecodeError, UnicodeDecodeError) as error:
                    projector.summary['invalid_source_lines'].append({'source_line': number, 'error': str(error)})
    finally:
        summary = projector.finish(destination)
    return summary


def export(out, destination=None, scope='正式逻辑会话已输出到stdout的实际可见正文；不含Godot画面。中断时未flush的原始DAG尾不在本文中，不代表完整会话正文。', raw_format='json', with_summary=False):
    destination = destination or out
    source_path = out / 'stdout.jsonl'
    projector = EvidenceProjector(source_path, destination)
    complete = True
    # The legacy normalized packet file remains available in default JSON mode.
    # DAG mode keeps untouched stdout as its sole packet source, avoiding a duplicate.
    events = (destination / 'events.jsonl').open('x', encoding='utf-8') if raw_format == 'json' else None
    try:
        with source_path.open('rb') as source, \
                (destination / '02-引擎输出的玩家可见动作.txt').open('x', encoding='utf-8') as visible, \
                (destination / '调试轨迹.txt').open('x', encoding='utf-8') as debug, \
                (destination / 'format-notices.jsonl').open('x', encoding='utf-8') as notices:
            visible.write(scope + '\n\n')
            for number, raw in enumerate(source, 1):
                projector.digest.update(raw)
                if not raw.strip():
                    continue
                try:
                    packet = record(json.loads(raw))
                except (json.JSONDecodeError, UnicodeDecodeError) as error:
                    complete = False
                    issue = {'source_line': number, 'issue': 'invalid-or-truncated-json', 'error': str(error)}
                    notices.write(json.dumps(issue) + '\n')
                    projector.summary['invalid_source_lines'].append(issue)
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
                        notices.write(json.dumps({'source_line': number, 'issue': 'visible-action-without-text', 'execution_event': p.get('execution_event'), 'action_id': p.get('action_id'), 'action_kind': p.get('action_kind')}, ensure_ascii=False) + '\n')
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
                        debug.write(f"完整投影：stdout.jsonl 第{number}行 batch.events[{index}]；原始事件以raw_event_ref定位。\n\n")
    finally:
        if events:
            events.close()
        summary = projector.finish(destination)
    # Output was streamed with final newlines; never reread a large text artifact.
    return (complete, summary) if with_summary else complete


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--entry', type=Path, default=LANGUAGE / 'game/debug-run.dv')
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--timeout', type=int, default=600)
    parser.add_argument('--projection-every', type=int, default=1, help='Flush diagnostic stdout every N actual completed steps; N>1 requires online raw DAG')
    parser.add_argument('--resume-checkpoint', type=Path, help='Explicit validated stage checkpoint bundle; never initializes a fresh session')
    parser.add_argument('--gc-mode', choices=('server', 'workstation'), default='server')
    parser.add_argument('--gc-heaps', type=int, default=min(4, os.cpu_count() or 1))
    parser.add_argument('--profile', action='store_true', help='Build an instrumented DV session and report per-function and work-type time')
    parser.add_argument('--evidence-from', type=Path, help='Only project an existing events.jsonl packet stream into a new --out directory; never build or execute a session')
    parser.add_argument('--detective-profile', default='Standard_Detective', choices=('Standard_Detective', 'Foolish_Detective', 'Shura_Detective'))
    parser.add_argument('--opening-seed', type=int, default=1)
    parser.add_argument('--homecoming-negotiation', action='store_true', help='Enable declared homecoming negotiation candidates; does not seed votes or outcomes')
    parser.add_argument('--su-initial-room', choices=('R08', 'R05'), default='R08')
    parser.add_argument('--private-execution-benefit-multiplier', type=int, choices=(1, 2), default=1)
    parser.add_argument('--detective-zone', default='zone_yi', choices=('zone_yi', 'zone_jia', 'zone_bing', 'zone_ding'))
    parser.add_argument('--trace-compression', choices=('none', 'gzip'), default='none', help='Lossless storage for the full raw JSON trace; no field projection')
    parser.add_argument('--raw-format', choices=('json', 'dag'), default='json', help='json preserves the legacy final trace; dag records checksummed lossless values and event commits online')
    args = parser.parse_args()
    if args.opening_seed < 0:
        parser.error('--opening-seed must be nonnegative')
    run_config = {'seed': args.opening_seed, 'profile': args.detective_profile, 'detective_zone': args.detective_zone, 'su_initial_room': args.su_initial_room, 'private_execution_benefit_multiplier': args.private_execution_benefit_multiplier, 'homecoming_negotiation': args.homecoming_negotiation}
    if args.projection_every < 1:
        parser.error('--projection-every must be positive')
    if args.projection_every > 1 and args.raw_format != 'dag':
        parser.error('Batched projection requires --raw-format dag; original raw events cannot be disabled')
    if args.resume_checkpoint and (args.raw_format != 'dag' or args.entry.resolve() != (LANGUAGE / 'game/debug-run.dv').resolve()):
        parser.error('Restore requires the production debug-run entry and --raw-format dag')
    if args.gc_heaps < 1:
        parser.error('--gc-heaps must be positive')
    if args.timeout < 1:
        parser.error('--timeout must be positive')
    if args.raw_format == 'dag' and args.trace_compression != 'none':
        parser.error('--trace-compression applies to JSON; DAG uses independently checksummed uncompressed frames')
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
            explicit_options = {item.split('=', 1)[0] for item in sys.argv[1:]}
            for option, key in config_options.items():
                if option in explicit_options and run_config[key] != saved_config[key]:
                    raise ValueError(f'Restore configuration conflicts with checkpoint: {option}')
            run_config = saved_config
            args.profile = resume_bundle['metadata']['provenance'].get('instrumented', False)
            args.opening_seed = run_config['seed']
            args.detective_profile = run_config['profile']
            args.detective_zone = run_config['detective_zone']
            args.su_initial_room = run_config['su_initial_room']
            args.private_execution_benefit_multiplier = run_config['private_execution_benefit_multiplier']
            args.homecoming_negotiation = run_config['homecoming_negotiation']
        except Exception as error:
            (out / 'result.json').write_text(json.dumps({'session_started': False, 'restore_rejected': True, 'error': str(error)}, indent=2), encoding='utf-8')
            return 1
    build = out / 'program'
    build.mkdir()
    env = environment()
    modules = sources([args.entry.resolve()])
    inputs = [path for path, _ in modules]
    inputs += [LANGUAGE / p for p in ('compiler/Runtime.cs', 'compiler/compiler.dv', 'tools/build_session.py', 'tools/dv_sources.py', 'tools/run_debug.py', 'tools/raw_dag.py', 'tools/dv_toolchain.py', 'tools/debug_checkpoints.py', 'tools/resume_debug.py', 'tools/DebugCheckpointResume.cs')]
    if args.profile:
        inputs += [LANGUAGE / p for p in ('tools/DvProfiler.cs', 'tools/core_profile.py')]
    hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}
    normalized = {str(path): hashlib.sha256(text.encode('utf-8')).hexdigest() for path, text in modules}
    (out / 'source-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
    def dv_record(value):
        if isinstance(value, dict):
            return [[key, dv_record(item)] for key, item in value.items()]
        if isinstance(value, list):
            return [dv_record(item) for item in value]
        return value
    provenance = {'format': 'dv-debug-provenance-v1', 'source_hashes': hashes, 'source_manifest_sha256': sha_file(out / 'source-hashes.json'), 'run_config': run_config, 'instrumented': args.profile}
    provenance_path = out / 'run-provenance.dv.json'
    provenance_path.write_text(json.dumps(dv_record(provenance), ensure_ascii=True), encoding='utf-8')
    restore_input_hashes = {}
    if resume_save:
        restore_input_hashes = {str(args.resume_checkpoint.resolve()): sha_file(args.resume_checkpoint), str(resume_save): sha_file(resume_save), resume_bundle['raw_source']: resume_bundle['raw_sha256']}
    (out / '.gitattributes').write_text('stdout.jsonl -text -diff\nstderr.txt -text -diff\n*.dag.jsonl* -text -diff\n', encoding='utf-8')
    commands = [
        [sys.executable, str(LANGUAGE / 'tools/build_session.py'), '--entry', str(args.entry.resolve()), '--out', str(build), '--main', 'dvg_debug_run'] + (['--profile'] if args.profile else []),
        ['dotnet', 'build', str(build / 'Program.csproj'), '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false']]
    shutil.copyfile(LANGUAGE / 'compiler/Runtime.cs', build / 'Runtime.cs')
    project = PROJECT
    if resume_bundle:
        shutil.copyfile(LANGUAGE / 'tools/DebugCheckpointResume.cs', build / 'DebugCheckpointResume.cs')
        project = PROJECT.replace('<OutputType>Exe</OutputType>', '<OutputType>Exe</OutputType><StartupObject>DebugCheckpointResume</StartupObject>')
    (build / 'Program.csproj').write_text(project, encoding='utf-8')
    build_start = time.monotonic()
    build_stages = []
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
        and hashlib.sha256((build / 'Runtime.cs').read_bytes()).hexdigest() == hashes[str(LANGUAGE / 'compiler/Runtime.cs')]
        and all(Path(p).is_file() and sha_file(p) == h for p, h in restore_input_hashes.items()))
    if not build_consistent:
        (out / 'result.json').write_text(json.dumps({'executor': 'formal-model', 'geometry': False, 'run_config': run_config, 'build_source_consistent': False, 'source_unchanged': False, 'trace_written': False, 'error': 'Sources changed during build or generated inputs differ; session not started.'}, indent=2), encoding='utf-8')
        return 1
    execution_env = env.copy()
    execution_env['DOTNET_gcServer'] = '1' if args.gc_mode == 'server' else '0'
    execution_env['DOTNET_GCHeapCount'] = format(args.gc_heaps, 'x')
    if args.profile:
        execution_env['DV_PROFILE_PATH'] = str(out / 'profile-raw.json')
    start = time.monotonic()
    trace_name = 'raw-events.dag.jsonl' if args.raw_format == 'dag' else ('trace.json.gz' if args.trace_compression == 'gzip' else 'trace.json')
    result = {'executor': 'formal-model', 'geometry': False, 'run_config': run_config, 'build_source_consistent': True, 'entry': str(args.entry.resolve()), 'detective_profile': args.detective_profile, 'detective_zone': args.detective_zone, 'raw_format': args.raw_format, 'build_elapsed_seconds': build_elapsed, 'build_stages': build_stages, 'gc_mode_requested': args.gc_mode, 'gc_heap_count_requested': args.gc_heaps, 'runtime_environment': {key: value for key, value in execution_env.items() if key.upper().startswith(('DOTNET_GC', 'DOTNET_TIERED', 'COMPLUS_TIERED'))}}
    result['projection_every'] = args.projection_every
    result['resumed'] = resume_bundle is not None
    result['restore_input_hashes'] = restore_input_hashes
    if resume_bundle:
        result['restore_origin'] = {key: resume_bundle['metadata'][key] for key in ('checkpoint', 'commit_frame', 'game_time', 'steps', 'last_event')}
    try:
        with (out / 'stdout.jsonl').open('wb') as stdout, (out / 'stderr.txt').open('wb') as stderr:
            program_args = [str(out / trace_name), args.detective_profile, args.detective_zone, args.raw_format, str(args.opening_seed), args.su_initial_room, str(args.private_execution_benefit_multiplier), str(args.homecoming_negotiation).lower(), str(args.projection_every), str(provenance_path), str(resume_save) if resume_save else '']
            process = subprocess.run(['dotnet', str(build / 'bin/Release/net8.0/Program.dll'), *program_args], cwd=ROOT, env=execution_env, stdout=stdout, stderr=stderr, timeout=args.timeout)
            result['exit_code'] = process.returncode
    except subprocess.TimeoutExpired:
        result['timeout_seconds'] = args.timeout
    finally:
        result['elapsed_seconds'] = time.monotonic() - start
        result['execution_elapsed_seconds'] = result['elapsed_seconds']
        postprocess_start = time.monotonic()
        result['source_unchanged'] = all(Path(p).is_file() and hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p, h in {**hashes, **restore_input_hashes}.items())
        result['raw_path'] = trace_name
        result['trace_path'] = trace_name if args.raw_format == 'json' else None
        result['trace_storage_encoding'] = args.trace_compression if args.raw_format == 'json' else 'dv-raw-dag-v1'
        result['raw_written'] = (out / trace_name).is_file()
        result['raw_partial_written'] = (out / (trace_name + '.partial')).is_file()
        result['trace_written'] = args.raw_format == 'json' and result['raw_written']
        result['trace_partial_written'] = args.raw_format == 'json' and result['raw_partial_written']
        if args.raw_format == 'dag':
            raw_source = out / (trace_name if result['raw_written'] else trace_name + '.partial')
            try:
                raw_summary = inspect_journal(raw_source, out / 'raw-event-index.jsonl')
                with (out / 'raw-dag-summary.json').open('x', encoding='utf-8') as output:
                    json.dump(raw_summary, output, ensure_ascii=False, indent=2)
                result['raw_dag_summary'] = 'raw-dag-summary.json'
                result['raw_event_index'] = 'raw-event-index.jsonl'
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
        if args.profile and (out / 'profile-raw.json').is_file():
            try:
                summarize_profile(out / 'profile-raw.json', modules, out / 'profile-summary.json')
                result['profile_written'] = True
            except Exception as error:
                result['profile_error'] = str(error)
        try:
            result['output_well_formed'], evidence = export(out, raw_format=args.raw_format, with_summary=True)
            result['text_exported'] = True
            result['actual_run_config'] = evidence.get('run_config')
            result['run_config_matches'] = result['actual_run_config'] == run_config
            result['event_packet_source'] = 'stdout.jsonl'
            result['stdout_projection_coverage'] = evidence.get('projection_coverage')
            coverage = evidence.get('projection_coverage', {})
            if args.raw_format == 'dag' and 'raw_committed_events' in result:
                projected = max(0, coverage.get('last_event_cursor', 0) - coverage.get('inherited_event_count', 0))
                result['raw_committed_events_not_yet_projected'] = max(0, result['raw_committed_events'] - projected)
            result['player_text_scope'] = 'flushed stdout presentations only; unflushed raw tail is not reconstructed or claimed complete'
            result['completed_steps_after_last_flush'] = 0 if evidence['debug_end_observed'] else 'unknown in interrupted computation; original committed raw events retained'
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
        result['evidence_complete'] = bool(result.get('exit_code') == 0 and result.get('output_well_formed') and result.get('logical_loop_finished') and result['raw_sealing_complete'] and result['source_unchanged'])
        result['postprocess_elapsed_seconds'] = time.monotonic() - postprocess_start
        (out / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    return 0 if result['evidence_complete'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
