"""Start compiler preparation outside the MCP/LSP request and game loops."""
import json
import os
from pathlib import Path
import subprocess
import sys

from dv_toolchain import LANGUAGE, ROOT, compiler, environment

_PROCESS = None


def start_prewarm():
    global _PROCESS
    cache = LANGUAGE / 'obj' / 'dv-toolchain'
    cache.mkdir(parents=True, exist_ok=True)
    log = cache / 'prewarm.log'
    if _PROCESS is None:
        options = {'creationflags': subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.BELOW_NORMAL_PRIORITY_CLASS} if os.name == 'nt' else {'start_new_session': True}
        with log.open('ab') as output:
            _PROCESS = subprocess.Popen(
                [sys.executable, str(Path(__file__).resolve()), '--worker'],
                cwd=ROOT, env=environment(), stdin=subprocess.DEVNULL,
                stdout=output, stderr=subprocess.STDOUT, **options,
            )
    return {'status': 'started' if _PROCESS.poll() is None else 'finished',
            'pid': _PROCESS.pid, 'exit_code': _PROCESS.returncode, 'log': str(log)}


if __name__ == '__main__':
    if sys.argv[1:] == ['--worker']:
        if os.name != 'nt':
            os.nice(10)
        print(json.dumps({'status': 'ready', 'compiler': str(compiler())}), flush=True)
    else:
        print(json.dumps(start_prewarm()))
