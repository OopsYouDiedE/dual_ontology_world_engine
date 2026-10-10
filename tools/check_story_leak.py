#!/usr/bin/env python3
"""Searches Core, the Godot UI and the shared host interfaces for story-specific content.

The vocabulary is taken from the compiled story outputs, so it follows the current
`.story` sources: person/place/object/evidence names, stable entity and evidence IDs,
and every authored line. A hit in code (not a comment) is a violation of the rule that
story content lives only in `.story` and translations.

  python tools/check_story_leak.py              # summary, exit 1 on code hits
  python tools/check_story_leak.py --list       # also print every hit
  python tools/check_story_leak.py --json out.json
  python tools/check_story_leak.py --report     # never fail (for baselines)
"""
from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GAME = ROOT / "godot_csharp"
SCRIPT1 = GAME / "scenes/Awakening/Script"
SCRIPT2 = GAME / "scenes/Chapter2/Script"

# Scope name -> (root, glob patterns). Checks, fixtures and examples are test or sample
# material and are reported by their own tasks, not here.
SCOPES = {
    "core": (ROOT / "csharp", ["**/*.cs"]),
    "ui": (GAME / "scenes/UI", ["**/*.gd", "**/*.cs", "**/*.tscn", "**/*.tres"]),
    "shared-ui": (GAME / "scenes/Shared/UI", ["**/*.gd", "**/*.cs", "**/*.tscn", "**/*.tres"]),
    "interface": (GAME / "scenes/Shared", ["**/*.gd", "**/*.cs", "**/*.tscn"]),
    "host": (GAME / "scenes/Host", ["**/*.gd", "**/*.cs", "**/*.tscn"]),
    # Chapter adapters are the migration target of S03–S12; reported, not gated yet.
    "adapter": (GAME / "scenes", ["Awakening/**/*.cs", "Awakening/**/*.gd", "Chapter2/**/*.cs", "Chapter2/**/*.gd"]),
}
GATED = {"core", "ui", "shared-ui", "interface", "host"}
EXCLUDE = re.compile(r"(^|/)(check|Examples|obj|bin)(/|$)|generated_|\.uid$")
CJK = re.compile(r"[㐀-鿿豈-﫿]")
CJK_RUN = re.compile(r"[㐀-鿿豈-﫿·，。、：；？！“”‘’（）—…]{6,}")
COMMENT = re.compile(r"^\s*(//|///|#|\*|/\*)")
# Words too generic to count as story-specific on their own.
GENERIC = {"取消", "确定", "返回", "继续", "关闭", "门", "床", "桌", "椅", "窗", "房间", "走廊"}


