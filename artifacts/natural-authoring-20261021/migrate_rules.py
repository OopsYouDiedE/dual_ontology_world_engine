#!/usr/bin/env python3
"""One-time, syntax-only migration of literal chapter axiom declarations.

This is an authoring migration utility, not a production parser. It uses Python's
AST only to recognize the existing expression subset, never executes DV, and
does not replace or participate in the DV compiler. Without --write it only
reports a plan. A report contains the exact before/after declaration for review.
"""
from __future__ import annotations

import argparse
import ast
from collections import Counter
from dataclasses import dataclass, asdict
import hashlib
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]
FILES = ("language_core/40-chapter1.dv", "language_core/50-chapter2.dv")
HEADER = re.compile(r"^(?P<kind>fn|axiom) (?P<name>\w+)\((?P<params>[^\n)]*)\):[ \t]*$")
RETURN = re.compile(r"^    return (?P<expression>.+)$")
PARAMETER = re.compile(r"[A-Za-z_\u0080-\uffff][\w]*")


@dataclass
class Change:
    name: str
    line: int
    start: int
    end: int
    before: str
    after: str
    premise_form: str
    effect_form: str
    natural_conditions: int
    natural_effects: int


def sha256(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def call(node: ast.AST, name: str, count: int | None = None) -> bool:
    return (isinstance(node, ast.Call)
            and isinstance(node.func, ast.Name) and node.func.id == name
            and not node.keywords
            and not any(isinstance(arg, ast.Starred) for arg in node.args)
            and (count is None or len(node.args) == count))


def sequence(node: ast.AST) -> list[ast.AST] | None:
    # These constructors are literal ordered list constructors in 20-core.dv.
    if isinstance(node, ast.List) and not any(isinstance(x, ast.Starred) for x in node.elts):
        return node.elts
    for name, arity in (("dv_single", 1), ("dv_pair", 2), ("dv_list3", 3)):
        if call(node, name, arity):
            return node.args
    return None


class Renderer:
    def __init__(self, expression: str):
        self.expression = expression

    def raw(self, node: ast.AST) -> str:
        segment = ast.get_source_segment(self.expression, node)
        if segment is None:
            raise ValueError("AST source segment is unavailable")
        return segment

    def reference(self, node: ast.AST) -> str:
        if call(node, "c2_ref", 2):
            return f"{self.raw(node.args[0])} 的 {self.raw(node.args[1])}"
        if call(node, "dv_ref", 3):
            obj, facet, field = node.args
            if isinstance(facet, ast.Constant) and facet.value == "state":
                return f"{self.raw(obj)} 的 {self.raw(field)}"
            return f"{self.raw(obj)} 的 {self.raw(facet)} 的 {self.raw(field)}"
        return self.raw(node)

    def proposition(self, node: ast.AST) -> str:
        if call(node, "dv_fact", 2):
            return f"{self.reference(node.args[0])} 等于 {self.raw(node.args[1])}"
        if call(node, "dv_at_least", 2):
            return f"{self.reference(node.args[0])} 至少 {self.raw(node.args[1])}"
        if call(node, "dv_not", 1):
            return f"非 {self.proposition(node.args[0])}"
        if call(node, "c1_at", 2) or call(node, "c2_at", 2):
            return f"{self.raw(node.args[0])} 位于 {self.raw(node.args[1])}"
        # Nested all/any and unrecognized domain helpers remain expressions.
        return self.raw(node)

    def effect(self, node: ast.AST) -> str:
        if call(node, "dv_assign", 2):
            return f"{self.reference(node.args[0])} 变为 {self.raw(node.args[1])}"
        if call(node, "dv_increment", 2):
            return f"{self.reference(node.args[0])} 增加 {self.raw(node.args[1])}"
        return self.raw(node)

    def declaration(self, name: str, params: list[str], node: ast.Call) -> tuple[str, str, str, int, int]:
        law_name, version, premise, effects = node.args
        heading = f"法则 {name}" + ("，给定 " + "、".join(params) if params else "") + "："
        lines = [heading]
        if not isinstance(law_name, ast.Constant) or law_name.value != name:
            lines.append(f"    名为 {self.raw(law_name)}。")
        if not isinstance(version, ast.Constant) or type(version.value) is not int or version.value != 1:
            lines.append(f"    版本 {self.raw(version)}。")
        conditions = sequence(premise.args[0]) if call(premise, "dv_all", 1) else None
        if conditions is not None and len(conditions) >= 2:
            for index, condition in enumerate(conditions):
                lines.append(f"    {'当' if index == 0 else '并且'} {self.proposition(condition)}。")
            premise_form = "ordered-all-clauses"
            natural_conditions = len(conditions)
        else:
            # In particular, do not turn all([p]) into p or all([]) into true.
            lines.append(f"    前提 {self.raw(premise)}。")
            premise_form = "original-expression"
            natural_conditions = 0
        items = sequence(effects)
        if items:
            for index, effect in enumerate(items):
                lines.append(f"    {'则' if index == 0 else '并使'} {self.effect(effect)}。")
            effect_form = "ordered-effect-clauses"
            natural_effects = len(items)
        else:
            lines.append(f"    效果 {self.raw(effects)}。")
            effect_form = "original-expression"
            natural_effects = 0
        return "\n".join(lines), premise_form, effect_form, natural_conditions, natural_effects


def declarations(text: str):
    """Yield top-level conventional declarations without consuming adjacent text."""
    lines = text.splitlines(keepends=True)
    starts, cursor = [], 0
    for line in lines:
        starts.append(cursor)
        cursor += len(line)
    for index, line in enumerate(lines):
        head = HEADER.fullmatch(line.rstrip("\r\n"))
        if head is None:
            continue
        last = index + 1
        while last < len(lines):
            candidate = lines[last]
            if candidate.strip() and not candidate.startswith((" ", "\t")):
                break
            last += 1
        body = [(n, lines[n].rstrip("\r\n")) for n in range(index + 1, last)
                if lines[n].strip()]
        # Comments inside a function require a human decision about placement.
        # Treat these as non-pure; do not silently drop author annotations.
        if len(body) != 1:
            yield head, index + 1, starts[index], None, None, "nontrivial-body"
            continue
        match = RETURN.fullmatch(body[0][1])
        if match is None:
            yield head, index + 1, starts[index], None, None, "not-direct-return"
            continue
        end = starts[body[0][0]] + len(body[0][1])
        yield head, index + 1, starts[index], end, match["expression"], None


def plan(text: str) -> tuple[list[Change], list[dict], Counter]:
    changes, actions = [], []
    skipped = Counter()
    for head, line, start, end, expression, reason in declarations(text):
        if reason:
            if head["kind"] == "axiom":
                skipped[reason] += 1
            continue
        try:
            node = ast.parse(expression, mode="eval").body
        except SyntaxError:
            if head["kind"] == "axiom":
                skipped["outside-python-expression-subset"] += 1
            continue
        action = node.args[0] if call(node, "c1_once", 1) else node
        if call(action, "c1_action", 7) or call(action, "c2_action", 8):
            actions.append({"name": head["name"], "line": line,
                            "constructor": action.func.id,
                            "once": call(node, "c1_once", 1)})
        if head["kind"] != "axiom":
            continue
        if not call(node, "dv_axiom", 4):
            skipped["not-direct-dv-axiom"] += 1
            continue
        params = [part.strip() for part in head["params"].split(",") if part.strip()]
        if any(PARAMETER.fullmatch(p) is None for p in params):
            skipped["nontrivial-parameters"] += 1
            continue
        result, premise_form, effect_form, conditions, effects = Renderer(expression).declaration(head["name"], params, node)
        changes.append(Change(head["name"], line, start, end, text[start:end], result,
                              premise_form, effect_form, conditions, effects))
    return changes, actions, skipped


def transformed(text: str, changes: list[Change]) -> str:
    for change in reversed(changes):
        if text[change.start:change.end] != change.before:
            raise ValueError(f"Source changed before {change.name}")
        text = text[:change.start] + change.after + text[change.end:]
    return text


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--write", action="store_true", help="Apply the printed, deterministic migration")
    parser.add_argument("--dry-run", action="store_true", help="Explicitly report only (the default)")
    parser.add_argument("--report", type=Path, help="Exclusively create a detailed engineering migration manifest")
    args = parser.parse_args()
    if args.write and args.dry_run:
        parser.error("--write and --dry-run are mutually exclusive")
    root = args.root.resolve()
    pending, entries = [], []
    for relative in FILES:
        path = root / relative
        original = path.read_text(encoding="utf-8-sig")
        changes, actions, skipped = plan(original)
        candidate = transformed(original, changes)
        entry = {"path": relative, "before_sha256": sha256(original), "after_sha256": sha256(candidate),
                 "changes": [asdict(change) for change in changes], "action_candidates": actions,
                 "skipped_axioms": dict(skipped)}
        entries.append(entry)
        pending.append((path, original, candidate))
    report = {"kind": "engineering-source-migration", "writes_requested": args.write,
              "scope": "Pure dv_axiom-returning functions only; no game execution",
              "files": entries}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        with args.report.open("x", encoding="utf-8", newline="\n") as handle:
            json.dump(report, handle, ensure_ascii=False, indent=2)
            handle.write("\n")
    if args.write:
        # Check all inputs again before writing any of them.
        for path, original, candidate in pending:
            if path.read_text(encoding="utf-8-sig") != original:
                raise RuntimeError(f"Source changed during migration planning: {path}")
        for path, original, candidate in pending:
            if candidate != original:
                path.write_text(candidate, encoding="utf-8", newline="\n")
    for entry in entries:
        print(json.dumps({"path": entry["path"], "axioms": len(entry["changes"]),
                          "natural_conditions": sum(x["natural_conditions"] for x in entry["changes"]),
                          "natural_effects": sum(x["natural_effects"] for x in entry["changes"]),
                          "skipped_axioms": entry["skipped_axioms"],
                          "action_candidates": entry["action_candidates"], "written": args.write},
                         ensure_ascii=False))


if __name__ == "__main__":
    main()
