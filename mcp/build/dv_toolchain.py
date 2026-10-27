"""Build the maintained C# legacy frontend; no bootstrap or self-compilation."""
from contextlib import contextmanager
import hashlib
import os
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
LANGUAGE = ROOT / "dv_core"
MODULES = LANGUAGE / "modules"

def environment():
    env = os.environ.copy()
    env.setdefault("DOTNET_ROLL_FORWARD", "Major")
    env.setdefault("NuGetAudit", "false")
    env["DOTNET_CLI_UI_LANGUAGE"] = "en"
    return env


def run(argv, **kwargs):
    return subprocess.run([str(arg) for arg in argv], cwd=ROOT, env=environment(),
                          capture_output=True, encoding="utf-8", errors="replace", **kwargs)


def checked(argv, **kwargs):
    result = run(argv, **kwargs)
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    return result


def source_graph(entries, overrides=None):
    overrides = overrides or {}
    result, seen, stack = [], set(), []
    dependencies = {}

    def visit(path):
        path = Path(path).resolve()
        if path in stack:
            raise ValueError('DV include cycle: ' + ' -> '.join(map(str, stack + [path])))
        if path in seen:
            return
        stack.append(path)
        dependencies[path] = []
        text = overrides[path] if path in overrides else path.read_text(encoding='utf-8-sig')
        # Only LF is structural. Unicode paragraph/line separators are authored
        # text and must reach the compiler unchanged.
        normalized = text.replace('\r\n', '\n').replace('\r', '\n')
        parts = normalized.split('\n')
        lines = [part + '\n' for part in parts[:-1]] + [parts[-1]]
        for index, line in enumerate(lines):
            # Includes are top-level directives. An indented #include can be
            # literal prose, which the compiler's raw-text scanner owns.
            if not line.startswith('#include'):
                continue
            match = re.fullmatch(r'#include\s+(.+?)\s*', line.strip())
            if not match:
                raise ValueError(f'{path}:{index + 1}: expected #include relative/path.dv')
            relative = Path(match[1])
            if relative.is_absolute() or relative.drive or relative.suffix != '.dv':
                raise ValueError(f'{path}:{index + 1}: include requires a relative .dv path')
            dependency = (path.parent / relative).resolve()
            dependencies[path].append(dependency)
            try:
                visit(dependency)
            except OSError as error:
                raise ValueError(f'{path}:{index + 1}: {error}') from error
            lines[index] = '\n'
        stack.pop()
        seen.add(path)
        result.append((path, ''.join(lines) + '\n'))

    for entry in entries:
        visit(entry)
    return result, dependencies


@contextmanager
def build_lock(path):
    # Editors may check several documents concurrently; serialize the shared C# frontend build.
    with path.open("a+b") as handle:
        if handle.tell() == 0:
            handle.write(b"0")
            handle.flush()
        handle.seek(0)
        if os.name == "nt":
            import msvcrt
            import time
            for _ in range(1200):
                try:
                    msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
                    break
                except OSError:
                    time.sleep(.1)
            else:
                raise TimeoutError("DV compiler build is still in progress")
        else:
            import fcntl
            fcntl.flock(handle, fcntl.LOCK_EX)
        try:
            yield
        finally:
            handle.seek(0)
            if os.name == "nt":
                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(handle, fcntl.LOCK_UN)


def compiler():
    folder = LANGUAGE / 'compiler'
    base = LANGUAGE / 'base'
    inputs = [folder / name for name in ('NativeCompiler.cs', 'DvCompiler.csproj')]
    inputs += sorted((base / 'Runtime').glob('*.cs'))
    inputs += [base / 'Values/Rational.cs', base / 'Evidence/BinaryEvidence.cs']
    identity = hashlib.sha256(b''.join(path.read_bytes() for path in inputs)).hexdigest()
    cache = folder / 'obj'
    cache.mkdir(parents=True, exist_ok=True)
    executable = folder / 'bin/Release/net8.0/DvCompiler.dll'
    fingerprint = cache / 'source-fingerprint.txt'
    with build_lock(cache / 'build.lock'):
        if executable.is_file() and fingerprint.is_file() and fingerprint.read_text() == identity:
            return executable
        checked(['dotnet', 'build', folder / 'DvCompiler.csproj', '-c', 'Release', '--disable-build-servers'], timeout=180)
        fingerprint.write_text(identity)
    return executable


def kernel_interface():
    signatures, definitions = [], []
    seen = set()
    base = LANGUAGE / 'base'
    for path in sorted(base.rglob('*.cs')):
        if any(part in {'bin', 'obj'} for part in path.relative_to(base).parts):
            continue
        source = path.read_text(encoding='utf-8-sig')
        for match in re.finditer(r'public static object F_([A-Za-z0-9_]+)\(([^)]*)\)', source):
            name, parameters = match.groups()
            if name in seen:
                raise ValueError('Duplicate native DV API: ' + name)
            seen.add(name)
            arity = len(parameters.split(',')) if parameters.strip() else 0
            mapping = bool(re.search(r'\[DvMapping\]\s*$', source[:match.start()]))
            signatures.append([name, arity, mapping])
            definitions.append([name, [['Value'] * arity, 'Value']])
    return [signatures, definitions]
