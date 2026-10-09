"""Neutral detector. Executes existing source; never modifies production files."""
from __future__ import annotations
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import time

base = Path(__file__).resolve().parent
root = base.parents[3]
logs = base / "logs"
logs.mkdir(exist_ok=True)
linked = sorted(p for p in (root / "csharp").glob("*.cs") if p.name not in {"ScopeRegressionProgram.cs", "ReasonerRegressionProgram.cs"})
linked += [root / "godot_csharp/scenes/Shared/Story" / n for n in ["CoreScriptWorld.cs", "WorldSession.cs", "WorldBehavior.cs", "InteractionPacket.cs"]]
reviewed = linked + [root / "csharp/DualOntologyWorldEngine.Core.csproj", root / "godot_csharp/tools/world_script.py", root / "godot_csharp/tools/story_rules.py"]
def hashes():
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in reviewed}
before = hashes()
metadata = {"schema": "D01A-execution-evidence/v1", "mode": "DETECTION_ONLY", "hard_acl": False,
    "baseline": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root).decode().strip(),
    "runtime": {"dotnet": subprocess.check_output(["dotnet", "--version"]).decode().strip(), "python": sys.version},
    "scope": "Neutral mechanics only. No design/story/oracle/alternative D01 read. No production edits.",
    "linked_source_sha256_before": before, "commands": []}
def run(name, args):
    started = time.time()
    proc = subprocess.run(args, cwd=root, capture_output=True, env={**os.environ, "PYTHONDONTWRITEBYTECODE": "1"})
    (logs / (name + ".stdout.log")).write_bytes(proc.stdout)
    (logs / (name + ".stderr.log")).write_bytes(proc.stderr)
    metadata["commands"].append({"name": name, "argv": [str(a) for a in args], "exit_code": proc.returncode,
        "elapsed_seconds": time.time() - started, "stdout": "logs/" + name + ".stdout.log", "stderr": "logs/" + name + ".stderr.log"})
    return proc
build = run("build", ["dotnet", "build", str(base / "experiment/NeutralProbes.csproj"), "--nologo", "-v:minimal"])
if build.returncode != 0:
    metadata["blocked_dependency"] = "Build failed; execution was not run."
else:
    actual = run("core-execution", ["dotnet", str(base / "experiment/bin/Debug/net8.0/NeutralProbes.dll")])
    if actual.returncode == 0:
        json.loads(actual.stdout)
        (base / "core-results.json").write_bytes(actual.stdout)
    else:
        metadata["blocked_dependency"] = "Runtime failed; raw logs preserved; no successful core-results claimed."

# world_script imports stdlib only. We use its actual Compiler with neutral in-memory sources.
sys.dont_write_bytecode = True
module_path = root / "godot_csharp/tools/world_script.py"
spec = importlib.util.spec_from_file_location("d01a_existing_world_script", module_path)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
compiler_results = []
def compile_probe(probe, sources):
    paths = [base / "compiler-inputs" / (probe + "-" + str(i) + ".py") for i in range(len(sources))]
    texts = {p.as_posix(): text for p, text in zip(paths, sources)}
    try:
        document = module.Compiler(paths, texts=texts).parse()
        observed = {"status": "COMPILED", "chapters": document["chapters"]}
    except module.ScriptError as exc:
        observed = {"status": "REJECTED", "type": type(exc).__name__, "message": str(exc), "source": exc.source.json() if exc.source else None}
    compiler_results.append({"id": probe, "neutral_source": sources, "observed": observed})
prefix = 'chapter(id="probe")\nr = room(id="r", name="空间")\n'
compile_probe("C01_duplicate_word", [prefix + 'x = object(id="x", name="件", room=r)\ny = object(id="y", name="件", room=r)\n'])
compile_probe("C02_alias_to_id", [prefix + 'x = object(id="x", name="件", aliases=["器"], room=r)\n@option(id="a", actor="player", target="器")\ndef a():\n    inspect(target="器")\n'])
compile_probe("C03_unicode_normalization", [prefix + 'x = object(id="x", name="é", room=r)\ny = object(id="y", name="e\u0301", room=r)\n'])
compile_probe("C04_bare_id_across_chapters", ['chapter(id="one")\nr = room(id="r", name="空间甲")\nx = object(id="x", name="件甲", room=r)\n',
    'chapter(id="two", requires=["one"])\ns = room(id="s", name="空间乙")\ny = object(id="x", name="件乙", room=s)\n'])
compile_probe("C05_unknown_capability", [prefix + 'x = object(id="x", name="件", room=r, properties={"facet": "neutral"})\n'])
(base / "compiler-results.json").write_text(json.dumps({"schema": "D01A-compiler-observations/v1", "actualCompiler": str(module_path), "results": compiler_results}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
after = hashes()
metadata["linked_source_sha256_after"] = after
metadata["reviewed_source_unchanged"] = before == after
metadata["historical_save"] = "NOT_VERIFIED: no historical save fixture or codec supplied; not simulated."
metadata["levels_1_2_3"] = "NOT_RUN: neutral Core mechanisms do not substitute for game acceptance."
(base / "execution-evidence.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"build": build.returncode, "core_execution": metadata["commands"][-1]["exit_code"], "source_unchanged": before == after,
    "compiler_observations": len(compiler_results), "output": str(base)}, ensure_ascii=False))
