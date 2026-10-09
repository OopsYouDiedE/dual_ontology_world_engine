"""R01 independent M01/F02 check; only neutral fixtures, no design/story reads."""
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
CLONE = ROOT / 'tmp/solution-review-workers/M01'
F02 = HERE.parent / 'F02'
LOCK_SOURCE = HERE.parent / 'control/M01.scope.lock.json'
BASE = '7489db56a714724eb7cb735958dd879dc662d238'
HEAD = '249471e53a8c1208fc80e98835ea0d0ce6e15e00'
FROZEN_GUARD_HASH = 'ac6a34ef73bf70d5b4bb2b4bb8ded5cf0441c3184021f567402f46cfc9cbc6c1'
COUNTER = max((int(json.loads(row)['id']) for row in (HERE / 'commands.jsonl').read_text(encoding='utf-8').splitlines()), default=0) if (HERE / 'commands.jsonl').exists() else 0
ENV = {key: value for key, value in os.environ.items() if not key.upper().startswith('GIT_')}
ENV.update({'GIT_CONFIG_NOSYSTEM': '1', 'GIT_CONFIG_GLOBAL': os.devnull, 'GIT_TERMINAL_PROMPT': '0'})

def sha(data):
    return hashlib.sha256(data).hexdigest()

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def save_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')

