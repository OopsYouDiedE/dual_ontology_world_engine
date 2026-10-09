"""Archive this review's neutral fixtures byte-for-byte; never follow reparse links."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import zipfile

HERE = Path(__file__).resolve().parent
NAMES = ['fixtures', 'fixtures-run02', 'fixtures-run03']
ARCHIVE = HERE / 'neutral-fixtures.zip'
rows = []
reparse_paths = []
assert HERE.name == 'R01-M01'
assert not ARCHIVE.exists()

def is_reparse(info):
    return stat.S_ISLNK(info.st_mode) or bool(getattr(info, 'st_file_attributes', 0) & 0x400)

with zipfile.ZipFile(ARCHIVE, 'x', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for name in NAMES:
        root = HERE / name
        assert root.parent == HERE and root.resolve().parent == HERE
        stack = [root]
        while stack:
            path = stack.pop()
            info = path.lstat()
            relative = path.relative_to(HERE).as_posix()
            row = {'path': relative, 'mode': info.st_mode, 'nlink': info.st_nlink, 'inode': info.st_ino,
                   'file_attributes': getattr(info, 'st_file_attributes', None), 'mtime_ns': info.st_mtime_ns}
            if is_reparse(info):
                row['type'] = 'reparse-link-not-followed'
                try:
                    row['link_target'] = os.readlink(path)
                except OSError:
                    row['link_target'] = None
                reparse_paths.append(path)
            elif stat.S_ISDIR(info.st_mode):
                row['type'] = 'directory'
                stack.extend(sorted(path.iterdir(), reverse=True))
                archive.writestr(relative.rstrip('/') + '/', b'')
            elif stat.S_ISREG(info.st_mode):
                row['type'] = 'file'
                data = path.read_bytes()
                row['size'] = len(data)
                row['sha256'] = hashlib.sha256(data).hexdigest()
                archive.writestr(relative, data)
            else:
                raise RuntimeError('Unexpected neutral fixture filesystem object: ' + str(path))
            rows.append(row)
    data = (json.dumps({'classification': 'NEUTRAL_FIXTURE_ARCHIVE', 'raw_file_bytes_preserved': True,
                       'links_preserved_as_metadata_without_dereference': True, 'entries': rows}, indent=2) + '\n').encode('utf-8')
    archive.writestr('archive-manifest.json', data)

with zipfile.ZipFile(ARCHIVE) as archive:
    for row in rows:
        if row['type'] == 'file':
            assert hashlib.sha256(archive.read(row['path'])).hexdigest() == row['sha256']

manifest = {'archive': ARCHIVE.name, 'archive_sha256': hashlib.sha256(ARCHIVE.read_bytes()).hexdigest(),
            'file_count': sum(row['type'] == 'file' for row in rows),
            'reparse_links': [row for row in rows if row['type'] == 'reparse-link-not-followed'],
            'review_fixture_directories_removed_after_archive_validation': NAMES}
(HERE / 'neutral-fixtures-archive.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')

# Delete only the three explicit, resolved R01-owned neutral fixture roots.
# Remove link entries themselves before recursively removing ordinary directories.
for path in reparse_paths:
    assert str(path.absolute()).casefold().startswith(str(HERE).casefold() + os.sep)
    if stat.S_ISDIR(path.lstat().st_mode):
        os.rmdir(path)
    else:
        path.unlink()
for name in NAMES:
    root = HERE / name
    assert root.resolve() == HERE / name and root.resolve().parent == HERE
    shutil.rmtree(root)
print(json.dumps(manifest, indent=2))
