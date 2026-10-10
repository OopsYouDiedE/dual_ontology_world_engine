"""S03 编译合同与实体扩展夹具；运行时检查由 EntityRegistryCheck 使用。"""
import json
import re
import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
ROOT = TOOLS.parents[1]
sys.path.insert(0, str(TOOLS))
import compile_story
import story_script
from story_base import StoryError

SOURCE = compile_story.SOURCE
FIXTURE = ROOT / "godot_csharp/scenes/Shared/check/Fixtures/s03-entities.story"
NUMERIC_FIXTURE = FIXTURE.with_name("s03-numeric-id.story")
OUTPUT = ROOT / "tmp/s03-check"


def prepare():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    lines = story_script.expand_includes(SOURCE)
    text = "\n".join("    " * line.indent + line.text for line in lines) + "\n"
    text = text.split("\n运行\n", 1)[0] + "\n"
    path = OUTPUT / "extension.story"
    path.write_text(text + FIXTURE.read_text(encoding="utf-8"), encoding="utf-8")
    document = story_script.compile_story([path], {"awakening"}, section="chapter2")
    (OUTPUT / "chapter2.json").write_text(json.dumps(document, ensure_ascii=False), encoding="utf-8")
    numeric = story_script.compile_story([NUMERIC_FIXTURE], set())
    (OUTPUT / "numeric.json").write_text(json.dumps(numeric, ensure_ascii=False), encoding="utf-8")
    # Append one new option to every domain, retaining all old stable IDs and indices.
    import story_chapter1
    def extend(match):
        block = match.group(0)
        block = re.sub(r"(    取值编号：[^\n]+)", r"\1、FixtureExtra", block)
        block = re.sub(r"(    绑定：)([^\n]+)", lambda m: m[1] + m[2] + "、" + m[2].split("、")[0], block)
        return block.replace("\n    证据：", "\n        - 中性附加取值\n    证据：")
    domain_path = OUTPUT / "domains.story"
    domain_path.write_text(re.sub(r"推演变量 [^\n]+\n(?:[ \t]+[^\n]*\n|\n)*", extend, text), encoding="utf-8")
    content = compile_story.content_data(story_chapter1.load(domain_path))
    (OUTPUT / "domains.json").write_text(json.dumps(content, ensure_ascii=False), encoding="utf-8")
    return path, document


class EntityCompilerTests(unittest.TestCase):
    def test_extension_compiles_without_changing_formal_source(self):
        before = SOURCE.read_bytes()
        _, document = prepare()
        entities = {e["id"]: e for e in document["chapters"][0]["entities"]}
        self.assertEqual(entities["fixture.room"]["kind"], "room")
        self.assertEqual(entities["fixture.object"]["properties"]["space"]["value"], "world:fixture.room")
        self.assertEqual(before, SOURCE.read_bytes())

    def test_duplicate_and_unknown_entity_rejected(self):
        path, _ = prepare()
        text = path.read_text(encoding="utf-8")
        for invalid in [text + FIXTURE.read_text(encoding="utf-8"), text.replace("位置：中性测试地点", "位置：不存在的测试地点")]:
            path.write_text(invalid, encoding="utf-8")
            with self.assertRaises((StoryError, story_script.ws.ScriptError)):
                story_script.compile_story([path], {"awakening"}, section="chapter2")

    def test_numeric_id_compiles_as_an_independent_declaration(self):
        document = story_script.compile_story([NUMERIC_FIXTURE], set())
        self.assertEqual(document["chapters"][0]["entities"][0]["id"], "room.0")

    def test_domains_are_declared_and_duplicates_fail(self):
        import story_chapter1
        path, _ = prepare()
        source = path.read_text(encoding="utf-8")
        for invalid in [source.replace("取值编号：OwnRoom、Hall、Pantry", "取值编号：OwnRoom、OwnRoom、Pantry"),
                        source.replace("绑定：R08、hall、pantry", "绑定：R08、hall")]:
            path.write_text(invalid, encoding="utf-8")
            with self.assertRaises(StoryError):
                story_chapter1.load(path)


if __name__ == "__main__":
    if "--prepare" in sys.argv:
        prepare()
        print(OUTPUT)
    else:
        unittest.main()
