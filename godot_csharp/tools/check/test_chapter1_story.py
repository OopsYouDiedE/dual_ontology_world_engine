"""第一章 .story 读取器回归：表达式、嵌套条件、选择、生成物同步。"""
import subprocess, sys, tempfile, unittest
from pathlib import Path
TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import story_chapter1 as C
from story_script import StoryError

def load_text(text):
    with tempfile.TemporaryDirectory() as d:
        p = Path(d) / "c.story"
        p.write_text(text, encoding="utf-8")
        return C.load(p)

class Chapter1Story(unittest.TestCase):
    def test_nested_condition_choice_and_effect(self):
        src = load_text("章节：t\n\n场面 a\n    侦探：你好\n    若 见过(\"E1\") 且 非 旗(\"x\")：\n        设旗 y\n    否则 若 钩数(\"su\") > 0：\n        沈鼎：嗯 [#emotion=warm]\n    否则：\n        选择：\n            - 走\n                条件：旗(\"y\")\n                跳转 END\n            - 留\n    跳转 END\n")
        body = src.entries[0][1]
        self.assertEqual([i.op for i in body], ["line", "if", "jump"])
        branch = body[1].data
        self.assertEqual(branch["condition"].op, "and")
        self.assertEqual(branch["else"][0].op, "if")
        options = branch["else"][0].data["else"][0].data["options"]
        self.assertEqual([o["label"] for o in options], ["走", "留"])
        self.assertEqual(options[0]["condition"].name, "flag")

    def test_bad_expression_reports_line(self):
        with self.assertRaises(StoryError) as ctx:
            load_text("章节：t\n\n场面 a\n    若 旗(：\n        跳转 END\n")
        self.assertEqual(ctx.exception.line, 4)

    def test_generated_outputs_are_current(self):
        result = subprocess.run([sys.executable, str(TOOLS / "compile_story.py"), "--check"], capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(result.returncode, 0, result.stderr)

if __name__ == "__main__":
    unittest.main()
