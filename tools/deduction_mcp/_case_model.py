"""Case grammar, structure validation, sequence expansion and event argument binding.

This module is the dependency root: it knows no replay, audit or filesystem services.
"""
from __future__ import annotations

import math
import re

CASE_ID = re.compile(r"^[a-z0-9][a-z0-9_-]{0,63}$")
TEMPLATE = re.compile(r"\{(\w+)\}")
START = "start"


class CaseError(ValueError):
    """The case file or a call argument is malformed."""


class Conflict(Exception):
    def __init__(self, reason: str, shown_by: list[str]):
        super().__init__(reason)
        self.reason, self.shown_by = reason, list(shown_by)


def _listify(x):
    return x if isinstance(x, list) else [x]


# ---------------------------------------------------------------- step grammar
def expand(case, steps, _stack=()) -> list[list]:
    if not isinstance(steps, list):
        raise CaseError("steps/sequence must be a list")
    out = []
    for step in steps:
        if isinstance(step, dict) and "use" in step:
            name = step["use"]
            if set(step) != {"use"} or not isinstance(name, str):
                raise CaseError("sequence reference must be {'use': name}")
            if name in _stack:
                raise CaseError(f"cyclic sequence reference: {' -> '.join((*_stack, name))}")
            seq = case.get("sequences", {}).get(name)
            if seq is None:
                raise CaseError(f"unknown sequence '{name}'")
            out += expand(case, seq, (*_stack, name))
        elif isinstance(step, list) and step and isinstance(step[0], str):
            out.append(step)
        else:
            raise CaseError(f"step must be [event, args...] or {{'use': seq}}: {step!r}")
    return out


def bind(case, step) -> dict:
    name, args = step[0], step[1:]
    if name not in case["events"]:
        raise CaseError(f"unknown event '{name}'")
    params = case["events"][name].get("params", [])
    if len(args) != len(params):
        raise CaseError(f"event '{name}' takes {len(params)} args {[p[0] for p in params]}, got {args}")
    bound = {}
    for (pname, dom), arg in zip(params, args):
        if arg not in case["sets"][dom]:
            raise CaseError(f"event '{name}': {pname}={arg!r} not in set {dom}")
        bound[pname] = arg
    return bound


