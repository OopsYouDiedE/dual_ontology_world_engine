#!/usr/bin/env python3
"""Compile typed chapter declarations into deterministic runtime and host data."""
from __future__ import annotations

import argparse
import csv
import importlib.util
import io
import json
import re
import sys
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
SCRIPT_DIR = HERE.parent / "scenes" / "Awakening" / "Script"
SOURCE = SCRIPT_DIR / "chapter_01.story"
sys.path.insert(0, str(HERE))

from engine_spec import Expr, Instruction, dialogue_document  # noqa: E402
from story_base import AUTHORED_LINES, origin  # noqa: E402
from story_script import StoryError, compile_story as compile_world_story  # noqa: E402
from world_script import ScriptError as WorldScriptError  # noqa: E402

EFFECTS = {"set_flag", "clear_flag", "amplify", "whisper", "share_recall"}
QUERIES = {
    "detective_name", "seen", "tested", "asked", "flag", "recalled", "compared", "v6_open",
    "told_rule", "hooks", "total_hooks", "matched", "replayed", "fragment", "alone_choice",
    "life_thought", "talk_first", "recall_thought", "wrong_name", "addr", "heard", "heard_where",
    "sound_sentence", "has", "count", "value", "replay_count", "full_match", "replay_refused",
    "in_room", "local", "busy", "said", "shown", "is_person", "geo_reach", "acquaintance_seen", "replay_var", "bolted",
    "credence", "knows", "speech", "dorms_open",
}
# 全局剧情进度：只许流程与阶段使用；对白条件须写成说话人自己的认知（总纲 §五.5）。
GLOBAL_PROGRESS_QUERIES = {"matched", "replayed", "full_match", "replay_count", "replay_refused"}
EXPR_OPS = {"literal", "call", "not", "and", "or", "eq", "ne", "gt", "gte", "lt", "lte"}
INTERPOLATION = re.compile(r"\{\{(.*?)\}\}")
CALL_ARITY = {
    "detective_name": 0, "seen": 1, "tested": 1, "asked": 1, "flag": 1, "recalled": 1,
    "compared": 1, "v6_open": 0, "told_rule": 1, "hooks": 1, "total_hooks": 0,
    "matched": 0, "replayed": 0, "fragment": 1, "alone_choice": 1, "life_thought": 1,
    "talk_first": 1, "recall_thought": 0, "wrong_name": 0, "addr": 1, "heard": 1,
    "heard_where": 1, "sound_sentence": 0, "has": 2, "count": 1, "value": 2, "replay_count": 0,
    "full_match": 0, "replay_refused": 0, "in_room": 2, "local": 2, "busy": 1, "said": 0, "shown": 0,
    "is_person": 1, "geo_reach": 1, "acquaintance_seen": 0, "replay_var": 1, "bolted": 0,
    "credence": 2, "knows": 2, "speech": 2, "dorms_open": 0,
}
# 宿主（Awakening.gd）能执行的演出指令，以及 C# 能执行的流程钩子。流程里写别的名字一律报错。
HOST_COMMANDS = {
    "name", "board", "rest", "investigate", "summon", "flash", "post", "sound", "music", "sfx", "lock", "unlock", "hud",
    "answerpick", "finale", "finale_prepare", "stage_actor", "door_open", "redraw", "su_approach", "save", "ending",
    "aside", "consider", "finish", "posts",
}
FLOW_HOOKS: set[str] = set()
FLOW_PROVIDERS = {"协助者", "答辩步", "答辩裁决"}
EFFECT_ARITY = {"set_flag": 1, "clear_flag": 1, "amplify": 2, "whisper": 2, "share_recall": 1}


def load_source(path: Path = SOURCE):
    import story_chapter1
    return story_chapter1.load(path)


