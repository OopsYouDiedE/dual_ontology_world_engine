"""Record native engineering builds; never launches a game or session."""
import hashlib, json, os, subprocess, sys
from pathlib import Path

root = Path(r"C:\Users\iii\Desktop\dual_ontology_world_engine")
out = root / "artifacts/natural-authoring-20261021/native-build-01"
out.mkdir(parents=True, exist_ok=False)
def write(name, value):
    (out / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
inputs = [root / "AGENTS.md", root / "language_core/AGENTS.md"]
inputs += sorted((root / "language_core").glob("*.dv"))
inputs += [root / "language_core/compiler/Runtime.cs", root / "language_core/compiler/seed/Program.cs",
           root / "language_core/tools/dv_toolchain.py", root / "language_core/tools/build_session.py",
           root / "language_core/Session/Session.csproj"]
inputs = [p for p in inputs if p.is_file()]
frozen = {p.relative_to(root).as_posix(): sha(p) for p in inputs}
write("source-manifest.json", frozen)
commands = [
    ["dotnet", "--version"],
    [sys.executable, "language_core/tools/build_session.py", "--out", "tmp/natural-authoring-20261021/after"],
    ["dotnet", "build", "language_core/LanguageCore.sln", "-c", "Release", "--disable-build-servers", "-p:NuGetAudit=false"],
    ["dotnet", "build", "godot_csharp/DualOntologyWorldEngine.sln", "-m:1", "--disable-build-servers", "-p:NuGetAudit=false"],
]
env = os.environ.copy()
env["DOTNET_CLI_UI_LANGUAGE"] = "en"
env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
results = []
for index, command in enumerate(commands, 1):
    current = {p.relative_to(root).as_posix(): sha(p) for p in inputs}
    if current != frozen:
        raise SystemExit("Frozen local source changed during build")
    write(f"{index:02d}.command.json", {"argv": command, "cwd": str(root)})
    try:
        result = subprocess.run(command, cwd=root, env=env, capture_output=True, timeout=240)
        stdout, stderr, code = result.stdout, result.stderr, result.returncode
    except subprocess.TimeoutExpired as error:
        stdout, stderr, code = error.stdout or b"", error.stderr or b"", "timeout"
    (out / f"{index:02d}.stdout.txt").write_bytes(stdout)
    (out / f"{index:02d}.stderr.txt").write_bytes(stderr)
    results.append({"command": index, "returncode": code})
    print(json.dumps(results[-1]), flush=True)
    if code != 0:
        write("summary.json", {"status": "build-failed", "commands": results, "scope": "Native builds only; no game or session"})
        raise SystemExit(1)
before = root / "tmp/natural-authoring-20261021/before/DVProgram.cs"
after = root / "tmp/natural-authoring-20261021/after/DVProgram.cs"
expected_before = "d4b46844e098c3fa490a11e02137fac27169868f2bcb4177acd0ffb809a9065f"
expected_after = "1e8efedc10365d80c6d8b8944114180e1a0d40b96bd3f29b1ccfd812ecc84a32"
hashes = {"before": sha(before), "after": sha(after)}
same = hashes == {"before": expected_before, "after": expected_after}
unchanged = frozen == {p.relative_to(root).as_posix(): sha(p) for p in inputs}
summary = {"status": "built-and-source-verified" if same and unchanged else "source-mismatch",
           "scope": "Windows compiler, Core and Godot host builds only; no gameplay or performance measurement",
           "commands": results, "generated_source_hashes": hashes,
           "matches_independent_declaration_comparison": same, "inputs_unchanged": unchanged}
write("summary.json", summary)
print(json.dumps(summary), flush=True)
raise SystemExit(0 if same and unchanged else 1)
