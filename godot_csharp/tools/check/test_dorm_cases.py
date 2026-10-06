"""寝区案件的解空间检查：寝区里的全部痕迹唯一确定作答的问题（答案字面正确），
另一问在寝区里无从确定（推论只能靠先入之见，可能留错）；真相与设计第三部分 §2 一致。"""
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import compile_story as C  # noqa: E402

EXPECTED = {"文契": ("开锁人", "雀儿"), "星仪": ("关门人", "阮清越"), "后勤": ("取走人", "维罗妮卡")}


class DormCases(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = json.loads(C.generated_files()[C.SCRIPT_DIR / "generated_awakening_content.json"])["DORM_CASES"]

    def test_three_groups_declared(self):
        self.assertEqual(set(self.cases), set(EXPECTED))

    def test_dorm_traces_determine_the_literal_answer_only(self):
        for name, case in self.cases.items():
            consistent = [h for h in case["hypotheses"] if h["facts"] == case["true_facts"]]
            answers = {h["options"][case["answer"]] for h in consistent}
            self.assertEqual(len(answers), 1, name)
            key, expected = EXPECTED[name]
            question = case["questions"][case["answer"]]
            self.assertEqual((question["key"], question["options"][answers.pop()]), (key, expected), name)
            others = [q for q in range(len(case["questions"])) if q != case["answer"]]
            for q in others:
                self.assertGreater(len({h["options"][q] for h in consistent}), 1,
                                   f"{name}: {case['questions'][q]['key']} must stay open inside the dorm")

    def test_each_prior_concerns_an_open_question(self):
        for name, case in self.cases.items():
            for prior in case["priors"]:
                self.assertNotEqual(prior["question"], case["answer"], name)

    def test_traces_are_revealed_in_increasing_effort(self):
        for name, case in self.cases.items():
            needs = [t["needs"] for t in case["traces"]]
            self.assertEqual(needs, sorted(needs), name)


if __name__ == "__main__":
    unittest.main()
