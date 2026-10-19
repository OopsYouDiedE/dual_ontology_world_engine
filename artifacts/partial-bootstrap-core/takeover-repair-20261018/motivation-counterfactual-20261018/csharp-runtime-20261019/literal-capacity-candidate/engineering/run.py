from pathlib import Path
import argparse, hashlib, json, os, shutil, subprocess, sys
HERE = Path(__file__).resolve().parent
TASK = HERE.parent.parent
ROOT = TASK.parents[4]
parser = argparse.ArgumentParser()
parser.add_argument("--out", required=True, type=Path)
parser.add_argument("--runtime", type=Path, default=ROOT / "language_core/compiler/Runtime.cs")
parser.add_argument("--before-source", type=Path, default=TASK / "frozen-04-integrated/source")
args = parser.parse_args()
out = args.out.resolve(); out.mkdir(parents=True, exist_ok=False)
commands = []; manifest = {}
env = os.environ.copy(); env.setdefault("DOTNET_ROLL_FORWARD", "Major")
def run(label, command):
    try:
        with (out / (label + ".stdout.log")).open("wb") as stdout, (out / (label + ".stderr.log")).open("wb") as stderr:
            result = subprocess.run([str(x) for x in command], stdout=stdout, stderr=stderr, env=env, timeout=180)
        commands.append({"label": label, "argv": [str(x) for x in command], "exit_code": result.returncode})
        (out / "commands.json").write_text(json.dumps(commands, indent=2), encoding="utf-8")
        if result.returncode: raise RuntimeError(label + " failed; raw stdout/stderr retained")
    except Exception as ex:
        (out / (label + ".driver-error.txt")).write_text(repr(ex), encoding="utf-8"); raise
def preserve(label, source, target):
    data = source.read_bytes(); target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
    manifest[label] = {"source": str(source), "sha256": hashlib.sha256(data).hexdigest()}
    (out / "source-hashes.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
# Resolve only the existing frozen C3 cache; never rebuild or mutate frozen source.
old = args.before_source.resolve(); old_language = old / "language_core"
digest = hashlib.sha256()
for source in sorted(x for x in (old_language / "compiler").rglob("*") if x.is_file() and not {"bin", "obj"}.intersection(x.parts)):
    digest.update(source.relative_to(old).as_posix().encode()); digest.update(source.read_bytes())
before_c3 = old_language / "obj/dv-toolchain" / digest.hexdigest() / "c3"
if not (before_c3 / "bin/Release/net8.0/Program.dll").exists(): raise RuntimeError("Frozen cached C3 is missing; no rebuild fallback: " + str(before_c3))
shutil.copytree(before_c3, out / "compiler-before")
preserve("before/compiler.dv", old_language / "compiler/compiler.dv", out / "compiler-source-before.dv.txt")
preserve("after/compiler.dv", ROOT / "language_core/compiler/compiler.dv", out / "compiler-source-after.dv.txt")
# Current compiler() may bootstrap at execution time. All native output is logged.
run("current-compiler", [sys.executable, "-c", "import sys;sys.path.insert(0,sys.argv[1]);from dv_toolchain import compiler;print(compiler())", ROOT / "language_core/tools"])
after_c3 = Path((out / "current-compiler.stdout.log").read_text(encoding="utf-8").strip().splitlines()[-1])
compilers = {"before": out / "compiler-before/bin/Release/net8.0/Program.dll", "after": after_c3}
for version in compilers:
    folder = out / version; folder.mkdir()
    chunks = []
    for source in sorted((ROOT / "language_core/core").glob("*.dv")):
        chosen = HERE.parent / "00-data.before.dv.txt" if version == "before" and source.name == "00-data.dv" else source
        preserve(version + "/core/" + source.name, chosen, folder / "sources" / source.name)
        chunks.append(chosen.read_text(encoding="utf-8"))
    preserve(version + "/fixture", HERE / "fixture.dv", folder / "fixture.dv")
    (folder / "bundle.dv").write_text("\n".join(chunks) + "\n" + (HERE / "fixture.dv").read_text(encoding="utf-8"), encoding="utf-8")
    preserve(version + "/runtime", args.runtime, folder / "Runtime.cs")
    preserve(version + "/wrapper", HERE / "Wrapper.cs", folder / "Wrapper.cs")
    run(version + "-generate", ["dotnet", compilers[version], folder / "bundle.dv", folder / "Program.cs"])
    project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><StartupObject>NeutralStartup</StartupObject><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup></Project>'
    (folder / "Probe.csproj").write_text(project, encoding="utf-8")
    run(version + "-build", ["dotnet", "build", folder / "Probe.csproj", "-c", "Release", "--disable-build-servers", "-p:NuGetAudit=false", "-m:1"])
    dll = folder / "bin/Release/net8.0/Probe.dll"
    run(version + "-semantics", ["dotnet", dll, folder])
    for sample in range(1, 4):
        target = folder / ("performance-" + str(sample)); target.mkdir()
        run(version + "-performance-" + str(sample), ["dotnet", dll, target, "performance"])
(out / "source-hashes.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
comparison = {}
for name in ["semantics.json", "exception.json"] + ["performance-" + str(n) + "/semantics.sha256" for n in range(1,4)]:
    a,b = [(out / version / name).read_bytes() for version in compilers]
    comparison[name] = {"byte_identical": a == b, "before_sha256": hashlib.sha256(a).hexdigest(), "after_sha256": hashlib.sha256(b).hexdigest()}
(out / "comparison.json").write_text(json.dumps(comparison, indent=2), encoding="utf-8")
if any(not item["byte_identical"] for item in comparison.values()): raise SystemExit("Neutral semantics differ; preserve and inspect raw evidence")
