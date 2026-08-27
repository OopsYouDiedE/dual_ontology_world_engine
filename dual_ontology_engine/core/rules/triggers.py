from __future__ import annotations

from collections import deque
from dataclasses import dataclass

from ..foundation.budgets import BudgetExceeded, BudgetMeter
from ..foundation.codec import content_id
from ..foundation.failures import FailureCertificate, FailureKind


@dataclass(frozen=True, slots=True)
class Trigger:
    rule_id: str
    target_id: str
    input_version: str
    event_lineage: str
    phase: str
    requires: tuple[str, ...]
    output_keys: tuple[str, ...]

    @property
    def reaction_key(self) -> tuple[str, ...]:
        return (
            self.rule_id,
            self.target_id,
            self.input_version,
            self.event_lineage,
            self.phase,
        )

    @property
    def content_id(self) -> str:
        return content_id((self.reaction_key, self.requires, self.output_keys))

    def __post_init__(self) -> None:
        if len(set(self.requires)) != len(self.requires):
            raise ValueError("duplicate trigger requirement")
        if len(set(self.output_keys)) != len(self.output_keys):
            raise ValueError("duplicate trigger output")


@dataclass(frozen=True, slots=True)
class ClosureResult:
    values: frozenset[str]
    meter: BudgetMeter
    failure: FailureCertificate | None
    fired_reaction_keys: tuple[tuple[str, ...], ...] = ()


def monotone_closure(
    initial: frozenset[str],
    triggers: tuple[Trigger, ...],
    domain: frozenset[str],
    meter: BudgetMeter,
) -> ClosureResult:
    """在冻结有限域上执行确定性的半朴素单调闭包。"""

    if not initial.issubset(domain):
        raise ValueError("initial closure values escaped frozen domain")
    by_key: dict[tuple[str, ...], Trigger] = {}
    for trigger in triggers:
        old = by_key.get(trigger.reaction_key)
        if old is not None and old.content_id != trigger.content_id:
            raise ValueError("same reaction key has conflicting content")
        by_key[trigger.reaction_key] = trigger
        if not set(trigger.requires + trigger.output_keys).issubset(domain):
            raise ValueError("trigger escaped frozen domain")

    ordered = tuple(sorted(by_key.values(), key=lambda item: item.reaction_key))
    consumers: dict[str, list[int]] = {}
    missing: list[int] = []
    current = set(initial)
    for index, trigger in enumerate(ordered):
        missing.append(sum(requirement not in current for requirement in trigger.requires))
        for requirement in trigger.requires:
            consumers.setdefault(requirement, []).append(index)

    ready = deque(index for index, count in enumerate(missing) if count == 0)
    queued = set(ready)
    fired: set[int] = set()
    fired_keys: list[tuple[str, ...]] = []
    try:
        while ready:
            index = ready.popleft()
            queued.discard(index)
            if index in fired or missing[index] != 0:
                continue
            meter = meter.charge(instructions=1, nodes=1)
            fired.add(index)
            trigger = ordered[index]
            fired_keys.append(trigger.reaction_key)
            for value in trigger.output_keys:
                if value in current:
                    continue
                current.add(value)
                for consumer in consumers.get(value, ()):
                    if consumer in fired:
                        continue
                    missing[consumer] -= 1
                    if missing[consumer] == 0 and consumer not in queued:
                        ready.append(consumer)
                        queued.add(consumer)
    except BudgetExceeded:
        failure = FailureCertificate(
            FailureKind.BUDGET_EXHAUSTED,
            "trigger-budget",
            next(iter((trigger.input_version for trigger in ordered)), "unknown"),
            dependency_refs=tuple(
                ":".join(trigger.reaction_key) for trigger in ordered
            ),
            witness_refs=tuple(sorted(current - set(initial))),
        )
        return ClosureResult(initial, meter, failure, tuple(fired_keys))
    return ClosureResult(frozenset(current), meter, None, tuple(fired_keys))
