"""Strict JSON reads and string-only edits that preserve resource serialization."""
from __future__ import annotations

import json
import re
from pathlib import Path


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def parse_json(source: str):
    return json.loads(source, object_pairs_hook=unique_object)


def read_json(path: Path):
    return parse_json(path.read_bytes().decode("utf-8"))


def require_strings(table, keys, namespace=()):
    if not isinstance(table, dict):
        raise ValueError("Text table must be an object")
    keys = tuple(keys)
    if len(set(keys)) != len(keys):
        raise ValueError("Duplicate text binding")
    for key in keys:
        if key not in table or not isinstance(table[key], str):
            raise ValueError(f"Missing or non-string text: {key}")
    unknown = {key for key in table if key.startswith(namespace)} - set(keys)
    if unknown:
        raise ValueError(f"Unknown text binding: {sorted(unknown)}")
    return {key: table[key] for key in keys}


def replace_spans(source: str, edits: list[tuple[int, int, str]]) -> str:
    for start, end, value in sorted(edits, reverse=True):
        source = source[:start] + value + source[end:]
    return source


def replace_json_strings(source: str, replacements: dict[tuple, str]) -> str:
    """Locate JSON value tokens by path; retain all other bytes, including escapes."""
    parse_json(source)
    decoder = json.JSONDecoder()
    found, edits = set(), []

    def skip(pos):
        return re.compile(r"\s*").match(source, pos).end()

    def walk(pos, path):
        pos = skip(pos)
        start = pos
        if source[pos] == "{":
            pos = skip(pos + 1)
            while source[pos] != "}":
                key, pos = decoder.raw_decode(source, pos)
                pos = walk(skip(pos) + 1, (*path, key))
                pos = skip(pos)
                if source[pos] == ",":
                    pos = skip(pos + 1)
            pos += 1
            value = None
        elif source[pos] == "[":
            pos, index = skip(pos + 1), 0
            while source[pos] != "]":
                pos = skip(walk(pos, (*path, index)))
                index += 1
                if source[pos] == ",":
                    pos = skip(pos + 1)
            pos += 1
            value = None
        else:
            value, pos = decoder.raw_decode(source, pos)
        if path in replacements:
            new = replacements[path]
            if not isinstance(value, str) or not isinstance(new, str):
                raise ValueError(f"Non-string JSON target: {path}")
            found.add(path)
            if value != new:
                edits.append((start, pos, json.dumps(new, ensure_ascii=False)))
        return pos

    walk(0, ())
    if found != set(replacements):
        raise ValueError(f"Missing JSON targets: {set(replacements) - found}")
    return replace_spans(source, edits)


def publish(candidates: dict[Path, bytes]) -> int:
    """Called only after the entire input batch has been validated and prepared."""
    changed = {path: data for path, data in candidates.items() if path.read_bytes() != data}
    for path, data in changed.items():
        path.write_bytes(data)
    return len(changed)
