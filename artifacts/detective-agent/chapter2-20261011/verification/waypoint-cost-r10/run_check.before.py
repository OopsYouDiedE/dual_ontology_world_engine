"""Capture each independent verification command without transcoding its output."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[6]
OUT = Path(__file__).resolve().parent
SOURCES = [
    "godot_csharp/scenes/Host/PilotNavigation.gd",
    "godot_csharp/scenes/Shared/FieldNavigation.gd",
    "godot_csharp/scenes/Shared/Story/PlayerController.cs",
    "godot_csharp/tools/check/check_virtual_navigation.gd",
    "godot_csharp/scenes/Shared/check/PlayerControllerCheck/RouteLearningCases.cs",
    "godot_csharp/scenes/Shared/check/PlayerControllerCheck/Program.cs",
    "godot_csharp/scenes/Shared/check/PlayerControllerCheck/PlayerControllerCheck.csproj",
]

def hashes():
    return {source: hashlib.sha256((ROOT / source).read_bytes()).hexdigest() for source in SOURCES}

stage, *command = sys.argv[1:]
if not command:
    raise SystemExit("Expected a stage name and command")
log = OUT / (stage + ".log")
metadata = OUT / (stage + ".json")
if log.exists() or metadata.exists():
    raise SystemExit("An archived run cannot be overwritten: " + stage)
started = datetime.now(timezone.utc).isoformat()
before = hashes()
with log.open("wb") as output:
    result = subprocess.run(command, cwd=ROOT, stdout=output, stderr=subprocess.STDOUT)
after = hashes()
record = {
    "stage": stage, "command": command, "started_utc": started,
    "finished_utc": datetime.now(timezone.utc).isoformat(), "returncode": result.returncode,
    "output_sha256": hashlib.sha256(log.read_bytes()).hexdigest(),
    "source_sha256_before": before, "source_sha256_after": after,
    "sources_unchanged_during_run": before == after,
}
metadata.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"stage": stage, "returncode": result.returncode, "log": str(log),
    "sources_unchanged_during_run": before == after}, ensure_ascii=False))
print(log.read_bytes().decode("utf-8", errors="replace"))
raise SystemExit(result.returncode)
