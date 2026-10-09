"""Independent neutral completeness probe prompted by M01 promisor metadata."""
import importlib.util
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('r01_capture', HERE / 'independent-review.py')
r = importlib.util.module_from_spec(spec)
spec.loader.exec_module(r)
CASE = HERE / 'promisor-supplement'
CASE.mkdir()
r.save_json(HERE / 'promisor-expectation-before-run.json', {
    'sealed_at': r.now(), 'expected_decision': 'DENY',
    'reason': 'A clone with a provably missing historical blob and promisor metadata is not a full independent clone',
    'classification': 'SUPPLEMENTAL_INDEPENDENT_COMPLETENESS_PROBE',
    'guard_sha256': r.FROZEN_GUARD_HASH,
})
donor, repo = CASE / 'donor', CASE / 'repo'
donor.mkdir()
r.git(donor, 'init', '-b', 'agent/r01-neutral')
for key, value in [('user.name', 'R01 Neutral'), ('user.email', 'r01-neutral@example.invalid'),
                   ('core.autocrlf', 'false'), ('uploadpack.allowFilter', 'true')]:
    r.git(donor, 'config', key, value)
(donor / 'allowed').mkdir()
(donor / 'old').mkdir()
(donor / 'allowed/item.txt').write_bytes(b'neutral-base\n')
(donor / 'old/forgotten.bin').write_bytes(b'neutral-older-history-only\n')
r.commit_all(donor, 'Neutral older history')
old_blob = r.git(donor, 'rev-parse', 'HEAD:old/forgotten.bin').stdout.decode().strip()
r.git(donor, 'rm', 'old/forgotten.bin')
r.git(donor, 'commit', '-m', 'Neutral baseline after old deletion')
base = r.git(donor, 'rev-parse', 'HEAD').stdout.decode().strip()
(donor / 'allowed/item.txt').write_bytes(b'neutral-legal\n')
r.commit_all(donor, 'Neutral legal scope change')
r.run('neutral-filtered-clone', ['git', 'clone', '--no-local', '--filter=blob:none', '--branch',
       'agent/r01-neutral', donor.as_uri(), repo], required=0)
r.git(repo, 'config', 'core.autocrlf', 'false')
r.git(repo, 'remote', 'remove', 'origin')
r.run('remove-partial-extension-for-neutral-model', ['git', '-C', repo, 'config', '--unset', 'extensions.partialclone'])
p, row = r.run('prove-missing-neutral-older-blob', ['git', '-C', repo, 'cat-file', '-e', old_blob])
if p.returncode == 0:
    raise RuntimeError('Probe not valid: old blob was hydrated')
frozen = r.git(repo, 'rev-parse', 'HEAD').stdout.decode().strip()
lock = {'schema': 'scope-lock.v1', 'status': 'SIGNED_BY_CONTROLLER', 'task_id': 'neutral-promisor-missing-older-blob',
        'branch': 'agent/r01-neutral', 'base_sha': base, 'capability_manifest_version': 'r01-neutral/v1',
        'allowed_writes': [{'path': 'allowed/item.txt', 'operations': ['modify']}],
        'protected_roots': ['oracle', 'tests'], 'build_output_dirs': [], 'isolation': 'DETECTION_ONLY'}
r.save_json(CASE / 'scope.lock.json', lock)
p, gate_row = r.run('independent-promisor-completeness', [sys.executable, HERE / 'guard-under-test.py', '--repo', repo,
                       '--lock', CASE / 'scope.lock.json', '--head', frozen, '--json'])
r.save_json(HERE / 'promisor-supplement-result.json', {
    'guard_sha256': r.FROZEN_GUARD_HASH, 'expected_decision': 'DENY',
    'actual_decision': 'PASS' if p.returncode == 0 else 'DENY', 'returncode': p.returncode,
    'missing_blob': old_blob, 'missing_blob_proof_command': row['id'], 'gate_command': gate_row['id'],
    'promisor_files': [path.name for path in (repo / '.git/objects/pack').glob('*.promisor')],
    'result': json.loads(p.stdout),
})
