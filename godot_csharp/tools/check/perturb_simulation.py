"""整场微调验证：逐一微调人物默认设置跑完整模拟，检查不变量，并报告与默认轨迹的差异范围。

用法：python godot_csharp/tools/check/perturb_simulation.py [--duration 到结局] [--quick]
每个变体都是一次独立的无窗口模拟（约十秒）。不变量：不崩溃、不卡死；生活翼正式开门；
续篇正常结束或在运行时长处停下；每个变体的差异只来自被微调的人（报告中列出最早分歧行，供人审读）。
"""
from __future__ import annotations

import argparse
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PROJECT = ROOT / "godot_csharp/scenes/Awakening/check/DetectiveSimulation"

VARIANTS = [
    {},
    {"苏泠.初醒信任.侦探": -2}, {"苏泠.初醒信任.侦探": 2},
    {"沈鼎.初醒信任.侦探": -1}, {"沈鼎.初醒信任.侦探": 3},
    {"霍铎.初醒信任.侦探": 2}, {"霍铎.初醒信任.铜口": 0},
    {"苏泠.谨慎": 4}, {"沈鼎.谨慎": 0}, {"霍铎.谨慎": 3},
    {"沈鼎.说话习惯.寡言": 3}, {"霍铎.说话习惯.会撒谎": 1},
]


def run(variant: dict[str, float], duration: str, out: Path) -> str:
    args = ["dotnet", "run", "-c", "Release", "--no-build", "--project", str(PROJECT), "--", "--enabled",
            "--duration", duration]
    for key, value in variant.items():
        args += ["--persona", f"{key}={value}"]
    args.append(str(out))
    result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
    if result.returncode != 0:
        raise SystemExit(f"变体 {variant} 运行失败：\n{result.stdout}\n{result.stderr}")
    return out.read_text(encoding="utf-8")


def invariants(text: str) -> list[str]:
    problems = []
    if "交接或续篇失败" in text or "Unhandled" in text:
        problems.append("交接或续篇失败")
    if "正式终局流程开启隔离门" not in text:
        problems.append("生活翼没有正式开门")
    if "续篇结束 True" not in text and "运行停点" not in text:
        problems.append("续篇既未结束也未在运行时长处停下")
    return problems


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--duration", default="到结局")
    parser.add_argument("--quick", action="store_true", help="只跑前四个变体")
    args = parser.parse_args()
    subprocess.run(["dotnet", "build", "-c", "Release", str(PROJECT)], cwd=ROOT, check=True, capture_output=True)
    variants = VARIANTS[:4] if args.quick else VARIANTS
    failed = 0
    with tempfile.TemporaryDirectory() as folder:
        baseline = None
        for index, variant in enumerate(variants):
            text = run(variant, args.duration, Path(folder) / f"v{index}.txt")
            lines = [line for line in text.splitlines() if "｜运行｜" not in line]
            if baseline is None:
                baseline = lines
            problems = invariants(text)
            differ = [i for i, (a, b) in enumerate(zip(baseline, lines)) if a != b]
            first = lines[differ[0]][:120] if differ else "（与默认相同）"
            label = "，".join(f"{k}={v}" for k, v in variant.items()) or "默认"
            status = "失败：" + "；".join(problems) if problems else "通过"
            print(f"[{status}] {label}｜轨迹 {len(lines)} 行，与默认不同 {len(differ) + abs(len(lines) - len(baseline))} 行｜最早分歧：{first}")
            failed += bool(problems)
    print(f"合计 {len(variants)} 个变体，失败 {failed} 个。自动验证，不代表动机合理性或游玩体验通过。")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
