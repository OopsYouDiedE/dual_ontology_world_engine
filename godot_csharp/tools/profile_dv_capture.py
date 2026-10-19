"""Run the real rendered detective capture with Godot and managed profiling."""

import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time


ROOT = Path(__file__).resolve().parents[2]
EVIDENCE_ATTRIBUTES = (
    "run/** -text -diff\nhost-session/** -text -diff\n*.log -text -diff\n"
    "*.nettrace -text -diff\n*.json -text -diff\n*.jsonl -text -diff\n"
    "managed-*.txt -text -diff\nVISUAL*.md -text -diff\n"
)


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def fingerprint(paths):
    result = {}
    for path in paths:
        if path.is_file():
            digest = hashlib.sha256()
            with path.open("rb") as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(block)
            result[str(path.relative_to(ROOT))] = digest.hexdigest()
        else:
            result[str(path.relative_to(ROOT))] = None
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path, help="New evidence directory")
    parser.add_argument("--godot", default=os.environ.get("GODOT"))
    parser.add_argument("--dotnet-trace", default=shutil.which("dotnet-trace"))
    parser.add_argument("--seconds", type=float, default=90)
    parser.add_argument("--controller", choices=("ai", "player"), default="ai")
    parser.add_argument("--speed", type=float, default=4.0, help="Explicit Godot playback speed; use 1 for normal-speed interaction checks")
    parser.add_argument("--managed-seconds", type=int, help="Managed sampling duration; defaults to window minus 10 seconds")
    parser.add_argument("--native-profiling", choices=("off", "full"), default="off",
                        help="Optional Godot console script/GPU profiling; .NET sampling always remains enabled")
    args = parser.parse_args()
    if not args.godot or not args.dotnet_trace:
        parser.error("Specify --godot and --dotnet-trace (or GODOT and PATH)")
    if args.seconds <= 0:
        parser.error("--seconds must be positive")
    if not 0.25 <= args.speed <= 8.0:
        parser.error("--speed must be between 0.25 and 8")
    managed_seconds = args.managed_seconds if args.managed_seconds is not None else max(1, int(args.seconds) - 10)
    if managed_seconds <= 0 or managed_seconds >= args.seconds:
        parser.error("--managed-seconds must be positive and shorter than --seconds, so the profiler can drain before host exit")
    godot = Path(args.godot).resolve(strict=True)
    trace = Path(args.dotnet_trace).resolve(strict=True)
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    (out / ".gitattributes").write_text(EVIDENCE_ATTRIBUTES, encoding="utf-8")
    names = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).decode().split("\0")
    paths = {ROOT / name for name in names if name and not name.startswith(("artifacts/", "tmp/", "scratch/"))}
    paths.add(Path(__file__).resolve())
    paths.update((ROOT / "godot_csharp/.godot/mono/temp/bin/Debug").glob("*.dll"))
    before = fingerprint(sorted(paths))
    write_json(out / "source-before.json", before)
    session_root = Path(os.environ["APPDATA"]) / "Godot/app_userdata/Dual Ontology World Engine/dv-sessions"
    existing = {p.name for p in session_root.glob("*")}
    command = [str(godot), "--path", str(ROOT / "godot_csharp"),
               "--debug", *(["--profiling", "--gpu-profile"] if args.native_profiling == "full" else []), "--script", "res://tools/dv_level3_capture.gd", "--",
               f"--out={out / 'run'}", "--mode=" + ("detective" if args.controller == "ai" else "player"), f"--seconds={args.seconds}",
               f"--dv-controller={args.controller}", f"--dv-speed={args.speed}", "--dv-audit"]
    if args.controller == "player":
        command.append("--dv-auto-start")
    launch = {
        "started_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip(),
        "command": command, "profiling": ".NET cpu-sampling attached automatically; passive frame/phase timings retained",
        "native_profiling_requested": args.native_profiling,
        "managed_requested_seconds": managed_seconds,
        "managed_coverage": "Bounded sampling stops before native host exits; final capture tail is not managed-profiled",
        "scope": "Bounded rendered capture; profiling overhead included; not a completion test or speed comparison",
    }
    started = time.monotonic()
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    duration = f"{managed_seconds // 86400:02}:{managed_seconds // 3600 % 24:02}:{managed_seconds // 60 % 60:02}:{managed_seconds % 60:02}"
    with (out / "godot-stdout.log").open("wb") as stdout, (out / "godot-stderr.log").open("wb") as stderr, \
            (out / "profiler-stdout.log").open("wb") as trace_stdout, (out / "profiler-stderr.log").open("wb") as trace_stderr:
        game = subprocess.Popen(command, cwd=ROOT, stdin=subprocess.DEVNULL, stdout=stdout, stderr=stderr, creationflags=flags)
        discovery_started = time.monotonic()
        managed_ready = False
        while game.poll() is None and time.monotonic() - discovery_started < 10:
            listing = subprocess.run([str(trace), "ps"], cwd=ROOT, capture_output=True, creationflags=flags)
            if any(line.split() and line.split()[0] == str(game.pid)
                   for line in listing.stdout.decode("utf-8", errors="replace").splitlines()):
                managed_ready = True
                break
            time.sleep(0.1)
        trace_command = [str(trace), "collect", "--process-id", str(game.pid), "--profile", "cpu-sampling",
                         "--duration", duration, "--output", str(out / "managed.nettrace")]
        launch.update({"game_pid": game.pid, "profiler_command": trace_command,
                       "managed_process_discovered": managed_ready,
                       "managed_discovery_elapsed_seconds": time.monotonic() - discovery_started})
        write_json(out / "launch.json", launch)
        try:
            collector = subprocess.Popen(trace_command, cwd=ROOT, stdin=subprocess.DEVNULL,
                                         stdout=trace_stdout, stderr=trace_stderr, creationflags=flags)
        except OSError:
            # Preserve the actual game and its evidence even if the profiler cannot launch.
            game.wait()
            raise
        game_exit = game.wait()
        collector_exit = collector.wait()
    result = {"exit_code": game_exit, "profiler_exit_code": collector_exit,
              "launcher_elapsed_seconds": time.monotonic() - started,
              "managed_requested_seconds": managed_seconds}
    after = fingerprint(sorted(paths))
    write_json(out / "source-after.json", after)
    result["source_changes"] = [name for name in before if before[name] != after[name]]
    sessions = [p for p in session_root.glob("*") if p.name not in existing]
    result["new_host_sessions"] = [str(p) for p in sessions]
    if len(sessions) == 1:
        shutil.copytree(sessions[0], out / "host-session")
    run = out / "run"
    if (run / "end.json").exists():
        end = json.loads((run / "end.json").read_text(encoding="utf-8"))
        rows = [json.loads(line) for line in (run / "measurement.jsonl").read_text(encoding="utf-8").splitlines()]
        valid = [row for row in rows if "game_time" in row.get("snapshot", {})]
        result["capture_end_real_ms"] = end["real_ms"]
        if valid:
            result["first_clock_real_ms"] = valid[0]["real_ms"]
            result["first_game_time_minutes"] = valid[0]["snapshot"]["game_time"]
            result["end_game_time_minutes"] = end["snapshot"].get("game_time")
            if result["end_game_time_minutes"] is not None:
                result["observed_game_elapsed_minutes"] = result["end_game_time_minutes"] - result["first_game_time_minutes"]
    if (out / "managed.nettrace").is_file():
        with (out / "managed-hotspots.txt").open("wb") as report:
            analysis = subprocess.run([str(trace), "report", str(out / "managed.nettrace"), "topN"],
                                      cwd=ROOT, stdout=report, stderr=subprocess.STDOUT, creationflags=flags)
        result["managed_report_exit_code"] = analysis.returncode
        report_text = (out / "managed-hotspots.txt").read_text(encoding="utf-8", errors="replace")
        result["managed_report_readable"] = analysis.returncode == 0 and "[ERROR]" not in report_text and bool(report_text.strip())
    godot_output = (out / "godot-stdout.log").read_text(encoding="utf-8", errors="replace")
    result["godot_script_profile_confirmed"] = "BEGIN PROFILING" in godot_output
    result["native_profiling_requested"] = args.native_profiling
    result["gpu_profile_confirmed"] = "GPU PROFILE" in godot_output
    write_json(out / "result.json", result)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return game_exit or collector_exit or (0 if result.get("managed_report_readable") else 1)


if __name__ == "__main__":
    raise SystemExit(main())
