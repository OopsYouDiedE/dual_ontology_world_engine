"""Compile and execute DV verification; Python performs only file/process I/O.

The plan supplies frozen session arguments and checker input files. No scenario
condition, answer, parameter range or finding is interpreted here.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
import time
import zipfile

ROOT = Path(__file__).resolve().parents[2]
LANGUAGE = ROOT / 'language_core'
ENTRIES = ('main-causal', 'compare-capability', 'check-evidence-boundary', 'sweep-parameters')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def pair_record(value):
    """Encode opaque platform metadata for DV's array-based persistence ABI."""
    if isinstance(value, dict):
        return [[k, pair_record(v)] for k, v in value.items()]
    if isinstance(value, (list, tuple)):
        return [pair_record(v) for v in value]
    return value


def main():
    global ROOT, LANGUAGE
    workspace = ROOT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', required=True)
    parser.add_argument('--config', required=True, help='JSON process/file plan; not domain rules')
    timeout_args = parser.add_mutually_exclusive_group()
    timeout_args.add_argument('--timeout', type=float, help='Legacy explicit process timeout alias')
    timeout_args.add_argument('--process-timeout-seconds', type=float, default=240, help='External engineering process limit; does not change game time or domain budgets')
    parser.add_argument('--rerun-frozen-session', help='Execute unchanged sealed producer binaries and inputs again; no compilation or domain export')
    parser.add_argument('--reuse-sessions', help='Judge sealed historical traces without repeating unchanged sessions')
    parser.add_argument('--primary-only', action='store_true', help='Historical judgment executes only main; supplement purposes remain unexecuted')
    parser.add_argument('--session-only', action='store_true', help='Execute planned sessions now; judgment is explicitly deferred')
    parser.add_argument('--judge-only', help='New checker/report snapshot judges the sealed inputs and traces of this previous judgment directory')
    parser.add_argument('--judge-core-root', help='Explicit separately sealed read-only checker Core dependency root; producer source remains unchanged')
    parser.add_argument('--source-root', help='Isolated extracted source archive; does not read the changing workspace core')
    args = parser.parse_args()
    args.timeout = args.process_timeout_seconds if args.timeout is None else args.timeout
    if not math.isfinite(args.timeout) or args.timeout <= 0: parser.error("Process timeout must be finite and positive")
    out = (workspace / args.out).resolve()
    previous_run = (workspace / args.reuse_sessions).resolve() if args.reuse_sessions else None
    if args.source_root:
        ROOT = (workspace / args.source_root).resolve()
        LANGUAGE = ROOT / 'language_core'
    config = (ROOT / args.config).resolve()
    plan = json.loads(config.read_text(encoding='utf-8-sig'))
    if args.rerun_frozen_session:
        rerun_frozen_session(args, workspace, ROOT, out, plan, config)
        return
    if args.judge_only:
        judge_history(args, workspace, ROOT, out, plan)
        return
    out.mkdir(parents=True, exist_ok=False)
    orchestrator = Path(__file__).resolve()
    orchestrator_hash = sha(orchestrator)
    shutil.copyfile(orchestrator, out / 'platform-orchestrator.py')
    (out / '.gitattributes').write_text('*.txt -text -diff\n*.json -text\n*.story -text\n*.zip -text -diff\n', encoding='utf-8')
    excluded = {'bin', 'obj', '__pycache__', '.git'}
    sources = {p.resolve() for p in LANGUAGE.rglob('*') if p.is_file() and not excluded.intersection(p.parts)}
    sources.update((ROOT / p).resolve() for p in plan['files'])
    sources.add(config)
    sources = sorted(sources)
    language_names = {p.relative_to(ROOT).as_posix() for p in LANGUAGE.rglob('*') if p.is_file() and not excluded.intersection(p.parts)}
    before = {p.relative_to(ROOT).as_posix(): sha(p) for p in sources}
    (out / 'sources.json').write_text(json.dumps(before, indent=2), encoding='utf-8')
    with zipfile.ZipFile(out / 'sources.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        for p in sources:
            archive.write(p, p.relative_to(ROOT).as_posix())
    inputs = out / 'inputs'
    inputs.mkdir()
    for name, source in plan['copies'].items():
        destination = inputs / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        source = (ROOT / source).resolve()
        if source not in sources:
            raise ValueError(f'Input was not frozen: {source}')
        shutil.copyfile(source, destination)
    commands = []
    origin = time.monotonic_ns()
    result = {'status': 'incomplete', 'scope': 'compiled DV logic sessions; no Godot or old Core integration',
              'independent_review': 'not performed by user instruction',
        'process_timeout_seconds': args.timeout, 'source_root': str(ROOT),
              'platform_orchestrator_sha256': orchestrator_hash}

    def run(label, argv):
        current = {p.relative_to(ROOT).as_posix(): sha(p) if p.exists() else None for p in sources}
        current_names = {p.relative_to(ROOT).as_posix() for p in LANGUAGE.rglob('*') if p.is_file() and not excluded.intersection(p.parts)}
        if current != before or current_names != language_names or sha(orchestrator) != orchestrator_hash:
            raise RuntimeError(f'Protected source changed before {label}')
        argv = [str(x) for x in argv]
        started = time.monotonic_ns()
        record = {'label': label, 'argv': argv, 'start_monotonic_ns': started,
                  'start_elapsed_seconds': (started - origin) / 1e9}
        try:
            process = subprocess.run(argv, cwd=ROOT, capture_output=True, timeout=args.timeout)
            stdout, stderr = process.stdout, process.stderr
            record['exit'] = process.returncode
        except subprocess.TimeoutExpired as exc:
            stdout, stderr = exc.stdout or b'', exc.stderr or b''
            record['timeout_seconds'] = args.timeout
            record['exit'] = None
        ended = time.monotonic_ns()
        record.update(end_monotonic_ns=ended, duration_seconds=(ended - started) / 1e9)
        (out / (label + '.stdout.txt')).write_bytes(stdout)
        (out / (label + '.stderr.txt')).write_bytes(stderr)
        commands.append(record)
        (out / 'commands.json').write_text(json.dumps(commands, indent=2), encoding='utf-8')
        if record['exit'] != 0:
            raise RuntimeError(f'{label}: exit {record["exit"]}; raw diagnostics retained in {out}')

    def project(folder):
        shutil.copyfile(LANGUAGE / 'compiler/Runtime.cs', folder / 'Runtime.cs')
        (folder / 'Program.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>', encoding='utf-8')
        run(folder.name + '-build', ['dotnet', 'build', folder / 'Program.csproj', '-c', 'Release', '-m:1', '--disable-build-servers'])
        executable = folder / 'bin/Release/net8.0/Program.dll'
        deps = json.loads(executable.with_suffix('.deps.json').read_text(encoding='utf-8'))
        if set(deps['libraries']) != {'Program/1.0.0'}:
            raise RuntimeError(f'Unexpected generated assembly dependency: {folder}')
        return executable

    def expand(value):
        return str(value).replace('{out}', str(out)).replace('{inputs}', str(inputs)).replace('{root}', str(ROOT))

    def expand_paths(value):
        if isinstance(value, str):
            return expand(value)
        if isinstance(value, list):
            return [expand_paths(x) for x in value]
        if isinstance(value, dict):
            return {k: expand_paths(v) for k, v in value.items()}
        return value

    def pair_record(value):
        # DV's persistence ABI accepts arrays, not JSON objects. This conversion
        # encodes platform metadata only; it does not interpret domain content.
        if isinstance(value, dict):
            return [[k, pair_record(v)] for k, v in value.items()]
        if isinstance(value, (list, tuple)):
            return [pair_record(v) for v in value]
        return value

    try:
        run('toolchain', ['dotnet', '--version'])
        seed = LANGUAGE / 'compiler/seed/Seed.csproj'
        run('seed-build', ['dotnet', 'build', seed, '-c', 'Release', '-m:1', '--disable-build-servers'])
        compiler = seed.parent / 'bin/Release/net8.0/Seed.dll'
        stages = {}
        for name in ('c1', 'c2', 'c3'):
            stage = out / name
            stage.mkdir()
            run(name + '-generate', ['dotnet', compiler, LANGUAGE / 'compiler/compiler.dv', stage / 'Program.cs'])
            compiler = project(stage)
            stages[name] = {'source_sha256': sha(stage / 'Program.cs'), 'assembly_sha256': sha(compiler)}
        result['compiler_stages'] = stages
        if stages['c2']['source_sha256'] != stages['c3']['source_sha256']:
            raise RuntimeError('C2/C3 generated-source fixed point differs')
        result['compiler_fixed_point'] = True
        core = sorted((LANGUAGE / 'core').glob('*.dv'))
        programs = {}
        for name in ('session-runner',) + (() if args.session_only else ENTRIES):
            folder = out / name
            folder.mkdir()
            modules = core + (sorted((LANGUAGE / 'verification/session').glob('*.dv')) if name == 'session-runner'
                              else sorted((LANGUAGE / 'verification/checks').glob('*.dv')) + [LANGUAGE / f'verification/{name}.dv'])
            source = '\n'.join(p.read_text(encoding='utf-8') for p in modules)
            if name == 'session-runner':
                source += '\n(main (dvs_main))\n'
            bundle = folder / 'bundle.dv'
            bundle.write_text(source, encoding='utf-8')
            run(name + '-generate', ['dotnet', compiler, bundle, folder / 'Program.cs'])
            programs[name] = project(folder)
        result['programs'] = {name: {'assembly_sha256': sha(p), 'generated_source_sha256': sha(p.parents[3] / 'Program.cs')} for name, p in programs.items()}
        result['exports'] = {}
        groups = {}
        for export in plan.get('exports', []):
            groups.setdefault(tuple(export['modules']), []).append(export)
        for module_names, exports in groups.items():
            name = 'export-' + exports[0]['id']
            folder = out / name
            folder.mkdir()
            modules = core + [(ROOT / p).resolve() for p in module_names]
            if any(p not in sources for p in modules):
                raise ValueError(f'Unfrozen DV export module: {name}')
            bundle = folder / 'bundle.dv'
            dispatch = '(rt_fail "Unknown I/O export function")'
            for export in reversed(exports):
                function = export['function']
                if not function.replace('_', '').replace('-', '').isalnum():
                    raise ValueError('Export function must be a DV identifier')
                dispatch = '(if (rt_eq (rt_arg 1) "' + function + '") (' + function + ') ' + dispatch + ')'
            bundle.write_text('\n'.join(p.read_text(encoding='utf-8') for p in modules) + '\n(main (rt_write (rt_arg 0) (rt_json_stringify ' + dispatch + ')))\n', encoding='utf-8')
            run(name + '-generate', ['dotnet', compiler, bundle, folder / 'Program.cs'])
            executable = project(folder)
            result['exports'][name] = {'assembly_sha256': sha(executable), 'generated_source_sha256': sha(folder / 'Program.cs'), 'functions': [x['function'] for x in exports]}
            for export in exports:
                run('export-' + export['id'] + '-execute', ['dotnet', executable, expand(export['output']), export['function']])
        actual_runtime = dict(result['programs'])
        if args.reuse_sessions:
            previous = previous_run
            sealed = json.loads((previous / 'result.json').read_text(encoding='utf-8'))
            old_sources = json.loads((previous / 'sources.json').read_text(encoding='utf-8'))
            old_commands = json.loads((previous / 'commands.json').read_text(encoding='utf-8'))
            if not sealed.get('sources_unchanged'):
                raise RuntimeError('Historical session source seal did not verify')
            for source in sources:
                if source.suffix in ('.dv', '.cs', '.csproj'):
                    name = source.relative_to(ROOT).as_posix()
                    if old_sources.get(name) != before[name]:
                        raise RuntimeError(f'Historical program source changed: {name}')
            old_runtime = sealed['programs']['session-runner']
            if old_runtime['generated_source_sha256'] != result['programs']['session-runner']['generated_source_sha256']:
                raise RuntimeError('Historical session generated code differs')
            actual_runtime['session-runner'] = old_runtime
            evidence = []
            for session in plan['sessions']:
                command = next((x for x in old_commands if x['label'] == 'session-' + session['id']), None)
                if command is None or command['exit'] != 0:
                    raise RuntimeError(f'Historical session did not finish: {session["id"]}')
                old_input = Path(command['argv'][2])
                new_input = Path(expand(session['args'][0]))
                if sha(old_input) != sha(new_input):
                    raise RuntimeError(f'Historical exported session input differs: {session["id"]}')
                old_trace = Path(command['argv'][3])
                trace = Path(expand(session['args'][1]))
                expected = sealed['outputs'].get(old_trace.relative_to(previous).as_posix())
                if expected != sha(old_trace):
                    raise RuntimeError(f'Historical trace fingerprint differs: {session["id"]}')
                shutil.copyfile(old_trace, trace)
                evidence.append({'id': session['id'], 'source_trace': str(old_trace), 'sha256': expected, 'historical_duration_seconds': command['duration_seconds']})
            result['session_evidence'] = {'scope': 'Judgment reexecution of the same historical traces, not fresh sessions', 'source_run': str(previous), 'actual_session_runtime': old_runtime, 'traces': evidence}
        else:
            for session in plan['sessions']:
                run('session-' + session['id'], ['dotnet', programs['session-runner']] + [expand(x) for x in session['args']])
        # These are file-reference copies only. Domain comparison configuration
        # is supplied unchanged by the approved input plan.
        for name, source in plan.get('path_documents', {}).items():
            value = json.loads(Path(expand(source)).read_text(encoding='utf-8-sig'))
            (inputs / name).write_text(json.dumps(expand_paths(value), ensure_ascii=False, indent=2), encoding='utf-8')
        frozen_now = {p.relative_to(ROOT).as_posix(): sha(p) if p.exists() else None for p in sources}
        current_names = {p.relative_to(ROOT).as_posix() for p in LANGUAGE.rglob('*') if p.is_file() and not excluded.intersection(p.parts)}
        if frozen_now != before or current_names != language_names:
            raise RuntimeError('Protected sources changed before checker execution')
        for name, template in plan.get('platform_manifests', {}).items():
            # Template contains frozen domain configuration copied as opaque data.
            copied = expand_paths(json.loads(Path(expand(template)).read_text(encoding='utf-8-sig')))
            if not isinstance(copied, list):
                raise ValueError('Manifest template must use DV pair-record JSON')
            platform = [['integrity', 'verified'], ['source_fingerprints', list(before.items())],
                ['runtime_fingerprints', pair_record(actual_runtime)],
                ['export_fingerprints', pair_record(result['exports'])],
                ['scope', result['scope']]]
            if 'session_evidence' in result:
                platform.append(['session_evidence', pair_record(result['session_evidence'])])
            if any(pair[0] in {item[0] for item in platform} for pair in copied):
                raise ValueError('Manifest template cannot overwrite platform seal')
            (inputs / name).write_text(json.dumps(copied + platform, ensure_ascii=False, indent=2), encoding='utf-8')
        for name in (() if args.session_only else ENTRIES):
            run(name + '-execute', ['dotnet', programs[name]] + [expand(x) for x in plan['checks'][name]])
        evidence = []
        if plan.get('archives'):
            with zipfile.ZipFile(out / 'raw-traces.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
                for reference in plan['archives']:
                    path = Path(expand(reference))
                    name = path.relative_to(out).as_posix()
                    archive.write(path, name)
                    evidence.append({'path': name, 'bytes': path.stat().st_size, 'sha256': sha(path)})
            result['raw_trace_archive'] = {'path': 'raw-traces.zip', 'sha256': sha(out / 'raw-traces.zip'), 'entries': evidence, 'originals_retained': True}
        result['status'] = 'sessions_executed_judgment_deferred' if args.session_only else 'executed'
    except Exception as exc:
        result['error'] = str(exc)
        raise
    finally:
        after = {p.relative_to(ROOT).as_posix(): sha(p) if p.exists() else None for p in sources}
        changed = [name for name in before if before[name] != after[name]]
        current_names = {p.relative_to(ROOT).as_posix() for p in LANGUAGE.rglob('*') if p.is_file() and not excluded.intersection(p.parts)}
        changed = sorted(set(changed) | (current_names ^ language_names))
        result['sources_unchanged'] = not changed
        result['changed_sources'] = changed
        if changed:
            result['status'] = 'invalid_run'
        result['elapsed_seconds'] = (time.monotonic_ns() - origin) / 1e9
        result['outputs'] = {p.relative_to(out).as_posix(): sha(p) for p in out.rglob('*') if p.is_file() and p.name != 'result.json'}
        (out / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    if changed:
        raise RuntimeError('Protected source changed during execution; evidence invalid')
    print(out)


def rerun_frozen_session(args, workspace, source_root, out, plan, config):
    """Pure I/O rerun of sealed producer bytes under an explicit external limit."""
    if not args.session_only:
        raise ValueError('Frozen rerun requires --session-only; judgment is separate')
    previous = (workspace / args.rerun_frozen_session).resolve()
    prior = json.loads((previous / 'result.json').read_text(encoding='utf-8'))
    if not prior.get('sources_unchanged') or str(source_root) != prior['source_root']:
        raise ValueError('Frozen producer source seal/root differs')
    seal = json.loads((previous / 'sources.json').read_text(encoding='utf-8'))
    config_name = config.relative_to(source_root).as_posix()
    if config_name not in seal or sha(config) != seal[config_name]:
        raise ValueError('Rerun plan is not in the sealed producer configuration')
    with zipfile.ZipFile(previous / 'sources.zip') as archive:
        if archive.read(config_name) != config.read_bytes():
            raise ValueError('Rerun plan differs from archived producer configuration')
    packages = {}
    def protected():
        names = {p.relative_to(source_root).as_posix() for p in (source_root / 'language_core').rglob('*') if p.is_file() and not {'bin', 'obj', '__pycache__', '.git'}.intersection(p.parts)}
        expected = {name for name in seal if name.startswith('language_core/')}
        return names == expected and all((source_root / name).is_file() and sha(source_root / name) == value for name, value in seal.items()) and all(path.is_file() and sha(path) == value for path, value in packages.items())
    if not protected(): raise RuntimeError('Frozen producer source differs')
    out.mkdir(parents=True, exist_ok=False)
    for name in ['.gitattributes', 'sources.json', 'sources.zip']:
        shutil.copyfile(previous / name, out / name)
    shutil.copytree(previous / 'inputs', out / 'inputs')
    for package in ['session-runner', 'c3']:
        shutil.copytree(previous / package, out / package, ignore=shutil.ignore_patterns('obj'))
    for name, expected in prior['outputs'].items():
        if name.startswith(('inputs/', 'session-runner/', 'c3/')) and '/obj/' not in name:
            target = out / name
            if not target.is_file() or sha(target) != expected: raise RuntimeError('Producer package differs: ' + name)
            packages[target] = expected
    binary = out / 'session-runner/bin/Release/net8.0/Program.dll'
    if sha(binary) != prior['programs']['session-runner']['assembly_sha256']:
        raise RuntimeError('Producer assembly fingerprint differs')
    shutil.copyfile(Path(__file__).resolve(), out / 'platform-orchestrator.py')
    platform_hash = sha(Path(__file__).resolve())
    inputs = out / 'inputs'
    def expand(value):
        return value.replace('{inputs}', str(inputs)).replace('{out}', str(out)).replace('{root}', str(source_root))
    def expand_paths(value):
        if isinstance(value, str): return expand(value)
        if isinstance(value, list): return [expand_paths(x) for x in value]
        if isinstance(value, dict): return {k: expand_paths(v) for k, v in value.items()}
        return value
    result = {'status': 'incomplete', 'scope': 'Fresh rerun of identical frozen producer package; external process limit changed only',
        'source_root': str(source_root), 'frozen_rerun_from': str(previous), 'process_timeout_seconds': args.timeout,
        'previous_process_timeout_seconds': next((x.get('timeout_seconds') for x in json.loads((previous / 'commands.json').read_text()) if x['label'].startswith('session-')), None),
        'compiler_execution': 'Reused exact sealed compiler package; no new bootstrap execution',
        'programs': prior['programs'], 'compiler_stages': prior['compiler_stages'], 'compiler_fixed_point': prior['compiler_fixed_point'],
        'exports': prior['exports'], 'platform_orchestrator_sha256': platform_hash, 'real_window_performance_comparison': False}
    commands = []
    origin = time.monotonic_ns()
    try:
        for session in plan['sessions']:
            if not protected() or sha(Path(__file__).resolve()) != platform_hash: raise RuntimeError('Protected rerun source changed')
            record = {'label': 'session-' + session['id'], 'argv': ['dotnet', str(binary)] + [expand(x) for x in session['args']], 'start_monotonic_ns': time.monotonic_ns()}
            try:
                process = subprocess.run(record['argv'], capture_output=True, timeout=args.timeout)
                record['exit'] = process.returncode
                stdout, stderr = process.stdout, process.stderr
            except subprocess.TimeoutExpired as exc:
                record['exit'] = None
                record['timeout_seconds'] = args.timeout
                stdout, stderr = exc.stdout or b'', exc.stderr or b''
            record['end_monotonic_ns'] = time.monotonic_ns()
            record['duration_seconds'] = (record['end_monotonic_ns'] - record['start_monotonic_ns']) / 1e9
            for suffix, data in [('stdout', stdout), ('stderr', stderr)]: (out / (record['label'] + '.' + suffix + '.txt')).write_bytes(data)
            commands.append(record)
            (out / 'commands.json').write_text(json.dumps(commands, indent=2), encoding='utf-8')
            if record['exit'] != 0: raise RuntimeError('Frozen session incomplete; original diagnostics retained')
        if not protected(): raise RuntimeError('Frozen producer source changed')
        generated_names = set(plan.get('path_documents', {})) | set(plan.get('platform_manifests', {}))
        session_inputs = {Path(expand(x)).resolve() for session in plan['sessions'] for x in session['args'] if '{inputs}' in x}
        if any((inputs / name).resolve() in session_inputs for name in generated_names):
            raise ValueError('Platform documents cannot overwrite frozen session inputs')
        for name in generated_names:
            packages.pop(inputs / name, None)
        for name, reference in plan.get('path_documents', {}).items():
            (inputs / name).write_text(json.dumps(expand_paths(json.loads(Path(expand(reference)).read_text(encoding='utf-8-sig'))), ensure_ascii=False, indent=2), encoding='utf-8')
        for name, reference in plan.get('platform_manifests', {}).items():
            value = expand_paths(json.loads(Path(expand(reference)).read_text(encoding='utf-8-sig')))
            metadata = [['integrity', 'verified'], ['source_fingerprints', list(seal.items())], ['runtime_fingerprints', pair_record(prior['programs'])], ['export_fingerprints', pair_record(prior['exports'])], ['scope', result['scope']]]
            if not isinstance(value, list) or any(x[0] in {p[0] for p in metadata} for x in value): raise ValueError('Manifest cannot overwrite platform metadata')
            (inputs / name).write_text(json.dumps(value + metadata, ensure_ascii=False, indent=2), encoding='utf-8')
        with zipfile.ZipFile(out / 'raw-traces.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
            for reference in plan.get('archives', []):
                path = Path(expand(reference)); archive.write(path, path.relative_to(out).as_posix())
        result['status'] = 'sessions_executed_judgment_deferred'
    except Exception as exc:
        result['error'] = str(exc)
        raise
    finally:
        result['sources_unchanged'] = protected()
        if not result['sources_unchanged']: result['status'] = 'invalid_run'
        result['elapsed_seconds'] = (time.monotonic_ns() - origin) / 1e9
        result['outputs'] = {p.relative_to(out).as_posix(): sha(p) for p in out.rglob('*') if p.is_file() and p.name != 'result.json'}
        (out / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(out)


def judge_history(args, workspace, historical_root, out, plan):
    """I/O-only historical judgment with independently sealed checker dependencies."""
    entries = ENTRIES[:1] if args.primary_only else ENTRIES
    previous = (workspace / args.judge_only).resolve()
    prior = json.loads((previous / 'result.json').read_text(encoding='utf-8'))
    if not prior.get('sources_unchanged'):
        raise RuntimeError('Historical judgment source seal was not verified')
    out.mkdir(parents=True, exist_ok=False)
    (out / '.gitattributes').write_text('*.txt -text -diff\n*.json -text\n*.zip -text -diff\n', encoding='utf-8')
    snapshot = out / 'source-snapshot'
    snapshot.mkdir()
    checker_root = (workspace / args.judge_core_root).resolve() if args.judge_core_root else historical_root
    files = sorted((checker_root / 'language_core/core').glob('*.dv'))
    live = workspace / 'language_core/verification'
    files += sorted((live / 'checks').glob('*.dv')) + sorted((live / 'report').glob('*.dv'))
    files += [live / (name + '.dv') for name in ENTRIES]
    files += [checker_root / 'language_core/compiler/Runtime.cs']
    seal = {}
    for source in files:
        relative = source.relative_to(checker_root if source.is_relative_to(checker_root) else workspace)
        target = snapshot / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        seal[relative.as_posix()] = sha(target)
    shutil.copyfile(Path(__file__).resolve(), out / 'platform-orchestrator.py')
    (out / 'sources.json').write_text(json.dumps(seal, indent=2), encoding='utf-8')
    with zipfile.ZipFile(out / 'sources.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        for name in seal: archive.write(snapshot / name, name)
    inputs = out / 'inputs'
    shutil.copytree(previous / 'inputs', inputs)
    trace_seal = {}
    for session in plan['sessions']:
        name = 'trace-' + session['id'] + '.json'
        source = previous / name
        expected = prior['outputs'].get(name)
        if sha(source) != expected: raise RuntimeError('Historical trace fingerprint differs: ' + name)
        shutil.copyfile(source, out / name)
        trace_seal[name] = expected
    def paths(value):
        if isinstance(value, str) and value.replace('\\', '/').startswith(str(previous).replace('\\', '/') + '/'):
            return str(out) + value[len(str(previous)):]
        if isinstance(value, list): return [paths(item) for item in value]
        if isinstance(value, dict): return {key: paths(item) for key, item in value.items()}
        return value
    for path in inputs.glob('*.json'):
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        path.write_text(json.dumps(paths(data), ensure_ascii=False, indent=2), encoding='utf-8')
    origin = time.monotonic_ns()
    commands = []
    result = {'status': 'incomplete', 'scope': 'New read-only DV checker contract over unchanged historical traces; not fresh sessions',
              'historical_judgment': str(previous), 'historical_source_root': str(historical_root),
              'checker_dependency_root': str(checker_root), 'producer_runtime_changed': False, 'process_timeout_seconds': args.timeout, 'trace_fingerprints': trace_seal,
              'checker_source_fingerprints': seal, 'oracle_condition': 'Copied original oracle bytes/domain values; no current scenario or oracle export'}
    def run(label, argv):
        if any(sha(snapshot / name) != expected for name, expected in seal.items()): raise RuntimeError('Judgment snapshot changed')
        started = time.monotonic_ns()
        record = {'label': label, 'argv': list(map(str, argv)), 'start_monotonic_ns': started}
        try:
            process = subprocess.run(record['argv'], capture_output=True, timeout=args.timeout)
            record['exit'] = process.returncode
            stdout, stderr = process.stdout, process.stderr
        except subprocess.TimeoutExpired as exc:
            record['exit'] = None
            record['timeout_seconds'] = args.timeout
            stdout, stderr = exc.stdout or b'', exc.stderr or b''
        record['end_monotonic_ns'] = time.monotonic_ns()
        record['duration_seconds'] = (record['end_monotonic_ns'] - started) / 1e9
        (out / (label + '.stdout.txt')).write_bytes(stdout)
        (out / (label + '.stderr.txt')).write_bytes(stderr)
        commands.append(record)
        (out / 'commands.json').write_text(json.dumps(commands, indent=2), encoding='utf-8')
        return record['exit'] == 0
    compiler = previous / 'c3/bin/Release/net8.0/Program.dll'
    result['compiler_sha256'] = sha(compiler)
    core = sorted((snapshot / 'language_core/core').glob('*.dv'))
    checks = sorted((snapshot / 'language_core/verification/checks').glob('*.dv'))
    try:
        programs = {}
        for name in (*entries, 'readable-report'):
            program = out / name
            program.mkdir()
            modules = core + (sorted((snapshot / 'language_core/verification/report').glob('*.dv')) if name == 'readable-report' else checks)
            suffix = '\n(main (dvr_main))\n' if name == 'readable-report' else '\n' + (snapshot / ('language_core/verification/' + name + '.dv')).read_text(encoding='utf-8-sig')
            (program / 'bundle.dv').write_text('\n'.join(p.read_text(encoding='utf-8-sig') for p in modules) + suffix, encoding='utf-8')
            shutil.copyfile(snapshot / 'language_core/compiler/Runtime.cs', program / 'Runtime.cs')
            project_template = previous / 'main-causal/Program.csproj'
            if not project_template.exists(): project_template = previous / 'session-runner/Program.csproj'
            shutil.copyfile(project_template, program / 'Program.csproj')
            if not run(name + '-generate', ['dotnet', compiler, program / 'bundle.dv', program / 'Program.cs']): continue
            if not run(name + '-build', ['dotnet', 'build', program / 'Program.csproj', '-c', 'Release', '--disable-build-servers', '-m:1']): continue
            programs[name] = program / 'bin/Release/net8.0/Program.dll'
        def expand(value): return value.replace('{inputs}', str(inputs)).replace('{out}', str(out)).replace('{root}', str(historical_root))
        successful = []
        for name in entries:
            if name in programs and run(name + '-execute', ['dotnet', programs[name]] + [expand(x) for x in plan['checks'][name]]): successful.append(name)
        if all(name in successful for name in entries) and 'readable-report' in programs:
            # Only platform paths and actual historical process durations are supplied.
            original = Path(prior.get('session_evidence', {}).get('source_run', prior.get('source_run', str(previous))))
            history = json.loads((original / 'commands.json').read_text())
            sessions = []
            for item in plan['sessions']:
                command = next(record for record in history if record['label'] == 'session-' + item['id'])
                sessions.append([['id', item['id']], ['trace', str(out / ('trace-' + item['id'] + '.json'))], ['real_elapsed_seconds', command['duration_seconds']], ['scenario', command['argv'][2]]])
            index = [['scope', result['scope']], ['producer_run', str(original)], ['judgment_run', str(out)], ['judgment_processes', [[['id', record['label']], ['real_elapsed_seconds', record['duration_seconds']]] for record in commands if record['label'].endswith('-execute')]], ['oracle', str(inputs / 'chapter1-oracle.json')], ['sessions', sessions]]
            (inputs / 'report-index.json').write_text(json.dumps(index, ensure_ascii=False), encoding='utf-8')
            run('readable-report-execute', ['dotnet', programs['readable-report']] + [str(out / (name + '-execute.stdout.txt')) if name in successful else '' for name in ENTRIES] + [inputs / 'report-index.json', out / 'readable-report.md'])
        result['status'] = ('primary_executed_supplements_deferred' if args.primary_only else 'executed') if all(name in successful for name in entries) else 'incomplete'
    finally:
        result['sources_unchanged'] = all(sha(snapshot / name) == expected for name, expected in seal.items())
        result['elapsed_seconds'] = (time.monotonic_ns() - origin) / 1e9
        result['outputs'] = {p.relative_to(out).as_posix(): sha(p) for p in out.rglob('*') if p.is_file() and p.name != 'result.json'}
        (out / 'result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(out)


if __name__ == '__main__':
    main()
