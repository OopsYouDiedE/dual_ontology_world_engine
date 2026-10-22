"""Read-only validation and streaming recovery of binary dv-raw-dag-v2 evidence (old v1 read-only compatible).

Examples (no game execution):
  python raw_dag.py raw-events.dvdag.partial --out prefix-summary.txt
  python raw_dag.py raw-events.dvdag --event e123 --out event-e123.dvdag
  python raw_dag.py raw-events.dvdag --node 456 --out original-value.dvdag
  python raw_dag.py raw-events.dvdag --checkpoint final --path '["trace"]' --out trace.dvdag
  python raw_dag.py raw-events.dvdag --event e123 --value-format trace-json
  python raw_dag.py raw-events.dvdag --node 456 --value-format dv-tagged

Selected values default to binary DAG bytes on stdout or in a new .dvdag/.dvsave
file. Text value formats are explicit stdout views: trace-json shows exact atoms
as text; dv-tagged retains their $dv_rational type tags. No JSON trace is written
to a file. With no selection, stdout or --out receives a readable index summary.

The journal is never modified. A temporary SQLite offset index bounds resident
memory; recovery expands only the requested value, directly to the output stream.
Interrupted/invalid tails remain in the original file and are reported, never fixed.
"""
import argparse
from fractions import Fraction
from collections import OrderedDict
import hashlib
import json
import math
import os
from pathlib import Path
import sqlite3
import sys
import tempfile


from matrix_lifecycle import case_guard
from binary_evidence import MAGIC, encode, decode, read_frame, write_frame, RecordWriter

FORMAT = 'dv-raw-dag-v2'


def _reject_constant(value):
    raise ValueError(f'Non-finite JSON constant: {value}')


def _parse_integer(value):
    # System.Text.Json may emit the double negative zero as -0.
    return -0.0 if value == '-0' else int(value)


def _integer(value):
    return type(value) is int and value >= 0