def run(label, argv, cwd=ROOT, required=None):
    global COUNTER
    COUNTER += 1
    ident = f'{COUNTER:04d}'
    started = now()
    p = subprocess.run([str(arg) for arg in argv], cwd=cwd, env=ENV, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    out, err = HERE / 'raw' / (ident + '.stdout.bin'), HERE / 'raw' / (ident + '.stderr.bin')
    out.write_bytes(p.stdout)
    err.write_bytes(p.stderr)
    row = {'id': ident, 'label': label, 'started_at': started, 'finished_at': now(), 'cwd': str(cwd),
           'argv': [str(arg) for arg in argv], 'returncode': p.returncode,
           'stdout_file': str(out.relative_to(HERE)), 'stderr_file': str(err.relative_to(HERE)),
           'stdout_sha256': sha(p.stdout), 'stderr_sha256': sha(p.stderr)}
    with (HERE / 'commands.jsonl').open('a', encoding='utf-8', newline='\n') as s:
        s.write(json.dumps(row, ensure_ascii=False) + '\n')
    print(f'{ident} {label}: rc={p.returncode}', flush=True)
    if required is not None and p.returncode != required:
        raise RuntimeError(f'{label}: expected rc={required}; observed rc={p.returncode}')
    return p, row

def prepare():
    (HERE / 'raw').mkdir(exist_ok=False)
    expected = {
        'sealed_at': now(), 'review': 'R01-M01 independent review; fresh context; no design/story/golden/history reads',
        'base': BASE, 'frozen_tip': HEAD,
        'm01': {
            'expected_files': {'csharp/HallCausalityRules.cs': 'delete', 'csharp/HallReplayProgram.cs': 'delete',
                              'csharp/AGENTS.md': 'modify-only-two-module-lines',
                              'artifacts/story-hardcode-marking/execution-20261010/M01/implementation-report.md': 'create'},
            'expected_capability_additions': 0, 'expected_test_edits': 0,
            'builds_and_regressions_expected_returncode': 0,
            'current_scope_lock_expected_guard_decision': 'DENY',
            'current_lock_reason': 'godot_csharp protected root conflicts with explicitly allowed build-output dirs',
        },
        'guard_expected': {
            'legal-clean': 'PASS', 'legal-build': 'PASS', 'hidden-intermediate-protected': 'DENY',
            'hidden-intermediate-test': 'DENY', 'ignored-expected': 'DENY', 'case-protected': 'DENY',
            'assume-unchanged': 'DENY', 'skip-worktree': 'DENY', 'staged-protected': 'DENY',
            'untracked-protected': 'DENY', 'excluded-expected': 'DENY', 'symlink': 'DENY', 'junction': 'DENY',
            'worktree-link': 'DENY', 'alternate-objects': 'DENY', 'frozen-tip': 'DENY',
            'build-protected': 'DENY', 'build-tracked': 'DENY', 'build-wildcard': 'DENY',
            'outside-build': 'DENY', 'raw-byte-dirty': 'DENY', 'self-signed': 'PASS', 'read-acl': 'PASS',
        },
        'external_gates_expected': ['read_acl NOT_ENFORCED', 'write_acl NOT_ENFORCED',
                                    'lock_authenticity NOT_VERIFIED', 'approval_authenticity NOT_VERIFIED',
                                    'merge_identity NOT_VERIFIED', 'semantic_review NOT_VERIFIED',
                                    'immutable_review_snapshot NOT_ENFORCED'],
        'f02_smoke': {'expected_total': 34, 'expected_mismatches': 0, 'classification': 'IMPLEMENTATION_SMOKE_ONLY'},
        'exploratory': {'hardlink': 'observe only; guard documents symlink/reparse detection, not hardlink rejection'},
    }
    save_json(HERE / 'expectations-before-run.json', expected)
    hashes = {}
    for filename, source in [('guard-under-test.py', F02 / 'hardened_scope_guard.py'),
                             ('smoke-under-test.py', F02 / 'check_hardened_guard.py'),
                             ('M01.scope.lock.initial.json', LOCK_SOURCE)]:
        data = source.read_bytes()
        (HERE / filename).write_bytes(data)
        hashes[filename] = sha(data)
    if hashes['guard-under-test.py'] != FROZEN_GUARD_HASH:
        raise RuntimeError('F02 guard differs from frozen SHA256')
    hashes['expectations-before-run.json'] = sha((HERE / 'expectations-before-run.json').read_bytes())
    save_json(HERE / 'frozen-inputs.json', {'sealed_at': now(), 'sha256': hashes})
    save_json(HERE / 'environment.json', {'time_utc': now(), 'python': sys.version, 'executable': sys.executable,
               'platform': sys.platform, 'cwd': str(ROOT), 'clone': str(CLONE),
               'selected_env': {key: os.environ.get(key) for key in ['OS', 'PROCESSOR_ARCHITECTURE', 'GODOT', 'DOTNET_ROOT']},
               'git_environment': {key: value for key, value in ENV.items() if key.startswith('GIT_')}})

def metadata_build():
    commands = [
        ('git-version', ['git', '--version'], ROOT),
        ('dotnet-info', ['dotnet', '--info'], ROOT),
        ('clone-branch', ['git', 'symbolic-ref', '--short', 'HEAD'], CLONE),
        ('clone-head', ['git', 'rev-parse', 'HEAD'], CLONE),
        ('clone-status-before', ['git', 'status', '--porcelain=v2', '--branch'], CLONE),
        ('clone-config', ['git', 'config', '--local', '--no-includes', '--list'], CLONE),
        ('clone-commits', ['git', 'log', '--format=%H %P %s', BASE + '..' + HEAD], CLONE),
        ('clone-diff-names', ['git', 'diff', '--no-ext-diff', '--no-textconv', '--name-status', BASE, HEAD], CLONE),
        ('clone-diff-full', ['git', 'diff', '--no-ext-diff', '--no-textconv', '--binary', BASE, HEAD], CLONE),
        ('clone-diff-check', ['git', 'diff', '--check', BASE, HEAD], CLONE),
        ('old-class-reference-index', ['rg', '--line-number', '--glob', '*.cs', '--glob', '*.csproj', '--glob', '*.sln',
                                     '--glob', 'AGENTS.md', '--glob', 'README.md',
                                     'HallCausalityRules|HallReplayProgram', 'csharp', 'godot_csharp'], CLONE),
    ]
    for label, cmd, cwd in commands:
        run(label, cmd, cwd)
    run('M01-guard-initial', [sys.executable, HERE / 'guard-under-test.py', '--repo', CLONE,
                            '--lock', HERE / 'M01.scope.lock.initial.json', '--head', HEAD, '--json'])
    builds = []
    for label, cmd in [
        ('build-core', ['dotnet', 'build', 'csharp/Core.sln']),
        ('build-host', ['dotnet', 'build', 'godot_csharp/DualOntologyWorldEngine.sln']),
        ('scope-regression', ['dotnet', 'run', '--project', 'csharp/ScopeRegression.csproj']),
        ('reasoner-regression', ['dotnet', 'run', '--project', 'csharp/ReasonerRegression.csproj']),
    ]:
        p, row = run(label, cmd, CLONE)
        builds.append({'label': label, 'returncode': p.returncode, 'command_id': row['id']})
    save_json(HERE / 'build-results.json', {'frozen_tip': HEAD, 'commands': builds})
    run('clone-status-after', ['git', 'status', '--porcelain=v2', '--branch'], CLONE)
    run('M01-guard-after-build', [sys.executable, HERE / 'guard-under-test.py', '--repo', CLONE,
                            '--lock', HERE / 'M01.scope.lock.initial.json', '--head', HEAD, '--json'])

def git(repo, *args, required=0):
    return run('fixture-git-' + args[0], ['git', '-C', repo, *args], required=required)[0]

def fixture(ident):
    directory = HERE / 'fixtures-run03' / ident
    directory.mkdir(parents=True)
    repo = directory / 'repo'
    repo.mkdir()
    git(repo, 'init', '-b', 'agent/r01-neutral')
    git(repo, 'config', 'user.name', 'R01 Neutral')
    git(repo, 'config', 'user.email', 'r01-neutral@example.invalid')
    git(repo, 'config', 'core.autocrlf', 'false')
    for name, data in {'allowed/item.txt': b'base\n', 'tests/expected.txt': b'expected-neutral\n',
                       'oracle/value.txt': b'protected-neutral\n', '.gitignore': b'ignored/\n'}.items():
        target = repo / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    git(repo, 'add', '.')
    git(repo, 'commit', '-m', 'Neutral baseline')
    base = git(repo, 'rev-parse', 'HEAD').stdout.decode().strip()
    (repo / 'allowed/item.txt').write_bytes(b'legal-neutral\n')
    git(repo, 'add', 'allowed/item.txt')
    git(repo, 'commit', '-m', 'Legal neutral change')
    lock = {'schema': 'scope-lock.v1', 'status': 'SIGNED_BY_CONTROLLER', 'task_id': ident,
            'branch': 'agent/r01-neutral', 'base_sha': base, 'capability_manifest_version': 'r01-neutral/v1',
            'allowed_reads': ['allowed/item.txt'],
            'allowed_writes': [{'path': 'allowed/item.txt', 'operations': ['modify']}],
            'protected_roots': ['oracle', 'tests'], 'build_output_dirs': ['build'], 'isolation': 'DETECTION_ONLY',
            'approval_authenticated_by_guard': False}
    return directory, repo, lock

def commit_all(repo, message):
    git(repo, 'add', '-A')
    git(repo, 'commit', '-m', message)

def independent_cases():
    expected = json.loads((HERE / 'expectations-before-run.json').read_text(encoding='utf-8'))['guard_expected']
    previous_path = HERE / 'independent-guard-partial-results.json'
    results = json.loads(previous_path.read_text(encoding='utf-8'))['results'] if previous_path.exists() else []
    completed = {row['id'] for row in results}
    for ident in [*expected, 'hardlink']:
        if ident in completed:
            continue
        directory, repo, lock = fixture(ident)
        frozen = None
        if ident.startswith('hidden-intermediate'):
            path = 'oracle/value.txt' if ident.endswith('protected') else 'tests/expected.txt'
            old = (repo / path).read_bytes()
            (repo / path).write_bytes(b'forbidden-transient-neutral\n')
            commit_all(repo, 'Forbidden transient change')
            (repo / path).write_bytes(old)
            commit_all(repo, 'Revert forbidden transient change')
        elif ident == 'legal-build':
            (repo / 'build').mkdir()
            (repo / 'build/item.bin').write_bytes(b'neutral-build')
        elif ident == 'ignored-expected':
            (repo / 'ignored').mkdir()
            (repo / 'ignored/expected.txt').write_bytes(b'ignored-neutral-expectation')
        elif ident == 'case-protected':
            git(repo, 'mv', 'oracle', '_case_temporary')
            git(repo, 'mv', '_case_temporary', 'Oracle')
            (repo / 'Oracle/value.txt').write_bytes(b'case-neutral')
            commit_all(repo, 'Case changed protected content')
        elif ident in {'assume-unchanged', 'skip-worktree'}:
            git(repo, 'update-index', '--' + ident, 'tests/expected.txt')
            (repo / 'tests/expected.txt').write_bytes(b'hidden-neutral')
        elif ident == 'staged-protected':
            (repo / 'tests/expected.txt').write_bytes(b'staged-neutral')
            git(repo, 'add', 'tests/expected.txt')
        elif ident == 'untracked-protected':
            (repo / 'oracle/untracked.txt').write_bytes(b'untracked-neutral')
        elif ident == 'excluded-expected':
            (repo / '.git/info/exclude').write_bytes(b'tests/hidden.txt\n')
            (repo / 'tests/hidden.txt').write_bytes(b'excluded-neutral')
        elif ident == 'symlink':
            target = directory / 'neutral-symlink-target.bin'
            target.write_bytes(b'item.txt')
            oid = git(repo, 'hash-object', '-w', str(target)).stdout.decode().strip()
            git(repo, 'update-index', '--add', '--cacheinfo', '120000,' + oid + ',allowed/link.txt')
            git(repo, 'commit', '-m', 'Neutral Git symlink mode')
            (repo / 'allowed/link.txt').write_bytes(b'item.txt')
        elif ident == 'junction':
            target = directory / 'outside'
            target.mkdir()
            (target / 'neutral.txt').write_bytes(b'junction-neutral')
            run('create-junction', ['cmd', '/c', 'mklink', '/J', str(repo / 'allowed/junction'), str(target)], required=0)
        elif ident == 'worktree-link':
            linked = directory / 'linked'
            git(repo, 'worktree', 'add', '-b', 'agent/linked', str(linked))
            repo = linked
            lock['branch'] = 'agent/linked'
        elif ident == 'alternate-objects':
            (repo / '.git/objects/info/alternates').write_text(str(repo / '.git/objects') + '\n', encoding='utf-8')
        elif ident == 'frozen-tip':
            frozen = lock['base_sha']
        elif ident == 'build-protected':
            lock['build_output_dirs'] = ['oracle']
        elif ident == 'build-tracked':
            lock['build_output_dirs'] = ['allowed']
        elif ident == 'build-wildcard':
            lock['build_output_dirs'] = ['build/**']
        elif ident == 'outside-build':
            (repo / 'unexpected').mkdir()
            (repo / 'unexpected/output.bin').write_bytes(b'outside-neutral')
        elif ident == 'raw-byte-dirty':
            (repo / 'tests/expected.txt').write_bytes(b'expected-neutral\r\n')
        elif ident == 'self-signed':
            lock['approval_refs'] = ['self-assigned-neutral-approval']
            lock['approval_authenticated_by_guard'] = True
        elif ident == 'read-acl':
            run('neutral-outside-read-demonstration', [sys.executable, '-c',
                'from pathlib import Path; p=Path("oracle/value.txt"); print(p.read_bytes().hex())'], cwd=repo, required=0)
        elif ident == 'hardlink':
            outside = directory / 'outside-neutral.txt'
            outside.write_bytes((repo / 'allowed/item.txt').read_bytes())
            (repo / 'allowed/item.txt').unlink()
            os.link(outside, repo / 'allowed/item.txt')
            save_json(directory / 'hardlink-observation.json', {'outside': str(outside),
                        'workspace_nlink': (repo / 'allowed/item.txt').stat().st_nlink,
                        'samefile': os.path.samefile(outside, repo / 'allowed/item.txt')})
        frozen = frozen or git(repo, 'rev-parse', 'HEAD').stdout.decode().strip()
        lock_path = directory / 'scope.lock.json'
        save_json(lock_path, lock)
        p, row = run('independent-' + ident, [sys.executable, HERE / 'guard-under-test.py', '--repo', repo,
                    '--lock', lock_path, '--head', frozen, '--json'])
        actual = 'PASS' if p.returncode == 0 else 'DENY'
        data = json.loads(p.stdout)
        results.append({'id': ident, 'required': expected.get(ident, 'OBSERVE'), 'actual': actual,
                        'matches': expected.get(ident, actual) == actual, 'command_id': row['id'],
                        'returncode': p.returncode, 'problems': data.get('problems'),
                        'external_gates': data.get('external_gates'), 'production_write_authorized': data.get('production_write_authorized')})
        save_json(HERE / 'independent-guard-partial-results.json', {'finished_at': now(), 'results': results})
    save_json(HERE / 'independent-guard-results.json', {'finished_at': now(), 'results': results,
             'total_acceptance': len(expected), 'matched': sum(r['matches'] for r in results if r['required'] != 'OBSERVE')})

def official_smoke():
    spec = importlib.util.spec_from_file_location('r01_f02_smoke', HERE / 'smoke-under-test.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.HERE = HERE
    module.ORIGINAL = F02 / 'run-20261010a'
    module.RUN = HERE / 'f02-smoke'
    module.GUARD = HERE / 'guard-under-test.py'
    return module.main()

def revised_m01_gate():
    data = LOCK_SOURCE.read_bytes()
    current = json.loads(data)
    revision = current.get('lock_revision', 'unknown')
    path = HERE / f'M01.scope.lock.rev{revision}.json'
    if path.exists():
        if path.read_bytes() != data:
            raise RuntimeError('Controller changed a sealed lock without changing revision')
    else:
        path.write_bytes(data)
    expectation_path = HERE / f'M01.scope.lock.rev{revision}.expectation.json'
    if expectation_path.exists():
        expectation_path = HERE / f'M01.scope.lock.rev{revision}.expectation-attempt-{COUNTER + 1:04d}.json'
    save_json(expectation_path, {
        'sealed_at': now(), 'lock_sha256': sha(data), 'guard_sha256': FROZEN_GUARD_HASH,
        'expected_decision': 'PASS' if revision > 2 else 'DENY',
        'reason': 'Revision2 still omits Chapter2/Script bin/obj generated by successful host build' if revision == 2
                  else 'Revised exact build output declarations expected to match observed build; no production grant changes',
        'production_write_authorized_expected': False,
    })
    run(f'M01-guard-rev{revision}-source-lock', [sys.executable, HERE / 'guard-under-test.py', '--repo', CLONE,
          '--lock', LOCK_SOURCE, '--head', HEAD, '--json'])
    if LOCK_SOURCE.read_bytes() != data:
        raise RuntimeError('Controller lock changed during gate')

if __name__ == '__main__':
    stage = sys.argv[1] if len(sys.argv) > 1 else 'prepare'
    if stage == 'prepare':
        prepare()
    elif stage == 'metadata-build':
        metadata_build()
    elif stage == 'independent-cases':
        independent_cases()
    elif stage == 'official-smoke':
        sys.argv = [sys.argv[0], 'run-hardened-r01-m01']
        sys.exit(official_smoke())
    elif stage == 'revised-m01-gate':
        revised_m01_gate()
    else:
        raise ValueError(stage)
