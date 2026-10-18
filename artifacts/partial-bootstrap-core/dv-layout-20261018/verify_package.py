"""Audit package provenance and idempotent batch generation, without DV execution."""
import hashlib
import json
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = ROOT / 'artifacts/dv_consolidated_package_v2'
BAT = HERE / 'export/export_dv_package.bat'

def hashes():
    return {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(OUT.iterdir()) if p.is_file()}

def main():
    before = hashes()
    subprocess.run([str(BAT)], cwd=ROOT, check=True)
    if hashes() != before:
        raise ValueError('repeat generation changed package bytes')
    subprocess.run([str(BAT), '--check'], cwd=ROOT, check=True)
    manifest = json.loads((OUT / 'manifest.json').read_text(encoding='utf-8'))
    sources = []
    for module in manifest['modules']:
        lines = (OUT / module['output']).read_text(encoding='utf-8').splitlines(keepends=True)
        for source in module['sources']:
            raw = (ROOT / source['path']).read_bytes()
            if hashlib.sha256(raw).hexdigest() != source['sha256']:
                raise ValueError('source hash mismatch: ' + source['path'])
            normalized = raw.decode('utf-8').replace('\r\n', '\n').replace('\r', '\n')
            if normalized and not normalized.endswith('\n'):
                normalized += '\n'
            body = ''.join(lines[source['package_line_start'] - 1:source['package_line_end']])
            if body != normalized:
                raise ValueError('source range mismatch: ' + source['path'])
            sources.append(source['path'])
    if len(sources) != len(set(sources)):
        raise ValueError('source duplicated across packages')
    report = {'scope': 'export bytes, source hashes and manifest ranges; no DV execution', 'repeat_generation_identical': True, 'all_manifest_ranges_reconstruct_sources': True, 'output_sha256': hashes(), 'sources': sources}
    (HERE / 'package-review.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Batch repeat is byte-identical; manifest source hashes and line ranges reconstruct the sources.')

if __name__ == '__main__':
    main()