def validate_expr(expr: Expr, where: str, dialogue: bool = False) -> None:
    if dialogue and expr.op == "call" and expr.name in GLOBAL_PROGRESS_QUERIES:
        raise ValueError(f"{where}: 对白条件不能用全局剧情进度 {expr.name!r}；请改写成说话人的认知（把握、知道）")
    if expr.op not in EXPR_OPS:
        raise ValueError(f"{where}: unsupported expression operator {expr.op!r}")
    if expr.op == "call" and expr.name not in QUERIES:
        raise ValueError(f"{where}: unsupported query {expr.name!r}")
    expected = {"literal": 0, "not": 1, "and": 2, "or": 2, "eq": 2, "ne": 2,
                "gt": 2, "gte": 2, "lt": 2, "lte": 2}
    if expr.op in expected and len(expr.args) != expected[expr.op]:
        raise ValueError(f"{where}: operator {expr.op!r} expects {expected[expr.op]} operands")
    if expr.op == "call" and len(expr.args) != CALL_ARITY[expr.name]:
        raise ValueError(f"{where}: query {expr.name!r} expects {CALL_ARITY[expr.name]} arguments")
    for arg in expr.args:
        validate_expr(arg, where, dialogue)


def validate_text(text: str, where: str) -> None:
    cursor = 0
    for match in INTERPOLATION.finditer(text):
        if "{{" in text[cursor:match.start()] or "}}" in text[cursor:match.start()]:
            raise ValueError(f"{where}: malformed interpolation")
        expression = match.group(1).strip()
        cursor = match.end()
        call = re.fullmatch(r"([a-z_]+)\((?:\"([^\"]*)\")?\)", expression)
        name = call.group(1) if call else expression
        if name not in QUERIES:
            raise ValueError(f"{where}: unsupported interpolation {expression!r}")
        if not call and not re.fullmatch(r"[a-z_]+", expression):
            raise ValueError(f"{where}: unsupported interpolation syntax {expression!r}")
        if not call and CALL_ARITY[name] != 0:
            raise ValueError(f"{where}: query {name!r} requires an argument")
        if call and call.group(2) is not None and CALL_ARITY.get(name) != 1:
            raise ValueError(f"{where}: query {name!r} does not accept a string argument")
        if call and call.group(2) is None and CALL_ARITY.get(name) != 0:
            raise ValueError(f"{where}: query {name!r} requires an argument")
    if "{{" in text[cursor:] or "}}" in text[cursor:]:
        raise ValueError(f"{where}: malformed interpolation")


def validate_instructions(instructions: list[dict[str, Any]], where: str) -> None:
    for index, node in enumerate(instructions):
        location = f"{where}[{index}]"
        op = node["op"]
        if op == "line":
            validate_text(node["text"], location)
            validate_text(node["speaker"], location)
        elif op == "if":
            validate_expr(_expression(node["condition"]), location, dialogue=True)
            validate_instructions(node["then"], location + ".then")
            validate_instructions(node["else"], location + ".else")
        elif op == "call":
            if node["name"] not in EFFECTS:
                raise ValueError(f"{location}: unsupported effect {node['name']!r}")
            for arg in node["args"]:
                validate_expr(_expression(arg), location)
            if len(node["args"]) != EFFECT_ARITY[node["name"]]:
                raise ValueError(f"{location}: effect {node['name']!r} expects {EFFECT_ARITY[node['name']]} arguments")
        elif op == "choices":
            for option in node["options"]:
                validate_text(option["label"], location + ".choice.label")
                validate_text(option["lock_hint"], location + ".choice.lock_hint")
                if option["condition"] is not None:
                    validate_expr(_expression(option["condition"]), location + ".choice", dialogue=True)
                validate_instructions(option["body"], location + ".choice")
        elif op == "jump":
            continue
        else:
            raise ValueError(f"{location}: unsupported instruction {op!r}")


def _expression(value: dict[str, Any]) -> Expr:
    return Expr(value["op"], value=value.get("value"), name=value.get("name", ""),
                args=tuple(_expression(item) for item in value.get("args", [])))


