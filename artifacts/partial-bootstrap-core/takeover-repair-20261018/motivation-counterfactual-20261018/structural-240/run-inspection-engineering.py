"""Opt-in neutral DV inspection-index differential; never starts a game session."""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import shutil
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
sys.path.insert(0, str(ROOT / 'language_core/tools'))
from dv_toolchain import PROJECT, environment
from dv_sources import sources
spec = importlib.util.spec_from_file_location('neutral_list_helpers', HERE.parent / 'run-list-index-engineering.py')
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--timeout', type=int, default=180)
    args = parser.parse_args()
    if args.timeout < 1:
        parser.error('--timeout must be positive')
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    origin = json.loads((HERE / 'old-source.json').read_text(encoding='utf-8'))
    origin_path = ROOT / origin['path']
    if hashlib.sha256(origin_path.read_bytes()).hexdigest() != origin['sha256']:
        raise RuntimeError('Frozen old source fingerprint differs; no build started')
    tracked = {path for path, _ in sources([HERE / 'inspection-probe.dv'])}
    tracked.add(origin_path)
    tracked.update([Path(__file__).resolve(), HERE / 'old-source.json', HERE.parent / 'run-list-index-engineering.py', ROOT / 'language_core/compiler/Runtime.cs', ROOT / 'language_core/compiler/compiler.dv', ROOT / 'language_core/tools/build_session.py', ROOT / 'language_core/tools/dv_sources.py', ROOT / 'language_core/tools/dv_toolchain.py'])
    hashes = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(tracked)}
    (out / 'source-manifest.json').write_text(json.dumps({'source_files': hashes, 'old_source': json.loads((HERE / 'old-source.json').read_text(encoding='utf-8')), 'formal_session': 'not-started'}, indent=2), encoding='utf-8')
    report = {'scope': 'neutral-inspection-index-only', 'formal_session': 'not-started', 'complete': False, 'stages': [], 'comparisons': []}

    def save():
        (out / 'engineering-result.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')

    def run(cmd, name, allow_error=False):
        stage = {'name': name, 'command': [str(x) for x in cmd]}
        report['stages'].append(stage)
        start = time.perf_counter()
        try:
            with (out / (name + '.stdout')).open('wb') as stdout, (out / (name + '.stderr')).open('wb') as stderr:
                result = subprocess.run([str(x) for x in cmd], cwd=ROOT, env=environment(), stdout=stdout, stderr=stderr, timeout=args.timeout)
            stage['exit_code'] = result.returncode
            if result.returncode and not allow_error:
                raise RuntimeError(f'{name}: exited {result.returncode}; retained raw output')
            return result.returncode
        finally:
            stage['elapsed_seconds'] = time.perf_counter() - start
            save()

    try:
        program = out / 'program'
        run([sys.executable, ROOT / 'language_core/tools/build_session.py', '--entry', HERE / 'inspection-probe.dv', '--out', program, '--main', 'dvg_debug_run'], 'dv-generation')
        shutil.copyfile(ROOT / 'language_core/compiler/Runtime.cs', program / 'Runtime.cs')
        (program / 'Program.csproj').write_text(PROJECT, encoding='utf-8')
        run(['dotnet', 'build', program / 'Program.csproj', '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false'], 'dv-build')
        dll = program / 'bin/Release/net8.0/Program.dll'
        for case in ['keys', 'dependencies', 'empty', 'ignored', 'mixed', 'error-operator-first', 'error-subject-first', 'error-refresh-first']:
            outputs = {}
            statuses = {}
            for which in ['old', 'current']:
                name = case + '-' + which
                statuses[which] = run(['dotnet', dll, which, case], name, allow_error=case.startswith('error-'))
                if case.startswith('error-'):
                    # Stack frames differ by implementation name; preserve all raw bytes,
                    # compare the first actual exception line and nonzero termination.
                    stderr = (out / (name + '.stderr')).read_text(encoding='utf-8-sig')
                    lines = [line for line in stderr.splitlines() if line.strip()]
                    outputs[which] = lines[0] if lines else None
                else:
                    lines = (out / (name + '.stdout')).read_text(encoding='utf-8-sig').splitlines()
                    if len(lines) != 1:
                        raise ValueError(f'{name}: expected one complete no-session packet')
                    outputs[which] = helpers.envelope(json.loads(lines[0]))
            difference = helpers.first_difference(outputs['old'], outputs['current'])
            valid = difference is None
            if case.startswith('error-'):
                valid = valid and statuses['old'] != 0 and statuses['current'] != 0 and outputs['old'] is not None
            else:
                for value in outputs.values():
                    valid = valid and value.get('formal_session') == 'not-started' and value.get('input_isolated') is True and value.get('reverse_isolated') is True
            report['comparisons'].append({'case': case, 'equivalent': valid, 'first_difference': difference, 'exit_codes': statuses, 'old': outputs['old'], 'current': outputs['current']})
            save()
        report['complete'] = True
    except Exception as error:
        report['error'] = f'{type(error).__name__}: {error}'
    finally:
        report['source_changes_during_run'] = [path for path, digest in hashes.items() if not Path(path).exists() or hashlib.sha256(Path(path).read_bytes()).hexdigest() != digest]
        report['source_consistent'] = not report['source_changes_during_run']
        report['conclusion_scope'] = 'Only individual neutral values, ownership and first exceptions; no gameplay or speedup conclusion.'
        save()
    return 0 if report['complete'] and report['source_consistent'] and all(item['equivalent'] for item in report['comparisons']) else 1


if __name__ == '__main__':
    raise SystemExit(main())
