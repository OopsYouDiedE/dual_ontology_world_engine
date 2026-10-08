#!/usr/bin/env python3
"""One-command regression for Core and the Godot host.

Runs, in order, and stops at the first failure unless --keep-going:
  1. dotnet build csharp/ScopeRegression.csproj -c Release (includes Core)
  2. dotnet run csharp/ScopeRegression.csproj (Core scope regression)
  3. dotnet build godot_csharp/DualOntologyWorldEngine.sln
  4. Godot headless Awakening/check/flow_check.gd (includes room routes)
  5. Godot headless Chapter2/Game/check_flow.gd (includes map walkability)
  6. Godot windowed state_gallery.gd (screenshots into artifacts/remake-belief-replay/gallery)

Godot steps also fail on script/engine errors and exit-time resource leaks, which the
engine can print without changing the exit code. Full stdout and stderr are retained
as raw log files; console output is not filtered or truncated.

These are automated checks with injected state. They are not playtests and do not
replace the independent blind playtest required by AGENTS.md.

  python tools/verify.py                 # everything
  python tools/verify.py --skip core     # core, scope, host, flow, flow2, gallery
  python tools/verify.py --only flow2    # run a selected subset; host binaries must exist
  python tools/verify.py --godot PATH    # Godot .NET console binary (or env GODOT)
"""
from __future__ import annotations

import argparse
from datetime import datetime
import glob
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
HOST = ROOT / "godot_csharp"
GALLERY = ROOT / "artifacts" / "remake-belief-replay" / "gallery"
STEP_NAMES = ["core", "scope", "host", "flow", "flow2", "gallery"]
LOG_ROOT = ROOT / "artifacts" / "project-audit" / "verify-logs"
ENGINE_ERRORS = re.compile(
    r"SCRIPT ERROR|Parse Error|Failed to load script|\bERROR:|ObjectDB[^\r\n]*leak|resources still in use",
    re.IGNORECASE,
)


def probe_godot(path: str) -> str | None:
    """Returns the version if `path` runs as a Godot .NET command line, else None.

    A PATH hit can be a desktop wrapper (godot.EXE) that refuses CLI use, so
    every candidate is run with --version and must report a mono build.
    """
    try:
        result = subprocess.run([path, "--version"], capture_output=True, timeout=60)
    except (OSError, subprocess.TimeoutExpired):
        return None
    version = decode(result.stdout).strip().splitlines()
    if result.returncode != 0 or not version or ".mono." not in version[-1]:
        return None
    return version[-1]


def find_godot(explicit: str | None) -> tuple[str | None, str]:
    """Returns (path, note). An explicit --godot or GODOT that does not probe is an error, not a fallback."""
    named = explicit or os.environ.get("GODOT")
    if named:
        version = probe_godot(named)
        source = "--godot" if explicit else "GODOT"
        return (named, version) if version else (None, f"{source}={named} is not a runnable Godot .NET console")
    local = os.environ.get("LOCALAPPDATA", "")
    candidates = sorted(glob.glob(os.path.join(local, "Programs", "Godot", "*", "Godot_*_console.exe")), reverse=True)
    candidates += [found for found in (shutil.which(name) for name in ("godot", "godot4")) if found]
    for candidate in candidates:
        version = probe_godot(candidate)
        if version:
            return candidate, version
    tried = ", ".join(candidates) or "none"
    return None, f"no runnable Godot .NET console found (tried: {tried}); pass --godot or set GODOT"


def decode(data: bytes) -> str:
    # Godot and dotnet write UTF-8 when piped, but some Windows setups fall back to the ANSI page.
    for encoding in ("utf-8", "gbk"):
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            continue
    return data.decode("utf-8", errors="replace")


