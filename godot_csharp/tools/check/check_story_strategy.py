"""Neutral declaration contracts for the formal strategy parser; no chapter content."""
from pathlib import Path
import sys

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
from story_base import Line, StoryError, read_lines
from story_chapter1 import read_strategy_track
from story_strategy import strategies


def field(body, value=""):
    return value, Line(1, 1, "策略：" + value), read_lines(body)


def declaration(name="A", priority=12, condition="本人 neutral_level >= 2", **overrides):
    values = {"对象": "neutral.target", "姿态": "neutral.stance", "优先度": str(priority),
              "依据": "source.a", "方法": "method.a、method.b", "条件": condition}
    values.update(overrides)
    return "        - " + name + "\n" + "".join(
        "            " + key + "：" + value + "\n" for key, value in values.items())


def main():
    passed = failed = 0

    def check(name, okay):
        nonlocal passed, failed
        if okay:
            passed += 1
            print("PASS", name)
        else:
            failed += 1
            print("FAIL", name)

    def reject(name, data):
        try:
            strategies(data)
        except StoryError:
            check(name, True)
        else:
            check(name, False)

    check("omitted_strategy_is_empty", strategies(None) == [])
    parsed = strategies(field(declaration()))
    check("legal_strategy_preserves_contract", parsed == [{
        "name": "A", "target": "neutral.target", "stance": "neutral.stance", "priority": 12,
        "source": "source.a", "methods": ["method.a", "method.b"],
        "conditions": [{"tag": "neutral_level", "minimum": 2}],
    }])
    check("legal_three_strategies_and_priority_boundaries", [x["priority"] for x in strategies(field(
        declaration("A", 0, "本人 neutral_level >= 0") + declaration("B", 1000, "") + declaration("C", 5)
    ))] == [0, 1000, 5])
    check("legal_multiple_personal_thresholds", strategies(field(declaration(
        condition="本人 neutral_level >= 2、本人 neutral_budget >= 1"
    )))[0]["conditions"] == [{"tag": "neutral_level", "minimum": 2}, {"tag": "neutral_budget", "minimum": 1}])
    for name, data in [
        ("global_truth_condition_rejected", field(declaration(condition="全局 neutral_level >= 2"))),
        ("player_intent_condition_rejected", field(declaration(condition="玩家 意图 >= 1"))),
        ("another_subject_condition_rejected", field(declaration(condition="他人 neutral_level >= 2"))),
        ("non_threshold_expression_rejected", field(declaration(condition="本人 neutral_level == 2"))),
        ("negative_threshold_rejected", field(declaration(condition="本人 neutral_level >= -1"))),
        ("inline_action_queue_rejected", field("", value="method.a、method.b")),
        ("indented_action_queue_rejected", field("        - method.a\n        - method.b\n")),
        ("explicit_action_queue_field_rejected", field(declaration() + "            行动队列：method.a、method.b\n")),
        ("unknown_intent_field_rejected", field(declaration() + "            玩家意图：neutral\n")),
        ("unknown_truth_field_rejected", field(declaration() + "            全局真值：neutral\n")),
        ("duplicate_strategy_name_rejected", field(declaration() + declaration())),
        ("duplicate_field_rejected", field(declaration() + "            优先度：50\n")),
        ("duplicate_method_rejected", field(declaration(**{"方法": "method.a、method.a"}))),
        ("more_than_three_strategies_rejected", field("".join(declaration(name) for name in "ABCD"))),
        ("negative_priority_rejected", field(declaration(priority=-1))),
        ("priority_above_limit_rejected", field(declaration(priority=1001))),
        ("nested_condition_block_rejected", field(declaration() + "                本人 neutral_other >= 1\n")),
        ("empty_explicit_strategy_rejected", field("")),
        ("missing_required_source_rejected", field(declaration().replace("            依据：source.a\n", ""))),
        ("empty_target_rejected", field(declaration(**{"对象": ""}))),
        ("empty_stance_rejected", field(declaration(**{"姿态": ""}))),
        ("empty_source_rejected", field(declaration(**{"依据": ""}))),
        ("empty_methods_rejected", field(declaration(**{"方法": ""}))),
    ]:
        reject(name, data)
    def track(**overrides):
        data = {"cooldown_minutes": 2, "wrong_scenes": "scene.wrong", "world_resolution_scenes": "scene.resolved",
                "decline_choices": {}, "object_labels": {}, "object_choices": {},
                "people": {"neutral.actor": {"clues": "neutral.clue", "methods": {"report": "scene.report"}}}}
        data.update(overrides)
        return data

    check("omitted_source_scenes_keeps_legacy_track_valid", "source_scenes" not in read_strategy_track(track(), 1))
    parsed_track = read_strategy_track(track(source_scenes={"source.a": "scene.initial、scene.repeat"}), 1)
    check("source_scene_aliases_compile_as_explicit_list",
          parsed_track["source_scenes"] == {"source.a": ["scene.initial", "scene.repeat"]})
    for name, value in [
        ("null_source_scenes_rejected", None),
        ("scalar_source_scenes_rejected", "scene.initial"),
        ("array_source_scenes_rejected", ["scene.initial"]),
        ("numeric_source_scenes_rejected", 1),
        ("empty_source_scenes_mapping_rejected", {}),
        ("source_scene_array_value_rejected", {"source.a": ["scene.initial"]}),
        ("source_scene_nested_value_rejected", {"source.a": {"scene.initial": True}}),
        ("source_scene_numeric_value_rejected", {"source.a": 1}),
        ("source_scene_null_value_rejected", {"source.a": None}),
        ("source_scene_empty_value_rejected", {"source.a": ""}),
        ("source_scene_whitespace_value_rejected", {"source.a": "  "}),
        ("duplicate_source_scene_list_rejected", {"source.a": "scene.initial、scene.initial"}),
    ]:
        try:
            read_strategy_track(track(source_scenes=value), 1)
        except StoryError:
            check(name, True)
        else:
            check(name, False)
    print(f"StoryStrategyCheck: {passed} passed, {failed} failed")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
