"""Read-only trajectory evidence. Event counts are not acceptance scores."""
import collections
import json
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "language_core/tools"))
from run_debug import record

root = Path(sys.argv[1])
executions = []
milestones = []
ends = []
invalid = []
for number, line in enumerate((root / "stdout.jsonl").read_text(encoding="utf-8").splitlines(), 1):
    if not line.strip():
        continue
    try:
        packet = record(json.loads(line))
    except ValueError as error:
        invalid.append({"line": number, "error": str(error)})
        continue
    if packet.get("type") == "debug_end":
        ends.append(packet)
    if packet.get("type") != "debug_events":
        continue
    for event in packet["batch"]["events"]:
        kind = event["type"]
        action = event.get("action_id", "")
        item = {k: event.get(k) for k in ("event_id", "type", "actor", "game_time", "action_id", "action_kind", "detail")}
        if kind == "action_execution":
            executions.append(item)
            if event.get("detail", {}).get("status") == "applied" and (action.endswith((".departure.cross-out", ".departure.join-living")) or event.get("action_kind") == "read"):
                milestones.append(item)
        if kind in ("chapter_content_available", "answer_accepted", "answer_rejected", "session_stopped"):
            milestones.append(item)
repeats = collections.Counter((e["actor"], e["action_id"]) for e in executions)
report = {"scope": "actual logical execution only; no geometry or inferred completion", "last_executions": executions[-12:], "milestones": milestones, "repeated_actions": [{"actor": key[0], "action_id": key[1], "occurrences": value} for key, value in repeats.most_common(20) if value > 1], "debug_end": ends, "invalid_lines": invalid}
(root / "trajectory-review.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"milestones": milestones, "debug_end": ends, "invalid_lines": invalid}, ensure_ascii=True))
