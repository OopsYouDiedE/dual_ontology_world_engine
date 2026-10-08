from __future__ import annotations
import json
import re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
BASE = ROOT / "artifacts/basic-ui-system"
TRACE = BASE / "player-traces"
ROUND = BASE / "literary-rounds"

def rows(path: Path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]

def compact(value: object) -> str:
    return re.sub(r"\s+", "", str(value))

def strip_bbcode(value: object) -> str:
    return re.sub(r"\[[^\]]+\]", "", str(value))

engine = rows(ROUND / "round-04-engine-full-output.jsonl")
ui = rows(ROUND / "round-04-ui-full-output.jsonl")
route = (ROUND / "round-04-bound-route.txt").read_text(encoding="utf-8-sig")
fixed03 = (ROUND / "round-04-03-ui-actions.txt").read_text(encoding="utf-8-sig")
visible_rows = [r for r in ui if r.get("kind") == "ui_visible"]
visible_texts = [x.get("text", "") for r in visible_rows for x in r.get("data", {}).get("texts", []) if x.get("text")]
visible_compact = compact("".join(visible_texts))

def is_visible(value: object) -> bool:
    return compact(strip_bbcode(value)) in visible_compact

packet_rendered = {int(r.get("data", {}).get("source_sequence", -1)) for r in ui if r.get("kind") == "packet_rendered"}
engine_sequences = {int(p.get("sequence", -1)) for p in engine}
render_finished = next((r.get("data", {}) for r in reversed(ui) if r.get("kind") == "render_finished"), {})
ending_rows = [r for r in visible_rows if r.get("data", {}).get("panel") == "ending"]

# Mirror capture_player_transcript.gd::_text_covered against raw visible text segments.
line_segments = defaultdict(list)
for row in visible_rows:
    data = row.get("data", {})
    for item in data.get("texts", []):
        if item.get("line_index") is not None and item.get("text"):
            key = (int(data.get("source_sequence", -1)), int(data.get("output_index", -1)), int(item["line_index"]))
            line_segments[key].append(str(item["text"]))

def visually_covered(expected: str, fragments: list[str]) -> bool:
    target = compact(expected)
    if not target:
        return True
    marks = [False] * len(target)
    for raw in fragments:
        fragment = compact(raw)
        if not fragment:
            continue
        start = 0
        while True:
            offset = target.find(fragment, start)
            if offset < 0:
                break
            marks[offset:offset + len(fragment)] = [True] * len(fragment)
            start = offset + 1
    return all(marks)

line_total = 0
line_covered = 0
unique_engine_lines = set()
for packet in engine:
    for output_index, event in enumerate(packet.get("output", [])):
        if event.get("kind") != "lines":
            continue
        for line_index, line in enumerate(event.get("lines", [])):
            text = str(line.get("text", ""))
            unique_engine_lines.add(text)
            line_total += 1
            if visually_covered(text, line_segments.get((int(packet["sequence"]), output_index, line_index), [])):
                line_covered += 1

route_lines = sum(1 for value in unique_engine_lines if compact(value) in compact(route))
line_events = sum(1 for packet in engine for event in packet.get("output", []) if event.get("kind") == "lines")

superseded_events = {(int(r.get("data", {}).get("source_sequence", -1)), int(r.get("data", {}).get("output_index", -1))) for r in ui if r.get("kind") == "superseded"}

def collect(kind: str, placeholders: bool = False) -> list[tuple[int, int, str]]:
    values: list[tuple[int, int, str]] = []
    for packet in engine:
        for output_index, event in enumerate(packet.get("output", [])):
            if event.get("kind") != kind:
                continue
            if (int(packet["sequence"]), output_index) in superseded_events:
                continue
            texts = [] if placeholders else [str(event[key]) for key in ("title", "intro", "prompt", "facts", "source", "text") if event.get(key)]
            if not placeholders:
                for group in ("items", "notes"):
                    texts.extend(str(item) for item in event.get(group, []))
            for group in ("options", "fields", "actions"):
                for item in event.get(group, []):
                    if isinstance(item, dict):
                        keys = ("placeholder",) if placeholders else ("label", "heading", "text")
                        texts.extend(str(item[key]) for key in keys if item.get(key))
                        for option in item.get("options", []):
                            if not placeholders and isinstance(option, dict) and option.get("label"):
                                texts.append(str(option["label"]))
            values.extend((int(packet["sequence"]), output_index, value) for value in texts)
    return values

event_visible = defaultdict(list)
for row in visible_rows:
    data = row.get("data", {})
    key = (int(data.get("source_sequence", -1)), int(data.get("output_index", -1)))
    event_visible[key].extend(str(item.get("text", "")) for item in data.get("texts", []) if item.get("text"))

def event_covered(item: tuple[int, int, str]) -> bool:
    seq, index, value = item
    return compact(strip_bbcode(value)) in compact("".join(event_visible[(seq, index)]))

candidate_values = collect("candidates")
card_values = collect("card")
form_values = collect("form")
form_placeholders = collect("form", placeholders=True)
ending_values = collect("ending")
category_results = {
    "candidates": (sum(event_covered(v) for v in candidate_values), len(candidate_values)),
    "cards": (sum(event_covered(v) for v in card_values), len(card_values)),
    "forms": (sum(event_covered(v) for v in form_values), len(form_values)),
    "endings": (sum(event_covered(v) for v in ending_values), len(ending_values)),
}

summary = [
    "第 4 轮一级技术覆盖复核",
    f"Engine packets: {len(engine)}; packet_rendered: {len(packet_rendered)}/{len(engine)}; unrendered sequences: {len(engine_sequences - packet_rendered)}.",
    f"Dialogue output events: {line_events}; dialogue lines: visually covered {line_covered}/{line_total}; unique line texts present in bound route: {route_lines}/{len(unique_engine_lines)}.",
    f"Rendered UI frames: {len(visible_rows)}; ending panel frames: {len(ending_rows)}; render_finished={render_finished.get('finished')}; ended_displayed={render_finished.get('ended_displayed')}; final_panel={render_finished.get('final_panel')}; screenshots={render_finished.get('screenshots')}.",
    f"Superseded events omitted from per-event text expectations: {len(superseded_events)}; form placeholders separately checked: {sum(event_covered(v) for v in form_placeholders)}/{len(form_placeholders)}.",
    "UI text coverage (each engine event matched only to its own rendered UI frames; BBCode removed):",
]
for name, (matched, total) in category_results.items():
    summary.append(f"  {name}: {matched}/{total} strings found in rendered UI text.")
for name, values in (("candidates", candidate_values), ("cards", card_values), ("forms", form_values), ("endings", ending_values)):
    missing = [f"{seq}/{index}: {value[:100]}" for seq, index, value in values if not event_covered((seq, index, value))]
    if missing:
        summary.append(f"  {name} missing per-event: " + " | ".join(missing[:20]))
summary += [
    f"Fixed 03 final marker present: {'第五日终局结束界面已实际绘制' in fixed03}.",
    "Method: dialogue coverage follows the capture script's segmented visible-text coverage algorithm; other categories compare each engine event payload only against the same sequence/output-index captured ui_visible frames. This is text/render alignment only, not geometry, collision, literary, or blind-play acceptance.",
]
print("\n".join(summary))
(ROUND / "round-04-coverage-check.txt").write_text("\n".join(summary) + "\n", encoding="utf-8")



