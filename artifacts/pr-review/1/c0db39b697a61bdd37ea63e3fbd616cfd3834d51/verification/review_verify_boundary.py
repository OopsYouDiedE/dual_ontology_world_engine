import importlib.util
import io
import contextlib
import sys
from unittest.mock import patch
from pathlib import Path
spec = importlib.util.spec_from_file_location('review_verify', Path(__file__).resolve().parents[4] / 'tools' / 'verify.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
calls = []
def fake_run(name, command, timeout, scan_engine=False):
    calls.append(name)
    return True, 'controlled build success; no command executed'
output = io.StringIO()
with patch.object(module, 'find_godot', return_value=None), patch.object(module, 'run', side_effect=fake_run), patch.object(sys, 'argv', ['verify.py']), contextlib.redirect_stdout(output):
    result = module.main()
print(output.getvalue())
print('OBSERVED exit:', result)
print('OBSERVED executed steps:', calls)
print('BUG CONFIRMED:', result == 0 and calls == ['core', 'scope', 'host'])
assert result == 0 and calls == ['core', 'scope', 'host']
