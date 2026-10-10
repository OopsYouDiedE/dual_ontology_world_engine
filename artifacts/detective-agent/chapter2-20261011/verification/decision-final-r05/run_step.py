import argparse
import hashlib
import json
import pathlib
import subprocess
import sys
import time
from datetime import datetime, timezone

ROOT = pathlib.Path(__file__).resolve().parents[5]
OUT = pathlib.Path(__file__).resolve().parent


def write_new(path, data):
    with path.open('xb') as stream:
        stream.write(data)


def hashes():
    paths = sorted(set((ROOT / 'csharp').glob('*.cs')) |
                   set((ROOT / 'csharp').glob('*.csproj')) |
                   {ROOT / 'csharp' / 'Core.sln', ROOT / 'AGENTS.md',
                    ROOT / 'csharp' / 'AGENTS.md'})
    paths += sorted(OUT.glob('*.cs')) + sorted(OUT.glob('*.csproj'))
    return {str(path.relative_to(ROOT)).replace('\\', '/'):
            hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}


parser = argparse.ArgumentParser()
parser.add_argument('step')
parser.add_argument('command', nargs=argparse.REMAINDER)
args = parser.parse_args()
before = OUT / 'source-before.sha256.json'
if not before.exists():
    write_new(before, json.dumps(hashes(), ensure_ascii=False, indent=2).encode())
if args.step == 'finish':
    write_new(OUT / 'source-after.sha256.json', json.dumps(hashes(), ensure_ascii=False, indent=2).encode())
    previous = json.loads(before.read_bytes())
    current = hashes()
    changed = [path for path in previous if previous[path] != current.get(path)]
    added = [path for path in current if path not in previous]
    print(json.dumps({'source_changed': changed, 'source_added': added}, ensure_ascii=False))
    sys.exit(0 if not changed else 1)
command = args.command
if command and command[0] == '--':
    command = command[1:]
if not command:
    parser.error('command required')
for suffix in ('.stdout.log', '.stderr.log', '.result.json'):
    if (OUT / (args.step + suffix)).exists():
        raise FileExistsError('Evidence must not be overwritten: ' + args.step + suffix)
started = datetime.now(timezone.utc).isoformat()
watch = time.perf_counter()
result = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
elapsed = time.perf_counter() - watch
write_new(OUT / (args.step + '.stdout.log'), result.stdout)
write_new(OUT / (args.step + '.stderr.log'), result.stderr)
summary = {'command': command, 'started_utc': started,
           'elapsed_seconds': elapsed, 'exit_code': result.returncode,
           'stdout_sha256': hashlib.sha256(result.stdout).hexdigest(),
           'stderr_sha256': hashlib.sha256(result.stderr).hexdigest()}
write_new(OUT / (args.step + '.result.json'), json.dumps(summary, ensure_ascii=False, indent=2).encode())
print(json.dumps(summary, ensure_ascii=False))
print(result.stdout.decode('utf-8', errors='replace'))
if result.stderr:
    print(result.stderr.decode('utf-8', errors='replace'), file=sys.stderr)
sys.exit(result.returncode)
