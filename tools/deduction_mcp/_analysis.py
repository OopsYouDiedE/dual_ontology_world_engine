"""Evidence visibility, hypothesis audits and bounded historical search.

Depends only on case grammar and replay; never reads or writes case files.
"""
from __future__ import annotations

import collections
import copy
import itertools
import json

if __package__:
    from ._case_model import (
        CaseError,
        Conflict,
        START,
        expand,
    )
    from ._replay import (
        apply_step,
        compatible,
        new_state,
        observed,
        run,
    )
else:
    from _case_model import (
        CaseError,
        Conflict,
        START,
        expand,
    )
    from _replay import (
        apply_step,
        compatible,
        new_state,
        observed,
        run,
    )


# ---------------------------------------------------------------- detectability
def availability(case) -> list[tuple[str, set, set]]:
    """[(round id, evidence available before the round, evidence unlocked by the round)]."""
    avail = {t for t, e in case["evidence"].items() if e.get("available", START) == START}
    out = []
    for rd in case["rounds"]:
        unlock = {t for t, e in case["evidence"].items() if e.get("available") == rd["id"]}
        out.append((rd["id"], set(avail), unlock))
        avail |= unlock
    return out


def detect(case, r, avail: set) -> tuple[list, list]:
    """Split a replay's failure items into those the player can see now and those still hidden."""
    found, hidden = [], []
    ev = case["evidence"]
    if r["conflict"]:
        c = r["conflict"]
        need = set(c["shown_by"])
        item = {"kind": "conflict", "step": c["step"], "event": c["event"], "reason": c["reason"],
                "evidence": sorted(need), "missing": sorted(need - avail)}
        (found if need and need <= avail else hidden).append(item)
    for t in r["unexplained"]:
        item = {"kind": "unexplained", "evidence": [t], "label": ev[t]["label"], "missing": [] if t in avail else [t]}
        (found if t in avail else hidden).append(item)
    for t in r["contradicts"]:
        ref = set(case.get("predictions", {}).get(t, {}).get("refuted_by", []))
        hit = ref & avail
        item = {"kind": "contradicted_prediction", "prediction": t,
                "label": case.get("predictions", {}).get(t, {}).get("label", "未登记的预测痕迹"),
                "evidence": sorted(hit or ref), "missing": [] if hit else sorted(ref)}
        (found if hit else hidden).append(item)
    return found, hidden


def pointer(case, items) -> list[dict]:
    """Turn hidden failure items into concrete 'go and check' instructions."""
    seen, out = set(), []
    for it in items:
        for t in it.get("missing", []):
            if t in seen:
                continue
            seen.add(t)
            e = case["evidence"][t]
            out.append({"evidence": t, "id": e["id"], "label": e["label"], "acquire": e["acquire"],
                        "available": e.get("available", START)})
    return out


# ---------------------------------------------------------------- identifiability and search
def _norm(case, step):
    free = {tuple(x) for x in case.get("free_params", [])}
    params = case["events"][step[0]].get("params", [])
    return (step[0],) + tuple("*" if (step[0], p[0]) in free else a for p, a in zip(params, step[1:]))


