#!/usr/bin/env python3
"""角色数值自动优化流水线（说服 / 采纳参数）。

  python tools/tuning/character_tuning.py all [--generations 80] [--seed 7] [--steps 1800] [--publish]
  python tools/tuning/character_tuning.py extract | optimize | validate --candidate C | publish --candidate C --confirm

流程：extract（从正式 .dv 读出人物意见参数与全部已声明提议）→ optimize（逐人物 (1+λ) 进化策略）
→ validate（真实 CLI 宿主，同种子同步数，基线与候选对照）→ publish（显式确认后写回 .dv）。
工具输出先存为候选（.runtime/tuning/<run>/），通过验证并明确发布后才进入游戏资源。

评估模型与 core/simulation/WorldRules.cs 的 EvaluateSuggestion 逐项一致：
  score = trust[说话者]（缺省 base_trust）+ evidence_weight×出示证据数 − cost_aversion×cost − risk_aversion×risk
        + Σ values[维度]×appeal[维度] + corroboration_weight×与本人记忆一致数 − contradiction_weight×与本人记忆矛盾数
  采纳 ⇔（证据齐全 或 partial_evidence 且至少一条）且 score ≥ threshold
目标函数（越小越好）：
  1 剧情安全（硬约束）：每条已声明提议在“证据齐全、无矛盾”时的采纳结论与基线相同；
  2 余量合理：采纳者余量落在 [0.15, 0.9]，拒绝者余量 ≤ −0.1；
  3 认知一致：重视诚实 / 理性的人物在出现一条与本人记忆矛盾的证据时应转为拒绝；
  4 人格贴合：参数靠近由人物价值倾向推出的先验（不再全员同一套参数）。
"""
import argparse, copy, datetime, json, math, pathlib, random, shutil, statistics, subprocess, sys, time

ROOT = pathlib.Path(__file__).resolve().parents[2]
SCENARIOS = ROOT / "assets/data/scenarios"
OUT = ROOT / ".runtime/tuning"
CLI_DLL = ROOT / "core/session_cli/bin/Debug/net8.0/DuelView.Cli.dll"
PARAMS = {"base_trust": (0.0, 0.8), "threshold": (0.9, 2.6), "evidence_weight": (0.2, 0.9), "cost_aversion": (0.2, 1.4),
          "risk_aversion": (0.4, 2.0), "corroboration_weight": (0.0, 0.5), "contradiction_weight": (0.0, 1.0)}
DEFAULTS = {"base_trust": 0.0, "threshold": 1.0, "evidence_weight": 0.4, "cost_aversion": 1.0, "risk_aversion": 1.0,
            "corroboration_weight": 0.0, "contradiction_weight": 0.0}
PRIOR_BASE = {"corroboration_weight": 0.12, "contradiction_weight": 0.25}
EFFECTS = {  # 价值维度（权重超过 1 的部分）对参数先验的影响
    "honesty": {"evidence_weight": 0.10, "contradiction_weight": 0.45}, "reason": {"evidence_weight": 0.12, "contradiction_weight": 0.40, "base_trust": -0.05},
    "care": {"base_trust": 0.12, "threshold": -0.12, "cost_aversion": -0.10}, "solidarity": {"base_trust": 0.14, "threshold": -0.10},
    "autonomy": {"threshold": 0.30, "base_trust": -0.08}, "recognition": {"threshold": 0.14, "risk_aversion": -0.12},
    "safety": {"risk_aversion": 0.40, "cost_aversion": 0.06}, "duty": {"cost_aversion": -0.18, "threshold": -0.05},
    "fairness": {"evidence_weight": 0.06, "corroboration_weight": 0.10}, "craft": {"evidence_weight": 0.08, "corroboration_weight": 0.12},
    "curiosity": {"risk_aversion": -0.15, "base_trust": 0.04}, "faith": {"base_trust": 0.10, "contradiction_weight": -0.10},
    "control": {"threshold": 0.22, "risk_aversion": 0.10}, "loyalty": {"base_trust": 0.10, "corroboration_weight": 0.05},
    "pride": {"threshold": 0.18}, "survival": {"risk_aversion": 0.30, "cost_aversion": 0.10},
}

