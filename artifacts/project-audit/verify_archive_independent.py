import csv, hashlib, json, time, zipfile
from pathlib import Path
root = Path(__file__).resolve().parent
started = time.time()
errors = []
summary = json.loads((root / 'archive-summary.json').read_text(encoding='utf-8-sig'))
with (root / 'files.csv').open('r', encoding='utf-8-sig', newline='') as f:
    reader = csv.DictReader(f)
    headers = reader.fieldnames
    rows = list(reader)
keys = {k.lower().replace('_', '').replace(' ', ''): k for k in headers}
def find_key(options):
    for option in options:
        if option in keys:
            return keys[option]
    raise ValueError('Unknown CSV schema: ' + repr(headers))
path_key = find_key(['path', 'relativepath', 'filepath', 'name'])
size_key = find_key(['size', 'length', 'bytes', 'sizebytes', 'filesize'])
hash_key = find_key(['sha256', 'hash', 'sha256hash'])
expected = {}
for row in rows:
    path = row[path_key].replace('\\', '/')
    if path in expected:
        errors.append({'type': 'duplicate_manifest_path', 'path': path})
    expected[path] = (int(row[size_key]), row[hash_key].lower())
verified = 0
read_bytes = 0
actual_paths = set()
crc_checked = 0
with zipfile.ZipFile(root / 'dual_ontology_world_engine-full.zip', 'r') as archive:
    members = archive.infolist()
    dirs = 0
    for info in members:
        if info.is_dir():
            dirs += 1
            continue
        path = info.filename.replace('\\', '/')
        if path in actual_paths:
            errors.append({'type': 'duplicate_zip_path', 'path': path})
        actual_paths.add(path)
        digest = hashlib.sha256()
        length = 0
        try:
            with archive.open(info, 'r') as stream:
                while True:
                    chunk = stream.read(1024 * 1024)
                    if not chunk:
                        break
                    digest.update(chunk)
                    length += len(chunk)
            crc_checked += 1
            read_bytes += length
        except Exception as e:
            errors.append({'type': 'read_or_crc_error', 'path': path, 'error': str(e)})
            continue
        if length != info.file_size:
            errors.append({'type': 'zip_size_mismatch', 'path': path, 'read_size': length, 'zip_size': info.file_size})
        if path not in expected:
            errors.append({'type': 'extra_zip_file', 'path': path})
            continue
        expected_size, expected_hash = expected[path]
        okay = True
        if length != expected_size:
            errors.append({'type': 'manifest_size_mismatch', 'path': path, 'read_size': length, 'expected_size': expected_size})
            okay = False
        if digest.hexdigest() != expected_hash:
            errors.append({'type': 'sha256_mismatch', 'path': path, 'actual': digest.hexdigest(), 'expected': expected_hash})
            okay = False
        if okay:
            verified += 1
    for path in sorted(set(expected) - actual_paths):
        errors.append({'type': 'missing_zip_file', 'path': path})
report = {
    'passed': not errors,
    'manifest_rows': len(rows),
    'manifest_unique_files': len(expected),
    'zip_files': len(members) - dirs,
    'zip_directories': dirs,
    'verified_files': verified,
    'crc_checked_files': crc_checked,
    'uncompressed_bytes_read': read_bytes,
    'missing_files': sorted(set(expected) - actual_paths),
    'extra_files': sorted(actual_paths - set(expected)),
    'errors': errors,
    'elapsed_seconds': round(time.time() - started, 3),
    'method': 'Every ZIP file streamed to EOF via Python zipfile (built-in CRC validation), SHA256 and size compared independently with files.csv; no sampling.',
    'csv_headers': headers,
    'archive_summary_read': True
}
(root / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
