"""Read-only structural comparison with the pre-layout Git commit; no DV execution."""
import hashlib
import json
import posixpath
import re
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
BASE = '25ee9282'
DECL = re.compile(r'^\(?(?:fn|axiom|defn|mapping|acceptance|imprint)\s+([^\s(]+)')

def old(path):
    return subprocess.check_output(['git', 'show', f'{BASE}:{path}'], cwd=ROOT)

def current(path):
    return (ROOT / path).read_bytes()

def declarations(raw):
    result = {}
    name = None
    for line in raw.decode('utf-8-sig').splitlines():
        if not line.strip() or line.lstrip().startswith('//') or line.startswith('#include '):
            continue
        match = DECL.match(line)
        if match:
            name = match[1]
            if name in result:
                raise ValueError(f'duplicate declaration: {name}')
            result[name] = []
        if name is None:
            raise ValueError(f'unclassified source statement: {line}')
        result[name].append(line.rstrip())
    return result

def merged(paths, read):
    result = {}
    for path in paths:
        for name, body in declarations(read(path)).items():
            if name in result:
                raise ValueError(f'duplicate declaration {name} in {path}')
            result[name] = body
    return result

def closure(entry, read):
    seen = set()
    def visit(path):
        if path in seen:
            return
        seen.add(path)
        for line in read(path).decode('utf-8-sig').splitlines():
            if line.startswith('#include '):
                visit(posixpath.normpath(posixpath.join(posixpath.dirname(path), line[9:].strip())))
    visit(entry)
    return sorted(seen)

def main():
    mapping = json.loads((HERE / 'path-map.json').read_text(encoding='utf-8-sig'))
    report = {'base_commit': BASE, 'scope': 'declaration text and production include closure; no gameplay execution', 'targets': [], 'sources': []}
    for target in sorted(set(mapping.values())):
        sources = [source for source, destination in mapping.items() if destination == target]
        before, after = merged(sources, old), declarations(current(target))
        if before != after:
            raise ValueError(f'{target}: missing={before.keys()-after.keys()}, extra={after.keys()-before.keys()}, changed={[n for n in before.keys() & after.keys() if before[n] != after[n]]}')
        report['targets'].append({'path': target, 'declarations_unchanged': True, 'sha256': hashlib.sha256(current(target)).hexdigest()})
        report['sources'].extend({'path': source, 'sha256': hashlib.sha256(old(source)).hexdigest()} for source in sources)
    entry = 'language_core/game/debug-run.dv'
    before_paths, after_paths = closure(entry, old), closure(entry, current)
    if merged(before_paths, old) != merged(after_paths, current):
        raise ValueError('production declaration closure changed')
    report['production_closure'] = {'before': before_paths, 'after': after_paths, 'declarations_unchanged': True}
    compiler = 'language_core/compiler/compiler.dv'
    # Compiler filename is discovered so the audit also covers a renamed compiler entry.
    compiler_paths = sorted((ROOT / 'language_core/compiler').glob('*.dv'))
    for path in compiler_paths:
        rel = path.relative_to(ROOT).as_posix()
        if old(rel) != current(rel):
            raise ValueError(f'compiler changed: {rel}')
    report['compiler_bytes_unchanged'] = True
    report['source_file_count_before'] = len(mapping) + len(compiler_paths) + 1
    report['source_file_count_after'] = len(set(mapping.values())) + len(compiler_paths) + 1
    (HERE / 'structural-review.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Declaration bodies and production declaration closure unchanged; compiler bytes unchanged.')

if __name__ == '__main__':
    main()
