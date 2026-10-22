"""Finite read-only differential checks; no world/session execution."""
import hashlib
import io
from pathlib import Path
import tempfile
import time
import unittest

from binary_evidence import MAGIC, encode, read_frame, write_frame
from raw_dag import RawDagIndex


class PostprocessChecks(unittest.TestCase):
    def test_exact_payload_slice(self):
        values = [None, False, True, -2**63, 2**63-1, -0.0, 1.125,
                  '原始字段\x00', [], {}, {'nested': [1, {'rational': '2/3'}]}]
        for value in values:
            payload = {'kind': 'test', 'value': value}
            envelope = {'seq': 0, 'prev': '0'*64, 'payload': payload, 'sha256': 'x'}
            stream = io.BytesIO()
            write_frame(stream, envelope)
            stream.seek(0)
            raw, decoded, span = read_frame(stream, payload_span=True)
            self.assertEqual(bytes(span), encode(payload))
            self.assertEqual(encode(decoded), encode(envelope))

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
