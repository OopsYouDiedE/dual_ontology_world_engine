#!/usr/bin/env python3
"""Summarize one identity-bound blind Agent input session without grading it.

Usage: python godot_csharp/tools/summarize_blind_playtest.py --session <id>

Only metadata.json, status.json, timeline.jsonl, and optional client.jsonl in the
existing artifacts/first-awakening/blind-<id> directory are read. Subjective
report.md/evaluation.json are referenced if present, never read or rewritten.
An Agent session is not a human playtest. Command gaps are unattributed time,
not reading, thinking, animation, or human playtime measurements.
"""

from __future__ import annotations

import argparse
from collections import defaultdict
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import re
import sys
from typing import Any
import uuid


TOOL_VERSION = "1.2.1"
SCHEMA_VERSION = 2
ARTIFACTS = Path(__file__).resolve().parents[2] / "artifacts" / "first-awakening"
SAFE_SESSION = re.compile(r"[A-Za-z0-9][A-Za-z0-9_-]{0,63}\Z")
SHA256 = re.compile(r"[0-9a-fA-F]{64}\Z")
LONG_GAP_SECONDS = 30.0


class TraceError(ValueError):
    """Missing, mixed, or inconsistent evidence must not produce a summary."""


def canonical(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True,
                      separators=(",", ":"), allow_nan=False)


def fingerprint(value: Any) -> str:
    return hashlib.sha256(canonical(value).encode("utf-8")).hexdigest()


