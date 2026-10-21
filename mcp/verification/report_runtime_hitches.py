"""Read passive host timestamps. Wall-time overlap is not sampled CPU attribution."""
import argparse
from binary_evidence import records, RecordWriter
import json
from collections import defaultdict
from pathlib import Path


def details(row):
    value = row.get("details", {})
    if isinstance(value, str):
        try:
            value = json.loads(value)
        except (ValueError, TypeError):
            return {}
    return value.get("details", value) if isinstance(value, dict) else {}


def analyze(rows):
    pairs = {
        "open_begin": ("open_end", "session_open"),
        "prepare_begin": ("prepare_end", "DV_prepare_other"),
        "complete_begin": ("complete_end", "DV_complete"),
        "snapshot_begin": ("snapshot_end", "snapshot"),
        "delivery_begin": ("delivery_end", "host_delivery_other"),
        "view_build_begin": ("view_build_end", "view_build"),
        "visible_trace_begin": ("visible_trace_end", "visible_trace"),
        "ui_apply_begin": ("ui_apply_end", "UI_apply"),
        "capture_begin": ("capture_png_end", "capture_PNG"),
        "capture_png_end": ("capture_end", "capture_metadata"),
        "journal_begin": ("journal_end", "diagnostic_journal"),
        "execution_begin": ("execution_end", "physical_execution_or_receipt"),
        "presentation_begin": ("reading_finished", "dialogue_reading"),
    }
    if any(r["stage"] == "capture_readback_end" for r in rows):
        pairs["capture_begin"] = ("capture_readback_end", "capture_readback")
        pairs["capture_readback_end"] = ("capture_png_end", "capture_pixel_copy_queue")
    for name in ("schedule", "goals", "cognition", "choice"):
        pairs[f"dv_{name}-start"] = (f"dv_{name}-end", f"DV_{name}")
    ends = defaultdict(list)
    for begin, (end, category) in pairs.items():
        ends[end].append((begin, category))
    stacks = defaultdict(list)
    spans = []
    commands = defaultdict(dict)
    for row in rows:
        stage, at = row["stage"], row["real_us"]
        command = row.get("command_id", 0)
        if stage in ("command_received", "command_started", "command_finished", "events_delivered"):
            commands[command][stage] = at
            commands[command]["kind"] = row.get("details")
        for begin, category in ends[stage]:
            # Host worker spans are command-scoped. Presentation/execution may span commands.
            scope = command if begin.startswith(("dv_", "prepare", "complete", "snapshot", "open")) else 0
            stack = stacks[(begin, scope)]
            if stack:
                start = stack.pop()
                spans.append({"begin_us": start["real_us"], "end_us": at, "category": category,
                              "command_id": start.get("command_id", 0), "details": details(start)})
        if stage in pairs:
            scope = command if stage.startswith(("dv_", "prepare", "complete", "snapshot", "open")) else 0
            stacks[(stage, scope)].append(row)
    priority = {"DV_prepare_other": 1, "host_delivery_other": 1,
                "physical_execution_or_receipt": -1, "dialogue_reading": -2}

    def composition(begin, end):
        selected = [s for s in spans if s["begin_us"] < end and begin < s["end_us"]
                    and priority.get(s["category"], 10) > 0]
        boundaries = sorted({begin, end} | {max(begin, s["begin_us"]) for s in selected}
                            | {min(end, s["end_us"]) for s in selected})
        totals = defaultdict(float)
        for left, right in zip(boundaries, boundaries[1:]):
            covering = [s for s in selected if s["begin_us"] <= left and right <= s["end_us"]]
            # Concurrent main-thread work takes precedence over a background DV interval.
            chosen = max(covering, key=lambda s: (priority.get(s["category"], 10)
                         + (20 if s["category"] in {"UI_apply", "view_build", "visible_trace", "capture_PNG", "capture_readback", "capture_pixel_copy_queue", "capture_metadata", "diagnostic_journal"} else 0),
                         s["begin_us"]), default=None)
            totals[chosen["category"] if chosen else "unattributed_wall_time"] += (right-left)/1000
        context = sorted({s["category"] for s in spans if s["begin_us"] < end and begin < s["end_us"]
                          and priority.get(s["category"], 10) < 0})
        return {"exclusive_wall_ms": dict(totals), "context": context,
                "overlapping_spans": [s for s in spans if s["begin_us"] < end and begin < s["end_us"]]}

    gaps = []
    locations = []
    for row in rows:
        if row["stage"] == "frame_gap":
            d = details(row)
            begin, end = d["begin_us"], d["end_us"]
            gaps.append({"begin_us": begin, "end_us": end, "duration_ms": (end-begin)/1000,
                         "frame_boundaries": {k: d[k] for k in ("process_begin_us", "render_begin_us") if k in d},
                         **composition(begin, end)})
        if row["stage"] in ("physical_location", "actor_in_viewport"):
            locations.append({"real_us": row["real_us"], "stage": row["stage"], **details(row)})
    waits = []
    for command, c in commands.items():
        begin = c.get("command_received")
        end = c.get("events_delivered", c.get("command_finished"))
        if begin is not None and end is not None and end-begin >= 100000:
            waits.append({"command_id": command, **c, "duration_ms": (end-begin)/1000,
                          "queue_ms": (c["command_started"]-begin)/1000 if "command_started" in c else None,
                          **composition(begin, end)})
    return {"clock": "GameEngine monotonic real microseconds; independent of playback speed",
            "interpretation": "frame_gap >=50ms between actual frame_post_draw callbacks; command wait >=100ms. Composition is exclusive WALL overlap, not CPU or proof of causation. Main-thread work takes precedence over concurrent DV. Unknown residual remains unknown. Normal walking, dialogue and human pauses are not themselves hitches. PNG required for visible content.",
            "frame_gaps": gaps, "slow_commands": waits, "locations": locations,
            "unfinished_spans": [r for stack in stacks.values() for r in stack]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--archive", type=Path, help="Separate background PNG worker intervals; never add them to main-thread blocking")
    args = parser.parse_args()
    if args.out.exists():
        raise SystemExit("Refusing to overwrite evidence")
    rows = list(records(args.source))
    result = analyze(rows)
    if args.archive:
        result["background_archive"] = list(records(args.archive))
    with RecordWriter(args.out) as output:
        output.append(result)
