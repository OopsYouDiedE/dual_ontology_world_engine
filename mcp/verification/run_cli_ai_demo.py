"""Run only engine-AI decisions within one hard, real-time process budget."""
import argparse
import base64
import json
import os
from pathlib import Path
from fractions import Fraction
import queue
import subprocess
import threading
import time

from binary_evidence import RecordWriter
from capture_cli import stop_tree


def game_minute(value):
    if isinstance(value, dict) and '$dv_rational' in value:
        return float(Fraction(value['$dv_rational']))
    return float(value)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', required=True, type=Path)
    parser.add_argument('--seconds', required=True, type=float)
    parser.add_argument('--game-minutes', type=float,
                        help='Stop after at least this many actually advanced game minutes; never assign the clock.')
    parser.add_argument('--planning', choices=('selective', 'precise'), default='selective')
    parser.add_argument('--grace', type=float, default=40,
                        help='Shutdown reserve inside the hard timeout, never extra test time.')
    parser.add_argument('--hard-timeout', type=float, default=300)
    parser.add_argument('--source-revision', default='')
    parser.add_argument('--cli', type=Path, default=Path('dv_core/cli/bin/Release/net8.0/dv-cli.dll'))
    args = parser.parse_args()
    if not 0 < args.hard_timeout <= 300:
        parser.error('hard-timeout must be in (0,300]')
    if args.game_minutes is not None and args.game_minutes <= 0:
        parser.error('game-minutes must be positive')
    if not 0 < args.grace < args.hard_timeout or not 0 < args.seconds <= args.hard_timeout - args.grace:
        parser.error('seconds plus shutdown reserve must fit inside hard-timeout')
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)
    command = ['dotnet', str(args.cli.resolve()), '--jsonl', '--controller', 'ai',
               '--viewer', 'detective', '--planning', args.planning, '--trace', str(output / 'session.dvdag'),
               '--save', str(output / 'session.dvsave'), '--performance', str(output / 'engine')]
    if args.source_revision:
        command.extend(['--source-revision', args.source_revision])
    origin = time.monotonic_ns()
    started = time.monotonic()
    hard_deadline = started + args.hard_timeout
    decision_deadline = min(started + args.seconds, hard_deadline - args.grace)
    lock = threading.Lock()
    messages = queue.Queue()
    finished = threading.Event()
    timed_out = threading.Event()
    recording_open = threading.Event()
    recording_open.set()
    exit_code = 1
    reason = 'not_started'
    advances = 0
    latest_status = {}
    partial = output / 'console.dvlog.partial'
    with RecordWriter(partial) as writer:
        def record(kind, **values):
            with lock:
                if not recording_open.is_set():
                    return
                writer.append({'kind': kind, 'real_ns': time.monotonic_ns() - origin, **values})
                writer.stream.flush()

        record('launch', command=command, source_revision=args.source_revision or None,
               requested_seconds=args.seconds, hard_timeout_seconds=args.hard_timeout,
               requested_game_minutes=args.game_minutes, planning=args.planning,
               shutdown_reserve_seconds=args.grace,
               scope='engine AI decisions only; fixed detective observer; formal-model',
               source_validation='not-run-by-user-request')
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)

        def watchdog():
            if not finished.wait(max(0, hard_deadline - time.monotonic())):
                timed_out.set()
                stop_tree(process)

        watchdog_thread = threading.Thread(target=watchdog, daemon=True)
        watchdog_thread.start()

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

        pumps = [threading.Thread(target=pump, args=(process.stdout, 'stdout'), daemon=True),
                 threading.Thread(target=pump, args=(process.stderr, 'stderr'), daemon=True)]
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
                remaining = min(deadline, hard_deadline) - time.monotonic()
                if remaining <= 0:
                    raise TimeoutError('AI decision deadline reached')
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
            reply(decision_deadline)
            initial_game_time = game_minute(latest_status['game_time'])
            record('window_start', remaining_seconds=max(0, decision_deadline - time.monotonic()),
                   clock_basis='process launch; opening included')
            last_progress = -30.0
            reason = 'window_elapsed'
            while time.monotonic() < decision_deadline:
                if args.game_minutes is not None and game_minute(latest_status.get('game_time', initial_game_time)) - initial_game_time >= args.game_minutes:
                    reason = 'game_window_reached'
                    break
                if latest_status.get('active') is False:
                    reason = 'session_inactive'
                    break
                send('next')
                advances += 1
                item = reply(hard_deadline - min(10, args.grace / 2))
                if item.get('kind') == 'intent':
                    intent = item.get('data') or {}
                    if intent.get('awaiting_input'):
                        reason = 'unexpected_manual_input'
                        break
                    if intent.get('status') in ('stopped', 'closed'):
                        reason = 'session_inactive'
                        break
                elapsed = time.monotonic() - started
                if elapsed - last_progress >= 30:
                    last_progress = elapsed
                    print(f'AI decisions {elapsed:.1f}/{args.seconds:g}s; next inputs {advances}; '
                          f'game time {latest_status.get("game_time", "unknown")}', flush=True)
            record('window_end', reason=reason, elapsed_seconds=time.monotonic() - started,
                   next_inputs=advances, last_status=latest_status,
                   initial_game_time=initial_game_time,
                   actual_game_minutes=game_minute(latest_status.get('game_time', initial_game_time)) - initial_game_time)
            send('quit')
            process.stdin.close()
            exit_code = process.wait(timeout=max(.001, hard_deadline - time.monotonic()))
        except (Exception, KeyboardInterrupt) as error:
            reason = 'hard_timeout' if timed_out.is_set() else 'driver_error'
            record('driver_error', error=str(error), next_inputs=advances)
            print(f'AI session stopped: {error}', flush=True)
            try:
                if process.poll() is None:
                    try:
                        send('quit')
                        process.stdin.close()
                    except (OSError, ValueError):
                        pass
                    process.wait(timeout=max(.001, hard_deadline - time.monotonic()))
            except subprocess.TimeoutExpired:
                timed_out.set()
                stop_tree(process)
            exit_code = 124 if timed_out.is_set() or isinstance(error, (TimeoutError, queue.Empty, subprocess.TimeoutExpired)) else 1
        finally:
            if process.poll() is None:
                timed_out.set()
                stop_tree(process)
            for thread in pumps:
                thread.join(timeout=max(0, min(2, hard_deadline - time.monotonic())))
            finished.set()
            pipes_complete = all(not thread.is_alive() for thread in pumps)
            record('closed', exit_code=exit_code, reason=reason, hard_timeout=timed_out.is_set(),
                   elapsed_seconds=time.monotonic() - started, pipes_complete=pipes_complete)
            with lock:
                recording_open.clear()
    if pipes_complete:
        partial.rename(output / 'console.dvlog')
    print(f'AI session closed: exit={exit_code}, reason={reason}, elapsed={time.monotonic() - started:.1f}s', flush=True)
    return exit_code or (0 if pipes_complete else 1)


if __name__ == '__main__':
    raise SystemExit(main())

