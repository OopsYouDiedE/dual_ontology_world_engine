"""Tool-only continuation; original failed tool compilation log is preserved."""
import hashlib
import json
from pathlib import Path
import subprocess

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
result = subprocess.run(["dotnet", "build", str(OUT / "ReviewHarness.csproj"), "-c", "Release", "--disable-build-servers"], cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
(OUT / "harness-build-r2.stdout.txt").write_bytes(result.stdout)
(OUT / "harness-build-r2.stderr.txt").write_bytes(result.stderr)
print(result.stdout.decode("utf-8", errors="replace"))
print(result.stderr.decode("utf-8", errors="replace"))
if result.returncode:
    raise SystemExit(result.returncode)
result = subprocess.run(["dotnet", str(OUT / "bin/Release/net8.0/ReviewHarness.dll")], cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
(OUT / "harness.stdout.txt").write_bytes(result.stdout)
(OUT / "harness.stderr.txt").write_bytes(result.stderr)
print(result.stdout.decode("utf-8", errors="replace"))
print(result.stderr.decode("utf-8", errors="replace"))
(OUT / "harness-execution.json").write_text(json.dumps({"exit_code": result.returncode,
    "harness_assembly_sha256": hashlib.sha256((OUT / "bin/Release/net8.0/ReviewHarness.dll").read_bytes()).hexdigest(),
    "copied_production_assembly_sha256": hashlib.sha256((OUT / "bin/Release/net8.0/Language.Core.dll").read_bytes()).hexdigest(),
    "scope": "review tool only, no game acceptance"}, indent=2), encoding="utf-8")
