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


def read_lines(raw: str) -> list[Line]:
    result = []
    for number, text in enumerate(raw.splitlines(), 1):
        stripped = text.strip()
        if not stripped or stripped.startswith("#"):
            continue
        expanded = text.expandtabs(4)
        indent = len(expanded) - len(expanded.lstrip(" "))
        if indent % 4:
            raise StoryError("缩进必须是 4 个空格的整数倍。", number)
        result.append(Line(number, indent // 4, expanded.strip()))
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
