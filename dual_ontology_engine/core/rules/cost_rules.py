from __future__ import annotations

from dataclasses import dataclass

from ..costs.modifiers import CostModifier


@dataclass(frozen=True, slots=True)
class CostRule:
    """规则影响动作路径的唯一正式出口。"""

    rule_id: str
    issuer_ref: str
    authority_domains: frozenset[str]
    modifier: CostModifier
    condition_fact_ids: tuple[str, ...]
    active: bool = True

    def __post_init__(self) -> None:
        if self.modifier.authority_domain not in self.authority_domains:
            raise ValueError("rule lacks authority to modify this cost dimension")
        if self.modifier.provenance_id == self.rule_id:
            raise ValueError("modifier provenance must be a proof node, not a self-reference")


def compile_cost_rules(rules: tuple[CostRule, ...], true_fact_ids: frozenset[str]) -> tuple[CostModifier, ...]:
    return tuple(
        rule.modifier
        for rule in sorted(rules, key=lambda item: item.rule_id)
        if rule.active and set(rule.condition_fact_ids).issubset(true_fact_ids)
    )

