#!/usr/bin/env python3
"""Read filesystem metadata only; assert the two clone metadata stores are separate."""
import hashlib
import json
from pathlib import Path
import stat

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parents[2]
records = {}
identities = {}
for task in ('M01', 'H01a'):
    directory = ROOT / 'tmp' / 'solution-review-workers' / task / '.git'
    root_info = directory.lstat()
    if not stat.S_ISDIR(root_info.st_mode) or root_info.st_file_attributes & 0x400:
        raise RuntimeError('Nonordinary clone metadata root: ' + task)
    rows = []
    for path in sorted(directory.rglob('*')):
        info = path.lstat()
        if stat.S_ISREG(info.st_mode):
            rows.append({'path': path.relative_to(directory).as_posix(), 'device': info.st_dev,
                         'file_id': info.st_ino, 'link_count': info.st_nlink, 'bytes': info.st_size})
            identities.setdefault((info.st_dev, info.st_ino), []).append(task + '/' + rows[-1]['path'])
    records[task] = {'metadata_files': len(rows), 'hardlinked_files': [row for row in rows if row['link_count'] != 1],
                     'metadata_stat_manifest_sha256': hashlib.sha256(json.dumps(rows, sort_keys=True).encode()).hexdigest(),
                     'metadata_stat_manifest': rows}
shared = [value for key, value in identities.items() if key[1] and len(value) != 1]
result = {'decision': 'PASS' if not shared and not any(row['hardlinked_files'] for row in records.values()) else 'DENY',
          'shared_metadata_file_ids': shared, 'clones': records,
          'method': 'Read-only Windows file identifiers and hardlink counts; no file contents opened.'}
(HERE / 'clone-independence-results.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8', newline='\n')
print(json.dumps({'decision': result['decision'], 'shared_metadata_file_ids': len(shared),
                  'metadata_files': {key: row['metadata_files'] for key, row in records.items()},
                  'hardlink_counts': {key: len(row['hardlinked_files']) for key, row in records.items()}}))
if result['decision'] != 'PASS':
    raise SystemExit(1)
