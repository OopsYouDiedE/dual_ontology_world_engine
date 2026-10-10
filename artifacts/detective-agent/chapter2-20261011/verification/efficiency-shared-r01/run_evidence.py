"""Run one declared verification command, preserving combined output bytes."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[5]
OUT = Path(__file__).resolve().parent
GODOT = r"C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe"
COMMANDS = {
    "host-build": ["dotnet", "build", "godot_csharp/DualOntologyWorldEngine.sln", "-m:1", "--disable-build-servers"],
    "controller-build": ["dotnet", "build", "godot_csharp/scenes/Shared/check/PlayerControllerCheck/PlayerControllerCheck.csproj", "-c", "Release", "-m:1", "--disable-build-servers"],
    "controller-run": ["dotnet", "run", "--project", "godot_csharp/scenes/Shared/check/PlayerControllerCheck/PlayerControllerCheck.csproj", "-c", "Release", "--no-build", "--no-restore"],
    "controller-python": ["python", "godot_csharp/tools/check/check_player_controller.py"],
    "integration-build": ["dotnet", "build", "godot_csharp/scenes/Shared/check/StrategyIntegrationCheck/StrategyIntegrationCheck.csproj", "-c", "Release", "-m:1", "--disable-build-servers"],
    "integration-run": ["dotnet", "run", "--project", "godot_csharp/scenes/Shared/check/StrategyIntegrationCheck/StrategyIntegrationCheck.csproj", "-c", "Release", "--no-build", "--no-restore"],
    "story-check": ["python", "godot_csharp/tools/compile_story.py", "--check"],
    "godot-navigation": [GODOT, "--headless", "--path", "godot_csharp", "--script", "res://tools/check/check_virtual_navigation.gd"],
    "godot-campus-access": [GODOT, "--headless", "--path", "godot_csharp", "--script", "res://tools/check/check_campus_access.gd"],
}

name = sys.argv[1]
suffix = sys.argv[2] if len(sys.argv) > 2 else ""
command = COMMANDS[name]
stem = name + suffix
log = OUT / (stem + ".log")
metadata = OUT / (stem + ".json")
if log.exists() or metadata.exists():
    raise SystemExit("Evidence already exists: " + stem)
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
timer = time.monotonic()
with log.open("wb") as stream:
    process = subprocess.run(command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT)
record = {
    "command": command,
    "cwd": str(ROOT),
    "started_utc": started,
    "elapsed_seconds": round(time.monotonic() - timer, 3),
    "exit_code": process.returncode,
    "log": log.name,
    "sha256": hashlib.sha256(log.read_bytes()).hexdigest(),
}
metadata.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(record, ensure_ascii=False))
print(log.read_text(encoding="utf-8", errors="replace")[-18000:])
raise SystemExit(process.returncode)
