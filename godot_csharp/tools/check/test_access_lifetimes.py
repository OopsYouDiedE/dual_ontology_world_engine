"""通行声明与规则/触发器有效期的编译合同回归，使用中性夹具。"""
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from story_access import compile_access
from story_base import StoryError, read_lines
from story_design import nodes
from story_rules import compile_rules
from test_story_script import HEAD
import story_script

def compile_text(text):
    directory = Path(__file__).resolve().parents[3] / "tmp" / "s21s16-compiler-check"
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / "neutral.story"
    path.write_text(text, encoding="utf-8")
    return story_script.compile_story([path], set())

ACCESS = """通行
    boundaries:
        fixture_gate:
            kind: corridor
            initial: false
            rooms: world:room.A
    routes:
        route.A:
            name: A
            to: world:room.A
            from: world:room.B
            visible_from: world:room.B
        route.B:
            name: B
            to: world:room.B
            from: all
            visible_from: world:room.A
    arrivals:
        person.X: world:room.B
    summons:
        fixture_message:
            sender: world:person.X
            people: world:person.X
            to: world:room.A
            text: neutral fixture
            compatibility: 2
"""


class AccessLifetimes(unittest.TestCase):
    def access(self, text=ACCESS):
        lines = read_lines(text)
        return compile_access(lines[1:], SimpleNamespace(dorms={"a": "A", "b": "B"}, person_enum={"x": "X"}), lines[0])

    def test_stable_ids_topology_and_message_references(self):
        data = self.access()
        self.assertEqual(data["routes"]["route.A"]["from"], ["world:room.B"])
        self.assertEqual(data["routes"]["route.B"]["from"], ["world:room.A", "world:room.B"])
        self.assertFalse(data["boundaries"]["fixture_gate"]["initial"])
        self.assertEqual(data["summons"]["fixture_message"]["people"], ["world:person.X"])

    def test_unknown_or_duplicate_references_rejected(self):
        for text in [ACCESS.replace("rooms: world:room.A", "rooms: world:room.Absent"),
                     ACCESS.replace("rooms: world:room.A", "rooms: world:room.A、world:room.A"),
                     ACCESS.replace("people: world:person.X", "people: world:person.Absent")]:
            with self.subTest(text=text), self.assertRaises(StoryError):
                self.access(text)

    def test_incomplete_or_duplicate_destinations_rejected(self):
        for text in [ACCESS.replace("to: world:room.B", "to: world:room.A"),
                     ACCESS.replace("initial: false", "initial: open"),
                     ACCESS.replace("fixture_gate:", "world:bad:")]:
            with self.subTest(text=text), self.assertRaises(StoryError):
                self.access(text)

    def test_trigger_lifetime_compiles_all_scopes(self):
        for scope in ("world", "chapter", "interaction"):
            doc = compile_text(HEAD + f"场面 fixture\n    事件：neutral\n    有效期：{scope}\n    中性测试。\n")
            self.assertEqual(doc["chapters"][0]["sequences"][0]["trigger"]["lifetime"], scope)

    def test_unknown_trigger_lifetime_rejected(self):
        with self.assertRaises(Exception) as error:
            compile_text(HEAD + "场面 fixture\n    事件：neutral\n    有效期：forever\n    中性测试。\n")
        self.assertIn("有效期", str(error.exception))

    def test_rule_lifetime_compiles_all_scopes_and_rejects_unknown(self):
        ctx = SimpleNamespace(vocab=SimpleNamespace(mapping=lambda _: {"中性": "Neutral"}), person_enum={"甲": "X"})
        for scope in ("world", "chapter", "interaction", "invalid"):
            lines = read_lines(f"规矩 中性\n    编号：fixture\n    立规者：甲\n    内容：中性测试\n    标记：中性\n    有效期：{scope}\n")
            if scope == "invalid":
                with self.assertRaises(StoryError):
                    compile_rules([(lines[0], nodes(lines[1:]))], ctx)
            else:
                self.assertEqual(compile_rules([(lines[0], nodes(lines[1:]))], ctx)[0]["lifetime"], scope)


if __name__ == "__main__":
    unittest.main()
