"""Finite read-only differential checks; no world/session execution."""
import hashlib
import io
from pathlib import Path
import tempfile
import time
import unittest

from binary_evidence import MAGIC, encode, decode, read_frame, write_frame
from raw_dag import RawDagIndex


class PostprocessChecks(unittest.TestCase):
    def test_stream_journal_rejection_parity(self):
        def journal(items, damage=None):
            stream, previous = io.BytesIO(MAGIC), '0' * 64
            stream.seek(0, 2)
            payloads = [{'kind': 'header', 'format': 'dv-raw-dag-v2'},
                        {'kind': 'node', 'node_id': 0, 'items': []},
                        {'kind': 'checkpoint', 'name': 'initial', 'root': 0, 'events': 0},
                        {'kind': 'node', 'node_id': 1, 'items': items},
                        {'kind': 'checkpoint', 'name': 'final', 'root': 1, 'events': 0},
                        {'kind': 'seal', 'events': 0, 'nodes': 2, 'final_checkpoint': True}]
            if damage == 'seal': payloads[-1]['nodes'] = 3
            if damage == 'after-seal': payloads.append(payloads[1])
            for seq, payload in enumerate(payloads):
                checksum = hashlib.sha256(previous.encode() + b'\n' + str(seq).encode() + b'\n' + encode(payload)).hexdigest()
                write_frame(stream, {'seq': seq, 'prev': previous, 'payload': payload,
                                     'sha256': 'x' * 64 if damage == 'chain' and seq == 3 else checksum})
                previous = checksum
            raw = stream.getvalue()
            if damage == 'frame': raw = raw[:-1] + bytes([raw[-1] ^ 255])
            if damage == 'truncated': raw = raw[:-5]
            return raw
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / 'case.dvdag'
            for items in ([], ['正文', -0.0, False, 2**63-1, {'node': 0}],
                          [{'node': -1}], [{'node': 1}], [{'node': True}],
                          [None], [{'node': 0, 'extra': 1}],
                          [{'rational': '2/3'}], [{'rational': '2/4'}]):
                for damage in (None, 'seal', 'chain', 'frame', 'truncated', 'after-seal'):
                    source.write_bytes(journal(items, damage))
                    summaries = []
                    recovered = []
                    for stream in (False, True):
                        with RawDagIndex(source, evidence_stream=stream) as index:
                            summaries.append(index.summary)
                            index._node_cache.clear()
                            index._node_cache_bytes = 0
                            recovered.append([encode(index.node(node)) for node in range(index.summary['validated_nodes'])])
                    self.assertEqual(*summaries, (items, damage))
                    self.assertEqual(*recovered, (items, damage))
                    if damage is not None or items in ([None], [{'node': -1}], [{'node': 1}], [{'node': True}], [{'node': 0, 'extra': 1}], [{'rational': '2/4'}]):
                        self.assertFalse(summaries[1]['complete_sealing'])

    def test_exact_payload_slice(self):
        values = [None, False, True, -2**63, 2**63-1, -0.0, 1.125,
                  '原始字段\x00', [], {}, {'nested': [1, {'rational': '2/3'}]},
                  {'first': -0.0, 'second': [-(2**63), {'inner': [2**63-1, []]}]},
                  {'second': [-(2**63), {'inner': [2**63-1, []]}], 'first': -0.0}]
        for value in values:
            payload = {'kind': 'test', 'value': value}
            envelope = {'seq': 0, 'prev': '0'*64, 'payload': payload, 'sha256': 'x'}
            stream = io.BytesIO()
            write_frame(stream, envelope)
            stream.seek(0)
            raw, decoded, span = read_frame(stream, payload_span=True)
            encoded = encode(payload)
            self.assertEqual(bytes(span), encoded)
            self.assertEqual(encode(decode(bytes(span))), encoded)
            self.assertEqual(encode(decoded), encode(envelope))

    def test_map_order_is_preserved(self):
        forward = encode({'a': 1, 'b': [2, {'c': -0.0}]})
        reverse = encode({'b': [2, {'c': -0.0}], 'a': 1})
        self.assertNotEqual(forward, reverse)
        for payload in (forward, reverse):
            self.assertEqual(encode(decode(payload)), payload)

    def test_indexed_decoder_differential(self):
        import random
        from binary_evidence import decode_indexed
        rng = random.Random(180)
        values = [None, -0.0, -2**63, 2**63-1, [], {},
                  {'b': [1, {'nested': '原始'}], 'a': False},
                  {'seq': 4, 'prev': '0'*64,
                   'payload': {'kind': 'node', 'node_id': 3, 'items': ['x', {'node': 0}]},
                   'sha256': 'f'*64}]
        cases = [encode(value) for value in values]
        import struct
        cases += [b'\x04' + struct.pack('<d', float('nan')),
                  b'\x05\x01\x00\x00\x00\xff',
                  b'\x07\x02\x00\x00\x00' + encode('same') + encode(1) + encode('same') + encode(2)]
        for encoded in list(cases):
            cases.extend(encoded[:length] for length in range(len(encoded)))
            for offset in range(len(encoded)):
                damaged = bytearray(encoded)
                damaged[offset] ^= 255
                cases.append(bytes(damaged))
        cases += [rng.randbytes(rng.randrange(80)) for _ in range(1000)]
        def outcome(decoder, payload):
            try:
                result = decoder(payload)
                return ('value', encode(result))
            except (ValueError, RecursionError) as issue:
                return ('error', type(issue), str(issue))
        for payload in cases:
            self.assertEqual(outcome(decode, payload), outcome(decode_indexed, payload), payload)
            self.assertEqual(outcome(decode, payload), outcome(lambda p: decode_indexed(p, evidence_stream=True), payload), payload)
        # Make the documented format depth limit reachable independently of the
        # interpreter stack ceiling and its context-dependent error wording.
        import sys
        old_limit = sys.getrecursionlimit()
        try:
            sys.setrecursionlimit(5000)
            deep = b'\x08\x01\x00\x00\x00' * 514 + b'\x00'
            self.assertEqual(outcome(decode, deep), outcome(decode_indexed, deep))
        finally:
            sys.setrecursionlimit(old_limit)

    def test_projection_on_off(self):
        from run_debug import export
        from binary_evidence import RecordWriter
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            event = {'type': 'action_execution', 'event_id': 'e0', 'actor': 'a',
                     'game_time': '1/3', 'parents': ['before'], 'label': 'raw',
                     'action_id': 'act', 'detail': {'status': 'ok', 'score': -0.0}}
            with RecordWriter(root / 'stdout.dvlog') as output:
                output.append({'type': 'debug_start', 'configuration': [], 'run_config': []})
                output.append({'type': 'debug_events', 'through_step': 1,
                               'batch': {'next': 1, 'events': [event]}})
                event = {**event, 'type': 'mind_simulation', 'event_id': 'e1',
                         'detail': {'hypothesis': [], 'result': {'stop_reason': 'finite'}}}
                output.append({'type': 'debug_events', 'through_step': 2,
                               'batch': {'next': 2, 'events': [event]}})
                output.append({'type': 'debug_end', 'stop_evidence': {'reasons': []}})
            outputs = []
            for optimize in (False, True):
                destination = root / str(optimize)
                destination.mkdir()
                export(root, destination=destination, optimize=optimize)
                outputs.append({p.name: p.read_bytes() for p in destination.iterdir()})
            self.assertEqual(outputs[0], outputs[1])

    def test_cache_eviction(self):
        # Exercise both entry and wire-size eviction without allocating a world.
        index = object.__new__(RawDagIndex)
        from collections import OrderedDict
        index.optimize, index._node_cache, index._node_cache_bytes = True, OrderedDict(), 0
        for node in range(8200):
            index._cache_node(node, [node], 1)
        self.assertEqual(len(index._node_cache), 8192)
        self.assertNotIn(0, index._node_cache)
        index._cache_node(9000, [], 16 * 1024 * 1024)
        self.assertEqual(list(index._node_cache), [9000])
        index._cache_node(9001, [], 16 * 1024 * 1024 + 1)
        self.assertNotIn(9001, index._node_cache)


