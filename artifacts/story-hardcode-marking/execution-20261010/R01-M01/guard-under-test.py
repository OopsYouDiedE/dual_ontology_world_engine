#!/usr/bin/env python3
"""Detection-only integration scope gate for an independently cloned worker repo.

Run this from a trusted controller, with its immutable lock and frozen full tip:
  python hardened_scope_guard.py --repo CLONE --lock LOCK --head FULL_SHA

It performs no Git mutations. It requires an ordinary standalone .git directory,
raw-byte-clean tracked files, no symlinks/reparse points/submodules, no hidden index
flags, and no uncommitted files except explicitly declared build_output_dirs.
It audits every post-base commit, the index, and all non-.git filesystem files.

PASS certifies these observations only. Read ACLs, lock authenticity, semantic
approval and merge identity are external gates; this process cannot enforce them.
Concurrent adversarial writes after or between observations remain an external
isolation/immutable-snapshot concern. No production-write permission is issued.
"""
import argparse
import fnmatch
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys

EXTERNAL_GATES = {'read_acl': 'NOT_ENFORCED', 'write_acl': 'NOT_ENFORCED',
                  'lock_authenticity': 'NOT_VERIFIED', 'approval_authenticity': 'NOT_VERIFIED',
                  'merge_identity': 'NOT_VERIFIED', 'semantic_review': 'NOT_VERIFIED',
                  'immutable_review_snapshot': 'NOT_ENFORCED'}
REPARSE_ATTRIBUTE = 0x400
NORMAL_MODES = {'100644', '100755'}
RESERVED_NAMES = {'con', 'prn', 'aux', 'nul', *('com' + str(i) for i in range(1, 10)),
                  *('lpt' + str(i) for i in range(1, 10))}


class Denied(Exception):
    pass


def digest(value):
    return hashlib.sha256(value).hexdigest()


def normalized_path(value, pattern=False):
    if not isinstance(value, str) or not value or value.startswith('/') or '\\' in value:
        raise Denied(f'invalid relative path: {value!r}')
    if any(ord(char) < 32 or ord(char) == 127 for char in value):
        raise Denied(f'control character in path: {value!r}')
    pieces = value.rstrip('/').split('/') if pattern else value.split('/')
    if any(piece in {'', '.', '..'} or ':' in piece for piece in pieces):
        raise Denied(f'unsafe relative path segments: {value!r}')
    if not pattern and any(any(char in piece for char in '*?[]') for piece in pieces):
        raise Denied(f'wildcard in exact path: {value!r}')
    if any(piece.endswith((' ', '.')) or piece.split('.')[0].casefold() in RESERVED_NAMES for piece in pieces):
        raise Denied(f'Windows path alias: {value!r}')
    if pieces[0].casefold() == '.git':
        raise Denied(f'Git metadata is never a content grant: {value!r}')
    return '/'.join(pieces)


def protected(path, patterns):
    folded = path.casefold()
    return any(folded == pattern or folded.startswith(pattern.rstrip('/') + '/')
               or fnmatch.fnmatchcase(folded, pattern) for pattern in patterns)


def reparse(path, info=None):
    info = info or path.lstat()
    return stat.S_ISLNK(info.st_mode) or bool(getattr(info, 'st_file_attributes', 0) & REPARSE_ATTRIBUTE)


def read_file(path):
    before = path.lstat()
    if reparse(path, before) or not stat.S_ISREG(before.st_mode):
        raise Denied(f'non-regular or linked file: {path}')
    data = path.read_bytes()
    after = path.lstat()
    if reparse(path, after) or (before.st_size, before.st_mtime_ns, before.st_ino) != (after.st_size, after.st_mtime_ns, after.st_ino):
        raise Denied(f'file changed while read: {path}')
    return data


