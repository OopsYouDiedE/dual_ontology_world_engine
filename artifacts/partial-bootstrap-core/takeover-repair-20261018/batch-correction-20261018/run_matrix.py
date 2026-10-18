"""Freeze reviewed sources, then serially record independent formal sessions.

Neither mode supplies player input, answers, budgets, or narrative state.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time
import zipfile

ROOT = Path(__file__).resolve().parents[4]
TASK = Path(__file__).resolve().parent
FREEZE = TASK / 'source-frozen.json'
OUT = TASK / 'matrix'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_paths():
    names = subprocess.check_output(
        ['git', 'ls-files', '-z', '--', 'language_core', 'godot_csharp',
         'docs', 'AGENTS.md', 'README.md'], cwd=ROOT).decode('utf-8').split('\0')
    extensions = {'.dv', '.cs', '.py', '.md', '.json', '.csproj', '.sln',
                  '.props', '.targets', '.story', '.csv', '.po', '.tscn', '.gd', '.godot'}
    paths = {name for name in names if name and Path(name).suffix in extensions
             and (ROOT / name).is_file()}
    paths.add('language_core/tools/raw_dag.py')
    paths.add(str(Path(__file__).resolve().relative_to(ROOT)).replace('\\', '/'))
    return sorted(paths)


def freeze():
    paths = source_paths()
    manifest = {
        'created_utc': datetime.now(timezone.utc).isoformat(),
        'base_commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip(),
        'scope': 'reviewed batch source snapshot; not a historical performance baseline',
        'files': {name: sha(ROOT / name) for name in paths},
    }
    with zipfile.ZipFile(TASK / 'source-frozen.zip', 'x', zipfile.ZIP_DEFLATED) as archive:
        for name in paths:
            archive.write(ROOT / name, name)
    with FREEZE.open('x', encoding='utf-8') as output:
        json.dump(manifest, output, ensure_ascii=False, indent=2)
    print('Reviewed source snapshot sealed.', flush=True)


def run():
    frozen = json.loads(FREEZE.read_text(encoding='utf-8'))['files']

    def changed():
        return [p for p, digest in frozen.items()
                if not (ROOT / p).is_file() or sha(ROOT / p) != digest]

    OUT.mkdir(exist_ok=False)
    with (OUT / 'journal.jsonl').open('x', encoding='utf-8') as journal:
        for zone in ('yi', 'jia', 'bing', 'ding'):
            for profile in ('Standard_Detective', 'Foolish_Detective'):
                differences = changed()
                if differences:
                    raise RuntimeError('Frozen source changed: ' + repr(differences))
                name = ('standard' if profile == 'Standard_Detective' else 'foolish') + '-' + zone
                destination = OUT / name
                command = [sys.executable, str(ROOT / 'language_core/tools/run_debug.py'),
                           '--out', str(destination), '--detective-profile', profile,
                           '--detective-zone', 'zone_' + zone, '--raw-format', 'dag',
                           '--timeout', '180']
                start = time.monotonic()
                event = {'configuration': name, 'stage': 'started', 'command': command,
                         'source_freeze': str(FREEZE.relative_to(ROOT)), 'source_sha256': sha(FREEZE)}
                journal.write(json.dumps(event) + '\n'); journal.flush()
                print(json.dumps(event), flush=True)
                with (OUT / (name + '-launch.log')).open('xb') as log:
                    process = subprocess.run(command, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
                result_path = destination / 'result.json'
                result = json.loads(result_path.read_text(encoding='utf-8')) if result_path.is_file() else None
                runtime_paths = ('program/bin/Release/net8.0/Program.dll', 'program/DVProgram.cs',
                                 'program/Runtime.cs', 'program/source-manifest.json')
                runtime = {p: sha(destination / p) for p in runtime_paths if (destination / p).is_file()}
                with (destination / 'runtime-fingerprints.json').open('x', encoding='utf-8') as output:
                    json.dump(runtime, output, indent=2)
                event = {'configuration': name, 'stage': 'finished', 'runner_exit': process.returncode,
                         'orchestration_seconds_including_build_and_export': time.monotonic() - start,
                         'result': result, 'frozen_source_differences': changed()}
                journal.write(json.dumps(event) + '\n'); journal.flush()
                print(json.dumps({k: v for k, v in event.items() if k != 'result'}), flush=True)
                if changed() or result is None or result.get('exit_code', 0) != 0:
                    raise RuntimeError('Changed source or engineering execution failure; remaining cases not started')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=('freeze', 'run'))
    args = parser.parse_args()
    freeze() if args.mode == 'freeze' else run()
