"""Pure state replay and the event operation interpreter."""
from __future__ import annotations

import copy

if __package__:
    from ._case_model import (
        CaseError,
        Conflict,
        TEMPLATE,
        _listify,
        bind,
        expand,
    )
else:
    from _case_model import (
        CaseError,
        Conflict,
        TEMPLATE,
        _listify,
        bind,
        expand,
    )


# ---------------------------------------------------------------- operation interpreter
def _sub(text: str, params: dict) -> str:
    def repl(m):
        if m.group(1) not in params:
            raise CaseError(f"template '{{{m.group(1)}}}' has no bound param")
        return str(params[m.group(1)])
    return TEMPLATE.sub(repl, text)


def _val(v, st, params):
    if isinstance(v, dict):
        if "param" in v:
            return params[v["param"]]
        if "var" in v:
            return st["vars"].get(_sub(v["var"], params))
    return v


def _cond(c, st, params, case) -> bool:
    if not isinstance(c, dict):
        raise CaseError(f"condition must be an object: {c!r}")
    if "all" in c:
        return all(_cond(x, st, params, case) for x in c["all"])
    if "any" in c:
        return any(_cond(x, st, params, case) for x in c["any"])
    if "not" in c:
        return not _cond(c["not"], st, params, case)
    if "has" in c:
        return _sub(c["has"], params) in st["tokens"]
    if "lacks" in c:
        return _sub(c["lacks"], params) not in st["tokens"]
    if "var" in c:
        left = st["vars"].get(_sub(c["var"], params))
    elif "param" in c:
        left = params[c["param"]]
    else:
        raise CaseError(f"condition needs var/param/has/lacks/all/any/not: {c!r}")

    def coll(x):
        return case["sets"][x] if isinstance(x, str) else x

    if "eq" in c:
        return left == _val(c["eq"], st, params)
    if "ne" in c:
        return left != _val(c["ne"], st, params)
    if "in" in c:
        return left in coll(c["in"])
    if "not_in" in c:
        return left not in coll(c["not_in"])
    raise CaseError(f"condition lacks comparator: {c!r}")


def _do(ops, st, params, case):
    for op in ops:
        if "require" in op:
            if not _cond(op["require"], st, params, case):
                raise Conflict(_sub(op["reason"], params), op.get("shown_by", []))
        elif "when" in op:
            branch = op.get("then", []) if _cond(op["when"], st, params, case) else op.get("else", [])
            _do(branch, st, params, case)
        else:
            if "set" in op:
                st["vars"][_sub(op["set"], params)] = _val(op["to"], st, params)
            for t in _listify(op.get("add", [])):
                st["tokens"].add(_sub(t, params))
            for t in _listify(op.get("remove", [])):
                st["tokens"].discard(_sub(t, params))


# ---------------------------------------------------------------- state replay
def observed(case) -> set[str]:
    return {t for t, e in case["evidence"].items() if not e.get("static")}


def new_state(case) -> dict:
    return {"vars": copy.deepcopy(case["initial"]), "tokens": set(), "terminal": None}


def apply_step(case, st, step) -> None:
    if st.get("terminal") is not None:
        raise Conflict(f"terminal event '{st['terminal']}' closes the history; no later step is allowed", [])
    params = bind(case, step)
    _do(case["events"][step[0]].get("do", []), st, params, case)
    if case["events"][step[0]].get("terminal"):
        st["terminal"] = step[0]


def run(case, steps) -> dict:
    steps = expand(case, steps)
    st = new_state(case)
    obs = observed(case)
    for i, step in enumerate(steps):
        try:
            apply_step(case, st, step)
        except Conflict as c:
            return {"status": "conflict", "conflict": {"step": i, "event": step, "reason": c.reason,
                                                        "shown_by": c.shown_by},
                    "generated": sorted(t for t in st["tokens"] if not t.startswith("_")),
                    "unexplained": [], "contradicts": [],
                    "banned": sorted(st["tokens"] & set(case.get("banned", []))), "final_vars": st["vars"]}
    gen = {t for t in st["tokens"] if not t.startswith("_")}
    un, co = sorted(obs - gen), sorted(gen - obs)
    return {"status": "compatible" if not un and not co else "unproven_or_contradicted",
            "conflict": None, "generated": sorted(gen), "unexplained": un, "contradicts": co,
            "banned": sorted(gen & set(case.get("banned", []))), "final_vars": st["vars"]}


def compatible(r) -> bool:
    return r["status"] == "compatible"
