"""Interpret existing neutral DV pair-record output; never execute a session."""
import json
import sys
from pathlib import Path

out = Path(sys.argv[1])
def record(value):
    if isinstance(value, list):
        if value and all(isinstance(p, list) and len(p) == 2 and isinstance(p[0], str) for p in value):
            return {p[0]: record(p[1]) for p in value}
        return [record(p) for p in value]
    return value

packets = [record(json.loads(line)) for line in (out / 'probe.stdout').read_text(encoding='utf-8-sig').splitlines() if line.startswith(('[', '{'))]
packet = next(p for p in packets if isinstance(p, dict) and p.get('kind') == 'neutral-scheduler-guard')
expected = [30, 20, 10, 30, 20, 20, 30, 11, 30, 12]
if [p['next'] for p in packet['boundaries']] != expected:
    raise RuntimeError('Boundary values differ')
if [p['complete'] for p in packet['boundaries']] != [i != 7 for i in range(len(expected))]:
    raise RuntimeError('Boundary completeness differs')
if any(v is not True for v in packet['invalidation'].values()):
    raise RuntimeError('Invalidation example differs')
(out / 'INTERPRETATION.txt').write_text('Neutral helper values match the declared boundary/invalidation examples. Initial reader expected JSON objects; this reader decodes existing DV pair records. Original stdout is preserved, without rerunning cognition or a game session. No performance or formal certificate issuance is established.', encoding='utf-8')
(out / 'engineering-result.json').write_text(json.dumps({'complete': True, 'scope': 'neutral-helper-only', 'reader_repair': 'DV pair-record decoding; original raw stdout preserved', 'observed': packet}, indent=2), encoding='utf-8')
