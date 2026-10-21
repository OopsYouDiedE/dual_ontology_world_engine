"""Execute one route from tools.json. The catalog is the only operation registry."""
from __future__ import annotations

import importlib
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / 'mcp'


def _run(command, route, *, input_text=None):
    result = subprocess.run(command, cwd=ROOT / route.get('cwd', ''), input=input_text,
                            stdin=subprocess.DEVNULL if input_text is None else None,
                            capture_output=True, text=True, encoding='utf-8',
                            errors='replace', timeout=route.get('timeout', 600))
    return {'exit_code': result.returncode, 'stdout': result.stdout, 'stderr': result.stderr}


def _workspace_command(route, parameters):
    command = [sys.executable, str(TOOLS / 'godot_workspace.py')]
    if parameters.get('godot'):
        command += ['--godot', parameters['godot']]
    command += route.get('flags', [])
    if route.get('csharp'):
        command.append('--csharp')
    if route.get('headless'):
        command.append('--headless')
    if route.get('script'):
        command += ['--script', route['script']]
    arguments = parameters.get('arguments', [])
    if arguments:
        command += ['--', *arguments]
    return command


def invoke(route, parameters):
    kind = route['kind']
    if kind == 'function':
        module = TOOLS / route['module']
        sys.path.insert(0, str(module.parent))
        function = getattr(importlib.import_module(module.stem), route['function'])
        result = function(**parameters.get('arguments', {}))
        return {route['result_key']: result} if 'result_key' in route else result
    if kind == 'workspace':
        logs = ROOT / 'artifacts/tool-workspaces'
        logs.mkdir(parents=True, exist_ok=True)
        output = Path(tempfile.mkdtemp(prefix=route['name'] + '-', dir=logs))
        with (output / 'launch.log').open('wb') as log:
            process = subprocess.Popen(_workspace_command(route, parameters), cwd=ROOT,
                                       stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT)
        return {'pid': process.pid, 'mode': route['name'], 'log': str(output / 'launch.log'),
                'workspace_lifetime': 'until the Godot window closes'}
    if kind == 'godot':
        return _run(_workspace_command(route, parameters), route)
    arguments = [*route.get('arguments', []), *parameters.get('arguments', [])]
    if kind == 'python':
        command = [sys.executable, str(TOOLS / route['path']), *arguments]
    elif kind == 'blender':
        blender = os.environ.get('BLENDER') or shutil.which('blender')
        if not blender:
            raise RuntimeError('Set BLENDER to the Blender executable')
        command = [blender, '--background', '--python', str(TOOLS / route['path']), '--', *arguments]
    elif kind == 'command':
        command = [*route['command'], *arguments]
        command[0] = shutil.which(command[0]) or command[0]
    else:
        raise ValueError('Unknown catalog route kind: ' + kind)
    return _run(command, route, input_text=parameters.get('stdin'))
