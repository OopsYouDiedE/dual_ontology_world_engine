"""Opt-in production dialogue template differential; engineering only, no session."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[5]
TASK = Path(__file__).resolve().parent
ENTRY = TASK / 'goal-construction-probe.dv'
sys.path.insert(0, str(ROOT / 'language_core/tools'))
from dv_toolchain import PROJECT, environment
from dv_sources import sources


def sha(data):
    return hashlib.sha256(data).hexdigest()


def output_hash(value):
    return sha(json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf-8'))


def first_difference(left, right, path='$'):
    if type(left) is not type(right):
        return {'path': path, 'old_type': type(left).__name__, 'current_type': type(right).__name__, 'old': left, 'current': right}
    if isinstance(left, list):
        if len(left) != len(right):
            return {'path': path, 'old_length': len(left), 'current_length': len(right)}
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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True, help='Exclusive new engineering evidence directory')
    parser.add_argument('--timeout', type=int, default=180, help='Wall-clock limit per engineering process')
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    report = {'scope': 'pure-goal-construction-and-live-qualification-engineering-differential-only', 'formal_session': 'not-started', 'game_acceptance': 'not-performed', 'stages': [], 'comparisons': [], 'fixture_diagnostics': [], 'complete': False}
    paths = {path for path, _ in sources([ENTRY])}
    paths.update({ROOT / 'language_core/compiler/Runtime.cs', ROOT / 'language_core/compiler/compiler.dv', ROOT / 'language_core/tools/build_session.py', ROOT / 'language_core/tools/dv_toolchain.py', ROOT / 'language_core/tools/dv_sources.py', Path(__file__).resolve(), TASK / 'old-c2d-goals.dv.txt', TASK / 'old-c2m-goals.dv.txt'})
    fingerprints = {str(path): sha(path.read_bytes()) for path in sorted(paths)}
    manifest = {'source_files': fingerprints, 'entry': str(ENTRY), 'current_function_source': str(ROOT / 'language_core/verification/scenarios/chapter2/dialogue.dv'), 'differential_boundary': 'Actual production c2d_goals/c2m_goals vs preserved inline constructors; per-row complete when/id inputs and alias isolation. Dynamic owner qualification remains current and is exercised by existing fixtures. Other callees are current. Declaration installation and synthetic prior-context structures do not execute or establish game decisions.'}
    (out / 'source-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (out / '.gitattributes').write_text('*.stdout -text -diff\n*.stderr -text -diff\n', encoding='utf-8')

    def save():
        (out / 'engineering-result.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    def run(command, name):
        stage = {'name': name, 'command': list(map(str, command)), 'timeout_seconds': args.timeout}
        report['stages'].append(stage)
        started = time.perf_counter()
        try:
            with (out / (name + '.stdout')).open('wb') as stdout, (out / (name + '.stderr')).open('wb') as stderr:
                result = subprocess.run(list(map(str, command)), cwd=ROOT, env=environment(), stdout=stdout, stderr=stderr, timeout=args.timeout)
            stage['exit_code'] = result.returncode
            if result.returncode:
                raise RuntimeError(f'{name} exited {result.returncode}; original logs retained')
        except subprocess.TimeoutExpired:
            stage['timed_out'] = True
            raise
        finally:
            stage['elapsed_seconds'] = time.perf_counter() - started
            save()

    try:
        program = out / 'program'
        run([sys.executable, ROOT / 'language_core/tools/build_session.py', '--entry', ENTRY, '--out', program, '--main', 'dvg_debug_run'], 'generation')
        shutil.copyfile(ROOT / 'language_core/compiler/Runtime.cs', program / 'Runtime.cs')
        (program / 'Program.csproj').write_text(PROJECT, encoding='utf-8')
        run(['dotnet', 'build', program / 'Program.csproj', '-c', 'Release', '--disable-build-servers', '-p:NuGetAudit=false'], 'probe-build')
        run(['dotnet', program / 'bin/Release/net8.0/Program.dll'], 'probe-output')
        with (out / 'probe-output.stdout').open(encoding='utf-8-sig') as stream:
            for number, line in enumerate(stream, 1):
                value = json.loads(line)
                packet = dict(value) if isinstance(value, list) else value
                if 'case' in packet:
                    old, current = packet['old'], packet['current']
                    report['comparisons'].append({'case': packet['case'], 'raw_line': number, 'dv_same': packet['same'], 'old_output_sha256': output_hash(old), 'current_output_sha256': output_hash(current), 'first_difference': first_difference(old, current)})
                elif 'fixture_scope' in packet:
                    report['fixture_diagnostics'].append({'raw_line': number, 'value': packet})
                elif packet.get('formal_session') == 'not-started':
                    report['final_marker'] = {'raw_line': number, 'value': packet}
                else:
                    raise ValueError(f'Unexpected output at raw line {number}')
        if 'final_marker' not in report:
            raise RuntimeError('Missing engineering-only completion marker; output incomplete')
        report['complete'] = True
    except Exception as error:
        report['error'] = f'{type(error).__name__}: {error}'
    finally:
        changes = []
        for path, original in fingerprints.items():
            final = sha(Path(path).read_bytes()) if Path(path).exists() else None
            if final != original:
                changes.append({'path': path, 'initial_sha256': original, 'final_sha256': final})
        report['source_changes_during_run'] = changes
        report['source_consistent'] = not changes
        report['conclusion_scope'] = 'Inspect complete named goal values, concrete differences and source consistency. Synthetic owner-context fixtures are not performed choices. No game behavior, runtime speedup or formal-session conclusion.'
        save()
    if not report['complete'] or not report['source_consistent'] or any(item['dv_same'] is not True or item['first_difference'] is not None for item in report['comparisons']):
        raise SystemExit(1)
    print(str(out / 'engineering-result.json'))


if __name__ == '__main__':
    main()
