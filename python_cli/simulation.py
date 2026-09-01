"""确定性成功／失败模拟与不删减轨迹导出。"""

from __future__ import annotations

import json
from collections import Counter
from pathlib import Path

from core.python.engine import Effect, Entity, Expr

from .academy import AcademyGame


ROUTINE_OPERATIONS = {
    "sample", "recite", "treat", "analyze", "strategize", "dance_path",
    "decipher", "repair", "infiltrate", "reinforce", "listen", "litigate",
    "suppress", "reconstruct", "ration",
}
RELATION_OPERATIONS = {"share_memory", "confront", "protect", "apologize"}
DEMON_OPERATIONS = {"spread_miasma", "whisper_doubt", "forge_false_rule"}
MAINLINE_OPERATIONS = {
    "investigate", "recover_memory", "analyze_memory", "synthesize_history",
    "refute_rule", "propose_rule", "invoke_word", "share_memory", "confront", "protect",
    "request_help", "answer_help", "assist", "decipher", "repair", "listen",
    "treat", "collect", "establish_rule", "begin_rite", "contribute_rite",
}


def _display(value):
    if isinstance(value, Entity):
        return value.name
    if hasattr(value, "name"):
        return str(value.name)
    if isinstance(value, (Expr, Effect)):
        return f"{value.op}({','.join(_display(item) for item in value.args)})"
    if isinstance(value, (tuple, list)):
        return "[" + "，".join(_display(item) for item in value) + "]"
    return "无" if value is None else str(value)


