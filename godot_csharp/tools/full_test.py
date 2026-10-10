"""Run one full test: the real game window with the AI in the player seat.

Wraps scenes/Shared/check/FullTest/FullTest.tscn with an outside watchdog: if Godot itself hangs past the
wall budget (plus a grace period) the process is killed and the run is marked so.
Only one Godot at a time; run from the repository root.

python godot_csharp/tools/full_test.py --name run-02 [--max-seconds 1800] [--no-progress-seconds 60]
"""
from pathlib import Path
import argparse
import json
import os
import subprocess
import sys
import time

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parents[2]
GODOT = os.environ.get("GODOT", r"C:\Users\Administrator\AppData\Local\Programs\Godot\4.6.1-mono\Godot_v4.6.1-stable_mono_win64_console.exe")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--name", required=True)
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--max-seconds", type=float, default=1800)
    parser.add_argument("--no-progress-seconds", type=float, default=60)
    parser.add_argument("--shot-seconds", type=float, default=5)
    parser.add_argument("--line-seconds", type=float, default=0.6)
    parser.add_argument("--time-scale", type=float, default=1)
    args = parser.parse_args()
    out = ROOT / "tmp" / "full-test" / args.name
    if out.exists():
        sys.exit(f"output exists: {out}")
    appdata = ROOT / "tmp" / "appdata"
    appdata.mkdir(parents=True, exist_ok=True)
    env = {**os.environ, "APPDATA": str(appdata), "LOCALAPPDATA": str(appdata)}
    command = [GODOT, "--path", str(ROOT / "godot_csharp"), "res://scenes/Shared/check/FullTest/FullTest.tscn", "--",
               f"--output={out}", f"--seed={args.seed}", f"--max-seconds={args.max_seconds}",
               f"--no-progress-seconds={args.no_progress_seconds}", f"--shot-seconds={args.shot_seconds}",
               f"--line-seconds={args.line_seconds}", f"--time-scale={args.time_scale}"]
    log = ROOT / "tmp" / "full-test" / f"{args.name}.log"
    log.parent.mkdir(parents=True, exist_ok=True)
    started = time.time()
    with log.open("w", encoding="utf-8") as stream:
        process = subprocess.Popen(command, stdout=stream, stderr=subprocess.STDOUT, env=env)
        try:
            code = process.wait(timeout=args.max_seconds + 120)
        except subprocess.TimeoutExpired:
            process.kill()
            code = "killed_by_watchdog"
    summary_path = out / "summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8")) if summary_path.exists() else {"reason": "no_summary"}
    summary.update({"exit": code, "wall_seconds": round(time.time() - started, 1), "log": str(log)})
    script_errors = [line for line in log.read_text(encoding="utf-8", errors="replace").splitlines() if "SCRIPT ERROR" in line]
    summary["script_errors"] = script_errors[:20]
    print(json.dumps(summary, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