def identifiability(case) -> dict:
    """Which truth steps, actors and orderings the evidence actually pins down."""
    truth_id = next(h for h, v in case["hypotheses"].items() if v.get("truth"))
    steps = expand(case, case["hypotheses"][truth_id]["steps"])
    n = len(steps)
    ok = lambda s: compatible(run(case, s))
    removable = [i for i in range(n) if ok(steps[:i] + steps[i + 1:])]
    # A pair counts when at least one member is needed alone: e.g. "pin_out" is needed only
    # because an unevidenced "pin_in" came first, so the two together leave no trace.
    pairs = [[i, j] for i, j in itertools.combinations(range(n), 2)
             if not (i in removable and j in removable) and ok([s for k, s in enumerate(steps) if k not in (i, j)])]
    actors = []
    free = {tuple(x) for x in case.get("free_params", [])}
    for i, step in enumerate(steps):
        params = case["events"][step[0]].get("params", [])
        for k, (pname, dom) in enumerate(params):
            alts = [v for v in case["sets"][dom] if v != step[k + 1]
                    and ok(steps[:i] + [step[:k + 1] + [v] + step[k + 2:]] + steps[i + 1:])]
            if alts:
                actors.append({"step": i, "event": step, "param": pname, "also_compatible": alts,
                               "accepted_free": (step[0], pname) in free})
    swaps = [{"steps": [i, i + 1], "events": [steps[i], steps[i + 1]]} for i in range(n - 1)
             if ok(steps[:i] + [steps[i + 1], steps[i]] + steps[i + 2:])]
    return {"truth": truth_id, "steps": steps, "unevidenced_steps": [{"step": i, "event": steps[i]} for i in removable],
            "unevidenced_pairs": [{"steps": p, "events": [steps[p[0]], steps[p[1]]]} for p in pairs],
            "undetermined_params": actors, "undetermined_adjacent_order": swaps}


def search_alternatives(case, max_depth: int | None = None, max_states: int = 200_000) -> dict:
    """Bounded breadth-first search for other action multisets matching the observed evidence.

    No-op actions are pruned; states include the normalized action multiset, preserving repetitions.
    Reorderings with the same multiset and world state collapse. free_params normalize to '*'.
    """
    truth_id = next(h for h, v in case["hypotheses"].items() if v.get("truth"))
    truth = expand(case, case["hypotheses"][truth_id]["steps"])
    if max_depth is not None and (type(max_depth) is not int or max_depth < 1):
        raise CaseError("max_depth must be a positive integer")
    if type(max_states) is not int or max_states < 1:
        raise CaseError("max_states must be a positive integer")
    depth = max_depth if max_depth is not None else len(truth) + 2
    actions = []
    for name, ev in case["events"].items():
        if ev.get("player_assumption"):
            continue  # player-side premises are not historical actions
        doms = [case["sets"][d] for _, d in ev.get("params", [])]
        for args in itertools.product(*doms):
            actions.append([name, *args])
    obs = observed(case)
    def action_key(step):
        return json.dumps(_norm(case, step), ensure_ascii=False, separators=(",", ":"))

    truth_sig = tuple(sorted(action_key(s) for s in truth))

    def key(st, used):
        return (json.dumps(st["vars"], sort_keys=True, ensure_ascii=False), frozenset(st["tokens"]),
                st.get("terminal"), used)

    start = new_state(case)
    frontier = [(start, (), [])]
    seen = {key(start, ())}
    found: dict[tuple, list] = {}
    truncated = False
    for _ in range(depth):
        nxt = []
        for st, used, path in frontier:
            for act in actions:
                trial = copy.deepcopy(st)
                try:
                    apply_step(case, trial, act)
                except Conflict:
                    continue
                if trial == st:
                    continue
                u2 = tuple(sorted((*used, action_key(act))))
                k = key(trial, u2)
                if k in seen:
                    continue
                seen.add(k)
                if len(seen) > max_states:
                    truncated = True
                    break
                p2 = path + [act]
                if {t for t in trial["tokens"] if not t.startswith("_")} == obs and u2 not in found:
                    found[u2] = p2
                nxt.append((trial, u2, p2))
            if truncated:
                break
        frontier = nxt
        if truncated or not frontier:
            break
    alts = []
    for sig, path in found.items():
        if sig == truth_sig:
            continue
        extra = collections.Counter(sig) - collections.Counter(truth_sig)
        missing = collections.Counter(truth_sig) - collections.Counter(sig)
        alts.append({"path": path, "extra_vs_truth": [json.loads(s) for s in sorted(extra.elements())],
                     "missing_vs_truth": [json.loads(s) for s in sorted(missing.elements())]})
    alts.sort(key=lambda a: (len(a["extra_vs_truth"]) + len(a["missing_vs_truth"]), len(a["path"])))
    return {"depth": depth, "states": len(seen), "truncated": truncated, "alternatives": alts,
            "scope": "Bounded registered-event search; compares normalized action multisets, including counts. "
                     "Equivalent orderings reaching the same state are merged. No-op steps and "
                     "player_assumption events are excluded; free_params are treated as equivalent.",
            "uniqueness_proven": False,
            "note": "Empty alternatives does not prove historical uniqueness; depth/state limits, equivalence "
                    "rules and the registered event model restrict the search."}


