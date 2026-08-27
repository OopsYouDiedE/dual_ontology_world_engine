from __future__ import annotations

from dataclasses import dataclass
from fractions import Fraction


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
