"""Capture a single neutral Godot run without newline conversion or overwrites."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
import time

evidence = Path(__file__).resolve().parent
root = evidence.parents[4]
production = [
    root / "godot_csharp/scenes/Host/AgentPilot.gd",
    root / "godot_csharp/scenes/Shared/check/FullTest/FullTest.gd",
]
run_name = sys.argv[2] if len(sys.argv) > 2 else "run-01"
paths = {name: evidence / f"{run_name}-{name}" for name in ("stdout.log", "stderr.log", "results.json", "metadata.json")}
for path in paths.values():
    if path.exists():
        raise SystemExit(f"Refusing to overwrite {path}")

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

before = {str(path.relative_to(root)).replace("\\", "/"): sha(path) for path in production}
command = [sys.argv[1], "--headless", "--path", str(root / "godot_csharp"),
           "--script", str(evidence / "presentation_supersession.gd"), "--", str(paths["results.json"])]
started = time.time()
timed_out = False
with paths["stdout.log"].open("xb") as stdout, paths["stderr.log"].open("xb") as stderr:
    process = subprocess.Popen(command, cwd=root, stdout=stdout, stderr=stderr)
    try:
        exit_code = process.wait(timeout=30)
    except subprocess.TimeoutExpired:
        timed_out = True
        process.kill()
        exit_code = process.wait()
after = {str(path.relative_to(root)).replace("\\", "/"): sha(path) for path in production}
metadata = {"command": command, "pid": process.pid, "exit_code": exit_code,
            "timed_out": timed_out, "elapsed_seconds": time.time() - started,
            "production_sha256_before": before, "production_sha256_after": after,
            "production_unchanged_during_run": before == after,
            "raw_sha256": {path.name: sha(path) for path in paths.values() if path.exists()},
            "fixture_sha256": sha(evidence / "presentation_supersession.gd")}
with paths["metadata.json"].open("x", encoding="utf-8", newline="\n") as output:
    json.dump(metadata, output, ensure_ascii=False, indent=2)
print(json.dumps(metadata, ensure_ascii=False, indent=2))
raise SystemExit(1 if timed_out else exit_code)