def number(value: Any, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise TraceError(f"{label} must be a finite nonnegative number")
    result = float(value)
    if not math.isfinite(result) or result < 0:
        raise TraceError(f"{label} must be a finite nonnegative number")
    return result


def integer(value: Any, label: str) -> int:
    result = number(value, label)
    if result != int(result):
        raise TraceError(f"{label} must be an integer")
    return int(result)


def object_value(value: Any, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise TraceError(f"{label} must be an object")
    return value


def local_file(directory: Path, name: str) -> Path:
    path = directory / name
    if path.resolve().parent != directory:
        raise TraceError(f"{name} resolves outside the requested session")
    return path


def read_file(directory: Path, name: str, *, optional: bool = False,
              lines: bool = False) -> tuple[Any, dict[str, Any]]:
    path = local_file(directory, name)
    if optional and not path.exists():
        return [], {"path": str(path), "present": False}
    raw = path.read_bytes()
    try:
        text = raw.decode("utf-8-sig")
        if lines:
            values = []
            for index, line in enumerate(text.splitlines(), 1):
                if line.strip():
                    values.append(object_value(json.loads(line), f"{name}:{index}"))
            value: Any = values
        else:
            value = object_value(json.loads(text), name)
    except (UnicodeError, json.JSONDecodeError) as error:
        raise TraceError(f"{name} is not complete valid UTF-8 JSON: {error}") from error
    return value, {"path": str(path), "present": True,
                   "sha256": hashlib.sha256(raw).hexdigest(), "bytes": len(raw)}


class SessionEvidence:
    def __init__(self, session: str) -> None:
        if not SAFE_SESSION.fullmatch(session):
            raise TraceError("session must be 1-64 letters/digits/_/- and start with a letter or digit")
        self.session = session
        self.directory = (ARTIFACTS / f"blind-{session}").resolve()
        if self.directory.parent != ARTIFACTS.resolve() or not self.directory.is_dir():
            raise TraceError("the requested existing session directory was not found inside artifacts/first-awakening")
        self.files: dict[str, Any] = {}
        self.metadata, self.files["metadata"] = read_file(self.directory, "metadata.json")
        self.status, self.files["status"] = read_file(self.directory, "status.json")
        self.status_file = self.status
        self.timeline, self.files["timeline"] = read_file(self.directory, "timeline.jsonl", lines=True)
        self.client, self.files["client"] = read_file(self.directory, "client.jsonl", lines=True, optional=True)
        self.run_id = self.metadata.get("run_id")
        self.agent = self.metadata.get("agent")
        if not isinstance(self.run_id, str) or not self.run_id.strip():
            raise TraceError("metadata lacks run_id")
        if not isinstance(self.agent, str) or not self.agent.strip() or self.agent.strip().lower() == "unspecified":
            raise TraceError("metadata lacks a named Agent identity")
        self.identity(self.metadata, "metadata")
        self.identity(self.status, "status")
        if self.metadata.get("protocol") != 1 or self.status.get("protocol") != 1:
            raise TraceError("only recorded input protocol 1 is supported")
        sources = object_value(self.metadata.get("source_sha256"), "metadata.source_sha256")
        runtime = object_value(self.metadata.get("runtime"), "metadata.runtime")
        if not sources or any(not isinstance(digest, str) or not SHA256.fullmatch(digest)
                              for digest in sources.values()):
            raise TraceError("metadata lacks complete source SHA-256 version evidence")
        if not isinstance(runtime.get("sha256"), str) or not SHA256.fullmatch(runtime["sha256"]):
            raise TraceError("metadata lacks runtime SHA-256 version evidence")
        object_value(self.metadata.get("engine_version"), "metadata.engine_version")
        self.commands: dict[int, dict[str, Any]] = {}
        self.actions: dict[tuple[int, int], dict[str, Any]] = {}
        self.snapshots: list[dict[str, Any]] = []
        self.errors: list[dict[str, Any]] = []
        self.rejections: list[dict[str, Any]] = []
        self.stalls: list[dict[str, Any]] = []
        self.client_sent: dict[int, dict[str, Any]] = {}
        self.client_ack: list[dict[str, Any]] = []
        self.client_timeouts: list[dict[str, Any]] = []
        self.sampling_coverage: dict[str, dict[str, int]] = {}
        self.coverage_warnings: dict[str, dict[str, Any]] = {}
        self.status_reconciliation: dict[str, Any] = {}
        self.validate_timeline()
        self.validate_client()

    def sampling(self, field: str, present: bool, label: str) -> None:
        counts = self.sampling_coverage.setdefault(field, {"present": 0, "missing": 0})
        counts["present" if present else "missing"] += 1
        if not present:
            warning = self.coverage_warnings.setdefault(field, {
                "field": field, "missing_count": 0, "example_locations": [],
                "effect": "Missing samples are unknown, not zero; complete totals or decompositions remain null when affected."})
            warning["missing_count"] += 1
            if len(warning["example_locations"]) < 20:
                warning["example_locations"].append(label)

    def optional_number(self, record: dict[str, Any], key: str, label: str, field: str) -> float | None:
        self.sampling(field, key in record, label + "." + key)
        return number(record[key], label + "." + key) if key in record else None

    def optional_object(self, record: dict[str, Any], key: str, label: str, field: str) -> dict[str, Any] | None:
        self.sampling(field, key in record, label + "." + key)
        return object_value(record[key], label + "." + key) if key in record else None

    def identity(self, record: dict[str, Any], label: str) -> None:
        if record.get("session") != self.session or record.get("run_id") != self.run_id:
            raise TraceError(f"{label} lacks this session/run_id or belongs to another session")
        if "agent" in record and record["agent"] != self.agent:
            raise TraceError(f"{label} belongs to another Agent")
        if record.get("session_dir") and Path(record["session_dir"]).resolve() != self.directory:
            raise TraceError(f"{label}.session_dir points to another session")

    def command(self, value: Any, seq: int, label: str) -> dict[str, Any]:
        record = object_value(value, label)
        self.identity(record, label)
        if integer(record.get("seq"), label + ".seq") != seq:
            raise TraceError(f"{label} command sequence mismatch")
        if not isinstance(record.get("actions"), list):
            # The driver may record an invalid submitted command before rejecting it.
            return record
        return record

    def validate_timeline(self) -> None:
        if not self.timeline or self.timeline[0].get("event") != "session_started":
            raise TraceError("timeline lacks its initial identity-bound session_started event")
        previous_time = -1.0
        last_started = 0
        last_completed = 0
        last_snapshot = 0
        for row_number, row in enumerate(self.timeline, 1):
            label = f"timeline:{row_number}"
            tick = number(row.get("elapsed_usec"), label + ".elapsed_usec")
            if tick < previous_time:
                raise TraceError(f"{label} has a backwards process clock")
            previous_time = tick
            if "session" in row or "run_id" in row or "agent" in row:
                self.identity(row, label)
            event = row.get("event")
            if event == "session_started":
                if row_number != 1:
                    raise TraceError("timeline contains more than one process/session start")
                self.identity(row, label)
            elif event == "command_started":
                seq = integer(row.get("seq"), label + ".seq")
                if seq != last_started + 1 or last_started != last_completed:
                    raise TraceError(f"{label} has a missing, duplicate, or overlapping command")
                command = self.command(row.get("input"), seq, label + ".input")
                self.commands[seq] = {"command": command, "start_usec": tick}
                last_started = seq
            elif event in {"input_started", "input_finished"}:
                seq = integer(row.get("seq"), label + ".seq")
                index = integer(row.get("index"), label + ".index")
                if seq != last_started or seq == last_completed or seq not in self.commands:
                    raise TraceError(f"{label} has no matching active command")
                inputs = self.commands[seq]["command"].get("actions")
                if not isinstance(inputs, list) or index >= len(inputs):
                    raise TraceError(f"{label} action index is outside its command")
                key = (seq, index)
                if event == "input_started":
                    if key in self.actions or row.get("input") != inputs[index]:
                        raise TraceError(f"{label} duplicates or changes its submitted input")
                    object_value(row.get("input"), label + ".input")
                    self.actions[key] = {"seq": seq, "index": index, "input": inputs[index], "start_usec": tick}
                else:
                    if key not in self.actions:
                        if isinstance(inputs[index], dict):
                            raise TraceError(f"{label} lacks input_started")
                        self.actions[key] = {"seq": seq, "index": index, "input": inputs[index]}
                    action = self.actions[key]
                    if "end_usec" in action:
                        raise TraceError(f"{label} duplicates input_finished")
                    action.update(end_usec=tick, elapsed_ms=self.optional_number(row, "elapsed_ms", label, "input.elapsed_ms"),
                                  error=str(row.get("error", "")))
                    if action["error"]:
                        self.errors.append({"source": "driver_input", "seq": seq, "index": index, "error": action["error"]})
            elif event == "snapshot":
                snapshot = object_value(row.get("status"), label + ".status")
                self.identity(snapshot, label + ".status")
                if snapshot.get("protocol") != 1:
                    raise TraceError(f"{label} uses a different input protocol")
                for field in ("pending", "movement", "frame_metrics", "dialogue"):
                    self.optional_object(snapshot, field, label, "snapshot." + field)
                if "frame_metrics" in snapshot:
                    for field in ("session", "command", "gameplay", "capture"):
                        metrics = self.optional_object(snapshot["frame_metrics"], field,
                                                       label + ".frame_metrics", "snapshot.frame_metrics." + field)
                        if metrics is not None:
                            for key in ("frames", "quantile_sample_frames", "hitch_count"):
                                if key in metrics:
                                    integer(metrics[key], label + ".frame_metrics." + field + "." + key)
                            for key in ("p50_ms", "p95_ms", "p99_ms", "hitch_threshold_ms", "max_ms"):
                                if key in metrics:
                                    number(metrics[key], label + ".frame_metrics." + field + "." + key)
                if "movement" in snapshot:
                    for key in ("stall_seconds", "stall_max_seconds"):
                        if key in snapshot["movement"]:
                            number(snapshot["movement"][key], label + ".movement." + key)
                    if "stall_count" in snapshot["movement"]:
                        integer(snapshot["movement"]["stall_count"], label + ".movement.stall_count")
                self.sampling("snapshot.visible_text", "visible_text" in snapshot, label + ".visible_text")
                if "visible_text" in snapshot and not isinstance(snapshot["visible_text"], list):
                    raise TraceError(f"{label}.visible_text must be an array when present")
                screenshot = snapshot.get("screenshot")
                headless_without_image = snapshot.get("headless") is True and screenshot == ""
                if not headless_without_image and (not isinstance(screenshot, str) or Path(screenshot).resolve().parent != self.directory):
                    raise TraceError(f"{label} screenshot belongs to another session")
                seq = integer(snapshot.get("ack_seq"), label + ".ack_seq")
                for field in ("seq", "command_seq"):
                    if integer(snapshot.get(field), label + "." + field) != seq:
                        raise TraceError(f"{label} snapshot sequence mismatch")
                count = integer(snapshot.get("snapshot"), label + ".snapshot")
                if count != last_snapshot + 1 or snapshot.get("accepted") is not True:
                    raise TraceError(f"{label} snapshot is missing, duplicated, or unaccepted")
                if seq == 0:
                    if self.snapshots or last_started or row.get("input") != {}:
                        raise TraceError(f"{label} has an invalid initial snapshot")
                else:
                    if seq != last_started or seq != last_completed + 1:
                        raise TraceError(f"{label} snapshot has no matching outstanding command")
                    command = self.command(row.get("input"), seq, label + ".input")
                    if command != self.commands[seq]["command"]:
                        raise TraceError(f"{label} snapshot changed its command")
                    if any(key[0] == seq and "end_usec" not in value for key, value in self.actions.items()):
                        raise TraceError(f"{label} snapshot precedes an unfinished input")
                    self.commands[seq].update(end_usec=tick, status=snapshot)
                    last_completed = seq
                self.optional_number(snapshot, "elapsed_ms", label, "snapshot.elapsed_ms")
                self.optional_number(snapshot, "capture_elapsed_ms", label, "snapshot.capture_elapsed_ms")
                self.snapshots.append({"elapsed_usec": tick, "status": snapshot})
                last_snapshot = count
                if snapshot.get("error"):
                    self.errors.append({"source": "driver_command", "seq": seq, "error": str(snapshot["error"])})
            elif event == "command_rejected":
                reason = str(row.get("error", ""))
                if reason == "session/run_id does not match this process":
                    raise TraceError(f"{label} records a mixed session/run command attempt")
                self.rejections.append({"seq": integer(row.get("command_seq"), label + ".command_seq"),
                                        "elapsed_usec": tick, "error": reason})
            elif event == "movement_stall":
                seq = integer(row.get("seq"), label + ".seq")
                if seq > last_started:
                    raise TraceError(f"{label} stall references an unknown command")
                self.stalls.append({"seq": seq, "elapsed_usec": tick,
                                    "stall_seconds": number(row.get("stall_seconds"), label + ".stall_seconds")})
            else:
                raise TraceError(f"{label} has unsupported protocol event {event!r}")
        if not self.snapshots:
            raise TraceError("timeline contains no rendered snapshot")
        # A Windows reader can prevent status.json replacement after a complete
        # snapshot was already flushed. Bind the file to its own recorded version,
        # then use the newest fully validated timeline snapshot as authoritative.
        file_ack = integer(self.status_file.get("ack_seq"), "status.ack_seq")
        file_snapshot = integer(self.status_file.get("snapshot"), "status.snapshot")
        if integer(self.status_file.get("seq"), "status.seq") != file_ack:
            raise TraceError("status file has inconsistent sequence fields")
        bound = next((item["status"] for item in self.snapshots
                      if item["status"]["snapshot"] == file_snapshot and item["status"]["ack_seq"] == file_ack), None)
        if bound is None or self.status_file.get("screenshot") != bound.get("screenshot"):
            raise TraceError("status file has no matching identity-bound timeline snapshot")
        command_seq = integer(self.status_file.get("command_seq"), "status.command_seq")
        file_pending = self.optional_object(self.status_file, "pending", "status", "status_file.pending")
        pending = file_pending.get("command_running") if file_pending is not None else None
        if self.status_file.get("accepted") is False:
            if not any(item["seq"] == command_seq for item in self.rejections):
                raise TraceError("rejected status lacks a matching driver rejection")
        elif pending:
            if command_seq != file_ack + 1 or command_seq not in self.commands:
                raise TraceError("pending status lacks its identity-bound command")
        elif command_seq != file_ack:
            raise TraceError("status file has no matching completed command")
        self.status = self.snapshots[-1]["status"]
        self.status_reconciliation = {
            "authoritative_source": "latest complete identity-bound timeline snapshot",
            "status_file_ack_seq": file_ack, "status_file_snapshot": file_snapshot,
            "timeline_ack_seq": last_completed, "timeline_snapshot": last_snapshot,
            "status_file_is_stale": file_snapshot < last_snapshot or file_ack < last_completed,
            "status_file_pending": file_pending,
            "unfinished_driver_command_seqs": [seq for seq, item in self.commands.items() if "status" not in item],
            "note": "An older same-run status file is retained as evidence, not treated as a session identity conflict. Truly mixed identities remain rejected."}

    def validate_client(self) -> None:
        last_sent = 0
        terminal_seqs: set[int] = set()
        for index, row in enumerate(self.client, 1):
            label = f"client:{index}"
            number(row.get("utc_epoch"), label + ".utc_epoch")
            if "session" in row or "run_id" in row or "agent" in row:
                self.identity(row, label)
            event = row.get("event")
            if event == "input_sent":
                command = object_value(row.get("command"), label + ".command")
                seq = integer(command.get("seq"), label + ".seq")
                self.command(command, seq, label + ".command")
                if seq <= last_sent or seq > max(self.commands, default=0) + 1:
                    raise TraceError(f"{label} has a duplicate or unbound client command")
                if seq in self.commands and command != self.commands[seq]["command"]:
                    raise TraceError(f"{label} and driver disagree about the same input")
                self.client_sent[seq] = row
                last_sent = seq
            elif event in {"input_acknowledged", "input_rejected"}:
                seq = integer(row.get("seq"), label + ".seq")
                if seq not in self.client_sent:
                    raise TraceError(f"{label} lacks its identity-bound input_sent record")
                if seq in terminal_seqs:
                    raise TraceError(f"{label} duplicates a client acknowledgement/rejection")
                terminal_seqs.add(seq)
                if event == "input_acknowledged":
                    if seq not in self.commands or "status" not in self.commands[seq]:
                        raise TraceError(f"{label} acknowledgement lacks its driver snapshot")
                    if row.get("screenshot") != self.commands[seq]["status"].get("screenshot"):
                        raise TraceError(f"{label} acknowledgement refers to a different rendered snapshot")
                    elapsed = self.optional_number(row, "client_elapsed_ms", label, "client.client_elapsed_ms")
                    self.client_ack.append(dict(row, client_elapsed_ms=elapsed))
                else:
                    if not any(item["seq"] == seq for item in self.rejections):
                        raise TraceError(f"{label} rejection lacks its driver record")
                    self.errors.append({"source": "client_rejection", "seq": seq, "error": str(row.get("error", ""))})
            elif event == "client_timeout":
                if last_sent == 0:
                    raise TraceError(f"{label} timeout lacks any identity-bound input_sent record")
                self.client_timeouts.append({"utc_epoch": row["utc_epoch"], "error": str(row.get("error", "")),
                                             "most_recent_sent_seq": last_sent,
                                             "outstanding_sent_seq": last_sent if last_sent not in terminal_seqs else None})
            else:
                raise TraceError(f"{label} has unsupported protocol event {event!r}")


def duration_stats(values: list[float], expected_count: int | None = None) -> dict[str, Any]:
    expected_count = len(values) if expected_count is None else expected_count
    ordered = sorted(values)
    def quantile(fraction: float) -> float | None:
        return ordered[math.ceil((len(ordered) - 1) * fraction)] if ordered else None
    return {"count": expected_count, "sample_count": len(values), "missing_count": expected_count - len(values),
            "total_seconds": sum(values) if len(values) == expected_count else None,
            "observed_total_seconds": sum(values) if values or expected_count == 0 else None,
            "p50_seconds": quantile(.5),
            "p95_seconds": quantile(.95), "max_seconds": max(values, default=None)}


def complement(start: float, end: float, intervals: list[tuple[float, float]]) -> list[dict[str, float]]:
    cursor = start
    result = []
    for lower, upper in sorted(intervals):
        lower, upper = max(start, lower), min(end, upper)
        if upper <= lower:
            continue
        if lower > cursor:
            result.append({"start_process_seconds": cursor / 1e6, "end_process_seconds": lower / 1e6,
                           "seconds": (lower - cursor) / 1e6})
        cursor = max(cursor, upper)
    if cursor < end:
        result.append({"start_process_seconds": cursor / 1e6, "end_process_seconds": end / 1e6,
                       "seconds": (end - cursor) / 1e6})
    return result


def repeated_inputs(actions: list[dict[str, Any]]) -> dict[str, Any]:
    groups: dict[str, list[dict[str, int]]] = defaultdict(list)
    kinds: dict[str, str] = {}
    consecutive = 0
    previous = None
    for item in actions:
        digest = fingerprint(item["input"])
        kind = str(item["input"].get("type", "invalid")) if isinstance(item["input"], dict) else "invalid"
        groups[digest].append({"seq": item["seq"], "index": item["index"]})
        kinds[digest] = kind
        consecutive += int(previous == digest)
        previous = digest
    duplicates = [{"input_sha256": digest, "type": kinds[digest], "count": len(places), "occurrences": places}
                  for digest, places in groups.items() if len(places) > 1]
    return {"comparison": "Recorded input attempts, using exact canonical JSON objects; snapshot control IDs are not equated across snapshots. Repetition is not automatically an error.",
            "distinct_input_objects": len(groups), "repeated_input_objects": len(duplicates),
            "extra_occurrences": sum(item["count"] - 1 for item in duplicates),
            "consecutive_identical_pairs": consecutive, "groups": duplicates}


def summarize(evidence: SessionEvidence) -> dict[str, Any]:
    actions = [value for _, value in sorted(evidence.actions.items())]
    completed = [item for item in actions if "end_usec" in item]
    per_type: dict[str, list[float]] = defaultdict(list)
    per_type_counts: dict[str, int] = defaultdict(int)
    for item in actions:
        kind = str(item["input"].get("type", "invalid")) if isinstance(item["input"], dict) else "invalid"
        per_type_counts[kind] += 1
        if item.get("elapsed_ms") is not None:
            per_type[kind].append(item["elapsed_ms"] / 1000.0)
    command_rows = []
    command_intervals = []
    for seq, item in evidence.commands.items():
        row: dict[str, Any] = {"seq": seq, "input_count_requested": len(item["command"].get("actions", []))
                              if isinstance(item["command"].get("actions"), list) else None,
                              "completed": "status" in item, "start_process_seconds": item["start_usec"] / 1e6}
        if "status" in item:
            status = item["status"]
            command_actions = [action for action in actions if action["seq"] == seq]
            input_values = [action["elapsed_ms"] / 1000.0 for action in command_actions
                            if action.get("elapsed_ms") is not None]
            measured_input = sum(input_values) if len(input_values) == len(command_actions) else None
            service = status["elapsed_ms"] / 1000.0 if "elapsed_ms" in status else None
            capture = status["capture_elapsed_ms"] / 1000.0 if "capture_elapsed_ms" in status else None
            residual = service - measured_input - capture if all(value is not None for value in
                                                                 (service, measured_input, capture)) else None
            if residual is not None and residual < 0:
                evidence.sampling("derived.command_other_service_seconds", False, f"command:{seq}")
                residual = None
            frame_metrics = status.get("frame_metrics")
            row.update(end_process_seconds=item["end_usec"] / 1e6,
                       observed_driver_window_seconds=(item["end_usec"] - item["start_usec"]) / 1e6,
                       reported_service_seconds=service, measured_input_action_seconds=measured_input,
                       input_action_timing=duration_stats(input_values, len(command_actions)),
                       capture_seconds=capture, other_reported_service_seconds=residual,
                       timing_decomposition_complete=residual is not None,
                       frame_metrics=frame_metrics.get("command") if isinstance(frame_metrics, dict) else None,
                       error=status.get("error", ""))
            command_intervals.append((item["start_usec"], item["end_usec"]))
        else:
            command_intervals.append((item["start_usec"], evidence.timeline[-1]["elapsed_usec"]))
            row["observed_incomplete_driver_window_seconds"] = (evidence.timeline[-1]["elapsed_usec"] - item["start_usec"]) / 1e6
        command_rows.append(row)
    start = float(evidence.timeline[0]["elapsed_usec"])
    end = float(evidence.timeline[-1]["elapsed_usec"])
    initial = evidence.snapshots[0]
    initial_capture_ms = initial["status"].get("capture_elapsed_ms")
    attributed_intervals = list(command_intervals)
    if initial_capture_ms is not None:
        attributed_intervals.append((initial["elapsed_usec"] - initial_capture_ms * 1000.0, initial["elapsed_usec"]))
    gaps = complement(start, end, attributed_intervals)
    texts: set[str] = set()
    dialogue_bodies: set[str] = set()
    novelty = []
    text_sample_count = 0
    dialogue_sample_count = 0
    for snapshot in evidence.snapshots:
        status = snapshot["status"]
        label = f"snapshot:{status['snapshot']}"
        visible = status.get("visible_text")
        shown: set[str] | None = None
        novel: set[str] | None = None
        if visible is not None:
            shown = set()
            for index, display in enumerate(visible):
                display = object_value(display, f"{label}.visible_text:{index}")
                if not isinstance(display.get("text"), str):
                    raise TraceError(f"{label}.visible_text:{index} lacks recorded string text")
                words = display["text"].replace("\r\n", "\n").replace("\r", "\n").strip()
                if words:
                    shown.add(words)
            text_sample_count += 1
            novel = shown - texts
            texts.update(shown)
        novelty.append({"snapshot": status["snapshot"], "seq": status["ack_seq"],
                        "visible_distinct_texts": len(shown) if shown is not None else None,
                        "new_distinct_texts": len(novel) if novel is not None else None})
        dialogue = status.get("dialogue")
        has_body = isinstance(dialogue, dict) and "body" in dialogue
        evidence.sampling("snapshot.dialogue.body", has_body, label + ".dialogue.body")
        if has_body:
            body = dialogue["body"]
            if not isinstance(body, str):
                raise TraceError(f"{label}.dialogue.body must be a string when present")
            dialogue_sample_count += 1
            if body.strip():
                dialogue_bodies.add(body.replace("\r\n", "\n").replace("\r", "\n").strip())
    latest = evidence.snapshots[-1]["status"]
    movement = latest.get("movement")
    subjective = {}
    for filename in ("report.md", "evaluation.json"):
        path = local_file(evidence.directory, filename)
        if path.is_file():
            subjective[filename] = str(path)
    version = {key: evidence.metadata[key] for key in ("protocol", "engine_version", "runtime", "source_sha256")}
    completed_command_count = sum(int(item["completed"]) for item in command_rows)
    service_values = [item["reported_service_seconds"] for item in command_rows
                      if item["completed"] and item["reported_service_seconds"] is not None]
    capture_values = [item["capture_seconds"] for item in command_rows
                      if item["completed"] and item["capture_seconds"] is not None]
    action_values = [item["elapsed_ms"] / 1000.0 for item in completed if item.get("elapsed_ms") is not None]
    client_values = [row["client_elapsed_ms"] / 1000.0 for row in evidence.client_ack if row["client_elapsed_ms"] is not None]
    latest_metrics = latest.get("frame_metrics")
    return {
        "schema_version": SCHEMA_VERSION, "tool_version": TOOL_VERSION,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "test_method": "blind_ai_agent_playtest", "is_human_playtest": False,
        "identity": {"agent": evidence.agent, "session": evidence.session, "run_id": evidence.run_id,
                     "basis": "Recorded --agent metadata; this tool does not authenticate independence or prior knowledge."},
        "fingerprints": {"metadata_sha256": fingerprint(evidence.metadata), "version_metadata_sha256": fingerprint(version),
                         "version_metadata": version, "input_files": evidence.files},
        "sampling_coverage": evidence.sampling_coverage,
        "coverage_warnings": [value for _, value in sorted(evidence.coverage_warnings.items())],
        "status_reconciliation": evidence.status_reconciliation,
        "process_time": {"latest_observed_process_clock_seconds": end / 1e6,
                         "first_session_event_process_seconds": start / 1e6,
                         "recorded_session_span_seconds": (end - start) / 1e6,
                         "started_utc": evidence.metadata.get("started_utc"),
                         "scope": "Godot monotonic ticks since engine startup, through the latest logged driver event; process exit is not instrumented."},
        "action_time": {"measured_input_actions": duration_stats(action_values, len(actions)),
                        "by_type": {kind: duration_stats(per_type[kind], count) for kind, count in sorted(per_type_counts.items())},
                        "incomplete_inputs": [{"seq": item["seq"], "index": item["index"]} for item in actions if "end_usec" not in item],
                        "finished_inputs_without_duration": [{"seq": item["seq"], "index": item["index"]}
                                                             for item in completed if item.get("elapsed_ms") is None],
                        "animation_seconds": None,
                        "animation_scope": "No separate animation timing exists in protocol 1. Hold/wait/input durations can include animation and frame waits; no gap is assumed to be animation."},
        "commands": {"started_count": len(command_rows), "completed_count": completed_command_count,
                     "service_time": duration_stats(service_values, completed_command_count),
                     "capture_time": duration_stats(capture_values, completed_command_count), "items": command_rows,
                     "client_acknowledged_count": len(evidence.client_ack),
                     "client_round_trip_time": duration_stats(client_values, len(evidence.client_ack)),
                     "client_inputs_not_observed_in_driver": sorted(set(evidence.client_sent) - set(evidence.commands)),
                     "timing_note": "Driver windows, service, capture, input, and client round trips overlap. Missing sampling or negative residuals leave decomposition null; window duration is not effective human playtime."},
        "unattributed": {"seconds": sum(item["seconds"] for item in gaps), "intervals": gaps,
                         "initial_capture_seconds": initial_capture_ms / 1000.0 if initial_capture_ms is not None else None,
                         "long_interval_threshold_seconds": LONG_GAP_SECONDS,
                         "long_intervals": [item for item in gaps if item["seconds"] >= LONG_GAP_SECONDS],
                         "meaning": "Outside observed driver command windows and known initial capture duration. Missing initial capture remains unattributed. May include tools, Agent thought, polling, startup, unmeasured capture, idle runtime, or animation; cause is unknown and this is not human playtime."},
        "visible_text": {"distinct_exact_strings": len(texts) if text_sample_count == len(evidence.snapshots) else None,
                         "observed_distinct_exact_strings": len(texts) if text_sample_count else None,
                         "distinct_dialogue_body_strings": len(dialogue_bodies) if dialogue_sample_count == len(evidence.snapshots) else None,
                         "observed_distinct_dialogue_body_strings": len(dialogue_bodies) if dialogue_sample_count else None,
                         "snapshot_count": len(evidence.snapshots), "text_sample_count": text_sample_count,
                         "dialogue_body_sample_count": dialogue_sample_count,
                         "visible_text_set_sha256": fingerprint(sorted(texts)) if text_sample_count else None, "per_snapshot": novelty,
                         "scope": "Only recorded visible_text; exact strings after newline normalization and edge trimming. Partial typewriter text counts separately; screenshots/custom drawing are not OCRed."},
        "presentation": {"headless_snapshot_count": sum(item["status"].get("headless") is True for item in evidence.snapshots),
                         "image_snapshot_count": sum(bool(item["status"].get("screenshot")) for item in evidence.snapshots),
                         "scope": "Headless snapshots contain no rendered pixels; texture previews, custom drawing, and style-only selection feedback cannot be visually evaluated."},
        "frame_metrics": {"latest_session": latest_metrics.get("session") if isinstance(latest_metrics, dict) else None,
                          "latest_gameplay": latest_metrics.get("gameplay") if isinstance(latest_metrics, dict) else None,
                          "latest_capture": latest_metrics.get("capture") if isinstance(latest_metrics, dict) else None,
                          "driver_scope": latest_metrics.get("scope") if isinstance(latest_metrics, dict) else None,
                          "scope": "Session totals and rolling-sample quantiles are preserved from the driver; per-command quantiles are listed, never averaged into session quantiles."},
        "diagnostics": {"driver_input_errors": [row for row in evidence.errors if row["source"] == "driver_input"],
                        "driver_command_errors": [row for row in evidence.errors if row["source"] == "driver_command"],
                        "client_rejections": [row for row in evidence.errors if row["source"] == "client_rejection"],
                        "command_rejections": evidence.rejections, "client_timeouts": evidence.client_timeouts,
                        "movement_stall_events": evidence.stalls, "latest_movement_counters": movement,
                        "movement_note": "Driver stall episodes measure requested movement under 0.25px per sample for at least 0.5s; they do not prove a softlock.",
                        "pending": latest.get("pending"),
                        "pending_scope": "Latest reliable rendered snapshot; unfinished command sequences are separately recorded in status_reconciliation."},
        "repeated_inputs": repeated_inputs(actions),
        "subjective_evaluation": {"existing_paths": subjective, "read_or_rewritten": False,
                                  "note": "Gameplay/narrative/reasoning judgments belong to the tester's report; no automatic grade or target-duration estimate is produced."},
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--session", required=True, help="existing fresh session id; never merges sessions")
    arguments = parser.parse_args()
    temporary: Path | None = None
    try:
        evidence = SessionEvidence(arguments.session)
        summary = summarize(evidence)
        output = local_file(evidence.directory, "summary.json")
        temporary = local_file(evidence.directory, f"summary-{uuid.uuid4().hex}.tmp")
        with temporary.open("x", encoding="utf-8") as stream:
            json.dump(summary, stream, ensure_ascii=False, indent=2, allow_nan=False)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, output)
        temporary = None
        print(str(output))
        return 0
    except (OSError, ValueError, TypeError, KeyError) as error:
        print(f"summarize_blind_playtest: {error}", file=sys.stderr)
        return 2
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


if __name__ == "__main__":
    raise SystemExit(main())
