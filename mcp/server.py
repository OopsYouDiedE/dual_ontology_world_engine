"""Small stdio MCP entrypoint. Tool implementations are imported only when called."""
from __future__ import annotations

import importlib
from contextlib import redirect_stdout
import json
from pathlib import Path
import sys

CATALOG = json.loads((Path(__file__).with_name('tools.json')).read_text(encoding='utf-8'))


def dispatch(request):
    method = request.get('method', '')
    if method == 'initialize':
        return {'protocolVersion': request.get('params', {}).get('protocolVersion', '2024-11-05'),
                'capabilities': {'tools': {'listChanged': False}},
                'serverInfo': {'name': 'duelview-tools', 'version': '2.0.0'}}
    if method == 'ping':
        return {}
    if method == 'tools/list':
        return {'tools': [{key: value for key, value in item.items() if key != 'handler'} for item in CATALOG]}
    if method == 'tools/call':
        params = request['params']
        tool = next(item for item in CATALOG if item['name'] == params['name'])
        try:
            module, function = tool['handler'].split(':')
            with redirect_stdout(sys.stderr):
                handler = getattr(importlib.import_module(module), function)
                result = handler(**params.get('arguments', {}))
            return {'content': [{'type': 'text', 'text': json.dumps(result, ensure_ascii=False)}]}
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
