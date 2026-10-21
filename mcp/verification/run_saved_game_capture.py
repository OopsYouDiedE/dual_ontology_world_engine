"""Rendered production-host capture with exclusive evidence and reusable bookmarks."""
from __future__ import annotations
import argparse
import datetime
import json
import math
import os
import shutil
import subprocess
import time
from pathlib import Path

from binary_evidence import RecordWriter
from capture_result import collect_capture_result, wait_owned
from capture_workspace import ROOT, fingerprint, prepare_capture, user_sessions

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default=os.environ.get("GODOT") or shutil.which("godot"))
    parser.add_argument("--out", required=True)
    parser.add_argument("--seconds", type=float, default=90)
    parser.add_argument("--mode", choices=["detective", "player"], default="detective")
    parser.add_argument("--speed", type=float, default=1)
    parser.add_argument("--load", help="Existing .dvsave; its saved controller is preserved")
    parser.add_argument("--checkpoint", help="Comma-separated real seconds to request safe bookmarks")
    parser.add_argument("--title", action="store_true", help="Wait on the production title UI")
    parser.add_argument("--window-mode", choices=["offscreen", "window"], default="offscreen")
    args = parser.parse_args()
    if not args.godot or not Path(args.godot).is_file():
        parser.error("Pass --godot or set GODOT to the Godot .NET executable")
    if not math.isfinite(args.seconds) or args.seconds <= 0 or not 0.25 <= args.speed <= 8:
        parser.error("Require finite positive --seconds and --speed between 0.25 and 8")
    if args.title and args.load:
        parser.error("--title and --load are mutually exclusive")
    checkpoints = []
    if args.checkpoint:
        try:
            checkpoints = [float(value) for value in args.checkpoint.split(",")]
            if any(not 0 <= value < args.seconds for value in checkpoints):
                raise ValueError()
        except ValueError:
            parser.error("--checkpoint requires finite nonnegative seconds before the capture end")
    godot = Path(args.godot).resolve()
    if godot.stem.endswith("_console"):
        actual = godot.with_name(godot.stem.removesuffix("_console") + godot.suffix)
        if actual.exists():
            godot = actual
    out = (ROOT / args.out).resolve()
    if any(out.is_relative_to(ROOT / name) for name in ("godot_csharp", "dv_core", "mcp")):
        parser.error("Keep evidence outside the fingerprinted source directories")
    out.mkdir(parents=True, exist_ok=False)
    work = prepare_capture(godot, out)
    before = fingerprint(work)
    (out / "source-before.json").write_text(json.dumps(before, ensure_ascii=False, indent=2), encoding="utf-8")
    sessions = user_sessions()
    old = {q.name for q in sessions.glob("*")}
    command = [str(godot), "--path", str(work), "--log-file",
               str(out / "godot-native.log"), "--ignore-error-breaks", "--script",
               "res://tools/capture_saved_game.gd", "--", f"--out={out / 'run'}",
               f"--mode={args.mode}", f"--seconds={args.seconds}",
               f"--dv-controller={'ai' if args.mode == 'detective' else 'player'}",
               f"--dv-speed={args.speed}", "--dv-audit", f"--window-mode={args.window_mode}"]
    if not args.title and not args.load:
        command.append("--dv-auto-start")
    if args.load:
        command.append("--load-save=" + str(Path(args.load).resolve()))
    if args.checkpoint:
        command.append("--checkpoint-at=" + args.checkpoint)
    launch = {"started_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "command": command, "start_mode": "restore" if args.load else "title" if args.title else "new",
              "scope": "Rendered production host in a tool workspace; full audit; no managed profiler",
              "tool_workspace": str(work), "audit_mode": True,
              "source_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip()}
    started = time.monotonic()
    timed_out = False
    with (out / "stdout.log").open("wb") as stdout, (out / "stderr.log").open("wb") as stderr:
        process = subprocess.Popen(command, cwd=ROOT, stdout=stdout, stderr=stderr,
                                   stdin=subprocess.DEVNULL,
                                   creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        launch["pid"] = process.pid
        (out / "launch.json").write_text(json.dumps(launch, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps({"pid": process.pid, "out": str(out)}), flush=True)
        code, timed_out = wait_owned(process, started + args.seconds + 180.0)

    after = fingerprint(work)
    (out / "source-after.json").write_text(json.dumps(after, ensure_ascii=False, indent=2), encoding="utf-8")
    result = collect_capture_result(out=out, session_root=sessions, existing=old,
                                    before=before, after=after, seconds=args.seconds,
                                    exit_code=code, timed_out=timed_out, checkpoints=checkpoints)
    result["elapsed_seconds"] = time.monotonic() - started
    result["success"] = result["capture_success"]

    with RecordWriter(out / "result.dvlog") as writer:
        writer.append(result)
    print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)
    return int(not result["success"])


if __name__ == "__main__":
    raise SystemExit(main())
