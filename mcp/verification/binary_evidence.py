"""Lossless binary record frames. JSON is accepted only when reading old evidence.

DVLOG2 files contain typed values, not JSON bytes: magic, then repeated
little-endian uint32 payload lengths, SHA-256 payload hashes and binary payloads.
The raw DAG adds its ordered checksum chain and commit/seal protocol above this.
"""
import hashlib
import io
import json
import math
from pathlib import Path
import struct

MAGIC = b'DVLOG2\r\n'
MAX_FRAME = 256 * 1024 * 1024
_INT64 = struct.Struct('<q').unpack_from
_DOUBLE = struct.Struct('<d').unpack_from
_UINT32 = struct.Struct('<I').unpack_from
_NODE_ATOM = b'\x07\x01\x00\x00\x00\x05\x04\x00\x00\x00node\x03'


def encode(value):
    out = io.BytesIO()
    def put(v, depth=0):
        if depth > 512:
            raise ValueError('Binary evidence nesting exceeds 512')
        if v is None: out.write(b'\x00')
        elif v is False: out.write(b'\x01')
        elif v is True: out.write(b'\x02')
        elif type(v) is int:
            out.write(b'\x03' + struct.pack('<q', v))
        elif type(v) is float:
            if not math.isfinite(v): raise ValueError('Nonfinite evidence number')
            out.write(b'\x04' + struct.pack('<d', v))
        elif isinstance(v, str):
            raw = v.encode('utf-8', errors='strict')
            out.write(b'\x05' + struct.pack('<I', len(raw)) + raw)
        elif isinstance(v, dict):
            out.write(b'\x07' + struct.pack('<I', len(v)))
            for k, item in v.items():
                if not isinstance(k, str): raise ValueError('Map keys must be text')
                put(k, depth + 1); put(item, depth + 1)
        elif isinstance(v, (list, tuple)):
            out.write(b'\x08' + struct.pack('<I', len(v)))
            for item in v: put(item, depth + 1)
        else: raise ValueError(f'Unsupported evidence atom: {type(v)}')
    put(value)
    return out.getvalue()


def decode(payload, spans=None):
    source = io.BytesIO(payload)
    def read(n):
        data = source.read(n)
        if len(data) != n: raise ValueError('Truncated binary value')
        return data
    def get(depth=0):
        if depth > 512: raise ValueError('Binary evidence nesting exceeds 512')
        tag = read(1)[0]
        if tag == 0: return None
        if tag == 1: return False
        if tag == 2: return True
        if tag == 3: return struct.unpack('<q', read(8))[0]
        if tag == 4:
            value = struct.unpack('<d', read(8))[0]
            if not math.isfinite(value): raise ValueError('Nonfinite evidence number')
            return value
        if tag == 5:
            size = struct.unpack('<I', read(4))[0]
            return read(size).decode('utf-8', errors='strict')
        if tag in (7, 8):
            count = struct.unpack('<I', read(4))[0]
            if count > len(payload): raise ValueError('Invalid binary collection length')
            if tag == 8: return [get(depth + 1) for _ in range(count)]
            value = {}
            track_spans = spans is not None and depth == 0
            for _ in range(count):
                key = get(depth + 1)
                if not isinstance(key, str) or key in value: raise ValueError('Invalid/duplicate binary map key')
                start = source.tell() if track_spans else 0
                value[key] = get(depth + 1)
                if track_spans:
                    spans[key] = (start, source.tell())
            return value
        raise ValueError(f'Unknown binary value tag: {tag}')
    value = get()
    if source.read(1): raise ValueError('Trailing binary value bytes')
    return value


