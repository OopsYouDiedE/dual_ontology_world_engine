"""Declarative deduction-case engine: replay, detectability, round balance, identifiability.

A case file (cases/<id>.json) is the machine-readable form of one case's author-side logic.
docs/design-standard.md stays the prose master; each case cites its section in "source".
Nothing here runs Core, Godot, or a playtest. Results are design-rule checks only.

Model
-----
state   = vars (dict) + tokens (set). Tokens starting with "_" are internal bookkeeping.
event   = ordered params with a domain set, and a "do" list of ops:
          {"require": cond, "reason": str, "shown_by": [evidence]}  -> Conflict if false
          {"set": var, "to": value} / {"add": token|[..]} / {"remove": token|[..]}
          {"when": cond, "then": [ops], "else": [ops]}
event.terminal = true closes the history after that event; subsequent steps conflict.
cond    = {"all"|"any": [cond]} | {"not": cond} | {"has"|"lacks": token}
          | {"var"|"param": name, "eq"|"ne"|"in"|"not_in": value}
value   = literal | {"param": name} | {"var": name};  "{param}" is substituted in var/token names.

evidence.available = "start" or the id of the round after whose failure it becomes obtainable.
evidence.static = true marks fixed facts (mechanics, plaques) that only serve as shown_by/refuted_by.
round.expect = "refute" | "pointer" | "answer" states the designed role; mismatches are reported.

A hypothesis is a step list. Its replay generates tokens; against the case's observed evidence we
get unexplained (observed, not generated) and contradicted predictions (generated, not observed).
A player can only *detect* a failure when the evidence it needs is available at that round.
"""

from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path

# Retain the existing engine API while the storage/patch layer owns all I/O.
if __package__:
    from ._case_model import (
        CASE_ID, TEMPLATE, START, CaseError, Conflict,
        _listify, expand, bind, validate,
        _validate_name, _validate_value, _validate_cond, _validate_ops,
    )
    from ._replay import (
        _sub, _val, _cond, _do,
        observed, new_state, apply_step, run, compatible,
    )
    from ._analysis import (
        availability, detect, pointer, _norm,
        identifiability, search_alternatives, audit, diagnose,
    )
else:
    from _case_model import (
        CASE_ID, TEMPLATE, START, CaseError, Conflict,
        _listify, expand, bind, validate,
        _validate_name, _validate_value, _validate_cond, _validate_ops,
    )
    from _replay import (
        _sub, _val, _cond, _do,
        observed, new_state, apply_step, run, compatible,
    )
    from _analysis import (
        availability, detect, pointer, _norm,
        identifiability, search_alternatives, audit, diagnose,
    )

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
CASES = HERE / "cases"
REPORTS = ROOT / "artifacts" / "deduction-balance"


# ---------------------------------------------------------------- loading
def case_path(case_id: str) -> Path:
    if not isinstance(case_id, str) or not CASE_ID.match(case_id):
        raise CaseError(f"case id must match {CASE_ID.pattern}: {case_id!r}")
    return CASES / f"{case_id}.json"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def load(case_id: str) -> tuple[dict, str]:
    path = case_path(case_id)
    if not path.is_file():
        raise CaseError(f"no case file {path.relative_to(ROOT).as_posix()}; list_cases shows available ids")
    raw = path.read_bytes()
    case = json.loads(raw.decode("utf-8"))
    validate(case)
    return case, sha(raw)


def list_cases() -> list[dict]:
    out = []
    for path in sorted(CASES.glob("*.json")):
        raw = path.read_bytes()
        try:
            case = json.loads(raw.decode("utf-8"))
            validate(case)
            out.append({"id": path.stem, "title": case.get("title"), "source": case.get("source"),
                        "sha256": sha(raw), "valid": True})
        except (ValueError, KeyError) as exc:
            out.append({"id": path.stem, "sha256": sha(raw), "valid": False, "error": str(exc)})
    return out


# ---------------------------------------------------------------- patches
def _pointer_parts(path: str) -> list:
    if not path.startswith("/"):
        raise CaseError(f"path must be a JSON pointer starting with '/': {path!r}")
    return [p.replace("~1", "/").replace("~0", "~") for p in path[1:].split("/")]


def apply_ops(case: dict, ops: list) -> dict:
    doc = copy.deepcopy(case)
    for op in ops:
        kind, parts = op.get("op"), _pointer_parts(op.get("path", ""))
        parent = doc
        for p in parts[:-1]:
            parent = parent[int(p)] if isinstance(parent, list) else parent.setdefault(p, {})
        last = parts[-1]
        if kind == "set":
            if isinstance(parent, list):
                parent[int(last)] = op["value"]
            else:
                parent[last] = op["value"]
        elif kind == "remove":
            if isinstance(parent, list):
                del parent[int(last)]
            else:
                parent.pop(last)
        elif kind == "append":
            target = parent[int(last)] if isinstance(parent, list) else parent.setdefault(last, [])
            if not isinstance(target, list):
                raise CaseError(f"append target {op['path']} is not a list")
            target.append(op["value"])
        else:
            raise CaseError(f"op must be set/remove/append: {op!r}")
    validate(doc)
    return doc


# ---------------------------------------------------------------- audit comparison
def _digest(a: dict) -> dict:
    return {"summary": a["summary"], "minutes": a["minutes"],
            "rounds": {r["round"]: r["kind"] for r in a["rounds"]},
            "findings": sorted({(f["level"], f["code"], f["where"]) for f in a["findings"]})}


def compare(before: dict, after: dict) -> dict:
    b, a = _digest(before), _digest(after)
    return {"summary": {"before": b["summary"], "after": a["summary"]},
            "minutes": {"before": b["minutes"], "after": a["minutes"]},
            "round_kind_changes": {r: [b["rounds"].get(r), a["rounds"].get(r)] for r in set(b["rounds"]) | set(a["rounds"])
                                   if b["rounds"].get(r) != a["rounds"].get(r)},
            "new_findings": [list(x) for x in sorted(set(a["findings"]) - set(b["findings"]))],
            "resolved_findings": [list(x) for x in sorted(set(b["findings"]) - set(a["findings"]))],
            "regressed": a["summary"]["error"] > b["summary"]["error"]}


# ---------------------------------------------------------------- persistence
def write_case(case_id: str, doc: dict, expected_sha: str | None) -> str:
    path = case_path(case_id)
    if doc.get("id") != case_id:
        raise CaseError(f"case 'id' field must equal file id '{case_id}'")
    if path.exists():
        current = sha(path.read_bytes())
        if expected_sha != current:
            raise CaseError(f"case file changed (sha {current}); re-read before writing")
    elif expected_sha is not None:
        raise CaseError("case file does not exist; pass expected_sha=null to create it")
    raw = (json.dumps(doc, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    tmp = path.with_suffix(".json.tmp")
    CASES.mkdir(parents=True, exist_ok=True)
    tmp.write_bytes(raw)
    tmp.replace(path)
    return sha(raw)


def write_report(case_id: str, report: dict) -> str:
    REPORTS.mkdir(parents=True, exist_ok=True)
    out = REPORTS / f"{case_id}.json"
    out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return out.relative_to(ROOT).as_posix()
