"""Run a timed engine-controlled CLI demonstration without selecting actions."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import queue
import subprocess
import sys
import threading
import time

from binary_evidence import RecordWriter
from capture_cli import stop_tree


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', required=True, type=Path)
    parser.add_argument('--seconds', required=True, type=float)
    parser.add_argument('--grace', type=float, default=90)
    parser.add_argument('--cli', type=Path, default=Path('dv_core/cli/bin/Release/net8.0/dv-cli.dll'))
    args = parser.parse_args()
    if not 0 < args.seconds <= 3600 or not 0 < args.grace <= 300:
        parser.error('seconds must be in (0,3600], grace in (0,300]')
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)
    root = Path(__file__).resolve().parents[2]
    files = sorted(set([*root.glob('dv_core/**/*.cs'), *root.glob('dv_core/modules/*.dv'),
                        *args.cli.resolve().parent.glob('*.dll'), Path(__file__),
                        Path(__file__).with_name('binary_evidence.py'),
                        Path(__file__).with_name('capture_cli.py')]))
    files = [p for p in files if not any(part in ('obj', 'bin') for part in p.relative_to(root).parts)
             or p.parent == args.cli.resolve().parent]
    fingerprints = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
    command = ['dotnet', str(args.cli.resolve()), '--jsonl', '--controller', 'ai',
               '--viewer', 'detective', '--trace', str(output / 'session.dvdag'),
               '--save', str(output / 'session.dvsave'), '--performance', str(output / 'engine')]
    origin = time.monotonic_ns()
    lock = threading.Lock()
    messages = queue.Queue()
    exit_code = 1
    reason = 'not_started'
    advances = 0
    window_start = None
    latest_status = {}
    partial = output / 'console.dvlog.partial'
    with RecordWriter(partial) as writer:
        def record(kind, **values):
            with lock:
                writer.append({'kind': kind, 'real_ns': time.monotonic_ns() - origin, **values})
                writer.stream.flush()

        record('launch', command=command, source_sha256=fingerprints,
               requested_seconds=args.seconds, grace_seconds=args.grace,
               scope='engine AI; fixed detective observer; formal-model; compact recording')
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                   stderr=subprocess.PIPE)

        def pump(pipe, kind):
            pending = b''
            try:
                while True:
                    block = os.read(pipe.fileno(), 65536)
                    if not block:
                        break
                    record(kind, encoding='base64', bytes=base64.b64encode(block).decode('ascii'))
                    if kind == 'stdout':
                        pending += block
                        while b'\n' in pending:
                            line, pending = pending.split(b'\n', 1)
                            if line.strip():
                                try:
                                    messages.put(json.loads(line.decode('utf-8')))
                                except (ValueError, UnicodeError) as error:
                                    messages.put({'kind': 'protocol_error', 'message': str(error)})
            finally:
                if kind == 'stdout':
                    messages.put({'kind': 'eof'})

        pumps = [threading.Thread(target=pump, args=(process.stdout, 'stdout')),
                 threading.Thread(target=pump, args=(process.stderr, 'stderr'))]
        for thread in pumps:
            thread.start()

        def send(command_text):
            raw = (command_text + '\n').encode('utf-8')
            record('stdin', encoding='base64', bytes=base64.b64encode(raw).decode('ascii'))
            process.stdin.write(raw)
            process.stdin.flush()

        def reply(deadline):
            nonlocal latest_status
            while True:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise TimeoutError('In-flight CLI operation exceeded its deadline and grace')
                item = messages.get(timeout=remaining)
                kind = item.get('kind')
                data = item.get('data') or {}
                if kind in ('error', 'protocol_error', 'eof'):
                    raise RuntimeError(str(item))
                if kind == 'status':
                    latest_status = data
                    return item
                if kind == 'intent' or kind == 'interaction' and data.get('phase') == 'focus':
                    return item

        try:
            send('view detective')
            reply(time.monotonic() + args.grace)
            window_start = time.monotonic()
            deadline = window_start + args.seconds
            record('window_start', requested_seconds=args.seconds)
            last_progress = -30.0
            reason = 'window_elapsed'
            while time.monotonic() < deadline:
                if latest_status.get('active') is False:
                    reason = 'session_inactive'
                    break
                send('next')
                advances += 1
                item = reply(deadline + args.grace)
                if item.get('kind') == 'intent':
                    intent = item.get('data') or {}
                    if intent.get('awaiting_input'):
                        reason = 'unexpected_manual_input'
                        break
                    if intent.get('status') in ('stopped', 'closed'):
                        reason = 'session_inactive'
                        break
                elapsed = time.monotonic() - window_start
                if elapsed - last_progress >= 30:
                    last_progress = elapsed
                    print(f'AI window {elapsed:.1f}/{args.seconds:g}s; next inputs {advances}; '
                          f'game time {latest_status.get("game_time", "unknown")}', flush=True)
            record('window_end', reason=reason, elapsed_seconds=time.monotonic() - window_start,
                   next_inputs=advances, last_status=latest_status)
            send('quit')
            process.stdin.close()
            exit_code = process.wait(timeout=args.grace)
        except (Exception, KeyboardInterrupt) as error:
            reason = 'driver_error'
            record('driver_error', error=str(error), next_inputs=advances)
            print(f'Demo stopped: {error}', flush=True)
            if isinstance(error, (TimeoutError, queue.Empty, subprocess.TimeoutExpired, KeyboardInterrupt)):
                stop_tree(process)
            else:
                # A CLI error may already be executing its one-shot Close.
                # Let it preserve that failure before resorting to termination.
                try:
                    process.stdin.close()
                    process.wait(timeout=args.grace)
                except (OSError, subprocess.TimeoutExpired):
                    stop_tree(process)
            exit_code = 124 if isinstance(error, (TimeoutError, queue.Empty, subprocess.TimeoutExpired)) else 1
        finally:
            for thread in pumps:
                thread.join(timeout=10)
            changed = [str(p) for p in files if not p.is_file() or
                       hashlib.sha256(p.read_bytes()).hexdigest() != fingerprints[str(p)]]
            pipes_complete = all(not thread.is_alive() for thread in pumps)
            record('closed', exit_code=exit_code, reason=reason, changed_sources=changed,
                   pipes_complete=pipes_complete)
    if pipes_complete:
        partial.rename(output / 'console.dvlog')
    print(f'Demo closed: exit={exit_code}, reason={reason}, changed_sources={len(changed)}', flush=True)
    return exit_code or (1 if changed or not pipes_complete else 0)


if __name__ == '__main__':
    raise SystemExit(main())
