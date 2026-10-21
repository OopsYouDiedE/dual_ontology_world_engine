"""Run a bounded rendered production capture with Godot and managed profiling."""
from __future__ import annotations

import argparse
import datetime
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import time

from binary_evidence import RecordWriter
from capture_result import collect_capture_result, read_journal, wait_owned, wait_owned_many
from capture_workspace import ROOT, fingerprint, prepare_capture, user_sessions

EVIDENCE_ATTRIBUTES = (
    "run/** -text -diff\nhost-session/** -text -diff\n*.log -text -diff\n"
    "*.nettrace -text -diff\n*.json -text -diff\n*.dvlog* -text -diff\n*.dvdag* -text -diff\n*.dvsave* -text -diff\n"
    "managed-*.txt -text -diff\nVISUAL*.md -text -diff\n"
)


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path, help="New evidence directory")
    parser.add_argument("--godot", default=os.environ.get("GODOT"))
    parser.add_argument("--dotnet-trace", default=shutil.which("dotnet-trace"))
    parser.add_argument("--managed-profile", default="dotnet-sampled-thread-time")
    parser.add_argument("--seconds", type=float, default=90)
    parser.add_argument("--controller", choices=("ai", "player"), default="ai")
    parser.add_argument("--window-mode", choices=("offscreen", "window"), default="offscreen")
    parser.add_argument("--speed", type=float, default=4.0)
    parser.add_argument("--managed-seconds", type=int)
    parser.add_argument("--native-profiling", choices=("off", "full"), default="off")
    args = parser.parse_args()
    if not args.godot or not args.dotnet_trace:
        parser.error("Specify --godot and --dotnet-trace (or GODOT and PATH)")
    if not math.isfinite(args.seconds) or args.seconds <= 0 or not 0.25 <= args.speed <= 8.0:
        parser.error("Require finite positive --seconds and --speed between 0.25 and 8")
    managed_seconds = args.managed_seconds if args.managed_seconds is not None else max(1, int(args.seconds) - 10)
    if managed_seconds <= 0 or managed_seconds >= args.seconds:
        parser.error("--managed-seconds must be positive and shorter than --seconds")
    godot = Path(args.godot).resolve(strict=True)
    if godot.name.endswith("_console.exe"):
        runtime = godot.with_name(godot.name.replace("_console.exe", ".exe"))
        if runtime.is_file():
            godot = runtime
    trace = Path(args.dotnet_trace).resolve(strict=True)
    out = args.out.resolve()
    if any(out.is_relative_to(ROOT / name) for name in ("godot_csharp", "dv_core", "mcp")):
        parser.error("Keep evidence outside the fingerprinted source directories")
    out.mkdir(parents=True, exist_ok=False)
    (out / ".gitattributes").write_text(EVIDENCE_ATTRIBUTES, encoding="utf-8")
    work = prepare_capture(godot, out)
    before = fingerprint(work)
    write_json(out / "source-before.json", before)
    session_root = user_sessions()
    existing = {path.name for path in session_root.glob("*")}
    command = [str(godot), "--path", str(work), "--log-file", str(out / "godot-native.log"),
               "--ignore-error-breaks", *(["--debug", "--profiling", "--gpu-profile"] if args.native_profiling == "full" else []),
               "--script", "res://tools/dv_level3_capture.gd", "--", f"--out={out / 'run'}",
               "--mode=" + ("detective" if args.controller == "ai" else "player"), f"--seconds={args.seconds}",
               f"--dv-controller={args.controller}", f"--dv-speed={args.speed}", "--dv-audit",
               f"--window-mode={args.window_mode}", "--dv-auto-start"]
    launch = {
        "started_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip(),
        "command": command, "profiling": "managed thread stack sampling plus passive frame/phase timings",
        "managed_profile": args.managed_profile, "native_profiling_requested": args.native_profiling,
        "tool_workspace": str(work), "audit_mode": True, "window_mode": args.window_mode,
        "managed_requested_seconds": managed_seconds,
        "managed_coverage": "Bounded sampling stops before host exit; the final capture tail is not managed-profiled",
        "scope": "Bounded rendered capture; profiling overhead included; not a completion test or speed comparison",
    }
    started = time.monotonic()
    game_deadline = started + args.seconds + 180.0
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    duration = f"{managed_seconds // 86400:02}:{managed_seconds // 3600 % 24:02}:{managed_seconds // 60 % 60:02}:{managed_seconds % 60:02}"
    collector = None
    collector_exit = None
    collector_timeout = False
    collector_error = ""
    with (out / "godot-stdout.log").open("wb") as stdout, (out / "godot-stderr.log").open("wb") as stderr, \
            (out / "profiler-stdout.log").open("wb") as trace_stdout, (out / "profiler-stderr.log").open("wb") as trace_stderr:
        game = subprocess.Popen(command, cwd=ROOT, stdin=subprocess.DEVNULL, stdout=stdout, stderr=stderr, creationflags=flags)
        launch["game_pid"] = game.pid
        write_json(out / "launch.json", launch)
        discovery_started = time.monotonic()
        discovery_deadline = discovery_started + 10.0
        managed_ready = False
        discovery_error = ""
        while game.poll() is None and time.monotonic() < discovery_deadline:
            try:
                listing = subprocess.run([str(trace), "ps"], cwd=ROOT, capture_output=True,
                                         creationflags=flags, timeout=min(3.0, max(0.01, discovery_deadline - time.monotonic())))
            except (subprocess.TimeoutExpired, OSError) as error:
                discovery_error = str(error)
                break
            if listing.returncode != 0:
                discovery_error = listing.stderr.decode("utf-8", errors="replace")
            if any(line.split() and line.split()[0] == str(game.pid)
                   for line in listing.stdout.decode("utf-8", errors="replace").splitlines()):
                managed_ready = True
                break
            time.sleep(0.1)
        trace_command = [str(trace), "collect", "--process-id", str(game.pid), "--profile", args.managed_profile,
                         "--duration", duration, "--output", str(out / "managed.nettrace")]
        launch.update({"profiler_command": trace_command, "managed_process_discovered": managed_ready,
                       "managed_discovery_elapsed_seconds": time.monotonic() - discovery_started,
                       "managed_discovery_error": discovery_error})
        write_json(out / "launch.json", launch)
        collector_deadline = time.monotonic() + managed_seconds + 60.0
        if managed_ready:
            try:
                collector = subprocess.Popen(trace_command, cwd=ROOT, stdin=subprocess.DEVNULL,
                                             stdout=trace_stdout, stderr=trace_stderr, creationflags=flags)
            except OSError as error:
                collector_error = str(error)
        owned = {"game": (game, game_deadline)}
        if collector is not None:
            owned["collector"] = (collector, collector_deadline)
        exits = wait_owned_many(owned)
        game_exit, game_timeout = exits["game"]
        if collector is not None:
            collector_exit, collector_timeout = exits["collector"]
    after = fingerprint(work)
    write_json(out / "source-after.json", after)
    result = collect_capture_result(out=out, session_root=session_root, existing=existing,
                                    before=before, after=after, seconds=args.seconds,
                                    exit_code=game_exit, timed_out=game_timeout,
                                    runtime_logs=("godot-stdout.log", "godot-stderr.log", "godot-native.log"))
    result.update(profiler_exit_code=collector_exit, profiler_timeout=collector_timeout,
                  profiler_launch_error=collector_error, managed_process_discovered=managed_ready,
                  managed_requested_seconds=managed_seconds, managed_report_readable=False,
                  managed_report_timeout=False, native_profiling_requested=args.native_profiling)
    rows = read_journal(out / "run/measurement.dvlog", result["artifact_errors"])
    valid = [row for row in rows if "game_time" in row.get("snapshot", {})]
    if valid:
        result["first_clock_real_ms"] = valid[0]["real_ms"]
        result["first_game_time_minutes"] = valid[0]["snapshot"]["game_time"]
        result["end_game_time_minutes"] = result.get("game_time")
        if result["end_game_time_minutes"] is not None:
            result["observed_game_elapsed_minutes"] = result["end_game_time_minutes"] - result["first_game_time_minutes"]
    if collector_exit == 0 and (out / "managed.nettrace").is_file():
        with (out / "managed-hotspots.txt").open("wb") as report, (out / "report-stderr.log").open("wb") as report_stderr:
            report_process = subprocess.Popen([str(trace), "report", str(out / "managed.nettrace"), "topN"],
                                              cwd=ROOT, stdin=subprocess.DEVNULL, stdout=report, stderr=report_stderr, creationflags=flags)
            report_exit, report_timeout = wait_owned(report_process, time.monotonic() + 45.0)
        result["managed_report_exit_code"] = report_exit
        result["managed_report_timeout"] = report_timeout
        report_text = (out / "managed-hotspots.txt").read_text(encoding="utf-8", errors="replace")
        report_errors = (out / "report-stderr.log").read_text(encoding="utf-8", errors="replace")
        result["managed_report_readable"] = report_exit == 0 and not report_timeout and "[ERROR]" not in report_text + report_errors and bool(report_text.strip())
    engine_text = "\n".join((out / name).read_text(encoding="utf-8", errors="replace")
                            for name in ("godot-stdout.log", "godot-stderr.log", "godot-native.log") if (out / name).is_file())
    result["godot_script_profile_confirmed"] = "BEGIN PROFILING" in engine_text
    result["gpu_profile_confirmed"] = "GPU PROFILE" in engine_text
    profiler_text = (out / "profiler-stderr.log").read_text(encoding="utf-8", errors="replace")
    result["profiler_error_lines"] = [line for line in profiler_text.splitlines()
                                      if line.lstrip().lower().startswith(("[error]", "error:", "unhandled exception"))]
    result["capture_success"] = result["capture_success"] and not result["artifact_errors"]
    result["profiling_success"] = (managed_ready and collector_exit == 0 and not collector_timeout
                                    and not collector_error and result["managed_report_readable"]
                                    and not result["profiler_error_lines"])
    result["success"] = result["capture_success"] and result["profiling_success"]
    result["launcher_elapsed_seconds"] = time.monotonic() - started
    with RecordWriter(out / "result.dvlog") as output:
        output.append(result)
    print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)
    return int(not result["success"])


if __name__ == "__main__":
    raise SystemExit(main())
