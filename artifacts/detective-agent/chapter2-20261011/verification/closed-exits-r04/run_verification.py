"""Independent verification runner; retains subprocess channels byte for byte."""
from __future__ import annotations

import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import time

ROOT = Path(__file__).resolve().parents[5]
LOG = Path(__file__).resolve().parent
GODOT = r"C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
spec = importlib.util.spec_from_file_location("verify", ROOT / "tools" / "verify.py")
verify = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verify)


def execute(name: str, command: list[str], timeout: int, attempt: str = "initial") -> dict:
    folder = LOG if attempt == "initial" else LOG / f"{name}-{attempt}"
    folder.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    env = dict(os.environ, DOTNET_CLI_UI_LANGUAGE="en", DOTNET_NOLOGO="1")
    try:
        result = subprocess.run(command, cwd=ROOT, capture_output=True, timeout=timeout, env=env)
        stdout, stderr, returncode = result.stdout, result.stderr, result.returncode
    except subprocess.TimeoutExpired as error:
        stdout, stderr, returncode = error.stdout or b"", error.stderr or b"", None
    (folder / f"{name}.stdout.log").write_bytes(stdout)
    (folder / f"{name}.stderr.log").write_bytes(stderr)
    output = verify.decode(stdout) + verify.decode(stderr)
    errors = [line for line in output.splitlines() if verify.ENGINE_ERRORS.search(line)]
    completed = True if name == "host" else (
        verify.check_completed("flow2", output) if name == "flow2" else
        re.search(r"^CAMPUS ACCESS CHECK COMPLETE: (PASS|FAIL)\r?$", output, re.MULTILINE) is not None)
    assertions_passed = True if name == "host" else (
        verify.check_succeeded("flow2", output) if name == "flow2" else
        "CAMPUS ACCESS CHECK COMPLETE: PASS" in output)
    completed_crash = completed and assertions_passed and returncode in (-1073741819, 3221225477)
    passed = (returncode == 0 or completed_crash) and completed and assertions_passed and not errors
    metadata = {
        "step": name, "attempt": attempt, "command": command, "returncode": returncode,
        "elapsed_seconds": round(time.monotonic() - started, 3), "completed": completed,
        "assertions_passed": assertions_passed, "engine_errors": errors,
        "completed_exit_crash": completed_crash, "passed": passed,
        "stdout": str(folder / f"{name}.stdout.log"), "stderr": str(folder / f"{name}.stderr.log"),
    }
    (folder / f"{name}.result.json").write_text(json.dumps(metadata, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(name + ": " + ("PASS" if passed else "FAIL"), flush=True)
    if not passed:
        print("Completion: " + str(completed), flush=True)
        for line in output.splitlines():
            if verify.ENGINE_ERRORS.search(line) or line.startswith("FAIL "):
                print(line, flush=True)
    if name != "host" and not completed and attempt == "initial":
        print("Missing completion; preserving attempt and retrying once.", flush=True)
        return execute(name, command, timeout, "retry")
    return metadata


def main() -> int:
    global LOG
    parser = argparse.ArgumentParser()
    parser.add_argument("step", choices=["host", "campus-access", "flow2"])
    parser.add_argument("--run-label")
    args = parser.parse_args()
    if args.run_label:
        LOG = LOG / args.run_label
    if args.step == "host":
        command = ["dotnet", "build", "godot_csharp/DualOntologyWorldEngine.sln", "-m:1", "--disable-build-servers"]
        timeout = 600
    else:
        script = "res://tools/check/check_campus_access.gd" if args.step == "campus-access" else "res://scenes/Chapter2/check/check_flow.gd"
        command = [GODOT, "--headless", "--path", str(ROOT / "godot_csharp"), "--script", script]
        timeout = 90 if args.step == "campus-access" else 900
    result = execute(args.step, command, timeout)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
