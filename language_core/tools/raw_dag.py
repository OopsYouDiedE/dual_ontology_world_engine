"""Read-only validation and streaming recovery of dv-raw-dag-v1 evidence.

Examples (no game execution):
  python raw_dag.py raw-events.dag.jsonl.partial --out prefix-summary.json
  python raw_dag.py raw-events.dag.jsonl --event e123 --out event-e123.json
  python raw_dag.py raw-events.dag.jsonl --node 456 --out original-value.json
  python raw_dag.py raw-events.dag.jsonl --checkpoint final --path '["trace"]' --out trace.json

The journal is never modified. A temporary SQLite offset index bounds resident
memory; recovery expands only the requested value, directly to the output stream.
Interrupted/invalid tails remain in the original file and are reported, never fixed.
"""
import argparse
from fractions import Fraction
import hashlib
import json
import math
import os
from pathlib import Path
import sqlite3
import sys
import tempfile


FORMAT = 'dv-raw-dag-v1'


def _reject_constant(value):
    raise ValueError(f'Non-finite JSON constant: {value}')


def _parse_integer(value):
    # System.Text.Json may emit the double negative zero as -0.
    return -0.0 if value == '-0' else int(value)


def _integer(value):
    return type(value) is int and value >= 0


class RawDagIndex:
    def __init__(self, source):
        self.source = Path(source).resolve()
        self.temporary = tempfile.TemporaryDirectory(prefix='dv-raw-dag-')
        self.db = sqlite3.connect(str(Path(self.temporary.name) / 'offsets.sqlite'))
        self.db.executescript('''
            PRAGMA journal_mode=OFF;
            PRAGMA synchronous=OFF;
            CREATE TABLE nodes(id INTEGER PRIMARY KEY, offset INTEGER NOT NULL);
            CREATE TABLE events(id TEXT PRIMARY KEY, root INTEGER NOT NULL, ordinal INTEGER UNIQUE NOT NULL, seq INTEGER NOT NULL);
            CREATE TABLE checkpoints(name TEXT NOT NULL, root INTEGER NOT NULL, events INTEGER NOT NULL, seq INTEGER NOT NULL);
        ''')
        self.reader = self.source.open('rb')
        self.summary = self._scan()

    def close(self):
        self.reader.close()
        self.db.close()
        self.temporary.cleanup()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()

    def _payload(self, raw, expected_seq, previous):
        if not raw.endswith(b'\n'):
            raise ValueError('Unterminated frame; no commit newline')
        envelope = json.loads(raw, parse_constant=_reject_constant, parse_int=_parse_integer)
        if not isinstance(envelope, dict) or set(envelope) != {'seq', 'prev', 'payload', 'sha256'}:
            raise ValueError('Invalid frame envelope')
        if type(envelope['seq']) is not int or envelope['seq'] != expected_seq or envelope['prev'] != previous:
            raise ValueError('Frame sequence or previous checksum mismatch')
        marker = b',"payload":'
        suffix = b',"sha256":"'
        begin = raw.find(marker)
        end = raw.rfind(suffix)
        if begin < 0 or end <= begin:
            raise ValueError('Noncanonical frame envelope')
        payload_bytes = raw[begin + len(marker):end]
        digest = hashlib.sha256(previous.encode('ascii') + b'\n' + str(expected_seq).encode('ascii') + b'\n' + payload_bytes).hexdigest()
        if envelope['sha256'] != digest:
            raise ValueError('Frame checksum mismatch')
        payload = envelope['payload']
        if not isinstance(payload, dict):
            raise ValueError('Frame payload is not an object')
        return payload, digest

    def _scan(self):
        digest = hashlib.sha256()
        previous = '0' * 64
        sequence = nodes = events = valid_bytes = 0
        last_commit_bytes = 0
        initial = final = seal = False
        last_event = None
        error = None
        last_checkpoint = None
        checkpoint_commits = []
        initial_stat = self.source.stat()
        with self.source.open('rb') as source:
            while True:
                offset = source.tell()
                raw = source.readline()
                if not raw:
                    break
                digest.update(raw)
                try:
                    if seal:
                        raise ValueError('Frames after final seal')
                    payload, checksum = self._payload(raw, sequence, previous)
                    kind = payload.get('kind')
                    if final and kind != 'seal':
                        raise ValueError('Only the final seal may follow the final checkpoint')
                    if sequence == 0:
                        if kind != 'header' or payload.get('format') != FORMAT:
                            raise ValueError('Missing supported journal header')
                    elif kind == 'node':
                        node_id, items = payload.get('node_id'), payload.get('items')
                        if type(node_id) is not int or node_id != nodes or not isinstance(items, list):
                            raise ValueError('Invalid node definition/order')
                        for item in items:
                            if isinstance(item, dict):
                                if set(item) == {'node'}:
                                    if not _integer(item['node']) or item['node'] >= node_id:
                                        raise ValueError('Node references a missing/forward definition')
                                elif set(item) == {'rational'}:
                                    if not isinstance(item['rational'], str) or str(Fraction(item['rational'])) != item['rational']:
                                        raise ValueError('Invalid canonical rational atom')
                                else:
                                    raise ValueError('Unknown atom/reference encoding')
                            elif not isinstance(item, (str, bool, int, float)) or isinstance(item, float) and not math.isfinite(item):
                                raise ValueError('Unsupported DV atom')
                        self.db.execute('INSERT INTO nodes VALUES (?, ?)', (node_id, offset))
                        nodes += 1
                    elif kind == 'event':
                        root, ordinal, event_id = payload.get('root'), payload.get('ordinal'), payload.get('event_id')
                        if not _integer(root) or root >= nodes or type(ordinal) is not int or ordinal != events or not isinstance(event_id, str):
                            raise ValueError('Invalid event commit')
                        if self.record_field(root, 'id') != event_id:
                            raise ValueError('Event ID differs from original frozen value')
                        self.db.execute('INSERT INTO events VALUES (?, ?, ?, ?)', (event_id, root, ordinal, sequence))
                        events += 1
                        last_event = event_id
                        last_commit_bytes = source.tell()
                    elif kind == 'checkpoint':
                        root, count, name = payload.get('root'), payload.get('events'), payload.get('name')
                        if not _integer(root) or root >= nodes or type(count) is not int or count != events or not isinstance(name, str):
                            raise ValueError('Invalid checkpoint commit')
                        if name == 'final' and final:
                            raise ValueError('Duplicate final checkpoint')
                        self.db.execute('INSERT INTO checkpoints VALUES (?, ?, ?, ?)', (name, root, count, sequence))
                        initial = initial or name == 'initial'
                        final = final or name == 'final'
                        last_checkpoint = {'name': name, 'root': root, 'events': count, 'frame': sequence}
                        checkpoint_commits.append(dict(last_checkpoint))
                        last_commit_bytes = source.tell()
                    elif kind == 'seal':
                        if not initial or not final or last_checkpoint is None or last_checkpoint['name'] != 'final' or last_checkpoint['events'] != events or payload.get('final_checkpoint') is not True or type(payload.get('events')) is not int or payload.get('events') != events or type(payload.get('nodes')) is not int or payload.get('nodes') != nodes:
                            raise ValueError('Seal lacks a complete final checkpoint/counts')
                        seal = True
                        last_commit_bytes = source.tell()
                    else:
                        raise ValueError('Unknown frame kind')
                    previous = checksum
                    sequence += 1
                    valid_bytes = source.tell()
                except (ValueError, TypeError, KeyError, ZeroDivisionError, json.JSONDecodeError, UnicodeDecodeError, sqlite3.IntegrityError) as issue:
                    error = {'byte_offset': offset, 'frame': sequence, 'error': str(issue)}
                    while chunk := source.read(1024 * 1024):
                        digest.update(chunk)
                    break
            size = source.tell()
        final_stat = self.source.stat()
        source_changed = (initial_stat.st_size, initial_stat.st_mtime_ns) != (final_stat.st_size, final_stat.st_mtime_ns)
        self.db.commit()
        return {
            'format': FORMAT, 'source': str(self.source), 'source_sha256': digest.hexdigest(),
            'source_bytes': size, 'validated_frames': sequence, 'validated_nodes': nodes,
            'source_changed_during_scan': source_changed,
            'committed_events': events, 'last_committed_event': last_event,
            'validated_bytes': valid_bytes, 'last_commit_bytes': last_commit_bytes,
            'residual_tail_bytes': size - valid_bytes, 'uncommitted_suffix_bytes': size - last_commit_bytes,
            'initial_checkpoint_observed': initial, 'final_checkpoint_observed': final,
            'seal_observed': seal, 'last_checkpoint': last_checkpoint, 'checkpoint_commits': checkpoint_commits, 'invalid_tail': error,
            'complete_sealing': bool(initial and final and seal and error is None and not source_changed),
            'scope': 'complete sealed raw values' if initial and final and seal and error is None and not source_changed else 'validated committed event prefix only; no inferred final session state',
            'uncompleted_computation': 'unknown; calculations without a committed event are not reconstructed',
            'original_tail_preserved': True,
        }

    def node(self, node_id):
        row = self.db.execute('SELECT offset FROM nodes WHERE id=?', (int(node_id),)).fetchone()
        if row is None:
            raise ValueError(f'Node {node_id} is not in the validated prefix')
        self.reader.seek(row[0])
        return json.loads(self.reader.readline(), parse_constant=_reject_constant, parse_int=_parse_integer)['payload']['items']

    def record_field(self, root, key):
        found = None
        for entry in self.node(root):
            if isinstance(entry, dict) and set(entry) == {'node'}:
                pair = self.node(entry['node'])
                if len(pair) >= 2 and pair[0] == key:
                    found = pair[1]
        return found

    def select(self, event=None, node=None, checkpoint=None, path=()):
        if event is not None:
            row = self.db.execute('SELECT root FROM events WHERE id=?', (event,)).fetchone()
        elif checkpoint is not None:
            row = self.db.execute('SELECT root FROM checkpoints WHERE name=? ORDER BY seq DESC LIMIT 1', (checkpoint,)).fetchone()
        else:
            row = (int(node),)
        if row is None:
            raise ValueError('Requested event/checkpoint is not in the validated prefix')
        selected = {'node': row[0]}
        for part in path:
            if not isinstance(selected, dict) or set(selected) != {'node'}:
                raise ValueError('Path enters a scalar')
            if type(part) is int and part >= 0:
                selected = self.node(selected['node'])[part]
            elif isinstance(part, str):
                # A missing field differs from a literal false, empty array or zero.
                entries = self.node(selected['node'])
                matched = False
                for entry in entries:
                    if isinstance(entry, dict) and set(entry) == {'node'}:
                        pair = self.node(entry['node'])
                        if len(pair) >= 2 and pair[0] == part:
                            selected, matched = pair[1], True
                if not matched:
                    raise ValueError(f'No field {part!r}')
            else:
                raise ValueError('Path entries must be field names or nonnegative indices')
        return selected

    def write_event_index(self, destination):
        with Path(destination).open('x', encoding='utf-8', newline='\n') as output:
            for event_id, root, ordinal, sequence in self.db.execute('SELECT id, root, ordinal, seq FROM events ORDER BY ordinal'):
                time = self.record_field(root, 'time')
                item = {'event_id': event_id, 'node_id': str(root), 'ordinal': ordinal, 'commit_frame': sequence,
                        'type': self.record_field(root, 'type'), 'actor': self.record_field(root, 'actor'),
                        'game_time': time['rational'] if isinstance(time, dict) and set(time) == {'rational'} else time, 'raw_source': str(self.source),
                        'scope': 'original committed event; no reconstruction of uncommitted work'}
                output.write(json.dumps(item, ensure_ascii=False) + '\n')

    def write_value(self, selected, output, value_format='trace-json'):
        # Iterative expansion avoids recursive Python objects and recursion limits.
        stack = [('value', selected)]
        while stack:
            kind, value = stack.pop()
            if kind == 'text':
                output.write(value)
            elif isinstance(value, dict) and set(value) == {'rational'}:
                atom = {'$dv_rational': value['rational']} if value_format == 'dv-tagged' else value['rational']
                output.write(json.dumps(atom, ensure_ascii=True, separators=(',', ':')))
            elif isinstance(value, dict):
                items = self.node(value['node'])
                output.write('[')
                stack.append(('text', ']'))
                for i in range(len(items) - 1, -1, -1):
                    stack.append(('value', items[i]))
                    if i:
                        stack.append(('text', ','))
            else:
                output.write(json.dumps(value, ensure_ascii=True, allow_nan=False, separators=(',', ':')))


