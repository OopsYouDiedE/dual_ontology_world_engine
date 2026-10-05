import collections
import csv
import hashlib
import json
import os
from pathlib import Path
import time
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'artifacts' / 'project-audit'
ARCHIVE = OUT / 'dual_ontology_world_engine-full.zip'


def main():
    rows = []
    dirs = []
    for base, children, files in os.walk(ROOT):
        children.sort()
        if Path(base) == OUT:
            children[:] = []
            continue
        dirs.append(Path(base).relative_to(ROOT).as_posix())
        for name in sorted(files):
            path = Path(base) / name
            stat = path.stat()
            rows.append({'path': path.relative_to(ROOT).as_posix(),
                         'size': stat.st_size, 'mtime_ns': stat.st_mtime_ns})
    total = sum(row['size'] for row in rows)
    print(f'Snapshot: {len(rows)} files, {total} bytes', flush=True)
    changed = []
    started = time.time()
    last = started
    done = 0
    with zipfile.ZipFile(ARCHIVE, 'w', zipfile.ZIP_DEFLATED, compresslevel=1,
                         allowZip64=True) as archive:
        for directory in dirs:
            if directory != '.':
                archive.writestr(directory + '/', b'')
        for index, row in enumerate(rows):
            path = ROOT / row['path']
            digest = hashlib.sha256()
            info = zipfile.ZipInfo.from_file(path, arcname=row['path'])
            info.compress_type = zipfile.ZIP_DEFLATED
            info._compresslevel = 1
            with path.open('rb') as source, archive.open(info, 'w', force_zip64=True) as target:
                for block in iter(lambda: source.read(1024 * 1024), b''):
                    digest.update(block)
                    target.write(block)
            row['sha256'] = digest.hexdigest()
            stat = path.stat()
            if stat.st_size != row['size'] or stat.st_mtime_ns != row['mtime_ns']:
                changed.append(row['path'])
            row['compressed_size'] = archive.getinfo(row['path']).compress_size
            done += row['size']
            if time.time() - last > 15:
                print(f'Packed {index + 1}/{len(rows)} files, {done}/{total} bytes', flush=True)
                last = time.time()
    groups = collections.defaultdict(list)
    for row in rows:
        groups[(row['size'], row['sha256'])].append(row['path'])
    duplicates = [{'size': size, 'sha256': digest, 'paths': paths,
                   'redundant_bytes': size * (len(paths) - 1)}
                  for (size, digest), paths in groups.items() if len(paths) > 1 and size]
    duplicates.sort(key=lambda group: -group['redundant_bytes'])
    with (OUT / 'files.csv').open('w', encoding='utf-8-sig', newline='') as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    (OUT / 'duplicates.json').write_text(json.dumps(duplicates, ensure_ascii=False, indent=2), encoding='utf-8')
    summary = {'file_count': len(rows), 'directory_count': len(dirs) - 1,
               'source_bytes': total, 'archive_bytes': ARCHIVE.stat().st_size,
               'excluded': ['artifacts/project-audit/**'],
               'changed_during_read': changed, 'elapsed_seconds': round(time.time() - started, 1)}
    with zipfile.ZipFile(ARCHIVE) as archive:
        entries = {item.filename: item for item in archive.infolist() if not item.is_dir()}
        summary['index_matches'] = (len(entries) == len(rows) and
                                    all(row['path'] in entries and entries[row['path']].file_size == row['size'] for row in rows))
    summary['archive_sha256'] = hashlib.file_digest(ARCHIVE.open('rb'), 'sha256').hexdigest() if hasattr(hashlib, 'file_digest') else sha_file(ARCHIVE)
    (OUT / 'archive-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(summary, ensure_ascii=False, indent=2), flush=True)


def sha_file(path):
    digest = hashlib.sha256()
    with path.open('rb') as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


if __name__ == '__main__':
    main()