def load_dv(path):
    text = path.read_text(encoding="utf-8"); at = text.find('"value"')
    if at < 0: return None, text
    try: data, _ = json.JSONDecoder().raw_decode(text, text.find("{", text.find(":", at)))
    except ValueError: return None, text
    return data, text

def opinion_spans(text):
    spans, decoder, at = [], json.JSONDecoder(), 0
    while True:
        at = text.find('"opinion"', at)
        if at < 0: return spans
        start = text.find("{", text.find(":", at)); obj, end = decoder.raw_decode(text, start)
        spans.append((start, end, obj)); at = end

def extract():
    actors, suggestions = {}, []
    for path in sorted(SCENARIOS.glob("*.dv")):
        data, _ = load_dv(path)
        if not isinstance(data, dict): continue
        for actor in data.get("actors") or []:
            if not isinstance(actor, dict): continue
            aid = actor.get("id"); opinion = actor.get("opinion")
            if aid and isinstance(opinion, dict):
                entry = actors.setdefault(aid, {"id": aid, "name": actor.get("name") or aid, "opinion": opinion, "files": []})
                entry["files"].append(path.name)
                if actor.get("name"): entry["name"] = actor["name"]
            for action in actor.get("actions") or []:
                proposal = action.get("suggestion") if isinstance(action, dict) else None
                if isinstance(proposal, dict) and proposal.get("target"):
                    suggestions.append({"file": path.name, "action": action.get("id"), "label": action.get("label", ""),
                                        "speaker": aid or str(action.get("id", "")).split(".")[0], "target": proposal["target"],
                                        "evidence": sorted(set(proposal.get("evidence") or [])), "appeal": proposal.get("appeal") or {},
                                        "cost": float(proposal.get("cost") or 0), "risk": float(proposal.get("risk") or 0)})
    unique = {}
    for s in suggestions: unique.setdefault((s["action"], s["target"]), s)
    return {"actors": actors, "suggestions": list(unique.values())}

def param(op, key): return float(op.get(key, DEFAULTS[key]))

def score(op, s, supplied, corroborated=0, contradicted=0):
    trust = float((op.get("trust") or {}).get(s["speaker"], param(op, "base_trust")))
    total = trust + param(op, "evidence_weight") * supplied - param(op, "cost_aversion") * s["cost"] - param(op, "risk_aversion") * s["risk"]
    values = op.get("values") or {}
    total += sum(float(values.get(k, 0)) * float(v) for k, v in s["appeal"].items())
    return total + param(op, "corroboration_weight") * corroborated - param(op, "contradiction_weight") * contradicted

def adopts(op, s, supplied, **kw):
    sufficient = supplied >= len(s["evidence"]) or (bool(op.get("partial_evidence")) and supplied > 0)
    return sufficient and score(op, s, supplied, **kw) >= param(op, "threshold")

def prior_for(op):
    prior = {k: param(op, k) for k in PARAMS}; prior.update(PRIOR_BASE)
    for dimension, weight in (op.get("values") or {}).items():
        for key, delta in EFFECTS.get(dimension, {}).items(): prior[key] += delta * max(0.0, float(weight) - 0.6)
    return {k: min(max(v, PARAMS[k][0]), PARAMS[k][1]) for k, v in prior.items()}

def merged(op, x):
    out = copy.deepcopy(op); out.update({k: round(v, 3) for k, v in x.items()}); return out

