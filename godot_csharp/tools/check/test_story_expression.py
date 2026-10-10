"""Neutral compiler contracts for authored response conditions and localization."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from story_script import compile_story, StoryError
from story_expression import validate_expression


class ExpressionTests(unittest.TestCase):
    def compile(self, source):
        # A single file needs no private directory ACL; inherit the workspace's sandbox permissions.
        folder = Path(__file__).resolve().parents[3] / 'tmp'
        folder.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', suffix='.story', dir=folder, delete=False) as file:
            file.write(source)
            path = Path(file.name)
        try:
            return compile_story([path], set())['chapters'][0]
        finally:
            path.unlink()

    def test_localization_does_not_change_conditions_or_world(self):
        source = '''章节：neutral
表达
    response：
        r：
            text：neutral.words
            priority：2
            when：
                speaker：actor.a
                listener：actor.b
                known:identity：true
文本
    neutral：
        words：original words
'''
        first = self.compile(source)
        second = self.compile(source.replace('original words', 'translated words with different punctuation!'))
        self.assertEqual(first['design']['expression'], second['design']['expression'])
        self.assertEqual(first['entities'], second['entities'])
        self.assertNotEqual(first['design']['text'], second['design']['text'])

    def test_rejects_missing_text(self):
        with self.assertRaises(StoryError): validate_expression({'g': {'r': {'when': {'known:x': True}}}}, 1)

    def test_rejects_noninteger_priority(self):
        for value in [True, 1.5, 'high']:
            with self.assertRaises(StoryError): validate_expression({'g': {'r': {'text': 'key', 'priority': value}}}, 1)

    def test_rejects_nested_condition(self):
        with self.assertRaises(StoryError): validate_expression({'g': {'r': {'text': 'key', 'when': {'x': {'v': 1}}}}}, 1)

    def test_rejects_invalid_comparison(self):
        with self.assertRaises(StoryError): validate_expression({'g': {'r': {'text': 'key', 'when': {'x': '>words'}}}}, 1)

    def test_allows_neutral_ids_and_conditions(self):
        table = {'g': {'r': {'text': 'key', 'when': {'speaker': 'new.person', 'known:identity': False, 'tag:object:count': '>=2'}}}}
        self.assertIs(validate_expression(table, 1), table)


if __name__ == '__main__': unittest.main()