def validate_flow(steps: list[dict[str, Any]], where: str, flows: dict, stages: dict, scenes: set[str], stop_ok: bool = True) -> None:
    for index, node in enumerate(steps):
        location = f"{where}[{index}]"
        op = node["op"]
        if op == "stop" and not (stop_ok and index == len(steps) - 1):
            raise ValueError(f"{location}: 结束 only allowed as the last step of the flow or of an 若 branch")
        if op == "show":
            scene = node["scene"]
            if scene.startswith("@"):
                if scene[1:].split(":")[0] not in HOST_COMMANDS:
                    raise ValueError(f"{location}: unknown host command {scene!r}")
            elif "{" not in scene and scene not in scenes:
                raise ValueError(f"{location}: unknown scene {scene!r}")
        elif op == "stage":
            if node["value"] not in stages:
                raise ValueError(f"{location}: unknown stage {node['value']!r}")
        elif op == "hook":
            if node["name"] not in FLOW_HOOKS:
                raise ValueError(f"{location}: unknown flow hook {node['name']!r}")
        elif op == "continue":
            if node["flow"] not in flows:
                raise ValueError(f"{location}: unknown flow {node['flow']!r}")
        elif op == "if":
            validate_expr(_expression(node["condition"]), location)
            validate_flow(node["then"], location + ".then", flows, stages, scenes, stop_ok=stop_ok)
            validate_flow(node["else"], location + ".else", flows, stages, scenes, stop_ok=False)
        elif op not in {"set_flag", "clear_flag", "record", "remove", "detail", "clear", "stop"}:
            raise ValueError(f"{location}: unsupported flow step {op!r}")


def validate_rules(rules: dict[str, Any], scenes: set[str]) -> None:
    stages = rules.get("STAGES", {})
    flows = rules.get("FLOWS", {})
    for name, stage in stages.items():
        for room, target in stage["enter"].items():
            if target["stage"] not in stages:
                raise ValueError(f"stage {name}: enter {room} goes to unknown stage {target['stage']!r}")
    for event, stage in rules.get("STAGE_EVENTS", {}).items():
        if stage not in stages:
            raise ValueError(f"stage event {event}: unknown stage {stage!r}")
    for name, flow in flows.items():
        if flow.get("provider") and flow["provider"] not in FLOW_PROVIDERS:
            raise ValueError(f"flow {name}: unknown parameter provider {flow['provider']!r}")
        validate_flow(flow["steps"], "flow:" + name, flows, stages, scenes)
    for step, verdict in rules.get("VERDICTS", {}).items():
        for key in ("say_ok", "show_ok", "need_test"):
            if key in verdict:
                validate_expr(_expression(verdict[key]), f"verdict {step}.{key}")


def assign_choice_ids(entries: dict[str, list[dict[str, Any]]]) -> None:
    for entry_id, body in entries.items():
        sequence = 0
        used: set[str] = set()
        def visit(nodes: list[dict[str, Any]]) -> None:
            nonlocal sequence
            for node in nodes:
                if node["op"] == "choices":
                    for option in node["options"]:
                        if not option["id"]:
                            while f"{entry_id}:choice:{sequence}" in used:
                                sequence += 1
                            option["id"] = f"{entry_id}:choice:{sequence}"
                        if option["id"] in used:
                            raise ValueError(f"{entry_id}: duplicate choice id {option['id']!r}")
                        used.add(option["id"])
                        sequence += 1
                        visit(option["body"])
                elif node["op"] == "if":
                    visit(node["then"])
                    visit(node["else"])
        visit(body)


def validate_clock(clock: dict[str, Any], scenes: set[str]) -> None:
    start, expected = clock["start_minutes"], clock["expected_exit_minutes"]
    earliest, latest = clock["normal_exit_earliest_minutes"], clock["normal_exit_latest_minutes"]
    if not start < expected <= earliest <= latest:
        raise ValueError("DAY_CLOCK requires start < expected <= normal earliest <= normal latest")
    if clock["companion_start_minutes"] >= start:
        raise ValueError("DAY_CLOCK companions must wake before the detective")
    if latest >= 24 * 60:
        raise ValueError("DAY_CLOCK must stay within the authored first day")
    unknown = set(clock["scenes"]) - scenes
    if unknown:
        raise ValueError("DAY_CLOCK unknown scenes: " + ", ".join(sorted(unknown)))
    required = {"检视", "试验", "协助", "交谈", "声路", "重演", "答辩", "走动"}
    if set(clock["actions"]) != required:
        raise ValueError("DAY_CLOCK actions must declare: " + ", ".join(sorted(required)))
    if any(type(value) is not int or value <= 0 for value in clock["actions"].values()):
        raise ValueError("DAY_CLOCK action durations must be positive integer minutes")