def objective(op_base, x, prior, targeted):
    op = merged(op_base, x); loss = 0.0
    for s in targeted:
        n = len(s["evidence"]); before = adopts(op_base, s, n); after = adopts(op, s, n)
        if before != after: loss += 100.0
        margin = score(op, s, n) - param(op, "threshold")
        if after:
            loss += 4 * max(0.0, 0.15 - margin) ** 2 + 2 * max(0.0, margin - 0.9) ** 2
            if prior["contradiction_weight"] >= 0.5 and n > 0:
                loss += 3 * max(0.0, score(op, s, n, contradicted=1) - param(op, "threshold") + 0.05)
        else:
            loss += 4 * max(0.0, margin + 0.1) ** 2
    return loss + 6 * sum(((x[k] - prior[k]) / (hi - lo)) ** 2 for k, (lo, hi) in PARAMS.items())

def optimize_actor(op_base, targeted, rng, generations, lam=8):
    prior = prior_for(op_base); x = dict(prior); fx = objective(op_base, x, prior, targeted); sigma = 0.15; history = [round(fx, 4)]
    for _ in range(generations):
        best, best_f, wins = None, math.inf, 0
        for _ in range(lam):
            child = {k: min(max(x[k] + rng.gauss(0, sigma * (hi - lo)), lo), hi) for k, (lo, hi) in PARAMS.items()}
            f = objective(op_base, child, prior, targeted)
            if f < best_f: best, best_f = child, f
            if f < fx: wins += 1
        if best_f <= fx: x, fx = best, best_f
        sigma = min(0.4, max(0.01, sigma * (1.22 if wins / lam > 0.2 else 0.85))); history.append(round(fx, 4))
    return {k: round(v, 3) for k, v in x.items()}, prior, fx, history

def optimize(model, generations, seed):
    rng = random.Random(seed); actors = {}
    for aid, actor in sorted(model["actors"].items()):
        targeted = [s for s in model["suggestions"] if s["target"] == aid]; base = actor["opinion"]
        tuned, prior, loss, history = optimize_actor(base, targeted, rng, generations)
        after = merged(base, tuned); rows = []
        for s in targeted:
            n = len(s["evidence"])
            rows.append({"action": s["action"], "label": s["label"], "speaker": s["speaker"], "evidence": n,
                         "margin_before": round(score(base, s, n) - param(base, "threshold"), 3), "margin_after": round(score(after, s, n) - param(after, "threshold"), 3),
                         "adopt_before": adopts(base, s, n), "adopt_after": adopts(after, s, n),
                         "adopt_if_contradicted": adopts(after, s, n, contradicted=1), "adopt_if_corroborated": adopts(after, s, n, corroborated=min(n, 2))})
        actors[aid] = {"name": actor["name"], "files": actor["files"], "values": base.get("values") or {}, "before": {k: param(base, k) for k in PARAMS},
                       "prior": {k: round(v, 3) for k, v in prior.items()}, "after": tuned, "loss_before": round(objective(base, {k: param(base, k) for k in PARAMS}, prior, targeted), 4),
                       "loss_after": round(loss, 4), "history": history, "suggestions": rows, "story_safe": all(r["adopt_before"] == r["adopt_after"] for r in rows)}
    def spread(key, stage): return round(statistics.pstdev([a[stage][key] for a in actors.values()]), 3) if len(actors) > 1 else 0.0
    summary = {"actors": len(actors), "suggestions": len(model["suggestions"]), "story_safe": all(a["story_safe"] for a in actors.values()),
               "threshold_spread": {"before": spread("threshold", "before"), "after": spread("threshold", "after")},
               "trust_spread": {"before": spread("base_trust", "before"), "after": spread("base_trust", "after")},
               "contradiction_sensitive": sum(1 for a in actors.values() if a["after"]["contradiction_weight"] >= 0.5),
               "loss_before": round(sum(a["loss_before"] for a in actors.values()), 3), "loss_after": round(sum(a["loss_after"] for a in actors.values()), 3)}
    return {"schema": "character-tuning-candidate-v1", "generated_at": datetime.datetime.now().isoformat(timespec="seconds"),
            "seed": seed, "generations": generations, "summary": summary, "actors": actors}

