"""Task-local serial orchestration. All decisions remain in the formal DV session."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
FROZEN = json.loads((OUT / 'source-counterfactual-frozen.json').read_text(encoding='utf-8'))
CONFIGURATIONS = [(zone, profile) for zone in ('yi', 'jia', 'bing', 'ding')
                  for profile in ('Standard_Detective', 'Foolish_Detective')]


def changed():
    return [name for name, digest in FROZEN.items()
            if not (ROOT / name).is_file()
            or hashlib.sha256((ROOT / name).read_bytes()).hexdigest() != digest]


def main():
    with (OUT / 'matrix-journal.jsonl').open('x', encoding='utf-8') as journal:
        for zone, profile in CONFIGURATIONS:
            differences = changed()
            if differences:
                raise RuntimeError('Frozen source changed; matrix stopped: ' + repr(differences))
            name = ('standard' if profile == 'Standard_Detective' else 'foolish') + '-' + zone
            destination = OUT / 'counterfactual-matrix' / name
            command = [sys.executable, str(ROOT / 'language_core/tools/run_debug.py'),
                       '--out', str(destination), '--detective-profile', profile,
                       '--detective-zone', 'zone_' + zone, '--trace-compression', 'gzip', '--timeout', '180']
            started = time.monotonic()
            event = {'configuration': name, 'command': command, 'stage': 'started',
                     'source_freeze': 'source-counterfactual-frozen.json'}
            journal.write(json.dumps(event) + '\n'); journal.flush()
            print(json.dumps(event), flush=True)
            with (OUT / (name + '-matrix-launch.log')).open('xb') as log:
                process = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
            path = destination / 'result.json'
            result = json.loads(path.read_text(encoding='utf-8')) if path.is_file() else None
            event = {'configuration': name, 'stage': 'finished', 'runner_exit': process.returncode,
                     'orchestration_seconds_including_build_and_export': time.monotonic() - started,
                     'result': result, 'frozen_source_differences': changed()}
            journal.write(json.dumps(event) + '\n'); journal.flush()
            print(json.dumps({k: v for k, v in event.items() if k != 'result'}), flush=True)
            if event['frozen_source_differences']:
                raise RuntimeError('Source changed during run; evidence is invalid for this freeze')


if __name__ == '__main__':
    main()