def benchmark(events=1500):
    """Bounded synthetic metadata-heavy journal, comparing complete outputs."""
    stream, previous, sequence, nodes = io.BytesIO(), '0'*64, 0, 0
    stream.write(MAGIC)
    def frame(payload):
        nonlocal previous, sequence
        previous_hash = previous
        previous = hashlib.sha256(previous.encode() + b'\n' + str(sequence).encode() + b'\n' + encode(payload)).hexdigest()
        write_frame(stream, {'seq': sequence, 'prev': previous_hash, 'payload': payload, 'sha256': previous})
        sequence += 1
    def node(items):
        nonlocal nodes
        identity = nodes
        frame({'kind': 'node', 'node_id': identity, 'items': items})
        nodes += 1
        return {'node': identity}
    frame({'kind': 'header', 'format': 'dv-raw-dag-v2'})
    empty = node([])
    frame({'kind': 'checkpoint', 'name': 'initial', 'root': empty['node'], 'events': 0})
    common = [node(['time', {'rational': '1/3'}]), node(['type', 'action_execution']),
              node(['actor', '原始主体']), node(['parents', empty])]
    for event in range(events):
        identity = node(['id', 'e' + str(event)])
        data = node(['data', node(['原始正文' * 60, event, -0.0])])
        root = node([identity, *common, data])
        frame({'kind': 'event', 'root': root['node'], 'ordinal': event, 'event_id': 'e' + str(event)})
    frame({'kind': 'checkpoint', 'name': 'final', 'root': root['node'], 'events': events})
    frame({'kind': 'seal', 'events': events, 'nodes': nodes, 'final_checkpoint': True})
    with tempfile.TemporaryDirectory(prefix='postprocess-finite-') as temporary:
        folder = Path(temporary)
        source = folder / 'source.dvdag'
        source.write_bytes(stream.getvalue())
        results, timings = [], []
        for optimize in (False, True):
            started = time.perf_counter()
            with RawDagIndex(source, optimize=optimize) as index:
                output = folder / (str(optimize) + '.dvlog')
                index.write_event_index(output)
                results.append((index.summary, output.read_bytes()))
            timings.append(time.perf_counter() - started)
        assert results[0] == results[1], 'Complete summary/index mismatch'
        print(f'synthetic_bytes={source.stat().st_size} nodes={nodes} events={events}')
        print(f'index_off_seconds={timings[0]:.6f} index_on_seconds={timings[1]:.6f} speedup={timings[0]/timings[1]:.3f}')


if __name__ == '__main__':
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(PostprocessChecks)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():
        raise SystemExit(1)
    benchmark()
