"""Read an existing raw session; never starts or modifies the engine."""
import json
import sys
from pathlib import Path


def record(value):
    if isinstance(value, list):
        if value and all(isinstance(p, list) and len(p) == 2 and isinstance(p[0], str) for p in value):
            return {p[0]: record(p[1]) for p in value}
        return [record(p) for p in value]
    return value


source = Path(sys.argv[1])
events, visible, executions, ends = [], [], [], []
last = None
with source.open(encoding="utf-8") as stream:
    for line in stream:
        try:
            packet = record(json.loads(line))
        except json.JSONDecodeError:
            continue
        last = packet
        if packet.get("type") == "debug_events":
            events.extend(packet["batch"]["events"])
        if packet.get("type") == "player_presentation":
            visible.append(packet["presentation"])
        if "status" in packet and "action_id" in packet:
            executions.append(packet)
        if packet.get("type") in ("debug_end", "session_stopped"):
            ends.append(packet)
result = {
    "source": str(source), "last_packet": last, "ending": ends,
    "executions": executions,
    "actions_by_actor": {actor: sorted({e["action_id"] for e in executions if e["actor"] == actor}) for actor in sorted({e["actor"] for e in executions})},
    "significant_events": [e for e in events if e["type"] in ("answer_accepted", "answer_rejected", "chapter_content_available", "norm_receipt", "norm_decision", "session_end", "transition", "simulation")],
    "visible_presentations": [v for v in visible if v.get("visible")],
}
print(json.dumps(result, ensure_ascii=False, indent=2))
