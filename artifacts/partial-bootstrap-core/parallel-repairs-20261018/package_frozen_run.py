"""Package one finished run and refresh raw evidence hashes; never executes DV."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


TASK = Path(__file__).resolve().parent
ROOT = TASK.parents[2]
EXTERNAL = ['rejection-probes-01', 'public-projection-probes-01',
            'public-projection-probes-02', 'public-projection-probes-03', 'wait-window-probes-01', 'horizon-probes-01']


def digest(path):
    sha = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            sha.update(block)
    return sha.hexdigest()


def entry(path):
    return {'path': path.relative_to(ROOT).as_posix(), 'bytes': path.stat().st_size, 'sha256': digest(path)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run', default='run-240s', help='One direct child of this task directory')
    args = parser.parse_args()
    if Path(args.run).name != args.run or args.run in ('.', '..'):
        parser.error('--run must be a single directory name')
    run = TASK / args.run
    result = run / 'result.json'
    if not result.is_file():
        parser.error('Finished run result.json is required; do not package a running process')
    json.loads(result.read_text(encoding='utf-8-sig'))
    program = run / 'program'
    required = ['session.dv', 'DVProgram.cs', 'Runtime.cs', 'ranges.json', 'source-manifest.json', 'Program.csproj']
    if any(not (program / name).is_file() for name in required):
        parser.error('Frozen generated program inputs are incomplete')
    manifest_path = TASK / 'evidence-manifest.json'
    previous = manifest_path.read_bytes() if manifest_path.exists() else None
    # Refuse to silently rehash changed older evidence as though it were original.
    if previous:
        for sealed in json.loads(previous)['files']:
            path = ROOT / sealed['path']
            if not path.is_file() or digest(path) != sealed['sha256'] or path.stat().st_size != sealed['bytes']:
                raise RuntimeError('Previously sealed evidence differs or is missing: ' + sealed['path'])
    archive_path = TASK / (args.run + '-frozen-program.zip')
    backup = TASK / ('evidence-manifest.pre-' + args.run + '.json')
    if archive_path.exists() or backup.exists():
        raise FileExistsError('New archive/manifest backup name must be unused')
    frozen = sorted(path for path in program.rglob('*') if path.is_file() and 'obj' not in path.relative_to(program).parts)
    frozen += [run / 'source-hashes.json', result]
    partial = run / 'trace.json.partial'
    if partial.is_file():
        frozen.append(partial)  # Preserve incomplete raw bytes in the exclusive archive.
    before = {path: digest(path) for path in frozen}
    # Exclusive create never rewrites any prior archive. Failed packages remain visible.
    with zipfile.ZipFile(archive_path, 'x', zipfile.ZIP_DEFLATED, compresslevel=3) as archive:
        for path in frozen:
            archive.write(path, path.relative_to(run).as_posix())
    if any(digest(path) != sha for path, sha in before.items()):
        raise RuntimeError('Frozen inputs changed during packaging; archive is not valid')
    if previous:
        with backup.open('xb') as stream:
            stream.write(previous)
    roots = [TASK] + [ROOT / 'artifacts' / name for name in EXTERNAL]
    files = []
    for scope in roots:
        for path in scope.rglob('*'):
            if not path.is_file() or path == manifest_path or path == partial:
                continue
            relative = path.relative_to(scope)
            if any(part in ('bin', 'obj', 'program', '__pycache__') for part in relative.parts):
                continue  # Complete run program, including assemblies, is in its new archive.
            if path.suffix in ('.json', '.jsonl', '.txt', '.zip', '.partial', '.cs', '.dv', '.csproj'):
                files.append(entry(path))
    refreshed = {'scope': 'raw evidence, neutral probe sources and frozen program archives; missing trace is not complete evidence',
                 'last_packaged_run': args.run, 'prior_manifest_backup': backup.relative_to(ROOT).as_posix() if previous else None,
                 'files': sorted(files, key=lambda item: item['path'])}
    temporary = manifest_path.with_name('evidence-manifest.' + args.run + '.partial')
    with temporary.open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(refreshed, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    temporary.replace(manifest_path)  # Explicitly requested manifest refresh; originals remain in backup.
    print(json.dumps({'archive': str(archive_path), 'sha256': digest(archive_path),
                      'manifest': str(manifest_path), 'prior_manifest': str(backup) if previous else None}))


if __name__ == '__main__':
    main()