class RawDagIndex:
    def __init__(self, source, optimize=True):
        self.optimize = optimize
        # Bounded by wire bytes and entry count; cached values are read-only.
        self._node_cache = OrderedDict()
        self._node_cache_bytes = 0
        self.source = Path(source).resolve()
        self._guard = case_guard(self.source)
        self._guard.__enter__()
        self._closed = False
        try:
            self.temporary = tempfile.TemporaryDirectory(prefix='dv-raw-dag-')
            self.db = sqlite3.connect(str(Path(self.temporary.name) / 'offsets.sqlite'))
            self.db.executescript("""
                PRAGMA journal_mode=OFF;
                PRAGMA synchronous=OFF;
                CREATE TABLE nodes(id INTEGER PRIMARY KEY, offset INTEGER NOT NULL);
                CREATE TABLE events(id TEXT PRIMARY KEY, root INTEGER NOT NULL, ordinal INTEGER UNIQUE NOT NULL, seq INTEGER NOT NULL, process_elapsed_ms REAL, metadata BLOB);
                CREATE TABLE checkpoints(name TEXT NOT NULL, root INTEGER NOT NULL, events INTEGER NOT NULL, seq INTEGER NOT NULL);
            """)
            self.reader = self.source.open('rb')
            self.binary = self.reader.read(len(MAGIC)) == MAGIC
            self.reader.seek(0)
            self.summary = self._scan()
        except BaseException:
            self.close()
            raise

    def close(self):
        if self._closed:
            return
        self._closed = True
        try:
            if hasattr(self, 'reader'):
                self.reader.close()
            if hasattr(self, 'db'):
                self.db.close()
            if hasattr(self, 'temporary'):
                self.temporary.cleanup()
        finally:
            self._guard.__exit__(None, None, None)

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()

    def _payload(self, raw, expected_seq, previous, envelope=None, payload_bytes=None):
        if self.binary:
            if envelope is None:
                envelope = decode(raw[36:])
            if not isinstance(envelope, dict) or set(envelope) != {'seq', 'prev', 'payload', 'sha256'}:
                raise ValueError('Invalid binary DAG envelope')
            if type(envelope['seq']) is not int or envelope['seq'] != expected_seq or envelope['prev'] != previous:
                raise ValueError('Binary DAG sequence/checksum predecessor mismatch')
            payload = envelope['payload']
            if not isinstance(payload, dict): raise ValueError('Invalid binary DAG payload')
            chain = hashlib.sha256(previous.encode('ascii') + b'\n' + str(expected_seq).encode('ascii') + b'\n')
            chain.update(payload_bytes if payload_bytes is not None else encode(payload))
            checksum = chain.hexdigest()
            if checksum != envelope['sha256']: raise ValueError('Binary DAG chain checksum mismatch')
            return payload, checksum
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
            if self.binary:
                digest.update(source.read(len(MAGIC)))
                valid_bytes = len(MAGIC)
            while True:
                offset = source.tell()
                try:
                    frame = read_frame(source, payload_span=self.optimize) if self.binary else None
                    raw = frame[0] if frame is not None else (b'' if self.binary else source.readline())
                except (ValueError, UnicodeDecodeError) as issue:
                    error = {'byte_offset': offset, 'frame': sequence, 'error': str(issue)}
                    source.seek(offset)
                    while chunk := source.read(1024 * 1024): digest.update(chunk)
                    break
                if not raw: break
                digest.update(raw)
                try:
                    if seal:
                        raise ValueError('Frames after final seal')
                    envelope = frame[1] if self.binary and self.optimize else None
                    payload, checksum = self._payload(raw, sequence, previous, envelope, frame[2] if self.binary and self.optimize else None)
                    kind = payload.get('kind')
                    if final and kind != 'seal':
                        raise ValueError('Only the final seal may follow the final checkpoint')
                    if sequence == 0:
                        if kind != 'header' or payload.get('format') != (FORMAT if self.binary else 'dv-raw-dag-v1'):
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
                        self._cache_node(node_id, items, len(raw))
                        nodes += 1
                    elif kind == 'event':
                        root, ordinal, event_id = payload.get('root'), payload.get('ordinal'), payload.get('event_id')
                        if not _integer(root) or root >= nodes or type(ordinal) is not int or ordinal != events or not isinstance(event_id, str):
                            raise ValueError('Invalid event commit')
                        fields = self.record_fields(root) if self.optimize else None
                        original_id = fields.get('id') if fields is not None else self.record_field(root, 'id')
                        if original_id != event_id:
                            raise ValueError('Event ID differs from original frozen value')
                        elapsed = payload.get('process_elapsed_ms')
                        if elapsed is not None and (type(elapsed) not in (int, float) or not math.isfinite(elapsed) or elapsed < 0):
                            raise ValueError('Invalid monotonic event timestamp')
                        metadata = encode(self.event_metadata(root, fields)) if fields is not None else None
                        self.db.execute('INSERT INTO events VALUES (?, ?, ?, ?, ?, ?)', (event_id, root, ordinal, sequence, elapsed, metadata))
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
            'format': FORMAT if self.binary else 'dv-raw-dag-v1', 'source': str(self.source), 'source_sha256': digest.hexdigest(),
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

    def _cache_node(self, node_id, items, size):
        if not self.optimize or size > 16 * 1024 * 1024:
            return
        old = self._node_cache.pop(node_id, None)
        if old is not None:
            self._node_cache_bytes -= old[1]
        self._node_cache[node_id] = (items, size)
        self._node_cache_bytes += size
        while self._node_cache_bytes > 16 * 1024 * 1024 or len(self._node_cache) > 8192:
            _, (_, removed) = self._node_cache.popitem(last=False)
            self._node_cache_bytes -= removed

    def node(self, node_id):
        cached = self._node_cache.get(int(node_id)) if self.optimize else None
        if cached is not None:
            self._node_cache.move_to_end(int(node_id))
            return cached[0]
        row = self.db.execute('SELECT offset FROM nodes WHERE id=?', (int(node_id),)).fetchone()
        if row is None:
            raise ValueError(f'Node {node_id} is not in the validated prefix')
        self.reader.seek(row[0])
        envelope = read_frame(self.reader)[1] if self.binary else json.loads(self.reader.readline(), parse_constant=_reject_constant, parse_int=_parse_integer)
        items = envelope['payload']['items']
        self._cache_node(int(node_id), items, self.reader.tell() - row[0])
        return items

    def record_fields(self, root):
        fields = {}
        for entry in self.node(root):
            if isinstance(entry, dict) and set(entry) == {'node'}:
                pair = self.node(entry['node'])
                if len(pair) >= 2 and isinstance(pair[0], str):
                    fields[pair[0]] = pair[1]
        return fields

    def event_metadata(self, root, fields=None):
        if fields is None:
            fields = {key: self.record_field(root, key) for key in ('time', 'type', 'actor', 'parents', 'data')}
        result = {key: fields.get(key) for key in ('time', 'type', 'actor')}
        parents = fields.get('parents')
        parent_ids = self.node(parents['node']) if isinstance(parents, dict) and set(parents) == {'node'} else None
        result['parent_ids'] = parent_ids if isinstance(parent_ids, list) and all(isinstance(x, str) for x in parent_ids) else None
        data = fields.get('data')
        result['host_action_event'] = (self.record_field(data['node'], 'action_event')
            if result['type'] == 'host_completion' and isinstance(data, dict) and set(data) == {'node'} else None)
        return result

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
        with RecordWriter(destination) as output:
            for event_id, root, ordinal, sequence, elapsed, metadata in self.db.execute('SELECT id, root, ordinal, seq, process_elapsed_ms, metadata FROM events ORDER BY ordinal'):
                fields = decode(metadata) if metadata is not None else self.event_metadata(root)
                time = fields['time']
                item = {'event_id': event_id, 'node_id': str(root), 'ordinal': ordinal, 'commit_frame': sequence,
                        'type': fields['type'], 'actor': fields['actor'],
                        'parent_ids': fields['parent_ids'], 'host_action_event': fields['host_action_event'],
                        'game_time': time['rational'] if isinstance(time, dict) and set(time) == {'rational'} else time, 'raw_source': str(self.source),
                        'process_elapsed_ms': elapsed,
                        'scope': 'original committed event; no reconstruction of uncommitted work'}
                output.append(item)

    def write_binary(self, selected, output):
        if not isinstance(selected, dict) or set(selected) != {'node'}:
            raise ValueError('Binary value export requires a list/record root')
        root = selected['node']; previous = '0' * 64; sequence = 0
        output.write(MAGIC)
        def frame(payload):
            nonlocal previous, sequence
            checksum = hashlib.sha256(previous.encode('ascii') + b'\n' + str(sequence).encode('ascii') + b'\n' + encode(payload)).hexdigest()
            write_frame(output, {'seq': sequence, 'prev': previous, 'payload': payload, 'sha256': checksum})
            sequence += 1; previous = checksum
        frame({'kind': 'header', 'format': FORMAT, 'value_encoding': 'typed binary atoms and prior node references', 'checksum': 'sha256(previous-ascii + LF + sequence-ascii + LF + binary-payload)'})
        for node_id in range(root + 1):
            frame({'kind': 'node', 'node_id': node_id, 'items': self.node(node_id)})
        frame({'kind': 'checkpoint', 'name': 'initial', 'root': root, 'events': 0})
        frame({'kind': 'checkpoint', 'name': 'final', 'root': root, 'events': 0})
        frame({'kind': 'seal', 'events': 0, 'nodes': root + 1, 'final_checkpoint': True})

    def write_value(self, selected, output, value_format='trace-json'):
        if value_format not in ('trace-json', 'dv-tagged'):
            raise ValueError('Text projection requires trace-json or dv-tagged; use write_binary for DAG bytes')
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


