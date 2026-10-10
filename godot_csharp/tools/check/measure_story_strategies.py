"""Run declared public-input policies in isolated story copies; preserve every trajectory."""
from pathlib import Path
import argparse
import json
import os
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed

TOOLS = Path(__file__).resolve().parents[1]
ROOT = TOOLS.parent.parent
sys.path.insert(0, str(TOOLS))
from story_script import compile_story


def policies():
    result = {}
    for line in (Path(__file__).with_name("strategy_players.story")).read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line.startswith("玩家策略 "):
            key = line.split()[1]
            if key in result:
                raise ValueError("Duplicate policy")
            result[key] = {"budget": "深度 4 展开 512 假设 32 论证 0 求知 0", "rules": []}
        elif line.strip().startswith("推理预算："):
            result[key]["budget"] = line.split("：", 1)[1]
        elif line.startswith("    - "):
            result[key]["rules"].append(line.strip())
        else:
            raise ValueError("Unsupported policy declaration")
    return result


def fixture(destination, policy):
    source = ROOT / "godot_csharp/scenes/Awakening/Script"
    destination.mkdir(parents=True, exist_ok=False)
    controlled = []
    for path in source.rglob("*.story"):
        target = destination / path.relative_to(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        text = path.read_text(encoding="utf-8-sig")
        if "操控者：玩家" in text:
            controlled.append(target)
            lines = text.splitlines()
            start = next(i for i, line in enumerate(lines) if line.strip() == "操控规则：")
            end = start + 1
            while end < len(lines) and (not lines[end].strip() or len(lines[end]) - len(lines[end].lstrip()) > 4):
                end += 1
            lines[start:end] = ["    操控规则：", *["        " + rule for rule in policy["rules"]]]
            text = "\n".join(lines).replace("操控者：玩家", "操控者：代理") + "\n"
            text = "\n".join("    推理预算：" + policy["budget"] if line.strip().startswith("推理预算：") else line for line in text.splitlines()) + "\n"
        target.write_text(text, encoding="utf-8", newline="\n")
    if len(controlled) != 1:
        raise ValueError("Expected one authored player role")
    document = compile_story([destination / "chapter_01.story"], set(), ecology_only=True)
    path = destination / "agent-world.json"
    path.write_text(json.dumps(document, ensure_ascii=False), encoding="utf-8")
    return path


def summarize(path):
    entered_answer = False
    world_opened = False
    traction = []
    deviations = []
    progress = []
    operations = []
    last = None
    for raw in path.read_text(encoding="utf-8").splitlines():
        row = json.loads(raw)
        if row["kind"] == "player_time":
            entered_answer |= row["value"].get("Phase") == "answer"
        if row["kind"] == "strategy_action":
            traction.append({"elapsed_ms": row["elapsed_ms"], "sequence": row["sequence"], **row["value"]})
        elif row["kind"] == "strategy_deviation":
            deviations.append({"elapsed_ms": row["elapsed_ms"], "sequence": row["sequence"], **row["value"]})
        elif row["kind"] == "case_progress":
            progress.append({"elapsed_ms": row["elapsed_ms"], "sequence": row["sequence"], **row["value"]})
        elif row["kind"] == "object_changed":
            operations.append(row["value"])
        elif row["kind"] == "world_resolution":
            world_opened = True
        elif row["kind"] == "stop":
            last = row["value"]
    recoveries = []
    for action in deviations:
        later = next((p for p in progress if p["sequence"] > action["sequence"]), None)
        recoveries.append(None if later is None else {"game_minutes": later["minutes"] - action["minutes"],
            "wall_seconds": (later["elapsed_ms"] - action["elapsed_ms"]) / 1000})
    return {"entered_answer": entered_answer, "world_resolution": world_opened, "stop": last,
        "traction": traction, "deviations": deviations, "progress": progress, "recovery_after_deviation": recoveries, "object_operations": operations}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", required=True)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--seeds", nargs="+", type=int, default=[1, 2])
    parser.add_argument("--cases", nargs="+", choices=list(policies()))
    parser.add_argument("--seconds", type=float, default=240)
    parser.add_argument("--parallel", type=int, choices=[1, 2], default=2)
    args = parser.parse_args()
    destination = args.output.resolve()
    destination.mkdir(parents=True, exist_ok=False)
    declared = policies()
    cases = args.cases or list(declared)
    fixtures = {case: fixture(destination / case / "story", declared[case]) for case in cases}
    env = os.environ.copy()
    env["APPDATA"] = env["LOCALAPPDATA"] = str(ROOT / "tmp/appdata")
    (ROOT / "tmp/appdata").mkdir(parents=True, exist_ok=True)
    def run(case, seed):
        trace = destination / case / f"seed-{seed}.jsonl"
        command = [args.godot, "--headless", "--path", str(ROOT / "godot_csharp"), "--script", "res://tools/run_virtual_player.gd", "--",
            f"--seed={seed}", "--days=3", "--max-selections=10000", "--max-observations=50000", f"--max-seconds={args.seconds}",
            f"--no-progress-seconds={args.seconds + 30}", f"--declarations={fixtures[case]}", f"--output={trace}"]
        with trace.with_suffix(".stdout.log").open("wb") as out, trace.with_suffix(".stderr.log").open("wb") as err:
            try:
                execution = subprocess.run(command, cwd=ROOT, env=env, stdout=out, stderr=err, timeout=args.seconds + 90)
                exit_code = execution.returncode
            except subprocess.TimeoutExpired:
                exit_code = "process_timeout"
        result = summarize(trace) if trace.exists() else {"stop": None}
        result.update(case=case, seed=seed, exit_code=exit_code)
        trace.with_suffix(".result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        return result
    results = []
    with ThreadPoolExecutor(max_workers=args.parallel) as pool:
        futures = [pool.submit(run, case, seed) for case in cases for seed in args.seeds]
        for future in as_completed(futures):
            result = future.result()
            results.append(result)
            print(json.dumps({k: result.get(k) for k in ("case", "seed", "exit_code", "entered_answer", "world_resolution", "stop")}, ensure_ascii=False), flush=True)
    (destination / "results.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0 if all(r["exit_code"] == 0 and r["stop"] for r in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
