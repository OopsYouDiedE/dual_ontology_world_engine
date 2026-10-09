"""Run the neutral D04 probe with byte-preserving process logs and local outputs."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import platform
import shutil
import subprocess
import sys

task = Path(__file__).resolve().parent
repo = task.parents[3]
sources = [
    "csharp/Reasoner.cs", "csharp/Model.cs", "csharp/WorldRuntime.cs", "csharp/Cognition.cs",
    "csharp/Ast.cs", "csharp/DecisionSystem.cs", "csharp/DualOntologyWorldEngine.Core.csproj",
    "godot_csharp/scenes/Chapter2/Prototype/Case.cs",
    "godot_csharp/scenes/Chapter2/Prototype/NightSnapshot.cs",
    "godot_csharp/scenes/Awakening/Night/DetectivePlanning.cs",
]
hashes = {path: hashlib.sha256((repo / path).read_bytes()).hexdigest() for path in sources}
head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo).decode().strip()
existing = task / "probe-results.json"
first = task / "probe-results-run-01.json"
if existing.exists() and not first.exists():
    shutil.copyfile(existing, first)
commands = [
    ["dotnet", "build", str(task / "D04Probe.csproj"), "--artifacts-path", str(task / "build")],
    ["dotnet", str(task / "build/bin/D04Probe/debug/D04Probe.dll"), str(task)],
]
results = []
for command, log_name in zip(commands, ["build-02.raw", "probe-run-02.raw"]):
    with (task / log_name).open("wb") as log:
        process = subprocess.run(command, cwd=repo, stdout=log, stderr=subprocess.STDOUT, check=False)
    results.append({"command": command, "log": log_name, "exit_code": process.returncode})
    if process.returncode:
        break
after = {path: hashlib.sha256((repo / path).read_bytes()).hexdigest() for path in sources}
manifest = {"head": head, "python": platform.python_version(), "source_sha256": hashes,
            "source_sha256_after": after, "source_unchanged": hashes == after, "runs": results,
            "raw_log_capture": "Native stdout/stderr redirected to binary file by subprocess; no text decoding."}
(task / "run-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
if existing.exists() and len(results) == 2 and results[-1]["exit_code"] == 0:
    shutil.copyfile(existing, task / "probe-results-run-02.json")
print(json.dumps({"head": head, "source_unchanged": hashes == after, "runs": results}, indent=2))
sys.exit(results[-1]["exit_code"] if hashes == after else 2)