class AcademySimulation:
    """玩家只建立协作规则；调查、取件和仪式主体均为 NPC。"""

    def __init__(self, intervention=True):
        self.intervention = intervention
        self.game = AcademyGame(autonomous=True)
        self.steps = []

    def do(self, label, action, required=True):
        game = self.game
        before = len(game.runtime.events)
        started = game.timeline.now
        result = action()
        self.steps.append({
            "index": len(self.steps) + 1, "label": label,
            "started": started, "ended": game.timeline.now,
            "success": bool(result), "event_start": before,
            "event_end": len(game.runtime.events), "location": game.player.parent.name,
            "pressure": game.crisis.tag("Pressure"),
        })
        if required and not result:
            raise RuntimeError(f"模拟步骤失败：{label}；{game.log[-1] if game.log else '无日志'}")
        return result

    def travel(self, scene_name):
        game, destination = self.game, self.game.scenes[scene_name]
        while game.player.parent is not destination and not game.ended:
            route = game.world.route(game.player.parent, destination, game.player, game.env)
            if not route or len(route.nodes) < 2:
                raise RuntimeError(f"玩家无法从{game.player.parent.name}逐段抵达{scene_name}")
            next_scene = route.nodes[1]
            self.do(
                f"逐 Passage 移动：{game.player.parent.name} → {next_scene.name}",
                lambda next_scene=next_scene: game.move(game.player, next_scene),
            )
        return game.player.parent is destination

    def _coordinate(self):
        game = self.game
        self.travel("契约审议室")
        witness = game.characters["律师"]
        self.do(
            "设立规则：禁止恶魔继续扩散灰潮，并发布四院协同行动令",
            lambda: game.establish_rule(
                game.player, witness, "spread_miasma", game.demon, game.world, game.world,
            ),
        )

    def run(self):
        game = self.game
        if self.intervention:
            self._coordinate()
        while not game.ended and game.timeline.entries and game.timeline.now < 2500:
            started, before = game.timeline.now, len(game.runtime.events)
            game.wait(120)
            self.steps.append({
                "index": len(self.steps) + 1,
                "label": "观察群像自主行动" if self.intervention else "无人响应广播",
                "started": started, "ended": game.timeline.now, "success": True,
                "event_start": before, "event_end": len(game.runtime.events),
                "location": game.player.parent.name, "pressure": game.crisis.tag("Pressure"),
            })
        if self.intervention and not game.won:
            raise RuntimeError(f"协作模拟未胜利：{game.ending or '时间线耗尽'}")
        if not self.intervention and (not game.ended or game.won):
            raise RuntimeError("无人干预模拟没有进入明确失败结局")
        return self

    def event_record(self, event, index):
        actor, effect = event.get("actor"), event.get("effect")
        origin, destination = event.get("origin"), event.get("destination")
        occurred = next((facet.value for facet in event.related("OccurredAt")), None)
        return {
            "index": index, "time": event.get("time"), "name": event.name,
            "actor": actor.name if isinstance(actor, Entity) else None,
            "actor_role": actor.get("role") if isinstance(actor, Entity) else None,
            "origin": origin.name if isinstance(origin, Entity) else None,
            "destination": destination.name if isinstance(destination, Entity) else None,
            "location": occurred.name if isinstance(occurred, Entity) else None,
            "effect": effect.op if isinstance(effect, Effect) else None,
            "arguments": [_display(item) for item in effect.args] if isinstance(effect, Effect) else [],
            "outcome": _display(event.get("outcome")),
            "decision_reason": event.get("decision_reason", "未记录"),
            "decision_score": event.get("decision_score"),
            "state_changes": list(event.get("state_changes", ())),
            "contract_checked": bool(event.get("contract_checked")),
            "phase": "reaction" if event.has("Reaction") else "main",
            "witnesses": [facet.value.name for facet in event.related("ObservedBy")],
        }

    def records(self):
        return [self.event_record(event, index) for index, event in enumerate(self.game.runtime.events, 1)]

    def metrics(self, records=None):
        records = records or self.records()
        game = self.game
        effects = Counter(record["effect"] for record in records)
        npc_records = [record for record in records if record["actor_role"] not in {None, "侦探"} and record["actor"] != game.demon.name]
        routine = sum(record["effect"] in ROUTINE_OPERATIONS for record in npc_records)
        npc_moves = [record for record in npc_records if record["effect"] == "move"]
        relation = [record for record in records if record["effect"] in RELATION_OPERATIONS]
        demon = [record for record in records if record["actor"] == game.demon.name and record["effect"] in DEMON_OPERATIONS]
        mainline = [record for record in records if record["effect"] in MAINLINE_OPERATIONS]
        player_mainline = [record for record in mainline if record["actor_role"] == "侦探"]
        violations, previous_by_actor = [], {}
        for record in records:
            actor = record["actor"]
            token = (record["effect"], tuple(record["arguments"]))
            previous = previous_by_actor.get(actor)
            if previous == token and not record["state_changes"]:
                violations.append(record["index"])
            previous_by_actor[actor] = token
        component_recoverers = [item.related("RecoveredBy")[0].value for item in game.seal_components if item.related("RecoveredBy")]
        contributors = {facet.value for facet in game.active_final_rite.related("Contributor")} if game.active_final_rite else set()
        autonomous_rules = [
            record for record in records
            if record["effect"] == "establish_rule" and record["actor_role"] != "侦探"
        ]
        counter_rule_index = autonomous_rules[0]["index"] if autonomous_rules else len(records) + 1
        adaptive_demon_events = [
            record for record in records
            if record["index"] > counter_rule_index
            and record["actor"] == game.demon.name
            and "forge_false_rule" in record["decision_reason"]
            and record["effect"] != "forge_false_rule"
        ]
        relation_without_prior_analysis = []
        analyzed_actors = set()
        for record in records:
            if record["effect"] == "analyze_memory":
                analyzed_actors.add(record["actor"])
            elif record["effect"] in RELATION_OPERATIONS and record["actor"] not in analyzed_actors:
                relation_without_prior_analysis.append(record["index"])
        return {
            "events": len(records), "effects": effects,
            "npc_routine": routine, "npc_routine_ratio": routine / max(1, len(records)),
            "npc_moves": len(npc_moves), "npc_movers": len({record["actor"] for record in npc_moves}),
            "npc_memories": sum(record["effect"] == "recover_memory" for record in npc_records),
            "help_requests": effects["request_help"], "help_answers": effects["answer_help"], "assists": effects["assist"],
            "demon_events": len(demon), "demon_types": len({record["effect"] for record in demon}),
            "relation_events": len(relation),
            "bond_decisions": sum("已恢复羁绊=" in record["decision_reason"] for record in records),
            "npc_components": sum(actor is not game.player for actor in component_recoverers),
            "contributors": len(contributors), "player_mainline": len(player_mainline), "mainline": len(mainline),
            "player_mainline_ratio": len(player_mainline) / max(1, len(mainline)),
            "repeat_violations": violations, "contract_blocks": game.words.blocked_intents,
            "recovered_carriers": sum(bool(carrier.related("RecoveredBy")) for carrier in game.memory_carriers),
            "analyses": effects["analyze_memory"],
            "syntheses": effects["synthesize_history"],
            "false_rules": effects["forge_false_rule"],
            "refutations": effects["refute_rule"],
            "rule_proposals": effects["propose_rule"],
            "autonomous_rules": len(autonomous_rules),
            "word_invocations": effects["invoke_word"] + effects["refute_rule"],
            "patch_blocks": game.patch_blocks,
            "adaptive_demon_events": len(adaptive_demon_events),
            "relation_without_prior_analysis": relation_without_prior_analysis,
        }

    def assert_acceptance(self):
        if not self.intervention:
            return
        metric = self.metrics()
        checks = {
            "重复职业事件≤35%": metric["npc_routine_ratio"] <= .35,
            "至少12名NPC移动": metric["npc_movers"] >= 12,
            "NPC move≥30": metric["npc_moves"] >= 30,
            "NPC恢复记忆≥12": metric["npc_memories"] >= 12,
            "help与assist各≥4": metric["help_requests"] >= 4 and metric["assists"] >= 4,
            "恶魔有效事件≥8且≥3种": metric["demon_events"] >= 8 and metric["demon_types"] >= 3,
            "自主材料分析≥15": metric["analyses"] >= 15,
            "五段历史均形成综合": metric["syntheses"] >= 5,
            "伪规则被接受并以言灵驳回≥2": metric["false_rules"] >= 2 and metric["refutations"] >= 2,
            "至少一次依据驳伪提案并自主设立规则": metric["rule_proposals"] >= 1 and metric["autonomous_rules"] >= 1,
            "至少六次真实言灵调用": metric["word_invocations"] >= 6,
            "反伪规则实际阻止恶魔候选": metric["patch_blocks"] >= 1,
            "恶魔在规则后改用合法策略": metric["adaptive_demon_events"] >= 1,
            "关系行为必须发生在材料分析后": not metric["relation_without_prior_analysis"],
            "可见关系事件≥4": metric["relation_events"] >= 4,
            "NPC取得≥3件部件": metric["npc_components"] >= 3,
            "最终至少4名贡献者": metric["contributors"] >= 4,
            "玩家关键主线贡献≤35%": metric["player_mainline_ratio"] <= .35,
            "无无变化连续重复": not metric["repeat_violations"],
        }
        failed = [label for label, passed in checks.items() if not passed]
        if failed:
            raise RuntimeError("SIMULATION_ACCEPTANCE_FAIL: " + "；".join(failed))

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
            f"# 《异常感知学园：灰潮禁闭》{'协作成功' if self.intervention else '无人干预失败'}完整轨迹", "",
            "## 运行摘要", "", f"- 结局：{'胜利' if game.won else '失败'}",
            f"- 最终行动值：{game.timeline.now:g}", f"- 运行时事件：{len(records)}",
            f"- 灰潮压力：{game.crisis.tag('Pressure')}/{game.crisis.get('limit')}",
            f"- 已恢复独立载体：{metric['recovered_carriers']}/{len(game.memory_carriers)}",
            f"- 结局文本：{game.ending}", "", "## 量化验收", "",
            f"- NPC 职业事件：{metric['npc_routine']}/{len(records)}（{metric['npc_routine_ratio']:.1%}）",
            f"- NPC 移动：{metric['npc_moves']} 次，涉及 {metric['npc_movers']} 名 NPC",
            f"- NPC 恢复记忆：{metric['npc_memories']} 次",
            f"- 材料分析／历史综合：{metric['analyses']} / {metric['syntheses']} 次",
            f"- 伪规则发布／言灵驳回：{metric['false_rules']} / {metric['refutations']} 次",
            f"- 规则提案／NPC 自主设立：{metric['rule_proposals']} / {metric['autonomous_rules']} 次；设立后阻止伪规则候选 {metric['patch_blocks']} 次，迫使恶魔改招 {metric['adaptive_demon_events']} 次",
            f"- 真实言灵调用：{metric['word_invocations']} 次（门槛正名、镇心或证词驳伪）",
            f"- 求助／远程回应／到场协助：{metric['help_requests']} / {metric['help_answers']} / {metric['assists']}",
            f"- 恶魔有效事件：{metric['demon_events']} 次，{metric['demon_types']} 种；契约或规则阻止检查累计 {metric['contract_blocks']} 次",
            f"- 可见关系事件：{metric['relation_events']} 次；决策原因明确引用恢复羁绊 {metric['bond_decisions']} 次",
            f"- NPC 取得部件：{metric['npc_components']} 件",
            f"- 最终不同贡献者：{metric['contributors']} 名",
            f"- 玩家关键主线贡献：{metric['player_mainline']}/{metric['mainline']}（{metric['player_mainline_ratio']:.1%}）",
            f"- 无状态差分连续重复违规：{len(metric['repeat_violations'])} 次", "", "## 玩家操作阶段", "",
        ]
        for step in self.steps:
            lines.extend((
                f"### {step['index']}. {step['label']}", "",
                f"- 行动值：{step['started']:g} → {step['ended']:g}",
                f"- 本阶段事件：{step['event_end'] - step['event_start']}",
                f"- 玩家位置：{step['location']}", f"- 灰潮压力：{step['pressure']}", "",
            ))
        lines.extend(("## 完整 Event 序列", ""))
        for record in records:
            lines.extend((
                f"### E{record['index']:04d} · AV {record['time']:g} · {record['name']}", "",
                f"- 行动者：{record['actor'] or '无'}",
                f"- 行程：{record['origin'] or '无'} → {record['destination'] or '无'}",
                f"- 效果：`{record['effect']}`（{'；'.join(record['arguments']) or '无参数'}）",
                f"- 决策原因：{record['decision_reason']}",
                f"- 决策分：{record['decision_score'] if record['decision_score'] is not None else '玩家提交'}",
                f"- 状态后果：{'；'.join(record['state_changes']) or '无状态差分'}",
                f"- 契约复核：{'是' if record['contract_checked'] else '否'}",
                f"- 见证 Soul：{'、'.join(record['witnesses']) or '无'}", "",
            ))
        lines.extend(("## 原始因果日志", ""))
        lines.extend(f"{index}. {entry}" for index, entry in enumerate(game.log, 1))
        lines.extend(("", f"逐事件 JSONL：`{jsonl_path.name}`", ""))
        markdown_path.write_text("\n".join(lines), encoding="utf-8")
        return markdown_path, jsonl_path


def run_simulation(markdown_path):
    success = AcademySimulation(intervention=True).run()
    success.assert_acceptance()
    success_paths = success.export(markdown_path)
    source = Path(markdown_path)
    failure_path = source.with_name(f"{source.stem}-failure{source.suffix}")
    failure = AcademySimulation(intervention=False).run()
    failure.export(failure_path)
    success.failure_simulation = failure
    success.failure_paths = (failure_path.resolve(), failure_path.with_suffix(".jsonl").resolve())
    return success, success_paths


__all__ = ["AcademySimulation", "run_simulation"]
