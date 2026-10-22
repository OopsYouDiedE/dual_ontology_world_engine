"""Bounded CLI protocol checks; each live capture completes one actual execution."""
from pathlib import Path
import argparse
import json
import os
import queue
import signal
import subprocess
import sys
import threading
import time


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def stop_owned_tree(process):
    if process.poll() is not None:
        return
    if os.name == "nt":
        subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=8)
    else:
        parents = {}
        for entry in Path("/proc").glob("[0-9]*/stat"):
            try:
                parents[int(entry.parent.name)] = int(entry.read_text().rsplit(")", 1)[1].split()[1])
            except (OSError, ValueError, IndexError):
                continue
        owned = [process.pid]
        for parent in owned:
            owned.extend(pid for pid, ppid in parents.items() if ppid == parent and pid not in owned)
        for pid in reversed(owned):
            try:
                os.kill(pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
    process.wait(timeout=5)


class Capture:
    def __init__(self, args, directory, controller, replay=None):
        self.directory = directory
        self.deadline = time.monotonic() + args.timeout
        command = [sys.executable, str(Path(__file__).with_name("capture_cli.py")),
                   "--out", str(directory), "--cli", str(args.cli), "--format", "jsonl",
                   "--controller", controller, "--timeout", str(args.timeout)]
        if replay is not None:
            command += ["--replay", str(replay)]
        flags = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == "nt" else {"start_new_session": True}
        self.process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                        stderr=subprocess.DEVNULL, text=True, encoding="utf-8", **flags)
        self.messages = queue.Queue(maxsize=128)
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        try:
            for line in self.process.stdout:
                if not line.strip():
                    continue
                try:
                    self.messages.put(json.loads(line))
                except json.JSONDecodeError:
                    self.messages.put({"kind": "error", "message": "CLI emitted a non-JSON protocol line"})
        finally:
            self.messages.put(None)

    def request(self, command, kinds):
        label = command["command"] if isinstance(command, dict) else command.split()[0]
        line = json.dumps(command, ensure_ascii=False) if isinstance(command, dict) else command
        self.process.stdin.write(line + "\n")
        self.process.stdin.flush()
        return self.receive(kinds, label=label)

    def receive(self, kinds, timeout=25, label="autoplay"):
        deadline = min(self.deadline, time.monotonic() + min(timeout, 25))
        received = []
        for _ in range(512):
            remaining = deadline - time.monotonic()
            require(remaining > 0, "Command deadline: " + label)
            try:
                value = self.messages.get(timeout=remaining)
            except queue.Empty as error:
                raise TimeoutError("Command deadline: " + label) from error
            require(value is not None, "CLI exited before response: " + label)
            require(value.get("kind") != "error", str(value.get("message", "CLI error")))
            received.append(value)
            if value.get("kind") in kinds:
                return value, received
        raise RuntimeError("Response frame bound exceeded")

    def data(self, command, kind):
        return self.request(command, (kind,))[0]["data"]

    def close(self):
        failing = sys.exc_info()[0] is not None
        if self.process.poll() is None:
            try:
                self.process.stdin.write("quit\n")
                self.process.stdin.flush()
                self.process.stdin.close()
                self.process.wait(timeout=max(0.1, min(12, self.deadline - time.monotonic())))
            except (BrokenPipeError, OSError, subprocess.TimeoutExpired):
                stop_owned_tree(self.process)
        if not failing:
            require(self.process.returncode == 0, "Capture exit: " + str(self.process.returncode))


def subjective_only(value):
    require("objective" not in value and "observers" not in value, "Subjective view exposed another scope")


