#!/usr/bin/env python3
"""Fail-closed patch scope verifier for a single agent's Git worktree.

This verifies changed *paths and operations* against a controller-issued
scope.lock.json. It does NOT enforce operating-system write/read access, verify
human approval authenticity, or audit semantic behavior. Keep this script and
locks outside the agent-writable filesystem and run it in a trusted controller.

Usage:
    python agent-scope-guard.py --repo PATH --lock /protected/scope.lock.json
    python agent-scope-guard.py --repo PATH --lock /protected/scope.lock.json --require-clean --head FULL_SHA

The worktree may contain commits made after the task's base SHA. Ignored files
are not enumerated; isolate build outputs via mounts/container policies.
This script now also verifies exact task branch and rejects merged worker histories.
It still does not enforce permissions or authorize the lock signature/status.
"""
import argparse
import fnmatch
import json
import subprocess
import sys
from pathlib import Path, PurePosixPath


def git(repo, *args):
    p = subprocess.run(['git', '-C', str(repo), *args], stdout=subprocess.PIPE,
                       stderr=subprocess.PIPE, check=False)
    if p.returncode:
        raise RuntimeError(f'git {" ".join(args)} failed: {p.stderr.decode(errors="replace").strip()}')
    return p.stdout


def relative_clean_path(s):
    if not isinstance(s, str) or not s or '\\' in s or s.startswith('/'):
        raise ValueError(f'invalid relative path {s!r}')
    if '\x00' in s or '\n' in s or '\r' in s:
        raise ValueError(f'unsafe path control character: {s!r}')
    segments = s.split('/')
    if any(seg in ('', '.', '..') for seg in segments):
        raise ValueError(f'unsafe relative path segments: {s!r}')
    # Refuse Windows drive aliases as well as UNIX absolute/traversal paths.
    if ':' in segments[0]:
        raise ValueError(f'unsafe drive-qualified path: {s!r}')
    return str(PurePosixPath(s))


def path_symlink_escape(repo, s):
    # Examine each parent to fail closed on any link in the path to be changed.
    part = repo
    for segment in s.split('/'):
        part = part / segment
        if part.is_symlink():
            return True
    return False


def collect_changes(repo, base):
    raw = git(repo, 'diff', '--name-status', '--no-renames', '-z', base, '--')
    fields = raw.split(b'\x00')
    if fields and fields[-1] == b'': fields.pop()
    if len(fields) % 2:
        raise RuntimeError('unexpected git diff --name-status output')
    changes = []
    for i in range(0, len(fields), 2):
        status = fields[i].decode('utf-8', 'surrogateescape')
        path = fields[i + 1].decode('utf-8', 'surrogateescape')
        if status.startswith('A'):
            operation = 'create'
        elif status.startswith('D'):
            operation = 'delete'
        elif status.startswith('M'):
            operation = 'modify'
        else:
            operation = f'UNSUPPORTED_STATUS_{status}'
        changes.append((path, operation))
    untracked = git(repo, 'ls-files', '--others', '--exclude-standard', '-z')
    for f in untracked.split(b'\x00'):
        if f:
            changes.append((f.decode('utf-8', 'surrogateescape'), 'create'))
    return changes


def is_protected(path, patterns):
    # Paths and their prefixes can be frozen. Patterns are controller-managed.
    return any(path == pattern or path.startswith(pattern.rstrip('/') + '/')
               or fnmatch.fnmatchcase(path, pattern) for pattern in patterns)


