"""Emit independent DV modules using cached source and imported interfaces."""
import argparse
import hashlib
import json
from pathlib import Path

from dv_toolchain import LANGUAGE, MODULES, build_lock, checked, compiler, kernel_interface, source_graph


def publish(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    data = text.encode('utf-8')
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False).encode('utf-8')).hexdigest()


def module_header():
    return 'using DuelView.Compiled;\nusing static DuelView.Core.DVKernel;\npublic static partial class DVProgram {\n'


def emit_module(path, source, dependencies, header, executable, compiler_id, builder_id):
    imports = kernel_interface()
    for dependency in dependencies:
        imports[0].extend(dependency['interface'][0])
        imports[1].extend(dependency['interface'][1])
    key = digest({
        'path': str(path), 'source': source, 'imports': imports,
        'header': header, 'compiler': compiler_id, 'builder': builder_id,
    })
    cache = LANGUAGE / 'obj' / 'compiled-modules'
    cache.mkdir(parents=True, exist_ok=True)
    work = cache / key
    output = work / 'module.cs'
    interface_path = work / 'interface.json'
    with build_lock(cache / (key + '.lock')):
        if not output.exists():
            work.mkdir(parents=True, exist_ok=True)
            source_path = work / 'source.dv'
            publish(source_path, source)
            ast_key = digest({'source': source, 'compiler': compiler_id})
            ast_path = LANGUAGE / 'obj' / 'parsed-modules' / (ast_key + '.json')
            config = work / 'module-config.json'
            publish(config, json.dumps([
                imports[0], imports[1], str(interface_path), header, str(ast_path),
            ], ensure_ascii=False))
            pending = work / 'emitted.cs'
            checked(['dotnet', executable, source_path, pending, '--module', config], timeout=180)
            pending.replace(output)
        interface = json.loads(interface_path.read_text(encoding='utf-8'))
        return {'interface': interface, 'source': output.read_text(encoding='utf-8'), 'cache_key': key}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--entry', type=Path, default=MODULES / '60-game.dv')
    args = parser.parse_args()
    target = args.out.resolve()
    if target.suffix:
        target = target.parent
    target.mkdir(parents=True, exist_ok=True)
    modules, edges = source_graph([args.entry])
    executable = compiler()
    compiler_id = hashlib.sha256(executable.read_bytes()).hexdigest()
    builder_id = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    signatures = [{'path': str(path), 'sha256': hashlib.sha256(source.encode('utf-8')).hexdigest()}
                  for path, source in modules]
    fingerprint = digest({
        'modules': signatures, 'compiler': compiler_id, 'builder': builder_id,
        'dependencies': {str(path): [str(dependency) for dependency in edges[path]] for path, _ in modules},
        'native_api': kernel_interface(),
    })
    manifest_path = target / 'source-manifest.json'
    previous = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else {}
    if previous.get('fingerprint') == fingerprint and all(
            (target / name).is_file() for name in previous['generated_files']):
        return

    compiled, closures, generated, emitted = {}, {}, {}, []
    for path, source in modules:
        closure = set()
        for dependency in edges[path]:
            closure.add(dependency)
            closure.update(closures[dependency])
        closures[path] = closure
        dependencies = [compiled[dependency] for dependency, _ in modules if dependency in closure]
        header = module_header()
        unit = emit_module(path, source, dependencies, header, executable, compiler_id, builder_id)
        compiled[path] = unit
        emitted.append({'path': str(path), 'cache_key': unit['cache_key'], 'interface_sha256': digest(unit['interface'])})
        name = 'modules/' + path.stem + '.' + digest(str(path))[:12] + '.g.cs'
        generated[name] = unit['source']

    generated['DVProgram.cs'] = module_header() + '}\n'
    for name, source in generated.items():
        publish(target / name, source)
    for name in set(previous.get('generated_files', [])) - set(generated):
        (target / name).unlink(missing_ok=True)
    # Retire outputs from the former combined-source emitter.
    for name in ('session.dv', 'ranges.json', 'emitted.cs'):
        (target / name).unlink(missing_ok=True)
    (target / 'DvProfiler.cs').unlink(missing_ok=True)
    publish(manifest_path, json.dumps({
        'fingerprint': fingerprint,
        'normalization': 'UTF-8/LF; source-local aliases and offsets; dependency-first module interfaces',
        'modules': signatures,
        'compiler': compiler_id,
        'emission': emitted,
        'generated_files': sorted(generated),
        'generated_sha256': digest(generated),
        'partition': 'legacy-content-native-csharp-kernel',
    }, indent=2) + '\n')


if __name__ == '__main__':
    main()
