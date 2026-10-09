#!/usr/bin/env python3
"""Implementation smoke checks; independent final acceptance belongs to a new agent."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ORIGINAL = HERE / 'run-20261010a'
RUN_NAME = sys.argv[1] if len(sys.argv) > 1 else 'run-hardened-20261010a'
if not re.fullmatch(r'run-hardened-[a-z0-9-]+', RUN_NAME):
    raise ValueError('Run name must be a simple run-hardened-* directory name')
RUN = HERE / RUN_NAME
GUARD = HERE / 'hardened_scope_guard.py'
counter = 0


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def sha(value):
    return hashlib.sha256(value).hexdigest()


def run(argv, okay=True):
    global counter
    counter += 1
    ident = f'{counter:04d}'
    started = now()
    process = subprocess.run([str(arg) for arg in argv], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    out = RUN / 'raw' / (ident + '.stdout.bin')
    err = RUN / 'raw' / (ident + '.stderr.bin')
    out.write_bytes(process.stdout)
    err.write_bytes(process.stderr)
    record = {'id': ident, 'started_at': started, 'finished_at': now(), 'argv': [str(arg) for arg in argv],
              'returncode': process.returncode, 'stdout_file': str(out.relative_to(HERE)),
              'stderr_file': str(err.relative_to(HERE)), 'stdout_sha256': sha(process.stdout),
              'stderr_sha256': sha(process.stderr)}
    with (RUN / 'commands.jsonl').open('a', encoding='utf-8', newline='\n') as stream:
        stream.write(json.dumps(record) + '\n')
    if okay and process.returncode:
        raise RuntimeError(f'{ident}: {process.stderr.decode(errors="replace")}')
    return process, ident


def main():
    RUN.mkdir(exist_ok=False)
    (RUN / 'raw').mkdir()
    guard_bytes = GUARD.read_bytes()
    (RUN / 'guard-under-test.py').write_bytes(guard_bytes)
    original_rows = json.loads((ORIGINAL / 'results.json').read_text(encoding='utf-8'))['results']
    cases = []
    for row in original_rows:
        ident = row['test_id']
        if ident == '22_symbolic_parent':
            continue
        fixture_ident = '16_masked_stage_basic' if ident == '17_masked_stage_integration' else ident
        repo = ORIGINAL / 'cases' / fixture_ident / 'repo'
        lock_path = ORIGINAL / 'cases' / fixture_ident / 'scope.lock.json'
        if ident == '01_template':
            lock_path = HERE.parents[3] / 'artifacts/story-hardcode-marking/execution-20261010/provided-pack/scope-lock.example.json'
        wanted = 'PASS' if ident in {'02_legal_modify', '03_legal_create_delete', '10_legal_rename', '25_self_signed_lock', '29_read_not_enforced'} else 'DENY'
        cases.append({'id': ident, 'repo': str(repo), 'lock': str(lock_path), 'required': wanted})
    cases += [
        {'id': 'H01_exact_build_output', 'fixture': '18_ignored_outside', 'required': 'PASS', 'build_output_dirs': ['ignored']},
        {'id': 'H02_protected_build_output', 'fixture': '18_ignored_outside', 'required': 'DENY', 'build_output_dirs': ['oracle']},
        {'id': 'H03_tracked_build_output', 'fixture': '02_legal_modify', 'required': 'DENY', 'build_output_dirs': ['allowed']},
        {'id': 'H04_wildcard_build_output', 'fixture': '18_ignored_outside', 'required': 'DENY', 'build_output_dirs': ['ignored/**']},
    ]
    expectations = json.dumps({'sealed_at': now(), 'classification': 'IMPLEMENTATION_SMOKE_ONLY',
                               'external_limits_expected_to_remain': ['25_self_signed_lock', '29_read_not_enforced'],
                               'tests': cases}, indent=2) + '\n'
    (RUN / 'expectations-before-run.json').write_text(expectations, encoding='utf-8')
    expected_hash = sha((RUN / 'expectations-before-run.json').read_bytes())
    (RUN / 'inputs.json').write_text(json.dumps({'guard_sha256': sha(guard_bytes), 'expectations_sha256': expected_hash}, indent=2) + '\n', encoding='utf-8')
    results = []
    for row in cases:
        ident = row['id']
        if 'fixture' in row:
            repo = ORIGINAL / 'cases' / row['fixture'] / 'repo'
            lock = json.loads((ORIGINAL / 'cases' / row['fixture'] / 'scope.lock.json').read_text(encoding='utf-8'))
            lock['build_output_dirs'] = row['build_output_dirs']
            lock['task_id'] = ident
            lock_path = RUN / (ident + '.scope.lock.json')
            lock_path.write_text(json.dumps(lock, indent=2) + '\n', encoding='utf-8')
        else:
            repo, lock_path = Path(row['repo']), Path(row['lock'])
        process, _ = run(['git', '-C', repo, 'rev-parse', 'HEAD'])
        frozen = process.stdout.decode().strip()
        if ident == '26_frozen_tip':
            frozen = json.loads(lock_path.read_text(encoding='utf-8'))['base_sha']
        process, command = run([sys.executable, RUN / 'guard-under-test.py', '--repo', repo, '--lock', lock_path, '--head', frozen, '--json'], okay=False)
        actual = 'PASS' if process.returncode == 0 else 'DENY'
        result = {'id': ident, 'required': row['required'], 'actual': actual, 'returncode': process.returncode,
                  'matches': row['required'] == actual, 'command_id': command,
                  'stdout': process.stdout.decode(errors='replace'), 'stderr': process.stderr.decode(errors='replace')}
        results.append(result)
        print(f'{ident}: expected={row["required"]}, actual={actual}, rc={process.returncode}', flush=True)
    if sha((RUN / 'expectations-before-run.json').read_bytes()) != expected_hash:
        raise RuntimeError('Sealed smoke expectations changed')
    summary = {'classification': 'IMPLEMENTATION_SMOKE_ONLY', 'finished_at': now(), 'total': len(results),
               'matched': sum(row['matches'] for row in results), 'mismatches': sum(not row['matches'] for row in results),
               'commands_recorded': counter, 'results': results}
    (RUN / 'results.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in summary.items() if key != 'results'}), flush=True)
    return 1 if summary['mismatches'] else 0


if __name__ == '__main__':
    sys.exit(main())
