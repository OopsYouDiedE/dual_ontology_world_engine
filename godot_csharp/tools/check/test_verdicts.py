"""判定按命题的回归：由证物“支撑”推出的四问判定，与手写基准在所有陈述、出示与检视／试验／协助状态下完全一致。

基准相对第三期原判定有两处有意修订（设计第二部分 §6，2026-10-07）：第 2 问出示音叉须先请苏泠辨认；
第 4 问只认“我信霍铎——我们早已相熟”，单说“我信霍铎”或“早已相熟”都不算答对。"""
import itertools
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import compile_story as C  # noqa: E402

STATEMENTS = ["", "me", "V1_0", "V2_0", "V3_0", "V3_1", "V4_0", "V4_1", "V5_0", "V5_1", "V6_0", "V6_1", "trust"]
EVIDENCE = [""] + [f"E{i}" for i in range(1, 13)]


def original(step: int, said: str, shown: str, seen: set[str], tested: set[str], asked: set[str]) -> str:
    """手写判定基准。"""
    if step == 1:
        ok_say = said == "me"
        ok_show = shown == "E3" or (shown in {"E12", "E7"} and "E12" in seen and "E7" in seen)
    elif step == 2:
        if said == "V3_0" and shown == "E8" and "E8" not in tested:
            return "need_test"
        ok_say = said == "V3_0"
        ok_show = (shown == "E8" and "E8" in tested) or (shown == "E5" and "E5" in asked)
    elif step == 3:
        ok_say, ok_show = said == "V4_0", shown in {"E7", "E6"}
    else:
        ok_say, ok_show = said == "trust", shown in {"E9", "E10", "E11", "E7"}
    return "ok" if ok_say and ok_show else "say" if not ok_say else "show"


def evaluate(node, said, shown, seen, tested, asked=frozenset()):
    op = node["op"]
    if op == "literal":
        return node["value"]
    if op == "call":
        name, args = node["name"], [evaluate(a, said, shown, seen, tested, asked) for a in node.get("args", [])]
        return {"said": lambda: said, "shown": lambda: shown, "seen": lambda: args[0] in seen,
                "tested": lambda: args[0] in tested, "asked": lambda: args[0] in asked}[name]()
    if op == "not":
        return not evaluate(node["args"][0], said, shown, seen, tested, asked)
    left, right = (evaluate(a, said, shown, seen, tested, asked) for a in node["args"])
    return {"and": lambda: left and right, "or": lambda: left or right, "eq": lambda: left == right}[op]()


class Verdicts(unittest.TestCase):
    def test_support_derived_verdicts_match_original(self):
        content = json.loads(C.generated_files()[C.SCRIPT_DIR / "generated_awakening_content.json"])
        verdicts = content["VERDICTS"]
        flags = ["E7", "E12", "E8"]
        checked = 0
        for step in range(1, 5):
            verdict = verdicts[str(step)]
            self.assertTrue(verdict["proposition"] and verdict["support"])
            for said, shown in itertools.product(STATEMENTS, EVIDENCE):
                for bits in itertools.product([False, True], repeat=4):
                    seen = {f for f, b in zip(flags[:2], bits[:2]) if b}
                    tested = {"E8"} if bits[2] else set()
                    asked = {"E5"} if bits[3] else set()
                    if "need_test" in verdict and evaluate(verdict["need_test"], said, shown, seen, tested, asked):
                        got = "need_test"
                    else:
                        ok_say = evaluate(verdict["say_ok"], said, shown, seen, tested, asked)
                        ok_show = evaluate(verdict["show_ok"], said, shown, seen, tested, asked)
                        got = "ok" if ok_say and ok_show else "say" if not ok_say else "show"
                    self.assertEqual(got, original(step, said, shown, seen, tested, asked), (step, said, shown, seen, tested, asked))
                    checked += 1
        self.assertGreater(checked, 5000)


if __name__ == "__main__":
    unittest.main()