def inspect_views(capture, status, replay=False):
    key = "index" if replay else "version"
    before, viewer = status[key], status["viewer"]
    both = capture.data("map both", "map")
    require(bool(both.get("objective", {}).get("rooms")), "Objective room inventory is absent")
    require(both.get("viewer") == viewer and both.get("snapshot", {}).get("viewer") == viewer,
            "Map preview does not belong to the current viewer")
    actors = capture.data("views", "views")
    require(bool(actors), "Actor inventory is absent")
    other = next((item["id"] for item in actors if item["id"] != viewer), None)
    require(other is not None, "No alternate recorded perspective is available")
    switched = capture.data("view " + other, "status")
    require(switched["viewer"] == other and switched[key] == before, "View switch changed state or kept the old viewer")
    if replay:
        require(switched.get("snapshot", {}).get("viewer") == other, "Replay status kept the old observer snapshot")
    subjective = capture.data("map subjective", "map")
    subjective_only(subjective)
    require(subjective.get("viewer") == other and subjective.get("snapshot", {}).get("viewer") == other,
            "Subjective map belongs to another observer")
    followed = capture.data("view follow", "status")
    require(followed["viewer"] == viewer and followed[key] == before, "Follow did not restore the action perspective")
    after = capture.data("status", "status")
    require(after[key] == before, "Read-only query advanced the session or replay")


def check_focus(capture, focused, options, before_version=None):
    require(focused.get("phase") == "focus", "Preparation did not produce a focus beat")
    require(focused["options"] == options, "Focus changed displayed alternatives or scores")
    require(any(item["id"] == focused["action_id"] for item in options), "Selected action is absent from the alternatives")
    require(bool(options) and all(any(item.get(key) is not None for key in ("ai_score", "score")) for item in options),
            "Displayed alternatives are missing AI scores")
    status = capture.data("status", "status")
    require(status["version"] == focused["state_version"], "Focus did not preserve its state version")
    require(status["viewer"] == focused["actor"], "Focus did not follow the acting character")
    if before_version is not None:
        require(focused["state_version"] == before_version, "Focus advanced the session")


def confirm(capture, focused, command):
    final, messages = capture.request(command, ("status",))
    confirms = [item["data"] for item in messages if item["kind"] == "interaction"]
    require(len(confirms) == 1 and confirms[0].get("phase") == "confirm" and
            all(confirms[0].get(key) == focused.get(key) for key in
                ("interaction_id", "actor", "action_id", "selection", "state_version", "options")),
            "Confirmation changed the focused interaction")
    scheduled = [item["data"] for item in messages if item["kind"] == "scheduled"]
    presentations = [item for item in messages if item["kind"] == "presentation"]
    if scheduled:
        require(len(scheduled) == 1 and not presentations, "Queue confirmation also executed an action")
        require(all(scheduled[0].get(key) == focused.get(key) for key in ("interaction_id", "actor", "action_id")) and
                scheduled[0]["state_version"] == final["data"]["version"], "Scheduled receipt does not match the selection")
    else:
        require(len(presentations) == 1, "The focused action did not reach Complete")
        result = presentations[0]["data"]
        require(result.get("completed_actor") == focused["actor"] and
                result.get("completed_action") == focused["action_id"], "Complete executed a different focused action")
    require(final["data"]["version"] > focused["state_version"], "Confirmation did not advance the version")
    return final["data"], bool(scheduled)