def inspect_journal(source, event_index_path=None):
    with RawDagIndex(source) as index:
        if event_index_path is not None:
            index.write_event_index(event_index_path)
        return index.summary


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('source', type=Path)
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument('--event')
    selection.add_argument('--node', type=int)
    selection.add_argument('--checkpoint')
    parser.add_argument('--path', default='[]', help='JSON path array: string record keys and integer list indices')
    parser.add_argument('--value-format', choices=('trace-json', 'dv-tagged'), default='trace-json', help='Trace exact-number text, or $dv_rational tags preserving the original DV atom type')
    parser.add_argument('--out', type=Path, help='New output file; never overwrites an existing path')
    parser.add_argument('--events-out', type=Path, help='New index of every validated event commit, including events not yet projected to stdout')
    args = parser.parse_args()
    path = json.loads(args.path)
    if not isinstance(path, list):
        parser.error('--path must be a JSON array')
    with RawDagIndex(args.source) as index:
        if args.events_out:
            index.write_event_index(args.events_out)
        partial = Path(str(args.out) + '.partial') if args.out else None
        if args.out and args.out.exists():
            raise FileExistsError(args.out)
        output = partial.open('x', encoding='utf-8', newline='\n') if partial else sys.stdout
        written = False
        try:
            if args.event is None and args.node is None and args.checkpoint is None:
                json.dump(index.summary, output, ensure_ascii=False, indent=2)
            else:
                index.write_value(index.select(args.event, args.node, args.checkpoint, path), output, args.value_format)
            output.write('\n')
            written = True
        finally:
            if args.out:
                output.close()
        if args.out and written:
            # Hard-link publication fails if a concurrent destination appeared;
            # unlike POSIX rename it can never overwrite an existing artifact.
            os.link(partial, args.out)
            partial.unlink()
        return 0 if index.summary['invalid_tail'] is None else 1


if __name__ == '__main__':
    raise SystemExit(main())
