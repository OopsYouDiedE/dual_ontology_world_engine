import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[5]
HERE = pathlib.Path(__file__).resolve().parent

command = {
    "controller-build": ["dotnet", "build", "godot_csharp/scenes/Shared/check/PlayerControllerCheck/PlayerControllerCheck.csproj", "-c", "Release", "-m:1", "--disable-build-servers"],
    "controller-run": ["dotnet", "run", "--project", "godot_csharp/scenes/Shared/check/PlayerControllerCheck/PlayerControllerCheck.csproj", "-c", "Release", "--no-build", "--no-restore"],
    "host-build": ["dotnet", "build", "godot_csharp/DualOntologyWorldEngine.sln", "-c", "Debug", "-m:1", "--disable-build-servers"],
}[sys.argv[1]]
name = sys.argv[2]
log = HERE / (name + ".log")
metadata = HERE / (name + ".json")
if log.exists() or metadata.exists():
    raise SystemExit("Refusing to overwrite independent run evidence")
with log.open("xb") as stream:
    result = subprocess.run(command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT)
metadata.write_text(json.dumps({"command": command, "cwd": str(ROOT), "exit_code": result.returncode, "log": log.name}, indent=2), encoding="utf-8")
print(json.dumps({"exit_code": result.returncode, "log": str(log)}))
raise SystemExit(result.returncode)
