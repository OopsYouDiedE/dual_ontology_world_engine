"""LSP transport for DV compiler diagnostics. The compiler remains the authority."""
import json
import os
from pathlib import Path
import subprocess
import sys
import threading
from urllib.parse import unquote, urlparse


def path_from_uri(uri):
    parsed = urlparse(uri)
    if parsed.scheme != "file":
        raise ValueError("DV checker requires a local file URI")
    path = unquote(parsed.path)
    if os.name == "nt" and len(path) > 2 and path[0] == "/" and path[2] == ":":
        path = path[1:]
    return Path(path)


class Server:
    def __init__(self):
        self.documents = {}
        self.write_lock = threading.Lock()
        self.state_lock = threading.Lock()
        self.processes = set()
        self.timers = {}
        self.stopping = False

    def send(self, value):
        payload = json.dumps({"jsonrpc": "2.0", **value}, ensure_ascii=False).encode("utf-8")
        with self.write_lock:
            sys.stdout.buffer.write(f"Content-Length: {len(payload)}\r\n\r\n".encode() + payload)
            sys.stdout.buffer.flush()

    def publish(self, uri, version, diagnostics):
        self.send({"method": "textDocument/publishDiagnostics", "params": {
            "uri": uri, "version": version, "diagnostics": diagnostics}})

    def schedule(self, uri):
        with self.state_lock:
            previous = self.timers.pop(uri, None)
            if previous:
                previous.cancel()
            version, text = self.documents[uri]
            timer = threading.Timer(.7, self.check, (uri, version, text))
            timer.daemon = True
            self.timers[uri] = timer
            timer.start()

    def check(self, uri, version, text):
        try:
            path = path_from_uri(uri)
            configured = os.environ.get("DV_CHECKER")
            checker = Path(configured) if configured else next((
                root / "mcp/editor_lsp/check_dv.py" for root in path.parents
                if (root / "mcp/editor_lsp/check_dv.py").exists()), None)
            if checker is None:
                raise FileNotFoundError("Set DV_CHECKER to mcp/editor_lsp/check_dv.py or open a file inside the DV repository")
            with self.state_lock:
                if self.stopping or self.documents.get(uri) != (version, text):
                    return
                process = subprocess.Popen([sys.executable, str(checker), "--format", "json", "--stdin", str(path)],
                                           stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
                self.processes.add(process)
            try:
                stdout, stderr = process.communicate(text.encode("utf-8"), timeout=240)
            except subprocess.TimeoutExpired:
                process.kill()
                process.communicate()
                raise TimeoutError("DV compiler check timed out")
            finally:
                with self.state_lock:
                    self.processes.discard(process)
            result = json.loads(stdout.decode("utf-8"))
            if process.returncode and not result.get("diagnostics"):
                raise RuntimeError(stderr.decode("utf-8", errors="replace") or "DV checker failed")
            diagnostics = []
            for item in result["diagnostics"]:
                target = Path(item["path"]).resolve()
                same_file = target == path.resolve()
                line, column = (max(0, item["line"]-1), max(0, item["column"]-1)) if same_file else (0, 0)
                message = item["message"]
                if not same_file:
                    message = f'{target}:{item["line"]}:{item["column"]}: {message}'
                diagnostic = {"range": {"start": {"line": line, "character": column},
                                        "end": {"line": line, "character": column+1}},
                              "severity": 1, "source": "DV compiler", "message": message}
                if not same_file:
                    position = {"line": max(0, item["line"]-1), "character": max(0, item["column"]-1)}
                    diagnostic["relatedInformation"] = [{"location": {"uri": target.as_uri(),
                        "range": {"start": position, "end": position}}, "message": item["message"]}]
                diagnostics.append(diagnostic)
        except Exception as error:
            diagnostics = [{"range": {"start": {"line": 0, "character": 0}, "end": {"line": 0, "character": 1}},
                            "severity": 1, "source": "DV checker", "message": str(error)}]
        with self.state_lock:
            if not self.stopping and self.documents.get(uri) == (version, text):
                self.publish(uri, version, diagnostics)

    def dispatch(self, message):
        method = message.get("method", "")
        params = message.get("params") or {}
        request = message.get("id")
        if method == "initialize":
            self.send({"id": request, "result": {"capabilities": {
                "textDocumentSync": {"openClose": True, "change": 1, "save": {"includeText": True}}},
                "serverInfo": {"name": "DuelView compiler checker", "version": "0.1.0"}}})
        elif method == "shutdown":
            self.stop()
            self.send({"id": request, "result": None})
        elif method == "exit":
            self.stop()
            return False
        elif method in ("textDocument/didOpen", "textDocument/didChange", "textDocument/didSave"):
            document = params["textDocument"]
            uri = document["uri"]
            with self.state_lock:
                previous_version, previous_text = self.documents.get(uri, (0, ""))
                text = document.get("text", params.get("text", previous_text))
                if method == "textDocument/didChange":
                    for change in params["contentChanges"]:
                        text = change["text"]
                version = document.get("version", previous_version)
                self.documents[uri] = (version, text)
            self.schedule(uri)
        elif method == "textDocument/didClose":
            uri = params["textDocument"]["uri"]
            with self.state_lock:
                self.documents.pop(uri, None)
                timer = self.timers.pop(uri, None)
                if timer:
                    timer.cancel()
            self.publish(uri, None, [])
        elif request is not None:
            self.send({"id": request, "error": {"code": -32601, "message": f"Unsupported method: {method}"}})
        return True

    def stop(self):
        with self.state_lock:
            self.stopping = True
            for timer in self.timers.values():
                timer.cancel()
            for process in self.processes:
                process.kill()

    def serve(self):
        try:
            while True:
                headers = {}
                while True:
                    line = sys.stdin.buffer.readline()
                    if not line:
                        return
                    if line == b"\r\n":
                        break
                    key, value = line.decode("ascii").split(":", 1)
                    headers[key.lower()] = value.strip()
                payload = sys.stdin.buffer.read(int(headers["content-length"]))
                if not self.dispatch(json.loads(payload)):
                    break
        finally:
            self.stop()


if __name__ == "__main__":
    Server().serve()
