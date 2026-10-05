#!/usr/bin/env python3
"""One-command regression for Core and the Godot host.

Runs, in order, and stops at the first failure unless --keep-going:
  1. dotnet build csharp/ScopeRegression.csproj -c Release (includes Core)
  2. dotnet run csharp/ScopeRegression.csproj (Core scope regression)
  3. dotnet build godot_csharp/DualOntologyWorldEngine.sln
  4. Godot headless Awakening/check/flow_check.gd (includes room routes)
  5. Godot headless Chapter2/Game/check_flow.gd (includes map walkability)
  6. Godot windowed state_gallery.gd (screenshots of key screens into tmp/state_gallery)

Godot steps also fail on any "SCRIPT ERROR" / "Parse Error" in the log, which the
engine otherwise prints without changing the exit code.

These are automated checks with injected state. They are not playtests and do not
replace the independent blind playtest required by AGENTS.md.

  python tools/verify.py                 # everything
  python tools/verify.py --skip core     # core, scope, host, flow, flow2, gallery
  python tools/verify.py --only flow2    # run a selected subset; host binaries must exist
  # Legacy nav option selects/skips flow2 (the retired navigation script is gone).
  python tools/verify.py --godot PATH    # Godot .NET console binary (or env GODOT)
"""
from __future__ import annotations

import argparse
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
ENGINE_ERRORS = re.compile(r"SCRIPT ERROR|Parse Error|Failed to load script")


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


def run(name: str, command: list[str], timeout: int, scan_engine: bool = False) -> tuple[bool, str]:
    print(f"\n== {name}: {' '.join(command)}", flush=True)
    started = time.monotonic()
    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en", DOTNET_NOLOGO="1")
    try:
        result = subprocess.run(command, cwd=ROOT, capture_output=True, env=env, timeout=timeout)
    except subprocess.TimeoutExpired:
        return False, f"timed out after {timeout}s"
    except OSError as error:
        return False, str(error)
    output = decode(result.stdout) + decode(result.stderr)
    lines = output.strip().splitlines()
    # Show the tail plus any result lines a check printed earlier.
    notes = [line for line in lines[:-12] if re.search(r"checks|verified|FAIL|blocked", line)]
    print("\n".join(notes + lines[-12:]))
    seconds = time.monotonic() - started
    if result.returncode != 0:
        return False, f"exit {result.returncode} ({seconds:.0f}s)"
    if scan_engine:
        errors = [line for line in output.splitlines() if ENGINE_ERRORS.search(line)]
        if errors:
            for line in errors[:10]:
                print("  ", line)
            return False, f"{len(errors)} engine script error(s) ({seconds:.0f}s)"
    summary = next((line for line in reversed(output.splitlines()) if "checks" in line or "routes" in line or "shot " in line or "通过" in line), "")
    return True, f"ok ({seconds:.0f}s) {summary.strip()}"


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--skip", nargs="*", default=[], choices=STEP_NAMES + ["nav"])
    parser.add_argument("--only", nargs="+", choices=STEP_NAMES + ["nav"], help="run only these steps; omitted build steps require existing binaries")
    parser.add_argument("--godot", help="Godot .NET console executable")
    parser.add_argument("--keep-going", action="store_true", help="run every step even after a failure")
    args = parser.parse_args()

    canonical = lambda names: {"flow2" if name == "nav" else name for name in names}
    selected = canonical(args.only) if args.only else set(STEP_NAMES)
    skipped = canonical(args.skip) | (set(STEP_NAMES) - selected)
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
                              "res://scenes/Chapter2/Game/check_flow.gd"], 900, True))
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
        ok, detail = run(name, command, timeout, scan)
        results.append((name, ok, detail))
        if not ok and not args.keep_going:
            break

    print("\n== summary")
    for name, ok, detail in results:
        print(f"  {'PASS' if ok else 'FAIL'}  {name:<8} {detail}")
    if any(name == "gallery" and ok and detail != "skipped" for name, ok, detail in results):
        print(f"  screenshots: {GALLERY}")
    return 0 if all(ok for _, ok, _ in results) else 1


if __name__ == "__main__":
    sys.exit(main())
