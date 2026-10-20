#!/usr/bin/env python3
"""Run neutral DV engineering cases through a frozen, real C3 toolchain.

This runner never imports a chapter, session entry, oracle, or retired flow.
Creating this file does not execute any check. Each invocation requires a new
output directory and preserves every compiler/build/program output separately.
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
    "AGENTS.md",
    "language_core/AGENTS.md",
    "language_core/10-compiler.dv",
    "language_core/20-core.dv",
    "language_core/compiler/Runtime.cs",
    "language_core/compiler/seed/Program.cs",
    "language_core/compiler/seed/Seed.csproj",
    "language_core/tools/dv_toolchain.py",
)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def write_json(path: Path, value: object) -> None:
    with path.open("x", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


@contextmanager
def retained_compiler_directory(*, dir, prefix):
    # dv_toolchain normally removes temporary bootstrap output on failure. This
    # evidence runner keeps it so a C1/C2/C3 failure remains independently inspectable.
    path = Path(tempfile.mkdtemp(dir=dir, prefix=prefix))
    yield str(path)


def freeze_inputs(destination: Path) -> list[dict[str, object]]:
    paths = [ROOT / name for name in INPUTS]
    paths.extend(sorted(path for path in CHECKS.rglob("*")
                        if path.is_file() and path.suffix in {".py", ".dv", ".json"}
                        and "__pycache__" not in path.parts))
    entries = []
    for path in paths:
        relative = path.relative_to(ROOT)
        if path.is_symlink():
            raise ValueError(f"Source symlinks are not accepted: {relative}")
        content = path.read_bytes()
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("xb") as handle:
            handle.write(content)
        entries.append({"path": relative.as_posix(), "bytes": len(content),
                        "sha256": digest(content)})
    return entries


def verify_inputs(entries: list[dict[str, object]], frozen: Path) -> None:
    for entry in entries:
        for base, label in ((ROOT, "working source"), (frozen, "frozen source")):
            path = base / str(entry["path"])
            if not path.is_file() or digest(path.read_bytes()) != entry["sha256"]:
                raise RuntimeError(f"Changed {label}; stop this evidence batch: {entry['path']}")


class Recorder:
    def __init__(self, output: Path, frozen: Path, entries: list[dict[str, object]],
                 environment: dict[str, str], timeout: float):
        self.directory = output / "commands"
        self.directory.mkdir()
        self.frozen = frozen
        self.entries = entries
        self.environment = environment
        self.timeout = timeout
        self.sequence = 0

    def run(self, argv, **kwargs):
        verify_inputs(self.entries, self.frozen)
        timeout = min(float(kwargs.pop("timeout", self.timeout)), self.timeout)
        if kwargs:
            raise TypeError(f"Unexpected command options: {sorted(kwargs)}")
        self.sequence += 1
        stem = self.directory / f"{self.sequence:03d}"
        command = [str(arg) for arg in argv]
        write_json(stem.with_suffix(".command.json"),
                   {"argv": command, "cwd": str(self.frozen), "timeout_seconds": timeout})
        creation = {"start_new_session": True} if os.name != "nt" else {
            "creationflags": subprocess.CREATE_NEW_PROCESS_GROUP}
        timed_out = False
        process = subprocess.Popen(command, cwd=self.frozen, env=self.environment,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, **creation)
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
        for extension, content in (("stdout.txt", stdout), ("stderr.txt", stderr)):
            with stem.with_suffix("." + extension).open("xb") as handle:
                handle.write(content)
        write_json(stem.with_suffix(".result.json"),
                   {"returncode": process.returncode, "timed_out": timed_out,
                    "stdout_sha256": digest(stdout), "stderr_sha256": digest(stderr)})
        verify_inputs(self.entries, self.frozen)
        if timed_out:
            raise TimeoutError(f"Command {self.sequence} exceeded {timeout:g}s; outputs retained")
        return subprocess.CompletedProcess(command, process.returncode,
                                           stdout.decode("utf-8", errors="replace"),
                                           stderr.decode("utf-8", errors="replace"))


def require_result(result, expected: str | None, stage: str) -> None:
    if expected is None:
        if result.returncode:
            raise RuntimeError(f"Unexpected failure in {stage}; inspect retained command outputs")
        return
    if result.returncode == 0:
        raise AssertionError(f"{stage} unexpectedly accepted an invalid input")
    if expected not in result.stdout + result.stderr:
        raise AssertionError(f"{stage} failed for a different reason; expected: {expected}")


def validate_include_scope(path: Path, allowed: set[Path]) -> None:
    # This is an input boundary, not a replacement for the real include resolver.
    # Reject an unlisted include before the resolver can read it.
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if line.lstrip().startswith("#include"):
            match = re.fullmatch(r"#include\s+(.+?)\s*", line.strip())
            if match is None or (path.parent / match[1]).resolve() not in allowed:
                raise ValueError(f"Include outside the frozen neutral source set: {path}")


def bundle_case(case: dict, frozen_checks: Path, work: Path, toolchain,
                allowed: set[Path]) -> tuple[Path, Path]:
    if "source" in case:
        entry = work / "input.dv"
        with entry.open("x", encoding="utf-8", newline="\n") as handle:
            handle.write(case["source"])
        validate_include_scope(entry, allowed)
    else:
        entry = (frozen_checks / case["file"]).resolve()
        if entry not in allowed:
            raise ValueError("Case file is outside the frozen source manifest")
    chunks, ranges, modules = [], [], []
    offset = 0
    for path, source in toolchain.sources([entry]):
        if path != entry and path not in allowed:
            raise ValueError("Resolved source is outside the neutral manifest")
        length = len(source.encode("utf-16-le")) // 2
        chunks.append(source)
        ranges.append([offset, length])
        modules.append({"path": str(path), "sha256": digest(source.encode("utf-8"))})
        offset += length
    if "reject" not in case:
        main = case.get("main", "checks_main")
        if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", main) is None:
            raise ValueError("Invalid neutral main name")
        tail = f"(main ({main}))\n"
        chunks.append(tail)
        ranges.append([offset, len(tail.encode("utf-16-le")) // 2])
    bundle = work / "sources.dv"
    with bundle.open("x", encoding="utf-8", newline="\n") as handle:
        handle.write("".join(chunks))
    boundaries = work / "ranges.json"
    write_json(boundaries, ranges)
    write_json(work / "modules.json", modules)
    return bundle, boundaries


def run_case(case: dict, frozen_checks: Path, output: Path, toolchain,
             executable: Path, recorder: Recorder, allowed: set[Path]) -> dict:
    name = case["id"]
    if re.fullmatch(r"[a-z][a-z0-9-]*", name) is None:
        raise ValueError(f"Invalid case id: {name}")
    work = output / "cases" / name
    work.mkdir(parents=True)
    bundle, boundaries = bundle_case(case, frozen_checks, work, toolchain, allowed)
    program = work / "program"
    program.mkdir()
    generated = program / "Program.cs"
    rejection = case.get("reject")
    checked = recorder.run(["dotnet", executable, bundle, work / "unused.cs", "--check", boundaries])
    require_result(checked, rejection, f"{name}: --check")
    emitted = recorder.run(["dotnet", executable, bundle, generated, "--emit", boundaries])
    require_result(emitted, rejection, f"{name}: --emit")
    if rejection is not None:
        return {"id": name, "outcome": "expected-compiler-rejection", "diagnostic": rejection}
    shutil.copyfile(toolchain.LANGUAGE / "compiler/Runtime.cs", program / "Runtime.cs")
    with (program / "Program.csproj").open("x", encoding="utf-8", newline="\n") as handle:
        handle.write(toolchain.PROJECT)
    built = recorder.run(["dotnet", "build", program / "Program.csproj", "-c", "Release",
                          "--disable-build-servers", "-p:NuGetAudit=false", "-p:UseSharedCompilation=false"])
    require_result(built, None, f"{name}: .NET build")
    executions = []
    for run in case.get("runs", [{}]):
        args = run.get("argv", [])
        result = recorder.run(["dotnet", program / "bin/Release/net8.0/Program.dll", *args])
        expected_error = run.get("reject")
        require_result(result, expected_error, f"{name}: execution {args}")
        marker = run.get("marker")
        if marker is not None and marker not in result.stdout:
            raise AssertionError(f"{name}: missing completion marker {marker!r}")
        executions.append({"argv": args, "outcome": "expected-runtime-rejection"
                           if expected_error else "completed", "diagnostic": expected_error,
                           "marker": marker})
    return {"id": name, "outcome": "compiled-and-executed", "executions": executions,
            "generated_sha256": digest(generated.read_bytes())}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path,
                        help="New exclusive output directory; existing directories are rejected")
    parser.add_argument("--timeout", type=float, default=180,
                        help="Maximum seconds for each compiler/build/program command")
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("--timeout must be positive")
    output = args.out.resolve()
    if output == CHECKS or CHECKS in output.parents:
        parser.error("--out must be outside the authored checks directory")
    output.mkdir(parents=True, exist_ok=False)
    frozen = output / "frozen"
    frozen.mkdir()
    summary = {"scope": "neutral compiler and Core engineering only; no chapters or gameplay",
               "status": "incomplete", "cases": []}
    try:
        entries = freeze_inputs(frozen)
        verify_inputs(entries, frozen)
        write_json(output / "source-manifest.json", {
            "working_root": str(ROOT), "frozen_root": str(frozen), "inputs": entries,
            "compiler": "real C0 -> C1 -> C2 -> C3; exact C2/C3 generated-source fixed point",
            "bootstrap_input_scope": "compiler source, Runtime, and seed source/project; no design documents",
            "case_scope": "syntax; generic Core state/contract boundaries; finite linear certificates",
        })
        sys.dont_write_bytecode = True
        specification = importlib.util.spec_from_file_location(
            "neutral_frozen_dv_toolchain", frozen / "language_core/tools/dv_toolchain.py")
        if specification is None or specification.loader is None:
            raise RuntimeError("Cannot load frozen DV toolchain")
        toolchain = importlib.util.module_from_spec(specification)
        specification.loader.exec_module(toolchain)
        toolchain.tempfile = SimpleNamespace(TemporaryDirectory=retained_compiler_directory)
        environment = toolchain.environment()
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        recorder = Recorder(output, frozen, entries, environment, args.timeout)
        toolchain.run = recorder.run
        version = recorder.run(["dotnet", "--version"])
        require_result(version, None, "SDK version")
        executable = toolchain.compiler()
        c2 = executable.parents[4] / "c2/Program.cs"
        c3 = executable.parents[3] / "Program.cs"
        if c2.read_bytes() != c3.read_bytes():
            raise AssertionError("C2/C3 generated source differs")
        write_json(output / "compiler-manifest.json", {
            "sdk": version.stdout.strip(), "c3": str(executable),
            "c3_sha256": digest(executable.read_bytes()),
            "c2_source_sha256": digest(c2.read_bytes()),
            "c3_source_sha256": digest(c3.read_bytes()), "fixed_point": True,
        })
        frozen_checks = frozen / CHECKS.relative_to(ROOT)
        cases = json.loads((frozen_checks / "cases.json").read_text(encoding="utf-8"))
        identifiers = [case["id"] for case in cases]
        if len(identifiers) != len(set(identifiers)):
            raise ValueError("Duplicate case ids")
        allowed = {(frozen / str(entry["path"])).resolve() for entry in entries
                   if str(entry["path"]).endswith(".dv")}
        for path in allowed:
            validate_include_scope(path, allowed)
        for case in cases:
            summary["cases"].append(run_case(case, frozen_checks, output, toolchain,
                                             executable, recorder, allowed))
        verify_inputs(entries, frozen)
        summary["status"] = "completed"
        summary["limitations"] = ["No formal session, gameplay, UI, or wall-clock speed claim",
                                  "Solver operation counters are not total runtime costs"]
    except Exception as error:
        summary["status"] = "failed"
        summary["error"] = f"{type(error).__name__}: {error}"
        with (output / "failure.txt").open("x", encoding="utf-8", newline="\n") as handle:
            handle.write(traceback.format_exc())
    write_json(output / "summary.json", summary)
    with (output / ("COMPLETE" if summary["status"] == "completed" else "INCOMPLETE")).open("x") as handle:
        handle.write(summary["status"] + "\n")
    print(f"Neutral engineering batch {summary['status']}: {output}")
    return 0 if summary["status"] == "completed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
