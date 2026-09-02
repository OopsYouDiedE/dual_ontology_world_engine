"""有限社交评价、目标情绪组合与态度压力。

一次互动只选择一个组合；不同事件产生的 SocialImpact 可以并存。组合只形成
临时态度压力，不替代 Knowledge、Disposition、Rule 或任务效用。
"""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any

from .entity_system import Entity, Influence
from .soul import ResponseMode, Soul

SOCIAL_INFLUENCE_COMBINATIONS = {
    "NO_STRONG_CHANGE": (),
    "AFFILIATION_GAIN": (("Joy", 2, 2), ("Hope", 1, 2)),
    "SAFE_ACCEPTANCE": (("Serenity", 2, 2), ("Hope", 1, 2), ("Joy", 1, 1)),
    "HURT_WITHDRAWAL": (("Sorrow", 2, 2), ("Anger", 1, 1)),
    "DEFENSIVE_ANGER": (("Anger", 2, 1), ("Fear", 1, 1)),
    "MORAL_REJECTION": (("Anger", 2, 2), ("Hatred", 1, 2), ("Sorrow", 1, 2)),
    "GRIEF_BOND": (("Sorrow", 3, 3), ("Devotion", 2, 3)),
    "REMORSE_REPAIR": (("Remorse", 3, 2), ("Sorrow", 1, 2), ("Hope", 1, 1)),
    "CAUTIOUS_REOPENING": (("Hope", 1, 2), ("Serenity", 1, 1), ("Sorrow", 1, 2)),
    "SHARED_RESOLVE": (("Hope", 2, 2), ("Devotion", 1, 2), ("Serenity", 1, 1)),
    "THREATENED_DISTRUST": (("Fear", 2, 2), ("Anger", 1, 2), ("Hatred", 1, 2)),
}

SOCIAL_APPRAISAL_INPUTS = frozenset(
    {
        "bond",
        "irreversible_loss",
        "corrected_self_blame",
        "malicious_intent",
        "threat",
        "severe_harm_attributed_to_target",
        "reciprocity_breach",
        "truth_conflict",
        "costly_repair",
        "vulnerability_accepted",
        "shared_objective_advanced",
        "support_received",
        "norm_breach",
        "credible_but_unverified_offer",
    }
)

# 每项 SocialImpact 对七种通用反应的临时偏置。未列方向不受该影响。
SOCIAL_ATTITUDE_PRESSURES = {
    "NO_STRONG_CHANGE": {},
    "AFFILIATION_GAIN": {ResponseMode.COOPERATE: 2, ResponseMode.NEGOTIATE: 1},
    "SAFE_ACCEPTANCE": {
        ResponseMode.COOPERATE: 2,
        ResponseMode.NEGOTIATE: 2,
        ResponseMode.CONCEAL: -1,
        ResponseMode.AVOID: -1,
    },
    "HURT_WITHDRAWAL": {
        ResponseMode.AVOID: 3,
        ResponseMode.RESTRAIN: 1,
        ResponseMode.COOPERATE: -2,
    },
    "DEFENSIVE_ANGER": {
        ResponseMode.RESTRAIN: 3,
        ResponseMode.PROTECT: 1,
        ResponseMode.AVOID: 1,
    },
    "MORAL_REJECTION": {
        ResponseMode.RESTRAIN: 4,
        ResponseMode.AVOID: 2,
        ResponseMode.COOPERATE: -3,
    },
    "GRIEF_BOND": {ResponseMode.PROTECT: 2, ResponseMode.AVOID: 1},
    "REMORSE_REPAIR": {
        ResponseMode.NEGOTIATE: 3,
        ResponseMode.COOPERATE: 2,
        ResponseMode.CONCEAL: -2,
    },
    "CAUTIOUS_REOPENING": {
        ResponseMode.VERIFY: 2,
        ResponseMode.NEGOTIATE: 1,
        ResponseMode.COOPERATE: 1,
    },
    "SHARED_RESOLVE": {ResponseMode.COOPERATE: 4, ResponseMode.PROTECT: 1},
    "THREATENED_DISTRUST": {
        ResponseMode.AVOID: 3,
        ResponseMode.VERIFY: 2,
        ResponseMode.RESTRAIN: 2,
        ResponseMode.CONCEAL: 1,
        ResponseMode.COOPERATE: -3,
    },
}


def select_social_influence(appraisal: Mapping[str, Any]) -> str:
    """从固定目录选择主导组合；拒绝内容层未登记的捷径字段。"""
    unknown = set(appraisal) - SOCIAL_APPRAISAL_INPUTS
    if unknown:
        raise ValueError(f"未登记的互动评价输入：{sorted(unknown)}")
    bond, threat = int(appraisal.get("bond", 0)), int(appraisal.get("threat", 0))
    if appraisal.get("corrected_self_blame", False):
        return "REMORSE_REPAIR"
    if appraisal.get("irreversible_loss", False) and bond >= 1:
        return "GRIEF_BOND"
    if appraisal.get("malicious_intent", False) and threat >= 2:
        return "THREATENED_DISTRUST"
    if appraisal.get("costly_repair", False):
        return "CAUTIOUS_REOPENING"
    if appraisal.get("credible_but_unverified_offer", False):
        return "CAUTIOUS_REOPENING"
    if appraisal.get("severe_harm_attributed_to_target", False):
        return "MORAL_REJECTION"
    if (
        appraisal.get("reciprocity_breach", False)
        or appraisal.get("truth_conflict", False)
    ) and bond >= 1:
        return "HURT_WITHDRAWAL"
    if appraisal.get("vulnerability_accepted", False):
        return "SAFE_ACCEPTANCE"
    if appraisal.get("shared_objective_advanced", False):
        return "SHARED_RESOLVE"
    if appraisal.get("support_received", False):
        return "AFFILIATION_GAIN"
    if appraisal.get("norm_breach", False):
        return "DEFENSIVE_ANGER"
    return "NO_STRONG_CHANGE"


def SocialImpact(
    name: str,
    parent: Soul,
    target: Entity,
    source: Entity,
    combination: str,
    started_at: int | float,
    duration_unit: int | float = 200,
) -> Entity:
    """建立一次目标化社交影响，并实体化组合内的全部 Influence。"""
    specs = SOCIAL_INFLUENCE_COMBINATIONS[combination]
    duration = max((item[2] for item in specs), default=0) * duration_unit
    impact = Entity(
        name,
        parent,
        {"SocialImpact": 1, "InfluenceBundle": 1},
        properties={
            "combination": combination,
            "started_at": started_at,
            "duration": duration,
            "expires_at": started_at + duration,
        },
    )
    impact.relate("Target", target)
    impact.relate("Source", source)
    for emotion, intensity, persistence in specs:
        Influence(
            f"{impact.name}·{emotion}",
            emotion,
            intensity,
            impact,
            target=target,
            source=source,
            mode="relational",
            persistence=persistence,
            started_at=started_at,
            expires_at=started_at + persistence * duration_unit,
        )
    return impact


def social_attitude_pressure(combination: str, mode: ResponseMode | str | None) -> int:
    """返回完整组合对一个反应方向的偏置。"""
    if isinstance(mode, str):
        try:
            mode = ResponseMode(mode)
        except ValueError:
            return 0
    return SOCIAL_ATTITUDE_PRESSURES[combination].get(mode, 0)


__all__ = [
    "SOCIAL_INFLUENCE_COMBINATIONS",
    "SOCIAL_APPRAISAL_INPUTS",
    "SOCIAL_ATTITUDE_PRESSURES",
    "select_social_influence",
    "SocialImpact",
    "social_attitude_pressure",
]
