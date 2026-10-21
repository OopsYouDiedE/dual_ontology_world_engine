"""MCP access to the shared LSP transport and its actual diagnostic publication."""
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent


def lsp_config():
    return {'command': sys.executable, 'args': [str(HERE / 'dv_lsp.py')],
            'env': {'DV_CHECKER': str(HERE / 'check_dv.py')},
            'transport': 'stdio with LSP Content-Length framing'}


def lsp_diagnostics(path: str, text: str | None = None, version: int = 1):
    from dv_lsp import Server

    source = Path(path).resolve()
    uri = source.as_uri()
    buffer = source.read_text(encoding='utf-8') if text is None else text
    messages = []
    server = Server(writer=messages.append)
    server.documents[uri] = (version, buffer)
    try:
        server.check(uri, version, buffer)
    finally:
        server.stop()
    return {'uri': uri, 'version': version, 'notifications': messages,
            'scope': 'compiler diagnostics through the same LSP publisher; content is not executed'}
