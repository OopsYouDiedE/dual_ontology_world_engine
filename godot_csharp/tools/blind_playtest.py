#!/usr/bin/env python3
"""Generic, human-directed client for a fresh input session of the single BasicUISystem game window.

Start the rendered Godot driver separately with:
  --script res://scenes/Awakening/check/playtest_session.gd -- \
  --session=<fresh-id> --agent=<canonical-agent-name>

Use status to read the last acknowledged screenshot, snapshot to request a
fresh screenshot, or action to send one JSON input object / a list of objects.
There are no story commands, suggested choices, or automatic progress loops.
Direct writers may atomically publish command.json with session, run_id,
seq=ack_seq+1, name, and actions, then wait for that exact acknowledgement.

Generic input fields (coordinates are viewport pixels):
  {"type":"click", "x":400, "y":300, "button":"left"}
  {"type":"tap", "key":"Space"}  # Enter, Escape and other key names also work
  {"type":"hold", "key":"D", "ms":800}   # duration: "ms", "seconds" or "frames"
  {"type":"wait", "seconds":2}           # same duration fields; unknown fields are rejected
  {"type":"wheel", "x":400, "y":300, "direction":"down", "steps":1}
  {"type":"click_control", "control_id":"ID from the current snapshot"}
  {"type":"text_control", "control_id":"visible text field ID",
   "text":"typed text", "replace":true}
  {"type":"text", "text":"typed text"}  # requires an already focused field
Control IDs refer to current displayed nodes. Use one control action and inspect
its returned snapshot before choosing another when the UI rebuilds. Space or
Enter reveals the current dialogue text, then advances on the next press; the
visible Continue button is an equivalent mouse control. These are UI protocols,
not story instructions. Character movement policy is set by the test request.
"""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import sys
import time
import uuid
from typing import Any


DEFAULT_ARTIFACTS = Path(__file__).resolve().parents[2] / "artifacts" / "remake-belief-replay"
INPUT_TYPES = {"tap", "hold", "text", "click", "wheel", "wait", "click_control", "text_control", "snapshot"}


