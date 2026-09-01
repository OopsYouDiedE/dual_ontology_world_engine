"""第一章二十日确定性模拟与完整决策轨迹导出。"""

from __future__ import annotations

import json
from collections import Counter
from pathlib import Path

from core.python.engine import Effect, Entity, Expr

from .academy_prologue import AcademyPrologue


def _display(value):
    if isinstance(value, Entity):
        return value.name
    if isinstance(value, (Expr, Effect)):
        return f"{value.op}({','.join(_display(item) for item in value.args)})"
    if isinstance(value, (tuple, list)):
        return "[" + "，".join(_display(item) for item in value) + "]"
    return "无" if value is None else str(value)


class PrologueSimulation:
    def __init__(self):
        self.game = AcademyPrologue()

    def run(self):
        self.game.run(20)
        self.assert_acceptance()
        return self

    def record(self, event, index):
        actor, effect = event.get("actor"), event.get("effect")
        scene = event.get("scene")
        return {
            "index": index,
            "day": event.get("day"),
            "time": event.get("time"),
            "moon": event.get("moon"),
            "name": event.name,
            "actor": actor.name if isinstance(actor, Entity) else None,
            "actor_role": actor.get("role") if isinstance(actor, Entity) else None,
            "scene": scene.name if isinstance(scene, Entity) else None,
            "effect": effect.op if isinstance(effect, Effect) else None,
            "arguments": [_display(item) for item in effect.args] if isinstance(effect, Effect) else [],
            "outcome": _display(event.get("outcome")),
            "decision_score": event.get("decision_score"),
            "decision_reason": event.get("decision_reason"),
            "candidates": list(event.get("candidates", ())),
            "blocked_candidates": list(event.get("blocked_candidates", ())),
            "state_changes": list(event.get("state_changes", ())),
            "witnesses": [facet.value.name for facet in event.related("ObservedBy")],
        }

    def records(self):
        return [self.record(event, index) for index, event in enumerate(self.game.runtime.events, 1)]

    def metrics(self):
        records = self.records()
        effects = Counter(record["effect"] for record in records)
        actors = Counter(record["actor_role"] or record["actor"] for record in records)
        acceptance = self.game.acceptance()
        acceptance.update({
            "effects": effects,
            "acting_students": len({role for role in actors if role in self.game.characters}),
            "candidate_choices": sum(len(record["candidates"]) for record in records),
            "blocked_candidates": sum(len(record["blocked_candidates"]) for record in records),
            "social_events": sum(record["effect"] in {"confide", "challenge", "collaborate"} for record in records),
            "student_action_counts": {
                role: actors.get(role, 0) for role in self.game.characters
            },
        })
        return acceptance

    def assert_acceptance(self):
        metric = self.metrics()
        checks = {
            "完整运行20日": metric["days"] == 20,
            "灰潮压力不超过3": metric["gray_pressure"] <= 3,
            "十六名学员均有自主行动": metric["acting_students"] == 16,
            "至少十四人建立正向信任": metric["socialized_characters"] >= 14,
            "至少五人形成可见威信": metric["leaders"] >= 5,
            "至少二十份工作或研究分析": metric["analyses"] >= 20,
            "至少两条初步客观规则": metric["rules"] >= 2,
            "规则实际淘汰一次冲动指控": metric["blocked_accusations"] >= 1 and metric["blocked_candidates"] >= 1,
            "恶魔至少四次实际协助": metric["demon_help"] >= 4,
            "言灵先失败后成功": metric["word_attempts"] >= 1 and metric["word_success"] >= 1,
            "首个威胁显现且被阶段性隔离": metric["threat_manifested"] and metric["threat_contained"],
            "初期篇完成但未宣告最终胜利": metric["arc_complete"],
        }
        failed = [label for label, passed in checks.items() if not passed]
        if failed:
            raise RuntimeError("PROLOGUE_ACCEPTANCE_FAIL: " + "；".join(failed))

    def export(self, markdown_path):
        markdown_path = Path(markdown_path).resolve()
        markdown_path.parent.mkdir(parents=True, exist_ok=True)
        jsonl_path = markdown_path.with_suffix(".jsonl")
        records, metric = self.records(), self.metrics()
        with jsonl_path.open("w", encoding="utf-8") as output:
            for record in records:
                output.write(json.dumps(record, ensure_ascii=False) + "\n")

        game = self.game
        lines = [
            "# 《异常感知学园》第一章：禁闭后的二十天完整轨迹", "",
            "## 结果", "",
            "- 这是 100–180 天长期进程的第 1–20 天，不是封印终局。",
            f"- 运行事件：{len(records)}；有实质候选：{metric['candidate_choices']}；被规则淘汰：{metric['blocked_candidates']}。",
            f"- 灰潮压力：{metric['gray_pressure']}/24；最早终局风险日：第 {game.gray_tide.get('earliest_terminal_day')} 日。",
            f"- 维生余量：食物 {game.resources.tag('FoodDays')} 日，饮水 {game.resources.tag('WaterDays')} 日，动力 {game.resources.tag('PowerDays')} 日。",
            f"- 初步规则：{metric['rules']} 条；分析知识：{metric['analyses']} 份；形成威信者：{metric['leaders']} 人。",
            f"- 十六名学员均作出自主选择；日常互动事件：{metric['social_events']} 次。",
            f"- 恶魔实际协助：{metric['demon_help']} 次；言灵：失败试验 {metric['word_attempts']} 次、真实成立 {metric['word_success']} 次。",
            f"- 首个威胁：{'已显现' if metric['threat_manifested'] else '未显现'}，{'阶段性隔离' if metric['threat_contained'] else '未隔离'}。", "",
            "## 二十日日程摘要", "",
        ]
        for summary in game.day_summaries:
            leaders = "、".join(f"{role}{score}" for role, _name, score in summary["leaders"])
            resources = summary["resources"]
            lines.extend((
                f"### 第{summary['day']}日 · {summary['title']}", "",
                f"- 月相：{summary['moon']}",
                f"- 当日事件：{summary['events']}",
                f"- 维生余量：食物 {resources['food']} / 饮水 {resources['water']} / 动力 {resources['power']} 日",
                f"- 当前威信前列：{leaders}",
                f"- 灰潮压力：{summary['gray_pressure']}/24", "",
            ))

        lines.extend(("## 完整决策事件", ""))
        for record in records:
            candidates = "；".join(
                f"{item['actor']}→[{item['operation']}]{item['label']}={item['score']}"
                for item in record["candidates"]
            ) or "无"
            blocked = "；".join(
                f"{item['actor']}→[{item['operation']}]{item['label']}（{item['rule']}）"
                for item in record["blocked_candidates"]
            ) or "无"
            lines.extend((
                f"### E{record['index']:03d} · 第{record['day']}日 · {record['name']}", "",
                f"- 月相／场景：{record['moon']}／{record['scene']}",
                f"- 行动者：{record['actor']}（{record['actor_role'] or '恶魔'}）",
                f"- 效果：`{record['effect']}`（{'；'.join(record['arguments'])}）",
                f"- 选择原因：{record['decision_reason']}",
                f"- 合法候选：{candidates}",
                f"- 被规则淘汰：{blocked}",
                f"- 状态后果：{'；'.join(record['state_changes']) or '无数值变化；后果保存在对象关系或知识中'}",
                f"- 见证者：{'、'.join(record['witnesses']) or '无'}", "",
            ))

        lines.extend(("## 因果叙事日志", ""))
        lines.extend(f"{index}. {entry}" for index, entry in enumerate(game.log, 1))
        lines.extend(("", f"逐事件 JSONL：`{jsonl_path.name}`", ""))
        markdown_path.write_text("\n".join(lines), encoding="utf-8")
        return markdown_path, jsonl_path


def run_prologue_simulation(markdown_path):
    simulation = PrologueSimulation().run()
    return simulation, simulation.export(markdown_path)


__all__ = ["PrologueSimulation", "run_prologue_simulation"]
