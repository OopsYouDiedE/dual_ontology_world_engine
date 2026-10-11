"""Record engineering evidence only; never run game or flow regression."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "language_core"


def fingerprints():
    return {
        path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted(SOURCE.rglob("*"))
        if path.is_file() and not {"bin", "obj", "__pycache__"}.intersection(path.relative_to(SOURCE).parts)
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    args = parser.parse_args()
    output = (ROOT / args.out).resolve()
    artifact_root = (ROOT / "artifacts" / "partial-bootstrap-core").resolve()
    if not output.is_relative_to(artifact_root) or output == artifact_root:
        parser.error("Use a new run directory inside artifacts/partial-bootstrap-core.")
    if output.exists():
        parser.error("Evidence directory already exists; choose a new run name.")
    output.mkdir(parents=True)
    baseline = fingerprints()
    (output / "source-sha256.json").write_text(json.dumps(baseline, indent=2), encoding="utf-8")
    commands = [
        ("build", ["dotnet", "build", "language_core/LanguageCore.sln", "-c", "Release", "--disable-build-servers"]),
        ("mechanisms", ["dotnet", "run", "--project", "language_core/Cli/Language.Cli.csproj", "-c", "Release", "--no-build", "--", "demo"]),
    ]
    results = []
    for name, command in commands:
        process = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
        (output / f"{name}.stdout.txt").write_bytes(process.stdout)
        (output / f"{name}.stderr.txt").write_bytes(process.stderr)
        results.append({"command": command, "exit_code": process.returncode})
        sys.stdout.buffer.write(process.stdout)
        sys.stderr.buffer.write(process.stderr)
        if process.returncode:
            break
    after = fingerprints()
    binaries = {
        path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted((SOURCE / "Cli" / "bin" / "Release" / "net8.0").glob("*.dll"))
    }
    evidence = {"commands": results, "source_unchanged": baseline == after, "assemblies": binaries,
                "scope": "language mechanisms only; no game acceptance"}
    (output / "execution.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")
    if baseline != after:
        (output / "source-after-sha256.json").write_text(json.dumps(after, indent=2), encoding="utf-8")
        print("Source changed during execution; evidence invalid.", file=sys.stderr)
        return 1
    return results[-1]["exit_code"]


if __name__ == "__main__":
    raise SystemExit(main())
