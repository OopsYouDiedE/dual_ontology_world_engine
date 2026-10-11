"""Independent frozen-DLL mechanism review; no game or production-source mutation."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
FREEZE = ROOT / "artifacts/partial-bootstrap-core/engineering-20261012-dv-r2"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inspect():
    expected_sources = json.loads((FREEZE / "source-sha256.json").read_text(encoding="utf-8-sig"))
    expected_dlls = json.loads((FREEZE / "execution.json").read_text(encoding="utf-8-sig"))["assemblies"]
    observed_sources = {}
    for path in sorted((ROOT / "language_core").rglob("*")):
        if path.is_file() and not {"bin", "obj", "__pycache__"}.intersection(path.relative_to(ROOT / "language_core").parts):
            # Hash-only integrity inspection. No design-file text is decoded or displayed.
            observed_sources[path.relative_to(ROOT).as_posix()] = digest(path)
    observed_dlls = {name: digest(ROOT / name) for name in expected_dlls}
    return {
        "sources": observed_sources,
        "assemblies": observed_dlls,
        "source_differences": {name: {"expected": expected_sources.get(name), "actual": observed_sources.get(name)}
            for name in sorted(expected_sources.keys() | observed_sources.keys())
            if expected_sources.get(name) != observed_sources.get(name)},
        "assembly_differences": {name: {"expected": expected_dlls[name], "actual": observed_dlls[name]}
            for name in expected_dlls if expected_dlls[name] != observed_dlls[name]},
    }


def save_json(name, value):
    path = OUT / name
    if path.exists():
        raise RuntimeError(f"Evidence already exists: {path.name}")
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def run(name, command):
    process = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    for stream, data in (("stdout", process.stdout), ("stderr", process.stderr)):
        path = OUT / f"{name}.{stream}.txt"
        if path.exists():
            raise RuntimeError(f"Evidence already exists: {path.name}")
        path.write_bytes(data)
    sys.stdout.buffer.write(process.stdout)
    sys.stderr.buffer.write(process.stderr)
    return {"command": command, "exit_code": process.returncode}


def main():
    before = inspect()
    save_json("fingerprint-before.json", before)
    if before["source_differences"] or before["assembly_differences"]:
        print("r2 freeze mismatch; no dependent mechanism run.", file=sys.stderr)
        return 2
    frozen = OUT / "frozen"
    frozen.mkdir()
    original = ROOT / "language_core/Cli/bin/Release/net8.0/Language.Core.dll"
    local = frozen / original.name
    shutil.copyfile(original, local)
    if digest(local) != before["assemblies"]["language_core/Cli/bin/Release/net8.0/Language.Core.dll"]:
        raise RuntimeError("Frozen local DLL mismatch")
    project = "artifacts/partial-bootstrap-core/review-dv-r2/ReviewHarness.csproj"
    results = [run("harness-build", ["dotnet", "build", project, "-c", "Release", "--disable-build-servers"])]
    if not results[-1]["exit_code"]:
        results.append(run("harness", ["dotnet", "run", "--project", project, "-c", "Release", "--no-build"]))
    after = inspect()
    save_json("fingerprint-after.json", after)
    save_json("execution.json", {"commands": results,
        "frozen_dll_sha256": digest(local),
        "source_unchanged": before["sources"] == after["sources"],
        "production_assemblies_unchanged": before["assemblies"] == after["assemblies"],
        "scope": "Independent generic language/host mechanisms only; no game acceptance"})
    return results[-1]["exit_code"]


if __name__ == "__main__":
    raise SystemExit(main())
