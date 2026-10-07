"""Read-only author / runtime / drawn-UI comparison. Never starts or advances a game."""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ENTRY = ROOT / "godot_csharp/scenes/Awakening/Script/chapter_01.story"
TAG = re.compile(r"\s*\[#([^\]]+)\]")


def author_files(entry: Path):
    """Use the entry's actual include order; wildcard fragments retain their author order."""
    yield entry
    for line in entry.read_text(encoding="utf-8-sig").splitlines():
        match = re.match(r"\s*引入[：:]\s*(.+?)\s*$", line)
        if match:
            for child in sorted(entry.parent.glob(match[1])):
                yield from author_files(child)


def author_blocks(snapshots=None):
    blocks = []
    files = ((ROOT / name, text) for name, text in snapshots.items()) if snapshots else (
        (file, file.read_text(encoding="utf-8-sig")) for file in author_files(ENTRY))
    for file, content in files:
        block = None
        for number, line in enumerate(content.splitlines(), 1):
            heading = re.match(r"^(\s*)(场面|流程|证物|推演变量|陈述|文案|守则|选项|回应|候选|请求|话题)(?:\s+(.+?))?\s*$", line)
            if heading:
                block = {"kind": heading[2], "name": heading[3] or "", "file": str(file),
                         "line": number, "indent": len(heading[1]), "items": []}
                blocks.append(block)
                continue
            if block is None or not line.strip() or line.lstrip().startswith("#"):
                continue
            indent = len(line) - len(line.lstrip())
            if indent <= block["indent"]:
                block = None
                continue
            tags = dict(part.split("=", 1) for part in TAG.findall(line) if "=" in part)
            body = TAG.sub("", line).strip()
            item = {"file": str(file), "line": number, "text": body, "line_id": tags.get("id", "")}
            if item["line_id"]:
                speaker, separator, text = body.partition("：")
                item["speaker"] = speaker if separator else ""
                item["text"] = text if separator else body
            block["items"].append(item)
    return blocks


def plain(text):
    return re.sub(r"\[/?(?:color|b|i|u|center|font|font_size)(?:=[^\]]+)?\]", "", text or "")


def source_matches(template, rendered):
    # Placeholder substitution is allowed; concrete words and punctuation must remain the same.
    pieces = re.split(r"(\{\{[^}]+\}\})", plain(template))
    pattern = "".join(".*?" if p.startswith("{{") else re.escape(p) for p in pieces)
    return re.fullmatch(pattern, plain(rendered), re.DOTALL) is not None


def load_trace(path):
    rows = []
    with path.open(encoding="utf-8-sig") as stream:
        for number, line in enumerate(stream, 1):
            if line.strip():
                try:
                    rows.append(json.loads(line))
                except json.JSONDecodeError as error:
                    raise ValueError(f"Incomplete trace row {number}: {error.msg}") from error
    return rows


def compare(rows):
    snapshots = next((row.get("data", {}).get("story_files") for row in rows if row.get("kind") == "author_sources"), None)
    authored = {item["line_id"]: item for block in author_blocks(snapshots) for item in block["items"] if item["line_id"]}
    outputs, visible = {}, {}
    for row in rows:
        data = row.get("data", {})
        if row.get("kind") == "output" and data.get("kind") == "lines":
            for index, line in enumerate(data.get("lines", [])):
                key = (data.get("trace_id", ""), index)
                outputs[key] = {"sequence": row["sequence"], **line}
        if row.get("kind") == "ui_visible":
            for item in data.get("texts", data.get("items", [])):
                if "line_index" not in item or not item.get("trace_id"):
                    continue
                key = (item["trace_id"], item["line_index"])
                text = item.get("text", "")
                previous = visible.get(key)
                if previous is None or len(text) >= len(previous.get("text", "")):
                    visible[key] = {"sequence": row["sequence"], **item}
    report = []
    for key, line in outputs.items():
        ui = visible.get(key)
        expected = plain(line.get("text", ""))
        source = authored.get(line.get("line_id", ""))
        status = "emitted_not_observed"
        if ui is not None:
            shown = ui.get("text", "")
            # Scrolling can expose a middle portion rather than a prefix.
            portions = shown.splitlines() if shown else []
            is_portion = not portions or all(part in expected for part in portions)
            status = "full" if shown == expected and ui.get("complete") and not ui.get("content_clipped") else "partial" if is_portion else "different"
        report.append({"trace_id": key[0], "line_index": key[1], "line_id": line.get("line_id", ""),
                       "speaker": line.get("speaker", ""), "runtime_text": expected,
                       "visible_text": ui.get("text", "") if ui else "", "display": status,
                       "author": source,
                       "source_match": source_matches(source["text"], expected) if source else None})
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["source", "timeline", "compare"])
    parser.add_argument("trace", nargs="?", type=Path)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    if args.mode == "source":
        result = author_blocks()
        if args.json:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        else:
            print("Author catalogue, including conditional branches; not a record of this player's run.")
            for block in result:
                print(f"\n{block['kind']} {block['name']} [{block['file']}:{block['line']}]")
                for item in block["items"]:
                    print((item.get("speaker", "") + "：" if item.get("speaker") else "") + item["text"])
        return
    if args.trace is None:
        parser.error("timeline and compare require a single run's JSONL path")
    rows = load_trace(args.trace)
    if args.mode == "compare":
        result = compare(rows)
        if args.json:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        else:
            print("Emitted / observed comparison. An unobserved line is not automatically an error or a player memory.")
            for item in result:
                print(f"[{item['display']}] {item['line_id']} {item['speaker']}：{item['runtime_text']}")
                if item["display"] != "full":
                    print("  Screen evidence: " + item["visible_text"])
                if item["source_match"] is False:
                    print("  Author text differs: " + item["author"]["text"])
                elif item["author"] is None:
                    print("  No authored line ID: generated text or author location unavailable.")
    else:
        # Keep repeats and physical controls. Core-only rows are never included as what the player saw.
        result = [row for row in rows if row.get("kind") in {"input", "ui_input", "ui_visible"}]
        if args.json:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        else:
            for row in result:
                print(f"[{row['elapsed_ms']}ms / #{row['sequence']}] {row['kind']}")
                if row["kind"] == "ui_visible":
                    for item in row["data"].get("texts", row["data"].get("items", [])):
                        if item.get("text"):
                            print("  " + item['text'])
                    if "objects" in row["data"]:
                        print("  场上物品：" + "、".join(row["data"]["objects"]))
                else:
                    print("  " + json.dumps(row["data"], ensure_ascii=False))


if __name__ == "__main__":
    main()
