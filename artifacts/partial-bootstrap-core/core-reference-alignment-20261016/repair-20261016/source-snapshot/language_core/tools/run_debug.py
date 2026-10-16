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
                    voice = {'narration': '叙述', 'private-monologue': '独白', 'visible-inspection': '观察', 'visible-action': '动作叙述', 'dialogue': '对白'}.get(kind, '正文（源记录未分型）')
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
    args = parser.parse_args()
    if args.gc_heaps < 1:
        parser.error('--gc-heaps must be positive')
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    build = out / 'program'
    build.mkdir()
    env = environment()
    modules = sources([args.entry.resolve()])
    inputs = [path for path, _ in modules]
    inputs += [LANGUAGE / p for p in ('compiler/Runtime.cs', 'compiler/compiler.dv', 'tools/build_session.py', 'tools/dv_sources.py', 'tools/run_debug.py', 'tools/dv_toolchain.py')]
    hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}
    normalized = {str(path): hashlib.sha256(text.encode('utf-8')).hexdigest() for path, text in modules}
    (out / 'source-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
    commands = [
        [sys.executable, str(LANGUAGE / 'tools/build_session.py'), '--entry', str(args.entry.resolve()), '--out', str(build), '--main', 'dvg_debug_run'],
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
    start = time.monotonic()
    result = {'executor': 'formal-model', 'geometry': False, 'build_source_consistent': True, 'entry': str(args.entry.resolve()), 'gc_mode_requested': args.gc_mode, 'gc_heap_count_requested': args.gc_heaps, 'runtime_environment': {key: value for key, value in execution_env.items() if key.startswith(('DOTNET_GC', 'DOTNET_gc'))}}
    try:
        with (out / 'stdout.jsonl').open('wb') as stdout, (out / 'stderr.txt').open('wb') as stderr:
            process = subprocess.run(['dotnet', str(build / 'bin/Release/net8.0/Program.dll'), str(out / 'trace.json')], cwd=ROOT, env=execution_env, stdout=stdout, stderr=stderr, timeout=args.timeout)
            result['exit_code'] = process.returncode
    except subprocess.TimeoutExpired:
        result['timeout_seconds'] = args.timeout
    finally:
        result['elapsed_seconds'] = time.monotonic() - start
        result['source_unchanged'] = all(Path(p).is_file() and hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p, h in hashes.items())
        result['trace_written'] = (out / 'trace.json').is_file()
        try:
            result['output_well_formed'] = export(out)
            result['text_exported'] = True
        except Exception as error:
            result['export_error'] = str(error)
        (out / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    return 0 if result.get('exit_code') == 0 and result.get('output_well_formed') and result['source_unchanged'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