def load_vocabulary() -> tuple[dict[str, str], dict[str, str], list[str]]:
    """Returns (names -> category, ids -> category, authored lines)."""
    names: dict[str, str] = {}
    ids: dict[str, str] = {}
    content = json.loads((SCRIPT1 / "generated_awakening_content.json").read_text(encoding="utf-8"))

    def add_name(value, category):
        if isinstance(value, str) and CJK.search(value) and len(value) >= 2 and value not in GENERIC:
            names.setdefault(value, category)

    for key, value in content.get("PERSON_NAMES", {}).items():
        add_name(value, "person")
        ids.setdefault(key, "person")
    for value in content.get("PLACES", []):
        add_name(value, "place")
    for value in content.get("ROOM_NAMES", {}).values():
        add_name(value, "place")
    for key, value in content.get("OBJECT_NAMES", {}).items():
        add_name(value, "object")
    for key, value in content.get("EVIDENCE", {}).items():
        ids.setdefault(key, "evidence")
        if isinstance(value, dict):
            add_name(value.get("name"), "evidence")
    for chapter_file in [SCRIPT2 / "generated_chapter_02.json"]:
        if not chapter_file.exists():
            continue
        document = json.loads(chapter_file.read_text(encoding="utf-8"))
        for chapter in document.get("chapters", []):
            for entity in chapter.get("entities", []):
                entity_id = str(entity.get("id", ""))
                kind, _, suffix = entity_id.partition(".")
                category = {"person": "person", "room": "place", "object": "object"}.get(kind, "object")
                add_name(entity.get("name"), category)
                if suffix and len(suffix) >= 3:
                    ids.setdefault(suffix, category)
    lines: list[str] = []
    with (SCRIPT1 / "generated_lines_zh.csv").open(encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            text = (row.get("zh") or "").strip()
            if len(text) >= 6:
                lines.append(text)
    return names, ids, lines


def scan(names, ids, lines):
    name_pattern = re.compile("|".join(sorted(map(re.escape, names), key=len, reverse=True)))
    # IDs only count as string literals or as members (P.Huo, Place.Granary); bare
    # identifiers like `su` would match ordinary words.
    id_pattern = re.compile(r"""(?:["'](%s)["'])|(?:\.(%s)\b)""" % ("|".join(map(re.escape, ids)), "|".join(
        re.escape(i) for i in ids if i[:1].isupper())))
    line_text = "\n".join(lines)
    hits = []
    for scope, (root, patterns) in SCOPES.items():
        if not root.exists():
            continue
        files = sorted({p for pattern in patterns for p in root.glob(pattern) if p.is_file()})
        for path in files:
            rel = path.relative_to(root).as_posix()
            if scope == "interface" and rel.startswith("UI/"):
                continue
            if EXCLUDE.search(rel):
                continue
            try:
                text = path.read_text(encoding="utf-8")
            except UnicodeDecodeError:
                continue
            for number, raw in enumerate(text.splitlines(), 1):
                if "STORY-HARDCODE" in raw:
                    continue
                kind = "comment" if COMMENT.match(raw) else "code"
                found = set()
                for match in name_pattern.finditer(raw):
                    found.add(("name:" + names[match.group(0)], match.group(0)))
                for match in id_pattern.finditer(raw):
                    token = match.group(1) or match.group(2)
                    found.add(("id:" + ids[token], token))
                for match in CJK_RUN.finditer(raw):
                    run = match.group(0)
                    if run in line_text:
                        found.add(("line", run))
                for category, token in sorted(found):
                    hits.append({"scope": scope, "file": path.relative_to(ROOT).as_posix(), "line": number,
                                 "kind": kind, "category": category, "token": token, "source": raw.strip()[:200]})
    return hits


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--list", action="store_true")
    parser.add_argument("--json")
    parser.add_argument("--report", action="store_true", help="always exit 0")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    names, ids, lines = load_vocabulary()
    hits = scan(names, ids, lines)
    if args.json:
        Path(args.json).write_text(json.dumps(hits, ensure_ascii=False, indent=1), encoding="utf-8")
    summary: dict[tuple[str, str], int] = {}
    for hit in hits:
        key = (hit["scope"], hit["kind"])
        summary[key] = summary.get(key, 0) + 1
    print(f"vocabulary: {len(names)} names, {len(ids)} ids, {len(lines)} authored lines")
    for scope in SCOPES:
        code, comment = summary.get((scope, "code"), 0), summary.get((scope, "comment"), 0)
        files = len({h["file"] for h in hits if h["scope"] == scope and h["kind"] == "code"})
        print(f"{scope:10s}{'' if scope in GATED else ' (report only)'} code hits {code:5d} in {files:3d} files; comment hits {comment:5d}")
    if args.list:
        for hit in hits:
            print(f'{hit["kind"]:7s} {hit["category"]:16s} {hit["file"]}:{hit["line"]}  [{hit["token"]}]  {hit["source"]}')
    code_hits = sum(1 for h in hits if h["kind"] == "code" and h["scope"] in GATED)
    print("RESULT", "PASS" if code_hits == 0 else f"FAIL {code_hits} code hits")
    return 0 if args.report or code_hits == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
