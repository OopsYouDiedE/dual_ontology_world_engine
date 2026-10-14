"""Generate the host library from DV source; all session decisions remain in DV."""
import argparse
import json
import hashlib
from pathlib import Path
from dv_toolchain import LANGUAGE, checked, compiler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    target = args.out.resolve()
    target.mkdir(parents=True, exist_ok=True)
    paths = sorted((LANGUAGE / 'core').glob('*.dv')) + sorted((LANGUAGE / 'verification/session').glob('*.dv'))
    manifest = json.loads((LANGUAGE / 'game/modules.json').read_text(encoding='utf-8'))
    paths += [LANGUAGE / name for name in manifest]
    chunks, ranges, sources, offset = [], [], [], 0
    for path in paths:
        source = path.read_text(encoding='utf-8-sig').replace('\r\n', '\n') + '\n'
        length = len(source.encode('utf-16-le')) // 2
        chunks.append(source)
        sources.append({'path': path.relative_to(LANGUAGE).as_posix(),
                        'sha256': hashlib.sha256(source.encode('utf-8')).hexdigest()})
        ranges.append([offset, length])
        offset += length
    tail = '(main false)\n'
    chunks.append(tail)
    ranges.append([offset, len(tail)])
    bundle = target / 'session.dv'
    bundle.write_text(''.join(chunks), encoding='utf-8', newline='\n')
    boundaries = target / 'ranges.json'
    boundaries.write_text(json.dumps(ranges), encoding='utf-8')
    checked(['dotnet', compiler(), bundle, target / 'DVProgram.cs', '--emit', boundaries], timeout=180)
    (target / 'source-manifest.json').write_text(json.dumps({
        'normalization': 'UTF-8, LF, appended module newline',
        'modules': sources,
        'generated_sha256': hashlib.sha256((target / 'DVProgram.cs').read_bytes()).hexdigest()
    }, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
