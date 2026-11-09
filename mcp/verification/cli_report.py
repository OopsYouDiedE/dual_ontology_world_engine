#!/usr/bin/env python3
"""双本体世界引擎 · CLI 自动测试可读报告（结论在前，细节在后）。

  python mcp/verification/cli_report.py run --steps 1500 --seed 1 [--controller ai] [--out DIR]
      启动真实 CLI 宿主（open → advance×N → close），保存原始回执并生成报告。
  python mcp/verification/cli_report.py render --receipt DIR/receipt.yaml [--trace DIR/trace.json]
      只根据已有原始回执生成报告。

输出 report.md（人读）、report.json（机器读，可导入工作台）与控制台摘要。
原始回执保持不变；报告只做归纳与翻译。CLI 是作者 / 测试宿主，可显示完整说服审计。
"""
import argparse, collections, datetime, json, os, pathlib, shutil, subprocess, sys, time

ROOT = pathlib.Path(__file__).resolve().parents[2]
CLI_DLL = ROOT / "core/session_cli/bin/Debug/net8.0/DuelView.Cli.dll"
PHYSIOLOGICAL = {"energy": "精力", "hunger": "饱食", "thirst": "水分", "hygiene": "清洁"}
CRITICAL = 20.0
FAILURE_TEXT = {
    "target_not_present": "目标人物或物件不在现场", "navigation_unreachable": "通路被挡住，走不到目标",
    "navigation_stalled": "行走途中被卡住", "navigation_path_lost": "目标离开了原通路",
    "suggestion_rejected": "提议被对方拒绝", "deduction_unproved": "推演证据不足，结论未成立",
    "incantation_precondition_failed": "言灵前提不成立", "slot_occupied": "交互位置被他人占用",
    "blocked_attempt": "现场条件暂不满足（可预期的阻碍）", "interrupted": "被更紧急的事打断", "cancelled": "主动取消",
}
OUTCOME_TEXT = {"completed": "完成", "rejected": "被拒绝", "blocked": "受阻", "blocked_attempt": "受阻",
                "cancelled": "取消", "failed": "失败", "interrupted": "被打断", "recoverable_cancellation": "可恢复取消"}
AMBIENT = {"与附近物件互动", "移动到这里"}
ICON = {"pass": "✔", "warn": "⚠", "fail": "✘", "info": "ℹ"}
VERDICT_TEXT = {"pass": "通过", "warn": "通过（有警告）", "fail": "未通过"}

