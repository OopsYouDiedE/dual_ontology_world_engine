"""Serial, source-frozen formal sessions. No player choices are made here."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[3]
TASK = Path(__file__).resolve().parent
OUT = TASK / 'counterfactual-final'
FREEZE = TASK / 'source-final-frozen.json'


def main():
    frozen = json.loads(FREEZE.read_text(encoding='utf-8'))
    def changed():
        return [p for p, digest in frozen.items()
                if not (ROOT / p).is_file()
                or hashlib.sha256((ROOT / p).read_bytes()).hexdigest() != digest]
    OUT.mkdir(exist_ok=False)
    with (OUT / 'journal.jsonl').open('x', encoding='utf-8') as journal:
        for zone in ('yi', 'jia', 'bing', 'ding'):
            for profile in ('Standard_Detective', 'Foolish_Detective'):
                if changed():
                    raise RuntimeError('Frozen source changed: ' + repr(changed()))
                name = ('standard' if profile == 'Standard_Detective' else 'foolish') + '-' + zone
                destination = OUT / name
                command = [sys.executable, str(ROOT / 'language_core/tools/run_debug.py'),
                           '--out', str(destination), '--detective-profile', profile,
                           '--detective-zone', 'zone_' + zone,
                           '--trace-compression', 'gzip', '--timeout', '180']
                started = time.monotonic()
                event = {'configuration': name, 'stage': 'started', 'command': command,
                         'source_freeze': str(FREEZE.relative_to(ROOT))}
                journal.write(json.dumps(event) + '\n'); journal.flush()
                print(json.dumps(event), flush=True)
                with (OUT / (name + '-launch.log')).open('xb') as log:
                    process = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
                result_path = destination / 'result.json'
                result = json.loads(result_path.read_text(encoding='utf-8')) if result_path.is_file() else None
                runtime_paths = ('program/bin/Release/net8.0/Program.dll', 'program/DVProgram.cs',
                                 'program/Runtime.cs', 'program/source-manifest.json')
                runtime = {p: hashlib.sha256((destination / p).read_bytes()).hexdigest()
                           for p in runtime_paths if (destination / p).is_file()}
                with (destination / 'runtime-fingerprints.json').open('x', encoding='utf-8') as output:
                    json.dump(runtime, output, indent=2)
                event = {'configuration': name, 'stage': 'finished', 'runner_exit': process.returncode,
                         'orchestration_seconds_including_build_and_export': time.monotonic() - started,
                         'result': result, 'frozen_source_differences': changed()}
                journal.write(json.dumps(event) + '\n'); journal.flush()
                print(json.dumps({k: v for k, v in event.items() if k != 'result'}), flush=True)
                if changed():
                    raise RuntimeError('Source changed during run; remaining configurations not started')


if __name__ == '__main__':
    main()
