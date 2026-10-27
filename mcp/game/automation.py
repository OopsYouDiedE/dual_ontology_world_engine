"""Deadline-bounded engine-AI runs owned entirely by one automatic MCP call."""
import base64
from datetime import datetime, timezone
from pathlib import Path
import json
import os
import secrets
import threading
import time

import runtime

RUNS = runtime.ROOT / ".runtime/runs"


def publish(pending, path):
    # Windows readers/virus scanners can briefly hold the destination without
    # delete sharing. Keep the last complete observation until replacement works.
    for attempt in range(11):
        try:
            pending.replace(path)
            return
        except PermissionError:
            if attempt == 10:
                raise
            time.sleep(0.02 * (attempt + 1))


def save(path, value):
    pending = path.with_suffix(path.suffix + ".tmp")
    pending.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    publish(pending, path)


def tool(backend, arguments):
    if "deadline" in arguments and "duration_seconds" in arguments:
        raise ValueError("Choose deadline OR duration_seconds")
    created = time.time()
    if arguments.get("deadline"):
        target = datetime.fromisoformat(arguments["deadline"].replace("Z", "+00:00"))
        if target.tzinfo is None:
            raise ValueError("deadline must include a timezone, for example +08:00 or Z")
        deadline = target.timestamp()
    else:
        deadline = created + arguments.get("duration_seconds", 60)
    if not 0 < deadline - created <= 86400:
        raise ValueError("Deadline must be in the next 24 hours")
    identity = backend + "-" + secrets.token_hex(12)
    directory = RUNS / identity
    directory.mkdir(parents=True)
    spec = {"run_id": identity, "backend": backend, "created_epoch": created,
            "deadline_epoch": deadline, "seed": arguments.get("seed", 1),
            "viewer": arguments.get("viewer", "detective")}
    if arguments.get("content_path"): spec["content_path"] = arguments["content_path"]
    save(directory / "request.json", spec)
    report = dict(spec, status="queued", deadline=datetime.fromtimestamp(deadline, timezone.utc).isoformat(),
                  report_path=str(directory / "report.json"), agent_required=False,
                  clock_policy="wall-clock-including-build-and-startup", observation=None,
                  observation_policy="last-successfully-received-before-stop", observations=0,
                  completed_actions=0 if backend == "cli" else None)
    if backend == "cli":
        report.update(advance_batch_steps=32, receipt_path=str(directory / "actions.jsonl"),
                      observation_policy="last-successfully-received-batch-observation-before-stop")
    save(directory / "report.json", report)
    # One MCP call owns the full run. No independent Agent, second call or
    # detached process is required to advance actions or enforce the cutoff.
    run(directory / "request.json")
    return json.loads((directory / "report.json").read_text(encoding="utf-8"))


