#!/usr/bin/env python3
"""Independent read-only scope-gate verification; neutral fixtures are owned here."""
import contextlib
import datetime
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import zipfile

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
EXECUTION = HERE.parent
ROOT = EXECUTION.parents[2]
F02 = EXECUTION / 'F02'
EXPECTED_GUARD = 'cf81c1bcb5c59f7cbea9d000bb1a6ca2fe7283e596c4bb0c60c1d77d8dfd017e'
BASE = '7489db56a714724eb7cb735958dd879dc662d238'
WORKERS = {
    'M01': {'branch': 'agent/M01-remove-old-case-run01', 'head': '249471e53a8c1208fc80e98835ea0d0ce6e15e00'},
    'H01a': {'branch': 'agent/H01a-line-provenance-run01', 'head': 'dc63af04213ac0d418b1b598bcd9e025d657eafe'},
}
ENV = {key: value for key, value in os.environ.items() if not key.upper().startswith('GIT_')}
ENV.update({'GIT_CONFIG_NOSYSTEM': '1', 'GIT_CONFIG_GLOBAL': os.devnull, 'GIT_NO_REPLACE_OBJECTS': '1',
            'GIT_OPTIONAL_LOCKS': '0', 'GIT_TERMINAL_PROMPT': '0', 'PYTHONDONTWRITEBYTECODE': '1'})


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=True, indent=2) + '\n', encoding='utf-8', newline='\n')


def frozen_sources():
    guard = (F02 / 'hardened_scope_guard.py').read_bytes()
    if sha(guard) != EXPECTED_GUARD:
        raise RuntimeError('Frozen guard hash mismatch; stop before verification')
    for name in ('hardened_scope_guard.py', 'check_hardened_guard.py'):
        data = (F02 / name).read_bytes()
        target = HERE / ('frozen-' + name)
        if target.exists():
            if target.read_bytes() != data:
                raise RuntimeError('Frozen input changed: ' + name)
        else:
            target.write_bytes(data)
    return {'guard_sha256': sha(guard), 'smoke_harness_sha256': sha((F02 / 'check_hardened_guard.py').read_bytes()),
            'bracket_harness_at_F02': (F02 / 'check_bracket_guard.py').exists(),
            'independent_driver_sha256': sha(Path(__file__).read_bytes())}


