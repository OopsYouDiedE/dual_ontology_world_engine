#!/usr/bin/env python3
"""Compile and run neutral natural-authoring fixtures with a frozen real C3.

No chapter, session, oracle, or gameplay flow is loaded. The output directory
must be new; compiler inputs, generated programs, diagnostics and failed runs
are retained. Results describe individual engineering scenarios, not gameplay.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import traceback
from types import SimpleNamespace


CHECKS = Path(__file__).resolve().parent
ROOT = CHECKS.parents[2]
INPUTS = (
    "AGENTS.md", "language_core/AGENTS.md",
    "language_core/10-compiler.dv", "language_core/20-core.dv",
    "language_core/compiler/Runtime.cs",
    "language_core/compiler/seed/Program.cs",
    "language_core/compiler/seed/Seed.csproj",
    "language_core/tools/dv_toolchain.py",
)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_file(path: Path, data: object) -> None:
    with path.open("x", encoding="utf-8", newline="\n") as handle:
        json.dump(data, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


class FrozenSourceChanged(RuntimeError):
    pass


def freeze(destination: Path) -> list[dict]:
    paths = [ROOT / name for name in INPUTS]
    paths += sorted(p for p in CHECKS.rglob("*")
                    if p.is_file() and p.suffix in {".dv", ".py", ".json"}
                    and "__pycache__" not in p.parts)
    manifest = []
    for path in paths:
        if path.is_symlink():
            raise ValueError(f"Fixture inputs must be regular files: {path}")
        relative, data = path.relative_to(ROOT), path.read_bytes()
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("xb") as handle:
            handle.write(data)
        manifest.append({"path": relative.as_posix(), "bytes": len(data),
                         "sha256": sha256(data),
                         "role": "workflow-snapshot" if path.name == "AGENTS.md"
                         else "execution-input"})
    return manifest


def unchanged(manifest: list[dict], frozen: Path) -> None:
    for item in manifest:
        # AGENTS is retained as the workflow snapshot read for this batch. It is
        # not a compiler input; documentation edits outside the batch may proceed.
        bases = (frozen,) if item["role"] == "workflow-snapshot" else (ROOT, frozen)
        for base in bases:
            path = base / item["path"]
            if not path.is_file() or sha256(path.read_bytes()) != item["sha256"]:
                raise FrozenSourceChanged(f"Frozen batch source changed: {path}")


@contextmanager
def retain_build_directory(*, dir, prefix):
    # Preserve bootstrap failures as well as successful generated stages.
    yield tempfile.mkdtemp(dir=dir, prefix=prefix)


class Commands:
    def __init__(self, output, frozen, manifest, environment, timeout):
        self.directory = output / "commands"
        self.directory.mkdir()
        self.frozen, self.manifest = frozen, manifest
        self.environment, self.timeout = environment, timeout
        self.sequence = 0

    def run(self, argv, **kwargs):
        unchanged(self.manifest, self.frozen)
        timeout = min(float(kwargs.pop("timeout", self.timeout)), self.timeout)
        if kwargs:
            raise TypeError(f"Unexpected subprocess options: {sorted(kwargs)}")
        self.sequence += 1
        stem = self.directory / f"{self.sequence:03d}"
        argv = [str(a) for a in argv]
        json_file(stem.with_suffix(".command.json"),
                  {"argv": argv, "cwd": str(self.frozen), "timeout": timeout})
        flags = {"start_new_session": True} if os.name != "nt" else {
            "creationflags": subprocess.CREATE_NEW_PROCESS_GROUP}
        process = subprocess.Popen(argv, cwd=self.frozen, env=self.environment,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, **flags)
        timed_out = False
        try:
            stdout, stderr = process.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            if os.name == "nt":
                subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                               capture_output=True, check=False)
            else:
                os.killpg(process.pid, signal.SIGKILL)
            stdout, stderr = process.communicate()
        for suffix, data in (("stdout.txt", stdout), ("stderr.txt", stderr)):
            with stem.with_suffix("." + suffix).open("xb") as handle:
                handle.write(data)
        json_file(stem.with_suffix(".result.json"), {
            "returncode": process.returncode, "timed_out": timed_out,
            "stdout_sha256": sha256(stdout), "stderr_sha256": sha256(stderr)})
        unchanged(self.manifest, self.frozen)
        if timed_out:
            raise TimeoutError(f"Command {self.sequence} exceeded {timeout:g}s")
        return subprocess.CompletedProcess(argv, process.returncode,
                                           stdout.decode("utf-8", "replace"),
                                           stderr.decode("utf-8", "replace"))


def success(result, stage):
    if result.returncode:
        raise RuntimeError(f"{stage} failed; retained stdout/stderr contain the actual error")


def allowed_includes(path: Path, allowed: set[Path]) -> None:
    # Boundary check only; the production resolver owns includes and source text.
    # Indented #include is deliberately ignored here because it can be prose.
    for line in path.read_text(encoding="utf-8-sig").split("\n"):
        if not line.startswith("#include"):
            continue
        match = re.fullmatch(r"#include\s+(.+?)\s*", line)
        if match is None or (path.parent / match[1]).resolve() not in allowed:
            raise ValueError(f"Include outside the neutral manifest: {path}: {line}")


def bundle(case, frozen_checks, work, toolchain, allowed):
    entry = (frozen_checks / case["file"]).resolve()
    if entry not in allowed:
        raise ValueError("Case entry is not in the frozen manifest")
    parts, ranges, locations, offset, row = [], [], [], 0, 1
    for path, source in toolchain.sources([entry]):
        if path.resolve() not in allowed:
            raise ValueError(f"Resolved non-neutral source: {path}")
        units = len(source.encode("utf-16-le")) // 2
        locations.append({"start": row, "end": row + source.count("\n"),
                          "path": str(path), "sha256": sha256(source.encode("utf-8"))})
        ranges.append([offset, units])
        parts.append(source)
        offset += units
        row += source.count("\n")
    # Emission validates the executable entry before checking function bodies.
    # Invalid declarations still need a neutral main so a missing entry cannot
    # mask the intended symbol or type diagnostic. Negative cases are never run.
    tail = "(main true)\n" if "reject" in case else "(main (checks_main))\n"
    parts.append(tail)
    ranges.append([offset, len(tail)])
    source, boundaries = work / "sources.dv", work / "ranges.json"
    with source.open("x", encoding="utf-8", newline="\n") as handle:
        handle.write("".join(parts))
    json_file(boundaries, ranges)
    json_file(work / "modules.json", locations)
    return source, boundaries, locations


def diagnostic(result, bundle_path, locations, case, frozen_checks):
    if result.returncode == 0:
        raise AssertionError("Compiler accepted the intentionally invalid declaration")
    raw = result.stdout + result.stderr
    found = re.search(re.escape(str(bundle_path)) + r":(\d+):(\d+): ([^\r\n]+)", raw)
    if found is None:
        raise AssertionError("Compiler rejection did not include a source line and column")
    row, column, message = int(found[1]), int(found[2]), found[3]
    module = next((x for x in locations if x["start"] <= row < x["end"]), None)
    if module is None:
        raise AssertionError(f"Diagnostic line is outside the source map: {row}")
    actual = {"path": module["path"], "line": row - module["start"] + 1,
              "column": column, "message": message}
    expected = case["reject"]
    if expected["message"] not in message:
        raise AssertionError(f"Unexpected rejection: {actual}; expected {expected}")
    expected_path = str((frozen_checks / expected.get("file", case["file"])).resolve())
    if (actual["path"], actual["line"], actual["column"]) != (
            expected_path, expected["line"], expected["column"]):
        raise AssertionError(f"Incorrect original-source diagnostic: {actual}; expected {expected}")
    return actual


def execute_case(case, frozen_checks, output, toolchain, compiler, commands, allowed):
    work = output / "cases" / case["id"]
    work.mkdir(parents=True)
    source, boundaries, locations = bundle(case, frozen_checks, work, toolchain, allowed)
    program = work / "program"
    program.mkdir()
    generated = program / "Program.cs"
    checks = []
    for mode in ("--check", "--emit"):
        result = commands.run(["dotnet", compiler, source, generated, mode, boundaries])
        if "reject" in case:
            checks.append({"mode": mode, "diagnostic": diagnostic(
                result, source, locations, case, frozen_checks)})
        else:
            success(result, f"{case['id']} {mode}")
    if "reject" in case:
        return {"id": case["id"], "outcome": "rejected-at-expected-source-location",
                "checks": checks}
    shutil.copyfile(toolchain.LANGUAGE / "compiler/Runtime.cs", program / "Runtime.cs")
    with (program / "Program.csproj").open("x", encoding="utf-8", newline="\n") as handle:
        handle.write(toolchain.PROJECT)
    built = commands.run(["dotnet", "build", program / "Program.csproj", "-c", "Release",
                          "--disable-build-servers", "-p:NuGetAudit=false"])
    success(built, f"{case['id']} .NET build")
    execution = commands.run(["dotnet", program / "bin/Release/net8.0/Program.dll"])
    success(execution, f"{case['id']} execution")
    # Only LF separates output records. U+2028 remains part of a literal value.
    lines = execution.stdout.split("\n")
    values = []
    for expected in case.get("outputs", []):
        matches = [line[len(expected["prefix"]):] for line in lines
                   if line.startswith(expected["prefix"])]
        if len(matches) != 1:
            raise AssertionError(f"Expected one output with prefix {expected['prefix']!r}")
        actual = json.loads(matches[0])
        if actual != expected["value"]:
            raise AssertionError(f"Literal output changed: {expected['prefix']!r}: {actual!r}")
        values.append({"prefix": expected["prefix"], "value": actual})
    for marker in case.get("markers", []):
        if marker not in lines:
            raise AssertionError(f"Execution did not reach scenario marker {marker!r}")
    return {"id": case["id"], "outcome": "compiled-and-executed",
            "outputs": values, "scenario_markers": case.get("markers", []),
            "generated_sha256": sha256(generated.read_bytes())}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--timeout", type=float, default=180,
                        help="Per-command timeout in seconds; no gameplay is executed")
    parser.add_argument("--only", nargs="+", metavar="CASE",
                        help="Run only named cases for a focused follow-up; all inputs are still frozen")
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("--timeout must be positive")
    output = args.out.resolve()
    if output == CHECKS or CHECKS in output.parents:
        parser.error("--out must be outside the authored checks")
    output.mkdir(parents=True, exist_ok=False)
    frozen = output / "frozen"
    frozen.mkdir()
    summary = {"scope": "neutral authoring syntax and Core data semantics; no gameplay",
               "status": "incomplete", "cases": []}
    try:
        manifest = freeze(frozen)
        json_file(output / "source-manifest.json", manifest)
        sys.dont_write_bytecode = True
        spec = importlib.util.spec_from_file_location(
            "natural_frozen_toolchain", frozen / "language_core/tools/dv_toolchain.py")
        if spec is None or spec.loader is None:
            raise RuntimeError("Cannot load the frozen production toolchain")
        toolchain = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(toolchain)
        toolchain.tempfile = SimpleNamespace(TemporaryDirectory=retain_build_directory)
        environment = toolchain.environment()
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        commands = Commands(output, frozen, manifest, environment, args.timeout)
        toolchain.run = commands.run
        version = commands.run(["dotnet", "--version"])
        success(version, "SDK version")
        compiler = toolchain.compiler()
        c2 = compiler.parents[4] / "c2/Program.cs"
        c3 = compiler.parents[3] / "Program.cs"
        if c2.read_bytes() != c3.read_bytes():
            raise AssertionError("Actual C2/C3 generated-source fixed point differs")
        json_file(output / "compiler-manifest.json", {
            "sdk": version.stdout.strip(), "c3": str(compiler),
            "assembly_sha256": sha256(compiler.read_bytes()),
            "c2_source_sha256": sha256(c2.read_bytes()),
            "c3_source_sha256": sha256(c3.read_bytes()), "fixed_point": True})
        frozen_checks = frozen / CHECKS.relative_to(ROOT)
        cases = json.loads((frozen_checks / "cases.json").read_text(encoding="utf-8"))
        names = [case["id"] for case in cases]
        if len(names) != len(set(names)) or any(re.fullmatch(r"[a-z][a-z0-9-]*", n) is None for n in names):
            raise ValueError("Case ids must be unique simple directory names")
        if args.only:
            if set(args.only) - set(names):
                raise ValueError(f"Unknown focused cases: {sorted(set(args.only) - set(names))}")
            cases = [case for case in cases if case["id"] in args.only]
        summary["selection"] = args.only or "all-authored-cases"
        allowed = {(frozen / item["path"]).resolve() for item in manifest
                   if item["path"].endswith(".dv")}
        for path in allowed:
            allowed_includes(path, allowed)
        for case in cases:
            try:
                result = execute_case(case, frozen_checks, output, toolchain,
                                      compiler, commands, allowed)
            except FrozenSourceChanged:
                raise
            except Exception as error:
                result = {"id": case["id"], "outcome": "unexpected-result",
                          "error": f"{type(error).__name__}: {error}"}
                with (output / "cases" / case["id"] / "failure.txt").open("x", encoding="utf-8") as handle:
                    handle.write(traceback.format_exc())
            summary["cases"].append(result)
            print(f"{result['id']}: {result['outcome']}", flush=True)
        unchanged(manifest, frozen)
        summary["status"] = "completed" if all(
            case["outcome"] != "unexpected-result" for case in summary["cases"]) else "failed"
    except Exception as error:
        summary["status"] = "failed"
        summary["error"] = f"{type(error).__name__}: {error}"
        with (output / "failure.txt").open("x", encoding="utf-8") as handle:
            handle.write(traceback.format_exc())
    json_file(output / "summary.json", summary)
    with (output / ("COMPLETE" if summary["status"] == "completed" else "INCOMPLETE")).open("x") as handle:
        handle.write(summary["status"] + "\n")
    print(f"Neutral authoring batch {summary['status']}: {output}")
    return 0 if summary["status"] == "completed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
