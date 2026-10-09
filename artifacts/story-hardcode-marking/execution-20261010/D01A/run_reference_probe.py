import json
from pathlib import Path
import subprocess
base = Path(__file__).resolve().parent
root = base.parents[3]
commands = []
for name, args in [
    ("reference-build", ["dotnet", "build", str(base / "reference-probe/ReferenceProbe.csproj"), "--nologo", "-v:minimal"]),
    ("reference-execution", ["dotnet", str(base / "reference-probe/bin/Debug/net8.0/ReferenceProbe.dll")]),
]:
    proc = subprocess.run(args, cwd=root, capture_output=True)
    (base / "logs" / (name + ".stdout.log")).write_bytes(proc.stdout)
    (base / "logs" / (name + ".stderr.log")).write_bytes(proc.stderr)
    commands.append({"name": name, "argv": args, "exit_code": proc.returncode})
    if proc.returncode != 0:
        break
    if name == "reference-execution":
        json.loads(proc.stdout)
        (base / "reference-results.json").write_bytes(proc.stdout)
(base / "reference-evidence.json").write_text(json.dumps({"commands": commands}, indent=2) + "\n", encoding="utf-8")
print(json.dumps(commands))
