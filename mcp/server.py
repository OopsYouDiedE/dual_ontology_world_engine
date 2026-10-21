"""Small stdio MCP entrypoint. Tool implementations are imported only when called."""
from __future__ import annotations

import importlib
from contextlib import redirect_stdout
from copy import deepcopy
import json
from pathlib import Path
import sys

CATALOG = json.loads((Path(__file__).with_name('tools.json')).read_text(encoding='utf-8'))
_prewarm_started = False


def public_tool(item):
    """The route catalog also owns the public operation choices."""
    tool = {key: deepcopy(value) for key, value in item.items()
            if key not in ('routes', 'selector', 'default')}
    selector = item.get('selector')
    if selector:
        choice = tool['inputSchema']['properties'][selector]
        choice['enum'] = list(item['routes'])
        if 'default' in item:
            choice['default'] = item['default']
    return tool


def start_prewarm():
    global _prewarm_started
    if not _prewarm_started:
        sys.path.insert(0, str(Path(__file__).parent / 'build'))
        with redirect_stdout(sys.stderr):
            importlib.import_module('prewarm').start_prewarm()
        _prewarm_started = True


def dispatch(request):
    method = request.get('method', '')
    if method == 'initialize':
        start_prewarm()
        return {'protocolVersion': request.get('params', {}).get('protocolVersion', '2024-11-05'),
                'capabilities': {'tools': {'listChanged': False}},
                'serverInfo': {'name': 'duelview-tools', 'version': '2.1.0'}}
    if method == 'ping':
        return {}
    if method == 'tools/list':
        return {'tools': [public_tool(item) for item in CATALOG]}
    if method == 'tools/call':
        params = request['params']
        tool = next(item for item in CATALOG if item['name'] == params['name'])
        try:
            arguments = dict(params.get('arguments', {}))
            selector = tool.get('selector')
            operation = arguments.pop(selector, tool.get('default')) if selector else 'run'
            route = tool['routes'][operation]
            with redirect_stdout(sys.stderr):
                result = importlib.import_module('tool_actions').invoke(route, arguments)
            response = {'content': [{'type': 'text', 'text': json.dumps(result, ensure_ascii=False, default=str)}]}
            if isinstance(result, dict) and result.get('exit_code', 0) != 0:
                response['isError'] = True
            return response
        except Exception as error:
            return {'content': [{'type': 'text', 'text': str(error)}], 'isError': True}
    raise ValueError('Unsupported MCP method: ' + method)


def main():
    for line in sys.stdin:
        request = json.loads(line)
        if 'id' not in request:
            continue
        response = {'jsonrpc': '2.0', 'id': request['id']}
        try:
            response['result'] = dispatch(request)
        except Exception as error:
            response['error'] = {'code': -32602, 'message': str(error)}
        print(json.dumps(response, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