def clock(minutes):
    if minutes is None: return "—"
    m = float(minutes); day = int(m // 1440) + 1; rem = m - (day - 1) * 1440
    return f"第{day}日 {int(rem // 60):02d}:{int(rem % 60):02d}"

def parse_records(path):
    records, current = [], None
    if not path or not pathlib.Path(path).exists(): return records
    with open(path, encoding="utf-8") as handle:
        for raw in handle:
            line = raw.rstrip("\n")
            if not line.strip(): continue
            if line.startswith("- "):
                current = {}; records.append(current); line = line[2:]
            elif line.startswith("  ") and current is not None:
                line = line[2:]
            else:
                continue
            key, sep, value = line.partition(": ")
            key = key.strip().rstrip(":")
            try: current[key] = json.loads(value) if sep and value else None
            except ValueError: current[key] = value
    return records

def load_actor_names():
    names = {"detective": "侦探"}
    for path in sorted((ROOT / "assets/data/scenarios").glob("*.dv")):
        text = path.read_text(encoding="utf-8"); at = text.find('"value"')
        if at < 0: continue
        try: data, _ = json.JSONDecoder().raw_decode(text, text.find("{", text.find(":", at)))
        except ValueError: continue
        for actor in (data.get("actors") or []) if isinstance(data, dict) else []:
            if isinstance(actor, dict) and actor.get("id") and actor.get("name"): names.setdefault(actor["id"], actor["name"])
    return names

def find_opinions(obj, found):
    if isinstance(obj, dict):
        if "accepted" in obj and ("threshold" in obj or "contributions" in obj or "missing" in obj): found.append(obj); return
        for value in obj.values(): find_opinions(value, found)
    elif isinstance(obj, list):
        for value in obj: find_opinions(value, found)

def run_cli(steps, seed, controller, out_dir, chunk=50, cwd=ROOT, content=None):
    out_dir = pathlib.Path(out_dir).resolve()  # 宿主可能在另一工作目录运行，回执路径必须是绝对路径
    out_dir.mkdir(parents=True, exist_ok=True)
    receipt = out_dir / "receipt.yaml"
    dotnet = shutil.which("dotnet") or "dotnet"
    proc = subprocess.Popen([dotnet, str(CLI_DLL)], cwd=str(cwd), stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, text=True, encoding="utf-8")
    def call(request):
        proc.stdin.write(json.dumps(request, ensure_ascii=False) + "\n"); proc.stdin.flush()
        line = proc.stdout.readline()
        if not line: raise RuntimeError("CLI 宿主无响应：" + proc.stderr.read()[-1500:])
        reply = json.loads(line)
        if reply.get("error"): raise RuntimeError("CLI 宿主报错：" + str(reply["error"]).splitlines()[0])
        return reply["result"]
    options = {"controller": controller, "seed": seed, "receipt_path": str(receipt)}
    if content: options["content_path"] = content
    started = time.time()
    trace = {"options": {k: v for k, v in options.items() if k != "receipt_path"}, "steps_requested": steps, "chunks": [], "opinions": []}
    last = call({"id": 1, "type": "open", "options": options})
    trace["initial"] = summarize(last)
    done = 0
    while done < steps and last.get("active", True):
        n = min(chunk, steps - done); last = call({"id": 2, "type": "advance", "steps": n}); done += n
        status = last.get("status") or {}
        trace["chunks"].append({"steps": done, "game_time": status.get("game_time"), "status": status.get("status")})
        if last.get("last_opinion"): trace["opinions"].append(last["last_opinion"])
        if status.get("status") not in (None, "running"): break
    trace["final"] = summarize(last)
    trace["steps_done"] = done
    call({"id": 3, "type": "close"})
    try: proc.wait(timeout=60)
    except subprocess.TimeoutExpired: proc.kill()
    trace["elapsed_seconds"] = round(time.time() - started, 2)
    (out_dir / "trace.json").write_text(json.dumps(trace, ensure_ascii=False, indent=1), encoding="utf-8")
    return receipt, trace

def summarize(observation):
    status = observation.get("status") or {}; screen = observation.get("screen") or {}
    paths = screen.get("player_paths") or {}
    return {"game_time": status.get("game_time"), "status": status.get("status"), "stop_reason": status.get("stop_reason"),
            "receipt_totals": observation.get("receipt_totals") or {}, "room": screen.get("room"), "needs": screen.get("needs") or {},
            "completed_tasks": (paths.get("completed_tasks") or {}).get("count", 0),
            "completed_task_ids": (paths.get("completed_tasks") or {}).get("ids", []),
            "task": {k: (paths.get("task") or {}).get(k) for k in ("id", "label", "status", "completed_steps", "total_steps", "reason")}}

def build_report(receipt_path, trace=None):
    receipt_path = pathlib.Path(receipt_path); trace = trace or {}
    receipts = parse_records(receipt_path)
    states = parse_records(receipt_path.with_name(receipt_path.stem + ".states.yaml"))
    coverage_path = receipt_path.with_name(receipt_path.stem + ".evidence-coverage.json")
    coverage = json.loads(coverage_path.read_text(encoding="utf-8")) if coverage_path.exists() else {}
    names = load_actor_names()
    actors = collections.defaultdict(lambda: {"actions": 0, "completed": 0, "failed": 0, "minutes": 0.0, "labels": collections.Counter(),
                                              "failures": collections.Counter(), "min_need": None, "decisions": 0, "last_decision": None})
    failures, timeline, persuasion, seen = collections.Counter(), [], [], set()
    for record in receipts:
        actor = str(record.get("actor") or "?"); label = str(record.get("label") or record.get("action") or "?")
        outcome = str(record.get("outcome_category") or record.get("outcome") or "")
        start = record.get("start_time"); minutes = float(record.get("duration") or 0)
        entry = actors[actor]; entry["actions"] += 1; entry["minutes"] += minutes
        entry["labels"]["环境互动（宿主）" if label in AMBIENT else label[:24]] += max(minutes, 0.5)
        if outcome == "completed": entry["completed"] += 1
        else:
            reason = str(record.get("reason") or outcome or "unknown"); failures[reason] += 1; entry["failed"] += 1; entry["failures"][reason] += 1
        audit = record.get("decision_audit") if isinstance(record.get("decision_audit"), dict) else {}
        for need, value in (audit.get("needs") or {}).items():
            if need in PHYSIOLOGICAL and isinstance(value, (int, float)) and (entry["min_need"] is None or value < entry["min_need"]["value"]):
                entry["min_need"] = {"need": PHYSIOLOGICAL[need], "value": round(value, 1), "time": clock(start)}
        intent = record.get("decision_intent") if isinstance(record.get("decision_intent"), dict) else {}
        drive = record.get("decision_drive") if isinstance(record.get("decision_drive"), dict) else {}
        if intent or drive:
            entry["decisions"] += 1
            top = sorted((drive.get("NeedContributions") or {}).items(), key=lambda kv: -abs(kv[1] or 0))[:2]
            entry["last_decision"] = {"time": clock(start), "action": label, "score": drive.get("Score"), "path": intent.get("Path"),
                                      "reason": intent.get("Reason"), "top_needs": [f"{k} {v:+.1f}" for k, v in top if isinstance(v, (int, float))]}
        if actor == "detective":
            timeline.append({"time": clock(start), "label": label, "outcome": OUTCOME_TEXT.get(outcome, outcome), "minutes": minutes,
                             "why": (intent.get("Reason") or "")[:60]})
        found = []; find_opinions(record, found)
        if not found and record.get("reason") == "suggestion_rejected": found.append({"accepted": False, "actor": actor, "action": record.get("action")})
        for opinion in found:
            key = (opinion.get("execution"), opinion.get("action") or record.get("action"))
            if key in seen: continue
            seen.add(key); persuasion.append(opinion_row(opinion, record, names))
    for opinion in trace.get("opinions") or []:
        key = (opinion.get("execution"), opinion.get("action"))
        if key not in seen: seen.add(key); persuasion.append(opinion_row(opinion, {}, names))
    initial, final = trace.get("initial") or {}, trace.get("final") or {}
    start_t = initial.get("game_time") or (min((r.get("start_time") or 0) for r in receipts) if receipts else None)
    end_t = final.get("game_time") or (max((r.get("start_time") or 0) for r in receipts) if receipts else None)
    rows = []
    for actor, entry in sorted(actors.items(), key=lambda kv: -kv[1]["actions"]):
        total = sum(entry["labels"].values()) or 1
        top = [{"label": k, "share": round(v / total, 3)} for k, v in entry["labels"].most_common(3)]
        rows.append({"id": actor, "name": names.get(actor, actor), "actions": entry["actions"], "completed": entry["completed"], "failed": entry["failed"],
                     "busy_minutes": round(entry["minutes"], 1), "top_activities": top, "min_need": entry["min_need"],
                     "failures": dict(entry["failures"]), "last_decision": entry["last_decision"]})
    checks = []
    def check(level, title, detail): checks.append({"level": level, "title": title, "detail": detail})
    advanced = (end_t or 0) - (start_t or 0)
    stalls = sum(1 for a, b in zip(trace.get("chunks", []), trace.get("chunks", [])[1:]) if a.get("game_time") == b.get("game_time"))
    check("pass" if advanced > 0 and stalls == 0 else ("warn" if advanced > 0 else "fail"), "时间正常推进",
          f"游戏时间 {clock(start_t)} → {clock(end_t)}，共前进 {advanced:.0f} 分钟；停滞批次 {stalls} 个。")
    totals = final.get("receipt_totals") or {}
    unexpected = int(totals.get("unexpected_failures", 0) or 0)
    check("pass" if unexpected == 0 else "fail", "没有意外失败",
          f"行动回执 {len(receipts)} 条；完成 {sum(r['completed'] for r in rows)}，可预期的拒绝/受阻 {sum(r['failed'] for r in rows)}，意外失败 {unexpected}。")
    npcs = [r for r in rows if r["id"] != "detective"]
    idle = [r["name"] for r in npcs if r["completed"] == 0]
    check("pass" if npcs and not idle else ("warn" if npcs else "fail"), "角色都在自主行动",
          f"{len(npcs)} 名角色有行动记录" + (f"；没有完成任何行动：{'、'.join(idle)}" if idle else "，全部至少完成一次行动。"))
    mono = [f"{r['name']}（{r['top_activities'][0]['label']} {r['top_activities'][0]['share']:.0%}）" for r in npcs
            if r["top_activities"] and r["top_activities"][0]["share"] > 0.7 and r["actions"] >= 5]
    check("pass" if not mono else "warn", "行为多样（像模拟人生一样轮换需要）",
          "没有角色被单一活动占满 70% 以上时间。" if not mono else "单一活动占比过高：" + "；".join(mono[:6]))
    low = [f"{r['name']} {r['min_need']['need']} {r['min_need']['value']}（{r['min_need']['time']}）" for r in rows if r["min_need"] and r["min_need"]["value"] < CRITICAL]
    check("pass" if not low else "warn", "身体需要得到照护", "没有人的精力、饱食、水分或清洁跌破临界值 20。" if not low else "跌破临界值：" + "；".join(low[:6]))
    if persuasion:
        accepted = sum(1 for p in persuasion if p["accepted"])
        check("pass", "说服与采纳博弈已发生", f"共 {len(persuasion)} 次提议：采纳 {accepted}，婉拒 {len(persuasion) - accepted}。明细见“说服与采纳”。")
    else:
        check("info", "说服与采纳博弈", "本次时段内没有发生提议（第二日起才有正式提议）。延长步数可覆盖。")
    tasks = final.get("completed_tasks", 0)
    check("pass" if tasks > (initial.get("completed_tasks") or 0) else "warn", "侦探任务有进展",
          f"完成任务 {tasks} 项" + (f"：{', '.join(final.get('completed_task_ids', [])[:8])}" if tasks else "") +
          (f"；当前任务「{(final.get('task') or {}).get('label')}」{(final.get('task') or {}).get('completed_steps')}/{(final.get('task') or {}).get('total_steps')}" if (final.get("task") or {}).get("label") else ""))
    if coverage:
        check("pass" if coverage.get("complete") else "fail", "原始记录完整", f"回执 {coverage.get('action_log', {}).get('rows')} 行、认知记录 {coverage.get('state_log', {}).get('rows')} 行，SHA-256 与宿主计数一致：{'是' if coverage.get('complete') else '否'}。")
    verdict = "fail" if any(c["level"] == "fail" for c in checks) else ("warn" if any(c["level"] == "warn" for c in checks) else "pass")
    return {"schema": "dual-ontology-cli-report-v1", "generated_at": datetime.datetime.now().isoformat(timespec="seconds"),
            "run": {"seed": (trace.get("options") or {}).get("seed"), "controller": (trace.get("options") or {}).get("controller"),
                    "steps": trace.get("steps_done"), "elapsed_seconds": trace.get("elapsed_seconds"), "start": clock(start_t), "end": clock(end_t),
                    "start_minutes": start_t, "end_minutes": end_t, "receipts": len(receipts), "state_records": len(states),
                    "content_hash": coverage.get("content_hash"), "receipt_path": str(receipt_path)},
            "verdict": verdict, "checks": checks, "actors": rows, "persuasion": persuasion,
            "failures": [{"reason": k, "text": FAILURE_TEXT.get(k, k), "count": v} for k, v in failures.most_common()],
            "detective_timeline": timeline[-60:], "progress": trace.get("chunks", [])}

def opinion_row(opinion, record, names):
    contributions = opinion.get("contributions") or {}
    factors = sorted(contributions.items(), key=lambda kv: -abs(kv[1] or 0))[:4]
    speaker = opinion.get("actor") or record.get("actor"); target = opinion.get("target")
    return {"time": clock(record.get("start_time")), "speaker": speaker, "speaker_name": names.get(speaker, speaker),
            "target": target, "target_name": names.get(target, target or "?"), "action": opinion.get("action") or record.get("action"),
            "label": record.get("label") or opinion.get("action"), "accepted": bool(opinion.get("accepted")),
            "score": opinion.get("score"), "threshold": opinion.get("threshold"), "evidence_given": len(opinion.get("evidence") or []),
            "evidence_missing": len(opinion.get("missing") or []), "corroborated": opinion.get("corroborated"), "contradicted": opinion.get("contradicted"),
            "reason": opinion.get("reason"), "factors": [{"factor": k, "weight": round(v, 3)} for k, v in factors if isinstance(v, (int, float))]}

def to_markdown(report):
    run = report["run"]; lines = [f"# 双本体世界引擎 · CLI 自动测试报告", "",
        f"**结论：{ICON[report['verdict']]} {VERDICT_TEXT[report['verdict']]}**　|　"
        f"种子 {run['seed']} · 控制 {run['controller']} · {run['steps']} 步 · {run['start']} → {run['end']} · 用时 {run['elapsed_seconds']} 秒", "",
        "## 一、检查结论", "", "| 结果 | 检查项 | 说明 |", "|:-:|---|---|"]
    lines += [f"| {ICON[c['level']]} | {c['title']} | {c['detail']} |" for c in report["checks"]]
    lines += ["", "## 二、角色一览（按行动数）", "", "| 角色 | 行动 | 完成 | 未成 | 主要活动（时间占比） | 身体需要最低 | 最近一次决定 |", "|---|--:|--:|--:|---|---|---|"]
    for a in report["actors"]:
        top = "、".join(f"{t['label']} {t['share']:.0%}" for t in a["top_activities"])
        need = f"{a['min_need']['need']} {a['min_need']['value']}" if a["min_need"] else "—"
        last = a["last_decision"] or {}
        lines.append(f"| {a['name']} | {a['actions']} | {a['completed']} | {a['failed']} | {top} | {need} | {last.get('action', '—')}：{(last.get('reason') or '')[:30]} |")
    lines += ["", "## 三、说服与采纳（作者审计）", ""]
    if report["persuasion"]:
        lines += ["| 时间 | 提议者 → 对象 | 提议 | 证据(给/缺) | 得分 / 阈值 | 结果 | 主要因素 |", "|---|---|---|---|---|:-:|---|"]
        for p in report["persuasion"]:
            score = f"{p['score']:.2f} / {p['threshold']:.2f}" if isinstance(p.get("score"), (int, float)) and isinstance(p.get("threshold"), (int, float)) else "—"
            factors = "、".join(f"{f['factor']} {f['weight']:+.2f}" for f in p["factors"])
            lines.append(f"| {p['time']} | {p['speaker_name']} → {p['target_name']} | {p['label']} | {p['evidence_given']}/{p['evidence_missing']} | {score} | {'采纳' if p['accepted'] else '婉拒'} | {factors} |")
    else: lines.append("本时段没有发生提议。")
    lines += ["", "## 四、未完成行动的原因", "", "| 原因 | 说明 | 次数 |", "|---|---|--:|"]
    lines += [f"| `{f['reason']}` | {f['text']} | {f['count']} |" for f in report["failures"]] or ["| — | 无 | 0 |"]
    lines += ["", "## 五、侦探行动时间线（最近 60 条）", "", "| 时间 | 行动 | 结果 | 用时(分) | 为什么 |", "|---|---|---|--:|---|"]
    lines += [f"| {t['time']} | {t['label']} | {t['outcome']} | {t['minutes']:.0f} | {t['why']} |" for t in report["detective_timeline"]]
    lines += ["", f"原始回执：`{run['receipt_path']}`（{run['receipts']} 条行动、{run['state_records']} 条认知记录，内容哈希 `{(run['content_hash'] or '')[:16]}`）。"]
    return "\n".join(lines) + "\n"

def print_summary(report, out_dir):
    run = report["run"]; bar = "═" * 64
    print(bar); print(f" 双本体世界引擎 · CLI 自动测试　结论：{ICON[report['verdict']]} {VERDICT_TEXT[report['verdict']]}")
    print(f" {run['start']} → {run['end']}｜{run['steps']} 步｜回执 {run['receipts']} 条｜用时 {run['elapsed_seconds']} 秒"); print(bar)
    for c in report["checks"]: print(f" {ICON[c['level']]} {c['title']}：{c['detail']}")
    print("─" * 64); print(" 角色            行动  完成  主要活动")
    for a in report["actors"][:17]:
        top = a["top_activities"][0] if a["top_activities"] else {"label": "—", "share": 0}
        print(f" {a['name'][:8]:<10}{a['actions']:>6}{a['completed']:>6}  {top['label'][:18]} {top['share']:.0%}")
    if report["persuasion"]:
        print("─" * 64)
        for p in report["persuasion"][:8]: print(f" {p['time']} {p['speaker_name']}→{p['target_name']}：{'采纳' if p['accepted'] else '婉拒'}　{p['label'][:24]}")
    print(bar); print(f" 详细报告：{out_dir / 'report.md'}"); print(f" 机器可读：{out_dir / 'report.json'}"); print(bar)

def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    run = sub.add_parser("run"); run.add_argument("--steps", type=int, default=1500); run.add_argument("--seed", type=int, default=1)
    run.add_argument("--controller", default="ai"); run.add_argument("--out"); run.add_argument("--chunk", type=int, default=50)
    render = sub.add_parser("render"); render.add_argument("--receipt", required=True); render.add_argument("--trace"); render.add_argument("--out")
    args = parser.parse_args()
    if args.command == "run":
        out = pathlib.Path(args.out or ROOT / ".runtime/cli-report" / datetime.datetime.now().strftime("%Y%m%d-%H%M%S"))
        receipt, trace = run_cli(args.steps, args.seed, args.controller, out, args.chunk)
    else:
        receipt = pathlib.Path(args.receipt); out = pathlib.Path(args.out or receipt.parent)
        trace = json.loads(pathlib.Path(args.trace).read_text(encoding="utf-8")) if args.trace else {}
    report = build_report(receipt, trace)
    out.mkdir(parents=True, exist_ok=True)
    (out / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8")
    (out / "report.md").write_text(to_markdown(report), encoding="utf-8")
    print_summary(report, out)
    sys.exit(1 if report["verdict"] == "fail" else 0)

if __name__ == "__main__":
    main()
