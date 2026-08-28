from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from fractions import Fraction

from ..foundation.codec import content_id


@dataclass(frozen=True, slots=True)
class HysteresisPolicy:
    switch_margin: Fraction
    minimum_residence_epochs: int
    maximum_replans: int

    def __post_init__(self) -> None:
        if self.switch_margin < 0 or min(self.minimum_residence_epochs, self.maximum_replans) < 0: raise ValueError("invalid hysteresis policy")


def may_switch(*, improvement: Fraction, residence_epochs: int, replan_attempt: int, policy: HysteresisPolicy, dependency_changed: bool) -> bool:
    return dependency_changed and replan_attempt < policy.maximum_replans and residence_epochs >= policy.minimum_residence_epochs and improvement > policy.switch_margin


def may_switch_vectors(
    *,
    incumbent: dict[str, Fraction],
    candidate: dict[str, Fraction],
    dimensions: tuple[str, ...],
    residence_epochs: int,
    replan_attempt: int,
    policy: HysteresisPolicy,
    dependency_changed: bool,
) -> bool:
    """稳健支配式切换；不把不可通约维度相加成一个总分。"""

    if not dependency_changed or replan_attempt >= policy.maximum_replans:
        return False
    if residence_epochs < policy.minimum_residence_epochs or not dimensions:
        return False
    pairs = tuple(
        (candidate.get(key, Fraction()), incumbent.get(key, Fraction()))
        for key in dimensions
    )
    return all(new <= old + policy.switch_margin for new, old in pairs) and any(
        old - new > policy.switch_margin for new, old in pairs
    )


class ProgressWitnessKind(str, Enum):
    RANK_DESCENT = "rank_descent"
    POTENTIAL_DESCENT = "potential_descent"
    RESOURCE_CONSUMPTION = "resource_consumption"
    TIME_ADVANCE = "time_advance"


@dataclass(frozen=True, slots=True)
class AutonomousTransition:
    transition_id: str
    source_state: str
    target_state: str
    rank_before: int | None = None
    rank_after: int | None = None
    potential_before: Fraction | None = None
    potential_after: Fraction | None = None
    resource_consumed: Fraction = Fraction()
    time_advanced: Fraction = Fraction()

    def __post_init__(self) -> None:
        if not self.transition_id or not self.source_state or not self.target_state:raise ValueError("autonomous transition requires identity and states")
        if self.resource_consumed<0 or self.time_advanced<0:raise ValueError("transition progress cannot be negative")
        if (self.rank_before is None)!=(self.rank_after is None):raise ValueError("rank witness requires both endpoints")
        if (self.potential_before is None)!=(self.potential_after is None):raise ValueError("potential witness requires both endpoints")


@dataclass(frozen=True, slots=True)
class TransitionTerminationCertificate:
    certificate_id: str
    transition_ids: tuple[str,...]
    witnesses: tuple[tuple[str,ProgressWitnessKind],...]
    minimum_resource_consumption: Fraction
    minimum_time_advance: Fraction


def certify_autonomous_transitions(transitions:tuple[AutonomousTransition,...],*,minimum_resource_consumption:Fraction=Fraction(1),minimum_time_advance:Fraction=Fraction(1),max_transitions:int=256)->TransitionTerminationCertificate:
    """证明每个零人工干预后继都严格推进某个良基量。"""
    if minimum_resource_consumption<=0 or minimum_time_advance<=0 or max_transitions<0:raise ValueError("invalid autonomous transition bounds")
    if len(transitions)>max_transitions:raise ValueError("autonomous transition budget exceeded")
    if len({item.transition_id for item in transitions})!=len(transitions):raise ValueError("duplicate autonomous transition")
    witnesses=[]
    for item in transitions:
        kinds=[]
        if item.rank_before is not None and item.rank_after is not None and 0<=item.rank_after<item.rank_before:kinds.append(ProgressWitnessKind.RANK_DESCENT)
        if item.potential_before is not None and item.potential_after is not None and 0<=item.potential_after<item.potential_before:kinds.append(ProgressWitnessKind.POTENTIAL_DESCENT)
        if item.resource_consumed>=minimum_resource_consumption:kinds.append(ProgressWitnessKind.RESOURCE_CONSUMPTION)
        if item.time_advanced>=minimum_time_advance:kinds.append(ProgressWitnessKind.TIME_ADVANCE)
        if not kinds:raise ValueError(f"autonomous transition lacks strict progress:{item.transition_id}")
        witnesses.append((item.transition_id,kinds[0]))
    values=(tuple(item.transition_id for item in transitions),tuple(witnesses),minimum_resource_consumption,minimum_time_advance)
    return TransitionTerminationCertificate(content_id(values),*values)
