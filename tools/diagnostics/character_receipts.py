"""Aggregate original native-character receipts without controlling a game session."""
import argparse
import csv
import json
import math
from pathlib import Path
from statistics import mean

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("observations", type=Path)
parser.add_argument("--destination", type=Path, required=True)
args = parser.parse_args()
paths = sorted(args.observations.glob("observation-*.json"))
records = [(path, json.loads(path.read_text(encoding="utf-8"))) for path in paths]
records = [(path, record) for path, record in records
           if record.get("character_session", {}).get("streaming", {}).get("scope") == "stage2-single-actor"]
if not records:
    raise SystemExit("No original stage-two native-character observations.")
last = records[-1][1]["character_session"]
streaming = last["streaming"]
directory = Path(streaming["receipt_directory"])
timeline = directory / "timeline.jsonl"
events = [json.loads(line) for line in timeline.read_text(encoding="utf-8").splitlines()]
destination = args.destination
destination.mkdir(parents=True, exist_ok=True)

def export(name, rows):
    if not rows:
        return
    fields = list(dict.fromkeys(key for row in rows for key in row))
    with (destination / name).open("w", newline="", encoding="utf-8-sig") as output:
        writer = csv.DictWriter(output, fields)
        writer.writeheader()
        writer.writerows({key: json.dumps(value, ensure_ascii=False) if isinstance(value, (dict, list)) else value
                          for key, value in row.items()} for row in rows)

def stats(values):
    values = sorted(values)
    return {"samples": len(values), "mean_ms": mean(values),
            "p99_ms": values[math.ceil(len(values) * .99) - 1], "maximum_ms": max(values)} if values else {}

for kind, name in [("frame", "frame-measurements.csv"), ("slow_frame", "slow-frames.csv"),
                   ("assembly", "assembly-measurements.csv"), ("room_transition", "phase-transitions.csv"),
                   ("bundle_activation", "bundle-activation.csv")]:
    export(name, [dict(time_us=event["time_us"], **event.get("details", {}),
                       **{key: value for key, value in event.items()
                          if key not in {"time_us", "details", "kind", "utc"}})
                  for event in events if event["kind"] == kind])
resources = [dict(time_us=event["time_us"], kind=event["kind"], **event["details"])
             for event in events if event["kind"] in {"baseline", "resources", "shutdown", "measured_run_begin"}]
export("resource-audit.csv", resources)
export("observation-timeline.csv", [dict(file=str(path.resolve()), **{key: record["character_session"].get(key)
              for key in ("phase", "position", "last_failure", "props", "state")}) for path, record in records])
actions_path = ROOT / "artifacts" / "coregodot-stage2" / ("live-" + args.observations.name.split("-")[1]) / "actions.jsonl"
actions = [json.loads(line) for line in actions_path.read_text(encoding="utf-8").splitlines()]
actor_frames = [event for event in actions if event["kind"] == "actor_frame"]
export("actor-frames.csv", actor_frames)
export("action-events.csv", [event for event in actions if event["kind"] != "actor_frame"])
final_summary_path = directory / "summary.json"
closed = json.loads(final_summary_path.read_text(encoding="utf-8"))
summary = {
    "scope": "stage2-single-actor", "observations": str(args.observations.resolve()),
    "timeline": str(timeline.resolve()), "actions": str(actions_path.resolve()),
    "inclusive_frames": stats([event["interval_ms"] for event in events if event["kind"] == "frame"]),
    "core": stats([event["core_ms"] for event in actor_frames]),
    "motor": stats([event["motor_ms"] for event in actor_frames]),
    "baseline": streaming["baseline"], "last_observation_resources": streaming["resources"],
    "closed_resources": closed["resources"], "host_status": closed.get("host_status"),
    "sampled_peaks": {key: max(row[key] for row in resources) for key in
                      ("nodes", "orphan_nodes", "active_collision_objects", "static_memory_bytes", "pending_loads")},
    "assembly": {key: stats([event["details"][key] for event in events if event["kind"] == "assembly"])
                 for key in ("instantiate_ms", "attach_ms", "total_ms")},
    "source_observation_files": [str(path.resolve()) for path, _ in records],
}
(destination / "receipt-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(summary, ensure_ascii=False))
