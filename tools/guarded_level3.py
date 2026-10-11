"""Run the real detective session against a sealed source/measurement baseline.

No state injection, answer selection, time conversion, or baseline regeneration.
The committed manifest is the authority; changed sources stop the run.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
SEAL = 'artifacts/detective-agent/chapter2-20261011/timing-optimization/protection/manifest.json'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_paths():
    result = subprocess.run(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'],
                            cwd=ROOT, capture_output=True, check=True)
    return {name for name in result.stdout.decode('utf-8').split('\0') if name
            and (name.startswith(('csharp/', 'godot_csharp/', 'tools/', 'docs/')) or name == 'AGENTS.md')
            and (ROOT / name).is_file()}


def check():
    committed = subprocess.run(['git', 'show', 'HEAD:' + SEAL], cwd=ROOT, capture_output=True, check=True).stdout
    if json.loads(committed) != json.loads((ROOT / SEAL).read_bytes()):
        raise RuntimeError('Protection manifest differs from its committed anchor.')
    files = json.loads(committed)['files']
    changed = [name for name, sha in files.items() if not (ROOT / name).is_file() or digest(ROOT / name) != sha]
    added = sorted(source_paths() - files.keys())
    if changed or added:
        raise RuntimeError('Protected baseline changed: ' + json.dumps({'changed': changed, 'added': added}, ensure_ascii=False))
    return {'manifest_sha256': hashlib.sha256(committed).hexdigest(), 'sources_unchanged': True}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--name', required=True)
    parser.add_argument('--max-seconds', type=float, default=600)
    args = parser.parse_args()
    if Path(args.name).name != args.name or args.name in ('.', '..'):
        raise ValueError('Use a plain session name.')
    out = ROOT / 'artifacts/detective-agent/chapter2-20261011/timing-optimization/runs' / args.name
    if out.exists():
        raise RuntimeError('Refusing to overwrite evidence.')
    start = check()
    out.mkdir(parents=True)
    (out / 'guard-start.json').write_text(json.dumps(start, indent=2), encoding='utf-8')
    command = [sys.executable, str(ROOT / 'godot_csharp/tools/full_test.py'), '--name', args.name,
               '--seed', '0', '--time-scale', '4', '--max-seconds', str(args.max_seconds),
               '--no-progress-seconds', '60', '--shot-seconds', '10', '--line-seconds', '0.6']
    (out / 'command.json').write_text(json.dumps(command, ensure_ascii=False, indent=2), encoding='utf-8')
    with (out / 'runner.log').open('wb') as stream:
        result = subprocess.run(command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT)
    raw = ROOT / 'tmp/full-test' / args.name
    if raw.exists():
        shutil.copytree(raw, out / 'raw')
    log = raw.with_suffix('.log')
    if log.exists():
        shutil.copyfile(log, out / 'godot.log')
    manifest = {p.relative_to(out).as_posix(): digest(p) for p in out.rglob('*') if p.is_file()}
    (out / 'evidence-sha256.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    try:
        ending = check()
    except Exception as error:
        (out / 'INVALID.json').write_text(json.dumps({'reason': str(error)}), encoding='utf-8')
        raise
    ending['runner_exit'] = result.returncode
    (out / 'guard-end.json').write_text(json.dumps(ending, indent=2), encoding='utf-8')
    print(json.dumps({'evidence': str(out), **ending}, ensure_ascii=False))


if __name__ == '__main__':
    main()