def _decode_cursor(payload, spans=None, start=0, end=None, depth=0):
    """Equivalent cursor decoder for indexed reads; no per-atom BytesIO reads."""
    position, limit = start, len(payload) if end is None else end
    integer, real, length = _INT64, _DOUBLE, _UINT32
    def get(depth=0):
        nonlocal position
        if depth > 512: raise ValueError('Binary evidence nesting exceeds 512')
        if position >= limit: raise ValueError('Truncated binary value')
        tag = payload[position]
        position += 1
        if tag == 0: return None
        if tag == 1: return False
        if tag == 2: return True
        if tag == 3 or tag == 4:
            if position + 8 > limit: raise ValueError('Truncated binary value')
            value = integer(payload, position)[0] if tag == 3 else real(payload, position)[0]
            position += 8
            if tag == 4 and not math.isfinite(value): raise ValueError('Nonfinite evidence number')
            return value
        if tag == 5 or tag == 7 or tag == 8:
            if position + 4 > limit: raise ValueError('Truncated binary value')
            count = length(payload, position)[0]
            position += 4
            if tag == 5:
                end = position + count
                if end > limit: raise ValueError('Truncated binary value')
                value = payload[position:end].decode('utf-8', errors='strict')
                position = end
                return value
            if count > limit: raise ValueError('Invalid binary collection length')
            if tag == 8:
                if depth > 510: return [get(depth + 1) for _ in range(count)]
                values = []
                for _ in range(count):
                    if position >= limit: raise ValueError('Truncated binary value')
                    atom = payload[position]
                    if atom == 5:
                        if position + 5 > limit: raise ValueError('Truncated binary value')
                        size = length(payload, position + 1)[0]
                        end = position + 5 + size
                        if end > limit: raise ValueError('Truncated binary value')
                        values.append(payload[position + 5:end].decode('utf-8', errors='strict'))
                        position = end
                    elif atom == 3 or atom == 4:
                        if position + 9 > limit: raise ValueError('Truncated binary value')
                        value = integer(payload, position + 1)[0] if atom == 3 else real(payload, position + 1)[0]
                        if atom == 4 and not math.isfinite(value): raise ValueError('Nonfinite evidence number')
                        values.append(value)
                        position += 9
                    elif atom < 3:
                        values.append(None if atom == 0 else atom == 2)
                        position += 1
                    elif atom == 7 and payload.startswith(_NODE_ATOM, position) and position + 23 <= limit:
                        values.append({'node': integer(payload, position + 15)[0]})
                        position += 23
                    else:
                        values.append(get(depth + 1))
                return values
            value = {}
            track_spans = spans is not None and depth == 0
            for _ in range(count):
                key = get(depth + 1)
                if not isinstance(key, str) or key in value: raise ValueError('Invalid/duplicate binary map key')
                start = position
                value[key] = get(depth + 1)
                if track_spans: spans[key] = (start, position)
            return value
        raise ValueError(f'Unknown binary value tag: {tag}')
    value = get(depth)
    if position != limit: raise ValueError('Trailing binary value bytes')
    return value


# Exact fixed-shape matching only elides generic decoding of constant schema
# atoms. Every variable value is still decoded and every hash is still checked.
_NODE_START = b'\x07' + struct.pack('<I', 4) + encode('seq') + b'\x03'
_NODE_PREV = encode('prev') + b'\x05' + struct.pack('<I', 64)
_NODE_PAYLOAD_KEY = encode('payload')
_NODE_PAYLOAD = (b'\x07' + struct.pack('<I', 3) + encode('kind') + encode('node')
                 + encode('node_id') + b'\x03')
_NODE_ITEMS = encode('items')
_NODE_SHA = encode('sha256') + b'\x05' + struct.pack('<I', 64)


def _decode_dag_items(payload, start, end, node_id):
    """Decode and validate flat DV atoms together; unsupported shapes fall back."""
    if payload[start:start + 1] != b'\x08' or start + 5 > end:
        raise ValueError('Not a flat node list')
    count = _UINT32(payload, start + 1)[0]
    position = start + 5
    if count > end: raise ValueError('Invalid binary collection length')
    items = []
    for _ in range(count):
        if position >= end: raise ValueError('Truncated binary value')
        tag = payload[position]
        if tag == 5:
            if position + 5 > end: raise ValueError('Truncated binary value')
            size = _UINT32(payload, position + 1)[0]
            stop = position + 5 + size
            if stop > end: raise ValueError('Truncated binary value')
            items.append(payload[position + 5:stop].decode('utf-8', errors='strict'))
            position = stop
        elif tag == 3 or tag == 4:
            if position + 9 > end: raise ValueError('Truncated binary value')
            value = (_INT64 if tag == 3 else _DOUBLE)(payload, position + 1)[0]
            if tag == 4 and not math.isfinite(value): raise ValueError('Nonfinite evidence number')
            items.append(value)
            position += 9
        elif tag == 1 or tag == 2:
            items.append(tag == 2)
            position += 1
        elif tag == 7 and position + 23 <= end and payload.startswith(_NODE_ATOM, position):
            reference = _INT64(payload, position + 15)[0]
            if reference < 0 or reference >= node_id: raise ValueError('Invalid node reference')
            items.append({'node': reference})
            position += 23
        else:
            # Includes rational atoms: the reference decoder and validator keep
            # their existing diagnostics and canonical Fraction checks.
            raise ValueError('Use generic node validation')
    if position != end: raise ValueError('Trailing binary value bytes')
    return items


