"""Rebuild this run's read-only inventory from Git and the supplied review pack."""
import hashlib
import json
import re
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
AUDIT = OUT.parent

def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])

def sha(data):
    return hashlib.sha256(data).hexdigest()

def save(name, obj):
    (OUT / name).write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

head = git('rev-parse', 'HEAD').decode().strip()
tracked = git('ls-files', '-z').decode().split('\0')
hashes = {p: sha((ROOT / p).read_bytes()) for p in tracked if p and (ROOT / p).is_file()}
dirty_raw = git('status', '--porcelain=v1', '-z')
(OUT / 'initial-status.raw').write_bytes(dirty_raw)
dirty = [s.decode(errors='replace') for s in dirty_raw.split(b'\0') if s]
save('baseline.json', {
    'head': head, 'original_branch': 'claude/blind-main-20261009',
    'evidence_branch': git('branch', '--show-current').decode().strip(),
    'tracked_working_file_hashes': hashes, 'initial_status': dirty,
    'user_scope': '2026-10-10: 未来40日；本轮仅前两章约3–5游戏日，未批准新的小时数。',
    'isolation': 'DETECTION_ONLY: child tools share host identity and filesystem; no enforced read ACL or protected merge identity.'
})
groups = json.loads((AUDIT / 'solution-groups.json').read_text(encoding='utf-8'))
with zipfile.ZipFile(AUDIT / 'solution-review-multiagent-pack.zip') as z:
    pack = OUT / 'provided-pack'
    pack.mkdir(exist_ok=True)
    checksums = {}
    for name in ['subagent-task-manifest.json', 'agent-scope-guard.py', 'scope-lock.example.json',
                 'task-card.example.md', 'README-START.md', 'validation-report.txt']:
        data = z.read(name)
        (pack / name).write_bytes(data)
        checksums[name] = sha(data)
    checksums['solution-review.md'] = sha(z.read('solution-review.md'))
    save('input-hashes.json', {
        'pack': sha((AUDIT / 'solution-review-multiagent-pack.zip').read_bytes()),
        'working_review': sha((AUDIT / 'solution-review.md').read_bytes()),
        'pack_files': checksums,
        'review_matches_pack': z.read('solution-review.md') == (AUDIT / 'solution-review.md').read_bytes()
    })
    manifest = json.loads(z.read('subagent-task-manifest.json'))

members = {}
for g in groups['groups']:
    for key in g['markers'] + g['resources']:
        assert key not in members, ('duplicate membership', key)
        members[key] = g['id']
idx = json.loads((AUDIT / 'index.json').read_text(encoding='utf-8'))['markers']
res = json.loads((AUDIT / 'resource-review.json').read_text(encoding='utf-8'))['indexed_marks']
rows = []
for original in idx + res:
    key, path = original['id'], original['path']
    current = ROOT / path
    lines = current.read_text(encoding='utf-8-sig').splitlines() if current.exists() else []
    old = git('show', 'b608f5b6:' + path).decode('utf-8-sig').splitlines()
    original_line = original['line']
    snippet = old[original_line - 1] if original_line <= len(old) else None
    if key.startswith('RES-'):
        same = original_line <= len(lines) and lines[original_line - 1] == snippet
        locations = [original_line] if same else [i + 1 for i, line in enumerate(lines) if line == snippet]
    else:
        locations = [i + 1 for i, line in enumerate(lines) if re.search(r'STORY-HARDCODE\[' + re.escape(key) + r'\]', line)]
    group = members[key]
    rows.append({
        'id': key, 'group': group, 'kind': original['kind'], 'scope': original['scope'],
        'original_commit': 'b608f5b6', 'original_path': path, 'original_line': original_line,
        'current_head': head, 'current_path': path if current.exists() else None,
        'current_lines': locations, 'location_status': 'EXACT' if len(locations) == 1 else 'AMBIGUOUS' if locations else 'NOT_FOUND',
        'original_snippet': snippet, 'current_snippets': [lines[i - 1] for i in locations],
        'file_sha256': sha(current.read_bytes()) if current.exists() else None,
        'root_cause': group, 'proposed_behavior': next(g['title'] for g in groups['groups'] if g['id'] == group),
        'tasks': manifest['source_groups'][group]['tasks'],
        'resolution': 'BLOCKED', 'resolution_reason': 'Inventory only; no production migration authorized or verified in this run.',
        'code_commit': None, 'verification_counterexample': None, 'reviewer': None,
        'source_evidence': 'artifacts/story-hardcode-marking/solution-evidence.md'
    })
assert len(rows) == 293 and len({r['id'] for r in rows}) == 293
assert set(members) == {r['id'] for r in rows}
save('issue-map.json', {'schema': 'issue-map.v1', 'head': head, 'source_count': len(idx),
                      'resource_count': len(res), 'items': rows})
save('inventory-summary.json', {'head': head, 'source_records': len(idx), 'resource_records': len(res),
     'total': len(rows), 'located_exactly': sum(r['location_status'] == 'EXACT' for r in rows),
     'ambiguous': [r['id'] for r in rows if r['location_status'] == 'AMBIGUOUS'],
     'not_found': [r['id'] for r in rows if r['location_status'] == 'NOT_FOUND']})
print((OUT / 'inventory-summary.json').read_text(encoding='utf-8'))