# ---------------------------------------------------------------- structure validation
def validate(case: dict) -> None:
    """Validate executable structure before replay, audit or a file write."""
    errs: list[str] = []
    if not isinstance(case, dict):
        raise CaseError("case must be an object")
    for key in ("id", "title", "source", "sets", "initial", "evidence", "events", "hypotheses", "rounds"):
        if key not in case:
            errs.append(f"missing top-level key '{key}'")
    if errs:
        raise CaseError("; ".join(errs))
    for key in ("sets", "initial", "evidence", "events", "hypotheses", "predictions", "sequences"):
        value = case.get(key, {})
        if not isinstance(value, dict) or not all(isinstance(k, str) and k for k in value):
            errs.append(f"'{key}' must be an object with nonempty string keys")
    for key in ("rounds", "free_params", "answer_requires_steps", "banned"):
        if not isinstance(case.get(key, []), list):
            errs.append(f"'{key}' must be a list")
    for key in ("id", "title", "source"):
        if not isinstance(case[key], str) or not case[key]:
            errs.append(f"'{key}' must be a nonempty string")
    if isinstance(case["id"], str) and not CASE_ID.fullmatch(case["id"]):
        errs.append("invalid case id")
    if errs:
        raise CaseError("; ".join(errs))

    def minutes(value, where):
        if not (isinstance(value, list) and len(value) == 2
                and all(type(x) in (int, float) and math.isfinite(x) and x >= 0 for x in value)
                and value[0] <= value[1]):
            errs.append(f"{where}: minutes must be [nonnegative minimum, maximum] with minimum <= maximum")

    if "target_minutes" in case:
        minutes(case["target_minutes"], "target_minutes")
    sets = case["sets"]
    for name, values in sets.items():
        if not (isinstance(values, list) and values
                and all(v is None or type(v) in (str, int, float, bool) for v in values)
                and all(not isinstance(v, float) or math.isfinite(v) for v in values)):
            errs.append(f"set '{name}' must be a nonempty list of finite scalar values")
    rounds = []
    for rd in case["rounds"]:
        if not isinstance(rd, dict):
            errs.append("each round must be an object")
            continue
        rid = rd.get("id")
        if not isinstance(rid, str) or not rid or rid == START:
            errs.append("round id must be a nonempty string other than 'start'")
        else:
            rounds.append(rid)
        if not isinstance(rd.get("hypothesis"), str) or rd["hypothesis"] not in case["hypotheses"]:
            errs.append(f"round '{rid}' names an unknown hypothesis")
        if rd.get("expect") not in (None, "refute", "pointer", "answer"):
            errs.append(f"round '{rid}': expect must be refute/pointer/answer")
        if "minutes" in rd:
            minutes(rd["minutes"], f"round '{rid}'")
        if "hint" in rd and not isinstance(rd["hint"], str):
            errs.append(f"round '{rid}': hint must be a string")
    if len(set(rounds)) != len(rounds):
        errs.append("duplicate round id")
    for tok, ev in case["evidence"].items():
        if not isinstance(ev, dict):
            errs.append(f"evidence '{tok}' must be an object")
            continue
        if tok.startswith("_"):
            errs.append(f"evidence '{tok}': '_' prefix is reserved for internal tokens")
        avail = ev.get("available", START)
        if avail != START and avail not in rounds:
            errs.append(f"evidence '{tok}': available '{avail}' is neither 'start' nor a round id")
        for key in ("id", "label", "acquire"):
            if not isinstance(ev.get(key), str) or not ev[key]:
                errs.append(f"evidence '{tok}' needs nonempty '{key}'")
        if "static" in ev and not isinstance(ev["static"], bool):
            errs.append(f"evidence '{tok}': static must be boolean")
    for tok, pr in case.get("predictions", {}).items():
        if not isinstance(pr, dict):
            errs.append(f"prediction '{tok}' must be an object")
            continue
        refs = pr.get("refuted_by", [])
        if not isinstance(refs, list) or not all(isinstance(ref, str) for ref in refs):
            errs.append(f"prediction '{tok}': refuted_by must be a list of evidence names")
            continue
        for ref in pr.get("refuted_by", []):
            if ref not in case["evidence"]:
                errs.append(f"prediction '{tok}' refuted_by unknown evidence '{ref}'")
    for name, ev in case["events"].items():
        if not isinstance(ev, dict):
            errs.append(f"event '{name}' must be an object")
            continue
        for flag in ("terminal", "player_assumption"):
            if flag in ev and not isinstance(ev[flag], bool):
                errs.append(f"event '{name}': {flag} must be boolean")
        params = ev.get("params", [])
        if not isinstance(params, list):
            errs.append(f"event '{name}': params must be a list")
            continue
        pnames = []
        for p in ev.get("params", []):
            if not (isinstance(p, list) and len(p) == 2
                    and all(isinstance(x, str) and x for x in p) and p[1] in sets):
                errs.append(f"event '{name}': param {p!r} must be [name, SET] with SET in sets")
            else:
                pnames.append(p[0])
        if len(set(pnames)) != len(pnames):
            errs.append(f"event '{name}': duplicate parameter name")
        _validate_ops(ev.get("do", []), f"event '{name}'", case, errs, set(pnames))
    for hid, hyp in case["hypotheses"].items():
        if not isinstance(hyp, dict) or not isinstance(hyp.get("steps"), list):
            errs.append(f"hypothesis '{hid}' must be an object with a steps list")
        elif "truth" in hyp and not isinstance(hyp["truth"], bool):
            errs.append(f"hypothesis '{hid}': truth must be boolean")
    truths = [h for h, v in case["hypotheses"].items() if isinstance(v, dict) and v.get("truth")]
    if len(truths) != 1:
        errs.append(f"exactly one hypothesis must have truth=true, found {truths}")
    for fp in case.get("free_params", []):
        if not (isinstance(fp, list) and len(fp) == 2 and all(isinstance(x, str) for x in fp)
                and fp[0] in case["events"] and isinstance(case["events"][fp[0]], dict)
                and isinstance(case["events"][fp[0]].get("params", []), list)
                and fp[1] in [p[0] for p in case["events"][fp[0]].get("params", [])
                             if isinstance(p, list) and len(p) == 2]):
            errs.append(f"free_params entry {fp!r} must name an event and one of its params")
    if not all(isinstance(t, str) and t for t in case.get("banned", [])):
        errs.append("banned must contain nonempty token names")
    if errs:
        raise CaseError("; ".join(errs))
    # Expand all sequences, including unused ones, to reject cycles before a saved case can be audited.
    for seq in case.get("sequences", {}):
        try:
            for step in expand(case, [{"use": seq}]):
                bind(case, step)
        except CaseError as exc:
            errs.append(f"sequence '{seq}': {exc}")
    for hid, hyp in case["hypotheses"].items():
        try:
            for step in expand(case, hyp["steps"]):
                bind(case, step)
        except CaseError as exc:
            errs.append(f"hypothesis '{hid}': {exc}")
    for step in case.get("answer_requires_steps", []):
        try:
            if not isinstance(step, list) or not step or not isinstance(step[0], str):
                raise CaseError("required step must be [event, args...]")
            bind(case, step)
        except CaseError as exc:
            errs.append(f"answer_requires_steps: {exc}")
    if errs:
        raise CaseError("; ".join(errs))


