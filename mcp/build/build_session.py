"""Build DV modules with cached parsing and a separately compiled mechanism layer."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from dv_toolchain import LANGUAGE, MODULES, VERIFICATION, checked, compiler, sources as resolve_sources


def publish(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    data = text.encode('utf-8')
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--entry', type=Path, default=MODULES / '60-game.dv')
    parser.add_argument('--main', default='false', choices=['false', 'dvg_debug_run'])
    parser.add_argument('--profile', action='store_true')
    parser.add_argument('--partition', action='store_true', help='Emit host content without the shared 20/30 mechanism layer')
    parser.add_argument('--base-only', action='store_true', help='Emit 20/30 as the independent DuelView.Core library')
    args = parser.parse_args()
    target = args.out.resolve()
    if target.suffix:
        target = target.parent
    target.mkdir(parents=True, exist_ok=True)
    entry = MODULES / '30-session.dv' if args.base_only else args.entry
    modules = resolve_sources([entry])
    executable = compiler()
    compiler_id = executable.parents[4].name
    signatures = [{'path': str(path), 'sha256': hashlib.sha256(source.encode()).hexdigest()}
                  for path, source in modules]
    fingerprint = hashlib.sha256(json.dumps({
        'modules': signatures, 'compiler': compiler_id,
        'builder': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'main': args.main, 'profile': args.profile,
        'partition': args.partition, 'base': args.base_only,
    }, sort_keys=True).encode()).hexdigest()
    output_name = 'DVKernel.cs' if args.base_only else 'DVProgram.cs'
    manifest_path = target / 'source-manifest.json'
    if manifest_path.exists() and (target / output_name).exists():
        previous = json.loads(manifest_path.read_text())
        if previous.get('fingerprint') == fingerprint:
            return

    chunks, ranges, offset = [], [], 0
    cache = LANGUAGE / 'obj' / 'parsed-modules'
    cache.mkdir(parents=True, exist_ok=True)
    for path, source in modules:
        length = len(source.encode('utf-16-le')) // 2
        key = hashlib.sha256((compiler_id + str(path) + str(offset) + source).encode()).hexdigest()
        chunks.append(source)
        ranges.append([offset, length, str(cache / (key + '.json'))])
        offset += length
    if args.main != 'false':
        tail = '(main (dvg_debug_run))\n'
        chunks.append(tail)
        ranges.append([offset, len(tail), ''])
    bundle = target / 'session.dv'
    publish(bundle, ''.join(chunks))
    boundaries = target / 'ranges.json'
    publish(boundaries, json.dumps(ranges))
    generated = target / 'emitted.cs'
    checked(['dotnet', executable, bundle, generated, '--emit', boundaries], timeout=180)
    source = generated.read_text(encoding='utf-8')

    if args.partition or args.base_only:
        marks = list(re.finditer(r'^// DV-SOURCE-OFFSET:(\d+)\n', source, re.MULTILINE))
        functions = []
        for index, mark in enumerate(marks):
            start = int(mark[1])
            owner = next(path for (path, _), bounds in zip(modules, ranges)
                         if bounds[0] <= start < bounds[0] + bounds[1])
            is_base = owner.name in {'20-core.dv', '30-session.dv'}
            end = marks[index + 1].start() if index + 1 < len(marks) else source.rfind('}')
            if is_base == args.base_only:
                functions.append(source[mark.start():end])
        header = ('using DuelView.Compiled;\nnamespace DuelView.Core;\npublic static partial class DVKernel {\n'
                  if args.base_only else
                  'using DuelView.Compiled;\nusing static DuelView.Core.DVKernel;\npublic static partial class DVProgram {\n')
        source = header + ''.join(functions) + '}\n'
    if args.profile:
        pattern = re.compile(r'^(public static object F_([A-Za-z0-9_]+)\([^\r\n]*\) \{)$', re.MULTILINE)
        source = pattern.sub(lambda match: match[1] + '\nusing var dvProfileScope = DvProfiler.Enter("' + match[2] + '");', source)
        publish(target / 'DvProfiler.cs', (VERIFICATION / 'DvProfiler.cs').read_text())
    publish(target / output_name, source)
    generated.unlink()
    publish(manifest_path, json.dumps({
        'fingerprint': fingerprint,
        'normalization': 'UTF-8/LF; source-local aliases; dependency-first includes; cached module AST',
        'modules': signatures,
        'compiler': compiler_id,
        'generated_sha256': hashlib.sha256(source.encode()).hexdigest(),
        'profile_instrumented': args.profile,
        'partition': 'mechanism' if args.base_only else 'content' if args.partition else 'complete',
    }, indent=2) + '\n')


if __name__ == '__main__':
    main()