def verify(repo, lock, require_clean=False, frozen_head=None):
    if lock.get('schema') != 'scope-lock.v1':
        raise ValueError('wrong lock schema')
    if lock.get('status') != 'SIGNED_BY_CONTROLLER':
        raise ValueError('unsigned or inactive controller scope lock')
    if not lock.get('task_id') or not lock.get('capability_manifest_version'):
        raise ValueError('task identity and capability version are required')
    base = lock.get('base_sha')
    if not isinstance(base, str) or len(base) not in (40, 64):
        raise ValueError('base_sha must be a full Git object ID')
    base_resolved = git(repo, 'rev-parse', '--verify', base + '^{commit}').decode().strip()
    if base_resolved != base:
        raise ValueError('base SHA not the exact specified commit')
    expected_branch = lock.get('branch')
    if not isinstance(expected_branch, str) or not expected_branch.startswith('agent/'):
        raise ValueError('lock must designate one exact agent task branch')
    actual_branch = git(repo, 'symbolic-ref', '--quiet', '--short', 'HEAD').decode().strip()
    if actual_branch != expected_branch:
        raise ValueError(f'wrong branch {actual_branch!r}; expected {expected_branch!r}')
    actual_head = git(repo, 'rev-parse', 'HEAD').decode().strip()
    if frozen_head is not None and actual_head != frozen_head:
        raise ValueError('branch tip moved after review was frozen')
    merges = git(repo, 'rev-list', '--merges', f'{base}..HEAD').decode().strip()
    if merges:
        raise ValueError('worker branch may not merge other branches; request controller rebase/integration')
    if require_clean and git(repo, 'status', '--porcelain=v1', '-z'):
        raise ValueError('working tree has uncommitted/untracked changes; commit or discard before integration')
    ancestor = subprocess.run(['git', '-C', str(repo), 'merge-base', '--is-ancestor', base, 'HEAD'])
    if ancestor.returncode:
        raise ValueError('task base is not an ancestor of current HEAD')

    protected = [relative_clean_path(p.rstrip('/')) if not p.endswith('/**') else p
                 for p in lock.get('protected_roots', [])]
    grants = {}
    for row in lock.get('allowed_writes', []):
        path = relative_clean_path(row['path'])
        ops = row.get('operations')
        if not isinstance(ops, list) or not ops or not set(ops).issubset({'create','modify','delete'}):
            raise ValueError(f'illegal operations for {path}')
        if path in grants:
            raise ValueError(f'duplicate path grant {path}')
        if is_protected(path, protected):
            raise ValueError(f'grant conflicts with protected path {path}')
        grants[path] = set(ops)

    changed = collect_changes(repo, base)
    problems = []
    normalized_changes = {}
    folded = {}
    for name, op in changed:
        try:
            path = relative_clean_path(name)
        except ValueError as e:
            problems.append(str(e))
            continue
        if path in normalized_changes and normalized_changes[path] != op:
            problems.append(f'conflicting operations on {path}')
        normalized_changes[path] = op
        if path.casefold() in folded and folded[path.casefold()] != path:
            problems.append(f'case-fold path collision: {folded[path.casefold()]} / {path}')
        folded[path.casefold()] = path
        if path_symlink_escape(repo, path):
            problems.append(f'symlink path not allowed: {path}')
        if is_protected(path, protected):
            problems.append(f'protected path changed: {path}')
        if path not in grants:
            problems.append(f'out of scope: {op} {path}')
        elif op not in grants[path]:
            problems.append(f'operation not granted: {op} {path}')
    print(f'Task: {lock["task_id"]}; base: {base[:12]}; changed paths: {len(normalized_changes)}')
    for p, op in sorted(normalized_changes.items()):
        print(f'  {op:<7} {p}')
    if problems:
        for p in problems: print('DENY:', p, file=sys.stderr)
        return 1
    print('PASS: path/operation scope only. Read access, semantic Core capability changes, '
          'tests, ignored generated files and user approvals require separate gates.')
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, required=True)
    parser.add_argument('--lock', type=Path, required=True)
    parser.add_argument('--require-clean', action='store_true', help='reject uncommitted/untracked changes (integration gate)')
    parser.add_argument('--head', help='full SHA of tip frozen for review')
    args = parser.parse_args()
    try:
        repo = args.repo.resolve(strict=True)
        if not (repo / '.git').exists():
            # git linked worktrees use a .git file, so .exists() handles both.
            raise ValueError(f'not a Git working tree root: {repo}')
        lock = json.loads(args.lock.read_text(encoding='utf-8'))
        return verify(repo, lock, require_clean=args.require_clean, frozen_head=args.head)
    except (ValueError, KeyError, RuntimeError, OSError, json.JSONDecodeError) as ex:
        print(f'DENY: {ex}', file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
