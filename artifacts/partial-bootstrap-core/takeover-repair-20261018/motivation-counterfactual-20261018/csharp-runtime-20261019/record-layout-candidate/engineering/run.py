from pathlib import Path
import argparse, hashlib, json, os, subprocess

HERE = Path(__file__).resolve().parent
CANDIDATE = HERE.parent
TASK = CANDIDATE.parent
ROOT = TASK.parents[4]
parser = argparse.ArgumentParser(description="Neutral isolated stage02/current Runtime + native Session engineering comparison")
parser.add_argument("--out", required=True, type=Path, help="New evidence directory; must not exist")
args = parser.parse_args()
out = args.out.resolve()
out.mkdir(parents=True, exist_ok=False)
(out / ".gitattributes").write_text("*.log -text -diff\n*.jsonl -text -diff\n*.partial -text -diff\n*.json -text\n", encoding="utf-8")
project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>'
sources = {
    "before": (CANDIDATE / "Runtime.baseline.cs.txt", CANDIDATE / "Session.baseline.cs.txt"),
    "after": (CANDIDATE / "Runtime.cs", CANDIDATE / "Session.baseline.cs.txt"),
}
manifest = {"scope": "neutral engineering only; no DV content or game session", "sources": {}}
# Capture both versions and shared harness before any build. No production edits.
for name, (runtime, session) in sources.items():
    directory = out / name
    directory.mkdir(exist_ok=False)
    for filename, source in [("Runtime.cs", runtime), ("Session.cs", session), ("Program.cs", HERE / "Probe.cs"), ("Stub.cs", HERE / "Stub.cs")]:
        data = source.read_bytes()
        (directory / filename).write_bytes(data)
        manifest["sources"][name + "/" + filename] = {"source": str(source), "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)}
    (directory / "Probe.csproj").write_text(project, encoding="utf-8")
(out / "source-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
env = os.environ.copy()
env["DOTNET_ROLL_FORWARD"] = "Major"
status = {}
for name in sources:
    directory = out / name
    commands = [
        ("build", ["dotnet", "build", str(directory / "Probe.csproj"), "-c", "Release", "--disable-build-servers", "-p:NuGetAudit=false", "-m:1"]),
        ("probe", ["dotnet", str(directory / "bin/Release/net8.0/Probe.dll"), str(directory)]),
    ]
    commands.append(("layout", ["dotnet", str(directory / "bin/Release/net8.0/Probe.dll"), str(directory), "layout"]))
    for sample in range(1, 4):
        sample_directory = directory / ("performance-" + str(sample))
        sample_directory.mkdir(exist_ok=False)
        commands.append(("performance-" + str(sample), ["dotnet", str(directory / "bin/Release/net8.0/Probe.dll"), str(sample_directory), "performance"]))
    for phase, command in commands:
        try:
            with (directory / (phase + ".stdout.log")).open("wb") as stdout, (directory / (phase + ".stderr.log")).open("wb") as stderr:
                result = subprocess.run(command, stdout=stdout, stderr=stderr, env=env, timeout=180)
            status[name + "/" + phase] = {"exit_code": result.returncode}
            if result.returncode:
                raise RuntimeError(name + "/" + phase + " failed; see preserved logs")
        except Exception as ex:
            status.setdefault(name + "/" + phase, {})["error"] = repr(ex)
            (out / "execution-status.json").write_text(json.dumps(status, indent=2) + "\n", encoding="utf-8")
            raise
(out / "execution-status.json").write_text(json.dumps(status, indent=2) + "\n", encoding="utf-8")
files = ["layout-semantics.json", "observations.json", "session-save.json", "session-save-stream.json", "session-restored-save.json", "session-trace.json", "session-trace-stream.json", "session-host.json", "neutral.dag.jsonl", "failure.dag.jsonl.partial"]
comparison = {}
for filename in files:
    left, right = [(out / version / filename).read_bytes() for version in sources]
    item = {"byte_identical": left == right, "before_sha256": hashlib.sha256(left).hexdigest(), "after_sha256": hashlib.sha256(right).hexdigest()}
    if filename == "observations.json" and left != right:
        a, b = json.loads(left), json.loads(right)
        item["different_observations"] = {key: {"before": a.get(key), "after": b.get(key)} for key in a.keys() | b.keys() if a.get(key) != b.get(key)}
    comparison[filename] = item
# Performance metrics are observations, never byte-equality requirements.
performance = {"layout": {version: json.loads((out / version / "layout-metrics.json").read_text(encoding="utf-8")) for version in sources}}
for sample in range(1, 4):
    folder = "performance-" + str(sample)
    left, right = [(out / version / folder / "performance-semantics.json").read_bytes() for version in sources]
    comparison[folder + "/performance-semantics.json"] = {"byte_identical": left == right, "before_sha256": hashlib.sha256(left).hexdigest(), "after_sha256": hashlib.sha256(right).hexdigest()}
    performance[folder] = {version: json.loads((out / version / folder / "performance-metrics.json").read_text(encoding="utf-8")) for version in sources}
(out / "performance-observations.json").write_text(json.dumps(performance, indent=2) + "\n", encoding="utf-8")
(out / "comparison.json").write_text(json.dumps(comparison, indent=2) + "\n", encoding="utf-8")
print(json.dumps(comparison, indent=2))
different = [name for name, item in comparison.items() if not item["byte_identical"]]
status["comparison"] = {"exit_code": 1 if different else 0, "different_files": different}
(out / "execution-status.json").write_text(json.dumps(status, indent=2) + "\n", encoding="utf-8")
if different:
    raise SystemExit("Observed engineering differences; inspect exact records before drawing conclusions")
