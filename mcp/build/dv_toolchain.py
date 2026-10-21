"""Build/cache the source DV compiler. No DV parsing or semantic decisions here."""
from contextlib import contextmanager
import hashlib
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
LANGUAGE = ROOT / "dv_core"
MODULES = LANGUAGE / "modules"
BUILD = ROOT / "mcp/build"
VERIFICATION = ROOT / "mcp/verification"
PROJECT = '''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation>
</PropertyGroup></Project>'''


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


def sources(entries, overrides=None):
    return source_graph(entries, overrides)[0]


@contextmanager
def build_lock(path):
    # Editors may check several documents concurrently; serialize the shared seed build.
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
    inputs = [LANGUAGE / "compiler/10-compiler.dv"] + [p for folder in ("compiler",)
              for p in (LANGUAGE / folder).rglob("*")
              if p.is_file() and not {"bin", "obj"}.intersection(p.parts)]
    digest = hashlib.sha256()
    for path in sorted(inputs):
        digest.update(path.relative_to(ROOT).as_posix().encode())
        digest.update(path.read_bytes())
    cache = LANGUAGE / "obj" / "dv-toolchain"
    cache.mkdir(parents=True, exist_ok=True)
    destination = cache / digest.hexdigest()
    executable = destination / "c3/bin/Release/net8.0/Program.dll"
    with build_lock(cache / "build.lock"):
        if executable.exists():
            return executable
        with tempfile.TemporaryDirectory(dir=cache, prefix="build-") as temporary:
            work = Path(temporary)
            seed = LANGUAGE / "compiler/seed/Seed.csproj"
            checked(["dotnet", "build", seed, "-c", "Release", "--disable-build-servers"], timeout=180)
            previous = seed.parent / "bin/Release/net8.0/Seed.dll"
            for stage in ("c1", "c2", "c3"):
                target = work / stage
                target.mkdir()
                checked(["dotnet", previous, LANGUAGE / "compiler/10-compiler.dv", target / "Program.cs"], timeout=180)
                shutil.copyfile(LANGUAGE / "compiler/Runtime.cs", target / "Runtime.cs")
                (target / "Program.csproj").write_text(PROJECT, encoding="utf-8")
                checked(["dotnet", "build", target / "Program.csproj", "-c", "Release", "--disable-build-servers"], timeout=180)
                previous = target / "bin/Release/net8.0/Program.dll"
            if (work / "c2/Program.cs").read_bytes() != (work / "c3/Program.cs").read_bytes():
                raise RuntimeError("DV compiler C2/C3 source fixed point differs")
            shutil.copytree(work, destination)
    return executable
