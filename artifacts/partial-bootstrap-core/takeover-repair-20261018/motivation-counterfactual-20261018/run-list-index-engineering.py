"""Prepare/run opt-in neutral list-index engineering probes; never a formal session."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[4]
TASK = Path(__file__).resolve().parent
CANDIDATE = TASK / 'list-index-candidate'
sys.path.insert(0, str(ROOT / 'language_core/tools'))
from dv_toolchain import PROJECT, environment
from dv_sources import sources


def sha(data):
    return hashlib.sha256(data).hexdigest()


def value_hash(value):
    return sha(json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf-8'))


def first_difference(left, right, path='$'):
    if type(left) is not type(right):
        return {'path': path, 'old_type': type(left).__name__, 'current_type': type(right).__name__, 'old': left, 'current': right}
    if isinstance(left, list):
        if len(left) != len(right):
            return {'path': path, 'old_length': len(left), 'current_length': len(right), 'old': left, 'current': right}
        for index, (a, b) in enumerate(zip(left, right)):
            found = first_difference(a, b, f'{path}[{index}]')
            if found is not None:
                return found
        return None
    if isinstance(left, dict):
        if list(left) != list(right):
            return {'path': path, 'old_keys': list(left), 'current_keys': list(right)}
        for key in left:
            found = first_difference(left[key], right[key], f'{path}.{key}')
            if found is not None:
                return found
        return None
    return None if left == right else {'path': path, 'old': left, 'current': right}


def envelope(value):
    return dict(value) if isinstance(value, list) else value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True, help='New evidence directory, exclusively created')
    parser.add_argument('--timeout', type=int, default=180, help='Maximum wall-clock seconds per engineering process')
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    report = {'scope': 'neutral-list-index-engineering-only', 'formal_session': 'not-started', 'game_acceptance': 'not-performed', 'stages': [], 'comparisons': [], 'markers': {}, 'complete': False}
    tracked = {ROOT / 'language_core/compiler/Runtime.cs', ROOT / 'language_core/compiler/compiler.dv', ROOT / 'language_core/tools/build_session.py', ROOT / 'language_core/tools/dv_toolchain.py', ROOT / 'language_core/tools/dv_sources.py', CANDIDATE / 'ContainerProbe.cs', CANDIDATE / 'list-index-probe.dv', Path(__file__).resolve()}
    tracked.update(path for path, _ in sources([CANDIDATE / 'list-index-probe.dv']))
    fingerprints = {str(path): sha(path.read_bytes()) for path in sorted(tracked)}
    manifest = {'source_files': fingerprints, 'runtime_source': 'current-production-Runtime.cs; isolated candidate Runtime.cs is not used', 'csharp_probe_source': str(CANDIDATE / 'ContainerProbe.cs'), 'dv_probe_source': str(CANDIDATE / 'list-index-probe.dv'), 'scope': 'container values, aliases, lazy reads and errors only; no world/session execution'}
    (out / 'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    def save_report():
        (out / 'engineering-result.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    def run(command, name):
        stage = {'name': name, 'command': [str(item) for item in command], 'timeout_seconds': args.timeout}
        report['stages'].append(stage)
        started = time.perf_counter()
        try:
            with (out / (name + '.stdout')).open('wb') as stdout, (out / (name + '.stderr')).open('wb') as stderr:
                completed = subprocess.run([str(item) for item in command], cwd=ROOT, env=environment(), stdout=stdout, stderr=stderr, timeout=args.timeout)
            stage['exit_code'] = completed.returncode
            if completed.returncode:
                raise RuntimeError(f'{name}: process exited {completed.returncode}; original stdout/stderr retained')
        except subprocess.TimeoutExpired:
            stage['timed_out'] = True
            raise
        finally:
            stage['elapsed_seconds'] = time.perf_counter() - started
            save_report()

    def inspect(name, path):
        with path.open(encoding='utf-8-sig') as stream:
            for number, line in enumerate(stream, 1):
                packet = envelope(json.loads(line))
                if 'old' in packet and 'current' in packet:
                    old, current = packet['old'], packet['current']
                    label = packet.get('case', f'index-case-line-{number}')
                    report['comparisons'].append({'probe': name, 'case': label, 'input_index': packet.get('index'), 'raw_file': str(path), 'raw_line': number, 'declared_same': packet.get('same'), 'old_output_sha256': value_hash(old), 'current_output_sha256': value_hash(current), 'first_difference': first_difference(old, current)})
                elif packet.get('formal_session') == 'not-started':
                    report['markers'][name] = {'raw_line': number, 'value': packet}
                    if name == 'dv':
                        for key in ('empty_non_number', 'empty_nested_index'):
                            report['comparisons'].append({'probe': name, 'case': key, 'raw_line': number, 'declared_same': packet.get(key) == [], 'old_output_sha256': value_hash([]), 'current_output_sha256': value_hash(packet.get(key)), 'first_difference': first_difference([], packet.get(key))})
                else:
                    raise ValueError(f'{name}: unexpected output at line {number}; raw bytes retained')
        if name not in report['markers']:
            raise RuntimeError(f'{name}: no no-session completion marker; output incomplete')

    try:
        native = out / 'native-probe'
        native.mkdir()
        shutil.copyfile(ROOT / 'language_core/compiler/Runtime.cs', native / 'Runtime.cs')
        shutil.copyfile(CANDIDATE / 'ContainerProbe.cs', native / 'Program.cs')
        (native / 'Program.csproj').write_text(PROJECT, encoding='utf-8')
        run(['dotnet', 'build', native / 'Program.csproj', '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false'], 'native-build')
        run(['dotnet', native / 'bin/Release/net8.0/Program.dll'], 'native-output')
        inspect('native', out / 'native-output.stdout')
        program = out / 'dv-probe'
        run([sys.executable, ROOT / 'language_core/tools/build_session.py', '--entry', CANDIDATE / 'list-index-probe.dv', '--out', program, '--main', 'dvg_debug_run'], 'dv-generation')
        shutil.copyfile(ROOT / 'language_core/compiler/Runtime.cs', program / 'Runtime.cs')
        (program / 'Program.csproj').write_text(PROJECT, encoding='utf-8')
        run(['dotnet', 'build', program / 'Program.csproj', '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false'], 'dv-build')
        run(['dotnet', program / 'bin/Release/net8.0/Program.dll'], 'dv-output')
        inspect('dv', out / 'dv-output.stdout')
        report['complete'] = True
    except Exception as error:
        report['error'] = f'{type(error).__name__}: {error}'
    finally:
        report['source_changes_during_run'] = [{'path': path, 'initial_sha256': initial, 'final_sha256': sha(Path(path).read_bytes()) if Path(path).exists() else None} for path, initial in fingerprints.items() if not Path(path).exists() or sha(Path(path).read_bytes()) != initial]
        report['source_consistent'] = not report['source_changes_during_run']
        report['conclusion_scope'] = 'Inspect individual comparisons, concrete differences and errors; no gameplay or speedup conclusion is made.'
        save_report()
    if not report['complete'] or not report['source_consistent'] or any(item['declared_same'] is not True or item['first_difference'] is not None for item in report['comparisons']):
        raise SystemExit(1)
    print(str(out / 'engineering-result.json'))


if __name__ == '__main__':
    main()
