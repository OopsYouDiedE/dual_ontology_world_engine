from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys
import time

root = Path(__file__).resolve().parent
label = sys.argv[1]
command = sys.argv[2:]
log = root / (label + '.log')
if log.exists():
    raise SystemExit('Refusing to overwrite archived evidence: ' + str(log))
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
clock = time.monotonic()
with log.open('xb') as output:
    result = subprocess.run(command, stdout=output, stderr=subprocess.STDOUT, shell=False)
record = {'command': command, 'started_utc': started, 'elapsed_wall_seconds': time.monotonic() - clock,
          'exit_code': result.returncode, 'log': log.name, 'sha256': hashlib.sha256(log.read_bytes()).hexdigest()}
(root / (label + '.json')).write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(('PASS' if result.returncode == 0 else 'FAIL') + ' ' + label + ' exit=' + str(result.returncode) + ' log=' + str(log))
raise SystemExit(result.returncode)
