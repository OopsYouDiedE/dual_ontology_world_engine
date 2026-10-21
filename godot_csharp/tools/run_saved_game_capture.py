"""Rendered production-host capture with exclusive evidence and reusable bookmarks."""
from __future__ import annotations
import argparse
import datetime
import hashlib
import json
import math
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "language_core/tools"))
from binary_evidence import RecordWriter, records

def fingerprint():
    names = subprocess.check_output(
        ["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard",
         "godot_csharp", "language_core"], cwd=ROOT).decode().split("\0")
    extensions = {".gd", ".gdshader", ".cs", ".csproj", ".tscn", ".tres", ".dv",
                  ".py", ".godot", ".json", ".png", ".webp", ".glb", ".ttf", ".otf",
                  ".ogg", ".wav", ".translation", ".po", ".svg", ".jpg", ".jpeg", ".res"}
    paths = {ROOT / n for n in names if n and Path(n).suffix.lower() in extensions}
    paths.update((ROOT / "godot_csharp/.godot/mono/temp/bin/Debug").glob("*.dll"))
    # Generated runtime audio is ignored by Git but still affects this run.
    for extension in ("*.ogg", "*.wav"):
        paths.update((ROOT / "godot_csharp/ShareAsset").rglob(extension))
    return {str(q.relative_to(ROOT)): hashlib.sha256(q.read_bytes()).hexdigest()
            for q in sorted(paths) if q.is_file()}

def user_sessions():
    if os.name == "nt":
        data = Path(os.environ["APPDATA"]) / "Godot"
    elif sys.platform == "darwin":
        data = Path.home() / "Library/Application Support/Godot"
    else:
        data = Path(os.environ.get("XDG_DATA_HOME", str(Path.home() / ".local/share"))) / "godot"
    return data / "app_userdata/Dual Ontology World Engine/dv-sessions"

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
    if any(out.is_relative_to(ROOT / name) for name in ("godot_csharp", "language_core")):
        parser.error("Keep evidence outside the fingerprinted source directories")
    out.mkdir(parents=True, exist_ok=False)
    before = fingerprint()
    (out / "source-before.json").write_text(json.dumps(before, ensure_ascii=False, indent=2), encoding="utf-8")
    sessions = user_sessions()
    old = {q.name for q in sessions.glob("*")}
    command = [str(godot), "--path", str(ROOT / "godot_csharp"), "--log-file",
               str(out / "godot-native.log"), "--ignore-error-breaks", "--script",
               "res://tools/capture_saved_game.gd", "--", f"--out={out / 'run'}",
               f"--mode={args.mode}", f"--seconds={args.seconds}",
               f"--dv-controller={'ai' if args.mode == 'detective' else 'player'}",
               f"--dv-speed={args.speed}", f"--window-mode={args.window_mode}"]
    if not args.title and not args.load:
        command.append("--dv-auto-start")
    if args.load:
        command.append("--load-save=" + str(Path(args.load).resolve()))
    if args.checkpoint:
        command.append("--checkpoint-at=" + args.checkpoint)
    launch = {"started_at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "command": command, "start_mode": "restore" if args.load else "title" if args.title else "new",
              "scope": "Rendered production host; normal logging; no managed profiler",
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
        try:
            code = process.wait(timeout=args.seconds + 180)
        except subprocess.TimeoutExpired:
            timed_out = True
            process.terminate()  # Only the runtime process owned by this invocation.
            code = process.wait(timeout=15)
    after = fingerprint()
    (out / "source-after.json").write_text(json.dumps(after, ensure_ascii=False, indent=2), encoding="utf-8")
    new = [q for q in sessions.glob("*") if q.name not in old]
    close_results = []
    for i, directory in enumerate(new):
        target = out / ("host-session" if len(new) == 1 else f"host-session-{i}")
        shutil.copytree(directory, target)
        if (target / "close-result.dvlog").exists():
            close_results.extend(records(target / "close-result.dvlog"))
    logs = "\n".join(q.read_text(encoding="utf-8", errors="replace")
                     for q in (out / "godot-native.log", out / "stderr.log") if q.exists())
    result = {"exit_code": code, "elapsed_seconds": time.monotonic() - started,
              "source_changes": [k for k in before.keys() | after.keys() if before.get(k) != after.get(k)],
              "host_sessions": [str(q) for q in new], "timeout": timed_out,
              "errors": [s for s in logs.splitlines() if s.lstrip().startswith(("ERROR:", "SCRIPT ERROR:"))],
              "warnings": [s for s in logs.splitlines() if s.lstrip().startswith("WARNING:")],
              "close_complete": len(new) == 1 and len(close_results) == 1 and all(r.get("complete") for r in close_results),
              "checkpoints_complete": not checkpoints, "capture_duration_complete": False}
    end_file = out / "run/end.dvlog"
    if end_file.exists():
        end = next(records(end_file))
        snapshot = end.get("snapshot", {})
        result.update(capture_end_real_ms=end["real_ms"], game_time=snapshot.get("game_time"),
                      location=snapshot.get("logical_location"), chapter=snapshot.get("chapter"),
                      pending_status=snapshot.get("pending", {}).get("status"),
                      capture_duration_complete=end["real_ms"] >= args.seconds * 1000 - 50)
    checkpoint_file = out / "run/checkpoint-summary.dvlog"
    if checkpoint_file.exists():
        result["checkpoints"] = next(records(checkpoint_file))
        due = len(checkpoints)
        result["checkpoints_complete"] = result["checkpoints"].get("saved", 0) >= due and not result["checkpoints"].get("copy_failed", False)
    event_file = out / "run/host-events.dvlog"
    result["load_errors"] = [event.get("message", "") for row in records(event_file)
                             for event in row["events"] if event.get("kind") == "load_error"] if event_file.exists() else []
    with RecordWriter(out / "result.dvlog") as writer:
        writer.append(result)
    print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)
    return int(bool(code or timed_out or result["errors"] or result["load_errors"] or result["source_changes"]
                    or not result["close_complete"] or not end_file.exists() or not result["capture_duration_complete"]
                    or not result["checkpoints_complete"]))

if __name__ == "__main__":
    raise SystemExit(main())
