"""Compiler contract tests and temporary formal agent declaration for headless runs."""
from pathlib import Path
import argparse
import json
import shutil
import sys

TOOLS = Path(__file__).resolve().parents[1]
ROOT = TOOLS.parent.parent
sys.path.insert(0, str(TOOLS))
from story_base import Line, StoryError
from story_controller import controller
from story_script import compile_story


def fields(mode="代理", rule="- 偏好 新选择 6"):
    return {"操控者": (mode, Line(1, 1, ""), []),
            "操控规则": ("", Line(2, 1, ""), [Line(3, 2, rule)])}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--prepare-agent", type=Path)
    args = parser.parse_args()
    passed = 0
    def check(name, okay):
        nonlocal passed
        assert okay, name
        passed += 1
        print("PASS", name)
    check("unspecified_is_player", controller({}, "world:actor", "A") is None)
    check("player_keeps_compiled_default", controller(fields("玩家"), "world:actor", "A") is None)
    check("generic_actor", controller(fields(), "world:any-role", "A")["actor"] == "world:any-role")
    check("typed_subjective_rule", controller(fields(), "world:actor", "A")["rules"] == [{"mode": "prefer", "kind": "untried", "weight": 6}])
    budget = {**fields(), "推理预算": ("深度 8 展开 2048 假设 128 论证 16 求知 8", Line(4, 1, ""), [])}
    check("role_reasoning_budget", controller(budget, "world:any-role", "A")["reasoning"] == {"depth": 8, "expansions": 2048, "hypotheses": 128, "argument_weight": 16, "information_weight": 8})
    check("public_label_uses_runtime_wire_name", controller(fields(rule='- 偏好 菜单 7 文案 "neutral-target"'),
          "world:any-role", "A")["rules"] == [{"mode": "prefer", "kind": "menu", "weight": 7, "label_contains": "neutral-target"}])
    check("player_budget_keeps_default", controller({**budget, "操控者": fields("玩家")["操控者"]}, "world:actor", "A") is None)
    for name, data in [
        ("unknown_controller", fields("任意")),
        ("chapter_id_not_policy", fields(rule="- 偏好 E1 6")),
        ("source_line_not_policy", fields(rule="- 偏好 123 6")),
        ("sequence_not_policy", fields(rule="- 先走再问")),
        ("answer_not_policy", fields(rule="- 偏好 答案 6")),
        ("negative_weight", fields(rule="- 偏好 菜单 -1")),
        ("oversized_weight", fields(rule="- 偏好 菜单 1001")),
        ("oversized_rule_count", {**fields(), "操控规则": ("", Line(2, 1, ""), [Line(3, 2, "- 偏好 菜单 1")] * 65)}),
        ("missing_personal_rules", {"操控者": ("代理", Line(1, 1, ""), [])}),
        ("orphan_rules", {"操控规则": fields()["操控规则"]}),
        ("bad_reasoning_syntax", {**budget, "推理预算": ("侦探专用", Line(4, 1, ""), [])}),
        ("zero_reasoning_expansions", {**budget, "推理预算": ("深度 8 展开 0 假设 128 论证 16 求知 8", Line(4, 1, ""), [])}),
        ("reasoning_depth_limit", {**budget, "推理预算": ("深度 65 展开 2048 假设 128 论证 16 求知 8", Line(4, 1, ""), [])}),
    ]:
        try:
            controller(data, "world:actor", "A")
        except StoryError:
            check(name, True)
        else:
            check(name, False)
    if args.prepare_agent:
        destination = args.prepare_agent.resolve()
        if destination.exists():
            raise RuntimeError("Refusing to overwrite a fixture directory")
        source = ROOT / "godot_csharp/scenes/Awakening/Script"
        destination.mkdir(parents=True)
        for path in source.rglob("*.story"):
            target = destination / path.relative_to(source)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, target)
        declarations = []
        for path in destination.rglob("*.story"):
            text = path.read_text(encoding="utf-8-sig")
            if "操控者：玩家" in text:
                declarations.append(path)
                path.write_text(text.replace("操控者：玩家", "操控者：代理"), encoding="utf-8", newline="\n")
        check("one_formal_declared_role", len(declarations) == 1)
        document = compile_story([destination / "chapter_01.story"], set(), ecology_only=True)
        check("formal_agent_compiler_chain", len(document["chapters"][0]["controllers"]) == 1)
        (destination / "agent-world.json").write_text(json.dumps(document, ensure_ascii=False), encoding="utf-8")
        print("DECLARATIONS", destination / "agent-world.json")
    print(passed, "checks passed")


if __name__ == "__main__":
    main()
