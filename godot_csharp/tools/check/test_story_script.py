"""story_script 编译器回归：成功路径、行号报错、前向引用。"""
import sys, tempfile, unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import story_script as S
import world_script as W

VOCAB = ("阵营\n    - 生活翼 | Life\n\n体型\n    - 中等 | Medium | 中等身量的\n\n时段\n    - 早晨 | Morning | 清晨\n    - 中午 | Noon | 午间\n\n"
         "价值\n    - 记录 | Records | 记账\n\n行动\n    - 做饭 | Cook | 在灶上煮粥 | 操作\n\n")
HEAD = ("章节：t\n前置章节：无\n\n" + VOCAB + "地点 门厅\n    编号：room.foyer\n\n"
        "地点 乙区\n    编号：room.DormLife\n")
def person(name, pid, extra=""):
    return (f"人物 {name}\n    编号：{pid}\n    所在：门厅\n    阵营：生活翼\n    宿舍：乙区\n    体型：中等\n"
            f"    性别：男\n    速度：1\n    欲望：x\n    魔音台词：y\n{extra}\n")

def compile_text(text):
    with tempfile.TemporaryDirectory() as d:
        path = Path(d) / "t.story"
        path.write_text(text, encoding="utf-8")
        return S.compile_story([path], set())

def entity(doc, name):
    return next(e for e in doc["chapters"][0]["entities"] if e["name"] == name)

def error_of(text):
    try:
        compile_text(text)
    except W.ScriptError as exc:
        return exc
    raise AssertionError("应当失败")

class StoryScript(unittest.TestCase):
    def test_person_profile_and_forward_prejudice(self):
        doc = compile_text(HEAD + person("甲", "person.Huo", "    偏见：乙 -2\n    看重：记录 1\n    日常：\n        早晨、中午 做饭 重要 3 合欲望\n")
                           + person("乙", "person.Shen"))
        a = entity(doc, "甲")["profile"]
        self.assertEqual(a["prejudice"], {"Shen": -2})
        self.assertEqual(a["values"], {"Records": 1})
        self.assertEqual(a["habits"], [{"job": "Cook", "slots": ["Morning", "Noon"], "weight": 3, "desire": True}])

    def test_unknown_vocabulary_names_the_line(self):
        err = error_of(HEAD + person("甲", "person.Huo", "    看重：不存在 1\n"))
        self.assertIn("不存在", str(err))
        self.assertGreater(err.source.line, 4)

    def test_undeclared_room(self):
        text = HEAD + "物件 钟\n    编号：o.clock\n    位置：地窖\n"
        self.assertIn("地窖", str(error_of(text)))

    def test_unknown_field(self):
        text = HEAD + "物件 钟\n    编号：o.clock\n    位置：门厅\n    颜色：红\n"
        err = error_of(text)
        self.assertIn("颜色", str(err))
        self.assertEqual(err.source.line, HEAD.count(chr(10)) + 4)

    def test_observable_and_state(self):
        doc = compile_text(HEAD + "物件 钟\n    编号：o.clock\n    位置：门厅\n    属性：走时 1\n    可见：响、停\n")
        props = entity(doc, "钟")["properties"]
        self.assertEqual(props["state:走时"]["value"], 1)
        self.assertTrue(props["observable:响"]["value"])

if __name__ == "__main__":
    unittest.main()
