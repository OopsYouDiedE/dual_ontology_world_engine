"""核心情境事件与加权选择调度系统 (Incident & Dispatcher System)。

本模块实现通用的“对事不对人（条件谓词驱动）”情境事件系统：
1. IncidentTemplate：声明式事件模版，槽位由实体状态与关系谓词动态匹配；
2. IncidentOption：行为响应分支，包含 ResponseMode、效果结算与参数化台词模版；
3. IncidentDispatcher：负责场景候选扫描、转化为角色决策意图、Soul 效用打分、
   加权概率决选以及统一状态差分记录。
"""

from __future__ import annotations

import math
import random
from dataclasses import dataclass, field
from typing import Any, Callable, Mapping, Sequence

from .entity_system import Entity, F
from .soul import ActionRuntime, Context, Decision, Intent, ResponseMode, RuleEnvironment, RuleSystem, Soul


@dataclass(frozen=True, slots=True)
class IncidentOption:
    """事件的具体行为分支定义。"""

    operation: str
    label: str
    resolve: Callable[[Mapping[str, Any]], Any]
    text_template: str = ""
    response_mode: ResponseMode | None = None
    cost: int = 20
    base_score: int | Callable[[Mapping[str, Any]], int] = 60
    reason: str = ""


@dataclass
class IncidentTemplate:
    """声明式情境事件模板。对事不对人，槽位由条件谓词匹配。"""

    name: str
    slots: Mapping[str, Callable[[Entity, Mapping[str, Any]], bool]]
    options: Sequence[IncidentOption]
    requirements: Callable[[RuleEnvironment, Mapping[str, Any]], bool] = lambda env, scope: True
    description: str = ""
    tags: dict[str, int] = field(default_factory=dict)


class IncidentDispatcher:
    """通用情境事件分发器与加权决选器。"""

    def __init__(
        self,
        runtime: ActionRuntime,
        rules: RuleSystem,
        env: RuleEnvironment,
        templates: Sequence[IncidentTemplate] | None = None,
    ):
        self.runtime = runtime
        self.rules = rules
        self.env = env
        self.templates: list[IncidentTemplate] = list(templates or [])

    def register(self, template: IncidentTemplate) -> None:
        """注册情境事件模板。"""
        self.templates.append(template)

    def match_scope(
        self,
        template: IncidentTemplate,
        candidates: Sequence[Entity],
        fixed_scope: Mapping[str, Any] | None = None,
    ) -> list[dict[str, Any]]:
        """在候选实体列表中递归匹配满足模板全部槽位条件的实体组合。"""
        slot_names = list(template.slots.keys())
        results: list[dict[str, Any]] = []

        def search(slot_idx: int, current_scope: dict[str, Any]):
            if slot_idx >= len(slot_names):
                if template.requirements(self.env, current_scope):
                    results.append(dict(current_scope))
                return

            name = slot_names[slot_idx]
            predicate = template.slots[name]

            if name in current_scope:
                if predicate(current_scope[name], current_scope):
                    search(slot_idx + 1, current_scope)
                return

            for entity in candidates:
                if predicate(entity, current_scope):
                    current_scope[name] = entity
                    search(slot_idx + 1, current_scope)
                    del current_scope[name]

        initial = dict(fixed_scope or {})
        search(0, initial)
        return results

    def build_intents(
        self,
        template: IncidentTemplate,
        scope: Mapping[str, Any],
        actor_slot: str = "actor",
        target_slot: str = "target",
    ) -> list[tuple[Intent, int, str, IncidentOption]]:
        """将匹配成功的事件及选项转化为可供角色灵魂评估的候选 Intent。"""
        actor = scope.get(actor_slot)
        if not isinstance(actor, Entity) or not actor.souls:
            return []

        target = scope.get(target_slot, actor)
        soul = actor.souls[0]
        results = []

        for opt in template.options:
            score_val = opt.base_score(scope) if callable(opt.base_score) else opt.base_score

            def make_resolver(o=opt, sc=scope):
                return lambda: o.resolve(sc)

            label = self.format_text(opt.label or opt.operation, scope)
            reason = self.format_text(opt.reason or template.description, scope)

            intent = Intent(
                actor=actor,
                mind=soul,
                effect=F(opt.operation, actor, target),
                motion_cost=opt.cost,
                resolve=make_resolver(),
                label=label,
                response_mode=opt.response_mode,
            )
            results.append((intent, score_val, reason, opt))

        return results

    def assess_and_choose(
        self,
        candidates: Sequence[tuple[Intent, int, str, IncidentOption]],
        scene: Entity,
        world: Entity,
        context: Context | None = None,
        weighted: bool = False,
        temperature: float = 10.0,
    ) -> tuple[Intent, int, str, IncidentOption, list[dict[str, Any]]]:
        """对候选事件选项进行灵魂偏好打分，并依据效用进行加权概率或最高分选择。"""
        domains = (world, scene)
        ctx = context or {"domains": domains}
        assessed = []
        blocked = []

        for intent, base_score, reason, opt in candidates:
            allowed, review = self.runtime.review(intent, ctx)
            decision = intent.mind.assess(
                (intent,),
                self.rules,
                self.env,
                lambda _intent, base=base_score: base,
                context={"domains": domains, "source": intent.actor},
            )[0]

            social = tuple(
                (amount, source.get("combination"))
                for amount, source in decision.preferences
                if isinstance(source, Entity) and source.has("SocialImpact")
            )
            record = {
                "actor": intent.actor.name,
                "operation": intent.effect.op,
                "label": intent.label,
                "score": decision.score,
                "reason": reason,
                "rule": decision.reason,
                "social_pressure": social,
            }
            if allowed:
                assessed.append((decision.score, intent, reason, opt, record))
            else:
                blocked.append(record)

        if not assessed:
            raise RuntimeError(f"场景 {scene.name} 中所有候选事件均被规则阻止")

        # 加权概率选择（分越高概率越高）或确定性选最高分
        if weighted and len(assessed) > 1:
            scores = [item[0] for item in assessed]
            max_s = max(scores)
            weights = [math.exp((s - max_s) / max(temperature, 1e-5)) for s in scores]
            chosen_tuple = random.choices(assessed, weights=weights, k=1)[0]
        else:
            assessed.sort(key=lambda item: item[0], reverse=True)
            chosen_tuple = assessed[0]

        score, chosen_intent, chosen_reason, chosen_opt, _ = chosen_tuple
        return chosen_intent, score, chosen_reason, chosen_opt, blocked

    @staticmethod
    def format_text(template_str: str, scope: Mapping[str, Any]) -> str:
        """根据 scope 字典内的实体引用动态填充文本占位符。"""
        if not template_str:
            return ""
        # 构造属性访问字典
        mapping = {}
        for key, val in scope.items():
            mapping[key] = val
            if isinstance(val, Entity):
                mapping[f"{key}.name"] = val.name
                mapping[f"{key}_name"] = val.name
        try:
            return template_str.format_map(mapping)
        except (KeyError, ValueError):
            # 兼容自定义属性解析
            res = template_str
            for k, v in scope.items():
                name = getattr(v, "name", str(v))
                res = res.replace(f"{{{k}.name}}", name).replace(f"{{{k}}}", name)
            return res
