"""分段编译等价：把 chapter_01.story 在任意顶层语句边界切开、按序引入，生成物必须与整份编译相同。

源位置映射（source_map）记录的是作者文件与行号，切分后理应指向片段文件，因此比较时剔除；
其余运行时数据、布局、对白与续篇 IR 必须逐项相同。
"""
import json
import random
import re
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import compile_story as C  # noqa: E402
from story_base import StoryError  # noqa: E402
from world_script import ScriptError  # noqa: E402


SOURCE_ID = re.compile(r".+\.story:\d+:\d+")


def statements(raw: str) -> tuple[str, list[tuple[str, str]]]:
    """Return the chapter header and ordered (scope, text) statements; scope is '' or a 续篇 id."""
    lines = raw.splitlines(keepends=True)
    header, result, scope, current = "", [], "", None

    def flush():
        if current is not None:
            result.append((current[0], "".join(current[1])))

    for text in lines:
        stripped = text.strip()
        indent = len(text) - len(text.lstrip(" "))
        if not stripped or stripped.startswith("#"):
            if current is not None:
                current[1].append(text)
            continue
        if indent == 0 and stripped.startswith("章节："):
            header = text
            continue
        if indent == 0 and stripped.startswith("续篇 "):
            flush()
            current, scope = None, stripped[len("续篇 "):]
            continue
        if indent == 0:
            flush()
            current, scope = ("", [text]), ""
        elif scope and indent == 4:
            flush()
            current = (scope, [text])
        else:
            current[1].append(text)
    flush()
    return header, result


def write_split(folder: Path, header: str, items: list[tuple[str, str]], cuts: list[int]) -> Path:
    bounds = [0, *cuts, len(items)]
    includes = []
    for index, (start, end) in enumerate(zip(bounds, bounds[1:])):
        chunk, last_scope = [], None
        for scope, text in items[start:end]:
            if scope and scope != last_scope:
                chunk.append(f"续篇 {scope}\n")
            last_scope = scope
            chunk.append(text)
        name = f"part_{index:03d}.story"
        (folder / name).write_text("".join(chunk), encoding="utf-8")
        includes.append(f"引入：{name}\n")
    root = folder / C.SOURCE.name
    root.write_text(header + "".join(includes), encoding="utf-8")
    return root


def normalized(outputs: dict[Path, str]) -> dict[str, object]:
    def strip(value):
        if isinstance(value, dict):
            return {k: strip(v) for k, v in value.items() if k != "source_map"}
        if isinstance(value, list):
            return [strip(v) for v in value]
        if isinstance(value, str) and SOURCE_ID.fullmatch(value):
            # Source IDs locate the translated root text; only the folder differs.
            return value.rsplit("/", 1)[-1]
        return value
    result = {}
    for path, text in outputs.items():
        result[path.name] = strip(json.loads(text)) if path.suffix == ".json" else text
    return result


class StorySplit(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.raw = C.SOURCE.read_text(encoding="utf-8-sig")
        cls.header, cls.items = statements(cls.raw)
        cls.whole = normalized(C.generated_files())

    def check(self, cuts):
        with tempfile.TemporaryDirectory() as folder:
            root = write_split(Path(folder), self.header, self.items, cuts)
            self.assertEqual(normalized(C.generated_files(root)), self.whole, f"cuts={cuts}")

    def test_statement_inventory_covers_both_scopes(self):
        scopes = {scope for scope, _ in self.items}
        self.assertEqual(scopes, {"", "chapter2"})
        self.assertGreater(len(self.items), 200)

    def test_every_statement_in_its_own_file(self):
        self.check(list(range(1, len(self.items))))

    def test_random_partitions(self):
        rng = random.Random(20261006)
        for parts in (2, 3, 7, 20):
            self.check(sorted(rng.sample(range(1, len(self.items)), parts - 1)))

    def test_cut_inside_continuation_reopens_scope(self):
        first = next(i for i, (scope, _) in enumerate(self.items) if scope)
        self.check([first + 1, first + 5, len(self.items) - 3])

    def test_error_names_fragment_and_line(self):
        with tempfile.TemporaryDirectory() as folder:
            folder = Path(folder)
            root = write_split(folder, self.header, self.items, [5])
            part = folder / "part_001.story"
            part.write_text("未知块示例\n    x：1\n" + part.read_text(encoding="utf-8"), encoding="utf-8")
            with self.assertRaises((StoryError, ScriptError)) as caught:
                C.generated_files(root)
            error = caught.exception
            if isinstance(error, StoryError):
                file, line = C.origin(error.line, C.SOURCE.name)
            else:
                file, line = error.source.path, error.source.line
            self.assertTrue(file.endswith("part_001.story"), file)
            self.assertEqual(line, 1)

    def test_prerequisite_only_in_first_continuation_block(self):
        with tempfile.TemporaryDirectory() as folder:
            folder = Path(folder)
            first = next(i for i, (scope, _) in enumerate(self.items) if scope)
            items = list(self.items)
            items.insert(first + 3, ("chapter2", "    前置章节：awakening\n"))
            root = write_split(folder, self.header, items, [first + 3])
            with self.assertRaises((StoryError, ScriptError)):
                C.generated_files(root)


if __name__ == "__main__":
    unittest.main()