class Gate:
    def __init__(self, repo, lock, frozen):
        self.repo = repo.absolute()
        self.lock = lock
        self.frozen = frozen
        self.problems = []
        self.observations = []
        self.blobs = {}
        self.grants = {}
        self.build_dirs = []
        self.env = {key: value for key, value in os.environ.items() if not key.upper().startswith('GIT_')}
        self.env.update({'GIT_CONFIG_NOSYSTEM': '1', 'GIT_CONFIG_GLOBAL': os.devnull,
                         'GIT_NO_REPLACE_OBJECTS': '1', 'GIT_OPTIONAL_LOCKS': '0', 'GIT_TERMINAL_PROMPT': '0'})

    def git(self, *args):
        # Never execute fsmonitor, filters, external diffs or textconv programs.
        command = ['git', '--no-optional-locks', '-C', str(self.repo),
                   '-c', 'core.fsmonitor=false', '-c', 'core.bare=false',
                   '-c', 'core.worktree=' + str(self.repo), '-c', 'core.excludesfile=' + os.devnull,
                   '-c', 'core.ignorestat=false', '-c', 'core.hooksPath=' + os.devnull, *args]
        process = subprocess.run(command, env=self.env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
        if process.returncode:
            raise Denied(f'git {" ".join(args)} failed ({process.returncode}): {process.stderr.decode(errors="replace").strip()}')
        return process.stdout

    def validate_lock(self):
        lock = self.lock
        if lock.get('schema') != 'scope-lock.v1' or lock.get('status') != 'SIGNED_BY_CONTROLLER':
            raise Denied('invalid lock schema or status (status is not authenticated here)')
        if not lock.get('task_id') or not lock.get('capability_manifest_version'):
            raise Denied('missing task identity/capability version')
        self.base = lock.get('base_sha')
        if not isinstance(self.base, str) or not re.fullmatch(r'[0-9a-f]{40}|[0-9a-f]{64}', self.base):
            raise Denied('base_sha must be a full lowercase Git object ID')
        if not re.fullmatch(r'[0-9a-f]{40}|[0-9a-f]{64}', self.frozen):
            raise Denied('--head must be a frozen full lowercase Git object ID')
        self.branch = lock.get('branch')
        if not isinstance(self.branch, str) or not self.branch.startswith('agent/'):
            raise Denied('lock must designate an exact agent task branch')
        roots = lock.get('protected_roots')
        if not isinstance(roots, list):
            raise Denied('protected_roots must be a list')
        self.patterns = [normalized_path(path, pattern=True).casefold() for path in roots]
        rows = lock.get('allowed_writes')
        if not isinstance(rows, list):
            raise Denied('allowed_writes must be a list')
        for row in rows:
            if not isinstance(row, dict):
                raise Denied('invalid grant row')
            path = normalized_path(row['path'])
            folded = path.casefold()
            operations = row.get('operations')
            if not isinstance(operations, list) or not operations or not all(isinstance(op, str) for op in operations) or not set(operations) <= {'create', 'modify', 'delete'}:
                raise Denied(f'invalid operations: {path}')
            if folded in self.grants:
                raise Denied(f'duplicate/case-colliding grant: {path}')
            if protected(path, self.patterns):
                raise Denied(f'grant conflicts with protected path: {path}')
            self.grants[folded] = (path, set(operations))
        build_dirs = lock.get('build_output_dirs', [])
        if not isinstance(build_dirs, list):
            raise Denied('build_output_dirs must be a list of exact relative directories')
        for value in build_dirs:
            path = normalized_path(value)
            folded = path.casefold()
            if protected(path, self.patterns) or protected(path + '/__scope_probe__', self.patterns):
                raise Denied(f'build directory conflicts with protected root: {path}')
            if any(folded == old or folded.startswith(old + '/') or old.startswith(folded + '/') for old in self.build_dirs):
                raise Denied(f'duplicate/overlapping build directory: {path}')
            self.build_dirs.append(folded)

    def scan(self, root, metadata=False, directories=None):
        found = {}
        if reparse(root):
            raise Denied(f'root is a symlink/junction/reparse point: {root}')
        stack = [(root, '')]
        folded_paths = {}
        while stack:
            directory, prefix = stack.pop()
            with os.scandir(directory) as entries:
                for entry in entries:
                    path = Path(entry.path)
                    rel = prefix + entry.name
                    info = path.lstat()
                    if reparse(path, info):
                        raise Denied(f'symlink/junction/reparse point forbidden: {path}')
                    if not metadata and prefix == '' and entry.name.casefold() == '.git':
                        if entry.name != '.git' or not stat.S_ISDIR(info.st_mode):
                            raise Denied('standalone ordinary .git directory required')
                        continue
                    if not metadata:
                        normalized_path(rel)
                        folded = rel.casefold()
                        if folded in folded_paths and folded_paths[folded] != rel:
                            raise Denied(f'filesystem case-fold collision: {rel}')
                        folded_paths[folded] = rel
                        if entry.name.casefold() == '.git':
                            raise Denied(f'nested Git metadata forbidden: {rel}')
                    if stat.S_ISDIR(info.st_mode):
                        if directories is not None:
                            directories.append(rel)
                        stack.append((path, rel + '/'))
                    elif stat.S_ISREG(info.st_mode):
                        found[rel] = digest(read_file(path))
                    else:
                        raise Denied(f'unsupported filesystem object: {path}')
        return found

    def metadata(self):
        directory = self.repo / '.git'
        if not directory.is_dir() or reparse(directory):
            raise Denied('standalone clone required; linked/shared worktrees are not accepted')
        # Scan metadata for links before asking Git to dereference its paths.
        snapshot = self.scan(directory, metadata=True)
        for special in ('commondir', 'objects/info/alternates', 'shallow', 'info/grafts'):
            if (directory / special).exists():
                raise Denied(f'shared/alternate/incomplete Git metadata forbidden: {special}')
        for rel in ('info/exclude', 'info/attributes'):
            path = directory / rel
            if path.exists():
                text = read_file(path).decode('utf-8', errors='strict')
                if any(line.strip() and not line.lstrip().startswith('#') for line in text.splitlines()):
                    raise Denied(f'active Git metadata rules forbidden: {rel}; declare exact build_output_dirs in the lock')
        if any(path.startswith('refs/replace/') or path.startswith('refs/rewritten/') for path in snapshot):
            raise Denied('replacement/rewritten refs forbidden')
        config = self.git('config', '--local', '--no-includes', '--null', '--list')
        for row in config.split(b'\0'):
            if not row:
                continue
            name, _, value = row.partition(b'\n')
            name = name.decode('utf-8', errors='strict').casefold()
            value = value.decode('utf-8', errors='strict').casefold()
            if name.startswith(('include.', 'includeif.', 'filter.', 'diff.', 'extensions.', 'fsck.')):
                raise Denied(f'executable/indirect/view-changing local Git config forbidden: {name}')
            if name in {'core.excludesfile', 'core.sparsecheckout', 'core.sparsecheckoutcone', 'core.worktree'}:
                raise Denied(f'view-changing local Git config forbidden: {name}')
            if name in {'core.fsmonitor', 'core.ignorestat', 'core.autocrlf'} and value not in {'false', 'no', '0', ''}:
                raise Denied(f'raw-byte clone requires disabled {name}')
        return snapshot

    def tree(self, revision):
        rows = self.git('ls-tree', '-r', '-z', '--full-tree', revision)
        result = {}
        folded = {}
        for row in rows.split(b'\0'):
            if not row:
                continue
            fields, path = row.split(b'\t', 1)
            mode, kind, oid = fields.decode('ascii').split(' ')
            path = normalized_path(path.decode('utf-8', errors='surrogateescape'))
            if mode not in NORMAL_MODES or kind != 'blob':
                raise Denied(f'Git mode/type forbidden in {revision}: {mode} {kind} {path}')
            if path.casefold() in folded:
                raise Denied(f'Git tree case-fold collision in {revision}: {path}')
            folded[path.casefold()] = path
            result[path] = (mode, oid)
        return result

    def index(self):
        for option in ('-v', '-f'):
            for row in self.git('ls-files', option, '-z').split(b'\0'):
                if not row:
                    continue
                tag = chr(row[0])
                if tag == 'S' or tag.islower():
                    raise Denied(f'hidden index flag ({option} {tag}) forbidden: {row[2:].decode(errors="replace")}')
        found = {}
        folded = {}
        for row in self.git('ls-files', '--stage', '-z').split(b'\0'):
            if not row:
                continue
            fields, path = row.split(b'\t', 1)
            mode, oid, stage = fields.decode('ascii').split(' ')
            path = normalized_path(path.decode('utf-8', errors='surrogateescape'))
            if stage != '0' or mode not in NORMAL_MODES:
                raise Denied(f'conflict or Git mode forbidden in index: {mode} stage={stage} {path}')
            if path.casefold() in folded:
                raise Denied(f'index case-fold collision: {path}')
            folded[path.casefold()] = path
            found[path] = (mode, oid)
        return found

    def blob_hash(self, oid):
        if oid not in self.blobs:
            self.blobs[oid] = digest(self.git('cat-file', 'blob', oid))
        return self.blobs[oid]

    def scope(self, path, operation, layer):
        path = normalized_path(path)
        self.observations.append({'layer': layer, 'path': path, 'operation': operation})
        if protected(path, self.patterns):
            self.problems.append(f'{layer}: protected path changed: {path}')
        grant = self.grants.get(path.casefold())
        if grant is None or operation not in grant[1]:
            self.problems.append(f'{layer}: ungranted {operation}: {path}')

    def differences(self, before, after, layer):
        for path in sorted(before.keys() | after.keys()):
            if path not in before:
                self.scope(path, 'create', layer)
            elif path not in after:
                self.scope(path, 'delete', layer)
            elif before[path] != after[path]:
                self.scope(path, 'modify', layer)

    def is_build(self, path):
        folded = path.casefold()
        return any(folded.startswith(root + '/') for root in self.build_dirs)

    def check_tip(self):
        if self.git('symbolic-ref', '--quiet', '--short', 'HEAD').decode().strip() != self.branch:
            raise Denied('wrong worker branch')
        if self.git('rev-parse', '--verify', 'HEAD^{commit}').decode().strip() != self.frozen:
            raise Denied('worker HEAD differs from frozen review tip')

    def verify(self):
        self.validate_lock()
        for ancestor in (self.repo, *self.repo.parents):
            if reparse(ancestor):
                raise Denied(f'repository path ancestor is a symlink/junction/reparse point: {ancestor}')
        metadata_start = self.metadata()
        if self.git('for-each-ref', '--format=%(refname)', 'refs/replace', 'refs/rewritten').strip():
            raise Denied('packed or loose replacement/rewritten refs forbidden')
        self.git('fsck', '--strict', '--full', '--no-reflogs', '--no-dangling')
        self.check_tip()
        if self.git('rev-parse', '--verify', self.base + '^{commit}').decode().strip() != self.base:
            raise Denied('base is not the exact supplied commit')
        self.git('merge-base', '--is-ancestor', self.base, self.frozen)
        if self.git('rev-list', '--merges', self.base + '..' + self.frozen).strip():
            raise Denied('worker branch may not contain merges')
        base_tree = self.tree(self.base)
        commits = self.git('rev-list', '--reverse', self.base + '..' + self.frozen).decode().splitlines()
        previous = base_tree
        for commit in commits:
            current = self.tree(commit)
            self.differences(previous, current, 'commit:' + commit)
            previous = current
        head_tree = previous
        index_start = self.index()
        self.differences(head_tree, index_start, 'index')
        if head_tree != index_start:
            self.problems.append('integration requires committed index equal to frozen HEAD')
        for path in head_tree.keys() | base_tree.keys() | index_start.keys():
            if any(path.casefold() == root or self.is_build(path) for root in self.build_dirs):
                self.problems.append(f'build_output_dirs cannot exempt tracked content: {path}')
        directories_start = []
        files_start = self.scan(self.repo, directories=directories_start)
        directories_start.sort()
        implicit_directories = set()
        for path in index_start:
            pieces = path.split('/')
            implicit_directories.update('/'.join(pieces[:length]) for length in range(1, len(pieces)))
        for path in directories_start:
            folded = path.casefold()
            declared_build_dir = any(folded == root or folded.startswith(root + '/') or root.startswith(folded + '/') for root in self.build_dirs)
            if path not in implicit_directories and not declared_build_dir:
                self.problems.append(f'undeclared untracked directory: {path}')
        # Bind actual casing to index; any case-only workspace rename is dirty.
        indexed_hashes = {path: self.blob_hash(oid) for path, (_, oid) in index_start.items()}
        build_outputs = []
        ordinary_files = {}
        for path, checksum in files_start.items():
            if path not in indexed_hashes and self.is_build(path) and not protected(path, self.patterns):
                build_outputs.append({'path': path, 'sha256': checksum})
            else:
                ordinary_files[path] = checksum
        self.differences(indexed_hashes, ordinary_files, 'working-tree+untracked+ignored')
        if indexed_hashes != ordinary_files:
            self.problems.append('integration requires raw-byte-clean tracked files and no undeclared untracked/ignored files')
        # Reobserve every surface; this detects observed races, not a hard lock.
        self.check_tip()
        directories_end = []
        files_end = self.scan(self.repo, directories=directories_end)
        if self.index() != index_start or files_end != files_start or sorted(directories_end) != directories_start or self.metadata() != metadata_start:
            raise Denied('worker surfaces changed during review')
        snapshot = digest(json.dumps({'head': self.frozen, 'index': index_start, 'files': files_start,
                                     'directories': directories_start, 'metadata': metadata_start}, sort_keys=True).encode('utf-8'))
        return {'task_id': self.lock['task_id'], 'classification': 'DETECTION_ONLY',
                'decision': 'DENY' if self.problems else 'PASS', 'base': self.base, 'head': self.frozen,
                'commits_audited': len(commits), 'files_observed': len(files_start),
                'directories_observed': directories_start,
                'changes': self.observations, 'build_outputs': build_outputs,
                'problems': self.problems, 'snapshot_sha256': snapshot,
                'external_gates': EXTERNAL_GATES, 'production_write_authorized': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, required=True)
    parser.add_argument('--lock', type=Path, required=True)
    parser.add_argument('--head', required=True)
    parser.add_argument('--json', action='store_true', help='emit structured detection result to stdout')
    args = parser.parse_args()
    try:
        lock = json.loads(read_file(args.lock).decode('utf-8'))
        if not isinstance(lock, dict):
            raise Denied('lock must be a JSON object')
        result = Gate(args.repo, lock, args.head).verify()
        if args.json:
            print(json.dumps(result, ensure_ascii=True, indent=2))
        else:
            print(f'{result["decision"]}: DETECTION_ONLY; task={result["task_id"]}; commits={result["commits_audited"]}; files={result["files_observed"]}')
            print('External gates: ' + json.dumps(EXTERNAL_GATES, sort_keys=True))
            print('Production write authorized: false')
        for problem in result['problems']:
            print('DENY: ' + problem, file=sys.stderr)
        return 1 if result['problems'] else 0
    except (Denied, OSError, UnicodeError, ValueError, KeyError, TypeError) as error:
        result = {'classification': 'DETECTION_ONLY', 'decision': 'DENY', 'problems': [str(error)],
                  'external_gates': EXTERNAL_GATES, 'production_write_authorized': False}
        if args.json:
            print(json.dumps(result, ensure_ascii=True, indent=2))
        print('DENY: ' + str(error), file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
