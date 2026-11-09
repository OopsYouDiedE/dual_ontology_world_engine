#!/usr/bin/env python3
"""Run real Godot engine-AI session with movie recording and report generation."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "mcp/game"))
import automation

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--duration-seconds", type=float, default=60.0)
    parser.add_argument("--movie-filename", default="patch_smoke_recording.avi")
    parser.add_argument("--movie-fps", type=int, default=30)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--viewer", default="detective")
    parser.add_argument("--output-mp4", default=str(ROOT / "artifacts/recordings/patch_smoke_recording.mp4"))
    args = parser.parse_args()

    avi_path = ROOT / "artifacts/recordings" / args.movie_filename
    if avi_path.exists():
        avi_path.unlink()

    spec = {
        "duration_seconds": args.duration_seconds,
        "seed": args.seed,
        "viewer": args.viewer,
        "movie_filename": args.movie_filename,
        "movie_fps": args.movie_fps,
    }

    print(f"Starting Godot real session with recording: duration={args.duration_seconds}s, fps={args.movie_fps}...", flush=True)
    t0 = time.time()
    result = automation.tool("godot", spec)
    elapsed = time.time() - t0
    print(f"Godot session completed in {elapsed:.1f}s", flush=True)
    print("STATUS:", result.get("status"))
    print("DETAIL:", result.get("detail"))
    print("OBSERVATIONS:", result.get("observations"))
    print("COMPLETED ACTIONS:", result.get("completed_actions"))
    print("TOTAL RECEIPTS:", result.get("total_action_receipts"))

    # Check movie file
    if avi_path.exists() and avi_path.stat().st_size > 0:
        size_mb = avi_path.stat().st_size / (1024 * 1024)
        print(f"AVI movie generated: {avi_path} ({size_mb:.2f} MB)", flush=True)
        ffmpeg = r"C:\Users\iii\Documents\ffmpeg\bin\ffmpeg.exe"
        mp4_path = Path(args.output_mp4)
        mp4_path.parent.mkdir(parents=True, exist_ok=True)
        conv_cmd = [
            ffmpeg, "-y", "-i", str(avi_path),
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            str(mp4_path)
        ]
        print(f"Transcoding AVI to MP4 via ffmpeg...", flush=True)
        ret = subprocess.run(conv_cmd, capture_output=True, text=True)
        if ret.returncode == 0 and mp4_path.exists():
            mp4_mb = mp4_path.stat().st_size / (1024 * 1024)
            print(f"MP4 recording successfully created: {mp4_path} ({mp4_mb:.2f} MB)", flush=True)
        else:
            print(f"Transcode warning: returncode={ret.returncode}\n{ret.stderr[-1000:]}", flush=True)
    else:
        print(f"Notice: Movie file not found at {avi_path} or empty. Checking screenshots...", flush=True)

if __name__ == "__main__":
    main()