def content_data(source) -> dict[str, Any]:
    result = dict(source.CONTENT_TABLES)
    result["EVIDENCE"] = {record.id: record.json() for record in source.EVIDENCE}
    result["LIFE_TRACES"] = {
        record.id: {"title": record.title, "room": record.room, "picture": record.picture, "facts": record.facts}
        for record in source.LIFE_TRACES
    }
    result["SITE_INTERACTIONS"] = site_interactions(source)
    result["OWN_OBJECTS"] = dict(source.OWN_OBJECTS)
    result["RECALL_OBJECTS"] = list(source.RECALL_OBJECTS)
    result["ACQUAINTANCE_EVIDENCE"] = list(source.ACQUAINTANCE_EVIDENCE)
    result.update(source.RULES_DATA)
    return result


def site_interactions(source) -> dict[str, dict[str, str]]:
    result: dict[str, dict[str, str]] = {}
    for site in source.SITES:
        if site.id in result:
            raise ValueError(f"duplicate site interaction id {site.id!r}")
        result[site.id] = {"kind": site.click, "target": site.evidence}
    for trace in source.LIFE_TRACES:
        if trace.id in result:
            raise ValueError(f"duplicate site interaction id {trace.id!r}")
        result[trace.id] = {"kind": "trace", "target": trace.id}
    for site in source.EXTRA_SITES:
        if site["id"] in result:
            raise ValueError(f"duplicate site interaction id {site['id']!r}")
        result[site["id"]] = {"kind": site["kind"], "target": site["target"]}
        if site["kind"] == "person":
            result[f"person:{site['target']}"] = {"kind": "person", "target": site["target"]}
    return result


def layout_data(source) -> dict[str, Any]:
    evidence_ids = {item.id for item in source.EVIDENCE}
    site_ids = [site.id for site in source.SITES]
    trace_ids = [trace.id for trace in source.LIFE_TRACES]
    if len(site_ids) != len(set(site_ids)) or len(trace_ids) != len(set(trace_ids)):
        raise ValueError("duplicate site or life trace id")
    missing = sorted({site.evidence for site in source.SITES} - evidence_ids)
    if missing:
        raise ValueError("site references missing evidence: " + ", ".join(missing))
    all_site_ids = set(site_ids) | set(trace_ids) | {site["id"] for site in source.EXTRA_SITES}
    invalid_boxes = sorted(set(source.SITE_BOXES) - all_site_ids)
    if invalid_boxes:
        raise ValueError("site boxes reference missing site ids: " + ", ".join(invalid_boxes))
    prop_ids = [prop.node for prop in source.PROPS]
    if len(prop_ids) != len(set(prop_ids)):
        raise ValueError("duplicate prop node id")
    return {
        "format": "awakening-layout/v1",
        "sites": [
            {"id": s.id, "evidence": s.evidence, "x": s.x, "y": s.y, "reach": s.reach, "title": s.title}
            for s in source.SITES
        ],
        "extra_sites": {site["id"]: dict(site) for site in source.EXTRA_SITES},
        "life_traces": [
            {"id": t.id, "title": t.title, "x": t.x, "y": t.y, "reach": t.reach,
             "room": t.room, "picture": t.picture, "facts": t.facts}
            for t in source.LIFE_TRACES
        ],
        "posts": {key: list(point) for key, point in source.POSTS.items()},
        "staging": {key: list(point) for key, point in source.STAGING.items()},
        "objects": {
            key: {"person": next(person for person, evidence in source.OWN_OBJECTS.items() if evidence == key),
                  "sprite": sprite}
            for key, sprite in source.OBJECT_SPRITES.items()
        },
        "site_boxes": {key: list(value) for key, value in source.SITE_BOXES.items() if isinstance(value, tuple)},
        "site_interactions": site_interactions(source),
        "props": [
            {"node": p.node, "texture": p.texture, "x": p.x, "y": p.y, "z_y": p.z_y}
            for p in source.PROPS
        ],
        "recall_objects": list(source.RECALL_OBJECTS),
        "acquaintance_evidence": list(source.ACQUAINTANCE_EVIDENCE),
        "help_notes": source.CONTENT_TABLES["HELP_NOTES"],
        "whispers": source.CONTENT_TABLES["WHISPERS"],
        "furniture_asides": source.FURNITURE_ASIDES,
        "dialogue_cast": source.DIALOGUE_CAST,
        **source.LAYOUT_CONSTANTS,
    }