def run(name: str, command: list[str], timeout: int, scan_engine: bool = False,
        log_dir: Path | None = None) -> tuple[bool, str]:
    print(f"\n== {name}: {' '.join(command)}", flush=True)
    started = time.monotonic()
    log_dir = log_dir or LOG_ROOT / (datetime.now().strftime("%Y%m%d-%H%M%S-%f") + f"-{os.getpid()}")
    log_dir.mkdir(parents=True, exist_ok=True)

    def retain_and_show(stdout: bytes, stderr: bytes) -> str:
        # Byte-for-byte channel logs remain the evidence; decoding is only for console display/scanning.
        (log_dir / f"{name}.stdout.log").write_bytes(stdout)
        (log_dir / f"{name}.stderr.log").write_bytes(stderr)
        output = decode(stdout) + decode(stderr)
        print(output, end="" if output.endswith("\n") else "\n", flush=True)
        print(f"Raw logs: {log_dir / (name + '.stdout.log')} | {log_dir / (name + '.stderr.log')}", flush=True)
        return output

    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en", DOTNET_NOLOGO="1")
    try:
        result = subprocess.run(command, cwd=ROOT, capture_output=True, env=env, timeout=timeout)
    except subprocess.TimeoutExpired as error:
        # Preserve all captured output: the last assertion or engine stack trace
        # is the evidence needed to diagnose a hung check.
        retain_and_show(error.stdout or b"", error.stderr or b"")
        return False, f"timed out after {timeout}s"
    except OSError as error:
        retain_and_show(b"", b"")
        return False, str(error)
    output = retain_and_show(result.stdout, result.stderr)
    seconds = time.monotonic() - started
    if result.returncode != 0:
        return False, f"exit {result.returncode} ({seconds:.0f}s)"
    if scan_engine:
        errors = [line for line in output.splitlines() if ENGINE_ERRORS.search(line)]
        if errors:
            return False, f"{len(errors)} engine error(s) ({seconds:.0f}s)"
    summary = next((line for line in reversed(output.splitlines()) if "checks" in line or "routes" in line or "shot " in line or "通过" in line), "")
    return True, f"ok ({seconds:.0f}s) {summary.strip()}"


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--skip", nargs="*", default=[], choices=STEP_NAMES)
    parser.add_argument("--only", nargs="+", choices=STEP_NAMES, help="run only these steps; omitted build steps require existing binaries")
    parser.add_argument("--godot", help="Godot .NET console executable")
    parser.add_argument("--keep-going", action="store_true", help="run every step even after a failure")
    args = parser.parse_args()
    log_dir = LOG_ROOT / (datetime.now().strftime("%Y%m%d-%H%M%S-%f") + f"-{os.getpid()}")

    selected = set(args.only) if args.only else set(STEP_NAMES)
    skipped = set(args.skip) | (set(STEP_NAMES) - selected)
    engine_steps = ["flow", "flow2", "gallery"]
    needs_godot = any(name not in skipped for name in engine_steps)
    godot, godot_note = find_godot(args.godot) if needs_godot else (None, "not needed for selected steps")
    if needs_godot:
        print(f"Godot: {godot} ({godot_note})" if godot else f"Godot: {godot_note}")
    scope_command = ["dotnet", "run", "--project", "csharp/ScopeRegression.csproj", "-c", "Release"]
    if "core" not in skipped:
        scope_command.append("--no-build")
    steps = [
        ("core", ["dotnet", "build", "csharp/ScopeRegression.csproj", "-c", "Release", "-nologo", "-v", "q"], 600, False),
        ("scope", scope_command, 600, False),
        ("host", ["dotnet", "build", "godot_csharp/DualOntologyWorldEngine.sln", "-nologo", "-v", "q"], 600, False),
    ]
    if godot:
        steps.append(("flow", [godot, "--headless", "--path", str(HOST), "--script",
                               "res://scenes/Awakening/check/flow_check.gd"], 600, True))
        steps.append(("flow2", [godot, "--headless", "--path", str(HOST), "--script",
                              "res://scenes/Chapter2/check/check_flow.gd"], 900, True))
        steps.append(("gallery", [godot, "--path", str(HOST), "--rendering-method", "gl_compatibility", "--script",
                                  "res://scenes/Awakening/check/state_gallery.gd"], 600, True))

    results: list[tuple[str, bool, str]] = []
    if not godot:
        # Engine checks that were not skipped on purpose cannot pass silently.
        missing = [name for name in engine_steps if name not in skipped]
        for name in engine_steps:
            results.append((name, name in skipped, "skipped" if name in skipped else godot_note))
        if missing and not args.keep_going:
            steps = []
    for name, command, timeout, scan in steps:
        if name in skipped:
            results.append((name, True, "skipped"))
            continue
        ok, detail = run(name, command, timeout, scan, log_dir)
        results.append((name, ok, detail))
        if not ok and not args.keep_going:
            break

    print("\n== summary")
    for name, ok, detail in results:
        status = "SKIP" if detail == "skipped" else "PASS" if ok else "FAIL"
        print(f"  {status}  {name:<8} {detail}")
    if any(name == "gallery" and ok and detail != "skipped" for name, ok, detail in results):
        print(f"  screenshots: {GALLERY}")
    return 0 if all(ok for _, ok, _ in results) else 1


if __name__ == "__main__":
    sys.exit(main())