def inspect_journal(source, event_index_path=None, optimize=True):
    with RawDagIndex(source, optimize=optimize) as index:
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
    parser.add_argument('--value-format', choices=('binary', 'trace-json', 'dv-tagged'), default='binary',
                        help='Selected values: binary DAG bytes (default, stdout or file); trace-json/dv-tagged are stdout-only text views')
    parser.add_argument('--out', type=Path, help='New binary .dvdag/.dvsave for a selected value, or a readable summary with no selection')
    parser.add_argument('--events-out', type=Path, help='New index of every validated event commit, including events not yet projected to stdout')
    args = parser.parse_args()
    path = json.loads(args.path)
    if not isinstance(path, list):
        parser.error('--path must be a JSON array')
    selected = args.event is not None or args.node is not None or args.checkpoint is not None
    if path and not selected:
        parser.error('--path requires --event, --node or --checkpoint')
    if selected and args.out and args.value_format != 'binary':
        parser.error('Text value projections use stdout; file exports require --value-format binary')
    with RawDagIndex(args.source) as index:
        if args.events_out:
            index.write_event_index(args.events_out)
        partial = Path(str(args.out) + '.partial') if args.out else None
        if args.out and args.out.exists():
            raise FileExistsError(args.out)
        if selected and args.out:
            if args.out.suffix.lower() not in ('.dvdag', '.dvsave'):
                raise ValueError('New evidence exports must be binary .dvdag/.dvsave')
            with partial.open('xb') as output:
                index.write_binary(index.select(args.event, args.node, args.checkpoint, path), output)
            os.link(partial, args.out); partial.unlink()
        elif selected:
            value = index.select(args.event, args.node, args.checkpoint, path)
            if args.value_format == 'binary':
                index.write_binary(value, sys.stdout.buffer)
                sys.stdout.buffer.flush()
            else:
                index.write_value(value, sys.stdout, args.value_format)
                sys.stdout.write('\n')
        else:
            text = '\n'.join(f'{key}: {value}' for key, value in index.summary.items()) + '\n'
            if args.out:
                if args.out.suffix.lower() in ('.json', '.jsonl', '.gz'): raise ValueError('Use .txt/.md for inspection summaries')
                with args.out.open('x', encoding='utf-8') as output: output.write(text)
            else: sys.stdout.write(text)
        return 0 if index.summary['invalid_tail'] is None else 1


if __name__ == '__main__':
    raise SystemExit(main())