def run(specification):
    spec = json.loads(specification.read_text(encoding="utf-8"))
    directory = specification.parent
    report_path = directory / "report.json"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report.update(status="running", started_epoch=time.time(), worker_pid=os.getpid())
    save(report_path, report)
    deadline = time.monotonic() + max(0, spec["deadline_epoch"] - time.time())
    endpoint = None
    timer = None

    def record(observation):
        observation = dict(observation)
        png = observation.pop("image_base64", None)
        if png:
            pending = directory / "latest.png.tmp"
            pending.write_bytes(base64.b64decode(png))
            publish(pending, directory / "latest.png")
            report["screenshot_path"] = str(directory / "latest.png")
            report["screenshot_updated_epoch"] = time.time()
        report["observation"] = observation
        report["observations"] += 1
        if spec["backend"] == "cli": report["completed_actions"] += int(observation.get("completed", 0))
        report["updated_epoch"] = time.time()
        save(report_path, report)

    try:
        options = {"mode": spec["backend"] + "_auto", "controller": "ai", "seed": spec["seed"], "viewer": spec["viewer"]}
        if spec["backend"] == "cli": options["receipt_path"] = report["receipt_path"]
        if spec.get("content_path"): options["content_path"] = spec["content_path"]
        observation = runtime.start(options, deadline=deadline)
        endpoint = runtime.SESSIONS[observation["session_id"]]
        timer = threading.Timer(max(0, deadline - time.monotonic()), endpoint.stop)
        timer.daemon = True
        timer.start()
        record(observation)
        if spec["backend"] == "godot":
            observation = endpoint.request({"type": "start", "wait_ms": 200, "capture_image": False})
            record(observation)
        next_image = time.monotonic() + 5
        while True:
            runtime.remaining(deadline, 1)
            if observation.get("faulted") or observation.get("host_errors"):
                raise RuntimeError("Live host reported a fault; see the last observation")
            if spec["backend"] == "cli":
                if not observation.get("active", True):
                    report["status"] = "completed"
                    break
                if observation.get("status", {}).get("status") == "awaiting_input":
                    raise RuntimeError("Engine-AI run unexpectedly requested a player decision")
                observation = endpoint.request({"type": "advance", "steps": report["advance_batch_steps"]},
                                               timeout=runtime.remaining(deadline, 86400))
            else:
                if observation.get("state", {}).get("active") is False:
                    report["status"] = "completed"
                    break
                # Advance only visible dialogue through the normal input path.
                # Campaign choices remain owned by the real engine AI.
                operation = {"type": "input_action", "action": "dialogue_continue", "frames": 1} \
                    if observation.get("reading") else {"type": "observe"}
                operation.update(wait_ms=200, capture_image=time.monotonic() >= next_image)
                if operation["capture_image"]: next_image = time.monotonic() + 5
                observation = endpoint.request(operation)
            record(observation)
    except Exception as error:
        report["status"] = "deadline_reached" if time.monotonic() >= deadline else "failed"
        report["detail"] = "Configured wall-clock deadline reached; live host stopped." \
            if report["status"] == "deadline_reached" else str(error)
    finally:
        if timer: timer.cancel()
        if endpoint:
            if time.monotonic() < deadline and not endpoint.closed:
                try: endpoint.request({"type": "close"}, timeout=5)
                except Exception as error: report["close_error"] = str(error)
            endpoint.stop()
            report["host_log"] = list(endpoint.logs)
            for key, value in list(runtime.SESSIONS.items()):
                if value is endpoint: runtime.SESSIONS.pop(key, None)
        report.update(finished_epoch=time.time(), agent_required=False,
                      host_stopped=endpoint is None or endpoint.process.poll() is not None)
        if spec["backend"] == "cli":
            receipt_path = Path(report["receipt_path"])
            count, in_budget, latest, outcomes = 0, 0, None, {}
            if receipt_path.is_file():
                with receipt_path.open(encoding="utf-8") as receipts:
                    for line in receipts:
                        if not line.endswith("\n"):
                            report["receipt_read_detail"] = "Incomplete final execution receipt excluded after host stop."
                            break
                        try:
                            receipt = json.loads(line)
                        except json.JSONDecodeError:
                            report["receipt_read_detail"] = "Incomplete final execution receipt excluded after host stop."
                            break
                        count += 1
                        if receipt.get("completed_epoch", float("inf")) > spec["deadline_epoch"]:
                            continue
                        in_budget += 1
                        latest = receipt
                        presentation = receipt.get("presentation", {})
                        outcome = presentation.get("outcome", presentation.get("status", "unreported"))
                        outcomes[outcome] = outcomes.get(outcome, 0) + 1
            report.update(flushed_execution_receipts=count, in_budget_execution_receipts=in_budget,
                          out_of_budget_execution_receipts=count - in_budget, last_execution_receipt=latest,
                          receipt_outcomes=outcomes,
                          receipt_coverage="Only actual CompleteFormal return values flushed by the CLI host; action outcomes are preserved. "
                                           "completed_actions sums successful batch observations; receipts additionally cover a final interrupted batch. "
                                           "last_execution_receipt and outcome counts exclude receipts timestamped after the configured deadline.")
        report["coverage"] = ("Only recorded live observations and flushed CLI execution receipts completed within the configured deadline; "
                              "batch observations and per-action receipt coverage are reported separately. "
                              "deadline_reached does not mean campaign passed.") if spec["backend"] == "cli" else \
                             "Only recorded live observations; deadline_reached does not mean campaign passed."
        save(report_path, report)

