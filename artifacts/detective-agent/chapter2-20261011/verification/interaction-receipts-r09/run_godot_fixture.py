import json
import os
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = pathlib.Path(__file__).resolve().parents[5]
name = sys.argv[2]
log = HERE / (name + ".log")
trace = HERE / (name + ".jsonl")
metadata = HERE / (name + ".json")
if any(path.exists() for path in (log, trace, metadata)):
    raise SystemExit("Refusing to overwrite independent Godot evidence")
command = [sys.argv[1], "--headless", "--path", str(ROOT / "godot_csharp"), "--script", str(HERE / "interaction_receipts_fixture.gd")]
environment = dict(os.environ, INTERACTION_RECEIPT_EVIDENCE=str(trace))
with log.open("xb") as stream:
    result = subprocess.run(command, cwd=ROOT, env=environment, stdout=stream, stderr=subprocess.STDOUT, timeout=60)
metadata.write_text(json.dumps({"command": command, "exit_code": result.returncode, "log": log.name, "trace": trace.name}, indent=2), encoding="utf-8")
print(json.dumps({"exit_code": result.returncode, "log": str(log), "trace": str(trace)}))
raise SystemExit(result.returncode)