def live(args, controller):
    capture = Capture(args, args.out / controller, controller)
    started = time.monotonic()
    try:
        initial = capture.data("status", "status")
        inspect_views(capture, initial)
        boundary, _ = capture.request("next", ("intent", "interaction"))
        cancelled = None
        if controller == "player":
            require(boundary["kind"] == "intent" and boundary["data"].get("awaiting_input"),
                    "First prepare did not reach the human input boundary; no extra action was executed")
            before_focus = capture.data("status", "status")["version"]
            options = capture.data("options", "options")
            require(bool(options), "No displayed action is available")
            cancelled = capture.data("focus 01", "interaction")
            check_focus(capture, cancelled, options, before_focus)
            cancelled_status = capture.data("cancel", "status")
            require(cancelled_status["version"] == before_focus, "Cancel changed the session")
            focused = capture.data({"command": "focus", "argument": {"action_id": options[0]["id"]}}, "interaction")
            require(focused["action_id"] == options[0]["id"], "Focus differs from the displayed first action")
            require(focused["interaction_id"] != cancelled["interaction_id"], "New focus reused the cancelled interaction")
            check_focus(capture, focused, options, before_focus)
        else:
            require(boundary["kind"] == "interaction" and boundary["data"].get("phase") == "focus",
                    "AI prepare did not produce a focus beat")
            focused = boundary["data"]
            options = capture.data("options", "options")
            check_focus(capture, focused, options)
        final, scheduled = confirm(capture, focused, "next" if controller == "ai" else {"command": "confirm"})
        operations = [{"focus": focused, "outcome": "scheduled" if scheduled else "executed", "version": final["version"]}]
        executed = focused
        if scheduled:
            require(controller == "player", "An already prepared AI focus was unexpectedly requeued")
            boundary, _ = capture.request("next", ("interaction", "intent"))
            require(boundary["kind"] == "interaction", "No due action was prepared; the bounded check will not chase the schedule")
            executed = boundary["data"]
            require(executed["interaction_id"] not in (focused["interaction_id"], cancelled["interaction_id"]),
                    "Due action reused an earlier interaction")
            check_focus(capture, executed, capture.data("options", "options"),
                        final["version"] if final.get("status") == "prepared" else None)
            final, rescheduled = confirm(capture, executed, "next")
            require(not rescheduled, "Due action was requeued instead of executed")
            operations.append({"focus": executed, "outcome": "executed", "version": final["version"]})
    finally:
        capture.close()
    trace = capture.directory / "session.dvdag"
    require(trace.is_file() and (capture.directory / "session.dvsave").is_file(), "Session artifacts are incomplete")
    summary = {"controller": controller, "submitted_actor": focused["actor"], "submitted_action_id": focused["action_id"],
               "actor": executed["actor"], "action_id": executed["action_id"], "scheduled": scheduled,
               "executed_actions": 1, "json_command_envelopes": controller == "player", "candidate_count": len(options),
               "scored_count": sum(any(item.get(key) is not None for key in ("ai_score", "score")) for item in options),
               "initial_version": initial["version"], "completed_version": final["version"],
               "seconds": round(time.monotonic() - started, 3)}
    return trace, operations, cancelled, summary


