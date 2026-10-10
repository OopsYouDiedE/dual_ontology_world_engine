"""Check a closed virtual-player JSONL trace without running or changing the game."""

from pathlib import Path
import argparse
from collections import Counter
import json
import math


POSITION_TOLERANCE = 0.25  # Collision depenetration can move a blocked axis slightly.
KEY_DIRECTIONS = {"W": (0, -1), "S": (0, 1), "A": (-1, 0), "D": (1, 0)}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def position(value, label):
    require(isinstance(value, list) and len(value) == 2, f"{label}: missing 2D position")
    require(all(isinstance(v, (int, float)) and math.isfinite(v) for v in value),
            f"{label}: non-finite position")
    return value


def movement_delta(keys, start, end, label):
    delta = [end[axis] - start[axis] for axis in (0, 1)]
    direction = [sum(KEY_DIRECTIONS[key][axis] for key in keys) for axis in (0, 1)]
    for axis in (0, 1):
        if direction[axis]:
            require(delta[axis] * direction[axis] >= -POSITION_TOLERANCE,
                    f"{label}: moved against requested direction")
        else:
            require(abs(delta[axis]) <= POSITION_TOLERANCE,
                    f"{label}: unexpected movement on unrequested axis")
    return delta


def check_trace(path, require_stop_release=False):
    rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    require(rows, "empty trace")
    require(rows[0]["kind"] == "start", "missing initial start")
    require(rows[-1]["kind"] == "stop", "missing final stop")
    require(sum(row["kind"] == "stop" for row in rows) == 1, "stop count is not one")
    require(rows[-1]["value"]["reason"] != "engine_error", "engine_error stop")

    active = released = awaiting_end = last_position = None
    pairs = []
    moves = []
    end_checks = 0
    headers = set()
    previous_elapsed = -1
    stop_release = None
    for sequence, row in enumerate(rows, 1):
        require(row["sequence"] == sequence, f"row {sequence}: noncontiguous sequence")
        elapsed = row["elapsed_ms"]
        require(isinstance(elapsed, (int, float)) and math.isfinite(elapsed)
                and elapsed >= previous_elapsed, f"row {sequence}: elapsed time went backwards")
        previous_elapsed = elapsed
        kind, value = row["kind"], row["value"]
        if kind == "observation":
            headers.update(text for text in value.get("Text", []) if "\u00b7" in text)
            if "Position" in value:
                last_position = position(value["Position"], f"row {sequence}")
                if active is not None:
                    active["position_samples"] += 1
                if awaiting_end is not None:
                    require(math.dist(last_position, awaiting_end) <= POSITION_TOLERANCE,
                            f"row {sequence}: recorded movement end differs from observed Position")
                    end_checks += 1
                    awaiting_end = None
        elif kind == "decision" or (kind == "input" and value.get("kind") == "select_candidate"):
            require(active is None, f"row {sequence}: a new choice happened while movement keys were held")
            require(released is None, f"row {sequence}: released movement lacks its completion record")
        elif kind == "keyboard_input":
            keys = value.get("keys")
            require(isinstance(keys, list) and 1 <= len(keys) <= 2 and len(set(keys)) == len(keys)
                    and all(key in KEY_DIRECTIONS for key in keys), f"row {sequence}: invalid keys")
            require(not ({"W", "S"} <= set(keys) or {"A", "D"} <= set(keys)),
                    f"row {sequence}: opposing keys")
            require(isinstance(value.get("pressed"), bool), f"row {sequence}: pressed is not boolean")
            if value["pressed"]:
                require(active is None and released is None, f"row {sequence}: overlapping key groups")
                require(last_position is not None, f"row {sequence}: no position before key press")
                active = {"keys": keys, "pressed_sequence": sequence, "pressed_ms": elapsed,
                          "observed_start": last_position, "position_samples": 0}
            else:
                require(active is not None, f"row {sequence}: release without press")
                require(keys == active["keys"], f"row {sequence}: release does not match held keys")
                active.update(released_sequence=sequence, released_ms=elapsed,
                              held_ms=elapsed - active["pressed_ms"])
                require(active["held_ms"] > 0, f"row {sequence}: zero-duration press")
                released, active = active, None
                pairs.append(released)
        elif kind == "keyboard_move":
            require(active is None and released is not None, f"row {sequence}: unpaired movement record")
            require(value.get("keys") == released["keys"], f"row {sequence}: movement keys mismatch")
            start = position(value.get("start_position"), f"row {sequence} start")
            end = position(value.get("end_position"), f"row {sequence} end")
            require(math.dist(start, released["observed_start"]) <= POSITION_TOLERANCE,
                    f"row {sequence}: movement start differs from pre-press Position")
            require(released["position_samples"] > 0, f"row {sequence}: no live Position samples during hold")
            require(250 <= released["held_ms"] <= 500,
                    f"row {sequence}: normal hold outside 250-500 ms ({released['held_ms']} ms)")
            delta = movement_delta(value["keys"], start, end, f"row {sequence}")
            moves.append({"sequence": sequence, "keys": value["keys"], "held_ms": released["held_ms"],
                          "distance": math.hypot(*delta), "delta": delta,
                          "position_samples": released["position_samples"]})
            awaiting_end, released = end, None
        elif kind == "stop":
            require(active is None, f"row {sequence}: stop leaves keys held")
            if released is not None:
                require(released["released_sequence"] == sequence - 1 and released["released_ms"] == elapsed,
                        f"row {sequence}: incomplete normal movement, rather than stop-time release")
                stop_release, released = released, None
                endpoint = position(value.get("visible_position"), f"row {sequence} stop")
                stop_release["delta"] = movement_delta(stop_release["keys"], stop_release["observed_start"],
                                                      endpoint, f"row {sequence} stop")
            if awaiting_end is not None:
                endpoint = position(value.get("visible_position"), f"row {sequence} stop")
                require(math.dist(endpoint, awaiting_end) <= POSITION_TOLERANCE,
                        f"row {sequence}: stop position differs from movement end")
                end_checks += 1
                awaiting_end = None

    require(active is None and released is None and awaiting_end is None, "incomplete keyboard lifecycle")
    require(moves, "no completed keyboard movement to check")
    require(end_checks == len(moves), "movement ends lack independent Position confirmation")
    require(not require_stop_release or stop_release is not None, "stop while keys held was not exercised")
    counts = Counter("+".join(move["keys"]) for move in moves)
    blocked = [move["sequence"] for move in moves if move["distance"] <= POSITION_TOLERANCE]
    return {"trace": str(path), "rows": len(rows), "stop": rows[-1]["value"],
            "paired_key_groups": len(pairs), "normal_moves": len(moves),
            "stop_release": stop_release, "directions": dict(sorted(counts.items())),
            "normal_hold_ms": [min(move["held_ms"] for move in moves), max(move["held_ms"] for move in moves)],
            "independent_end_position_checks": end_checks,
            "moving_count": len(moves) - len(blocked), "nearly_stationary_sequences": blocked,
            "visible_location_headers": sorted(headers),
            "note": "Stationary movement is collision/lock evidence to inspect; it does not alone identify the obstacle."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("traces", nargs="+", type=Path)
    parser.add_argument("--require-stop-release", action="store_true")
    args = parser.parse_args()
    for path in args.traces:
        try:
            result = check_trace(path, args.require_stop_release)
        except (ValueError, KeyError, TypeError, OSError) as error:
            print(json.dumps({"trace": str(path), "passed": False, "error": str(error)}, ensure_ascii=False))
            return 1
        print(json.dumps({"passed": True, **result}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
