"""Fingerprint and copy a complete, previously built diagnostic program; never execute it."""
import hashlib
import json
import shutil
from pathlib import Path, PurePosixPath
from matrix_lifecycle import case_guard

ROOT = Path(__file__).resolve().parents[2]
LANGUAGE = ROOT / 'dv_core'


def compiler_inputs():
    # Match dv_toolchain.compiler's complete seed/compiler input set, including
    # its duplicate startup source entry in the compiler cache fingerprint.
    return [LANGUAGE / 'compiler/10-compiler.dv'] + [p for p in (LANGUAGE / 'compiler').rglob('*')
            if p.is_file() and not {'bin', 'obj'}.intersection(p.parts)]


def compiler_fingerprint():
    result = hashlib.sha256()
    for path in sorted(compiler_inputs()):
        result.update(path.relative_to(ROOT).as_posix().encode())
        result.update(path.read_bytes())
    return result.hexdigest()


RUNTIME_CONTRACT = {name: env + '; 1=on, 0=off; same compiled binary' for name, env in
                    (('opt_runtimespeed', 'DV_OPT_RUNTIMESPEED'), ('opt_stateindex', 'DV_OPT_STATEINDEX'))}
SCHEMA = 'dv-compiled-program-v2'
BINARY_ROOT = 'bin/Release/net8.0/'
REQUIRED_BINARIES = {BINARY_ROOT + name for name in
                     ('Program.dll', 'Program.deps.json', 'Program.runtimeconfig.json')}


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def files(program, binaries):
    program = Path(program).resolve()
    result = {}
    for path in program.rglob('*'):
        relative = path.relative_to(program)
        if 'obj' in relative.parts:
            continue
        if path.is_symlink():
            raise ValueError('Reusable program contains a symlink: ' + str(relative))
        if path.is_file() and (relative.as_posix().startswith(BINARY_ROOT) == binaries):
            if not binaries and relative.parts[0] == 'bin':
                raise ValueError('Unexpected program output outside Release/net8.0')
            result[relative.as_posix()] = digest(path)
    return result


def checked_path(program, relative):
    if not isinstance(relative, str) or '\\' in relative:
        raise ValueError('Noncanonical reusable program path')
    parts = PurePosixPath(relative)
    if parts.is_absolute() or any(part in ('', '.', '..') for part in parts.parts):
        raise ValueError('Unsafe reusable program path')
    target = (program / relative).resolve()
    if not target.is_relative_to(program):
        raise ValueError('Reusable program path escapes its directory')
    return target


def verify(program, compiled, hashes, instrumented, normalized, compiler_id, entry_kind='fresh-debug'):
    program = Path(program).resolve()
    if compiled.get('schema') != SCHEMA or compiled.get('source_hashes') != hashes:
        raise ValueError('Reusable program schema/source fingerprints differ')
    runtime_config = compiled.get('runtime_config_at_capture')
    if (compiled.get('runtime_switch_contract') != RUNTIME_CONTRACT or not isinstance(runtime_config, dict) or
            set(runtime_config) != set(RUNTIME_CONTRACT) or
            any(type(runtime_config[key]) is not bool for key in RUNTIME_CONTRACT)):
        raise ValueError('Reusable program runtime-switch metadata differs')
    if compiled.get('entry_kind') != entry_kind:
        raise ValueError('Reusable program entry point differs')
    if type(compiled.get('instrumented')) is not bool or compiled['instrumented'] != instrumented:
        raise ValueError('Reusable program instrumentation differs')
    for field, binary in (('binaries', True), ('program_sources', False)):
        expected = compiled.get(field)
        if not isinstance(expected, dict) or not expected:
            raise ValueError('Reusable program fingerprint set missing: ' + field)
        for relative in expected:
            checked_path(program, relative)
        if binary and not REQUIRED_BINARIES.issubset(expected):
            raise ValueError('Reusable program lacks required assembly fingerprints')
        if files(program, binary) != expected:
            raise ValueError('Reusable program complete file set/hash mismatch: ' + field)
    manifest = json.loads((program / 'source-manifest.json').read_text(encoding='utf-8'))
    if (manifest.get('profile_instrumented') is not instrumented or
            manifest.get('compiler') != compiler_id or
            {entry['path']: entry['sha256'] for entry in manifest['modules']} != normalized):
        raise ValueError('Reusable program generated source/compiler/profile manifest differs')
    return compiled


def capture(program, hashes, instrumented, normalized, compiler_id, entry_kind='fresh-debug', runtime_config=None):
    runtime_config = {key: True for key in RUNTIME_CONTRACT} if runtime_config is None else runtime_config
    value = {'runtime_switch_contract': RUNTIME_CONTRACT, 'runtime_config_at_capture': runtime_config,
             'schema': SCHEMA, 'source_hashes': hashes, 'instrumented': instrumented, 'entry_kind': entry_kind,
             'binaries': files(program, True), 'program_sources': files(program, False)}
    return verify(program, value, hashes, instrumented, normalized, compiler_id, entry_kind)


def _copy_verified(source, target, hashes, instrumented, normalized, compiler_id,
                   entry_kind='fresh-debug', manifest_name='compiled-program.json'):
    source, target = Path(source).resolve(), Path(target).resolve()
    if source == target or source.is_relative_to(target) or target.is_relative_to(source):
        raise ValueError('Reusable source and destination overlap')
    if manifest_name not in ('compiled-program.json', 'compiled-causal-program.json'):
        raise ValueError('Unknown reusable program manifest name')
    compiled = json.loads((source.parent / manifest_name).read_text(encoding='utf-8'))
    verify(source, compiled, hashes, instrumented, normalized, compiler_id, entry_kind)
    for relative in (*compiled['program_sources'], *compiled['binaries']):
        destination = checked_path(target, relative)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(checked_path(source, relative), destination)
    # Verify copied bytes, complete sets and instrumentation again. A source
    # mutation between initial verification and copying cannot be re-certified.
    verify(target, compiled, hashes, instrumented, normalized, compiler_id, entry_kind)
    return compiled


def copy_verified(source, target, hashes, instrumented, normalized, compiler_id,
                  entry_kind='fresh-debug', manifest_name='compiled-program.json'):
    with case_guard(source):
        return _copy_verified(source, target, hashes, instrumented, normalized, compiler_id,
                              entry_kind, manifest_name)
