"""Persistent player CLI transport. Sends requests; supplies no game decisions or verdicts."""
from __future__ import annotations

import argparse
from collections import deque
import json
from pathlib import Path
import queue
import subprocess
import sys
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.request import Request, urlopen
import uuid

ROOT = Path(__file__).resolve().parents[2]


class Player:
    def __init__(self, output: Path):
        output.mkdir(parents=True, exist_ok=True)
        occupied = [p for p in output.iterdir() if p.name.startswith(('session.', 'performance.'))]
        if occupied:
            raise FileExistsError('Evidence prefix already exists; no CLI process started: ' + str(output))
        assembly = ROOT / 'dv_core/cli/bin/Release/net8.0/dv-cli.dll'
        self.process = subprocess.Popen(
            ['dotnet', str(assembly), '--jsonl', '--controller', 'player',
             '--trace', str(output / 'session.dvdag'), '--save', str(output / 'session.dvsave'),
             '--performance', str(output / 'performance')],
            cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            encoding='utf-8', errors='replace', bufsize=1)
        self.lines: queue.Queue = queue.Queue()
        self.errors: deque[str] = deque(maxlen=100)
        threading.Thread(target=self._stdout, daemon=True).start()
        threading.Thread(target=self._stderr, daemon=True).start()

    def _stdout(self):
        for line in self.process.stdout:
            self.lines.put(line)
        self.lines.put(None)

    def _stderr(self):
        for line in self.process.stderr:
            self.errors.append(line.rstrip()[:2000])

    def request(self, body: dict):
        if self.process.poll() is not None:
            raise RuntimeError('CLI exited: ' + str(self.process.returncode))
        request_id = uuid.uuid4().hex
        request = dict(body, request_id=request_id)
        self.process.stdin.write(json.dumps(request, ensure_ascii=False) + '\n')
        self.process.stdin.flush()
        output = []
        while True:
            line = self.lines.get(timeout=120)
            if line is None:
                raise RuntimeError('CLI closed before request completion: ' + '\n'.join(self.errors))
            value = json.loads(line)
            output.append(value)
            # All CLI protocol fields live in data, including the completion id.
            if value.get('kind') == 'command_complete' and value.get('data', {}).get('request_id') == request_id:
                break
        result = {'output': output}
        if output[-1]['data'].get('closing'):
            result['exit_code'] = self.process.wait(timeout=120)
            result['stderr'] = list(self.errors)
        return result


def serve(output: Path):
    # Bind before starting the one formal session so a port failure cannot
    # accidentally leave a second, unobservable CLI session behind.
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def do_POST(self):
            closing = False
            try:
                length = int(self.headers.get('Content-Length', '0'))
                if self.path != '/request' or not 0 < length <= 65536:
                    raise ValueError('POST /request requires a JSON command object')
                body = json.loads(self.rfile.read(length))
                if not isinstance(body, dict) or not isinstance(body.get('command'), str):
                    raise ValueError('A command string is required')
                value = player.request(body)
                closing = 'exit_code' in value
                status = 200
            except Exception as error:
                value = {'transport_error': type(error).__name__, 'message': str(error)}
                status = 500
            encoded = json.dumps(value, ensure_ascii=False).encode('utf-8')
            self.send_response(status)
            self.send_header('Content-Type', 'application/json; charset=utf-8')
            self.send_header('Content-Length', str(len(encoded)))
            self.end_headers()
            self.wfile.write(encoded)
            if closing:
                threading.Thread(target=self.server.shutdown, daemon=True).start()

    server = HTTPServer(('127.0.0.1', 0), Handler)
    player = Player(output.resolve())
    print(json.dumps({'bridge_url': f'http://127.0.0.1:{server.server_port}',
                      'cli_pid': player.process.pid, 'output': str(output.resolve())}), flush=True)
    try:
        server.serve_forever()
    finally:
        server.server_close()
        if player.process.poll() is None:
            player.process.stdin.close()
            player.process.wait(timeout=120)


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='mode', required=True)
    serving = commands.add_parser('serve')
    serving.add_argument('--out', type=Path, required=True)
    client = commands.add_parser('request')
    client.add_argument('--url', required=True)
    client.add_argument('--command', required=True)
    client.add_argument('--argument')
    args = parser.parse_args()
    if args.mode == 'serve':
        serve(args.out)
    else:
        body = {'command': args.command}
        if args.argument is not None:
            body['argument'] = args.argument
        request = Request(args.url.rstrip('/') + '/request',
                          data=json.dumps(body).encode('utf-8'),
                          headers={'Content-Type': 'application/json'}, method='POST')
        with urlopen(request, timeout=130) as response:
            print(response.read().decode('utf-8'))


if __name__ == '__main__':
    main()
