"""Generate the host library from DV source; all session decisions remain in DV."""
import argparse
import json
import hashlib
from pathlib import Path
from dv_toolchain import LANGUAGE, checked, compiler
from dv_sources import sources as resolve_sources


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--entry', type=Path, default=LANGUAGE / 'game/entry.dv')
    parser.add_argument('--main', default='false', choices=['false', 'dvg_debug_run'])
    args = parser.parse_args()
    target = args.out.resolve()
    target.mkdir(parents=True, exist_ok=True)
    chunks, ranges, sources, offset = [], [], [], 0
    for path, source in resolve_sources([args.entry]):
        length = len(source.encode('utf-16-le')) // 2
        chunks.append(source)
        sources.append({'path': str(path),
                        'sha256': hashlib.sha256(source.encode('utf-8')).hexdigest()})
        ranges.append([offset, length])
        offset += length
    tail = '(main false)\n' if args.main == 'false' else '(main (dvg_debug_run))\n'
    chunks.append(tail)
    ranges.append([offset, len(tail)])
    bundle = target / 'session.dv'
    bundle.write_text(''.join(chunks), encoding='utf-8', newline='\n')
    boundaries = target / 'ranges.json'
    boundaries.write_text(json.dumps(ranges), encoding='utf-8')
    checked(['dotnet', compiler(), bundle, target / 'DVProgram.cs', '--emit', boundaries], timeout=180)
    (target / 'source-manifest.json').write_text(json.dumps({
        'normalization': 'UTF-8, LF, include rows blanked, appended module newline; independent module ranges',
        'modules': sources,
        'generated_sha256': hashlib.sha256((target / 'DVProgram.cs').read_bytes()).hexdigest()
    }, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
