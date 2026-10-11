"""Summarize recorded public actions, without running or steering a game."""
import json
import math
import sys
from collections import Counter
from pathlib import Path

path = Path(sys.argv[1])
events = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line]
observed = {}
actions = []
for line, event in enumerate(events, 1):
    value = event["value"]
    if event["kind"] == "observation":
        observed = value
    if event["kind"] == "decision":
        candidate = next(c for c in observed["Candidates"] if c["Token"] == value["Token"])
        actions.append({"line": line, "seconds": event["elapsed_ms"] / 1000,
                        "candidate": candidate, "decision": value, "field": observed.get("Field")})
endings = [e for e in events if e["kind"] == "shown" and e["value"].get("kind") == "ending"]
first_ending = endings[0]["elapsed_ms"] / 1000 if endings else float("inf")

def counts(rows):
    moves = [r for r in rows if r["candidate"]["Kind"] == "move"]
    return {"actions": len(rows), "moves": len(moves),
            "move_fraction": len(moves) / len(rows) if rows else 0,
            "kinds": dict(Counter(r["candidate"]["Kind"] for r in rows)),
            "purposes": dict(Counter(r["candidate"]["Purpose"] for r in rows)),
            "top_nonmovement": [{"kind": k, "label": label, "count": count}
                                for (k, label), count in Counter((r["candidate"]["Kind"], r["candidate"]["Label"])
                                for r in rows if r["candidate"]["Kind"] not in ("move", "continue", "wait")).most_common(12)]}

key_moves = [e for e in events if e["kind"] == "keyboard_move"]
stalls = [e for e in key_moves if math.dist(e["value"]["start_position"], e["value"]["end_position"]) < 1]
returns = []
for previous, current in zip(key_moves, key_moves[1:]):
    p, c = previous["value"], current["value"]
    if math.dist(p["end_position"], c["start_position"]) > 1:
        continue
    distance = math.dist(p["start_position"], p["end_position"])
    if distance > 8 and math.dist(p["start_position"], c["end_position"]) < max(8, distance * .2):
        returns.append({"seconds": current["elapsed_ms"] / 1000, "previous": p, "current": c})
result = {"source": str(path), "start": next(e["value"] for e in events if e["kind"] == "start"),
          "ending_seconds": [e["elapsed_ms"] / 1000 for e in endings],
          "all": counts(actions), "through_first_ending": counts([r for r in actions if r["seconds"] <= first_ending]),
          "after_first_ending": counts([r for r in actions if r["seconds"] > first_ending]),
          "keyboard_moves": len(key_moves), "stalled_keyboard_moves": len(stalls),
          "consecutive_keyboard_returns": returns, "last_actions": actions[-15:],
          "stop": next((e["value"] for e in reversed(events) if e["kind"] == "stop"), None)}
print(json.dumps(result, ensure_ascii=False, indent=2))
