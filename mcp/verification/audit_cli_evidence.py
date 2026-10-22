"""Read-only indexes of a sealed CLI trace, retaining original event and node references."""
import argparse
from collections import Counter
from fractions import Fraction
import json
from pathlib import Path
from binary_evidence import RecordWriter
from raw_dag import RawDagIndex

def isnode(value):
    return isinstance(value, dict) and set(value) == {"node"}

def fields(index, value):
    return index.record_fields(value["node"]) if isnode(value) else {}

def at(index, value, *path):
    for key in path:
        value = fields(index, value).get(key)
    return value

def expand(index, value, depth=12):
    if not isnode(value):
        return value
    if depth <= 0:
        return {"raw_node": value["node"], "not_expanded": True}
    items = index.node(value["node"])
    pairs = [index.node(item["node"]) if isnode(item) else None for item in items]
    if items and all(isinstance(pair, list) and len(pair) == 2 and isinstance(pair[0], str) for pair in pairs):
        return {pair[0]: expand(index, pair[1], depth-1) for pair in pairs}
    return [expand(index, item, depth-1) for item in items]

def scalar(index, value):
    if isnode(value):
        return expand(index, value, 4)
    return value

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trace", type=Path)
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=False)
    counts = Counter()
    selected = []
    states = {}
    event_count = 0
    visible_count = 0
    with RawDagIndex(args.trace) as index:
        if not index.summary["complete_sealing"]:
            raise ValueError("Trace is not completely sealed: " + repr(index.summary))
        root = index.select(checkpoint="final")
        trace_fields = fields(index, root)
        event_nodes = index.node(trace_fields["events"]["node"])
        with RecordWriter(args.out / "events.dvlog") as event_writer, RecordWriter(args.out / "visible.dvlog") as visible_writer:
            for ordinal, pointer in enumerate(event_nodes):
                event = fields(index, pointer)
                kind = event.get("type")
                counts[kind] += 1
                data = event.get("data")
                action = at(index, data, "action")
                receipt = at(index, data, "receipt")
                record = {
                    "ordinal": ordinal, "event_id": event.get("id"), "type": kind,
                    "actor": event.get("actor"), "time": event.get("time"),
                    "raw_node": pointer.get("node"), "raw_source": str(args.trace.resolve()),
                    "parents": expand(index, event.get("parents"), 2),
                    "action_id": at(index, action, "id"),
                    "label": at(index, action, "label"),
                    "status": at(index, receipt, "execution", "status"),
                    "host_action_event": at(index, data, "action_event"),
                }
                small_keys = ("reason", "stage", "question", "answer", "accepted", "law", "source",
                              "outcome", "execution_id", "presentation_id", "status", "chapter",
                              "planning_stop", "planning_strategy", "planning_approximation")
                record["detail"] = {key: expand(index, value, 5) for key, value in fields(index, data).items() if key in small_keys}
                if kind in ("observation", "listener_parse", "speech", "answer_judgment"):
                    for key in ("observations", "message", "utterance", "literal", "source_refs"):
                        if key in fields(index, data):
                            record["detail"][key] = expand(index, fields(index, data)[key], 9)
                event_writer.append(record)
                event_count += 1
                action_id = str(record["action_id"] or "")
                if kind in ("action_execution", "session_stopped", "chapter_content_available", "answer_judgment"):
                    if kind != "action_execution" or any(term in action_id for term in
                        ("answer", "granary", "reconcil", "medicine", "anti_erosion", "council", "rescue", "trial", "unlock", "restriction", "confront", "invite", "joined", "scar")):
                        selected.append(record)
                if kind == "cli_interaction":
                    payload = fields(index, data)
                    visible = {key: expand(index, payload.get(key), 12) for key in
                        ("phase", "interaction_id", "actor", "viewer", "action_id", "label", "selection", "state_version", "real_us", "input_mode", "presentation", "cause_input")}
                    visible.update(event_id=event.get("id"), ordinal=ordinal, raw_node=pointer["node"], game_time=event.get("time"))
                    visible_writer.append(visible)
                    visible_count += 1
        physical = at(index, root, "final", "physical")
        objects = fields(index, at(index, physical, "objects"))
        tracked = ("c2.granary", "c2.counter", "case.contract", "case.astral", "case.logistics", "c2.casualty", "c2.clinic", "c2.daily", "c2.ledger", "c2.council", "c2.medicine")
        health_keys = {"alive", "dead", "stone_stupor", "in_coma", "serious_injury", "severe_weakness",
            "motor_impairment", "respiratory_failure", "anti_erosion_treated", "anti_erosion_carried",
            "erosion_exposure", "active_erosion_disease", "location", "restriction_withdrawn",
            "support_active", "guard_active"}
        for object_id, pointer in objects.items():
            state = fields(index, at(index, pointer, "facets", "state"))
            if object_id in tracked:
                values = {key: expand(index, value, 8) for key, value in state.items()}
            else:
                values = {key: expand(index, value, 8) for key, value in state.items() if key in health_keys}
            if values:
                states[object_id] = values
        summary = {"scope": "read-only evidence index; no gameplay verdict", "trace_integrity": index.summary,
            "event_count": event_count, "visible_interactions": visible_count, "event_types": dict(counts),
            "status": expand(index, trace_fields.get("status"), 4), "time": trace_fields.get("time")}
        with RecordWriter(args.out / "state.dvlog") as writer:
            writer.append({"kind": "final_state", "objects": states, "source": str(args.trace.resolve())})
        with RecordWriter(args.out / "key-events.dvlog") as writer:
            for record in selected:
                writer.append(record)
        (args.out / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
        lines = ["# CLI 正式事件索引", "", "此索引只陈列当前封存记录；剧情与体验结论须结合原始事件和实际截图。", "",
                 f"原始文件：`{args.trace.resolve()}`", f"事件数：{event_count}；交互帧：{visible_count}；终止状态：{summary['status']}；游戏分钟：{summary['time']}", "",
                 "| 事件 | 时间 | 主体 | 类型／行动 | 执行结果 |", "| --- | --- | --- | --- | --- |"]
        for item in selected:
            cells = [item["event_id"], item["time"], item["actor"], item["action_id"] or item["type"], item["status"] or item["detail"].get("reason", "")]
            lines.append("| " + " | ".join(str(v).replace("|", "\\|").replace("\n", " ") for v in cells) + " |")
        (args.out / "event-summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(json.dumps(summary, ensure_ascii=False))
if __name__ == "__main__":
    main()