def display_path(path: Path) -> str:
    try:
        return path.relative_to(HERE.parent.parent).as_posix()
    except ValueError:
        return path.as_posix()


def check_line_ids(lines: list[tuple[str, str, int]], root: Path) -> None:
    """Every authored line carries one stable, unique ID (translation key)."""
    seen: dict[str, int] = {}
    missing = 0
    for _, line_id, number in lines:
        if not line_id:
            missing += 1
        elif line_id in seen and seen[line_id] != number:
            file, line = origin(number, root.name)
            raise ValueError(f"{file}:{line}: 台词编号“{line_id}”重复")
        else:
            seen[line_id] = number
    if missing:
        raise ValueError(f"{missing} 句台词缺少编号；运行 python godot_csharp/tools/compile_story.py --assign-ids 写回编号")


def assign_line_ids(lines: list[tuple[str, str, int]], root: Path) -> int:
    """Append `[#id=<前缀>.<序号>]` to unnumbered lines in their own files; existing IDs never change."""
    used = {line_id for _, line_id, _ in lines if line_id}
    edits: dict[str, dict[int, str]] = {}
    for prefix, line_id, number in lines:
        if line_id:
            continue
        serial = 1
        while f"{prefix}.{serial:03d}" in used:
            serial += 1
        new_id = f"{prefix}.{serial:03d}"
        used.add(new_id)
        file, line = origin(number, root.as_posix())
        edits.setdefault(file, {})[line] = new_id
    for file, by_line in edits.items():
        path = Path(file)
        raw = path.read_text(encoding='utf-8-sig')
        physical = raw.split('\n')
        for line, new_id in by_line.items():
            text = physical[line - 1]
            ending = '\r' if text.endswith('\r') else ''
            physical[line - 1] = text.rstrip() + f' [#id={new_id}]' + ending
        path.write_text('\n'.join(physical), encoding='utf-8', newline='')
    return sum(len(by_line) for by_line in edits.values())


def line_table(documents: list[tuple[str, Any]]) -> str:
    """Translation source table: one row per authored line, in authoring order."""
    rows: list[tuple[str, str, str, str]] = []

    def visit(node: Any, context: str) -> None:
        if isinstance(node, dict):
            if node.get("op") == "line" and node.get("line_id"):
                rows.append((node["line_id"], context, node.get("speaker", ""), node["text"]))
            if node.get("label_id"):
                rows.append((node["label_id"], context, "", node["label"]))
            here = node.get("id") if isinstance(node.get("id"), str) and "steps" in node else context
            for value in node.values():
                visit(value, here)
        elif isinstance(node, list):
            for value in node:
                visit(value, context)

    for context, document in documents:
        if isinstance(document, dict) and "entries" in document:
            for entry_id, instructions in document["entries"].items():
                visit(instructions, entry_id)
        else:
            visit(document, context)
    out = io.StringIO()
    writer = csv.writer(out, lineterminator="\n")
    writer.writerow(["id", "scene", "speaker", "zh"])
    writer.writerows(rows)
    return out.getvalue()


