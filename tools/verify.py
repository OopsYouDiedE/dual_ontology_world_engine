#!/usr/bin/env python3
"""One-command regression for Core and the Godot host.

Runs, in order, and stops at the first failure unless --keep-going:
  1. dotnet build csharp/Core.sln -c Release
  2. dotnet run csharp/ScopeRegression.csproj (Core scope regression)
  3. dotnet build godot_csharp/DualOntologyWorldEngine.sln
  4. Godot headless story_flow_check.gd (jump-to-state staging and dialogue wiring)
  5. Godot headless navigation_check.gd (grid routes swept with the detective's real collider)
  6. Godot windowed state_gallery.gd (screenshots of key screens into tmp/state_gallery)

Godot steps also fail on any "SCRIPT ERROR" / "Parse Error" in the log, which the
engine otherwise prints without changing the exit code.

These are automated checks with injected state. They are not playtests and do not
replace the independent blind playtest required by AGENTS.md.

  python tools/verify.py                 # everything
  python tools/verify.py --skip core     # skip steps by name: core, scope, host, flow, nav, gallery
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
GALLERY = ROOT / "tmp" / "state_gallery"
ENGINE_ERRORS = re.compile(r"SCRIPT ERROR|Parse Error|Failed to load script")


def find_godot(explicit: str | None) -> str | None:
    for candidate in (explicit, os.environ.get("GODOT")):
        if candidate and Path(candidate).exists():
            return candidate
    for name in ("godot", "godot4"):
        found = shutil.which(name)
        if found:
            return found
    local = os.environ.get("LOCALAPPDATA", "")
    matches = sorted(glob.glob(os.path.join(local, "Programs", "Godot", "*", "Godot_*_console.exe")))
    return matches[-1] if matches else None


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
    parser.add_argument("--skip", nargs="*", default=[], choices=["core", "scope", "host", "flow", "nav", "gallery"])
    parser.add_argument("--godot", help="Godot .NET console executable")
    parser.add_argument("--keep-going", action="store_true", help="run every step even after a failure")
    args = parser.parse_args()

    godot = find_godot(args.godot)
    steps = [
        ("core", ["dotnet", "build", "csharp/Core.sln", "-c", "Release", "-nologo", "-v", "q"], 600, False),
        ("scope", ["dotnet", "run", "--project", "csharp/ScopeRegression.csproj", "-c", "Release"], 600, False),
        ("host", ["dotnet", "build", "godot_csharp/DualOntologyWorldEngine.sln", "-nologo", "-v", "q"], 600, False),
    ]
    if godot:
        steps.append(("flow", [godot, "--headless", "--path", str(HOST), "--script",
                               "res://scenes/FirstAwakening/check/story_flow_check.gd"], 600, True))
        steps.append(("nav", [godot, "--headless", "--path", str(HOST), "--script",
                              "res://scenes/FirstAwakening/check/navigation_check.gd"], 900, True))
        steps.append(("gallery", [godot, "--path", str(HOST), "--rendering-method", "gl_compatibility", "--script",
                                  "res://scenes/FirstAwakening/check/state_gallery.gd", "--", f"--out={GALLERY}"], 600, True))
    else:
        print("Godot not found (pass --godot or set GODOT); skipping flow and gallery.")

    results: list[tuple[str, bool, str]] = []
    for name, command, timeout, scan in steps:
        if name in args.skip:
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