def apply(candidate, scenario_dir):
    changed = []
    for path in sorted(scenario_dir.glob("*.dv")):
        data, text = load_dv(path)
        if not isinstance(data, dict): continue
        ids = [a.get("id") for a in data.get("actors") or [] if isinstance(a, dict) and isinstance(a.get("opinion"), dict)]
        spans = opinion_spans(text)
        if len(ids) != len(spans): print(f"  跳过 {path.name}：意见块数量 {len(spans)} 与人物 {len(ids)} 不一致"); continue
        pieces, cursor, count = [], 0, 0
        for aid, (start, end, obj) in zip(ids, spans):
            tuned = (candidate["actors"].get(aid) or {}).get("after")
            if not tuned: continue
            new = {}
            for key, value in obj.items():
                if key == "values":
                    for extra in ("corroboration_weight", "contradiction_weight"):
                        if extra not in obj: new[extra] = tuned[extra]
                new[key] = tuned.get(key, value) if key in PARAMS else value
            pieces.append(text[cursor:start]); pieces.append(json.dumps(new, ensure_ascii=False, separators=(",", ":"))); cursor = end; count += 1
        if count:
            pieces.append(text[cursor:]); path.write_text("".join(pieces), encoding="utf-8"); changed.append((path.name, count))
    return changed

def cli_metrics(cwd, steps, seed, out):
    sys.path.insert(0, str(ROOT / "mcp/verification")); import cli_report
    receipt, trace = cli_report.run_cli(steps, seed, "ai", out, chunk=100, cwd=cwd)
    report = cli_report.build_report(receipt, trace); final = trace.get("final") or {}
    return {"game_time": final.get("game_time"), "end": report["run"]["end"], "completed_tasks": final.get("completed_tasks", 0),
            "unexpected_failures": (final.get("receipt_totals") or {}).get("unexpected_failures", 0), "receipts": report["run"]["receipts"],
            "persuasion": len(report["persuasion"]), "adopted": sum(1 for p in report["persuasion"] if p["accepted"]), "verdict": report["verdict"],
            "elapsed_seconds": trace.get("elapsed_seconds")}

def validate(candidate_path, steps, seed):
    candidate = json.loads(candidate_path.read_text(encoding="utf-8")); work = candidate_path.parent / "validation"
    root = work / "root"
    if root.exists(): shutil.rmtree(root)
    shutil.copytree(ROOT / "assets/data", root / "assets/data")
    changed = apply(candidate, root / "assets/data/scenarios")
    baseline = cli_metrics(ROOT, steps, seed, work / "baseline"); tuned = cli_metrics(root, steps, seed, work / "candidate")
    passed = tuned["unexpected_failures"] <= baseline["unexpected_failures"] and tuned["completed_tasks"] >= baseline["completed_tasks"] and tuned["verdict"] != "fail"
    result = {"steps": steps, "seed": seed, "files_patched": changed, "baseline": baseline, "candidate": tuned, "passed": passed,
              "rule": "候选不得减少已完成任务数、不得新增意外失败，且 CLI 报告不得为未通过。"}
    candidate["validation"] = result; candidate_path.write_text(json.dumps(candidate, ensure_ascii=False, indent=1), encoding="utf-8")
    return result

