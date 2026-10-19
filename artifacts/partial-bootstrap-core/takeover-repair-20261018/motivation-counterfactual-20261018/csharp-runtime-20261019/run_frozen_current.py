"""Freeze the integrated sources, then run one bounded fresh formal-model session."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
parser = argparse.ArgumentParser()
parser.add_argument('--name', required=True, help='New child directory for this freeze')
args = parser.parse_args()
if Path(args.name).name != args.name or args.name in {'.', '..'}:
    raise ValueError('name must be a simple new directory name')
frozen = HERE / args.name
frozen.mkdir(exist_ok=False)
source = frozen / 'source'
source.mkdir()
shutil.copytree(ROOT / 'language_core', source / 'language_core',
                ignore=shutil.ignore_patterns('bin', 'obj', '__pycache__', 'node_modules', '.vs'))
(source / 'docs').mkdir()
for name in ['AGENTS.md', 'docs/design-standard.md']:
    shutil.copyfile(ROOT / name, source / name)
hashes = {p.relative_to(source).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
          for p in sorted(source.rglob('*')) if p.is_file()}
(frozen / 'source-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
origin = {
    'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
    'scope': 'integrated corrected DV plus readonly-array runtime; fresh formal-model; no Godot geometry; changed rules are not same-condition comparison with frozen-01/02/03',
    'tracked_dirty': subprocess.check_output(['git', 'diff', '--name-only'], cwd=ROOT, text=True).splitlines(),
}
(frozen / 'origin.json').write_text(json.dumps(origin, indent=2), encoding='utf-8')
(frozen / 'working-diff.patch').write_bytes(subprocess.check_output(['git', 'diff', '--', 'language_core'], cwd=ROOT))
command = [sys.executable, str(source / 'language_core/tools/run_debug.py'),
           '--out', str(frozen / 'standard-yi'), '--timeout', '240',
           '--raw-format', 'dag', '--projection-every', '32']
(frozen / 'command.json').write_text(json.dumps(command, indent=2), encoding='utf-8')
print('Integrated sources frozen; one fresh 240-second formal-model window.', flush=True)
start = time.monotonic()
with (frozen / 'driver.stdout.txt').open('xb') as output, (frozen / 'driver.stderr.txt').open('xb') as errors:
    process = subprocess.run(command, cwd=source, env=os.environ.copy(), stdout=output, stderr=errors)
changes = [name for name, digest in hashes.items()
           if not (source / name).is_file() or hashlib.sha256((source / name).read_bytes()).hexdigest() != digest]
result = {'exit_code': process.returncode, 'driver_wall_seconds': time.monotonic() - start,
          'changed_frozen_sources': changes,
          'workspace_changes_since_freeze': [name for name, digest in hashes.items()
              if (ROOT / name).is_file() and hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != digest]}
(frozen / 'freeze-result.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2), flush=True)
if (frozen / 'standard-yi/result.json').exists():
    print((frozen / 'standard-yi/result.json').read_text(encoding='utf-8'), flush=True)
raise SystemExit(process.returncode)
