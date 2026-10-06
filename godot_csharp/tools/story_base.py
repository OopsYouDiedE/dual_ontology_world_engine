"""`.story` 读取器共用的基础：行、缩进、键值切分。第一章与第二章的读取器都从这里取。"""
from __future__ import annotations

import re
from dataclasses import dataclass


class StoryError(Exception):
    def __init__(self, message: str, line: int = 0):
        super().__init__(message)
        self.line = line


@dataclass
class Line:
    number: int
    indent: int
    text: str


# Lines from files reached by `引入` get synthetic numbers (FILE_STRIDE per file) so the
# existing integer line plumbing stays unchanged; `origin` maps them back for diagnostics.
FILE_STRIDE = 1_000_000
_ORIGINS: dict[int, str] = {}


def register_file(path: str) -> int:
    base = (len(_ORIGINS) + 1) * FILE_STRIDE
    _ORIGINS[base] = path
    return base


def origin(number: int, root: str) -> tuple[str, int]:
    """(file, physical line) of a possibly synthetic line number."""
    base = number - number % FILE_STRIDE
    return (_ORIGINS[base], number - base) if base in _ORIGINS else (root, number)


def read_lines(raw: str, base: int = 0) -> list[Line]:
    result = []
    for number, text in enumerate(raw.splitlines(), 1):
        stripped = text.strip()
        if not stripped or stripped.startswith("#"):
            continue
        expanded = text.expandtabs(4)
        indent = len(expanded) - len(expanded.lstrip(" "))
        if indent % 4:
            raise StoryError("缩进必须是 4 个空格的整数倍。", base + number)
        result.append(Line(base + number, indent // 4, expanded.strip()))
    return result


def split_pair(line: Line) -> tuple[str, str]:
    for sep in ("：", ":"):
        if sep in line.text:
            key, value = line.text.split(sep, 1)
            return key.strip(), value.strip()
    raise StoryError(f"需要“键：值”形式：{line.text}", line.number)


def split_list(value: str) -> list[str]:
    return [item.strip() for item in re.split(r"[、,，]", value) if item.strip()]


def children(lines: list[Line], start: int, depth: int) -> tuple[list[Line], int]:
    end = start
    while end < len(lines) and lines[end].indent > depth:
        end += 1
    return lines[start:end], end


# ---- 台词编号：行尾 `[#id=…]`，与 `[#emotion=…]` 顺序不限 ----
LINE_ID = re.compile(r"[A-Za-z0-9_][A-Za-z0-9_.\-]*")
_TAG = re.compile(r"\s*\[#(emotion|id)=([^\]]*)\]\s*$")
# Every authored line seen by a parser this run: (prefix, line_id or "", line number).
AUTHORED_LINES: list[tuple[str, str, int]] = []


def split_tags(text: str, number: int) -> tuple[str, str, str]:
    """Strip trailing tags; returns (text, emotion, line_id)."""
    emotion = line_id = ""
    while (match := _TAG.search(text)) is not None:
        kind, value = match.group(1), match.group(2).strip()
        if kind == "id":
            if line_id or not LINE_ID.fullmatch(value):
                raise StoryError(f"台词编号须是唯一的 [#id=字母数字._-]：{value}", number)
            line_id = value
        else:
            if emotion:
                raise StoryError("一行只能有一个情绪标记。", number)
            emotion = value
        text = text[:match.start()]
    return text.rstrip(), emotion, line_id


def note_line(prefix: str, line_id: str, number: int) -> None:
    AUTHORED_LINES.append((prefix, line_id, number))