def write_report(candidate, path):
    s = candidate["summary"]; lines = ["# 角色数值自动优化报告", "", f"种子 {candidate['seed']} · {candidate['generations']} 代 · 人物 {s['actors']} · 已声明提议 {s['suggestions']} · 剧情安全：{'是' if s['story_safe'] else '否'}", "",
        f"- 阈值离散度 {s['threshold_spread']['before']} → {s['threshold_spread']['after']}；基础信任离散度 {s['trust_spread']['before']} → {s['trust_spread']['after']}",
        f"- 对“与本人记忆矛盾的证据”敏感的人物：{s['contradiction_sensitive']} 名", f"- 总目标值 {s['loss_before']} → {s['loss_after']}", "",
        "| 人物 | 价值倾向 | 基础信任 | 阈值 | 证据权重 | 成本厌恶 | 风险厌恶 | 佐证权重 | 矛盾权重 |", "|---|---|---|---|---|---|---|---|---|"]
    for aid, a in candidate["actors"].items():
        b, t = a["before"], a["after"]; vals = "、".join(f"{k}{v}" for k, v in list(a["values"].items())[:4])
        lines.append(f"| {a['name']} | {vals} | " + " | ".join(f"{b[k]:.2f}→{t[k]:.2f}" for k in PARAMS) + " |")
    v = candidate.get("validation")
    if v: lines += ["", f"## 真实 CLI 对照（{v['steps']} 步，种子 {v['seed']}）：{'通过' if v['passed'] else '未通过'}", "", "| 指标 | 基线 | 候选 |", "|---|---|---|"] + \
        [f"| {k} | {v['baseline'][k]} | {v['candidate'][k]} |" for k in ("end", "completed_tasks", "unexpected_failures", "receipts", "persuasion", "adopted", "verdict")]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")

def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("command", choices=["extract", "optimize", "validate", "publish", "all"])
    parser.add_argument("--generations", type=int, default=80); parser.add_argument("--seed", type=int, default=7)
    parser.add_argument("--steps", type=int, default=1800); parser.add_argument("--candidate"); parser.add_argument("--confirm", action="store_true")
    parser.add_argument("--publish", action="store_true")
    args = parser.parse_args(); OUT.mkdir(parents=True, exist_ok=True)
    if args.command in ("extract", "optimize", "all"):
        model = extract(); (OUT / "model.json").write_text(json.dumps(model, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"提取：人物 {len(model['actors'])} 名，已声明提议 {len(model['suggestions'])} 条 → {OUT / 'model.json'}")
        if args.command == "extract": return
        run = OUT / datetime.datetime.now().strftime("%Y%m%d-%H%M%S"); run.mkdir(parents=True, exist_ok=True)
        candidate = optimize(model, args.generations, args.seed); path = run / "candidate.json"
        path.write_text(json.dumps(candidate, ensure_ascii=False, indent=1), encoding="utf-8"); write_report(candidate, run / "report.md")
        s = candidate["summary"]; print(f"优化：剧情安全 {s['story_safe']}；阈值离散 {s['threshold_spread']}；目标 {s['loss_before']}→{s['loss_after']} → {path}")
        args.candidate = str(path)
        if args.command == "optimize": return
    candidate_path = pathlib.Path(args.candidate or "")
    if args.command in ("validate", "all"):
        result = validate(candidate_path, args.steps, args.seed)
        candidate = json.loads(candidate_path.read_text(encoding="utf-8")); write_report(candidate, candidate_path.parent / "report.md")
        print(f"验证（真实 CLI，{args.steps} 步）：{'通过' if result['passed'] else '未通过'}；基线 {result['baseline']}；候选 {result['candidate']}")
        if args.command == "validate" or not args.publish: return
        if not result["passed"]: sys.exit("验证未通过，不发布。")
        args.confirm = True
    if args.command in ("publish", "all"):
        if not args.confirm: sys.exit("发布会改写 assets/data/scenarios/*.dv，请加 --confirm。")
        candidate = json.loads(candidate_path.read_text(encoding="utf-8"))
        if not candidate.get("summary", {}).get("story_safe"): sys.exit("候选不满足剧情安全约束，拒绝发布。")
        changed = apply(candidate, SCENARIOS)
        (OUT / "published.json").write_text(json.dumps({"candidate": str(candidate_path), "published_at": datetime.datetime.now().isoformat(timespec="seconds"), "files": changed}, ensure_ascii=False, indent=1), encoding="utf-8")
        print("已发布：" + "，".join(f"{name}×{count}" for name, count in changed))

if __name__ == "__main__":
    main()
