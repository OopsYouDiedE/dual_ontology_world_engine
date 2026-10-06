"""混沌测试：随机代理在公开协议上乱做（乱查、毁物、拧接头、乱重演、乱作答、乱逛、歇着），多种子无窗口运行。

用法：python godot_csharp/tools/check/chaos_simulation.py [--seeds 20] [--duration 1天] [--jobs 8]
不变量：进程不崩溃；没有交接失败；每个种子都在运行时长内到达终局或停在时长处（不卡在等待输入或动作预算上）。
报告各种子的结局分布、毁物、补救与未建模次数。自动验证，不代表游玩体验。
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PROJECT = ROOT / "godot_csharp/scenes/Awakening/check/DetectiveSimulation"
SUMMARY = re.compile(r"｜混沌｜随机动作 (\d+) 次；毁物 (\d+) 件；补救 (\d+) 次；未建模 (\d+) 次；终止阶段 (\w+)；(\S+)")


def run(seed: int, duration: str, folder: Path) -> tuple[int, list[str], dict]:
    out = folder / f"chaos{seed}.txt"
    result = subprocess.run(["dotnet", "run", "-c", "Release", "--no-build", "--project", str(PROJECT), "--",
                             "--enabled", "--chaos", str(seed), "--duration", duration, str(out)],
                            cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    problems = []
    if result.returncode != 0:
        problems.append("进程失败：" + (result.stderr or result.stdout).strip().splitlines()[-1][:160])
        return seed, problems, {}
    text = out.read_text(encoding="utf-8")
    if "交接或续篇失败" in text:
        problems.append("交接或续篇失败")
    if "｜待输入｜" in text:
        problems.append("卡在等待输入：" + next(l for l in text.splitlines() if "｜待输入｜" in l)[:120])
    match = SUMMARY.search(text)
    if not match:
        problems.append("缺少混沌摘要")
        return seed, problems, {}
    info = {"actions": int(match[1]), "destroyed": int(match[2]), "rescues": int(match[3]),
            "unmodeled": int(match[4]), "stage": match[5], "end": match[6]}
    if info["end"] == "动作预算用尽":
        problems.append("动作预算用尽仍未到终局或时长")
    return seed, problems, info


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--seeds", type=int, default=20)
    parser.add_argument("--first", type=int, default=1)
    parser.add_argument("--duration", default="1天")
    parser.add_argument("--jobs", type=int, default=8)
    args = parser.parse_args()
    subprocess.run(["dotnet", "build", "-c", "Release", str(PROJECT)], cwd=ROOT, check=True, capture_output=True)
    seeds = range(args.first, args.first + args.seeds)
    with tempfile.TemporaryDirectory() as folder, ThreadPoolExecutor(args.jobs) as pool:
        results = list(pool.map(lambda s: run(s, args.duration, Path(folder)), seeds))
    failed = [(seed, problems) for seed, problems, _ in results if problems]
    infos = [info for _, problems, info in results if info]
    for seed, problems in failed:
        print(f"[失败] 种子 {seed}：" + "；".join(problems))
    ends: dict[str, int] = {}
    for info in infos:
        ends[info["end"]] = ends.get(info["end"], 0) + 1
    print(f"合计 {len(results)} 个种子，失败 {len(failed)} 个；终止方式 {ends}；"
          f"毁物 {sum(i['destroyed'] for i in infos)} 件，补救 {sum(i['rescues'] for i in infos)} 次，"
          f"未建模 {sum(i['unmodeled'] for i in infos)} 次；随机动作共 {sum(i['actions'] for i in infos)} 次。")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
