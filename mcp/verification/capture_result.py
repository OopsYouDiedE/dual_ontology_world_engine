"""Shared completion contract for rendered runtime captures and profiled captures."""
from __future__ import annotations

import shutil
import subprocess
import time
from pathlib import Path

from binary_evidence import records


def wait_owned(process: subprocess.Popen, deadline: float, grace: float = 5.0):
    """Bound one process owned by this invocation, preserving timeout as failure."""
    try:
        return process.wait(timeout=max(0.0, deadline - time.monotonic())), False
    except subprocess.TimeoutExpired:
        process.terminate()
        try:
            return process.wait(timeout=grace), True
        except subprocess.TimeoutExpired:
            process.kill()
            return process.wait(timeout=grace), True


def wait_owned_many(owned):
    """Supervise runtime and collector deadlines together."""
    pending = dict(owned)
    results = {}
    while pending:
        now = time.monotonic()
        for name, (process, deadline) in list(pending.items()):
            code = process.poll()
            if code is not None:
                results[name] = (code, False)
                del pending[name]
            elif now >= deadline:
                results[name] = wait_owned(process, deadline)
                del pending[name]
        if pending:
            time.sleep(0.1)
    return results


def read_journal(path: Path, errors: list[str]):
    if not path.is_file():
        return []
    try:
        values = list(records(path))
        if any(not isinstance(value, dict) for value in values):
            raise ValueError("Expected record dictionaries")
        return values
    except (OSError, ValueError) as error:
        errors.append(f"{path.name}: {error}")
        return []


def collect_capture_result(*, out: Path, session_root: Path, existing: set[str],
                           before: dict, after: dict, seconds: float,
                           exit_code: int, timed_out: bool, checkpoints=(),
                           runtime_logs=("stdout.log", "stderr.log", "godot-native.log")):
    artifact_errors: list[str] = []
    sessions = sorted((path for path in session_root.iterdir() if path.is_dir() and path.name not in existing),
                      key=lambda path: path.name) if session_root.is_dir() else []
    close_rows = []
    for index, directory in enumerate(sessions):
        target = out / ("host-session" if len(sessions) == 1 else f"host-session-{index}")
        try:
            shutil.copytree(directory, target)
        except OSError as error:
            artifact_errors.append(f"session copy {directory.name}: {error}")
        close_rows.extend(read_journal(target / "close-result.dvlog", artifact_errors))

    run = out / "run"
    ends = read_journal(run / "end.dvlog", artifact_errors)
    closes = read_journal(run / "capture-close.dvlog", artifact_errors)
    checkpoint_rows = read_journal(run / "checkpoint-summary.dvlog", artifact_errors)
    host_rows = read_journal(run / "host-events.dvlog", artifact_errors)
    end = ends[0] if len(ends) == 1 else {}
    capture_close = closes[0] if len(closes) == 1 else {}
    snapshot = end.get("snapshot", {})
    text = "\n".join((out / name).read_text(encoding="utf-8", errors="replace")
                     for name in runtime_logs if (out / name).is_file())
    errors = list(dict.fromkeys(line for line in text.splitlines()
                               if line.lstrip().lower().startswith(("error:", "script error:", "unhandled exception", "[error]"))))
    warnings = list(dict.fromkeys(line for line in text.splitlines() if line.lstrip().startswith("WARNING:")))
    changes = sorted(key for key in before.keys() | after.keys() if before.get(key) != after.get(key))
    load_errors = [event.get("message", "") for row in host_rows for event in row.get("events", [])
                   if event.get("kind") == "load_error"]
    unique_session = len(sessions) == 1
    close_complete = (unique_session and len(close_rows) == 1
                      and close_rows[0].get("complete") is True
                      and close_rows[0].get("session_opened") is True)
    capture_close_complete = (len(closes) == 1 and capture_close.get("complete") is True
                              and capture_close.get("host_closed") is True
                              and capture_close.get("archive_idle") is True
                              and capture_close.get("held_inputs_released") is True
                              and not capture_close.get("archive_errors")
                              and not capture_close.get("timeout"))
    duration_complete = len(ends) == 1 and end.get("real_ms", -1) >= seconds * 1000.0 - 50.0
    checkpoints_complete = not checkpoints
    if checkpoint_rows:
        checkpoint = checkpoint_rows[0]
        checkpoints_complete = (len(checkpoint_rows) == 1 and checkpoint.get("saved", 0) >= len(checkpoints)
                                and not checkpoint.get("copy_failed") and not checkpoint.get("inflight"))
    result = {
        "exit_code": exit_code, "timeout": timed_out,
        "host_sessions": [str(path) for path in sessions], "unique_session": unique_session,
        "close_complete": close_complete, "capture_close_complete": capture_close_complete,
        "capture_duration_complete": duration_complete, "checkpoints_complete": checkpoints_complete,
        "source_changes": changes, "errors": errors, "warnings": warnings,
        "load_errors": load_errors, "artifact_errors": artifact_errors,
        "end_record_count": len(ends), "capture_close_record_count": len(closes),
        "capture_end_real_ms": end.get("real_ms"), "game_time": snapshot.get("game_time"),
        "location": snapshot.get("logical_location"), "chapter": snapshot.get("chapter"),
        "pending_status": snapshot.get("pending", {}).get("status"),
    }
    if checkpoint_rows:
        result["checkpoints"] = checkpoint_rows[0]
    result["capture_success"] = (exit_code == 0 and not timed_out and unique_session and close_complete
                                  and capture_close_complete and duration_complete and checkpoints_complete
                                  and not changes and not errors and not load_errors and not artifact_errors)
    return result