def _validate_name(value, where, errs, params):
    if not isinstance(value, str) or not value:
        errs.append(f"{where}: name must be a nonempty string")
    elif not set(TEMPLATE.findall(value)) <= params:
        errs.append(f"{where}: template refers to an unknown parameter")


def _validate_value(value, where, errs, params):
    if isinstance(value, dict):
        if len(value) != 1 or not ({"var", "param"} & value.keys()):
            errs.append(f"{where}: value reference must be {{'var': name}} or {{'param': name}}")
        elif "var" in value:
            _validate_name(value["var"], where, errs, params)
        elif not isinstance(value["param"], str) or value["param"] not in params:
            errs.append(f"{where}: value refers to an unknown parameter")


def _validate_cond(cond, where, case, errs, params):
    if not isinstance(cond, dict):
        errs.append(f"{where}: condition must be an object")
        return
    if set(cond) in ({"all"}, {"any"}):
        parts = next(iter(cond.values()))
        if not isinstance(parts, list):
            errs.append(f"{where}: all/any needs a list")
        else:
            for part in parts:
                _validate_cond(part, where, case, errs, params)
    elif set(cond) == {"not"}:
        _validate_cond(cond["not"], where, case, errs, params)
    elif set(cond) in ({"has"}, {"lacks"}):
        _validate_name(next(iter(cond.values())), where, errs, params)
    else:
        selectors = set(cond) & {"var", "param"}
        comparisons = set(cond) & {"eq", "ne", "in", "not_in"}
        if len(selectors) != 1 or len(comparisons) != 1 or len(cond) != 2:
            errs.append(f"{where}: condition needs exactly one var/param and one comparator")
            return
        selector, comparison = next(iter(selectors)), next(iter(comparisons))
        _validate_value({selector: cond[selector]}, where, errs, params)
        value = cond[comparison]
        if comparison in ("in", "not_in"):
            if not ((isinstance(value, str) and value in case["sets"]) or isinstance(value, list)):
                errs.append(f"{where}: in/not_in needs a registered set or a list")
        else:
            _validate_value(value, where, errs, params)


def _validate_ops(ops, where, case, errs, params):
    if not isinstance(ops, list):
        errs.append(f"{where}: 'do' must be a list")
        return
    for op in ops:
        if not isinstance(op, dict):
            errs.append(f"{where}: op {op!r} is not an object")
        elif "require" in op:
            if set(op) - {"require", "reason", "shown_by"}:
                errs.append(f"{where}: require contains unsupported or mixed operations")
            _validate_cond(op["require"], where, case, errs, params)
            if not isinstance(op.get("reason"), str) or not op["reason"]:
                errs.append(f"{where}: require needs a nonempty reason")
            else:
                _validate_name(op["reason"], where, errs, params)
            refs = op.get("shown_by", [])
            if not isinstance(refs, list) or not all(isinstance(t, str) for t in refs):
                errs.append(f"{where}: shown_by must be a list of evidence names")
                continue
            for ev in refs:
                if ev not in case["evidence"]:
                    errs.append(f"{where}: shown_by unknown evidence '{ev}'")
        elif "when" in op:
            if set(op) - {"when", "then", "else"} or "then" not in op:
                errs.append(f"{where}: when needs then and cannot mix with other operations")
            _validate_cond(op["when"], where, case, errs, params)
            _validate_ops(op.get("then", []), where, case, errs, params)
            _validate_ops(op.get("else", []), where, case, errs, params)
        else:
            if not ({"set", "add", "remove"} & op.keys()) or set(op) - {"set", "to", "add", "remove"}:
                errs.append(f"{where}: unknown op {op!r}")
            if "set" in op:
                _validate_name(op["set"], where, errs, params)
                if "to" not in op:
                    errs.append(f"{where}: set needs to")
                else:
                    _validate_value(op["to"], where, errs, params)
            elif "to" in op:
                errs.append(f"{where}: to requires set")
            for key in ("add", "remove"):
                if key in op:
                    for token in _listify(op[key]):
                        _validate_name(token, where, errs, params)
