#!/usr/bin/env python3
"""Neutral bracket-name regression, preserving original failure and raw bytes."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import zipfile

HERE = Path(__file__).resolve().parent


def sha(data):
    return hashlib.sha256(data).hexdigest()


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--phase', choices=('before', 'after'), required=True)
    parser.add_argument('--run-name', required=True)
    args = parser.parse_args()
    if not re.fullmatch(r'run-bracket-[a-z0-9-]+', args.run_name):
        raise ValueError('Use a simple run-bracket-* directory name')
    destination = HERE / args.run_name
    destination.mkdir(exist_ok=False)
    (destination / 'raw').mkdir()
    counter = 0
    guard_bytes = (HERE / 'hardened_scope_guard.py').read_bytes()
    tested_guard = destination / 'guard-under-test.py'
    tested_guard.write_bytes(guard_bytes)
    expectations = [{'id': 'B01_literal_brackets', 'required_decision': 'PASS',
                     'reason': 'Windows-legal literal brackets in an exact grant and tracked filename must be accepted.'}]
    if args.phase == 'after':
        expectations.append({'id': 'B02_no_glob_expansion', 'required_decision': 'DENY',
                             'required_diagnostic': 'ungranted modify: allowed/neutralv.txt',
                             'reason': 'The literal neutral[value].txt grant must not match neutralv.txt.'})
    expectation_path = destination / 'expectations-before-run.json'
    expectation_path.write_text(json.dumps({'sealed_at': now(), 'phase': args.phase,
                                          'classification': 'IMPLEMENTATION_SMOKE_ONLY', 'tests': expectations}, indent=2) + '\n', encoding='utf-8')
    expected_hash = sha(expectation_path.read_bytes())
    (destination / 'inputs.json').write_text(json.dumps({'guard_sha256': sha(guard_bytes), 'expectations_sha256': expected_hash}, indent=2) + '\n', encoding='utf-8')

    def execute(argv, okay=True):
        nonlocal counter
        counter += 1
        command_id = f'{counter:04d}'
        start = now()
        process = subprocess.run([str(item) for item in argv], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        out, err = destination / 'raw' / (command_id + '.stdout.bin'), destination / 'raw' / (command_id + '.stderr.bin')
        out.write_bytes(process.stdout)
        err.write_bytes(process.stderr)
        record = {'id': command_id, 'started_at': start, 'finished_at': now(), 'argv': [str(item) for item in argv],
                  'returncode': process.returncode, 'stdout_file': str(out.relative_to(destination)),
                  'stderr_file': str(err.relative_to(destination)), 'stdout_sha256': sha(process.stdout),
                  'stderr_sha256': sha(process.stderr)}
        with (destination / 'commands.jsonl').open('a', encoding='utf-8', newline='\n') as stream:
            stream.write(json.dumps(record) + '\n')
        if okay and process.returncode:
            raise RuntimeError(process.stderr.decode(errors='replace'))
        return process, command_id

    def git(repo, *argv):
        return execute(['git', '--literal-pathspecs', '-C', repo, *argv])

    results = []
    for case in expectations:
        case_dir = destination / 'cases' / case['id']
        repo = case_dir / 'repo'
        repo.mkdir(parents=True)
        git(repo, 'init', '--initial-branch=agent/F02-bracket')
        for key, value in [('user.name', 'F02 Neutral Bracket'), ('user.email', 'f02@example.invalid'),
                           ('core.autocrlf', 'false'), ('core.symlinks', 'false'), ('core.ignorecase', 'true')]:
            git(repo, 'config', '--local', key, value)
        (repo / 'allowed').mkdir()
        (repo / 'allowed/neutral[value].txt').write_bytes(b'neutral bracket baseline\n')
        (repo / 'allowed/neutralv.txt').write_bytes(b'neutral alternate baseline\n')
        git(repo, 'add', '--', '.')
        git(repo, 'commit', '-m', 'Neutral bracket baseline')
        baseline, _ = git(repo, 'rev-parse', 'HEAD')
        changed = 'allowed/neutral[value].txt' if case['id'] == 'B01_literal_brackets' else 'allowed/neutralv.txt'
        (repo / changed).write_bytes(b'neutral test update\n')
        git(repo, 'add', '--', changed)
        git(repo, 'commit', '-m', 'Neutral bracket test update')
        head, _ = git(repo, 'rev-parse', 'HEAD')
        lock = {'schema': 'scope-lock.v1', 'status': 'SIGNED_BY_CONTROLLER', 'task_id': case['id'],
                'branch': 'agent/F02-bracket', 'base_sha': baseline.stdout.decode().strip(),
                'capability_manifest_version': 'F02-neutral-v1', 'allowed_reads': [],
                'allowed_writes': [{'path': 'allowed/neutral[value].txt', 'operations': ['modify']}],
                'protected_roots': ['oracle/**'], 'build_output_dirs': []}
        lock_path = case_dir / 'scope.lock.json'
        lock_path.write_text(json.dumps(lock, indent=2) + '\n', encoding='utf-8')
        process, command_id = execute([sys.executable, tested_guard, '--repo', repo, '--lock', lock_path,
                                       '--head', head.stdout.decode().strip(), '--json'], okay=False)
        actual = 'PASS' if process.returncode == 0 else 'DENY'
        diagnostic = process.stderr.decode(errors='replace')
        expected_diagnostic = case.get('required_diagnostic')
        matches = case['required_decision'] == actual and (not expected_diagnostic or expected_diagnostic in diagnostic)
        results.append({**case, 'actual_decision': actual, 'returncode': process.returncode,
                        'meets_requirement': matches, 'command_id': command_id,
                        'stdout': process.stdout.decode(errors='replace'), 'stderr': diagnostic})
        print(f'{case["id"]}: required={case["required_decision"]}, actual={actual}, rc={process.returncode}', flush=True)
    assert sha(expectation_path.read_bytes()) == expected_hash
    summary = {'classification': 'IMPLEMENTATION_SMOKE_ONLY', 'phase': args.phase,
               'guard_sha256': sha(guard_bytes), 'finished_at': now(), 'total': len(results),
               'requirements_met': sum(row['meets_requirement'] for row in results),
               'commands_recorded': counter, 'original_false_positive_preserved': args.phase == 'before',
               'results': results}
    (destination / 'results.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
    integrity = []
    archive = destination / 'raw.zip'
    with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as zipped:
        for path in sorted((destination / 'raw').iterdir()):
            checksum = sha(path.read_bytes())
            integrity.append({'file': 'raw/' + path.name, 'sha256': checksum, 'length': path.stat().st_size})
            zipped.write(path, arcname='raw/' + path.name)
    with zipfile.ZipFile(archive) as zipped:
        for row in integrity:
            assert sha(zipped.read(row['file'])) == row['sha256']
    (destination / 'raw-integrity.json').write_text(json.dumps(integrity, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in summary.items() if key != 'results'}), flush=True)
    if args.phase == 'before':
        assert results[0]['actual_decision'] == 'DENY' and 'wildcard in exact path' in results[0]['stderr']
        return 0
    return 0 if all(row['meets_requirement'] for row in results) else 1


if __name__ == '__main__':
    sys.exit(main())
