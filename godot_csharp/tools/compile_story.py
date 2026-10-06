#!/usr/bin/env python3
"""Compile typed chapter declarations into deterministic runtime and host data."""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import sys
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
SCRIPT_DIR = HERE.parent / "scenes" / "Awakening" / "Script"
SOURCE = SCRIPT_DIR / "ch1_awakening.py"
sys.path.insert(0, str(HERE))

from engine_spec import Expr, Instruction, dialogue_document  # noqa: E402

EFFECTS = {"set_flag", "clear_flag", "amplify", "whisper", "share_recall"}
QUERIES = {
    "detective_name", "seen", "tested", "asked", "flag", "recalled", "compared", "v6_open",
    "told_rule", "hooks", "total_hooks", "matched", "replayed", "fragment", "alone_choice",
    "life_thought", "talk_first", "recall_thought", "wrong_name", "addr", "heard", "heard_where",
    "sound_sentence",
}
EXPR_OPS = {"literal", "call", "not", "and", "or", "eq", "ne", "gt", "gte", "lt", "lte"}
INTERPOLATION = re.compile(r"\{\{(.*?)\}\}")
CALL_ARITY = {
    "detective_name": 0, "seen": 1, "tested": 1, "asked": 1, "flag": 1, "recalled": 1,
    "compared": 1, "v6_open": 0, "told_rule": 1, "hooks": 1, "total_hooks": 0,
    "matched": 0, "replayed": 0, "fragment": 1, "alone_choice": 1, "life_thought": 1,
    "talk_first": 1, "recall_thought": 0, "wrong_name": 0, "addr": 1, "heard": 1,
    "heard_where": 1, "sound_sentence": 0,
}
EFFECT_ARITY = {"set_flag": 1, "clear_flag": 1, "amplify": 2, "whisper": 2, "share_recall": 1}


def load_source():
    spec = importlib.util.spec_from_file_location("ch1_awakening", SOURCE)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load Python story source: {SOURCE}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def validate_expr(expr: Expr, where: str) -> None:
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
        validate_expr(arg, where)


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
            validate_expr(_expression(node["condition"]), location)
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
                    validate_expr(_expression(option["condition"]), location + ".choice")
                validate_instructions(option["body"], location + ".choice")
        elif op == "jump":
            continue
        else:
            raise ValueError(f"{location}: unsupported instruction {op!r}")


def _expression(value: dict[str, Any]) -> Expr:
    return Expr(value["op"], value=value.get("value"), name=value.get("name", ""),
                args=tuple(_expression(item) for item in value.get("args", [])))


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


def content_data(source) -> dict[str, Any]:
    result = dict(source.CONTENT_TABLES)
    result["EVIDENCE"] = {record.id: record.json() for record in source.EVIDENCE}
    result["LIFE_TRACES"] = {
        record.id: {"title": record.title, "room": record.room, "picture": record.picture, "facts": record.facts}
        for record in source.LIFE_TRACES
    }
    result["SITE_INTERACTIONS"] = site_interactions(source)
    return result


def site_interactions(source) -> dict[str, dict[str, str]]:
    result: dict[str, dict[str, str]] = {}
    for site in source.SITES:
        if site.id in result:
            raise ValueError(f"duplicate site interaction id {site.id!r}")
        kind = "tube" if site.id == "E8" else "evidence"
        result[site.id] = {"kind": kind, "target": "E8" if kind == "tube" else site.evidence}
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


def generated_files() -> dict[Path, str]:
    source = load_source()
    if len({entity.id for entity in source.EVIDENCE}) != len(source.EVIDENCE):
        raise ValueError("duplicate evidence id")
    if len({entity.id for entity in source.LIFE_TRACES}) != len(source.LIFE_TRACES):
        raise ValueError("duplicate life trace id")
    document = dialogue_document(source.CHAPTER.entries)
    assign_choice_ids(document["entries"])
    for entry_id, instructions in document["entries"].items():
        validate_instructions(instructions, entry_id)
    content = content_data(source)
    layout = layout_data(source)
    dialogue_json = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    content_json = json.dumps(content, ensure_ascii=False, indent=2) + "\n"
    layout_json = json.dumps(layout, ensure_ascii=False, indent=2) + "\n"
    escaped = "\n".join("    " + line for line in dialogue_json.rstrip("\n").splitlines())
    csharp = (
        "using DualOntologyWorldEngine.Story;\n\n"
        "namespace DualOntologyWorldEngine.Awakening;\n\n"
        "/// <summary>Generated from ch1_awakening.py by tools/compile_story.py.</summary>\n"
        "public static class Chapter1CompiledDialogue\n{\n"
        "    private const string Json = \"\"\"\n" + escaped + "\n    \"\"\";\n\n"
        "    public static CompiledDialogueDocument Create() => CompiledDialogueDocument.Parse(Json);\n}\n"
    )
    return {
        SCRIPT_DIR / "generated_awakening_dialogue.json": dialogue_json,
        SCRIPT_DIR / "generated_awakening_content.json": content_json,
        SCRIPT_DIR / "generated_awakening_layout.json": layout_json,
        SCRIPT_DIR / "Chapter1CompiledDialogue.cs": csharp,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="fail when generated files are stale; do not write")
    args = parser.parse_args()
    try:
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
    except (ValueError, KeyError, TypeError) as error:
        print(f"story compile failed: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
