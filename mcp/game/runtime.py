"""Shared production host lifecycle for automatic and input MCP services."""
from collections import deque
from contextlib import contextmanager
import json
import os
from pathlib import Path
import queue
import secrets
import socket
import subprocess
import sys
import threading
import time
from environment import resolve_dotnet, resolve_godot

ROOT = Path(__file__).resolve().parents[2]
SESSIONS = {}
BUILD_LOCK = threading.Lock()


class DeadlineReached(TimeoutError):
    pass


def remaining(deadline, maximum):
    if deadline is None:
        return maximum
    seconds = min(maximum, deadline - time.monotonic())
    if seconds <= 0:
        raise DeadlineReached("Configured wall-clock deadline reached.")
    return seconds


def terminate(process):
    if process.poll() is not None: return
    if os.name == "nt":
        try:
            subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=5,
                           creationflags=subprocess.CREATE_NO_WINDOW)
        except (OSError, subprocess.TimeoutExpired):
            pass
    else:
        process.kill()
    if process.poll() is None: process.kill()
    process.wait(timeout=5)

def spawn(command, **kwargs):
    environment = os.environ.copy()
    # MCP stdio clients may intentionally inherit a minimal Windows environment.
    # NuGet resolves its user configuration under APPDATA even for local builds.
    if os.name == "nt":
        environment.setdefault("APPDATA", str(Path.home() / "AppData/Roaming"))
        environment.setdefault("LOCALAPPDATA", str(Path.home() / "AppData/Local"))
        drive = environment.get("SYSTEMDRIVE", "C:")
        for key, folder in {"ProgramFiles": "Program Files", "ProgramFiles(x86)": "Program Files (x86)",
                            "ProgramData": "ProgramData", "ALLUSERSPROFILE": "ProgramData", "WINDIR": "Windows"}.items():
            environment.setdefault(key, drive + "/" + folder)
    environment.setdefault("DOTNET_ROLL_FORWARD", "Major")
    local_dotnet = ROOT / ".runtime/dotnet"
    if local_dotnet.is_dir():
        environment["DOTNET_ROOT"] = str(local_dotnet)
        environment["PATH"] = str(local_dotnet) + os.pathsep + environment.get("PATH", "")
    return subprocess.Popen([str(part) for part in command], cwd=ROOT,
                            encoding="utf-8", errors="replace", text=True,
                            env=environment,
                            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
                            **kwargs)


@contextmanager
def build_gate(deadline):
    if not BUILD_LOCK.acquire(timeout=remaining(deadline, 180)):
        remaining(deadline, 1)
        raise RuntimeError("Build lock timed out")
    try:
        yield
    finally:
        BUILD_LOCK.release()


