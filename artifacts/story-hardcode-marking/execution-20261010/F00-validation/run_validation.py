"""F00 evidence capture; runs only the parent-authorized existing entry points."""
from __future__ import annotations
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parent
RESULTS = OUT / "results.json"

def capture(name: str, command: list[str], timeout: int = 600) -> dict:
    started_at = datetime.now(timezone.utc).isoformat()
    started = time.monotonic()
    proc = subprocess.Popen(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    timed_out = False
    try:
        stdout, stderr = proc.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        timed_out = True
        subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"], capture_output=True)
        stdout, stderr = proc.communicate()
    (OUT / f"{name}.stdout.log").write_bytes(stdout)
    (OUT / f"{name}.stderr.log").write_bytes(stderr)
    result = {
        "name": name, "command": command, "started_utc": started_at,
        "elapsed_seconds": round(time.monotonic() - started, 3),
        "exit_code": proc.returncode, "timed_out": timed_out,
        "stdout": f"{name}.stdout.log", "stderr": f"{name}.stderr.log",
    }
    entries = json.loads(RESULTS.read_text(encoding="utf-8")) if RESULTS.exists() else []
    entries.append(result)
    RESULTS.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    return result

if __name__ == "__main__":
    phase = sys.argv[1]
    if phase == "first3":
        capture("status-before", ["git", "status", "--porcelain=v1"])
        capture("tracked-diff-before", ["git", "diff", "--name-status", "HEAD"])
        capture("branch", ["git", "branch", "--show-current"])
        capture("head", ["git", "rev-parse", "HEAD"])
        capture("python-version", ["python", "--version"], 60)
        capture("dotnet-info", ["dotnet", "--info"], 60)
        capture("core-build", ["dotnet", "build", "csharp/Core.sln"])
        capture("host-build", ["dotnet", "build", "godot_csharp/DualOntologyWorldEngine.sln"])
        capture("story-check", ["python", "godot_csharp/tools/compile_story.py", "--check"])
    elif phase == "verify":
        capture("verify", ["python", "tools/verify.py", "--skip", "gallery", "--keep-going"], 3600)
    elif phase == "verify-console":
        godot = sys.argv[2]
        capture("godot-version", [godot, "--version"], 60)
        capture("verify-console", ["python", "tools/verify.py", "--skip", "gallery", "--keep-going", "--godot", godot], 3600)
    elif phase == "final":
        capture("status-after", ["git", "status", "--porcelain=v1"])
        capture("tracked-diff-after", ["git", "diff", "--name-status", "HEAD"])
        capture("head-after", ["git", "rev-parse", "HEAD"])
    elif phase == "final-console":
        capture("status-after-console", ["git", "status", "--porcelain=v1"])
        capture("tracked-diff-after-console", ["git", "diff", "--name-status", "HEAD"])
        capture("head-after-console", ["git", "rev-parse", "HEAD"])
    else:
        raise SystemExit("unknown phase")
