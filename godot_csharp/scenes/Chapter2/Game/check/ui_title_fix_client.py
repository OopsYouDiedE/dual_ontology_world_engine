"""Human-directed local automated UI regression client; no story shortcuts."""
import sys
import json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[4] / 'tools'))
from blind_playtest import BlindSession
s = BlindSession('ui-title-fix-20261004b')
status = s.submit(json.loads(sys.argv[1]), sys.argv[2]) if len(sys.argv) > 1 else s.status()
print(json.dumps({'seq': status['ack_seq'], 'elapsed_ms': status['elapsed_ms'],
    'screenshot': status['screenshot'], 'controls': status['controls'],
    'text': [x['text'] for x in status['visible_text']]}, ensure_ascii=False, indent=2))
