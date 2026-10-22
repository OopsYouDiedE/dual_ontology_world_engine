"""Capture explicitly supplied CLI inputs with bounded process lifetime."""
from pathlib import Path
import argparse
import base64
import hashlib
import os
import signal
import subprocess
import sys
import threading
import time

from binary_evidence import RecordWriter


def stop_tree(process):
    if process.poll() is not None:
        return
    if os.name == "nt":
        subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=8)
    else:
        os.killpg(process.pid, signal.SIGKILL)
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--cli", type=Path, default=Path("dv_core/cli/bin/Release/net8.0/dv-cli.dll"))
    parser.add_argument("--width", type=int, default=100)
    parser.add_argument("--height", type=int, default=42)
    parser.add_argument("--timeout", type=float, default=90)
    parser.add_argument("--controller", choices=("player", "ai"), default="player")
    parser.add_argument("--viewer", default="detective")
    parser.add_argument("--format", choices=("tui", "jsonl"), default="tui")
    parser.add_argument("--replay", type=Path)
    args = parser.parse_args()
    if not 0 < args.timeout <= 600:
        parser.error("--timeout must be in (0, 600]")
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)
    command = ["dotnet", str(args.cli.resolve()), "--" + args.format,
               "--width", str(args.width), "--height", str(args.height)]
    if args.replay:
        command += ["--replay", str(args.replay.resolve()), "--interactive"]
    else:
        command += ["--controller", args.controller, "--viewer", args.viewer, "--audit",
                    "--trace", str(output / "session.dvdag"), "--save", str(output / "session.dvsave")]
    lock = threading.Lock()
    origin = time.monotonic_ns()
    partial = output / "console.dvlog.partial"
    stopped = threading.Event()
    log_open = True
    timed_out = interrupted = False
    exit_code = 1
    with RecordWriter(partial) as writer:
        def record(value):
            with lock:
                if log_open:
                    writer.append({"real_ns": time.monotonic_ns() - origin, **value})
                    writer.stream.flush()
        root = Path(__file__).resolve().parents[2]
        sources = [*sorted((root / "dv_core/cli").glob("*.cs")),
                   *sorted((root / "dv_core/host_bridge").glob("*.cs")),
                   *sorted((root / "dv_core/modules").glob("*.dv")),
                   *sorted(args.cli.resolve().parent.glob("*.dll")), Path(__file__)]
        fingerprints = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
        record({"kind": "launch", "command": command, "source_sha256": fingerprints,
                "timeout_seconds": args.timeout, "scope": "explicit operator inputs; recorded replay" if args.replay
                else "explicit operator inputs; formal-model", "controller": args.controller})
        popen_options = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == "nt" else {"start_new_session": True}
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                   stderr=subprocess.PIPE, **popen_options)

        def pump(pipe, destination, name):
            try:
                while True:
                    block = os.read(pipe.fileno(), 65536)
                    if not block:
                        return
                    record({"kind": name, "encoding": "base64", "bytes": base64.b64encode(block).decode("ascii")})
                    destination.write(block)
                    destination.flush()
            except (BrokenPipeError, OSError):
                return

        def inputs():
            try:
                # A blocked daemon must not hold Python's buffered stdin lock
                # when the CLI exits before its operator sends another line.
                pending = b""
                while not stopped.is_set():
                    block = os.read(sys.stdin.fileno(), 65536)
                    if not block:
                        if pending:
                            record({"kind": "stdin", "encoding": "base64", "bytes": base64.b64encode(pending).decode("ascii")})
                            process.stdin.write(pending)
                            process.stdin.flush()
                        break
                    pending += block
                    while b"\n" in pending:
                        line, pending = pending.split(b"\n", 1)
                        line += b"\n"
                        if stopped.is_set():
                            return
                        record({"kind": "stdin", "encoding": "base64", "bytes": base64.b64encode(line).decode("ascii")})
                        process.stdin.write(line)
                        process.stdin.flush()
                        if line.strip().lower() in (b"quit", b"exit"):
                            return
            except (BrokenPipeError, OSError, ValueError):
                pass
            finally:
                try:
                    process.stdin.close()
                except (BrokenPipeError, OSError, ValueError):
                    pass

        pumps = [threading.Thread(target=pump, args=(process.stdout, sys.stdout.buffer, "stdout"), daemon=True),
                 threading.Thread(target=pump, args=(process.stderr, sys.stderr.buffer, "stderr"), daemon=True)]
        for thread in pumps:
            thread.start()
        threading.Thread(target=inputs, daemon=True).start()
        try:
            exit_code = process.wait(timeout=args.timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            record({"kind": "timeout", "pid": process.pid, "timeout_seconds": args.timeout})
            stop_tree(process)
            exit_code = 124
        except KeyboardInterrupt:
            interrupted = True
            record({"kind": "operator_interruption", "pid": process.pid})
            stop_tree(process)
            exit_code = 130
        finally:
            stopped.set()
            for thread in pumps:
                thread.join(timeout=5)
            changed = [str(p) for p in sources
                       if not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest() != fingerprints[str(p)]]
            record({"kind": "closed", "exit_code": exit_code, "timed_out": timed_out,
                    "interrupted": interrupted, "changed_sources": changed,
                    "pipes_complete": all(not t.is_alive() for t in pumps)})
            with lock:
                log_open = False
    if not timed_out and not interrupted and all(not t.is_alive() for t in pumps):
        partial.rename(output / "console.dvlog")
    return exit_code or (1 if changed else 0)


if __name__ == "__main__":
    raise SystemExit(main())