class BlindSession:
    """A single outstanding command at a time, confirmed by session + run + seq."""

    def __init__(self, session: str, artifacts: Path | str = DEFAULT_ARTIFACTS,
                 timeout: float = 180.0, poll: float = 0.05) -> None:
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,63}", session):
            raise ValueError("session must be 1-64 letters/digits/_/- and start with a letter or digit")
        if timeout <= 0 or poll <= 0:
            raise ValueError("timeout and poll must be positive")
        self.session = session
        self.directory = Path(artifacts).resolve() / f"blind-{session}"
        self.timeout = timeout
        self.poll = poll

    @staticmethod
    def _read(path: Path) -> dict[str, Any] | None:
        try:
            value = json.loads(path.read_text(encoding="utf-8-sig"))
        except (OSError, ValueError):
            return None
        return value if isinstance(value, dict) else None

    def _sleep(self, deadline: float, message: str) -> None:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise TimeoutError(message)
        time.sleep(min(self.poll, remaining))

    def _status(self, deadline: float) -> dict[str, Any]:
        while True:
            metadata = self._read(self.directory / "metadata.json")
            status = self._read(self.directory / "status.json")
            # Windows readers can briefly prevent replacement of status.json.
            # A flushed, identity-bound snapshot is also an exact acknowledgement;
            # never infer completion from an input-started or partial record.
            snapshot = self._latest_snapshot()
            if snapshot and (not status or int(snapshot.get("ack_seq", -1)) > int(status.get("ack_seq", -1))):
                status = snapshot
            if metadata and status:
                if metadata.get("session") != self.session or status.get("session") != self.session:
                    raise RuntimeError("session identity mismatch")
                if not metadata.get("run_id") or status.get("run_id") != metadata["run_id"]:
                    raise RuntimeError("run identity mismatch; historical status cannot be used")
                return status
            self._sleep(deadline, "session did not publish its initial status before the timeout")

    def _latest_snapshot(self) -> dict[str, Any] | None:
        try:
            with (self.directory / "timeline.jsonl").open("rb") as stream:
                stream.seek(0, os.SEEK_END)
                size = stream.tell()
                stream.seek(max(0, size - 2 * 1024 * 1024))
                lines = stream.read().splitlines()
        except OSError:
            return None
        for line in reversed(lines):
            try:
                row = json.loads(line)
            except (ValueError, UnicodeError):
                continue
            value = row.get("status") if isinstance(row, dict) and row.get("event") == "snapshot" else None
            if isinstance(value, dict) and value.get("session") == self.session and value.get("accepted") is True:
                return value
        return None

    def status(self) -> dict[str, Any]:
        """Read the last published status without injecting input."""
        return self._status(time.monotonic() + self.timeout)

    def _settled(self, deadline: float) -> dict[str, Any]:
        while True:
            status = self._status(deadline)
            queued = self._read(self.directory / "command.json")
            outstanding = False
            if queued:
                if queued.get("session") != self.session or queued.get("run_id") != status["run_id"]:
                    raise RuntimeError("command.json belongs to another run; it will not be replayed")
                outstanding = int(queued.get("seq", -1)) > int(status.get("ack_seq", 0))
                if status.get("accepted") is False and status.get("command_seq") == queued.get("seq"):
                    outstanding = False
            if not outstanding and not status.get("pending", {}).get("command_running", False):
                return status
            self._sleep(deadline, "previous input is still pending; no new input was written")

    def _trace(self, event: dict[str, Any]) -> None:
        event["utc_epoch"] = time.time()
        with (self.directory / "client.jsonl").open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(event, ensure_ascii=False) + "\n")
            stream.flush()

    def submit(self, actions: list[dict[str, Any]], name: str = "input") -> dict[str, Any]:
        if not isinstance(actions, list) or len(actions) > 256:
            raise ValueError("actions must be a list containing at most 256 input objects")
        for action in actions:
            if not isinstance(action, dict) or action.get("type") not in INPUT_TYPES:
                raise ValueError("unknown input type; use tap/hold/text/click/wheel/wait/click_control/text_control/snapshot")
        deadline = time.monotonic() + self.timeout
        self._status(deadline)
        lock_path = self.directory / "client.lock"
        locked = False
        while not locked:
            try:
                descriptor = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
            except FileExistsError:
                self._sleep(deadline, "another client holds client.lock; no new input was written")
            else:
                with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
                    stream.write(str(os.getpid()))
                locked = True
        temporary: Path | None = None
        try:
            status = self._settled(deadline)
            known_controls = {item["control_id"] for item in status.get("controls", []) if "control_id" in item}
            for action in actions:
                if action["type"] in {"click_control", "text_control"} and action.get("control_id") not in known_controls:
                    raise ValueError("control_id must come from this run's latest displayed snapshot")
            seq = int(status.get("ack_seq", 0)) + 1
            command = {"session": self.session, "run_id": status["run_id"], "seq": seq,
                       "name": name, "actions": actions}
            temporary = self.directory / f"command-{uuid.uuid4().hex}.tmp"
            with temporary.open("x", encoding="utf-8") as stream:
                json.dump(command, stream, ensure_ascii=False)
                stream.flush()
                os.fsync(stream.fileno())
            sent = time.monotonic()
            publish_retries = 0
            while True:
                try:
                    os.replace(temporary, self.directory / "command.json")
                except PermissionError:
                    # Godot/Windows may hold the previous file briefly. Retry
                    # publishing this same payload, never issue another seq or
                    # rewrite a partially readable command in place.
                    publish_retries += 1
                    self._sleep(deadline, f"input {seq} could not be atomically published; no replacement was confirmed")
                else:
                    break
            temporary = None
            self._trace({"event": "input_sent", "command": command, "publish_retries": publish_retries})
            while True:
                result = self._status(deadline)
                if result["run_id"] != command["run_id"]:
                    raise RuntimeError("run changed while input was pending")
                if result.get("command_seq") == seq and result.get("accepted") is False:
                    self._trace({"event": "input_rejected", "seq": seq, "error": result.get("error")})
                    raise RuntimeError(str(result.get("error", "input rejected")))
                ack = int(result.get("ack_seq", 0))
                if ack == seq and not result.get("pending", {}).get("command_running", False):
                    result["client_elapsed_ms"] = (time.monotonic() - sent) * 1000.0
                    self._trace({"event": "input_acknowledged", "seq": seq, "client_elapsed_ms": result["client_elapsed_ms"],
                                 "error": result.get("error", ""), "screenshot": result.get("screenshot")})
                    return result
                if ack > seq:
                    raise RuntimeError("a concurrent direct writer advanced the session past this input")
                self._sleep(deadline, f"input {seq} was not acknowledged; inspect status.json before retrying")
        except TimeoutError as error:
            self._trace({"event": "client_timeout", "error": str(error)})
            raise
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
            lock_path.unlink(missing_ok=True)

    def action(self, action: dict[str, Any], name: str = "input") -> dict[str, Any]:
        return self.submit([action], name)

    def snapshot(self) -> dict[str, Any]:
        return self.submit([], "snapshot")


def main() -> int:
    # Game text includes symbols outside legacy Windows code pages; never fail on printing.
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--session", required=True, help="the fresh id passed to this Godot process")
    parser.add_argument("--artifacts", type=Path, default=DEFAULT_ARTIFACTS)
    parser.add_argument("--timeout", type=float, default=180.0, help="seconds to wait for the matching acknowledgement")
    parser.add_argument("--poll", type=float, default=0.05)
    commands = parser.add_subparsers(dest="operation", required=True)
    commands.add_parser("status", help="read the last published status without input")
    commands.add_parser("snapshot", help="request a fresh screenshot and visible UI inventory")
    action_parser = commands.add_parser("action", help="send generic input JSON and await its sequence")
    source = action_parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--json", help="one input object, a list, or an object with actions")
    source.add_argument("--file", type=Path, help="UTF-8 JSON input file; - reads standard input")
    action_parser.add_argument("--name", default="input", help="human-readable trace label")
    arguments = parser.parse_args()
    try:
        client = BlindSession(arguments.session, arguments.artifacts, arguments.timeout, arguments.poll)
        if arguments.operation == "status":
            result = client.status()
        elif arguments.operation == "snapshot":
            result = client.snapshot()
        else:
            if arguments.json is not None:
                source_text = arguments.json
            elif str(arguments.file) == "-":
                source_text = sys.stdin.read()
            else:
                source_text = arguments.file.read_text(encoding="utf-8-sig")
            value = json.loads(source_text)
            actions = value.get("actions") if isinstance(value, dict) and "actions" in value else value
            if isinstance(actions, dict):
                actions = [actions]
            result = client.submit(actions, arguments.name)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 1 if result.get("error") else 0
    except (OSError, ValueError, TypeError, TimeoutError, RuntimeError) as error:
        print(f"blind_playtest: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
