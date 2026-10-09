#!/usr/bin/env python3
"""Archive raw experiment bytes and check only permitted control inputs/metadata."""
import ast
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import zipfile

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[3]
PROVIDED = PROJECT / 'artifacts/story-hardcode-marking/execution-20261010/provided-pack'
POST = HERE / 'postflight'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    POST.mkdir(exist_ok=False)
    records = []
    for number, args in enumerate((['git', '-C', str(PROJECT), 'rev-parse', 'HEAD'],
                                   ['git', '-C', str(PROJECT), 'status', '--porcelain=v1', '--untracked-files=no']), 1):
        process = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        out = POST / f'{number:04d}.stdout.bin'
        err = POST / f'{number:04d}.stderr.bin'
        out.write_bytes(process.stdout)
        err.write_bytes(process.stderr)
        records.append({'argv': args, 'returncode': process.returncode, 'stdout_file': str(out.relative_to(HERE)),
                        'stderr_file': str(err.relative_to(HERE)), 'stdout_sha256': sha(process.stdout),
                        'stderr_sha256': sha(process.stderr)})
        assert process.returncode == 0
    inputs = json.loads((HERE / 'run-20261010a/inputs.json').read_text(encoding='utf-8'))
    original_guard_unchanged = sha((PROVIDED / 'agent-scope-guard.py').read_bytes()) == inputs['guard_sha256']
    original_example_unchanged = sha((PROVIDED / 'scope-lock.example.json').read_bytes()) == inputs['example_sha256']
    head_unchanged = (POST / '0001.stdout.bin').read_bytes() == (HERE / 'run-20261010a/raw/0001.stdout.bin').read_bytes()
    tracked_status_unchanged = (POST / '0002.stdout.bin').read_bytes() == (HERE / 'run-20261010a/raw/0002.stdout.bin').read_bytes()
    archives = []
    for run_name in ('run-20261010a', 'run-hardened-20261010a', 'run-hardened-20261010b'):
        run = HERE / run_name
        raw = sorted((run / 'raw').iterdir())
        integrity = [{'file': str(path.relative_to(run)), 'length': path.stat().st_size,
                      'sha256': sha(path.read_bytes())} for path in raw]
        # The first run already contains its original hash index; verify it.
        prior = run / 'raw-integrity.json'
        if prior.exists():
            recorded = json.loads(prior.read_text(encoding='utf-8'))
            assert {Path(row['file']).name: row['sha256'] for row in recorded} == {path.name: sha(path.read_bytes()) for path in raw}
        else:
            prior.write_text(json.dumps(integrity, indent=2) + '\n', encoding='utf-8')
        archive = run / 'raw.zip'
        with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as zipped:
            for path in raw:
                zipped.write(path, arcname='raw/' + path.name)
        with zipfile.ZipFile(archive) as zipped:
            for path in raw:
                assert zipped.read('raw/' + path.name) == path.read_bytes()
        archives.append({'file': str(archive.relative_to(HERE)), 'sha256': sha(archive.read_bytes()),
                         'raw_files': len(raw), 'verified_byte_for_byte': True})
    syntax = []
    for name in ('run_guard_validation.py', 'hardened_scope_guard.py', 'check_hardened_guard.py', 'finish_evidence.py'):
        ast.parse((HERE / name).read_text(encoding='utf-8'), filename=name)
        syntax.append(name)
    hardening_input = json.loads((HERE / 'run-hardened-20261010b/inputs.json').read_text(encoding='utf-8'))
    hardened_matches_tested_copy = sha((HERE / 'hardened_scope_guard.py').read_bytes()) == hardening_input['guard_sha256']
    assertions = [original_guard_unchanged, original_example_unchanged, head_unchanged,
                  tracked_status_unchanged, hardened_matches_tested_copy]
    assert all(assertions)
    data = {'finished_at': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'commands': records,
            'original_guard_unchanged': original_guard_unchanged, 'original_example_unchanged': original_example_unchanged,
            'head_unchanged': head_unchanged, 'tracked_status_unchanged': tracked_status_unchanged,
            'hardened_matches_tested_copy': hardened_matches_tested_copy,
            'hardened_sha256': hardening_input['guard_sha256'], 'syntax_checked': syntax, 'archives': archives,
            'classification': 'DETECTION_ONLY', 'independent_revalidation': 'PENDING_SEPARATE_AGENT'}
    (POST / 'verification.json').write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(data, indent=2))


if __name__ == '__main__':
    main()