def replay(args, controller, trace, operations, cancelled):
    capture = Capture(args, args.out / (controller + "-replay"), controller, trace)
    phases = {}
    interaction_frames = []
    expected = {operation["focus"]["interaction_id"]: operation for operation in operations}
    if cancelled:
        expected[cancelled["interaction_id"]] = {"focus": cancelled, "outcome": "cancelled"}
    try:
        status = capture.data("status", "status")
        require(0 < status["count"] <= 512, "Replay frame bound is outside 1..512")
        require(capture.data("play 8", "replay")["playing"], "Play did not start the replay clock")
        automatic, _ = capture.receive(("frame",), timeout=3)
        require(automatic.get("scope") == "recorded-replay" and automatic["data"]["Sequence"] > status["index"],
                "Automatic playback did not advance")
        paused = capture.data("pause", "replay")
        require(not paused["playing"], "Pause did not stop the replay clock")
        paused_status, paused_messages = capture.request("status", ("status",))
        require(paused_status["data"]["index"] == paused["index"] and
                not any(item["kind"] == "frame" for item in paused_messages), "Paused replay advanced")
        restarted = capture.data("restart", "replay")
        require(restarted["index"] == -1 and not restarted["playing"], "Restart did not reset the paused replay")
        viewed = False
        confirmations = executions = 0
        for _ in range(status["count"]):
            envelope, _ = capture.request("step", ("frame",))
            require(envelope.get("scope") == "recorded-replay", "Replay entered a live session")
            frame = envelope["data"]
            kind, data = frame["Kind"], frame["Data"]
            subjective_only(data.get("preview", {}))
            if kind == "confirm":
                confirmations += 1
            identity = data.get("interaction_id")
            if kind in ("focus", "confirm", "cancel"):
                require(identity in expected, "Unexpected replay interaction")
                interaction_frames.append({"index": frame["Sequence"], "kind": kind, "interaction_id": identity,
                                           "actor": frame["Actor"], "action": frame["Action"],
                                           "outcome": data.get("outcome", "")})
                phases.setdefault(identity, []).append(kind)
                operation = expected[identity]
                focus = operation["focus"]
                require(data["options"] == focus["options"], "Replay changed alternatives or AI scores")
                require(frame["Actor"] == focus["actor"] and frame["Action"] == focus["action_id"],
                        "Replay changed the acting character or action")
                require(data.get("selected") == ("" if kind == "cancel" else focus["action_id"]),
                        "Replay selected highlight differs from the recorded choice")
                require(data.get("source_state_version") == focus["state_version"], "Replay changed the focus version")
                if kind == "focus":
                    require(data["state_version"] == focus["state_version"], "Replay focus contains a later state")
                if kind == "confirm":
                    require(data["state_version"] == operation["version"], "Replay confirmation changed its result version")
                    if operation["outcome"] == "scheduled":
                        require(data.get("outcome") == "scheduled" and not data.get("result_kind") and not frame["Text"],
                                "Queue confirmation absorbed a later execution")
                    else:
                        require(operation["outcome"] == "executed" and data.get("result_kind") and
                                data.get("outcome") != "scheduled", "Executed confirmation lost its recorded result")
                        executions += 1
            if kind == "focus" and not viewed:
                inspect_views(capture, capture.data("status", "status"), replay=True)
                viewed = True
        require(confirmations == len(operations) and executions == 1 and
                all(phases.get(operation["focus"]["interaction_id"]) == ["focus", "confirm"] for operation in operations),
                "Replay did not preserve each two-beat operation and exactly one actual execution")
        if cancelled:
            require(phases.get(cancelled["interaction_id"]) == ["focus", "cancel"], "Cancelled focus acquired a confirmation")
    finally:
        capture.close()
    require(not (capture.directory / "session.dvdag").exists() and not (capture.directory / "session.dvsave").exists(),
            "Read-only replay wrote a live session")
    return {"frame_count": status["count"], "operation_phases": [
                {"outcome": operation["outcome"], "phases": ["focus", "confirm"]} for operation in operations],
            "executed_actions": executions, "autoplay_advanced": True, "interaction_frames": interaction_frames,
            "cancel_phases": ["focus", "cancel"] if cancelled else [], "read_only": True}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--cli", type=Path, default=Path("dv_core/cli/bin/Release/net8.0/dv-cli.dll"))
    parser.add_argument("--timeout", type=float, default=90, help="Per capture window, at most 90 seconds")
    args = parser.parse_args()
    require(0 < args.timeout <= 90, "--timeout must be in (0, 90]")
    args.out = args.out.resolve()
    args.cli = args.cli.resolve()
    args.out.mkdir(parents=True, exist_ok=False)
    report = {"scope": "CLI protocol and recorded replay; one actual execution per controller", "runs": []}
    for controller in ("player", "ai"):
        try:
            trace, operations, cancelled, result = live(args, controller)
            result["replay"] = replay(args, controller, trace, operations, cancelled)
            result["status"] = "completed"
        except Exception as error:
            result = {"controller": controller, "status": "incomplete", "error": str(error)}
        report["runs"].append(result)
        (args.out / "summary.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps(result, ensure_ascii=False), flush=True)
    return int(any(run["status"] != "completed" for run in report["runs"]))


if __name__ == "__main__":
    raise SystemExit(main())
