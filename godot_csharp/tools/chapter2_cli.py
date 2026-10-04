"""Persistent command-line play; records only input and the game's printed output."""
import argparse
import json
import os
import signal
from pathlib import Path
import subprocess
import sys
import threading
import time

ROOT = Path(__file__).resolve().parents[2]
DOTNET = Path('C:/Program Files/dotnet/dotnet.exe')
DLL = ROOT / 'godot_csharp/scenes/Chapter2/Prototype/bin/Debug/net8.0/Chapter2Prototype.dll'

def append(path, data):
    with path.open('a', encoding='utf-8') as stream:
        stream.write(json.dumps(data, ensure_ascii=False) + '\n')

def status(folder):
    for attempt in range(5):
        try:
            return json.loads((folder/'status.json').read_text(encoding='utf-8'))
        except PermissionError:
            if attempt == 4:
                raise
            time.sleep(.05)

def serve(folder):
    started = time.time()
    process = subprocess.Popen([str(DOTNET), str(DLL), 'play', '--input-ready'], stdin=subprocess.PIPE,
                               stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                               text=True, encoding='utf-8', bufsize=1)
    last_output = [time.time()]
    ready = [0]
    def read():
        for line in process.stdout:
            if line.rstrip('\r\n') == '\u001einput-ready':
                ready[0] += 1
                continue
            last_output[0] = time.time()
            append(folder / 'timeline.jsonl', {'elapsed': time.time()-started,
                   'type': 'output', 'text': line.rstrip('\r\n')})
    reader = threading.Thread(target=read, daemon=True)
    reader.start()
    offset, count = 0, 0
    while process.poll() is None:
        with (folder / 'requests.jsonl').open(encoding='utf-8') as stream:
            stream.seek(offset)
            while raw := stream.readline():
                request = json.loads(raw)
                append(folder / 'timeline.jsonl', {'elapsed': time.time()-started,
                       'type': 'input', 'text': request['line']})
                process.stdin.write(request['line']+'\n')
                process.stdin.flush()
                count += 1
            offset = stream.tell()
        state = {'pid': process.pid, 'alive': True, 'inputs': count,
                 'elapsed': time.time()-started, 'last_output': last_output[0], 'ready': ready[0]}
        temporary = folder / 'status.tmp'
        temporary.write_text(json.dumps(state), encoding='utf-8')
        try:
            os.replace(temporary, folder / 'status.json')
        except PermissionError:
            # Windows readers can briefly hold the old file open; publish on the next tick.
            pass
        time.sleep(.05)
    reader.join(timeout=2)
    state.update(alive=False, exit_code=process.returncode, elapsed=time.time()-started)
    (folder/'status.json').write_text(json.dumps(state), encoding='utf-8')

def snapshot(folder):
    events = [json.loads(line) for line in (folder/'timeline.jsonl').read_text(encoding='utf-8').splitlines()]
    cursor_file = folder/'read_cursor.txt'
    cursor = int(cursor_file.read_text()) if cursor_file.exists() else 0
    for item in events[cursor:]:
        if item['type']=='output':
            print(item['text'])
    cursor_file.write_text(str(len(events)))
    print('\n[CLI session] '+json.dumps(status(folder)))

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--session', required=True)
    parser.add_argument('action', choices=['start','send','snapshot','serve','stop'])
    parser.add_argument('line', nargs='?', default='')
    args=parser.parse_args()
    if not args.session.replace('-','').replace('_','').isalnum():
        parser.error('session must contain letters, digits, hyphens or underscores')
    folder=ROOT/'artifacts/remake-chapter2'/args.session
    if args.action=='serve':
        serve(folder)
        return
    if args.action=='start':
        folder.mkdir(parents=True, exist_ok=False)
        (folder/'requests.jsonl').touch()
        (folder/'timeline.jsonl').touch()
        print('CLI session created. Keep this command running; use snapshot/send in another command.', flush=True)
        serve(folder)
    elif args.action=='send':
        before=status(folder)
        append(folder/'requests.jsonl', {'line':args.line})
        deadline=time.time()+10
        while time.time()<deadline:
            state=status(folder)
            if not state['alive'] or state['inputs']>before['inputs'] and state['ready']>before['ready']:
                break
            time.sleep(.05)
        snapshot(folder)
    elif args.action=='snapshot':
        snapshot(folder)
    else:
        state=status(folder)
        if state['alive']:
            os.kill(state['pid'], signal.SIGTERM)
        print('Session stopped; transcript preserved at '+str(folder))

if __name__=='__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    main()
