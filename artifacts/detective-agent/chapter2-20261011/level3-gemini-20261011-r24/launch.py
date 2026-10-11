from __future__ import annotations
from datetime import datetime, timezone, timedelta
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / 'artifacts/detective-agent/chapter2-20261011/level3-gemini-20261011-r24'
NAME = 'gemini-level3-20261011-r24'
GODOT = r'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe'
ENV = dict(os.environ, GODOT=GODOT, PYTHONIOENCODING='utf-8', DOTNET_CLI_UI_LANGUAGE='en')


def dump(path: Path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sources():
    data = subprocess.run(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=ROOT, capture_output=True, check=True).stdout
    return {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
        for name in sorted(set(data.decode('utf-8').split('\0')))
        if name and (ROOT / name).is_file() and not name.startswith(('artifacts/', 'tmp/', 'scratch/'))}


def run(label: str, command: list[str], timeout: int):
    started = time.monotonic()
    print(label + ' started', flush=True)
    try:
        result = subprocess.run(command, cwd=ROOT, env=ENV, capture_output=True, timeout=timeout)
        stdout, stderr, code = result.stdout, result.stderr, result.returncode
    except subprocess.TimeoutExpired as error:
        stdout, stderr, code = error.stdout or b'', error.stderr or b'', 'timeout'
    (EVIDENCE / (label + '.stdout.log')).write_bytes(stdout)
    (EVIDENCE / (label + '.stderr.log')).write_bytes(stderr)
    dump(EVIDENCE / (label + '.json'), {'command': command, 'exit': code, 'wall_seconds': time.monotonic() - started})
    print(label + ' finished: ' + str(code), flush=True)
    return code, stdout + stderr


def main():
    session = ROOT / 'tmp/full-test' / NAME
    if session.exists() or EVIDENCE.exists():
        raise RuntimeError('Refusing to replace existing evidence')
    EVIDENCE.mkdir(parents=True)
    (EVIDENCE / '.gitattributes').write_text('*.log -text -diff\n*.jsonl -text -diff\n', encoding='utf-8')
    (EVIDENCE / '.gitignore').write_text('raw/\n*.log\n', encoding='utf-8')
    shutil.copyfile(__file__, EVIDENCE / 'launch.py')
    beginning = sources()
    dump(EVIDENCE / 'sources-start.json', beginning)
    dump(EVIDENCE / 'session.json', {
        'level': 3, 'mode': 'engine_detective', 'seed': 0, 'time_scale': 4,
        'max_seconds': 180, 'no_progress_seconds': 60, 'line_seconds': 0.6, 'shot_seconds': 10,
        'started_at': datetime.now(timezone(timedelta(hours=8))).isoformat(),
        'head': subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=ROOT, capture_output=True, check=True).stdout.decode().strip(),
        'original_session': str(session), 'godot': GODOT})
    (EVIDENCE / 'initial-status.log').write_bytes(subprocess.run(['git', 'status', '--porcelain=v1', '-uall'], cwd=ROOT, capture_output=True, check=True).stdout)
    for label, command, timeout in [
        ('build', ['dotnet', 'build', 'godot_csharp/DualOntologyWorldEngine.sln', '-m:1', '--disable-build-servers'], 180),
        ('story-consistency', [sys.executable, 'godot_csharp/tools/compile_story.py', '--check'], 180),
        ('import', [GODOT, '--headless', '--path', str(ROOT / 'godot_csharp'), '--import'], 240)]:
        code, output = run(label, command, timeout)
        if code != 0 or b'SCRIPT ERROR' in output or b'ERROR:' in output:
            dump(EVIDENCE / 'preparation-error.json', {'step': label, 'exit': code})
            return 1
    run('session-run', [sys.executable, 'godot_csharp/tools/full_test.py', '--name', NAME,
        '--seed', '0', '--time-scale', '4', '--max-seconds', '180', '--no-progress-seconds', '60',
        '--shot-seconds', '10', '--line-seconds', '0.6'], 320)
    raw = EVIDENCE / 'raw'
    if session.exists():
        shutil.copytree(session, raw)
    source_log = session.with_suffix('.log')
    if source_log.exists():
        shutil.copyfile(source_log, EVIDENCE / 'godot.log')
    manifest = {}
    if raw.exists():
        for path in sorted(raw.rglob('*')):
            if not path.is_file():
                continue
            relative = path.relative_to(raw)
            content = path.read_bytes()
            if content != (session / relative).read_bytes():
                raise RuntimeError('Evidence archive changed: ' + str(relative))
            manifest[relative.as_posix()] = {'bytes': len(content), 'sha256': hashlib.sha256(content).hexdigest()}
    dump(EVIDENCE / 'raw-manifest.json', {'copy_checked_byte_for_byte': True, 'files': manifest})
    ending = sources()
    dump(EVIDENCE / 'sources-end.json', ending)
    dump(EVIDENCE / 'source-changes.json', {'changed': [name for name in beginning if ending.get(name) != beginning[name]],
        'added': [name for name in ending if name not in beginning]})
    summary = raw / 'summary.json'
    if summary.exists():
        value = json.loads(summary.read_text(encoding='utf-8'))
        print(json.dumps({key: value.get(key) for key in ('reason', 'elapsed_seconds', 'time', 'visible_position')}, ensure_ascii=False), flush=True)
    else:
        print('No completion summary; inspect the retained trace and log.', flush=True)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