# ---------------------------------------------------------------- audit
def audit(case) -> dict:
    findings = []

    def add(level, code, where, message, fix=None, **detail):
        findings.append({"level": level, "code": code, "where": where, "message": message,
                         **({"fix": fix} if fix else {}), **detail})

    hyps = case["hypotheses"]
    def missing_refutations(ops, event):
        for i, op in enumerate(ops):
            if "require" in op and not op.get("shown_by"):
                add("warning", "require_without_evidence", f"{event}#{i}",
                    "前提失败未登记 shown_by，不能作为玩家可见的否定。", "登记能够核对该前提的物证。")
            if "when" in op:
                missing_refutations(op.get("then", []), f"{event}#{i}/then")
                missing_refutations(op.get("else", []), f"{event}#{i}/else")

    for event, definition in case["events"].items():
        missing_refutations(definition.get("do", []), event)
    results = {h: run(case, v["steps"]) for h, v in hyps.items()}
    truth_id = next(h for h, v in hyps.items() if v.get("truth"))
    tr = results[truth_id]
    if not compatible(tr):
        add("error", "truth_incompatible", truth_id, "真相回放未恰好生成全部观察证据。",
            "按 conflict/unexplained/contradicts 修改真相步骤或证据登记，使真相成为可执行且完全吻合的经过。",
            conflict=tr["conflict"], unexplained=tr["unexplained"], contradicts=tr["contradicts"])
    for h, r in results.items():
        if r["banned"]:
            add("error", "banned_trace", h, f"生成了禁止出现的痕迹 {r['banned']}。",
                "删除产生这些痕迹的效果，或把它们改为不暴露禁区信息的痕迹。")
        if h != truth_id and compatible(r):
            add("error", "ambiguous_competitor", h, "竞争假说与真相同样恰好生成全部观察证据，答案不唯一。",
                "新增一项只由真相或只由该假说产生的物证，并登记其获取方式；或把该假说并入等价解。")
    obs = observed(case)
    preds = case.get("predictions", {})
    for h, r in results.items():
        for t in r["contradicts"]:
            if t not in preds:
                add("warning", "unregistered_prediction", h, f"'{t}' 不是物证也未登记为预测痕迹，玩家无法据此否定。",
                    f"在 predictions 中登记 '{t}' 并给出 refuted_by。")
    all_ev = set(case["evidence"])
    for h, r in results.items():
        if h == truth_id:
            continue
        found, hidden = detect(case, r, all_ev)
        if not found and not compatible(r):
            add("error", "undetectable_competitor", h, "证据全部到手后，该假说的失败仍无法被玩家看见。",
                "为 hidden 中的失败补 shown_by 或 refuted_by 物证。", hidden=hidden)

    rounds_out = []
    used_by_refutation: dict[str, int] = {}
    avail_table = availability(case)
    for (rid, before, unlock), rd in zip(avail_table, case["rounds"]):
        h = rd["hypothesis"]
        r = results[h]
        after = before | unlock
        fb, hb = detect(case, r, before)
        fa, ha = detect(case, r, after)
        entry = {"round": rid, "hypothesis": h, "label": hyps[h].get("label"), "unlocks": sorted(unlock),
                 "minutes": rd.get("minutes"), "hint": rd.get("hint")}
        if h == truth_id:
            entry["kind"] = "answer"
            if fa:
                add("error", "truth_refuted_in_round", rid, "真相在本轮可见证据下被判失败。", "检查 shown_by/refuted_by 登记。", items=fa)
        elif fb:
            entry["kind"] = "refute"
            entry["feedback"] = fb
            if unlock:
                entry["next_pointer"] = pointer(case, [{"missing": sorted(unlock)}])
                if not rd.get("hint"):
                    add("warning", "unlock_without_hint", rid, "本轮否定后解锁新证据，但未写引导台词/提示。",
                        "在 round.hint 写出同伴或系统怎样把玩家引到解锁证据（对应 next_pointer）。")
            else:
                entry["note"] = "整合轮：只用已持有证据否定，不解锁新证据。"
        elif fa:
            entry["kind"] = "pointer"
            entry["feedback_needs"] = pointer(case, [dict(i, missing=sorted(set(i["evidence"]) - before))
                                                   for i in fa])
            entry["feedback"] = fa
            if not rd.get("hint"):
                add("warning", "pointer_without_hint", rid, "该轮假说在已有证据下相容，要靠反馈指向未核对位置，但未写提示。",
                    "在 round.hint 写出检视系统/同伴对 feedback_needs 所列位置的指引。")
        else:
            entry["kind"] = "stuck"
            add("error", "round_stuck", rid, f"{h} 在本轮前后可得证据下都不能被否定，玩家会卡住或以错误答案通过。",
                "把 hidden 中 missing 的证据设为本轮解锁（available=本轮 id），或在该假说的失败处补 shown_by。",
                hidden=ha)
        for it in (fb or fa):
            for t in it.get("evidence", []):
                used_by_refutation[t] = used_by_refutation.get(t, 0) + 1
        want = rd.get("expect")
        if want and want != entry["kind"]:
            add("warning", "round_kind_mismatch", rid,
                f"设计写的是 {want}，模型得出 {entry['kind']}。",
                {"pointer": "本轮假说在进轮前已可见失败（见 feedback），它并不像设计说的那样“看起来完整”。"
                            "让该假说也能解释 feedback 中的证据，或把这些证据推迟到本轮之后才可得。",
                 "refute": "本轮假说的失败要到新证据解锁后才看得见。把所需证据提前，或把本轮设计改为指引轮（pointer）。"
                 }.get(want, "按 feedback 调整本轮假说或证据可得时点。"))
        entry["survivors_after"] = sorted(x for x, rr in results.items()
                                          if x != truth_id and not detect(case, rr, after)[0])
        rounds_out.append(entry)
    if case["rounds"] and case["rounds"][-1]["hypothesis"] != truth_id:
        add("warning", "last_round_not_truth", case["rounds"][-1]["id"], "最后一轮不是真相答辩。", "把真相放在最后一轮。")
    kinds = [e["kind"] for e in rounds_out]
    for i in range(1, len(kinds)):
        if kinds[i] == "refute" and kinds[i - 1] == "refute" and not rounds_out[i - 1]["unlocks"] and not rounds_out[i]["unlocks"]:
            add("info", "consecutive_integration", rounds_out[i]["round"], "连续两轮都只用已有证据否定，节奏可能拖沓。",
                "考虑合并其一，或让其中一轮解锁新证据。")
    for t, e in case["evidence"].items():
        if e.get("static"):
            continue
        if t not in used_by_refutation:
            add("info", "evidence_never_refutes", t, f"{e['id']} {e['label']} 在各轮中未参与否定任何假说，只由真相解释。",
                "确认它是答辩所需的正面证据；若只是装饰，考虑删去或给它一个否定角色。")
    lo = sum((rd.get("minutes") or [0, 0])[0] for rd in case["rounds"])
    hi = sum((rd.get("minutes") or [0, 0])[1] for rd in case["rounds"])
    target = case.get("target_minutes")
    if target and (lo < target[0] or hi > target[1]):
        add("warning", "time_budget", "rounds", f"推演估时 {lo}–{hi} 分钟，目标 {target[0]}–{target[1]}。",
            "调整轮次数量或单轮估时；估时本身须以真人试玩修正。")
    ident = identifiability(case) if compatible(tr) else None
    if ident:
        demanded = set(map(tuple, case.get("answer_requires_steps", [])))
        for u in ident["unevidenced_steps"]:
            lvl = "error" if tuple(u["event"]) in demanded else "info"
            add(lvl, "unevidenced_step", f"{truth_id}#{u['step']}", f"删去真相第 {u['step']} 步 {u['event']} 仍完全吻合：该步没有物证足迹。",
                "答辩不能要求该步；若必须要求，为它增加痕迹或证言。")
        for p in ident["unevidenced_pairs"]:
            hit = any(tuple(e) in demanded for e in p["events"])
            add("error" if hit else "info", "unevidenced_pair", f"{truth_id}#{p['steps']}",
                f"同时删去 {p['events']} 仍吻合：这对步骤作为整体没有物证足迹。",
                "答辩不能要求这对步骤；若必须要求，为其中一步增加痕迹或证言。")
        for a in ident["undetermined_params"]:
            if not a["accepted_free"]:
                hit = tuple(a["event"]) in demanded
                add("warning" if hit else "info", "undetermined_param", f"{truth_id}#{a['step']}",
                    f"{a['event']} 的 {a['param']} 换成 {a['also_compatible']} 也吻合。",
                    "若答辩要求指认此人，增加区分证据；否则在 free_params 登记为等价解。")
        for s in ident["undetermined_adjacent_order"]:
            add("info", "undetermined_order", f"{truth_id}#{s['steps']}", f"{s['events'][0]} 与 {s['events'][1]} 对调仍吻合：先后不由证据确定。",
                "答辩不应要求这对先后；需要时增加能区分先后的层次痕迹。")
    summary = {lvl: sum(f["level"] == lvl for f in findings) for lvl in ("error", "warning", "info")}
    return {"case": case["id"], "source": case["source"], "summary": summary,
            "uniqueness": {"scope": "registered_hypotheses_only", "checked": sorted(results),
                           "compatible": sorted(h for h, r in results.items() if compatible(r)),
                           "historical_uniqueness_proven": False},
            "minutes": [lo, hi], "target_minutes": target, "rounds": rounds_out,
            "hypotheses": {h: {k: v for k, v in r.items() if k != "final_vars"} for h, r in results.items()},
            "identifiability": ident, "findings": findings,
            "limits": ["规则设计的抽象校验，不运行 Core/Godot，不代表实机或真人试玩结果。",
                       "唯一性检查只比较已登记假说；未穷尽所有可能历史，不能据此证明历史唯一。",
                       "可检测性只按登记的 shown_by/refuted_by 计算；玩家是否真的想到，需要试玩。",
                       "冲突后的步骤不再回放；被隐藏的冲突之后的痕迹不计入。"]}


# ---------------------------------------------------------------- player diagnosis
def diagnose(case, steps, round_id: str | None = None) -> dict:
    """Replay an arbitrary player hypothesis and say what the player can see and where to look next."""
    r = run(case, steps)
    table = availability(case)
    if round_id is None:
        avail = set(case["evidence"])
    else:
        row = next((t for t in table if t[0] == round_id), None)
        if row is None:
            raise CaseError(f"unknown round '{round_id}'")
        avail = row[1]
    found, hidden = detect(case, r, avail)
    return {"replay": {k: v for k, v in r.items() if k != "final_vars"}, "final_vars": r["final_vars"],
            "visible_failures": found, "hidden_failures": hidden, "next_pointer": pointer(case, hidden),
            "verdict": ("相容：当前证据无法否定" if not found and compatible(r) else
                        "当前可见失败" if found else "表面相容：失败尚被隐藏，需按 next_pointer 补查")}