def run(argv, label, okay=True, cwd=None, location=HERE):
    raw = location / 'raw'
    raw.mkdir(exist_ok=True)
    stdout_path = raw / (label + '.stdout.bin')
    stderr_path = raw / (label + '.stderr.bin')
    if stdout_path.exists() or stderr_path.exists():
        raise RuntimeError('Refusing to overwrite evidence: ' + label)
    started = now()
    process = subprocess.run([str(arg) for arg in argv], cwd=cwd, env=ENV,
                             stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    stdout_path.write_bytes(process.stdout)
    stderr_path.write_bytes(process.stderr)
    row = {'label': label, 'started_at': started, 'finished_at': now(), 'argv': [str(arg) for arg in argv],
           'cwd': str(cwd) if cwd else None, 'returncode': process.returncode,
           'stdout_file': str(stdout_path.relative_to(HERE)), 'stderr_file': str(stderr_path.relative_to(HERE)),
           'stdout_sha256': sha(process.stdout), 'stderr_sha256': sha(process.stderr)}
    with (location / 'commands.jsonl').open('a', encoding='utf-8', newline='\n') as stream:
        stream.write(json.dumps(row) + '\n')
    if okay and process.returncode:
        raise RuntimeError(label + ': nonzero exit code; inspect preserved raw stderr')
    return process, label


def git(repo, *args, label, okay=True):
    return run(['git', '--no-optional-locks', '-C', repo, '-c', 'core.fsmonitor=false',
                '-c', 'core.hooksPath=' + os.devnull, '-c', 'core.excludesfile=' + os.devnull, *args],
               label, okay=okay)


def old34():
    inputs = frozen_sources()
    spec = importlib.util.spec_from_file_location('scope_smoke_round02', F02 / 'check_hardened_guard.py')
    module = importlib.util.module_from_spec(spec)
    original_argv = sys.argv
    try:
        sys.argv = [str(F02 / 'check_hardened_guard.py'), 'run-hardened-r01-scope-round02']
        spec.loader.exec_module(module)
    finally:
        sys.argv = original_argv
    module.RUN = HERE / 'old34'
    module.GUARD = HERE / 'frozen-hardened_scope_guard.py'

    def redirect_run(argv, okay=True):
        module.counter += 1
        return run(argv, f'{module.counter:04d}', okay=okay, location=module.RUN)

    module.run = redirect_run
    console = io.StringIO()
    try:
        with contextlib.redirect_stdout(console):
            code = module.main()
    finally:
        (HERE / 'old34-console.stdout.bin').write_bytes(console.getvalue().encode('utf-8'))
    result = json.loads((module.RUN / 'results.json').read_text(encoding='utf-8'))
    if result['total'] != 34 or result['matched'] != 34 or result['mismatches'] != 0 or code:
        raise RuntimeError('Old34 mismatch; inspect preserved raw evidence')
    inputs['old34_results_sha256'] = sha((module.RUN / 'results.json').read_bytes())
    print('Old34: 34/34 expected classifications matched.', flush=True)
    return inputs


def make_neutral(name, changed_path):
    repo = HERE / 'neutralrepos' / name / 'repo'
    repo.mkdir(parents=True, exist_ok=False)
    run(['git', '-c', 'init.defaultBranch=agent/bracket-test', 'init', repo], name + '-init')
    for option, value in [('core.autocrlf', 'false'), ('user.name', 'Independent scope test'),
                          ('user.email', 'scope-test@example.invalid')]:
        git(repo, 'config', '--local', option, value, label=name + '-' + option.replace('.', '-'))
    (repo / 'allowed').mkdir()
    (repo / 'allowed' / '[v].txt').write_bytes(b'neutral baseline\n')
    git(repo, 'add', '--', '.', label=name + '-add-base')
    git(repo, 'commit', '-m', 'neutral baseline', label=name + '-commit-base')
    process, _ = git(repo, 'rev-parse', 'HEAD', label=name + '-base')
    base = process.stdout.decode('ascii').strip()
    (repo / 'allowed' / changed_path).write_bytes(b'neutral changed bytes\n')
    git(repo, 'add', '--', '.', label=name + '-add-tip')
    git(repo, 'commit', '-m', 'neutral literal-path check', label=name + '-commit-tip')
    process, _ = git(repo, 'rev-parse', 'HEAD', label=name + '-tip')
    head = process.stdout.decode('ascii').strip()
    lock = {'schema': 'scope-lock.v1', 'status': 'SIGNED_BY_CONTROLLER', 'task_id': name,
            'capability_manifest_version': 'independent-neutral/v1', 'base_sha': base,
            'branch': 'agent/bracket-test', 'protected_roots': ['protected'],
            'allowed_writes': [{'path': 'allowed/[v].txt', 'operations': ['create', 'modify', 'delete']}],
            'build_output_dirs': []}
    lock_path = HERE / (name + '.scope.lock.json')
    write_json(lock_path, lock)
    return repo, lock_path, head


def brackets():
    expectations = {'sealed_at': now(), 'guard_sha256': EXPECTED_GUARD, 'cases': [
        {'id': 'literal-brackets', 'grant': 'allowed/[v].txt', 'actual_changed': 'allowed/[v].txt', 'required': 'PASS'},
        {'id': 'no-glob-expansion', 'grant': 'allowed/[v].txt', 'actual_changed': 'allowed/v.txt', 'required': 'DENY'},
    ]}
    expectation_path = HERE / 'bracket-expectations-before-run.json'
    write_json(expectation_path, expectations)
    expectation_hash = sha(expectation_path.read_bytes())
    rows = []
    for case in expectations['cases']:
        repo, lock_path, head = make_neutral(case['id'], Path(case['actual_changed']).name)
        process, command = run([sys.executable, HERE / 'frozen-hardened_scope_guard.py', '--repo', repo,
                                '--lock', lock_path, '--head', head, '--json'], case['id'] + '-gate', okay=False)
        result = json.loads(process.stdout.decode('utf-8'))
        row = {**case, 'actual': result['decision'], 'returncode': process.returncode, 'command': command,
               'head': head, 'lock_sha256': sha(lock_path.read_bytes()), 'problems': result['problems']}
        row['matches'] = row['required'] == row['actual']
        if case['id'] == 'literal-brackets':
            row['meaningful'] = result['decision'] == 'PASS' and result['changes'] == [
                {'layer': 'commit:' + head, 'path': 'allowed/[v].txt', 'operation': 'modify'}]
        else:
            row['meaningful'] = result['decision'] == 'DENY' and any(
                'ungranted create: allowed/v.txt' in problem for problem in result['problems'])
        rows.append(row)
        print(case['id'] + ': ' + result['decision'], flush=True)
    if sha(expectation_path.read_bytes()) != expectation_hash:
        raise RuntimeError('Bracket expectations changed during run')
    write_json(HERE / 'bracket-results.json', {'classification': 'INDEPENDENT_NEUTRAL_VERIFICATION',
                'expectations_sha256': expectation_hash, 'results': rows,
                'all_matched': all(row['matches'] and row['meaningful'] for row in rows)})
    if not all(row['matches'] and row['meaningful'] for row in rows):
        raise RuntimeError('Bracket case mismatch')
    manifest = []
    archive = HERE / 'neutral-repos.zip'
    with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as stream:
        for path in sorted((HERE / 'neutralrepos').rglob('*')):
            if path.is_file():
                data = path.read_bytes()
                rel = path.relative_to(HERE / 'neutralrepos').as_posix()
                stream.writestr(rel, data)
                manifest.append({'path': rel, 'bytes': len(data), 'sha256': sha(data)})
    with zipfile.ZipFile(archive) as stream:
        for row in manifest:
            if sha(stream.read(row['path'])) != row['sha256']:
                raise RuntimeError('Neutral archive verification failed')
    write_json(HERE / 'neutral-archive-integrity.json', {'archive': archive.name, 'sha256': sha(archive.read_bytes()),
               'file_count': len(manifest), 'files': manifest, 'residual_directory': 'neutralrepos/',
               'residual_ignored_by_local_gitignore': True, 'recursive_delete_performed': False})
    return rows


def actual(task, lock_expected):
    inputs = frozen_sources()
    detail = WORKERS[task]
    repo = ROOT / 'tmp' / 'solution-review-workers' / task
    lock_path = EXECUTION / 'control' / (task + '.scope.lock.json')
    lock_bytes = lock_path.read_bytes()
    if sha(lock_bytes) != lock_expected:
        raise RuntimeError('Controller frozen lock hash mismatch for ' + task)
    copied_lock = HERE / ('frozen-' + task + '.scope.lock.json')
    if copied_lock.exists():
        raise RuntimeError('Refusing to overwrite frozen worker lock')
    copied_lock.write_bytes(lock_bytes)
    lock = json.loads(lock_bytes.decode('utf-8'))
    if lock['base_sha'] != BASE or lock['branch'] != detail['branch']:
        raise RuntimeError('Worker lock baseline/branch mismatch')
    results = {}
    queries = {
        'branch': ('symbolic-ref', '--quiet', '--short', 'HEAD'),
        'head': ('rev-parse', '--verify', 'HEAD^{commit}'),
        'base': ('rev-parse', '--verify', BASE + '^{commit}'),
        'remotes': ('remote', '-v'),
        'shallow': ('rev-parse', '--is-shallow-repository'),
        'history-count': ('rev-list', '--count', 'HEAD'),
        'ancestry': ('merge-base', '--is-ancestor', BASE, detail['head']),
        'missing-objects': ('rev-list', '--objects', '--missing=print', '--all'),
        'fsck': ('fsck', '--strict', '--full', '--no-reflogs', '--no-dangling'),
        'status': ('status', '--porcelain=v1', '-z', '--untracked-files=all'),
        'commit-list': ('rev-list', '--reverse', BASE + '..' + detail['head']),
        'diff-raw': ('diff', '--no-ext-diff', '--no-textconv', '--no-renames', '--raw', '-z',
                     '--full-index', '--abbrev=40', BASE, detail['head']),
    }
    for key, args in queries.items():
        process, _ = git(repo, *args, label=task + '-' + key)
        results[key] = process.stdout
    if results['branch'].decode().strip() != detail['branch'] or results['head'].decode().strip() != detail['head']:
        raise RuntimeError('Worker branch/tip mismatch')
    if results['base'].decode().strip() != BASE or results['shallow'].decode().strip() != 'false' or results['remotes'].strip():
        raise RuntimeError('Worker base/full-history/no-remote requirement failed')
    missing = [line for line in results['missing-objects'].splitlines() if line.startswith(b'?')]
    if missing:
        raise RuntimeError('Missing worker objects')
    fields = results['diff-raw'].split(b'\0')
    changes = []
    for index in range(0, len(fields) - 1, 2):
        header, path = fields[index], fields[index + 1]
        if not header:
            break
        mode_old, mode_new, oid_old, oid_new, status = header.decode('ascii').split()
        changes.append({'path': path.decode('utf-8'), 'status': status,
                        'old_mode': mode_old.lstrip(':'), 'new_mode': mode_new,
                        'old_oid': oid_old, 'new_oid': oid_new})
    protected_module = importlib.util.spec_from_file_location('frozen_scope_guard_actual', HERE / 'frozen-hardened_scope_guard.py')
    guard = importlib.util.module_from_spec(protected_module)
    protected_module.loader.exec_module(guard)
    patterns = [guard.normalized_path(value, pattern=True).casefold() for value in lock['protected_roots']]
    old_tests = [row for row in changes if any('test' in piece.casefold() for piece in row['path'].split('/'))]
    protected_changes = [row for row in changes if guard.protected(row['path'], patterns)]
    grants = {row['path'].casefold(): row['operations'] for row in lock['allowed_writes']}
    for row in changes:
        operation = {'A': 'create', 'D': 'delete', 'M': 'modify'}.get(row['status'])
        if operation not in grants.get(row['path'].casefold(), []) or row['new_mode'] not in {'000000', '100644', '100755'}:
            raise RuntimeError('Unexpected actual diff path/mode/operation')
    print(task + ': metadata and diff paths/modes checked; no protected/test paths changed. Gate starting.', flush=True)
    process, _ = run([sys.executable, HERE / 'frozen-hardened_scope_guard.py', '--repo', repo,
                      '--lock', copied_lock, '--head', detail['head'], '--json'], task + '-gate', okay=False)
    result = json.loads(process.stdout.decode('utf-8'))
    if lock_path.read_bytes() != lock_bytes:
        raise RuntimeError('Controller lock changed during review')
    summary = {'task': task, 'finished_at': now(), **inputs, 'lock_sha256': lock_expected,
               'repo': str(repo), 'branch': detail['branch'], 'head': detail['head'], 'base': BASE,
               'full_history': True, 'origin_removed': True, 'history_count': int(results['history-count']),
               'missing_objects': len(missing), 'fsck_passed': True, 'changes_metadata': changes,
               'protected_changes': protected_changes, 'old_test_changes': old_tests,
               'gate_returncode': process.returncode, 'gate': result}
    write_json(HERE / (task + '-actual-results.json'), summary)
    if process.returncode or result['decision'] != 'PASS' or old_tests or protected_changes:
        raise RuntimeError('Actual gate or independent diff checks failed for ' + task)
    print(task + ': final gate PASS; commits=' + str(result['commits_audited']) + '; files=' + str(result['files_observed']), flush=True)


def integrity():
    rows = []
    for path in sorted(HERE.rglob('*')):
        if path.is_file() and 'neutralrepos' not in path.relative_to(HERE).parts and path.name != 'evidence-integrity.json':
            data = path.read_bytes()
            rows.append({'path': path.relative_to(HERE).as_posix(), 'bytes': len(data), 'sha256': sha(data)})
    write_json(HERE / 'evidence-integrity.json', {'sealed_at': now(), 'files': rows, 'file_count': len(rows)})


def main():
    phase = sys.argv[1]
    if phase == 'neutral':
        inputs = old34()
        rows = brackets()
        write_json(HERE / 'neutral-summary.json', {'finished_at': now(), **inputs,
                   'old34_matched': 34, 'old34_total': 34, 'bracket_cases': rows})
    elif phase == 'actual':
        actual(sys.argv[2], sys.argv[3])
    elif phase == 'integrity':
        integrity()
    else:
        raise RuntimeError('Unknown phase')


if __name__ == '__main__':
    main()
