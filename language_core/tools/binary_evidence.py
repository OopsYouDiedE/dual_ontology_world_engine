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


def decode(payload):
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
            for _ in range(count):
                key = get(depth + 1)
                if not isinstance(key, str) or key in value: raise ValueError('Invalid/duplicate binary map key')
                value[key] = get(depth + 1)
            return value
        raise ValueError(f'Unknown binary value tag: {tag}')
    value = get()
    if source.read(1): raise ValueError('Trailing binary value bytes')
    return value


def write_frame(stream, value):
    payload = encode(value)
    if len(payload) > MAX_FRAME: raise ValueError('Binary frame exceeds size limit')
    stream.write(struct.pack('<I', len(payload)))
    stream.write(hashlib.sha256(payload).digest())
    stream.write(payload)


def read_frame(stream):
    prefix = stream.read(4)
    if not prefix: return None
    if len(prefix) != 4: raise ValueError('Truncated binary frame length')
    size = struct.unpack('<I', prefix)[0]
    if size > MAX_FRAME: raise ValueError('Binary frame exceeds size limit')
    checksum, payload = stream.read(32), stream.read(size)
    if len(checksum) != 32 or len(payload) != size: raise ValueError('Truncated binary frame')
    if hashlib.sha256(payload).digest() != checksum: raise ValueError('Binary frame checksum mismatch')
    return prefix + checksum + payload, decode(payload)


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
