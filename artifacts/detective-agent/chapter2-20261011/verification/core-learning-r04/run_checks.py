"""Run one verification process and preserve its combined output without decoding it."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[5]
HERE = Path(__file__).resolve().parent
name, *command = sys.argv[1:]
paths = sorted((ROOT / "csharp").glob("*.cs")) + sorted((ROOT / "csharp").glob("*.csproj"))
before = {str(p.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
start = time.monotonic()
completed = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
log = HERE / (name + ".log")
log.write_bytes(completed.stdout)
after = {str(p.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
manifest = {
    "command": command, "cwd": str(ROOT), "exit_code": completed.returncode,
    "elapsed_seconds": round(time.monotonic() - start, 3), "log": log.name,
    "log_sha256": hashlib.sha256(completed.stdout).hexdigest(),
    "source_hashes_before": before, "source_hashes_after": after,
    "source_changed_during_command": before != after,
}
(HERE / (name + ".manifest.json")).write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(completed.stdout.decode("utf-8", errors="replace"))
print("LOG:", str(log), "EXIT:", completed.returncode, "SHA256:", manifest["log_sha256"])
sys.exit(completed.returncode)
