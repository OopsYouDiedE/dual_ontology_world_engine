"""权威对象的唯一规范编码器。"""
from __future__ import annotations

from dataclasses import fields, is_dataclass
from enum import Enum
from fractions import Fraction
from hashlib import sha256
from types import MappingProxyType
from typing import Mapping


class CanonicalEncodingError(TypeError):
    pass


def _blob(tag: bytes, payload: bytes) -> bytes:
    return tag + str(len(payload)).encode("ascii") + b":" + payload


def canonical_bytes(value: object) -> bytes:
    if value is None: return b"n"
    if value is True: return b"b1"
    if value is False: return b"b0"
    if isinstance(value, float): raise CanonicalEncodingError("float is forbidden")
    if isinstance(value, int): return _blob(b"i", str(value).encode("ascii"))
    if isinstance(value, Fraction):
        if value.numerator.bit_length() > 256 or value.denominator.bit_length() > 256:
            raise OverflowError("exact value exceeds 256-bit policy")
        return _blob(b"q", f"{value.numerator}/{value.denominator}".encode("ascii"))
    if isinstance(value, str): return _blob(b"s", value.encode("utf-8"))
    if isinstance(value, bytes): return _blob(b"y", value)
    if isinstance(value, Enum): return _blob(b"e", canonical_bytes(type(value).__qualname__) + canonical_bytes(value.name))
    if isinstance(value, tuple): return _blob(b"t", b"".join(_blob(b"x", canonical_bytes(item)) for item in value))
    if isinstance(value, frozenset):
        items = sorted(canonical_bytes(item) for item in value)
        return _blob(b"f", b"".join(_blob(b"x", item) for item in items))
    if isinstance(value, Mapping):
        encoded = sorted((canonical_bytes(k), canonical_bytes(v)) for k, v in value.items())
        return _blob(b"m", b"".join(_blob(b"k", k) + _blob(b"v", v) for k, v in encoded))
    if isinstance(value, (list, set, bytearray)):
        raise CanonicalEncodingError(f"mutable {type(value).__name__} is forbidden")
    params = getattr(type(value), "__dataclass_params__", None)
    if is_dataclass(value) and params is not None and params.frozen:
        payload = canonical_bytes(type(value).__module__ + "." + type(value).__qualname__)
        payload += canonical_bytes(tuple((field.name, getattr(value, field.name)) for field in fields(value)))
        return _blob(b"d", payload)
    raise CanonicalEncodingError(f"unknown authoritative type: {type(value).__name__}")


def content_id(value: object) -> str:
    return sha256(canonical_bytes(value)).hexdigest()
