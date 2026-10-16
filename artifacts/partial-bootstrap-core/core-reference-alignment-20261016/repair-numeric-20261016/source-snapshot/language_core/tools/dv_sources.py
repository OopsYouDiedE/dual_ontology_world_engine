"""Resolve file includes for DV build/editor adapters; semantics remain in DV."""
from pathlib import Path
import re


def sources(entries, overrides=None):
    overrides = overrides or {}
    result, seen, stack = [], set(), []

    def visit(path):
        path = Path(path).resolve()
        if path in stack:
            raise ValueError('DV include cycle: ' + ' -> '.join(map(str, stack + [path])))
        if path in seen:
            return
        stack.append(path)
        text = overrides[path] if path in overrides else path.read_text(encoding='utf-8-sig')
        lines = text.replace('\r\n', '\n').replace('\r', '\n').splitlines(keepends=True)
        for index, line in enumerate(lines):
            if not line.lstrip().startswith('#include'):
                continue
            match = re.fullmatch(r'#include\s+(.+?)\s*', line.strip())
            if not match:
                raise ValueError(f'{path}:{index + 1}: expected #include relative/path.dv')
            relative = Path(match[1])
            if relative.is_absolute() or relative.drive or relative.suffix != '.dv':
                raise ValueError(f'{path}:{index + 1}: include requires a relative .dv path')
            try:
                visit(path.parent / relative)
            except OSError as error:
                raise ValueError(f'{path}:{index + 1}: {error}') from error
            lines[index] = '\n'
        stack.pop()
        seen.add(path)
        result.append((path, ''.join(lines) + '\n'))

    for entry in entries:
        visit(entry)
    return result