def generated_files(story: Path = SOURCE, *, require_line_ids: bool = True) -> dict[Path, str]:
    """Every output compiled from one root story; `引入` fragments are spliced in order."""
    AUTHORED_LINES.clear()
    source = load_source(story)
    if len({entity.id for entity in source.EVIDENCE}) != len(source.EVIDENCE):
        raise ValueError("duplicate evidence id")
    if len({entity.id for entity in source.LIFE_TRACES}) != len(source.LIFE_TRACES):
        raise ValueError("duplicate life trace id")
    document = dialogue_document(source.CHAPTER.entries)
    assign_choice_ids(document["entries"])
    for entry_id, instructions in document["entries"].items():
        validate_instructions(instructions, entry_id)
    content = content_data(source)
    validate_rules(source.RULES_DATA, set(document["entries"]))
    validate_clock(content["DAY_CLOCK"], set(document["entries"]))
    for opening in content.get("OPENINGS", []):
        if opening["scene"] not in document["entries"]:
            raise ValueError(f"开局人选 {opening['person']} 的开场场面 {opening['scene']!r} 不存在")
    for goal, method in content.get("METHODS", {}).items():
        for part in method["parts"]:
            if part not in content["METHODS"]:
                raise ValueError(f"方法 {goal} 分解出未声明的目标 {part!r}")
    for flag, goals in content.get("WISHES", {}).items():
        for goal in goals:
            if goal not in content.get("METHODS", {}):
                raise ValueError(f"心愿 {flag} 指向未声明的方法 {goal!r}")
    layout = layout_data(source)
    dialogue_json = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    content_json = json.dumps(content, ensure_ascii=False, indent=2) + "\n"
    layout_json = json.dumps(layout, ensure_ascii=False, indent=2) + "\n"
    folder = story.parent.as_posix() + "/"
    world_json = json.dumps(compile_world_story([story], set(), ecology_only=True),
                            ensure_ascii=False, indent=2) + "\n"
    world_json = world_json.replace(folder, display_path(story.parent) + "/")
    continuation_json = json.dumps(
        compile_world_story([story], {"awakening"}, section="chapter2"),
        ensure_ascii=False, indent=2) + "\n"
    continuation_json = continuation_json.replace(folder, display_path(story.parent) + "/")
    if require_line_ids:
        check_line_ids(AUTHORED_LINES, story)
    lines_csv = line_table([("", document), ("chapter2", json.loads(continuation_json)),
                            *((f"topic.{name}", topic) for name, topic in content.get("TOPICS", {}).items())])
    escaped = "\n".join("    " + line for line in dialogue_json.rstrip("\n").splitlines())
    csharp = (
        "using DualOntologyWorldEngine.Story;\n\n"
        "namespace DualOntologyWorldEngine.Awakening;\n\n"
        "/// <summary>Generated from chapter_01.story by tools/compile_story.py.</summary>\n"
        "public static class Chapter1CompiledDialogue\n{\n"
        "    private const string Json = \"\"\"\n" + escaped + "\n    \"\"\";\n\n"
        "    public static CompiledDialogueDocument Create() => CompiledDialogueDocument.Parse(Json);\n}\n"
    )
    return {
        SCRIPT_DIR / "generated_awakening_dialogue.json": dialogue_json,
        SCRIPT_DIR / "generated_awakening_content.json": content_json,
        SCRIPT_DIR / "generated_awakening_layout.json": layout_json,
        SCRIPT_DIR / "generated_chapter_01_world.json": world_json,
        SCRIPT_DIR / "Chapter1CompiledDialogue.cs": csharp,
        SCRIPT_DIR.parent.parent / "Chapter2" / "Script" / "generated_chapter_02.json": continuation_json,
        SCRIPT_DIR / "generated_lines_zh.csv": lines_csv,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="fail when generated files are stale; do not write")
    parser.add_argument("--assign-ids", action="store_true", help="write stable IDs onto unnumbered lines, then compile")
    args = parser.parse_args()
    try:
        if args.assign_ids:
            generated_files(require_line_ids=False)
            print(f"assigned {assign_line_ids(list(AUTHORED_LINES), SOURCE)} line IDs")
        outputs = generated_files()
        stale = []
        for path, content in outputs.items():
            if args.check:
                if not path.exists() or path.read_text(encoding="utf-8") != content:
                    stale.append(path)
            else:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content, encoding="utf-8", newline="\n")
        if stale:
            print("generated files are stale: " + ", ".join(str(path.relative_to(HERE.parent.parent)) for path in stale), file=sys.stderr)
            return 1
        if args.check:
            print(f"generated files are current ({len(outputs)} outputs)")
        else:
            print(f"generated {len(outputs)} outputs from {SOURCE.name}")
        return 0
    except StoryError as error:
        file, line = origin(error.line, SOURCE.name)
        print(f"{file}:{line}: {error}", file=sys.stderr)
        return 2
    except WorldScriptError as error:
        location = f"{error.source.path}:{error.source.line}" if error.source else SOURCE.name
        print(f"{location}: {error}", file=sys.stderr)
        return 2
    except (ValueError, KeyError, TypeError) as error:
        print(f"story compile failed: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