def decode_indexed(payload, spans=None, evidence_stream=False):
    """Decode indexed reads, recognizing the exact common node envelope shape."""
    position = len(_NODE_START)
    if payload.startswith(_NODE_START) and payload.startswith(_NODE_PREV, position + 8):
        sequence = struct.unpack_from('<q', payload, position)[0]
        position += 8 + len(_NODE_PREV)
        previous_start = position
        position += 64
        if payload.startswith(_NODE_PAYLOAD_KEY, position):
            position += len(_NODE_PAYLOAD_KEY)
            payload_start = position
            if payload.startswith(_NODE_PAYLOAD, position):
                position += len(_NODE_PAYLOAD)
                if position + 8 <= len(payload):
                    node_id = struct.unpack_from('<q', payload, position)[0]
                    position += 8
                    suffix = len(payload) - len(_NODE_SHA) - 64
                    if payload.startswith(_NODE_ITEMS, position) and suffix >= position + len(_NODE_ITEMS) and payload.startswith(_NODE_SHA, suffix):
                        items_start = position + len(_NODE_ITEMS)
                        try:
                            # Atoms have the same original nesting depth (items=2).
                            validated = False
                            if evidence_stream:
                                try:
                                    items = _decode_dag_items(payload, items_start, suffix, node_id)
                                    validated = True
                                except (ValueError, UnicodeDecodeError):
                                    items = _decode_cursor(payload, start=items_start, end=suffix, depth=2)
                            else:
                                items = _decode_cursor(payload, start=items_start, end=suffix, depth=2)
                            previous = payload[previous_start:previous_start + 64].decode('utf-8', errors='strict')
                            checksum = payload[-64:].decode('utf-8', errors='strict')
                        except (ValueError, UnicodeDecodeError, RecursionError):
                            # Preserve the generic decoder's exact first failure.
                            return decode(payload, spans)
                        if spans is not None:
                            spans['payload'] = (payload_start, suffix)
                            spans['validated_node_items'] = validated
                        return {'seq': sequence, 'prev': previous,
                                'payload': {'kind': 'node', 'node_id': node_id, 'items': items},
                                'sha256': checksum}
    try:
        return _decode_cursor(payload, spans)
    except (ValueError, RecursionError):
        # Invalid evidence retains the established exact diagnostic behavior.
        return decode(payload, spans)


def write_frame(stream, value):
    payload = encode(value)
    if len(payload) > MAX_FRAME: raise ValueError('Binary frame exceeds size limit')
    stream.write(struct.pack('<I', len(payload)))
    stream.write(hashlib.sha256(payload).digest())
    stream.write(payload)


def read_frame(stream, payload_span=False, evidence_stream=False):
    """Validate/decode a frame; optionally expose exact top-level payload bytes.

    Typed values have a unique encoding after validation (including map order and
    negative zero). The span allows DAG chain validation without re-encoding.
    With both options enabled, a fourth result marks fully validated flat DV
    node items; callers must still verify node order, envelope, chain and commits.
    """
    prefix = stream.read(4)
    if not prefix: return None
    if len(prefix) != 4: raise ValueError('Truncated binary frame length')
    size = struct.unpack('<I', prefix)[0]
    if size > MAX_FRAME: raise ValueError('Binary frame exceeds size limit')
    checksum, payload = stream.read(32), stream.read(size)
    if len(checksum) != 32 or len(payload) != size: raise ValueError('Truncated binary frame')
    if hashlib.sha256(payload).digest() != checksum: raise ValueError('Binary frame checksum mismatch')
    spans = {} if payload_span else None
    value = decode_indexed(payload, spans, evidence_stream=evidence_stream) if payload_span else decode(payload)
    frame = (prefix + checksum + payload, value)
    if payload_span:
        span = spans.get('payload')
        result = (*frame, memoryview(payload)[slice(*span)] if span is not None else None)
        return (*result, spans.get('validated_node_items', False)) if evidence_stream else result
    return frame


def iter_packets(source):
    """Yield original bytes plus decoded values; old JSON is read-only compatible."""
    prefix = source.read(len(MAGIC))
    if prefix != MAGIC:
        source.seek(0)
        for line in source:
            yield line, None
        return
    first = True
    while True:
        frame = read_frame(source)
        if frame is None: return
        raw, value = frame
        yield (MAGIC + raw if first else raw), value
        first = False


def records(path):
    with Path(path).open('rb') as source:
        for _, value in iter_packets(source):
            if value is not None: yield value
            elif _.strip(): yield json.loads(_)


class RecordWriter:
    def __init__(self, path):
        self.path = Path(path)
        if any(s.lower() in ('.json', '.jsonl', '.gz') for s in self.path.suffixes):
            raise ValueError('New recordings must use a binary extension')
        self.stream = self.path.open('xb')
        self.stream.write(MAGIC)
    def append(self, value): write_frame(self.stream, value)
    def write(self, text):
        # Adapter for existing projection producers; JSON exists in memory only.
        if text.strip(): self.append(json.loads(text))
    def close(self): self.stream.close()
    def __enter__(self): return self
    def __exit__(self, *_): self.close()
