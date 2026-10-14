"""Encode rendered 5 Hz samples on their measured real timeline, without rerunning."""
import argparse
import json
import pathlib
import shutil
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('directory', type=pathlib.Path)
args = parser.parse_args()
directory = args.directory.resolve()
target = directory / 'recording.mp4'
if target.exists():
    raise SystemExit('Refusing to overwrite an existing recording')
rows = [json.loads(line) for line in (directory / 'frames.jsonl').read_text(encoding='utf-8').splitlines()]
if not rows:
    raise SystemExit('No actual rendered frames')
concat = directory / 'recording.ffconcat'
with concat.open('w', encoding='utf-8', newline='\n') as stream:
    stream.write('ffconcat version 1.0\n')
    for index, row in enumerate(rows):
        name = row['file'].replace("'", "'\\''")
        stream.write(f"file '{name}'\n")
        duration = (rows[index + 1]['real_ms'] - row['real_ms']) / 1000 if index + 1 < len(rows) else 0.2
        stream.write(f'duration {duration:.6f}\n')
    stream.write(f"file '{rows[-1]['file']}'\n")
subprocess.run([shutil.which('ffmpeg') or 'ffmpeg', '-nostdin', '-v', 'warning', '-n',
                '-f', 'concat', '-safe', '0', '-i', str(concat), '-vf', 'fps=5',
                '-c:v', 'libx264', '-preset', 'fast', '-crf', '20', '-pix_fmt', 'yuv420p',
                '-movflags', '+faststart', str(target)], check=True)
gaps = [b['real_ms'] - a['real_ms'] for a, b in zip(rows, rows[1:])]
(directory / 'recording-metadata.json').write_text(json.dumps({
    'capture_target_hz': 5, 'video_fps': 5, 'time_basis': 'actual real_ms, no game-speed conversion',
    'first_real_ms': rows[0]['real_ms'], 'last_real_ms': rows[-1]['real_ms'],
    'max_capture_gap_ms': max(gaps, default=0),
    'resampling': '5fps output holds the last actually captured image between samples',
    'video': target.name}, indent=2) + '\n', encoding='utf-8')
print(target)