def build(backend, deadline=None):
    with build_gate(deadline):
        project = ROOT / ("dv_core/Cli/DuelView.Cli.csproj" if backend == "cli"
                          else "godot_csharp/DualOntologyWorldEngine.csproj")
        dotnet = resolve_dotnet(deadline=deadline)
        timeout = remaining(deadline, 180)
        command = [dotnet, "build", project, "--nologo"]
        # Each live CLI owns an immutable build output. Windows must not need
        # to overwrite DLLs held by another formal input/automatic session.
        output_directory = None
        if backend == "cli":
            output_directory = ROOT / ".runtime/hosts" / ("cli-" + secrets.token_hex(12))
            command += ["--configuration", "Release", "--output", output_directory]
        process = spawn(command,
                        stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        try:
            output, _ = process.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            terminate(process)
            process.communicate()
            if deadline is not None and time.monotonic() >= deadline:
                raise DeadlineReached("Deadline reached during build; no game session was started.")
            raise RuntimeError("Build timed out; no game session was started.")
        if process.returncode:
            raise RuntimeError("Build failed:\n" + output[-10000:])
        if output_directory is not None:
            build_receipt = {
                "project": str(project), "configuration": "Release",
                "completed_epoch": time.time(), "exit_code": process.returncode}
            (output_directory / "formal-build.yaml").write_text("".join(
                key + ": " + json.dumps(value, ensure_ascii=False) + "\n"
                for key, value in build_receipt.items()), encoding="utf-8")
        return output_directory


def drain(stream, target):
    for line in stream:
        if isinstance(target, queue.Queue):
            target.put(line)
        else:
            target.append(line.rstrip())
    if isinstance(target, queue.Queue):
        target.put(None)


class Endpoint:
    def __init__(self, mode, controller, deadline=None):
        self.mode, self.controller = mode, controller
        self.backend = "cli" if mode.startswith("cli") else "godot"
        self.lock = threading.Lock()
        self.logs = deque(maxlen=32)
        self.lines = queue.Queue()
        self.process = None
        self.connection = None
        self.stream = None
        self.serial = 0
        self.token = secrets.token_hex(24)
        self.closed = False
        self.content_path = str(ROOT / "godot_csharp/Content/world.dv")
        self.deadline = deadline
        self.stop_lock = threading.RLock()

    def attach(self, process):
        self.process = process
        threading.Thread(target=drain, args=(process.stderr, self.logs), daemon=True).start()
        threading.Thread(target=drain, args=(process.stdout, self.lines if self.backend == "cli" else self.logs),
                         daemon=True).start()

    def request(self, operation, timeout=45):
        if self.closed:
            raise RuntimeError("Session is closed; start a new session.")
        timeout = remaining(self.deadline, timeout)
        self.serial += 1
        message = dict(operation, id=self.serial)
        try:
            if self.backend == "cli":
                self.process.stdin.write(json.dumps(message, ensure_ascii=False) + "\n")
                self.process.stdin.flush()
                line = self.lines.get(timeout=timeout)
            else:
                self.connection.settimeout(timeout)
                self.connection.sendall((json.dumps(dict(message, token=self.token), ensure_ascii=False) + "\n").encode())
                line = self.stream.readline(16 * 1024 * 1024)
            if not line:
                raise RuntimeError("Host exited: " + "\n".join(self.logs)[-4000:])
            response = json.loads(line)
            if response.get("id") != self.serial:
                raise RuntimeError("Host response id mismatch.")
        except (queue.Empty, TimeoutError, OSError, ValueError, RuntimeError) as error:
            self.stop()
            raise RuntimeError("Host request failed; session closed to prevent duplicate actions. " + str(error)) from error
        if "error" in response:
            raise RuntimeError(str(response["error"]))
        result = response["result"]
        if self.backend == "godot":
            result["host_errors"] = [line for line in self.logs if "ERROR:" in line or "SCRIPT ERROR" in line]
        return result

    def stop(self):
        with self.stop_lock:
            self.closed = True
            if self.process: terminate(self.process)
            if self.connection:
                try: self.connection.shutdown(socket.SHUT_RDWR)
                except OSError: pass
                self.connection.close()
            if self.stream: self.stream.close()
            if self.process:
                for stream in (self.process.stdin, self.process.stdout, self.process.stderr):
                    if stream: stream.close()


def start(arguments, deadline=None):
    mode = arguments.get("mode", "cli_ai")
    controller = arguments.get("controller", "ai" if mode in ("cli_auto", "godot_auto") else "player")
    if mode == "cli_auto" and controller != "ai":
        raise ValueError("cli_auto requires controller=ai; use cli_ai for SubAgent decisions.")
    if mode in ("cli_ai", "cli_manual") and controller != "player":
        raise ValueError("cli_ai / cli_manual require controller=player.")
    endpoint = Endpoint(mode, controller, deadline)
    endpoint.content_path = str(Path(arguments.get("content_path", endpoint.content_path)).resolve())
    backend = endpoint.backend
    output_directory = build(backend, deadline)
    try:
        if backend == "cli":
            endpoint.attach(spawn([resolve_dotnet(deadline=deadline), output_directory / "DuelView.Cli.dll"],
                                  stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE))
            open_options = {
                "controller": controller, "viewer": arguments.get("viewer", "detective"),
                "decision_maker": "engine-ai" if controller == "ai" else arguments.get("decision_maker", "agent" if mode == "cli_ai" else "human"),
                "campaign": {"seed": arguments.get("seed", 1)}, "content_path": endpoint.content_path,
                "audit": False, "developer": False}
            if arguments.get("receipt_path"):
                open_options["receipt_path"] = str(Path(arguments["receipt_path"]).resolve())
            observation = endpoint.request({"type": "open", "options": open_options})
        else:
            with socket.socket() as reservation:
                reservation.bind(("127.0.0.1", 0))
                port = reservation.getsockname()[1]
            # Use the project's renderer, matching editor and exported gameplay.
            command = [resolve_godot(deadline=deadline), "--path", ROOT / "godot_csharp"]
            scene = arguments.get("scene", "")
            if mode == "scene":
                path = (ROOT / "godot_csharp" / scene.removeprefix("res://")).resolve()
                path.relative_to((ROOT / "godot_csharp/scenes").resolve())
                if path.suffix != ".tscn" or not path.is_file():
                    raise ValueError("scene must name an existing scenes/*.tscn resource.")
                command.append("res://" + path.relative_to(ROOT / "godot_csharp").as_posix())
            elif scene:
                raise ValueError("scene is only accepted in mode=scene.")
            # The opt-in GameMcp autoload is available in every production scene.
            command += ["--", f"--dv-mcp-port={port}", f"--dv-mcp-token={endpoint.token}",
                        f"--dv-controller={controller}", f"--dv-seed={arguments.get('seed', 1)}",
                        "--dv-viewer=" + arguments.get("viewer", "detective")]
            command.append("--dv-content=" + endpoint.content_path)
            endpoint.attach(spawn(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE))
            connect_deadline = time.monotonic() + remaining(deadline, 30)
            while True:
                try:
                    endpoint.connection = socket.create_connection(("127.0.0.1", port), timeout=remaining(deadline, 1))
                    break
                except OSError:
                    if endpoint.process.poll() is not None or time.monotonic() >= connect_deadline:
                        raise RuntimeError("Godot bridge did not start: " + "\n".join(endpoint.logs)[-4000:])
                    time.sleep(.1)
            endpoint.stream = endpoint.connection.makefile("rb")
            observation = endpoint.request({"type": "observe", "wait_ms": 300})
        identity = secrets.token_hex(8)
        SESSIONS[identity] = endpoint
        return dict(observation, session_id=identity, mode=mode)
    except BaseException:
        endpoint.stop()
        raise


def execute(name, arguments):
    if name == "on":
        return start(arguments)
    identity = arguments["session_id"]
    endpoint = SESSIONS.get(identity)
    if endpoint is None:
        raise ValueError("Unknown session_id in this MCP server. Start your own session.")
    with endpoint.lock:
        if name == "close":
            try:
                if not endpoint.closed:
                    endpoint.request({"type": "close"}, timeout=15)
            finally:
                endpoint.stop()
                SESSIONS.pop(identity, None)
            return {"session_id": identity, "closed": True}
        operation = dict(arguments.get("operation", {})) if name == "step" else {"type": "observe"}
        if endpoint.backend == "cli":
            if operation.get("type") not in ("select", "advance", "observe", "reload_content"):
                raise ValueError("CLI operations: select(choice_id), advance(steps), observe.")
        elif operation.get("type") not in ("start", "select", "input_action", "click", "observe", "reload_content"):
            raise ValueError("Godot operations: start, select(choice_id), input_action(action,frames), click(x,y), observe.")
        if endpoint.backend == "godot":
            operation["wait_ms"] = arguments.get("wait_ms", 200)
        if operation.get("type") == "reload_content" and not operation.get("path") and "content_source" not in operation:
            operation["path"] = endpoint.content_path
        # A legal CLI batch can complete up to 100 formal actions. Give it a
        # bounded host budget inside the input client's 240-second timeout;
        # a failed request still closes the session and is never retried.
        request_timeout = 45
        if (endpoint.backend == "cli" and operation.get("type") in ("advance", "select")
                and isinstance(operation.get("steps", 1), int) and operation.get("steps", 1) > 1):
            request_timeout = 180
        observation = endpoint.request(operation, timeout=request_timeout)
        return dict(observation, session_id=identity, mode=endpoint.mode)
