"""运行动物园规则怪谈规则系统五天推演测试并输出报告。"""

from __future__ import annotations

import sys
from .zoo_simulation import ZooSimulation


def main() -> None:
    print("=" * 80)
    print("动物园规则怪谈：规则系统五天连续推演测试")
    print("=" * 80)

    sim = ZooSimulation()
    sim.run_all()

    for day in range(1, 6):
        print(f"\n【第 {day} 天推演日志】")
        for line in sim.daily_logs[day]:
            print(f"  {line}")

    summary = sim.generate_summary()
    print("\n" + "=" * 80)
    print("推演结算统计：")
    print(f"推演总天数: {summary['total_days']} 天")
    print(f"消耗总行动值: {summary['total_av']} AV")
    print(f"结算事件总数: {summary['total_events']} 起")
    print("\n角色最终生存与认知状态：")
    for s in summary["survivors"]:
        status = "人类理智存活" if s["is_human"] and s["sanity"] > 0 else "认知异化/阵亡"
        print(
            f"  - {s['name']} | SAN: {s['sanity']:3d}/100 | 位置: {s['final_location']} | 状态: {status}"
        )
    print("=" * 80)


if __name__ == "__main__":
    main()
