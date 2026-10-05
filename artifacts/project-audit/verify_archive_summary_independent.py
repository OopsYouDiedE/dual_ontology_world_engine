import hashlib, json, time
from pathlib import Path
root = Path(__file__).resolve().parent
started = time.time()
summary = json.loads((root / 'archive-summary.json').read_text(encoding='utf-8-sig'))
report = json.loads((root / 'verification.json').read_text(encoding='utf-8-sig'))
archive = root / 'dual_ontology_world_engine-full.zip'
digest = hashlib.sha256()
with archive.open('rb') as stream:
    while True:
        chunk = stream.read(1024 * 1024)
        if not chunk:
            break
        digest.update(chunk)
checks = {
    'file_count': report['zip_files'],
    'directory_count': report['zip_directories'],
    'source_bytes': report['uncompressed_bytes_read'],
    'archive_bytes': archive.stat().st_size,
    'archive_sha256': digest.hexdigest()
}
for key, actual in checks.items():
    if summary[key] != actual:
        report['errors'].append({'type': 'summary_mismatch', 'key': key, 'expected': summary[key], 'actual': actual})
report['summary_checks'] = checks
report['summary_hash_check_seconds'] = round(time.time() - started, 3)
report['elapsed_seconds'] = round(report['elapsed_seconds'] + report['summary_hash_check_seconds'], 3)
report['passed'] = not report['errors']
(root / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
