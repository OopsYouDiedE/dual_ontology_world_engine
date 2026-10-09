from pathlib import Path
from datetime import datetime, timezone
import subprocess
import sys

root = Path(__file__).resolve().parent
project = root / "NeutralHabitProbe.csproj"
output = root
if (output / "raw-output.jsonl").exists():
    output = root / ("rerun-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ"))
    output.mkdir()
build = root / "build-output"
with (output / "build.log").open("wb") as log:
    result = subprocess.run(
        ["dotnet", "build", str(project), "--artifacts-path", str(build)],
        stdout=log, stderr=subprocess.STDOUT,
    )
print(f"build_exit={result.returncode}")
if result.returncode:
    sys.exit(result.returncode)
binary = build / "bin" / "NeutralHabitProbe" / "debug" / "NeutralHabitProbe.dll"
with (output / "raw-output.jsonl").open("wb") as log:
    result = subprocess.run(["dotnet", str(binary)], stdout=log, stderr=subprocess.STDOUT)
print(f"probe_exit={result.returncode}")
print(f"evidence={output}")
print((output / "raw-output.jsonl").read_text(encoding="utf-8"))
sys.exit(result.returncode)
