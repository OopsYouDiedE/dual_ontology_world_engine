"""Read host monotonic phase records; never runs or changes a game session."""
import argparse
import sys
from pathlib import Path as EvidencePath
sys.path.insert(0, str(EvidencePath(__file__).resolve().parents[2] / "language_core/tools"))
from binary_evidence import records, RecordWriter
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("--out", required=True, type=Path)
args = parser.parse_args()
if args.out.exists():
    raise SystemExit("Refusing to overwrite timing summary")
groups = {}
for row in records(args.source):
    identifier = row.get("interaction_id", "")
    if identifier:
        groups.setdefault(identifier, []).append(row)
intervals = {
    "queue_ms": ("command_received", "command_started"),
    "prepare_ms": ("prepare_begin", "prepare_end"),
    "snapshot_ms": ("snapshot_begin", "snapshot_end"),
    "execution_ms": ("execution_begin", "execution_end"),
    "complete_ms": ("complete_begin", "complete_end"),
    "input_to_first_frame_ms": ("input_request", "first_frame_drawn"),
    "input_to_local_choices_ms": ("input_request", "local_choices_frame"),
    "local_motion_ms": ("local_motion_begin", "local_motion_end"),
}
result = []
for identifier, rows in groups.items():
    first = {}
    for row in rows:
        first.setdefault(row["stage"], row["real_us"])
    values = {}
    for name, (begin, end) in intervals.items():
        values[name] = (first[end] - first[begin]) / 1000 if begin in first and end in first and first[end] >= first[begin] else None
    result.append({"interaction_id": identifier, "milliseconds": values, "stages": rows})
with RecordWriter(args.out) as output:
    output.append({"clock": "GameEngine Stopwatch, real microseconds; independent of playback speed", "missing": "null means stage not observed; reading and idle time are not CPU time", "interactions": result})
