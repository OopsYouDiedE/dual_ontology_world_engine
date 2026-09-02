"""学园社交与情绪影响系统 (Social System)。

工作代码：负责情绪评价映射、SocialImpact 实体生成与管理、事件目击者收集以及社交羁绊计算。
"""

from __future__ import annotations

from core.python.engine import (
    Character,
    Entity,
    SocialImpact,
    select_social_influence,
    social_attitude_pressure,
)


def bond_strength(game, actor, target):
    """计算 actor 对 target 的有效历史羁绊强度。

    只有当 actor 已恢复自身对应的故事记忆载体后，旧有羁绊才显化为当前强度。
    """
    if not isinstance(target, Entity):
        return 0
    carrier = game.story_carrier(actor)
    if not carrier or not game.carrier_done(actor, carrier):
        return 0
    return max(
        (
            facet.strength
            for facet in actor.related("PastBond")
            if facet.value is target
        ),
        default=0,
    )


def social_target(intent):
    """从 Intent 意图中提取涉及的社交目标 Character/Entity。"""
    op = intent.effect.op
    if op in {"share_memory", "clarify_memory", "confront", "protect"}:
        return intent.effect.args[1]
    if op in {"answer_help", "assist"}:
        request = intent.effect.args[1]
        return request.get("requester") if isinstance(request, Entity) else None
    return None


def impact_target(impact):
    """获取 SocialImpact 实体的目标对象。"""
    relation = impact.related("Target")
    return relation[0].value if relation else None


def active_social_impacts(game, soul, target=None):
    """获取某灵魂当前未过期的全部活跃社交态度偏置。"""
    return tuple(
        impact
        for impact in game.social_impacts
        if impact.parent is soul
        and (target is None or impact_target(impact) is target)
        and game.timeline.now < impact.get("expires_at", game.timeline.now)
    )


def add_social_impact(game, receiver, target, event, appraisal):
    """根据对事件的评价 (appraisal)，向 receiver 的 soul 添加对 target 的 SocialImpact。"""
    if not isinstance(receiver, Character) or not isinstance(target, Character):
        return None

    combination = select_social_influence(appraisal)
    if combination == "NO_STRONG_CHANGE":
        return None

    soul = receiver.souls[0]
    existing = next(
        (
            impact
            for impact in active_social_impacts(game, soul, target)
            if impact.get("combination") == combination
        ),
        None,
    )
    if existing is not None:
        # 延长已存在情绪的有效期并建立强化关联
        existing.set(
            "expires_at",
            max(
                existing.get("expires_at", game.timeline.now),
                game.timeline.now + existing.get("duration", 200),
            ),
        )
        existing.relate("ReinforcedBy", event)
        event.relate("CreatedImpact", existing)
        return existing

    impact = SocialImpact(
        f"{receiver.name}对{target.name}：{combination}",
        soul,
        target,
        event,
        combination,
        game.timeline.now,
    )

    def attitude(_soul, intent, _env, _context, impact=impact, target=target):
        if game.timeline.now >= impact.get("expires_at", game.timeline.now):
            return 0
        if social_target(intent) is not target:
            return 0
        return social_attitude_pressure(
            impact.get("combination"),
            intent.response_mode,
        )

    soul.install_preference(attitude, source=impact)
    game.social_impacts.append(impact)
    event.relate("CreatedImpact", impact)
    return impact


def observe_social_outcome(game, event, _context):
    """监听行动结算事件，评估并生成人际互动的情绪影响。"""
    if not game.social_emotions:
        return
    effect, actor = event.get("effect"), event.get("actor")
    if effect is None or not isinstance(actor, Character):
        return

    operation = effect.op
    receiver = target = None
    appraisal = None

    if operation in {"share_memory", "clarify_memory", "protect"}:
        receiver, target = effect.args[1], actor
        appraisal = {
            "bond": bond_strength(game, receiver, actor),
            "support_received": True,
        }
        if operation == "clarify_memory":
            appraisal["vulnerability_accepted"] = True
    elif operation == "confront":
        receiver, target = effect.args[1], actor
        bond = bond_strength(game, receiver, actor)
        appraisal = {
            "bond": bond,
            "truth_conflict": bond >= 1,
            "norm_breach": bond < 1,
        }
    elif operation in {"answer_help", "assist"}:
        request = effect.args[1]
        receiver, target = request.get("requester"), actor
        appraisal = {
            "bond": bond_strength(game, receiver, actor),
            "support_received": True,
            "shared_objective_advanced": operation == "assist",
        }
    elif operation == "whisper_doubt":
        receiver, target = effect.args[1], actor
        appraisal = {
            "bond": 0,
            "malicious_intent": True,
            "threat": 2,
            "norm_breach": True,
        }
    elif operation == "forge_false_rule":
        publication = event.get("outcome")
        targets = (
            publication.related("Targets")
            if isinstance(publication, Entity)
            else ()
        )
        receiver = targets[0].value if targets else None
        target = actor
        appraisal = {
            "bond": 0,
            "malicious_intent": True,
            "threat": 2,
            "norm_breach": True,
        }

    if appraisal is None:
        return

    impact = add_social_impact(game, receiver, target, event, appraisal)
    if impact is not None:
        event.set("social_impact", impact.get("combination"))
        event.set("social_receiver", receiver)
        event.set("social_target", target)
        game.log.append(
            f"{event.name}使{receiver.name}对{target.name}产生"
            f"{impact.get('combination')}，它只形成临时态度偏置。"
        )


def witnesses_collector(game, event, _context):
    """收集事件的物理目击者灵魂 Soul，并记录事件元数据。"""
    actor = event.get("actor")
    meta = game._pending_event_meta.pop(actor, {})
    origin, destination = meta.get("origin"), meta.get("destination")
    if isinstance(origin, Entity):
        event.relate("OccurredAt", origin)
        event.set("origin", origin)
    if isinstance(destination, Entity):
        event.set("destination", destination)
        if destination is not origin:
            event.relate("ArrivedAt", destination)
    event.set("decision_reason", meta.get("reason", "未记录"))
    event.set("decision_score", meta.get("score"))
    event.set("state_changes", meta.get("changes", ()))
    event.set("contract_checked", meta.get("contract_checked", False))

    scenes = {
        item
        for item in (origin, destination)
        if isinstance(item, Entity) and item.has("Scene")
    }
    return tuple(
        soul
        for role, soul in game.souls.items()
        if game.characters[role].parent in scenes
    )
