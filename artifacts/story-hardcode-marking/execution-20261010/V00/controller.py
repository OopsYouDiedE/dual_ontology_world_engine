"""Neutral candidate policy. It has no engine/session/observer handle.

This is a protocol skeleton, not a solver and not an OS sandbox.
The adversarial modes are only used by the synthetic boundary suite.
"""
import argparse
import json
import sys
import time


class BoundaryError(ValueError):
    pass


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise BoundaryError("duplicate_field")
        result[key] = value
    return result


def exact_keys(value, expected):
    if not isinstance(value, dict) or set(value) != set(expected):
        raise BoundaryError("unknown_or_missing_field")


def bounded_text(value, limit=4096):
    if not isinstance(value, str) or len(value) > limit:
        raise BoundaryError("invalid_text")


def parse_observation(raw):
    if len(raw.encode("utf-8")) > 65536:
        raise BoundaryError("observation_limit")
    value = json.loads(raw, object_pairs_hook=unique_object)
    exact_keys(value, ("schema", "turn", "lines", "candidates", "card", "complete"))
    if value["schema"] != "v00-public-candidate/v1":
        raise BoundaryError("unsupported_schema")
    if type(value["turn"]) is not int or value["turn"] < 1:
        raise BoundaryError("invalid_turn")
    if type(value["complete"]) is not bool:
        raise BoundaryError("invalid_complete")
    for field in ("lines", "candidates"):
        if not isinstance(value[field], list) or len(value[field]) > 256:
            raise BoundaryError("invalid_list")
    for line in value["lines"]:
        exact_keys(line, ("speaker", "mood", "text"))
        for text in line.values():
            bounded_text(text)
    tokens = set()
    for candidate in value["candidates"]:
        exact_keys(candidate, ("token", "label"))
        bounded_text(candidate["token"], 2048)
        bounded_text(candidate["label"])
        if not candidate["token"] or candidate["token"] in tokens:
            raise BoundaryError("invalid_candidate_token")
        tokens.add(candidate["token"])
    if value["card"] is not None:
        exact_keys(value["card"], ("title", "facts", "source", "notes"))
        for field in ("title", "facts", "source"):
            bounded_text(value["card"][field])
        notes = value["card"]["notes"]
        if not isinstance(notes, list) or len(notes) > 256:
            raise BoundaryError("invalid_notes")
        for note in notes:
            bounded_text(note)
    return value


def decide(observation):
    # Generic ordering of CURRENT visible labels, with no stored route or token.
    candidates = observation["candidates"]
    if observation["complete"] or not candidates:
        return None  # Transport abstention: creates no WorldInput.
    choice = min(candidates, key=lambda item: (item["label"], item["token"]))
    return {"type": "SelectCandidate", "token": choice["token"]}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", default="neutral", choices=(
        "neutral", "unknown", "hidden", "duplicate", "stale", "oversized", "hang"))
    args = parser.parse_args()
    remembered = None
    last_turn = 0
    for raw in sys.stdin:
        try:
            observation = parse_observation(raw)
            if observation["turn"] <= last_turn:
                raise BoundaryError("non_monotonic_turn")
            last_turn = observation["turn"]
            decision = decide(observation)
            if args.mode == "hang":
                time.sleep(10)
            elif args.mode == "oversized":
                print("x" * 20000, flush=True)
                continue
            elif args.mode == "unknown":
                decision = {"type": "Probe", "name": "neutral"}
            elif args.mode == "hidden":
                decision = dict(decision or {}, state={"value": 1})
            elif args.mode == "duplicate":
                token = (decision or {}).get("token", "synthetic")
                print('{"type":"SelectCandidate","token":' + json.dumps(token)
                      + ',"token":' + json.dumps(token) + '}', flush=True)
                continue
            elif args.mode == "stale":
                if remembered is None:
                    remembered = decision
                decision = remembered
            print(json.dumps(decision, ensure_ascii=False, separators=(",", ":")), flush=True)
        except (BoundaryError, ValueError, TypeError) as error:
            print(str(error), file=sys.stderr, flush=True)
            return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
